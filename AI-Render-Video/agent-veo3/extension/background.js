// Flow Kit — Chrome Extension Background Service Worker
const AGENT_WS_URL = 'ws://127.0.0.1:9222';
const API_KEY = 'AIzaSyBtrm0o5ab1c-Ec8ZuLcGt3oJAA5VWt3pY';

let ws = null;
let flowKey = null;
let callbackSecret = null;
let currentAccount = { email: null, userId: null };
let state = 'off';
let manualDisconnect = false;
let metrics = {
  tokenCapturedAt: null,
  requestCount: 0,
  successCount: 0,
  failedCount: 0,
  lastError: null,
};

// ─── URL → Log Type Classifier ─────────────────────────────

// Visible log types — only these appear in the request log
const _VISIBLE_TYPES = new Set(['GEN_IMG', 'GEN_VID', 'GEN_VID_REF', 'UPSCALE', 'TRACKING', 'URL_REFRESH']);

function _classifyApiUrl(url) {
  if (url.includes('uploadImage'))                     return 'UPLOAD';
  if (url.includes('batchGenerateImages'))              return 'GEN_IMG';
  if (url.includes('UpsampleVideo'))                   return 'UPSCALE';
  if (url.includes('ReferenceImages'))                 return 'GEN_VID_REF';
  if (url.includes('batchAsyncGenerateVideo'))          return 'GEN_VID';
  if (url.includes('batchCheckAsync'))                  return 'POLL';
  if (url.includes('upsampleImage'))                   return 'UPS_IMG';
  if (url.includes('/media/'))                         return 'MEDIA';
  if (url.includes('/credits'))                        return 'CREDITS';
  return 'API';
}

// ─── Request Log ────────────────────────────────────────────

let requestLog = [];

function addRequestLog(entry) {
  requestLog.unshift(entry);
  if (requestLog.length > 100) requestLog.pop();
  broadcastRequestLog();
}

function updateRequestLog(id, updates) {
  const entry = requestLog.find((e) => e.id === id);
  if (entry) Object.assign(entry, updates);
  broadcastRequestLog();
}

function broadcastRequestLog() {
  chrome.runtime.sendMessage({ type: 'REQUEST_LOG_UPDATE', log: requestLog }).catch(() => {});
}

let initializationPromise = null;

chrome.runtime.onInstalled.addListener(() => {
  void ensureInitialized();
});
chrome.runtime.onStartup.addListener(() => {
  void ensureInitialized();
});
chrome.alarms.onAlarm.addListener(async (alarm) => {
  await ensureInitialized();
  if (alarm.name === 'reconnect') connectToAgent();
  if (alarm.name === 'keepAlive') keepAlive();
  if (alarm.name === 'token-refresh') {
    await captureTokenFromFlowTab();
  }
});

function ensureInitialized() {
  if (!initializationPromise) {
    initializationPromise = initialize().catch((error) => {
      initializationPromise = null;
      console.error('[FlowAgent] Initialization failed', error);
      throw error;
    });
  }
  return initializationPromise;
}

async function initialize() {
  const data = await chrome.storage.local.get(['flowKey', 'metrics', 'callbackSecret', 'currentAccount']);
  if (data.flowKey) flowKey = data.flowKey;
  if (data.metrics) Object.assign(metrics, data.metrics);
  if (data.callbackSecret) callbackSecret = data.callbackSecret;
  if (data.currentAccount) currentAccount = data.currentAccount;
  connectToAgent();
  chrome.alarms.create('keepAlive', { periodInMinutes: 0.4 });
}

// MV3 workers can be suspended and restarted without onStartup firing.
// Rehydrate the persisted Flow key on every worker start.
void ensureInitialized();

// ─── Token Capture ──────────────────────────────────────────

