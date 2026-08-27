/**
 * MeshDeformer2D — Moving Least Squares (MLS) rigid deformation engine.
 *
 * Pipeline:
 * 1. Generate a triangle mesh over the image (simple grid tessellation)
 * 2. Compute skinning weights (bone influence per vertex)
 * 3. When bones rotate → compute new control point positions
 * 4. Apply MLS rigid deformation to warp all mesh vertices
 * 5. Render deformed mesh triangles with texture mapping
 *
 * No external dependencies. Pure TypeScript math.
 */

// ─── Types ────────────────────────────────────────────────────────

export interface Vec2 {
  x: number;
  y: number;
}

export interface MeshTriangle {
  /** Indices into the vertex array */
  a: number;
  b: number;
  c: number;
}

export interface MeshVertex {
  /** Original (rest) position in pixel space */
  ox: number;
  oy: number;
  /** Deformed position in pixel space */
  dx: number;
  dy: number;
  /** UV texture coordinates (0..1) */
  u: number;
  v: number;
}

export interface SkinWeight {
  boneId: string;
  weight: number;
}

export interface ControlPoint {
  id: string;
  /** Rest position (original bone joint position in pixels) */
  restX: number;
  restY: number;
  /** Current (posed) position in pixels */
  posedX: number;
  posedY: number;
}

// ─── Grid Mesh Generation ─────────────────────────────────────────

/**
 * Generate a regular grid mesh over a rectangular region.
 * Returns vertices + triangles suitable for texture-mapped deformation.
 *
 * @param width - Image width in pixels
 * @param height - Image height in pixels
 * @param cols - Number of grid columns (more = finer deformation, slower)
 * @param rows - Number of grid rows
 */
export function generateGridMesh(
  width: number,
  height: number,
  cols: number = 20,
  rows: number = 25
): { vertices: MeshVertex[]; triangles: MeshTriangle[] } {
  const vertices: MeshVertex[] = [];
  const triangles: MeshTriangle[] = [];

  // Generate vertices
  for (let row = 0; row <= rows; row++) {
    for (let col = 0; col <= cols; col++) {
      const u = col / cols;
      const v = row / rows;
      vertices.push({
        ox: u * width,
        oy: v * height,
        dx: u * width,
        dy: v * height,
        u,
        v,
      });
    }
  }

  // Generate triangles (2 per grid cell)
  for (let row = 0; row < rows; row++) {
    for (let col = 0; col < cols; col++) {
      const i = row * (cols + 1) + col;
      // Top-left triangle
      triangles.push({ a: i, b: i + 1, c: i + cols + 1 });
      // Bottom-right triangle
      triangles.push({ a: i + 1, b: i + cols + 2, c: i + cols + 1 });
    }
  }

  return { vertices, triangles };
}

// ─── Skinning Weight Computation ──────────────────────────────────

/**
 * Compute skinning weights for each vertex based on distance to control points.
 * Uses inverse-distance weighting with exponential falloff.
 *
 * @param vertices - Mesh vertices
 * @param controlPoints - Bone joint positions (rest pose)
 * @param falloff - Distance falloff exponent (higher = more localized influence)
 * @returns Array of weight arrays, one per vertex
 */
export function computeSkinningWeights(
  vertices: MeshVertex[],
  controlPoints: ControlPoint[],
  falloff: number = 2.5
): SkinWeight[][] {
  if (controlPoints.length === 0) {
    return vertices.map(() => []);
  }

  return vertices.map((vtx) => {
    const rawWeights: { id: string; w: number }[] = [];
    let totalWeight = 0;

    for (const cp of controlPoints) {
      const dx = vtx.ox - cp.restX;
      const dy = vtx.oy - cp.restY;
      const dist = Math.sqrt(dx * dx + dy * dy) + 1e-6; // avoid div-by-zero
      const w = 1.0 / Math.pow(dist, falloff);
      rawWeights.push({ id: cp.id, w });
      totalWeight += w;
    }

    // Normalize to sum = 1
    return rawWeights
      .map((rw) => ({ boneId: rw.id, weight: rw.w / totalWeight }))
      .filter((sw) => sw.weight > 0.001); // prune tiny weights for perf
  });
}

// ─── MLS Rigid Deformation ────────────────────────────────────────

/**
 * Apply Moving Least Squares (Rigid) deformation to mesh vertices.
 *
 * For each vertex, compute the optimal rigid transformation (rotation + translation)
 * that best maps the control points from rest → posed positions, weighted by distance.
 *
 * This is the core deformation function.
 *
 * @param vertices - Mesh vertices (modified in-place: dx, dy updated)
 * @param controlPoints - Control points with rest and posed positions
 * @param alpha - Weighting exponent (higher = more localized deformation)
 */
