(function () {
    'use strict';

    var shared = window.GrokShared;
    if (!shared || !shared.imageState) {
        console.error('grok.shared.js or grok.image-upload.js is missing');
        return;
    }

    var imageState = shared.imageState;

    var imageCardSizeMode = 'md';
    var fitContainImage = false;
    var fitWidthImage = false;

    function normPathForImage(p) {
        return String(p || '').replace(/\\/g, '/').trim().toLowerCase();
    }

    function pickImageLocalPath(linkImage) {
        var s = String(linkImage || '').trim();
        if (!s) return '';
        if (shared.isAbsoluteWinPath(s)) return s.replace(/\//g, '\\');
        if (/^file:/i.test(s)) {
            var p = shared.fileUriToLocalPath(s);
            return p || '';
        }
        return '';
    }

    function pickUploadedImageDisplayUrl(img) {
        var r = (img.resolvedImageUrl || '').trim();
        if (r) return r;
        var lk = String(img.linkImage || '').trim();
        if (!lk) return '';
        if (shared.isAbsoluteWinPath(lk) || /^file:/i.test(lk)) return '';
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
        if (imageState.hydratingImageGuids[guid]) return;
        imageState.hydratingImageGuids[guid] = true;
        shared.resolveLocalPlayableUrl(lp, '', function (resolved) {
            delete imageState.hydratingImageGuids[guid];
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
        Object.keys(imageState.imageItemsDict).forEach(function (k) {
            hydrateUploadedImageSrc(imageState.imageItemsDict[k]);
        });
    }

    function computeImageDictEntry(item, existing) {
        existing = existing || {};
        var iguid = String(item.imageGuid || item.id || '');
        if (!iguid) return null;
        var normalizedStatus = shared.normalizeStatusToState(item.status, item.isSuccess);
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

    function updateUploadedCount() {
        var el = document.getElementById('imgUploadedCount');
        if (el) el.textContent = Object.keys(imageState.imageItemsDict).length + ' ảnh';
    }

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
            var srcAttr = disp ? shared.esc(disp) : shared.IMG_UPLOAD_PLACEHOLDER;
            var g = String(img.imageGuid || '').replace(/'/g, '');
            mediaHtml =
                '<div class="img-uploaded-media" onclick="imgOpenUploadedImageByGuid(\'' + g + '\')">' +
                    '<img src="' + srcAttr + '" alt="' + shared.esc(img.fileName || '') + '" loading="lazy">' +
                    '<div class="img-card-zoom-overlay"><i class="bi bi-zoom-in"></i></div>' +
                '</div>';
        } else if (st === 'failed') {
            mediaHtml =
                '<div class="img-uploaded-media img-media-failed">' +
                    '<div class="img-status-overlay">' +
                        '<i class="bi bi-exclamation-triangle-fill" style="font-size:26px;color:#f87171;margin-bottom:6px"></i>' +
                        '<div class="img-overlay-label">Upload thất bại</div>' +
                    '</div>' +
                '</div>';
        } else {
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
            '<div class="img-uploaded-name" title="' + shared.esc(img.fileName || 'Không tên') + '">' + shared.esc(img.fileName || 'Không tên') + '</div>';

        if (st === 'failed' && img.error) {
            bodyHtml += '<div class="img-uploaded-error" title="' + shared.esc(img.error) + '">' + shared.esc((img.error || '').substring(0, 50)) + '</div>';
        } else {
            var guidLabel = (img.uploadGuid || '').substring(0, 8);
            if (guidLabel) bodyHtml += '<div class="img-uploaded-meta">Batch: ' + shared.esc(guidLabel) + '…</div>';
        }
        bodyHtml += '</div>';

        card.innerHTML = mediaHtml + badgeHtml + bodyHtml;
        return card;
    }

    function renderUploadedGrid() {
        var grid = document.getElementById('imgUploadedGrid');
        if (!grid) return;
        updateUploadedCount();

        var keys = Object.keys(imageState.imageItemsDict);
        if (!keys.length) {
            grid.innerHTML =
                '<div class="img-empty-uploaded">' +
                    '<i class="bi bi-images" style="font-size:40px;color:var(--accent);opacity:0.35;margin-bottom:4px"></i>' +
                    '<div style="font-size:13px;font-weight:600;color:var(--clr2)">Chưa có ảnh nào đã upload</div>' +
                    '<div style="font-size:11px;color:var(--clr3);margin-top:4px">Upload ảnh từ cột bên trái để bắt đầu</div>' +
                '</div>';
            return;
        }

        var items = keys.map(function (k) { return imageState.imageItemsDict[k]; });
        items.sort(function (a, b) {
            var ords = { processing: 0, done: 1, failed: 2 };
            var so = (ords[a.status] || 1) - (ords[b.status] || 1);
            if (so !== 0) return so;
            return (b.addedAt || 0) - (a.addedAt || 0);
        });

        var existingByGuid = {};
        var existingNodes = grid.querySelectorAll('.img-uploaded-card');
        for (var i = 0; i < existingNodes.length; i++) {
            var g = existingNodes[i].getAttribute('data-image-guid');
            if (g) existingByGuid[g] = existingNodes[i];
        }

        for (var exG in existingByGuid) {
            if (!imageState.imageItemsDict[exG]) existingByGuid[exG].remove();
        }

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

        var allNow = grid.querySelectorAll('.img-uploaded-card[data-image-guid]');
        for (var j = 0; j < allNow.length; j++) {
            var gg = allNow[j].getAttribute('data-image-guid');
            if (!gg || seen[gg]) continue;
            allNow[j].remove();
        }
    }

    function processDataImagesPayload(dataImages, options) {
        var items = normalizeDataImagesToItems(dataImages);
        if (!items || !items.length) return { changed: false, completedRows: [], failedRows: [] };
        return applyIncomingDataImages(items, options || { onlyTracked: false });
    }

    function normalizeDataImagesToItems(dataImages) {
        if (dataImages == null) return null;
        var out = [];

        function pushOneObjectRecord(obj) {
            if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return;
            if (obj.imageGuid || obj.id) out.push(obj);
            else {
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
            
            // Re-use parseLooseDataImageRecord if it exists, but I didn't port it to shared explicitly
            // Wait, I did. No, wait, I didn't put it in shared? I'll re-implement inline.
            if(window.GrokVideoCache && window.GrokVideoCache.parseVideoResultFromEntry) {
                var parsed = window.GrokVideoCache.parseVideoResultFromEntry(t);
                // Not ideal, let's just parse it manually
            }
            
            var t_esc = shared.decodeUnicodeEscapes(t);
            var imageGuid = shared.normalizeLooseJsonToken(shared.extractJsonLikeRawToken(t_esc, 'imageGuid'));
            if(imageGuid) {
                var linkRaw = shared.extractJsonLikeField(t_esc, 'linkImage');
                if (!linkRaw) linkRaw = shared.extractJsonLikeRawToken(t_esc, 'linkImage');
                var status = shared.normalizeLooseJsonToken(shared.extractJsonLikeRawToken(t_esc, 'status') || shared.extractJsonLikeField(t_esc, 'status'));
                var ok = shared.extractJsonLikeBoolField(t_esc, 'isSuccess');
                var normalizedStatus = shared.normalizeStatusToState(status, ok);
                out.push({
                    imageGuid:      imageGuid,
                    uploadGuid:     shared.normalizeLooseJsonToken(shared.extractJsonLikeRawToken(t_esc, 'uploadGuid')),
                    fileName:       shared.normalizeLooseJsonToken(shared.extractJsonLikeRawToken(t_esc, 'fileName')),
                    status:         normalizedStatus,
                    isSuccess:      normalizedStatus === 'done',
                    linkImage:      shared.normalizeLooseJsonToken(linkRaw),
                    fileMetadataId: shared.normalizeLooseJsonToken(shared.extractJsonLikeRawToken(t_esc, 'fileMetadataId')),
                    error:          shared.normalizeLooseJsonToken(shared.extractJsonLikeField(t_esc, 'error') || shared.extractJsonLikeRawToken(t_esc, 'error'))
                });
            }
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

            var tracked = imageState.imageGuidTracker[iguid];
            if (tracked && tracked.terminal) return;
            if (!tracked && opts.onlyTracked) return;
            if (tracked && tracked.uploadGuid) {
                var incomingUploadGuid = String(item.uploadGuid || '');
                if (incomingUploadGuid && incomingUploadGuid !== String(tracked.uploadGuid)) return;
            }

            var normalizedItem = {};
            Object.keys(item).forEach(function (k) { normalizedItem[k] = item[k]; });
            normalizedItem.status = shared.normalizeStatusToState(item.status, item.isSuccess);
            if (normalizedItem.status === 'failed' && !String(normalizedItem.error || '').trim()) {
                normalizedItem.error = 'Backend trả trạng thái lỗi (status != 200)';
            }

            var previous = imageState.imageItemsDict[iguid] || {};
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

            imageState.imageItemsDict[iguid] = row;
            if (beforeSig !== afterSig) changed = true;

            var justCompleted = completeTrackedGuidIfNeeded(row);
            if (justCompleted) {
                if (row.status === 'done' && row.isSuccess) completedRows.push(row);
                else if (row.status === 'failed') failedRows.push(row);
            }
        });

        return { changed: changed, completedRows: completedRows, failedRows: failedRows };
    }

    function completeTrackedGuidIfNeeded(row) {
        if (!row || !row.imageGuid || !isTerminalImageStatus(row.status)) return false;
        var guid = String(row.imageGuid);
        var tracked = imageState.imageGuidTracker[guid];
        if (!tracked || tracked.terminal) return false;

        tracked.terminal = true;
        var uploadGuid = tracked.uploadGuid || row.uploadGuid || '';
        var batch = imageState.uploadBatchTracker[uploadGuid];
        if (batch && !batch.doneByGuid[guid]) {
            batch.doneByGuid[guid] = true;
            batch.terminalCount++;
            if (!batch.completed && batch.expectedCount > 0 && batch.terminalCount >= batch.expectedCount) {
                batch.completed = true;
                shared.showToast(
                    'success',
                    'Upload batch hoàn tất',
                    'Đã nhận đủ ' + batch.terminalCount + '/' + batch.expectedCount + ' ảnh từ dataImages',
                    2600
                );
            }
        }
        return true;
    }

    function showImageUploadToasts(applied) {
        if (!applied) return;
        applied.completedRows.forEach(function (item) {
            shared.showToast('success', 'Ảnh đã upload', (item.fileName || '') + ' → done', 2500);
        });
        applied.failedRows.forEach(function (item) {
            var errMsg = (item.error && String(item.error).trim()) ? item.error : (item.fileName || '');
            shared.showToast('error', 'Upload thất bại', errMsg, 3500);
        });
    }

    function getCurrentDataImagesSnapshot() {
        var dataImages = null;
        try {
            if (window.__ac && window.__ac.live) dataImages = window.__ac.live.dataImages;
            if (!dataImages && window.__ac && window.__ac.datas) dataImages = window.__ac.datas.dataImages;
        } catch (_) {}
        return dataImages;
    }

    function timeoutUploadBatch(uploadGuid, batch) {
        if (!uploadGuid || !batch || batch.completed) return;
        batch.completed = true;
        batch.timedOut = true;
        var remain = 0;
        Object.keys(imageState.imageGuidTracker).forEach(function (g) {
            var tracked = imageState.imageGuidTracker[g];
            if (!tracked || tracked.uploadGuid !== uploadGuid || tracked.terminal) return;
            tracked.terminal = true;
            remain++;
            var previous = imageState.imageItemsDict[g] || {};
            imageState.imageItemsDict[g] = {
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
            shared.showToast('error', 'Batch timeout', 'UploadGuid ' + uploadGuid.substring(0, 8) + '… quá 180s', 3800);
        }
    }

    function ensureImageBatchPolling() {
        if (shared.asyncPushReceiverBound) return;
        if (imageState.imageBatchPollTimer) return;
        var IMAGE_BATCH_POLL_INTERVAL_MS = 1000;
        imageState.imageBatchPollTimer = setInterval(function () {
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

            Object.keys(imageState.uploadBatchTracker).forEach(function (ug) {
                var batch = imageState.uploadBatchTracker[ug];
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
                clearInterval(imageState.imageBatchPollTimer);
                imageState.imageBatchPollTimer = null;
            }
        }, IMAGE_BATCH_POLL_INTERVAL_MS);
    }

    function trackUploadBatch(uploadGuid, imageGuids) {
        var gids = Array.isArray(imageGuids) ? imageGuids.filter(Boolean) : [];
        var startedAt = Date.now();
        var IMAGE_BATCH_TIMEOUT_MS = 180000;
        imageState.uploadBatchTracker[uploadGuid] = {
            expectedCount: gids.length,
            terminalCount: 0,
            doneByGuid: {},
            completed: false,
            timedOut: false,
            startedAt: startedAt,
            timeoutAt: startedAt + IMAGE_BATCH_TIMEOUT_MS
        };
        gids.forEach(function (g) {
            imageState.imageGuidTracker[g] = { uploadGuid: uploadGuid, terminal: false };
        });
        ensureImageBatchPolling();
    }

    /* ════════════════════════════════════════
       UI PREFS & TOGGLES
    ════════════════════════════════════════ */
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
            var p = shared.loadUiPrefs();
            p.imageCardSizeMode = m;
            shared.saveUiPrefs(p);
        } catch (_) {}
    }

    function applyImageFitContain(enabled) {
        fitContainImage = !!enabled;
        var app = document.getElementById('app');
        if (app) app.classList.toggle('fit-image-contain', fitContainImage);
        var cb = document.getElementById('imgFitContainCheck');
        if (cb) cb.checked = fitContainImage;
        try {
            var p = shared.loadUiPrefs();
            p.fitContainImage = fitContainImage;
            shared.saveUiPrefs(p);
        } catch (_) {}
    }

    function applyImageFitWidth(enabled) {
        fitWidthImage = !!enabled;
        var app = document.getElementById('app');
        if (app) app.classList.toggle('fit-image-width', fitWidthImage);
        var cb = document.getElementById('imgFitWidthCheck');
        if (cb) cb.checked = fitWidthImage;
        try {
            var p = shared.loadUiPrefs();
            p.fitWidthImage = fitWidthImage;
            shared.saveUiPrefs(p);
        } catch (_) {}
    }

    (function initImageViewControls() {
        var prefs = shared.loadUiPrefs();
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

    // Expose lightbox helper for dynamically created cards
    window.imgOpenUploadedImageByGuid = function (guid) {
        var row = imageState.imageItemsDict[guid];
        if (!row) return;
        var u = pickUploadedImageDisplayUrl(row);
        if (u) {
            if(window.imgOpenUploadedLightbox) window.imgOpenUploadedLightbox(u);
            return;
        }
        var localPath = String(row.imageLocalPath || pickImageLocalPath(row.linkImage) || '').trim();
        if (!localPath) return;
        shared.resolveLocalPlayableUrl(localPath, '', function (resolved) {
            if (!resolved) return;
            row.resolvedImageUrl = resolved;
            if(window.imgOpenUploadedLightbox) window.imgOpenUploadedLightbox(resolved);
        });
    };

    /* ════════════════════════════════════════
       @MENTION AUTOCOMPLETE 
    ════════════════════════════════════════ */
    var mentionDropdown = null;
    var mentionQuery    = '';
    var mentionStart    = -1;
    var mentionHiIdx    = 0;

    function getDoneImages() {
        return Object.values(imageState.imageItemsDict).filter(function (img) {
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
                    ? '<img src="' + shared.esc(thumb) + '" alt="" loading="lazy">'
                    : '<div class="mention-item-thumb-ph" style="width:22px;height:22px;border-radius:50%"><i class="bi bi-image" style="font-size:10px;color:var(--clr3)"></i></div>'
                ) +
                '<span class="pm-label" title="' + shared.esc(label) + '">' + shared.esc(label) + '</span>';
            box.appendChild(chip);
            if (!thumb && localPath) {
                shared.resolveLocalPlayableUrl(localPath, '', function (resolved) {
                    if (!resolved || /^file:/i.test(resolved)) return;
                    img.resolvedImageUrl = resolved;
                    var ph = chip.querySelector('.mention-item-thumb-ph');
                    if (!ph) return;
                    ph.outerHTML = '<img src="' + shared.esc(resolved) + '" alt="" loading="lazy">';
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
            var src = thumb ? shared.esc(thumb) : '';
            var localPath = String(img.imageLocalPath || pickImageLocalPath(img.linkImage) || '').trim();
            item.innerHTML =
                (src
                    ? '<img class="mention-item-thumb" src="' + src + '" alt="" loading="lazy">'
                    : '<div class="mention-item-thumb-ph"><i class="bi bi-image" style="font-size:12px;color:var(--clr3)"></i></div>'
                ) +
                '<span class="mention-item-name">@' + shared.esc(img.fileName || '') + '</span>';
            if (!src && localPath) {
                shared.resolveLocalPlayableUrl(localPath, '', function (resolved) {
                    if (!resolved || /^file:/i.test(resolved)) return;
                    img.resolvedImageUrl = resolved;
                    var ph = item.querySelector('.mention-item-thumb-ph');
                    if (!ph) return;
                    ph.outerHTML = '<img class="mention-item-thumb" src="' + shared.esc(resolved) + '" alt="" loading="lazy">';
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
        try { var p = shared.loadUiPrefs(); p.promptInputText = promptEl.value; shared.saveUiPrefs(p); } catch (_) {}
        try { if(window.updateBatchInfo) window.updateBatchInfo(); } catch (_) {}
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

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeMentionDropdown();
        }
    });

    /* ════════════════════════════════════════
       EXPOSED METHODS FOR BRIDGE
    ════════════════════════════════════════ */
    shared.registerDataImagesHook(function (dataImages) {
        if (!dataImages) return;
        var applied = processDataImagesPayload(dataImages, { onlyTracked: false });
        if (applied.changed) {
            renderUploadedGrid();
            hydrateAllUploadedImagesNeedingResolve();
            syncPromptMentionPreview();
        }
        showImageUploadToasts(applied);
    });

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
            shared.showToast('info', 'Làm mới ảnh', 'Đã quét lại dataImages hiện có', 1800);
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
        imageState.imageItemsDict = {};
        imageState.uploadBatchTracker = {};
        imageState.imageGuidTracker = {};
        if (imageState.imageBatchPollTimer) {
            clearInterval(imageState.imageBatchPollTimer);
            imageState.imageBatchPollTimer = null;
        }
        renderUploadedGrid();
        syncPromptMentionPreview();
        shared.showToast('info', 'Đã dọn ảnh', 'Đã xóa danh sách ảnh đã upload', 2000);
    };

    window.grokSyncPromptMentionPreview = syncPromptMentionPreview;

    (function doEagerInitialImageLoad() {
        function attempt() {
            try {
                var dataImages = getCurrentDataImagesSnapshot();
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

    renderUploadedGrid();
    if(window.GrokImageUpload && window.GrokImageUpload.updatePendingCount) window.GrokImageUpload.updatePendingCount();
    syncPromptMentionPreview();

    window.GrokImageCache = {
        renderUploadedGrid: renderUploadedGrid,
        trackUploadBatch: trackUploadBatch
    };

})();
