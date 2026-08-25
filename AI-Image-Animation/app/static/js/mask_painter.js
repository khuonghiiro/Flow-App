/**
 * Hair & Cloth Mask Painter Module
 */
class MaskPainter {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
    this.isPainting = false;
    this.brushSize = 30;
    this.isEraser = false;
    this.lastX = 0;
    this.lastY = 0;
    this.visible = true;
  }

  setSize(width, height) {
    this.canvas.width = width;
    this.canvas.height = height;
    this.clear();
  }

  clear() {
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
  }

  startStroke(x, y) {
    this.isPainting = true;
    this.lastX = x;
    this.lastY = y;
    this.drawCircle(x, y);
  }

  drawCircle(x, y) {
    this.ctx.save();
    if (this.isEraser) {
      this.ctx.globalCompositeOperation = "destination-out";
      this.ctx.fillStyle = "rgba(0,0,0,1)";
    } else {
      this.ctx.globalCompositeOperation = "source-over";
      this.ctx.fillStyle = "rgba(236, 72, 153, 0.65)"; // Radiant Pink Mask
    }
    
    this.ctx.beginPath();
    this.ctx.arc(x, y, this.brushSize / 2, 0, Math.PI * 2);
    this.ctx.fill();
    this.ctx.restore();
  }

  continueStroke(x, y) {
    if (!this.isPainting) return;

    this.ctx.save();
    if (this.isEraser) {
      this.ctx.globalCompositeOperation = "destination-out";
      this.ctx.strokeStyle = "rgba(0,0,0,1)";
    } else {
      this.ctx.globalCompositeOperation = "source-over";
      this.ctx.strokeStyle = "rgba(236, 72, 153, 0.65)";
    }

    this.ctx.lineWidth = this.brushSize;
    this.ctx.lineCap = "round";
    this.ctx.lineJoin = "round";

    this.ctx.beginPath();
    this.ctx.moveTo(this.lastX, this.lastY);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
    this.ctx.restore();

    this.lastX = x;
    this.lastY = y;
  }

  endStroke() {
    this.isPainting = false;
  }

  toggleVisibility(show) {
    this.visible = show;
    this.canvas.style.display = show ? "block" : "none";
  }

  getMaskBase64() {
    // Generate a clean grayscale mask (white = animate, black = static)
    const tempCanvas = document.createElement("canvas");
    tempCanvas.width = this.canvas.width;
    tempCanvas.height = this.canvas.height;
    const tempCtx = tempCanvas.getContext("2d");

    // Fill black background
    tempCtx.fillStyle = "#000000";
    tempCtx.fillRect(0, 0, tempCanvas.width, tempCanvas.height);

    // Draw white where painted
    tempCtx.drawImage(this.canvas, 0, 0);
    const imgData = tempCtx.getImageData(0, 0, tempCanvas.width, tempCanvas.height);
    const data = imgData.data;

    let hasPaint = false;
    for (let i = 0; i < data.length; i += 4) {
      const alpha = data[i + 3];
      if (alpha > 10) {
        data[i] = 255;     // R
        data[i + 1] = 255; // G
        data[i + 2] = 255; // B
        data[i + 3] = 255; // A
        hasPaint = true;
      } else {
        data[i] = 0;
        data[i + 1] = 0;
        data[i + 2] = 0;
        data[i + 3] = 255;
      }
    }

    if (!hasPaint) return null; // Animate whole image if empty

    tempCtx.putImageData(imgData, 0, 0);
    return tempCanvas.toDataURL("image/png");
  }
}

window.MaskPainter = MaskPainter;
