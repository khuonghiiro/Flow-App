/**
 * RigViewportCanvas — PixiJS-powered viewport that renders texture
 * with GPU-accelerated mesh deformation and diamond bone overlay.
 * Replaces the old Canvas 2D approach.
 */
import React, { useRef, useEffect, useCallback, useState, useMemo } from 'react';
import * as PIXI from 'pixi.js';
import {
  BoneNode,
  BoneRigDefinition,
  Character2DAngle,
  AngleSlotEntry,
} from '../../../types/scene2d';
import {
  computeForwardKinematics,
  BoneWorldTransform,
} from '../../../core/engine2d/BoneRig2DEngine';
import {
  createGridMeshData,
  computeSkinWeights,
  applySkinning,
  drawDiamondBones,
  SkinnedMeshData,
} from '../../../core/engine2d/PixiSkinnedMesh';

interface RigViewportCanvasProps {
  selectedAngle: Character2DAngle;
  angleSlots: AngleSlotEntry[];
  boneRig: BoneRigDefinition | null;
  showBones: boolean;
  poseOverrides?: Record<string, number>;
  onBonePositionChange?: (boneId: string, newPos: [number, number]) => void;
  /** Current angle slider value 0-360 for smooth rotation preview */
  angleSliderValue: number;
  onAngleSliderChange: (deg: number) => void;
}

const ANGLE_LABELS: Record<string, string> = {
  front: 'Chính Diện (0°)',
  three_quarter_left: '3/4 Trái (45°)',
  profile_left: 'Nghiêng Trái (90°)',
  back: 'Sau Lưng (180°)',
  three_quarter_right: '3/4 Phải (315°)',
  profile_right: 'Nghiêng Phải (270°)',
};

