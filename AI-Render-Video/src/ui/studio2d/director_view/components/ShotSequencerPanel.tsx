import React from 'react';
import {
  Clapperboard,
  Plus,
  Trash2,
  Copy,
  RotateCw,
  MessageSquare,
  Volume2,
  Clock,
  Sparkles,
} from 'lucide-react';
import {
  MultiAngleDirectorShot,
  Director2DProject,
  Actor2DProfile,
} from '../../../../types/studio2d_director';

interface ShotSequencerPanelProps {
  project: Director2DProject;
  activeShotId: string;
  onSelectShot: (shotId: string) => void;
  onUpdateProject: (updated: Director2DProject) => void;
}

export const ShotSequencerPanel: React.FC<ShotSequencerPanelProps> = ({
  project,
  activeShotId,
  onSelectShot,
  onUpdateProject,
}) => {
  const activeShot = project.shots.find((s) => s.id === activeShotId) || project.shots[0];

  const handleAddShot = () => {
    const newId = `shot_${Date.now().toString().slice(-4)}`;
    const newShot: MultiAngleDirectorShot = {
      id: newId,
      title: `Shot #${project.shots.length + 1}: Cảnh Quay Mới`,
      durationSeconds: 3.0,
      camera: {
        angleStart: 0,
        angleEnd: 0,
        zoomStart: 1.0,
        zoomEnd: 1.2,
        panStart: [0, 0],
        panEnd: [0, 0],
      },
      actors: { ...activeShot.actors },
      dialogueText: '',
      speakerActorId: project.actors[0]?.id,
    };

    onUpdateProject({
      ...project,
      shots: [...project.shots, newShot],
    });
    onSelectShot(newId);
  };

  /**
   * 1-Click Reverse Shot Creator:
   * Creates an over-the-shoulder reverse dialogue counter-argument shot
   */
  const handleCreateReverseShot = () => {
    if (!activeShot) return;
    const newId = `shot_${Date.now().toString().slice(-4)}`;
    const newCamAngle = (activeShot.camera.angleStart + 180) % 360;

    // Swap actors in dialogue
    const actorIds = Object.keys(activeShot.actors);
    const newActors = { ...activeShot.actors };

    // Swap speaker
    const currentSpeaker = activeShot.speakerActorId;
    const nextSpeaker = project.actors.find((a) => a.id !== currentSpeaker)?.id || currentSpeaker;

    const reverseShot: MultiAngleDirectorShot = {
      id: newId,
      title: `Shot #${project.shots.length + 1}: Đối Thoại Phản Bác (Đảo góc 180°)`,
      durationSeconds: 3.5,
      camera: {
        angleStart: newCamAngle,
        angleEnd: newCamAngle,
        zoomStart: 1.1,
        zoomEnd: 1.35, // Dramatic Zoom
        panStart: [-activeShot.camera.panStart[0], activeShot.camera.panStart[1]],
        panEnd: [-activeShot.camera.panEnd[0], activeShot.camera.panEnd[1]],
        shakeIntensity: 0.1,
      },
      actors: newActors,
      speakerActorId: nextSpeaker,
      dialogueText: 'Không thể nào... Lẽ nào ngươi đã đạt đến cảnh giới đó?!',
      transitionIn: 'jump_cut',
    };

    onUpdateProject({
      ...project,
      shots: [...project.shots, reverseShot],
    });
    onSelectShot(newId);
  };

  const handleDeleteShot = (shotId: string) => {
    if (project.shots.length <= 1) {
      alert('Dự án phải có ít nhất 1 cảnh quay!');
      return;
    }
    const filtered = project.shots.filter((s) => s.id !== shotId);
    onUpdateProject({ ...project, shots: filtered });
    if (activeShotId === shotId) {
      onSelectShot(filtered[0].id);
    }
  };

  const handleUpdateActiveShot = (updates: Partial<MultiAngleDirectorShot>) => {
    const updated = project.shots.map((s) => (s.id === activeShotId ? { ...s, ...updates } : s));
    onUpdateProject({ ...project, shots: updated });
  };

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
        height: '100%',
        overflowY: 'auto',
      }}
    >
      {/* Header & Quick Action Buttons */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Clapperboard size={13} /> DANH SÁCH CẢNH QUAY (SHOTS)
        </div>
        <span style={{ fontSize: 10, color: '#94a3b8' }}>{project.shots.length} shots</span>
      </div>

      {/* 1-Click Reverse Dialogue Creator Button */}
      <button
        onClick={handleCreateReverseShot}
        title="Tự động đảo camera 180° và hoán đổi vị trí/góc nhìn để nhân vật đối diện phản bác"
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          padding: '7px 10px',
          borderRadius: 6,
          background: 'linear-gradient(135deg, rgba(168, 85, 247, 0.25), rgba(56, 189, 248, 0.2))',
          border: '1px solid rgba(168, 85, 247, 0.4)',
          color: '#c084fc',
          fontSize: 11,
          fontWeight: 700,
          cursor: 'pointer',
          boxShadow: '0 2px 8px rgba(168, 85, 247, 0.2)',
        }}
      >
        <RotateCw size={12} /> 🔄 Tạo Shot Đối Thoại Phản Bác (180°)
      </button>

      {/* Shots List */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, maxHeight: 220, overflowY: 'auto' }}>
        {project.shots.map((shot, idx) => {
          const isSelected = shot.id === activeShotId;
          const speaker = project.actors.find((a) => a.id === shot.speakerActorId);

          return (
            <div
              key={shot.id}
              onClick={() => onSelectShot(shot.id)}
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: 4,
                padding: '8px 10px',
                borderRadius: 6,
                background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255, 255, 255, 0.02)',
                border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.05)',
                cursor: 'pointer',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ fontSize: 11, fontWeight: 700, color: isSelected ? '#38bdf8' : '#f1f5f9' }}>
                  #{idx + 1}. {shot.title}
                </span>
                <span style={{ fontSize: 9.5, color: '#94a3b8' }}>{shot.durationSeconds}s | 🎥 {shot.camera.angleStart}°</span>
              </div>

              {shot.dialogueText && (
                <div style={{ fontSize: 10, color: '#94a3b8', fontStyle: 'italic', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {speaker ? `[${speaker.name}]: ` : ''}{shot.dialogueText}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <button
        onClick={handleAddShot}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          padding: '6px',
          borderRadius: 6,
          background: 'rgba(255,255,255,0.04)',
          border: '1px dashed rgba(255,255,255,0.15)',
          color: '#e2e8f0',
          fontSize: 10.5,
          cursor: 'pointer',
        }}
      >
        <Plus size={12} /> Thêm Cảnh Quay Mới
      </button>

      {/* Active Shot Property Form */}
      {activeShot && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 6, borderTop: '1px solid rgba(255,255,255,0.08)', paddingTop: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 10.5, fontWeight: 700, color: '#4ade80' }}>CHI TIẾT PHÂN CẢNH</span>
            <button
              onClick={() => handleDeleteShot(activeShot.id)}
              title="Xóa shot này"
              style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', padding: 2 }}
            >
              <Trash2 size={13} />
            </button>
          </div>

          <div>
            <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Tên cảnh quay:</label>
            <input
              type="text"
              value={activeShot.title}
              onChange={(e) => handleUpdateActiveShot({ title: e.target.value })}
              style={{
                width: '100%',
                padding: '4px 8px',
                background: 'rgba(2, 6, 23, 0.7)',
                border: '1px solid rgba(255,255,255,0.1)',
                borderRadius: 4,
                color: '#fff',
                fontSize: 11,
              }}
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Thời lượng (giây):</label>
              <input
                type="number"
                step="0.5"
                min="0.5"
                value={activeShot.durationSeconds}
                onChange={(e) => handleUpdateActiveShot({ durationSeconds: parseFloat(e.target.value) || 1.0 })}
                style={{
                  width: '100%',
                  padding: '4px 8px',
                  background: 'rgba(2, 6, 23, 0.7)',
                  border: '1px solid rgba(255,255,255,0.1)',
                  borderRadius: 4,
                  color: '#fff',
                  fontSize: 11,
                }}
              />
            </div>
            <div>
              <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Người phát ngôn:</label>
              <select
                value={activeShot.speakerActorId || ''}
                onChange={(e) => handleUpdateActiveShot({ speakerActorId: e.target.value })}
                style={{
                  width: '100%',
                  padding: '4px 8px',
                  background: 'rgba(2, 6, 23, 0.7)',
                  border: '1px solid rgba(255,255,255,0.1)',
                  borderRadius: 4,
                  color: '#38bdf8',
                  fontSize: 11,
                }}
              >
                {project.actors.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.avatarIcon} {a.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Lời thoại / Phụ đề:</label>
            <textarea
              rows={2}
              value={activeShot.dialogueText || ''}
              onChange={(e) => handleUpdateActiveShot({ dialogueText: e.target.value })}
              placeholder="Nhập câu thoại của nhân vật..."
              style={{
                width: '100%',
                padding: '4px 8px',
                background: 'rgba(2, 6, 23, 0.7)',
                border: '1px solid rgba(255,255,255,0.1)',
                borderRadius: 4,
                color: '#fff',
                fontSize: 11,
                resize: 'none',
              }}
            />
          </div>
        </div>
      )}
    </div>
  );
};
