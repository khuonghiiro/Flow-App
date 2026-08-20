import React, { useState, useRef, useCallback, useEffect } from 'react';
import { 
  Compass, ChevronDown, ChevronUp, RotateCw, ZoomIn, ZoomOut, 
  ArrowUp, ArrowDown, ArrowLeft, ArrowRight, Eye, EyeOff, X, Maximize2
} from 'lucide-react';
import { ThreeRenderer } from '../core/engine/ThreeRenderer';

interface CameraNavigationWidgetProps {
  renderer: ThreeRenderer | null;
  visible?: boolean;
}

interface ContinuousButtonProps {
  onAction: () => void;
  title: string;
  className?: string;
  style?: React.CSSProperties;
  children: React.ReactNode;
}

const ContinuousButton: React.FC<ContinuousButtonProps> = ({
  onAction,
  title,
  style,
  children,
}) => {
  const timerRef = useRef<any>(null);
  const intervalRef = useRef<any>(null);

  const start = useCallback((e: React.SyntheticEvent) => {
    e.preventDefault();
    onAction();
    timerRef.current = setTimeout(() => {
      intervalRef.current = setInterval(() => {
        onAction();
      }, 35);
    }, 180);
  }, [onAction]);

  const stop = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  useEffect(() => {
    return () => stop();
  }, [stop]);

  return (
    <button
      onMouseDown={start}
      onMouseUp={stop}
      onMouseLeave={stop}
      onTouchStart={start}
      onTouchEnd={stop}
      onTouchCancel={stop}
      style={{
        userSelect: 'none',
        WebkitUserSelect: 'none',
        touchAction: 'manipulation',
        cursor: 'pointer',
        ...style,
      }}
      title={`${title} (Nhấn giữ để di chuyển liên tục)`}
    >
      {children}
    </button>
  );
};

