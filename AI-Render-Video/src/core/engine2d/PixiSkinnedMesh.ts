/**
 * PixiSkinnedMesh — PixiJS-based 2D skeletal mesh deformation engine.
 * Uses PixiJS SimpleMesh for GPU-accelerated vertex manipulation.
 *
 * Features:
 * - Triangle grid mesh generation with UV mapping
 * - Linear Blend Skinning (LBS) with bone weights
 * - Diamond-shaped bone visualization
 * - Pose-driven vertex deformation
 */
import * as PIXI from 'pixi.js';
import { BoneNode, BoneRigDefinition } from '../../types/scene2d';
import { computeForwardKinematics, BoneWorldTransform } from './BoneRig2DEngine';

// ─── Types ────────────────────────────────────────────────────────

/** Skinning weight: which bones influence a vertex and by how much */
export interface SkinWeight {
  boneIds: string[];
  weights: number[];
}

/** Mesh data for skinning */
export interface SkinnedMeshData {
  /** Original vertex positions [x0,y0, x1,y1, ...] */
  restVertices: Float32Array;
  /** Current deformed positions */
  vertices: Float32Array;
  /** UV coordinates [u0,v0, u1,v1, ...] */
  uvs: Float32Array;
  /** Triangle indices [a0,b0,c0, a1,b1,c1, ...] */
  indices: Uint16Array;
  /** Skinning weights per vertex */
  skinWeights: SkinWeight[];
  /** Number of columns/rows in the grid */
  cols: number;
  rows: number;
}

// ─── Mesh Generation ──────────────────────────────────────────────

/**
 * Generate a grid mesh over a rectangular region.
 * Returns mesh data suitable for PixiJS SimpleMesh.
 */
export function createGridMeshData(
  width: number,
  height: number,
  cols: number = 20,
  rows: number = 26
): SkinnedMeshData {
  const vertCount = (cols + 1) * (rows + 1);
  const restVertices = new Float32Array(vertCount * 2);
  const vertices = new Float32Array(vertCount * 2);
  const uvs = new Float32Array(vertCount * 2);

  // Generate vertices + UVs
  for (let r = 0; r <= rows; r++) {
    for (let c = 0; c <= cols; c++) {
      const idx = (r * (cols + 1) + c) * 2;
      const x = (c / cols) * width;
      const y = (r / rows) * height;
      restVertices[idx] = x;
      restVertices[idx + 1] = y;
      vertices[idx] = x;
      vertices[idx + 1] = y;
      uvs[idx] = c / cols;
      uvs[idx + 1] = r / rows;
    }
  }

  // Generate triangle indices
  const triCount = cols * rows * 2;
  const indices = new Uint16Array(triCount * 3);
  let ti = 0;
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      const tl = r * (cols + 1) + c;
      const tr = tl + 1;
      const bl = (r + 1) * (cols + 1) + c;
      const br = bl + 1;
      // Triangle 1: TL-BL-TR
      indices[ti++] = tl;
      indices[ti++] = bl;
      indices[ti++] = tr;
      // Triangle 2: TR-BL-BR
      indices[ti++] = tr;
      indices[ti++] = bl;
      indices[ti++] = br;
    }
  }

  return {
    restVertices,
    vertices,
    uvs,
    indices,
    skinWeights: new Array(vertCount).fill(null).map(() => ({ boneIds: [], weights: [] })),
    cols,
    rows,
  };
}

// ─── Skinning Weight Computation ──────────────────────────────────

/**
 * Compute skinning weights for each vertex based on bone proximity.
 * Uses inverse-distance weighting with heat diffusion falloff.
 * Each vertex is influenced by the N closest bones within an influence radius.
 */
