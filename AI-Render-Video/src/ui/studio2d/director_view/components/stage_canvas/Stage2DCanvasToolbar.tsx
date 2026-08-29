// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import {
  Move,
  ZoomIn,
  ZoomOut,
  Hand,
  Compass,
  Rotate3d,
  Layers,
} from 'lucide-react';

interface Stage2DCanvasToolbarProps {
  activeTool: 'hand' | 'orbit360';
  viewportZoom: number;
  currentCamAngle: number;
  currentPitch: number;
  zToast: { text: string; time: number } | null;
  onSelectTool: (tool: 'hand' | 'orbit360') => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onResetZoom: () => void;
}

export const Stage2DCanvasToolbar: React.FC<Stage2DCanvasToolbarProps> = ({
  activeTool,
  viewportZoom,
  currentCamAngle,
  currentPitch,
  zToast,
  onSelectTool,
  onZoomIn,
  onZoomOut,
  onResetZoom,
}) => {
  return (
    <>
      {/* ─── TOP-RIGHT INTERACTIVE TOOLBAR (Hand / 360° / Zoom) ───────────── */}
      <div
        style={{
          position: 'absolute',
          top: 14,
          right: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 5,
          background: 'rgba(9, 13, 22, 0.92)',
          backdropFilter: 'blur(12px)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          borderRadius: 8,
          padding: '4px 6px',
          boxShadow: '0 8px 24px rgba(0,0,0,0.7)',
          zIndex: 20,
        }}
      >
        {/* 1. Hand / Transform Tool (DEFAULT ACTIVE) */}
        <button
          onClick={() => onSelectTool('hand')}
          title="Bàn tay: Kéo đối tượng để di chuyển, kéo 4 góc BBox để co dãn size (Phím Shift +/- chỉnh Scale, +/- chỉnh Z-Index)"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 8px',
            borderRadius: 5,
            background: activeTool === 'hand' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.05)',
            border: activeTool === 'hand' ? '1px solid #38bdf8' : '1px solid transparent',
            color: activeTool === 'hand' ? '#38bdf8' : '#94a3b8',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <Hand size={13} />
          <span>Bàn tay</span>
        </button>

        {/* 2. 360° Orbit Tool */}
        <button
          onClick={() => onSelectTool('orbit360')}
          title="Chế độ 360°: Kéo chuột trên khung tranh để xoay 360 độ góc camera quanh sân khấu"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 8px',
            borderRadius: 5,
            background: activeTool === 'orbit360' ? 'linear-gradient(135deg, #7c3aed, #a855f7)' : 'rgba(255, 255, 255, 0.05)',
            border: activeTool === 'orbit360' ? '1px solid #c084fc' : '1px solid transparent',
            color: activeTool === 'orbit360' ? '#ffffff' : '#94a3b8',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            boxShadow: activeTool === 'orbit360' ? '0 0 12px rgba(168, 85, 247, 0.4)' : 'none',
            transition: 'all 0.15s ease',
          }}
        >
          <Rotate3d size={13} />
          <span>360°</span>
        </button>

        <div style={{ width: 1, height: 16, background: 'rgba(255,255,255,0.12)', margin: '0 2px' }} />

        {/* 3. Zoom Controls */}
        <button
          onClick={onZoomOut}
          title="Thu nhỏ Viewport"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 24,
            height: 24,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.06)',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <ZoomOut size={12} />
        </button>

        <button
          onClick={onResetZoom}
          title="Tỉ lệ 100%"
          style={{
            padding: '2px 5px',
            fontSize: 10,
            fontFamily: 'monospace',
            fontWeight: 700,
            color: '#38bdf8',
            background: 'rgba(2, 6, 23, 0.6)',
            border: '1px solid rgba(56, 189, 248, 0.2)',
            borderRadius: 4,
            cursor: 'pointer',
          }}
        >
          {Math.round(viewportZoom * 100)}%
        </button>

        <button
          onClick={onZoomIn}
          title="Phóng to Viewport"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 24,
            height: 24,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.06)',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <ZoomIn size={12} />
        </button>
      </div>

      {/* ─── LIVE 360° COMPASS OVERLAY (Top-Center when active) ──────────── */}
      {activeTool === 'orbit360' && (
        <div
          style={{
            position: 'absolute',
            top: 14,
            left: '50%',
            transform: 'translateX(-50%)',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid rgba(168, 85, 247, 0.5)',
            borderRadius: 20,
            padding: '4px 12px',
            color: '#e9d5ff',
            fontSize: 11,
            fontWeight: 700,
            boxShadow: '0 4px 16px rgba(0,0,0,0.6)',
            zIndex: 15,
            pointerEvents: 'none',
          }}
        >
          <Compass size={14} color="#c084fc" />
          <span>360° Xoay Góc: <b>{Math.round(currentCamAngle)}°</b> (Cao: {Math.round(currentPitch)}°)</span>
        </div>
      )}

      {/* ─── TRANSIENT HUD TOAST ─────────────────────────────────────────── */}
      {zToast && Date.now() - zToast.time < 1800 && (
        <div
          style={{
            position: 'absolute',
            bottom: 20,
            left: '50%',
            transform: 'translateX(-50%)',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid #38bdf8',
            borderRadius: 8,
            padding: '6px 14px',
            color: '#38bdf8',
            fontSize: 12,
            fontWeight: 800,
            boxShadow: '0 8px 24px rgba(0,0,0,0.7)',
            zIndex: 30,
            pointerEvents: 'none',
          }}
        >
          <Layers size={14} />
          <span>{zToast.text}</span>
        </div>
      )}

      {/* Keyboard Shortcut Helper Hint */}
      <div
        style={{
          position: 'absolute',
          bottom: 14,
          left: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(2, 6, 23, 0.85)',
          backdropFilter: 'blur(8px)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 6,
          padding: '4px 8px',
          color: '#cbd5e1',
          fontSize: 9.5,
          pointerEvents: 'none',
        }}
      >
        <Move size={11} color="#38bdf8" /> Phím [0..9] Đổi góc ảnh • [Shift + 0..9] Lật ngang gương ảnh • [ [ ] ] Xoay • [Shift +/-] Co Dãn • [+] / [-] Z-Index
      </div>
    </>
  );
};
