// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Top Header Bar (Save Action Modal & Playback Controls)
// =========================================================================================
import React from 'react';
import {
  Video,
  Play,
  Pause,
  Layers,
  Download,
  Share2,
  Eye,
  RotateCcw,
  Film,
  Gauge,
  Sun,
  Moon,
  Save,
} from 'lucide-react';
import { VideoMetadata } from '../../../../types/video_slicer';

export interface VideoSlicerHeaderBarProps {
  videoMetadata: VideoMetadata | null;
  framesCount: number;
  viewMode: 'video' | 'frames';
  onToggleViewMode: (mode: 'video' | 'frames') => void;
  isAnimationPlaying: boolean;
  onTogglePlayAnimation: () => void;
  playbackFps: number;
  onChangePlaybackFps: (fps: number) => void;
  onionSkinMode: 'off' | 'sequential' | 'all';
  onToggleOnionSkin: () => void;
  previewDisplayMode: 'transparent' | 'original';
  onTogglePreviewMode: () => void;
  checkerTheme: 'dark' | 'light';
  onToggleCheckerTheme: () => void;
  onExportGif: () => void;
  onOpenSavePoseModal?: () => void;
  onTransferToAnimSlicer?: () => void;
  onReset: () => void;
}

export const VideoSlicerHeaderBar: React.FC<VideoSlicerHeaderBarProps> = ({
  videoMetadata,
  framesCount,
  viewMode,
  onToggleViewMode,
  isAnimationPlaying,
  onTogglePlayAnimation,
  playbackFps,
  onChangePlaybackFps,
  onionSkinMode,
  onToggleOnionSkin,
  previewDisplayMode,
  onTogglePreviewMode,
  checkerTheme,
  onToggleCheckerTheme,
  onExportGif,
  onOpenSavePoseModal,
  onTransferToAnimSlicer,
  onReset,
}) => {
  return (
    <div
      style={{
        height: 48,
        background: 'rgba(15, 23, 42, 0.85)',
        borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: '8px 8px 0 0',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 12px',
        flexShrink: 0,
        gap: 8,
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* Left: Title & Mode Toggle */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
        <div
          style={{
            width: 28,
            height: 28,
            borderRadius: 6,
            background: 'linear-gradient(135deg, #0284c7, #10b981)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            flexShrink: 0,
          }}
        >
          <Video size={16} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          <div style={{ fontSize: 11.5, fontWeight: 700, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
            1.3 🎥 Tách Frame & Bóc Nền Video
            <span
              style={{
                fontSize: 9,
                padding: '1px 6px',
                borderRadius: 4,
                background: 'rgba(16, 185, 129, 0.15)',
                color: '#34d399',
                border: '1px solid rgba(16, 185, 129, 0.3)',
              }}
            >
              Chroma Key Standard
            </span>
          </div>
          {videoMetadata && (
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              {videoMetadata.name} • {videoMetadata.width}x{videoMetadata.height}px • {videoMetadata.duration.toFixed(1)}s
            </div>
          )}
        </div>

        {/* View Mode Segmented Buttons: Video vs Frames */}
        <div
          style={{
            display: 'flex',
            background: 'rgba(0, 0, 0, 0.4)',
            padding: 2,
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.08)',
            marginLeft: 8,
          }}
        >
          <button
            onClick={() => onToggleViewMode('video')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 8px',
              borderRadius: 4,
              fontSize: 10,
              fontWeight: viewMode === 'video' ? 700 : 500,
              border: 'none',
              background: viewMode === 'video' ? '#0284c7' : 'transparent',
              color: viewMode === 'video' ? '#fff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            <Video size={11} /> Xem Video
          </button>
          <button
            onClick={() => onToggleViewMode('frames')}
            disabled={framesCount === 0}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 8px',
              borderRadius: 4,
              fontSize: 10,
              fontWeight: viewMode === 'frames' ? 700 : 500,
              border: 'none',
              background: viewMode === 'frames' ? '#10b981' : 'transparent',
              color: viewMode === 'frames' ? '#fff' : '#94a3b8',
              cursor: framesCount > 0 ? 'pointer' : 'not-allowed',
              opacity: framesCount > 0 ? 1 : 0.5,
            }}
          >
            <Film size={11} /> Xem Frame ({framesCount})
          </button>
        </div>
      </div>

      {/* Middle Controls: Animation Playback & Speed Controls */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        {viewMode === 'frames' && (
          <>
            {/* Play / Pause Animation Button */}
            <button
              onClick={onTogglePlayAnimation}
              disabled={framesCount === 0}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '5px 12px',
                borderRadius: 6,
                fontSize: 10.5,
                fontWeight: 700,
                border: 'none',
                background: isAnimationPlaying ? '#ef4444' : '#10b981',
                color: '#ffffff',
                cursor: framesCount > 0 ? 'pointer' : 'not-allowed',
                boxShadow: isAnimationPlaying ? '0 0 12px rgba(239, 68, 68, 0.4)' : '0 0 10px rgba(16, 185, 129, 0.3)',
              }}
            >
              {isAnimationPlaying ? <Pause size={13} /> : <Play size={13} />}
              {isAnimationPlaying ? 'Dừng Hoạt Ảnh' : 'Phát Hoạt Ảnh'}
            </button>

            {/* Playback FPS Control / Fast & Slow-Motion */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                background: 'rgba(0, 0, 0, 0.4)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                borderRadius: 6,
                padding: '2px 6px',
              }}
            >
              <Gauge size={12} color="#38bdf8" />
              <span style={{ fontSize: 9.5, color: '#94a3b8' }}>Tốc độ:</span>
              <input
                type="number"
                min={1}
                max={60}
                value={playbackFps}
                onChange={(e) => onChangePlaybackFps(Math.max(1, Math.min(60, Number(e.target.value))))}
                style={{
                  width: 32,
                  background: '#090e1a',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                  borderRadius: 3,
                  color: '#38bdf8',
                  fontSize: 9.5,
                  fontWeight: 700,
                  textAlign: 'center',
                  padding: '1px 2px',
                }}
              />
              <span style={{ fontSize: 9.5, color: '#38bdf8', fontWeight: 700 }}>FPS</span>

              {/* Quick Speed Preset Buttons */}
              <div style={{ display: 'flex', gap: 2, marginLeft: 2 }}>
                <button
                  onClick={() => onChangePlaybackFps(6)}
                  title="Chậm (0.5x - 6 FPS)"
                  style={{
                    background: playbackFps === 6 ? '#0284c7' : 'rgba(255, 255, 255, 0.06)',
                    border: 'none',
                    borderRadius: 3,
                    color: '#fff',
                    fontSize: 8.5,
                    padding: '2px 4px',
                    cursor: 'pointer',
                  }}
                >
                  0.5x
                </button>
                <button
                  onClick={() => onChangePlaybackFps(12)}
                  title="Chuẩn (1.0x - 12 FPS)"
                  style={{
                    background: playbackFps === 12 ? '#0284c7' : 'rgba(255, 255, 255, 0.06)',
                    border: 'none',
                    borderRadius: 3,
                    color: '#fff',
                    fontSize: 8.5,
                    padding: '2px 4px',
                    cursor: 'pointer',
                  }}
                >
                  1.0x
                </button>
                <button
                  onClick={() => onChangePlaybackFps(24)}
                  title="Nhanh (2.0x - 24 FPS)"
                  style={{
                    background: playbackFps === 24 ? '#0284c7' : 'rgba(255, 255, 255, 0.06)',
                    border: 'none',
                    borderRadius: 3,
                    color: '#fff',
                    fontSize: 8.5,
                    padding: '2px 4px',
                    cursor: 'pointer',
                  }}
                >
                  2.0x
                </button>
              </div>
            </div>

            {/* Onion Skin */}
            <button
              onClick={onToggleOnionSkin}
              title="Bật/Tắt Onion Skin"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 8px',
                borderRadius: 6,
                fontSize: 10,
                fontWeight: 600,
                border: '1px solid',
                borderColor: onionSkinMode !== 'off' ? '#a855f7' : 'rgba(255, 255, 255, 0.1)',
                background: onionSkinMode !== 'off' ? 'rgba(168, 85, 247, 0.2)' : 'rgba(255, 255, 255, 0.05)',
                color: onionSkinMode !== 'off' ? '#d8b4fe' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <Layers size={12} />
              Onion Skin: {onionSkinMode === 'sequential' ? 'Khung Kế' : onionSkinMode === 'all' ? 'Tất Cả' : 'Tắt'}
            </button>

            {/* Display Mode Toggle */}
            <button
              onClick={onTogglePreviewMode}
              title="Chuyển đổi hiển thị Tách nền / Video gốc"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 8px',
                borderRadius: 6,
                fontSize: 10,
                fontWeight: 600,
                border: '1px solid rgba(255, 255, 255, 0.1)',
                background: previewDisplayMode === 'transparent' ? 'rgba(16, 185, 129, 0.15)' : 'rgba(255, 255, 255, 0.05)',
                color: previewDisplayMode === 'transparent' ? '#34d399' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <Eye size={12} />
              {previewDisplayMode === 'transparent' ? 'Đã Bóc Nền' : 'Ảnh Gốc'}
            </button>

            {/* Checkerboard Theme Toggle: Light vs Dark */}
            <button
              onClick={onToggleCheckerTheme}
              title={checkerTheme === 'light' ? 'Đổi sang nền Caro Tối' : 'Đổi sang nền Caro Sáng'}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 8px',
                borderRadius: 6,
                fontSize: 10,
                fontWeight: 600,
                border: '1px solid rgba(255, 255, 255, 0.1)',
                background: checkerTheme === 'light' ? 'rgba(255, 255, 255, 0.2)' : 'rgba(15, 23, 42, 0.6)',
                color: checkerTheme === 'light' ? '#fef08a' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              {checkerTheme === 'light' ? <Sun size={12} color="#facc15" /> : <Moon size={12} />}
              Caro: {checkerTheme === 'light' ? 'Sáng' : 'Tối'}
            </button>
          </>
        )}
      </div>

      {/* Right: Save Action, Transfer & Export Actions */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        {/* Save Pose / Action Button (Like Tab 1.2) */}
        {onOpenSavePoseModal && (
          <button
            onClick={onOpenSavePoseModal}
            disabled={framesCount === 0}
            title="Lưu chuỗi frame này thành động tác nhân vật (chuẩn Tab 1.2)"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '5px 10px',
              borderRadius: 6,
              fontSize: 10.5,
              fontWeight: 700,
              border: 'none',
              background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
              color: '#ffffff',
              cursor: framesCount > 0 ? 'pointer' : 'not-allowed',
              opacity: framesCount > 0 ? 1 : 0.5,
              boxShadow: '0 0 10px rgba(56, 189, 248, 0.4)',
            }}
          >
            <Save size={13} /> 💾 Lưu Động Tác
          </button>
        )}

        {onTransferToAnimSlicer && (
          <button
            onClick={onTransferToAnimSlicer}
            disabled={framesCount === 0}
            title="Chuyển toàn bộ frame sang Tab 1.2 Ghép Hoạt Ảnh"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '5px 10px',
              borderRadius: 6,
              fontSize: 10.5,
              fontWeight: 700,
              border: 'none',
              background: 'linear-gradient(135deg, #7c3aed, #a855f7)',
              color: '#ffffff',
              cursor: framesCount > 0 ? 'pointer' : 'not-allowed',
              opacity: framesCount > 0 ? 1 : 0.5,
              boxShadow: '0 0 10px rgba(168, 85, 247, 0.3)',
            }}
          >
            <Share2 size={13} /> Sang Tab 1.2 🎬
          </button>
        )}

        <button
          onClick={onExportGif}
          disabled={framesCount === 0}
          title="Xuất chuỗi frame thành ảnh GIF động chuyển động"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            padding: '5px 10px',
            borderRadius: 6,
            fontSize: 10.5,
            fontWeight: 700,
            border: 'none',
            background: '#10b981',
            color: '#ffffff',
            cursor: framesCount > 0 ? 'pointer' : 'not-allowed',
            opacity: framesCount > 0 ? 1 : 0.5,
            boxShadow: '0 0 10px rgba(16, 185, 129, 0.3)',
          }}
        >
          <Film size={13} /> 🎞️ Xuất Ảnh GIF
        </button>

        <button
          onClick={onReset}
          title="Đặt lại toàn bộ"
          style={{
            padding: '5px 8px',
            borderRadius: 6,
            fontSize: 11,
            border: '1px solid rgba(255, 255, 255, 0.1)',
            background: 'transparent',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <RotateCcw size={13} />
        </button>
      </div>
    </div>
  );
};