async function getAccountIdentity(token, tabId = null) {
  // 1. Try Google tokeninfo endpoint (standard, no extra permissions, returns email & user_id)
  if (token) {
    try {
      const res = await fetch(`https://www.googleapis.com/oauth2/v1/tokeninfo?access_token=${encodeURIComponent(token)}`);
      if (res.ok) {
        const info = await res.json();
        if (info.email || info.user_id) {
          return { email: info.email || null, userId: info.user_id || null };
        }
      }
    } catch (e) {}

    try {
      const res = await fetch('https://www.googleapis.com/oauth2/v3/userinfo', {
        headers: { authorization: `Bearer ${token}` },
      });
      if (res.ok) {
        const info = await res.json();
        if (info.email || info.sub) {
          return { email: info.email || null, userId: info.sub || null };
        }
      }
    } catch (e) {}
  }

  // 2. Fallback: inspect Flow tab DOM for Google account badge / email
  let targetTabId = tabId;
  if (!targetTabId) {
    try {
      const flowTabs = await chrome.tabs.query({ url: ['https://flow.google.com/*', 'https://labs.google/*'] });
      targetTabId = flowTabs.find((t) => t.active)?.id || flowTabs[0]?.id;
    } catch (e) {}
  }

  if (targetTabId) {
    try {
      const results = await chrome.scripting.executeScript({
        target: { tabId: targetTabId },
        func: () => {
          const emailRegex = /[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+/;
          const accountEls = document.querySelectorAll(
            'a[aria-label*="@"], button[aria-label*="@"], [aria-label*="Google Account"], [aria-label*="Tài khoản Google"], [data-email]',
          );
          for (const el of accountEls) {
            const label = el.getAttribute('aria-label') || el.getAttribute('data-email') || '';
            const m = label.match(emailRegex);
            if (m) return { email: m[0] };
          }
          const imgs = document.querySelectorAll('img[alt*="@"]');
          for (const img of imgs) {
            const m = (img.alt || '').match(emailRegex);
            if (m) return { email: m[0] };
          }
          const html = document.documentElement.innerHTML.slice(0, 100000);
          const m = html.match(/"email":"([a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+)"/);
          if (m) return { email: m[1] };
          return null;
        },
      });
      const domResult = results[0]?.result;
      if (domResult?.email) return domResult;
    } catch (e) {}
  }

  return null;
}

chrome.webRequest.onBeforeSendHeaders.addListener(
  async (details) => {
    if (!details?.requestHeaders?.length) return;
    const authHeader = details.requestHeaders.find(
      (h) => h.name?.toLowerCase() === 'authorization',
    );
    const value = authHeader?.value || '';
    if (!value.startsWith('Bearer ya29.')) return;

    const token = value.replace(/^Bearer\s+/i, '').trim();
    if (!token) return;

    flowKey = token;
    metrics.tokenCapturedAt = Date.now();
    chrome.storage.local.set({ flowKey, metrics });

    // Check account identity (email / user ID) to differentiate token refresh vs actual account switch
    let accountSwitched = false;
    const accInfo = await getAccountIdentity(token, details.tabId);
    if (accInfo?.email || accInfo?.userId) {
      const prevEmail = currentAccount.email;
      const prevUserId = currentAccount.userId;
      const newEmail = accInfo.email;
      const newUserId = accInfo.userId;

      // Account changed ONLY when email or user_id changes to a different identity
      if ((prevEmail && newEmail && prevEmail !== newEmail) ||
          (prevUserId && newUserId && prevUserId !== newUserId)) {
        accountSwitched = true;
        console.log(`[FlowAgent] ACCOUNT SWITCH DETECTED: ${prevEmail || prevUserId} -> ${newEmail || newUserId}`);
      }

      currentAccount = {
        email: newEmail || currentAccount.email,
        userId: newUserId || currentAccount.userId,
      };
      chrome.storage.local.set({ currentAccount });
    }

    console.log('[FlowAgent] Bearer token captured. Account:', currentAccount.email || currentAccount.userId || 'unknown');

    // Notify agent with active project ID and verified account identity
    if (ws?.readyState === WebSocket.OPEN) {
      ensureActiveProjectTab().then(({ projectId }) => {
        ws.send(JSON.stringify({
          type: 'token_captured',
          flowKey,
          accountChanged: accountSwitched,
          accountEmail: currentAccount.email,
          accountUserId: currentAccount.userId,
          activeProjectId: projectId,
        }));
      }).catch(() => {
        ws.send(JSON.stringify({
          type: 'token_captured',
          flowKey,
          accountChanged: accountSwitched,
          accountEmail: currentAccount.email,
          accountUserId: currentAccount.userId,
        }));
      });
    }
  },
  { urls: ['<all_urls>'] },
  ['requestHeaders', 'extraHeaders'],
);

let _latestVideoUrls = [];
if (chrome.webRequest?.onBeforeRequest) {
  chrome.webRequest.onBeforeRequest.addListener(
    (details) => {
      const u = details.url || '';
      if (u.includes('.mp4') || u.includes('/video/') || u.includes('flow-content.google') || (u.includes('/asb/') && !u.includes('=s512') && !u.includes('=s32'))) {
        console.log('[FlowAgent] Captured media URL:', u);
        _latestVideoUrls.push({ url: u, time: Date.now(), method: details.method });
        if (_latestVideoUrls.length > 50) _latestVideoUrls.shift();
        if (ws?.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({ type: 'video_request_captured', url: u }));
        }
      }
    },
    { urls: ['<all_urls>'] }
  );
}

let _openingFlowTab = false;

async function captureTokenFromFlowTab() {
  const tabs = await chrome.tabs.query({
    url: ['https://flow.google.com/*', 'https://labs.google/fx/tools/flow*', 'https://labs.google/fx/*', 'https://labs.google/*'],
  });
  if (!tabs.length) {
    if (_openingFlowTab) {
      console.log('[FlowAgent] Flow tab already opening, skipping');
      return;
    }
    _openingFlowTab = true;
    try {
      console.log('[FlowAgent] No Flow tab found — opening one in background');
      await chrome.tabs.create({ url: 'https://flow.google.com/', active: false });
      await sleep(3000);
      const retryTabs = await chrome.tabs.query({
        url: ['https://flow.google.com/*', 'https://labs.google/fx/tools/flow*', 'https://labs.google/fx/*', 'https://labs.google/*'],
      });
      if (!retryTabs.length) {
        console.log('[FlowAgent] Flow tab not ready yet after open');
        return;
      }
      await chrome.scripting.executeScript({
        target: { tabId: retryTabs[0].id },
        files: ['content.js'],
      });
      console.log('[FlowAgent] Token refresh triggered on newly opened Flow tab');
    } catch (e) {
      console.error('[FlowAgent] Token refresh failed after opening tab:', e);
    } finally {
      _openingFlowTab = false;
    }
    return;
  }
  try {
    await chrome.scripting.executeScript({
      target: { tabId: tabs[0].id },
      files: ['content.js'],
    });
    console.log('[FlowAgent] Token refresh triggered on Flow tab');
  } catch (e) {
    console.error('[FlowAgent] Token refresh failed:', e);
  }
}

// ─── WebSocket to Agent ─────────────────────────────────────

function connectToAgent() {
  if (manualDisconnect) return;
  if (ws?.readyState === WebSocket.CONNECTING) return;
  if (ws?.readyState === WebSocket.OPEN) return;

  try {
    ws = new WebSocket(AGENT_WS_URL);
  } catch (e) {
    console.error('[FlowAgent] WS connect error:', e);
    scheduleReconnect();
    return;
  }

let _heartbeatTimer = null;
function startWsHeartbeat() {
  if (_heartbeatTimer) clearInterval(_heartbeatTimer);
  _heartbeatTimer = setInterval(() => {
    if (ws?.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({ type: 'ping' }));
    }
  }, 10000);
}

