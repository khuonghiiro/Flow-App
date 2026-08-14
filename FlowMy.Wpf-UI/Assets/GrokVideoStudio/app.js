/* ── app.js ── Grok Video Studio ── */

// ─── STATE ───────────────────────────────────────────────────────────────────
const state = {
  videos: [],
  processing: 0,
};

// ─── DOM REFS ─────────────────────────────────────────────────────────────────
const videoGrid = document.getElementById('videoGrid');
const emptyState = document.getElementById('emptyState');
const totalCountEl = document.getElementById('totalCount');
const processingCountEl = document.getElementById('processingCount');
const errorMsgEl = document.getElementById('errorMsg');
const generateBtn = document.getElementById('generateBtn');

// ─── UTILS ───────────────────────────────────────────────────────────────────
function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.random() * 16 | 0;
    return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
  });
}

/**
 * ✅ Tab2→Tab1 JS injection helpers
 * tab1Seq được inject bởi HtmlUiNodeControl khi UseWebTab = true.
 * Safe wrapper: nếu không chạy trong 2-tab mode thì không làm gì.
 */
function t1Seq(js) {
  if (typeof tab1Seq === 'function') tab1Seq(js);
}
function t1Par(js) {
  if (typeof tab1Par === 'function') tab1Par(js);
}

// Tab1 (return): gửi JS sang Tab1 và nhận kết quả trả về (tránh CORS origin null)
const __tab1JobPromises = new Map();
window.__tab1_last_job = null;
window.__tab1_debug_events = window.__tab1_debug_events || [];
window.__tab1_job_resolve = function(jobId, rawJson) {
  const h = __tab1JobPromises.get(jobId);
  if (!h) {
    try {
      window.__tab1_debug_events.push({ t: Date.now(), ev: 'resolve_no_handler', jobId, type: typeof rawJson });
      console.warn('[Tab2] __tab1_job_resolve but no handler', { jobId, type: typeof rawJson });
    } catch {}
    return;
  }
  const meta = h.meta || null;
  __tab1JobPromises.delete(jobId);
  try {
    try {
      const rawPreview = (typeof rawJson === 'string')
        ? rawJson.slice(0, 500)
        : JSON.stringify(rawJson).slice(0, 500);
      window.__tab1_last_job = { jobId, ok: true, type: typeof rawJson, preview: rawPreview, meta };
      window.__tab1_debug_events.push({ t: Date.now(), ev: 'resolve', jobId, type: typeof rawJson, preview: rawPreview, meta });
      console.log('[Tab2] __tab1_job_resolve', { jobId, type: typeof rawJson, preview: rawPreview, meta });
    } catch {}
    // rawJson thường là JSON-string do ExecuteScriptAsync trả về (vd: "\"{\\\"ok\\\":true}\"").
    // Nhưng ở một số đường code C# có thể truyền thẳng string "Source ...", "Success ...", "curl ..."
    // (không được JSON-quote). Khi đó JSON.parse sẽ throw: Unexpected token 'S'...
    // => Chỉ JSON.parse khi input "trông giống JSON", còn lại giữ nguyên.
    let v = rawJson;
    if (typeof rawJson === 'string') {
      const t0 = rawJson.trim();
      const looksJson =
        t0 === '' ||
        t0 === 'null' || t0 === 'true' || t0 === 'false' ||
        t0[0] === '{' || t0[0] === '[' || t0[0] === '"' ||
        t0[0] === '-' || (t0[0] >= '0' && t0[0] <= '9');
      if (looksJson && t0 !== '') {
        try { v = JSON.parse(t0); } catch { v = rawJson; }
      } else {
        v = rawJson;
      }
    } else if (rawJson != null) {
      // non-string values: keep as-is
      v = rawJson;
    }
    if (typeof v === 'string') {
      const t = v.trim();
      if ((t.startsWith('{') && t.endsWith('}')) || (t.startsWith('[') && t.endsWith(']'))) {
        try { v = JSON.parse(t); } catch {}
      }
    }
    try {
      const pv = (typeof v === 'string') ? v.slice(0, 500) : JSON.stringify(v).slice(0, 500);
      window.__tab1_debug_events.push({ t: Date.now(), ev: 'resolve_parsed', jobId, parsedType: typeof v, parsedPreview: pv });
      console.log('[Tab2] __tab1_job_resolve parsed', { jobId, parsedType: typeof v, parsedPreview: pv });
    } catch {}
    h.resolve(v);
  } catch {
    // fallback: nếu rawJson không parse được thì trả nguyên
    h.resolve(rawJson);
  }
};
window.__tab1_job_reject = function(jobId, errMsg) {
  const h = __tab1JobPromises.get(jobId);
  if (!h) {
    try {
      window.__tab1_debug_events.push({ t: Date.now(), ev: 'reject_no_handler', jobId, err: String(errMsg || '') });
      console.warn('[Tab2] __tab1_job_reject but no handler', { jobId, err: String(errMsg || '') });
    } catch {}
    return;
  }
  const meta = h.meta || null;
  __tab1JobPromises.delete(jobId);
  try {
    window.__tab1_last_job = { jobId, ok: false, error: String(errMsg || ''), meta };
    try {
      window.__tab1_debug_events.push({ t: Date.now(), ev: 'reject', jobId, err: String(errMsg || ''), meta });
      console.warn('[Tab2] __tab1_job_reject', { jobId, err: String(errMsg || ''), meta });
    } catch {}
  } catch {}
  h.reject(new Error(errMsg || 'Tab1 job failed'));
};

