import React, { useState, useRef, useEffect } from 'react';
import {
  X,
  Eye,
  EyeOff,
  RotateCcw,
  Sliders,
  Sparkles,
  Move,
  Maximize2,
  Layers,
  FlipHorizontal,
  Check,
  Copy,
  Box,
  Image as ImageIcon,
  Columns,
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

type ViewportSyncMode = '3d_only' | '2d_only' | 'split_2d_3d';

const CAMERA_ANGLES_LIST: { id: Character2DAngle; label: string; deg: number; isTop?: boolean }[] = [
  { id: 'front', label: '0° Thẳng', deg: 0, isTop: false },
  { id: 'three_quarter_left', label: '45° 3/4 Trái', deg: 45, isTop: false },
  { id: 'profile_left', label: '👂 90° Tai Trái', deg: 90, isTop: false },
  { id: 'back_three_quarter_left', label: '135° Sau Trái', deg: 135, isTop: false },
  { id: 'back', label: '180° Sau Lưng', deg: 180, isTop: false },
  { id: 'profile_right', label: '👂 270° Tai Phải', deg: 270, isTop: false },
  { id: 'top_down', label: '👑 Đỉnh Đầu 0°', deg: 0, isTop: true },
  { id: 'top_down_profile_left', label: '👑 Đỉnh 90° Tai', deg: 90, isTop: true },
];

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
  const [notification, setNotification] = useState<string | null>(null);

  // Embedded Three.js WebGL Viewport in Modal
  const modalThreeContainerRef = useRef<HTMLDivElement>(null);
  const modalThreeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);

  // Embedded 2D Canvas in Modal
  const modal2DCanvasRef = useRef<HTMLCanvasElement>(null);

  // Initialize Three.js Engine inside Modal
  useEffect(() => {
    if (!isOpen || !modalThreeContainerRef.current) return;

    if (!modalThreeEngineRef.current) {
      const engine = new ThreeMultiAngleBillboardEngine(modalThreeContainerRef.current);
      engine.setBackgroundMode('checkerboard');
      modalThreeEngineRef.current = engine;
    }

    modalThreeEngineRef.current.setAssembly(assemblyDraft);

    const angleInfo = CAMERA_ANGLES_LIST.find((a) => a.id === selectedAngle);
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

  // Update Three.js when angle or assemblyDraft changes
  useEffect(() => {
    if (modalThreeEngineRef.current) {
      modalThreeEngineRef.current.setAssembly(assemblyDraft);
      const angleInfo = CAMERA_ANGLES_LIST.find((a) => a.id === selectedAngle);
      if (angleInfo) {
        modalThreeEngineRef.current.jumpToAngle(angleInfo.deg, angleInfo.isTop);
      }
    }
  }, [selectedAngle, assemblyDraft]);

  // Draw 2D texture in Canvas
  useEffect(() => {
    const canvas = modal2DCanvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const part = assemblyDraft.parts[selectedSlot];
    const textureUrl = part?.angles?.[selectedAngle] || part?.path;

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Checkerboard background
    const size = 12;
    for (let x = 0; x < canvas.width; x += size) {
      for (let y = 0; y < canvas.height; y += size) {
        ctx.fillStyle = ((x / size + y / size) % 2 === 0) ? '#0b0f19' : '#162032';
        ctx.fillRect(x, y, size, size);
      }
    }

    if (textureUrl) {
      const img = new Image();
      img.src = textureUrl;
      img.onload = () => {
        const override = part?.angle_overrides?.[selectedAngle] || {};
        const isVis = override.visible !== false;
        if (!isVis) {
          ctx.fillStyle = 'rgba(239, 68, 68, 0.2)';
          ctx.fillRect(0, 0, canvas.width, canvas.height);
          ctx.fillStyle = '#f87171';
          ctx.font = 'bold 12px sans-serif';
          ctx.textAlign = 'center';
          ctx.fillText('Đang ẨN trên góc này', canvas.width / 2, canvas.height / 2);
          return;
        }

        const offX = override.offset?.[0] ?? part?.offset?.[0] ?? 0;
        const offY = override.offset?.[1] ?? part?.offset?.[1] ?? 0;
        const scX = override.scale?.[0] ?? part?.scale?.[0] ?? 1.0;
        const scY = override.scale?.[1] ?? part?.scale?.[1] ?? 1.0;
        const rot = (override.rotation ?? part?.rotation ?? 0) * (Math.PI / 180);
        const flipX = override.flipX ?? part?.flipX ?? false;

        ctx.save();
        ctx.translate(canvas.width / 2 + offX * 0.5, canvas.height / 2 + offY * 0.5);
        ctx.rotate(rot);
        ctx.scale(scX * (flipX ? -1 : 1), scY);

        const drawW = 120;
        const drawH = 120;
        ctx.drawImage(img, -drawW / 2, -drawH / 2, drawW, drawH);

        // Bounding box & Center anchor crosshair
        ctx.strokeStyle = 'rgba(56, 189, 248, 0.6)';
        ctx.lineWidth = 1;
        ctx.strokeRect(-drawW / 2, -drawH / 2, drawW, drawH);
        ctx.beginPath();
        ctx.arc(0, 0, 3, 0, Math.PI * 2);
        ctx.fillStyle = '#38bdf8';
        ctx.fill();

        ctx.restore();
      };
    }
  }, [selectedSlot, selectedAngle, assemblyDraft, viewportMode]);

  if (!isOpen) return null;

  const currentPart = assemblyDraft.parts[selectedSlot];
  const override: PartAngleOverride = currentPart?.angle_overrides?.[selectedAngle] || {};

  // Resolved values
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

      onApplyAssembly(next);
      return next;
    });
  };

  const handleResetCurrentAngle = () => {
    setAssemblyDraft((prev) => {
      const next: Character2DAssembly = JSON.parse(JSON.stringify(prev));
      const part = next.parts[selectedSlot];
      if (part?.angle_overrides?.[selectedAngle]) {
        delete part.angle_overrides[selectedAngle];
      }
      onApplyAssembly(next);
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

      CAMERA_ANGLES_LIST.forEach((ang) => {
        part.angle_overrides![ang.id] = { ...currentOv };
      });

      onApplyAssembly(next);
      return next;
    });
    showToast('Đã đồng bộ thông số sang tất cả các góc');
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
        background: 'rgba(0, 0, 0, 0.85)',
        backdropFilter: 'blur(8px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1100,
        padding: 16,
      }}
    >
      <div
        style={{
          background: '#0a0f1d',
          border: '1px solid rgba(56, 189, 248, 0.35)',
          borderRadius: 12,
          width: 1040,
          maxHeight: '94vh',
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 20px 60px rgba(0,0,0,0.9), 0 0 30px rgba(56, 189, 248, 0.15)',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: '12px 18px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'linear-gradient(90deg, rgba(14, 165, 233, 0.12) 0%, transparent 100%)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Sliders size={18} color="#38bdf8" />
            <div>
              <div style={{ fontSize: 13.5, fontWeight: 700, color: '#f8fafc' }}>
                🎛️ TINH CHỈNH & CÂN ĐỐI TÓC THEO TỪNG GÓC CAMERA (ĐỒNG BỘ 2D + 3D)
              </div>
              <div style={{ fontSize: 10.5, color: '#94a3b8' }}>
                Cân chỉnh lệch tóc, thu nhỏ/phóng to tỉ lệ và ẩn tóc thừa với màn hình xem trước 2D & 3D đồng bộ trực tiếp
              </div>
            </div>
          </div>

          <button
            onClick={onClose}
            style={{
              background: 'rgba(255, 255, 255, 0.06)',
              border: 'none',
              borderRadius: 6,
              color: '#94a3b8',
              padding: 6,
              cursor: 'pointer',
            }}
          >
            <X size={16} />
          </button>
        </div>

        {/* 1. Camera Angle Selection Bar */}
        <div
          style={{
            padding: '8px 18px',
            background: 'rgba(15, 23, 42, 0.7)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            overflowX: 'auto',
          }}
        >
          <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', whiteSpace: 'nowrap', marginRight: 4 }}>
            🎥 Góc Đang Chỉnh:
          </span>
          {CAMERA_ANGLES_LIST.map((ang) => {
            const isSelected = selectedAngle === ang.id;
            return (
              <button
                key={ang.id}
                onClick={() => {
                  setSelectedAngle(ang.id);
                  if (onJumpToAngle) {
                    onJumpToAngle(ang.deg, ang.isTop);
                  }
                }}
                style={{
                  padding: '4px 9px',
                  fontSize: 10.5,
                  fontWeight: 600,
                  borderRadius: 6,
                  border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                  background: isSelected ? 'rgba(56, 189, 248, 0.22)' : 'rgba(255,255,255,0.03)',
                  color: isSelected ? '#38bdf8' : '#cbd5e1',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap',
                  boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.3)' : 'none',
                }}
              >
                {ang.label}
              </button>
            );
          })}
        </div>

        {/* 2. Main 3-Column Studio Grid: Left Slots (190px), Center Visual Viewport (410px), Right Transform Controls (380px) */}
        <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '190px 430px 1fr', overflow: 'hidden', minHeight: 460 }}>
          {/* Left: Layer Selector */}
          <div
            style={{
              padding: 10,
              background: 'rgba(11, 19, 41, 0.6)',
              borderRight: '1px solid rgba(255, 255, 255, 0.08)',
              display: 'flex',
              flexDirection: 'column',
              gap: 5,
              overflowY: 'auto',
            }}
          >
            <div style={{ fontSize: 10.5, fontWeight: 700, color: '#94a3b8', marginBottom: 2 }}>
              CHỌN LỚP LINH KIỆN:
            </div>

            {(['toc_truoc', 'toc_sau', 'dau', 'khuon_mat', 'mat', 'mui', 'mieng', 'trang_phuc', 'vu_khi'] as Character2DPartType[]).map((slot) => {
              const p = assemblyDraft.parts[slot];
              const isSelected = selectedSlot === slot;
              const hasOverride = Boolean(p?.angle_overrides?.[selectedAngle]);
              const isHiddenOnAngle = p?.angle_overrides?.[selectedAngle]?.visible === false;

              return (
                <button
                  key={slot}
                  onClick={() => setSelectedSlot(slot)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '7px 8px',
                    borderRadius: 6,
                    border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                    background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.02)',
                    color: isSelected ? '#ffffff' : '#94a3b8',
                    cursor: 'pointer',
                    textAlign: 'left',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <span style={{ fontSize: 12 }}>
                      {slot === 'toc_truoc' ? '💇' : slot === 'toc_sau' ? '🌊' : slot === 'dau' ? '👑' : slot === 'khuon_mat' ? '✨' : '📦'}
                    </span>
                    <span style={{ fontSize: 10.5, fontWeight: isSelected ? 700 : 500 }}>
                      {PART_HIERARCHY_CONFIG[slot]?.label || slot}
                    </span>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
                    {isHiddenOnAngle && <span style={{ fontSize: 8.5, color: '#f87171', fontWeight: 700 }}>[Ẩn]</span>}
                    {hasOverride && !isHiddenOnAngle && <span style={{ fontSize: 8.5, color: '#4ade80', fontWeight: 700 }}>[Chỉnh]</span>}
                  </div>
                </button>
              );
            })}
          </div>

          {/* Center: Live 2D & 3D Viewport Synchronizer */}
          <div
            style={{
              padding: 12,
              background: 'rgba(5, 9, 20, 0.9)',
              borderRight: '1px solid rgba(255, 255, 255, 0.08)',
              display: 'flex',
              flexDirection: 'column',
              gap: 8,
              overflow: 'hidden',
            }}
          >
            {/* Viewport Mode Switcher */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                <Box size={13} /> XEM TRƯỚC ĐỒNG BỘ 2D/3D
              </div>

              <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.5)', padding: 2, borderRadius: 5 }}>
                <button
                  onClick={() => setViewportMode('split_2d_3d')}
                  style={{
                    padding: '3px 7px',
                    fontSize: 9.5,
                    fontWeight: 600,
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
                  <Columns size={10} /> Song Song
                </button>
                <button
                  onClick={() => setViewportMode('3d_only')}
                  style={{
                    padding: '3px 7px',
                    fontSize: 9.5,
                    fontWeight: 600,
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
                  <Box size={10} /> 3D View
                </button>
                <button
                  onClick={() => setViewportMode('2d_only')}
                  style={{
                    padding: '3px 7px',
                    fontSize: 9.5,
                    fontWeight: 600,
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
                  <ImageIcon size={10} /> 2D Layer
                </button>
              </div>
            </div>

            {/* Viewport Render Area */}
            <div style={{ flex: 1, display: 'flex', gap: 6, minHeight: 0, overflow: 'hidden' }}>
              {/* 2D Canvas View */}
              {(viewportMode === '2d_only' || viewportMode === 'split_2d_3d') && (
                <div
                  style={{
                    flex: 1,
                    display: 'flex',
                    flexDirection: 'column',
                    background: '#040711',
                    borderRadius: 8,
                    overflow: 'hidden',
                    border: '1px solid rgba(56, 189, 248, 0.25)',
                    position: 'relative',
                  }}
                >
                  <div style={{ position: 'absolute', top: 6, left: 8, fontSize: 9.5, fontWeight: 700, color: '#38bdf8', background: 'rgba(0,0,0,0.6)', padding: '2px 6px', borderRadius: 4, zIndex: 10 }}>
                    🖼️ Lớp 2D: {PART_HIERARCHY_CONFIG[selectedSlot]?.label}
                  </div>
                  <canvas
                    ref={modal2DCanvasRef}
                    width={240}
                    height={320}
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
                    borderRadius: 8,
                    overflow: 'hidden',
                    border: '1px solid rgba(56, 189, 248, 0.35)',
                    position: 'relative',
                  }}
                >
                  <div style={{ position: 'absolute', top: 6, left: 8, fontSize: 9.5, fontWeight: 700, color: '#4ade80', background: 'rgba(0,0,0,0.6)', padding: '2px 6px', borderRadius: 4, zIndex: 10 }}>
                    🌟 Nhân Vật 3D ({CAMERA_ANGLES_LIST.find((a) => a.id === selectedAngle)?.label})
                  </div>
                  <div
                    ref={modalThreeContainerRef}
                    style={{ width: '100%', height: '100%' }}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Right: Fine-Tuning Controllers */}
          <div style={{ padding: 14, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12 }}>
            {/* Active Part Header & Visibility */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                background: 'rgba(0,0,0,0.3)',
                padding: '8px 12px',
                borderRadius: 8,
                border: '1px solid rgba(255,255,255,0.06)',
              }}
            >
              <div>
                <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8' }}>
                  {PART_HIERARCHY_CONFIG[selectedSlot]?.label || selectedSlot}
                </span>
                <span style={{ fontSize: 10, color: '#94a3b8', marginLeft: 6 }}>
                  • {CAMERA_ANGLES_LIST.find((a) => a.id === selectedAngle)?.label}
                </span>
              </div>

              {/* Visibility Toggle Button */}
              <button
                onClick={() => updateOverride({ visible: !isVisible })}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  padding: '4px 8px',
                  fontSize: 10.5,
                  fontWeight: 600,
                  borderRadius: 5,
                  border: isVisible ? '1px solid rgba(34, 197, 94, 0.4)' : '1px solid rgba(239, 68, 68, 0.4)',
                  background: isVisible ? 'rgba(34, 197, 94, 0.15)' : 'rgba(239, 68, 68, 0.15)',
                  color: isVisible ? '#4ade80' : '#f87171',
                  cursor: 'pointer',
                }}
              >
                {isVisible ? <Eye size={12} /> : <EyeOff size={12} />}
                {isVisible ? 'Đang Hiển Thị' : 'Đang ẨN'}
              </button>
            </div>

            {/* Position Offsets (X & Y) */}
            <div style={{ background: 'rgba(15, 23, 42, 0.5)', padding: 10, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 8 }}>
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
                  <button onClick={() => updateOverride({ offset: [currentOffsetX - 5, currentOffsetY] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>-5</button>
                  <button onClick={() => updateOverride({ offset: [0, currentOffsetY] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
                  <button onClick={() => updateOverride({ offset: [currentOffsetX + 5, currentOffsetY] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>+5</button>
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
                  <button onClick={() => updateOverride({ offset: [currentOffsetX, currentOffsetY - 5] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>-5</button>
                  <button onClick={() => updateOverride({ offset: [currentOffsetX, 0] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
                  <button onClick={() => updateOverride({ offset: [currentOffsetX, currentOffsetY + 5] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>+5</button>
                </div>
              </div>
            </div>

            {/* Scale & Proportion Control */}
            <div style={{ background: 'rgba(15, 23, 42, 0.5)', padding: 10, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 8 }}>
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
                  <button onClick={() => updateOverride({ scale: [currentScaleX * 0.9, currentScaleY * 0.9] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>-10%</button>
                  <button onClick={() => updateOverride({ scale: [1.0, 1.0] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>100%</button>
                  <button onClick={() => updateOverride({ scale: [currentScaleX * 1.1, currentScaleY * 1.1] })} style={{ padding: '2px 5px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>+10%</button>
                </div>
              </div>
            </div>

            {/* Rotation & Flip */}
            <div style={{ background: 'rgba(15, 23, 42, 0.5)', padding: 10, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
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
                    padding: '5px',
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 4,
                    border: currentFlipX ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: currentFlipX ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.04)',
                    color: currentFlipX ? '#38bdf8' : '#cbd5e1',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                  }}
                >
                  <FlipHorizontal size={12} />
                  {currentFlipX ? 'Đã Lật Ngang' : 'Mặc Định'}
                </button>
              </div>
            </div>

            {/* Quick Action Tools */}
            <div style={{ display: 'flex', gap: 6, marginTop: 2 }}>
              <button
                onClick={handleResetCurrentAngle}
                style={{
                  flex: 1,
                  padding: '6px 8px',
                  fontSize: 10,
                  fontWeight: 600,
                  borderRadius: 5,
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
                <RotateCcw size={11} /> Đặt Lại Góc Này
              </button>

              <button
                onClick={handleCopyToAllAngles}
                style={{
                  flex: 1,
                  padding: '6px 8px',
                  fontSize: 10,
                  fontWeight: 600,
                  borderRadius: 5,
                  background: 'rgba(56, 189, 248, 0.1)',
                  color: '#38bdf8',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 4,
                }}
              >
                <Copy size={11} /> Đồng Bộ Tất Cả Góc
              </button>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '10px 18px',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(15, 23, 42, 0.8)',
          }}
        >
          <div style={{ fontSize: 10.5, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 6 }}>
            {notification ? (
              <span>✓ {notification}</span>
            ) : (
              <span style={{ color: '#94a3b8' }}>Mọi thay đổi vị trí, kích thước và ẩn/hiện được cập nhật đồng bộ tức thì vào không gian 3D.</span>
            )}
          </div>

          <button
            onClick={() => {
              onApplyAssembly(assemblyDraft);
              onClose();
            }}
            style={{
              padding: '7px 18px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #2563eb)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              boxShadow: '0 2px 10px rgba(2, 132, 199, 0.4)',
            }}
          >
            <Check size={13} /> Hoàn Tất & Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