if (chrome.runtime?.onConnect) {
  chrome.runtime.onConnect.addListener((port) => {
    if (port.name === 'FLOW_KEEPALIVE') {
      port.onDisconnect.addListener(() => {});
    }
  });
}

  ws.onopen = () => {
    console.log('[FlowAgent] Connected to agent');
    chrome.alarms.clear('reconnect');
    setState('idle');
    startWsHeartbeat();

    // Token refresh alarm — 45 min gives buffer before ~60 min expiry
    chrome.alarms.create('token-refresh', { periodInMinutes: 45 });

    // Send current state + resend token with active project and account identity
    ensureActiveProjectTab().then(({ projectId }) => {
      ws.send(JSON.stringify({
        type: 'extension_ready',
        flowKeyPresent: !!flowKey,
        activeProjectId: projectId,
        accountEmail: currentAccount.email,
        accountUserId: currentAccount.userId,
        tokenAge: flowKey && metrics.tokenCapturedAt ? Date.now() - metrics.tokenCapturedAt : null,
      }));
      if (flowKey) {
        ws.send(JSON.stringify({
          type: 'token_captured',
          flowKey,
          activeProjectId: projectId,
          accountEmail: currentAccount.email,
          accountUserId: currentAccount.userId,
        }));
      }
    }).catch(() => {
      ws.send(JSON.stringify({
        type: 'extension_ready',
        flowKeyPresent: !!flowKey,
        accountEmail: currentAccount.email,
        accountUserId: currentAccount.userId,
        tokenAge: flowKey && metrics.tokenCapturedAt ? Date.now() - metrics.tokenCapturedAt : null,
      }));
      if (flowKey) {
        ws.send(JSON.stringify({
          type: 'token_captured',
          flowKey,
          accountEmail: currentAccount.email,
          accountUserId: currentAccount.userId,
        }));
      }
    });
  };

  ws.onmessage = async ({ data }) => {
    try {
      const msg = JSON.parse(data);

      if (msg.method === 'api_request') {
        await handleApiRequest(msg);
      } else if (msg.method === 'trpc_request') {
        await handleTrpcRequest(msg);
      } else if (msg.method === 'solve_captcha') {
        await handleSolveCaptcha(msg);
      } else if (msg.method === 'reload_extension') {
        sendToAgent({ id: msg.id, result: { reloading: true } });
        setTimeout(() => chrome.runtime.reload(), 100);
      } else if (msg.method === 'navigate_tab') {
        const { tabId, url } = msg.params || {};
        try {
          let target = tabId;
          if (!target) {
            const act = await chrome.tabs.query({ active: true, currentWindow: true });
            target = act[0]?.id;
          }
          if (target && url) {
            await chrome.tabs.update(target, { url, active: true });
            sendToAgent({ id: msg.id, result: { updated: true, tabId: target, url } });
          } else {
            sendToAgent({ id: msg.id, error: 'NO_TARGET_OR_URL' });
          }
        } catch (e) {
          sendToAgent({ id: msg.id, error: e.message });
        }
      } else if (msg.method === 'get_status') {
        const allTabs = await chrome.tabs.query({});
        const localData = await chrome.storage.local.get(null);
        if (!flowKey && localData?.flowKey) {
          flowKey = localData.flowKey;
          if (ws?.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type: 'token_captured', flowKey }));
          }
        }
        const activeProj = await ensureActiveProjectTab().catch(() => ({ projectId: null }));
        sendToAgent({
          id: msg.id,
          result: {
            state,
            flowKeyPresent: !!flowKey,
            activeProjectId: activeProj?.projectId || null,
            accountEmail: currentAccount.email,
            accountUserId: currentAccount.userId,
            storageKeys: Object.keys(localData || {}),
            manualDisconnect,
            tokenAge: metrics.tokenCapturedAt ? Date.now() - metrics.tokenCapturedAt : null,
            metrics,
            tabs: allTabs.map(t => ({ id: t.id, url: t.url, active: t.active, title: t.title })),
          },
        });
      } else if (msg.method === 'fetch_blob') {
        const { url } = msg.params || {};
        try {
          const fetchHeaders = {};
          if (flowKey) fetchHeaders['authorization'] = `Bearer ${flowKey}`;
          const resp = await fetch(url, { headers: fetchHeaders, credentials: 'include' });
          if (!resp.ok) {
            sendToAgent({ id: msg.id, status: resp.status, error: `HTTP_${resp.status}` });
          } else {
            const buffer = await resp.arrayBuffer();
            let binary = '';
            const bytes = new Uint8Array(buffer);
            const len = bytes.byteLength;
            const chunkSize = 8192;
            for (let i = 0; i < len; i += chunkSize) {
              binary += String.fromCharCode.apply(null, bytes.subarray(i, Math.min(i + chunkSize, len)));
            }
            const base64Data = btoa(binary);
            sendToAgent({ id: msg.id, status: 200, size: len, data: base64Data });
          }
        } catch (e) {
          sendToAgent({ id: msg.id, status: 500, error: e.message });
        }
      } else if (msg.method === 'get_captured_video_urls') {
        sendToAgent({ id: msg.id, result: _latestVideoUrls });
      } else if (msg.method === 'exec_tab') {
        const { tabId, code } = msg.params || {};
        try {
          let target = tabId;
          if (!target) {
            const tabs = await chrome.tabs.query({ url: ['https://flow.google.com/*', 'https://labs.google/*'] });
            target = tabs[0]?.id;
          }
          if (!target) {
            sendToAgent({ id: msg.id, error: 'NO_TARGET_TAB' });
          } else if (code === 'reload') {
            await chrome.tabs.reload(target);
            sendToAgent({ id: msg.id, result: { success: true, reloaded: true, tabId: target } });
          } else if (code && code.startsWith('js:')) {
            const rawJs = code.slice(3);
            const results = await chrome.scripting.executeScript({
              target: { tabId: target },
              func: (expr) => {
                try {
                  const res = eval(expr);
                  return { success: true, result: res };
                } catch (err) {
                  return { success: false, error: err.message };
                }
              },
              args: [rawJs],
            });
            sendToAgent({ id: msg.id, result: results[0]?.result });
          } else {
            const results = await chrome.scripting.executeScript({
              target: { tabId: target },
              func: (mode) => {
                try {
                  const html = document.documentElement.innerHTML || '';
                  if (mode === 'open_video') {
                    const cards = Array.from(document.querySelectorAll('button, [role="button"], div')).filter(el => {
                      const t = el.innerText || '';
                      return t.includes('play_circle') && !t.includes('play_circle\n');
                    });
                    const targetCards = cards.length ? cards : Array.from(document.querySelectorAll('button, [role="button"], div')).filter(el => (el.innerText || '').includes('play_circle'));
                    if (targetCards.length) {
                      // Click innermost element
                      const target = targetCards[targetCards.length - 1];
                      target.click();
                      return { success: true, clicked: target.innerText.slice(0, 60) };
                    }
                    return { success: false, reason: 'Card not found' };
                  }
                  if (mode === 'click_720p') {
                    const item = Array.from(document.querySelectorAll('.mat-mdc-menu-item, [role="menuitem"]')).find(el => el.innerText.includes('720p'));
                    if (item) {
                      item.click();
                      return { success: true, text: item.innerText.trim() };
                    }
                    return { success: false, error: '720p button not found' };
                  }
                  if (mode === 'click_download') {
                    const dlBtn = Array.from(document.querySelectorAll('button, [role="button"], a, .mat-mdc-menu-item, [role="menuitem"]')).find(el => (el.innerText || '').includes('download') || (el.innerText || '').includes('Tải xuống') || (el.getAttribute('aria-label') || '').toLowerCase().includes('download'));
                    if (dlBtn) {
                      dlBtn.click();
                      return { success: true, text: (dlBtn.innerText || dlBtn.getAttribute('aria-label') || '').trim() };
                    }
                    return { success: false, error: 'download button not found' };
                  }
                  if (mode === 'storage') {
                    const keys = Object.keys(localStorage);
                    const tokens = [];
                    for (const k of keys) {
                      const v = localStorage.getItem(k) || '';
                      if (v.includes('ya29.') || v.includes('Bearer')) {
                        const m = v.match(/ya29\.[a-zA-Z0-9_\-]+/);
                        if (m) tokens.push(m[0]);
                      }
                    }
                    return { success: true, keyCount: keys.length, tokens };
                  }
                  if (mode === 'click_more') {
                    const hotbar = document.querySelector('flow-video-hotbar');
                    const btn = hotbar?.querySelector('button[aria-label*="Tuỳ chọn"], button[aria-label*="More"], button[aria-label*="khác"]');
                    if (btn) {
                      btn.click();
                      return { success: true };
                    }
                    return { success: false, error: 'button not found' };
                  }
                  if (mode === 'inspect_menu') {
                    const items = Array.from(document.querySelectorAll('.mat-mdc-menu-item, [role="menuitem"], .cdk-overlay-container a, .cdk-overlay-container button')).map(el => ({
                      tag: el.tagName,
                      text: el.innerText.trim(),
                      href: el.href || el.getAttribute('href'),
                      html: el.outerHTML.slice(0, 300),
                    }));
                    return { success: true, count: items.length, items };
                  }
                  if (mode === 'buttons') {
                    const buttons = Array.from(document.querySelectorAll('button, [role="tab"], [role="button"], a')).map(b => (b.innerText || b.textContent || b.getAttribute('aria-label') || '').trim()).filter(Boolean);
                    return { success: true, count: buttons.length, buttons: Array.from(new Set(buttons)).slice(0, 50) };
                  }
                  if (mode === 'asb') {
                    const matches = html.match(/https?:\/\/[^\s"'>]*\/asb\/[^\s"'>]*/gi) || [];
                    return { success: true, count: matches.length, asb: Array.from(new Set(matches)) };
                  }
                  if (mode === 'video_tags') {
                    const vids = Array.from(document.querySelectorAll('video, [data-video-id], [data-media-id]')).map(v => ({
                      tag: v.tagName,
                      src: v.src || v.currentSrc,
                      dataset: { ...v.dataset },
                      className: v.className,
                    }));
                    return { success: true, vids };
                  }
                  return {
                    success: true,
                    title: document.title,
                    location: window.location.href,
                    htmlLen: html.length,
                    imgCount: document.querySelectorAll('img').length,
                    vidCount: document.querySelectorAll('video').length,
                  };
                } catch (e) {
                  return { success: false, error: e.message };
                }
              },
              args: [code || ''],
            });
            sendToAgent({ id: msg.id, result: results[0]?.result });
          }
        } catch (e) {
          sendToAgent({ id: msg.id, error: e.message });
        }
      } else if (msg.type === 'callback_secret') {
        callbackSecret = msg.secret;
        chrome.storage.local.set({ callbackSecret: msg.secret });
        console.log('[FlowAgent] Received callback secret');
      } else if (msg.type === 'pong') {
        // keepalive response
      } else if (msg.type === 'ping') {
        if (ws?.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({ type: 'pong' }));
        }
      } else if (msg.type === 'reload_extension' || msg.method === 'reload_extension') {
        console.log('[FlowAgent] Reloading flow tabs and extension on agent command');
        try {
          const flowTabs = await chrome.tabs.query({ url: ['https://flow.google.com/*', 'https://labs.google/*'] });
          for (const t of flowTabs) {
            if (t.id) chrome.tabs.reload(t.id);
          }
        } catch {}
        setTimeout(() => chrome.runtime.reload(), 500);
      } else if (msg.type === 'update_request_log') {
        let updated = false;
        if (msg.id) {
          const entry = requestLog.find((e) => e.id === msg.id);
          if (entry) {
            if (msg.status) entry.status = msg.status;
            if (msg.outputUrl) entry.outputUrl = msg.outputUrl;
            updated = true;
          }
        }
        if (!updated && msg.mediaId) {
          const entry = requestLog.find(
            (e) =>
              (e.payloadSummary && e.payloadSummary.includes(msg.mediaId)) ||
              (e.responseSummary && e.responseSummary.includes(msg.mediaId)),
          );
          if (entry) {
            if (msg.status) entry.status = msg.status;
            if (msg.outputUrl) entry.outputUrl = msg.outputUrl;
            updated = true;
          }
        }
        if (!updated) {
          const vidEntry = requestLog.find(
            (e) =>
              ['GEN_VID', 'GEN_VID_REF', 'UPSCALE'].includes(e.type) &&
              (!e.outputUrl || e.status !== 'COMPLETED'),
          );
          if (vidEntry) {
            if (msg.status) vidEntry.status = msg.status;
            if (msg.outputUrl) vidEntry.outputUrl = msg.outputUrl;
            updated = true;
          }
        }
        if (updated) broadcastRequestLog();
      }
    } catch (e) {
      console.error('[FlowAgent] Message error:', e);
    }
  };

  ws.onclose = () => {
    setState('off');
    if (_heartbeatTimer) {
      clearInterval(_heartbeatTimer);
      _heartbeatTimer = null;
    }
    chrome.alarms.clear('token-refresh');
    if (!manualDisconnect) scheduleReconnect();
  };

  ws.onerror = (e) => {
    console.error('[FlowAgent] WS error:', e);
    metrics.lastError = 'WS_ERROR';
    chrome.storage.local.set({ metrics });
  };
}