export function applyMLSRigidDeformation(
  vertices: MeshVertex[],
  controlPoints: ControlPoint[],
  alpha: number = 2.0
): void {
  if (controlPoints.length === 0) return;

  // Single control point → pure translation
  if (controlPoints.length === 1) {
    const cp = controlPoints[0];
    const tx = cp.posedX - cp.restX;
    const ty = cp.posedY - cp.restY;
    for (const vtx of vertices) {
      vtx.dx = vtx.ox + tx;
      vtx.dy = vtx.oy + ty;
    }
    return;
  }

  for (const vtx of vertices) {
    // Step 1: Compute per-control-point weights for this vertex
    const weights: number[] = [];
    let weightSum = 0;

    for (const cp of controlPoints) {
      const dx = vtx.ox - cp.restX;
      const dy = vtx.oy - cp.restY;
      const distSq = dx * dx + dy * dy;
      // Avoid division by zero when vertex is exactly on a control point
      if (distSq < 0.01) {
        vtx.dx = cp.posedX;
        vtx.dy = cp.posedY;
        weights.length = 0; // signal: skip normal computation
        break;
      }
      const w = 1.0 / Math.pow(distSq, alpha / 2);
      weights.push(w);
      weightSum += w;
    }

    if (weights.length === 0) continue; // vertex was on a control point

    // Step 2: Compute weighted centroids
    let pStarX = 0, pStarY = 0; // weighted centroid of rest positions
    let qStarX = 0, qStarY = 0; // weighted centroid of posed positions

    for (let i = 0; i < controlPoints.length; i++) {
      const w = weights[i] / weightSum;
      pStarX += w * controlPoints[i].restX;
      pStarY += w * controlPoints[i].restY;
      qStarX += w * controlPoints[i].posedX;
      qStarY += w * controlPoints[i].posedY;
    }

    // Step 3: Compute the optimal rotation angle
    // Using the SVD-free rigid MLS formula
    let mu_s = 0; // sum of w_i * |p_hat_i|^2
    let rotCos = 0, rotSin = 0;

    for (let i = 0; i < controlPoints.length; i++) {
      const w = weights[i];
      const pHatX = controlPoints[i].restX - pStarX;
      const pHatY = controlPoints[i].restY - pStarY;
      const qHatX = controlPoints[i].posedX - qStarX;
      const qHatY = controlPoints[i].posedY - qStarY;

      mu_s += w * (pHatX * pHatX + pHatY * pHatY);

      // Cross terms for rotation
      rotCos += w * (pHatX * qHatX + pHatY * qHatY);
      rotSin += w * (pHatX * qHatY - pHatY * qHatX);
    }

    // Normalize rotation vector
    const rotLen = Math.sqrt(rotCos * rotCos + rotSin * rotSin);
    if (rotLen > 1e-8) {
      rotCos /= rotLen;
      rotSin /= rotLen;
    } else {
      rotCos = 1;
      rotSin = 0;
    }

    // Step 4: Apply rigid transform to vertex
    const vHatX = vtx.ox - pStarX;
    const vHatY = vtx.oy - pStarY;

    // Rotate v_hat and translate to new centroid
    vtx.dx = (rotCos * vHatX - rotSin * vHatY) + qStarX;
    vtx.dy = (rotSin * vHatX + rotCos * vHatY) + qStarY;
  }
}

// ─── Bone Pose → Control Points ───────────────────────────────────

import { BoneNode, BoneRigDefinition } from '../../types/scene2d';
import { computeForwardKinematics, BoneWorldTransform } from './BoneRig2DEngine';

/**
 * Convert bone rig rest pose to control points.
 * Uses FK to compute world positions of all joints.
 */
export function boneRigToControlPoints(
  rig: BoneRigDefinition,
  canvasW: number,
  canvasH: number
): ControlPoint[] {
  const transforms = computeForwardKinematics(rig.bones, canvasW, canvasH);
  return transforms.map((t) => ({
    id: t.boneId,
    restX: t.worldX,
    restY: t.worldY,
    posedX: t.worldX,
    posedY: t.worldY,
  }));
}

/**
 * Compute posed control points given bone rotation overrides.
 * Each entry in `poseOverrides` maps boneId → rotation delta (degrees).
 */
