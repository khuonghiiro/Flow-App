// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useEffect, useCallback } from 'react';
import {
  Clapperboard,
  Save,
  Check,
  Sparkles,
  FolderOpen,
} from 'lucide-react';
import {
  AnimationSliceFrame,
  AnimationPoseSavePayload,
  AnimationSequenceConfig,
} from '../../../types/animation_slicer';
import { AnimationPreviewStage } from './components/AnimationPreviewStage';
import { AnimationFrameFilmstripBar } from './components/AnimationFrameFilmstripBar';
import { AnimationPropertiesPanel } from './components/AnimationPropertiesPanel';
import { AnimationPoseSaveModal } from './components/AnimationPoseSaveModal';
import { AnimationSequenceLoadModal } from './components/AnimationSequenceLoadModal';
import { saveAnimationSequence } from './utils/animationPoseRegistry';
import { useAnimationHistory } from './hooks/useAnimationHistory';
import { autoTrimAllFramesBBox } from './utils/autoTrimBBoxHelper';

// Sample 4-Frame Martial Arts Action Sprite Sheet (Demo)
const SAMPLE_DEMO_FRAMES_SVG = [
  // Frame 1: Ready Pose
  `data:image/svg+xml;utf8,${encodeURIComponent(`
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200">
      <g transform="translate(100, 100)">
        <circle cx="0" cy="-40" r="16" fill="#fed7aa"/>
        <path d="M-12 -24 L12 -24 L16 35 L-16 35 Z" fill="#0284c7"/>
        <line x1="-10" y1="35" x2="-10" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="10" y1="35" x2="10" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="14" y1="-10" x2="35" y2="10" stroke="#38bdf8" stroke-width="4"/>
      </g>
    </svg>
  `)}`,
  // Frame 2: Draw Sword / Power Up
  `data:image/svg+xml;utf8,${encodeURIComponent(`
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200">
      <g transform="translate(100, 95)">
        <circle cx="0" cy="-40" r="16" fill="#fed7aa"/>
        <path d="M-14 -24 L14 -24 L20 35 L-14 35 Z" fill="#0284c7"/>
        <line x1="-15" y1="35" x2="-25" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="15" y1="35" x2="10" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="-10" y1="-10" x2="45" y2="-45" stroke="#38bdf8" stroke-width="5"/>
        <circle cx="45" cy="-45" r="8" fill="#facc15" opacity="0.6"/>
      </g>
    </svg>
  `)}`,
  // Frame 3: Slash Forward
  `data:image/svg+xml;utf8,${encodeURIComponent(`
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200">
      <g transform="translate(100, 100)">
        <circle cx="10" cy="-40" r="16" fill="#fed7aa"/>
        <path d="M0 -24 L24 -24 L28 35 L-8 35 Z" fill="#0284c7"/>
        <line x1="-10" y1="35" x2="-35" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="20" y1="35" x2="35" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="-15" y1="-5" x2="55" y2="15" stroke="#38bdf8" stroke-width="6"/>
        <path d="M15 -45 Q65 15 15 55" stroke="#67e8f9" stroke-width="8" fill="none" opacity="0.8"/>
      </g>
    </svg>
  `)}`,
  // Frame 4: Recovery Pose
  `data:image/svg+xml;utf8,${encodeURIComponent(`
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200">
      <g transform="translate(100, 100)">
        <circle cx="0" cy="-40" r="16" fill="#fed7aa"/>
        <path d="M-12 -24 L12 -24 L16 35 L-16 35 Z" fill="#0284c7"/>
        <line x1="-12" y1="35" x2="-15" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="12" y1="35" x2="15" y2="75" stroke="#0f172a" stroke-width="6"/>
        <line x1="-14" y1="-10" x2="-35" y2="25" stroke="#38bdf8" stroke-width="4"/>
      </g>
    </svg>
  `)}`,
];

interface AnimationSlicerTabProps {
  initialFrames?: string[] | null;
  externalSpriteSheetUrl?: string | null;
}