function scheduleReconnect() {
  chrome.alarms.create('reconnect', { delayInMinutes: 0.083 }); // ~5s
}

function keepAlive() {
  if (ws?.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify({ type: 'ping' }));
  } else {
    connectToAgent();
  }
}

function sendToAgent(msg) {
  // API responses (with msg.id) go via HTTP — immune to WS disconnect
  if (msg.id) {
    fetch('http://127.0.0.1:8100/api/ext/callback', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(msg),
    }).catch(() => {
      // HTTP failed — fallback to WS
      if (ws?.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
    });
    return;
  }
  // Non-response messages (ping, status) or no secret yet — use WS
  if (ws?.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(msg));
  }
}

// ─── reCAPTCHA Solving ──────────────────────────────────────

async function requestCaptchaFromTab(tabId, requestId, pageAction) {
  try {
    const tab = await chrome.tabs.get(tabId);
    if (!tab || !tab.url || tab.url.startsWith('chrome://') || tab.url.startsWith('chrome-extension://')) {
      return { error: 'INVALID_TAB_URL' };
    }
  } catch (e) {}

  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const resp = await chrome.tabs.sendMessage(tabId, {
        type: 'GET_CAPTCHA',
        requestId,
        pageAction,
      });
      if (resp) {
        if (resp.token) return resp;
        if (resp.error) {
          console.warn(`[FlowAgent] Tab returned captcha error: ${resp.error}`);
          return resp;
        }
      }
    } catch (error) {
      const msg = error?.message || '';
      const shouldInject =
        msg.includes('Receiving end does not exist') ||
        msg.includes('Could not establish connection');
      if (shouldInject && attempt === 0) {
        try {
          await chrome.scripting.executeScript({
            target: { tabId },
            files: ['content.js'],
          });
        } catch (e) {
          // Ignore executeScript permission error, content script is auto-injected by manifest
        }
      }
      await sleep(1000);
    }
  }
  return { error: 'NO_CAPTCHA_LISTENER' };
}