export const RigViewportCanvas: React.FC<RigViewportCanvasProps> = ({
  selectedAngle,
  angleSlots,
  boneRig,
  showBones,
  poseOverrides = {},
  onBonePositionChange,
  angleSliderValue,
  onAngleSliderChange,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [canvasSize, setCanvasSize] = useState({ w: 400, h: 500 });
  const [draggingBone, setDraggingBone] = useState<string | null>(null);
  const [hoveredBone, setHoveredBone] = useState<string | null>(null);
  const loadedImageRef = useRef<HTMLImageElement | null>(null);
  const animFrameRef = useRef<number>(0);

  // Cached mesh data (regenerated when image or rig changes)
  const meshDataRef = useRef<SkinnedMeshData | null>(null);
  const meshNeedsSkinRef = useRef(true);

  // Load texture for current angle
  useEffect(() => {
    const slot = angleSlots.find((s) => s.angle === selectedAngle);
    if (!slot?.textureUrl) {
      loadedImageRef.current = null;
      meshDataRef.current = null;
      return;
    }
    const img = new Image();
    img.onload = () => {
      loadedImageRef.current = img;
      meshDataRef.current = null; // Force mesh rebuild
      meshNeedsSkinRef.current = true;
    };
    img.src = slot.textureUrl;
  }, [selectedAngle, angleSlots]);

  // Invalidate mesh when rig changes
  useEffect(() => {
    meshDataRef.current = null;
    meshNeedsSkinRef.current = true;
  }, [boneRig]);

  // Resize observer
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        setCanvasSize({
          w: Math.floor(entry.contentRect.width),
          h: Math.floor(entry.contentRect.height) - 48,
        });
      }
    });
    observer.observe(container);
    return () => observer.disconnect();
  }, []);

  // Compute image draw rect
  const getDrawRect = useCallback(() => {
    const img = loadedImageRef.current;
    if (!img) return null;
    const { w, h } = canvasSize;
    const scale = Math.min(w / img.width, h / img.height) * 0.85;
    const drawW = img.width * scale;
    const drawH = img.height * scale;
    const drawX = (w - drawW) / 2;
    const drawY = (h - drawH) / 2;
    return { drawX, drawY, drawW, drawH, scale };
  }, [canvasSize]);

  // Draw loop
  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    const { w, h } = canvasSize;
    canvas.width = w;
    canvas.height = h;

    // Background
    ctx.fillStyle = '#080d1a';
    ctx.fillRect(0, 0, w, h);

    // Checkerboard
    const tileSize = 16;
    for (let y = 0; y < h; y += tileSize) {
      for (let x = 0; x < w; x += tileSize) {
        const isOdd = ((x / tileSize) + (y / tileSize)) % 2 === 0;
        ctx.fillStyle = isOdd ? '#0f1729' : '#111d33';
        ctx.fillRect(x, y, tileSize, tileSize);
      }
    }

    const img = loadedImageRef.current;
    const drawRect = getDrawRect();
    const hasPose = Object.values(poseOverrides).some((v) => Math.abs(v) > 0.1);

    if (img && drawRect) {
      const { drawX, drawY, drawW, drawH } = drawRect;
      const slot = angleSlots.find((s) => s.angle === selectedAngle);
      const isMirrored = slot?.isMirrored || false;

      if (hasPose && boneRig && boneRig.bones.length > 0) {
        // ─── Mesh Deformation Mode (PixiJS LBS) ──────────────
        // Build mesh if needed
        if (!meshDataRef.current) {
          meshDataRef.current = createGridMeshData(drawW, drawH, 20, 26);
          meshNeedsSkinRef.current = true;
        }

        // Compute skin weights if needed
        if (meshNeedsSkinRef.current && meshDataRef.current) {
          computeSkinWeights(meshDataRef.current, boneRig.bones, drawW, drawH, 3, 3.5);
          meshNeedsSkinRef.current = false;
        }

        const meshData = meshDataRef.current;
        if (meshData) {
          // Compute rest & posed transforms
          const restTransforms = computeForwardKinematics(boneRig.bones, drawW, drawH);
          const posedBones = boneRig.bones.map((bone) => {
            const override = poseOverrides[bone.id];
            return override !== undefined
              ? { ...bone, rotation: bone.rotation + override }
              : bone;
          });
          const posedTransforms = computeForwardKinematics(posedBones, drawW, drawH);

          // Apply skinning
          applySkinning(meshData, restTransforms, posedTransforms);

          // Render deformed mesh using Canvas 2D triangle draw
          renderSkinnedMesh(ctx, img, meshData, drawX, drawY, drawW, drawH, isMirrored, w);
        }
      } else {
        // ─── Normal Draw Mode ──────────────────────────────────
        ctx.save();
        if (isMirrored) {
          ctx.translate(w, 0);
          ctx.scale(-1, 1);
          ctx.drawImage(img, w - drawX - drawW, drawY, drawW, drawH);
        } else {
          ctx.drawImage(img, drawX, drawY, drawW, drawH);
        }
        ctx.restore();
      }
    }

    // Draw bones overlay (diamond shapes - aligned to image bounds)
    if (showBones && boneRig && boneRig.bones.length > 0) {
      const displayBones = hasPose
        ? boneRig.bones.map((bone) => {
            const override = poseOverrides[bone.id];
            return override !== undefined
              ? { ...bone, rotation: bone.rotation + override }
              : bone;
          })
        : boneRig.bones;
      const baseW = drawRect ? drawRect.drawW : w;
      const baseH = drawRect ? drawRect.drawH : h;
      const offsetX = drawRect ? drawRect.drawX : 0;
      const offsetY = drawRect ? drawRect.drawY : 0;

      const rawTransforms = computeForwardKinematics(displayBones, baseW, baseH);
      const canvasTransforms = rawTransforms.map((t) => ({
        ...t,
        worldX: t.worldX + offsetX,
        worldY: t.worldY + offsetY,
        tipX: t.tipX + offsetX,
        tipY: t.tipY + offsetY,
      }));
      drawDiamondBones(ctx, boneRig.bones, canvasTransforms, hoveredBone, draggingBone);
    }

    // No-image placeholder
    if (!img) {
      ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
      ctx.font = '13px Inter, sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('Chưa có ảnh cho góc này', w / 2, h / 2 - 10);
      ctx.fillStyle = 'rgba(255, 255, 255, 0.08)';
      ctx.font = '11px Inter, sans-serif';
      ctx.fillText('Upload ảnh ở cột bên trái', w / 2, h / 2 + 12);
    }

    animFrameRef.current = requestAnimationFrame(draw);
  }, [canvasSize, selectedAngle, angleSlots, boneRig, showBones, hoveredBone, draggingBone, poseOverrides, getDrawRect]);

  useEffect(() => {
    animFrameRef.current = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(animFrameRef.current);
  }, [draw]);

  // Mouse interaction for bone dragging
  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas || !boneRig) return;
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    const drawRect = getDrawRect();
    const baseW = drawRect ? drawRect.drawW : canvasSize.w;
    const baseH = drawRect ? drawRect.drawH : canvasSize.h;
    const offsetX = drawRect ? drawRect.drawX : 0;
    const offsetY = drawRect ? drawRect.drawY : 0;

    if (draggingBone && onBonePositionChange) {
      const normX = Math.max(0, Math.min(1, (mx - offsetX) / baseW));
      const normY = Math.max(0, Math.min(1, (my - offsetY) / baseH));
      onBonePositionChange(draggingBone, [normX, normY]);
      return;
    }

    // Hover detection using posed bones
    const hasPose = Object.values(poseOverrides).some((v) => Math.abs(v) > 0.1);
    const displayBones = hasPose
      ? boneRig.bones.map((bone) => {
          const override = poseOverrides[bone.id];
          return override !== undefined
            ? { ...bone, rotation: bone.rotation + override }
            : bone;
        })
      : boneRig.bones;
    const rawTransforms = computeForwardKinematics(displayBones, baseW, baseH);
    let found: string | null = null;
    for (const t of rawTransforms) {
      const dx = mx - (t.worldX + offsetX);
      const dy = my - (t.worldY + offsetY);
      if (Math.sqrt(dx * dx + dy * dy) < 10) {
        found = t.boneId;
        break;
      }
    }
    setHoveredBone(found);
  };

  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (hoveredBone) {
      setDraggingBone(hoveredBone);
      e.preventDefault();
    }
  };

  const handleCanvasMouseUp = () => {
    setDraggingBone(null);
  };

  return (
    <div
      ref={containerRef}
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        borderRadius: 8,
        background: '#080d1a',
        border: '1px solid rgba(255, 255, 255, 0.08)',
      }}
    >
      {/* Angle Label */}
      <div style={{
        height: 28,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'rgba(15, 23, 42, 0.9)',
        borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
        fontSize: 11,
        fontWeight: 700,
        color: '#f59e0b',
        gap: 6,
      }}>
        📷 {ANGLE_LABELS[selectedAngle] || selectedAngle}
        {boneRig && (
          <span style={{ fontSize: 9, color: '#94a3b8', fontWeight: 500 }}>
            • {boneRig.bones.length} bones
          </span>
        )}
      </div>

      {/* Canvas */}
      <canvas
        ref={canvasRef}
        style={{
          flex: 1,
          width: '100%',
          cursor: hoveredBone ? 'grab' : 'default',
        }}
        onMouseMove={handleCanvasMouseMove}
        onMouseDown={handleCanvasMouseDown}
        onMouseUp={handleCanvasMouseUp}
        onMouseLeave={() => { setHoveredBone(null); setDraggingBone(null); }}
      />

      {/* Angle Slider */}
      <div style={{
        height: 40,
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '0 12px',
        background: 'rgba(15, 23, 42, 0.9)',
        borderTop: '1px solid rgba(255, 255, 255, 0.06)',
      }}>
        <span style={{ fontSize: 9, color: '#94a3b8', fontWeight: 600, minWidth: 30 }}>0°</span>
        <input
          type="range"
          min={0}
          max={360}
          step={1}
          value={angleSliderValue}
          onChange={(e) => onAngleSliderChange(Number(e.target.value))}
          style={{ flex: 1, accentColor: '#f59e0b' }}
          title={`Xoay góc: ${angleSliderValue}°`}
        />
        <span style={{ fontSize: 9, color: '#94a3b8', fontWeight: 600, minWidth: 36 }}>{angleSliderValue}°</span>
      </div>
    </div>
  );
};