export const CameraNavigationWidget: React.FC<CameraNavigationWidgetProps> = ({
  renderer,
  visible = true,
}) => {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isOpen, setIsOpen] = useState(true);

  if (!visible || !isOpen) {
    return (
      <button
        onClick={() => setIsOpen(true)}
        style={{
          position: 'absolute',
          bottom: 16,
          right: 16,
          width: 32,
          height: 32,
          borderRadius: 8,
          background: 'rgba(15, 23, 42, 0.85)',
          border: '1px solid rgba(56, 189, 248, 0.4)',
          color: '#38bdf8',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          boxShadow: '0 4px 14px rgba(0, 0, 0, 0.4)',
          zIndex: 40,
          cursor: 'pointer',
        }}
        title="Mở Bảng Điều Khiển & Xoay Camera 360"
      >
        <Compass size={16} />
      </button>
    );
  }

  return (
    <div
      style={{
        position: 'absolute',
        bottom: 16,
        right: 16,
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
        background: 'rgba(15, 23, 42, 0.9)',
        border: '1px solid rgba(255, 255, 255, 0.12)',
        borderRadius: 8,
        padding: '6px 8px',
        backdropFilter: 'blur(10px)',
        boxShadow: '0 4px 20px rgba(0, 0, 0, 0.5)',
        zIndex: 40,
        pointerEvents: 'auto',
        fontFamily: 'Inter, system-ui, sans-serif',
        minWidth: 120,
      }}
    >
      {/* Widget Title Bar with Toggle & Minimize Buttons */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        borderBottom: isCollapsed ? 'none' : '1px solid rgba(255, 255, 255, 0.08)',
        paddingBottom: isCollapsed ? 0 : 4,
        marginBottom: isCollapsed ? 0 : 4,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, color: '#38bdf8', fontSize: 10, fontWeight: 700 }}>
          <Compass size={12} />
          <span>Camera Nav</span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <button
            onClick={() => setIsCollapsed(!isCollapsed)}
            style={{
              background: 'transparent', border: 'none', color: '#94a3b8',
              cursor: 'pointer', padding: 2, display: 'flex', alignItems: 'center',
            }}
            title={isCollapsed ? "Mở rộng bảng điều khiển" : "Thu gọn"}
          >
            {isCollapsed ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
          </button>
          <button
            onClick={() => setIsOpen(false)}
            style={{
              background: 'transparent', border: 'none', color: '#94a3b8',
              cursor: 'pointer', padding: 2, display: 'flex', alignItems: 'center',
            }}
            title="Ẩn bảng điều khiển camera"
          >
            <X size={12} />
          </button>
        </div>
      </div>

      {!isCollapsed && (
        <>
          {/* Preset Views Row */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 3, marginBottom: 2 }}>
            <button
              onClick={() => renderer?.setCameraPresetView('top')}
              style={{
                padding: '3px 0', fontSize: 9, fontWeight: 700, borderRadius: 3,
                background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#38bdf8', cursor: 'pointer',
              }}
              title="Góc nhìn từ trên xuống (Top View)"
            >
              TOP
            </button>
            <button
              onClick={() => renderer?.setCameraPresetView('front')}
              style={{
                padding: '3px 0', fontSize: 9, fontWeight: 700, borderRadius: 3,
                background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#4ade80', cursor: 'pointer',
              }}
              title="Góc nhìn chính diện (Front View)"
            >
              FRONT
            </button>
            <button
              onClick={() => renderer?.setCameraPresetView('right')}
              style={{
                padding: '3px 0', fontSize: 9, fontWeight: 700, borderRadius: 3,
                background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#fb923c', cursor: 'pointer',
              }}
              title="Góc nhìn cạnh bên (Side View)"
            >
              SIDE
            </button>
            <button
              onClick={() => renderer?.setCameraPresetView('perspective')}
              style={{
                padding: '3px 0', fontSize: 9, fontWeight: 700, borderRadius: 3,
                background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#fef08a', cursor: 'pointer',
              }}
              title="Góc nhìn 3D tổng thể (3D View)"
            >
              3D
            </button>
          </div>

          {/* Directional Navigation D-Pad with Continuous Long-Press Support */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 26px)', gridTemplateRows: 'repeat(3, 26px)', gap: 3, justifyContent: 'center' }}>
            {/* Top: Pan Up */}
            <div />
            <ContinuousButton
              onAction={() => renderer?.panCamera(0, 0.35)}
              title="Kéo camera lên trên"
              style={{
                borderRadius: 4, background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.12)',
                color: '#ffffff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11,
              }}
            >
              ⬆️
            </ContinuousButton>
            <div />

            {/* Middle Row: Pan Left, Orbit 360, Pan Right */}
            <ContinuousButton
              onAction={() => renderer?.panCamera(-0.35, 0)}
              title="Kéo camera sang trái"
              style={{
                borderRadius: 4, background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.12)',
                color: '#ffffff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11,
              }}
            >
              ⬅️
            </ContinuousButton>

            <ContinuousButton
              onAction={() => renderer?.rotateCameraInPlace(2.5, 0)}
              title="Xoay góc nhìn 360 tại chỗ (Đứng yên tại vị trí camera và xoay nhìn xung quanh 360 độ)"
              style={{
                borderRadius: 4, background: 'rgba(56, 189, 248, 0.25)', border: '1px solid #38bdf8',
                color: '#38bdf8', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11,
                boxShadow: '0 0 10px rgba(56, 189, 248, 0.3)',
              }}
            >
              🔄
            </ContinuousButton>

            <ContinuousButton
              onAction={() => renderer?.panCamera(0.35, 0)}
              title="Kéo camera sang phải"
              style={{
                borderRadius: 4, background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.12)',
                color: '#ffffff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11,
              }}
            >
              ➡️
            </ContinuousButton>

            {/* Bottom: Pan Down */}
            <div />
            <ContinuousButton
              onAction={() => renderer?.panCamera(0, -0.35)}
              title="Kéo camera xuống dưới"
              style={{
                borderRadius: 4, background: 'rgba(255, 255, 255, 0.08)', border: '1px solid rgba(255, 255, 255, 0.12)',
                color: '#ffffff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11,
              }}
            >
              ⬇️
            </ContinuousButton>
            <div />
          </div>

          {/* Zoom Buttons Row with Continuous Long-Press Support */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4, marginTop: 2 }}>
            <ContinuousButton
              onAction={() => renderer?.zoomCamera(-0.5)}
              title="Phóng to (Zoom In)"
              style={{
                padding: '4px', borderRadius: 4, background: 'rgba(255, 255, 255, 0.08)',
                border: '1px solid rgba(255, 255, 255, 0.12)', color: '#ffffff', fontSize: 10,
                display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 2,
              }}
            >
              🔍 +
            </ContinuousButton>

            <ContinuousButton
              onAction={() => renderer?.zoomCamera(0.5)}
              title="Thu nhỏ (Zoom Out)"
              style={{
                padding: '4px', borderRadius: 4, background: 'rgba(255, 255, 255, 0.08)',
                border: '1px solid rgba(255, 255, 255, 0.12)', color: '#ffffff', fontSize: 10,
                display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 2,
              }}
            >
              🔎 -
            </ContinuousButton>
          </div>
        </>
      )}
    </div>
  );
};
