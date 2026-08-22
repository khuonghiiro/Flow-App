import React, { useState, useRef, useEffect } from 'react';
import {
  X,
  Eye,
  EyeOff,
  RotateCcw,
  Sliders,
  Move,
  Maximize2,
  FlipHorizontal,
  Check,
  Copy,
  Box,
  Image as ImageIcon,
  Columns,
  Compass,
  Layers,
} from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DAngle,
  PartAngleOverride,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../../core/assets/Asset2DRegistry';
import { ThreeMultiAngleBillboardEngine } from '../../core/engine2d/ThreeMultiAngleBillboardEngine';

export interface MultiAngleTunerModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentAssembly: Character2DAssembly;
  activeCameraAngle: Character2DAngle;
  onApplyAssembly: (updated: Character2DAssembly) => void;
  onJumpToAngle?: (angleDeg: number, isTopDown?: boolean) => void;
}

type ViewportSyncMode = 'split_2d_3d' | '3d_only' | '2d_only';
type Canvas2DCompositeMode = 'composite' | 'isolated';

interface AngleMenuItem {
  id: Character2DAngle;
  label: string;
  compass: string;
  deg: number;
  isTop?: boolean;
  category: 'horizontal' | 'top_down';
}

const CAMERA_ANGLES_MENU: AngleMenuItem[] = [
  // Horizontal 360°
  { id: 'front', label: 'Chính Diện (0°)', compass: 'S', deg: 0, category: 'horizontal' },
  { id: 'three_quarter_left', label: 'Nghiêng Trái (45°)', compass: 'SE', deg: 45, category: 'horizontal' },
  { id: 'profile_left', label: 'Ngang Trái (90°)', compass: 'E', deg: 90, category: 'horizontal' },
  { id: 'back_three_quarter_left', label: 'Sau Trái (135°)', compass: 'NE', deg: 135, category: 'horizontal' },
  { id: 'back', label: 'Sau Lưng (180°)', compass: 'N', deg: 180, category: 'horizontal' },
  { id: 'back_three_quarter_right', label: 'Sau Phải (225°)', compass: 'NW', deg: 225, category: 'horizontal' },
  { id: 'profile_right', label: 'Ngang Phải (270°)', compass: 'W', deg: 270, category: 'horizontal' },
  { id: 'three_quarter_right', label: 'Nghiêng Phải (315°)', compass: 'SW', deg: 315, category: 'horizontal' },

  // Top-Down Bird's Eye
  { id: 'top_down', label: 'Đỉnh Đầu 0°', compass: '👑 0°', deg: 0, isTop: true, category: 'top_down' },
  { id: 'top_down_three_quarter_left', label: 'Đỉnh Nghiêng Trái 45°', compass: '👑 45°', deg: 45, isTop: true, category: 'top_down' },
  { id: 'top_down_profile_left', label: 'Đỉnh Ngang Trái 90°', compass: '👑 90°', deg: 90, isTop: true, category: 'top_down' },
  { id: 'top_down_back', label: 'Đỉnh Sau Lưng 180°', compass: '👑 180°', deg: 180, isTop: true, category: 'top_down' },
];

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

  // Sync draft ONLY when modal is opened (transition from closed to open)
  useEffect(() => {
    if (isOpen && !prevOpenRef.current) {
      setAssemblyDraft(JSON.parse(JSON.stringify(currentAssembly)));
      if (activeCameraAngle) setSelectedAngle(activeCameraAngle);
    }
    prevOpenRef.current = isOpen;
  }, [isOpen, currentAssembly, activeCameraAngle]);

  // Initialize Three.js Engine inside Modal with Angle Change Detection
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

  // Update Three.js when assemblyDraft changes
  useEffect(() => {
    if (modalThreeEngineRef.current) {
      modalThreeEngineRef.current.setAssembly(assemblyDraft);
    }
  }, [assemblyDraft]);

  // Render 2D Composite or Isolated Canvas View
  useEffect(() => {
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

    // Grid center cross lines
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(canvas.width / 2, 0);
    ctx.lineTo(canvas.width / 2, canvas.height);
    ctx.moveTo(0, canvas.height / 2);
    ctx.lineTo(canvas.width, canvas.height / 2);
    ctx.stroke();

    const slotsToRender = canvas2DMode === 'composite' 
      ? (selectedAngle.startsWith('top_down') ? (['toc_sau', 'khuon_mat', 'toc_truoc', 'dau'] as Character2DPartType[]) : (['toc_sau', 'khuon_mat', 'toc_truoc'] as Character2DPartType[])) 
      : [selectedSlot];

    // Load and render images in stack order
    const imageLoadPromises = slotsToRender.map((slot) => {
      return new Promise<{ slot: Character2DPartType; img: HTMLImageElement | null; isMirror: boolean }>((resolve) => {
        const part = assemblyDraft.parts[slot];
        if (!part) {
          resolve({ slot, img: null, isMirror: false });
          return;
        }

        const { url, autoMirror } = resolvePartTextureForAngle(part, selectedAngle);
        if (!url) {
          resolve({ slot, img: null, isMirror: false });
          return;
        }

        const img = new Image();
        img.crossOrigin = 'anonymous';
        img.src = url;
        img.onload = () => resolve({ slot, img, isMirror: autoMirror });
        img.onerror = () => resolve({ slot, img: null, isMirror: false });
      });
    });

    Promise.all(imageLoadPromises).then((loadedLayers) => {
      loadedLayers.forEach(({ slot, img, isMirror }) => {
        if (!img) return;

        const part = assemblyDraft.parts[slot];
        if (!part) return;

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
        const flipX = isMirror ? !(override.flipX ?? part.flipX ?? false) : (override.flipX ?? part.flipX ?? false);

        ctx.save();
        ctx.translate(canvas.width / 2 + offX * 0.7, canvas.height / 2 + offY * 0.7);
        ctx.rotate(rot);
        ctx.scale(scX * (flipX ? -1 : 1), scY);

        const baseSize = 220;
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

        // Highlight currently selected layer with bounding box & crosshair
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
    });
  }, [selectedSlot, selectedAngle, assemblyDraft, viewportMode, canvas2DMode]);

  if (!isOpen) return null;

  const currentPart = assemblyDraft.parts[selectedSlot];
  const override: PartAngleOverride = currentPart?.angle_overrides?.[selectedAngle] || {};

  const isVisible = override.visible !== false;
  const currentOffsetX = override.offset?.[0] ?? currentPart?.offset?.[0] ?? 0;
  const currentOffsetY = override.offset?.[1] ?? currentPart?.offset?.[1] ?? 0;
  const currentScaleX = override.scale?.[0] ?? currentPart?.scale?.[0] ?? 1.0;
  const currentScaleY = override.scale?.[1] ?? currentPart?.scale?.[1] ?? 1.0;
  const currentRotation = override.rotation ?? currentPart?.rotation ?? 0;
  const currentFlipX = override.flipX ?? currentPart?.flipX ?? false;
  const currentZDepth = override.z_depth_3d ?? currentPart?.z_depth_3d ?? PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZDepth3D ?? 0;

  const updateOverride = (patch: Partial<PartAngleOverride>) => {
    setAssemblyDraft((prev) => {
      const next: Character2DAssembly = JSON.parse(JSON.stringify(prev));
      const part = next.parts[selectedSlot];
      if (!part) return prev;

      if (!part.angle_overrides) part.angle_overrides = {};
      const currentOv = part.angle_overrides[selectedAngle] || {};

      part.angle_overrides[selectedAngle] = {
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

      const currentOv = part.angle_overrides[selectedAngle] || {
        offset: [currentOffsetX, currentOffsetY],
        scale: [currentScaleX, currentScaleY],
        rotation: currentRotation,
        flipX: currentFlipX,
        z_depth_3d: currentZDepth,
        visible: isVisible,
      };

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

        {/* 3-Column Main Body: Left (Menu Dọc Góc Quay 230px), Center (Dual Viewport flex: 1), Right (Hair Tuner Controls 380px) */}
        <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '230px 1fr 380px', overflow: 'hidden', minHeight: 0 }}>
          {/* Left: Vertical Camera Angle Tab Menu */}
          <div
            style={{
              padding: '12px 10px',
              background: 'rgba(9, 14, 28, 0.95)',
              borderRight: '1px solid rgba(255, 255, 255, 0.08)',
              display: 'flex',
              flexDirection: 'column',
              gap: 4,
              overflowY: 'auto',
            }}
          >
            <div style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 5 }}>
              <Compass size={13} /> 🎥 GÓC QUAY CAMERA:
            </div>

            {/* Horizontal Angles Category */}
            <div style={{ fontSize: 9.5, fontWeight: 600, color: '#64748b', marginTop: 4, marginBottom: 2 }}>
              XOAY NGANG 360°:
            </div>
            {CAMERA_ANGLES_MENU.filter((a) => a.category === 'horizontal').map((ang) => {
              const isSelected = selectedAngle === ang.id;
              const hasOverride = Boolean(assemblyDraft.parts[selectedSlot]?.angle_overrides?.[ang.id]);

              return (
                <button
                  key={ang.id}
                  onClick={() => handleSelectAngle(ang)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '7px 10px',
                    borderRadius: 6,
                    border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                    background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
                    color: isSelected ? '#38bdf8' : '#cbd5e1',
                    cursor: 'pointer',
                    fontSize: 10.5,
                    fontWeight: isSelected ? 700 : 500,
                    textAlign: 'left',
                    boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.2)' : 'none',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ fontSize: 9, padding: '1px 5px', borderRadius: 3, background: isSelected ? '#0284c7' : 'rgba(255,255,255,0.08)', color: '#fff', fontWeight: 700 }}>
                      {ang.compass}
                    </span>
                    <span>{ang.label}</span>
                  </div>
                  {hasOverride && <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 700 }}>•</span>}
                </button>
              );
            })}

            {/* Top-Down Angles Category */}
            <div style={{ fontSize: 9.5, fontWeight: 600, color: '#64748b', marginTop: 10, marginBottom: 2 }}>
              SOI ĐỈNH ĐẦU TỪ TRÊN XUỐNG:
            </div>
            {CAMERA_ANGLES_MENU.filter((a) => a.category === 'top_down').map((ang) => {
              const isSelected = selectedAngle === ang.id;
              const hasOverride = Boolean(assemblyDraft.parts[selectedSlot]?.angle_overrides?.[ang.id]);

              return (
                <button
                  key={ang.id}
                  onClick={() => handleSelectAngle(ang)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '7px 10px',
                    borderRadius: 6,
                    border: isSelected ? '1.5px solid #eab308' : '1px solid rgba(255,255,255,0.06)',
                    background: isSelected ? 'rgba(234, 179, 8, 0.2)' : 'rgba(255,255,255,0.02)',
                    color: isSelected ? '#facc15' : '#cbd5e1',
                    cursor: 'pointer',
                    fontSize: 10.5,
                    fontWeight: isSelected ? 700 : 500,
                    textAlign: 'left',
                    boxShadow: isSelected ? '0 0 10px rgba(234, 179, 8, 0.2)' : 'none',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ fontSize: 9, padding: '1px 5px', borderRadius: 3, background: isSelected ? '#ca8a04' : 'rgba(255,255,255,0.08)', color: '#fff', fontWeight: 700 }}>
                      {ang.compass}
                    </span>
                    <span>{ang.label}</span>
                  </div>
                  {hasOverride && <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 700 }}>•</span>}
                </button>
              );
            })}
          </div>

          {/* Center: Live 2D & 3D Viewport Synchronizer */}
          <div
            style={{
              padding: 14,
              background: 'rgba(5, 8, 18, 0.95)',
              borderRight: '1px solid rgba(255, 255, 255, 0.08)',
              display: 'flex',
              flexDirection: 'column',
              gap: 10,
              overflow: 'hidden',
              minHeight: 0,
            }}
          >
            {/* Viewport Top Bar */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 6 }}>
              <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Box size={15} /> XEM TRƯỚC: {CAMERA_ANGLES_MENU.find((a) => a.id === selectedAngle)?.label}
              </div>

              <div style={{ display: 'flex', gap: 6 }}>
                {/* 2D Mode Switcher (Composite vs Isolated) */}
                <div style={{ display: 'flex', gap: 2, background: 'rgba(0,0,0,0.6)', padding: 2, borderRadius: 5, border: '1px solid rgba(255,255,255,0.1)' }}>
                  <button
                    onClick={() => setCanvas2DMode('composite')}
                    style={{
                      padding: '3px 7px',
                      fontSize: 9.5,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: 'none',
                      background: canvas2DMode === 'composite' ? '#38bdf8' : 'transparent',
                      color: canvas2DMode === 'composite' ? '#090d16' : '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                    title="Ghép đầy đủ các lớp: Tóc sau + Đỉnh đầu + Tóc mai + Mái trước"
                  >
                    <Layers size={11} /> 🎭 Ghép Toàn Bộ Tóc
                  </button>
                  <button
                    onClick={() => setCanvas2DMode('isolated')}
                    style={{
                      padding: '3px 7px',
                      fontSize: 9.5,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: 'none',
                      background: canvas2DMode === 'isolated' ? '#38bdf8' : 'transparent',
                      color: canvas2DMode === 'isolated' ? '#090d16' : '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                    title="Chỉ hiển thị riêng lớp đang chọn"
                  >
                    <ImageIcon size={11} /> 🎯 Chỉ Lớp Này
                  </button>
                </div>

                {/* Viewport Split Switcher */}
                <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.6)', padding: 2, borderRadius: 5 }}>
                  <button
                    onClick={() => setViewportMode('split_2d_3d')}
                    style={{
                      padding: '3px 7px',
                      fontSize: 9.5,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: 'none',
                      background: viewportMode === 'split_2d_3d' ? '#0284c7' : 'transparent',
                      color: viewportMode === 'split_2d_3d' ? '#fff' : '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                  >
                    <Columns size={11} /> Song Song 2D + 3D
                  </button>
                  <button
                    onClick={() => setViewportMode('3d_only')}
                    style={{
                      padding: '3px 7px',
                      fontSize: 9.5,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: 'none',
                      background: viewportMode === '3d_only' ? '#0284c7' : 'transparent',
                      color: viewportMode === '3d_only' ? '#fff' : '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                  >
                    <Box size={11} /> 3D View
                  </button>
                  <button
                    onClick={() => setViewportMode('2d_only')}
                    style={{
                      padding: '3px 7px',
                      fontSize: 9.5,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: 'none',
                      background: viewportMode === '2d_only' ? '#0284c7' : 'transparent',
                      color: viewportMode === '2d_only' ? '#fff' : '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                  >
                    <ImageIcon size={11} /> 2D Layer
                  </button>
                </div>
              </div>
            </div>

            {/* Viewport Canvas Containers */}
            <div style={{ flex: 1, display: 'flex', gap: 10, minHeight: 0, overflow: 'hidden' }}>
              {/* 2D Canvas View */}
              {(viewportMode === '2d_only' || viewportMode === 'split_2d_3d') && (
                <div
                  style={{
                    flex: 1,
                    display: 'flex',
                    flexDirection: 'column',
                    background: '#040711',
                    borderRadius: 10,
                    overflow: 'hidden',
                    border: '1px solid rgba(56, 189, 248, 0.3)',
                    position: 'relative',
                  }}
                >
                  <div style={{ position: 'absolute', top: 8, left: 10, fontSize: 10, fontWeight: 700, color: '#38bdf8', background: 'rgba(0,0,0,0.75)', padding: '3px 8px', borderRadius: 4, zIndex: 10 }}>
                    {canvas2DMode === 'composite'
                      ? `🎭 2D Ghép Lớp Tóc (Đang chọn: ${PART_HIERARCHY_CONFIG[selectedSlot]?.label})`
                      : `🎯 Lớp 2D: ${PART_HIERARCHY_CONFIG[selectedSlot]?.label}`}
                  </div>
                  <canvas
                    ref={modal2DCanvasRef}
                    width={400}
                    height={500}
                    style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                  />
                </div>
              )}

              {/* 3D WebGL Billboard View */}
              {(viewportMode === '3d_only' || viewportMode === 'split_2d_3d') && (
                <div
                  style={{
                    flex: 1,
                    display: 'flex',
                    flexDirection: 'column',
                    background: '#040711',
                    borderRadius: 10,
                    overflow: 'hidden',
                    border: '1px solid rgba(56, 189, 248, 0.4)',
                    position: 'relative',
                  }}
                >
                  <div style={{ position: 'absolute', top: 8, left: 10, fontSize: 10, fontWeight: 700, color: '#4ade80', background: 'rgba(0,0,0,0.75)', padding: '3px 8px', borderRadius: 4, zIndex: 10 }}>
                    🌟 Nhân Vật 3D Billboard ({CAMERA_ANGLES_MENU.find((a) => a.id === selectedAngle)?.label})
                  </div>
                  <div
                    ref={modalThreeContainerRef}
                    style={{ width: '100%', height: '100%' }}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Right: Fine-Tuning Hair Controllers */}
          <div style={{ padding: 14, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(9, 14, 28, 0.95)' }}>
            {/* Active Part Header & Visibility */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                background: 'rgba(0,0,0,0.4)',
                padding: '10px 12px',
                borderRadius: 8,
                border: '1px solid rgba(255,255,255,0.08)',
              }}
            >
              <div>
                <span style={{ fontSize: 12.5, fontWeight: 700, color: '#38bdf8' }}>
                  {PART_HIERARCHY_CONFIG[selectedSlot]?.label || selectedSlot}
                </span>
                <div style={{ fontSize: 10, color: '#94a3b8', marginTop: 2 }}>
                  Góc: {CAMERA_ANGLES_MENU.find((a) => a.id === selectedAngle)?.label}
                </div>
              </div>

              {/* Visibility Toggle Button */}
              <button
                onClick={() => updateOverride({ visible: !isVisible })}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  padding: '5px 10px',
                  fontSize: 10.5,
                  fontWeight: 700,
                  borderRadius: 6,
                  border: isVisible ? '1px solid rgba(34, 197, 94, 0.4)' : '1px solid rgba(239, 68, 68, 0.4)',
                  background: isVisible ? 'rgba(34, 197, 94, 0.2)' : 'rgba(239, 68, 68, 0.2)',
                  color: isVisible ? '#4ade80' : '#f87171',
                  cursor: 'pointer',
                }}
              >
                {isVisible ? <Eye size={13} /> : <EyeOff size={13} />}
                {isVisible ? 'Hiện' : 'Ẩn'}
              </button>
            </div>

            {/* Position Offsets (X & Y) */}
            <div style={{ background: 'rgba(15, 23, 42, 0.6)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                <Move size={13} /> DỊCH CHUYỂN VỊ TRÍ (CÂN CHỈNH LỆCH TÓC):
              </div>

              {/* Offset X */}
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                  <span>↔️ Vị trí Ngang (X):</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{Math.round(currentOffsetX)} px</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <input
                    type="range"
                    min="-150"
                    max="150"
                    step="1"
                    value={currentOffsetX}
                    onChange={(e) => updateOverride({ offset: [parseInt(e.target.value), currentOffsetY] })}
                    style={{ flex: 1 }}
                  />
                  <button onClick={() => updateOverride({ offset: [currentOffsetX - 5, currentOffsetY] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>-5</button>
                  <button onClick={() => updateOverride({ offset: [0, currentOffsetY] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 4, cursor: 'pointer' }}>0</button>
                  <button onClick={() => updateOverride({ offset: [currentOffsetX + 5, currentOffsetY] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>+5</button>
                </div>
              </div>

              {/* Offset Y */}
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                  <span>↕️ Vị trí Dọc (Y):</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{Math.round(currentOffsetY)} px</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <input
                    type="range"
                    min="-150"
                    max="150"
                    step="1"
                    value={currentOffsetY}
                    onChange={(e) => updateOverride({ offset: [currentOffsetX, parseInt(e.target.value)] })}
                    style={{ flex: 1 }}
                  />
                  <button onClick={() => updateOverride({ offset: [currentOffsetX, currentOffsetY - 5] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>-5</button>
                  <button onClick={() => updateOverride({ offset: [currentOffsetX, 0] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 4, cursor: 'pointer' }}>0</button>
                  <button onClick={() => updateOverride({ offset: [currentOffsetX, currentOffsetY + 5] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>+5</button>
                </div>
              </div>
            </div>

            {/* Scale & Proportion Control */}
            <div style={{ background: 'rgba(15, 23, 42, 0.6)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                <Maximize2 size={13} /> TỈ LỆ KÍCH THƯỚC (THU NHỎ / PHÓNG TO VỪA VẶN):
              </div>

              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                  <span>🔍 Kích Cỡ Toàn Bộ (Scale):</span>
                  <span style={{ color: '#4ade80', fontWeight: 700 }}>{(currentScaleX * 100).toFixed(0)}%</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <input
                    type="range"
                    min="0.3"
                    max="2.0"
                    step="0.02"
                    value={currentScaleX}
                    onChange={(e) => {
                      const val = parseFloat(e.target.value);
                      updateOverride({ scale: [val, val] });
                    }}
                    style={{ flex: 1 }}
                  />
                  <button onClick={() => updateOverride({ scale: [currentScaleX * 0.9, currentScaleY * 0.9] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>-10%</button>
                  <button onClick={() => updateOverride({ scale: [1.0, 1.0] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 4, cursor: 'pointer' }}>100%</button>
                  <button onClick={() => updateOverride({ scale: [currentScaleX * 1.1, currentScaleY * 1.1] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>+10%</button>
                </div>
              </div>
            </div>

            {/* Rotation & Flip */}
            <div style={{ background: 'rgba(15, 23, 42, 0.6)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              {/* Rotation */}
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                  <span>🔄 Xoay Góc:</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{Math.round(currentRotation)}°</span>
                </div>
                <input
                  type="range"
                  min="-90"
                  max="90"
                  step="1"
                  value={currentRotation}
                  onChange={(e) => updateOverride({ rotation: parseInt(e.target.value) })}
                  style={{ width: '100%' }}
                />
              </div>

              {/* Flip Horizontal */}
              <div>
                <div style={{ fontSize: 10, color: '#cbd5e1', marginBottom: 4 }}>
                  <span>🔀 Lật Ngang:</span>
                </div>
                <button
                  onClick={() => updateOverride({ flipX: !currentFlipX })}
                  style={{
                    width: '100%',
                    padding: '6px',
                    fontSize: 10.5,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: currentFlipX ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: currentFlipX ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.04)',
                    color: currentFlipX ? '#38bdf8' : '#cbd5e1',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 5,
                  }}
                >
                  <FlipHorizontal size={13} />
                  {currentFlipX ? 'Đã Lật' : 'Mặc Định'}
                </button>
              </div>
            </div>

            {/* Quick Action Tools */}
            <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
              <button
                onClick={handleResetCurrentAngle}
                style={{
                  flex: 1,
                  padding: '7px 10px',
                  fontSize: 10.5,
                  fontWeight: 600,
                  borderRadius: 6,
                  background: 'rgba(255,255,255,0.06)',
                  color: '#e2e8f0',
                  border: '1px solid rgba(255,255,255,0.1)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 4,
                }}
              >
                <RotateCcw size={12} /> Đặt Lại Góc Này
              </button>

              <button
                onClick={handleCopyToAllAngles}
                style={{
                  flex: 1,
                  padding: '7px 10px',
                  fontSize: 10.5,
                  fontWeight: 600,
                  borderRadius: 6,
                  background: 'rgba(56, 189, 248, 0.12)',
                  color: '#38bdf8',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 4,
                }}
              >
                <Copy size={12} /> Đồng Bộ Mọi Góc
              </button>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '12px 20px',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(11, 19, 41, 0.95)',
          }}
        >
          <div style={{ fontSize: 11, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 6 }}>
            {notification ? (
              <span>✓ {notification}</span>
            ) : (
              <span style={{ color: '#94a3b8' }}>Mọi thao tác vị trí, tỉ lệ và ẩn/hiện được cập nhật đồng bộ trực tiếp vào không gian 3D Billboard.</span>
            )}
          </div>

          <button
            onClick={() => {
              onApplyAssembly(assemblyDraft);
              onClose();
            }}
            style={{
              padding: '8px 22px',
              fontSize: 11.5,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #2563eb)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              boxShadow: '0 2px 12px rgba(2, 132, 199, 0.45)',
            }}
          >
            <Check size={14} /> Hoàn Tất & Áp Dụng
          </button>
        </div>
      </div>
    </div>
  );
};
