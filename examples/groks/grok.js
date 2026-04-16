(function () {
    'use strict';

    /* ═══════════════════════════════════════════════
       STATE
    ═══════════════════════════════════════════════ */
    var state = { videos: [], processing: 0 };
    var pollJobs = {};
    var viewMode = 'grid'; // 'grid' | 'list'
    var cardSizeMode = 'md'; // sm | md | lg | xl | xx1 | xx2
    var fitContainMedia = false;
    var groupPromptEnabled = false;
    var groupCollapsedState = {};
    var groupLayoutApplied = false;
    var promptGroupingSignature = '';

    /* ═══════════════════════════════════════════════
       BATCH QUEUE STATE
       Queue of prompt batches to send sequentially.
       Each item: { prompts: [], sendCount, aspect, length, res, guildId, label }
    ═══════════════════════════════════════════════ */
    var batchQueue      = [];    // pending batches
    var batchRunning    = false; // whether a batch is currently processing
    var currentBatchIdx = 0;     // which batch is currently active (for UI)

    /* ═══════════════════════════════════════════════
       TOAST NOTIFICATIONS
    ═══════════════════════════════════════════════ */
    function showToast(type, title, msg, duration) {
        duration = duration || 4000;
        var icons = {
            success: 'bi-check-circle-fill',
            error:   'bi-exclamation-circle-fill',
            info:    'bi-info-circle-fill',
            warn:    'bi-exclamation-triangle-fill'
        };
        var container = document.getElementById('toastContainer');
        if (!container) return;

        var el = document.createElement('div');
        el.className = 'toast-notif toast-' + type;
        el.innerHTML =
            '<i class="bi ' + (icons[type] || icons.info) + ' toast-icon"></i>' +
            '<div class="toast-body">' +
                (title ? '<div class="toast-title">' + esc(title) + '</div>' : '') +
                (msg   ? '<div class="toast-msg">'   + esc(msg)   + '</div>' : '') +
            '</div>' +
            '<div class="toast-progress">' +
                '<div class="toast-progress-fill" style="animation-duration:' + duration + 'ms"></div>' +
            '</div>';

        container.appendChild(el);
        el.addEventListener('click', function () {
            el.classList.add('hiding');
            setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 300);
        });
        setTimeout(function () {
            el.classList.add('hiding');
            setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 320);
        }, duration);
    }

    /* ═══════════════════════════════════════════════
       UTILS
    ═══════════════════════════════════════════════ */
    function uid() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0;
            return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
        });
    }

    function timeAgo(d) {
        var s = Math.max(0, Math.floor((Date.now() - d) / 1000));
        if (s < 5) return '0s';
        if (s < 60) return s + 's';
        var m = Math.floor(s / 60);
        var rs = s % 60;
        if (m < 60) return m + 'm' + (rs > 0 ? rs + 's' : '');
        var h = Math.floor(m / 60);
        var rm = m % 60;
        return h + 'h' + (rm > 0 ? rm + 'm' : '');
    }

    function esc(s) {
        return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;')
                              .replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    }

    function aspectClass(r) {
        return { '9:16': 'r916', '16:9': 'r169', '1:1': 'r11', '2:3': 'r23', '3:2': 'r32' }[r] || '';
    }

    /** Hậu tố cho .card-aspect-* trên .video-card (Fit Width: max-width body khớp bề rộng khung video). */
    function cardAspectSuffix(itemOrAspect) {
        var a = itemOrAspect && typeof itemOrAspect === 'object' ? itemOrAspect.aspect : itemOrAspect;
        var k = normalizeAspectValue(a);
        if (!k) k = '9:16';
        return String(k).replace(':', '');
    }

    function applyVideoCardAspectClass(el, item) {
        if (!el) return;
        var suff = cardAspectSuffix(item);
        var prefix = 'card-aspect-';
        var parts = String(el.className || '').split(/\s+/).filter(Boolean);
        parts = parts.filter(function (c) { return c.indexOf(prefix) !== 0; });
        parts.push(prefix + suff);
        el.className = parts.join(' ');
    }

    function normalizeAspectValue(v) {
        var s = String(v == null ? '' : v).trim();
        if (!s) return '';
        s = s.replace(/\s+/g, '');
        if (s === '9:16' || s === '16:9' || s === '1:1' || s === '2:3' || s === '3:2') return s;
        return '';
    }

    function normalizeLengthValue(v) {
        var n = parseInt(String(v == null ? '' : v).trim(), 10);
        return Number.isFinite(n) && n > 0 ? n : 0;
    }

    function normalizeResolutionValue(v) {
        var n = parseInt(String(v == null ? '' : v).trim(), 10);
        return Number.isFinite(n) && n > 0 ? String(n) : '';
    }

    function toFileUri(path) {
        if (!path) return '';
        var p = String(path).replace(/\\/g, '/');
        if (/^file:\/\//i.test(p)) return p;
        if (/^[a-zA-Z]:\//.test(p)) return 'file:///' + p;
        return p;
    }

    function toPlayableSrc(pathOrUrl) {
        if (!pathOrUrl) return '';
        try {
            return encodeURI(String(pathOrUrl));
        } catch (_) {
            return String(pathOrUrl);
        }
    }

    var localPathResolveWaiters = {};

    function resolveLocalPlayableUrl(localPath, fallbackUrl, cb) {
        var done = false;
        function finish(url, useRawFallback) {
            if (done) return;
            done = true;
            // Nếu resolve thất bại (url rỗng) và fallbackUrl là file:// thì không dùng
            // vì WebView2 chặn file:// → trả '' cho caller tự xử lý
            if (!url && fallbackUrl && /^file:/i.test(fallbackUrl)) {
                cb(useRawFallback ? fallbackUrl : '');
                return;
            }
            cb(url || fallbackUrl || '');
        }

        var reqId = uid();
        var attemptNo = 0;

        function attempt() {
            if (done) return;
            if (typeof window.acResolveLocalPath !== 'function') {
                // Bridge chưa sẵn sàng (page đang load), retry tối đa 4 lần × 500ms = 2s
                attemptNo++;
                if (attemptNo <= 4) {
                    setTimeout(attempt, 500);
                } else {
                    finish(fallbackUrl || '', true); // hết retry → dùng fallback
                }
                return;
            }
            localPathResolveWaiters[reqId] = function (resolvedUrl) {
                finish(resolvedUrl, false);
            };
            try {
                window.acResolveLocalPath(localPath, reqId);
            } catch (_) {
                delete localPathResolveWaiters[reqId];
                finish(fallbackUrl || '', true);
                return;
            }
            // Timeout 8s sau khi gửi request
            setTimeout(function () {
                var waiter = localPathResolveWaiters[reqId];
                if (!waiter) return;
                delete localPathResolveWaiters[reqId];
                finish(fallbackUrl || '', true);
            }, 8000);
        }

        attempt();
    }

    function hydrateLocalPlayable(item) {
        if (!item || !item.localPath) return;
        // Tránh gọi resolve nhiều lần cho cùng 1 item (dedup bằng item.id - unique per card)
        var trackKey = item.id || '';
        if (trackKey && hydratingGuildIds[trackKey]) return;
        if (trackKey) hydratingGuildIds[trackKey] = true;
        // Không truyền file:// làm fallback vào resolveLocalPlayableUrl:
        // nếu resolve_local_path thất bại thì WebView2 cũng không phát được file://
        // → trả '' để video ở trạng thái "chưa load" thay vì show 1 URL sai.
        resolveLocalPlayableUrl(item.localPath, '', function (resolvedSrc) {
            if (trackKey) delete hydratingGuildIds[trackKey];
            if (!resolvedSrc || /^file:/i.test(resolvedSrc)) return; // bỏ qua file:// URL
            item.localUrl = resolvedSrc;
            item.videoUrl = resolvedSrc;
            // Dùng srcOnly để chỉ update <video src> trong DOM
            // Tránh re-render toàn card → tránh toast duplicate + hydrate loop
            updateCard(item, { srcOnly: true });
        });
    }

    /** Đổi file:///C:/... thành đường dẫn Windows — WebView2 thường chặn file:// trong <video> khi trang không phải file://. */
    function fileUriToLocalPath(s) {
        if (!s || typeof s !== 'string') return '';
        var u = s.trim();
        if (!/^file:/i.test(u)) return '';
        try {
            u = u.replace(/^file:\/\//i, '');
            // file:///C:/path → /C:/path
            if (u.charAt(0) === '/' && /^\/[a-zA-Z]:\//.test(u)) u = u.substring(1);
            else if (u.charAt(0) === '/') u = u.replace(/^\/+/, '');
            u = decodeURIComponent(String(u).replace(/\+/g, '%20')).replace(/\//g, '\\');
            return u;
        } catch (_) {
            return '';
        }
    }

    function isAbsoluteWinPath(s) {
        return !!s && /^[a-zA-Z]:[\\/]/.test(String(s).trim());
    }

    function normalizeDoneVideoSources(item) {
        if (!item || item.status !== 'done') return;
        var vu = (item.videoUrl || '').trim();
        var lu = (item.localUrl || '').trim();
        if (!item.localPath && vu && /^file:/i.test(vu)) {
            var p = fileUriToLocalPath(vu);
            if (p) item.localPath = p;
        }
        if (!item.localPath && lu && /^file:/i.test(lu)) {
            var p2 = fileUriToLocalPath(lu);
            if (p2) item.localPath = p2;
        }
        if (!item.localPath && vu && isAbsoluteWinPath(vu)) {
            item.localPath = vu.replace(/\//g, '\\');
        }
    }

    /** Chỉ tên file (vd. download_xxx.mp4) — không dùng làm href/src/poster (gây ERR_NAME_NOT_RESOLVED). */
    function isBareMediaFileName(s) {
        if (!s || typeof s !== 'string') return false;
        var t = s.trim();
        if (!t) return false;
        if (/^[a-zA-Z]:[\\/]/.test(t) || /^file:/i.test(t) || /^https?:\/\//i.test(t) || isInternalPlayableRef(t)) return false;
        if (/[\\/]/.test(t)) return false;
        return /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test(t);
    }

    /**
     * Lấy đường dẫn tuyệt đối từ datas (__ac) theo guildId khi item chỉ còn tên file.
     * Tránh trường hợp workflow đẩy đủ path vào datas nhưng item.videoUrl đã bị ghi đè thành tên file.
     */
    function patchCardDownloadHref(item) {
        if (!item || !item.id) return;
        var h = item.localUrl || (item.localPath ? toFileUri(item.localPath) : item.videoUrl || '');
        if (!h || isBareMediaFileName(h)) return;
        var card = document.getElementById('card-' + item.id);
        if (!card) return;
        var links = card.querySelectorAll('.card-actions a');
        for (var i = 0; i < links.length; i++) {
            if (links[i].getAttribute('download')) {
                links[i].setAttribute('href', h);
                break;
            }
        }
    }

    function enrichItemPathsFromDatas(item) {
        if (!item || !item.guildId) return;
        if (item.localPath && /^[a-zA-Z]:[\\/]/.test(String(item.localPath).trim())) return;
        try {
            var dict = pickResultDictionary();
            if (!dict || typeof dict !== 'object') return;
            var gid = String(item.guildId).trim();
            var raw = dict[gid];
            if (raw === undefined || raw === null) {
                var gnorm = normalizeKeyText(gid);
                var keys = Object.keys(dict);
                for (var i = 0; i < keys.length; i++) {
                    if (normalizeKeyText(keys[i]) === gnorm) {
                        raw = dict[keys[i]];
                        break;
                    }
                }
            }
            var pathStr = null;
            if (typeof raw === 'string') {
                var ts = raw.trim();
                if (/^[a-zA-Z]:[\\/]/.test(ts) || /^file:/i.test(ts)) pathStr = ts;
            } else if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
                var o = raw;
                var s = o.filePath || o.localPath || o.path || o.videoUrl || o.url || o.file || '';
                pathStr = s != null ? String(s).trim() : '';
                if (!pathStr) return;
            }
            if (pathStr && /^[a-zA-Z]:[\\/]/.test(pathStr.trim())) {
                item.localPath = pathStr.trim().replace(/\//g, '\\');
            } else if (pathStr && /^file:/i.test(pathStr.trim())) {
                var p3 = fileUriToLocalPath(pathStr.trim());
                if (p3) item.localPath = p3;
            }
        } catch (_) {}
    }

    /** URL nội bộ WebView2 — chỉ phát được sau khi host gọi SetVirtualHostNameToFolderMapping. */
    function isInternalPlayableRef(url) {
        if (!url || typeof url !== 'string') return false;
        return /^https:\/\/((localfiles(?:-[a-z0-9]+)?)|downloads)\.local\//i.test(url.trim());
    }

    function withCacheBust(u) {
        if (!u || typeof u !== 'string') return u || '';
        var s = u.trim();
        if (!s) return '';
        var sep = (s.indexOf('?') >= 0) ? '&' : '?';
        return s + sep + 't=' + Date.now();
    }

    /**
     * mode: 'modal' — load đầy đủ để modal phát; 'inline' — chỉ cần metadata + 1 frame preview, không autoplay.
     */
    function applyVideoElementSource(videoEl, url, mode) {
        if (!videoEl || !url) return;
        var safeUrl = String(url).trim();
        if (!safeUrl) return;
        if (/^file:/i.test(safeUrl)) return; // WebView2 preview chặn file://
        var isModal = mode === 'modal';
        try {
            // Dùng <source> giúp WebView2 refresh pipeline ổn định hơn sau lần load fail trước đó.
            var sourceEl = null;
            try { sourceEl = videoEl.querySelector('source'); } catch (_) { sourceEl = null; }
            if (!sourceEl) {
                try {
                    sourceEl = document.createElement('source');
                    sourceEl.type = 'video/mp4';
                    videoEl.innerHTML = '';
                    videoEl.appendChild(sourceEl);
                } catch (_) { sourceEl = null; }
            }
            if (sourceEl) sourceEl.src = safeUrl;
            // Giữ compatibility: set cả src trực tiếp.
            videoEl.src = safeUrl;
            try { videoEl.setAttribute('data-src-resolved', safeUrl); } catch (_) {}
            try {
                videoEl.autoplay = false;
                videoEl.removeAttribute('autoplay');
            } catch (_) {}

            if (isModal) {
                if (videoEl.load) videoEl.load();
                return;
            }

            // Gallery inline: hiển thị frame (tránh màn đen), không autoplay.
            try { videoEl.preload = 'metadata'; } catch (_) {}
            try { videoEl.muted = true; } catch (_) {}

            // ─── WebView2 Host Mapping Propagation Retry ───────────────────────────
            // SetVirtualHostNameToFolderMapping được C# gọi async. Browser process
            // đã đăng ký mapping nhưng renderer process chưa nhận được ngay →
            // lần đầu set src, video fail âm thầm (KHÔNG có onerror, readyState=0).
            // Load page lần 2 thì mapping đã sẵn sàng ở renderer → hoạt động bình thường.
            //
            // Fix: retry nhiều lần với cache-bust URL để renderer nhận mapping mới.
            // Delays: 400ms → 1.2s → 2.5s → 4.5s (covering ~1-3s propagation time).
            // ──────────────────────────────────────────────────────────────────────
            var _fbEl = videoEl;
            var _fbSrc = safeUrl;
            var _retryDone = false;
            var _retryDelays = [400, 1200, 2500, 4500];
            var _retryIdx = 0;

            function scheduleVideoRetry() {
                if (_retryDone || _retryIdx >= _retryDelays.length) return;
                var delay = _retryDelays[_retryIdx++];
                setTimeout(function () {
                    try {
                        if (_retryDone || !_fbEl) return;
                        var rs = _fbEl.readyState;
                        var ns = _fbEl.networkState;
                        // readyState >= 1 (HAVE_METADATA) = đã load được → dừng retry
                        // networkState = 1 (NETWORK_IDLE, đã load) hoặc 2 (NETWORK_LOADING) → đang ổn
                        if (rs >= 1 && ns !== 3) {
                            _retryDone = true;
                            return;
                        }
                        // Vẫn chưa load (readyState=0 hoặc networkState=3=NO_SOURCE) → retry
                        // Dùng cache-bust để tránh WebView2 dùng response lỗi đã cache
                        var bustSrc = withCacheBust(_fbSrc);
                        _fbEl.src = bustSrc;
                        try {
                            var _srcEl = _fbEl.querySelector('source');
                            if (_srcEl) _srcEl.src = bustSrc;
                        } catch (_) {}
                        if (_fbEl.load) _fbEl.load();
                        scheduleVideoRetry(); // schedule retry tiếp theo
                    } catch (_) {}
                }, delay);
            }
            scheduleVideoRetry(); // bắt đầu retry loop

            function primeFirstVisibleFrame() {
                try {
                    if (videoEl.error) return;
                    try { if (videoEl.pause) videoEl.pause(); } catch (_) {}
                    var seekT = 0.05;
                    try {
                        var d = videoEl.duration;
                        if (Number.isFinite(d) && d > 0) {
                            seekT = Math.min(0.12, Math.max(0.001, d * 0.003));
                        }
                    } catch (_) {}
                    if (videoEl.seekable && videoEl.seekable.length > 0) {
                        try {
                            var start = videoEl.seekable.start(0);
                            var end = videoEl.seekable.end(videoEl.seekable.length - 1);
                            if (Number.isFinite(start) && Number.isFinite(end) && end > start + 0.02) {
                                seekT = Math.min(Math.max(seekT, start + 0.01), end - 0.05);
                            }
                        } catch (_) {}
                    }
                    function onSeeked() {
                        videoEl.removeEventListener('seeked', onSeeked);
                        try { if (videoEl.pause) videoEl.pause(); } catch (_) {}
                    }
                    videoEl.addEventListener('seeked', onSeeked);
                    videoEl.currentTime = seekT;
                } catch (_) {
                    try { if (videoEl.pause) videoEl.pause(); } catch (_) {}
                }
            }

            var didPrime = false;
            function onLoadedMetadata() {
                if (didPrime) return;
                didPrime = true;
                _retryDone = true; // video đã load → huỷ retry loop
                primeFirstVisibleFrame();
            }
            videoEl.addEventListener('loadedmetadata', onLoadedMetadata, { once: true });
            if (videoEl.readyState >= 1) {
                try { onLoadedMetadata(); } catch (_) {}
            }
        } catch (_) {}
    }

    window.handleInlineVideoError = function (id) {
        try {
            if (!id) return;
            var item = null;
            for (var i = 0; i < state.videos.length; i++) {
                if (state.videos[i] && state.videos[i].id === id) { item = state.videos[i]; break; }
            }
            if (!item) return;
            var v = document.getElementById('vid-' + id);
            if (!v) return;

            var src = '';
            try { src = String(v.getAttribute('data-src-resolved') || '').trim(); } catch (_) { src = ''; }
            if (!src) src = (item.localUrl || item.videoUrl || '').trim();
            if (!src && item.localPath) src = toFileUri(item.localPath);
            if (!src) return;

            // 1) Ưu tiên re-resolve từ localPath để host mapping được thiết lập lại chính xác
            if (item.localPath) {
                resolveLocalPlayableUrl(item.localPath, '', function (resolved) {
                    if (!resolved) return;
                    item.localUrl = resolved;
                    item.videoUrl = resolved;
                    applyVideoElementSource(v, withCacheBust(resolved));
                    patchCardDownloadHref(item);
                });
                return;
            }

            // 2) URL nội bộ: resolve lại rồi gán vào <video>. Không dùng iframe — Edge/WebView2
            //    hay hiển thị trang lỗi gây hiểu nhầm dù file vẫn tồn tại.
            if (isInternalPlayableRef(src)) {
                resolvePlayableRefUrl(src, function (resolved) {
                    if (!resolved) {
                        applyVideoElementSource(v, withCacheBust(src));
                        return;
                    }
                    item.localUrl = resolved;
                    item.videoUrl = resolved;
                    applyVideoElementSource(v, withCacheBust(resolved));
                    patchCardDownloadHref(item);
                });
                return;
            }

            if (/^https?:\/\//i.test(src)) {
                applyVideoElementSource(v, withCacheBust(src));
            }
        } catch (_) {}
    };

    function resolvePlayableRefUrl(url, cb) {
        var done = false;
        function finish(u) {
            if (done) return;
            done = true;
            cb(u || '');
        }
        if (!isInternalPlayableRef(url) || typeof window.acResolvePlayableRef !== 'function') {
            finish('');
            return;
        }
        var reqId = uid();
        localPathResolveWaiters[reqId] = finish;
        try { window.acResolvePlayableRef(url, reqId); } catch (_) { finish(''); }
        setTimeout(function () {
            var w = localPathResolveWaiters[reqId];
            if (!w) return;
            delete localPathResolveWaiters[reqId];
            finish('');
        }, 5000);
    }

    /** Khi chỉ có URL ảo (không có localPath): bảo host tìm file + map thư mục rồi cập nhật src video. */
    function hydrateDoneCardMedia(item) {
        if (!item || item.status !== 'done') return;
        // Nếu đã có localUrl rồi → chỉ apply vào DOM, không resolve lại
        if (item.localUrl && isInternalPlayableRef(item.localUrl)) {
            var v0 = document.getElementById('vid-' + item.id);
            if (v0) {
                var curAttr = '';
                try { curAttr = String(v0.getAttribute('src') || '').trim(); } catch (_) { curAttr = ''; }
                if (!curAttr || isBareMediaFileName(curAttr) || curAttr.indexOf('localfiles') < 0) {
                    applyVideoElementSource(v0, withCacheBust(item.localUrl));
                }
            }
            patchCardDownloadHref(item);
            return;
        }
        enrichItemPathsFromDatas(item);
        normalizeDoneVideoSources(item);
        if (item.localPath) {
            hydrateLocalPlayable(item);
            return;
        }
        var refU = (item.localUrl || item.videoUrl || '').trim();
        if (isInternalPlayableRef(refU)) {
            resolvePlayableRefUrl(refU, function (resolved) {
                if (!resolved) return;
                item.localUrl = resolved;
                item.videoUrl = resolved;
                var v = document.getElementById('vid-' + item.id);
                if (v) {
                    applyVideoElementSource(v, withCacheBust(resolved));
                }
                patchCardDownloadHref(item);
            });
            return;
        }
        // Chỉ tên file (không có / \\ :) → ERR_NAME_NOT_RESOLVED nếu nhét thẳng vào src; nhờ host tìm trong thư mục gợi ý.
        if (refU && !/[\\/:]/.test(refU) && /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test(refU)) {
            resolvePlayableRefUrl('https://localfiles.local/' + encodeURIComponent(refU), function (resolved) {
                if (!resolved) return;
                item.localUrl = resolved;
                item.videoUrl = resolved;
                var v2 = document.getElementById('vid-' + item.id);
                if (v2) {
                    applyVideoElementSource(v2, withCacheBust(resolved));
                }
                patchCardDownloadHref(item);
            });
        }
    }

    function showError(msg) {
        var el = document.getElementById('errorMsg');
        if (!el) return;
        el.textContent = msg;
        el.style.display = 'flex';
        el.classList.remove('animate__shakeX');
        void el.offsetWidth;
        el.classList.add('animate__shakeX');
        setTimeout(function () { el.style.display = 'none'; }, 7000);
    }

    var UI_PREFS_KEY = 'grok_ui_prefs_v1';

    function loadUiPrefs() {
        try {
            var raw = localStorage.getItem(UI_PREFS_KEY);
            if (!raw) return {};
            var parsed = JSON.parse(raw);
            return parsed && typeof parsed === 'object' ? parsed : {};
        } catch (_) {
            return {};
        }
    }

    function saveUiPrefs(prefs) {
        try {
            localStorage.setItem(UI_PREFS_KEY, JSON.stringify(prefs || {}));
        } catch (_) {}
    }

    function isGuidLikeText(s) {
        if (!s || typeof s !== 'string') return false;
        return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(String(s).trim());
    }

    /**
     * Chuẩn hóa danh sách prompt:
     * - bỏ rỗng
     * - khi có nhiều dòng, tự bỏ dòng GUID (thường là outputGuildId bị dán nhầm vào prompt)
     */
    function sanitizePromptLines(lines) {
        var arr = (lines || []).map(function (s) { return String(s || '').trim(); }).filter(Boolean);
        if (arr.length <= 1) return arr;
        var nonGuid = arr.filter(function (p) { return !isGuidLikeText(p); });
        return nonGuid.length > 0 ? nonGuid : arr;
    }

    function bindPrefsPersistence() {
        var prefs = loadUiPrefs();
        var ids = ['aspectRatio', 'videoLength', 'resolution', 'sendCount', 'batchSize'];
        for (var i = 0; i < ids.length; i++) {
            var id = ids[i];
            var el = document.getElementById(id);
            if (!el) continue;
            if (prefs[id] != null && prefs[id] !== '') {
                el.value = String(prefs[id]);
            }
            el.addEventListener('change', function (e) {
                var t = e && e.target ? e.target : null;
                if (!t || !t.id) return;
                var p = loadUiPrefs();
                p[t.id] = t.value;
                saveUiPrefs(p);
            });
        }

        var multi = document.getElementById('multiPrompt');
        if (multi) {
            if (prefs.multiPrompt != null) {
                multi.checked = !!prefs.multiPrompt;
            }
            multi.addEventListener('change', function () {
                var p = loadUiPrefs();
                p.multiPrompt = !!multi.checked;
                saveUiPrefs(p);
            });
        }

        if (prefs.cardSizeMode && /^(sm|md|lg|xl|xx1|xx2)$/.test(String(prefs.cardSizeMode))) {
            cardSizeMode = String(prefs.cardSizeMode);
        }
        if (prefs.fitContainMedia != null) {
            fitContainMedia = !!prefs.fitContainMedia;
        }
        if (prefs.groupPromptEnabled != null) {
            groupPromptEnabled = !!prefs.groupPromptEnabled;
        }
        if (prefs.viewMode === 'list' || prefs.viewMode === 'grid') {
            viewMode = prefs.viewMode;
        }

        var promptEl = document.getElementById('promptInput');
        if (promptEl) {
            if (typeof prefs.promptInputText === 'string') promptEl.value = prefs.promptInputText;
            if (prefs.promptInputHeight != null && Number(prefs.promptInputHeight) > 70) {
                promptEl.style.height = Number(prefs.promptInputHeight) + 'px';
            }
            promptEl.addEventListener('input', function () {
                var p = loadUiPrefs();
                p.promptInputText = promptEl.value;
                saveUiPrefs(p);
            });
            promptEl.addEventListener('mouseup', function () {
                var p = loadUiPrefs();
                p.promptInputHeight = Math.round(promptEl.offsetHeight || 0);
                saveUiPrefs(p);
            });
        }
    }

    /* ═══════════════════════════════════════════════
       STATS & UI STATE
    ═══════════════════════════════════════════════ */
    function updateStats() {
        var t  = state.videos.length;
        var tp = document.getElementById('totalPill');
        if (tp) tp.textContent = t + ' video' + (t !== 1 ? 's' : '');

        var pp = document.getElementById('procPill');
        if (pp) {
            if (state.processing > 0) {
                pp.style.setProperty('display', 'flex', 'important');
                var pc = document.getElementById('procCount');
                if (pc) pc.textContent = state.processing;
            } else {
                pp.style.setProperty('display', 'none', 'important');
            }
        }

        var em = document.getElementById('emptyState');
        if (em) em.style.display = state.videos.length === 0 ? 'flex' : 'none';

        var clearBtn = document.getElementById('clearAllBtn');
        if (clearBtn) clearBtn.style.display = state.videos.length > 0 ? 'flex' : 'none';

        var pt   = document.getElementById('pollToast');
        var keys = Object.keys(pollJobs).length;
        if (pt) {
            if (keys > 0) {
                pt.classList.add('show');
                var ptxt = document.getElementById('pollText');
                if (ptxt) ptxt.textContent = 'Đang poll ' + keys + ' job(s)...';
            } else {
                pt.classList.remove('show');
            }
        }
        applyPromptGrouping();
        try { if (typeof window.__grokSyncImgClearBtn === 'function') window.__grokSyncImgClearBtn(); } catch (_) {}
    }

    function promptGroupKey(s) {
        return String(s == null ? '' : s).trim().replace(/\s+/g, ' ').toLowerCase();
    }

    function applyPromptGrouping() {
        var grid = document.getElementById('videoGrid');
        if (!grid) return;
        var empty = document.getElementById('emptyState');

        var cards = [];
        for (var i = 0; i < state.videos.length; i++) {
            var it = state.videos[i];
            if (!it) continue;
            var card = document.getElementById('card-' + it.id);
            if (!card) continue;
            cards.push({ item: it, card: card, key: promptGroupKey(it.prompt || '') || ('__empty__' + it.id) });
        }

        if (!groupPromptEnabled) {
            if (!groupLayoutApplied) return;
            grid.innerHTML = '';
            if (cards.length === 0) {
                if (empty) grid.appendChild(empty);
                groupLayoutApplied = false;
                promptGroupingSignature = '';
                return;
            }
            cards.forEach(function (c) {
                c.card.style.display = '';
                grid.appendChild(c.card);
            });
            groupLayoutApplied = false;
            promptGroupingSignature = '';
            return;
        }
        var groups = {};
        var order = [];
        cards.forEach(function (c) {
            if (!groups[c.key]) {
                groups[c.key] = [];
                order.push(c.key);
            }
            groups[c.key].push(c);
        });
        var groupSig = cards.map(function (c) { return c.item.id + '@' + c.key; }).join('|') + '::' +
            order.map(function (k) { return k + ':' + (groupCollapsedState[k] ? 1 : 0); }).join('|');
        if (groupLayoutApplied && promptGroupingSignature === groupSig) return;
        groupLayoutApplied = true;
        promptGroupingSignature = groupSig;

        grid.innerHTML = '';
        if (cards.length === 0) {
            if (empty) grid.appendChild(empty);
            return;
        }
        if (empty) empty.style.display = 'none';

        order.forEach(function (k) {
            var list = groups[k];
            if (!list || !list.length) return;
            var title = String(list[0].item.prompt || '').trim() || '(Không có prompt)';
            var groupEl = document.createElement('section');
            groupEl.className = 'prompt-group' + (groupCollapsedState[k] ? ' collapsed' : '');
            groupEl.setAttribute('data-group-key', k);
            groupEl.innerHTML =
                '<button class="prompt-group-header" type="button" data-group-key="' + esc(k) + '">' +
                    '<span class="prompt-group-title" title="' + esc(title) + '">' + esc(title) + '</span>' +
                    '<span class="prompt-group-meta"><span>' + list.length + ' video</span><i class="bi ' + (groupCollapsedState[k] ? 'bi-chevron-down' : 'bi-chevron-up') + '"></i></span>' +
                '</button>' +
                '<div class="prompt-group-body" id="prompt-group-body-' + esc(k).replace(/[^a-zA-Z0-9_-]/g, '_') + '"></div>';
            grid.appendChild(groupEl);
            var body = groupEl.querySelector('.prompt-group-body');
            list.forEach(function (entry) {
                entry.card.style.display = '';
                if (body) body.appendChild(entry.card);
            });
        });
    }

    /* ═══════════════════════════════════════════════
       BATCH QUEUE UI
    ═══════════════════════════════════════════════ */
    function renderBatchQueue() {
        var statusEl = document.getElementById('batchQueueStatus');
        var bodyEl   = document.getElementById('bqBody');
        if (!statusEl || !bodyEl) return;

        if (batchQueue.length === 0) {
            statusEl.style.display = 'none';
            return;
        }

        statusEl.style.display = 'flex';
        bodyEl.innerHTML = '';

        batchQueue.forEach(function (batch, idx) {
            var item  = document.createElement('div');
            var cls   = batch.done ? 'done-item' : (batch.active ? 'active' : 'pending');
            item.className = 'bq-item ' + cls;

            var statusIcon = batch.done
                ? '<i class="bi bi-check2" style="color:var(--success-clr)"></i>'
                : (batch.active
                    ? '<i class="bi bi-play-fill" style="color:var(--accent2)"></i>'
                    : '<i class="bi bi-hourglass-split" style="color:var(--clr3)"></i>');

            item.innerHTML =
                '<span class="bq-dot"></span>' +
                statusIcon +
                '<span class="bq-item-label">Đợt ' + (idx + 1) + ': ' + esc(batch.prompts.join(', ').substring(0, 32)) + (batch.label.length > 32 ? '…' : '') + '</span>' +
                '<span class="bq-item-count">' + batch.prompts.length + ' prompt × ' + batch.sendCount + '</span>';
            bodyEl.appendChild(item);
        });
    }

    /* ═══════════════════════════════════════════════
       CARD HTML
    ═══════════════════════════════════════════════ */
    function cardHTML(item) {
        var ac   = aspectClass(item.aspect);
        var t    = timeAgo(item.createdAt);
        var promptHtml = '<div class="card-prompt prompt-ellipsis" title="' + esc(item.prompt) + '">' + esc(item.prompt) + '</div>';
        var tags =
            '<span class="tag">' + esc(item.aspect) + '</span>' +
            '<span class="tag">' + item.length + 's</span>' +
            '<span class="tag">' + item.res + 'p</span>';

        if (item.status === 'generating') {
            return (
                '<div class="card-media ' + ac + '">' +
                    (item.thumbUrl ? '<img class="card-thumb" src="' + esc(item.thumbUrl) + '" alt="">' : '') +
                    '<div class="card-progress" id="prog-' + item.id + '">' +
                        '<div class="prog-label">ĐANG TẠO VIDEO</div>' +
                        '<div class="prog-pct" id="pct-' + item.id + '">' + item.progress + '%</div>' +
                        '<div class="prog-track"><div class="prog-fill" id="fill-' + item.id + '" style="width:' + item.progress + '%"></div></div>' +
                        '<div class="prog-sub" id="sub-' + item.id + '">Khởi tạo...</div>' +
                    '</div>' +
                    '<div class="card-badge badge-gen">XỬ LÝ</div>' +
                '</div>' +
                '<div class="card-body-custom">' +
                    promptHtml +
                    '<div class="card-meta"><div class="card-tags">' + tags + '</div><span class="card-time">' + t + '</span></div>' +
                '</div>'
            );
        }

        if (item.status === 'done') {
            enrichItemPathsFromDatas(item);
            normalizeDoneVideoSources(item);
            // Không đưa file:// vào src (WebView2 chặn). Chỉ dùng https nội bộ sau resolve, hoặc URL remote.
            var vuRaw = (item.videoUrl || '').trim();
            var forPlay = (item.localUrl || '').trim();
            if (!forPlay && vuRaw && !/^file:/i.test(vuRaw) && !isAbsoluteWinPath(vuRaw) && (/^https?:\/\//i.test(vuRaw) || isInternalPlayableRef(vuRaw)))
                forPlay = vuRaw;
            if (!forPlay && vuRaw && !/^file:/i.test(vuRaw) && !isAbsoluteWinPath(vuRaw) && /[\\/]/.test(vuRaw))
                forPlay = vuRaw;
            if (isBareMediaFileName(forPlay) || isBareMediaFileName(vuRaw)) forPlay = '';
            var playSrc = toPlayableSrc(forPlay);
            var posterAttr = '';
            if (item.thumbUrl && !isBareMediaFileName(item.thumbUrl)) {
                posterAttr = ' poster="' + esc(item.thumbUrl) + '"';
            }
            return (
                '<div class="card-media ' + ac + ' card-media-inline">' +
                    // Không render src="" khi playSrc rỗng — tránh WebView2 silent error state
                    // (src="" → pipeline lỗi âm thầm → sau khi set src đúng cũng không tự retry)
                    '<video class="card-video-inline" id="vid-' + item.id + '"' +
                    (playSrc ? ' src="' + esc(playSrc) + '"' : '') + posterAttr +
                    ' controls playsinline muted preload="metadata" onerror="handleInlineVideoError(\'' + item.id + '\')"></video>' +
                    '<div class="card-badge badge-done">HOÀN THÀNH</div>' +
                '</div>' +
                '<div class="card-body-custom">' +
                    promptHtml +
                    '<div class="card-meta"><div class="card-tags">' + tags + '</div><span class="card-time">' + t + '</span></div>' +
                '</div>'
            );
        }

        if (item.status === 'error') {
            return (
                '<div class="card-media ' + ac + '">' +
                    '<div style="position:absolute;inset:0;background:var(--surface2);display:flex;flex-direction:column;align-items:center;justify-content:center;gap:10px;padding:20px">' +
                        '<i class="bi bi-exclamation-triangle-fill" style="font-size:28px;color:#f87171"></i>' +
                        '<div style="font-size:10px;color:#f87171;text-align:center;line-height:1.5">' + esc(item.errorMsg || 'Lỗi không xác định') + '</div>' +
                    '</div>' +
                    '<div class="card-badge badge-err">LỖI</div>' +
                '</div>' +
                '<div class="card-body-custom">' +
                    promptHtml +
                    '<div class="card-meta"><div class="card-tags">' + tags + '</div><span class="card-time">' + t + '</span></div>' +
                '</div>'
            );
        }

        return '';
    }

    function createCard(item) {
        var c = document.createElement('div');
        c.className = 'video-card animate__animated animate__fadeInUp';
        c.id = 'card-' + item.id;
        applyVideoCardAspectClass(c, item);
        c.innerHTML = cardHTML(item);
        return c;
    }


    function updateCard(item, opts) {
        var c = document.getElementById('card-' + item.id);
        if (!c) return;
        // opts.srcOnly: chỉ update src video hiện có, không render lại toàn card
        if (opts && opts.srcOnly && item.localUrl) {
            var v = document.getElementById('vid-' + item.id);
            if (v) {
                applyVideoElementSource(v, toPlayableSrc(item.localUrl), 'inline');
            }
            patchCardDownloadHref(item);
            return;
        }
        c.innerHTML = cardHTML(item);
        applyVideoCardAspectClass(c, item);
        if (item.status === 'done') {
            c.classList.add('animate__animated', 'animate__pulse');
            // Chỉ show toast 1 lần per item
            if (!item._toastShown) {
                item._toastShown = true;
                showToast('success', 'Video hoàn thành!', item.prompt.substring(0, 60) + (item.prompt.length > 60 ? '…' : ''));
            }
            setTimeout(function () { c.classList.remove('animate__pulse'); }, 900);
            hydrateDoneCardMedia(item);
        }
        if (item.status === 'error') {
            showToast('error', 'Tạo video thất bại', item.errorMsg || 'Lỗi không xác định');
        }
    }


    function updateProg(id, pct, sub) {
        var item = null;
        for (var i = 0; i < state.videos.length; i++) {
            if (state.videos[i].id === id) { item = state.videos[i]; break; }
        }
        if (!item) return;
        item.progress = pct;
        var pe = document.getElementById('pct-' + id);
        var fe = document.getElementById('fill-' + id);
        var se = document.getElementById('sub-' + id);
        if (pe) pe.textContent = pct + '%';
        if (fe) fe.style.width  = pct + '%';
        if (se) se.textContent  = sub || '';
    }

    /* ═══════════════════════════════════════════════
       POLLING
    ═══════════════════════════════════════════════ */
    function debugLog(msg, data) {
        // try {
        //     if (typeof console !== 'undefined' && console.log) {
        //         if (typeof data !== 'undefined') console.log('[grok-ui] ' + msg, data);
        //         else console.log('[grok-ui] ' + msg);
        //     }
        // } catch (_) {}
    }

    function safeJsonParse(text) {
        if (typeof text !== 'string') return null;
        try { return JSON.parse(text); } catch (_) { return null; }
    }

    function decodeHtmlEntities(text) {
        if (typeof text !== 'string') return text;
        return text
            .replace(/&quot;/g, '"')
            .replace(/&#x27;/g, "'")
            .replace(/&#39;/g, "'")
            .replace(/&amp;/g, '&')
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>');
    }

    function decodeUnicodeEscapes(text) {
        if (typeof text !== 'string') return text;
        return text.replace(/\\u([0-9a-fA-F]{4})/g, function (_, hex) {
            try { return String.fromCharCode(parseInt(hex, 16)); } catch (_) { return _; }
        });
    }

    function unescapeJsonLikeValue(text) {
        if (typeof text !== 'string') return '';
        return decodeUnicodeEscapes(text)
            .replace(/\\r/g, '\r')
            .replace(/\\n/g, '\n')
            .replace(/\\t/g, '\t')
            .replace(/\\"/g, '"')
            .replace(/\\\\/g, '\\');
    }

    function extractJsonLikeField(text, fieldName) {
        if (typeof text !== 'string' || !fieldName) return '';
        var escName = String(fieldName).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        var re = new RegExp('"' + escName + '"\\s*:\\s*"((?:\\\\.|[^"\\\\])*)"', 'i');
        var m = text.match(re);
        if (!m || !m[1]) return '';
        return unescapeJsonLikeValue(m[1]).trim();
    }

    function extractJsonLikeRawToken(text, fieldName) {
        if (typeof text !== 'string' || !fieldName) return '';
        var escName = String(fieldName).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        var re = new RegExp('"' + escName + '"\\s*:\\s*([^,\\r\\n}\\]]+)', 'i');
        var m = text.match(re);
        if (!m || !m[1]) return '';
        return String(m[1]).trim().replace(/^["']|["']$/g, '');
    }

    function extractJsonLikeBoolField(text, fieldName) {
        if (typeof text !== 'string' || !fieldName) return null;
        var escName = String(fieldName).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        var re = new RegExp('"' + escName + '"\\s*:\\s*(true|false)', 'i');
        var m = text.match(re);
        if (!m || !m[1]) return null;
        return String(m[1]).toLowerCase() === 'true';
    }

    /** Loose token from backend (unquoted values, JSON-style escapes). */
    function normalizeLooseJsonToken(token) {
        if (token == null) return '';
        var s = String(token).trim().replace(/^["']|["']$/g, '');
        s = unescapeJsonLikeValue(s);
        return s.replace(/\\\\/g, '\\').trim();
    }

    function normalizeStatusToState(statusValue, isSuccessValue) {
        var s = String(statusValue == null ? '' : statusValue).trim().toLowerCase();
        if (s === '200') return 'done';
        if (s === 'done' || s === 'success' || s === 'ok') return 'done';
        if (s === 'failed' || s === 'error' || s === '500' || s === '400' || s === '404') return 'failed';
        if (s === 'processing' || s === 'pending' || s === 'running' || s === 'in_progress') return 'processing';

        var n = parseInt(s, 10);
        if (!isNaN(n)) return n === 200 ? 'done' : 'failed';

        if (isSuccessValue === true) return 'done';
        if (isSuccessValue === false) return 'failed';
        return 'processing';
    }

    /** Near-JSON dataImages blob (invalid JSON.parse); same extractJsonLike* idea as linkVideo. */
    function parseLooseDataImageRecord(text) {
        if (typeof text !== 'string' || !String(text).trim()) return null;
        var t = decodeUnicodeEscapes(String(text).trim());
        var imageGuid = normalizeLooseJsonToken(extractJsonLikeRawToken(t, 'imageGuid'));
        if (!imageGuid) return null;
        var linkRaw = extractJsonLikeField(t, 'linkImage');
        if (!linkRaw) linkRaw = extractJsonLikeRawToken(t, 'linkImage');
        var status = normalizeLooseJsonToken(extractJsonLikeRawToken(t, 'status') || extractJsonLikeField(t, 'status'));
        var ok = extractJsonLikeBoolField(t, 'isSuccess');
        var normalizedStatus = normalizeStatusToState(status, ok);
        return {
            imageGuid:      imageGuid,
            uploadGuid:     normalizeLooseJsonToken(extractJsonLikeRawToken(t, 'uploadGuid')),
            fileName:       normalizeLooseJsonToken(extractJsonLikeRawToken(t, 'fileName')),
            status:         normalizedStatus,
            isSuccess:      normalizedStatus === 'done',
            linkImage:      normalizeLooseJsonToken(linkRaw),
            fileMetadataId: normalizeLooseJsonToken(extractJsonLikeRawToken(t, 'fileMetadataId')),
            error:          normalizeLooseJsonToken(extractJsonLikeField(t, 'error') || extractJsonLikeRawToken(t, 'error'))
        };
    }

    function normalizePromptText(s) {
        return String(s == null ? '' : s).trim().replace(/\s+/g, ' ').toLowerCase();
    }

    var pendingCardsByPromptKey = {};

    function pendingPromptKey(guildId, promptText) {
        return String(guildId || '').trim() + '::' + normalizePromptText(promptText || '');
    }

    function registerPendingCard(item) {
        if (!item || item.status !== 'generating') return;
        var key = pendingPromptKey(item.guildId, item.prompt);
        if (!pendingCardsByPromptKey[key]) pendingCardsByPromptKey[key] = [];
        pendingCardsByPromptKey[key].push(item.id);
    }

    function unregisterPendingCard(item) {
        if (!item) return;
        var key = pendingPromptKey(item.guildId, item.prompt);
        var queue = pendingCardsByPromptKey[key];
        if (!queue || !queue.length) return;
        var next = [];
        for (var i = 0; i < queue.length; i++) {
            if (queue[i] !== item.id) next.push(queue[i]);
        }
        if (next.length) pendingCardsByPromptKey[key] = next;
        else delete pendingCardsByPromptKey[key];
    }

    function findGeneratingCardById(id) {
        var cid = String(id || '').trim();
        if (!cid) return null;
        for (var i = 0; i < state.videos.length; i++) {
            var it = state.videos[i];
            if (!it || it.status !== 'generating') continue;
            if (String(it.id || '') === cid ||
                String(it.requestId || '') === cid ||
                String(it.videoGuid || '') === cid) return it;
        }
        return null;
    }

    function findPendingCardForResult(guildId, result) {
        var reqId = String(
            (result && (result.requestId || result.cardId || result.taskId || result.itemId || result.id)) || ''
        ).trim();
        if (reqId) {
            var byId = findGeneratingCardById(reqId);
            if (byId) return byId;
        }

        var resultPromptNorm = normalizePromptText(result && result.prompt ? result.prompt : '');
        if (resultPromptNorm) {
            var key = String(guildId || '').trim() + '::' + resultPromptNorm;
            var queue = pendingCardsByPromptKey[key] || [];
            while (queue.length > 0) {
                var itemId = queue.shift();
                var byQueue = findGeneratingCardById(itemId);
                if (byQueue && byQueue.guildId === guildId) return byQueue;
            }
            if (pendingCardsByPromptKey[key] && pendingCardsByPromptKey[key].length === 0) {
                delete pendingCardsByPromptKey[key];
            }
        }

        for (var j = 0; j < state.videos.length; j++) {
            if (state.videos[j].guildId === guildId && state.videos[j].status === 'generating') {
                return state.videos[j];
            }
        }
        return null;
    }

    function parseVideoResultFromEntry(raw) {
        function toStatusCode(val) {
            if (val == null) return null;
            var n = parseInt(String(val).trim(), 10);
            return isNaN(n) ? null : n;
        }
        if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
            var o = raw;
            var vu = o.linkVideo || o.videoUrl || o.url || '';
            var lp = o.localPath || o.filePath || o.path || '';
            var pp = o.prompt || o.textPrompt || '';
            var rq = o.requestId || o.videoGuid || o.cardId || o.taskId || o.itemId || o.id || '';
            var vg = o.videoGuid || o.requestId || o.cardId || o.taskId || o.itemId || o.id || '';
            var gid = o.guildId || o.outputGuildId || '';
            var stCode = toStatusCode(o.status);
            var ar = normalizeAspectValue(o.aspect || o.aspectRatio || '');
            var ln = normalizeLengthValue(o.length || o.videoLength || o.durationSec || o.duration || 0);
            var rs = normalizeResolutionValue(o.resolution || o.res || o.quality || '');
            if (vu != null && typeof vu !== 'string') vu = String(vu);
            if (lp != null && typeof lp !== 'string') lp = String(lp);
            if (pp != null && typeof pp !== 'string') pp = String(pp);
            if (rq != null && typeof rq !== 'string') rq = String(rq);
            if (vg != null && typeof vg !== 'string') vg = String(vg);
            if (gid != null && typeof gid !== 'string') gid = String(gid);
            vu = (vu || '').trim();
            lp = (lp || '').trim();
            pp = (pp || '').trim();
            rq = (rq || '').trim();
            vg = (vg || '').trim();
            gid = (gid || '').trim();
            if (!vu && lp) return { videoUrl: toFileUri(lp), localPath: lp, prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            if (vu && lp) return { videoUrl: vu.replace(/\\\//g, '/'), localPath: lp, prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            if (vu) {
                if (/^file:\/\//i.test(vu)) {
                    var lpF = fileUriToLocalPath(vu);
                    return { videoUrl: vu.replace(/\\\//g, '/'), localPath: lpF || '', prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
                }
                if (/^[a-zA-Z]:[\\/]/.test(vu) || vu.indexOf('\\') >= 0 || vu.indexOf('/') >= 0) {
                    var lpW = vu.replace(/\//g, '\\');
                    return { videoUrl: toFileUri(lpW), localPath: lpW, prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
                }
                return { videoUrl: vu.replace(/\\\//g, '/'), localPath: '', prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            }
            if (stCode !== null) return { videoUrl: '', localPath: '', prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            return null;
        }
        if (typeof raw !== 'string') return null;
        var text = decodeHtmlEntities(raw).trim().replace(/^\uFEFF/, '').replace(/^["']|["']$/g, '');
        text = decodeUnicodeEscapes(text);
        if (!text) return null;

        var parsedObj = safeJsonParse(text);
        if (typeof parsedObj === 'string') parsedObj = safeJsonParse(parsedObj);
        if (parsedObj && typeof parsedObj === 'object' && !Array.isArray(parsedObj)) {
            return parseVideoResultFromEntry(parsedObj);
        }
        var promptFromJsonLike = extractJsonLikeField(text, 'prompt');
        var linkFromJsonLike = extractJsonLikeField(text, 'linkVideo')
            || extractJsonLikeField(text, 'videoUrl')
            || extractJsonLikeField(text, 'url');
        var reqFromJsonLike = extractJsonLikeField(text, 'requestId')
            || extractJsonLikeField(text, 'cardId')
            || extractJsonLikeField(text, 'taskId')
            || extractJsonLikeField(text, 'itemId')
            || extractJsonLikeField(text, 'id')
            || extractJsonLikeRawToken(text, 'requestId')
            || extractJsonLikeRawToken(text, 'cardId')
            || extractJsonLikeRawToken(text, 'taskId')
            || extractJsonLikeRawToken(text, 'itemId')
            || extractJsonLikeRawToken(text, 'id');
        var aspectFromJsonLike = normalizeAspectValue(
            extractJsonLikeField(text, 'aspect')
            || extractJsonLikeField(text, 'aspectRatio')
            || extractJsonLikeRawToken(text, 'aspect')
            || extractJsonLikeRawToken(text, 'aspectRatio')
        );
        var lengthFromJsonLike = normalizeLengthValue(
            extractJsonLikeField(text, 'length')
            || extractJsonLikeField(text, 'videoLength')
            || extractJsonLikeField(text, 'durationSec')
            || extractJsonLikeRawToken(text, 'length')
            || extractJsonLikeRawToken(text, 'videoLength')
            || extractJsonLikeRawToken(text, 'durationSec')
            || 0
        );
        var resolutionFromJsonLike = normalizeResolutionValue(
            extractJsonLikeField(text, 'resolution')
            || extractJsonLikeField(text, 'res')
            || extractJsonLikeField(text, 'quality')
            || extractJsonLikeRawToken(text, 'resolution')
            || extractJsonLikeRawToken(text, 'res')
            || extractJsonLikeRawToken(text, 'quality')
            || ''
        );
        var statusFromJsonLike = extractJsonLikeField(text, 'status')
            || extractJsonLikeRawToken(text, 'status');
        var statusCodeFromJsonLike = (function () {
            if (statusFromJsonLike == null) return null;
            var n = parseInt(String(statusFromJsonLike).trim(), 10);
            return isNaN(n) ? null : n;
        })();
        if (linkFromJsonLike) {
            var payload = {
                prompt: promptFromJsonLike || '',
                linkVideo: linkFromJsonLike,
                requestId: reqFromJsonLike || '',
                aspect: aspectFromJsonLike,
                length: lengthFromJsonLike,
                resolution: resolutionFromJsonLike
            };
            if (statusCodeFromJsonLike !== null) payload.status = statusCodeFromJsonLike;
            return parseVideoResultFromEntry({
                prompt: payload.prompt,
                linkVideo: payload.linkVideo,
                requestId: payload.requestId,
                status: payload.status,
                aspect: payload.aspect,
                length: payload.length,
                resolution: payload.resolution
            });
        }
        if (statusCodeFromJsonLike !== null) {
            return parseVideoResultFromEntry({
                prompt: promptFromJsonLike || '',
                requestId: reqFromJsonLike || '',
                status: statusCodeFromJsonLike,
                aspect: aspectFromJsonLike,
                length: lengthFromJsonLike,
                resolution: resolutionFromJsonLike
            });
        }

        // Local file URI — luôn gắn localPath để hydrate gọi acResolveLocalPath
        if (/^file:\/\//i.test(text)) {
            var lpFromFile = fileUriToLocalPath(text);
            return { videoUrl: text, localPath: lpFromFile || '' };
        }

        // Local absolute path (Windows / Unix-like)
        if (/^[a-zA-Z]:[\\/]/.test(text) || /^\//.test(text)) {
            return { videoUrl: toFileUri(text), localPath: text };
        }

        // Direct URL
        if (/^https?:\/\//i.test(text)) return { videoUrl: text.replace(/\\\//g, '/'), localPath: '' };

        // curl --location 'https://...'
        var m1 = text.match(/--location\s+'([^']+)'/i);
        if (m1 && m1[1]) return { videoUrl: m1[1].trim().replace(/\\\//g, '/'), localPath: '' };

        // curl --location "https://..."
        var m2 = text.match(/--location\s+"([^"]+)"/i);
        if (m2 && m2[1]) return { videoUrl: m2[1].trim().replace(/\\\//g, '/'), localPath: '' };

        // curl --location 'C:\path\to\video.mp4' / "D:\...\video.mp4"
        var m4 = text.match(/--location\s+'([a-zA-Z]:[\\/][^']+\.mp4)'/i);
        if (m4 && m4[1]) return { videoUrl: toFileUri(m4[1].trim()), localPath: m4[1].trim() };
        var m5 = text.match(/--location\s+"([a-zA-Z]:[\\/][^"]+\.mp4)"/i);
        if (m5 && m5[1]) return { videoUrl: toFileUri(m5[1].trim()), localPath: m5[1].trim() };

        // Fallback: first local path ending with .mp4
        var m6 = text.match(/[a-zA-Z]:[\\/][^'"\\r\\n]+\.mp4/i);
        if (m6 && m6[0]) return { videoUrl: toFileUri(m6[0].trim()), localPath: m6[0].trim() };

        // Fallback: first URL inside the payload
        var m3 = text.match(/https?:\/\/[^\s'"\\]+/i);
        var out = m3 && m3[0] ? m3[0].trim() : '';
        return out ? { videoUrl: out.replace(/\\\//g, '/'), localPath: '' } : null;
    }

    function collectNewReadyResults(job, list) {
        var newResults = [];
        if (!Array.isArray(list)) return newResults;

        for (var i = 0; i < list.length; i++) {
            if (job.handledIndexes[i]) continue;
            var raw = list[i];
            var parsed = parseVideoResultFromEntry(raw);
            if (!parsed) continue;
            if (!parsed.videoUrl && parsed.statusCode !== 200) continue;
            job.handledIndexes[i] = true;
            newResults.push(parsed);
        }
        return newResults;
    }

    var latestDatasDict = null;
    var asyncPushReceiverBound = false;
    var asyncVideoJobs = {};
    var pendingLoadVideoCacheGuildId = '';
    /** Snapshot dict loadVideoDatas (cùng dạng guid → entry như datas) khi host trả về sau statusCreate=2. */
    var latestLoadVideoDatasDict = null;
    /** Poll riêng cho nút tải cache (không dùng pollJobs của generate). */
    var loadVideoDatasPollJobs = {};
    /** Khi true, tick poll loadVideoDatas (1s) bỏ qua (đang xóa gallery / vừa dừng poll). */
    var loadVideoDatasPollPaused = false;

    function stopAllLoadVideoDatasPolling() {
        Object.keys(loadVideoDatasPollJobs).forEach(function (oldG) {
            var j = loadVideoDatasPollJobs[oldG];
            if (j && j.interval) {
                try { clearInterval(j.interval); } catch (_) {}
            }
            delete loadVideoDatasPollJobs[oldG];
        });
    }
    var downloadedPaths = {};
    var requestedDownloadKeys = {};
    var requestedDownloadUrls = {};
    var downloadMetaByKey = {};
    var downloadItemIdByKey = {};

    /** Set các guildId đã được ingest từ datas dict → không tạo card trùng lặp. */
    var handledGuildIds = {};
    /** Set các guildId đang trong quá trình hydrate (resolve local path) → không gọi lại. */
    var hydratingGuildIds = {};

    function bindAsyncPushReceiver() {
        if (asyncPushReceiverBound) return;
        if (!window.__acAsync || typeof window.__acAsync.onReceive !== 'function') return;

        function toObjectMaybe(v) {
            if (v && typeof v === 'object') return v;
            if (typeof v !== 'string') return null;
            try { return JSON.parse(v); } catch (_) { return null; }
        }

        function handleAsyncVideoObject(obj) {
            var parsed = parseVideoResultFromEntry(obj);
            if (!parsed) return false;

            var gid = String(parsed.guildId || obj.guildId || obj.outputGuildId || '').trim();
            var req = String(parsed.videoGuid || parsed.requestId || obj.videoGuid || obj.requestId || obj.cardId || obj.id || '').trim();
            if (!gid && !req) return false;

            var target = req ? findGeneratingCardById(req) : null;
            if (!target && gid) {
                for (var i = 0; i < state.videos.length; i++) {
                    var it = state.videos[i];
                    if (!it || it.status !== 'generating') continue;
                    if (String(it.guildId || '') === gid) { target = it; break; }
                }
            }
            if (!target) return false;

            unregisterPendingCard(target);
            if (parsed.videoGuid) target.videoGuid = parsed.videoGuid;
            if (parsed.localPath) target.localPath = parsed.localPath;
            if (parsed.videoUrl) target.videoUrl = parsed.videoUrl;
            if (parsed.aspect) target.aspect = normalizeAspectValue(parsed.aspect) || target.aspect;
            if (parsed.length) target.length = normalizeLengthValue(parsed.length) || target.length;
            if (parsed.resolution) target.res = normalizeResolutionValue(parsed.resolution) || target.res;

            if (parsed.statusCode !== null && parsed.statusCode !== 200) {
                target.status = 'error';
                target.errorMsg = String(obj.error || ('Tạo video thất bại (status=' + parsed.statusCode + ')'));
            } else if (parsed.videoUrl || parsed.localPath) {
                target.status = 'done';
            }

            if (target.status !== 'generating') {
                target.progress = 100;
                state.processing = Math.max(0, state.processing - 1);
            }
            updateCard(target);
            updateStats();

            var jobGid = gid || String(target.guildId || '');
            if (jobGid) {
                var hasGenerating = false;
                for (var j = 0; j < state.videos.length; j++) {
                    var gitem = state.videos[j];
                    if (gitem && String(gitem.guildId || '') === jobGid && gitem.status === 'generating') {
                        hasGenerating = true;
                        break;
                    }
                }
                if (!hasGenerating && asyncVideoJobs[jobGid]) {
                    var doneCb = asyncVideoJobs[jobGid].onFinished;
                    delete asyncVideoJobs[jobGid];
                    delete pollJobs[jobGid];
                    if (typeof doneCb === 'function') doneCb(false);
                }
            }
            return true;
        }

        function handleAsyncDatasPayload(value) {
            var dict = pickResultDictionary({ datas: value });
            latestDatasDict = dict;
            if (!dict || typeof dict !== 'object') return;

            Object.keys(asyncVideoJobs).forEach(function (gid) {
                var job = asyncVideoJobs[gid];
                if (!job) return;
                var res = resolveResultList(dict, gid);
                if (!res || !Array.isArray(res)) return;

                var tempJob = pollJobs[gid] || {
                    guildId: gid,
                    expectedCount: job.expectedCount || 0,
                    renderedCount: 0,
                    handledIndexes: {},
                    interval: null,
                    startTime: Date.now(),
                    onResult: job.onResult
                };
                var newItems = collectNewReadyResults(tempJob, res);
                if (newItems.length > 0) {
                    tempJob.renderedCount += newItems.length;
                    if (typeof job.onResult === 'function') {
                        job.onResult(newItems, tempJob.renderedCount, job.expectedCount || 0);
                    }
                }
                pollJobs[gid] = tempJob;
            });
        }

        function handleAsyncDataImagesPayload(value) {
            if (typeof window.__grokApplyAsyncDataImages !== 'function') return;
            window.__grokApplyAsyncDataImages(value);
        }

        function handleAsyncLoadVideoDatasPayload(value) {
            var dict = pickLoadVideoDatasDictionary({ loadVideoDatas: value });
            latestLoadVideoDatasDict = dict;
            if (!dict || typeof dict !== 'object') return;
            ingestDatasDict(dict);

            if (pendingLoadVideoCacheGuildId) {
                var hit = findGuildKeyInDict(dict, pendingLoadVideoCacheGuildId);
                if (hit != null && handledGuildIds[hit]) {
                    stopAllLoadVideoDatasPolling();
                    resetLoadCacheVideosBtnUi();
                    pendingLoadVideoCacheGuildId = '';
                    showToast('success', 'loadVideoDatas', 'Đã nhận dữ liệu và hiển thị video.', 3500);
                }
            }
        }

        window.__acAsync.onReceive('datas', function (value) {
            handleAsyncDatasPayload(value);
        });

        window.__acAsync.onReceive('dataImages', function (value) {
            handleAsyncDataImagesPayload(value);
        });

        window.__acAsync.onReceive('loadVideoDatas', function (value) {
            handleAsyncLoadVideoDatasPayload(value);
        });

        // Backward compatibility: nếu host vẫn push key "item" thì cố gắng route mềm.
        window.__acAsync.onReceive('item', function (value) {
            var obj = toObjectMaybe(value);
            if (!obj) return;
            if (Array.isArray(obj)) {
                if (obj.length > 0 && obj[0] && typeof obj[0] === 'object' && obj[0].imageGuid) {
                    handleAsyncDataImagesPayload(obj);
                    return;
                }
                handleAsyncDatasPayload(obj);
                return;
            }
            if (obj.imageGuid) {
                handleAsyncDataImagesPayload(obj);
                return;
            }
            if (obj.videoGuid || obj.requestId || obj.guildId || obj.outputGuildId || obj.linkVideo) {
                if (!handleAsyncVideoObject(obj)) handleAsyncDatasPayload(obj);
                return;
            }
        });

        asyncPushReceiverBound = true;
        debugLog('Async receiver bound: datas/dataImages/loadVideoDatas');
    }

    bindAsyncPushReceiver();

    window.addEventListener('__ac_curl_download_done', function (ev) {
        try {
            var d = ev && ev.detail ? ev.detail : {};
            if (!d || !d.path) return;
            if (downloadedPaths[d.path]) return;
            downloadedPaths[d.path] = true;
            var key = d.key || '';
            var meta = key ? downloadMetaByKey[key] : null;
            if (d.ok) {
                var target = null;
                var itemId = key ? downloadItemIdByKey[key] : '';
                if (itemId) {
                    for (var i = 0; i < state.videos.length; i++) {
                        if (state.videos[i].id === itemId) { target = state.videos[i]; break; }
                    }
                }
                if (!target && meta && meta.guildId) {
                    for (var j = 0; j < state.videos.length; j++) {
                        var v = state.videos[j];
                        if (v.guildId !== meta.guildId) continue;
                        if (meta.remoteUrl && v.videoUrl === meta.remoteUrl) { target = v; break; }
                    }
                }
                if (target) {
                    target.localPath = d.path;
                    target.localUrl = d.localUrl || '';
                    target.videoUrl = d.localUrl || toFileUri(d.path) || target.videoUrl;
                    updateCard(target);
                    if (!target.localUrl && target.localPath) hydrateLocalPlayable(target);
                }
                showToast('success', 'Đã tải video', String(d.path), 5000);
            } else {
                showToast('error', 'Tải video thất bại', d.error || 'Unknown error', 6000);
            }
        } catch (_) {}
    });

    window.addEventListener('__ac_local_path_resolved', function (ev) {
        try {
            var d = ev && ev.detail ? ev.detail : {};
            var reqId = d && d.requestId ? String(d.requestId) : '';
            if (!reqId) return;
            var waiter = localPathResolveWaiters[reqId];
            if (!waiter) return;
            delete localPathResolveWaiters[reqId];
            var resolvedUrl = (d && d.ok && d.localUrl) ? String(d.localUrl) : '';
            waiter(resolvedUrl);

            // Nếu video/img đã thử load trước khi mapping xong thì WebView2 có thể không tự retry.
            // Refresh lại các <video> inline tương ứng để nó load lại ngay sau khi resolve.
            if (!resolvedUrl) return;

            var resolvedPath = d && d.path ? String(d.path) : '';
            var baseName = '';
            try {
                if (resolvedPath) {
                    var m = resolvedPath.replace(/\\/g, '/').split('/');
                    baseName = m.length ? m[m.length - 1] : '';
                }
            } catch (_) { baseName = ''; }

            // Update state.videos để lần render sau có localUrl đúng
            try {
                for (var i = 0; i < state.videos.length; i++) {
                    var it = state.videos[i];
                    if (!it || it.status !== 'done') continue;
                    if (resolvedPath && it.localPath && String(it.localPath).trim() === resolvedPath) {
                        it.localUrl = resolvedUrl;
                        it.videoUrl = resolvedUrl;
                    } else if (baseName && (String(it.videoUrl || '').trim() === baseName || String(it.localUrl || '').trim() === baseName)) {
                        // trường hợp item đang giữ tên file trơn
                        it.localUrl = resolvedUrl;
                        it.videoUrl = resolvedUrl;
                    }
                }
            } catch (_) {}

            // Refresh DOM video
            try {
                var vids = document.querySelectorAll('video.card-video-inline');
                for (var j = 0; j < vids.length; j++) {
                    var v = vids[j];
                    if (!v) continue;
                    var s = '';
                    try { s = String(v.getAttribute('src') || v.src || '').trim(); } catch (_) { s = ''; }
                    if (!s) continue;

                    var match = false;
                    if (s === resolvedUrl) match = true;
                    else if (baseName && s.indexOf(baseName) >= 0) match = true;

                    if (match) {
                        try {
                            applyVideoElementSource(v, withCacheBust(resolvedUrl));
                        } catch (_) {}
                    }
                }
            } catch (_) {}
        } catch (_) {}
    });

    function normalizeDictMaybe(raw) {
        if (raw && typeof raw === 'object' && !Array.isArray(raw)) return raw;
        var parsed = safeJsonParse(raw);
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed;

        if (typeof raw === 'string') {
            var t = decodeHtmlEntities(raw).trim();
            // remove code fences if runtime accidentally wraps payload
            t = t.replace(/^```[a-zA-Z]*\s*/, '').replace(/\s*```$/, '');
            parsed = safeJsonParse(t);
            if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed;
        }
        return null;
    }

    function normalizeKeyText(v) {
        return String(v == null ? '' : v).trim().replace(/^['"]|['"]$/g, '').toLowerCase();
    }

    /** Giá trị mapping C# có thể là object { filePath, ... } thay vì chuỗi. */
    function coerceMappingValueToMediaString(v) {
        if (v == null) return null;
        if (typeof v === 'string') {
            var t = v.trim();
            return t || null;
        }
        if (typeof v === 'object' && !Array.isArray(v)) {
            var o = v;
            var s = o.linkVideo || o.filePath || o.localPath || o.path || o.videoUrl || o.url || o.file || '';
            if (s != null && typeof s !== 'string') s = String(s);
            var t2 = (s || '').trim();
            return t2 || null;
        }
        return null;
    }

    function wrapResultValue(v) {
        if (v == null) return null;
        if (Array.isArray(v)) return v;
        if (typeof v === 'string' && v.trim()) return [v.trim()];
        if (typeof v === 'object' && !Array.isArray(v)) return [v];
        var c = coerceMappingValueToMediaString(v);
        return c ? [c] : null;
    }

    function resolveResultList(dict, guildId) {
        if (!dict || typeof dict !== 'object') return null;
        var gid = String(guildId || '').trim();
        if (!gid) return null;

        var hit = wrapResultValue(dict[gid]);
        if (hit) return hit;

        var gidNorm = normalizeKeyText(gid);
        var keys = Object.keys(dict);
        for (var i = 0; i < keys.length; i++) {
            if (normalizeKeyText(keys[i]) === gidNorm) {
                return wrapResultValue(dict[keys[i]]);
            }
        }
        return null;
    }

    /**
     * Gom dữ liệu giống "datas" từ __ac.live khi không có key 'datas':
     * host có thể __acPush(từng key) hoặc chuỗi JSON — guid → path nằm top-level.
     */
    function buildFlatDatasFromLive() {
        var live = window.__ac && window.__ac.live;
        if (!live || typeof live !== 'object') return null;
        var out = {};
        var n = 0;
        Object.keys(live).forEach(function (k) {
            if (k === '_callbacks' || k === 'outputGuildId' || k === 'outputCacheGuildId' || k === 'cacheDatas' || k === 'loadVideoDatas') return;
            var v = live[k];
            if (v == null) return;
            if (typeof v === 'string') {
                var t = v.trim();
                if (!t) return;
                if (t.charAt(0) === '{' || t.charAt(0) === '[') {
                    var p = normalizeDictMaybe(t);
                    if (p && typeof p === 'object' && !Array.isArray(p)) {
                        Object.keys(p).forEach(function (ik) {
                            out[ik] = p[ik];
                            n++;
                        });
                        return;
                    }
                }
                if (/^[a-zA-Z]:[\\/]/.test(t) || /^file:/i.test(t) || /^https?:/i.test(t) ||
                    /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test(t)) {
                    out[k] = v;
                    n++;
                }
            } else if (typeof v === 'object' && !Array.isArray(v)) {
                var added = 0;
                Object.keys(v).forEach(function (ik) {
                    out[ik] = v[ik];
                    added++;
                });
                if (added) n += added;
            }
        });
        return n > 0 ? out : null;
    }

    function pickResultDictionary(payload) {
        // Priority for HtmlUiNode input mapping:
        // onUpdate('datas', cb) -> live.datas -> global datas -> __ac.live
        var d1 = payload && payload.datas;
        var n1 = normalizeDictMaybe(d1);
        if (n1) return n1;

        var n2 = normalizeDictMaybe(payload);
        if (n2) return n2;

        var n3 = normalizeDictMaybe(latestDatasDict);
        if (n3) return n3;

        var d4 = window.__ac && window.__ac.live ? window.__ac.live.datas : null;
        var n4 = normalizeDictMaybe(d4);
        if (n4) return n4;

        var flat = buildFlatDatasFromLive();
        if (flat) return flat;

        var n5 = normalizeDictMaybe(window.datas);
        if (n5) return n5;

        var n6 = normalizeDictMaybe(window.__ac && window.__ac.datas ? window.__ac.datas : null);
        if (n6) return n6;

        return {};
    }

    /**
     * Gom dict loadVideoDatas — cùng ý nghĩa pickResultDictionary nhưng key riêng (sau statusCreate=2).
     */
    function pickLoadVideoDatasDictionary(payload) {
        var d1 = payload && payload.loadVideoDatas;
        var n1 = normalizeDictMaybe(d1);
        if (n1) return n1;

        var n2 = normalizeDictMaybe(payload);
        if (n2) return n2;

        var n3 = normalizeDictMaybe(latestLoadVideoDatasDict);
        if (n3) return n3;

        var d4 = window.__ac && window.__ac.live ? window.__ac.live.loadVideoDatas : null;
        var n4 = normalizeDictMaybe(d4);
        if (n4) return n4;

        var n5 = normalizeDictMaybe(typeof window.loadVideoDatas !== 'undefined' ? window.loadVideoDatas : null);
        if (n5) return n5;

        var n6 = normalizeDictMaybe(window.__ac && window.__ac.loadVideoDatas ? window.__ac.loadVideoDatas : null);
        if (n6) return n6;

        return {};
    }

    function findGuildKeyInDict(dict, guildId) {
        if (!dict || typeof dict !== 'object' || Array.isArray(dict)) return null;
        var g = String(guildId || '').trim();
        if (!g) return null;
        if (Object.prototype.hasOwnProperty.call(dict, g)) return g;
        var gn = normalizeKeyText(g);
        var keys = Object.keys(dict);
        for (var i = 0; i < keys.length; i++) {
            if (normalizeKeyText(keys[i]) === gn) return keys[i];
        }
        return null;
    }

    function resetLoadCacheVideosBtnUi() {
        var btn = document.getElementById('loadCacheVideosBtn');
        var icon = document.getElementById('loadCacheVideosIcon');
        if (btn) btn.disabled = false;
        if (icon) icon.className = 'bi bi-cloud-arrow-down';
    }

    /** Xóa gallery (state + DOM + ingest dedup) — dùng cho clear all và trước khi nhận loadVideoDatas mới. */
    function clearVideoGalleryStateAndDom() {
        state.videos = [];
        pendingCardsByPromptKey = {};
        handledGuildIds = {};
        hydratingGuildIds = {};
        var grid  = document.getElementById('videoGrid');
        var empty = document.getElementById('emptyState');
        if (grid) {
            grid.innerHTML = '';
            if (empty) grid.appendChild(empty);
        }
        updateStats();
    }

    function startLoadVideoDatasPolling(guildId) {
        bindAsyncPushReceiver();
        if (asyncPushReceiverBound) return;
        var g = String(guildId || '').trim();
        if (!g) return;
        if (loadVideoDatasPollJobs[g]) {
            try { clearInterval(loadVideoDatasPollJobs[g].interval); } catch (_) {}
        }
        var MAX_MS = 180000;
        var startTime = Date.now();
        var iv = setInterval(function () {
            if (loadVideoDatasPollPaused) return;
            if (Date.now() - startTime > MAX_MS) {
                try { clearInterval(iv); } catch (_) {}
                delete loadVideoDatasPollJobs[g];
                resetLoadCacheVideosBtnUi();
                showToast('warn', 'loadVideoDatas', 'Timeout 180s: không nhận được dữ liệu.', 5000);
                return;
            }
            try {
                var dict = pickLoadVideoDatasDictionary();
                var dk = findGuildKeyInDict(dict, g);
                if (dk == null) return;
                var raw = dict[dk];
                if (raw == null) return;

                var wasHandled = !!handledGuildIds[dk];
                latestLoadVideoDatasDict = dict;
                var slice = {};
                slice[dk] = raw;
                ingestDatasDict(slice);

                if (handledGuildIds[dk]) {
                    try { clearInterval(iv); } catch (_) {}
                    delete loadVideoDatasPollJobs[g];
                    resetLoadCacheVideosBtnUi();
                    if (!wasHandled) {
                        showToast('success', 'loadVideoDatas', 'Đã nhận dữ liệu và hiển thị video.', 3500);
                    }
                }
            } catch (_) {}
        }, 1000);

        loadVideoDatasPollJobs[g] = { interval: iv, startTime: startTime };
    }

    /**
     * Nút gallery: gửi workflow giống Tạo video — outputGuildId + outputParams + statusCreate=2,
     * host điền loadVideoDatas[guildId] giống datas → poll + ingestDatasDict.
     */
    window.requestLoadVideoCacheFromHost = function () {
        stopAllLoadVideoDatasPolling();
        resetLoadCacheVideosBtnUi();

        if (state.processing > 0) {
            showToast('warn', 'Không thể tải cache', 'Còn ' + state.processing + ' video đang xử lý');
            return;
        }
        clearVideoGalleryStateAndDom();
        latestLoadVideoDatasDict = null;

        var guildId = uid();
        pendingLoadVideoCacheGuildId = guildId;
        var og = document.getElementById('outputGuildId');
        var op = document.getElementById('outputParams');
        var sc = document.getElementById('statusCreate');
        if (og) og.value = guildId;
        if (op) {
            op.value = JSON.stringify({
                guildId: guildId,
                action: 'loadVideoCache',
                timestamp: Date.now()
            });
        }
        if (sc) sc.value = '2';

        try { if (typeof acSubmit === 'function') acSubmit(); } catch (_) {}
        try { if (typeof acStartWorkflow === 'function') acStartWorkflow(); } catch (_) {}

        var btn = document.getElementById('loadCacheVideosBtn');
        var icon = document.getElementById('loadCacheVideosIcon');
        if (btn) btn.disabled = true;
        if (icon) icon.className = 'bi bi-hourglass-split';

        showToast('info', 'Tải cache video', 'Đã gửi statusCreate=2, chờ loadVideoDatas…', 3200);
        startLoadVideoDatasPolling(guildId);
    };

    /**
     * Nút xóa tất cả: gửi outputGuildId + outputParams + statusCreate=3 để host xóa data cache;
     * sau đó dọn gallery trong UI (giữ chặn khi còn video đang xử lý).
     */
    window.requestClearAllCacheFromHost = function () {
        if (state.processing > 0) {
            showToast('warn', 'Không thể xóa', 'Còn ' + state.processing + ' video đang xử lý');
            return;
        }
        var guildId = uid();
        var og = document.getElementById('outputGuildId');
        var op = document.getElementById('outputParams');
        var sc = document.getElementById('statusCreate');
        if (og) og.value = guildId;
        if (op) {
            op.value = JSON.stringify({
                guildId: guildId,
                action: 'clearCache',
                timestamp: Date.now()
            });
        }
        if (sc) sc.value = '3';

        try { if (typeof acSubmit === 'function') acSubmit(); } catch (_) {}
        try { if (typeof acStartWorkflow === 'function') acStartWorkflow(); } catch (_) {}

        clearVideoGalleryStateAndDom();
        showToast('info', 'Đã xóa', 'Đã dọn gallery và gửi statusCreate=3 (clear cache).', 2800);
    };

    function startPolling(guildId, expectedCount, onResult, onFinished) {
        bindAsyncPushReceiver();
        if (asyncPushReceiverBound) {
            asyncVideoJobs[guildId] = {
                expectedCount: expectedCount,
                onResult: onResult,
                onFinished: onFinished
            };
            pollJobs[guildId] = {
                guildId: guildId,
                expectedCount: expectedCount,
                renderedCount: 0,
                handledIndexes: {},
                interval: null,
                startTime: Date.now(),
                onResult: onResult
            };
            updateStats();
            return;
        }

        var MAX_MS = 300000;
        var job = {
            guildId:       guildId,
            expectedCount: expectedCount,
            renderedCount: 0,
            handledIndexes: {},
            interval:      null,
            startTime:     Date.now(),
            onResult:      onResult
        };

        job.interval = setInterval(function () {
            var timedOut = Date.now() - job.startTime > MAX_MS;

            if (timedOut) {
                clearInterval(job.interval);
                delete pollJobs[guildId];
                state.videos.forEach(function (v) {
                    if (v.guildId === guildId && v.status === 'generating') {
                        unregisterPendingCard(v);
                        v.status   = 'error';
                        v.errorMsg = 'Timeout 300s: không nhận được link video';
                        state.processing = Math.max(0, state.processing - 1);
                        updateCard(v);
                    }
                });
                updateStats();
                if (typeof onFinished === 'function') onFinished(true);
                return;
            }

            try {
                var dict = pickResultDictionary();
                var res  = resolveResultList(dict, guildId);
                debugLog('poll tick guild=' + guildId + ' keys=' + Object.keys(dict).length + ' listLen=' + (Array.isArray(res) ? res.length : 0) + ' sampleKeys=' + Object.keys(dict).slice(0, 3).join(','));
                if (res && Array.isArray(res)) {
                    var newItems = collectNewReadyResults(job, res);
                    if (newItems.length > 0) {
                        job.renderedCount += newItems.length;
                        onResult(newItems, job.renderedCount, expectedCount);
                    }
                    if (job.renderedCount >= expectedCount) {
                        clearInterval(job.interval);
                        delete pollJobs[guildId];
                        updateStats();
                        if (typeof onFinished === 'function') onFinished(false);
                    }
                }
            } catch (_) {}
        }, 1000);

        pollJobs[guildId] = job;
        updateStats();
    }

    /* ═══════════════════════════════════════════════
       SEND ONE BATCH
       Takes a batch object and fires it, then when
       all videos are done (or timeout), calls onDone.
    ═══════════════════════════════════════════════ */
    function sendBatch(batch, onDone) {
        var guildId       = batch.guildId;
        var prompts       = batch.prompts;
        var sendCount     = batch.sendCount;
        var aspect        = batch.aspect;
        var length        = batch.length;
        var res           = batch.res;
        var totalExpected = prompts.length * sendCount;
        handledGuildIds[guildId] = true; // guild này đã có card do flow generate quản lý
        var batchFinished = false;

        function finishBatchOnce(timedOut) {
            if (batchFinished) return;
            batchFinished = true;
            batch.done   = true;
            batch.active = false;
            renderBatchQueue();
            onDone(!!timedOut);
        }

        var requestItems = [];
        prompts.forEach(function (prompt) {
            for (var i = 0; i < sendCount; i++) {
                requestItems.push({
                    cardId: uid(),
                    requestId: uid(),
                    prompt: prompt
                });
            }
        });

        // Write PARAM outputs
        var og = document.getElementById('outputGuildId');
        var op = document.getElementById('outputParams');
        var sc = document.getElementById('statusCreate');
        if (og) og.value = guildId;
        if (op) op.value = JSON.stringify({
            guildId: guildId, prompts: prompts,
            aspect: aspect, length: length,
            resolution: res, sendCount: sendCount,
            totalExpected: totalExpected,
            requestItems: requestItems,
            requestIds: requestItems.map(function (x) { return x.requestId; }),
            cardIds: requestItems.map(function (x) { return x.cardId; }),
            timestamp: Date.now()
        });
        if (sc) sc.value = '0'; // 0 = tạo video

        try { if (typeof acSubmit        === 'function') acSubmit(); }        catch (_) {}
        try { if (typeof acStartWorkflow === 'function') acStartWorkflow(); } catch (_) {}

        // Create cards
        var grid  = document.getElementById('videoGrid');
        var empty = document.getElementById('emptyState');

        requestItems.forEach(function (req) {
            var item = {
                id: req.cardId, requestId: req.requestId, prompt: req.prompt, aspect: aspect,
                length: length, res: res,
                status: 'generating', progress: 0,
                videoUrl: '', thumbUrl: '', errorMsg: '',
                createdAt: Date.now(), guildId: guildId
            };
            state.videos.push(item);
            registerPendingCard(item);
            state.processing++;
            if (empty && empty.parentNode === grid) grid.removeChild(empty);
            grid.appendChild(createCard(item));
        });

        updateStats();

        // Simulate progress
        var generatingItems = state.videos.filter(function (v) {
            return v.guildId === guildId && v.status === 'generating';
        });
        var steps = ['Đang khởi tạo...', 'Xử lý prompt...', 'Render frames...', 'Đang hoàn thiện...'];
        generatingItems.forEach(function (item, idx) {
            var p  = 0;
            var iv = setInterval(function () {
                if (item.status !== 'generating') { clearInterval(iv); return; }
                p = Math.min(p + Math.random() * 2.5 + 0.3, 94);
                updateProg(item.id, Math.floor(p), steps[Math.min(Math.floor(p / 25), 3)]);
            }, 1800 + idx * 200);
        });

        // Poll for results
        startPolling(guildId, totalExpected, function (newLinks) {
            newLinks.forEach(function (result) {
                var pending = findPendingCardForResult(guildId, result);
                if (pending) {
                    unregisterPendingCard(pending);
                    if (result.localPath) pending.localPath = result.localPath;
                    if (result.videoUrl) pending.videoUrl = result.videoUrl;
                    if (result.aspect) pending.aspect = normalizeAspectValue(result.aspect) || pending.aspect;
                    if (result.length) pending.length = normalizeLengthValue(result.length) || pending.length;
                    if (result.resolution) pending.res = normalizeResolutionValue(result.resolution) || pending.res;
                    if (result.statusCode !== null && result.statusCode !== 200) {
                        pending.status = 'error';
                        pending.errorMsg = 'Tạo video thất bại (status=' + result.statusCode + ')';
                    } else {
                        pending.status = 'done';
                    }
                    pending.progress = 100;
                    state.processing = Math.max(0, state.processing - 1);
                    updateCard(pending);
                    updateStats();
                }
            });

            // Fallback: nếu tất cả card của guild đã thoát trạng thái generating thì kết thúc batch ngay.
            // Tránh kẹt queue trong một số runtime async khi poll count không khớp.
            var hasGenerating = false;
            for (var gi = 0; gi < state.videos.length; gi++) {
                var gitem = state.videos[gi];
                if (gitem && gitem.guildId === guildId && gitem.status === 'generating') {
                    hasGenerating = true;
                    break;
                }
            }
            if (!hasGenerating) {
                var job = pollJobs[guildId];
                if (job && job.interval) {
                    clearInterval(job.interval);
                    delete pollJobs[guildId];
                }
                updateStats();
                finishBatchOnce(false);
            }
        }, function (timedOut) {
            finishBatchOnce(timedOut);
        });
    }

    /* ═══════════════════════════════════════════════
       BATCH QUEUE RUNNER
       Processes batchQueue sequentially, one at a time.
    ═══════════════════════════════════════════════ */
    function runNextBatch() {
        // Find next pending batch
        var next = null;
        for (var i = 0; i < batchQueue.length; i++) {
            if (!batchQueue[i].done && !batchQueue[i].active) {
                next = batchQueue[i];
                break;
            }
        }

        if (!next) {
            // All done
            batchRunning = false;
            showToast('success', 'Tất cả đợt hoàn thành!', batchQueue.length + ' đợt đã xử lý xong', 4000);
            batchQueue = [];
            renderBatchQueue();
            return;
        }

        next.active = true;
        renderBatchQueue();
        showToast('info', 'Đợt tiếp theo', 'Đang gửi: ' + next.prompts.join(', ').substring(0, 50), 3000);

        sendBatch(next, function (/*timedOut*/) {
            runNextBatch();
        });
    }

    /* ═══════════════════════════════════════════════
       GENERATE
    ═══════════════════════════════════════════════ */
    window.generateVideo = function () {
        try {
            var raw       = (document.getElementById('promptInput').value || '').trim();
            var isMulti   = document.getElementById('multiPrompt').checked;
            var aspect    = document.getElementById('aspectRatio').value;
            var length    = parseInt(document.getElementById('videoLength').value, 10);
            var res       = document.getElementById('resolution').value;
            var sendCount = parseInt(document.getElementById('sendCount').value, 10);
            var batchSize = isMulti ? parseInt(document.getElementById('batchSize').value, 10) : 0;

            // Cách dùng đúng:
            // - promptInput chỉ chứa nội dung prompt (1 dòng hoặc nhiều dòng)
            // - outputGuildId/outputParams là hidden output nội bộ, không dán vào promptInput
            var allPrompts = isMulti
                ? sanitizePromptLines(raw.split('\n'))
                : (raw ? [raw] : []);

            if (!allPrompts.length) { showError('Vui lòng nhập ít nhất một prompt!'); return; }

            // Disable button
            var btn      = document.getElementById('generateBtn');
            var btnLabel = document.getElementById('btnLabel');
            var btnIcon  = document.getElementById('btnIcon');
            if (btn)      btn.disabled = true;
            if (btnLabel) btnLabel.textContent = 'ĐANG GỬI...';
            if (btnIcon)  {
                btnIcon.className = '';
                btnIcon.style.cssText =
                    'width:14px;height:14px;border:2px solid rgba(255,255,255,0.3);' +
                    'border-top-color:#fff;border-radius:50%;' +
                    'animation:spin-btn 0.7s linear infinite;display:inline-block';
            }

            // Build batches
            if (isMulti && batchSize > 0 && allPrompts.length > batchSize) {
                // Split into batches of batchSize
                var newBatches = [];
                for (var i = 0; i < allPrompts.length; i += batchSize) {
                    var chunk = allPrompts.slice(i, i + batchSize);
                    newBatches.push({
                        prompts:   chunk,
                        sendCount: sendCount,
                        aspect:    aspect,
                        length:    length,
                        res:       res,
                        guildId:   uid(),
                        label:     chunk.join(', '),
                        active:    false,
                        done:      false
                    });
                }

                batchQueue = batchQueue.concat(newBatches);
                renderBatchQueue();

                showToast('info', 'Xếp hàng ' + newBatches.length + ' đợt',
                    allPrompts.length + ' prompt, mỗi đợt ' + batchSize + ' × ' + sendCount + ' lần', 4000);

                if (!batchRunning) {
                    batchRunning = true;
                    runNextBatch();
                }
            } else {
                // Single shot — no batching
                var singleGuild = uid();
                var totalExp    = allPrompts.length * sendCount;
                handledGuildIds[singleGuild] = true; // tránh ingest tạo card done trùng
                var requestItems = [];
                allPrompts.forEach(function (prompt) {
                    for (var j = 0; j < sendCount; j++) {
                        requestItems.push({
                            cardId: uid(),
                            requestId: uid(),
                            prompt: prompt
                        });
                    }
                });

                var og = document.getElementById('outputGuildId');
                var op = document.getElementById('outputParams');
                var sc = document.getElementById('statusCreate');
                if (og) og.value = singleGuild;
                if (op) op.value = JSON.stringify({
                    guildId: singleGuild, prompts: allPrompts,
                    aspect: aspect, length: length,
                    resolution: res, sendCount: sendCount,
                    totalExpected: totalExp,
                    isMultiPrompt: isMulti,
                    requestItems: requestItems,
                    requestIds: requestItems.map(function (x) { return x.requestId; }),
                    cardIds: requestItems.map(function (x) { return x.cardId; }),
                    timestamp: Date.now()
                });
                if (sc) sc.value = '0'; // 0 = tạo video

                try { if (typeof acSubmit        === 'function') acSubmit(); }        catch (_) {}
                try { if (typeof acStartWorkflow === 'function') acStartWorkflow(); } catch (_) {}

                var grid  = document.getElementById('videoGrid');
                var empty = document.getElementById('emptyState');

                requestItems.forEach(function (req) {
                    var item = {
                        id: req.cardId, requestId: req.requestId, prompt: req.prompt, aspect: aspect,
                        length: length, res: res,
                        status: 'generating', progress: 0,
                        videoUrl: '', thumbUrl: '', errorMsg: '',
                        createdAt: Date.now(), guildId: singleGuild
                    };
                    state.videos.push(item);
                    registerPendingCard(item);
                    state.processing++;
                    if (empty && empty.parentNode === grid) grid.removeChild(empty);
                    grid.appendChild(createCard(item));
                });

                updateStats();
                showToast('info', 'Đã gửi yêu cầu', allPrompts.length + ' prompt × ' + sendCount + ' lần', 3000);

                var genItems = state.videos.filter(function (v) {
                    return v.guildId === singleGuild && v.status === 'generating';
                });
                var steps = ['Đang khởi tạo...', 'Xử lý prompt...', 'Render frames...', 'Đang hoàn thiện...'];
                genItems.forEach(function (item, idx) {
                    var p  = 0;
                    var iv = setInterval(function () {
                        if (item.status !== 'generating') { clearInterval(iv); return; }
                        p = Math.min(p + Math.random() * 2.5 + 0.3, 94);
                        updateProg(item.id, Math.floor(p), steps[Math.min(Math.floor(p / 25), 3)]);
                    }, 1800 + idx * 200);
                });

                startPolling(singleGuild, totalExp, function (newLinks) {
                    newLinks.forEach(function (result) {
                        var pending = findPendingCardForResult(singleGuild, result);
                        if (pending) {
                            unregisterPendingCard(pending);
                            if (result.localPath) pending.localPath = result.localPath;
                            if (result.videoUrl) pending.videoUrl = result.videoUrl;
                            if (result.aspect) pending.aspect = normalizeAspectValue(result.aspect) || pending.aspect;
                            if (result.length) pending.length = normalizeLengthValue(result.length) || pending.length;
                            if (result.resolution) pending.res = normalizeResolutionValue(result.resolution) || pending.res;
                            if (result.statusCode !== null && result.statusCode !== 200) {
                                pending.status = 'error';
                                pending.errorMsg = 'Tạo video thất bại (status=' + result.statusCode + ')';
                            } else {
                                pending.status = 'done';
                            }
                            pending.progress = 100;
                            state.processing = Math.max(0, state.processing - 1);
                            updateCard(pending);
                            updateStats();
                        }
                    });
                }, null);
            }

            // Restore button after short delay
            setTimeout(function () {
                if (btn)      btn.disabled = false;
                if (btnLabel) btnLabel.textContent = 'TẠO VIDEO';
                if (btnIcon)  { btnIcon.style.cssText = ''; btnIcon.className = 'bi bi-lightning-fill btn-icon'; }
            }, 800);

        } catch (err) {
            showError('Lỗi: ' + (err && err.message ? err.message : String(err)));
            var b2 = document.getElementById('generateBtn');
            var bl = document.getElementById('btnLabel');
            var bi = document.getElementById('btnIcon');
            if (b2) b2.disabled = false;
            if (bl) bl.textContent = 'TẠO VIDEO';
            if (bi) { bi.style.cssText = ''; bi.className = 'bi bi-lightning-fill btn-icon'; }
        }
    };

    /* ═══════════════════════════════════════════════
       INGEST DATAS DICT → TỰ TẠO CARD DONE KHI HOST PUSH
       Khi host push object {guid: path} hoặc {guid: [path1, path2]}
       mà chưa có card nào trong state → tự tạo card "done" và load ngay.
    ═══════════════════════════════════════════════ */
    function ingestDatasDict(dict) {
        if (!dict || typeof dict !== 'object' || Array.isArray(dict)) return;
        var grid  = document.getElementById('videoGrid');
        var empty = document.getElementById('emptyState');
        var changed = false;

        Object.keys(dict).forEach(function (gid) {
            // Bỏ qua các guid đã xử lý
            if (handledGuildIds[gid]) return;
            // Nếu guild này đã có card trong state thì bỏ qua ingest để tránh tạo card trùng.
            for (var ex = 0; ex < state.videos.length; ex++) {
                var exItem = state.videos[ex];
                if (exItem && String(exItem.guildId || '') === String(gid)) {
                    handledGuildIds[gid] = true;
                    return;
                }
            }

            var raw = dict[gid];
            // Bỏ qua các key không phải guid dạng media path
            if (raw == null) return;

            // Normalize thành mảng entry (string/object) để parse được cả status != 200
            var entries = [];
            if (Array.isArray(raw)) {
                raw.forEach(function (v) {
                    if (v == null) return;
                    if (typeof v === 'object' || typeof v === 'string') entries.push(v);
                    else {
                        var s = coerceMappingValueToMediaString(v);
                        if (s) entries.push(s);
                    }
                });
            } else {
                if (typeof raw === 'object' || typeof raw === 'string') entries.push(raw);
                else {
                    var s = coerceMappingValueToMediaString(raw);
                    if (s) entries.push(s);
                }
            }

            if (entries.length === 0) return;

            var parsedEntries = [];
            entries.forEach(function (entry) {
                var parsed = parseVideoResultFromEntry(entry);
                if (!parsed) return;
                if (!parsed.videoUrl && parsed.statusCode !== 200) return;
                parsedEntries.push(parsed);
            });
            if (parsedEntries.length === 0) return;

            // Đánh dấu đã handled NGAY để tránh race condition khi onUpdate gọi nhiều lần
            handledGuildIds[gid] = true;
            changed = true;

            parsedEntries.forEach(function (parsed) {
                var itemId = uid();
                var item = {
                    id: itemId,
                    prompt: (parsed.prompt && String(parsed.prompt).trim()) || gid.substring(0, 36),
                    aspect: normalizeAspectValue(parsed.aspect) || '16:9',
                    length: normalizeLengthValue(parsed.length) || 0,
                    res: normalizeResolutionValue(parsed.resolution) || '',
                    status: (parsed.statusCode !== null && parsed.statusCode !== 200) ? 'error' : 'done',
                    progress: 100,
                    videoUrl: parsed.videoUrl || '',
                    localPath: parsed.localPath || '',
                    localUrl: '',
                    thumbUrl: '',
                    errorMsg: (parsed.statusCode !== null && parsed.statusCode !== 200)
                        ? ('Tạo video thất bại (status=' + parsed.statusCode + ')')
                        : '',
                    createdAt: Date.now(),
                    guildId: gid
                };

                state.videos.push(item);
                if (empty && empty.parentNode === grid) grid.removeChild(empty);
                if (grid) grid.appendChild(createCard(item));

                // Chọn async: nếu có localPath → resolve ngay; không thì dùng hydrate
                if (item.status === 'done' && item.localPath) {
                    hydrateLocalPlayable(item);
                } else if (item.status === 'done') {
                    hydrateDoneCardMedia(item);
                }
            });
        });

        if (changed) updateStats();
    }

    /* ═══════════════════════════════════════════════════════════════
       Nút tải cache → requestLoadVideoCacheFromHost (statusCreate=2 + poll loadVideoDatas)
    ═══════════════════════════════════════════════════════════════ */

    /* ═══════════════════════════════════════════════
       __ac REALTIME CALLBACK
    ═══════════════════════════════════════════════ */
    try {
        if (window.__ac && typeof window.__ac.onUpdate === 'function') {
            // Preferred for mapped input key "datas"
            window.__ac.onUpdate('datas', function (datas) {
                var normalized = null;
                if (datas && typeof datas === 'object' && !Array.isArray(datas)) {
                    normalized = datas;
                } else if (typeof datas === 'string') {
                    var parsed = safeJsonParse(datas);
                    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
                        normalized = parsed;
                    }
                }
                if (normalized) {
                    latestDatasDict = normalized;
                    // Tự tạo card cho guid chưa có trong state (không cần generateVideo trước)
                    ingestDatasDict(normalized);
                }
                debugLog('onUpdate(datas) fired type=' + (datas == null ? 'null' : typeof datas), datas);
            });

            // Some runtimes only fire reliably with multi-key signature.
            window.__ac.onUpdate('datas', 'outputGuildId', function (datas, outputGuildId) {
                var normalized = normalizeDictMaybe(datas);
                if (normalized) {
                    latestDatasDict = normalized;
                    ingestDatasDict(normalized);
                }
                debugLog('onUpdate(datas, outputGuildId) fired gid=' + (outputGuildId == null ? '' : String(outputGuildId)), datas);
            });

            window.__ac.onUpdate('loadVideoDatas', function (loadVideoDatas) {
                var normalized = null;
                if (loadVideoDatas && typeof loadVideoDatas === 'object' && !Array.isArray(loadVideoDatas)) {
                    normalized = loadVideoDatas;
                } else if (typeof loadVideoDatas === 'string') {
                    var parsedLv = safeJsonParse(loadVideoDatas);
                    if (parsedLv && typeof parsedLv === 'object' && !Array.isArray(parsedLv)) {
                        normalized = parsedLv;
                    }
                }
                if (normalized) {
                    latestLoadVideoDatasDict = normalized;
                    ingestDatasDict(normalized);
                }
                debugLog('onUpdate(loadVideoDatas) fired type=' + (loadVideoDatas == null ? 'null' : typeof loadVideoDatas), loadVideoDatas);
            });

            window.__ac.onUpdate('loadVideoDatas', 'outputGuildId', function (loadVideoDatas, outputGuildId) {
                var normalized = normalizeDictMaybe(loadVideoDatas);
                if (normalized) {
                    latestLoadVideoDatasDict = normalized;
                    ingestDatasDict(normalized);
                }
                debugLog('onUpdate(loadVideoDatas, outputGuildId) gid=' + (outputGuildId == null ? '' : String(outputGuildId)), loadVideoDatas);
            });

            // Fallback generic callback
            window.__ac.onUpdate(function (live) {
                var dict = pickResultDictionary(live);
                if (dict && typeof dict === 'object' && Object.keys(dict).length > 0) {
                    latestDatasDict = dict;
                    ingestDatasDict(dict);
                }
                var lvd = pickLoadVideoDatasDictionary(live);
                if (lvd && typeof lvd === 'object' && Object.keys(lvd).length > 0) {
                    latestLoadVideoDatasDict = lvd;
                    ingestDatasDict(lvd);
                }
                debugLog('onUpdate(live) fired keys=' + Object.keys(dict).length);
                Object.keys(pollJobs).forEach(function (gid) {
                    var job = pollJobs[gid];
                    if (!job) return;
                    var res = resolveResultList(dict, gid);
                    if (res && Array.isArray(res)) {
                        var ni = collectNewReadyResults(job, res);
                        if (ni.length > 0) {
                            job.renderedCount += ni.length;
                            job.onResult(ni, job.renderedCount, job.expectedCount);
                        }
                        if (job.renderedCount >= job.expectedCount) {
                            clearInterval(job.interval);
                            delete pollJobs[gid];
                            updateStats();
                        }
                    }
                });
            });
        }
    } catch (_) {}

    /* ═══════════════════════════════════════════════
       EAGER INITIAL LOAD (fix: video/poll không load lần đầu)
       onUpdate chỉ fire khi data thay đổi, không fire cho data
       đã có sẵn lúc init → cần chủ động check ngay khi load.
    ═══════════════════════════════════════════════ */
    (function doEagerInitialVideoLoad() {
        function attempt() {
            try {
                var dict = pickResultDictionary();
                if (dict && typeof dict === 'object' && Object.keys(dict).length > 0) {
                    latestDatasDict = dict;
                    ingestDatasDict(dict);
                    // Cũng thông báo cho các pollJobs đang chờ (nếu có)
                    Object.keys(pollJobs).forEach(function (gid) {
                        var job = pollJobs[gid];
                        if (!job) return;
                        var res = resolveResultList(dict, gid);
                        if (res && Array.isArray(res)) {
                            var ni = collectNewReadyResults(job, res);
                            if (ni.length > 0) {
                                job.renderedCount += ni.length;
                                job.onResult(ni, job.renderedCount, job.expectedCount);
                            }
                            if (job.renderedCount >= job.expectedCount) {
                                clearInterval(job.interval);
                                delete pollJobs[gid];
                                updateStats();
                            }
                        }
                    });
                }
                var ldict = pickLoadVideoDatasDictionary();
                if (ldict && typeof ldict === 'object' && Object.keys(ldict).length > 0) {
                    latestLoadVideoDatasDict = ldict;
                    ingestDatasDict(ldict);
                }
            } catch (_) {}
        }
        attempt();                    // ngay lập tức
        setTimeout(attempt, 300);     // sau 300ms (đợi __ac.live populate)
        setTimeout(attempt, 1000);    // sau 1s (safety net)
        setTimeout(attempt, 2500);    // sau 2.5s (cho các trường hợp host chậm)
    })();

    /* ═══════════════════════════════════════════════
       MODAL
    ═══════════════════════════════════════════════ */
    window.openModal = function (id) {
        var item = null;
        for (var i = 0; i < state.videos.length; i++) {
            if (state.videos[i].id === id) { item = state.videos[i]; break; }
        }
        if (!item || item.status !== 'done') return;

        normalizeDoneVideoSources(item);

        var inlinePrev = document.getElementById('vid-' + id);
        if (inlinePrev && inlinePrev.pause) {
            try { inlinePrev.pause(); } catch (_) {}
        }

        var mv = document.getElementById('modalVideo');
        var mp = document.getElementById('modalPrompt');
        var dl = document.getElementById('modalDownload');
        var vm = document.getElementById('videoModal');
        var mi = document.querySelector('.video-modal-inner');

        var dlHref = item.localUrl || (item.localPath ? toFileUri(item.localPath) : item.videoUrl);
        var fallbackSrc = toPlayableSrc(item.localUrl || item.videoUrl || dlHref);

        if (mv) mv.pause();

        function applyModalVideoSrc(resolvedSrc) {
            if (!mv) return;
            var src0 = resolvedSrc || fallbackSrc;
            // cache-bust cho URL nội bộ để tránh dính lỗi DNS đã cache
            if (isInternalPlayableRef(String(src0 || '').trim())) src0 = withCacheBust(String(src0).trim());
            var src = toPlayableSrc(src0);
            applyVideoElementSource(mv, src, 'modal');
            var pp = mv.play && mv.play();
            if (pp && typeof pp.catch === 'function') pp.catch(function () {});
        }

        if (item.localPath) {
            resolveLocalPlayableUrl(item.localPath, fallbackSrc, applyModalVideoSrc);
        } else if (isInternalPlayableRef(fallbackSrc)) {
            resolvePlayableRefUrl(fallbackSrc, function (u) {
                applyModalVideoSrc(u || fallbackSrc);
            });
        } else if ((item.videoUrl || '').trim() && !/[\\/:]/.test((item.videoUrl || '').trim()) && /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test((item.videoUrl || '').trim())) {
            resolvePlayableRefUrl('https://localfiles.local/' + encodeURIComponent((item.videoUrl || '').trim()), function (u) {
                applyModalVideoSrc(u || fallbackSrc);
            });
        } else {
            applyModalVideoSrc(fallbackSrc);
        }
        if (mp) mp.textContent = item.prompt;
        if (dl) { dl.href = dlHref || fallbackSrc; dl.download = 'grok_' + item.id + '.mp4'; }
        if (vm) vm.style.display = 'flex';
        if (mi) { mi.classList.remove('animate__zoomOut'); mi.classList.add('animate__zoomIn'); }
    };

    window.closeModal = function (e) {
        var vm = document.getElementById('videoModal');
        var mi = document.querySelector('.video-modal-inner');
        if (e && e.target !== vm && !(e.target && e.target.closest && e.target.closest('.modal-close-btn'))) return;
        if (mi) { mi.classList.remove('animate__zoomIn'); mi.classList.add('animate__zoomOut'); }
        setTimeout(function () {
            if (vm) vm.style.display = 'none';
            var v = document.getElementById('modalVideo');
            if (v) { v.pause(); v.src = ''; }
        }, 240);
    };

    window.openPreviewModal = function (id) {
        var item = null;
        for (var i = 0; i < state.videos.length; i++) {
            if (state.videos[i].id === id) { item = state.videos[i]; break; }
        }
        if (!item || item.status !== 'done') return;
        normalizeDoneVideoSources(item);

        var vm = document.getElementById('videoPreviewModal');
        var pf = document.getElementById('previewFrame');
        if (!vm || !pf) return;

        var fallbackSrc = toPlayableSrc(item.localUrl || item.videoUrl || (item.localPath ? toFileUri(item.localPath) : ''));
        function applyFrame(src) {
            var s = src || fallbackSrc;
            if (isInternalPlayableRef(String(s || '').trim())) s = withCacheBust(String(s).trim());
            s = toPlayableSrc(s);
            pf.srcdoc = '';
            pf.src = s || 'about:blank';
        }

        if (item.localPath) {
            resolveLocalPlayableUrl(item.localPath, fallbackSrc, function (u) { applyFrame(u || fallbackSrc); });
        } else {
            applyFrame(fallbackSrc);
        }
        vm.style.display = 'flex';
    };

    window.closePreviewModal = function (e) {
        var vm = document.getElementById('videoPreviewModal');
        if (!vm) return;
        if (e && e.target !== vm && !(e.target && e.target.closest && e.target.closest('.modal-close-btn'))) return;
        vm.style.display = 'none';
        var pf = document.getElementById('previewFrame');
        if (pf) { pf.src = 'about:blank'; pf.srcdoc = ''; }
    };

    /* ═══════════════════════════════════════════════
       THEME TOGGLE
    ═══════════════════════════════════════════════ */
    var isDark   = true;
    var themeBtn = document.getElementById('themeBtn');
    (function applyCachedTheme() {
        var prefs = loadUiPrefs();
        if (prefs && prefs.theme === 'light') isDark = false;
        var app = document.getElementById('app');
        var icon = document.getElementById('themeIcon');
        if (app)  { app.classList.toggle('dark', isDark); app.classList.toggle('light', !isDark); }
        if (icon) icon.className = isDark ? 'bi bi-moon-stars-fill' : 'bi bi-sun-fill';
    })();
    if (themeBtn) {
        themeBtn.addEventListener('click', function () {
            isDark = !isDark;
            var app  = document.getElementById('app');
            var icon = document.getElementById('themeIcon');
            if (app)  { app.classList.toggle('dark', isDark); app.classList.toggle('light', !isDark); }
            if (icon) icon.className = isDark ? 'bi bi-moon-stars-fill' : 'bi bi-sun-fill';
            var p = loadUiPrefs();
            p.theme = isDark ? 'dark' : 'light';
            saveUiPrefs(p);
            showToast('info', null, isDark ? 'Chuyển sang Dark Mode' : 'Chuyển sang Light Mode', 2000);
        });
    }

    /* ═══════════════════════════════════════════════
       SIDEBAR TOGGLE (desktop) + FLOAT BUTTON
    ═══════════════════════════════════════════════ */
    var sidebarCollapsed = false;

    function setSidebarCollapsed(collapsed) {
        sidebarCollapsed = collapsed;
        var sidebar      = document.getElementById('sidebar');
        var icon         = document.getElementById('sidebarToggleIcon');
        var floatBtn     = document.getElementById('sidebarFloatBtn');

        if (sidebar) {
            sidebar.classList.toggle('collapsed', sidebarCollapsed);
            /* initSidebarResizer / kéo cạnh đặt width & minWidth inline — thắng cả .sidebar.collapsed trong CSS,
               nên khi thu gọn desktop phải ghi đè inline (0), khi mở lại khôi phục theo prefs. */
            if (window.innerWidth > 640) {
                if (sidebarCollapsed) {
                    sidebar.style.width = '0';
                    sidebar.style.minWidth = '0';
                    sidebar.style.maxWidth = '0';
                } else {
                    var sp = loadUiPrefs();
                    var cw = Number(sp.sidebarWidth || 0);
                    if (cw >= 220 && cw <= 520) {
                        sidebar.style.width = cw + 'px';
                        sidebar.style.minWidth = cw + 'px';
                    } else {
                        sidebar.style.removeProperty('width');
                        sidebar.style.removeProperty('minWidth');
                    }
                    sidebar.style.removeProperty('maxWidth');
                }
            }
            try {
                if (window.innerWidth > 640) {
                    sidebar.setAttribute('aria-hidden', sidebarCollapsed ? 'true' : 'false');
                } else {
                    sidebar.removeAttribute('aria-hidden');
                }
            } catch (_) {}
        }
        if (icon)     icon.className = sidebarCollapsed ? 'bi bi-layout-sidebar' : 'bi bi-layout-sidebar-reverse';

        // Float button: only on desktop (> 640px)
        if (floatBtn) {
            var isMobile = window.innerWidth <= 640;
            if (!isMobile) {
                floatBtn.style.display = sidebarCollapsed ? 'flex' : 'none';
            } else {
                floatBtn.style.display = 'none';
            }
        }
        var p = loadUiPrefs();
        p.sidebarCollapsed = !!sidebarCollapsed;
        saveUiPrefs(p);
    }

    var sidebarToggleBtn = document.getElementById('sidebarToggleBtn');
    if (sidebarToggleBtn) {
        sidebarToggleBtn.addEventListener('click', function () {
            setSidebarCollapsed(!sidebarCollapsed);
        });
    }

    var sidebarFloatBtn = document.getElementById('sidebarFloatBtn');
    if (sidebarFloatBtn) {
        sidebarFloatBtn.addEventListener('click', function () {
            setSidebarCollapsed(false);
        });
    }

    (function initSidebarResizer() {
        var sidebar = document.getElementById('sidebar');
        if (!sidebar) return;
        var prefs = loadUiPrefs();
        var cachedW = Number(prefs.sidebarWidth || 0);
        if (cachedW >= 220 && cachedW <= 520 && window.innerWidth > 640) {
            sidebar.style.width = cachedW + 'px';
            sidebar.style.minWidth = cachedW + 'px';
        }
        if (prefs.sidebarCollapsed != null && window.innerWidth > 640) {
            setSidebarCollapsed(!!prefs.sidebarCollapsed);
        }
        var EDGE_HIT_PX = 8;
        var dragging = false;
        var resizingCandidate = false;
        var prevSelect = '';
        var prevDocCursor = '';
        var prevSidebarTransition = '';

        function isNearRightEdge(ev) {
            if (!ev || typeof ev.clientX !== 'number') return false;
            var rect = sidebar.getBoundingClientRect();
            if (!rect || rect.width <= 0) return false;
            return (ev.clientX >= rect.right - EDGE_HIT_PX) && (ev.clientX <= rect.right + 2);
        }

        function updateResizeCursor(ev) {
            if (window.innerWidth <= 640 || sidebarCollapsed || dragging) {
                resizingCandidate = false;
                sidebar.style.cursor = '';
                return;
            }
            resizingCandidate = isNearRightEdge(ev);
            sidebar.style.cursor = resizingCandidate ? 'col-resize' : '';
        }

        function onMove(ev) {
            if (!dragging || window.innerWidth <= 640) return;
            var x = (ev && ev.clientX) ? ev.clientX : 0;
            var w = Math.max(220, Math.min(520, x));
            sidebar.style.width = w + 'px';
            sidebar.style.minWidth = w + 'px';
            if (ev && ev.preventDefault) ev.preventDefault();
        }
        function onUp() {
            if (!dragging) return;
            dragging = false;
            sidebar.style.cursor = '';
            sidebar.style.transition = prevSidebarTransition;
            document.body.style.userSelect = prevSelect;
            document.body.style.cursor = prevDocCursor;
            try {
                var w = Math.round(sidebar.getBoundingClientRect().width || 0);
                var p = loadUiPrefs();
                p.sidebarWidth = w;
                saveUiPrefs(p);
            } catch (_) {}
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
        }
        sidebar.addEventListener('mousemove', updateResizeCursor);
        sidebar.addEventListener('mouseleave', function () {
            if (!dragging) sidebar.style.cursor = '';
            resizingCandidate = false;
        });

        sidebar.addEventListener('mousedown', function (ev) {
            if (window.innerWidth <= 640 || sidebarCollapsed) return;
            updateResizeCursor(ev);
            if (!resizingCandidate) return;
            dragging = true;
            prevSidebarTransition = sidebar.style.transition || '';
            prevSelect = document.body.style.userSelect || '';
            prevDocCursor = document.body.style.cursor || '';
            sidebar.style.transition = 'none';
            document.body.style.userSelect = 'none';
            document.body.style.cursor = 'col-resize';
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
            if (ev && ev.preventDefault) ev.preventDefault();
        });
    })();

    /* ═══════════════════════════════════════════════
       MULTI PROMPT CHECKBOX → BATCH ROW TOGGLE + INFO
    ═══════════════════════════════════════════════ */
    function updateBatchInfo() {
        var raw        = (document.getElementById('promptInput').value || '');
        var isMulti    = document.getElementById('multiPrompt').checked;
        var batchSzEl  = document.getElementById('batchSize');
        var infoEl     = document.getElementById('batchInfoText');
        if (!batchSzEl || !infoEl) return;

        var batchSz = parseInt(batchSzEl.value, 10) || 1;
        var sendCnt = parseInt((document.getElementById('sendCount') || {}).value, 10) || 1;

        if (!isMulti) { infoEl.textContent = '—'; return; }

        var lines   = raw.split('\n').map(function (s) { return s.trim(); }).filter(Boolean);
        var total   = lines.length;
        var batches = total > 0 ? Math.ceil(total / batchSz) : 0;
        var videos  = total * sendCnt;

        infoEl.textContent = batches + ' đợt · ' + videos + ' video';
    }

    var multiPromptCb = document.getElementById('multiPrompt');
    var batchRowEl    = document.getElementById('batchRow');
    if (multiPromptCb && batchRowEl) {
        multiPromptCb.addEventListener('change', function () {
            if (multiPromptCb.checked) {
                batchRowEl.classList.add('visible');
            } else {
                batchRowEl.classList.remove('visible');
            }
            updateBatchInfo();
        });
    }

    // Also update info when prompt or selects change
    var promptInput = document.getElementById('promptInput');
    if (promptInput) promptInput.addEventListener('input', function () {
        updateBatchInfo();
        if (typeof window.grokSyncPromptMentionPreview === 'function') window.grokSyncPromptMentionPreview();
    });
    var batchSzSel  = document.getElementById('batchSize');
    if (batchSzSel) batchSzSel.addEventListener('change', updateBatchInfo);
    var sendCntSel  = document.getElementById('sendCount');
    if (sendCntSel) sendCntSel.addEventListener('change', updateBatchInfo);

    /* ═══════════════════════════════════════════════
       MOBILE SIDEBAR (bottom drawer)
    ═══════════════════════════════════════════════ */
    var mobileSidebarBtn = document.getElementById('mobileSidebarBtn');
    var mobileSidebarBtnImage = document.getElementById('mobileSidebarBtnImage');
    if (mobileSidebarBtn || mobileSidebarBtnImage) {
        var overlay = document.createElement('div');
        overlay.className = 'sidebar-overlay';
        overlay.id = 'sidebarOverlay';
        document.body.appendChild(overlay);

        function openMobileSidebar() {
            var s = document.getElementById('sidebar');
            var o = document.getElementById('sidebarOverlay');
            if (s) s.classList.add('mobile-open');
            if (o) o.classList.add('active');
        }
        function closeMobileSidebar() {
            var s = document.getElementById('sidebar');
            var o = document.getElementById('sidebarOverlay');
            if (s) s.classList.remove('mobile-open');
            if (o) o.classList.remove('active');
        }

        if (mobileSidebarBtn) mobileSidebarBtn.addEventListener('click', openMobileSidebar);
        if (mobileSidebarBtnImage) mobileSidebarBtnImage.addEventListener('click', openMobileSidebar);
        overlay.addEventListener('click', closeMobileSidebar);
    }

    bindPrefsPersistence();

    /* ═══════════════════════════════════════════════
       VIEW TOGGLE (grid / list)
    ═══════════════════════════════════════════════ */
    var viewToggleBtn = document.getElementById('viewToggleBtn');
    if (viewToggleBtn) {
        function applyViewMode(mode, persist) {
            viewMode = mode === 'list' ? 'list' : 'grid';
            var grid = document.getElementById('videoGrid');
            var icon = document.getElementById('viewToggleIcon');
            if (grid) grid.classList.toggle('view-list', viewMode === 'list');
            if (icon) icon.className = viewMode === 'list' ? 'bi bi-grid-3x3-gap-fill' : 'bi bi-list-ul';
            viewToggleBtn.classList.toggle('active', viewMode === 'list');
            if (persist !== false) {
                var p = loadUiPrefs();
                p.viewMode = viewMode;
                saveUiPrefs(p);
            }
        }
        viewToggleBtn.addEventListener('click', function () {
            applyViewMode(viewMode === 'grid' ? 'list' : 'grid', true);
        });
        applyViewMode(viewMode, false);
    }

    (function setupCardDisplayControls() {
        var grid = document.getElementById('videoGrid');
        var app = document.getElementById('app');
        var fitCb = document.getElementById('fitContainCheck');
        var fitWidthCb = document.getElementById('fitWidthCheck');
        if (!fitWidthCb && fitCb && fitCb.parentNode && fitCb.parentNode.parentNode) {
            var fitWidthLabel = document.createElement('label');
            fitWidthLabel.className = 'fit-toggle';
            fitWidthLabel.setAttribute('for', 'fitWidthCheck');
            fitWidthLabel.setAttribute('title', 'Card rộng theo khung video');
            fitWidthLabel.innerHTML = '<input type="checkbox" id="fitWidthCheck"><span>Fit Width</span>';
            fitCb.parentNode.parentNode.insertBefore(fitWidthLabel, fitCb.parentNode.nextSibling);
            fitWidthCb = document.getElementById('fitWidthCheck');
        }
        var sizeBtns = document.querySelectorAll('.card-size-btn');
        if (!grid || !app) return;

        function applyCardSizeMode(mode) {
            cardSizeMode = /^(sm|md|lg|xl|xx1|xx2)$/.test(String(mode)) ? String(mode) : 'md';
            grid.classList.remove('size-sm', 'size-md', 'size-lg', 'size-xl', 'size-xx1', 'size-xx2');
            grid.classList.add('size-' + cardSizeMode);
            for (var i = 0; i < sizeBtns.length; i++) {
                var b = sizeBtns[i];
                if (!b) continue;
                b.classList.toggle('active', b.getAttribute('data-size') === cardSizeMode);
            }
            var p = loadUiPrefs();
            p.cardSizeMode = cardSizeMode;
            saveUiPrefs(p);
        }

        function applyFitContain(enabled) {
            fitContainMedia = !!enabled;
            app.classList.toggle('fit-contain', fitContainMedia);
            if (fitCb) fitCb.checked = fitContainMedia;
            var p = loadUiPrefs();
            p.fitContainMedia = fitContainMedia;
            saveUiPrefs(p);
        }

        function applyFitWidth(enabled) {
            var on = !!enabled;
            app.classList.toggle('fit-video-width', on);
            if (fitWidthCb) fitWidthCb.checked = on;
            if (on) applyFitContain(true);
            var p = loadUiPrefs();
            p.fitWidthMedia = on;
            saveUiPrefs(p);
        }

        for (var i = 0; i < sizeBtns.length; i++) {
            (function (btn) {
                if (!btn) return;
                btn.addEventListener('click', function () {
                    applyCardSizeMode(btn.getAttribute('data-size') || 'md');
                });
            })(sizeBtns[i]);
        }

        if (fitCb) {
            fitCb.checked = fitContainMedia;
            fitCb.addEventListener('change', function () {
                applyFitContain(!!fitCb.checked);
                if (!fitCb.checked && fitWidthCb && fitWidthCb.checked) {
                    fitWidthCb.checked = false;
                    applyFitWidth(false);
                }
            });
        }
        if (fitWidthCb) {
            var prefs = loadUiPrefs();
            fitWidthCb.checked = !!prefs.fitWidthMedia;
            fitWidthCb.addEventListener('change', function () {
                applyFitWidth(!!fitWidthCb.checked);
            });
        }

        applyCardSizeMode(cardSizeMode);
        applyFitContain(fitContainMedia);
        if (fitWidthCb && fitWidthCb.checked) applyFitWidth(true);
    })();

    // Removed preview eye button handler (button hidden/disabled by design).

    document.addEventListener('click', function (ev) {
        var btn = ev && ev.target && ev.target.closest ? ev.target.closest('.prompt-group-header[data-group-key]') : null;
        if (!btn) return;
        var key = btn.getAttribute('data-group-key');
        if (!key) return;
        groupCollapsedState[key] = !groupCollapsedState[key];
        applyPromptGrouping();
    });

    (function setupGroupPromptButton() {
        var btn = document.getElementById('groupPromptBtn');
        var icon = document.getElementById('groupPromptIcon');
        if (!btn) return;
        function syncBtn() {
            btn.classList.toggle('active', groupPromptEnabled);
            if (icon) icon.className = groupPromptEnabled ? 'bi bi-collection-fill' : 'bi bi-collection';
        }
        btn.addEventListener('click', function () {
            groupPromptEnabled = !groupPromptEnabled;
            var p = loadUiPrefs();
            p.groupPromptEnabled = groupPromptEnabled;
            saveUiPrefs(p);
            syncBtn();
            applyPromptGrouping();
        });
        syncBtn();
        applyPromptGrouping();
    })();

    /* ═══════════════════════════════════════════════
       CLEAR ALL
    ═══════════════════════════════════════════════ */
    var loadCacheVideosBtn = document.getElementById('loadCacheVideosBtn');
    if (loadCacheVideosBtn) {
        loadCacheVideosBtn.addEventListener('click', function () {
            window.requestLoadVideoCacheFromHost();
        });
    }

    var clearAllBtn = document.getElementById('clearAllBtn');
    if (clearAllBtn) {
        clearAllBtn.addEventListener('click', function () {
            window.requestClearAllCacheFromHost();
        });
    }

    var loadCacheImagesBtn = document.getElementById('loadCacheImagesBtn');
    if (loadCacheImagesBtn) {
        loadCacheImagesBtn.addEventListener('click', function () {
            if (typeof window.grokRefreshImageData === 'function') window.grokRefreshImageData();
        });
    }

    var clearImageUploadedBtn = document.getElementById('clearImageUploadedBtn');
    if (clearImageUploadedBtn) {
        clearImageUploadedBtn.addEventListener('click', function () {
            if (typeof window.grokClearUploadedImages === 'function') window.grokClearUploadedImages();
        });
    }

    /* ═══════════════════════════════════════════════
       KEYBOARD SHORTCUTS
    ═══════════════════════════════════════════════ */
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            window.closeModal({ target: document.getElementById('videoModal') });
            var s = document.getElementById('sidebar');
            var o = document.getElementById('sidebarOverlay');
            if (s) s.classList.remove('mobile-open');
            if (o) o.classList.remove('active');
        }
        if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) window.generateVideo();
    });

    /* ═══════════════════════════════════════════════
       SPIN KEYFRAME
    ═══════════════════════════════════════════════ */
    var styleEl = document.createElement('style');
    styleEl.textContent =
        '@keyframes spin-btn { to { transform: rotate(360deg); } } ' +
        '.prompt-ellipsis{display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:100%;}' +
        '.mention-dropdown{z-index:2147483647;background:var(--card-bg,#1f2231);border:1px solid var(--line,#2b2f45);border-radius:10px;overflow:auto;box-shadow:0 10px 30px rgba(0,0,0,.35);}' +
        '.mention-item{display:flex;align-items:center;gap:8px;padding:8px 10px;cursor:pointer;}' +
        '.mention-item:hover,.mention-item.highlighted{background:rgba(255,255,255,.08);}' +
        '.mention-item-thumb,.mention-item-thumb-ph{width:30px;height:30px;border-radius:6px;flex-shrink:0;}' +
        '.mention-item-thumb{object-fit:cover;background:#111;}' +
        '.mention-item-thumb-ph{display:flex;align-items:center;justify-content:center;background:rgba(255,255,255,.05);}' +
        '.mention-item-name{font-size:12px;color:var(--clr1,#e8eaf6);}';
    document.head.appendChild(styleEl);

    /* ═══════════════════════════════════════════════
       INIT
    ═══════════════════════════════════════════════ */
    if (multiPromptCb && batchRowEl) {
        if (multiPromptCb.checked) batchRowEl.classList.add('visible');
        else batchRowEl.classList.remove('visible');
    }
    updateStats();
    updateBatchInfo();

    setInterval(function () {
        state.videos.forEach(function (v) {
            var c = document.getElementById('card-' + v.id);
            if (c) {
                var timeEl = c.querySelector('.card-time');
                if (timeEl) timeEl.textContent = timeAgo(v.createdAt);
            }
        });
    }, 30000);


    /* ═══════════════════════════════════════════════
       SIDEBAR TABS — VIDEO / IMAGE SWITCHING
    ═══════════════════════════════════════════════ */
    (function initMainTabs() {
        function switchTab(tab) {
            var isImage = (tab === 'image');

            // Sidebar panels
            var panelV = document.getElementById('tabPanelVideo');
            var panelI = document.getElementById('tabPanelImage');
            if (panelV) panelV.style.display = isImage ? 'none' : '';
            if (panelI) panelI.style.display = isImage ? '' : 'none';

            // Main panels
            var vGrid  = document.getElementById('videoGrid');
            var iPanel = document.getElementById('imageGridPanel');
            if (vGrid)  vGrid.style.display  = isImage ? 'none' : '';
            if (iPanel) iPanel.style.display = isImage ? 'flex'  : 'none';

            // Page title
            var title = document.querySelector('.page-title');
            if (title) title.innerHTML = isImage ? 'IMAGE <em>MANAGER</em>' : 'VIDEO <em>GALLERY</em>';

            // Video-only header controls
            var vCtrl = document.getElementById('videoHeaderControls');
            if (vCtrl) vCtrl.style.display = isImage ? 'none' : '';
            var iCtrl = document.getElementById('imageHeaderControls');
            if (iCtrl) iCtrl.style.display = isImage ? '' : 'none';

            // Tab buttons
            var tV = document.getElementById('tabVideo');
            var tI = document.getElementById('tabImage');
            if (tV) tV.classList.toggle('active', !isImage);
            if (tI) tI.classList.toggle('active',  isImage);

            try { var p = loadUiPrefs(); p.mainTab = tab; saveUiPrefs(p); } catch (_) {}
        }

        var btnV = document.getElementById('tabVideo');
        var btnI = document.getElementById('tabImage');
        if (btnV) btnV.addEventListener('click', function () { switchTab('video'); });
        if (btnI) btnI.addEventListener('click', function () { switchTab('image'); });

        // Restore from prefs
        try { var pr = loadUiPrefs(); if (pr.mainTab === 'image') switchTab('image'); } catch (_) {}
    })();

    /* ═══════════════════════════════════════════════
       IMAGE MODULE
       — Upload zone (file input + drag-drop)
       — Pending preview grid with rename, remove, clear-all
       — Lightbox (pending + uploaded)
       — Upload button → sends batch with uploadGuid + imageGuid per image
       — Col 2: status cards (processing / done / failed) from dataImages poll
       — @mention autocomplete in promptInput (done images only)
    ═══════════════════════════════════════════════

    ═══ dataImages structure (from backend): ═══════
    Dict keyed by imageGuid OR array of objects:
    {
      "<imageGuid>": {
        "imageGuid":      "uuid",           // unique per image
        "uploadGuid":     "uuid",           // batch ID (same across batch)
        "fileName":       "photo.jpg",
        "status":         "processing" | "done" | "failed",
        "isSuccess":      true | false,
        "linkImage":      "https://...",    // filled when done
        "fileMetadataId": "uuid",           // filled when done
        "error":          ""                // error message when failed
      },
      ...
    }

    ═══ outputImageUpload payload (sent to backend): ═══
    {
      "uploadGuid": "uuid",   // identifies the whole upload batch
      "images": [
        { "imageGuid": "uuid", "base64": "data:image/...", "filename": "photo.jpg" },
        ...
      ]
    }
    ═══════════════════════════════════════════════ */
    (function initImageModule() {
        /* ── State ── */
        var pendingImages  = [];   // { id, base64, filename, originalName }
        var imageItemsDict = {};   // imageGuid → row + imageLocalPath / resolvedImageUrl for WebView2
        var hydratingImageGuids = {};
        var uploadBatchTracker = {}; // uploadGuid -> { expectedCount, terminalCount, doneByGuid, completed, startedAt, timeoutAt }
        var imageGuidTracker = {};   // imageGuid  -> { uploadGuid, terminal }
        var imageBatchPollTimer = null;
        var IMAGE_BATCH_POLL_INTERVAL_MS = 1000;
        var IMAGE_BATCH_TIMEOUT_MS = 180000;
        var imageCardSizeMode = 'md';
        var fitContainImage = false;
        var fitWidthImage = false;
        var IMG_UPLOAD_PLACEHOLDER = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

        function normPathForImage(p) {
            return String(p || '').replace(/\\/g, '/').trim().toLowerCase();
        }

        function pickImageLocalPath(linkImage) {
            var s = String(linkImage || '').trim();
            if (!s) return '';
            if (isAbsoluteWinPath(s)) return s.replace(/\//g, '\\');
            if (/^file:/i.test(s)) {
                var p = fileUriToLocalPath(s);
                return p || '';
            }
            return '';
        }

        function pickUploadedImageDisplayUrl(img) {
            var r = (img.resolvedImageUrl || '').trim();
            if (r) return r;
            var lk = String(img.linkImage || '').trim();
            if (!lk) return '';
            if (isAbsoluteWinPath(lk) || /^file:/i.test(lk)) return '';
            return lk;
        }

        function hydrateUploadedImageSrc(img) {
            if (!img || img.status !== 'done') return;
            if ((img.resolvedImageUrl || '').trim()) return;
            var lp = (img.imageLocalPath || pickImageLocalPath(img.linkImage) || '').trim();
            if (!lp) return;
            img.imageLocalPath = lp;
            var guid = String(img.imageGuid || '');
            if (!guid) return;
            if (hydratingImageGuids[guid]) return;
            hydratingImageGuids[guid] = true;
            resolveLocalPlayableUrl(lp, '', function (resolved) {
                delete hydratingImageGuids[guid];
                if (!resolved || /^file:/i.test(resolved)) return;
                img.resolvedImageUrl = resolved;
                var g = document.getElementById('imgUploadedGrid');
                if (!g) return;
                var card = g.querySelector('.img-uploaded-card[data-image-guid="' + guid + '"]');
                if (!card) return;
                var im = card.querySelector('.img-uploaded-media img');
                if (im) im.src = resolved;
            });
        }

        function hydrateAllUploadedImagesNeedingResolve() {
            Object.keys(imageItemsDict).forEach(function (k) {
                hydrateUploadedImageSrc(imageItemsDict[k]);
            });
        }

        function computeImageDictEntry(item, existing) {
            existing = existing || {};
            var iguid = String(item.imageGuid || item.id || '');
            if (!iguid) return null;
            var normalizedStatus = normalizeStatusToState(item.status, item.isSuccess);
            var mergedLink = item.linkImage || existing.linkImage || '';
            var mTrim = String(mergedLink).trim();
            var fromPick = pickImageLocalPath(mergedLink);

            var effectiveLp = fromPick;
            if (!effectiveLp && existing.imageLocalPath) {
                var exLink = String(existing.linkImage || '').trim();
                if (!mTrim || mTrim === exLink) {
                    effectiveLp = existing.imageLocalPath;
                } else if (!/^https?:\/\//i.test(mTrim)) {
                    effectiveLp = existing.imageLocalPath;
                }
            }

            var resolvedImageUrl = existing.resolvedImageUrl || '';
            var nOld = normPathForImage(existing.imageLocalPath);
            var nNew = normPathForImage(effectiveLp);
            if (nOld && nNew && nOld !== nNew) resolvedImageUrl = '';

            return {
                imageGuid:        iguid,
                uploadGuid:       item.uploadGuid      || existing.uploadGuid      || '',
                fileName:         item.fileName        || existing.fileName        || '',
                status:           normalizedStatus || existing.status || 'processing',
                isSuccess:        normalizedStatus === 'done',
                linkImage:        mergedLink,
                fileMetadataId:   item.fileMetadataId  || existing.fileMetadataId  || '',
                error:            item.error           || existing.error           || '',
                addedAt:          existing.addedAt     || Date.now(),
                imageLocalPath:   effectiveLp,
                resolvedImageUrl: resolvedImageUrl
            };
        }

        /* ── Badge helpers ── */
        function updatePendingCount() {
            var el = document.getElementById('pendingCount');
            if (el) el.textContent = pendingImages.length + ' \u1ea3nh';
            var clr = document.getElementById('imgClearBtn');
            var vidN = state.videos && state.videos.length ? state.videos.length : 0;
            if (clr) clr.style.display = (pendingImages.length > 0 || vidN > 0) ? '' : 'none';
        }
        function updateUploadedCount() {
            var el = document.getElementById('imgUploadedCount');
            if (el) el.textContent = Object.keys(imageItemsDict).length + ' \u1ea3nh';
        }

        /* ════════════════════════════════════════
           PENDING PREVIEW GRID
        ════════════════════════════════════════ */
        function renderPendingGrid() {
            var grid = document.getElementById('imgPreviewGrid');
            if (!grid) return;
            grid.innerHTML = '';
            updatePendingCount();
            pendingImages.forEach(function (img) {
                var card = document.createElement('div');
                card.className = 'img-preview-card animate__animated animate__fadeIn';
                card.id = 'imgcard-' + img.id;
                card.innerHTML =
                    '<div class="img-card-media" onclick="imgOpenLightbox(\'' + img.id + '\')">' +
                        '<img src="' + esc(img.base64) + '" alt="' + esc(img.filename) + '" loading="lazy">' +
                        '<div class="img-card-zoom-overlay"><i class="bi bi-zoom-in"></i></div>' +
                    '</div>' +
                    '<button class="img-card-remove" onclick="imgRemovePending(\'' + img.id + '\')" title="X\u00f3a \u1ea3nh">' +
                        '<i class="bi bi-x-lg"></i>' +
                    '</button>' +
                    '<div class="img-card-name-wrap">' +
                        '<input class="img-card-name" type="text" value="' + esc(img.filename) + '" ' +
                               'id="imgname-' + img.id + '" placeholder="T\u00ean \u1ea3nh..." ' +
                               'onchange="imgUpdateName(\'' + img.id + '\', this.value)">' +
                    '</div>';
                grid.appendChild(card);
            });
        }

        /* ════════════════════════════════════════
           LIGHTBOX
        ════════════════════════════════════════ */
        function openLightboxSrc(src) {
            var lb    = document.getElementById('imageLightbox');
            var lbImg = document.getElementById('lightboxImg');
            if (!lb || !lbImg || !src) return;
            lbImg.src = src;
            lb.style.display = 'flex';
            var inner = lb.querySelector('.lightbox-inner');
            if (inner) {
                inner.classList.remove('animate__zoomOut');
                void inner.offsetWidth;
                inner.classList.add('animate__zoomIn');
            }
        }

        window.imgOpenLightbox = function (imgId) {
            for (var i = 0; i < pendingImages.length; i++) {
                if (pendingImages[i].id === imgId) { openLightboxSrc(pendingImages[i].base64); return; }
            }
        };
        window.imgOpenUploadedLightbox = function (src) { openLightboxSrc(src); };
        window.imgOpenUploadedImageByGuid = function (guid) {
            var row = imageItemsDict[guid];
            if (!row) return;
            var u = pickUploadedImageDisplayUrl(row);
            if (u) {
                openLightboxSrc(u);
                return;
            }
            var localPath = String(row.imageLocalPath || pickImageLocalPath(row.linkImage) || '').trim();
            if (!localPath) return;
            resolveLocalPlayableUrl(localPath, '', function (resolved) {
                if (!resolved) return;
                row.resolvedImageUrl = resolved;
                openLightboxSrc(resolved);
            });
        };

        window.closeLightbox = function (e) {
            var lb = document.getElementById('imageLightbox');
            if (!lb) return;
            if (e && e.target !== lb && !(e.target && e.target.closest && e.target.closest('.modal-close-btn'))) return;
            var inner = lb.querySelector('.lightbox-inner');
            if (inner) { inner.classList.remove('animate__zoomIn'); inner.classList.add('animate__zoomOut'); }
            setTimeout(function () {
                lb.style.display = 'none';
                var img = document.getElementById('lightboxImg');
                if (img) img.src = '';
            }, 280);
        };

        /* ════════════════════════════════════════
           REMOVE / RENAME / CLEAR PENDING
        ════════════════════════════════════════ */
        window.imgRemovePending = function (imgId) {
            pendingImages = pendingImages.filter(function (i) { return i.id !== imgId; });
            renderPendingGrid();
        };
        window.imgUpdateName = function (imgId, newName) {
            for (var i = 0; i < pendingImages.length; i++) {
                if (pendingImages[i].id === imgId) {
                    var n = (newName || '').trim();
                    pendingImages[i].filename = n || pendingImages[i].originalName;
                    break;
                }
            }
        };

        var clearBtn = document.getElementById('imgClearBtn');
        if (clearBtn) {
            clearBtn.addEventListener('click', function () {
                var hadPending = pendingImages.length > 0;
                var hadVideos = state.videos && state.videos.length > 0;
                if (!hadPending && !hadVideos) return;
                loadVideoDatasPollPaused = true;
                try {
                    stopAllLoadVideoDatasPolling();
                    resetLoadCacheVideosBtnUi();
                    clearVideoGalleryStateAndDom();
                    latestLoadVideoDatasDict = null;
                    pendingImages = [];
                    renderPendingGrid();
                } finally {
                    setTimeout(function () { loadVideoDatasPollPaused = false; }, 0);
                }
                showToast('info', '\u0110\u00e3 x\u00f3a', '\u0110\u00e3 x\u00f3a \u1ea3nh ch\u1edd (n\u1ebfu c\u00f3), to\u00e0n b\u1ed9 video tr\u00ean l\u01b0\u1edbi v\u00e0 d\u1eebng poll loadVideoDatas.', 2600);
            });
        }

        /* ════════════════════════════════════════
           FILE HANDLING (input + drag-drop)
        ════════════════════════════════════════ */
        var hostImagePickWaiters = {};

        window.addEventListener('__ac_image_files_picked', function (ev) {
            try {
                var d = ev && ev.detail ? ev.detail : {};
                var reqId = d && d.requestId ? String(d.requestId) : '';
                if (!reqId) return;
                var waiter = hostImagePickWaiters[reqId];
                if (!waiter) return;
                delete hostImagePickWaiters[reqId];
                waiter(d);
            } catch (_) {}
        });

        function processHostPickedFiles(items) {
            if (!Array.isArray(items) || !items.length) return;
            var added = 0;
            for (var i = 0; i < items.length; i++) {
                var it = items[i] || {};
                var dataUrl = String(it.dataUrl || '').trim();
                if (!/^data:image\//i.test(dataUrl)) continue;
                var name = String(it.name || '').trim() || ('image_' + uid() + '.png');
                var fullPath = String(it.path || '').trim();
                pendingImages.push({
                    id: uid(),
                    base64: dataUrl,
                    filename: name,
                    originalName: name,
                    size: Number(it.size || 0) || 0,
                    linkPathImage: fullPath || name
                });
                added++;
            }
            if (added > 0) {
                renderPendingGrid();
                showToast('info', '\u0110\u00e3 th\u00eam ' + added + ' \u1ea3nh', '\u0110\u00e3 l\u1ea5y \u0111\u01b0\u1eddng d\u1eabn \u0111\u1ea7y \u0111\u1ee7 t\u1eeb host', 2600);
            }
        }

        function tryPickImagesFromHost() {
            if (typeof window.acPickImageFiles !== 'function') return false;
            var reqId = 'img_pick_' + uid();
            hostImagePickWaiters[reqId] = function (detail) {
                try {
                    if (!detail || !detail.ok) return;
                    processHostPickedFiles(detail.files);
                } catch (_) {}
            };
            try {
                window.acPickImageFiles(reqId);
            } catch (_) {
                delete hostImagePickWaiters[reqId];
                return false;
            }
            setTimeout(function () {
                if (!hostImagePickWaiters[reqId]) return;
                delete hostImagePickWaiters[reqId];
            }, 30000);
            return true;
        }

        function parsePathLikeText(rawText) {
            var out = [];
            if (!rawText || typeof rawText !== 'string') return out;
            var lines = rawText.split(/\r?\n/).map(function (x) { return (x || '').trim(); }).filter(Boolean);
            lines.forEach(function (line) {
                var s = line;
                // text/uri-list may include comments.
                if (s.charAt(0) === '#') return;
                try {
                    if (/^file:\/\//i.test(s)) {
                        var p = decodeURIComponent(s.replace(/^file:\/\//i, '')).replace(/\//g, '\\');
                        if (/^\\[a-zA-Z]:\\/.test(p)) p = p.substring(1);
                        s = p;
                    }
                } catch (_) {}
                if (/^[a-zA-Z]:[\\/]/.test(s)) out.push(s.replace(/\//g, '\\'));
            });
            return out;
        }

        function processFiles(files, options) {
            options = options || {};
            var arr = Array.prototype.slice.call(files || []);
            arr = arr.filter(function (f) { return /^image\//i.test(f.type || ''); });
            if (!arr.length) return;
            var done = 0;
            var pathHints = Array.isArray(options.pathHints) ? options.pathHints : [];
            function resolveImagePath(file, idx) {
                if (!file || typeof file !== 'object') return '';
                if (pathHints[idx] && /^[a-zA-Z]:[\\/]/.test(String(pathHints[idx]).trim())) {
                    return String(pathHints[idx]).trim().replace(/\//g, '\\');
                }
                // Desktop wrappers (Electron/WebView2 host) may expose absolute path.
                var rawPath =
                    (typeof file.path === 'string' && file.path) ||
                    (typeof file.fullPath === 'string' && file.fullPath) ||
                    (typeof file.webkitRelativePath === 'string' && file.webkitRelativePath) ||
                    '';
                return rawPath || (file.name || '');
            }
            arr.forEach(function (file, idx) {
                var reader = new FileReader();
                reader.onload = function (ev) {
                    pendingImages.push({
                        id: uid(),
                        base64: ev.target.result,
                        filename: file.name,
                        originalName: file.name,
                        size: file.size,
                        linkPathImage: resolveImagePath(file, idx)
                    });
                    done++;
                    if (done === arr.length) {
                        renderPendingGrid();
                        showToast('info', '\u0110\u00e3 th\u00eam ' + arr.length + ' \u1ea3nh', 'Nh\u1ea5n Upload \u0111\u1ec3 g\u1eedi l\u00ean', 2500);
                    }
                };
                reader.readAsDataURL(file);
            });
        }

        var fileInput = document.getElementById('imgFileInput');
        if (fileInput) {
            fileInput.addEventListener('change', function (e) {
                var hintPaths = [];
                try {
                    var v = String((e && e.target && e.target.value) || '').trim();
                    // Some desktop wrappers still return absolute path in input.value for single file.
                    if (v && /^[a-zA-Z]:[\\/]/.test(v) && !/^[a-zA-Z]:\\fakepath\\/i.test(v)) hintPaths.push(v);
                } catch (_) {}
                processFiles(e.target.files, { pathHints: hintPaths });
                e.target.value = '';
            });
        }

        var dropZone = document.getElementById('imgDropZone');
        if (dropZone) {
            dropZone.addEventListener('click', function () {
                // Prefer native host picker to guarantee absolute file path.
                if (tryPickImagesFromHost()) return;
                if (fileInput) fileInput.click();
            });
            dropZone.addEventListener('dragover',  function (e) { e.preventDefault(); dropZone.classList.add('drag-over'); });
            dropZone.addEventListener('dragleave', function ()  { dropZone.classList.remove('drag-over'); });
            dropZone.addEventListener('dragend',   function ()  { dropZone.classList.remove('drag-over'); });
            dropZone.addEventListener('drop', function (e) {
                e.preventDefault();
                dropZone.classList.remove('drag-over');
                var hintPaths = [];
                try {
                    var dt = e.dataTransfer;
                    if (dt) {
                        hintPaths = hintPaths.concat(parsePathLikeText(dt.getData('text/uri-list') || ''));
                        hintPaths = hintPaths.concat(parsePathLikeText(dt.getData('text/plain') || ''));
                    }
                } catch (_) {}
                processFiles(e.dataTransfer && e.dataTransfer.files, { pathHints: hintPaths });
            });
        }

        /* ════════════════════════════════════════
           UPLOAD BUTTON
           — Generates uploadGuid (batch) + imageGuid (per image)
           — Pre-creates "processing" placeholder cards in col2
        ════════════════════════════════════════ */
        var uploadBtn = document.getElementById('imgUploadBtn');
        if (uploadBtn) {
            uploadBtn.addEventListener('click', function () {
                if (!pendingImages.length) {
                    showToast('warn', 'Ch\u01b0a c\u00f3 \u1ea3nh', 'Vui l\u00f2ng ch\u1ecdn \u00edt nh\u1ea5t 1 \u1ea3nh', 3000);
                    return;
                }
                uploadBtn.disabled = true;
                uploadBtn.innerHTML =
                    '<span style="display:inline-block;width:14px;height:14px;border:2px solid rgba(255,255,255,0.3);' +
                    'border-top-color:#fff;border-radius:50%;animation:spin-btn 0.7s linear infinite;margin-right:6px;vertical-align:middle"></span>' +
                    '\u0110ang upload...';

                // Generate batch GUID (= outputImageGuildId, giống outputGuildId của video)
                var uploadGuid = uid();
                var now = Date.now();

                // Assign imageGuid to each image and pre-create processing cards
                var imageList = pendingImages.map(function (img) {
                    var imageGuid = uid();
                    // Pre-insert as processing in col2
                    imageItemsDict[imageGuid] = {
                        imageGuid: imageGuid,
                        uploadGuid: uploadGuid,
                        fileName: img.filename,
                        status: 'processing',
                        isSuccess: false,
                        linkImage: '',
                        fileMetadataId: '',
                        error: '',
                        addedAt: now
                    };
                    return {
                        imageGuid: imageGuid,
                        base64: img.base64,
                        filename: img.filename,
                        linkPathImage: img.linkPathImage || img.filename || ''
                    };
                });
                trackUploadBatch(uploadGuid, imageList.map(function (i) { return i.imageGuid; }));

                // Immediately show processing cards
                renderUploadedGrid();

                // ════ PARAMS — theo đúng pattern của video ════
                // outputImageGuildId = uploadGuid (batch ID, giống outputGuildId của video)
                // outputImageUpload  = mảng ảnh JSON (giống outputParams của video)
                // statusCreate       = '1' (phân biệt: 0=video, 1=image)
                var imgGuildEl = document.getElementById('outputImageGuildId');
                var imgDataEl  = document.getElementById('outputImageUpload');
                var scEl       = document.getElementById('statusCreate');
                if (imgGuildEl) imgGuildEl.value = uploadGuid;
                if (imgDataEl)  imgDataEl.value  = JSON.stringify(
                    imageList.map(function (i) {
                        return {
                            uploadGuid: uploadGuid,
                            imageGuid: i.imageGuid,
                            base64: i.base64,
                            filename: i.filename,
                            linkPathImage: i.linkPathImage || i.filename || ''
                        };
                    })
                );
                if (scEl) scEl.value = '1'; // 1 = upload ảnh

                try { if (typeof acSubmit        === 'function') acSubmit(); }        catch (_) {}
                try { if (typeof acStartWorkflow === 'function') acStartWorkflow(); } catch (_) {}

                showToast('success',
                    '\u0110\u00e3 g\u1eedi ' + imageList.length + ' \u1ea3nh',
                    'Batch GUID: ' + uploadGuid.substring(0, 8) + '...', 3000);

                // Clear pending preview
                pendingImages = [];
                renderPendingGrid();

                setTimeout(function () {
                    uploadBtn.disabled = false;
                    uploadBtn.innerHTML = '<i class="bi bi-cloud-upload-fill"></i> Upload \u1ea2nh';
                }, 1500);
            });
        }

        /* ════════════════════════════════════════
           UPLOADED IMAGES GRID (col 2)
           — Status: processing | done | failed
        ════════════════════════════════════════ */
        function buildUploadCard(img, idx) {
            var card = document.createElement('div');
            var st = img.status || 'processing';
            card.className = 'img-uploaded-card img-status-' + st;
            card.setAttribute('data-image-guid', img.imageGuid);
            card.setAttribute('data-upload-guid', img.uploadGuid || '');
            card.style.animationDelay = Math.min(idx * 0.04, 0.5) + 's';

            var mediaHtml;
            if (st === 'done' && (img.resolvedImageUrl || img.linkImage || img.imageLocalPath)) {
                var disp = pickUploadedImageDisplayUrl(img);
                var srcAttr = disp ? esc(disp) : IMG_UPLOAD_PLACEHOLDER;
                var g = String(img.imageGuid || '').replace(/'/g, '');
                mediaHtml =
                    '<div class="img-uploaded-media" onclick="imgOpenUploadedImageByGuid(\'' + g + '\')">' +
                        '<img src="' + srcAttr + '" alt="' + esc(img.fileName || '') + '" loading="lazy">' +
                        '<div class="img-card-zoom-overlay"><i class="bi bi-zoom-in"></i></div>' +
                    '</div>';
            } else if (st === 'failed') {
                mediaHtml =
                    '<div class="img-uploaded-media img-media-failed">' +
                        '<div class="img-status-overlay">' +
                            '<i class="bi bi-exclamation-triangle-fill" style="font-size:26px;color:#f87171;margin-bottom:6px"></i>' +
                            '<div class="img-overlay-label">Upload th\u1ea5t b\u1ea1i</div>' +
                        '</div>' +
                    '</div>';
            } else {
                // processing
                mediaHtml =
                    '<div class="img-uploaded-media img-media-processing">' +
                        '<div class="img-shimmer"></div>' +
                        '<div class="img-status-overlay">' +
                            '<div class="img-spin-ring"></div>' +
                        '</div>' +
                    '</div>';
            }

            var badgeHtml;
            if (st === 'processing') {
                badgeHtml = '<div class="img-status-badge img-badge-processing"><i class="bi bi-hourglass-split"></i> UPLOADING</div>';
            } else if (st === 'done') {
                badgeHtml = '<div class="img-status-badge img-badge-done"><i class="bi bi-check-circle-fill"></i> DONE</div>';
            } else {
                badgeHtml = '<div class="img-status-badge img-badge-failed"><i class="bi bi-x-circle-fill"></i> FAILED</div>';
            }

            var bodyHtml = '<div class="img-uploaded-body">' +
                '<div class="img-uploaded-name" title="' + esc(img.fileName || 'Kh\u00f4ng t\u00ean') + '">' + esc(img.fileName || 'Kh\u00f4ng t\u00ean') + '</div>';

            if (st === 'failed' && img.error) {
                bodyHtml += '<div class="img-uploaded-error" title="' + esc(img.error) + '">' + esc((img.error || '').substring(0, 50)) + '</div>';
            } else {
                // Show upload guid (trimmed) as batch reference
                var guidLabel = (img.uploadGuid || '').substring(0, 8);
                if (guidLabel) bodyHtml += '<div class="img-uploaded-meta">Batch: ' + esc(guidLabel) + '\u2026</div>';
            }
            bodyHtml += '</div>';

            card.innerHTML = mediaHtml + badgeHtml + bodyHtml;
            return card;
        }

        function renderUploadedGrid() {
            var grid = document.getElementById('imgUploadedGrid');
            if (!grid) return;
            updateUploadedCount();

            var keys = Object.keys(imageItemsDict);
            if (!keys.length) {
                grid.innerHTML =
                    '<div class="img-empty-uploaded">' +
                        '<i class="bi bi-images" style="font-size:40px;color:var(--accent);opacity:0.35;margin-bottom:4px"></i>' +
                        '<div style="font-size:13px;font-weight:600;color:var(--clr2)">Ch\u01b0a c\u00f3 \u1ea3nh n\u00e0o \u0111\u00e3 upload</div>' +
                        '<div style="font-size:11px;color:var(--clr3);margin-top:4px">Upload \u1ea3nh t\u1eeb c\u1ed9t b\u00ean tr\u00e1i \u0111\u1ec3 b\u1eaft \u0111\u1ea7u</div>' +
                    '</div>';
                return;
            }

            // Sort: processing first (show at top), then by addedAt desc
            var items = keys.map(function (k) { return imageItemsDict[k]; });
            items.sort(function (a, b) {
                var ords = { processing: 0, done: 1, failed: 2 };
                var so = (ords[a.status] || 1) - (ords[b.status] || 1);
                if (so !== 0) return so;
                return (b.addedAt || 0) - (a.addedAt || 0);
            });

            // Smart update: update existing cards in-place, append/remove new/old ones
            var existingByGuid = {};
            var existingNodes = grid.querySelectorAll('.img-uploaded-card');
            for (var i = 0; i < existingNodes.length; i++) {
                var g = existingNodes[i].getAttribute('data-image-guid');
                if (g) existingByGuid[g] = existingNodes[i];
            }

            // Remove cards no longer in dict
            for (var exG in existingByGuid) {
                if (!imageItemsDict[exG]) existingByGuid[exG].remove();
            }

            // Build target order without clearing whole grid to avoid full image reload.
            var seen = {};
            items.forEach(function (img, idx) {
                var node = existingByGuid[img.imageGuid];
                var newSt = img.status || 'processing';
                if (node) {
                    var oldSt = node.getAttribute('data-status') || '';
                    if (oldSt !== newSt) {
                        var rebuilt = buildUploadCard(img, idx);
                        rebuilt.setAttribute('data-status', newSt);
                        node.parentNode && node.parentNode.replaceChild(rebuilt, node);
                        node = rebuilt;
                    } else {
                        node.setAttribute('data-status', newSt);
                    }
                } else {
                    node = buildUploadCard(img, idx);
                    node.className += ' animate__animated animate__fadeIn';
                    node.setAttribute('data-status', newSt);
                }
                seen[img.imageGuid] = true;
                grid.appendChild(node);
            });

            // Remove leftover nodes not in target list
            var allNow = grid.querySelectorAll('.img-uploaded-card[data-image-guid]');
            for (var j = 0; j < allNow.length; j++) {
                var gg = allNow[j].getAttribute('data-image-guid');
                if (!gg || seen[gg]) continue;
                allNow[j].remove();
            }
        }

        function applyImageCardSizeMode(mode) {
            var grid = document.getElementById('imgUploadedGrid');
            if (!grid) return;
            var m = String(mode || 'md').toLowerCase();
            if (!/^(sm|md|lg|xl|xx1|xx2)$/.test(m)) m = 'md';
            imageCardSizeMode = m;
            grid.classList.remove('size-sm', 'size-md', 'size-lg', 'size-xl', 'size-xx1', 'size-xx2');
            grid.classList.add('size-' + m);
            var btns = document.querySelectorAll('.img-card-size-btn');
            for (var i = 0; i < btns.length; i++) {
                btns[i].classList.toggle('active', btns[i].getAttribute('data-size') === m);
            }
            try {
                var p = loadUiPrefs();
                p.imageCardSizeMode = m;
                saveUiPrefs(p);
            } catch (_) {}
        }

        function applyImageFitContain(enabled) {
            fitContainImage = !!enabled;
            var app = document.getElementById('app');
            if (app) app.classList.toggle('fit-image-contain', fitContainImage);
            var cb = document.getElementById('imgFitContainCheck');
            if (cb) cb.checked = fitContainImage;
            try {
                var p = loadUiPrefs();
                p.fitContainImage = fitContainImage;
                saveUiPrefs(p);
            } catch (_) {}
        }

        function applyImageFitWidth(enabled) {
            fitWidthImage = !!enabled;
            var app = document.getElementById('app');
            if (app) app.classList.toggle('fit-image-width', fitWidthImage);
            var cb = document.getElementById('imgFitWidthCheck');
            if (cb) cb.checked = fitWidthImage;
            try {
                var p = loadUiPrefs();
                p.fitWidthImage = fitWidthImage;
                saveUiPrefs(p);
            } catch (_) {}
        }

        /** Dict/array/string from backend -> array of objects. Values may be JSON strings (double-serialized). */
        function normalizeDataImagesToItems(dataImages) {
            if (dataImages == null) return null;
            var out = [];

            function pushOneObjectRecord(obj) {
                if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return;
                if (obj.imageGuid || obj.id) out.push(obj);
                else {
                    // Nested dict keyed by uploadGuid/imageGuid -> recurse values.
                    Object.values(obj).forEach(function (v) { consumeAny(v); });
                }
            }

            function consumeStringRecord(s) {
                var t = String(s || '').trim();
                if (!t) return;
                try {
                    consumeAny(JSON.parse(t));
                    return;
                } catch (_) {}
                var loose = parseLooseDataImageRecord(t);
                if (loose) out.push(loose);
            }

            function consumeAny(input) {
                if (input == null) return;
                if (Array.isArray(input)) {
                    for (var i = 0; i < input.length; i++) consumeAny(input[i]);
                    return;
                }
                if (typeof input === 'string') {
                    consumeStringRecord(input);
                    return;
                }
                if (typeof input === 'object') {
                    pushOneObjectRecord(input);
                }
            }

            consumeAny(dataImages);
            return out.length ? out : null;
        }

        function isTerminalImageStatus(st) {
            var s = String(st || '').toLowerCase();
            return s === 'done' || s === 'failed';
        }

        function getCurrentDataImagesSnapshot() {
            var dataImages = null;
            try {
                if (window.__ac && window.__ac.live) dataImages = window.__ac.live.dataImages;
                if (!dataImages && window.__ac && window.__ac.datas) dataImages = window.__ac.datas.dataImages;
            } catch (_) {}
            return dataImages;
        }

        function processDataImagesPayload(dataImages, options) {
            var items = normalizeDataImagesToItems(dataImages);
            if (!items || !items.length) return { changed: false, completedRows: [], failedRows: [] };
            return applyIncomingDataImages(items, options || { onlyTracked: false });
        }

        function showImageUploadToasts(applied) {
            if (!applied) return;
            applied.completedRows.forEach(function (item) {
                showToast('success', 'Ảnh đã upload', (item.fileName || '') + ' → done', 2500);
            });
            applied.failedRows.forEach(function (item) {
                var errMsg = (item.error && String(item.error).trim()) ? item.error : (item.fileName || '');
                showToast('error', 'Upload thất bại', errMsg, 3500);
            });
        }

        function timeoutUploadBatch(uploadGuid, batch) {
            if (!uploadGuid || !batch || batch.completed) return;
            batch.completed = true;
            batch.timedOut = true;
            var remain = 0;
            Object.keys(imageGuidTracker).forEach(function (g) {
                var tracked = imageGuidTracker[g];
                if (!tracked || tracked.uploadGuid !== uploadGuid || tracked.terminal) return;
                tracked.terminal = true;
                remain++;
                var previous = imageItemsDict[g] || {};
                imageItemsDict[g] = {
                    imageGuid: g,
                    uploadGuid: uploadGuid,
                    fileName: previous.fileName || '',
                    status: 'failed',
                    isSuccess: false,
                    linkImage: previous.linkImage || '',
                    fileMetadataId: previous.fileMetadataId || '',
                    error: previous.error || 'Quá 180s chưa có kết quả trả về',
                    addedAt: previous.addedAt || Date.now(),
                    imageLocalPath: previous.imageLocalPath || '',
                    resolvedImageUrl: previous.resolvedImageUrl || ''
                };
            });
            if (remain > 0) {
                showToast('error', 'Batch timeout', 'UploadGuid ' + uploadGuid.substring(0, 8) + '… quá 180s', 3800);
            }
        }

        function ensureImageBatchPolling() {
            if (asyncPushReceiverBound) return;
            if (imageBatchPollTimer) return;
            imageBatchPollTimer = setInterval(function () {
                var activeCount = 0;
                var now = Date.now();

                var snapshot = getCurrentDataImagesSnapshot();
                if (snapshot) {
                    var applied = processDataImagesPayload(snapshot, { onlyTracked: true });
                    if (applied.changed) {
                        renderUploadedGrid();
                        hydrateAllUploadedImagesNeedingResolve();
                    }
                    showImageUploadToasts(applied);
                }

                Object.keys(uploadBatchTracker).forEach(function (ug) {
                    var batch = uploadBatchTracker[ug];
                    if (!batch || batch.completed) return;
                    if (batch.timeoutAt > 0 && now >= batch.timeoutAt) {
                        timeoutUploadBatch(ug, batch);
                        return;
                    }
                    if (batch.expectedCount > 0 && batch.terminalCount >= batch.expectedCount) {
                        batch.completed = true;
                        return;
                    }
                    activeCount++;
                });

                if (!activeCount) {
                    clearInterval(imageBatchPollTimer);
                    imageBatchPollTimer = null;
                }
            }, IMAGE_BATCH_POLL_INTERVAL_MS);
        }

        function trackUploadBatch(uploadGuid, imageGuids) {
            var gids = Array.isArray(imageGuids) ? imageGuids.filter(Boolean) : [];
            var startedAt = Date.now();
            uploadBatchTracker[uploadGuid] = {
                expectedCount: gids.length,
                terminalCount: 0,
                doneByGuid: {},
                completed: false,
                timedOut: false,
                startedAt: startedAt,
                timeoutAt: startedAt + IMAGE_BATCH_TIMEOUT_MS
            };
            gids.forEach(function (g) {
                imageGuidTracker[g] = { uploadGuid: uploadGuid, terminal: false };
            });
            ensureImageBatchPolling();
        }

        function completeTrackedGuidIfNeeded(row) {
            if (!row || !row.imageGuid || !isTerminalImageStatus(row.status)) return false;
            var guid = String(row.imageGuid);
            var tracked = imageGuidTracker[guid];
            if (!tracked || tracked.terminal) return false;

            tracked.terminal = true;
            var uploadGuid = tracked.uploadGuid || row.uploadGuid || '';
            var batch = uploadBatchTracker[uploadGuid];
            if (batch && !batch.doneByGuid[guid]) {
                batch.doneByGuid[guid] = true;
                batch.terminalCount++;
                if (!batch.completed && batch.expectedCount > 0 && batch.terminalCount >= batch.expectedCount) {
                    batch.completed = true;
                    showToast(
                        'success',
                        'Upload batch hoàn tất',
                        'Đã nhận đủ ' + batch.terminalCount + '/' + batch.expectedCount + ' ảnh từ dataImages',
                        2600
                    );
                }
            }
            return true;
        }

        function applyIncomingDataImages(items, options) {
            if (!items || !items.length) return { changed: false, completedRows: [], failedRows: [] };
            var opts = options || {};
            var changed = false;
            var completedRows = [];
            var failedRows = [];

            items.forEach(function (item) {
                if (!item || typeof item !== 'object') return;
                var iguid = item.imageGuid || item.id || null;
                if (!iguid) return;
                iguid = String(iguid);

                var tracked = imageGuidTracker[iguid];
                if (tracked && tracked.terminal) return;
                if (!tracked && opts.onlyTracked) return;
                if (tracked && tracked.uploadGuid) {
                    var incomingUploadGuid = String(item.uploadGuid || '');
                    if (incomingUploadGuid && incomingUploadGuid !== String(tracked.uploadGuid)) return;
                }

                var normalizedItem = {};
                Object.keys(item).forEach(function (k) { normalizedItem[k] = item[k]; });
                normalizedItem.status = normalizeStatusToState(item.status, item.isSuccess);
                if (normalizedItem.status === 'failed' && !String(normalizedItem.error || '').trim()) {
                    normalizedItem.error = 'Backend trả trạng thái lỗi (status != 200)';
                }

                var previous = imageItemsDict[iguid] || {};
                var row = computeImageDictEntry(normalizedItem, previous);
                if (!row) return;

                var beforeSig = JSON.stringify({
                    status: previous.status || '',
                    isSuccess: !!previous.isSuccess,
                    linkImage: previous.linkImage || '',
                    error: previous.error || '',
                    fileMetadataId: previous.fileMetadataId || ''
                });
                var afterSig = JSON.stringify({
                    status: row.status || '',
                    isSuccess: !!row.isSuccess,
                    linkImage: row.linkImage || '',
                    error: row.error || '',
                    fileMetadataId: row.fileMetadataId || ''
                });

                imageItemsDict[iguid] = row;
                if (beforeSig !== afterSig) changed = true;

                var justCompleted = completeTrackedGuidIfNeeded(row);
                if (justCompleted) {
                    if (row.status === 'done' && row.isSuccess) completedRows.push(row);
                    else if (row.status === 'failed') failedRows.push(row);
                }
            });

            return { changed: changed, completedRows: completedRows, failedRows: failedRows };
        }

        /* ════════════════════════════════════════
           POLL dataImages — merge/update by imageGuid
        ════════════════════════════════════════ */
        try {
            if (window.__ac && typeof window.__ac.onUpdate === 'function') {
                window.__ac.onUpdate('dataImages', function (dataImages) {
                    if (!dataImages) return;
                    var applied = processDataImagesPayload(dataImages, { onlyTracked: false });
                    if (applied.changed) {
                        renderUploadedGrid();
                        hydrateAllUploadedImagesNeedingResolve();
                        syncPromptMentionPreview();
                    }
                    showImageUploadToasts(applied);
                });
            }
        } catch (_) {}

        /* ════════════════════════════════════════
           EAGER INITIAL LOAD CHO ẢNH (fix: dataImages không hiện lần đầu)
        ════════════════════════════════════════ */
        (function doEagerInitialImageLoad() {
            function attempt() {
                try {
                    var dataImages = null;
                    dataImages = getCurrentDataImagesSnapshot();
                    if (!dataImages) return;
                    var applied = processDataImagesPayload(dataImages, { onlyTracked: false });
                    if (applied.changed) {
                        renderUploadedGrid();
                        hydrateAllUploadedImagesNeedingResolve();
                        syncPromptMentionPreview();
                    }
                } catch (_) {}
            }
            attempt();
            setTimeout(attempt, 300);
            setTimeout(attempt, 1000);
            setTimeout(attempt, 2500);
        })();

        /* ════════════════════════════════════════
           @MENTION AUTOCOMPLETE (done images only)
        ════════════════════════════════════════ */
        var mentionDropdown = null;
        var mentionQuery    = '';
        var mentionStart    = -1;
        var mentionHiIdx    = 0;

        function getDoneImages() {
            return Object.values(imageItemsDict).filter(function (img) {
                return img.status === 'done' && !!(img.fileMetadataId || '').trim() &&
                    (img.resolvedImageUrl || img.linkImage || img.imageLocalPath || img.fileName);
            });
        }

        function getMentionMatches(q) {
            refreshDoneImagesFromSnapshot();
            var qLow = (q || '').toLowerCase();
            return getDoneImages().filter(function (img) {
                return !qLow || (img.fileName || '').toLowerCase().indexOf(qLow) >= 0;
            }).slice(0, 12);
        }

        function refreshDoneImagesFromSnapshot() {
            try {
                var snap = getCurrentDataImagesSnapshot();
                if (!snap) return;
                var applied = processDataImagesPayload(snap, { onlyTracked: false });
                if (applied.changed) {
                    renderUploadedGrid();
                    hydrateAllUploadedImagesNeedingResolve();
                    syncPromptMentionPreview();
                }
            } catch (_) {}
        }

        function syncPromptMentionPreview() {
            var box = document.getElementById('promptMentionPreview');
            var promptEl = document.getElementById('promptInput');
            if (!box || !promptEl) return;
            var tokenRe = /@([A-Za-z0-9._:-]+)/g;
            var text = String(promptEl.value || '');
            var m;
            var tokenSet = {};
            while ((m = tokenRe.exec(text)) !== null) {
                var tk = String(m[1] || '').trim();
                if (tk) tokenSet[tk] = true;
            }
            var tokens = Object.keys(tokenSet);
            if (!tokens.length) {
                box.innerHTML = '';
                return;
            }
            var done = getDoneImages();
            var byToken = {};
            done.forEach(function (img) {
                var md = String(img.fileMetadataId || '').trim();
                if (md) byToken[md] = img;
            });

            box.innerHTML = '';
            tokens.forEach(function (tk) {
                var img = byToken[tk];
                if (!img) return;
                var chip = document.createElement('div');
                chip.className = 'prompt-mention-chip';
                var thumb = pickUploadedImageDisplayUrl(img);
                var localPath = String(img.imageLocalPath || pickImageLocalPath(img.linkImage) || '').trim();
                var label = '@' + tk;
                chip.innerHTML =
                    (thumb
                        ? '<img src="' + esc(thumb) + '" alt="" loading="lazy">'
                        : '<div class="mention-item-thumb-ph" style="width:22px;height:22px;border-radius:50%"><i class="bi bi-image" style="font-size:10px;color:var(--clr3)"></i></div>'
                    ) +
                    '<span class="pm-label" title="' + esc(label) + '">' + esc(label) + '</span>';
                box.appendChild(chip);
                if (!thumb && localPath) {
                    resolveLocalPlayableUrl(localPath, '', function (resolved) {
                        if (!resolved || /^file:/i.test(resolved)) return;
                        img.resolvedImageUrl = resolved;
                        var ph = chip.querySelector('.mention-item-thumb-ph');
                        if (!ph) return;
                        ph.outerHTML = '<img src="' + esc(resolved) + '" alt="" loading="lazy">';
                    });
                }
            });
        }

        function closeMentionDropdown() {
            if (mentionDropdown && mentionDropdown.parentNode) {
                mentionDropdown.parentNode.removeChild(mentionDropdown);
            }
            mentionDropdown = null;
            mentionQuery    = '';
            mentionStart    = -1;
            mentionHiIdx    = 0;
        }

        function highlightMentionItem(idx) {
            if (!mentionDropdown) return;
            var items = mentionDropdown.querySelectorAll('.mention-item');
            mentionHiIdx = Math.max(0, Math.min(idx, items.length - 1));
            for (var i = 0; i < items.length; i++) {
                items[i].classList.toggle('highlighted', i === mentionHiIdx);
            }
            if (items[mentionHiIdx]) items[mentionHiIdx].scrollIntoView({ block: 'nearest' });
        }

        function buildMentionDropdown(items) {
            closeMentionDropdown();
            if (!items || !items.length) return;
            var promptEl = document.getElementById('promptInput');
            if (!promptEl) return;

            var dropdown = document.createElement('div');
            dropdown.className = 'mention-dropdown';
            dropdown.id = 'mentionDropdown';

            items.forEach(function (img, idx) {
                var item = document.createElement('div');
                item.className = 'mention-item' + (idx === 0 ? ' highlighted' : '');
                item.setAttribute('data-idx', String(idx));
                item.setAttribute('data-token', String(img.fileMetadataId || img.imageGuid || img.fileName || ''));
                var thumb = pickUploadedImageDisplayUrl(img);
                var src = thumb ? esc(thumb) : '';
                var localPath = String(img.imageLocalPath || pickImageLocalPath(img.linkImage) || '').trim();
                item.innerHTML =
                    (src
                        ? '<img class="mention-item-thumb" src="' + src + '" alt="" loading="lazy">'
                        : '<div class="mention-item-thumb-ph"><i class="bi bi-image" style="font-size:12px;color:var(--clr3)"></i></div>'
                    ) +
                    '<span class="mention-item-name">@' + esc(img.fileName || '') + '</span>';
                var mentionToken = (img.fileMetadataId || img.imageGuid || img.fileName || '');
                if (!src && localPath) {
                    resolveLocalPlayableUrl(localPath, '', function (resolved) {
                        if (!resolved || /^file:/i.test(resolved)) return;
                        img.resolvedImageUrl = resolved;
                        var ph = item.querySelector('.mention-item-thumb-ph');
                        if (!ph) return;
                        ph.outerHTML = '<img class="mention-item-thumb" src="' + esc(resolved) + '" alt="" loading="lazy">';
                    });
                }
                dropdown.appendChild(item);
            });
            dropdown.addEventListener('pointerdown', function (e) {
                var it = e && e.target && e.target.closest ? e.target.closest('.mention-item[data-token]') : null;
                if (!it) return;
                e.preventDefault();
                insertMention(it.getAttribute('data-token') || '');
            });

            var rect = promptEl.getBoundingClientRect();
            dropdown.style.position  = 'fixed';
            dropdown.style.left      = rect.left + 'px';
            dropdown.style.width     = rect.width + 'px';
            dropdown.style.top       = (rect.bottom + 4) + 'px';
            dropdown.style.maxHeight = Math.min(240, window.innerHeight - rect.bottom - 12) + 'px';
            document.body.appendChild(dropdown);
            mentionDropdown = dropdown;
            mentionHiIdx = 0;
        }

        function insertMention(fileMetadataId) {
            var promptEl = document.getElementById('promptInput');
            if (!promptEl) { closeMentionDropdown(); return; }
            var token = String(fileMetadataId || '').trim();
            if (!token) { closeMentionDropdown(); return; }
            if (mentionStart < 0) {
                var pv = String(promptEl.value || '');
                var pp = Number(promptEl.selectionStart || 0);
                for (var k = pp - 1; k >= 0; k--) {
                    if (pv[k] === '\n' || pv[k] === ' ') break;
                    if (pv[k] === '@') { mentionStart = k; mentionQuery = pv.substring(k + 1, pp); break; }
                }
                if (mentionStart < 0) { closeMentionDropdown(); return; }
            }
            var val    = promptEl.value;
            var before = val.substring(0, mentionStart);
            var after  = val.substring(mentionStart + 1 + mentionQuery.length);
            var tag    = '@' + token + ' ';
            promptEl.value = before + tag + after;
            var pos = before.length + tag.length;
            promptEl.setSelectionRange(pos, pos);
            promptEl.focus();
            closeMentionDropdown();
            try { var p = loadUiPrefs(); p.promptInputText = promptEl.value; saveUiPrefs(p); } catch (_) {}
            try { updateBatchInfo(); } catch (_) {}
            syncPromptMentionPreview();
        }

        var promptFocus = document.getElementById('promptInput');
        if (promptFocus) {
            promptFocus.addEventListener('input', function () {
                var val = promptFocus.value;
                var pos = promptFocus.selectionStart || 0;
                var atPos = -1;
                for (var i = pos - 1; i >= 0; i--) {
                    if (val[i] === '\n' || val[i] === ' ') break;
                    if (val[i] === '@') { atPos = i; break; }
                }
                if (atPos >= 0) {
                    mentionStart = atPos;
                    mentionQuery = val.substring(atPos + 1, pos);
                    refreshDoneImagesFromSnapshot();
                    var matches = getMentionMatches(mentionQuery);
                    if (matches.length) buildMentionDropdown(matches);
                    else closeMentionDropdown();
                } else {
                    closeMentionDropdown();
                }
            });

            promptFocus.addEventListener('keydown', function (e) {
                if (!mentionDropdown) return;
                if (e.key === 'ArrowDown') {
                    e.preventDefault(); highlightMentionItem(mentionHiIdx + 1);
                } else if (e.key === 'ArrowUp') {
                    e.preventDefault(); highlightMentionItem(mentionHiIdx - 1);
                } else if (e.key === 'Enter' && !e.ctrlKey && !e.metaKey) {
                    var matches = getMentionMatches(mentionQuery);
                    if (matches[mentionHiIdx]) {
                        e.preventDefault(); e.stopPropagation();
                        insertMention(matches[mentionHiIdx].fileMetadataId || '');
                    }
                } else if (e.key === 'Escape') {
                    closeMentionDropdown();
                }
            });
            promptFocus.addEventListener('blur',  function () { setTimeout(closeMentionDropdown, 160); });
            promptFocus.addEventListener('click',  function () { closeMentionDropdown(); });
        }

        /* ── ESC closes lightbox ── */
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                var lb = document.getElementById('imageLightbox');
                if (lb && lb.style.display === 'flex') window.closeLightbox({ target: lb });
                closeMentionDropdown();
            }
        });

        /* ── Init ── */
        window.grokRefreshImageData = function () {
            try {
                var snap = getCurrentDataImagesSnapshot();
                var applied = processDataImagesPayload(snap, { onlyTracked: false });
                if (applied.changed) {
                    renderUploadedGrid();
                    hydrateAllUploadedImagesNeedingResolve();
                }
                showImageUploadToasts(applied);
                syncPromptMentionPreview();
                showToast('info', 'Làm mới ảnh', 'Đã quét lại dataImages hiện có', 1800);
            } catch (_) {}
        };

        window.__grokApplyAsyncImageItem = function (item) {
            try {
                if (!item || typeof item !== 'object' || !item.imageGuid) return;
                var applied = applyIncomingDataImages([item], { onlyTracked: false });
                if (applied.changed) {
                    renderUploadedGrid();
                    hydrateAllUploadedImagesNeedingResolve();
                }
                showImageUploadToasts(applied);
                syncPromptMentionPreview();
            } catch (_) {}
        };

        window.__grokApplyAsyncDataImages = function (payload) {
            try {
                var applied = processDataImagesPayload(payload, { onlyTracked: false });
                if (applied.changed) {
                    renderUploadedGrid();
                    hydrateAllUploadedImagesNeedingResolve();
                }
                showImageUploadToasts(applied);
                syncPromptMentionPreview();
            } catch (_) {}
        };

        window.grokClearUploadedImages = function () {
            imageItemsDict = {};
            uploadBatchTracker = {};
            imageGuidTracker = {};
            if (imageBatchPollTimer) {
                clearInterval(imageBatchPollTimer);
                imageBatchPollTimer = null;
            }
            renderUploadedGrid();
            syncPromptMentionPreview();
            showToast('info', 'Đã dọn ảnh', 'Đã xóa danh sách ảnh đã upload', 2000);
        };

        (function initImageViewControls() {
            var prefs = loadUiPrefs();
            imageCardSizeMode = String(prefs.imageCardSizeMode || 'md').toLowerCase();
            fitContainImage = !!prefs.fitContainImage;
            fitWidthImage = !!prefs.fitWidthImage;

            var btns = document.querySelectorAll('.img-card-size-btn');
            for (var i = 0; i < btns.length; i++) {
                (function (btn) {
                    btn.addEventListener('click', function () {
                        applyImageCardSizeMode(btn.getAttribute('data-size') || 'md');
                    });
                })(btns[i]);
            }
            var fitCb = document.getElementById('imgFitContainCheck');
            if (fitCb) {
                fitCb.checked = fitContainImage;
                fitCb.addEventListener('change', function () {
                    applyImageFitContain(!!fitCb.checked);
                    if (!fitCb.checked) {
                        var fitWidthCb2 = document.getElementById('imgFitWidthCheck');
                        if (fitWidthCb2 && fitWidthCb2.checked) {
                            fitWidthCb2.checked = false;
                            applyImageFitWidth(false);
                        }
                    }
                });
            }
            var fitWidthCb = document.getElementById('imgFitWidthCheck');
            if (fitWidthCb) {
                fitWidthCb.checked = fitWidthImage;
                fitWidthCb.addEventListener('change', function () {
                    if (fitWidthCb.checked && fitCb && !fitCb.checked) {
                        fitCb.checked = true;
                        applyImageFitContain(true);
                    }
                    applyImageFitWidth(!!fitWidthCb.checked);
                });
            }
            applyImageCardSizeMode(imageCardSizeMode);
            applyImageFitContain(fitContainImage);
            applyImageFitWidth(fitWidthImage);
        })();

        window.grokSyncPromptMentionPreview = syncPromptMentionPreview;
        window.__grokSyncImgClearBtn = function () { updatePendingCount(); };
        renderUploadedGrid();
        updatePendingCount();
        syncPromptMentionPreview();
    })();

})();
