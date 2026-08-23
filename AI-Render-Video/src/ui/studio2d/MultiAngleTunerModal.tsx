import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Sliders, X } from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DAngle,
  PartAngleOverride,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../../core/assets/Asset2DRegistry';
import { ThreeMultiAngleBillboardEngine } from '../../core/engine2d/ThreeMultiAngleBillboardEngine';
import {
  AngleMenuItem,
  CAMERA_ANGLES_MENU,
  TunerAngleSidebar,
} from './tuner/TunerAngleSidebar';
import {
  TunerDualViewport,
  ViewportSyncMode,
  Canvas2DCompositeMode,
} from './tuner/TunerDualViewport';
import { TunerControlsPanel } from './tuner/TunerControlsPanel';

export interface MultiAngleTunerModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentAssembly: Character2DAssembly;
  activeCameraAngle: Character2DAngle;
  onApplyAssembly: (updated: Character2DAssembly) => void;
  onJumpToAngle?: (angleDeg: number, isTopDown?: boolean) => void;
}

const HAIR_TARGET_SLOTS: { slot: Character2DPartType; label: string; icon: string }[] = [
  { slot: 'toc_truoc', label: 'Mái Trước (Front Bangs)', icon: '💇' },
  { slot: 'dau', label: 'Đỉnh Đầu / Búi Đầu (Crown)', icon: '👑' },
  { slot: 'toc_sau', label: 'Suối Tóc Sau (Back Hair)', icon: '🌊' },
  { slot: 'khuon_mat', label: 'Tóc Mai / Khuôn Mặt (Sideburns)', icon: '✨' },
];

const HAIR_STACK_ORDER: Character2DPartType[] = ['toc_sau', 'dau', 'khuon_mat', 'toc_truoc'];

export function resolvePartTextureForAngle(part: any, angle: Character2DAngle): { url: string; autoMirror: boolean } {
  if (!part) return { url: '', autoMirror: false };
  if (part.angles?.[angle]) return { url: part.angles[angle], autoMirror: false };

  // Top-down multi-angle fallbacks
  if (angle.startsWith('top_down')) {
    if (angle === 'top_down_three_quarter_right') {
      if (part.angles?.top_down_three_quarter_right) return { url: part.angles.top_down_three_quarter_right, autoMirror: false };
      if (part.angles?.top_down_three_quarter_left) return { url: part.angles.top_down_three_quarter_left, autoMirror: true };
      return resolvePartTextureForAngle(part, 'three_quarter_right');
    }
    if (angle === 'top_down_profile_right') {
      if (part.angles?.top_down_profile_right) return { url: part.angles.top_down_profile_right, autoMirror: false };
      if (part.angles?.top_down_profile_left) return { url: part.angles.top_down_profile_left, autoMirror: true };
      return resolvePartTextureForAngle(part, 'profile_right');
    }
    if (angle === 'top_down_back_three_quarter_right') {
      if (part.angles?.top_down_back_three_quarter_right) return { url: part.angles.top_down_back_three_quarter_right, autoMirror: false };
      if (part.angles?.top_down_back_three_quarter_left) return { url: part.angles.top_down_back_three_quarter_left, autoMirror: true };
      return resolvePartTextureForAngle(part, 'back_three_quarter_right');
    }
    if (angle === 'top_down_three_quarter_left') {
      if (part.angles?.top_down_three_quarter_left) return { url: part.angles.top_down_three_quarter_left, autoMirror: false };
      return resolvePartTextureForAngle(part, 'three_quarter_left');
    }
    if (angle === 'top_down_profile_left') {
      if (part.angles?.top_down_profile_left) return { url: part.angles.top_down_profile_left, autoMirror: false };
      return resolvePartTextureForAngle(part, 'profile_left');
    }
    if (angle === 'top_down_back_three_quarter_left') {
      if (part.angles?.top_down_back_three_quarter_left) return { url: part.angles.top_down_back_three_quarter_left, autoMirror: false };
      return resolvePartTextureForAngle(part, 'back_three_quarter_left');
    }
    if (angle === 'top_down_back') {
      if (part.angles?.top_down_back) return { url: part.angles.top_down_back, autoMirror: false };
      return resolvePartTextureForAngle(part, 'back');
    }
    if (part.angles?.top_down) return { url: part.angles.top_down, autoMirror: false };
    return resolvePartTextureForAngle(part, 'front');
  }

  // Standard horizontal symmetry fallbacks
  if (angle === 'three_quarter_right') {
    if (part.angles?.three_quarter_right) return { url: part.angles.three_quarter_right, autoMirror: false };
    if (part.angles?.three_quarter_left) return { url: part.angles.three_quarter_left, autoMirror: true };
  }
  if (angle === 'profile_right') {
    if (part.angles?.profile_right) return { url: part.angles.profile_right, autoMirror: false };
    if (part.angles?.profile_left) return { url: part.angles.profile_left, autoMirror: true };
  }
  if (angle === 'back_three_quarter_left') {
    if (part.angles?.back_three_quarter_left) return { url: part.angles.back_three_quarter_left, autoMirror: false };
  }
  if (angle === 'back_three_quarter_right') {
    if (part.angles?.back_three_quarter_right) return { url: part.angles.back_three_quarter_right, autoMirror: false };
    if (part.angles?.back_three_quarter_left) return { url: part.angles.back_three_quarter_left, autoMirror: true };
  }
  if (angle === 'back' && part.angles?.back) return { url: part.angles.back, autoMirror: false };
  if (part.angles?.[angle]) return { url: part.angles[angle], autoMirror: false };
  if (part.angles?.front) return { url: part.angles.front, autoMirror: false };

  return { url: part.path || '', autoMirror: false };
}

