/**
 * RigViewportCanvas — Interactive canvas viewport for 2D bone rigging & deformation.
 * Supports direct on-canvas drag & drop editing of bone joints, rotation & length handles,
 * bone selection, and live pose testing with Linear Blend Skinning.
 */
import React, { useRef, useEffect, useCallback, useState } from 'react';
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
import { Wrench, UserCheck, RotateCcw, HelpCircle } from 'lucide-react';

interface RigViewportCanvasProps {
  selectedAngle: Character2DAngle;
  angleSlots: AngleSlotEntry[];
  boneRig: BoneRigDefinition | null;
  showBones: boolean;
  selectedBoneId: string | null;
  onSelectBoneId: (boneId: string | null) => void;
  onUpdateBone?: (boneId: string, updates: Partial<BoneNode>) => void;
  editorMode: 'rig_edit' | 'pose_test';
  onSetEditorMode: (mode: 'rig_edit' | 'pose_test') => void;
  poseOverrides?: Record<string, number>;
  onPoseChange?: (boneId: string, deltaRot: number) => void;
  onResetPose?: () => void;
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

function distToSegment(px: number, py: number, x1: number, y1: number, x2: number, y2: number): number {
  const l2 = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
  if (l2 === 0) return Math.hypot(px - x1, py - y1);
  let t = ((px - x1) * (x2 - x1) + (py - y1) * (y2 - y1)) / l2;
  t = Math.max(0, Math.min(1, t));
  return Math.hypot(px - (x1 + t * (x2 - x1)), py - (y1 + t * (y2 - y1)));
}

export const RigViewportCanvas: React.FC<RigViewportCanvasProps> = ({
  selectedAngle,
  angleSlots,
  boneRig,
  showBones,
  selectedBoneId,
  onSelectBoneId,
  onUpdateBone,
  editorMode,
  onSetEditorMode,
  poseOverrides = {},
  onPoseChange,
  onResetPose,
  angleSliderValue,
  onAngleSliderChange,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [canvasSize, setCanvasSize] = useState({ w: 400, h: 500 });
  const [draggingHandle, setDraggingHandle] = useState<{
    boneId: string;
    type: 'joint' | 'tip' | 'body';
  } | null>(null);
  const [hoveredHandle, setHoveredHandle] = useState<{
    boneId: string;
    type: 'joint' | 'tip' | 'body';
  } | null>(null);

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
          h: Math.floor(entry.contentRect.height) - 80,
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
        const isOdd = (Math.floor(x / tileSize) + Math.floor(y / tileSize)) % 2 === 0;
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
        // Mesh Deformation Mode (PixiJS LBS)
        if (!meshDataRef.current) {
          meshDataRef.current = createGridMeshData(drawW, drawH, 20, 26);
          meshNeedsSkinRef.current = true;
        }

        if (meshNeedsSkinRef.current && meshDataRef.current) {
          computeSkinWeights(meshDataRef.current, boneRig.bones, drawW, drawH, 3, 3.5);
          meshNeedsSkinRef.current = false;
        }

        const meshData = meshDataRef.current;
        if (meshData) {
          const restTransforms = computeForwardKinematics(boneRig.bones, drawW, drawH);
          const posedBones = boneRig.bones.map((bone) => {
            const override = poseOverrides[bone.id];
            return override !== undefined
              ? { ...bone, rotation: bone.rotation + override }
              : bone;
          });
          const posedTransforms = computeForwardKinematics(posedBones, drawW, drawH);

          applySkinning(meshData, restTransforms, posedTransforms);
          renderSkinnedMesh(ctx, img, meshData, drawX, drawY, drawW, drawH, isMirrored, w);
        }
      } else {
        // Normal Draw Mode
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

    // Draw bones overlay
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

      drawDiamondBones(
        ctx,
        boneRig.bones,
        canvasTransforms,
        hoveredHandle?.boneId || null,
        draggingHandle?.boneId || null,
        selectedBoneId,
        hoveredHandle
      );
    }

    // No-image placeholder
    if (!img) {
      ctx.fillStyle = 'rgba(255, 255, 255, 0.25)';
      ctx.font = 'bold 13px Inter, sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('Chưa có ảnh texture cho góc này', w / 2, h / 2 - 10);
      ctx.fillStyle = 'rgba(255, 255, 255, 0.12)';
      ctx.font = '11px Inter, sans-serif';
      ctx.fillText('Nhấn nút "Tải Mẫu Demo Bàn Tay" ở trên hoặc upload ảnh ở cột trái', w / 2, h / 2 + 12);
    }

    animFrameRef.current = requestAnimationFrame(draw);
  }, [canvasSize, selectedAngle, angleSlots, boneRig, showBones, hoveredHandle, draggingHandle, selectedBoneId, poseOverrides, getDrawRect]);

  useEffect(() => {
    animFrameRef.current = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(animFrameRef.current);
  }, [draw]);

  // ─── Mouse Hover & Drag Interaction ──────────────────────────────
  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas || !boneRig || boneRig.bones.length === 0) return;
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    const drawRect = getDrawRect();
    const baseW = drawRect ? drawRect.drawW : canvasSize.w;
    const baseH = drawRect ? drawRect.drawH : canvasSize.h;
    const offsetX = drawRect ? drawRect.drawX : 0;
    const offsetY = drawRect ? drawRect.drawY : 0;

    const hasPose = Object.values(poseOverrides).some((v) => Math.abs(v) > 0.1);
    const displayBones = hasPose
      ? boneRig.bones.map((b) => {
          const override = poseOverrides[b.id];
          return override !== undefined ? { ...b, rotation: b.rotation + override } : b;
        })
      : boneRig.bones;

    const rawTransforms = computeForwardKinematics(displayBones, baseW, baseH);

    // If dragging a bone handle
    if (draggingHandle) {
      const activeBone = boneRig.bones.find((b) => b.id === draggingHandle.boneId);
      if (!activeBone) return;

      const currentTransform = rawTransforms.find((t) => t.boneId === draggingHandle.boneId);
      const parentBone = activeBone.parentId ? boneRig.bones.find((b) => b.id === activeBone.parentId) : null;
      const parentTransform = parentBone ? rawTransforms.find((t) => t.boneId === parentBone.id) : null;

      if (editorMode === 'pose_test') {
        // Pose Mode: rotate bone relative to its rest pose
        if (onPoseChange && currentTransform) {
          const originX = currentTransform.worldX + offsetX;
          const originY = currentTransform.worldY + offsetY;
          const angleRad = Math.atan2(mx - originX, -(my - originY));
          const targetWorldDeg = (angleRad * 180) / Math.PI;
          const restTransforms = computeForwardKinematics(boneRig.bones, baseW, baseH);
          const restTransform = restTransforms.find((r) => r.boneId === draggingHandle.boneId);
          const restWorldDeg = restTransform ? restTransform.worldRotation : 0;
          let deltaRot = Math.round(targetWorldDeg - restWorldDeg);
          while (deltaRot > 180) deltaRot -= 360;
          while (deltaRot < -180) deltaRot += 360;
          onPoseChange(draggingHandle.boneId, deltaRot);
        }
        return;
      }

      // Rig Edit Mode
      if (onUpdateBone) {
        if (draggingHandle.type === 'joint' || (draggingHandle.type === 'body' && activeBone.parentId === null)) {
          // Move joint position
          if (activeBone.parentId === null) {
            // Root bone: absolute normalized position
            const normX = Math.max(0.01, Math.min(0.99, (mx - offsetX) / baseW));
            const normY = Math.max(0.01, Math.min(0.99, (my - offsetY) / baseH));
            onUpdateBone(draggingHandle.boneId, { position: [normX, normY] });
          } else if (parentTransform) {
            // Child bone: compute local offset relative to parent tip in parent orientation
            const parentTipX = parentTransform.tipX + offsetX;
            const parentTipY = parentTransform.tipY + offsetY;
            const dx = mx - parentTipX;
            const dy = my - parentTipY;
            const pRad = (parentTransform.worldRotation * Math.PI) / 180;
            const localX = (dx * Math.cos(-pRad) - dy * Math.sin(-pRad)) / baseW;
            const localY = (dx * Math.sin(-pRad) + dy * Math.cos(-pRad)) / baseH;
            onUpdateBone(draggingHandle.boneId, {
              position: [Math.round(localX * 1000) / 1000, Math.round(localY * 1000) / 1000],
            });
          }
        } else if (draggingHandle.type === 'tip') {
          // Adjust rotation & length
          if (currentTransform) {
            const originX = currentTransform.worldX + offsetX;
            const originY = currentTransform.worldY + offsetY;
            const dx = mx - originX;
            const dy = my - originY;
            const angleRad = Math.atan2(dx, -dy);
            const targetWorldDeg = (angleRad * 180) / Math.PI;
            const parentWorldRot = parentTransform ? parentTransform.worldRotation : 0;
            let localRot = Math.round(targetWorldDeg - parentWorldRot);
            while (localRot > 180) localRot -= 360;
            while (localRot < -180) localRot += 360;
            const distPx = Math.hypot(dx, dy);
            const localLen = Math.max(0.02, distPx / Math.min(baseW, baseH));
            onUpdateBone(draggingHandle.boneId, {
              rotation: localRot,
              length: Math.round(localLen * 1000) / 1000,
            });
          }
        }
      }
      return;
    }

    // Hover Detection
    let found: { boneId: string; type: 'joint' | 'tip' | 'body' } | null = null;

    // 1. Tip handles (highest priority)
    for (const t of rawTransforms) {
      const tipDist = Math.hypot(mx - (t.tipX + offsetX), my - (t.tipY + offsetY));
      if (tipDist <= 14) {
        found = { boneId: t.boneId, type: 'tip' };
        break;
      }
    }

    // 2. Joint origin handles
    if (!found) {
      for (const t of rawTransforms) {
        const jointDist = Math.hypot(mx - (t.worldX + offsetX), my - (t.worldY + offsetY));
        if (jointDist <= 14) {
          found = { boneId: t.boneId, type: 'joint' };
          break;
        }
      }
    }

    // 3. Bone body
    if (!found) {
      for (const t of rawTransforms) {
        const bodyDist = distToSegment(
          mx,
          my,
          t.worldX + offsetX,
          t.worldY + offsetY,
          t.tipX + offsetX,
          t.tipY + offsetY
        );
        if (bodyDist <= 12) {
          found = { boneId: t.boneId, type: 'body' };
          break;
        }
      }
    }

    setHoveredHandle(found);
  };

  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (hoveredHandle) {
      setDraggingHandle(hoveredHandle);
      onSelectBoneId(hoveredHandle.boneId);
      e.preventDefault();
    } else {
      // Click background to deselect
      onSelectBoneId(null);
    }
  };

  const handleCanvasMouseUp = () => {
    setDraggingHandle(null);
  };

  const getCursorStyle = () => {
    if (draggingHandle) return 'grabbing';
    if (!hoveredHandle) return 'default';
    if (hoveredHandle.type === 'tip') return 'crosshair';
    if (hoveredHandle.type === 'joint') return 'move';
    return 'pointer';
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
      {/* ─── Top Control Bar: Angle + Mode Switcher ─────────────────── */}
      <div
        style={{
          height: 38,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 10px',
          background: 'rgba(15, 23, 42, 0.95)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          gap: 8,
          flexShrink: 0,
        }}
      >
        {/* Angle Badge */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11, fontWeight: 700, color: '#f59e0b' }}>
          <span>📷 {ANGLE_LABELS[selectedAngle] || selectedAngle}</span>
          {boneRig && (
            <span style={{ fontSize: 9, color: '#94a3b8', fontWeight: 500 }}>
              • {boneRig.bones.length} bones
            </span>
          )}
        </div>

        {/* Mode Switcher Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6 }}>
          <button
            onClick={() => onSetEditorMode('rig_edit')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '3px 9px',
              borderRadius: 4,
              border: 'none',
              background: editorMode === 'rig_edit' ? '#0284c7' : 'transparent',
              color: editorMode === 'rig_edit' ? '#ffffff' : '#94a3b8',
              fontSize: 10,
              fontWeight: editorMode === 'rig_edit' ? 700 : 500,
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
            title="Kéo các điểm nút (tròn trắng/đỏ) để định vị, xoay và chỉnh độ dài xương"
          >
            <Wrench size={11} /> 🛠️ Chỉnh Sửa Xương
          </button>

          <button
            onClick={() => onSetEditorMode('pose_test')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '3px 9px',
              borderRadius: 4,
              border: 'none',
              background: editorMode === 'pose_test' ? '#9333ea' : 'transparent',
              color: editorMode === 'pose_test' ? '#ffffff' : '#c084fc',
              fontSize: 10,
              fontWeight: editorMode === 'pose_test' ? 700 : 500,
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
            title="Kéo xương để uốn khớp động và xem biến dạng đồ họa (PixiJS Mesh)"
          >
            <UserCheck size={11} /> 🕺 Uốn Khớp (Pose)
          </button>

          {Object.values(poseOverrides).some((v) => Math.abs(v) > 0.1) && (
            <button
              onClick={onResetPose}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '3px 7px',
                borderRadius: 4,
                border: '1px solid rgba(239, 68, 68, 0.3)',
                background: 'rgba(239, 68, 68, 0.15)',
                color: '#f87171',
                fontSize: 9,
                fontWeight: 600,
                cursor: 'pointer',
              }}
              title="Khôi phục dáng xương ban đầu (Reset FK)"
            >
              <RotateCcw size={9} /> Reset
            </button>
          )}
        </div>
      </div>

      {/* ─── Interactive Viewport Canvas ───────────────────────────── */}
      <canvas
        ref={canvasRef}
        style={{
          flex: 1,
          width: '100%',
          cursor: getCursorStyle(),
          touchAction: 'none',
        }}
        onMouseMove={handleCanvasMouseMove}
        onMouseDown={handleCanvasMouseDown}
        onMouseUp={handleCanvasMouseUp}
        onMouseLeave={() => {
          setHoveredHandle(null);
          setDraggingHandle(null);
        }}
      />

      {/* ─── Bottom Toolbar: Tips & 360 Rotation Slider ───────────── */}
      <div
        style={{
          height: 42,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 12px',
          background: 'rgba(15, 23, 42, 0.95)',
          borderTop: '1px solid rgba(255, 255, 255, 0.08)',
          gap: 12,
          flexShrink: 0,
        }}
      >
        {/* Help Tip */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 9.5, color: '#94a3b8' }}>
          <HelpCircle size={12} color="#38bdf8" />
          <span>
            {editorMode === 'rig_edit'
              ? '💡 Kéo điểm tròn trắng (Gốc) để di chuyển • Kéo điểm tròn đỏ (Đầu) để xoay/kéo dài xương.'
              : '💡 Chế độ Pose: Kéo trực tiếp bất kỳ khớp xương nào để uốn cong bộ phận linh hoạt.'}
          </span>
        </div>

        {/* Angle 360 Slider */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, minWidth: 180 }}>
          <span style={{ fontSize: 9, color: '#94a3b8', fontWeight: 600 }}>0°</span>
          <input
            type="range"
            min={0}
            max={360}
            step={1}
            value={angleSliderValue}
            onChange={(e) => onAngleSliderChange(Number(e.target.value))}
            style={{ flex: 1, accentColor: '#f59e0b', height: 4, cursor: 'pointer' }}
            title={`Xoay góc xem: ${angleSliderValue}°`}
          />
          <span style={{ fontSize: 9.5, color: '#fbbf24', fontWeight: 700, minWidth: 32, textAlign: 'right' }}>
            {angleSliderValue}°
          </span>
        </div>
      </div>
    </div>
  );
};

