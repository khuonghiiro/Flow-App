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
  subtitlesConfig?: SubtitlesConfig | null;
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
        
        {/* Left Side: FPS & Controls Guide */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, opacity: showUI ? 1 : 0, transition: 'opacity 0.2s', pointerEvents: showUI ? 'auto' : 'none' }}>
          <div className="hud-pill fps-counter" style={{ padding: '2px 8px', fontSize: 10, borderRadius: 12, background: 'rgba(15, 23, 42, 0.6)', alignSelf: 'flex-start' }}>
            <Zap size={10} /> {fps} FPS
          </div>
          
          {/* Controls Guide */}
          <div style={{
            background: 'rgba(15, 23, 42, 0.7)',
            backdropFilter: 'blur(8px)',
            borderRadius: 8,
            padding: '10px 12px',
            border: '1px solid rgba(255, 255, 255, 0.1)',
            fontSize: 10,
            color: '#cbd5e1',
            display: 'flex',
            flexDirection: 'column',
            gap: 6,
            marginTop: 4,
            width: 140
          }}>
            <div style={{ color: '#38bdf8', fontWeight: 600, marginBottom: 4, fontSize: 11 }}>ĐIỀU KHIỂN</div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>W,A,S,D</span> <span style={{ opacity: 0.7 }}>Di chuyển</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>Space</span> <span style={{ opacity: 0.7 }}>Nhảy</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>Shift</span> <span style={{ opacity: 0.7 }}>Chạy</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>1</span> <span style={{ opacity: 0.7 }}>Nói chuyện</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>2</span> <span style={{ opacity: 0.7 }}>Đỡ đòn</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>3</span> <span style={{ opacity: 0.7 }}>Ngồi</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>4</span> <span style={{ opacity: 0.7 }}>Tấn công</span>
            </div>
          </div>
        </div>
        <div style={{ flex: 1 }} />

        {/* Right Actions: Eye Toggle, CC Subtitle, 360 Free Cam, Inspect Reset */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, alignItems: 'flex-end', pointerEvents: 'auto' }}>
          {/* Eye Toggle (Always visible) */}
          <button
            className="btn-secondary"
            style={{
              borderRadius: 8,
              width: 32,
              height: 32,
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
            {showUI ? <Eye size={16} /> : <EyeOff size={16} />}
          </button>

          {/* Hidden UI Items */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, opacity: showUI ? 1 : 0, pointerEvents: showUI ? 'auto' : 'none', transition: 'opacity 0.2s', alignItems: 'flex-end' }}>
            
            {/* CC Toggle */}
            <button
              id="toggle-subtitles-btn"
              className={`btn-secondary ${showCC ? 'active' : ''}`}
              style={{
                borderRadius: 8,
                width: 32,
                height: 32,
                padding: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: showCC ? 'rgba(99, 102, 241, 0.8)' : 'rgba(30, 41, 59, 0.7)',
                borderColor: showCC ? '#818cf8' : '#475569',
                color: showCC ? '#ffffff' : '#64748b',
                cursor: 'pointer',
                fontWeight: 700,
                fontSize: 12,
              }}
              onClick={onToggleCC}
              title="Phụ đề"
            >
              CC
            </button>

            {/* 360 Cam Toggle */}
            <button
              id="toggle-free-cam-btn"
              className={isFreeCam ? "btn-primary" : "btn-secondary"}
              style={{
                borderRadius: 8,
                width: 32,
                height: 32,
                padding: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: isFreeCam ? 'rgba(16, 185, 129, 0.9)' : 'rgba(30, 41, 59, 0.7)',
                borderColor: isFreeCam ? '#34d399' : '#475569',
                color: isFreeCam ? '#ffffff' : '#64748b',
                cursor: 'pointer',
                fontWeight: 700,
                fontSize: 10,
              }}
              onClick={onToggleFreeCam}
              title="Cam tự do (360 độ)"
            >
              360
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
