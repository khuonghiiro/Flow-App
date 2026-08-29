// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useEffect } from 'react';
import {
  Save,
  Plus,
  X,
  Check,
  FolderPlus,
  Compass,
  FileCode,
  Sparkles,
  Download,
} from 'lucide-react';
import {
  CustomPoseDefinition,
  AnimationSliceFrame,
  AnimationPoseSavePayload,
} from '../../../../types/animation_slicer';
import { StandardHorizontalAngle, STANDARD_8_ANGLES } from '../../../../types/studio2d_director';
import {
  loadAllActionPoses,
  registerNewCustomPose,
  downloadBatchScript,
} from '../utils/animationPoseRegistry';
import { slugifyVietnamese, getActionFolderPath } from '../utils/slugifyHelper';

interface AnimationPoseSaveModalProps {
  isOpen: boolean;
  onClose: () => void;
  frames: AnimationSliceFrame[];
  frameOrder: number[];
  fps: number;
  loopMode: 'loop' | 'ping_pong' | 'once';
  onSaveSuccess: (payload: AnimationPoseSavePayload) => void;
}

export const AnimationPoseSaveModal: React.FC<AnimationPoseSaveModalProps> = ({
  isOpen,
  onClose,
  frames,
  frameOrder,
  fps,
  loopMode,
  onSaveSuccess,
}) => {
  const [poses, setPoses] = useState<CustomPoseDefinition[]>([]);
  const [selectedPoseId, setSelectedPoseId] = useState<string>('chem-kiem-loi-dien');
  const [selectedAngleDeg, setSelectedAngleDeg] = useState<number>(0);
  const [isCreatingNew, setIsCreatingNew] = useState<boolean>(false);
  const [newPoseName, setNewPoseName] = useState<string>('');
  const [newPoseCategory, setNewPoseCategory] = useState<CustomPoseDefinition['category']>('combat');
  const [saveToast, setSaveToast] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      const list = loadAllActionPoses();
      setPoses(list);
      if (list.length > 0 && !selectedPoseId) {
        setSelectedPoseId(list[0].id);
      }
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const currentPose = poses.find((p) => p.id === selectedPoseId) || poses[0];
  const newSlugPreview = slugifyVietnamese(newPoseName);
  const activeFolderPath = isCreatingNew
    ? getActionFolderPath(newPoseName || 'Dong Tac Moi', selectedAngleDeg)
    : getActionFolderPath(currentPose?.name || 'chem-kiem', selectedAngleDeg);

  const angleInfo = STANDARD_8_ANGLES.find((a) => a.deg === selectedAngleDeg) || STANDARD_8_ANGLES[0];

  const handleSave = () => {
    let finalPoseId = selectedPoseId;
    let finalPoseName = currentPose?.name || 'Chém Kiếm';

    if (isCreatingNew) {
      if (!newPoseName.trim()) {
        alert('Vui lòng nhập tên động tác mới!');
        return;
      }
      const registered = registerNewCustomPose(newPoseName, newPoseCategory);
      finalPoseId = registered.id;
      finalPoseName = registered.name;
    }

    const payload: AnimationPoseSavePayload = {
      poseId: finalPoseId,
      poseName: finalPoseName,
      folderSlug: activeFolderPath.poseSlug,
      angleDeg: selectedAngleDeg,
      angleId: angleInfo.id as StandardHorizontalAngle,
      fps,
      loopMode,
      frames,
      frameOrder,
    };

    onSaveSuccess(payload);
    setSaveToast('Đã lưu cấu hình hoạt ảnh thành công!');
    setTimeout(() => {
      setSaveToast(null);
      onClose();
    }, 1200);
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(2, 6, 23, 0.88)',
        backdropFilter: 'blur(16px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
      }}
    >
      <div
        style={{
          width: '90vw',
          maxWidth: '640px',
          background: '#090d16',
          border: '1px solid rgba(56, 189, 248, 0.35)',
          borderRadius: 12,
          boxShadow: '0 24px 60px rgba(0, 0, 0, 0.9), 0 0 30px rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            height: 50,
            background: 'rgba(15, 23, 42, 0.95)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0 16px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #a855f7)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
              }}
            >
              <Save size={15} />
            </div>
            <div style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
              Lưu Chuỗi Khung Hình Hoạt Ảnh (Animation Sequence)
            </div>
          </div>

          <button
            onClick={onClose}
            style={{
              width: 28,
              height: 28,
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#94a3b8',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
            }}
          >
            <X size={15} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12, overflowY: 'auto' }}>
          {/* Section 1: Choose or Create Action Pose */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
                1. Chọn Động Tác (Action Pose):
              </label>
              <button
                onClick={() => setIsCreatingNew(!isCreatingNew)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  fontSize: 10,
                  color: isCreatingNew ? '#f87171' : '#c084fc',
                  background: 'none',
                  border: 'none',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                {isCreatingNew ? '← Chọn từ danh sách có sẵn' : '+ Tạo tên động tác mới'}
              </button>
            </div>

            {!isCreatingNew ? (
              <select
                value={selectedPoseId}
                onChange={(e) => setSelectedPoseId(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 10px',
                  borderRadius: 6,
                  background: 'rgba(2, 6, 23, 0.85)',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                  color: '#fff',
                  fontSize: 12,
                  outline: 'none',
                }}
              >
                {poses.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.icon} {p.name} ({p.folderPath})
                  </option>
                ))}
              </select>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, background: 'rgba(2, 6, 23, 0.6)', padding: 10, borderRadius: 6, border: '1px solid rgba(168, 85, 247, 0.3)' }}>
                <div>
                  <span style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
                    Tên động tác (hỗ trợ Tiếng Việt có dấu):
                  </span>
                  <input
                    type="text"
                    placeholder="VD: Chém Kiếm Lôi Điện, Phi Thân Vạn Kiếm..."
                    value={newPoseName}
                    onChange={(e) => setNewPoseName(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '6px 8px',
                      borderRadius: 4,
                      background: '#020617',
                      border: '1px solid #7c3aed',
                      color: '#fff',
                      fontSize: 11,
                    }}
                  />
                </div>

                <div style={{ fontSize: 10, color: '#94a3b8' }}>
                  Slug thư mục tự động: <code style={{ color: '#4ade80', background: 'rgba(0,0,0,0.5)', padding: '2px 6px', borderRadius: 4 }}>{newSlugPreview || 'chua-nhap-ten'}</code>
                </div>
              </div>
            )}
          </div>

          {/* Section 2: Facing Angle Selection */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
              2. Chọn Góc Quay Cơ Thể (Facing Angle):
            </label>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 6 }}>
              {STANDARD_8_ANGLES.map((ang) => {
                const isActive = selectedAngleDeg === ang.deg;
                return (
                  <button
                    key={ang.deg}
                    onClick={() => setSelectedAngleDeg(ang.deg)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                      padding: '6px 8px',
                      borderRadius: 6,
                      fontSize: 10,
                      fontWeight: isActive ? 700 : 500,
                      background: isActive ? 'linear-gradient(135deg, #0284c7, #38bdf8)' : 'rgba(255,255,255,0.03)',
                      border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                      color: isActive ? '#ffffff' : '#cbd5e1',
                      cursor: 'pointer',
                    }}
                  >
                    <span>🧭</span>
                    <span>{ang.deg}° ({ang.compass})</span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Section 3: Summary & Sanitized Path Info */}
          <div style={{ background: 'rgba(2, 6, 23, 0.8)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ fontSize: 10, color: '#94a3b8' }}>
              📁 Đường dẫn lưu trữ thư mục chuẩn hệ thống:
            </div>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#facc15', fontFamily: 'monospace' }}>
              {activeFolderPath.fullPath}
            </div>
            <div style={{ fontSize: 9.5, color: '#64748b' }}>
              Đã bao gồm {frames.length} frame(s) • Thứ tự chuỗi: [{frameOrder.map((i) => i + 1).join(', ')}] • Tốc độ: {fps} FPS
            </div>
          </div>
        </div>

        {/* Footer */}
        <div
          style={{
            height: 52,
            background: 'rgba(15, 23, 42, 0.95)',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0 16px',
          }}
        >
          <button
            onClick={() => downloadBatchScript(poses)}
            title="Tải file script .bat để tự động tạo toàn bộ thư mục động tác trên Windows"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '5px 10px',
              borderRadius: 5,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#38bdf8',
              fontSize: 10,
              cursor: 'pointer',
            }}
          >
            <Download size={12} /> Tải Script Tạo Folder (.bat)
          </button>

          <div style={{ display: 'flex', gap: 8 }}>
            <button
              onClick={onClose}
              style={{
                padding: '6px 12px',
                borderRadius: 6,
                background: 'rgba(255, 255, 255, 0.05)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#cbd5e1',
                fontSize: 11,
                cursor: 'pointer',
              }}
            >
              Hủy
            </button>

            <button
              onClick={handleSave}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 16px',
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #a855f7)',
                border: 'none',
                color: '#ffffff',
                fontSize: 11,
                fontWeight: 700,
                cursor: 'pointer',
                boxShadow: '0 2px 10px rgba(56, 189, 248, 0.4)',
              }}
            >
              <Check size={13} /> {saveToast || 'Xác Nhận & Lưu Động Tác'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
