// MV3 service worker (hiện tại chỉ để giữ chỗ; logic chạy trong content-script).
chrome.runtime.onInstalled.addListener(() => {
  // noop
});

chrome.action.onClicked.addListener(async () => {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab && tab.id) {
      const resp = await chrome.tabs.sendMessage(tab.id, { type: 'GVS_TOGGLE_MODAL' });
      if (resp && resp.ok) return;
    }
  } catch {
    // ignore and fallback below
  }

  try {
    await chrome.tabs.create({ url: 'https://grok.com/imagine', active: true });
  } catch {
    // ignore
  }
});

