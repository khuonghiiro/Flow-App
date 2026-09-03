/**
 * Content script — bridge between background.js and injected.js
 * Injects injected.js into MAIN world to access window.grecaptcha
 */
(function () {
  if (window.__FLOWKIT_CONTENT_LOADED__) {
    return;
  }
  window.__FLOWKIT_CONTENT_LOADED__ = true;

  function ensureInjected() {
    if (document.getElementById('flowkit-injected-script')) return;
    const s = document.createElement('script');
    s.id = 'flowkit-injected-script';
    s.src = chrome.runtime.getURL('injected.js');
    s.onload = () => s.remove();
    (document.head || document.documentElement).appendChild(s);
  }

  ensureInjected();

  chrome.runtime.onMessage.addListener((msg, _, reply) => {
    if (msg.type !== 'GET_CAPTCHA') return;

    const { requestId, pageAction } = msg;
    ensureInjected();

    let replied = false;
    const cleanUp = () => {
      window.removeEventListener('CAPTCHA_RESULT', customEventHandler);
      window.removeEventListener('message', messageHandler);
      clearTimeout(timer);
    };

    const handleResult = (token, error) => {
      if (replied) return;
      replied = true;
      cleanUp();
      reply({ token, error });
    };

    const customEventHandler = (e) => {
      const detail = e.detail || {};
      if (detail.requestId === requestId) {
        handleResult(detail.token, detail.error);
      }
    };

    const messageHandler = (e) => {
      if (e.data && e.data.type === 'CAPTCHA_RESULT' && e.data.requestId === requestId) {
        handleResult(e.data.token, e.data.error);
      }
    };

    const timer = setTimeout(() => {
      if (!replied) {
        replied = true;
        cleanUp();
        reply({ error: 'CONTENT_TIMEOUT' });
      }
    }, 25000);

    window.addEventListener('CAPTCHA_RESULT', customEventHandler);
    window.addEventListener('message', messageHandler);

    // Send via both postMessage and CustomEvent for 100% reliable cross-world dispatch
    window.postMessage({ type: 'GET_CAPTCHA', requestId, pageAction }, '*');
    window.dispatchEvent(new CustomEvent('GET_CAPTCHA', {
      detail: { requestId, pageAction },
    }));

    return true; // keep channel open for async reply
  });

  // ─── TRPC Media URL Monitor ─────────────────────────────────
  const forwardTrpc = (url, body) => {
    if (!body) return;
    chrome.runtime.sendMessage({
      type: 'TRPC_MEDIA_URLS',
      trpcUrl: url,
      body,
    }).catch(() => {});
  };

  window.addEventListener('TRPC_MEDIA_URLS', (e) => {
    const { url, body } = e.detail || {};
    forwardTrpc(url, body);
  });

  window.addEventListener('message', (e) => {
    if (e.data && e.data.type === 'TRPC_MEDIA_URLS') {
      forwardTrpc(e.data.url, e.data.body);
    }
  });
})();
