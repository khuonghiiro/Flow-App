/**
 * Interactive 3D/Spherical Angle Compass Gizmo
 */
class OrbitGizmo {
  constructor(canvasId, onChange) {
    this.canvas = document.getElementById(canvasId);
    if (!this.canvas) return;
    this.ctx = this.canvas.getContext('2d');
    this.onChange = onChange || (() => {});

    this.azimuth = 0; // -180 to 180 or 0 to 360
    this.elevation = 0; // -60 to 60
    this.isDragging = false;

    this.initCanvas();
    this.bindEvents();
    this.render();
  }

  initCanvas() {
    const dpr = window.devicePixelRatio || 1;
    this.width = 140;
    this.height = 140;
    this.canvas.width = this.width * dpr;
    this.canvas.height = this.height * dpr;
    this.ctx.scale(dpr, dpr);
  }

  setAngles(azimuth, elevation) {
    this.azimuth = azimuth;
    this.elevation = elevation;
    this.render();
  }

  bindEvents() {
    const onMove = (e) => {
      if (!this.isDragging) return;
      const rect = this.canvas.getBoundingClientRect();
      const clientX = e.clientX || (e.touches && e.touches[0].clientX);
      const clientY = e.clientY || (e.touches && e.touches[0].clientY);

      const x = clientX - rect.left - this.width / 2;
      const y = clientY - rect.top - this.height / 2;

      let az = Math.atan2(y, x) * (180 / Math.PI) + 90;
      if (az < 0) az += 360;

      const dist = Math.sqrt(x * x + y * y);
      const maxR = this.width / 2 - 10;
      const el = Math.max(-60, Math.min(60, (1 - dist / maxR) * 60));

      this.azimuth = Math.round(az);
      this.elevation = Math.round(el);
      this.render();
      this.onChange(this.azimuth, this.elevation);
    };

    this.canvas.addEventListener('mousedown', (e) => { this.isDragging = true; onMove(e); });
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', () => { this.isDragging = false; });

    this.canvas.addEventListener('touchstart', (e) => { this.isDragging = true; onMove(e); }, { passive: false });
    window.addEventListener('touchmove', onMove);
    window.addEventListener('touchend', () => { this.isDragging = false; });
  }

  render() {
    const ctx = this.ctx;
    const cx = this.width / 2;
    const cy = this.height / 2;
    const radius = cx - 12;

    ctx.clearRect(0, 0, this.width, this.height);

    // Outer Orbital Track
    ctx.beginPath();
    ctx.arc(cx, cy, radius, 0, Math.PI * 2);
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Cardinal Ticks
    const labels = ['0° (F)', '90° (R)', '180° (B)', '270° (L)'];
    const angles = [-Math.PI / 2, 0, Math.PI / 2, Math.PI];
    ctx.font = '9px monospace';
    ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';

    angles.forEach((ang, idx) => {
      const tx = cx + (radius - 14) * Math.cos(ang);
      const ty = cy + (radius - 14) * Math.sin(ang);
      ctx.fillText(labels[idx], tx, ty);
    });

    // Center Cross
    ctx.beginPath();
    ctx.arc(cx, cy, 3, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
    ctx.fill();

    // Active Angle Pointer Vector
    const rad = (this.azimuth - 90) * (Math.PI / 180);
    const pointerRadius = radius * (1 - (this.elevation / 120));
    const px = cx + pointerRadius * Math.cos(rad);
    const py = cy + pointerRadius * Math.sin(rad);

    // Pointer Line
    ctx.beginPath();
    ctx.moveTo(cx, cy);
    ctx.lineTo(px, py);
    ctx.strokeStyle = 'rgba(0, 242, 254, 0.6)';
    ctx.lineWidth = 2;
    ctx.setLineDash([3, 3]);
    ctx.stroke();
    ctx.setLineDash([]);

    // Glowing Node
    ctx.beginPath();
    ctx.arc(px, py, 7, 0, Math.PI * 2);
    ctx.fillStyle = '#00f2fe';
    ctx.shadowColor = '#00f2fe';
    ctx.shadowBlur = 10;
    ctx.fill();
    ctx.shadowBlur = 0;
  }
}

window.OrbitGizmo = OrbitGizmo;
