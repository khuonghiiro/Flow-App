import React from 'react';
import {
  Move,
  Maximize2,
  Minimize2,
  Zap,
  Activity,
  User,
  Sparkles,
  Sliders,
  Crown,
  Compass,
} from 'lucide-react';
import {
  MultiAngleDirectorShot,
  ActionPoseType,
  Actor2DProfile,
} from '../../../../types/studio2d_director';

interface MotionTrajectoryEditorProps {
  activeShot: MultiAngleDirectorShot;
  actors: Actor2DProfile[];
  selectedActorId: string | null;
  onUpdateShot: (updated: Partial<MultiAngleDirectorShot>) => void;
}

const ACTION_POSES: { id: ActionPoseType; label: string; icon: string }[] = [
  { id: 'idle_breathe', label: 'Đứng thở (Idle)', icon: '🧘' },
  { id: 'talk_dialogue', label: 'Nói thoại (Talk)', icon: '🗣️' },
  { id: 'combat_slash', label: 'Chém kiếm (Slash)', icon: '⚔️' },
  { id: 'combat_cast', label: 'Vận công (Cast)', icon: '✨' },
  { id: 'shocked_back', label: 'Giật mình (Shock)', icon: '😱' },
  { id: 'fly_dash', label: 'Lướt bay (Dash)', icon: '💨' },
];

