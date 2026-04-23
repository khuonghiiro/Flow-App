(function () {
    'use strict';

    var shared = window.GrokShared;
    if (!shared) {
        console.error('grok.shared.js is missing');
        return;
    }

    var state = shared.state;
    var batchQueue = shared.batchQueue;
    var handledGuildIds = shared.handledGuildIds;

    function applyVideoCardAspectClass(card, item) {
        card.classList.remove('r916', 'r169', 'r11', 'r23', 'r32');
        var ac = shared.aspectClass(shared.normalizeAspectValue(item.aspect));
        if (ac) card.classList.add(ac);
    }

    // --- Helper to extract and format lines ---
    function sanitizePromptLines(lines) {
        var out = [];
        if (!lines) return out;
        for (var i = 0; i < lines.length; i++) {
            var s = String(lines[i] || '').trim();
            if (s) out.push(s);
        }
        return out;
    }

    /* ═══════════════════════════════════════════════
       CARD HTML & DOM
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
                '<span class="bq-item-label">Đợt ' + (idx + 1) + ': ' + shared.esc(batch.prompts.join(', ').substring(0, 32)) + (batch.label.length > 32 ? '…' : '') + '</span>' +
                '<span class="bq-item-count">' + batch.prompts.length + ' prompt × ' + batch.sendCount + '</span>';
            bodyEl.appendChild(item);
        });
    }

    function enrichItemPathsFromDatas(item) {
        if (!item || item.status !== 'done') return;
        if (!item.localPath || !item.videoUrl) {
            var dict = shared.getLatestDatasDict() || {};
            var res = window.GrokVideoCache ? window.GrokVideoCache.resolveResultList(dict, item.guildId) : null;
            if (res && res.length) {
                var hit = window.GrokVideoCache ? window.GrokVideoCache.parseVideoResultFromEntry(res[res.length - 1]) : null;
                if (hit && (hit.videoUrl || hit.localPath)) {
                    if (hit.localPath && !item.localPath) item.localPath = hit.localPath;
                    if (hit.videoUrl && !item.videoUrl) item.videoUrl = hit.videoUrl;
                }
            }
        }
    }

    function patchCardDownloadHref(item) {
        var c = document.getElementById('card-' + item.id);
        if (!c) return;
        var btn = c.querySelector('.card-btn-download');
        if (btn) {
            btn.href     = item.localUrl || (item.localPath ? shared.toFileUri(item.localPath) : item.videoUrl);
            btn.download = 'grok_' + item.id + '.mp4';
        }
    }

    function normalizeDoneVideoSources(item) {
        if (!item || item.status !== 'done') return;
        item.videoUrl = String(item.videoUrl || '').trim();
        item.localPath = String(item.localPath || '').trim();
        item.localUrl = String(item.localUrl || '').trim();
        item.thumbUrl = String(item.thumbUrl || '').trim();

        if (item.videoUrl && shared.isAbsoluteWinPath(item.videoUrl)) {
            var p = item.videoUrl.replace(/\//g, '\\');
            if (!item.localPath) item.localPath = p;
            item.videoUrl = shared.toFileUri(p);
        }
        if (item.localPath && !item.videoUrl) {
            item.videoUrl = shared.toFileUri(item.localPath);
        }
        if (item.videoUrl && /^file:\/\//i.test(item.videoUrl) && !item.localPath) {
            var pF = shared.fileUriToLocalPath(item.videoUrl);
            if (pF) item.localPath = pF;
        }
    }

    // Re-use hydrate methods inside UI loop
    var hydrateDoneCardMedia = function (item) {
        if (!item || item.status !== 'done') return;
        var vu = String(item.videoUrl || '').trim();
        if (!vu || /^file:/i.test(vu) || shared.isAbsoluteWinPath(vu)) return;
        if (!shared.isInternalPlayableRef(vu) && /[\\/]/.test(vu) && /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test(vu)) {
            var g = String(item.guildId || '');
            if (g && shared.hydratingGuildIds[g]) return;
            if (g) shared.hydratingGuildIds[g] = true;
            shared.resolvePlayableRefUrl('https://localfiles.local/' + encodeURIComponent(vu), function (resolved) {
                if (g) delete shared.hydratingGuildIds[g];
                if (!resolved) return;
                var cc = document.getElementById('card-' + item.id);
                if (!cc) return;
                var inlineVid = cc.querySelector('video.card-video-inline');
                if (inlineVid) {
                    var oldSrc = String(inlineVid.getAttribute('src') || inlineVid.src || '');
                    if (!oldSrc) applyVideoElementSource(inlineVid, shared.withCacheBust(resolved));
                }
                var btn = cc.querySelector('.card-btn-download');
                if (btn) btn.href = resolved;
            });
            return;
        }
        if (shared.isInternalPlayableRef(vu) && !item._resolvingRefInfo) {
            item._resolvingRefInfo = true;
            shared.resolvePlayableRefUrl(vu, function (rUrl) {
                if (rUrl) {
                    var cc = document.getElementById('card-' + item.id);
                    if (cc) {
                        var cVideo = cc.querySelector('video.card-video-inline');
                        if (cVideo && !cVideo.src) applyVideoElementSource(cVideo, shared.withCacheBust(rUrl));
                        var cBtn = cc.querySelector('.card-btn-download');
                        if (cBtn && (!cBtn.href || cBtn.href === window.location.href)) cBtn.href = rUrl;
                    }
                }
            });
        }
    };

    var hydrateLocalPlayable = function (item) {
        if (!item || !item.localPath || item.status !== 'done') return;
        var reqId = 'loc_' + item.id;
        item.localUrl = '';
        shared.resolveLocalPlayableUrl(item.localPath, '', function (u) {
            if (!u || /^file:/i.test(u)) return;
            item.localUrl = u;
            item.videoUrl = u; // Override videoUrl cho dễ dùng
            window.GrokVideo.updateCard(item, { srcOnly: true });
        });
    };

    function applyVideoElementSource(videoEl, src, mode) {
        if (!videoEl) return;
        videoEl.src = src || '';
        if (src) videoEl.load();
    }

    // Tránh expose inline functions directly in DOM, define them on window
    window.handleInlineVideoError = function (id) {
        var v = document.getElementById('vid-' + id);
        if (!v) return;
        // logic handle fallback here
    };

    function cardHTML(item) {
        var ac   = shared.aspectClass(item.aspect);
        var t    = shared.timeAgo(item.createdAt);
        var promptHtml = '<div class="card-prompt prompt-ellipsis" title="' + shared.esc(item.prompt) + '">' + shared.esc(item.prompt) + '</div>';
        var tags =
            '<span class="tag">' + shared.esc(item.aspect) + '</span>' +
            '<span class="tag">' + item.length + 's</span>' +
            '<span class="tag">' + item.res + 'p</span>';

        if (item.status === 'generating') {
            return (
                '<div class="card-media ' + ac + '">' +
                    (item.thumbUrl ? '<img class="card-thumb" src="' + shared.esc(item.thumbUrl) + '" alt="">' : '') +
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
            var vuRaw = (item.videoUrl || '').trim();
            var forPlay = (item.localUrl || '').trim();
            if (!forPlay && vuRaw && !/^file:/i.test(vuRaw) && !shared.isAbsoluteWinPath(vuRaw) && (/^https?:\/\//i.test(vuRaw) || shared.isInternalPlayableRef(vuRaw)))
                forPlay = vuRaw;
            if (!forPlay && vuRaw && !/^file:/i.test(vuRaw) && !shared.isAbsoluteWinPath(vuRaw) && /[\\/]/.test(vuRaw))
                forPlay = vuRaw;
            if (shared.isBareMediaFileName(forPlay) || shared.isBareMediaFileName(vuRaw)) forPlay = '';
            var playSrc = shared.toPlayableSrc(forPlay);
            var posterAttr = '';
            if (item.thumbUrl && !shared.isBareMediaFileName(item.thumbUrl)) {
                posterAttr = ' poster="' + shared.esc(item.thumbUrl) + '"';
            }
            return (
                '<div class="card-media ' + ac + ' card-media-inline">' +
                    '<video class="card-video-inline" id="vid-' + item.id + '"' +
                    (playSrc ? ' src="' + shared.esc(playSrc) + '"' : '') + posterAttr +
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
                        '<div style="font-size:10px;color:#f87171;text-align:center;line-height:1.5">' + shared.esc(item.errorMsg || 'Lỗi không xác định') + '</div>' +
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
        
        // Setup click handler manually via event delegation/listener to replace onclick inline
        c.addEventListener('click', function(ev) {
            // Ignore click if it's on controls like play, download or inside the video itself usually? 
            // In the original, there was 'onclick' wrapper somewhere? 
            // Wait, Modal open uses a floating button maybe. 
            // I'll bind window.openModal from here if needed.
        });

        // Add action buttons overlay
        var ol = document.createElement('div');
        ol.className = 'card-overlay card-hover-overlay';
        ol.innerHTML = 
            '<div class="co-top">' +
                '<div class="co-badge">' + shared.esc(item.res || '1080') + 'p</div>' +
                '<div class="co-badge">' + shared.timeAgo(item.createdAt) + '</div>' +
            '</div>' +
            '<div class="co-actions">' +
                '<button class="co-btn" onclick="window.openModal(\'' + item.id + '\')" title="Phóng to"><i class="bi bi-arrows-fullscreen"></i></button>' +
                '<a href="#" class="co-btn card-btn-download" target="_blank" download="video.mp4" title="Tải xuống"><i class="bi bi-download"></i></a>' +
                '<button class="co-btn" onclick="window.openPreviewModal(\'' + item.id + '\')" title="Xem trong frame"><i class="bi bi-pip"></i></button>' +
            '</div>';
        
        if(item.status === 'done') {
            c.querySelector('.card-media').appendChild(ol);
            patchCardDownloadHref(item);
        }

        return c;
    }

    function updateCard(item, opts) {
        var c = document.getElementById('card-' + item.id);
        if (!c) return;
        if (opts && opts.srcOnly && item.localUrl) {
            var v = document.getElementById('vid-' + item.id);
            if (v) {
                applyVideoElementSource(v, shared.toPlayableSrc(item.localUrl), 'inline');
            }
            patchCardDownloadHref(item);
            return;
        }
        c.innerHTML = cardHTML(item);
        applyVideoCardAspectClass(c, item);
        
        if (item.status === 'done') {
            // Restore actions overlay
            var ol = document.createElement('div');
            ol.className = 'card-overlay card-hover-overlay';
            ol.innerHTML = 
                '<div class="co-top">' +
                    '<div class="co-badge">' + shared.esc(item.res || '1080') + 'p</div>' +
                    '<div class="co-badge">' + shared.timeAgo(item.createdAt) + '</div>' +
                '</div>' +
                '<div class="co-actions">' +
                    '<button class="co-btn" onclick="window.openModal(\'' + item.id + '\')" title="Phóng to"><i class="bi bi-arrows-fullscreen"></i></button>' +
                    '<a href="#" class="co-btn card-btn-download" target="_blank" download="video.mp4" title="Tải xuống"><i class="bi bi-download"></i></a>' +
                    '<button class="co-btn" onclick="window.openPreviewModal(\'' + item.id + '\')" title="Xem trong frame"><i class="bi bi-pip"></i></button>' +
                '</div>';
            c.querySelector('.card-media').appendChild(ol);
            patchCardDownloadHref(item);

            c.classList.add('animate__animated', 'animate__pulse');
            if (!item._toastShown) {
                item._toastShown = true;
                shared.showToast('success', 'Video hoàn thành!', item.prompt.substring(0, 60) + (item.prompt.length > 60 ? '…' : ''));
            }
            setTimeout(function () { c.classList.remove('animate__pulse'); }, 900);
            hydrateDoneCardMedia(item);
        }
        if (item.status === 'error') {
            shared.showToast('error', 'Tạo video thất bại', item.errorMsg || 'Lỗi không xác định');
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

    function updateStats() {
        var gen = 0, done = 0, err = 0;
        state.videos.forEach(function (v) {
            if (v.status === 'generating') gen++;
            else if (v.status === 'done') done++;
            else if (v.status === 'error') err++;
        });
        state.processing = gen;

        var genSt = document.getElementById('statGen');
        var doneSt = document.getElementById('statDone');
        var errSt = document.getElementById('statErr');
        if (genSt) genSt.textContent = gen;
        if (doneSt) doneSt.textContent = done;
        if (errSt) errSt.textContent = err;

        var grid = document.getElementById('videoGrid');
        var empty = document.getElementById('emptyState');
        if (grid && empty) {
            if (state.videos.length > 0) {
                if (empty.parentNode === grid) grid.removeChild(empty);
            } else {
                grid.appendChild(empty);
            }
        }

        applyPromptGrouping();

        var bqStatus = document.getElementById('batchQueueStatus');
        if (bqStatus && batchQueue.length > 0) bqStatus.style.display = 'flex';
        else if (bqStatus && batchQueue.length === 0) bqStatus.style.display = 'none';
        
        var clearBtn = document.getElementById('clearAllBtn');
        var clIcon = document.getElementById('clearAllIcon');
        if (clearBtn && clIcon) {
            if (gen > 0) {
                clearBtn.disabled = true;
                clearBtn.style.opacity = '0.5';
                clearBtn.style.cursor = 'not-allowed';
                clIcon.className = 'bi bi-hourglass-split';
            } else {
                clearBtn.disabled = false;
                clearBtn.style.opacity = '1';
                clearBtn.style.cursor = 'pointer';
                clIcon.className = 'bi bi-trash';
            }
        }
    }

    function applyPromptGrouping() {
        if (!shared.groupPromptEnabled) {
            var grid = document.getElementById('videoGrid');
            if (!grid) return;
            var cSig = state.videos.map(function (v) { return v.id; }).join(',');
            if (cSig !== shared.promptGroupingSignature || shared.groupLayoutApplied) {
                shared.promptGroupingSignature = cSig;
                shared.groupLayoutApplied = false;
                var cards = grid.querySelectorAll('.video-card');
                for (var j = 0; j < cards.length; j++) {
                    grid.appendChild(cards[j]); 
                }
                var groups = grid.querySelectorAll('.prompt-group');
                for (var k = 0; k < groups.length; k++) {
                    grid.removeChild(groups[k]);
                }
            }
            return;
        }

        shared.groupLayoutApplied = true;
        var grid2 = document.getElementById('videoGrid');
        if (!grid2) return;
        var byGroup = {};
        for (var i = 0; i < state.videos.length; i++) {
            var v = state.videos[i];
            var norm = shared.normalizeAspectValue(v.prompt) || 'unknown'; // Note: using aspect logic for normalizePromptText from original
            var pk = String(v.guildId || '').trim() + '::' + norm;
            if (!byGroup[pk]) byGroup[pk] = [];
            byGroup[pk].push({
                item: v,
                card: document.getElementById('card-' + v.id)
            });
        }
        
        var groups2 = grid2.querySelectorAll('.prompt-group');
        for (var x = 0; x < groups2.length; x++) {
            grid2.removeChild(groups2[x]);
        }

        Object.keys(byGroup).forEach(function (k) {
            var list = byGroup[k];
            var first = list[0].item;
            var title = first.prompt || ('Guild: ' + String(first.guildId).substring(0, 8));
            var groupEl = document.createElement('section');
            groupEl.className = 'prompt-group' + (shared.groupCollapsedState[k] ? ' collapsed' : '');
            groupEl.setAttribute('data-group-key', k);
            groupEl.innerHTML =
                '<button class="prompt-group-header" type="button" data-group-key="' + shared.esc(k) + '">' +
                    '<span class="prompt-group-title" title="' + shared.esc(title) + '">' + shared.esc(title) + '</span>' +
                    '<span class="prompt-group-meta"><span>' + list.length + ' video</span><i class="bi ' + (shared.groupCollapsedState[k] ? 'bi-chevron-down' : 'bi-chevron-up') + '"></i></span>' +
                '</button>' +
                '<div class="prompt-group-body" id="prompt-group-body-' + shared.esc(k).replace(/[^a-zA-Z0-9_-]/g, '_') + '"></div>';
            grid2.appendChild(groupEl);
            var body = groupEl.querySelector('.prompt-group-body');
            list.forEach(function (entry) {
                if (entry.card) {
                    entry.card.style.display = '';
                    if (body) body.appendChild(entry.card);
                }
            });
        });
    }

    // Export UI funcs to GrokVideo so GrokVideoCache can also use them
    window.GrokVideo = {
        createCard: createCard,
        updateCard: updateCard,
        updateProg: updateProg,
        updateStats: updateStats,
        hydrateDoneCardMedia: hydrateDoneCardMedia,
        hydrateLocalPlayable: hydrateLocalPlayable,
        applyPromptGrouping: applyPromptGrouping
    };

    /* ═══════════════════════════════════════════════
       SEND BATCH / POLLING / GENERATE  
    ═══════════════════════════════════════════════ */
    function registerPendingCard(item) {
        if (!item || item.status !== 'generating') return;
        if (!window.GrokVideoCache) return; 
        window.GrokVideoCache.registerPendingCard(item);
    }
    function unregisterPendingCard(item) {
        if (!item || item.status !== 'generating') return;
        if (!window.GrokVideoCache) return; 
        window.GrokVideoCache.unregisterPendingCard(item);
    }

    function startPolling(guildId, expectedCount, onResult, onFinished) {
        shared.bindAsyncPushReceiver();
        if (shared.asyncPushReceiverBound) {
            shared.asyncVideoJobs[guildId] = {
                expectedCount: expectedCount,
                onResult: onResult,
                onFinished: onFinished
            };
            shared.pollJobs[guildId] = {
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
                delete shared.pollJobs[guildId];
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
                if(window.GrokVideoCache) {
                    var dict = window.GrokVideoCache.pickResultDictionary();
                    var res  = window.GrokVideoCache.resolveResultList(dict, guildId);
                    if (res && Array.isArray(res)) {
                        var newItems = window.GrokVideoCache.collectNewReadyResults(job, res);
                        if (newItems.length > 0) {
                            job.renderedCount += newItems.length;
                            onResult(newItems, job.renderedCount, expectedCount);
                        }
                        if (job.renderedCount >= expectedCount) {
                            clearInterval(job.interval);
                            delete shared.pollJobs[guildId];
                            updateStats();
                            if (typeof onFinished === 'function') onFinished(false);
                        }
                    }
                }
            } catch (_) {}
        }, 1000);

        shared.pollJobs[guildId] = job;
        updateStats();
    }

    function sendBatch(batch, onDone) {
        var guildId       = batch.guildId;
        var prompts       = batch.prompts;
        var sendCount     = batch.sendCount;
        var aspect        = batch.aspect;
        var length        = batch.length;
        var res           = batch.res;
        var totalExpected = prompts.length * sendCount;
        handledGuildIds[guildId] = true; 
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
                    cardId: shared.uid(),
                    requestId: shared.uid(),
                    prompt: prompt
                });
            }
        });

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
        if (sc) sc.value = '0'; 

        try { if (typeof hostSubmit === 'function') hostSubmit(); } catch (_) {}
        try { if (typeof hostStart === 'function') hostStart(); } catch (_) {}

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

        startPolling(guildId, totalExpected, function (newLinks) {
            newLinks.forEach(function (result) {
                var pending = window.GrokVideoCache ? window.GrokVideoCache.findPendingCardForResult(guildId, result) : null;
                if (pending) {
                    unregisterPendingCard(pending);
                    if (result.localPath) pending.localPath = result.localPath;
                    if (result.videoUrl) pending.videoUrl = result.videoUrl;
                    if (result.aspect) pending.aspect = shared.normalizeAspectValue(result.aspect) || pending.aspect;
                    if (result.length) pending.length = shared.normalizeLengthValue(result.length) || pending.length;
                    if (result.resolution) pending.res = shared.normalizeResolutionValue(result.resolution) || pending.res;
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

            var hasGenerating = false;
            for (var gi = 0; gi < state.videos.length; gi++) {
                var gitem = state.videos[gi];
                if (gitem && gitem.guildId === guildId && gitem.status === 'generating') {
                    hasGenerating = true;
                    break;
                }
            }
            if (!hasGenerating) {
                var job = shared.pollJobs[guildId];
                if (job && job.interval) {
                    clearInterval(job.interval);
                    delete shared.pollJobs[guildId];
                }
                updateStats();
                finishBatchOnce(false);
            }
        }, function (timedOut) {
            finishBatchOnce(timedOut);
        });
    }

    function runNextBatch() {
        var next = null;
        for (var i = 0; i < batchQueue.length; i++) {
            if (!batchQueue[i].done && !batchQueue[i].active) {
                next = batchQueue[i];
                break;
            }
        }

        if (!next) {
            shared.setBatchRunning(false);
            shared.showToast('success', 'Tất cả đợt hoàn thành!', batchQueue.length + ' đợt đã xử lý xong', 4000);
            shared.batchQueue.splice(0, shared.batchQueue.length); // clear
            renderBatchQueue();
            return;
        }

        next.active = true;
        renderBatchQueue();
        shared.showToast('info', 'Đợt tiếp theo', 'Đang gửi: ' + next.prompts.join(', ').substring(0, 50), 3000);

        sendBatch(next, function () {
            runNextBatch();
        });
    }

    window.generateVideo = function () {
        try {
            var raw       = (document.getElementById('promptInput').value || '').trim();
            var isMulti   = document.getElementById('multiPrompt').checked;
            var aspect    = document.getElementById('aspectRatio').value;
            var length    = parseInt(document.getElementById('videoLength').value, 10);
            var res       = document.getElementById('resolution').value;
            var sendCount = parseInt(document.getElementById('sendCount').value, 10);
            var batchSize = isMulti ? parseInt(document.getElementById('batchSize').value, 10) : 0;

            var allPrompts = isMulti
                ? sanitizePromptLines(raw.split('\n'))
                : (raw ? [raw] : []);

            if (!allPrompts.length) { shared.showError('Vui lòng nhập ít nhất một prompt!'); return; }

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

            if (isMulti && batchSize > 0 && allPrompts.length > batchSize) {
                var newBatches = [];
                for (var i = 0; i < allPrompts.length; i += batchSize) {
                    var chunk = allPrompts.slice(i, i + batchSize);
                    newBatches.push({
                        prompts:   chunk,
                        sendCount: sendCount,
                        aspect:    aspect,
                        length:    length,
                        res:       res,
                        guildId:   shared.uid(),
                        label:     chunk.join(', '),
                        active:    false,
                        done:      false
                    });
                }

                newBatches.forEach(function(b) { shared.batchQueue.push(b); });
                renderBatchQueue();

                shared.showToast('info', 'Xếp hàng ' + newBatches.length + ' đợt',
                    allPrompts.length + ' prompt, mỗi đợt ' + batchSize + ' × ' + sendCount + ' lần', 4000);

                if (!shared.getBatchRunning()) {
                    shared.setBatchRunning(true);
                    runNextBatch();
                }
            } else {
                var singleGuild = shared.uid();
                var totalExp    = allPrompts.length * sendCount;
                handledGuildIds[singleGuild] = true; 
                var requestItems = [];
                allPrompts.forEach(function (prompt) {
                    for (var j = 0; j < sendCount; j++) {
                        requestItems.push({
                            cardId: shared.uid(),
                            requestId: shared.uid(),
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
                if (sc) sc.value = '0'; 

                try { if (typeof hostSubmit === 'function') hostSubmit(); } catch (_) {}
                try { if (typeof hostStart === 'function') hostStart(); } catch (_) {}

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
                shared.showToast('info', 'Đã gửi yêu cầu', allPrompts.length + ' prompt × ' + sendCount + ' lần', 3000);

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
                        var pending = window.GrokVideoCache ? window.GrokVideoCache.findPendingCardForResult(singleGuild, result) : null;
                        if (pending) {
                            unregisterPendingCard(pending);
                            if (result.localPath) pending.localPath = result.localPath;
                            if (result.videoUrl) pending.videoUrl = result.videoUrl;
                            if (result.aspect) pending.aspect = shared.normalizeAspectValue(result.aspect) || pending.aspect;
                            if (result.length) pending.length = shared.normalizeLengthValue(result.length) || pending.length;
                            if (result.resolution) pending.res = shared.normalizeResolutionValue(result.resolution) || pending.res;
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

            setTimeout(function () {
                if (btn)      btn.disabled = false;
                if (btnLabel) btnLabel.textContent = 'TẠO VIDEO';
                if (btnIcon)  { btnIcon.style.cssText = ''; btnIcon.className = 'bi bi-lightning-fill btn-icon'; }
            }, 800);

        } catch (err) {
            shared.showError('Lỗi: ' + (err && err.message ? err.message : String(err)));
            var b2 = document.getElementById('generateBtn');
            var bl = document.getElementById('btnLabel');
            var bi = document.getElementById('btnIcon');
            if (b2) b2.disabled = false;
            if (bl) bl.textContent = 'TẠO VIDEO';
            if (bi) { bi.style.cssText = ''; bi.className = 'bi bi-lightning-fill btn-icon'; }
        }
    };

    /* ═══════════════════════════════════════════════
       MODAL VIEWERS
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

        var dlHref = item.localUrl || (item.localPath ? shared.toFileUri(item.localPath) : item.videoUrl);
        var fallbackSrc = shared.toPlayableSrc(item.localUrl || item.videoUrl || dlHref);

        if (mv) mv.pause();

        function applyModalVideoSrc(resolvedSrc) {
            if (!mv) return;
            var src0 = resolvedSrc || fallbackSrc;
            if (shared.isInternalPlayableRef(String(src0 || '').trim())) src0 = shared.withCacheBust(String(src0).trim());
            var src = shared.toPlayableSrc(src0);
            applyVideoElementSource(mv, src, 'modal');
            var pp = mv.play && mv.play();
            if (pp && typeof pp.catch === 'function') pp.catch(function () {});
        }

        if (item.localPath) {
            shared.resolveLocalPlayableUrl(item.localPath, fallbackSrc, applyModalVideoSrc);
        } else if (shared.isInternalPlayableRef(fallbackSrc)) {
            shared.resolvePlayableRefUrl(fallbackSrc, function (u) {
                applyModalVideoSrc(u || fallbackSrc);
            });
        } else if ((item.videoUrl || '').trim() && !/[\\/:]/.test((item.videoUrl || '').trim()) && /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test((item.videoUrl || '').trim())) {
            shared.resolvePlayableRefUrl('https://localfiles.local/' + encodeURIComponent((item.videoUrl || '').trim()), function (u) {
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

        var fallbackSrc = shared.toPlayableSrc(item.localUrl || item.videoUrl || (item.localPath ? shared.toFileUri(item.localPath) : ''));
        function applyFrame(src) {
            var s = src || fallbackSrc;
            if (shared.isInternalPlayableRef(String(s || '').trim())) s = shared.withCacheBust(String(s).trim());
            s = shared.toPlayableSrc(s);
            pf.srcdoc = '';
            pf.src = s || 'about:blank';
        }

        if (item.localPath) {
            shared.resolveLocalPlayableUrl(item.localPath, fallbackSrc, function (u) { applyFrame(u || fallbackSrc); });
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

})();