function t1SeqRet(js, timeoutMs = 180000, meta = null) {
  return new Promise((resolve, reject) => {
    const jobId = uuid();
    // IMPORTANT: C# side may spend time starting the job + polling, so callbacks can arrive
    // slightly after our JS timeout. Keep a grace window to avoid "reject but no handler".
    const hardTimeoutMs = Math.max(1000, timeoutMs);
    const graceMs = 20000;
    const timer = setTimeout(() => {
      const h2 = __tab1JobPromises.get(jobId);
      if (h2) {
        h2.timedOut = true;
        h2.timedOutAt = Date.now();
      }
      let lj = '';
      try { lj = JSON.stringify(window.__tab1_last_job || null).slice(0, 300); } catch { lj = String(window.__tab1_last_job || ''); }
      try { window.__tab1_debug_events.push({ t: Date.now(), ev: 't1SeqRet_timeout', jobId, hardTimeoutMs, graceMs, lastJob: lj }); } catch {}
      reject(new Error('Tab1 job timeout (jobId=' + jobId + ') lastJob=' + lj));
      // Delete handler after grace window (to capture late resolve/reject for debugging)
      setTimeout(() => {
        const h3 = __tab1JobPromises.get(jobId);
        if (h3 && h3.timedOut) __tab1JobPromises.delete(jobId);
      }, graceMs);
    }, hardTimeoutMs);

    __tab1JobPromises.set(jobId, {
      resolve: (v) => {
        clearTimeout(timer);
        resolve(v);
      },
      reject: (e) => {
        clearTimeout(timer);
        reject(e);
      },
      meta: meta || undefined,
      timedOut: false,
    });

    try {
      window.__tab1_debug_events.push({ t: Date.now(), ev: 't1SeqRet_start', jobId, meta });
      console.log('__TAB2_T1SEQRET_START__', { jobId, meta, jsPreview: String(js || '').slice(0, 180) });
    } catch {}

    if (typeof tab1SeqRet !== 'function') {
      clearTimeout(timer);
      __tab1JobPromises.delete(jobId);
      try { window.__tab1_debug_events.push({ t: Date.now(), ev: 't1SeqRet_missing_bridge', jobId }); } catch {}
      reject(new Error('Tab1 return bridge missing (tab1SeqRet)'));
      return;
    }

    // Mark as "sent" so we can tell if C# never calls back.
    try { window.__tab1_debug_events.push({ t: Date.now(), ev: 't1SeqRet_sent', jobId }); } catch {}
    tab1SeqRet(jobId, js, timeoutMs);
  });
}

// Sync headerParam từ bên ngoài vào textarea
window.hostLive.on('headerParam', function (headerParam) {
  document.getElementById('headerParam').value = headerParam || '';
});

function timeAgo(date) {
  const s = Math.floor((Date.now() - date) / 1000);
  if (s < 60) return `${s}s trước`;
  if (s < 3600) return `${Math.floor(s / 60)}m trước`;
  return `${Math.floor(s / 3600)}h trước`;
}

function showError(msg) {
  errorMsgEl.textContent = msg;
  errorMsgEl.style.display = 'block';
  setTimeout(() => { errorMsgEl.style.display = 'none'; }, 7000);
}

/**
 * Parse headerParam JSON — dùng để bổ sung các header đặc thù cho từng API.
 * Cookie KHÔNG cần truyền ở đây vì credentials: 'include' lo rồi.
 */
function parseHeaderParam() {
  const raw = document.getElementById('headerParam').value.trim();
  if (!raw) return {};
  try {
    return JSON.parse(raw);
  } catch (e) {
    showError('headerParam JSON không hợp lệ: ' + e.message);
    return null;
  }
}

/**
 * Remove/ignore headers that fetch() cannot set in browsers/WebView2.
 * We also drop Cookie intentionally (credentials: 'include' will handle it).
 */
function sanitizeExtraHeaders(extraHeaders) {
  const out = {};
  if (!extraHeaders) return out;

  for (const [k, v] of Object.entries(extraHeaders)) {
    if (v === undefined || v === null) continue;
    const lk = String(k).toLowerCase();

    // Cookie must not be set manually
    if (lk === 'cookie') continue;

    // Forbidden / browser-controlled request headers
    if (lk === 'user-agent') continue;
    if (lk === 'accept-encoding') continue;
    if (lk === 'content-length') continue;
    if (lk === 'host') continue;
    if (lk === 'connection') continue;

    // Client hints / fetch metadata are controlled by the browser
    if (lk.startsWith('sec-ch-ua')) continue;
    if (lk.startsWith('sec-fetch-')) continue;

    out[k] = v;
  }

  return out;
}

function getDefaultRequestHeaders() {
  // Keep defaults minimal but include headers that Grok sometimes expects in practice.
  const lang = (typeof navigator !== 'undefined' && navigator.language) ? navigator.language : 'en-US';
  const acceptLang = lang ? `${lang},en;q=0.9` : 'en-US,en;q=0.9';

  return {
    'Accept': '*/*',
    'Accept-Language': acceptLang,
    'Content-Type': 'application/json',
  };
}

/**
 * Merge extra headers vào default headers.
 * Extra headers thắng nếu trùng key (case-insensitive).
 * Cookie bị loại bỏ khỏi extraHeaders — để credentials: 'include' xử lý.
 */
