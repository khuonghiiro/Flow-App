/**
 * Wind Vector & Anchor Pin Interactive Overlay Module
 */
class VectorTools {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
    this.vectors = []; // { start_x, start_y, end_x, end_y, strength } in normalized [0, 1]
    this.pins = [];    // { x, y, radius, weight } in normalized [0, 1]
    
    this.isDragging = false;
    this.currentStart = null;
    this.currentEnd = null;
    this.visible = true;
  }

  setSize(width, height) {
    this.canvas.width = width;
    this.canvas.height = height;
    this.redraw();
  }

  clearVectors() {
    this.vectors = [];
    this.redraw();
  }

  clearPins() {
    this.pins = [];
    this.redraw();
  }

  startVector(x, y) {
    this.isDragging = true;
    this.currentStart = {
      x: x / this.canvas.width,
      y: y / this.canvas.height
    };
    this.currentEnd = { ...this.currentStart };
  }

  updateVector(x, y) {
    if (!this.isDragging || !this.currentStart) return;
    this.currentEnd = {
      x: x / this.canvas.width,
      y: y / this.canvas.height
    };
    this.redraw();
  }

  endVector() {
    if (!this.isDragging || !this.currentStart || !this.currentEnd) {
      this.isDragging = false;
      return;
    }

    const dx = this.currentEnd.x - this.currentStart.x;
    const dy = this.currentEnd.y - this.currentStart.y;
    const dist = Math.hypot(dx, dy);

    // Only add if user dragged meaningful distance
    if (dist > 0.02) {
      this.vectors.push({
        start_x: this.currentStart.x,
        start_y: this.currentStart.y,
        end_x: this.currentEnd.x,
        end_y: this.currentEnd.y,
        strength: 1.0
      });
    }

    this.isDragging = false;
    this.currentStart = null;
    this.currentEnd = null;
    this.redraw();
  }

  addPin(x, y) {
    this.pins.push({
      x: x / this.canvas.width,
      y: y / this.canvas.height,
      radius: 0.08,
      weight: 1.0
    });
    this.redraw();
  }

  eraseAt(x, y, radius = 25) {
    const normX = x / this.canvas.width;
    const normY = y / this.canvas.height;
    const normRadius = radius / Math.min(this.canvas.width, this.canvas.height);

    // Filter out vectors close to eraser
    this.vectors = this.vectors.filter(v => {
      const midX = (v.start_x + v.end_x) / 2;
      const midY = (v.start_y + v.end_y) / 2;
      return Math.hypot(midX - normX, midY - normY) > normRadius;
    });

    // Filter out pins close to eraser
    this.pins = this.pins.filter(p => {
      return Math.hypot(p.x - normX, p.y - normY) > normRadius;
    });

    this.redraw();
  }

  toggleVisibility(show) {
    this.visible = show;
    this.canvas.style.display = show ? "block" : "none";
  }

  redraw() {
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    if (!this.visible) return;

    const w = this.canvas.width;
    const h = this.canvas.height;

    // Draw saved vectors
    this.vectors.forEach(v => {
      this._drawArrow(v.start_x * w, v.start_y * h, v.end_x * w, v.end_y * h, "#38bdf8");
    });

    // Draw active drag arrow
    if (this.isDragging && this.currentStart && this.currentEnd) {
      this._drawArrow(
        this.currentStart.x * w,
        this.currentStart.y * h,
        this.currentEnd.x * w,
        this.currentEnd.y * h,
        "#f43f5e",
        true
      );
    }

    // Draw pins
    this.pins.forEach(p => {
      this._drawPin(p.x * w, p.y * h);
    });
  }

  _drawArrow(fromX, fromY, toX, toY, color, isDashed = false) {
    const headLen = 14;
    const angle = Math.atan2(toY - fromY, toX - fromX);

    this.ctx.save();
    this.ctx.strokeStyle = color;
    this.ctx.fillStyle = color;
    this.ctx.lineWidth = 3;
    this.ctx.shadowColor = "rgba(56, 189, 248, 0.6)";
    this.ctx.shadowBlur = 8;

    if (isDashed) {
      this.ctx.setLineDash([6, 4]);
    }

    // Line
    this.ctx.beginPath();
    this.ctx.moveTo(fromX, fromY);
    this.ctx.lineTo(toX, toY);
    this.ctx.stroke();

    // Arrowhead
    this.ctx.setLineDash([]);
    this.ctx.beginPath();
    this.ctx.moveTo(toX, toY);
    this.ctx.lineTo(
      toX - headLen * Math.cos(angle - Math.PI / 6),
      toY - headLen * Math.sin(angle - Math.PI / 6)
    );
    this.ctx.lineTo(
      toX - headLen * Math.cos(angle + Math.PI / 6),
      toY - headLen * Math.sin(angle + Math.PI / 6)
    );
    this.ctx.closePath();
    this.ctx.fill();

    // Start dot
    this.ctx.beginPath();
    this.ctx.arc(fromX, fromY, 4, 0, Math.PI * 2);
    this.ctx.fill();

    this.ctx.restore();
  }

  _drawPin(x, y) {
    this.ctx.save();
    this.ctx.fillStyle = "#f43f5e";
    this.ctx.strokeStyle = "#ffffff";
    this.ctx.lineWidth = 2;
    this.ctx.shadowColor = "rgba(244, 63, 94, 0.8)";
    this.ctx.shadowBlur = 10;

    this.ctx.beginPath();
    this.ctx.arc(x, y, 7, 0, Math.PI * 2);
    this.ctx.fill();
    this.ctx.stroke();

    // Cross in center
    this.ctx.strokeStyle = "#ffffff";
    this.ctx.lineWidth = 1.5;
    this.ctx.beginPath();
    this.ctx.moveTo(x - 3, y);
    this.ctx.lineTo(x + 3, y);
    this.ctx.moveTo(x, y - 3);
    this.ctx.lineTo(x, y + 3);
    this.ctx.stroke();

    this.ctx.restore();
  }
}

window.VectorTools = VectorTools;
