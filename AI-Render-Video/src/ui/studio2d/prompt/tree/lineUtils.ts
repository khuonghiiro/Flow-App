/**
 * Line utility functions for Skill Tree Canvas
 * Generates orthogonal (square-angle / elbow) and curved (Bézier) SVG paths.
 */

export function getOrthogonalPath(
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  radius = 14
): string {
  const dx = x2 - x1;
  const dy = y2 - y1;

  if (Math.abs(dx) < 6 || Math.abs(dy) < 6) {
    return `M ${x1} ${y1} L ${x2} ${y2}`;
  }

  const midX = x1 + dx * 0.5;
  const signX = dx > 0 ? 1 : -1;
  const signY = dy > 0 ? 1 : -1;
  const r = Math.min(radius, Math.abs(dx) * 0.45, Math.abs(dy) * 0.45);

  if (r <= 2) {
    return `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`;
  }

  return (
    `M ${x1} ${y1} ` +
    `L ${midX - signX * r} ${y1} ` +
    `Q ${midX} ${y1}, ${midX} ${y1 + signY * r} ` +
    `L ${midX} ${y2 - signY * r} ` +
    `Q ${midX} ${y2}, ${midX + signX * r} ${y2} ` +
    `L ${x2} ${y2}`
  );
}

export function getCurvedPath(
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  curvature = 0.45
): string {
  const dx = x2 - x1;
  const ctrl1X = x1 + dx * curvature;
  const ctrl1Y = y1;
  const ctrl2X = x2 - dx * curvature;
  const ctrl2Y = y2;
  return `M ${x1} ${y1} C ${ctrl1X} ${ctrl1Y}, ${ctrl2X} ${ctrl2Y}, ${x2} ${y2}`;
}