// ─── Canvas 2D Triangle Mesh Renderer ─────────────────────────────
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
  const { restVertices, vertices, indices } = meshData;

  for (let i = 0; i < indices.length; i += 3) {
    const a = indices[i],
      b = indices[i + 1],
      c = indices[i + 2];

    const dx0 = imgX + vertices[a * 2],
      dy0 = imgY + vertices[a * 2 + 1];
    const dx1 = imgX + vertices[b * 2],
      dy1 = imgY + vertices[b * 2 + 1];
    const dx2 = imgX + vertices[c * 2],
      dy2 = imgY + vertices[c * 2 + 1];

    const sx0 = imgX + restVertices[a * 2],
      sy0 = imgY + restVertices[a * 2 + 1];
    const sx1 = imgX + restVertices[b * 2],
      sy1 = imgY + restVertices[b * 2 + 1];
    const sx2 = imgX + restVertices[c * 2],
      sy2 = imgY + restVertices[c * 2 + 1];

    const sdu1 = sx1 - sx0,
      sdv1 = sy1 - sy0;
    const sdu2 = sx2 - sx0,
      sdv2 = sy2 - sy0;
    const ddu1 = dx1 - dx0,
      ddv1 = dy1 - dy0;
    const ddu2 = dx2 - dx0,
      ddv2 = dy2 - dy0;

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
    ctx.beginPath();
    ctx.moveTo(dx0, dy0);
    ctx.lineTo(dx1, dy1);
    ctx.lineTo(dx2, dy2);
    ctx.closePath();
    ctx.clip();

    ctx.setTransform(m11, m12, m21, m22, tx, ty);
    ctx.drawImage(img, imgX, imgY, imgW, imgH);
    ctx.restore();
  }
}
