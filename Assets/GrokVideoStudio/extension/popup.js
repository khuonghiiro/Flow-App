const els = {
  promptInput: document.getElementById('promptInput'),
  aspectRatio: document.getElementById('aspectRatio'),
  videoLength: document.getElementById('videoLength'),
  resolution: document.getElementById('resolution'),
  generateBtn: document.getElementById('generateBtn'),
  status: document.getElementById('status'),
  result: document.getElementById('result'),

  blockRules: document.getElementById('blockRules'),
  applyNetBtn: document.getElementById('applyNetBtn'),
  refreshNetBtn: document.getElementById('refreshNetBtn'),
  netStatus: document.getElementById('netStatus'),
  netOut: document.getElementById('netOut'),

  injectJs: document.getElementById('injectJs'),
  jsInterval: document.getElementById('jsInterval'),
  jsJobId: document.getElementById('jsJobId'),
  runJsBtn: document.getElementById('runJsBtn'),
  stopJsBtn: document.getElementById('stopJsBtn'),
  jsStatus: document.getElementById('jsStatus'),
  jsOut: document.getElementById('jsOut'),

  openUiBtn: document.getElementById('openUiBtn'),
};

function setStatus(text, isErr = false) {
  els.status.textContent = text || '';
  els.status.classList.toggle('err', !!isErr);
}

async function loadState() {
  const st = await chrome.storage.local.get([
    'lastPrompt', 'aspectRatio', 'videoLength', 'resolution',
    'blockRules', 'injectJs', 'jsInterval', 'jsJobId'
  ]);
  if (st.lastPrompt) els.promptInput.value = st.lastPrompt;
  if (st.aspectRatio) els.aspectRatio.value = st.aspectRatio;
  if (st.videoLength) els.videoLength.value = String(st.videoLength);
  if (st.resolution) els.resolution.value = st.resolution;
  if (st.blockRules) els.blockRules.value = st.blockRules;
  if (st.injectJs) els.injectJs.value = st.injectJs;
  if (st.jsInterval != null) els.jsInterval.value = String(st.jsInterval);
  if (st.jsJobId) els.jsJobId.value = st.jsJobId;
}

async function saveState() {
  await chrome.storage.local.set({
    lastPrompt: els.promptInput.value,
    aspectRatio: els.aspectRatio.value,
    videoLength: parseInt(els.videoLength.value, 10),
    resolution: els.resolution.value,

    blockRules: els.blockRules.value,
    injectJs: els.injectJs.value,
    jsInterval: parseInt(els.jsInterval.value || '0', 10) || 0,
    jsJobId: els.jsJobId.value,
  });
}

async function getOrCreateGrokTab() {
  const tabs = await chrome.tabs.query({ url: ['https://grok.com/*'] });
  if (tabs && tabs.length > 0) return tabs[0];
  return await chrome.tabs.create({ url: 'https://grok.com/imagine', active: true });
}

function sendToTab(tabId, msg) {
  return new Promise((resolve, reject) => {
    chrome.tabs.sendMessage(tabId, msg, (resp) => {
      const err = chrome.runtime.lastError;
      if (err) return reject(err);
      resolve(resp);
    });
  });
}

function setSmallStatus(el, text, isErr = false) {
  el.textContent = text || '';
  el.classList.toggle('err', !!isErr);
}

function parseJsonTextarea(raw, fallback) {
  const s = String(raw || '').trim();
  if (!s) return fallback;
  const obj = JSON.parse(s);
  return obj;
}

function getGenerateParams() {
  return {
    prompt: (els.promptInput.value || '').trim(),
    aspectRatio: els.aspectRatio.value,
    videoLength: parseInt(els.videoLength.value, 10),
    resolution: els.resolution.value,
  };
}

