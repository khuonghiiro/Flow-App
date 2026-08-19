import React, { useRef, useEffect, useState } from 'react';
import { Camera, Zap, RotateCcw, Compass, MessageSquare, Eye, EyeOff } from 'lucide-react';
import { ThreeRenderer } from '../core/engine/ThreeRenderer';
import { SubtitleOverlay } from './SubtitleOverlay';
import { ActiveSubtitle } from '../core/subtitles/SubtitleSynchronizer';
import { SubtitlesConfig } from '../types/scene';

interface ViewportCanvasProps {
  renderer: ThreeRenderer | null;
  fps: number;
  activeSubtitle: ActiveSubtitle | null;
  subtitlesConfig: SubtitlesConfig;
  showCC: boolean;
  isInspecting?: boolean;
  isFreeCam?: boolean;
  isLoadingMap?: boolean;
  onToggleCC: () => void;
  onToggleFreeCam?: () => void;
  onResetCamera?: () => void;
}

export const ViewportCanvas: React.FC<ViewportCanvasProps> = ({
  renderer,
  fps,
  activeSubtitle,
  subtitlesConfig,
  showCC,
  isInspecting,
  isFreeCam,
  isLoadingMap,
  onToggleCC,
  onToggleFreeCam,
  onResetCamera,
}) => {
  const mountRef = useRef<HTMLDivElement>(null);
  const [showUI, setShowUI] = useState(true);

  useEffect(() => {
    if (!renderer || !mountRef.current) return;
    renderer.mount(mountRef.current);
    renderer.start();

    return () => {
      renderer.unmount();
    };
  }, [renderer]);

  return (
    <div className="viewport-wrapper">
      <div ref={mountRef} className="viewport-canvas-container" />

      {/* Loading Map Notification Banner */}
      {isLoadingMap && (
        <div
          style={{
            position: 'absolute',
            top: 54,
            left: '50%',
            transform: 'translateX(-50%)',
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid #38bdf8',
            boxShadow: '0 0 24px rgba(56, 189, 248, 0.45)',
            borderRadius: 24,
            padding: '8px 20px',
            color: '#ffffff',
            fontSize: 12,
            fontWeight: 600,
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            zIndex: 100,
            pointerEvents: 'none',
          }}
        >
          <div
            style={{
              width: 14,
              height: 14,
              border: '2px solid #38bdf8',
              borderTopColor: 'transparent',
              borderRadius: '50%',
              animation: 'spin 0.8s linear infinite',
            }}
          />
          ⏳ Đang nạp Map 3D Đại Thánh Đường (103.8 MB)... Vui lòng đợi trong giây lát
        </div>
      )}

      {/* Top HUD */}
      <div className="viewport-hud" style={{ alignItems: 'flex-start' }}>
        
        {/* Left Side: FPS and Free Cam */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, opacity: showUI ? 1 : 0, transition: 'opacity 0.2s', pointerEvents: showUI ? 'auto' : 'none' }}>
          <div className="hud-pill fps-counter">
            <Zap size={13} /> {fps} FPS
          </div>
          
          {/* Free Cam Toggle */}
          {isFreeCam ? (
            <button
              id="toggle-free-cam-btn"
              className="btn-primary"
              style={{
                borderRadius: 20,
                padding: '4px 14px',
                fontSize: 11,
                background: 'linear-gradient(135deg, #059669, #10b981)',
                borderColor: '#34d399',
                boxShadow: '0 0 16px rgba(16, 185, 129, 0.4)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
              }}
              onClick={onToggleFreeCam}
            >
              <RotateCcw size={13} /> <strong>360 độ (Tắt)</strong>
            </button>
          ) : (
            <button
              id="toggle-free-cam-btn"
              className="btn-secondary"
              style={{
                borderRadius: 20,
                padding: '4px 12px',
                fontSize: 11,
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                borderColor: '#38bdf8',
                color: '#38bdf8',
                cursor: 'pointer',
                background: 'rgba(15, 23, 42, 0.7)',
              }}
              onClick={onToggleFreeCam}
              title="Ngắt camera kịch bản để tự do xoay, phóng to thu nhỏ và soi quanh cảnh vật bằng chuột"
            >
              <Compass size={13} /> <strong>Cam Tự Do</strong>
            </button>
          )}
        </div>
        <div style={{ flex: 1 }} />

        {/* Right Actions: Eye Toggle, CC Subtitle, Inspect Reset */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, alignItems: 'flex-end', pointerEvents: 'auto' }}>
          <div style={{ display: 'flex', gap: 8 }}>
            <button
              className="btn-secondary"
              style={{
                borderRadius: '50%',
                width: 28,
                height: 28,
                padding: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: 'rgba(30, 41, 59, 0.7)',
                borderColor: '#475569',
                color: showUI ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
              }}
              onClick={() => setShowUI(!showUI)}
              title="Ẩn/Hiện Giao Diện"
            >
              {showUI ? <Eye size={14} /> : <EyeOff size={14} />}
            </button>

            <button
              id="toggle-subtitles-btn"
              className={`btn-secondary ${showCC ? 'active' : ''}`}
              style={{
                borderRadius: 20,
                padding: '4px 12px',
                fontSize: 11,
                backgroundColor: showCC ? 'rgba(99, 102, 241, 0.8)' : 'rgba(30, 41, 59, 0.7)',
                borderColor: showCC ? '#818cf8' : '#475569',
                color: showCC ? '#ffffff' : '#64748b',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                cursor: 'pointer',
                fontWeight: showCC ? 600 : 400,
              }}
              onClick={onToggleCC}
            >
              <MessageSquare size={12} /> CC
            </button>
          </div>

          {/* Inspect Mode Reset Button (Only visible if showUI is true) */}
          {showUI && isInspecting && !isFreeCam && (
            <button
              id="reset-inspect-camera-btn"
              className="btn-primary"
              style={{
                padding: '4px 12px',
                fontSize: 10,
                background: 'linear-gradient(135deg, rgba(239, 68, 68, 0.85), rgba(244, 63, 94, 0.95))',
                borderColor: '#f43f5e',
                boxShadow: '0 0 16px rgba(244, 63, 94, 0.4)',
                borderRadius: 20,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
              onClick={onResetCamera}
            >
              <RotateCcw size={10} /> Khôi Phục Cam Phim
            </button>
          )}
        </div>
      </div>

      {/* Subtitle Overlay */}
      <SubtitleOverlay
        subtitle={activeSubtitle}
        config={subtitlesConfig}
        showCC={showCC}
      />
    </div>
  );
};