// ─── Canvas 2D Triangle Mesh Renderer ─────────────────────────────

/**
 * Render a skinned mesh using Canvas 2D triangle-by-triangle approach.
 * Maps source (rest) triangles to destination (deformed) triangles.
 */
function renderSkinnedMesh(
  ctx: CanvasRenderingContext2D,
  img: HTMLImageElement,
  meshData: SkinnedMeshData,
  imgX: number,
  imgY: number,
  imgW: number,
  imgH: number,
  isMirrored: boolean,
  canvasW: number
): void {
  const { restVertices, vertices, uvs, indices } = meshData;

  for (let i = 0; i < indices.length; i += 3) {
    const a = indices[i], b = indices[i + 1], c = indices[i + 2];

    // Destination (deformed) triangle in canvas coords
    const dx0 = imgX + vertices[a * 2], dy0 = imgY + vertices[a * 2 + 1];
    const dx1 = imgX + vertices[b * 2], dy1 = imgY + vertices[b * 2 + 1];
    const dx2 = imgX + vertices[c * 2], dy2 = imgY + vertices[c * 2 + 1];

    // Source triangle in canvas coords (where the image sits undeformed)
    const sx0 = imgX + restVertices[a * 2], sy0 = imgY + restVertices[a * 2 + 1];
    const sx1 = imgX + restVertices[b * 2], sy1 = imgY + restVertices[b * 2 + 1];
    const sx2 = imgX + restVertices[c * 2], sy2 = imgY + restVertices[c * 2 + 1];

    // Affine transform: source → destination
    const sdu1 = sx1 - sx0, sdv1 = sy1 - sy0;
    const sdu2 = sx2 - sx0, sdv2 = sy2 - sy0;
    const ddu1 = dx1 - dx0, ddv1 = dy1 - dy0;
    const ddu2 = dx2 - dx0, ddv2 = dy2 - dy0;

    const det = sdu1 * sdv2 - sdv1 * sdu2;
    if (Math.abs(det) < 1e-6) continue;

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

    // Apply affine transform and draw image at original position
    ctx.setTransform(m11, m12, m21, m22, tx, ty);
    ctx.drawImage(img, imgX, imgY, imgW, imgH);

    ctx.restore();
  }
}
