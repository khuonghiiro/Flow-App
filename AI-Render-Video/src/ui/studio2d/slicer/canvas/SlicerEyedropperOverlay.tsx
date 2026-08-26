import React from 'react';

interface SlicerEyedropperOverlayProps {
  isEyedropperActive: boolean;
  eyedropperTarget: 'chroma' | 'fringe' | 'smooth';
  eyedropperHoverColor: { hex: string; r: number; g: number; b: number; x: number; y: number } | null;
}

export const SlicerEyedropperOverlay: React.FC<SlicerEyedropperOverlayProps> = ({
  isEyedropperActive,
  eyedropperTarget,
  eyedropperHoverColor,
}) => {
  if (!isEyedropperActive) return null;

  return (
    <>
      {/* Top Eyedropper Guidance Banner */}
      <div
        style={{
          position: 'absolute',
          top: 12,
          left: '50%',
          transform: 'translateX(-50%)',
          background: 'rgba(15, 23, 42, 0.94)',
          border:
            eyedropperTarget === 'smooth'
              ? '1.5px solid #38bdf8'
              : eyedropperTarget === 'fringe'
              ? '1.5px solid #10b981'
              : '1.5px solid #f59e0b',
          borderRadius: 20,
          padding: '5px 14px',
          color:
            eyedropperTarget === 'smooth'
              ? '#38bdf8'
              : eyedropperTarget === 'fringe'
              ? '#a7f3d0'
              : '#fef08a',
          fontSize: 10.5,
          fontWeight: 600,
          boxShadow: '0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(56,189,248,0.3)',
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          zIndex: 30,
          pointerEvents: 'none',
          fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
        }}
      >
        <span>
          {eyedropperTarget === 'smooth'
            ? '🎯 Chế độ hút màu viền làm mịn: Rê chuột và nhấp vào nét vẽ để chọn màu viền khử răng cưa'
            : eyedropperTarget === 'fringe'
            ? '🎯 Chế độ hút màu viền rác: Rê chuột và nhấp vào vùng viền sượng/sạn để chọn màu khử'
            : '🎯 Chế độ hút màu nền: Rê chuột lên ảnh và nhấp chuột để chọn mã màu nền cần tách'}
        </span>
      </div>

      {/* Photoshop CS6 Style Floating Loupe / Color Preview Swatch */}
      {eyedropperHoverColor && (
        <div
          style={{
            position: 'fixed',
            left: eyedropperHoverColor.x + 18,
            top: eyedropperHoverColor.y - 45,
            pointerEvents: 'none',
            zIndex: 9999,
            background: 'rgba(15, 23, 42, 0.94)',
            backdropFilter: 'blur(8px)',
            border: '1.5px solid #38bdf8',
            borderRadius: 8,
            padding: '6px 10px',
            boxShadow: '0 8px 24px rgba(0,0,0,0.85), 0 0 14px rgba(56,189,248,0.4)',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            transform: 'translate3d(0,0,0)',
            fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
          }}
        >
          <div
            style={{
              width: 30,
              height: 30,
              borderRadius: '50%',
              background: eyedropperHoverColor.hex,
              border: '2.5px solid #ffffff',
              boxShadow: '0 0 0 1.5px #000000, 0 2px 6px rgba(0,0,0,0.5)',
              flexShrink: 0,
            }}
          />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div
              style={{
                fontSize: 11.5,
                fontWeight: 700,
                color: '#38bdf8',
                fontFamily: 'monospace',
                letterSpacing: 0.5,
              }}
            >
              {eyedropperHoverColor.hex}
            </div>
            <div style={{ fontSize: 9, color: '#94a3b8', fontFamily: 'monospace' }}>
              RGB({eyedropperHoverColor.r}, {eyedropperHoverColor.g}, {eyedropperHoverColor.b})
            </div>
            <div style={{ fontSize: 8.5, color: '#4ade80', fontWeight: 600 }}>
              👆 Nhấp chuột để lấy màu
            </div>
          </div>
        </div>
      )}
    </>
  );
};