async function createFlowProject(title = 'Animation Studio') {
  if (!flowKey) return null;
  try {
    const resp = await fetch('https://labs.google/fx/api/trpc/project.createProject', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${flowKey}`,
      },
      body: JSON.stringify({ json: { projectTitle: title, toolName: 'PINHOLE' } }),
      credentials: 'include',
    });
    const data = await resp.json();
    const pid = data?.result?.data?.json?.result?.projectId || data?.result?.data?.json?.projectId;
    if (pid) {
      console.log('[FlowAgent] Created new project on Flow for current account:', pid);
      return pid;
    }
  } catch (e) {
    console.error('[FlowAgent] Failed to create project:', e);
  }
  return null;
}

async function ensureActiveProjectTab() {
  let tabs = await chrome.tabs.query({
    url: [
      'https://flow.google.com/*',
      'https://labs.google/fx/tools/flow*',
      'https://labs.google/fx/*',
      'https://labs.google/*',
    ],
  });

  tabs = tabs.filter(
    (t) =>
      t.url &&
      !t.url.startsWith('chrome://') &&
      !t.url.startsWith('chrome-extension://') &&
      !t.url.includes('/about'),
  );

  // 1. Check existing /project/ tabs
  const projectTabs = tabs.filter((t) => t.url && t.url.includes('/project/'));
  if (projectTabs.length) {
    const landingTabs = tabs.filter(
      (t) =>
        (t.url === 'https://flow.google.com/' || t.url === 'https://flow.google.com') &&
        projectTabs.some((o) => o.id !== t.id),
    );
    for (const lt of landingTabs) {
      try { chrome.tabs.remove(lt.id); } catch {}
    }

    let targetTab = projectTabs.find((t) => t.active) || projectTabs[0];
    const m = targetTab.url.match(/\/project\/([0-9a-fA-F-]{36})/);
    return { tab: targetTab, projectId: m ? m[1] : null };
  }

  // 2. No /project/ tab found. Create project for current account on Flow
  console.log('[FlowAgent] No /project/ tab found for current account. Creating new project on Flow...');
  const newPid = await createFlowProject('Animation Studio');
  let baseTab = tabs.find((t) => t.active) || tabs[0];

  if (newPid) {
    const projectUrl = `https://labs.google/fx/tools/flow/project/${newPid}`;
    let targetTab = baseTab;
    if (targetTab) {
      await chrome.tabs.update(targetTab.id, { url: projectUrl });
    } else {
      targetTab = await chrome.tabs.create({ url: projectUrl, active: false });
    }
    await sleep(3500);
    return { tab: targetTab, projectId: newPid };
  }

  return { tab: baseTab || null, projectId: null };
}

async function solveCaptcha(requestId, captchaAction, targetTabId = null) {
  let targetTab = null;
  if (targetTabId) {
    try { targetTab = await chrome.tabs.get(targetTabId); } catch {}
  }
  if (!targetTab) {
    const res = await ensureActiveProjectTab();
    targetTab = res.tab;
  }

  if (targetTab) {
    // Run captcha silently in background without stealing OS window focus
    try {
      const resp = await Promise.race([
        requestCaptchaFromTab(targetTab.id, requestId, captchaAction),
        new Promise((_, rej) => setTimeout(() => rej(new Error('CAPTCHA_TIMEOUT')), 30000)),
      ]);
      if (resp) return resp;
    } catch (e) {
      console.warn('[FlowAgent] Captcha request error on target tab:', e);
      return { error: e.message || 'CAPTCHA_ERROR' };
    }
  }

  return { error: 'NO_FLOW_PROJECT_TAB' };
}

async function handleSolveCaptcha(msg) {
  const { id, params } = msg;
  const result = await solveCaptcha(id, params?.captchaAction || 'VIDEO_GENERATION');

  // Standalone captcha solve counts as captcha-consuming
  metrics.requestCount++;
  if (result?.token) {
    metrics.successCount++;
  } else {
    metrics.failedCount++;
    metrics.lastError = result?.error || 'NO_TOKEN';
  }
  chrome.storage.local.set({ metrics });

  sendToAgent({ id, result });
}

// ─── API Request Proxy ──────────────────────────────────────

async function handleTrpcRequest(msg) {
  const { id, params } = msg;
  const { url, method = 'POST', headers = {}, body, responseMode } = params;

  if (!url || (!url.startsWith('https://labs.google/') && !url.startsWith('https://flow.google.com/'))) {
    sendToAgent({ id, error: 'INVALID_TRPC_URL' });
    return;
  }

  setState('running');
  // TRPC calls don't consume captcha — don't count in metrics

  const logId = id;
  const logType = url.includes('createProject') ? 'CREATE_PROJECT' : 'TRPC';
  // TRPC calls are silent — don't show in request log

  const fetchHeaders = { 'Content-Type': 'application/json', ...headers };
  if (flowKey) {
    fetchHeaders['authorization'] = `Bearer ${flowKey}`;
  }

  try {
    const resp = await fetch(url, {
      method,
      headers: fetchHeaders,
      body: body ? JSON.stringify(body) : undefined,
      credentials: 'include',
    });
    let data;
    if (responseMode === 'url') {
      // fetch() has already followed the authenticated Flow redirect. Return
      // only the final signed URL and cancel the body so large videos are not
      // buffered in the extension or copied through the WebSocket bridge.
      data = {
        url: resp.url,
        contentType: resp.headers.get('content-type'),
      };
      await resp.body?.cancel();
    } else {
      data = await resp.json();
    }
    chrome.storage.local.set({ metrics });
    updateRequestLog(logId, { status: 'success' });
    sendToAgent({ id, status: resp.status, data });
  } catch (e) {
    console.error('[FlowAgent] tRPC request failed:', e);
    chrome.storage.local.set({ metrics });
    updateRequestLog(logId, { status: 'failed', error: e.message || 'TRPC_FETCH_FAILED' });
    sendToAgent({ id, error: e.message || 'TRPC_FETCH_FAILED' });
  } finally {
    setState('idle');
  }
}

async function handleApiRequest(msg) {
  const { id, params } = msg;
  const { url, method, headers, body, captchaAction } = params;

  if (!url) {
    sendToAgent({ id, error: 'MISSING_URL' });
    return;
  }

  if (!url.startsWith('https://aisandbox-pa.googleapis.com/')) {
    sendToAgent({ id, error: 'INVALID_URL' });
    return;
  }

  setState('running');
  const hasCaptcha = !!captchaAction;
  if (hasCaptcha) metrics.requestCount++;

  const logId = id;
  const logType = _classifyApiUrl(url);
  if (_VISIBLE_TYPES.has(logType)) {
    const payloadSummary = body ? JSON.stringify(body).slice(0, 200) : null;
    addRequestLog({ id: logId, type: logType, time: new Date().toISOString(), status: 'processing', error: null, outputUrl: null, url, payloadSummary });
  }

  try {
    // Step 0: Ensure active project tab & ID of current account
    let activeProjectId = null;
    let targetTab = null;
    try {
      const projInfo = await ensureActiveProjectTab();
      targetTab = projInfo.tab;
      activeProjectId = projInfo.projectId;
    } catch (e) {
      console.warn('[FlowAgent] ensureActiveProjectTab error:', e);
    }

    // Step 1: Solve captcha if needed
    let captchaToken = null;
    if (captchaAction) {
      const captchaResult = await solveCaptcha(id, captchaAction, targetTab?.id);
      captchaToken = captchaResult?.token || null;
      if (!captchaToken) {
        // Cannot proceed without captcha — API will 403
        const err = captchaResult?.error || 'CAPTCHA_FAILED';
        console.error(`[FlowAgent] Captcha failed for ${captchaAction}: ${err}`);
        sendToAgent({ id, status: 403, error: `CAPTCHA_FAILED: ${err}` });
        if (hasCaptcha) { metrics.failedCount++; metrics.lastError = `CAPTCHA_FAILED: ${err}`; }
        chrome.storage.local.set({ metrics });
        updateRequestLog(logId, { status: 'failed', error: `CAPTCHA_FAILED: ${err}` });
        setState('idle');
        return;
      }
    }

    // Step 2: Inject captcha token and align projectId to current account's active project
    let finalBody = body;
    if (finalBody) {
      finalBody = JSON.parse(JSON.stringify(finalBody)); // deep clone

      // Align projectId if different from current account's active project
      if (activeProjectId) {
        if (finalBody.clientContext?.projectId && finalBody.clientContext.projectId !== activeProjectId) {
          console.log(`[FlowAgent] Aligning clientContext.projectId ${finalBody.clientContext.projectId} -> ${activeProjectId}`);
          finalBody.clientContext.projectId = activeProjectId;
        }
        if (finalBody.projectId && finalBody.projectId !== activeProjectId) {
          finalBody.projectId = activeProjectId;
        }
        if (finalBody.requests && Array.isArray(finalBody.requests)) {
          for (const req of finalBody.requests) {
            if (req.clientContext?.projectId && req.clientContext.projectId !== activeProjectId) {
              req.clientContext.projectId = activeProjectId;
            }
            if (req.projectId && req.projectId !== activeProjectId) {
              req.projectId = activeProjectId;
            }
          }
        }
      }

      if (captchaToken) {
        if (finalBody.clientContext?.recaptchaContext) {
          finalBody.clientContext.recaptchaContext.token = captchaToken;
        }
        if (finalBody.requests && Array.isArray(finalBody.requests)) {
          for (const req of finalBody.requests) {
            if (req.clientContext?.recaptchaContext) {
              req.clientContext.recaptchaContext.token = captchaToken;
            }
          }
        }
      }
    }

    if (activeProjectId && ws?.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({ type: 'active_project_updated', projectId: activeProjectId }));
    }

    // Step 3: Use flowKey for auth
    const activeFlowKey = flowKey;
    if (!activeFlowKey) {
      sendToAgent({ id, status: 503, error: 'NO_FLOW_KEY' });
      if (hasCaptcha) { metrics.failedCount++; metrics.lastError = 'NO_FLOW_KEY'; }
      chrome.storage.local.set({ metrics });
      updateRequestLog(logId, { status: 'failed', error: 'NO_FLOW_KEY' });
      setState('idle');
      return;
    }

    const fetchHeaders = { ...(headers || {}) };
    fetchHeaders['authorization'] = `Bearer ${activeFlowKey}`;

    // Step 4: Make the API call from browser context
    const response = await fetch(url, {
      method: method || 'POST',
      headers: fetchHeaders,
      credentials: 'include',
      body: method === 'GET' ? undefined : JSON.stringify(finalBody),
    });

    let responseData;
    const responseText = await response.text();
    try {
      responseData = JSON.parse(responseText);
    } catch {
      responseData = responseText;
    }

    sendToAgent({
      id,
      status: response.status,
      data: responseData,
    });

    const responseSummary = responseText ? responseText.slice(0, 300) : null;
    if (response.ok) {
      if (hasCaptcha) { metrics.successCount++; metrics.lastError = null; }
      updateRequestLog(logId, { status: 'success', httpStatus: response.status, responseSummary });
    } else {
      if (hasCaptcha) { metrics.failedCount++; metrics.lastError = `API_${response.status}`; }
      updateRequestLog(logId, { status: 'failed', error: `API_${response.status}`, httpStatus: response.status, responseSummary });
    }
  } catch (e) {
    sendToAgent({
      id,
      status: 500,
      error: e.message || 'API_REQUEST_FAILED',
    });
    if (hasCaptcha) { metrics.failedCount++; metrics.lastError = e.message; }
    updateRequestLog(logId, { status: 'failed', error: e.message || 'API_REQUEST_FAILED' });
  }

  chrome.storage.local.set({ metrics });
  setState('idle');
}

