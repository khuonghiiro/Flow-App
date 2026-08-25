/**
 * Interactive 360 Turntable Viewport Controller
 */
class TurntableViewer {
  constructor(stageId, imageId, hudId) {
    this.stage = document.getElementById(stageId);
    this.imgEl = document.getElementById(imageId);
    this.hudEl = document.getElementById(hudId);

    this.frames = []; // Array of { index, azimuth, image_base64 }
    this.currentIndex = 0;
    this.isDragging = false;
    this.startX = 0;
    this.currentAzimuth = 0;
    this.isAutoSpinning = false;
    this.autoSpinTimer = null;
    this.spinSpeed = 100; // ms per frame

    this.bindEvents();
  }

  loadFrames(frames) {
    this.frames = frames || [];
    this.currentIndex = 0;
    if (this.frames.length > 0) {
      this.updateDisplay(0);
    }
  }

  updateDisplay(index) {
    if (!this.frames || this.frames.length === 0) return;
    this.currentIndex = (index + this.frames.length) % this.frames.length;
    const currentFrame = this.frames[this.currentIndex];

    if (this.imgEl && currentFrame) {
      this.imgEl.src = currentFrame.image_base64;
      this.currentAzimuth = currentFrame.azimuth_deg;
    }

    if (this.hudEl) {
      this.hudEl.textContent = `AZIMUTH: ${Math.round(this.currentAzimuth)}° | FRAME: ${this.currentIndex + 1}/${this.frames.length}`;
    }

    // Trigger frame change callback if gallery registered
    if (window.galleryManager) {
      window.galleryManager.highlightFrame(this.currentIndex);
    }
  }

  nextFrame() {
    this.updateDisplay(this.currentIndex + 1);
  }

  prevFrame() {
    this.updateDisplay(this.currentIndex - 1);
  }

  toggleAutoSpin() {
    this.isAutoSpinning = !this.isAutoSpinning;
    if (this.isAutoSpinning) {
      this.startAutoSpin();
    } else {
      this.stopAutoSpin();
    }
    return this.isAutoSpinning;
  }

  startAutoSpin() {
    this.stopAutoSpin();
    this.isAutoSpinning = true;
    this.autoSpinTimer = setInterval(() => {
      this.nextFrame();
    }, this.spinSpeed);
  }

  stopAutoSpin() {
    this.isAutoSpinning = false;
    if (this.autoSpinTimer) {
      clearInterval(this.autoSpinTimer);
      this.autoSpinTimer = null;
    }
  }

  bindEvents() {
    if (!this.stage) return;

    const onStart = (clientX) => {
      if (this.frames.length <= 1) return;
      this.isDragging = true;
      this.startX = clientX;
      this.stage.classList.add('grabbing');
      if (this.isAutoSpinning) this.stopAutoSpin();
    };

    const onMove = (clientX) => {
      if (!this.isDragging || this.frames.length <= 1) return;
      const deltaX = clientX - this.startX;
      const sensitivity = 15; // Pixels per frame step

      if (Math.abs(deltaX) >= sensitivity) {
        const steps = Math.floor(deltaX / sensitivity);
        this.updateDisplay(this.currentIndex + steps);
        this.startX = clientX;
      }
    };

    const onEnd = () => {
      this.isDragging = false;
      this.stage.classList.remove('grabbing');
    };

    // Mouse events
    this.stage.addEventListener('mousedown', (e) => onStart(e.clientX));
    window.addEventListener('mousemove', (e) => onMove(e.clientX));
    window.addEventListener('mouseup', onEnd);

    // Touch events
    this.stage.addEventListener('touchstart', (e) => {
      if (e.touches.length === 1) onStart(e.touches[0].clientX);
    }, { passive: true });
    window.addEventListener('touchmove', (e) => {
      if (e.touches.length === 1) onMove(e.touches[0].clientX);
    }, { passive: true });
    window.addEventListener('touchend', onEnd);
  }
}

window.TurntableViewer = TurntableViewer;
