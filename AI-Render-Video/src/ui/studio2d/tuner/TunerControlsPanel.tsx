import React from 'react';
import {
  Eye,
  EyeOff,
  RotateCcw,
  Move,
  Maximize2,
  FlipHorizontal,
  Copy,
  Layers,
} from 'lucide-react';
import { Character2DAngle, Character2DAssembly, Character2DPartType, PartAngleOverride } from '../../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../../../core/assets/Asset2DRegistry';
import { AngleMenuItem } from './TunerAngleSidebar';

interface TunerControlsPanelProps {
  selectedSlot: Character2DPartType;
  selectedAngle: Character2DAngle;
  currentAngleInfo?: AngleMenuItem;
  assemblyDraft: Character2DAssembly;
  updateOverride: (patch: Partial<PartAngleOverride>) => void;
  onResetCurrentAngle: () => void;
  onCopyToAllAngles: () => void;
}

export const TunerControlsPanel: React.FC<TunerControlsPanelProps> = ({
  selectedSlot,
  selectedAngle,
  currentAngleInfo,
  assemblyDraft,
  updateOverride,
  onResetCurrentAngle,
  onCopyToAllAngles,
}) => {
  const currentPart = assemblyDraft.parts[selectedSlot];
  const override: PartAngleOverride = currentPart?.angle_overrides?.[selectedAngle] || {};

  const hierarchy = PART_HIERARCHY_CONFIG[selectedSlot];
  const defaultOff = hierarchy?.defaultOffset ?? [0, 0];

  const isVisible = override.visible !== false;
  const currentOffsetX = override.offset?.[0] ?? (currentPart?.offset && (currentPart.offset[0] !== 0 || currentPart.offset[1] !== 0) ? currentPart.offset[0] : defaultOff[0]);
  const currentOffsetY = override.offset?.[1] ?? (currentPart?.offset && (currentPart.offset[0] !== 0 || currentPart.offset[1] !== 0) ? currentPart.offset[1] : defaultOff[1]);
  const currentScaleX = override.scale?.[0] ?? currentPart?.scale?.[0] ?? 1.0;
  const currentScaleY = override.scale?.[1] ?? currentPart?.scale?.[1] ?? 1.0;
  const currentRotation = override.rotation ?? currentPart?.rotation ?? 0;
  const currentFlipX = override.flipX ?? currentPart?.flipX ?? false;
  const currentZDepth = override.z_depth_3d ?? currentPart?.z_depth_3d ?? hierarchy?.defaultZDepth3D ?? 0;

  return (
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
            {hierarchy?.label || selectedSlot}
          </span>
          <div style={{ fontSize: 10, color: '#94a3b8', marginTop: 2 }}>
            Góc: {currentAngleInfo?.label}
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
              max="2.5"
              step="0.01"
              value={currentScaleX}
              onChange={(e) => {
                const val = parseFloat(e.target.value);
                updateOverride({ scale: [val, val] });
              }}
              style={{ flex: 1 }}
            />
            <button onClick={() => updateOverride({ scale: [Math.max(0.1, +(currentScaleX - 0.05).toFixed(2)), Math.max(0.1, +(currentScaleY - 0.05).toFixed(2))] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>-5%</button>
            <button onClick={() => updateOverride({ scale: [1.0, 1.0] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 4, cursor: 'pointer' }}>100%</button>
            <button onClick={() => updateOverride({ scale: [Math.min(3.0, +(currentScaleX + 0.05).toFixed(2)), Math.min(3.0, +(currentScaleY + 0.05).toFixed(2))] })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>+5%</button>
          </div>
        </div>
      </div>

      {/* Rotation & Flip */}
      <div style={{ background: 'rgba(15, 23, 42, 0.6)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <FlipHorizontal size={13} /> XOAY GÓC & LẬT ĐỐI XỨNG:
        </div>

        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span>🔄 Góc Nghiêng (Rotation):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{Math.round(currentRotation)}°</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <input
              type="range"
              min="-180"
              max="180"
              step="1"
              value={currentRotation}
              onChange={(e) => updateOverride({ rotation: parseInt(e.target.value) })}
              style={{ flex: 1 }}
            />
            <button onClick={() => updateOverride({ rotation: currentRotation - 5 })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>-5°</button>
            <button onClick={() => updateOverride({ rotation: 0 })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 4, cursor: 'pointer' }}>0°</button>
            <button onClick={() => updateOverride({ rotation: currentRotation + 5 })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>+5°</button>
          </div>
        </div>

        {/* Flip X Button */}
        <button
          onClick={() => updateOverride({ flipX: !currentFlipX })}
          style={{
            marginTop: 4,
            padding: '7px 12px',
            fontSize: 11,
            fontWeight: 700,
            borderRadius: 6,
            border: currentFlipX ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.12)',
            background: currentFlipX ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.04)',
            color: currentFlipX ? '#38bdf8' : '#cbd5e1',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
          }}
        >
          <FlipHorizontal size={13} /> {currentFlipX ? '✓ Đang Lật Ngang (Flipped X)' : 'Lật Ngang Đối Xứng (Flip X)'}
        </button>
      </div>

      {/* 3D Depth Layering (Z-Index / Z-Depth) */}
      <div style={{ background: 'rgba(15, 23, 42, 0.6)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Layers size={13} /> THỨ TỰ LỚP (Z-INDEX):
        </div>

        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span>📦 Chỉ Số Z-Index:</span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{Math.round(currentZDepth)}</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <input
              type="range"
              min="-50"
              max="50"
              step="1"
              value={currentZDepth}
              onChange={(e) => updateOverride({ z_depth_3d: parseFloat(e.target.value) })}
              style={{ flex: 1 }}
            />
            <button onClick={() => updateOverride({ z_depth_3d: currentZDepth - 1 })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>-1</button>
            <button onClick={() => updateOverride({ z_depth_3d: hierarchy?.defaultZDepth3D ?? 0 })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 4, cursor: 'pointer' }}>Mặc định</button>
            <button onClick={() => updateOverride({ z_depth_3d: currentZDepth + 1 })} style={{ padding: '3px 6px', fontSize: 9.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>+1</button>
          </div>
        </div>
      </div>

      {/* Sync & Reset Action Buttons */}
      <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
        <button
          onClick={onCopyToAllAngles}
          style={{
            flex: 1,
            padding: '8px 10px',
            fontSize: 10.5,
            fontWeight: 700,
            borderRadius: 6,
            border: '1px solid rgba(56, 189, 248, 0.4)',
            background: 'rgba(56, 189, 248, 0.15)',
            color: '#38bdf8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
          }}
          title="Sao chép toàn bộ vị trí & scale của tầng tóc này sang 8 góc còn lại"
        >
          <Copy size={12} /> Áp Dụng 360°
        </button>

        <button
          onClick={onResetCurrentAngle}
          style={{
            padding: '8px 12px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 6,
            border: '1px solid rgba(255,255,255,0.1)',
            background: 'rgba(255,255,255,0.05)',
            color: '#94a3b8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
          }}
          title="Đặt lại thông số của góc này về mặc định"
        >
          <RotateCcw size={12} /> Reset
        </button>
      </div>
    </div>
  );
};
