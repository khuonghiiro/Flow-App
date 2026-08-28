import React, { useState } from 'react';
import { Upload, Sparkles, RefreshCw, User, Check, Layers, Image as ImageIcon, Crown, Compass } from 'lucide-react';
import {
  Actor2DProfile,
  STANDARD_8_ANGLES,
  TOP_DOWN_ANGLES,
  StandardHorizontalAngle,
} from '../../../../types/studio2d_director';

interface ActorAngleSlotManagerProps {
  actors: Actor2DProfile[];
  selectedActorId: string;
  onSelectActor: (actorId: string) => void;
  onUpdateActor: (updated: Actor2DProfile) => void;
}

export const ActorAngleSlotManager: React.FC<ActorAngleSlotManagerProps> = ({
  actors,
  selectedActorId,
  onSelectActor,
  onUpdateActor,
}) => {
  const [angleCategory, setAngleCategory] = useState<'horizontal' | 'top_down'>('horizontal');
  const actor = actors.find((a) => a.id === selectedActorId) || actors[0];

  if (!actor) return null;

  const handleUploadImageForAngle = (angle: StandardHorizontalAngle, file: File) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const dataUrl = e.target?.result as string;
      if (dataUrl) {
        onUpdateActor({
          ...actor,
          sprites: {
            ...actor.sprites,
            [angle]: dataUrl,
          },
        });
      }
    };
    reader.readAsDataURL(file);
  };

  const currentAngleList = angleCategory === 'horizontal' ? STANDARD_8_ANGLES : TOP_DOWN_ANGLES;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        background: 'rgba(15, 23, 42, 0.85)',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: 8,
        padding: 12,
      }}
    >
      {/* Header & Category Switcher */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Layers size={13} /> QUẢN LÝ GÓC ẢNH SPRITE
        </div>

        {/* Horizontal vs Top-Down Tabs */}
        <div style={{ display: 'flex', gap: 2, background: 'rgba(0,0,0,0.4)', padding: 2, borderRadius: 6 }}>
          <button
            onClick={() => setAngleCategory('horizontal')}
            style={{
              padding: '2px 6px',
              fontSize: 9,
              fontWeight: angleCategory === 'horizontal' ? 700 : 500,
              background: angleCategory === 'horizontal' ? '#0284c7' : 'transparent',
              color: '#fff',
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
          >
            <Compass size={10} /> 360° Ngang
          </button>
          <button
            onClick={() => setAngleCategory('top_down')}
            style={{
              padding: '2px 6px',
              fontSize: 9,
              fontWeight: angleCategory === 'top_down' ? 700 : 500,
              background: angleCategory === 'top_down' ? '#ca8a04' : 'transparent',
              color: '#fff',
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
          >
            <Crown size={10} /> 👑 Đỉnh Đầu
          </button>
        </div>
      </div>

      {/* Actor Switcher Pill Buttons */}
      <div style={{ display: 'flex', gap: 6, overflowX: 'auto', paddingBottom: 4 }}>
        {actors.map((act) => {
          const isSelected = act.id === selectedActorId;
          return (
            <button
              key={act.id}
              onClick={() => onSelectActor(act.id)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '4px 10px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: isSelected ? 700 : 500,
                background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.04)',
                border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                color: isSelected ? '#38bdf8' : '#cbd5e1',
                cursor: 'pointer',
                whiteSpace: 'nowrap',
              }}
            >
              <span>{act.avatarIcon || '👤'}</span>
              <span>{act.name}</span>
            </button>
          );
        })}
      </div>

      {/* Auto-Mirror Symmetry Toggle */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(2, 6, 23, 0.6)',
          padding: '6px 10px',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.05)',
        }}
      >
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          <span style={{ fontSize: 10.5, fontWeight: 600, color: '#e2e8f0' }}>Tự động lật đối xứng (Auto-Mirror)</span>
          <span style={{ fontSize: 9, color: '#94a3b8' }}>Tự đảo 225°/270°/315° từ ảnh 135°/90°/45°</span>
        </div>
        <input
          type="checkbox"
          checked={actor.autoMirrorSymmetry}
          onChange={(e) =>
            onUpdateActor({
              ...actor,
              autoMirrorSymmetry: e.target.checked,
            })
          }
          style={{ cursor: 'pointer', accentColor: '#38bdf8' }}
        />
      </div>

      {/* Angle Slots Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8 }}>
        {currentAngleList.map((ang) => {
          const directUrl = actor.sprites[ang.id];
          const isMirrored = !directUrl && actor.autoMirrorSymmetry && !!ang.mirroredFrom && !!actor.sprites[ang.mirroredFrom];
          const effectiveUrl = directUrl || (isMirrored && ang.mirroredFrom ? actor.sprites[ang.mirroredFrom] : '');

          return (
            <div
              key={ang.id}
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 4,
                padding: 6,
                background: 'rgba(2, 6, 23, 0.7)',
                borderRadius: 6,
                border: directUrl
                  ? '1px solid rgba(56, 189, 248, 0.4)'
                  : isMirrored
                  ? '1px dashed rgba(168, 85, 247, 0.4)'
                  : '1px solid rgba(255,255,255,0.06)',
                position: 'relative',
              }}
            >
              {/* Badge */}
              <div style={{ display: 'flex', justifyContent: 'space-between', width: '100%', alignItems: 'center' }}>
                <span style={{ fontSize: 9, fontWeight: 700, color: ang.isTopDown ? '#facc15' : '#38bdf8' }}>{ang.deg}°</span>
                <span style={{ fontSize: 8, color: '#94a3b8' }}>{ang.compass}</span>
              </div>

              {/* Image Thumbnail Preview */}
              <div
                style={{
                  width: '100%',
                  height: 64,
                  borderRadius: 4,
                  background: 'rgba(15, 23, 42, 0.9)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  overflow: 'hidden',
                  position: 'relative',
                }}
              >
                {effectiveUrl ? (
                  <img
                    src={effectiveUrl}
                    alt={ang.labelVi}
                    style={{
                      maxHeight: '100%',
                      maxWidth: '100%',
                      objectFit: 'contain',
                      transform: isMirrored ? 'scaleX(-1)' : 'none',
                    }}
                  />
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2, color: '#475569' }}>
                    <ImageIcon size={16} />
                    <span style={{ fontSize: 8 }}>Chưa có</span>
                  </div>
                )}

                {isMirrored && (
                  <div
                    style={{
                      position: 'absolute',
                      bottom: 2,
                      right: 2,
                      background: 'rgba(168, 85, 247, 0.85)',
                      color: '#fff',
                      fontSize: 7,
                      padding: '1px 3px',
                      borderRadius: 2,
                      fontWeight: 700,
                    }}
                  >
                    Lật {ang.mirroredFrom?.replace(/_/g, ' ')}
                  </div>
                )}
              </div>

              {/* Upload input button */}
              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 3,
                  width: '100%',
                  padding: '3px 0',
                  borderRadius: 4,
                  background: 'rgba(255,255,255,0.05)',
                  border: '1px solid rgba(255,255,255,0.1)',
                  color: '#e2e8f0',
                  fontSize: 9,
                  cursor: 'pointer',
                  textAlign: 'center',
                }}
              >
                <Upload size={10} /> Đổi ảnh
                <input
                  type="file"
                  accept="image/*"
                  style={{ display: 'none' }}
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) handleUploadImageForAngle(ang.id, f);
                    e.target.value = '';
                  }}
                />
              </label>
            </div>
          );
        })}
      </div>
    </div>
  );
};