export function computePosedControlPoints(
  rig: BoneRigDefinition,
  poseOverrides: Record<string, number>,
  canvasW: number,
  canvasH: number
): ControlPoint[] {
  // 1. Rest pose positions
  const restTransforms = computeForwardKinematics(rig.bones, canvasW, canvasH);

  // 2. Posed bones: apply rotation overrides
  const posedBones: BoneNode[] = rig.bones.map((bone) => {
    const override = poseOverrides[bone.id];
    if (override !== undefined) {
      return { ...bone, rotation: bone.rotation + override };
    }
    return bone;
  });

  // 3. Compute posed world positions via FK
  const posedTransforms = computeForwardKinematics(posedBones, canvasW, canvasH);

  // 4. Build control points with rest + posed positions
  return restTransforms.map((rest) => {
    const posed = posedTransforms.find((p) => p.boneId === rest.boneId);
    return {
      id: rest.boneId,
      restX: rest.worldX,
      restY: rest.worldY,
      posedX: posed ? posed.worldX : rest.worldX,
      posedY: posed ? posed.worldY : rest.worldY,
    };
  });
}

// ─── Canvas Mesh Rendering ────────────────────────────────────────

/**
 * Render a deformed triangle mesh with texture mapping onto a canvas.
 * Uses Canvas 2D drawImage with clipping + affine transform per triangle.
 *
 * Strategy: For each triangle, compute affine transform that maps
 * from the UN-DEFORMED mesh positions (ox, oy) to the DEFORMED positions (dx, dy),
 * then use drawImage to draw the original image region.
 */
export function renderDeformedMesh(
  ctx: CanvasRenderingContext2D,
  textureImg: HTMLImageElement,
  vertices: MeshVertex[],
  triangles: MeshTriangle[],
  imgDrawRect: { x: number; y: number; w: number; h: number }
): void {
  const { x: imgX, y: imgY, w: imgW, h: imgH } = imgDrawRect;
  const texW = textureImg.width;
  const texH = textureImg.height;

  for (const tri of triangles) {
    const va = vertices[tri.a];
    const vb = vertices[tri.b];
    const vc = vertices[tri.c];

    // Destination triangle (deformed, in canvas coords)
    const dx0 = imgX + va.dx, dy0 = imgY + va.dy;
    const dx1 = imgX + vb.dx, dy1 = imgY + vb.dy;
    const dx2 = imgX + vc.dx, dy2 = imgY + vc.dy;

    // Source triangle (original, in canvas coords - where the image was drawn)
    const sx0 = imgX + va.ox, sy0 = imgY + va.oy;
    const sx1 = imgX + vb.ox, sy1 = imgY + vb.oy;
    const sx2 = imgX + vc.ox, sy2 = imgY + vc.oy;

    // Compute affine transform: source triangle → destination triangle
    // M maps (sx, sy) → (dx, dy)
    const sdu1 = sx1 - sx0, sdv1 = sy1 - sy0;
    const sdu2 = sx2 - sx0, sdv2 = sy2 - sy0;
    const ddu1 = dx1 - dx0, ddv1 = dy1 - dy0;
    const ddu2 = dx2 - dx0, ddv2 = dy2 - dy0;

    const det = sdu1 * sdv2 - sdv1 * sdu2;
    if (Math.abs(det) < 1e-6) continue; // skip degenerate

    const invDet = 1 / det;
    const m11 = (sdv2 * ddu1 - sdv1 * ddu2) * invDet;
    const m21 = (sdu1 * ddu2 - sdu2 * ddu1) * invDet;
    const m12 = (sdv2 * ddv1 - sdv1 * ddv2) * invDet;
    const m22 = (sdu1 * ddv2 - sdu2 * ddv1) * invDet;
    const tx = dx0 - m11 * sx0 - m21 * sy0;
    const ty = dy0 - m12 * sx0 - m22 * sy0;

    ctx.save();
    ctx.setTransform(1, 0, 0, 1, 0, 0);

    // Clip to destination triangle
    ctx.beginPath();
    ctx.moveTo(dx0, dy0);
    ctx.lineTo(dx1, dy1);
    ctx.lineTo(dx2, dy2);
    ctx.closePath();
    ctx.clip();

    // Apply affine transform: maps source canvas coords → dest canvas coords
    // Then draw the image at its original position (imgX, imgY) scaled to (imgW, imgH)
    ctx.setTransform(m11, m12, m21, m22, tx, ty);
    ctx.drawImage(textureImg, imgX, imgY, imgW, imgH);

    ctx.restore();
  }
}
