/**
 * Injected into MAIN world on labs.google — has access to window.grecaptcha
 * Also intercepts TRPC fetch responses to capture fresh signed media URLs.
 */
(function () {
  if (window.__FLOWKIT_INJECTED__) {
    console.log('[FlowKit Injected] Already loaded in main world');
    return;
  }
  window.__FLOWKIT_INJECTED__ = true;

  const SITE_KEY = '6LdsFiUsAAAAAIjVDZcuLhaHiDn5nnHVXVRQGeMV';

  // ─── TRPC Response Monitor ─────────────────────────────────
  const _originalFetch = window.fetch;
  window.fetch = async function (...args) {
    const response = await _originalFetch.apply(this, args);
    try {
      const url = typeof args[0] === 'string' ? args[0] : args[0]?.url || '';
      if (url.includes('/fx/api/trpc/') && response.ok) {
        const clone = response.clone();
        clone.text().then(text => {
          if (text.includes('storage.googleapis.com/ai-sandbox-videofx/')) {
            window.postMessage({ type: 'TRPC_MEDIA_URLS', url, body: text }, '*');
            window.dispatchEvent(new CustomEvent('TRPC_MEDIA_URLS', {
              detail: { url, body: text },
            }));
          }
        }).catch(() => {});
      }
    } catch {}
    return response;
  };

  async function handleGetCaptcha(requestId, pageAction) {
    try {
      await waitForGrecaptcha();
      const token = await window.grecaptcha.enterprise.execute(SITE_KEY, {
        action: pageAction || 'IMAGE_GENERATION',
      });
      console.log('[FlowKit Injected] Captcha token solved for', requestId);
      window.postMessage({ type: 'CAPTCHA_RESULT', requestId, token }, '*');
      window.dispatchEvent(new CustomEvent('CAPTCHA_RESULT', {
        detail: { requestId, token },
      }));
    } catch (e) {
      console.error('[FlowKit Injected] Captcha solve error:', e);
      window.postMessage({ type: 'CAPTCHA_RESULT', requestId, error: e.message || 'UNKNOWN_ERROR' }, '*');
      window.dispatchEvent(new CustomEvent('CAPTCHA_RESULT', {
        detail: { requestId, error: e.message || 'UNKNOWN_ERROR' },
      }));
    }
  }

  // Support both CustomEvent and postMessage
  window.addEventListener('GET_CAPTCHA', (e) => {
    const detail = e.detail || {};
    const requestId = detail.requestId;
    const pageAction = detail.pageAction;
    if (requestId) handleGetCaptcha(requestId, pageAction);
  });

  window.addEventListener('message', (e) => {
    if (e.data && e.data.type === 'GET_CAPTCHA') {
      handleGetCaptcha(e.data.requestId, e.data.pageAction);
    }
  });

  function waitForGrecaptcha(timeout = 10000) {
    return new Promise((resolve, reject) => {
      const start = Date.now();
      const check = () => {
        if (window.grecaptcha?.enterprise?.execute) return resolve();
        if (Date.now() - start > timeout) return reject(new Error('grecaptcha not available'));
        setTimeout(check, 200);
      };
      check();
    });
  }

  console.log('[FlowKit Injected] Initialized successfully in main world');
})();