export const MultiAngleTunerModal: React.FC<MultiAngleTunerModalProps> = ({
  isOpen,
  onClose,
  currentAssembly,
  activeCameraAngle,
  onApplyAssembly,
  onJumpToAngle,
}) => {
  const [selectedAngle, setSelectedAngle] = useState<Character2DAngle>(activeCameraAngle || 'front');
  const [selectedSlot, setSelectedSlot] = useState<Character2DPartType>('toc_truoc');
  const [assemblyDraft, setAssemblyDraft] = useState<Character2DAssembly>(() => JSON.parse(JSON.stringify(currentAssembly)));
  const [viewportMode, setViewportMode] = useState<ViewportSyncMode>('split_2d_3d');
  const [canvas2DMode, setCanvas2DMode] = useState<Canvas2DCompositeMode>('composite');
  const [notification, setNotification] = useState<string | null>(null);

  const prevOpenRef = useRef(false);
  const modalThreeContainerRef = useRef<HTMLDivElement>(null);
  const modalThreeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);
  const modal2DCanvasRef = useRef<HTMLCanvasElement>(null);
  const cachedImagesRef = useRef<Map<string, HTMLImageElement>>(new Map());

  // Sync draft ONLY when modal is opened
  useEffect(() => {
    if (isOpen && !prevOpenRef.current) {
      setAssemblyDraft(JSON.parse(JSON.stringify(currentAssembly)));
      if (activeCameraAngle) setSelectedAngle(activeCameraAngle);
    }
    prevOpenRef.current = isOpen;
  }, [isOpen, currentAssembly, activeCameraAngle]);

  // Initialize Three.js Engine inside Modal
  useEffect(() => {
    if (!isOpen || !modalThreeContainerRef.current) return;

    if (!modalThreeEngineRef.current) {
      const engine = new ThreeMultiAngleBillboardEngine(modalThreeContainerRef.current, (res) => {
        setSelectedAngle(res.discreteAngle);
      });
      engine.setBackgroundMode('checkerboard');
      modalThreeEngineRef.current = engine;
    }

    modalThreeEngineRef.current.setAssembly(assemblyDraft);

    const angleInfo = CAMERA_ANGLES_MENU.find((a) => a.id === selectedAngle);
    if (angleInfo) {
      modalThreeEngineRef.current.jumpToAngle(angleInfo.deg, angleInfo.isTop);
    }

    return () => {
      if (modalThreeEngineRef.current) {
        modalThreeEngineRef.current.dispose();
        modalThreeEngineRef.current = null;
      }
    };
  }, [isOpen]);

  // Update Three.js when assemblyDraft or selectedAngle changes
  useEffect(() => {
    if (modalThreeEngineRef.current) {
      modalThreeEngineRef.current.setAssembly(assemblyDraft);
      modalThreeEngineRef.current.applyTransformsForAngle(selectedAngle);
    }
  }, [assemblyDraft, selectedAngle]);

  // Render 2D Canvas with Image Caching for Instant Real-Time Adjustment
  const redraw2DCanvas = useCallback(() => {
    const canvas = modal2DCanvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Dark Checkerboard background
    const size = 16;
    for (let x = 0; x < canvas.width; x += size) {
      for (let y = 0; y < canvas.height; y += size) {
        ctx.fillStyle = ((x / size + y / size) % 2 === 0) ? '#090d16' : '#131b2e';
        ctx.fillRect(x, y, size, size);
      }
    }

    // Center Crosshair
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.2)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(canvas.width / 2, 0);
    ctx.lineTo(canvas.width / 2, canvas.height);
    ctx.moveTo(0, canvas.height / 2);
    ctx.lineTo(canvas.width, canvas.height / 2);
    ctx.stroke();

    // Draw stack of layers sorted by active Z-Index
    const sortedHairSlots = [...HAIR_TARGET_SLOTS.map((s) => s.slot)].sort((a, b) => {
      const partA = assemblyDraft.parts[a];
      const partB = assemblyDraft.parts[b];
      const hierA = PART_HIERARCHY_CONFIG[a];
      const hierB = PART_HIERARCHY_CONFIG[b];
      const overrideA = partA?.angle_overrides?.[selectedAngle];
      const overrideB = partB?.angle_overrides?.[selectedAngle];
      const zA = overrideA?.z_depth_3d ?? partA?.z_depth_3d ?? hierA?.defaultZDepth3D ?? 0;
      const zB = overrideB?.z_depth_3d ?? partB?.z_depth_3d ?? hierB?.defaultZDepth3D ?? 0;
      return zA - zB;
    });

    sortedHairSlots.forEach((slot) => {
      const part = assemblyDraft.parts[slot];
      if (!part) return;

      if (canvas2DMode === 'isolated' && slot !== selectedSlot) return;

      const { url, autoMirror } = resolvePartTextureForAngle(part, selectedAngle);
      if (!url) return;

      let img = cachedImagesRef.current.get(url);
      if (!img) {
        img = new Image();
        img.crossOrigin = 'anonymous';
        img.src = url;
        img.onload = () => {
          cachedImagesRef.current.set(url, img!);
          redraw2DCanvas();
        };
        return;
      }

      const override = part.angle_overrides?.[selectedAngle] || {};
      const isVis = override.visible !== false;
      if (!isVis && canvas2DMode === 'composite') return;

      const isCurrentSelected = slot === selectedSlot;
      const hierarchy = PART_HIERARCHY_CONFIG[slot];
      const defaultOff = hierarchy?.defaultOffset ?? [0, 0];
      const offX = override.offset?.[0] ?? (part.offset && (part.offset[0] !== 0 || part.offset[1] !== 0) ? part.offset[0] : defaultOff[0]);
      const offY = override.offset?.[1] ?? (part.offset && (part.offset[0] !== 0 || part.offset[1] !== 0) ? part.offset[1] : defaultOff[1]);
      const scX = override.scale?.[0] ?? part.scale?.[0] ?? 1.0;
      const scY = override.scale?.[1] ?? part.scale?.[1] ?? 1.0;
      const rot = (override.rotation ?? part.rotation ?? 0) * (Math.PI / 180);
      const flipX = autoMirror ? !(override.flipX ?? part.flipX ?? false) : (override.flipX ?? part.flipX ?? false);

      ctx.save();
      ctx.translate(canvas.width / 2 + offX * 0.7, canvas.height / 2 + offY * 0.7);
      ctx.rotate(rot);
      ctx.scale(scX * (flipX ? -1 : 1), scY);

      const baseSize = 240;
      const imgAspect = (img.width && img.height) ? (img.width / img.height) : 1.0;
      let drawW = baseSize;
      let drawH = baseSize;
      if (imgAspect >= 1.0) {
        drawW = baseSize;
        drawH = baseSize / imgAspect;
      } else {
        drawH = baseSize;
        drawW = baseSize * imgAspect;
      }

      if (!isVis && isCurrentSelected) {
        ctx.globalAlpha = 0.35;
      }

      ctx.drawImage(img, -drawW / 2, -drawH / 2, drawW, drawH);

      // Highlight selected layer
      if (isCurrentSelected) {
        ctx.strokeStyle = '#38bdf8';
        ctx.lineWidth = 2;
        ctx.setLineDash([4, 4]);
        ctx.strokeRect(-drawW / 2, -drawH / 2, drawW, drawH);
        ctx.setLineDash([]);

        ctx.beginPath();
        ctx.arc(0, 0, 4, 0, Math.PI * 2);
        ctx.fillStyle = '#38bdf8';
        ctx.fill();
      }

      ctx.restore();
    });
  }, [assemblyDraft, selectedAngle, selectedSlot, canvas2DMode]);

  useEffect(() => {
    redraw2DCanvas();
  }, [redraw2DCanvas]);

  if (!isOpen) return null;

  const updateOverride = (patch: Partial<PartAngleOverride>) => {
    setAssemblyDraft((prev) => {
      const next: Character2DAssembly = JSON.parse(JSON.stringify(prev));
      let part = next.parts[selectedSlot];
      if (!part) {
        part = {
          name: PART_HIERARCHY_CONFIG[selectedSlot]?.label || selectedSlot,
          path: '',
          pivot: [0.5, 0.5],
          flipX: false,
          flipY: false,
          z_index: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZ ?? 0,
          z_depth_3d: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZDepth3D ?? 0,
          offset: [0, 0],
          scale: [1, 1],
          rotation: 0,
          opacity: 1,
          angle_overrides: {},
        };
        next.parts[selectedSlot] = part;
      }

      const activePart = next.parts[selectedSlot]!;
      if (!activePart.angle_overrides) activePart.angle_overrides = {};
      const currentOv = activePart.angle_overrides[selectedAngle] || {};

      activePart.angle_overrides[selectedAngle] = {
        ...currentOv,
        ...patch,
      };

      return next;
    });
  };

  const handleSelectAngle = (item: AngleMenuItem) => {
    setSelectedAngle(item.id);
    if (modalThreeEngineRef.current) {
      modalThreeEngineRef.current.jumpToAngle(item.deg, item.isTop);
    }
    if (onJumpToAngle) {
      onJumpToAngle(item.deg, item.isTop);
    }
  };

  const handleResetCurrentAngle = () => {
    setAssemblyDraft((prev) => {
      const next: Character2DAssembly = JSON.parse(JSON.stringify(prev));
      const part = next.parts[selectedSlot];
      if (part?.angle_overrides?.[selectedAngle]) {
        delete part.angle_overrides[selectedAngle];
      }
      return next;
    });
    showToast('Đã đặt lại thông số góc này về mặc định');
  };

  const handleCopyToAllAngles = () => {
    setAssemblyDraft((prev) => {
      const next: Character2DAssembly = JSON.parse(JSON.stringify(prev));
      const part = next.parts[selectedSlot];
      if (!part) return prev;
      if (!part.angle_overrides) part.angle_overrides = {};

      const currentOv = part.angle_overrides[selectedAngle] || {};
      CAMERA_ANGLES_MENU.forEach((ang) => {
        part.angle_overrides![ang.id] = { ...currentOv };
      });

      return next;
    });
    showToast('Đã đồng bộ thông số sang tất cả các góc');
  };

  const handleApplyToCharacter = () => {
    onApplyAssembly(assemblyDraft);
    showToast('💾 Đã áp dụng & lưu cấu hình vào nhân vật');
  };

  const handleApplyAndClose = () => {
    onApplyAssembly(assemblyDraft);
    onClose();
  };

  const showToast = (msg: string) => {
    setNotification(msg);
    setTimeout(() => setNotification(null), 2500);
  };

  const currentAngleInfo = CAMERA_ANGLES_MENU.find((a) => a.id === selectedAngle);

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0, 0, 0, 0.88)',
        backdropFilter: 'blur(10px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1100,
        padding: '12px',
      }}
    >
      <div
        style={{
          background: '#070b16',
          border: '1px solid rgba(56, 189, 248, 0.4)',
          borderRadius: 14,
          width: '96vw',
          maxWidth: 1560,
          height: '92vh',
          maxHeight: '94vh',
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 25px 70px rgba(0,0,0,0.95), 0 0 40px rgba(56, 189, 248, 0.2)',
          overflow: 'hidden',
        }}
      >
        {/* Top Header */}
        <div
          style={{
            padding: '12px 20px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'linear-gradient(90deg, rgba(14, 165, 233, 0.15) 0%, rgba(15, 23, 42, 0.8) 100%)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Sliders size={20} color="#38bdf8" />
            <div>
              <div style={{ fontSize: 14.5, fontWeight: 800, color: '#f8fafc', letterSpacing: '0.3px' }}>
                🎛️ BÀN CÂN CHỈNH ĐA GÓC & TÓC 3D (STUDIO TUNER PRO)
              </div>
              <div style={{ fontSize: 11, color: '#94a3b8' }}>
                Chọn góc quay bên menu dọc để cân chỉnh lệch tóc, thu phóng tỉ lệ, và đồng bộ trực tiếp giữa 2D Canvas & 3D Billboard
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button
              onClick={handleApplyToCharacter}
              style={{
                background: 'linear-gradient(135deg, #0284c7, #0369a1)',
                border: '1px solid #38bdf8',
                borderRadius: 8,
                color: '#ffffff',
                fontWeight: 700,
                fontSize: 12,
                padding: '6px 14px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                boxShadow: '0 2px 10px rgba(56, 189, 248, 0.3)',
              }}
            >
              💾 Lưu Cấu Hình
            </button>
            <button
              onClick={handleApplyAndClose}
              style={{
                background: 'rgba(255, 255, 255, 0.08)',
                border: '1px solid rgba(255, 255, 255, 0.12)',
                borderRadius: 8,
                color: '#cbd5e1',
                fontSize: 12,
                padding: '6px 12px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
            >
              <X size={16} /> Đóng
            </button>
          </div>
        </div>

        {/* Hair Part Target Selector Bar */}
        <div
          style={{
            padding: '8px 20px',
            background: 'rgba(11, 19, 41, 0.8)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            overflowX: 'auto',
          }}
        >
          <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', whiteSpace: 'nowrap', marginRight: 4 }}>
            💇 TẦNG TÓC CẦN CHỈNH:
          </span>

          {HAIR_TARGET_SLOTS.map((target) => {
            const isSelected = selectedSlot === target.slot;
            const p = assemblyDraft.parts[target.slot];
            const hasOverride = Boolean(p?.angle_overrides?.[selectedAngle]);
            const isHidden = p?.angle_overrides?.[selectedAngle]?.visible === false;

            return (
              <button
                key={target.slot}
                onClick={() => setSelectedSlot(target.slot)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '6px 12px',
                  borderRadius: 6,
                  border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                  background: isSelected ? 'rgba(56, 189, 248, 0.22)' : 'rgba(255,255,255,0.03)',
                  color: isSelected ? '#ffffff' : '#cbd5e1',
                  cursor: 'pointer',
                  fontSize: 11,
                  fontWeight: isSelected ? 700 : 500,
                  boxShadow: isSelected ? '0 0 12px rgba(56, 189, 248, 0.25)' : 'none',
                }}
              >
                <span>{target.icon}</span>
                <span>{target.label}</span>
                {isHidden && <span style={{ fontSize: 9, color: '#f87171', fontWeight: 700 }}>[Ẩn]</span>}
                {hasOverride && !isHidden && <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 700 }}>[Chỉnh]</span>}
              </button>
            );
          })}
        </div>

        {/* 3-Column Main Body */}
        <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '230px 1fr 380px', overflow: 'hidden', minHeight: 0 }}>
          {/* Left Column: Vertical Camera Angles */}
          <TunerAngleSidebar
            selectedAngle={selectedAngle}
            onSelectAngle={handleSelectAngle}
            selectedSlot={selectedSlot}
            assemblyDraft={assemblyDraft}
          />

          {/* Center Column: Live 2D Canvas & 3D WebGL Viewport */}
          <TunerDualViewport
            viewportMode={viewportMode}
            setViewportMode={setViewportMode}
            canvas2DMode={canvas2DMode}
            setCanvas2DMode={setCanvas2DMode}
            currentAngleInfo={currentAngleInfo}
            modal2DCanvasRef={modal2DCanvasRef}
            modalThreeContainerRef={modalThreeContainerRef}
            selectedSlotLabel={PART_HIERARCHY_CONFIG[selectedSlot]?.label || selectedSlot}
          />

          {/* Right Column: Fine-Tuning Controls */}
          <TunerControlsPanel
            selectedSlot={selectedSlot}
            selectedAngle={selectedAngle}
            currentAngleInfo={currentAngleInfo}
            assemblyDraft={assemblyDraft}
            updateOverride={updateOverride}
            onResetCurrentAngle={handleResetCurrentAngle}
            onCopyToAllAngles={handleCopyToAllAngles}
          />
        </div>

        {/* Toast notification */}
        {notification && (
          <div
            style={{
              position: 'fixed',
              bottom: 30,
              left: '50%',
              transform: 'translateX(-50%)',
              background: 'rgba(15, 23, 42, 0.95)',
              border: '1px solid #38bdf8',
              color: '#38bdf8',
              padding: '10px 22px',
              borderRadius: 8,
              fontSize: 12.5,
              fontWeight: 700,
              boxShadow: '0 10px 30px rgba(0,0,0,0.8), 0 0 20px rgba(56, 189, 248, 0.3)',
              zIndex: 1200,
            }}
          >
            {notification}
          </div>
        )}
      </div>
    </div>
  );
};