function buildHeaders(defaults, extraHeaders) {
  const out = {};

  // Nhét defaults trước
  for (const [k, v] of Object.entries(defaults)) {
    if (v === undefined || v === null) continue;
    out[k.toLowerCase()] = { key: k, val: v };
  }

  // Extra headers override (bỏ qua cookie)
  if (extraHeaders) {
    for (const [k, v] of Object.entries(extraHeaders)) {
      if (v === undefined || v === null) continue;
      const lk = k.toLowerCase();
      if (lk === 'cookie') continue; // browser tự lo qua credentials: 'include'
      out[lk] = { key: k, val: v };
    }
  }

  // Gom lại thành object thường
  const result = {};
  for (const { key, val } of Object.values(out)) {
    result[key] = val;
  }
  return result;
}

function updateStats() {
  totalCountEl.textContent = `${state.videos.length} video${state.videos.length !== 1 ? 's' : ''}`;
  if (state.processing > 0) {
    processingCountEl.style.display = 'inline';
    processingCountEl.textContent = `● ${state.processing} đang xử lý`;
  } else {
    processingCountEl.style.display = 'none';
  }
  emptyState.style.display = state.videos.length === 0 ? 'flex' : 'none';
}

function getAspectClass(ratio) {
  if (ratio === '16:9') return 'landscape';
  if (ratio === '1:1') return 'square';
  return '';
}

// ─── CARD RENDERING ──────────────────────────────────────────────────────────
function escHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function cardHTML(item) {
  const aspectClass = getAspectClass(item.aspect);
  const created = timeAgo(item.createdAt);

  if (item.status === 'generating') {
    return `
      <div class="card-media ${aspectClass}">
        ${item.thumbnailUrl
        ? `<img class="card-thumbnail" src="${item.thumbnailUrl}" alt="" onerror="this.style.display='none'">`
        : `<div style="width:100%;height:100%;background:var(--surface2)"></div>`}
        <div class="card-progress-overlay" id="overlay-${item.id}">
          <div class="progress-label">● ĐANG TẠO VIDEO</div>
          <div class="progress-pct" id="pct-${item.id}">${item.progress}%</div>
          <div class="progress-track">
            <div class="progress-fill" id="fill-${item.id}" style="width:${item.progress}%"></div>
          </div>
          <div class="progress-status" id="status-${item.id}">Khởi tạo...</div>
        </div>
        <div class="card-status generating">ĐANG XỬ LÝ</div>
      </div>
      <div class="card-body">
        <div class="card-prompt">${escHtml(item.prompt)}</div>
        <div class="card-meta">
          <div class="card-tags">
            <span class="tag">${item.aspect}</span>
            <span class="tag">${item.length}s</span>
            <span class="tag">${item.res}</span>
          </div>
          <span class="card-time">${created}</span>
        </div>
        <div class="card-actions">
          <button class="card-btn" disabled>⏸ Xem</button>
          <button class="card-btn" disabled>↓ Tải</button>
        </div>
      </div>`;
  }

  if (item.status === 'done') {
    return `
      <div class="card-media ${aspectClass}" onclick="openModal('${item.id}')">
        ${item.thumbnailUrl
        ? `<img class="card-thumbnail" src="${item.thumbnailUrl}" alt="">`
        : ''}
        <video class="card-video-preview" src="${item.videoUrl}" muted loop preload="none"></video>
        <div class="card-play-overlay">
          <div class="play-btn">▶</div>
        </div>
        <div class="card-status done">HOÀN THÀNH</div>
      </div>
      <div class="card-body">
        <div class="card-prompt">${escHtml(item.prompt)}</div>
        <div class="card-meta">
          <div class="card-tags">
            <span class="tag">${item.aspect}</span>
            <span class="tag">${item.length}s</span>
            <span class="tag">${item.res}</span>
          </div>
          <span class="card-time">${created}</span>
        </div>
        <div class="card-actions">
          <button class="card-btn primary" onclick="openModal('${item.id}')">▶ Xem</button>
          <a class="card-btn" href="${item.videoUrl}" download="grok_${item.id}.mp4">↓ Tải</a>
        </div>
      </div>`;
  }

  if (item.status === 'error') {
    return `
      <div class="card-media ${aspectClass}">
        ${item.thumbnailUrl
        ? `<img class="card-thumbnail" src="${item.thumbnailUrl}" alt="">`
        : `<div style="width:100%;height:100%;background:var(--surface2)"></div>`}
        <div class="card-error-overlay">
          <div class="error-icon">⚠</div>
          <div class="error-text">${escHtml(item.errorMsg || 'Có lỗi xảy ra')}</div>
        </div>
        <div class="card-status error">LỖI</div>
      </div>
      <div class="card-body">
        <div class="card-prompt">${escHtml(item.prompt)}</div>
        <div class="card-meta">
          <div class="card-tags">
            <span class="tag">${item.aspect}</span>
            <span class="tag">${item.length}s</span>
            <span class="tag">${item.res}</span>
          </div>
          <span class="card-time">${created}</span>
        </div>
      </div>`;
  }

  return '';
}

function createCard(item) {
  const card = document.createElement('div');
  card.className = 'video-card';
  card.id = `card-${item.id}`;
  card.innerHTML = cardHTML(item);
  return card;
}

