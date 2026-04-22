(function () {
    'use strict';

    var shared = window.GrokShared;
    if (!shared) {
        console.error('grok.shared.js is missing');
        return;
    }

    // Bind Prefs 
    function bindPrefsPersistence() {
        var prefs = shared.loadUiPrefs();
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
                var p = shared.loadUiPrefs();
                p[t.id] = t.value;
                shared.saveUiPrefs(p);
            });
        }

        var multi = document.getElementById('multiPrompt');
        if (multi) {
            if (prefs.multiPrompt != null) {
                multi.checked = !!prefs.multiPrompt;
            }
            multi.addEventListener('change', function () {
                var p = shared.loadUiPrefs();
                p.multiPrompt = !!multi.checked;
                shared.saveUiPrefs(p);
            });
        }

        if (prefs.cardSizeMode && /^(sm|md|lg|xl|xx1|xx2)$/.test(String(prefs.cardSizeMode))) {
            shared.cardSizeMode = String(prefs.cardSizeMode);
        }
        if (prefs.fitContainMedia != null) {
            shared.fitContainMedia = !!prefs.fitContainMedia;
        }
        if (prefs.groupPromptEnabled != null) {
            shared.groupPromptEnabled = !!prefs.groupPromptEnabled;
        }
        if (prefs.viewMode === 'list' || prefs.viewMode === 'grid') {
            shared.viewMode = prefs.viewMode;
        }

        var promptEl = document.getElementById('promptInput');
        if (promptEl) {
            if (typeof prefs.promptInputText === 'string') promptEl.value = prefs.promptInputText;
            if (prefs.promptInputHeight != null && Number(prefs.promptInputHeight) > 70) {
                promptEl.style.height = Number(prefs.promptInputHeight) + 'px';
            }
            promptEl.addEventListener('input', function () {
                var p = shared.loadUiPrefs();
                p.promptInputText = promptEl.value;
                shared.saveUiPrefs(p);
            });
            promptEl.addEventListener('mouseup', function () {
                var p = shared.loadUiPrefs();
                p.promptInputHeight = Math.round(promptEl.offsetHeight || 0);
                shared.saveUiPrefs(p);
            });
        }
    }

    // Theme Toggle
    var isDark = true;
    var themeBtn = document.getElementById('themeBtn');
    (function applyCachedTheme() {
        var prefs = shared.loadUiPrefs();
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
            var p = shared.loadUiPrefs();
            p.theme = isDark ? 'dark' : 'light';
            shared.saveUiPrefs(p);
            shared.showToast('info', null, isDark ? 'Chuyển sang Dark Mode' : 'Chuyển sang Light Mode', 2000);
        });
    }

    // Sidebar Toggle
    var sidebarCollapsed = false;
    function setSidebarCollapsed(collapsed) {
        sidebarCollapsed = collapsed;
        var sidebar      = document.getElementById('sidebar');
        var icon         = document.getElementById('sidebarToggleIcon');
        var floatBtn     = document.getElementById('sidebarFloatBtn');

        if (sidebar) {
            sidebar.classList.toggle('collapsed', sidebarCollapsed);
            if (window.innerWidth > 640) {
                if (sidebarCollapsed) {
                    sidebar.style.width = '0';
                    sidebar.style.minWidth = '0';
                    sidebar.style.maxWidth = '0';
                } else {
                    var sp = shared.loadUiPrefs();
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
                sidebar.setAttribute('aria-hidden', sidebarCollapsed ? 'true' : 'false');
            } else {
                sidebar.removeAttribute('aria-hidden');
            }
        }
        if (icon) icon.className = sidebarCollapsed ? 'bi bi-layout-sidebar' : 'bi bi-layout-sidebar-reverse';

        if (floatBtn) {
            var isMobile = window.innerWidth <= 640;
            if (!isMobile) {
                floatBtn.style.display = sidebarCollapsed ? 'flex' : 'none';
            } else {
                floatBtn.style.display = 'none';
            }
        }
        var p = shared.loadUiPrefs();
        p.sidebarCollapsed = !!sidebarCollapsed;
        shared.saveUiPrefs(p);
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
        var prefs = shared.loadUiPrefs();
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
                var p = shared.loadUiPrefs();
                p.sidebarWidth = w;
                shared.saveUiPrefs(p);
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

    // Mobile Sidebar
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

    // View Toggle
    var viewToggleBtn = document.getElementById('viewToggleBtn');
    if (viewToggleBtn) {
        function applyViewMode(mode, persist) {
            shared.viewMode = mode === 'list' ? 'list' : 'grid';
            var grid = document.getElementById('videoGrid');
            var icon = document.getElementById('viewToggleIcon');
            if (grid) grid.classList.toggle('view-list', shared.viewMode === 'list');
            if (icon) icon.className = shared.viewMode === 'list' ? 'bi bi-grid-3x3-gap-fill' : 'bi bi-list-ul';
            viewToggleBtn.classList.toggle('active', shared.viewMode === 'list');
            if (persist !== false) {
                var p = shared.loadUiPrefs();
                p.viewMode = shared.viewMode;
                shared.saveUiPrefs(p);
            }
        }
        viewToggleBtn.addEventListener('click', function () {
            applyViewMode(shared.viewMode === 'grid' ? 'list' : 'grid', true);
        });
        applyViewMode(shared.viewMode, false);
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
            shared.cardSizeMode = /^(sm|md|lg|xl|xx1|xx2)$/.test(String(mode)) ? String(mode) : 'md';
            grid.classList.remove('size-sm', 'size-md', 'size-lg', 'size-xl', 'size-xx1', 'size-xx2');
            grid.classList.add('size-' + shared.cardSizeMode);
            for (var i = 0; i < sizeBtns.length; i++) {
                var b = sizeBtns[i];
                if (!b) continue;
                b.classList.toggle('active', b.getAttribute('data-size') === shared.cardSizeMode);
            }
            var p = shared.loadUiPrefs();
            p.cardSizeMode = shared.cardSizeMode;
            shared.saveUiPrefs(p);
        }

        function applyFitContain(enabled) {
            shared.fitContainMedia = !!enabled;
            app.classList.toggle('fit-contain', shared.fitContainMedia);
            if (fitCb) fitCb.checked = shared.fitContainMedia;
            var p = shared.loadUiPrefs();
            p.fitContainMedia = shared.fitContainMedia;
            shared.saveUiPrefs(p);
        }

        function applyFitWidth(enabled) {
            var on = !!enabled;
            app.classList.toggle('fit-video-width', on);
            if (fitWidthCb) fitWidthCb.checked = on;
            if (on) applyFitContain(true);
            var p = shared.loadUiPrefs();
            p.fitWidthMedia = on;
            shared.saveUiPrefs(p);
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
            fitCb.checked = shared.fitContainMedia;
            fitCb.addEventListener('change', function () {
                applyFitContain(!!fitCb.checked);
                if (!fitCb.checked && fitWidthCb && fitWidthCb.checked) {
                    fitWidthCb.checked = false;
                    applyFitWidth(false);
                }
            });
        }
        if (fitWidthCb) {
            var prefs = shared.loadUiPrefs();
            fitWidthCb.checked = !!prefs.fitWidthMedia;
            fitWidthCb.addEventListener('change', function () {
                applyFitWidth(!!fitWidthCb.checked);
            });
        }

        applyCardSizeMode(shared.cardSizeMode);
        applyFitContain(shared.fitContainMedia);
        if (fitWidthCb && fitWidthCb.checked) applyFitWidth(true);
    })();

    // Sidebar Tabs (Video / Image)
    (function initMainTabs() {
        function switchTab(tab) {
            var isImage = (tab === 'image');
            var panelV = document.getElementById('tabPanelVideo');
            var panelI = document.getElementById('tabPanelImage');
            if (panelV) panelV.style.display = isImage ? 'none' : '';
            if (panelI) panelI.style.display = isImage ? '' : 'none';

            var vGrid  = document.getElementById('videoGrid');
            var iPanel = document.getElementById('imageGridPanel');
            if (vGrid)  vGrid.style.display  = isImage ? 'none' : '';
            if (iPanel) iPanel.style.display = isImage ? 'flex'  : 'none';

            var title = document.querySelector('.page-title');
            if (title) title.innerHTML = isImage ? 'IMAGE <em>MANAGER</em>' : 'VIDEO <em>GALLERY</em>';

            var vCtrl = document.getElementById('videoHeaderControls');
            if (vCtrl) vCtrl.style.display = isImage ? 'none' : '';
            var iCtrl = document.getElementById('imageHeaderControls');
            if (iCtrl) iCtrl.style.display = isImage ? '' : 'none';

            var tV = document.getElementById('tabVideo');
            var tI = document.getElementById('tabImage');
            if (tV) tV.classList.toggle('active', !isImage);
            if (tI) tI.classList.toggle('active',  isImage);

            try { var p = shared.loadUiPrefs(); p.mainTab = tab; shared.saveUiPrefs(p); } catch (_) {}
        }

        var btnV = document.getElementById('tabVideo');
        var btnI = document.getElementById('tabImage');
        if (btnV) btnV.addEventListener('click', function () { switchTab('video'); });
        if (btnI) btnI.addEventListener('click', function () { switchTab('image'); });

        try { var pr = shared.loadUiPrefs(); if (pr.mainTab === 'image') switchTab('image'); } catch (_) {}
    })();

    // Keyboard Shortcuts
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            if (typeof window.closeModal === 'function') window.closeModal({ target: document.getElementById('videoModal') });
            if (typeof window.closeLightbox === 'function') window.closeLightbox({ target: document.getElementById('imageLightbox') });
            if (typeof window.closeMentionDropdown === 'function') window.closeMentionDropdown();
            var s = document.getElementById('sidebar');
            var o = document.getElementById('sidebarOverlay');
            if (s) s.classList.remove('mobile-open');
            if (o) o.classList.remove('active');
        }
        if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
            if (typeof window.generateVideo === 'function') window.generateVideo();
        }
    });

    // Styles for Spin Btn and Mentions
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

    bindPrefsPersistence();

})();
