// JavaScript Logic cho Sciter UI Test - Local File Media & Interop
(function () {
    var imgInput = document.getElementById('imgPathInput');
    var videoInput = document.getElementById('videoPathInput');
    var imgPreview = document.getElementById('imgPreview');
    var videoPreview = document.getElementById('videoPreview');
    var statusBox = document.getElementById('statusBox');

    function setStatus(msg) {
        if (statusBox) statusBox.textContent = '[' + new Date().toLocaleTimeString() + '] ' + msg;
    }

    // Helper format Win32 path sang file:///
    function toFileUrl(winPath) {
        if (!winPath) return '';
        var p = winPath.trim().replace(/\\/g, '/');
        if (!p.startsWith('file://')) {
            p = 'file:///' + p.replace(/^\/+/, '');
        }
        return p;
    }

    // Load ảnh local
    var btnLoadImg = document.getElementById('btnLoadImg');
    if (btnLoadImg) {
        btnLoadImg.onclick = function () {
            var raw = imgInput ? imgInput.value : '';
            var url = toFileUrl(raw);
            if (imgPreview) {
                imgPreview.src = url;
                setStatus('Đã gán URL Ảnh: ' + url);
            }
        };
    }

    // Load video local
    var btnLoadVideo = document.getElementById('btnLoadVideo');
    if (btnLoadVideo) {
        btnLoadVideo.onclick = function () {
            var raw = videoInput ? videoInput.value : '';
            var url = toFileUrl(raw);
            if (videoPreview) {
                videoPreview.src = url;
                setStatus('Đã gán URL Video: ' + url);
            }
        };
    }

    // Interop Actions
    var btnSubmit = document.getElementById('btnSubmit');
    if (btnSubmit) {
        btnSubmit.onclick = function () {
            if (typeof sciterUpdate === 'function') {
                sciterUpdate();
                setStatus('Đã gọi sciterUpdate()');
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
                setStatus('Đã gọi sciterRunSingleNode()');
            }
        };
    }

    var btnStartWorkflow = document.getElementById('btnStartWorkflow');
    if (btnStartWorkflow) {
        btnStartWorkflow.onclick = function () {
            if (typeof hostStart === 'function') {
                hostStart();
                setStatus('Đã gọi hostStart()');
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

    // Auto-load ảnh/video nếu có giá trị sẵn
    setTimeout(function () {
        if (btnLoadImg) btnLoadImg.click();
    }, 200);

    setStatus('JS Sciter Loaded thành công');
})();