function updateCard(item) {
  const card = document.getElementById(`card-${item.id}`);
  if (!card) return;
  card.innerHTML = cardHTML(item);
}

function updateProgress(id, progress, statusText) {
  const item = state.videos.find(v => v.id === id);
  if (!item) return;
  item.progress = progress;

  const pctEl = document.getElementById(`pct-${id}`);
  const fillEl = document.getElementById(`fill-${id}`);
  const statusEl = document.getElementById(`status-${id}`);

  if (pctEl) pctEl.textContent = `${progress}%`;
  if (fillEl) fillEl.style.width = `${progress}%`;
  if (statusEl) statusEl.textContent = statusText || '';
}

// ─── MAIN GENERATE FLOW ──────────────────────────────────────────────────────
async function generateVideo() {
  const prompt = document.getElementById('promptInput').value.trim();
  const aspect = document.getElementById('aspectRatio').value;
  const length = parseInt(document.getElementById('videoLength').value);
  const res = document.getElementById('resolution').value;

  if (!prompt) {
    showError('Vui lòng nhập prompt!');
    return;
  }

  // Parse extra headers (không có cookie)
  const extraHeaders = parseHeaderParam();
  if (extraHeaders === null) return; // lỗi JSON đã hiển thị

  const item = {
    id: uuid(),
    prompt,
    aspect,
    length,
    res,
    status: 'generating',
    progress: 0,
    videoUrl: '',
    thumbnailUrl: '',
    errorMsg: '',
    createdAt: Date.now(),
  };

  state.videos.unshift(item);
  state.processing++;

  if (emptyState.parentNode === videoGrid) {
    videoGrid.removeChild(emptyState);
  }
  const card = createCard(item);
  videoGrid.prepend(card);
  updateStats();

  generateBtn.disabled = true;
  generateBtn.querySelector('.btn-label').textContent = 'ĐANG GỬI...';

  // ✅ [Tab1] Báo trạng thái đang xử lý (parallel — fire-and-forget, không cần đợi)
  t1Par(`document.title = '\u23f3 Grok Video Studio: \u0111ang gửi yêu cầu...';`);

  try {
    // Không phụ thuộc __tab1_available (flag này có thể bị “đóng băng” theo thời điểm inject).
    // Chỉ cần bridge return tồn tại là có thể thử chạy qua Tab1; nếu Tab1 chưa ready thì C# sẽ reject rõ ràng.
    const canApiInTab1 = typeof tab1SeqRet === 'function';

    // Một số máy có bridge tồn tại nhưng bị "kẹt" (Tab1 chưa ready / navigation loop / WebView2 issue).
    // Ping nhanh để quyết định dùng Tab1 hay fallback Tab2, tránh cảm giác treo.
    let useTab1 = canApiInTab1;
    if (useTab1) {
      const pingScript =
        `(function(){ try { return { ok:true, href:String(location.href||''), host:String(location.hostname||''), ready:String(document.readyState||'') }; } catch(e){ return { ok:false, error:String(e&&e.message||e) }; } })();`;

      const tryPingOnce = async (timeoutMs) => {
        return await t1SeqRet(pingScript, timeoutMs, { label: 'tab1_ping' });
      };

      let pingOk = false;
      let lastPingErr = null;

      // Try a few times (Tab1 may be navigating/initializing WebView2)
      for (let i = 0; i < 3; i++) {
        try {
          await tryPingOnce(6000);
          pingOk = true;
          break;
        } catch (e) {
          lastPingErr = e;
          await new Promise(r => setTimeout(r, 1200));
        }
      }

      // If still not ok, attempt to navigate Tab1 to grok.com/imagine and retry ping.
      if (!pingOk && typeof tab1Seq === 'function') {
        try {
          t1Seq(`(function(){ try { document.title='⏳ Opening grok.com/imagine...'; } catch(e){} try { location.href='https://grok.com/imagine'; } catch(e){} })();`);
        } catch {}

        // Give navigation time, then retry ping a few more times.
        await new Promise(r => setTimeout(r, 9000));
        for (let i = 0; i < 4; i++) {
          try {
            await tryPingOnce(8000);
            pingOk = true;
            break;
          } catch (e) {
            lastPingErr = e;
            await new Promise(r => setTimeout(r, 1500));
          }
        }
      }

      if (!pingOk) {
        useTab1 = false;
        try {
          const msg = (lastPingErr && lastPingErr.message) ? lastPingErr.message : String(lastPingErr || 'ping failed');
          console.warn('[generateVideo] Tab1 ping failed (after retries)', msg);
        } catch {}
      }
    }

    // IMPORTANT: Tab2 is loaded from local HTML (origin null) -> Grok REST endpoints are blocked by CORS.
    // So we MUST run API calls inside Tab1 (grok.com origin). If Tab1 isn't ready, fail fast with a clear error.
    if (!useTab1) {
      throw new Error(
        'Tab1 chưa sẵn sàng nên không thể gọi API Grok. ' +
        'Tab2 (origin=null) sẽ bị CORS block. ' +
        'Hãy bật UseWebTab, đảm bảo Tab1 đã vào https://grok.com/imagine và đăng nhập.'
      );
    }

    // ── STEP 1: Tạo media post ──
    updateProgress(item.id, 3, 'Bước 1/2 — Tạo yêu cầu...');

    let postId = '';
    let thumbUrl = '';
    let postStep1 = null;

    if (useTab1) {
      // Guard ensure with timeout so it can't "hang" forever
      const ensure = await Promise.race([
        ensureTab1OnGrokOrigin(),
        new Promise((_, rej) => setTimeout(() => rej(new Error('Timeout: ensureTab1OnGrokOrigin')), 45000))
      ]);
      if (!ensure?.ok) {
        let i1 = '';
        let i2 = '';
        try { i1 = JSON.stringify(ensure?.info1 || '').slice(0, 300); } catch {}
        try { i2 = JSON.stringify(ensure?.info2 || '').slice(0, 300); } catch {}
        // Không hard-fail ở bước origin check.
        // Mục tiêu: vẫn thử đẩy request chạy trong Tab1 qua fetch, để xem request thực sự chạy được không.
        // Nếu Tab1 chưa vào grok.com thì lỗi sẽ xuất hiện ở bước fetch/debug (không còn kẹt ở origin check).
        console.warn('[generateVideo] Tab1 origin check failed; will still try Tab1 requests', { i1, i2 });
      }

      try { console.log('__TAB2_STEP1_START__', { itemId: item.id }); } catch {}
      postStep1 = await createMediaPostViaTab1(prompt, extraHeaders);
      // C# bridge wraps Tab1 result as { ok: true, result: <actual> }
      const postStep1Inner = (postStep1 && typeof postStep1 === 'object' && 'result' in postStep1)
        ? postStep1.result
        : postStep1;
      postId = postStep1Inner?.postId || '';
      thumbUrl = postStep1Inner?.thumbUrl || '';
      try { console.log('__TAB2_STEP1_DONE__', { itemId: item.id, postId: postId || '', hasDebug: !!(postStep1 && postStep1.debug) }); } catch {}
    }

    if (!postId) {
    const postStep1InnerForErr = (postStep1 && typeof postStep1 === 'object' && 'result' in postStep1)
      ? postStep1.result
      : postStep1;
    const dbg = useTab1 ? (postStep1InnerForErr?.debug || '') : '';
      let p1 = '';
    if (useTab1) {
        try { p1 = JSON.stringify(postStep1).slice(0, 500); }
        catch { p1 = String(postStep1); }
      }
      let lj = '';
      try { lj = JSON.stringify(window.__tab1_last_job).slice(0, 300); } catch { lj = String(window.__tab1_last_job || ''); }
      throw new Error(
        'Không lấy được post ID từ bước 1'
        + (dbg ? (' | debug=' + dbg.slice(0, 500)) : (p1 ? (' | postStep1=' + p1) : ''))
        + (lj ? (' | lastJob=' + lj) : '')
      );
    }

    item.thumbnailUrl = thumbUrl;
    updateProgress(item.id, 8, 'Bước 1 hoàn thành — Khởi tạo hội thoại...');

    const thumbEl = document.querySelector(`#card-${item.id} .card-thumbnail`);
    if (thumbEl && thumbUrl) thumbEl.src = thumbUrl;

    // ── STEP 2: Tạo conversation & stream progress ──
    updateProgress(item.id, 10, 'Bước 2/2 — Đang tạo video...');

    let videoUrl = '';
    if (useTab1) {
      try { console.log('__TAB2_STEP2_START__', { itemId: item.id, postId, aspect, length, res }); } catch {}
      const step2 = await createConversationAndPollViaTab1(prompt, postId, aspect, length, res, extraHeaders);
      const step2Inner = (step2 && typeof step2 === 'object' && 'result' in step2)
        ? step2.result
        : step2;
      videoUrl = step2Inner?.videoUrl || '';
      try { console.log('__TAB2_STEP2_DONE__', { itemId: item.id, ok: !!videoUrl }); } catch {}
    }

    // ── DONE ──
    item.videoUrl = videoUrl;
    item.status = 'done';
    item.progress = 100;
    state.processing--;
    updateCard(item);
    updateStats();

    // ✅ [Tab1] Sequential: chờ bước trước xong rồi navigate → video URL trong Tab1
    t1Seq(`document.title = '\u2705 Video xong! \u0110ang mở...';`);
    t1Seq(`window.location.href = '${videoUrl}';`);

  } catch (err) {
    item.status = 'error';
    item.errorMsg = err.message || String(err);
    item.progress = 0;
    state.processing--;
    updateCard(item);
    updateStats();
    showError('Lỗi: ' + item.errorMsg);
    console.error('[generateVideo]', err);
    // ✅ [Tab1] Báo lỗi vào title của Tab1 (parallel, không block)
    t1Par(`document.title = '\u274c Lỗi tạo video: ${(err.message || '').slice(0, 40)}';`);
  }

  generateBtn.disabled = false;
  generateBtn.querySelector('.btn-label').textContent = 'TẠO VIDEO';
}

