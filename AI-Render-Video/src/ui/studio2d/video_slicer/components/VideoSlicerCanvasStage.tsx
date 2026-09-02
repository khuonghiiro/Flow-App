// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Interactive Canvas Stage (Multi-Mode Orchestrator)
// =========================================================================================
import React, { useState, useRef, useEffect, useMemo } from 'react';
import { Play, Layers, Sparkles, RefreshCw, ZoomIn, ZoomOut, Maximize2 } from 'lucide-react';
import { VideoSliceFrame, VideoMetadata, VideoCropBBox } from '../../../../types/video_slicer';
import { VideoSlicerTimelineRangeBar } from './VideoSlicerTimelineRangeBar';
import { VideoSlicerFrameCard, useStartEndThumbnails } from './VideoSlicerStartEndComparator';
import { VideoSlicerFramesCanvasStage } from './VideoSlicerFramesCanvasStage';

export interface VideoSlicerCanvasStageProps {
  viewMode: 'video' | 'frames';
  videoMetadata: VideoMetadata | null;
  videoElementRef: React.RefObject<HTMLVideoElement | null>;
  videoCurrentTime: number;
  onVideoTimeUpdate: (time: number) => void;
  onUserSeekVideoTime: (time: number) => void;
  startTime: number;
  setStartTime: (time: number) => void;
  endTime: number;
  setEndTime: (time: number) => void;
  isVideoPlaying: boolean;
  onTogglePlayVideo: () => void;
  isLooping: boolean;
  setIsLooping: (loop: boolean) => void;

  // Manual Loop Matcher Props
  isScanByBBox: boolean;
  setIsScanByBBox: (v: boolean) => void;
  maxSearchDuration: number;
  setMaxSearchDuration: (v: number) => void;
  isSearchingEnd: boolean;
  searchProgress: number;
  searchStatusText: string;
  onTriggerSearchEnd: () => void;
  onStopSearch: () => void;

  frames: VideoSliceFrame[];
  setFrames: React.Dispatch<React.SetStateAction<VideoSliceFrame[]>>;
  frameOrder: number[];
  selectedFrameIndex: number | null;
  activePlaybackIndex: number;
  isAnimationPlaying: boolean;
  demoPeeledUrl: string | null;
  onionSkinMode: 'off' | 'sequential' | 'all';
  onToggleOnionSkin: () => void;
  previewDisplayMode: 'transparent' | 'original';
  checkerTheme: 'dark' | 'light';
  setCheckerTheme: React.Dispatch<React.SetStateAction<'dark' | 'light'>>;
  isBBoxCropMode: boolean;
  activeBBox: VideoCropBBox | null;
  onUpdateActiveBBox: (bbox: VideoCropBBox) => void;
  onShowToast?: (text: string, type?: 'success' | 'error' | 'info') => void;
}

