import React from 'react';
import { ZoomIn, ZoomOut, RotateCcw, Sparkles, Move, FileCode, Check } from 'lucide-react';
import { SkillBranchCategory } from './types';
import { BRANCH_CONFIGS } from './skillTreeData';

interface SkillTreeControlBarProps {
  activeBranch: SkillBranchCategory | 'all';
  onSelectBranch: (branch: SkillBranchCategory | 'all') => void;
  zoom: number;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onResetZoom: () => void;
  totalNodes: number;
  isEditMode: boolean;
  onToggleEditMode: () => void;
  onOpenExportModal: () => void;
  isCustomized?: boolean;
  lineStyle?: 'orthogonal' | 'curved';
  onToggleLineStyle?: () => void;
  isAltPressed?: boolean;
  canvasTheme?: 'dark' | 'light';
  onToggleCanvasTheme?: () => void;
}

export const SkillTreeControlBar: React.FC<SkillTreeControlBarProps> = ({
  activeBranch,
  onSelectBranch,
  zoom,
  onZoomIn,
  onZoomOut,
  onResetZoom,
  totalNodes,
  isEditMode,
  onToggleEditMode,
  onOpenExportModal,
  isCustomized = false,
  lineStyle = 'orthogonal',
  onToggleLineStyle,
  isAltPressed = false,
  canvasTheme = 'dark',
  onToggleCanvasTheme,
}) => {
  const branches: (SkillBranchCategory | 'all')[] = [
    'all',
    'character',
    'walk',
    'actions',
    'face',
    'weapons',
  ];

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: 8,
        padding: '8px 12px',
        background: canvasTheme === 'dark' ? 'rgba(18, 25, 38, 0.94)' : 'rgba(255, 255, 255, 0.92)',
        border: canvasTheme === 'dark' ? '1px solid rgba(148, 163, 184, 0.14)' : '1px solid rgba(0, 0, 0, 0.10)',
        borderRadius: 8,
        backdropFilter: 'blur(8px)',
      }}
    >
      {/* ─── Branch Filter Pills ─── */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 11, fontWeight: 600, color: '#94a3b8', marginRight: 4, display: 'flex', alignItems: 'center', gap: 4 }}>
          <Sparkles size={12} color="#f59e0b" /> Nhánh:
        </span>
        {branches.map((b) => {
          const isAll = b === 'all';
          const isSelected = activeBranch === b;
          const conf = !isAll ? BRANCH_CONFIGS[b] : null;
          const color = isAll ? '#38bdf8' : conf?.color || '#38bdf8';
          const label = isAll ? '🌟 Toàn Bộ Cây' : `${conf?.icon} ${conf?.label}`;

          return (
            <button
              key={b}
              onClick={() => onSelectBranch(b)}
              style={{
                padding: '4px 10px',
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 20,
                cursor: 'pointer',
                border: isSelected ? `1.5px solid ${color}` : '1px solid rgba(255, 255, 255, 0.08)',
                background: isSelected ? `${color}25` : 'rgba(255, 255, 255, 0.03)',
                color: isSelected ? '#ffffff' : '#94a3b8',
                boxShadow: isSelected ? `0 0 14px ${color}55` : 'none',
                transition: 'all 0.15s ease',
              }}
            >
              {label}
            </button>
          );
        })}
      </div>

      {/* ─── Right Side: Drag Mode Toggle & Export Layout Button ─── */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        {/* Toggle Drag Edit Mode */}
        <button
          onClick={onToggleEditMode}
          title={isEditMode ? 'Đang bật chế độ kéo thả node' : 'Bật chế độ kéo thả node'}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            padding: '5px 11px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 6,
            cursor: 'pointer',
            border: isEditMode
              ? '1.5px solid #38bdf8'
              : '1px solid rgba(255, 255, 255, 0.12)',
            background: isEditMode
              ? 'rgba(56, 189, 248, 0.2)'
              : 'rgba(255, 255, 255, 0.04)',
            color: isEditMode ? '#38bdf8' : '#94a3b8',
            boxShadow: isEditMode ? '0 0 14px rgba(56, 189, 248, 0.35)' : 'none',
            transition: 'all 0.15s ease',
          }}
        >
          <Move size={12} />
          {isEditMode ? '✏️ Kéo thả: Bật' : '🔒 Khóa'}
        </button>

        {/* Alt-drag Hint Badge when Edit Mode is active */}
        {isEditMode && (
          <div
            title="Nhấn giữ phím Alt và kéo chuột trái vào bất kỳ node nào để di chuyển cả cụm các node con của nó theo"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 8px',
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 6,
              background: isAltPressed
                ? 'rgba(245, 158, 11, 0.25)'
                : 'rgba(255, 255, 255, 0.04)',
              border: isAltPressed
                ? '1px solid #f59e0b'
                : '1px dashed rgba(255, 255, 255, 0.15)',
              color: isAltPressed ? '#fbbf24' : '#94a3b8',
              boxShadow: isAltPressed ? '0 0 12px rgba(245, 158, 11, 0.35)' : 'none',
              transition: 'all 0.15s ease',
              userSelect: 'none',
            }}
          >
            <span>{isAltPressed ? '🔥 [Alt] Đang kéo cả cụm con' : '💡 Giữ Alt: Kéo cả cụm con'}</span>
          </div>
        )}

        {/* Toggle Line Style: Orthogonal vs Curved */}
        <button
          onClick={onToggleLineStyle}
          title={
            lineStyle === 'orthogonal'
              ? 'Đang dùng đường nối vuông góc (Nhấp để đổi sang uốn cong Bézier)'
              : 'Đang dùng đường nối uốn cong Bézier (Nhấp để đổi sang vuông góc)'
          }
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            padding: '5px 11px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 6,
            cursor: 'pointer',
            border: lineStyle === 'orthogonal'
              ? '1.5px solid #a855f7'
              : '1px solid rgba(255, 255, 255, 0.12)',
            background: lineStyle === 'orthogonal'
              ? 'rgba(168, 85, 247, 0.25)'
              : 'rgba(255, 255, 255, 0.04)',
            color: lineStyle === 'orthogonal' ? '#c084fc' : '#94a3b8',
            boxShadow: lineStyle === 'orthogonal' ? '0 0 14px rgba(168, 85, 247, 0.4)' : 'none',
            transition: 'all 0.15s ease',
          }}
        >
          {lineStyle === 'orthogonal' ? '🔲 Line: Vuông góc' : '〰️ Line: Uốn cong'}
        </button>

        {/* Toggle Canvas Theme: Dark vs Light */}
        <button
          onClick={onToggleCanvasTheme}
          title={canvasTheme === 'dark' ? 'Chuyển sang nền Canvas Sáng' : 'Chuyển sang nền Canvas Tối'}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '5px 10px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 6,
            cursor: 'pointer',
            border: canvasTheme === 'light' ? '1.5px solid #f59e0b' : '1px solid rgba(255, 255, 255, 0.12)',
            background: canvasTheme === 'light'
              ? 'rgba(245, 158, 11, 0.22)'
              : 'rgba(255, 255, 255, 0.04)',
            color: canvasTheme === 'light' ? '#f59e0b' : '#94a3b8',
            boxShadow: canvasTheme === 'light' ? '0 0 14px rgba(245, 158, 11, 0.35)' : 'none',
            transition: 'all 0.15s ease',
          }}
        >
          {canvasTheme === 'dark' ? '🌙 Nền tối' : '☀️ Nền sáng'}
        </button>

        {/* Copy / Export Layout Coordinates */}
        <button
          onClick={onOpenExportModal}
          title="Xem và Copy toạ độ vị trí các node để gửi cho AI"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            padding: '5px 12px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 6,
            cursor: 'pointer',
            border: isCustomized
              ? '1.5px solid #10b981'
              : '1px solid rgba(56, 189, 248, 0.35)',
            background: isCustomized
              ? 'linear-gradient(135deg, rgba(16, 185, 129, 0.25) 0%, rgba(5, 150, 105, 0.25) 100%)'
              : 'linear-gradient(135deg, rgba(2, 132, 199, 0.2) 0%, rgba(3, 105, 161, 0.2) 100%)',
            color: isCustomized ? '#34d399' : '#38bdf8',
            boxShadow: isCustomized
              ? '0 0 16px rgba(16, 185, 129, 0.35)'
              : '0 0 12px rgba(56, 189, 248, 0.25)',
            transition: 'all 0.15s ease',
          }}
        >
          <FileCode size={12} />
          {isCustomized ? '📋 Đã sửa — Copy toạ độ' : '📋 Xuất toạ độ'}
        </button>

        {/* Zoom Controls */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            background: 'rgba(255, 255, 255, 0.05)',
            border: '1px solid rgba(255, 255, 255, 0.1)',
            borderRadius: 6,
            padding: 2,
          }}
        >
          <button
            onClick={onZoomOut}
            title="Thu nhỏ"
            style={{
              padding: '4px 6px',
              background: 'transparent',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <ZoomOut size={13} />
          </button>
          <span style={{ fontSize: 10, fontWeight: 600, color: '#e2e8f0', minWidth: 34, textAlign: 'center' }}>
            {Math.round(zoom * 100)}%
          </span>
          <button
            onClick={onZoomIn}
            title="Phóng to"
            style={{
              padding: '4px 6px',
              background: 'transparent',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <ZoomIn size={13} />
          </button>
          <button
            onClick={onResetZoom}
            title="Đặt lại khung nhìn"
            style={{
              padding: '4px 6px',
              background: 'transparent',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              borderLeft: '1px solid rgba(255, 255, 255, 0.1)',
            }}
          >
            <RotateCcw size={13} />
          </button>
        </div>
      </div>
    </div>
  );
};

