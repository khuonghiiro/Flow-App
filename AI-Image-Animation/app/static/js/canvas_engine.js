/**
 * Real-time 60 FPS 2D Mesh Deformation & Physics Simulation Engine
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
    this.windStrength = 1.0;
    this.waveFrequency = 1.0;
    this.turbulence = 0.4;
    this.flutterScale = 1.0;
    this.duration = 3.0; // seconds

    // Grid mesh settings (24x24 quads = 1152 triangles for locked 60 FPS)
    this.gridCols = 24;
    this.gridRows = 24;

    // Callbacks
    this.onPhaseUpdate = null;
  }

  loadImage(imgElement) {
    this.sourceImage = imgElement;
    const w = imgElement.naturalWidth || imgElement.width;
    const h = imgElement.naturalHeight || imgElement.height;

    // Limit maximum canvas viewport dimension for smooth 60 FPS
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

  getFlowVector(nx, ny) {
    const vectors = window.vectorTools ? window.vectorTools.vectors : [];
    const pins = window.vectorTools ? window.vectorTools.pins : [];

    if (!vectors || vectors.length === 0) {
      return { fx: 0.8, fy: 0.1 };
    }

    let totalFx = 0;
    let totalFy = 0;
    let totalWeight = 1e-5;

    for (const vec of vectors) {
      const vx = (vec.end_x - vec.start_x) * vec.strength;
      const vy = (vec.end_y - vec.start_y) * vec.strength;
      const lenSq = (vec.end_x - vec.start_x) ** 2 + (vec.end_y - vec.start_y) ** 2 + 1e-5;
      const len = Math.sqrt(lenSq);

      const tProj = Math.max(0, Math.min(1, ((nx - vec.start_x) * (vec.end_x - vec.start_x) + (ny - vec.start_y) * (vec.end_y - vec.start_y)) / lenSq));
      const closestX = vec.start_x + tProj * (vec.end_x - vec.start_x);
      const closestY = vec.start_y + tProj * (vec.end_y - vec.start_y);

      const distSq = (nx - closestX) ** 2 + (ny - closestY) ** 2;
      const radius = Math.max(0.12, len * 0.9);
      const spatialW = Math.exp(-distSq / (2.0 * radius * radius));
      const progressiveGain = Math.max(0.2, Math.min(1.4, 0.3 + 0.9 * tProj));

      totalFx += vx * spatialW * progressiveGain;
      totalFy += vy * spatialW * progressiveGain;
      totalWeight += spatialW;
    }

    let fx = totalFx / totalWeight;
    let fy = totalFy / totalWeight;

    // Apply anchor pin dampening
    if (pins && pins.length > 0) {
      for (const pin of pins) {
        const d = Math.hypot(nx - pin.x, ny - pin.y);
        const r = Math.max(0.02, pin.radius);
        if (d < r) {
          const infl = d / r;
          const smooth = infl * infl * (3 - 2 * infl);
          const factor = (1 - pin.weight) + pin.weight * smooth;
          fx *= factor;
          fy *= factor;
        }
      }
    }

    return { fx, fy };
  }

  getMaskValue(px, py, w, h) {
    if (!window.maskPainter || !window.maskPainter.maskCtx) return 1.0;
    try {
      const p = window.maskPainter.maskCtx.getImageData(Math.round(px), Math.round(py), 1, 1).data;
      // White mask (255) = 1.0, Black (0) = 0.0
      return (p[0] / 255.0);
    } catch (e) {
      return 1.0;
    }
  }

  renderFrame(phase) {
    if (!this.sourceImage) return;

    const w = this.prevCanvas.width;
    const h = this.prevCanvas.height;
    const ctx = this.prevCtx;

    // 1. Draw static background
    ctx.clearRect(0, 0, w, h);
    ctx.drawImage(this.sourceImage, 0, 0, w, h);

    const cols = this.gridCols;
    const rows = this.gridRows;
    const cellW = w / cols;
    const cellH = h / rows;

    const freqK = Math.max(1, Math.round(this.waveFrequency));
    const phaseRad = phase * Math.PI * 2 * freqK;
    const maxDisp = 18.0 * this.windStrength;

    // Fast Grid Deformation
    for (let j = 0; j < rows; j++) {
      for (let i = 0; i < cols; i++) {
        const x = i * cellW;
        const y = j * cellH;
        const nx = (x + cellW * 0.5) / w;
        const ny = (y + cellH * 0.5) / h;

        const maskVal = this.getMaskValue(x + cellW * 0.5, y + cellH * 0.5, w, h);
        if (maskVal < 0.05) continue; // Skip static regions for extreme speed

        const { fx, fy } = this.getFlowVector(nx, ny);
        const spatialLag = (nx * fx + ny * fy) * (Math.PI * 2.0);

        const osc = Math.sin(phaseRad - spatialLag * 0.75) + 
                    this.turbulence * (0.35 * Math.sin(phaseRad * 2.0 - spatialLag * 1.5 + 0.5));

        const dx = fx * osc * maxDisp * maskVal;
        const dy = fy * osc * maxDisp * maskVal;

        // Render warped mesh cell
        ctx.save();
        ctx.beginPath();
        ctx.rect(x, y, cellW + 0.5, cellH + 0.5);
        ctx.clip();
        ctx.drawImage(this.sourceImage, x - dx, y - dy, cellW + 1, cellH + 1, x, y, cellW + 1, cellH + 1);
        ctx.restore();
      }
    }
  }
}

window.CanvasEngine = CanvasEngine;