// ─── API CALLS ────────────────────────────────────────────────────────────────

async function ensureTab1OnGrokOrigin() {
  // Nếu không có bridge return thì không đảm bảo được origin
  if (typeof tab1SeqRet !== 'function') return false;

  // Cache ngắn để tránh mỗi request lại làm Tab1 reload
  try {
    if (window.__tab1_origin_ok_until && Date.now() < window.__tab1_origin_ok_until) {
      return { ok: true, info1: { cached: true }, info2: null };
    }
  } catch {}

  // 1) Thử đọc origin hiện tại trước (KHÔNG navigate), nếu đang ở grok.com thì return luôn
  let pre = null;
  try {
    pre = await t1SeqRet(
      `(function(){ try {
          var host = String(window.location.hostname||'');
          var origin = String(window.location.origin||'');
          return { href: String(window.location.href||''), host: host, origin: origin };
        } catch(e) { return { href:'', host:'', origin:'' }; } })();`,
      8000,
      { label: 'ensurePreInfo', url: 'grok.com' }
    );
  } catch (e) {
    pre = { error: (e && e.message) ? e.message : String(e) };
  }
  if (pre && (String(pre.host).includes('grok.com') || String(pre.origin).includes('grok.com'))) {
    try { window.__tab1_origin_ok_until = Date.now() + 60000; } catch {}
    return { ok: true, info1: pre, info2: null };
  }

  // 2) Nếu chưa phải grok.com thì mới điều hướng Tab1 về grok.com
  t1Seq(`(function(){
    try { document.title = '⏳ Switching to grok.com...'; } catch (e) {}
    try { window.location.href = 'https://grok.com/imagine'; } catch (e) {}
  })();`);

  // Đợi theo kiểu "sleep rồi read" để tránh bị kẹt do navigation đang diễn ra
  await new Promise(r => setTimeout(r, 10000));

  let info1 = null;
  let info2 = null;

  try {
    info1 = await t1SeqRet(
      `(function(){ try {
          var host = String(window.location.hostname||'');
          var origin = String(window.location.origin||'');
          try { document.title = 'Tab1 host=' + host; } catch(e) {}
          return {
            href: String(window.location.href||''),
            host: host,
            origin: origin
          };
        } catch(e) { return { href:'', host:'', origin:'' }; } })();`,
      25000,
      { label: 'ensureInfo', url: 'grok.com' }
    );
  } catch (e) {
    info1 = { error: (e && e.message) ? e.message : String(e) };
  }

  if (info1 && (String(info1.host).includes('grok.com') || String(info1.origin).includes('grok.com'))) {
    try { window.__tab1_origin_ok_until = Date.now() + 60000; } catch {}
    return { ok: true, info1, info2 };
  }

  // Retry 1 lần nữa
  await new Promise(r => setTimeout(r, 5000));
  try {
    info2 = await t1SeqRet(
      `(function(){ try {
          var host = String(window.location.hostname||'');
          var origin = String(window.location.origin||'');
          try { document.title = 'Tab1 host=' + host; } catch(e) {}
          return {
            href: String(window.location.href||''),
            host: host,
            origin: origin
          };
        } catch(e) { return { href:'', host:'', origin:'' }; } })();`,
      25000,
      { label: 'ensureInfoRetry', url: 'grok.com' }
    );
  } catch (e) {
    info2 = { error: (e && e.message) ? e.message : String(e) };
  }

  if (info2 && (String(info2.host).includes('grok.com') || String(info2.origin).includes('grok.com'))) {
    try { window.__tab1_origin_ok_until = Date.now() + 60000; } catch {}
    return { ok: true, info1, info2 };
  }

  return { ok: false, info1, info2 };
}

