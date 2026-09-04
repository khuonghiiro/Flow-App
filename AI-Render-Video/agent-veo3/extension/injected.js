/**
 * Injected into MAIN world on labs.google — has access to window.grecaptcha
 * Also intercepts TRPC fetch responses to capture fresh signed media URLs.
 */
(function () {
  if (window.__FLOW_INJECTED__) {
    console.log('[FlowAgent] injected.js already initialized');
    return;
  }
  window.__FLOW_INJECTED__ = true;

  const SITE_KEY = '6LdsFiUsAAAAAIjVDZcuLhaHiDn5nnHVXVRQGeMV';

  // ─── TRPC Response Monitor ─────────────────────────────────
  if (!window._flowOriginalFetch) {
    window._flowOriginalFetch = window.fetch;
    window.fetch = async function (...args) {
      const response = await window._flowOriginalFetch.apply(this, args);
      try {
        const url = typeof args[0] === 'string' ? args[0] : args[0]?.url || '';
        // Only intercept TRPC calls on labs.google that return project/flow data
        if (url.includes('/fx/api/trpc/') && response.ok) {
          const clone = response.clone();
          clone.text().then(text => {
            if (text.includes('flow-content.google') || text.includes('storage.googleapis.com/ai-sandbox-videofx/')) {
              window.dispatchEvent(new CustomEvent('TRPC_MEDIA_URLS', {
                detail: { url, body: text },
              }));
            }
          }).catch(() => {});
        }
      } catch {}
      return response;
    };
  }

  window.addEventListener('GET_CAPTCHA', async ({ detail }) => {
    const { requestId, pageAction } = detail;
    try {
      await waitForGrecaptcha();
      const executePromise = window.grecaptcha.enterprise.execute(SITE_KEY, {
        action: pageAction,
      });
      const timeoutPromise = new Promise((_, reject) =>
        setTimeout(() => reject(new Error('GRECAPTCHA_EXECUTE_TIMEOUT')), 15000)
      );
      const token = await Promise.race([executePromise, timeoutPromise]);

      window.dispatchEvent(new CustomEvent('CAPTCHA_RESULT', {
        detail: { requestId, token },
      }));
    } catch (e) {
      console.error('[FlowAgent] grecaptcha error:', e);
      window.dispatchEvent(new CustomEvent('CAPTCHA_RESULT', {
        detail: { requestId, error: e.message || 'CAPTCHA_EXEC_ERROR' },
      }));
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
})();
