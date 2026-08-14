const el = (id) => document.getElementById(id);

const DEFAULT_FLOW_JS = `const headers = {
  "accept": "*/*",
  "content-type": "application/json",
  "origin": "https://grok.com",
  "referer": "https://grok.com/imagine"
};

const createBody = { mediaType: "MEDIA_POST_TYPE_VIDEO", prompt: params.prompt };
const createRes = await fetch("https://grok.com/rest/media/post/create", {
  method: "POST",
  headers,
  credentials: "include",
  body: JSON.stringify(createBody)
});
const createText = await createRes.text();
if (!createRes.ok) throw new Error("create fail: " + createRes.status + " " + createText.slice(0, 500));
const createJson = JSON.parse(createText);
const postId = createJson?.post?.id;
if (!postId) throw new Error("Missing postId from /media/post/create");

const convoBody = {
  temporary: true,
  modelName: "grok-3",
  message: (params.prompt || "") + " --mode=custom",
  toolOverrides: { videoGen: true },
  enableSideBySide: true,
  responseMetadata: {
    experiments: [],
    modelConfigOverride: {
      modelMap: {
        videoGenModelConfig: {
          parentPostId: postId,
          aspectRatio: params.aspectRatio || "9:16",
          videoLength: Number(params.videoLength || 10),
          resolutionName: params.resolution || "720p"
        }
      }
    }
  }
};

const convoRes = await fetch("https://grok.com/rest/app-chat/conversations/new", {
  method: "POST",
  headers,
  credentials: "include",
  body: JSON.stringify(convoBody)
});
const convoText = await convoRes.text();
if (!convoRes.ok) throw new Error("conversations/new fail: " + convoRes.status + " " + convoText.slice(0, 800));

const mp4Regex = /https:\\/\\/assets\\.grok\\.com\\/users\\/[^\\s"'<>]+\\/generated\\/[^\\s"'<>]+\\/generated_video\\.mp4[^\\s"'<>]*/g;
const mp4Candidates = Array.from(new Set(convoText.match(mp4Regex) || []));

let lastJson = null;
for (const line of convoText.split("\\n").map(s => s.trim()).filter(Boolean)) {
  try { lastJson = JSON.parse(line); } catch {}
}

return {
  step: "create->new->mp4",
  postId,
  create: createJson,
  ndjsonLast: lastJson,
  mp4Candidates,
  rawLength: convoText.length
};`;

const els = {
  openGrokBtn: el('openGrokBtn'),
  focusGrok: el('focusGrok'),
  promptInput: el('promptInput'),
  aspectRatio: el('aspectRatio'),
  videoLength: el('videoLength'),
  resolution: el('resolution'),
  searchText: el('searchText'),
  injectJs: el('injectJs'),
  blockRules: el('blockRules'),
  executeBtn: el('executeBtn'),
  resetJsBtn: el('resetJsBtn'),
  clearVideosBtn: el('clearVideosBtn'),
  status: el('status'),
  rightOut: el('rightOut'),
  videoList: el('videoList'),
  eventLog: el('eventLog'),
};

let selectedFn = 'defaultFlow';
let videos = [];

function setStatus(node, text, isErr = false) {
  node.textContent = text || '';
  node.classList.toggle('err', !!isErr);
}

function logLine(text) {
  const ts = new Date().toLocaleTimeString();
  const prev = els.eventLog.textContent || '';
  els.eventLog.textContent = `[${ts}] ${text}\n${prev}`.slice(0, 30000);
}

function parseJson(raw, fallback) {
  const s = String(raw || '').trim();
  if (!s) return fallback;
  return JSON.parse(s);
}

function getParams() {
  return {
    prompt: (els.promptInput.value || '').trim(),
    aspectRatio: els.aspectRatio.value,
    videoLength: parseInt(els.videoLength.value, 10) || 10,
    resolution: els.resolution.value,
  };
}