async function createMediaPostViaTab1(prompt, extraHeaders) {
  const job = `(async () => {
    const prompt = ${JSON.stringify(prompt)};
    const extraHeaders = ${JSON.stringify(sanitizeExtraHeaders(extraHeaders || {}))};

    // Capture logs so Tab2 can debug when a machine "hangs"
    const logs = [];
    const olog = console.log, owarn = console.warn, oerr = console.error;
    const push = (type, args) => {
      try {
        logs.push({
          t: Date.now(),
          type,
          msg: Array.from(args).map(x => {
            try { return typeof x === 'string' ? x : JSON.stringify(x); } catch { return String(x); }
          }).join(' ')
        });
      } catch {}
    };
    console.log = function(){ push('log', arguments); try { return olog.apply(console, arguments); } catch {} };
    console.warn = function(){ push('warn', arguments); try { return owarn.apply(console, arguments); } catch {} };
    console.error = function(){ push('error', arguments); try { return oerr.apply(console, arguments); } catch {} };
    try {
      const pageInfo = (() => {
        try {
          const href = String(location.href || '');
          const origin = String(location.origin || '');
          const host = String(location.hostname || '');
          let cookieLen = -1;
          try { cookieLen = String(document.cookie || '').length; } catch {}
          return { href, origin, host, cookieLen };
        } catch (e) {
          return { href: '', origin: '', host: '', cookieLen: -1, error: String((e && e.message) ? e.message : e) };
        }
      })();

      console.log('[Tab1][step1] POST /media/post/create start', pageInfo);

      const headers = {
        ${Object.entries(getDefaultRequestHeaders()).map(([k, v]) => `'${k}': ${JSON.stringify(v)}`).join(',\n        ')},
        'Referer': 'https://grok.com/imagine',
        'Origin': 'https://grok.com'
      };

      for (const [k, v] of Object.entries(extraHeaders || {})) {
        if (v === undefined || v === null) continue;
        if (String(k).toLowerCase() === 'cookie') continue;
        headers[k] = v;
      }

      // Avoid indefinite hang: enforce a hard timeout
      const ac = (typeof AbortController !== 'undefined') ? new AbortController() : null;
      const timer = ac ? setTimeout(() => { try { ac.abort(); } catch {} }, 30000) : null;

      let res;
      try {
        res = await fetch('https://grok.com/rest/media/post/create', {
          method: 'POST',
          headers,
          credentials: 'include',
          signal: ac ? ac.signal : undefined,
          body: JSON.stringify({ mediaType: 'MEDIA_POST_TYPE_VIDEO', prompt })
        });
      } finally {
        if (timer) try { clearTimeout(timer); } catch {}
      }

      const status = res.status;
      const ct = (res.headers && res.headers.get) ? (res.headers.get('content-type') || '') : '';

      // Đọc body an toàn (kể cả khi không phải JSON)
      let text = '';
      try { text = await res.text(); } catch (e) { text = ''; }

      // Thử parse JSON nếu có thể
      let json = null;
      try { json = text ? JSON.parse(text) : null; } catch (e) { json = null; }

      // Best-effort lấy postId theo vài schema phổ biến
      const postId =
        (json && json.post && json.post.id) ? json.post.id :
        (json && json.result && json.result.post && json.result.post.id) ? json.result.post.id :
        '';

      const thumbUrl =
        (json && json.post && json.post.thumbnailImageUrl) ? json.post.thumbnailImageUrl :
        (json && json.result && json.result.post && json.result.post.thumbnailImageUrl) ? json.result.post.thumbnailImageUrl :
        '';

      // Debug string (để Tab2 hiển thị nếu lỗi)
      let dbg = '';
      try {
        dbg = JSON.stringify({
          pageInfo,
          status,
          contentType: ct,
          head: String(text || '').slice(0, 1200),
          json: json ? json : undefined
        }).slice(0, 2000);
      } catch (e) {
        dbg = String(text || '').slice(0, 1200);
      }

      if (!res.ok || !postId) {
        // Heuristics: common reasons when running in WebView2 on another machine
        let hint = '';
        try {
          const headLower = String(text || '').toLowerCase();
          const ctLower = String(ct || '').toLowerCase();
          if (status === 0) hint = 'fetch failed (network/CORS/blocked)';
          else if (status === 401) hint = '401: chưa đăng nhập trong Tab1 WebView2';
          else if (status === 403) hint = '403: bị chặn (Cloudflare/cf_clearance thiếu hoặc cookie không đúng)';
          else if (headLower.includes('just a moment') || headLower.includes('cf-chl') || headLower.includes('cloudflare'))
            hint = 'Cloudflare challenge: Tab1 WebView2 chưa có cf_clearance / chưa vượt challenge';
          else if (ctLower && !ctLower.includes('application/json'))
            hint = 'Response không phải JSON (có thể là HTML login/challenge)';
        } catch {}

        try { console.warn('[Tab1] media/post/create debug:', dbg); } catch (e) {}
        return { __marker: 'step1', ok: false, status, postId: postId || '', thumbUrl: thumbUrl || '', debug: dbg, hint, logs };
      }

      return { __marker: 'step1', ok: true, status, postId, thumbUrl, debug: '', logs };
    } finally {
      try { console.log = olog; console.warn = owarn; console.error = oerr; } catch {}
    }
  })();`;

  return await t1SeqRet(
    job,
    35000,
    { label: 'step1_mediaPostCreate', method: 'POST', url: 'https://grok.com/rest/media/post/create' }
  );
}