// ─── State & Popup ──────────────────────────────────────────

function setState(newState) {
  state = newState;
  const badges = { idle: '●', running: '▶', off: '○' };
  const colors = { idle: '#22c55e', running: '#f59e0b', off: '#6b7280' };
  chrome.action.setBadgeText({ text: badges[state] || '' });
  chrome.action.setBadgeBackgroundColor({ color: colors[state] || '#000' });
  broadcastStatus();
}

function broadcastStatus() {
  chrome.runtime.sendMessage({ type: 'STATUS_PUSH' }).catch(() => {});
}

chrome.runtime.onMessage.addListener((msg, _, reply) => {
  if (msg.type === 'TOKEN_CAPTURED') {
    flowKey = msg.token;
    metrics.tokenCapturedAt = Date.now();
    chrome.storage.local.set({ flowKey, metrics });
    console.log('[FlowAgent] Bearer token captured from page fetch');
    if (ws?.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({ type: 'token_captured', flowKey }));
    }
    reply({ ok: true });
    return true;
  }

  if (msg.type === 'STATUS') {
    reply({
      connected: ws?.readyState === WebSocket.OPEN,
      agentConnected: ws?.readyState === WebSocket.OPEN,
      flowKeyPresent: !!flowKey,
      manualDisconnect,
      tokenAge: metrics.tokenCapturedAt ? Date.now() - metrics.tokenCapturedAt : null,
      metrics: {
        requestCount: metrics.requestCount,
        successCount: metrics.successCount,
        failedCount: metrics.failedCount,
        lastError: metrics.lastError,
      },
      state,
    });
  }

  if (msg.type === 'DISCONNECT') {
    manualDisconnect = true;
    if (ws) ws.close();
    reply({ ok: true });
    return true;
  }

  if (msg.type === 'RECONNECT') {
    manualDisconnect = false;
    connectToAgent();
    reply({ ok: true });
    return true;
  }

  if (msg.type === 'REQUEST_LOG') {
    reply({ log: requestLog });
    return true;
  }

  if (msg.type === 'CLEAR_REQUEST_LOG') {
    requestLog = [];
    broadcastRequestLog();
    reply({ ok: true });
    return true;
  }

  if (msg.type === 'RELOAD_EXTENSION') {
    chrome.runtime.reload();
    return;
  }

  if (msg.type === 'OPEN_FLOW_TAB') {
    chrome.tabs.query({
      url: ['https://flow.google.com/*', 'https://labs.google/fx/tools/flow*', 'https://labs.google/fx/*', 'https://labs.google/*'],
    }).then((tabs) => {
      if (tabs.length) {
        chrome.tabs.update(tabs[0].id, { active: true });
        reply({ ok: true, tabId: tabs[0].id });
      } else {
        chrome.tabs.create({ url: 'https://flow.google.com/' })
          .then((tab) => reply({ ok: true, tabId: tab.id }))
          .catch((e) => reply({ error: e.message }));
      }
    }).catch((e) => reply({ error: e.message }));
    return true;
  }

  if (msg.type === 'REFRESH_TOKEN') {
    captureTokenFromFlowTab()
      .then(() => reply({ ok: true }))
      .catch((e) => reply({ error: e.message }));
    return true;
  }

  if (msg.type === 'TEST_CAPTCHA') {
    solveCaptcha(`test-${Date.now()}`, msg.pageAction || 'IMAGE_GENERATION')
      .then((r) => reply(r))
      .catch((e) => reply({ error: e.message }));
    return true;
  }

  if (msg.type === 'TRPC_MEDIA_URLS') {
    handleTrpcMediaUrls(msg.trpcUrl, msg.body);
    reply({ ok: true });
    return true;
  }

  return true;
});