export const MotionTrajectoryEditor: React.FC<MotionTrajectoryEditorProps> = ({
  activeShot,
  actors,
  selectedActorId,
  onUpdateShot,
}) => {
  const cam = activeShot.camera;
  const targetActorId = selectedActorId || Object.keys(activeShot.actors)[0];
  const actorState = targetActorId ? activeShot.actors[targetActorId] : null;

  const updateCamera = (updates: Partial<typeof cam>) => {
    onUpdateShot({
      camera: { ...cam, ...updates },
    });
  };

  const updateActorState = (updates: Partial<typeof actorState>) => {
    if (!targetActorId || !actorState) return;
    onUpdateShot({
      actors: {
        ...activeShot.actors,
        [targetActorId]: {
          ...actorState,
          ...updates,
        },
      },
    });
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
        background: 'rgba(15, 23, 42, 0.85)',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: 8,
        padding: 12,
        overflowY: 'auto',
      }}
    >
      <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
        <Sliders size={13} /> QUỸ ĐẠO CHUYỂN ĐỘNG & ZOOM
      </div>

      {/* Camera Pan & Zoom Interpolation Settings */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          background: 'rgba(2, 6, 23, 0.6)',
          padding: 10,
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.05)',
        }}
      >
        <span style={{ fontSize: 10, fontWeight: 700, color: '#eab308' }}>🎥 ZOOM & GÓC CAMERA</span>

        {/* Zoom Start and End */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <div>
            <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
              Zoom Đầu ({cam.zoomStart.toFixed(2)}x):
            </label>
            <input
              type="range"
              min="0.7"
              max="2.5"
              step="0.05"
              value={cam.zoomStart}
              onChange={(e) => updateCamera({ zoomStart: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#38bdf8' }}
            />
          </div>
          <div>
            <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
              Zoom Đích ({cam.zoomEnd.toFixed(2)}x):
            </label>
            <input
              type="range"
              min="0.7"
              max="2.5"
              step="0.05"
              value={cam.zoomEnd}
              onChange={(e) => updateCamera({ zoomEnd: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#ec4899' }}
            />
          </div>
        </div>

        {/* Camera Elevation Pitch (Top-Down) */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
            <span style={{ fontSize: 9.5, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Crown size={11} color="#facc15" /> Góc từ trên đầu xuống (Pitch):
            </span>
            <span style={{ fontSize: 9.5, color: (cam.pitchStart ?? 0) >= 45 ? '#facc15' : '#38bdf8', fontWeight: 700 }}>
              {(cam.pitchStart ?? 0) >= 45 ? '👑 Đỉnh Đầu 60°' : 'Ngang Tầm Mắt 0°'}
            </span>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <button
              onClick={() => updateCamera({ pitchStart: 0, pitchEnd: 0 })}
              style={{
                flex: 1,
                padding: '4px',
                fontSize: 9.5,
                fontWeight: (cam.pitchStart ?? 0) < 45 ? 700 : 500,
                background: (cam.pitchStart ?? 0) < 45 ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.03)',
                border: (cam.pitchStart ?? 0) < 45 ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                color: (cam.pitchStart ?? 0) < 45 ? '#38bdf8' : '#cbd5e1',
                borderRadius: 4,
                cursor: 'pointer',
              }}
            >
              Ngang Tầm Mắt (0°)
            </button>
            <button
              onClick={() => updateCamera({ pitchStart: 60, pitchEnd: 60 })}
              style={{
                flex: 1,
                padding: '4px',
                fontSize: 9.5,
                fontWeight: (cam.pitchStart ?? 0) >= 45 ? 700 : 500,
                background: (cam.pitchStart ?? 0) >= 45 ? 'rgba(234, 179, 8, 0.25)' : 'rgba(255,255,255,0.03)',
                border: (cam.pitchStart ?? 0) >= 45 ? '1px solid #facc15' : '1px solid rgba(255,255,255,0.06)',
                color: (cam.pitchStart ?? 0) >= 45 ? '#facc15' : '#cbd5e1',
                borderRadius: 4,
                cursor: 'pointer',
              }}
            >
              👑 Đỉnh Đầu (60°)
            </button>
          </div>
        </div>

        {/* Shake & Transitions */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <div>
            <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
              Rung máy (Shake): {(cam.shakeIntensity || 0).toFixed(1)}
            </label>
            <input
              type="range"
              min="0"
              max="1"
              step="0.1"
              value={cam.shakeIntensity || 0}
              onChange={(e) => updateCamera({ shakeIntensity: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#ef4444' }}
            />
          </div>
          <div>
            <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
              Chuyển cảnh (Transition):
            </label>
            <select
              value={activeShot.transitionIn || 'none'}
              onChange={(e) => onUpdateShot({ transitionIn: e.target.value as any })}
              style={{
                width: '100%',
                padding: '4px',
                background: 'rgba(15, 23, 42, 0.9)',
                border: '1px solid rgba(255,255,255,0.1)',
                borderRadius: 4,
                color: '#fff',
                fontSize: 10,
              }}
            >
              <option value="none">Mặc định (None)</option>
              <option value="jump_cut">Jump Cut (Cắt gấp)</option>
              <option value="flash_white">Flash White (Chớp trắng)</option>
              <option value="fade_black">Fade Black (Mờ đen)</option>
              <option value="whip_pan">Whip Pan (Lướt nhanh)</option>
            </select>
          </div>
        </div>
      </div>

      {/* Selected Actor Action Pose & Motion */}
      {actorState && (
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            background: 'rgba(2, 6, 23, 0.6)',
            padding: 10,
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.05)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 10, fontWeight: 700, color: '#4ade80' }}>
              👤 ĐỘNG TÁC NHÂN VẬT: {actors.find((a) => a.id === targetActorId)?.name}
            </span>
          </div>

          {/* Action Pose Selector Grid */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 4 }}>
            {ACTION_POSES.map((pose) => {
              const isSelected = actorState.actionPose === pose.id;
              return (
                <button
                  key={pose.id}
                  onClick={() => updateActorState({ actionPose: pose.id })}
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: 2,
                    padding: '5px 2px',
                    borderRadius: 4,
                    background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.03)',
                    border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.06)',
                    color: isSelected ? '#38bdf8' : '#cbd5e1',
                    fontSize: 9.5,
                    cursor: 'pointer',
                  }}
                >
                  <span style={{ fontSize: 13 }}>{pose.icon}</span>
                  <span>{pose.label.split(' ')[0]}</span>
                </button>
              );
            })}
          </div>

          {/* Actor Facing Angle in World */}
          <div style={{ marginTop: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
              <span style={{ fontSize: 9.5, color: '#94a3b8' }}>Hướng quay của nhân vật trong cảnh:</span>
              <span style={{ fontSize: 9.5, color: '#38bdf8', fontWeight: 700 }}>{actorState.worldFacingAngle}°</span>
            </div>
            <input
              type="range"
              min="0"
              max="360"
              step="45"
              value={actorState.worldFacingAngle}
              onChange={(e) => updateActorState({ worldFacingAngle: parseInt(e.target.value) })}
              style={{ width: '100%', accentColor: '#38bdf8' }}
            />
          </div>

          {/* Scaling and Z-Index Controls */}
          <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 0.8fr', gap: 6, marginTop: 4 }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                <span style={{ fontSize: 9.5, color: '#94a3b8' }}>Tỉ lệ (Scale):</span>
                <span style={{ fontSize: 9.5, color: '#38bdf8', fontWeight: 700 }}>
                  {(actorState.scale || 1.6).toFixed(2)}x
                </span>
              </div>
              <input
                type="range"
                min="0.2"
                max="5.0"
                step="0.05"
                value={actorState.scale || 1.6}
                onChange={(e) => updateActorState({ scale: parseFloat(e.target.value) || 1.0 })}
                style={{ width: '100%', accentColor: '#38bdf8' }}
              />
            </div>
            <div>
              <span style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
                Lớp (Z-Index):
              </span>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <button
                  onClick={() => updateActorState({ zIndex: Math.max(1, (actorState.zIndex || 10) - 1) })}
                  style={{
                    width: 22,
                    height: 22,
                    borderRadius: 3,
                    background: 'rgba(255,255,255,0.06)',
                    border: '1px solid rgba(255,255,255,0.1)',
                    color: '#fff',
                    fontSize: 11,
                    fontWeight: 700,
                    cursor: 'pointer',
                  }}
                >
                  -
                </button>
                <input
                  type="number"
                  min={1}
                  max={100}
                  value={actorState.zIndex || 10}
                  onChange={(e) => updateActorState({ zIndex: parseInt(e.target.value) || 10 })}
                  style={{
                    width: '100%',
                    padding: '2px 4px',
                    fontSize: 10,
                    fontWeight: 700,
                    textAlign: 'center',
                    background: '#090d16',
                    border: '1px solid #334155',
                    color: '#38bdf8',
                    borderRadius: 3,
                  }}
                />
                <button
                  onClick={() => updateActorState({ zIndex: (actorState.zIndex || 10) + 1 })}
                  style={{
                    width: 22,
                    height: 22,
                    borderRadius: 3,
                    background: 'rgba(255,255,255,0.06)',
                    border: '1px solid rgba(255,255,255,0.1)',
                    color: '#fff',
                    fontSize: 11,
                    fontWeight: 700,
                    cursor: 'pointer',
                  }}
                >
                  +
                </button>
              </div>
            </div>
          </div>

          {/* Position Coordinates */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6, marginTop: 4 }}>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Vị trí Bắt đầu (X, Y):</span>
              <div style={{ display: 'flex', gap: 4 }}>
                <input
                  type="number"
                  value={actorState.positionStart[0]}
                  onChange={(e) =>
                    updateActorState({
                      positionStart: [parseInt(e.target.value) || 0, actorState.positionStart[1]],
                    })
                  }
                  style={{
                    width: '100%',
                    padding: '2px 4px',
                    fontSize: 10,
                    background: '#090d16',
                    border: '1px solid #334155',
                    color: '#fff',
                    borderRadius: 3,
                  }}
                />
                <input
                  type="number"
                  value={actorState.positionStart[1]}
                  onChange={(e) =>
                    updateActorState({
                      positionStart: [actorState.positionStart[0], parseInt(e.target.value) || 0],
                    })
                  }
                  style={{
                    width: '100%',
                    padding: '2px 4px',
                    fontSize: 10,
                    background: '#090d16',
                    border: '1px solid #334155',
                    color: '#fff',
                    borderRadius: 3,
                  }}
                />
              </div>
            </div>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Vị trí Kết thúc (X, Y):</span>
              <div style={{ display: 'flex', gap: 4 }}>
                <input
                  type="number"
                  value={actorState.positionEnd[0]}
                  onChange={(e) =>
                    updateActorState({
                      positionEnd: [parseInt(e.target.value) || 0, actorState.positionEnd[1]],
                    })
                  }
                  style={{
                    width: '100%',
                    padding: '2px 4px',
                    fontSize: 10,
                    background: '#090d16',
                    border: '1px solid #334155',
                    color: '#fff',
                    borderRadius: 3,
                  }}
                />
                <input
                  type="number"
                  value={actorState.positionEnd[1]}
                  onChange={(e) =>
                    updateActorState({
                      positionEnd: [actorState.positionEnd[0], parseInt(e.target.value) || 0],
                    })
                  }
                  style={{
                    width: '100%',
                    padding: '2px 4px',
                    fontSize: 10,
                    background: '#090d16',
                    border: '1px solid #334155',
                    color: '#fff',
                    borderRadius: 3,
                  }}
                />
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
