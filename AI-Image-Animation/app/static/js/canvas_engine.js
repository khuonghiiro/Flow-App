/**
 * Real-time 60 FPS Browser Physics Preview Simulation Engine
 */
class CanvasEngine {
  constructor(imageCanvas, previewCanvas) {
    this.imgCanvas = imageCanvas;
    this.prevCanvas = previewCanvas;
    this.imgCtx = imageCanvas.getContext("2d");
    this.prevCtx = previewCanvas.getContext("2d");

    this.sourceImage = null;
    this.isPlaying = false;
    this.animationId = null;
    
    // Physics parameters
    this.phase = 0.0;
    this.windStrength = 1.2;
    this.waveFrequency = 1.8;
    this.turbulence = 0.6;
    this.flutterScale = 1.4;
    this.duration = 3.0; // seconds

    // Callbacks
    this.onPhaseUpdate = null;
  }

  loadImage(imgElement) {
    this.sourceImage = imgElement;
    const w = imgElement.naturalWidth || imgElement.width;
    const h = imgElement.naturalHeight || imgElement.height;

    // Limit maximum canvas viewport dimension to maintain 60 FPS
    const maxDim = 800;
    let dispW = w;
    let dispH = h;

    if (Math.max(w, h) > maxDim) {
      const scale = maxDim / Math.max(w, h);
      dispW = Math.round(w * scale);
      dispH = Math.round(h * scale);
    }

    this.imgCanvas.width = dispW;
    this.imgCanvas.height = dispH;
    this.prevCanvas.width = dispW;
    this.prevCanvas.height = dispH;

    this.imgCtx.clearRect(0, 0, dispW, dispH);
    this.imgCtx.drawImage(imgElement, 0, 0, dispW, dispH);
  }

  setPhysics(params) {
    if (params.windStrength !== undefined) this.windStrength = params.windStrength;
    if (params.waveFrequency !== undefined) this.waveFrequency = params.waveFrequency;
    if (params.turbulence !== undefined) this.turbulence = params.turbulence;
    if (params.flutterScale !== undefined) this.flutterScale = params.flutterScale;
    if (params.duration !== undefined) this.duration = params.duration;
  }

  play() {
    if (this.isPlaying) return;
    this.isPlaying = true;
    this.prevCanvas.style.display = "block";
    this.lastTime = performance.now();
    this.animateLoop();
  }

  pause() {
    this.isPlaying = false;
    if (this.animationId) {
      cancelAnimationFrame(this.animationId);
      this.animationId = null;
    }
  }

  setPhase(p) {
    this.phase = Math.max(0.0, Math.min(1.0, p));
    this.renderFrame(this.phase);
  }

  animateLoop() {
    if (!this.isPlaying) return;

    const now = performance.now();
    const dt = (now - this.lastTime) / 1000.0;
    this.lastTime = now;

    this.phase = (this.phase + dt / this.duration) % 1.0;
    this.renderFrame(this.phase);

    if (this.onPhaseUpdate) {
      this.onPhaseUpdate(this.phase);
    }

    this.animationId = requestAnimationFrame(() => this.animateLoop());
  }

  renderFrame(phase) {
    if (!this.sourceImage) return;

    const w = this.prevCanvas.width;
    const h = this.prevCanvas.height;

    // Fast multi-harmonic dynamic warp simulation
    this.prevCtx.clearRect(0, 0, w, h);

    // Subtle breathing/hair sway transform
    const angle = Math.sin(phase * Math.PI * 2 * this.waveFrequency) * (0.015 * this.windStrength);
    const scaleX = 1.0 + Math.sin(phase * Math.PI * 2 * this.waveFrequency * 1.5) * (0.008 * this.flutterScale);
    const shiftX = Math.sin(phase * Math.PI * 2 * this.waveFrequency) * (4.0 * this.windStrength);
    const shiftY = Math.cos(phase * Math.PI * 2 * this.waveFrequency * 0.8) * (2.0 * this.turbulence);

    this.prevCtx.save();
    this.prevCtx.translate(w / 2 + shiftX, h / 2 + shiftY);
    this.prevCtx.rotate(angle);
    this.prevCtx.scale(scaleX, 1.0);
    this.prevCtx.drawImage(this.sourceImage, -w / 2, -h / 2, w, h);
    this.prevCtx.restore();
  }
}

window.CanvasEngine = CanvasEngine;
