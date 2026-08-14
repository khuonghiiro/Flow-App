function safeJsonParse(raw) {
  try { return JSON.parse(raw); } catch { return null; }
}

function parseHeaderParam(raw) {
  const s = String(raw || '').trim();
  if (!s) return {};
  const obj = safeJsonParse(s);
  if (!obj || typeof obj !== 'object') throw new Error('headerParam JSON không hợp lệ');
  return obj;
}

function parseCookieInput(raw) {
  const s = String(raw || '').trim();
  if (!s) return { cookie: '', headers: {} };
  if (s.startsWith('{')) {
    const obj = safeJsonParse(s);
    if (!obj || typeof obj !== 'object') throw new Error('cookieInput JSON không hợp lệ');
    const cookie = String(obj.cookie || obj.Cookie || '').trim();
    const headers = { ...obj };
    delete headers.cookie;
    delete headers.Cookie;
    return { cookie, headers };
  }
  return { cookie: s, headers: {} };
}

function normalizeHeaderKeyCI(k) {
  if (!k) return k;
  const s = String(k);
  if (s.toLowerCase() === 'cookie') return 'cookie';
  return s.toLowerCase();
}

function mergeHeadersCI(...objs) {
  const out = {};
  for (const obj of objs) {
    if (!obj) continue;
    for (const [k, v] of Object.entries(obj)) {
      if (v === undefined || v === null) continue;
      out[normalizeHeaderKeyCI(k)] = String(v);
    }
  }
  return out;
}

async function setCookiesFromString(cookieStr) {
  const cookie = String(cookieStr || '').trim();
  if (!cookie) return;
  const parts = cookie.split(';');
  for (const p of parts) {
    const seg = p.trim();
    if (!seg) continue;
    const eq = seg.indexOf('=');
    if (eq <= 0) continue;
    const name = seg.slice(0, eq).trim();
    const value = seg.slice(eq + 1).trim();
    if (!name) continue;
    try {
      await chrome.cookies.set({
        url: 'https://grok.com/',
        name,
        value,
        domain: 'grok.com',
        path: '/',
        secure: true,
      });
    } catch {
      // ignore single cookie failures
    }
  }
}

function sanitizeHeadersForFetch(headers) {
  const h = { ...headers };
  // Không set các header browser sẽ chặn/không cần set tay
  delete h['cookie'];
  delete h['content-length'];
  delete h['host'];
  delete h['connection'];
  delete h['accept-encoding'];
  return h;
}

// ─────────────────────────────────────────────────────────────────────────────
// WebNodeControl-like network hooks (fetch + XHR)
// - blocking rules
// - capture request headers/params/payload/body + response text
// ─────────────────────────────────────────────────────────────────────────────
const __gvs = (globalThis.__gvs = globalThis.__gvs || {
  installed: false,
  config: { blockingRules: [] },
  captures: [],
  jobs: {},
});