// ─── TRPC Media URL Extractor ──────────────────────────────

function handleTrpcMediaUrls(trpcUrl, bodyText) {
  try {
    // Extract all fresh signed URLs (flow-content.google or legacy GCS)
    const urlRegex = /https:\/\/(?:flow-content\.google|storage\.googleapis\.com\/ai-sandbox-videofx)\/(?:image|video)\/[0-9a-f-]{36}\?[^"'\s]+/g;
    const matches = bodyText.match(urlRegex) || [];
    if (!matches.length) return;

    // Deduplicate and parse
    const urlMap = {};
    for (const rawUrl of matches) {
      // Unescape JSON-escaped URLs
      const url = rawUrl.replace(/\\u0026/g, '&').replace(/\\/g, '');
      const mediaMatch = url.match(/\/(image|video)\/([0-9a-f-]{36})\?/);
      if (mediaMatch) {
        const [, mediaType, mediaId] = mediaMatch;
        // Keep last occurrence (freshest)
        urlMap[mediaId] = { mediaType, url, mediaId };
      }
    }

    const entries = Object.values(urlMap);
    if (!entries.length) return;

    console.log(`[FlowAgent] Captured ${entries.length} fresh media URLs from TRPC`);
    // URL refresh is silent — don't show in request log

    // Update matching video/upscale entries in requestLog if done
    for (const item of entries) {
      if (item.mediaType === 'video') {
        _latestVideoUrls.push({ url: item.url, time: Date.now(), method: 'TRPC', mediaId: item.mediaId });
        if (_latestVideoUrls.length > 50) _latestVideoUrls.shift();
        const vidEntry = requestLog.find(e => ['GEN_VID', 'GEN_VID_REF', 'UPSCALE'].includes(e.type) && (!e.outputUrl || e.status !== 'COMPLETED'));
        if (vidEntry) {
          vidEntry.outputUrl = item.url;
          vidEntry.status = 'COMPLETED';
          broadcastRequestLog();
        }
      }
    }

    // Forward to agent for DB update
    if (ws?.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({
        type: 'media_urls_refresh',
        urls: entries,
      }));
    }
  } catch (e) {
    console.error('[FlowAgent] Failed to extract TRPC media URLs:', e);
  }
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

// ─── Human-like Telemetry ──────────────────────────────────
// Periodically send tracking events to Google's analytics endpoints
const _UA = navigator.userAgent;
let _telemetrySessionId = `;${Date.now()}`;
function _rand(min, max) { return Math.floor(Math.random() * (max - min + 1)) + min; }

function _buildBatchLogPayload() {
  const types = ['FLOW_IMAGE_LATENCY', 'FLOW_VIDEO_LATENCY'];
  return {
    appEvents: Array.from({ length: _rand(1, 3) }, () => ({
      event: types[_rand(0, types.length - 1)],
      eventProperties: [
        { key: 'CURRENT_TIME_MS', doubleValue: Date.now() },
        { key: 'DURATION_MS', doubleValue: _rand(150, 800) },
        { key: 'USER_AGENT', stringValue: _UA },
        { key: 'IS_DESKTOP', booleanValue: true },
      ],
      eventMetadata: { sessionId: _telemetrySessionId },
      eventTime: new Date().toISOString(),
    })),
  };
}

function _buildFrontendEventsPayload() {
  const eventTypes = ['FLOW_IMAGE_LATENCY', 'FLOW_VIDEO_LATENCY', 'GRID_SCROLL_DEPTH', 'FLOW_PROJECT_OPEN', 'FLOW_SCENE_VIEW'];
  return {
    events: Array.from({ length: _rand(1, 4) }, () => {
      const et = eventTypes[_rand(0, eventTypes.length - 1)];
      const params = {
        USER_AGENT: { '@type': 'type.googleapis.com/google.protobuf.StringValue', value: _UA },
        IS_DESKTOP: { '@type': 'type.googleapis.com/google.protobuf.StringValue', value: 'true' },
      };
      if (et.includes('LATENCY')) {
        params.CURRENT_TIME_MS = { '@type': 'type.googleapis.com/google.protobuf.StringValue', value: String(Date.now()) };
        params.DURATION_MS = { '@type': 'type.googleapis.com/google.protobuf.StringValue', value: String(_rand(100, 600)) };
      }
      if (et === 'GRID_SCROLL_DEPTH') params.MEDIA_GENERATION_PAYGATE_TIER = { '@type': 'type.googleapis.com/google.protobuf.StringValue', value: 'PAYGATE_TIER_TWO' };
      return { eventType: et, metadata: { sessionId: _telemetrySessionId, createTime: new Date().toISOString(), additionalParams: params } };
    }),
  };
}

async function sendTelemetry() {
  if (!flowKey || state === 'off') return;
  const headers = { 'Content-Type': 'text/plain;charset=UTF-8', authorization: `Bearer ${flowKey}` };
  try {
    const url = Math.random() < 0.5 ? 'https://aisandbox-pa.googleapis.com/v1:batchLog' : 'https://aisandbox-pa.googleapis.com/v1/flow:batchLogFrontendEvents';
    const body = Math.random() < 0.5 ? _buildBatchLogPayload() : _buildFrontendEventsPayload();
    await fetch(url, { method: 'POST', headers, credentials: 'include', body: JSON.stringify(body) });
  } catch {}
}

function scheduleTelemetry() {
  setTimeout(async () => { await sendTelemetry(); scheduleTelemetry(); }, _rand(45, 120) * 1000);
}

setInterval(() => { _telemetrySessionId = `;${Date.now()}`; }, _rand(25, 35) * 60 * 1000);
scheduleTelemetry();
console.log('[FlowAgent] Extension loaded');
