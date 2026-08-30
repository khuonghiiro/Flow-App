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
  User,
  Activity,
  Folder,
} from 'lucide-react';
import {
  AnimationSliceFrame,
  AnimationPoseSavePayload,
} from '../../../../types/animation_slicer';
import { StandardHorizontalAngle, STANDARD_8_ANGLES } from '../../../../types/studio2d_director';
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

const DEFAULT_ANGLES = [
  { id: 'chinh_dien', deg: 0, label: 'Chính Diện (0°)' },
  { id: 'cheo_truoc_trai', deg: 45, label: 'Chéo Trước Trái (45°)' },
  { id: 'ngang_trai', deg: 90, label: 'Ngang Trái (90°)' },
  { id: 'cheo_sau_trai', deg: 135, label: 'Chéo Sau Trái (135°)' },
  { id: 'sau_lung', deg: 180, label: 'Sau Lưng (180°)' },
  { id: 'cheo_sau_phai', deg: 225, label: 'Chéo Sau Phải (225°)' },
  { id: 'ngang_phai', deg: 270, label: 'Ngang Phải (270°)' },
  { id: 'cheo_truoc_phai', deg: 315, label: 'Chéo Trước Phải (315°)' },
];

export const VIETNAMESE_NAME_MAP: Record<string, string> = {
  // Characters
  nhan_vat_chinh: 'Nhân Vật Chính',
  ton_ngo_khong: 'Tôn Ngộ Không',
  tieu_viem: 'Tiêu Viêm',
  nu_hiep: 'Nữ Hiệp',
  phap_su: 'Pháp Sư',
  kiem_khach: 'Kiếm Khách',
  quai_vat: 'Quái Vật',
  boss: 'Thủ Lĩnh (Boss)',

  // Actions
  chem_kiem: 'Chém Kiếm',
  di_bo: 'Đi Bộ',
  chay: 'Chạy Bộ',
  tung_chuong: 'Tung Chưởng',
  dung_yen: 'Đứng Yên (Idle)',
  phong_thu: 'Phòng Thủ',
  nhay: 'Nhảy Lên',
  nga: 'Ngã / Gục',
  bi_danh: 'Bị Trúng Đòn',
  nem_phi_tieu: 'Ném Phi Tiêu',
  van_cong: 'Vận Công',
  ban_cung: 'Bắn Cung',
  niem_chu: 'Niệm Chú Phép',
  luot_gio: 'Lướt Gió',
  gong_nang_luong: 'Gồng Năng Lượng',
};

export const formatVietnameseDisplayName = (slug: string): string => {
  if (!slug) return '';
  if (VIETNAMESE_NAME_MAP[slug]) return VIETNAMESE_NAME_MAP[slug];
  return slug
    .split('_')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
};