function urlMatchesPattern(url, pattern) {
  if (!pattern) return false;
  const p = String(pattern).trim();
  if (!p) return false;
  // simple wildcard: * -> .*
  if (p.includes('*')) {
    const re = new RegExp('^' + p.split('*').map(s => s.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*') + '$', 'i');
    return re.test(url);
  }
  // regex literal: /.../i
  if (p.startsWith('/') && p.lastIndexOf('/') > 0) {
    const last = p.lastIndexOf('/');
    const body = p.slice(1, last);
    const flags = p.slice(last + 1) || 'i';
    try { return new RegExp(body, flags).test(url); } catch { return false; }
  }
  return url.toLowerCase().includes(p.toLowerCase());
}

function methodMatches(ruleMethod, actual) {
  const rm = String(ruleMethod || 'ALL').trim().toUpperCase();
  if (rm === 'ALL' || rm === '*') return true;
  return rm === String(actual || '').trim().toUpperCase();
}

function parseQueryParams(url) {
  try {
    const u = new URL(url, location.href);
    const obj = {};
    for (const [k, v] of u.searchParams.entries()) obj[k] = v;
    return obj;
  } catch {
    return {};
  }
}

function pushCapture(item) {
  __gvs.captures.unshift({ t: Date.now(), ...item });
  if (__gvs.captures.length > 60) __gvs.captures.length = 60;
}

function cssPath(el) {
  try {
    if (!el || !el.tagName) return '';
    let path = el.tagName.toLowerCase();
    if (el.id) return `${path}#${el.id}`;
    if (el.classList && el.classList.length) path += '.' + [...el.classList].slice(0, 3).join('.');
    const parent = el.parentElement;
    if (!parent) return path;
    const siblings = [...parent.children].filter(e => e.tagName === el.tagName);
    if (siblings.length > 1) path += `:nth-of-type(${siblings.indexOf(el) + 1})`;
    return cssPath(parent) + ' > ' + path;
  } catch {
    return '';
  }
}

function shouldBlock(url, method) {
  const rules = __gvs.config.blockingRules || [];
  for (const r of rules) {
    if (!r) continue;
    if (urlMatchesPattern(url, r.url || r.urlPattern || '')) {
      if (methodMatches(r.method || r.Method || 'ALL', method)) return { matched: true, rule: r };
    }
  }
  return { matched: false, rule: null };
}

async function readBodyAsText(body) {
  if (body == null) return '';
  if (typeof body === 'string') return body;
  if (body instanceof URLSearchParams) return body.toString();
  if (body instanceof FormData) {
    const out = {};
    for (const [k, v] of body.entries()) out[k] = typeof v === 'string' ? v : `[File ${v.name || 'blob'}]`;
    return JSON.stringify(out);
  }
  if (body instanceof Blob) return await body.text();
  try { return JSON.stringify(body); } catch { return String(body); }
}

function headersToObject(h) {
  const obj = {};
  try {
    if (h instanceof Headers) {
      h.forEach((v, k) => { obj[String(k).toLowerCase()] = String(v); });
      return obj;
    }
  } catch {}
  if (Array.isArray(h)) {
    for (const [k, v] of h) obj[String(k).toLowerCase()] = String(v);
    return obj;
  }
  if (h && typeof h === 'object') {
    for (const [k, v] of Object.entries(h)) obj[String(k).toLowerCase()] = String(v);
  }
  return obj;
}

function installNetHooksOnce() {
  if (__gvs.installed) return;
  __gvs.installed = true;

  // fetch
  if (globalThis.fetch) {
    const origFetch = globalThis.fetch.bind(globalThis);
    globalThis.fetch = async function(input, init) {
      const url = (typeof input === 'string') ? input : (input && input.url) ? input.url : String(input || '');
      const method = (init && init.method) ? init.method : (input && input.method) ? input.method : 'GET';
      const reqHeaders = headersToObject((init && init.headers) ? init.headers : (input && input.headers) ? input.headers : null);
      let bodyText = '';
      try { bodyText = await readBodyAsText(init && init.body); } catch {}
      const params = parseQueryParams(url);

      const block = shouldBlock(url, method);
      pushCapture({ kind: 'request', api: 'fetch', url, method, headers: reqHeaders, params, body: bodyText, blocked: block.matched });
      if (block.matched) {
        return new Response('Blocked by GVS rule', { status: 403, statusText: 'Blocked' });
      }

      const res = await origFetch(input, init);
      try {
        const clone = res.clone();
        const txt = await clone.text();
        pushCapture({ kind: 'response', api: 'fetch', url, method, status: res.status, ok: res.ok, text: txt.slice(0, 20000) });
      } catch {}
      return res;
    };
  }

  // XHR
  if (globalThis.XMLHttpRequest) {
    const OrigXHR = globalThis.XMLHttpRequest;
    function WrappedXHR() {
      const xhr = new OrigXHR();
      let _url = '';
      let _method = 'GET';
      let _reqHeaders = {};
      let _bodyText = '';

      const origOpen = xhr.open;
      xhr.open = function(method, url) {
        _method = String(method || 'GET');
        _url = String(url || '');
        return origOpen.apply(xhr, arguments);
      };

      const origSetHeader = xhr.setRequestHeader;
      xhr.setRequestHeader = function(k, v) {
        _reqHeaders[String(k).toLowerCase()] = String(v);
        return origSetHeader.apply(xhr, arguments);
      };

      const origSend = xhr.send;
      xhr.send = function(body) {
        try { _bodyText = (body == null) ? '' : (typeof body === 'string' ? body : String(body)); } catch {}
        const params = parseQueryParams(_url);
        const block = shouldBlock(_url, _method);
        pushCapture({ kind: 'request', api: 'xhr', url: _url, method: _method, headers: _reqHeaders, params, body: _bodyText, blocked: block.matched });
        if (block.matched) {
          try { xhr.abort(); } catch {}
          // mimic blocked response by firing readystatechange
          return;
        }
        return origSend.apply(xhr, arguments);
      };

      xhr.addEventListener('loadend', () => {
        try {
          const txt = (xhr.responseType && xhr.responseType !== '' && xhr.responseType !== 'text')
            ? '[non-text responseType]'
            : (xhr.responseText || '');
          pushCapture({ kind: 'response', api: 'xhr', url: _url, method: _method, status: xhr.status, ok: xhr.status >= 200 && xhr.status < 300, text: String(txt).slice(0, 20000) });
        } catch {}
      });

      return xhr;
    }
    globalThis.XMLHttpRequest = WrappedXHR;
  }
}

async function setConfig(newCfg) {
  __gvs.config = {
    ...__gvs.config,
    ...(newCfg || {}),
  };
  installNetHooksOnce();
  await chrome.storage.local.set({ gvsConfig: __gvs.config });
}

async function loadConfig() {
  const st = await chrome.storage.local.get(['gvsConfig']);
  if (st.gvsConfig) __gvs.config = st.gvsConfig;
  installNetHooksOnce();
}

loadConfig().catch(() => {});

function removeGvsModal() {
  const root = document.getElementById('__gvs_modal_root');
  if (root) root.remove();
  document.documentElement.style.overflow = '';
}

function openGvsModal() {
  removeGvsModal();

  const root = document.createElement('div');
  root.id = '__gvs_modal_root';
  root.style.cssText = [
    'position:fixed',
    'inset:0',
    'z-index:2147483647',
    'background:rgba(0,0,0,.48)',
    'display:flex',
    'align-items:center',
    'justify-content:center',
    'padding:18px',
  ].join(';');

  const panel = document.createElement('div');
  panel.style.cssText = [
    'width:min(96vw,1800px)',
    'height:min(94vh,1200px)',
    'background:#0b0d12',
    'border:1px solid #242a3d',
    'border-radius:14px',
    'overflow:hidden',
    'position:relative',
    'box-shadow:0 20px 60px rgba(0,0,0,.45)',
  ].join(';');

  const closeBtn = document.createElement('button');
  closeBtn.textContent = 'x';
  closeBtn.style.cssText = [
    'position:absolute',
    'top:10px',
    'right:10px',
    'width:32px',
    'height:32px',
    'border-radius:8px',
    'border:1px solid #242a3d',
    'background:#111522',
    'color:#e7ecff',
    'cursor:pointer',
    'font-weight:700',
    'z-index:2',
  ].join(';');
  closeBtn.addEventListener('click', removeGvsModal);

  const frame = document.createElement('iframe');
  frame.src = chrome.runtime.getURL('ui.html');
  frame.style.cssText = 'width:100%;height:100%;border:0;display:block;background:#0b0d12;';

  panel.appendChild(closeBtn);
  panel.appendChild(frame);
  root.appendChild(panel);
  root.addEventListener('click', (e) => { if (e.target === root) removeGvsModal(); });
  document.addEventListener('keydown', function onEsc(e) {
    if (e.key === 'Escape') {
      removeGvsModal();
      document.removeEventListener('keydown', onEsc);
    }
  });

  document.documentElement.style.overflow = 'hidden';
  document.documentElement.appendChild(root);
}

async function createMediaPost(prompt, headers) {
  const res = await fetch('https://grok.com/rest/media/post/create', {
    method: 'POST',
    headers,
    credentials: 'include',
    body: JSON.stringify({ mediaType: 'MEDIA_POST_TYPE_VIDEO', prompt }),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`POST /media/post/create thất bại: ${res.status} ${text.slice(0, 500)}`);
  return JSON.parse(text);
}

async function createConversationAndRead(prompt, parentPostId, aspectRatio, videoLength, resolutionName, headers) {
  const body = {
    temporary: true,
    modelName: 'grok-3',
    message: `${prompt} --mode=custom`,
    toolOverrides: { videoGen: true },
    enableSideBySide: true,
    responseMetadata: {
      experiments: [],
      modelConfigOverride: {
        modelMap: {
          videoGenModelConfig: { parentPostId, aspectRatio, videoLength, resolutionName },
        },
      },
    },
  };

  const res = await fetch('https://grok.com/rest/app-chat/conversations/new', {
    method: 'POST',
    headers,
    credentials: 'include',
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`POST /conversations/new thất bại: ${res.status} ${text.slice(0, 800)}`);

  // NDJSON stream: nếu fetch trả text đầy đủ thì parse từng dòng
  const lines = text.split('\n').map(s => s.trim()).filter(Boolean);
  let last = null;
  for (const ln of lines) {
    try { last = JSON.parse(ln); } catch {}
  }
  return { raw: text, last };
}

function extractMp4Candidates(text) {
  const src = String(text || '');
  const re = /https:\/\/assets\.grok\.com\/users\/[^\s"'<>]+\/generated\/[^\s"'<>]+\/generated_video\.mp4[^\s"'<>]*/g;
  return Array.from(new Set(src.match(re) || []));
}

function pickLatestCapturedHeaders(urlPart, method = 'POST') {
  const wantedMethod = String(method || '').toUpperCase();
  for (const item of __gvs.captures) {
    if (!item || item.kind !== 'request') continue;
    if (String(item.method || '').toUpperCase() !== wantedMethod) continue;
    if (!String(item.url || '').includes(urlPart)) continue;
    if (item.headers && typeof item.headers === 'object') return item.headers;
  }
  return {};
}

function randomHex(n) {
  let s = '';
  while (s.length < n) s += Math.floor(Math.random() * 16).toString(16);
  return s.slice(0, n);
}

function buildDynamicHeadersFromCaptures() {
  const hNew = pickLatestCapturedHeaders('/rest/app-chat/conversations/new', 'POST');
  const hCreate = pickLatestCapturedHeaders('/rest/media/post/create', 'POST');
  const src = mergeHeadersCI(hNew, hCreate);
  const out = {};

  const passthrough = [
    'x-statsig-id',
    'x-xai-request-id',
    'baggage',
    'sentry-trace',
    'traceparent',
    'accept-language',
    'priority',
  ];
  for (const k of passthrough) {
    if (src[k]) out[k] = src[k];
  }

  if (!out['x-xai-request-id']) {
    try {
      out['x-xai-request-id'] = (globalThis.crypto && crypto.randomUUID) ? crypto.randomUUID() : `${Date.now()}-${randomHex(12)}`;
    } catch {
      out['x-xai-request-id'] = `${Date.now()}-${randomHex(12)}`;
    }
  }
  if (!out['traceparent']) out['traceparent'] = `00-${randomHex(32)}-${randomHex(16)}-00`;
  if (!out['sentry-trace']) out['sentry-trace'] = `${randomHex(32)}-${randomHex(16)}-0`;

  return out;
}

chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (msg && msg.type === 'GVS_TOGGLE_MODAL') {
    const exists = !!document.getElementById('__gvs_modal_root');
    if (exists) removeGvsModal();
    else openGvsModal();
    sendResponse({ ok: true, opened: !exists });
    return;
  }

  if (msg && msg.type === 'GVS_SET_CONFIG') {
    (async () => {
      try {
        await setConfig(msg.payload || {});
        sendResponse({ ok: true });
      } catch (e) {
        sendResponse({ ok: false, error: String(e && e.message ? e.message : e) });
      }
    })();
    return true;
  }

  if (msg && msg.type === 'GVS_GET_SNAPSHOT') {
    sendResponse({
      ok: true,
      snapshot: {
        url: location.href,
        config: __gvs.config,
        captures: __gvs.captures.slice(0, 30),
      },
    });
    return;
  }

  if (msg && msg.type === 'GVS_SEARCH_TEXT') {
    try {
      const q = String(msg.payload?.query || '').trim().toLowerCase();
      if (!q) return sendResponse({ ok: false, error: 'Query rỗng' });
      const all = Array.from(document.querySelectorAll('button,a,div,span,p,label,h1,h2,h3,input,textarea'));
      const hits = [];
      for (const e of all) {
        const t = (e.innerText || e.textContent || '').trim();
        if (!t) continue;
        if (t.toLowerCase().includes(q)) {
          hits.push({ text: t.slice(0, 160), selector: cssPath(e) });
          if (hits.length >= 10) break;
        }
      }
      if (hits[0] && hits[0].selector) {
        try {
          const target = document.querySelector(hits[0].selector);
          if (target) target.scrollIntoView({ block: 'center', behavior: 'instant' });
        } catch {}
      }
      sendResponse({ ok: true, result: { count: hits.length, hits } });
    } catch (e) {
      sendResponse({ ok: false, error: String(e && e.message ? e.message : e) });
    }
    return;
  }

  if (msg && msg.type === 'GVS_RUN_JS') {
    (async () => {
      try {
        const code = String(msg.payload?.code || '');
        const params = (msg.payload && msg.payload.params && typeof msg.payload.params === 'object')
          ? msg.payload.params
          : {};
        const intervalMs = Number(msg.payload?.intervalMs || 0);
        const jobId = String(msg.payload?.jobId || 'job1');

        if (intervalMs > 0) {
          if (__gvs.jobs[jobId]) clearInterval(__gvs.jobs[jobId]);
          __gvs.jobs[jobId] = setInterval(() => {
            try { (0, eval)(`(()=>{ const params = ${JSON.stringify(params)};\n${code}\n})()`); } catch {}
          }, Math.max(100, intervalMs));
          sendResponse({ ok: true, result: { jobId, intervalMs, started: true } });
          return;
        }

        // one-shot eval in content-script context
        let r;
        try { r = (0, eval)(`(async ()=>{ const params = ${JSON.stringify(params)};\n${code}\n})()`); } catch (e) { throw e; }
        if (r && typeof r.then === 'function') r = await r;
        sendResponse({ ok: true, result: r });
      } catch (e) {
        sendResponse({ ok: false, error: String(e && e.message ? e.message : e) });
      }
    })();
    return true;
  }

  if (msg && msg.type === 'GVS_FLOW_GENERATE') {
    (async () => {
      try {
        const p = msg.payload || {};
        const prompt = String(p.prompt || '').trim();
        if (!prompt) throw new Error('Prompt rong');

        const dynamicHeaders = buildDynamicHeadersFromCaptures();
        const headers = {
          'content-type': 'application/json',
          'accept': '*/*',
          'origin': 'https://grok.com',
          'referer': 'https://grok.com/imagine',
          ...dynamicHeaders,
        };

        const post = await createMediaPost(prompt, headers);
        const postId = post?.post?.id;
        if (!postId) throw new Error('Khong lay duoc postId tu /media/post/create');

        const convo = await createConversationAndRead(
          prompt,
          postId,
          p.aspectRatio || '9:16',
          Number(p.videoLength || 10),
          p.resolution || '720p',
          headers
        );

        const mp4Candidates = extractMp4Candidates(convo?.raw || '');
        sendResponse({
          ok: true,
          result: {
            step: 'create->new->mp4',
            postId,
            mp4Candidates,
            usedHeaders: {
              hasStatsig: !!headers['x-statsig-id'],
              hasXaiRequestId: !!headers['x-xai-request-id'],
              hasBaggage: !!headers['baggage'],
              hasSentryTrace: !!headers['sentry-trace'],
              hasTraceparent: !!headers['traceparent'],
            },
            post,
            convoLast: convo?.last || null,
            convoRawLength: String(convo?.raw || '').length,
          },
        });
      } catch (e) {
        sendResponse({ ok: false, error: String(e && e.message ? e.message : e) });
      }
    })();
    return true;
  }

  if (msg && msg.type === 'GVS_STOP_JS') {
    const jobId = String(msg.payload?.jobId || 'job1');
    if (__gvs.jobs[jobId]) {
      clearInterval(__gvs.jobs[jobId]);
      delete __gvs.jobs[jobId];
    }
    sendResponse({ ok: true });
    return;
  }

  if (!msg || msg.type !== 'GVS_GENERATE') return;
  (async () => {
    try {
      const p = msg.payload || {};
      installNetHooksOnce();
      const headerParamObj = parseHeaderParam(p.headerParam);
      const cookieParsed = parseCookieInput(p.cookieInput);

      const cookie = (cookieParsed.cookie || '').trim();
      if (cookie) await setCookiesFromString(cookie);

      const merged = mergeHeadersCI(headerParamObj, cookieParsed.headers, {
        'content-type': 'application/json',
        'accept': '*/*',
        'origin': 'https://grok.com',
        'referer': 'https://grok.com/imagine',
      });

      const headers = sanitizeHeadersForFetch(merged);

      const post = await createMediaPost(p.prompt, headers);
      const postId = post?.post?.id;
      if (!postId) throw new Error('Không lấy được postId từ bước 1');

      const convo = await createConversationAndRead(
        p.prompt,
        postId,
        p.aspectRatio || '9:16',
        Number(p.videoLength || 10),
        p.resolution || '720p',
        headers
      );

      sendResponse({ ok: true, result: { post, convo } });
    } catch (e) {
      sendResponse({ ok: false, error: String(e && e.message ? e.message : e) });
    }
  })();
  return true; // keep channel open
});