export const AnimationSlicerTab: React.FC<AnimationSlicerTabProps> = ({
  initialFrames,
}) => {
  // Sliced Animation Frames & Sequencer
  const [frames, setFrames] = useState<AnimationSliceFrame[]>([]);
  const [frameOrder, setFrameOrder] = useState<number[]>([]);
  const [selectedFrameIndex, setSelectedFrameIndex] = useState<number | null>(0);
  const [fps, setFps] = useState<number>(8);
  const [loopMode, setLoopMode] = useState<'loop' | 'ping_pong' | 'once'>('loop');

  // Multi-frame Onion Skin & BBox
  const [onionSkinMode, setOnionSkinMode] = useState<'off' | 'sequential' | 'all'>('sequential');
  const [showBBox, setShowBBox] = useState<boolean>(true);

  // Modals & Notifications
  const [isSaveModalOpen, setIsSaveModalOpen] = useState<boolean>(false);
  const [isLoadModalOpen, setIsLoadModalOpen] = useState<boolean>(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // History & Undo/Redo Hook
  const { canUndo, canRedo, undo, redo, pushSnapshot } = useAnimationHistory(
    frames,
    frameOrder,
    (restoredFrames, restoredOrder, label) => {
      setFrames(restoredFrames);
      setFrameOrder(restoredOrder);
      setToastMessage(label);
      setTimeout(() => setToastMessage(null), 2000);
    }
  );

  // Handle selecting a saved sequence to load
  const handleSelectSequence = (seq: AnimationSequenceConfig) => {
    if (seq.frames && seq.frames.length > 0) {
      pushSnapshot(frames, frameOrder, `Nạp hoạt ảnh: ${seq.poseName}`);
      setFrames(seq.frames);
      setFrameOrder(seq.frameOrder && seq.frameOrder.length > 0 ? seq.frameOrder : seq.frames.map((_, i) => i));
      if (seq.fps) setFps(seq.fps);
      if (seq.loopMode) setLoopMode(seq.loopMode);
      setSelectedFrameIndex(0);
      setToastMessage(`✓ Đã mở ${seq.frames.length} frames của "${seq.poseName}" (Góc ${seq.angleDeg}°)!`);
      setTimeout(() => setToastMessage(null), 3000);
    }
  };

  // Keyboard Shortcuts for Undo/Redo (Ctrl+Z / Ctrl+Y)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (
        e.target instanceof HTMLInputElement ||
        e.target instanceof HTMLTextAreaElement ||
        e.target instanceof HTMLSelectElement
      ) {
        return;
      }

      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        if (e.shiftKey) {
          e.preventDefault();
          redo(frames, frameOrder);
        } else {
          e.preventDefault();
          undo(frames, frameOrder);
        }
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') {
        e.preventDefault();
        redo(frames, frameOrder);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [undo, redo, frames, frameOrder]);

  // Load Demo Frames Helper
  const handleLoadDemoFrames = useCallback(() => {
    const demo: AnimationSliceFrame[] = SAMPLE_DEMO_FRAMES_SVG.map((url, idx) => ({
      id: `demo_frame_${idx}`,
      index: idx,
      originalDataUrl: url,
      transparentDataUrl: url,
      cropRect: { x: 0, y: 0, width: 200, height: 200 },
      offsetX: 0,
      offsetY: 0,
      scale: 1.0,
      rotation: 0,
      flipX: false,
      durationMs: idx === 0 ? 1000 : idx === 1 ? 200 : idx === 2 ? 300 : 1200,
    }));
    setFrames(demo);
    setFrameOrder(demo.map((_, i) => i));
    setSelectedFrameIndex(0);
    setToastMessage(`Đã nạp ${demo.length} khung hình mẫu demo!`);
    setTimeout(() => setToastMessage(null), 2500);
  }, []);

  // Handle Transferred Initial Frames from Tab 1
  useEffect(() => {
    if (initialFrames && initialFrames.length > 0) {
      let isMounted = true;
      const loadAllFrames = async () => {
        const loaded: AnimationSliceFrame[] = await Promise.all(
          initialFrames.map(async (url, idx) => {
            const img = new Image();
            img.crossOrigin = 'anonymous';
            await new Promise<void>((res) => {
              img.onload = () => res();
              img.onerror = () => res();
              img.src = url;
            });
            const w = img.naturalWidth || img.width || 200;
            const h = img.naturalHeight || img.height || 260;
            return {
              id: `transferred_frame_${idx}_${Date.now()}`,
              index: idx,
              originalDataUrl: url,
              transparentDataUrl: url,
              cropRect: { x: 0, y: 0, width: w, height: h },
              offsetX: 0,
              offsetY: 0,
              scale: 1.0,
              rotation: 0,
              flipX: false,
              durationMs: 500,
            };
          })
        );
        if (!isMounted) return;
        setFrames(loaded);
        setFrameOrder(loaded.map((_, i) => i));
        setSelectedFrameIndex(0);
        setToastMessage(`✓ Đã nhận ${loaded.length} khung hình chuẩn từ Tab 1!`);
        setTimeout(() => setToastMessage(null), 3000);
      };
      loadAllFrames();
      return () => {
        isMounted = false;
      };
    } else if (frames.length === 0) {
      handleLoadDemoFrames();
    }
  }, [initialFrames, handleLoadDemoFrames]);

  // Update Transform or Duration for a single frame
  const handleUpdateFrameTransform = (
    frameIndex: number,
    updates: Partial<Pick<AnimationSliceFrame, 'offsetX' | 'offsetY' | 'scale' | 'rotation' | 'flipX' | 'durationMs' | 'transparentDataUrl'>>
  ) => {
    pushSnapshot(frames, frameOrder, 'Chỉnh sửa frame');
    setFrames((prev) =>
      prev.map((f, idx) => (idx === frameIndex ? { ...f, ...updates } : f))
    );
  };

  // Update multiple frames data URLs (used for multi-frame pixel eraser)
  const handleUpdateMultipleFramesDataUrl = (updates: { index: number; dataUrl: string }[], label: string = 'Tẩy pixel') => {
    pushSnapshot(frames, frameOrder, label);
    const updateMap = new Map<number, string>(updates.map((u) => [u.index, u.dataUrl]));
    setFrames((prev) =>
      prev.map((f, idx) => {
        if (updateMap.has(idx)) {
          return { ...f, transparentDataUrl: updateMap.get(idx)! };
        }
        return f;
      })
    );
  };

  // Auto-Trim BBox for ALL frames (Cắt sát viền trong suốt)
  const handleAutoTrimAllBBox = async () => {
    if (frames.length === 0) return;
    pushSnapshot(frames, frameOrder, 'Cắt BBox sát viền');
    const trimmedFrames = await autoTrimAllFramesBBox(frames, 4);
    setFrames(trimmedFrames);
    setToastMessage(`✓ Đã tự động cắt sát viền BBox cho ${trimmedFrames.length} frames!`);
    setTimeout(() => setToastMessage(null), 2500);
  };

  // Manual Crop for ALL frames using Stage-relative BBox
  const handleApplyManualCropAllFrames = async (cropRect: StageCropRect) => {
    if (frames.length === 0) return;
    pushSnapshot(frames, frameOrder, 'Cắt BBox thủ công');
    const getImage = (url: string): HTMLImageElement | null => {
      const img = new Image();
      img.src = url;
      return img;
    };
    const cropped = await cropAllFramesWithStageRect(frames, cropRect, getImage);
    setFrames(cropped);
    setToastMessage(`✓ Đã cắt theo BBox thủ công cho ${cropped.length} frames!`);
    setTimeout(() => setToastMessage(null), 2500);
  };

  // Toggle Onion Skin Mode ('sequential' -> 'all' -> 'off' -> 'sequential')
  const handleToggleOnionSkinMode = () => {
    setOnionSkinMode((prev) => {
      if (prev === 'sequential') return 'all';
      if (prev === 'all') return 'off';
      return 'sequential';
    });
  };

  // Set uniform duration for ALL frames
  const handleSetAllFramesDuration = (durationMs: number) => {
    pushSnapshot(frames, frameOrder, 'Đặt thời lượng tất cả');
    setFrames((prev) =>
      prev.map((f) => ({
        ...f,
        durationMs,
      }))
    );
    setToastMessage(`Đã đặt thời lượng ${(durationMs / 1000).toFixed(2)}s cho tất cả frames!`);
    setTimeout(() => setToastMessage(null), 2000);
  };

  // Apply Transform of one frame to ALL frames
  const handleApplyTransformToAllFrames = (sourceFrameIndex: number) => {
    const src = frames[sourceFrameIndex];
    if (!src) return;
    pushSnapshot(frames, frameOrder, 'Áp dụng vị trí tất cả');
    setFrames((prev) =>
      prev.map((f) => ({
        ...f,
        offsetX: src.offsetX,
        offsetY: src.offsetY,
        scale: src.scale,
        rotation: src.rotation,
        flipX: src.flipX,
        durationMs: src.durationMs,
      }))
    );
    setToastMessage(`Đã áp dụng vị trí & tỉ lệ của Frame F${sourceFrameIndex + 1} cho tất cả frames!`);
    setTimeout(() => setToastMessage(null), 2000);
  };

  // Duplicate Frame
  const handleDuplicateFrame = (index: number) => {
    const src = frames[index];
    if (!src) return;
    pushSnapshot(frames, frameOrder, 'Nhân bản frame');
    const cloned: AnimationSliceFrame = {
      ...src,
      id: `frame_${Date.now()}`,
      index: frames.length,
    };
    const nextFrames = [...frames, cloned];
    setFrames(nextFrames);
    setFrameOrder(nextFrames.map((_, i) => i));
    setSelectedFrameIndex(nextFrames.length - 1);
    setToastMessage(`Đã nhân bản Frame F${index + 1}!`);
    setTimeout(() => setToastMessage(null), 1500);
  };

  // Delete Frame
  const handleDeleteFrame = (index: number) => {
    if (frames.length <= 1) return;
    pushSnapshot(frames, frameOrder, 'Xóa frame');
    const nextFrames = frames.filter((_, i) => i !== index);
    setFrames(nextFrames);
    setFrameOrder(nextFrames.map((_, i) => i));
    setSelectedFrameIndex(Math.max(0, index - 1));
    setToastMessage(`Đã xóa Frame F${index + 1}!`);
    setTimeout(() => setToastMessage(null), 1500);
  };

  // Move Frame Left / Right
  const handleMoveFrame = (index: number, direction: 'left' | 'right') => {
    const targetIdx = direction === 'left' ? index - 1 : index + 1;
    if (targetIdx < 0 || targetIdx >= frames.length) return;

    pushSnapshot(frames, frameOrder, 'Đổi thứ tự frame');
    const nextFrames = [...frames];
    const temp = nextFrames[index];
    nextFrames[index] = nextFrames[targetIdx];
    nextFrames[targetIdx] = temp;

    setFrames(nextFrames);
    setSelectedFrameIndex(targetIdx);
  };

  // Handle Save Success
  const handleSaveSuccess = (payload: AnimationPoseSavePayload) => {
    const sequenceConfig: AnimationSequenceConfig = {
      id: `seq_${payload.poseId}_${payload.angleDeg}_${Date.now()}`,
      poseId: payload.poseId,
      poseName: payload.poseName,
      folderSlug: payload.folderSlug,
      angleDeg: payload.angleDeg,
      angleId: payload.angleId,
      fps: payload.fps,
      loopMode: payload.loopMode,
      frames: payload.frames,
      frameOrder: payload.frameOrder,
      sourceImageWidth: 800,
      sourceImageHeight: 200,
      columnsCount: frames.length,
      rowsCount: 1,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    saveAnimationSequence(sequenceConfig);
  };

  const selectedFrame = frames[selectedFrameIndex ?? 0] || frames[0] || null;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        background: '#070b14',
        color: '#f8fafc',
        overflow: 'hidden',
        position: 'relative',
      }}
    >
      {/* Toast Notification */}
      {toastMessage && (
        <div
          style={{
            position: 'absolute',
            top: 10,
            left: '50%',
            transform: 'translateX(-50%)',
            zIndex: 9999,
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid #38bdf8',
            boxShadow: '0 8px 24px rgba(0,0,0,0.8)',
            color: '#38bdf8',
            fontSize: 11.5,
            fontWeight: 700,
            padding: '6px 14px',
            borderRadius: 8,
            display: 'flex',
            alignItems: 'center',
            gap: 6,
          }}
        >
          <Check size={14} /> {toastMessage}
        </div>
      )}

      {/* Top Header Bar */}
      <div
        style={{
          height: 38,
          background: 'rgba(15, 23, 42, 0.92)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 12px',
          flexShrink: 0,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Clapperboard size={14} /> 1.2 GHÉP HOẠT ẢNH CHUYỂN ĐỘNG (ANIMATION FRAME SEQUENCER)
          </div>
          <span style={{ fontSize: 10, color: '#94a3b8' }}>
            {frames.length > 0 ? `${frames.length} Frame(s)` : 'Chưa có frame'} • Cọ tẩy pixel vòng tròn • Hoàn tác Ctrl+Z • Cắt BBox sát viền • Caro Trắng/Đen
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            onClick={() => setIsLoadModalOpen(true)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 12px',
              borderRadius: 6,
              background: 'rgba(56, 189, 248, 0.15)',
              border: '1px solid rgba(56, 189, 248, 0.35)',
              color: '#38bdf8',
              fontSize: 11,
              fontWeight: 700,
              cursor: 'pointer',
              boxShadow: '0 2px 8px rgba(56, 189, 248, 0.2)',
            }}
          >
            <FolderOpen size={13} /> Mở Động Tác
          </button>

          <button
            onClick={() => setIsSaveModalOpen(true)}
            disabled={frames.length === 0}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 12px',
              borderRadius: 6,
              background: frames.length > 0 ? 'linear-gradient(135deg, #0284c7, #a855f7)' : 'rgba(255,255,255,0.05)',
              border: 'none',
              color: frames.length > 0 ? '#ffffff' : '#64748b',
              fontSize: 11,
              fontWeight: 700,
              cursor: frames.length > 0 ? 'pointer' : 'not-allowed',
              boxShadow: frames.length > 0 ? '0 2px 10px rgba(56, 189, 248, 0.3)' : 'none',
            }}
          >
            <Save size={13} /> Lưu Động Tác
          </button>
        </div>
      </div>

      {/* Main Workspace (Top: Live Preview Stage + Right: Properties Panel) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: '1fr 310px',
          flex: '1 1 65%',
          minHeight: 0,
          gap: 8,
          padding: 8,
          overflow: 'hidden',
        }}
      >
        {/* Center: Live Motion Animation Preview Stage */}
        <div style={{ height: '100%', minHeight: 0, overflow: 'hidden' }}>
          <AnimationPreviewStage
            frames={frames}
            frameOrder={frameOrder}
            selectedFrameIndex={selectedFrameIndex}
            fps={fps}
            loopMode={loopMode}
            onionSkinMode={onionSkinMode}
            showBBox={showBBox}
            canUndo={canUndo}
            canRedo={canRedo}
            onUndo={() => undo(frames, frameOrder)}
            onRedo={() => redo(frames, frameOrder)}
            onAutoTrimAllBBox={handleAutoTrimAllBBox}
            onApplyManualCropAllFrames={handleApplyManualCropAllFrames}
            onSelectFrameIndex={setSelectedFrameIndex}
            onUpdateFrameTransform={handleUpdateFrameTransform}
            onUpdateMultipleFramesDataUrl={handleUpdateMultipleFramesDataUrl}
            onToggleOnionSkinMode={handleToggleOnionSkinMode}
            onToggleShowBBox={() => setShowBBox(!showBBox)}
            onLoadDemoFrames={handleLoadDemoFrames}
          />
        </div>

        {/* Right: Timing & Transform Properties Panel */}
        <div style={{ height: '100%', minHeight: 0, overflow: 'hidden' }}>
          <AnimationPropertiesPanel
            selectedFrame={selectedFrame}
            selectedFrameIndex={selectedFrameIndex}
            totalFramesCount={frames.length}
            onUpdateFrameTransform={handleUpdateFrameTransform}
            onApplyTransformToAllFrames={handleApplyTransformToAllFrames}
            onSetAllFramesDuration={handleSetAllFramesDuration}
            onAutoTrimAllBBox={handleAutoTrimAllBBox}
            onOpenSaveModal={() => setIsSaveModalOpen(true)}
            onOpenLoadModal={() => setIsLoadModalOpen(true)}
          />
        </div>
      </div>

      {/* Bottom Area: Filmstrip Frame Sequencer */}
      <div style={{ flex: '0 0 160px', height: 160, minHeight: 160, padding: '0 8px 8px 8px', overflow: 'hidden' }}>
        <AnimationFrameFilmstripBar
          frames={frames}
          frameOrder={frameOrder}
          selectedFrameIndex={selectedFrameIndex}
          loopMode={loopMode}
          onionSkinMode={onionSkinMode}
          onSelectFrameIndex={setSelectedFrameIndex}
          onUpdateFrameOrder={setFrameOrder}
          onUpdateLoopMode={setLoopMode}
          onToggleOnionSkinMode={handleToggleOnionSkinMode}
          onDuplicateFrame={handleDuplicateFrame}
          onDeleteFrame={handleDeleteFrame}
          onMoveFrame={handleMoveFrame}
        />
      </div>

      {/* Save Animation Pose Modal */}
      <AnimationPoseSaveModal
        isOpen={isSaveModalOpen}
        onClose={() => setIsSaveModalOpen(false)}
        frames={frames}
        frameOrder={frameOrder}
        fps={fps}
        loopMode={loopMode}
        onSaveSuccess={handleSaveSuccess}
      />

      {/* Load Saved Animation Sequence Modal */}
      <AnimationSequenceLoadModal
        isOpen={isLoadModalOpen}
        onClose={() => setIsLoadModalOpen(false)}
        onSelectSequence={handleSelectSequence}
      />
    </div>
  );
};