async function generate() {
  const prompt = els.promptInput.value.trim();
  if (!prompt) {
    setStatus('Vui lòng nhập prompt.', true);
    return;
  }

  await saveState();
  els.generateBtn.disabled = true;
  els.result.textContent = '';
  setStatus('Đang mở tab grok.com/imagine…');

  try {
    const tab = await getOrCreateGrokTab();
    setStatus('Đang gửi yêu cầu sang tab…');

    const resp = await sendToTab(tab.id, {
      type: 'GVS_GENERATE',
      payload: getGenerateParams(),
    });

    if (!resp || !resp.ok) {
      const msg = (resp && resp.error) ? resp.error : 'Unknown error';
      setStatus(msg, true);
      return;
    }

    setStatus('OK');
    els.result.textContent = JSON.stringify(resp.result, null, 2);
  } catch (e) {
    setStatus(String(e && e.message ? e.message : e), true);
  } finally {
    els.generateBtn.disabled = false;
  }
}

els.generateBtn.addEventListener('click', generate);

// tabs
document.querySelectorAll('.tab').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(b => b.classList.toggle('active', b === btn));
    const key = btn.getAttribute('data-tab');
    document.querySelectorAll('[data-tab-body]').forEach(p => {
      p.classList.toggle('hidden', p.getAttribute('data-tab-body') !== key);
    });
  });
});

async function applyNet() {
  await saveState();
  setSmallStatus(els.netStatus, 'Đang apply…');
  try {
    const tab = await getOrCreateGrokTab();
    const rules = parseJsonTextarea(els.blockRules.value, []);
    const resp = await sendToTab(tab.id, { type: 'GVS_SET_CONFIG', payload: { blockingRules: rules } });
    if (!resp || !resp.ok) throw new Error(resp?.error || 'Apply failed');
    setSmallStatus(els.netStatus, 'OK');
  } catch (e) {
    setSmallStatus(els.netStatus, String(e && e.message ? e.message : e), true);
  }
}

async function refreshNet() {
  setSmallStatus(els.netStatus, 'Đang lấy snapshot…');
  try {
    const tab = await getOrCreateGrokTab();
    const resp = await sendToTab(tab.id, { type: 'GVS_GET_SNAPSHOT' });
    if (!resp || !resp.ok) throw new Error(resp?.error || 'Snapshot failed');
    els.netOut.textContent = JSON.stringify(resp.snapshot, null, 2);
    setSmallStatus(els.netStatus, 'OK');
  } catch (e) {
    setSmallStatus(els.netStatus, String(e && e.message ? e.message : e), true);
  }
}

async function runJs() {
  await saveState();
  setSmallStatus(els.jsStatus, 'Đang chạy…');
  els.jsOut.textContent = '';
  try {
    const tab = await getOrCreateGrokTab();
    const code = els.injectJs.value || '';
    const intervalMs = parseInt(els.jsInterval.value || '0', 10) || 0;
    const jobId = (els.jsJobId.value || 'job1').trim();
    const resp = await sendToTab(tab.id, {
      type: 'GVS_RUN_JS',
      payload: { code, intervalMs, jobId, params: getGenerateParams() }
    });
    if (!resp || !resp.ok) throw new Error(resp?.error || 'Run failed');
    els.jsOut.textContent = JSON.stringify(resp.result, null, 2);
    setSmallStatus(els.jsStatus, 'OK');
  } catch (e) {
    setSmallStatus(els.jsStatus, String(e && e.message ? e.message : e), true);
  }
}

async function stopJs() {
  await saveState();
  setSmallStatus(els.jsStatus, 'Đang dừng…');
  try {
    const tab = await getOrCreateGrokTab();
    const jobId = (els.jsJobId.value || 'job1').trim();
    const resp = await sendToTab(tab.id, { type: 'GVS_STOP_JS', payload: { jobId } });
    if (!resp || !resp.ok) throw new Error(resp?.error || 'Stop failed');
    setSmallStatus(els.jsStatus, 'Stopped');
  } catch (e) {
    setSmallStatus(els.jsStatus, String(e && e.message ? e.message : e), true);
  }
}

els.applyNetBtn.addEventListener('click', applyNet);
els.refreshNetBtn.addEventListener('click', refreshNet);
els.runJsBtn.addEventListener('click', runJs);
els.stopJsBtn.addEventListener('click', stopJs);

els.openUiBtn.addEventListener('click', async () => {
  try {
    await chrome.tabs.create({ url: chrome.runtime.getURL('ui.html'), active: true });
  } catch {
    // ignore
  }
});

loadState().catch(() => {});