async function createConversationAndPollViaTab1(prompt, parentPostId, aspect, length, resName, extraHeaders) {
  const job = `(async () => {
    const prompt = ${JSON.stringify(prompt)};
    const parentPostId = ${JSON.stringify(parentPostId)};
    const aspect = ${JSON.stringify(aspect)};
    const length = ${JSON.stringify(length)};
    const resName = ${JSON.stringify(resName)};
    const extraHeaders = ${JSON.stringify(sanitizeExtraHeaders(extraHeaders || {}))};

    console.log('[Tab1][step2] POST /app-chat/conversations/new start');

    const headers = {
      ${Object.entries(getDefaultRequestHeaders()).map(([k, v]) => `'${k}': ${JSON.stringify(v)}`).join(',\n      ')},
      'Referer': 'https://grok.com/imagine',
      'Origin': 'https://grok.com'
    };

    for (const [k, v] of Object.entries(extraHeaders || {})) {
      if (v === undefined || v === null) continue;
      if (String(k).toLowerCase() === 'cookie') continue;
      headers[k] = v;
    }

    const body = {
      temporary: true,
      modelName: 'grok-3',
      message: prompt + ' --mode=custom',
      toolOverrides: { videoGen: true },
      enableSideBySide: true,
      responseMetadata: {
        experiments: [],
        modelConfigOverride: {
          modelMap: {
            videoGenModelConfig: {
              parentPostId: parentPostId,
              aspectRatio: aspect,
              videoLength: length,
              resolutionName: resName
            }
          }
        }
      }
    };

    const response = await fetch('https://grok.com/rest/app-chat/conversations/new', {
      method: 'POST',
      headers,
      credentials: 'include',
      body: JSON.stringify(body)
    });

    if (!response.ok) {
      const txt = await response.text().catch(() => '');
      throw new Error('POST /conversations/new thất bại: ' + response.status + ' ' + txt.slice(0, 200));
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let mediaUrl = '';
    let videoId = '';
    let userId = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\\n');
      buffer = lines.pop();

      for (const line of lines) {
        const trimmed = String(line).trim();
        if (!trimmed) continue;

        let json;
        try { json = JSON.parse(trimmed); } catch { continue; }

        const resp = json?.result?.response;
        if (!resp) continue;

        const svgr = resp.streamingVideoGenerationResponse;
        if (svgr) {
          videoId = svgr.videoId || videoId;
          if (svgr.mediaUrl) mediaUrl = svgr.mediaUrl;
        }

        const ur = resp.userResponse;
        if (ur?.userId) userId = ur.userId;
      }
    }

    // Ưu tiên link mp4 theo đúng format assets.grok.com (ổn định cho download)
    let videoUrl = '';
    if (videoId && userId) {
      videoUrl = 'https://assets.grok.com/users/' + userId + '/generated/' + videoId + '/generated_video.mp4?cache=1';
    } else if (mediaUrl) {
      videoUrl = mediaUrl;
    }

    if (!videoUrl) throw new Error('Không lấy được URL video sau khi stream kết thúc.');
    return { videoUrl, videoId, userId, mediaUrl };
  })();`;

  return await t1SeqRet(
    job,
    600000,
    { label: 'step2_conversationNew', method: 'POST', url: 'https://grok.com/rest/app-chat/conversations/new' }
  );
}

