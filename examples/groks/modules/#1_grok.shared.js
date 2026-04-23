(function () {
    'use strict';

    // ═══ SHARED STATE ═════════════════════════════════
    var state = { videos: [], processing: 0 };
    var pollJobs = {};
    var viewMode = 'grid'; // 'grid' | 'list'
    var cardSizeMode = 'md'; // sm | md | lg | xl | xx1 | xx2
    var fitContainMedia = false;
    var groupPromptEnabled = false;
    var groupCollapsedState = {};
    var groupLayoutApplied = false;
    var promptGroupingSignature = '';

    // Batch Queue State (shared with video-create)
    var batchQueue = [];
    var batchRunning = false;
    var currentBatchIdx = 0;

    // Load Cache State
    var loadVideoDatasPollJobs = {};
    var loadVideoDatasPollPaused = false;
    var pendingLoadVideoCacheGuildId = '';
    
    // Parsed dictionaries for real-time pushing
    var latestDatasDict = null;
    var latestLoadVideoDatasDict = null;
    var handledGuildIds = {};
    var hydratingGuildIds = {};

    var asyncVideoJobs = {};

    // ═══ UI PREFS ═════════════════════════════════════
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
        try { localStorage.setItem(UI_PREFS_KEY, JSON.stringify(prefs || {})); } catch (_) {}
    }

    // ═══ TOAST & UTILS ════════════════════════════════
    function showToast(type, title, msg, duration) {
        duration = duration || 4000;
        var icons = { success: 'bi-check-circle-fill', error: 'bi-exclamation-circle-fill', info: 'bi-info-circle-fill', warn: 'bi-exclamation-triangle-fill' };
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
        return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    }

    function aspectClass(r) { return { '9:16': 'r916', '16:9': 'r169', '1:1': 'r11', '2:3': 'r23', '3:2': 'r32' }[r] || ''; }
    
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
        try { return encodeURI(String(pathOrUrl)); } catch (_) { return String(pathOrUrl); }
    }
    function isAbsoluteWinPath(s) { return !!s && /^[a-zA-Z]:[\\/]/.test(String(s).trim()); }

    function fileUriToLocalPath(s) {
        if (!s || typeof s !== 'string') return '';
        var u = s.trim();
        if (!/^file:/i.test(u)) return '';
        try {
            u = u.replace(/^file:\/\//i, '');
            if (u.charAt(0) === '/' && /^\/[a-zA-Z]:\//.test(u)) u = u.substring(1);
            else if (u.charAt(0) === '/') u = u.replace(/^\/+/, '');
            u = decodeURIComponent(String(u).replace(/\+/g, '%20')).replace(/\//g, '\\');
            return u;
        } catch (_) { return ''; }
    }

    var localPathResolveWaiters = {};
    function resolveLocalPlayableUrl(localPath, fallbackUrl, cb) {
        var done = false;
        function finish(url, useRawFallback) {
            if (done) return;
            done = true;
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
            if (typeof window.hostResolvePath !== 'function') {
                attemptNo++;
                if (attemptNo <= 4) setTimeout(attempt, 500);
                else finish(fallbackUrl || '', true);
                return;
            }
            localPathResolveWaiters[reqId] = function (resolvedUrl) { finish(resolvedUrl, false); };
            try { window.hostResolvePath(localPath, reqId); } 
            catch (_) { delete localPathResolveWaiters[reqId]; finish(fallbackUrl || '', true); return; }
            
            setTimeout(function () {
                var waiter = localPathResolveWaiters[reqId];
                if (!waiter) return;
                delete localPathResolveWaiters[reqId];
                finish(fallbackUrl || '', true);
            }, 8000);
        }
        attempt();
    }

    function isInternalPlayableRef(url) {
        if (!url || typeof url !== 'string') return false;
        return /^https:\/\/((localfiles(?:-[a-z0-9]+)?)|downloads)\.local\//i.test(url.trim());
    }

    function resolvePlayableRefUrl(url, cb) {
        var done = false;
        function finish(u) {
            if (done) return;
            done = true;
            cb(u || '');
        }
        if (!isInternalPlayableRef(url) || typeof window.hostResolveRef !== 'function') {
            finish('');
            return;
        }
        var reqId = uid();
        localPathResolveWaiters[reqId] = finish;
        try { window.hostResolveRef(url, reqId); } catch (_) { finish(''); }
        setTimeout(function () {
            var w = localPathResolveWaiters[reqId];
            if (!w) return;
            delete localPathResolveWaiters[reqId];
            finish('');
        }, 5000);
    }
    
    function isBareMediaFileName(s) {
        if (!s || typeof s !== 'string') return false;
        var t = s.trim();
        if (!t) return false;
        if (/^[a-zA-Z]:[\\/]/.test(t) || /^file:/i.test(t) || /^https?:\/\//i.test(t) || isInternalPlayableRef(t)) return false;
        if (/[\\/]/.test(t)) return false;
        return /\.(mp4|webm|m4v|mov)(\?.*)?$/i.test(t);
    }

    function withCacheBust(u) {
        if (!u || typeof u !== 'string') return u || '';
        var s = u.trim();
        if (!s) return '';
        var sep = (s.indexOf('?') >= 0) ? '&' : '?';
        return s + sep + 't=' + Date.now();
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

    // ═══ PARSERS ══════════════════════════════════════
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

    // ═══ CALLBACK REGISTRY FOR BRIDGE ═════════════════
    var onReceiveDataVideoHooks = [];
    var onReceiveDataImagesHooks = [];
    var onReceiveLoadVideoDatasHooks = [];
    
    function registerDataVideoHook(fn) { onReceiveDataVideoHooks.push(fn); }
    function registerDataImagesHook(fn) { onReceiveDataImagesHooks.push(fn); }
    function registerLoadVideoDatasHook(fn) { onReceiveLoadVideoDatasHooks.push(fn); }

    var asyncPushReceiverBound = false;
    function bindAsyncPushReceiver() {
        if (asyncPushReceiverBound) return;
        if (!window.hostAsync || typeof window.hostAsync.on !== 'function') return;

        window.hostAsync.on('datas', function (value) {
            onReceiveDataVideoHooks.forEach(function(h) { h(value); });
        });
        window.hostAsync.on('dataImages', function (value) {
            onReceiveDataImagesHooks.forEach(function(h) { h(value); });
        });
        window.hostAsync.on('loadVideoDatas', function (value) {
            onReceiveLoadVideoDatasHooks.forEach(function(h) { h(value); });
        });

        // Backward compatibility
        window.hostAsync.on('item', function (value) {
            var obj = (function(v) {
                if (v && typeof v === 'object') return v;
                if (typeof v !== 'string') return null;
                try { return JSON.parse(v); } catch (_) { return null; }
            })(value);
            if (!obj) return;
            if (Array.isArray(obj)) {
                if (obj.length > 0 && obj[0] && typeof obj[0] === 'object' && obj[0].imageGuid) {
                    onReceiveDataImagesHooks.forEach(function(h) { h(obj); });
                    return;
                }
                onReceiveDataVideoHooks.forEach(function(h) { h(obj); });
                return;
            }
            if (obj.imageGuid) {
                onReceiveDataImagesHooks.forEach(function(h) { h(obj); });
                return;
            }
            if (obj.videoGuid || obj.requestId || obj.guildId || obj.outputGuildId || obj.linkVideo) {
                onReceiveDataVideoHooks.forEach(function(h) { h(obj); });
            }
        });

        asyncPushReceiverBound = true;
    }

    // EVENT LISTENER FOR path resolve
    window.addEventListener('hostPathResolved', function(ev) {
        var d = ev.detail || {};
        if (d.requestId && localPathResolveWaiters[d.requestId]) {
            var cb = localPathResolveWaiters[d.requestId];
            delete localPathResolveWaiters[d.requestId];
            cb(d.ok && d.localUrl ? d.localUrl : '');
        }
    });

    // ═══ EXPORT NAMESPACE ═════════════════════════════
    window.GrokShared = {
        state: state,
        pollJobs: pollJobs,
        batchQueue: batchQueue,
        asyncVideoJobs: asyncVideoJobs,
        loadVideoDatasPollJobs: loadVideoDatasPollJobs,
        handledGuildIds: handledGuildIds,
        hydratingGuildIds: hydratingGuildIds,
        
        getLatestDatasDict: function() { return latestDatasDict; },
        setLatestDatasDict: function(val) { latestDatasDict = val; },
        getLatestLoadVideoDatasDict: function() { return latestLoadVideoDatasDict; },
        setLatestLoadVideoDatasDict: function(val) { latestLoadVideoDatasDict = val; },
        
        getLoadVideoDatasPollPaused: function() { return loadVideoDatasPollPaused; },
        setLoadVideoDatasPollPaused: function(val) { loadVideoDatasPollPaused = val; },
        getPendingLoadVideoCacheGuildId: function() { return pendingLoadVideoCacheGuildId; },
        setPendingLoadVideoCacheGuildId: function(val) { pendingLoadVideoCacheGuildId = val; },
        
        getBatchRunning: function() { return batchRunning; },
        setBatchRunning: function(val) { batchRunning = val; },
        getCurrentBatchIdx: function() { return currentBatchIdx; },
        setCurrentBatchIdx: function(val) { currentBatchIdx = val; },
        
        // Settings / UI state
        viewMode: viewMode,
        cardSizeMode: cardSizeMode,
        fitContainMedia: fitContainMedia,
        groupPromptEnabled: groupPromptEnabled,
        groupCollapsedState: groupCollapsedState,
        groupLayoutApplied: groupLayoutApplied,
        promptGroupingSignature: promptGroupingSignature,

        // Utils
        loadUiPrefs: loadUiPrefs,
        saveUiPrefs: saveUiPrefs,
        showToast: showToast,
        showError: showError,
        uid: uid,
        timeAgo: timeAgo,
        esc: esc,
        aspectClass: aspectClass,
        normalizeAspectValue: normalizeAspectValue,
        normalizeLengthValue: normalizeLengthValue,
        normalizeResolutionValue: normalizeResolutionValue,
        toFileUri: toFileUri,
        toPlayableSrc: toPlayableSrc,
        isAbsoluteWinPath: isAbsoluteWinPath,
        fileUriToLocalPath: fileUriToLocalPath,
        resolveLocalPlayableUrl: resolveLocalPlayableUrl,
        isInternalPlayableRef: isInternalPlayableRef,
        resolvePlayableRefUrl: resolvePlayableRefUrl,
        isBareMediaFileName: isBareMediaFileName,
        withCacheBust: withCacheBust,

        // Parsers
        safeJsonParse: safeJsonParse,
        decodeHtmlEntities: decodeHtmlEntities,
        decodeUnicodeEscapes: decodeUnicodeEscapes,
        unescapeJsonLikeValue: unescapeJsonLikeValue,
        extractJsonLikeField: extractJsonLikeField,
        extractJsonLikeRawToken: extractJsonLikeRawToken,
        extractJsonLikeBoolField: extractJsonLikeBoolField,
        normalizeLooseJsonToken: normalizeLooseJsonToken,
        normalizeStatusToState: normalizeStatusToState,

        // Bridge setup
        bindAsyncPushReceiver: bindAsyncPushReceiver,
        registerDataVideoHook: registerDataVideoHook,
        registerDataImagesHook: registerDataImagesHook,
        registerLoadVideoDatasHook: registerLoadVideoDatasHook
    };

})();
