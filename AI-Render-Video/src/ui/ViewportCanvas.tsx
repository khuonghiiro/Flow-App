import React, { useRef, useEffect } from 'react';
import { Camera, Eye, Zap, Layers } from 'lucide-react';
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
  onToggleCC: () => void;
}

export const ViewportCanvas: React.FC<ViewportCanvasProps> = ({
  renderer,
  fps,
  activeSubtitle,
  subtitlesConfig,
  showCC,
  onToggleCC,
}) => {
  const mountRef = useRef<HTMLDivElement>(null);

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

      {/* Top HUD */}
      <div className="viewport-hud">
        <div style={{ display: 'flex', gap: 8 }}>
          <div className="hud-pill fps-counter">
            <Zap size={13} /> {fps} FPS
          </div>
          <div className="hud-pill">
            <Camera size={13} /> Live GPU Viewport
          </div>
        </div>

        <div style={{ display: 'flex', gap: 8 }}>
          <button
            className={`btn-secondary ${showCC ? 'active' : ''}`}
            style={{
              borderRadius: 20,
              padding: '4px 12px',
              backgroundColor: showCC ? 'rgba(99, 102, 241, 0.25)' : undefined,
              borderColor: showCC ? '#818cf8' : undefined,
            }}
            onClick={onToggleCC}
          >
            <strong>[CC]</strong> Phụ đề {showCC ? 'BẬT' : 'TẮT'}
          </button>
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