export function computeSkinWeights(
  meshData: SkinnedMeshData,
  bones: BoneNode[],
  canvasW: number,
  canvasH: number,
  maxInfluences: number = 3,
  falloff: number = 3.5
): void {
  const transforms = computeForwardKinematics(bones, canvasW, canvasH);
  const vertCount = meshData.restVertices.length / 2;
  const maxInfluenceRadius = canvasH * 0.18; // Bones only influence nearby vertices

  for (let vi = 0; vi < vertCount; vi++) {
    const vx = meshData.restVertices[vi * 2];
    const vy = meshData.restVertices[vi * 2 + 1];

    // Calculate distance to each bone segment
    const distances: { boneId: string; dist: number }[] = [];

    for (const t of transforms) {
      const dist = pointToSegmentDistance(
        vx, vy,
        t.worldX, t.worldY,
        t.tipX, t.tipY
      );
      if (dist <= maxInfluenceRadius) {
        distances.push({ boneId: t.boneId, dist: Math.max(dist, 1) });
      }
    }

    // Sort by distance, take closest N
    distances.sort((a, b) => a.dist - b.dist);
    const closest = distances.slice(0, maxInfluences);

    if (closest.length === 0) {
      // Fallback to nearest single bone if outside radius
      let nearestBone = transforms[0]?.boneId || '';
      let minDist = Infinity;
      for (const t of transforms) {
        const d = pointToSegmentDistance(vx, vy, t.worldX, t.worldY, t.tipX, t.tipY);
        if (d < minDist) {
          minDist = d;
          nearestBone = t.boneId;
        }
      }
      meshData.skinWeights[vi] = {
        boneIds: [nearestBone],
        weights: [1.0],
      };
      continue;
    }

    // Compute weights using inverse distance with sharp falloff
    const rawWeights = closest.map((d) => 1.0 / Math.pow(d.dist, falloff));
    const totalWeight = rawWeights.reduce((sum, w) => sum + w, 0);

    meshData.skinWeights[vi] = {
      boneIds: closest.map((d) => d.boneId),
      weights: rawWeights.map((w) => w / (totalWeight || 1)),
    };
  }
}

/** Point-to-line-segment distance */
function pointToSegmentDistance(
  px: number, py: number,
  ax: number, ay: number,
  bx: number, by: number
): number {
  const dx = bx - ax, dy = by - ay;
  const lenSq = dx * dx + dy * dy;
  if (lenSq < 1e-6) {
    // Degenerate segment
    return Math.sqrt((px - ax) ** 2 + (py - ay) ** 2);
  }
  let t = ((px - ax) * dx + (py - ay) * dy) / lenSq;
  t = Math.max(0, Math.min(1, t));
  const closestX = ax + t * dx;
  const closestY = ay + t * dy;
  return Math.sqrt((px - closestX) ** 2 + (py - closestY) ** 2);
}

// ─── Vertex Skinning (Deformation) ───────────────────────────────

/**
 * Apply Linear Blend Skinning: deform mesh vertices based on bone transforms.
 *
 * For each vertex, compute the weighted blend of bone transformations
 * (rest → posed) and apply to the rest position.
 */
export function applySkinning(
  meshData: SkinnedMeshData,
  restTransforms: BoneWorldTransform[],
  posedTransforms: BoneWorldTransform[]
): void {
  const restMap = new Map<string, BoneWorldTransform>();
  const posedMap = new Map<string, BoneWorldTransform>();
  restTransforms.forEach((t) => restMap.set(t.boneId, t));
  posedTransforms.forEach((t) => posedMap.set(t.boneId, t));

  const vertCount = meshData.restVertices.length / 2;

  for (let vi = 0; vi < vertCount; vi++) {
    const rx = meshData.restVertices[vi * 2];
    const ry = meshData.restVertices[vi * 2 + 1];
    const sw = meshData.skinWeights[vi];

    if (sw.boneIds.length === 0) {
      meshData.vertices[vi * 2] = rx;
      meshData.vertices[vi * 2 + 1] = ry;
      continue;
    }

    let newX = 0, newY = 0;

    for (let bi = 0; bi < sw.boneIds.length; bi++) {
      const boneId = sw.boneIds[bi];
      const weight = sw.weights[bi];
      const rest = restMap.get(boneId);
      const posed = posedMap.get(boneId);

      if (!rest || !posed) {
        newX += rx * weight;
        newY += ry * weight;
        continue;
      }

      // Compute relative transform: rest → posed
      const restRad = (rest.worldRotation * Math.PI) / 180;
      const posedRad = (posed.worldRotation * Math.PI) / 180;
      const deltaRad = posedRad - restRad;

      // Transform vertex relative to rest bone position
      const localX = rx - rest.worldX;
      const localY = ry - rest.worldY;

      // Rotate local position by delta rotation
      const cos = Math.cos(deltaRad);
      const sin = Math.sin(deltaRad);
      const rotX = cos * localX - sin * localY;
      const rotY = sin * localX + cos * localY;

      // Translate to posed bone position
      const resultX = posed.worldX + rotX;
      const resultY = posed.worldY + rotY;

      newX += resultX * weight;
      newY += resultY * weight;
    }

    meshData.vertices[vi * 2] = newX;
    meshData.vertices[vi * 2 + 1] = newY;
  }
}

