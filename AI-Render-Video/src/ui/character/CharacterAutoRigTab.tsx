import React from 'react';
import {
  Wrench,
  RotateCcw,
  Sparkles,
  CheckCircle,
  Eye,
  EyeOff,
  Play,
  Pause,
  Layers,
  Activity,
} from 'lucide-react';
import { CharacterCategory } from '../CharacterAssetRegistry';

export interface CharacterAutoRigTabProps {
  availableCategories: CharacterCategory[];
  modelToRig: string;
  onSelectModelToRig: (path: string) => void;
  isRigged: boolean;
  isRiggingLoading: boolean;
  onRunAutoRig: () => void;
  showJoints: boolean;
  onToggleJoints: () => void;
  activePose: string;
  onSelectPose: (pose: string) => void;
  isPosePlaying: boolean;
  onTogglePosePlay: () => void;
}

export const CharacterAutoRigTab: React.FC<CharacterAutoRigTabProps> = ({
  availableCategories,
  modelToRig,
  onSelectModelToRig,
  isRigged,
  isRiggingLoading,
  onRunAutoRig,
  showJoints,
  onToggleJoints,
  activePose,
  onSelectPose,
  isPosePlaying,
  onTogglePosePlay,
}) => {
  const bodies = availableCategories.find((c) => c.id === 'than_co_ban')?.items || [];

  const POSES = [
    { id: 't_pose', label: '🤸 T-Pose (Chuẩn)', desc: 'Tư thế đứng chữ T chuẩn để gắn xương' },
    { id: 'walk', label: '🚶 Dáng Đi Tự Nhiên', desc: 'Chu kỳ bước đi Humanoid chuẩn nhịp tay' },
    { id: 'slash', label: '⚔️ Xuất Chiêu Kiếm Pháp', desc: 'Hoạt cảnh vung kiếm chém ngang uy lực' },
    { id: 'defend', label: '🛡️ Thế Thủ Võ Thuật', desc: 'Hạ trọng tâm phòng ngự' },
    { id: 'wave', label: '👋 Vẫy Tay Chào', desc: 'Giơ tay cao vẫy chào thân thiện' },
    { id: 'sit', label: '🪑 Tư Thế Ngồi Ghế', desc: 'Gập gối 90° ngồi thư giãn' },
  ];

  return (
    <div style={{ flex: 1, padding: 18, overflowY: 'auto' }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14, maxWidth: 720 }}>
        {/* Header Title */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <span style={{ fontSize: 15, fontWeight: 800, color: '#c084fc' }}>
              Auto-Rigging Studio (Gắn Xương Chuẩn Giải Phẫu 3D)
            </span>
            <p style={{ margin: '4px 0 0 0', fontSize: 11, color: '#94a3b8' }}>
              Tự động phân tích hình học mô hình tĩnh và tạo khung xương 17 khớp chuẩn Three.js.
            </p>
          </div>
          <span style={{ fontSize: 10, background: 'rgba(168, 85, 247, 0.15)', color: '#c084fc', padding: '2px 8px', borderRadius: 12, fontWeight: 700, border: '1px solid rgba(168, 85, 247, 0.3)' }}>
            Capsule Skinning
          </span>
        </div>

        {/* Model Selection Box */}
        <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
          <label style={{ display: 'block', fontWeight: 700, marginBottom: 8, color: '#e2e8f0', fontSize: 12 }}>
            Chọn Mô Hình 3D Cần Gắn Xương (.glb)
          </label>
          <select
            value={modelToRig}
            onChange={(e) => onSelectModelToRig(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 12px',
              background: '#0f172a',
              color: '#f8fafc',
              border: '1px solid rgba(255,255,255,0.15)',
              borderRadius: 6,
              fontSize: 12,
              outline: 'none',
              cursor: 'pointer',
            }}
          >
            {bodies.map((item) => (
              <option key={item.path} value={item.path}>
                {item.name} ({item.path})
              </option>
            ))}
          </select>
        </div>

        {/* Auto-Rig Action Button */}
        <div style={{ display: 'flex', gap: 10 }}>
          <button
            onClick={onRunAutoRig}
            disabled={isRiggingLoading}
            style={{
              flex: 1,
              padding: '10px 16px',
              background: 'linear-gradient(135deg, #a855f7, #6366f1)',
              color: '#ffffff',
              border: 'none',
              borderRadius: 8,
              fontWeight: 800,
              fontSize: 12,
              cursor: isRiggingLoading ? 'not-allowed' : 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              boxShadow: '0 4px 14px rgba(168, 85, 247, 0.4)',
              opacity: isRiggingLoading ? 0.7 : 1,
              transition: 'all 0.15s ease',
            }}
          >
            {isRiggingLoading ? (
              <>
                <RotateCcw size={16} className="animate-spin" /> Đang tính toán ma trận xương & trọng số da...
              </>
            ) : (
              <>
                <Sparkles size={16} /> THỰC HIỆN AUTO-RIG (GẮN XƯƠNG TỰ ĐỘNG)
              </>
            )}
          </button>
        </div>

        {/* Rigged Status & Joint Inspector */}
        {isRigged && (
          <div style={{ background: 'rgba(34, 197, 94, 0.08)', padding: 14, borderRadius: 10, border: '1px solid rgba(34, 197, 94, 0.25)', display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: '#4ade80', fontWeight: 800, fontSize: 13 }}>
                <CheckCircle size={16} />
                <span>Gắn xương thành công (17 Khớp Xương Humanoid)</span>
              </div>

              <button
                onClick={onToggleJoints}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '4px 10px',
                  fontSize: 11,
                  fontWeight: 700,
                  borderRadius: 6,
                  background: showJoints ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.06)',
                  color: showJoints ? '#38bdf8' : '#94a3b8',
                  border: showJoints ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                  cursor: 'pointer',
                }}
              >
                {showJoints ? <Eye size={13} /> : <EyeOff size={13} />}
                <span>{showJoints ? 'Ẩn Khớp Xương' : 'Hiện Khớp Xương'}</span>
              </button>
            </div>

            {/* Pose Tester Grid */}
            <div style={{ marginTop: 4 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#e2e8f0', display: 'block', marginBottom: 8 }}>
                Kiểm Thử Cử Động Khớp Xương (Pose Test):
              </span>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: 8 }}>
                {POSES.map((pose) => {
                  const isSelected = activePose === pose.id;
                  return (
                    <button
                      key={pose.id}
                      onClick={() => onSelectPose(pose.id)}
                      style={{
                        padding: '8px 12px',
                        borderRadius: 6,
                        background: isSelected ? 'rgba(168, 85, 247, 0.3)' : 'rgba(255,255,255,0.04)',
                        border: isSelected ? '1px solid #a855f7' : '1px solid rgba(255,255,255,0.08)',
                        color: isSelected ? '#ffffff' : '#cbd5e1',
                        fontSize: 11,
                        fontWeight: isSelected ? 700 : 500,
                        cursor: 'pointer',
                        textAlign: 'left',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 2,
                        transition: 'all 0.15s ease',
                      }}
                    >
                      <span style={{ fontWeight: 700 }}>{pose.label}</span>
                      <span style={{ fontSize: 9, color: '#94a3b8' }}>{pose.desc}</span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Play/Pause Pose Button */}
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 4 }}>
              <button
                onClick={onTogglePosePlay}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '6px 14px',
                  borderRadius: 6,
                  background: isPosePlaying ? 'rgba(239, 68, 68, 0.25)' : 'rgba(34, 197, 94, 0.25)',
                  border: isPosePlaying ? '1px solid #ef4444' : '1px solid #22c55e',
                  color: isPosePlaying ? '#f87171' : '#4ade80',
                  fontWeight: 700,
                  fontSize: 11,
                  cursor: 'pointer',
                }}
              >
                {isPosePlaying ? <Pause size={13} /> : <Play size={13} />}
                <span>{isPosePlaying ? 'Tạm Dừng Cử Động' : 'Chạy Thử Cử Động'}</span>
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