async function getOrCreateGrokTab(active) {
  const tabs = await chrome.tabs.query({ url: ['https://grok.com/*'] });
  if (tabs && tabs.length > 0) {
    if (active) await chrome.tabs.update(tabs[0].id, { active: true });
    return tabs[0];
  }
  return await chrome.tabs.create({ url: 'https://grok.com/imagine', active: !!active });
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

function renderVideos() {
  if (!videos.length) {
    els.videoList.innerHTML = '<div class="video-meta">Chua co video nao.</div>';
    return;
  }
  els.videoList.innerHTML = videos.map((v) => {
    const playable = (v.urls || []).find((u) => /\.mp4(\?|$)/i.test(u));
    const links = (v.urls || []).map((u) => `<a href="${u}" target="_blank" rel="noreferrer">open</a>`).join('');
    return `
      <article class="video-item">
        ${playable ? `<video controls src="${playable}"></video>` : '<div class="video-meta">Dang doi URL mp4...</div>'}
        <div class="video-meta">${new Date(v.createdAt).toLocaleString()}</div>
        <div class="video-meta">${(v.prompt || '').slice(0, 140)}</div>
        <div class="video-links">${links}</div>
      </article>
    `;
  }).join('');
}

function addVideo(prompt, urls) {
  const clean = Array.from(new Set((urls || []).filter(Boolean)));
  videos.unshift({
    id: `${Date.now()}_${Math.random().toString(16).slice(2, 8)}`,
    createdAt: Date.now(),
    prompt,
    urls: clean,
  });
  videos = videos.slice(0, 100);
  renderVideos();
}

async function loadState() {
  const st = await chrome.storage.local.get([
    'lastPrompt', 'aspectRatio', 'videoLength', 'resolution',
    'blockRules', 'focusGrok', 'injectJs', 'gvsVideos'
  ]);
  els.promptInput.value = st.lastPrompt || '';
  els.aspectRatio.value = st.aspectRatio || '9:16';
  els.videoLength.value = String(st.videoLength || 10);
  els.resolution.value = st.resolution || '720p';
  els.searchText.value = '';
  els.blockRules.value = st.blockRules || '';
  els.focusGrok.checked = !!st.focusGrok;
  els.injectJs.value = st.injectJs || DEFAULT_FLOW_JS;
  videos = Array.isArray(st.gvsVideos) ? st.gvsVideos : [];
  renderVideos();
}

async function saveState() {
  await chrome.storage.local.set({
    lastPrompt: els.promptInput.value,
    aspectRatio: els.aspectRatio.value,
    videoLength: parseInt(els.videoLength.value, 10) || 10,
    resolution: els.resolution.value,
    blockRules: els.blockRules.value,
    focusGrok: els.focusGrok.checked,
    injectJs: els.injectJs.value,
    gvsVideos: videos.slice(0, 100),
  });
}

async function runSelected() {
  await saveState();
  setStatus(els.status, 'Dang thuc thi...');
  els.rightOut.textContent = '';
  try {
    const params = getParams();
    const tab = await getOrCreateGrokTab(els.focusGrok.checked);
    let resp;

    if (selectedFn === 'applyNet') {
      const rules = parseJson(els.blockRules.value, []);
      resp = await sendToTab(tab.id, { type: 'GVS_SET_CONFIG', payload: { blockingRules: rules } });
      if (!resp || !resp.ok) throw new Error(resp?.error || 'Apply block rules failed');
      logLine('Da apply chan request');
    } else if (selectedFn === 'search') {
      resp = await sendToTab(tab.id, { type: 'GVS_SEARCH_TEXT', payload: { query: (els.searchText.value || '').trim() } });
      if (!resp || !resp.ok) throw new Error(resp?.error || 'Search failed');
      logLine('Search xong');
    } else if (selectedFn === 'snapshot') {
      resp = await sendToTab(tab.id, { type: 'GVS_GET_SNAPSHOT' });
      if (!resp || !resp.ok) throw new Error(resp?.error || 'Snapshot failed');
      logLine('Lay snapshot xong');
    } else {
      if (!params.prompt) throw new Error('Vui long nhap prompt');
      if (selectedFn === 'defaultFlow') {
        resp = await sendToTab(tab.id, { type: 'GVS_FLOW_GENERATE', payload: params });
        if (!resp || !resp.ok) throw new Error(resp?.error || 'Flow create->new fail');
      } else {
        const code = els.injectJs.value || '';
        resp = await sendToTab(tab.id, {
          type: 'GVS_RUN_JS',
          payload: { code, intervalMs: 0, jobId: 'single_run', params },
        });
        if (!resp || !resp.ok) throw new Error(resp?.error || 'Run JS failed');
      }
      const data = resp.result || {};
      const mp4s = Array.isArray(data.mp4Candidates) ? data.mp4Candidates : [];
      addVideo(params.prompt, mp4s);
      logLine(`Flow xong. Tim thay ${mp4s.length} link mp4`);
    }

    const out = resp.result || resp.snapshot || resp;
    els.rightOut.textContent = JSON.stringify(out, null, 2);
    setStatus(els.status, 'OK');
  } catch (e) {
    const msg = String(e && e.message ? e.message : e);
    setStatus(els.status, msg, true);
    logLine(`Loi: ${msg}`);
  }
}

els.openGrokBtn.addEventListener('click', async () => {
  try { await getOrCreateGrokTab(true); } catch {}
});

els.executeBtn.addEventListener('click', runSelected);
els.resetJsBtn.addEventListener('click', () => { els.injectJs.value = DEFAULT_FLOW_JS; });
els.clearVideosBtn.addEventListener('click', async () => {
  videos = [];
  renderVideos();
  await saveState();
  logLine('Da clear video list');
});

document.querySelectorAll('.menu').forEach((btn) => {
  btn.addEventListener('click', () => {
    const key = btn.getAttribute('data-view');
    document.querySelectorAll('.menu').forEach((m) => m.classList.toggle('active', m === btn));
    document.querySelectorAll('[data-view-body]').forEach((panel) => {
      panel.classList.toggle('hidden', panel.getAttribute('data-view-body') !== key);
    });
  });
});

document.querySelectorAll('.fn-btn').forEach((btn) => {
  btn.addEventListener('click', () => {
    selectedFn = btn.getAttribute('data-fn') || 'defaultFlow';
    document.querySelectorAll('.fn-btn').forEach((f) => f.classList.toggle('active', f === btn));
  });
});

loadState().catch(() => {});

