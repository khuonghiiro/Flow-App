(function () {
    'use strict';

    var shared = window.GrokShared;
    if (!shared) {
        console.error('grok.shared.js is missing');
        return;
    }

    // Shared state for image logic between upload and cache
    var imageState = {
        pendingImages: [],
        imageItemsDict: {},
        hydratingImageGuids: {},
        uploadBatchTracker: {},
        imageGuidTracker: {},
        imageBatchPollTimer: null
    };
    shared.imageState = imageState;
    shared.IMG_UPLOAD_PLACEHOLDER = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

    var pendingImages = imageState.pendingImages;

    // Badge helpers
    function updatePendingCount() {
        var el = document.getElementById('pendingCount');
        if (el) el.textContent = pendingImages.length + ' ảnh';
        var clr = document.getElementById('imgClearBtn');
        var vidN = shared.state.videos && shared.state.videos.length ? shared.state.videos.length : 0;
        if (clr) clr.style.display = (pendingImages.length > 0 || vidN > 0) ? '' : 'none';
    }

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
                    '<img src="' + shared.esc(img.base64) + '" alt="' + shared.esc(img.filename) + '" loading="lazy">' +
                    '<div class="img-card-zoom-overlay"><i class="bi bi-zoom-in"></i></div>' +
                '</div>' +
                '<button class="img-card-remove" onclick="imgRemovePending(\'' + img.id + '\')" title="Xóa ảnh">' +
                    '<i class="bi bi-x-lg"></i>' +
                '</button>' +
                '<div class="img-card-name-wrap">' +
                    '<input class="img-card-name" type="text" value="' + shared.esc(img.filename) + '" ' +
                           'id="imgname-' + img.id + '" placeholder="Tên ảnh..." ' +
                           'onchange="imgUpdateName(\'' + img.id + '\', this.value)">' +
                '</div>';
            grid.appendChild(card);
        });
    }

    window.imgRemovePending = function (imgId) {
        imageState.pendingImages = imageState.pendingImages.filter(function (i) { return i.id !== imgId; });
        pendingImages = imageState.pendingImages;
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
            var hadVideos = shared.state.videos && shared.state.videos.length > 0;
            if (!hadPending && !hadVideos) return;
            shared.setLoadVideoDatasPollPaused(true);
            try {
                if (window.GrokVideoCache) {
                    window.GrokVideoCache.stopAllLoadVideoDatasPolling();
                    window.GrokVideoCache.resetLoadCacheVideosBtnUi();
                    window.GrokVideoCache.clearVideoGalleryStateAndDom();
                }
                shared.setLatestLoadVideoDatasDict(null);
                
                imageState.pendingImages = [];
                pendingImages = imageState.pendingImages;
                renderPendingGrid();
            } finally {
                setTimeout(function () { shared.setLoadVideoDatasPollPaused(false); }, 0);
            }
            shared.showToast('info', 'Đã xóa', 'Đã xóa ảnh chờ (nếu có), toàn bộ video trên lưới và dừng poll loadVideoDatas.', 2600);
        });
    }

    // --- Lightbox logic for pending ---
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
    
    // will be augmented in cache module
    window.imgOpenUploadedLightbox = function (src) { openLightboxSrc(src); };

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
            var name = String(it.name || '').trim() || ('image_' + shared.uid() + '.png');
            var fullPath = String(it.path || '').trim();
            pendingImages.push({
                id: shared.uid(),
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
            shared.showToast('info', 'Đã thêm ' + added + ' ảnh', 'Đã lấy đường dẫn theo file từ host', 2600);
        }
    }

    function tryPickImagesFromHost() {
        if (typeof window.acPickImageFiles !== 'function') return false;
        var reqId = 'img_pick_' + shared.uid();
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
                    id: shared.uid(),
                    base64: ev.target.result,
                    filename: file.name,
                    originalName: file.name,
                    size: file.size,
                    linkPathImage: resolveImagePath(file, idx)
                });
                done++;
                if (done === arr.length) {
                    renderPendingGrid();
                    shared.showToast('info', 'Đã thêm ' + arr.length + ' ảnh', 'Nhấn Upload để gửi lên', 2500);
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
                if (v && /^[a-zA-Z]:[\\/]/.test(v) && !/^[a-zA-Z]:\\fakepath\\/i.test(v)) hintPaths.push(v);
            } catch (_) {}
            processFiles(e.target.files, { pathHints: hintPaths });
            e.target.value = '';
        });
    }

    var dropZone = document.getElementById('imgDropZone');
    if (dropZone) {
        dropZone.addEventListener('click', function () {
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
    ════════════════════════════════════════ */
    var uploadBtn = document.getElementById('imgUploadBtn');
    if (uploadBtn) {
        uploadBtn.addEventListener('click', function () {
            if (!pendingImages.length) {
                shared.showToast('warn', 'Chưa có ảnh', 'Vui lòng chọn ít nhất 1 ảnh', 3000);
                return;
            }
            uploadBtn.disabled = true;
            uploadBtn.innerHTML =
                '<span style="display:inline-block;width:14px;height:14px;border:2px solid rgba(255,255,255,0.3);' +
                'border-top-color:#fff;border-radius:50%;animation:spin-btn 0.7s linear infinite;margin-right:6px;vertical-align:middle"></span>' +
                'Đang upload...';

            var uploadGuid = shared.uid();
            var now = Date.now();

            var imageList = pendingImages.map(function (img) {
                var imageGuid = shared.uid();
                imageState.imageItemsDict[imageGuid] = {
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
            
            if (window.GrokImageCache) {
                window.GrokImageCache.trackUploadBatch(uploadGuid, imageList.map(function (i) { return i.imageGuid; }));
                window.GrokImageCache.renderUploadedGrid();
            }

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
            if (scEl) scEl.value = '1';

            try { if (typeof acSubmit        === 'function') acSubmit(); }        catch (_) {}
            try { if (typeof acStartWorkflow === 'function') acStartWorkflow(); } catch (_) {}

            shared.showToast('success',
                'Đã gửi ' + imageList.length + ' ảnh',
                'Batch GUID: ' + uploadGuid.substring(0, 8) + '...', 3000);

            imageState.pendingImages = [];
            pendingImages = imageState.pendingImages;
            renderPendingGrid();

            setTimeout(function () {
                uploadBtn.disabled = false;
                uploadBtn.innerHTML = '<i class="bi bi-cloud-upload-fill"></i> Upload Ảnh';
            }, 1500);
        });
    }

    // Export needed func
    window.GrokImageUpload = {
        updatePendingCount: updatePendingCount
    };

})();
