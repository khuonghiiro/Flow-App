// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================

/**
 * Robust cross-browser image loader that avoids CORS issues with blob: and data: URLs
 */
export function loadSafeImage(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    if (!src) {
      reject(new Error('Empty image src'));
      return;
    }
    const img = new Image();
    // Do NOT set crossOrigin on blob: or data: URLs to avoid CORS rejections in Chromium
    if (!src.startsWith('data:') && !src.startsWith('blob:')) {
      img.crossOrigin = 'anonymous';
    }
    img.onload = () => resolve(img);
    img.onerror = (err) => {
      // If failed with crossOrigin, retry once without crossOrigin
      if (img.crossOrigin) {
        const fallbackImg = new Image();
        fallbackImg.onload = () => resolve(fallbackImg);
        fallbackImg.onerror = (e) => reject(e);
        fallbackImg.src = src;
      } else {
        reject(err);
      }
    };
    img.src = src;
  });
}
