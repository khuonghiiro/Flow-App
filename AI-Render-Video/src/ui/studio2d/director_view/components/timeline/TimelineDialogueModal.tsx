import React from 'react';
import { MessageSquare, X } from 'lucide-react';
import { Director2DProject } from '../../../../../types/studio2d_director';

interface TimelineDialogueModalProps {
  modalState: {
    isOpen: boolean;
    targetShotId: string;
    targetTime: number;
    text: string;
    speakerId: string;
  };
  project: Director2DProject;
  onClose: () => void;
  onChangeText: (text: string) => void;
  onChangeSpeaker: (speakerId: string) => void;
  onApplyDialogue: (text: string, speakerId: string) => void;
}

export const TimelineDialogueModal: React.FC<TimelineDialogueModalProps> = ({
  modalState,
  project,
  onClose,
  onChangeText,
  onChangeSpeaker,
  onApplyDialogue,
}) => {
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'rgba(0, 0, 0, 0.72)',
        backdropFilter: 'blur(6px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: 'linear-gradient(180deg, #0f172a 0%, #090d16 100%)',
          border: '1px solid rgba(168, 85, 247, 0.5)',
          boxShadow: '0 16px 50px rgba(0, 0, 0, 0.9), 0 0 30px rgba(168, 85, 247, 0.25)',
          borderRadius: 14,
          padding: 18,
          width: 440,
          maxWidth: '92vw',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 14, fontWeight: 700, color: '#d8b4fe' }}>
            <MessageSquare size={18} /> Gắn Lời Thoại Cho Đoạn Này
          </div>
          <button
            onClick={onClose}
            style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer' }}
          >
            <X size={18} />
          </button>
        </div>

        {/* Speaker Selector */}
        <div style={{ marginBottom: 12 }}>
          <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 5 }}>
            Nhân vật phát biểu:
          </label>
          <select
            value={modalState.speakerId}
            onChange={(e) => onChangeSpeaker(e.target.value)}
            style={{
              width: '100%',
              padding: '7px 10px',
              borderRadius: 8,
              background: 'rgba(2, 6, 23, 0.9)',
              border: '1px solid rgba(255, 255, 255, 0.18)',
              color: '#fff',
              fontSize: 12,
              outline: 'none',
            }}
          >
            {project.actors.map((a) => (
              <option key={a.id} value={a.id}>
                {a.avatarIcon || '👤'} {a.name}
              </option>
            ))}
          </select>
        </div>

        {/* Dialogue Text Input */}
        <div style={{ marginBottom: 14 }}>
          <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 5 }}>
            Nội dung câu thoại / Phụ đề:
          </label>
          <textarea
            rows={3}
            value={modalState.text}
            onChange={(e) => onChangeText(e.target.value)}
            placeholder="Nhập nội dung thoại đối thoại tại đây..."
            style={{
              width: '100%',
              padding: '9px 12px',
              borderRadius: 8,
              background: 'rgba(2, 6, 23, 0.9)',
              border: '1.5px solid rgba(168, 85, 247, 0.4)',
              color: '#fff',
              fontSize: 12,
              outline: 'none',
              resize: 'none',
            }}
          />
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button
            onClick={onClose}
            style={{
              padding: '6px 14px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.06)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              color: '#94a3b8',
              fontSize: 11.5,
              cursor: 'pointer',
            }}
          >
            Hủy
          </button>
          <button
            onClick={() => onApplyDialogue(modalState.text, modalState.speakerId)}
            style={{
              padding: '6px 16px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #a855f7, #ec4899)',
              border: 'none',
              color: '#fff',
              fontSize: 11.5,
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            Lưu Thoại
          </button>
        </div>
      </div>
    </div>
  );
};