export const AnimationPoseSaveModal: React.FC<AnimationPoseSaveModalProps> = ({
  isOpen,
  onClose,
  frames,
  frameOrder,
  fps,
  loopMode,
  onSaveSuccess,
}) => {
  // Characters
  const [characterList, setCharacterList] = useState<string[]>(['nhan_vat_chinh', 'ton_ngo_khong', 'tieu_viem']);
  const [selectedCharacter, setSelectedCharacter] = useState<string>('nhan_vat_chinh');
  const [isCreatingNewChar, setIsCreatingNewChar] = useState<boolean>(false);
  const [newCharName, setNewCharName] = useState<string>('');

  // Actions
  const [actionList, setActionList] = useState<string[]>(['chem_kiem', 'di_bo', 'tung_chuong', 'dung_yen']);
  const [selectedAction, setSelectedAction] = useState<string>('chem_kiem');
  const [isCreatingNewAction, setIsCreatingNewAction] = useState<boolean>(false);
  const [newActionName, setNewActionName] = useState<string>('');

  // Angle
  const [selectedAngleDeg, setSelectedAngleDeg] = useState<number>(0);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveToast, setSaveToast] = useState<string | null>(null);

  // Fetch available characters from API
  useEffect(() => {
    if (!isOpen) return;
    fetch('/api/list-2d-characters')
      .then((res) => res.json())
      .then((data) => {
        if (Array.isArray(data) && data.length > 0) {
          setCharacterList(data);
          if (!data.includes(selectedCharacter)) {
            setSelectedCharacter(data[0]);
          }
        }
      })
      .catch(() => {});
  }, [isOpen]);

  // Fetch available actions for selected character
  useEffect(() => {
    if (!isOpen) return;
    const charSlug = isCreatingNewChar ? slugifyVietnamese(newCharName) : selectedCharacter;
    if (!charSlug) return;

    fetch(`/api/list-2d-actions?character=${encodeURIComponent(charSlug)}`)
      .then((res) => res.json())
      .then((data) => {
        if (Array.isArray(data) && data.length > 0) {
          setActionList(data);
          if (!data.includes(selectedAction)) {
            setSelectedAction(data[0]);
          }
        }
      })
      .catch(() => {});
  }, [isOpen, selectedCharacter, isCreatingNewChar, newCharName]);

  if (!isOpen) return null;

  const finalCharSlug = isCreatingNewChar
    ? slugifyVietnamese(newCharName || 'nhan_vat_moi')
    : selectedCharacter || 'nhan_vat_chinh';

  const finalActionSlug = isCreatingNewAction
    ? slugifyVietnamese(newActionName || 'dong_tac_moi')
    : selectedAction || 'chem_kiem';

  const activeAngle = DEFAULT_ANGLES.find((a) => a.deg === selectedAngleDeg) || DEFAULT_ANGLES[0];
  const targetFolderPath = getActionFolderPath(finalCharSlug, finalActionSlug, selectedAngleDeg, activeAngle.id);

  // Save to Disk via Backend Vite API
  const handleSave = async () => {
    if (isCreatingNewChar && !newCharName.trim()) {
      alert('Vui lòng nhập tên nhân vật mới!');
      return;
    }
    if (isCreatingNewAction && !newActionName.trim()) {
      alert('Vui lòng nhập tên động tác mới!');
      return;
    }

    setIsSaving(true);
    setSaveToast('Đang lưu chuỗi frame vào thư mục asset_2ds/nhan_vat/...');

    try {
      const response = await fetch('/api/save-2d-animation-motion', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          character: finalCharSlug,
          actionName: finalActionSlug,
          actionDisplayName: isCreatingNewAction ? newActionName : selectedAction,
          angleSlug: activeAngle.id,
          angleDeg: selectedAngleDeg,
          fps,
          loopMode,
          frames,
          frameOrder,
        }),
      });

      const resData = await response.json();
      if (!resData.success) {
        throw new Error(resData.error || 'Lưu thất bại');
      }

      setSaveToast(`✓ Đã lưu thành công ${frames.length} frame vào ${resData.targetDir}!`);

      const payload: AnimationPoseSavePayload = {
        poseId: `${finalCharSlug}_${finalActionSlug}`,
        poseName: isCreatingNewAction ? newActionName : selectedAction,
        folderSlug: finalActionSlug,
        angleDeg: selectedAngleDeg,
        angleId: activeAngle.id as StandardHorizontalAngle,
        fps,
        loopMode,
        frames,
        frameOrder,
      };

      onSaveSuccess(payload);
      setTimeout(() => {
        setIsSaving(false);
        setSaveToast(null);
        onClose();
      }, 1500);
    } catch (err: any) {
      setIsSaving(false);
      setSaveToast(`❌ Lỗi: ${err?.message || 'Không thể lưu file'}`);
      setTimeout(() => setSaveToast(null), 3000);
    }
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
          maxWidth: '660px',
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
              Lưu Hoạt Ảnh Vào asset_2ds/nhan_vat
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
        <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12, overflowY: 'auto', maxHeight: '80vh' }}>
          {/* 1. Character Selection & Creation */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                <User size={13} /> 1. Nhân Vật (Character):
              </label>
              <button
                onClick={() => setIsCreatingNewChar(!isCreatingNewChar)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 8px',
                  borderRadius: 4,
                  fontSize: 10,
                  fontWeight: 600,
                  background: isCreatingNewChar ? 'rgba(239, 68, 68, 0.2)' : 'rgba(56, 189, 248, 0.15)',
                  border: isCreatingNewChar ? '1px solid #ef4444' : '1px solid #38bdf8',
                  color: isCreatingNewChar ? '#fca5a5' : '#38bdf8',
                  cursor: 'pointer',
                }}
              >
                {isCreatingNewChar ? <X size={10} /> : <Plus size={10} />}
                {isCreatingNewChar ? 'Chọn nhân vật có sẵn' : 'Tạo nhân vật mới'}
              </button>
            </div>

            {isCreatingNewChar ? (
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input
                  type="text"
                  value={newCharName}
                  onChange={(e) => setNewCharName(e.target.value)}
                  placeholder="Ví dụ: Tôn Ngộ Không, Tiêu Viêm, Nữ Hiệp Áo Trắng..."
                  style={{
                    flex: 1,
                    background: '#0d1527',
                    border: '1px solid #38bdf8',
                    borderRadius: 6,
                    padding: '6px 10px',
                    fontSize: 11,
                    color: '#f8fafc',
                    outline: 'none',
                  }}
                />
                <span style={{ fontSize: 10, color: '#94a3b8', background: 'rgba(255,255,255,0.05)', padding: '6px 8px', borderRadius: 4 }}>
                  Slug: <b style={{ color: '#38bdf8' }}>{slugifyVietnamese(newCharName || 'nhan_vat_moi')}</b>
                </span>
              </div>
            ) : (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {characterList.map((char) => (
                  <button
                    key={char}
                    onClick={() => setSelectedCharacter(char)}
                    style={{
                      padding: '5px 12px',
                      borderRadius: 6,
                      fontSize: 11,
                      fontWeight: 600,
                      background: selectedCharacter === char ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255, 255, 255, 0.04)',
                      border: selectedCharacter === char ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                      color: selectedCharacter === char ? '#38bdf8' : '#cbd5e1',
                      cursor: 'pointer',
                    }}
                  >
                    👤 {formatVietnameseDisplayName(char)}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* 2. Action Selection & Creation */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: 11, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
                <Activity size={13} /> 2. Động Tác (Action Motion):
              </label>
              <button
                onClick={() => setIsCreatingNewAction(!isCreatingNewAction)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 8px',
                  borderRadius: 4,
                  fontSize: 10,
                  fontWeight: 600,
                  background: isCreatingNewAction ? 'rgba(239, 68, 68, 0.2)' : 'rgba(74, 222, 128, 0.15)',
                  border: isCreatingNewAction ? '1px solid #ef4444' : '1px solid #4ade80',
                  color: isCreatingNewAction ? '#fca5a5' : '#4ade80',
                  cursor: 'pointer',
                }}
              >
                {isCreatingNewAction ? <X size={10} /> : <Plus size={10} />}
                {isCreatingNewAction ? 'Chọn động tác có sẵn' : 'Tạo động tác mới'}
              </button>
            </div>

            {isCreatingNewAction ? (
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input
                  type="text"
                  value={newActionName}
                  onChange={(e) => setNewActionName(e.target.value)}
                  placeholder="Ví dụ: Chém Kiếm Lôi Điện, Vung Đao, Xuất Chưởng..."
                  style={{
                    flex: 1,
                    background: '#0d1527',
                    border: '1px solid #4ade80',
                    borderRadius: 6,
                    padding: '6px 10px',
                    fontSize: 11,
                    color: '#f8fafc',
                    outline: 'none',
                  }}
                />
                <span style={{ fontSize: 10, color: '#94a3b8', background: 'rgba(255,255,255,0.05)', padding: '6px 8px', borderRadius: 4 }}>
                  Slug: <b style={{ color: '#4ade80' }}>{slugifyVietnamese(newActionName || 'dong_tac_moi')}</b>
                </span>
              </div>
            ) : (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {actionList.map((act) => (
                  <button
                    key={act}
                    onClick={() => setSelectedAction(act)}
                    style={{
                      padding: '5px 12px',
                      borderRadius: 6,
                      fontSize: 11,
                      fontWeight: 600,
                      background: selectedAction === act ? 'rgba(74, 222, 128, 0.25)' : 'rgba(255, 255, 255, 0.04)',
                      border: selectedAction === act ? '1px solid #4ade80' : '1px solid rgba(255, 255, 255, 0.08)',
                      color: selectedAction === act ? '#4ade80' : '#cbd5e1',
                      cursor: 'pointer',
                    }}
                  >
                    ⚔️ {formatVietnameseDisplayName(act)}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* 3. Angle Selection */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 700, color: '#facc15', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Compass size={13} /> 3. Góc Nhìn (Horizontal Angle):
            </label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 6 }}>
              {DEFAULT_ANGLES.map((ang) => {
                const isSelected = selectedAngleDeg === ang.deg;
                return (
                  <button
                    key={ang.id}
                    onClick={() => setSelectedAngleDeg(ang.deg)}
                    style={{
                      padding: '6px 8px',
                      borderRadius: 6,
                      fontSize: 10,
                      fontWeight: isSelected ? 700 : 500,
                      background: isSelected ? 'rgba(250, 204, 21, 0.25)' : 'rgba(255, 255, 255, 0.04)',
                      border: isSelected ? '1px solid #facc15' : '1px solid rgba(255, 255, 255, 0.08)',
                      color: isSelected ? '#facc15' : '#cbd5e1',
                      cursor: 'pointer',
                    }}
                  >
                    {ang.label}
                  </button>
                );
              })}
            </div>
          </div>

          {/* 4. Target Folder Path & Files Preview */}
          <div
            style={{
              background: 'rgba(2, 6, 23, 0.95)',
              border: '1px solid rgba(56, 189, 248, 0.25)',
              borderRadius: 8,
              padding: 10,
              display: 'flex',
              flexDirection: 'column',
              gap: 4,
            }}
          >
            <div style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Folder size={12} /> Đường Dẫn Thư Mục Chuẩn Sẽ Được Tạo:
            </div>
            <div
              style={{
                fontFamily: 'monospace',
                fontSize: 10.5,
                color: '#34d399',
                background: 'rgba(0, 0, 0, 0.5)',
                padding: '5px 8px',
                borderRadius: 4,
                border: '1px solid rgba(52, 211, 153, 0.2)',
                wordBreak: 'break-all',
              }}
            >
              {targetFolderPath.fullPath}/
            </div>
            <div style={{ fontSize: 9.5, color: '#94a3b8', marginTop: 2 }}>
              📦 Gồm <b>{frames.length} file ảnh frame PNG</b> (<code>frame_01.png</code> ... <code>frame_{String(frames.length).padStart(2, '0')}.png</code>) + file metadata <code>motion_meta.json</code>.
            </div>
          </div>

          {/* Toast Notification */}
          {saveToast && (
            <div
              style={{
                padding: '6px 12px',
                borderRadius: 6,
                background: saveToast.startsWith('❌') ? 'rgba(239, 68, 68, 0.25)' : 'rgba(16, 185, 129, 0.25)',
                border: saveToast.startsWith('❌') ? '1px solid #ef4444' : '1px solid #10b981',
                color: saveToast.startsWith('❌') ? '#fca5a5' : '#6ee7b7',
                fontSize: 11,
                fontWeight: 600,
                textAlign: 'center',
              }}
            >
              {saveToast}
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div
          style={{
            height: 52,
            background: 'rgba(15, 23, 42, 0.95)',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
            padding: '0 16px',
            gap: 8,
          }}
        >
          <button
            onClick={onClose}
            disabled={isSaving}
            style={{
              padding: '6px 14px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#94a3b8',
              fontSize: 11,
              fontWeight: 600,
              cursor: isSaving ? 'not-allowed' : 'pointer',
            }}
          >
            Hủy Bỏ
          </button>

          <button
            onClick={handleSave}
            disabled={isSaving || frames.length === 0}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '7px 18px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #10b981)',
              border: 'none',
              color: '#ffffff',
              fontSize: 11,
              fontWeight: 700,
              cursor: isSaving || frames.length === 0 ? 'not-allowed' : 'pointer',
              boxShadow: '0 2px 12px rgba(16, 185, 129, 0.4)',
            }}
          >
            <Check size={13} /> {isSaving ? 'Đang Lưu...' : 'Xác Nhận & Lưu Vào asset_2ds'}
          </button>
        </div>
      </div>
    </div>
  );
};
