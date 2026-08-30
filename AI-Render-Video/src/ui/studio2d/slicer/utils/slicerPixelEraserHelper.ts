// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================

/**
 * Erases a circular brush stroke on an HTML5 canvas using destination-out
 */
export function eraseBrushStrokeOnCanvas(
  ctx: CanvasRenderingContext2D,
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
  brushRadius: number
): void {
  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  ctx.lineWidth = brushRadius * 2;

  ctx.beginPath();
  ctx.moveTo(fromX, fromY);
  ctx.lineTo(toX, toY);
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(toX, toY, brushRadius, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
}

/**
 * Erases a rectangular selection box on an HTML5 canvas using destination-out
 */
export function eraseBoxSelectionOnCanvas(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number
): void {
  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';
  ctx.fillRect(x, y, width, height);
  ctx.restore();
}

/**
 * Automatically cleans and eliminates any dark/black border lines along the outer boundary edges
 */
export function cleanOuterEdgeDarkBorders(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  borderThickness: number = 3
): void {
  if (width <= 0 || height <= 0) return;
  const imgData = ctx.getImageData(0, 0, width, height);
  const data = imgData.data;
  const t = Math.max(1, Math.min(borderThickness, Math.floor(Math.min(width, height) / 4)));

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      // Check if pixel is within borderThickness of any edge
      const isEdge = x < t || x >= width - t || y < t || y >= height - t;
      if (!isEdge) continue;

      const idx = (y * width + x) * 4;
      const a = data[idx + 3];
      if (a === 0) continue;

      const r = data[idx];
      const g = data[idx + 1];
      const b = data[idx + 2];

      // If pixel is dark / black line / grey divider border
      if (r < 55 && g < 55 && b < 55) {
        data[idx + 3] = 0; // eliminate completely
      }
    }
  }

  ctx.putImageData(imgData, 0, 0);
}
