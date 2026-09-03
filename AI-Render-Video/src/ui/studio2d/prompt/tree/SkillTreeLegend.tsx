import React from 'react';

interface SkillTreeLegendProps {
  isEditMode: boolean;
  isAltPressed: boolean;
}

export const SkillTreeLegend: React.FC<SkillTreeLegendProps> = ({
  isEditMode,
  isAltPressed,
}) => {
  return (
    <div
      style={{
        position: 'absolute',
        bottom: 12,
        left: 12,
        padding: '6px 14px',
        borderRadius: 8,
        background: 'rgba(18, 25, 38, 0.92)',
        border: '1px solid rgba(148, 163, 184, 0.18)',
        boxShadow: '0 4px 16px rgba(0, 0, 0, 0.3)',
        fontSize: 11,
        color: '#cbd5e1',
        backdropFilter: 'blur(8px)',
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        pointerEvents: 'none',
        userSelect: 'none',
      }}
    >
      <span>• <b>Kéo nền:</b> Di chuyển góc nhìn</span>
      <span>• <b>Cuộn chuột:</b> Thu phóng camera</span>
      {isEditMode ? (
        <span style={{ color: isAltPressed ? '#fbbf24' : '#38bdf8', fontWeight: 700 }}>
          • {isAltPressed ? '🔥 Đang giữ Alt: Kéo sẽ di chuyển toàn bộ nhánh con' : '⚡ Giữ phím [Alt] + Kéo chuột: Di chuyển cả cụm nhánh con'}
        </span>
      ) : (
        <span>• <b>Click node:</b> Chọn & nạp Prompt</span>
      )}
    </div>
  );
};