export const VideoSlicerCanvasStage: React.FC<VideoSlicerCanvasStageProps> = ({
  viewMode,
  videoMetadata,
  videoElementRef,
  videoCurrentTime,
  onVideoTimeUpdate,
  onUserSeekVideoTime,
  startTime,
  setStartTime,
  endTime,
  setEndTime,
  isVideoPlaying,
  onTogglePlayVideo,
  isLooping,
  setIsLooping,
  isScanByBBox,
  setIsScanByBBox,
  maxSearchDuration,
  setMaxSearchDuration,
  isSearchingEnd,
  searchProgress,
  searchStatusText,
  onTriggerSearchEnd,
  onStopSearch,
  frames,
  setFrames,
  frameOrder,
  selectedFrameIndex,
  activePlaybackIndex,
  isAnimationPlaying,
  demoPeeledUrl,
  onionSkinMode,
  onToggleOnionSkin,
  previewDisplayMode,
  checkerTheme,
  setCheckerTheme,
  isBBoxCropMode,
  activeBBox,
  onUpdateActiveBBox,
  onShowToast,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const mediaContainerRef = useRef<HTMLDivElement>(null);

  const [zoom, setZoom] = useState<number>(1.0);
  const [panOffset, setPanOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState<boolean>(false);
  const [panStart, setPanStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // A/B Blink Test Mode
  const [isBlinking, setIsBlinking] = useState<boolean>(false);
  const [blinkFrame, setBlinkFrame] = useState<'start' | 'end'>('start');

  // Should we show/use BBox?
  const shouldShowBBox = isBBoxCropMode || isScanByBBox;

  // Extract Start & End Frame Thumbnails & Similarity Score
  const { startThumb, endThumb, similarityScore } = useStartEndThumbnails(
    videoMetadata?.dataUrl || '',
    startTime,
    endTime,
    shouldShowBBox ? activeBBox : null
  );

  // BBox dragging & resizing
  const [isDraggingBBox, setIsDraggingBBox] = useState<boolean>(false);
  const [dragHandle, setDragHandle] = useState<'move' | 'nw' | 'ne' | 'se' | 'sw' | null>(null);
  const [dragStartPos, setDragStartPos] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [initialBBox, setInitialBBox] = useState<VideoCropBBox | null>(null);

  // Check if video is Vertical / Portrait (Height >= Width)
  const isPortrait = (videoMetadata?.height || 0) >= (videoMetadata?.width || 0);

  // Zoom handlers for video mode
  const handleZoomIn = () => setZoom((z) => Math.min(z + 0.25, 4.0));
  const handleZoomOut = () => setZoom((z) => Math.max(z - 0.25, 0.25));
  const handleResetZoom = () => {
    setZoom(1.0);
    setPanOffset({ x: 0, y: 0 });
  };

  // Mouse wheel zoom for video mode
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    if (e.deltaY < 0) {
      setZoom((z) => Math.min(z + 0.15, 4.0));
    } else {
      setZoom((z) => Math.max(z - 0.15, 0.25));
    }
  };

  // A/B Blink Timer
  useEffect(() => {
    let timer: any;
    if (isBlinking) {
      timer = setInterval(() => {
        setBlinkFrame((prev) => (prev === 'start' ? 'end' : 'start'));
      }, 160);
    } else {
      setBlinkFrame('start');
    }
    return () => {
      if (timer) clearInterval(timer);
    };
  }, [isBlinking]);

  // BBox Drag Start
  const handleBBoxMouseDown = (
    e: React.MouseEvent,
    handle: 'move' | 'nw' | 'ne' | 'se' | 'sw'
  ) => {
    e.stopPropagation();
    if (!activeBBox) return;
    setIsDraggingBBox(true);
    setDragHandle(handle);
    setDragStartPos({ x: e.clientX, y: e.clientY });
    setInitialBBox({ ...activeBBox });
  };

  // Window mouse listeners for Percentage-Based 4-Corner Dragging
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDraggingBBox || !initialBBox || !dragHandle || !mediaContainerRef.current) return;

      const rect = mediaContainerRef.current.getBoundingClientRect();
      const mediaW = rect.width;
      const mediaH = rect.height;
      if (mediaW <= 0 || mediaH <= 0) return;

      const dxPct = ((e.clientX - dragStartPos.x) / (mediaW * zoom)) * 100;
      const dyPct = ((e.clientY - dragStartPos.y) / (mediaH * zoom)) * 100;

      let x = initialBBox.x;
      let y = initialBBox.y;
      let width = initialBBox.width;
      let height = initialBBox.height;

      if (dragHandle === 'move') {
        x = Math.max(0, Math.min(100 - width, initialBBox.x + dxPct));
        y = Math.max(0, Math.min(100 - height, initialBBox.y + dyPct));
      } else if (dragHandle === 'se') {
        width = Math.max(5, Math.min(100 - x, initialBBox.width + dxPct));
        height = Math.max(5, Math.min(100 - y, initialBBox.height + dyPct));
      } else if (dragHandle === 'sw') {
        const newX = Math.max(0, Math.min(initialBBox.x + initialBBox.width - 5, initialBBox.x + dxPct));
        width = initialBBox.width + (initialBBox.x - newX);
        x = newX;
        height = Math.max(5, Math.min(100 - y, initialBBox.height + dyPct));
      } else if (dragHandle === 'ne') {
        width = Math.max(5, Math.min(100 - x, initialBBox.width + dxPct));
        const newY = Math.max(0, Math.min(initialBBox.y + initialBBox.height - 5, initialBBox.y + dyPct));
        height = initialBBox.height + (initialBBox.y - newY);
        y = newY;
      } else if (dragHandle === 'nw') {
        const newX = Math.max(0, Math.min(initialBBox.x + initialBBox.width - 5, initialBBox.x + dxPct));
        const newY = Math.max(0, Math.min(initialBBox.y + initialBBox.height - 5, initialBBox.y + dyPct));
        width = initialBBox.width + (initialBBox.x - newX);
        height = initialBBox.height + (initialBBox.y - newY);
        x = newX;
        y = newY;
      }

      onUpdateActiveBBox({
        x: Number(x.toFixed(2)),
        y: Number(y.toFixed(2)),
        width: Number(width.toFixed(2)),
        height: Number(height.toFixed(2)),
      });
    };

    const handleMouseUp = () => {
      if (isDraggingBBox) {
        setIsDraggingBBox(false);
        setDragHandle(null);
      }
    };

    if (isDraggingBBox) {
      window.addEventListener('mousemove', handleMouseMove);
      window.addEventListener('mouseup', handleMouseUp);
    }
    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isDraggingBBox, initialBBox, dragHandle, dragStartPos, zoom, onUpdateActiveBBox]);

  // If in frames mode, render dedicated high-performance frames canvas stage
  if (viewMode === 'frames') {
    return (
      <VideoSlicerFramesCanvasStage
        frames={frames}
        setFrames={setFrames}
        selectedFrameIndex={selectedFrameIndex}
        activePlaybackIndex={activePlaybackIndex}
        isAnimationPlaying={isAnimationPlaying}
        demoPeeledUrl={demoPeeledUrl}
        onionSkinMode={onionSkinMode}
        onToggleOnionSkin={onToggleOnionSkin}
        previewDisplayMode={previewDisplayMode}
        checkerTheme={checkerTheme}
        setCheckerTheme={setCheckerTheme}
        isBBoxCropMode={isBBoxCropMode}
        activeBBox={activeBBox}
        onUpdateActiveBBox={onUpdateActiveBBox}
        onShowToast={onShowToast}
      />
    );
  }

  // Otherwise, render Live Video Stage
  return (
    <div
      ref={containerRef}
      onWheel={handleWheel}
      onMouseDown={(e) => {
        if (e.button === 0 && (e.target === containerRef.current || (e.target as HTMLElement).dataset.stageCanvas)) {
          setIsPanning(true);
          setPanStart({ x: e.clientX - panOffset.x, y: e.clientY - panOffset.y });
        }
      }}
      onMouseMove={(e) => {
        if (isPanning) {
          setPanOffset({ x: e.clientX - panStart.x, y: e.clientY - panStart.y });
        }
      }}
      onMouseUp={() => setIsPanning(false)}
      style={{
        flex: '1 1 0%',
        height: '100%',
        minHeight: 0,
        position: 'relative',
        background: '#070a13',
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        userSelect: 'none',
        cursor: isPanning ? 'grabbing' : 'default',
      }}
    >
      {/* ─── FLOATING TOP COMPARISON TOOLBAR ──────────────────── */}
      {videoMetadata && (
        <div
          style={{
            position: 'absolute',
            top: 10,
            left: 10,
            zIndex: 20,
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            background: 'rgba(15, 23, 42, 0.9)',
            backdropFilter: 'blur(8px)',
            border: '1px solid rgba(255, 255, 255, 0.12)',
            borderRadius: 6,
            padding: '3px 8px',
          }}
        >
          {similarityScore !== null && (
            <span
              style={{
                fontSize: 10,
                fontWeight: 800,
                color: similarityScore >= 85 ? '#34d399' : '#fbbf24',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
            >
              <Sparkles size={11} />
              Độ Khớp {shouldShowBBox ? 'BBox' : 'Start/End'}: {similarityScore}%
            </span>
          )}

          <button
            onClick={() => setIsBlinking(!isBlinking)}
            title="Chớp nhấp nháy A/B liên tục để kiểm tra vòng lặp"
            style={{
              background: isBlinking ? '#a855f7' : 'rgba(255, 255, 255, 0.08)',
              border: 'none',
              borderRadius: 4,
              color: '#fff',
              fontSize: 9.5,
              fontWeight: 700,
              padding: '2px 7px',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <RefreshCw size={10} className={isBlinking ? 'spin' : ''} />
            {isBlinking ? 'Dừng Chớp A/B' : 'Chớp A/B (160ms)'}
          </button>
        </div>
      )}

      {/* ─── FLOATING TOP-RIGHT ZOOM TOOLBAR ──────────────────── */}
      <div
        style={{
          position: 'absolute',
          top: 10,
          right: 10,
          zIndex: 20,
          display: 'flex',
          gap: 4,
          background: 'rgba(15, 23, 42, 0.85)',
          backdropFilter: 'blur(8px)',
          border: '1px solid rgba(255, 255, 255, 0.1)',
          borderRadius: 6,
          padding: 3,
        }}
      >
        <button
          onClick={handleZoomIn}
          title="Phóng to"
          style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 4, display: 'flex', borderRadius: 4 }}
        >
          <ZoomIn size={14} />
        </button>
        <span style={{ fontSize: 10, color: '#38bdf8', padding: '0 4px', display: 'flex', alignItems: 'center' }}>
          {Math.round(zoom * 100)}%
        </span>
        <button
          onClick={handleZoomOut}
          title="Thu nhỏ"
          style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 4, display: 'flex', borderRadius: 4 }}
        >
          <ZoomOut size={14} />
        </button>
        <button
          onClick={handleResetZoom}
          title="Vừa khung hình"
          style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 4, display: 'flex', borderRadius: 4 }}
        >
          <Maximize2 size={14} />
        </button>
      </div>

      {/* ─── CENTER VIEWPORT (LIVE VIDEO) ───────── */}
      <div
        data-stage-canvas="true"
        style={{
          flex: 1,
          width: '100%',
          minHeight: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          overflow: 'hidden',
          padding: 10,
          boxSizing: 'border-box',
        }}
      >
        <div
          data-stage-canvas="true"
          style={{
            transform: `translate(${panOffset.x}px, ${panOffset.y}px) scale(${zoom})`,
            transformOrigin: 'center center',
            transition: isPanning ? 'none' : 'transform 0.05s ease-out',
            position: 'relative',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {videoMetadata ? (
            isPortrait ? (
              /* ─── 1A. VERTICAL LAYOUT ─── */
              <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 16 }}>
                <VideoSlicerFrameCard
                  type="start"
                  timestamp={startTime}
                  thumbUrl={startThumb}
                  aspectRatio={videoMetadata.width / videoMetadata.height}
                  onSeek={onUserSeekVideoTime}
                  maxHeight={430}
                  maxWidth={210}
                />

                <div
                  ref={mediaContainerRef}
                  style={{
                    position: 'relative',
                    borderRadius: 8,
                    overflow: 'visible',
                    boxShadow: '0 8px 30px rgba(0, 0, 0, 0.8)',
                    background: '#000',
                    display: 'inline-block',
                    cursor: 'pointer',
                    maxHeight: 430,
                  }}
                  onClick={onTogglePlayVideo}
                >
                  <video
                    ref={videoElementRef as any}
                    src={videoMetadata.dataUrl}
                    playsInline
                    preload="auto"
                    style={{
                      display: 'block',
                      maxWidth: '380px',
                      maxHeight: '430px',
                      objectFit: 'contain',
                      borderRadius: 8,
                    }}
                    onTimeUpdate={(e) => onVideoTimeUpdate(e.currentTarget.currentTime)}
                  />

                  {isBlinking && (
                    <div style={{ position: 'absolute', inset: 0, background: '#000', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 12, pointerEvents: 'none', borderRadius: 8 }}>
                      <img src={(blinkFrame === 'start' ? startThumb : endThumb) || ''} alt="Blink" style={{ width: '100%', height: '100%', objectFit: 'contain' }} />
                    </div>
                  )}

                  {!isVideoPlaying && !isBlinking && (
                    <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(0, 0, 0, 0.3)', pointerEvents: 'none', borderRadius: 8 }}>
                      <div style={{ width: 52, height: 52, borderRadius: '50%', background: 'rgba(2, 132, 199, 0.85)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 0 20px rgba(2, 132, 199, 0.6)' }}>
                        <Play size={24} color="#fff" style={{ marginLeft: 3 }} />
                      </div>
                    </div>
                  )}

                  {shouldShowBBox && activeBBox && !isBlinking && (
                    <div
                      onClick={(e) => e.stopPropagation()}
                      onMouseDown={(e) => handleBBoxMouseDown(e, 'move')}
                      style={{
                        position: 'absolute',
                        left: `${activeBBox.x}%`,
                        top: `${activeBBox.y}%`,
                        width: `${activeBBox.width}%`,
                        height: `${activeBBox.height}%`,
                        border: '2px dashed #f59e0b',
                        background: 'rgba(245, 158, 11, 0.12)',
                        boxSizing: 'border-box',
                        cursor: 'move',
                        zIndex: 25,
                        boxShadow: '0 0 12px rgba(245, 158, 11, 0.4)',
                      }}
                    >
                      <div style={{ position: 'absolute', top: -18, left: 0, background: '#f59e0b', color: '#000', fontSize: 8.5, fontWeight: 800, padding: '1px 5px', borderRadius: 3, pointerEvents: 'none', whiteSpace: 'nowrap' }}>
                        🎯 Khung So Khớp BBox
                      </div>
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'nw')} style={{ position: 'absolute', top: -6, left: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nwse-resize', borderRadius: 2 }} />
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'ne')} style={{ position: 'absolute', top: -6, right: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nesw-resize', borderRadius: 2 }} />
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'se')} style={{ position: 'absolute', bottom: -6, right: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nwse-resize', borderRadius: 2 }} />
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'sw')} style={{ position: 'absolute', bottom: -6, left: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nesw-resize', borderRadius: 2 }} />
                    </div>
                  )}
                </div>

                <VideoSlicerFrameCard
                  type="end"
                  timestamp={endTime}
                  thumbUrl={endThumb}
                  aspectRatio={videoMetadata.width / videoMetadata.height}
                  onSeek={onUserSeekVideoTime}
                  maxHeight={430}
                  maxWidth={210}
                />
              </div>
            ) : (
              /* ─── 1B. HORIZONTAL LAYOUT ─── */
              <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 16 }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <VideoSlicerFrameCard
                    type="start"
                    timestamp={startTime}
                    thumbUrl={startThumb}
                    aspectRatio={videoMetadata.width / videoMetadata.height}
                    onSeek={onUserSeekVideoTime}
                    maxHeight={200}
                    maxWidth={220}
                  />
                  <VideoSlicerFrameCard
                    type="end"
                    timestamp={endTime}
                    thumbUrl={endThumb}
                    aspectRatio={videoMetadata.width / videoMetadata.height}
                    onSeek={onUserSeekVideoTime}
                    maxHeight={200}
                    maxWidth={220}
                  />
                </div>

                <div
                  ref={mediaContainerRef}
                  style={{
                    position: 'relative',
                    borderRadius: 8,
                    overflow: 'visible',
                    boxShadow: '0 8px 30px rgba(0, 0, 0, 0.8)',
                    background: '#000',
                    display: 'inline-block',
                    cursor: 'pointer',
                    maxHeight: 430,
                  }}
                  onClick={onTogglePlayVideo}
                >
                  <video
                    ref={videoElementRef as any}
                    src={videoMetadata.dataUrl}
                    playsInline
                    preload="auto"
                    style={{
                      display: 'block',
                      maxWidth: '640px',
                      maxHeight: '430px',
                      objectFit: 'contain',
                      borderRadius: 8,
                    }}
                    onTimeUpdate={(e) => onVideoTimeUpdate(e.currentTarget.currentTime)}
                  />

                  {isBlinking && (
                    <div style={{ position: 'absolute', inset: 0, background: '#000', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 12, pointerEvents: 'none', borderRadius: 8 }}>
                      <img src={(blinkFrame === 'start' ? startThumb : endThumb) || ''} alt="Blink" style={{ width: '100%', height: '100%', objectFit: 'contain' }} />
                    </div>
                  )}

                  {!isVideoPlaying && !isBlinking && (
                    <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(0, 0, 0, 0.3)', pointerEvents: 'none', borderRadius: 8 }}>
                      <div style={{ width: 52, height: 52, borderRadius: '50%', background: 'rgba(2, 132, 199, 0.85)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 0 20px rgba(2, 132, 199, 0.6)' }}>
                        <Play size={24} color="#fff" style={{ marginLeft: 3 }} />
                      </div>
                    </div>
                  )}

                  {shouldShowBBox && activeBBox && !isBlinking && (
                    <div
                      onClick={(e) => e.stopPropagation()}
                      onMouseDown={(e) => handleBBoxMouseDown(e, 'move')}
                      style={{
                        position: 'absolute',
                        left: `${activeBBox.x}%`,
                        top: `${activeBBox.y}%`,
                        width: `${activeBBox.width}%`,
                        height: `${activeBBox.height}%`,
                        border: '2px dashed #f59e0b',
                        background: 'rgba(245, 158, 11, 0.12)',
                        boxSizing: 'border-box',
                        cursor: 'move',
                        zIndex: 25,
                        boxShadow: '0 0 12px rgba(245, 158, 11, 0.4)',
                      }}
                    >
                      <div style={{ position: 'absolute', top: -18, left: 0, background: '#f59e0b', color: '#000', fontSize: 8.5, fontWeight: 800, padding: '1px 5px', borderRadius: 3, pointerEvents: 'none', whiteSpace: 'nowrap' }}>
                        🎯 Khung So Khớp BBox
                      </div>
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'nw')} style={{ position: 'absolute', top: -6, left: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nwse-resize', borderRadius: 2 }} />
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'ne')} style={{ position: 'absolute', top: -6, right: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nesw-resize', borderRadius: 2 }} />
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'se')} style={{ position: 'absolute', bottom: -6, right: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nwse-resize', borderRadius: 2 }} />
                      <div onMouseDown={(e) => handleBBoxMouseDown(e, 'sw')} style={{ position: 'absolute', bottom: -6, left: -6, width: 12, height: 12, background: '#fbbf24', border: '2px solid #000', cursor: 'nesw-resize', borderRadius: 2 }} />
                    </div>
                  )}
                </div>
              </div>
            )
          ) : (
            <div style={{ textAlign: 'center', color: '#64748b' }}>
              <Layers size={40} style={{ margin: '0 auto 10px', opacity: 0.4 }} />
              <div style={{ fontSize: 13, fontWeight: 600 }}>Chưa nạp video hoạt ảnh</div>
              <div style={{ fontSize: 11, marginTop: 4 }}>
                Tải lên video bên cột trái để bắt đầu cắt khung và bóc nền
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ─── BOTTOM 58PX TALL 3-PIN TIMELINE BAR ──────────────── */}
      {videoMetadata && (
        <VideoSlicerTimelineRangeBar
          videoMetadata={videoMetadata}
          videoCurrentTime={videoCurrentTime}
          onUserSeekVideoTime={onUserSeekVideoTime}
          startTime={startTime}
          setStartTime={setStartTime}
          endTime={endTime}
          setEndTime={setEndTime}
          isVideoPlaying={isVideoPlaying}
          onTogglePlayVideo={onTogglePlayVideo}
          isLooping={isLooping}
          setIsLooping={setIsLooping}
          isScanByBBox={isScanByBBox}
          setIsScanByBBox={setIsScanByBBox}
          maxSearchDuration={maxSearchDuration}
          setMaxSearchDuration={setMaxSearchDuration}
          isSearchingEnd={isSearchingEnd}
          searchProgress={searchProgress}
          searchStatusText={searchStatusText}
          onTriggerSearchEnd={onTriggerSearchEnd}
          onStopSearch={onStopSearch}
        />
      )}
    </div>
  );
};
