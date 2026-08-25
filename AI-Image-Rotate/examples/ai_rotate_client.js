/**
 * Standalone JavaScript Client SDK for AI Image Rotate API (Port 3978)
 * Use this in ANY external web application to easily generate & embed 360-degree rotations.
 */
class AIRotateSDK {
  constructor(serverUrl = 'http://localhost:3978') {
    this.serverUrl = serverUrl.replace(/\/$/, '');
  }

  /**
   * Generates a single novel view angle
   * @param {Object} options
   * @param {string} options.imageBase64
   * @param {number} options.azimuthDeg - Horizontal angle (0-360)
   * @param {number} options.elevationDeg - Vertical angle (-45 to 45)
   * @param {boolean} options.removeBackground
   */
  async rotateSingle({ imageBase64, azimuthDeg = 0, elevationDeg = 0, removeBackground = true }) {
    const res = await fetch(`${this.serverUrl}/api/rotate/single`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        image_base64: imageBase64,
        azimuth_deg: azimuthDeg,
        elevation_deg: elevationDeg,
        remove_background: removeBackground
      })
    });
    if (!res.ok) throw new Error(`Rotation failed: ${res.statusText}`);
    return await res.json();
  }

  /**
   * Generates full 360-degree orbital sequence
   * @param {Object} options
   * @param {string} options.imageBase64
   * @param {number} options.numFrames - e.g. 16, 24, 36
   * @param {number} options.elevationDeg
   * @param {boolean} options.removeBackground
   */
  async rotateTurntable({ imageBase64, numFrames = 16, elevationDeg = 0, removeBackground = true }) {
    const res = await fetch(`${this.serverUrl}/api/rotate/turntable`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        image_base64: imageBase64,
        num_frames: numFrames,
        elevation_deg: elevationDeg,
        remove_background: removeBackground,
        generate_gif: true,
        generate_spritesheet: true
      })
    });
    if (!res.ok) throw new Error(`Turntable failed: ${res.statusText}`);
    return await res.json();
  }

  /**
   * Embeds a lightweight 360 interactive turntable into any DOM element
   * @param {HTMLElement} container
   * @param {Array} frames
   */
  embedViewer(container, frames) {
    if (!container || !frames || frames.length === 0) return;
    container.innerHTML = '';
    container.style.position = 'relative';
    container.style.overflow = 'hidden';
    container.style.userSelect = 'none';
    container.style.cursor = 'grab';

    const img = document.createElement('img');
    img.src = frames[0].image_base64;
    img.style.width = '100%';
    img.style.height = '100%';
    img.style.objectFit = 'contain';
    img.style.pointerEvents = 'none';
    container.appendChild(img);

    let idx = 0;
    let isDragging = false;
    let startX = 0;

    const setFrame = (newIdx) => {
      idx = (newIdx + frames.length) % frames.length;
      img.src = frames[idx].image_base64;
    };

    container.addEventListener('mousedown', (e) => { isDragging = true; startX = e.clientX; container.style.cursor = 'grabbing'; });
    window.addEventListener('mousemove', (e) => {
      if (!isDragging) return;
      const dx = e.clientX - startX;
      if (Math.abs(dx) > 15) {
        setFrame(idx + (dx > 0 ? 1 : -1));
        startX = e.clientX;
      }
    });
    window.addEventListener('mouseup', () => { isDragging = false; container.style.cursor = 'grab'; });
  }
}

// Export for module and global
if (typeof module !== 'undefined' && module.exports) {
  module.exports = AIRotateSDK;
} else {
  window.AIRotateSDK = AIRotateSDK;
}
