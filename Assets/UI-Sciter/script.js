// JavaScript Logic cho Sciter UI Test - Modern File URL Formatting & Interop
(function () {
    var imgInput = document.getElementById('imgPathInput');
    var videoInput = document.getElementById('videoPathInput');
    var imgPreview = document.getElementById('imgPreview');
    var videoPreview = document.getElementById('videoPreview');
    var imgUrlHint = document.getElementById('imgUrlHint');
    var videoUrlHint = document.getElementById('videoUrlHint');
    var statusBox = document.getElementById('statusBox');

    function setStatus(msg) {
        if (statusBox) statusBox.textContent = '[' + new Date().toLocaleTimeString() + '] ' + msg;
    }

    // 🔴 Helper chuẩn hóa Windows Path (kể cả khoảng trắng & tiếng Việt có dấu) sang file:///
    function toFileUrl(winPath) {
        if (!winPath) return '';
        var p = winPath.trim().replace(/\\/g, '/');
        if (!p.startsWith('file://')) {
            p = 'file:///' + p.replace(/^\/+/, '');
        }
        // Chuẩn hóa khoảng trắng (%20) & ký tự đặc biệt/tiếng Việt
        try {
            return encodeURI(p);
        } catch (_) {
            return p;
        }
    }

    // Load Ảnh
    function doLoadImage() {
        var rawPath = imgInput ? imgInput.value : '';
        if (!rawPath) {
            setStatus('⚠ Vui lòng nhập đường dẫn ảnh.');
            return;
        }
        var formattedUrl = toFileUrl(rawPath);
        if (imgUrlHint) imgUrlHint.textContent = 'URL Format: ' + formattedUrl;
        if (imgPreview) {
            imgPreview.src = formattedUrl;
            setStatus('Đã nạp Ảnh: ' + formattedUrl);
        }
    }

    // Load Video
    function doLoadVideo() {
        var rawPath = videoInput ? videoInput.value : '';
        if (!rawPath) {
            setStatus('⚠ Vui lòng nhập đường dẫn video.');
            return;
        }
        var formattedUrl = toFileUrl(rawPath);
        if (videoUrlHint) videoUrlHint.textContent = 'URL Format: ' + formattedUrl;
        if (videoPreview) {
            videoPreview.src = formattedUrl;
            try { videoPreview.load(); } catch (_) { }
            setStatus('Đã nạp Video: ' + formattedUrl);
        }
    }

    // Event Bindings cho Tải Ảnh / Video
    var btnLoadImg = document.getElementById('btnLoadImg');
    if (btnLoadImg) btnLoadImg.onclick = doLoadImage;

    var btnLoadVideo = document.getElementById('btnLoadVideo');
    if (btnLoadVideo) btnLoadVideo.onclick = doLoadVideo;

    // Chọn File từ đĩa cứng qua host API
    var btnPickImg = document.getElementById('btnPickImg');
    if (btnPickImg) {
        btnPickImg.onclick = function () {
            var reqId = 'img_pick_' + Date.now();
            if (typeof hostPickImages === 'function') {
                hostPickImages(reqId);
                setStatus('Đang mở hộp thoại chọn file ảnh...');
            } else {
                setStatus('⚠ Nút Chọn File cần hàm hostPickImages từ C# Host.');
            }
        };
    }

    var btnPickVideo = document.getElementById('btnPickVideo');
    if (btnPickVideo) {
        btnPickVideo.onclick = function () {
            var reqId = 'vid_pick_' + Date.now();
            if (typeof hostPickImages === 'function') {
                hostPickImages(reqId);
                setStatus('Đang mở hộp thoại chọn file video...');
            } else {
                setStatus('⚠ Nút Chọn File cần hàm hostPickImages từ C# Host.');
            }
        };
    }

    // Nhận Callback từ host (nếu có chọn file từ Host)
    if (typeof window.addEventListener === 'function') {
        window.addEventListener('hostImagesPicked', function (ev) {
            var d = (ev && ev.detail) || {};
            if (d.ok && d.files && d.files.length > 0) {
                var chosenFile = d.files[0];
                if (imgInput) imgInput.value = chosenFile;
                doLoadImage();
                setStatus('Đã chọn file từ đĩa: ' + chosenFile);
            }
        });
    }

    // Action Control Buttons
    var btnSubmit = document.getElementById('btnSubmit');
    if (btnSubmit) {
        btnSubmit.onclick = function () {
            if (typeof sciterUpdate === 'function') {
                sciterUpdate();
                setStatus('Đã gọi sciterUpdate() -> Cập nhật Output keys');
            } else if (typeof hostSubmit === 'function') {
                hostSubmit();
                setStatus('Đã gọi hostSubmit()');
            }
        };
    }

    var btnRunSingle = document.getElementById('btnRunSingle');
    if (btnRunSingle) {
        btnRunSingle.onclick = function () {
            if (typeof sciterRunSingleNode === 'function') {
                sciterRunSingleNode();
                setStatus('Đã gọi sciterRunSingleNode() -> Chạy riêng node');
            }
        };
    }

    var btnStartWorkflow = document.getElementById('btnStartWorkflow');
    if (btnStartWorkflow) {
        btnStartWorkflow.onclick = function () {
            if (typeof hostStart === 'function') {
                hostStart();
                setStatus('Đã gọi hostStart() -> Bắt đầu Workflow');
            }
        };
    }

    var btnClose = document.getElementById('btnClose');
    if (btnClose) {
        btnClose.onclick = function () {
            if (typeof sciterSubmitAndClose === 'function') {
                sciterSubmitAndClose();
            } else if (typeof hostSubmitAndClose === 'function') {
                hostSubmitAndClose();
            }
        };
    }

    // Auto-load sẵn khi trang vừa hiển thị
    setTimeout(function () {
        doLoadImage();
        doLoadVideo();
    }, 150);

    setStatus('Sciter JS Interop Engine Ready');
})();