/**
 * Step 1 — POST https://grok.com/rest/media/post/create
 */
async function createMediaPost(prompt, extraHeaders) {
  const headers = buildHeaders({
    ...getDefaultRequestHeaders(),
    'Referer': 'https://grok.com/imagine',
    'Origin': 'https://grok.com',
  }, sanitizeExtraHeaders(extraHeaders));

  const res = await fetch('https://grok.com/rest/media/post/create', {
    method: 'POST',
    headers,
    credentials: 'include',  // browser tự đính cookie phiên
    body: JSON.stringify({
      mediaType: 'MEDIA_POST_TYPE_VIDEO',
      prompt,
    }),
  });

  if (!res.ok) {
    const txt = await res.text().catch(() => '');
    throw new Error(`POST /media/post/create thất bại: ${res.status} ${txt.slice(0, 200)}`);
  }

  return res.json();
}

/**
 * Step 2 — POST https://grok.com/rest/app-chat/conversations/new (streaming NDJSON)
 */
async function createConversationAndPoll(prompt, parentPostId, aspect, length, res, extraHeaders, cardId) {
  const headers = buildHeaders({
    ...getDefaultRequestHeaders(),
    'Referer': 'https://grok.com/imagine',
    'Origin': 'https://grok.com',
  }, sanitizeExtraHeaders(extraHeaders));

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
          videoGenModelConfig: {
            parentPostId,
            aspectRatio: aspect,
            videoLength: length,
            resolutionName: res,
          },
        },
      },
    },
  };

  const response = await fetch('https://grok.com/rest/app-chat/conversations/new', {
    method: 'POST',
    headers,
    credentials: 'include',  // browser tự đính cookie phiên
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    const txt = await response.text().catch(() => '');
    throw new Error(`POST /conversations/new thất bại: ${response.status} ${txt.slice(0, 200)}`);
  }

  // ── Đọc streaming NDJSON ──
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let mediaUrl = '';
  let videoId = '';
  let userId = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    const lines = buffer.split('\n');
    buffer = lines.pop();

    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed) continue;

      let json;
      try { json = JSON.parse(trimmed); }
      catch { continue; }

      const resp = json?.result?.response;
      if (!resp) continue;

      const svgr = resp.streamingVideoGenerationResponse;
      if (svgr) {
        const pct = svgr.progress || 0;
        videoId = svgr.videoId || videoId;
        updateProgress(cardId, Math.min(10 + Math.floor(pct * 0.88), 98),
          `Đang render... ${pct}%`);
      if (svgr.mediaUrl) mediaUrl = svgr.mediaUrl;
      }

      const ur = resp.userResponse;
      if (ur?.userId) userId = ur.userId;
    }
  }

  // Ưu tiên link mp4 theo đúng format assets.grok.com (ổn định cho download)
  let videoUrl = '';
  if (videoId && userId) {
    videoUrl = `https://assets.grok.com/users/${userId}/generated/${videoId}/generated_video.mp4?cache=1`;
  } else if (mediaUrl) {
    videoUrl = mediaUrl;
  }

  if (!videoUrl) {
    throw new Error('Không lấy được URL video sau khi stream kết thúc.');
  }

  return videoUrl;
}

// ─── MODAL ────────────────────────────────────────────────────────────────────
function openModal(id) {
  const item = state.videos.find(v => v.id === id);
  if (!item || item.status !== 'done') return;

  const modal = document.getElementById('videoModal');
  const video = document.getElementById('modalVideo');
  const prompt = document.getElementById('modalPrompt');
  const dl = document.getElementById('modalDownload');

  video.src = item.videoUrl;
  prompt.textContent = item.prompt;
  dl.href = item.videoUrl;
  dl.download = `grok_${item.id}.mp4`;

  modal.classList.add('open');
}

function closeModal(e) {
  if (e && e.target !== document.getElementById('videoModal')
    && !e.target.classList.contains('modal-close')) return;
  const modal = document.getElementById('videoModal');
  const video = document.getElementById('modalVideo');
  modal.classList.remove('open');
  video.pause();
  video.src = '';
}

document.addEventListener('keydown', e => {
  if (e.key === 'Escape') closeModal({ target: document.getElementById('videoModal') });
});

document.getElementById('promptInput').addEventListener('keydown', e => {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) generateVideo();
});

// ─── INIT ─────────────────────────────────────────────────────────────────────
updateStats();