// ─── Diamond Bone Drawing (Canvas 2D overlay) ─────────────────────

/**
 * Draw diamond-shaped bones on a Canvas 2D context.
 * Similar to Blender/Spine bone visualization.
 */
export function drawDiamondBones(
  ctx: CanvasRenderingContext2D,
  bones: BoneNode[],
  transforms: BoneWorldTransform[],
  hoveredBone: string | null,
  draggingBone: string | null
): void {
  const boneMap = new Map<string, BoneNode>();
  bones.forEach((b) => boneMap.set(b.id, b));

  for (const t of transforms) {
    const bone = boneMap.get(t.boneId);
    if (!bone) continue;

    const isHovered = hoveredBone === t.boneId;
    const isDragging = draggingBone === t.boneId;

    // Diamond shape: origin → left → tip → right → origin
    const dx = t.tipX - t.worldX;
    const dy = t.tipY - t.worldY;
    const len = Math.sqrt(dx * dx + dy * dy);
    if (len < 1) continue;

    // Perpendicular direction for diamond width
    const perpX = -dy / len;
    const perpY = dx / len;
    const diamondWidth = Math.min(len * 0.2, 8); // 20% of length, max 8px

    // Diamond midpoint (30% from origin)
    const midFrac = 0.3;
    const midX = t.worldX + dx * midFrac;
    const midY = t.worldY + dy * midFrac;

    // Draw diamond
    ctx.beginPath();
    ctx.moveTo(t.worldX, t.worldY);
    ctx.lineTo(midX + perpX * diamondWidth, midY + perpY * diamondWidth);
    ctx.lineTo(t.tipX, t.tipY);
    ctx.lineTo(midX - perpX * diamondWidth, midY - perpY * diamondWidth);
    ctx.closePath();

    // Fill
    const alpha = isDragging ? 0.6 : isHovered ? 0.45 : 0.3;
    ctx.fillStyle = bone.color || '#f59e0b';
    ctx.globalAlpha = alpha;
    ctx.fill();

    // Outline
    ctx.strokeStyle = isDragging ? '#fbbf24' : isHovered ? '#fcd34d' : (bone.color || '#f59e0b');
    ctx.lineWidth = isDragging ? 2.5 : isHovered ? 2 : 1.5;
    ctx.globalAlpha = isDragging ? 1 : isHovered ? 0.9 : 0.7;
    ctx.stroke();
    ctx.globalAlpha = 1;

    // Joint circle at origin
    const jointRadius = isDragging ? 5 : isHovered ? 4 : 3;
    ctx.beginPath();
    ctx.arc(t.worldX, t.worldY, jointRadius, 0, Math.PI * 2);
    ctx.fillStyle = isDragging ? '#fbbf24' : isHovered ? '#f59e0b' : '#fff';
    ctx.fill();
    ctx.strokeStyle = bone.color || '#f59e0b';
    ctx.lineWidth = 1.5;
    ctx.stroke();

    // Tip circle (smaller)
    ctx.beginPath();
    ctx.arc(t.tipX, t.tipY, 2, 0, Math.PI * 2);
    ctx.fillStyle = bone.color || '#f59e0b';
    ctx.globalAlpha = 0.5;
    ctx.fill();
    ctx.globalAlpha = 1;

    // Label on hover
    if (isHovered || isDragging) {
      ctx.font = '10px Inter, sans-serif';
      ctx.fillStyle = '#fbbf24';
      ctx.textAlign = 'left';
      ctx.fillText(bone.name, t.worldX + jointRadius + 4, t.worldY + 3);
    }
  }
}
