(function () {
    'use strict';

    var shared = window.GrokShared;
    var ui = window.GrokVideo;
    if (!shared || !ui) {
        console.error('grok.shared.js or grok.video-create.js is missing');
        return;
    }

    var state = shared.state;
    var pendingCardsByPromptKey = {}; // moved from video-create
    var handledGuildIds = shared.handledGuildIds;

    // --- Core cache helper methods ---

    function pendingPromptKey(guildId, promptText) {
        var norm = String(promptText == null ? '' : promptText).trim().replace(/\s+/g, ' ').toLowerCase();
        return String(guildId || '').trim() + '::' + norm;
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

    function findVideoCardByAnyId(ids, generatingOnly) {
        if (!ids || !ids.length) return null;
        var normalized = [];
        for (var n = 0; n < ids.length; n++) {
            var token = String(ids[n] || '').trim();
            if (token) normalized.push(token);
        }
        if (!normalized.length) return null;

        for (var i = 0; i < state.videos.length; i++) {
            var it = state.videos[i];
            if (!it) continue;
            if (generatingOnly && it.status !== 'generating') continue;
            var candidates = [
                String(it.id || '').trim(),
                String(it.requestId || '').trim(),
                String(it.videoGuid || '').trim()
            ];
            for (var j = 0; j < normalized.length; j++) {
                var needle = normalized[j];
                if (!needle) continue;
                for (var k = 0; k < candidates.length; k++) {
                    if (candidates[k] && candidates[k] === needle) return it;
                }
            }
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

        var resultPromptNorm = String(result && result.prompt ? result.prompt : '').trim().replace(/\s+/g, ' ').toLowerCase();
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
            var rq = o.requestId || o.cardId || o.videoGuid || o.videoId || o.taskId || o.itemId || o.id || '';
            var vg = o.videoGuid || o.videoId || o.requestId || o.cardId || o.taskId || o.itemId || o.id || '';
            var gid = o.guildId || o.outputGuildId || '';
            var stCode = toStatusCode(o.status);
            var ar = shared.normalizeAspectValue(o.aspect || o.aspectRatio || '');
            var ln = shared.normalizeLengthValue(o.length || o.videoLength || o.durationSec || o.duration || 0);
            var rs = shared.normalizeResolutionValue(o.resolution || o.res || o.quality || '');
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
            if (!vu && lp) return { videoUrl: shared.toFileUri(lp), localPath: lp, prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            if (vu && lp) return { videoUrl: vu.replace(/\\\//g, '/'), localPath: lp, prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            if (vu) {
                if (/^file:\/\//i.test(vu)) {
                    var lpF = shared.fileUriToLocalPath(vu);
                    return { videoUrl: vu.replace(/\\\//g, '/'), localPath: lpF || '', prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
                }
                if (/^[a-zA-Z]:[\\/]/.test(vu) || vu.indexOf('\\') >= 0 || vu.indexOf('/') >= 0) {
                    var lpW = vu.replace(/\//g, '\\');
                    return { videoUrl: shared.toFileUri(lpW), localPath: lpW, prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
                }
                return { videoUrl: vu.replace(/\\\//g, '/'), localPath: '', prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            }
            if (stCode !== null) return { videoUrl: '', localPath: '', prompt: pp, requestId: rq, videoGuid: vg, guildId: gid, statusCode: stCode, aspect: ar, length: ln, resolution: rs };
            return null;
        }
        if (typeof raw !== 'string') return null;
        var text = shared.decodeHtmlEntities(raw).trim().replace(/^\uFEFF/, '').replace(/^["']|["']$/g, '');
        text = shared.decodeUnicodeEscapes(text);
        if (!text) return null;

        var parsedObj = shared.safeJsonParse(text);
        if (typeof parsedObj === 'string') parsedObj = shared.safeJsonParse(parsedObj);
        if (parsedObj && typeof parsedObj === 'object' && !Array.isArray(parsedObj)) {
            return parseVideoResultFromEntry(parsedObj);
        }
        var promptFromJsonLike = shared.extractJsonLikeField(text, 'prompt');
        var linkFromJsonLike = shared.extractJsonLikeField(text, 'linkVideo')
            || shared.extractJsonLikeField(text, 'videoUrl')
            || shared.extractJsonLikeField(text, 'url');
        var reqFromJsonLike = shared.extractJsonLikeField(text, 'requestId')
            || shared.extractJsonLikeField(text, 'cardId')
            || shared.extractJsonLikeField(text, 'videoId')
            || shared.extractJsonLikeField(text, 'taskId')
            || shared.extractJsonLikeField(text, 'itemId')
            || shared.extractJsonLikeField(text, 'id')
            || shared.extractJsonLikeRawToken(text, 'requestId')
            || shared.extractJsonLikeRawToken(text, 'cardId')
            || shared.extractJsonLikeRawToken(text, 'videoId')
            || shared.extractJsonLikeRawToken(text, 'taskId')
            || shared.extractJsonLikeRawToken(text, 'itemId')
            || shared.extractJsonLikeRawToken(text, 'id');
        var aspectFromJsonLike = shared.normalizeAspectValue(
            shared.extractJsonLikeField(text, 'aspect')
            || shared.extractJsonLikeField(text, 'aspectRatio')
            || shared.extractJsonLikeRawToken(text, 'aspect')
            || shared.extractJsonLikeRawToken(text, 'aspectRatio')
        );
        var lengthFromJsonLike = shared.normalizeLengthValue(
            shared.extractJsonLikeField(text, 'length')
            || shared.extractJsonLikeField(text, 'videoLength')
            || shared.extractJsonLikeField(text, 'durationSec')
            || shared.extractJsonLikeRawToken(text, 'length')
            || shared.extractJsonLikeRawToken(text, 'videoLength')
            || shared.extractJsonLikeRawToken(text, 'durationSec')
            || 0
        );
        var resolutionFromJsonLike = shared.normalizeResolutionValue(
            shared.extractJsonLikeField(text, 'resolution')
            || shared.extractJsonLikeField(text, 'res')
            || shared.extractJsonLikeField(text, 'quality')
            || shared.extractJsonLikeRawToken(text, 'resolution')
            || shared.extractJsonLikeRawToken(text, 'res')
            || shared.extractJsonLikeRawToken(text, 'quality')
            || ''
        );
        var statusFromJsonLike = shared.extractJsonLikeField(text, 'status')
            || shared.extractJsonLikeRawToken(text, 'status');
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

        if (/^file:\/\//i.test(text)) {
            var lpFromFile = shared.fileUriToLocalPath(text);
            return { videoUrl: text, localPath: lpFromFile || '' };
        }
        if (/^[a-zA-Z]:[\\/]/.test(text) || /^\//.test(text)) {
            return { videoUrl: shared.toFileUri(text), localPath: text };
        }
        if (/^https?:\/\//i.test(text)) return { videoUrl: text.replace(/\\\//g, '/'), localPath: '' };

        var m1 = text.match(/--location\s+'([^']+)'/i);
        if (m1 && m1[1]) return { videoUrl: m1[1].trim().replace(/\\\//g, '/'), localPath: '' };
        var m2 = text.match(/--location\s+"([^"]+)"/i);
        if (m2 && m2[1]) return { videoUrl: m2[1].trim().replace(/\\\//g, '/'), localPath: '' };
        var m4 = text.match(/--location\s+'([a-zA-Z]:[\\/][^']+\.mp4)'/i);
        if (m4 && m4[1]) return { videoUrl: shared.toFileUri(m4[1].trim()), localPath: m4[1].trim() };
        var m5 = text.match(/--location\s+"([a-zA-Z]:[\\/][^"]+\.mp4)"/i);
        if (m5 && m5[1]) return { videoUrl: shared.toFileUri(m5[1].trim()), localPath: m5[1].trim() };
        var m6 = text.match(/[a-zA-Z]:[\\/][^'"\\r\\n]+\.mp4/i);
        if (m6 && m6[0]) return { videoUrl: shared.toFileUri(m6[0].trim()), localPath: m6[0].trim() };
        var m3 = text.match(/https?:\/\/[^\s'"\\]+/i);
        var out = m3 && m3[0] ? m3[0].trim() : '';
        return out ? { videoUrl: out.replace(/\\\//g, '/'), localPath: '' } : null;
    }

    function normalizeKeyText(v) {
        return String(v == null ? '' : v).trim().replace(/^['"]|['"]$/g, '').toLowerCase();
    }

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

    function normalizeDictMaybe(raw) {
        if (raw && typeof raw === 'object' && !Array.isArray(raw)) return raw;
        var parsed = shared.safeJsonParse(raw);
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed;

        if (typeof raw === 'string') {
            var t = shared.decodeHtmlEntities(raw).trim();
            t = t.replace(/^```[a-zA-Z]*\s*/, '').replace(/\s*```$/, '');
            parsed = shared.safeJsonParse(t);
            if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed;
        }
        return null;
    }

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
        var d1 = payload && payload.datas;
        var n1 = normalizeDictMaybe(d1);
        if (n1) return n1;

        var n2 = normalizeDictMaybe(payload);
        if (n2) return n2;

        var n3 = normalizeDictMaybe(shared.getLatestDatasDict());
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

    function pickLoadVideoDatasDictionary(payload) {
        var d1 = payload && payload.loadVideoDatas;
        var n1 = normalizeDictMaybe(d1);
        if (n1) return n1;

        var n2 = normalizeDictMaybe(payload);
        if (n2) return n2;

        var n3 = normalizeDictMaybe(shared.getLatestLoadVideoDatasDict());
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

    // --- Core ingest logic ---
    function ingestDatasDict(dict) {
        if (!dict || typeof dict !== 'object' || Array.isArray(dict)) return;
        var grid  = document.getElementById('videoGrid');
        var empty = document.getElementById('emptyState');
        var changed = false;

        Object.keys(dict).forEach(function (gid) {
            if (handledGuildIds[gid]) return;
            var raw = dict[gid];
            if (raw == null) return;

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

            handledGuildIds[gid] = true;

            parsedEntries.forEach(function (parsed) {
                var target = findVideoCardByAnyId([
                    parsed.requestId,
                    parsed.videoGuid
                ], false);

                // Load-cache can have many done videos under the same guildId.
                // Avoid collapsing all entries into a single card by matching more specific keys.
                if (!target && (parsed.localPath || parsed.videoUrl)) {
                    for (var ex = 0; ex < state.videos.length; ex++) {
                        var exItem2 = state.videos[ex];
                        if (!exItem2) continue;
                        if (String(exItem2.guildId || '') !== String(gid)) continue;
                        var sameLocal = !!parsed.localPath && String(exItem2.localPath || '') === String(parsed.localPath || '');
                        var sameVideo = !!parsed.videoUrl && String(exItem2.videoUrl || '') === String(parsed.videoUrl || '');
                        if (sameLocal || sameVideo) {
                            target = exItem2;
                            break;
                        }
                    }
                }

                var isError = (parsed.statusCode !== null && parsed.statusCode !== 200);
                if (target) {
                    target.status = isError ? 'error' : 'done';
                    target.progress = 100;
                    if (parsed.requestId && !target.requestId) target.requestId = parsed.requestId;
                    if (parsed.videoGuid) target.videoGuid = parsed.videoGuid;
                    if (parsed.videoUrl) target.videoUrl = parsed.videoUrl;
                    if (parsed.localPath) target.localPath = parsed.localPath;
                    target.errorMsg = isError
                        ? ('Tạo video thất bại (status=' + parsed.statusCode + ')')
                        : '';

                    ui.updateCard(target);

                    if (target.status === 'done' && target.localPath) {
                        ui.hydrateLocalPlayable(target);
                    } else if (target.status === 'done') {
                        ui.hydrateDoneCardMedia(target);
                    }
                    changed = true;
                    return;
                }

                changed = true;
                var itemId = shared.uid();
                var item = {
                    id: itemId,
                    prompt: (parsed.prompt && String(parsed.prompt).trim()) || gid.substring(0, 36),
                    aspect: shared.normalizeAspectValue(parsed.aspect) || '16:9',
                    length: shared.normalizeLengthValue(parsed.length) || 0,
                    res: shared.normalizeResolutionValue(parsed.resolution) || '',
                    status: isError ? 'error' : 'done',
                    progress: 100,
                    videoUrl: parsed.videoUrl || '',
                    localPath: parsed.localPath || '',
                    localUrl: '',
                    thumbUrl: '',
                    errorMsg: isError
                        ? ('Tạo video thất bại (status=' + parsed.statusCode + ')')
                        : '',
                    createdAt: Date.now(),
                    guildId: gid,
                    requestId: parsed.requestId || '',
                    videoGuid: parsed.videoGuid || ''
                };

                state.videos.push(item);
                if (empty && empty.parentNode === grid) grid.removeChild(empty);
                if (grid) grid.appendChild(ui.createCard(item));

                if (item.status === 'done' && item.localPath) {
                    ui.hydrateLocalPlayable(item);
                } else if (item.status === 'done') {
                    ui.hydrateDoneCardMedia(item);
                }
            });
        });

        if (changed) ui.updateStats();
    }

    // --- Bridge Callbacks & Polling ---
    function handleAsyncVideoObject(obj) {
        var parsed = parseVideoResultFromEntry(obj);
        if (!parsed) return false;

        var gid = String(parsed.guildId || obj.guildId || obj.outputGuildId || '').trim();
        var req = String(parsed.videoGuid || parsed.requestId || obj.videoGuid || obj.videoId || obj.requestId || obj.cardId || obj.id || '').trim();
        if (!gid && !req) return false;

        var target = findVideoCardByAnyId([
            req,
            parsed.requestId,
            parsed.videoGuid,
            obj && obj.requestId,
            obj && obj.cardId,
            obj && obj.videoGuid,
            obj && obj.videoId,
            obj && obj.id
        ], true);
        if (!target && gid) {
            for (var i = 0; i < state.videos.length; i++) {
                var it = state.videos[i];
                if (!it || it.status !== 'generating') continue;
                if (String(it.guildId || '') === gid) { target = it; break; }
            }
        }
        if (!target) {
            var canCreateDone = !!(parsed.videoUrl || parsed.localPath || (parsed.statusCode !== null));
            if (!canCreateDone) return false;

            var fallbackId = String(parsed.requestId || parsed.videoGuid || obj.requestId || obj.videoId || obj.cardId || obj.id || shared.uid()).trim();
            var item = {
                id: shared.uid(),
                prompt: (parsed.prompt && String(parsed.prompt).trim()) || 'Video cache',
                aspect: shared.normalizeAspectValue(parsed.aspect) || '16:9',
                length: shared.normalizeLengthValue(parsed.length) || 0,
                res: shared.normalizeResolutionValue(parsed.resolution) || '',
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
                guildId: gid || fallbackId,
                requestId: parsed.requestId || fallbackId,
                videoGuid: parsed.videoGuid || fallbackId
            };

            state.videos.push(item);
            var grid  = document.getElementById('videoGrid');
            var empty = document.getElementById('emptyState');
            if (empty && empty.parentNode === grid) grid.removeChild(empty);
            if (grid) grid.appendChild(ui.createCard(item));

            if (item.status === 'done' && item.localPath) {
                ui.hydrateLocalPlayable(item);
            } else if (item.status === 'done') {
                ui.hydrateDoneCardMedia(item);
            }
            ui.updateStats();
            return true;
        }

        unregisterPendingCard(target);
        if (parsed.videoGuid) target.videoGuid = parsed.videoGuid;
        if (parsed.localPath) target.localPath = parsed.localPath;
        if (parsed.videoUrl) target.videoUrl = parsed.videoUrl;
        if (parsed.aspect) target.aspect = shared.normalizeAspectValue(parsed.aspect) || target.aspect;
        if (parsed.length) target.length = shared.normalizeLengthValue(parsed.length) || target.length;
        if (parsed.resolution) target.res = shared.normalizeResolutionValue(parsed.resolution) || target.res;

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
        ui.updateCard(target);
        ui.updateStats();

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
            if (!hasGenerating && shared.asyncVideoJobs[jobGid]) {
                var doneCb = shared.asyncVideoJobs[jobGid].onFinished;
                delete shared.asyncVideoJobs[jobGid];
                delete shared.pollJobs[jobGid];
                if (typeof doneCb === 'function') doneCb(false);
            }
        }
        return true;
    }

    shared.registerDataVideoHook(function(value) {
        if (handleAsyncVideoObject(value)) return;
        var dict = pickResultDictionary({ datas: value });
        shared.setLatestDatasDict(dict);
        if (!dict || typeof dict !== 'object') return;

        Object.keys(shared.asyncVideoJobs).forEach(function (gid) {
            var job = shared.asyncVideoJobs[gid];
            if (!job) return;
            var res = resolveResultList(dict, gid);
            if (!res || !Array.isArray(res)) return;

            var tempJob = shared.pollJobs[gid] || {
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
            shared.pollJobs[gid] = tempJob;
        });

        ingestDatasDict(dict); // auto ingest if any unconnected result 
    });

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

    function stopAllLoadVideoDatasPolling() {
        Object.keys(shared.loadVideoDatasPollJobs).forEach(function (oldG) {
            var j = shared.loadVideoDatasPollJobs[oldG];
            if (j && j.interval) {
                try { clearInterval(j.interval); } catch (_) {}
            }
            delete shared.loadVideoDatasPollJobs[oldG];
        });
    }

    shared.registerLoadVideoDatasHook(function(value) {
        var dict = pickLoadVideoDatasDictionary({ loadVideoDatas: value });
        shared.setLatestLoadVideoDatasDict(dict);
        if (!dict || typeof dict !== 'object') return;
        ingestDatasDict(dict);

        var pendingLoadVideoCacheGuildId = shared.getPendingLoadVideoCacheGuildId();
        if (pendingLoadVideoCacheGuildId) {
            var hit = findGuildKeyInDict(dict, pendingLoadVideoCacheGuildId);
            if (hit != null && handledGuildIds[hit]) {
                stopAllLoadVideoDatasPolling();
                resetLoadCacheVideosBtnUi();
                shared.setPendingLoadVideoCacheGuildId('');
                shared.showToast('success', 'loadVideoDatas', 'Đã nhận dữ liệu và hiển thị video.', 3500);
            }
        }
    });

    shared.bindAsyncPushReceiver();

    // Setup generic __ac.onUpdate wrappers if they exist
    try {
        if (window.__ac && typeof window.__ac.onUpdate === 'function') {
            window.__ac.onUpdate('datas', function(datas, og) {
                if(handleAsyncVideoObject(datas)) return;
                var dict = normalizeDictMaybe(datas);
                if(dict) { shared.setLatestDatasDict(dict); ingestDatasDict(dict); }
            });
            window.__ac.onUpdate('loadVideoDatas', function(datas, og) {
                var dict = normalizeDictMaybe(datas);
                if(dict) { shared.setLatestLoadVideoDatasDict(dict); ingestDatasDict(dict); }
            });
            window.__ac.onUpdate(function(live) {
                var d = pickResultDictionary(live);
                if(d && Object.keys(d).length) { shared.setLatestDatasDict(d); ingestDatasDict(d); }
                var l = pickLoadVideoDatasDictionary(live);
                if(l && Object.keys(l).length) { shared.setLatestLoadVideoDatasDict(l); ingestDatasDict(l); }
                
                Object.keys(shared.pollJobs).forEach(function (gid) {
                    var job = shared.pollJobs[gid];
                    if (!job) return;
                    var res = resolveResultList(d, gid);
                    if (res && Array.isArray(res)) {
                        var ni = collectNewReadyResults(job, res);
                        if (ni.length > 0) {
                            job.renderedCount += ni.length;
                            job.onResult(ni, job.renderedCount, job.expectedCount);
                        }
                        if (job.renderedCount >= job.expectedCount) {
                            clearInterval(job.interval);
                            delete shared.pollJobs[gid];
                            ui.updateStats();
                        }
                    }
                });
            });
        }
    } catch(_) {}

    // Eager Load
    (function doEagerInitialVideoLoad() {
        function attempt() {
            var d = pickResultDictionary();
            if(d && Object.keys(d).length) { shared.setLatestDatasDict(d); ingestDatasDict(d); }
            var l = pickLoadVideoDatasDictionary();
            if(l && Object.keys(l).length) { shared.setLatestLoadVideoDatasDict(l); ingestDatasDict(l); }
        }
        attempt();
        setTimeout(attempt, 300);
        setTimeout(attempt, 1000);
        setTimeout(attempt, 2500);
    })();

    // --- Load/Clear Cache methods ---
    function clearVideoGalleryStateAndDom() {
        state.videos = [];
        for(var k in pendingCardsByPromptKey) delete pendingCardsByPromptKey[k];
        for(var m in handledGuildIds) delete handledGuildIds[m];
        for(var o in shared.hydratingGuildIds) delete shared.hydratingGuildIds[o];
        var grid  = document.getElementById('videoGrid');
        var empty = document.getElementById('emptyState');
        if (grid) {
            grid.innerHTML = '';
            if (empty) grid.appendChild(empty);
        }
        ui.updateStats();
    }

    function startLoadVideoDatasPolling(guildId) {
        shared.bindAsyncPushReceiver();
        var g = String(guildId || '').trim();
        if (!g) return;
        if (shared.loadVideoDatasPollJobs[g]) {
            try { clearInterval(shared.loadVideoDatasPollJobs[g].interval); } catch (_) {}
        }
        var MAX_MS = 180000;
        var startTime = Date.now();
        function entryCountFromRaw(raw) {
            if (raw == null) return 0;
            if (Array.isArray(raw)) return raw.length;
            if (typeof raw === 'object') return Object.keys(raw).length;
            return 1;
        }

        var iv = setInterval(function () {
            if (shared.getLoadVideoDatasPollPaused()) return;
            if (Date.now() - startTime > MAX_MS) {
                clearInterval(iv);
                delete shared.loadVideoDatasPollJobs[g];
                resetLoadCacheVideosBtnUi();
                shared.showToast('warn', 'loadVideoDatas', 'Timeout 180s: không nhận được dữ liệu.', 5000);
                return;
            }
            try {
                var dict = pickLoadVideoDatasDictionary();
                var dk = findGuildKeyInDict(dict, g);
                if (dk == null) return;
                var raw = dict[dk];
                if (raw == null) return;

                var wasHandled = !!handledGuildIds[dk];
                shared.setLatestLoadVideoDatasDict(dict);
                var slice = {};
                slice[dk] = raw;
                ingestDatasDict(slice);

                if (handledGuildIds[dk]) {
                    var job = shared.loadVideoDatasPollJobs[g];
                    if (!job) return;
                    var currentCount = entryCountFromRaw(raw);
                    if (job.lastSeenCount === currentCount) job.stableTicks = (job.stableTicks || 0) + 1;
                    else job.stableTicks = 0;
                    job.lastSeenCount = currentCount;

                    // Keep polling a bit longer to catch late-arriving entries of the same guild.
                    if ((job.stableTicks || 0) >= 2) {
                        clearInterval(iv);
                        delete shared.loadVideoDatasPollJobs[g];
                        resetLoadCacheVideosBtnUi();
                        if (!wasHandled) {
                            shared.showToast('success', 'loadVideoDatas', 'Đã nhận dữ liệu và hiển thị video.', 3500);
                        }
                    }
                }
            } catch (_) {}
        }, 1000);

        shared.loadVideoDatasPollJobs[g] = {
            interval: iv,
            startTime: startTime,
            lastSeenCount: -1,
            stableTicks: 0
        };
    }

    window.requestLoadVideoCacheFromHost = function () {
        stopAllLoadVideoDatasPolling();
        resetLoadCacheVideosBtnUi();

        if (state.processing > 0) {
            shared.showToast('warn', 'Không thể tải cache', 'Còn ' + state.processing + ' video đang xử lý');
            return;
        }
        clearVideoGalleryStateAndDom();
        shared.setLatestLoadVideoDatasDict(null);

        var guildId = shared.uid();
        shared.setPendingLoadVideoCacheGuildId(guildId);
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

        shared.showToast('info', 'Tải cache video', 'Đã gửi statusCreate=2, chờ loadVideoDatas…', 3200);
        startLoadVideoDatasPolling(guildId);
    };

    window.requestClearAllCacheFromHost = function () {
        if (state.processing > 0) {
            shared.showToast('warn', 'Không thể xóa', 'Còn ' + state.processing + ' video đang xử lý');
            return;
        }
        var guildId = shared.uid();
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
        shared.showToast('info', 'Đã xóa', 'Đã dọn gallery và gửi statusCreate=3 (clear cache).', 2800);
    };

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

    // curl download callback
    var downloadedPaths = {};
    var downloadMetaByKey = {};
    var downloadItemIdByKey = {};

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
                    target.videoUrl = d.localUrl || shared.toFileUri(d.path) || target.videoUrl;
                    ui.updateCard(target);
                    if (!target.localUrl && target.localPath) ui.hydrateLocalPlayable(target);
                }
                shared.showToast('success', 'Đã tải video', String(d.path), 5000);
            } else {
                shared.showToast('error', 'Tải video thất bại', d.error || 'Unknown error', 6000);
            }
        } catch (_) {}
    });

    // --- Export ---
    window.GrokVideoCache = {
        dict: latestDatasDict, // ? check accesss
        pickResultDictionary: pickResultDictionary,
        resolveResultList: resolveResultList,
        collectNewReadyResults: collectNewReadyResults,
        findPendingCardForResult: findPendingCardForResult,
        registerPendingCard: registerPendingCard,
        unregisterPendingCard: unregisterPendingCard,
        parseVideoResultFromEntry: parseVideoResultFromEntry,
        clearVideoGalleryStateAndDom: clearVideoGalleryStateAndDom,
        stopAllLoadVideoDatasPolling: stopAllLoadVideoDatasPolling,
        resetLoadCacheVideosBtnUi: resetLoadCacheVideosBtnUi
    };

})();
