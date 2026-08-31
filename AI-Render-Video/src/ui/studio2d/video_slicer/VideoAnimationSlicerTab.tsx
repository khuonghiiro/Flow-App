// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Tab 1.3: Video Animation Slicer & Chroma Peeling Studio (FFmpeg 8.0.1 Engine)
// =========================================================================================
import React, { useState, useEffect, useCallback, useRef } from 'react';
import { VideoSliceFrame } from '../../../types/video_slicer';
import { useVideoExtractor } from './hooks/useVideoExtractor';
import { useVideoChromaPeeling } from './hooks/useVideoChromaPeeling';
import { useVideoBBoxCrop } from './hooks/useVideoBBoxCrop';
import { useVideoLoopMatcher } from './hooks/useVideoLoopMatcher';
import { VideoSlicerHeaderBar } from './components/VideoSlicerHeaderBar';
import { VideoSlicerSidebar } from './components/VideoSlicerSidebar';
import { VideoSlicerCanvasStage } from './components/VideoSlicerCanvasStage';
import { VideoSlicerPropertiesPanel } from './components/VideoSlicerPropertiesPanel';
import { VideoSlicerFilmstripBar } from './components/VideoSlicerFilmstripBar';

export interface VideoAnimationSlicerTabProps {
  onTransferToAnimSlicer?: (data: { frames: string[] }) => void;
  externalVideoUrl?: string | null;
}

export const VideoAnimationSlicerTab: React.FC<VideoAnimationSlicerTabProps> = ({
  onTransferToAnimSlicer,
  externalVideoUrl,
}) => {
  // 1. Video Extractor & Live Continuous Video Player Hook
  const {
    videoMetadata,
    frames,
    setFrames,
    frameOrder,
    setFrameOrder,
    selectedFrameIndex,
    setSelectedFrameIndex,
    viewMode,
    setViewMode,
    videoCurrentTime,
    setVideoCurrentTime,
    isVideoPlaying,
    isLooping,
    setIsLooping,
    videoElementRef,
    handleVideoTimeUpdate,
    handleUserSeekTime,
    handleTogglePlayVideo,
    targetFps,
    setTargetFps,
    startTime,
    setStartTime,
    endTime,
    setEndTime,
    maxFrames,
    setMaxFrames,
    estimatedTotalFrames,
    videoCropBBox,
    setVideoCropBBox,
    isExtracting,
    extractProgress,
    extractStatusText,
    videoFileInputRef,
    handleLoadVideoFile,
    handleExtractFrames,
  } = useVideoExtractor();

  // 2. Chroma Key Background Peeling Hook (Tab 1 Standard Engine with Single-Frame Tuning)
  const {
    keyColorType,
    setKeyColorType,
    keyColorHex,
    setKeyColorHex,
    isolationMode,
    setIsolationMode,
    tolerance,
    setTolerance,
    feather,
    setFeather,
    shadowRetention,
    setShadowRetention,
    despeckleSize,
    setDespeckleSize,
    defringeStrength,
    setDefringeStrength,
    demoPeeledUrl,
    isApplyingAll,
    isApplyingSingle,
    peelStatusText,
    handleApplyChromaToSingleFrame,
    handleApplyChromaToAllFrames,
    isEyedropperActive,
    handleTriggerEyedropper,
  } = useVideoChromaPeeling({
    frames,
    setFrames,
    selectedFrameIndex,
  });

  // 3. BBox Video Cropping Hook
  const {
    isBBoxCropMode,
    setIsBBoxCropMode,
    handleAutoTrimAllFramesBBox,
    handleApplyCropBoxToAllFrames,
  } = useVideoBBoxCrop({
    frames,
    setFrames,
  });

  // 4. Auto Loop Frame Matcher Hook (Customizable Duration Search)
  const {
    isAutoFindEnd,
    setIsAutoFindEnd,
    maxSearchDuration,
    setMaxSearchDuration,
    isSearchingEnd,
    searchProgress,
    searchStatusText,
    handleTriggerSearchEnd,
    handleStopSearch,
    handleStartPinReleased,
  } = useVideoLoopMatcher();

  // 5. Pipeline All-in-One Mode States
  const [isPipelineMode, setIsPipelineMode] = useState<boolean>(false);
  const [pipelineIncludeExtract, setPipelineIncludeExtract] = useState<boolean>(true);
  const [pipelineIncludeBBox, setPipelineIncludeBBox] = useState<boolean>(true);
  const [pipelineIncludeChroma, setPipelineIncludeChroma] = useState<boolean>(true);
  const [isPipelineRunning, setIsPipelineRunning] = useState<boolean>(false);
  const [pipelineStatusText, setPipelineStatusText] = useState<string>('');

  // 6. Animation Playback & Display States (Playback FPS / Checkerboard Theme)
  const [isAnimationPlaying, setIsAnimationPlaying] = useState<boolean>(false);
  const [playbackFps, setPlaybackFps] = useState<number>(12);
  const [activePlaybackIndex, setActivePlaybackIndex] = useState<number>(0);
  const [onionSkinMode, setOnionSkinMode] = useState<'off' | 'sequential' | 'all'>('off');
  const [previewDisplayMode, setPreviewDisplayMode] = useState<'transparent' | 'original'>('original');
  const [checkerTheme, setCheckerTheme] = useState<'dark' | 'light'>('dark');
  const animPlaybackTimerRef = useRef<any>(null);

  // Handle Animation Playback Loop: strictly adheres to each frame's own durationMs (Column 3)
  useEffect(() => {
    if (isAnimationPlaying && frameOrder.length > 0) {
      const currentFrame = frames[frameOrder[activePlaybackIndex]];
      const baseDuration = currentFrame?.durationMs || Math.round(1000 / targetFps);
      const speedScale = playbackFps > 0 ? 12 / playbackFps : 1;
      const duration = Math.max(16, Math.round(baseDuration * speedScale));

      animPlaybackTimerRef.current = setTimeout(() => {
        setActivePlaybackIndex((prev) => (prev + 1) % frameOrder.length);
      }, duration);
    }

    return () => {
      if (animPlaybackTimerRef.current) {
        clearTimeout(animPlaybackTimerRef.current);
      }
    };
  }, [isAnimationPlaying, activePlaybackIndex, frameOrder, frames, targetFps, playbackFps]);

  /**
   * Triggers manual or auto search for best matching end frame
   */
  const handlePerformSearchEnd = useCallback(() => {
    if (!videoMetadata) return;
    handleTriggerSearchEnd(
      videoMetadata.dataUrl,
      startTime,
      videoMetadata.duration,
      videoCropBBox,
      (foundEnd) => setEndTime(foundEnd),
      maxSearchDuration
    );
  }, [videoMetadata, startTime, videoCropBBox, handleTriggerSearchEnd, setEndTime, maxSearchDuration]);

  const handleStartPinReleaseAction = useCallback(
    (newStartTime: number) => {
      if (!videoMetadata) return;
      handleStartPinReleased(
        videoMetadata.dataUrl,
        newStartTime,
        videoMetadata.duration,
        videoCropBBox,
        (foundEnd) => setEndTime(foundEnd)
      );
    },
    [videoMetadata, videoCropBBox, handleStartPinReleased, setEndTime]
  );

  /**
   * Individual Action: Apply Extract Original Video Frames ONLY (Does NOT peel background)
   */
  const handleApplyExtractOnly = useCallback(async () => {
    setPreviewDisplayMode('original');
    await handleExtractFrames(null);
  }, [handleExtractFrames]);

  /**
   * Individual Action: Apply Chroma Peeling to Single Selected Frame
   */
  const handleApplyChromaSingle = useCallback(async () => {
    await handleApplyChromaToSingleFrame();
    setPreviewDisplayMode('transparent');
  }, [handleApplyChromaToSingleFrame]);

  /**
   * Individual Action: Apply Chroma Peeling to ALL frames
   */
  const handleApplyChromaAll = useCallback(async () => {
    await handleApplyChromaToAllFrames();
    setPreviewDisplayMode('transparent');
  }, [handleApplyChromaToAllFrames]);

  /**
   * Executes the All-in-One Sequential Pipeline (Cắt + BBox + Bóc Nền 1 chạm)
   */
  const handleRunAllInOnePipeline = useCallback(async () => {
    if (!videoMetadata) return;

    setIsPipelineRunning(true);
    setPipelineStatusText('Đang khởi chạy quy trình liên hoàn...');

    try {
      // Step 1: Extract frames (crop with BBox if pipelineIncludeBBox is active)
      let currentExtractedFrames: VideoSliceFrame[] = [];
      if (pipelineIncludeExtract) {
        setPipelineStatusText('Bước 1/3: Đang trích xuất frame từ video...');
        const cropTarget = pipelineIncludeBBox && videoCropBBox ? videoCropBBox : null;
        currentExtractedFrames = await handleExtractFrames(cropTarget);
      } else {
        currentExtractedFrames = [...frames];
      }

      if (currentExtractedFrames.length === 0) {
        throw new Error('Chưa có frame nào được trích xuất');
      }

      // Step 2: Crop to BBox if not already cropped during extraction
      if (pipelineIncludeBBox && videoCropBBox && !pipelineIncludeExtract) {
        setPipelineStatusText('Bước 2/3: Đang cắt khung BBox cho các frame...');
        await handleApplyCropBoxToAllFrames(videoCropBBox);
      }

      // Step 3: Chroma Key background peeling
      if (pipelineIncludeChroma) {
        setPipelineStatusText('Bước 3/3: Đang bóc nền trong suốt...');
        await handleApplyChromaToAllFrames();
        setPreviewDisplayMode('transparent');
      } else {
        setPreviewDisplayMode('original');
      }

      setViewMode('frames');
      setPipelineStatusText('✓ Hoàn tất xử lý đồng thời tất cả các bước!');
      setTimeout(() => setPipelineStatusText(''), 3000);
    } catch (err: any) {
      alert(`Lỗi trong quy trình xử lý: ${err.message}`);
    } finally {
      setIsPipelineRunning(false);
    }
  }, [
    videoMetadata,
    pipelineIncludeExtract,
    pipelineIncludeBBox,
    pipelineIncludeChroma,
    videoCropBBox,
    frames,
    handleExtractFrames,
    handleApplyCropBoxToAllFrames,
    handleApplyChromaToAllFrames,
    setViewMode,
  ]);

  // Frame Transform / Property update
  const handleUpdateFrameTransform = useCallback(
    (
      index: number,
      updates: Partial<Pick<VideoSliceFrame, 'offsetX' | 'offsetY' | 'scale' | 'rotation' | 'flipX' | 'durationMs'>>
    ) => {
      setFrames((prev) =>
        prev.map((f, idx) => (idx === index ? { ...f, ...updates } : f))
      );
    },
    [setFrames]
  );

  // Apply Transform of one frame to ALL frames
  const handleApplyTransformToAll = useCallback(
    (sourceIndex: number) => {
      const src = frames[sourceIndex];
      if (!src) return;
      setFrames((prev) =>
        prev.map((f) => ({
          ...f,
          offsetX: src.offsetX,
          offsetY: src.offsetY,
          scale: src.scale,
          rotation: src.rotation,
          flipX: src.flipX,
          durationMs: src.durationMs,
        }))
      );
    },
    [frames, setFrames]
  );

  // Set uniform duration for all frames
  const handleSetAllDuration = useCallback(
    (durationMs: number) => {
      setFrames((prev) => prev.map((f) => ({ ...f, durationMs })));
    },
    [setFrames]
  );

  // Duplicate frame
  const handleDuplicateFrame = useCallback(
    (index: number) => {
      const target = frames[index];
      if (!target) return;
      const duplicated: VideoSliceFrame = {
        ...target,
        id: `dup_${target.id}_${Date.now()}`,
        index: frames.length,
      };
      setFrames((prev) => [...prev, duplicated]);
      setFrameOrder((prev) => [...prev, frames.length]);
    },
    [frames, setFrames, setFrameOrder]
  );

  // Delete frame
  const handleDeleteFrame = useCallback(
    (index: number) => {
      setFrames((prev) => prev.filter((_, idx) => idx !== index));
      setFrameOrder((prev) => {
        const nextOrder = prev
          .filter((idx) => idx !== index)
          .map((idx) => (idx > index ? idx - 1 : idx));
        return nextOrder;
      });
      setSelectedFrameIndex((prev) =>
        prev !== null && prev >= frames.length - 1 ? Math.max(0, frames.length - 2) : prev
      );
    },
    [frames.length, setFrames, setFrameOrder, setSelectedFrameIndex]
  );

  // Reorder frames in sequence
  const handleMoveFrame = useCallback(
    (fromIdx: number, toIdx: number) => {
      if (fromIdx < 0 || toIdx < 0 || fromIdx >= frameOrder.length || toIdx >= frameOrder.length) return;
      const newOrder = [...frameOrder];
      const [moved] = newOrder.splice(fromIdx, 1);
      newOrder.splice(toIdx, 0, moved);
      setFrameOrder(newOrder);
    },
    [frameOrder, setFrameOrder]
  );

  // Export Spritesheet PNG
  const handleExportSpriteSheet = useCallback(async () => {
    if (frames.length === 0) return;

    const cols = Math.min(frames.length, 6);
    const rows = Math.ceil(frames.length / cols);

    const loadedImgs: HTMLImageElement[] = await Promise.all(
      frames.map(async (f) => {
        const img = new Image();
        img.crossOrigin = 'anonymous';
        await new Promise<void>((res) => {
          img.onload = () => res();
          img.onerror = () => res();
          img.src = previewDisplayMode === 'transparent' ? f.transparentDataUrl : f.originalDataUrl;
        });
        return img;
      })
    );

    const cellW = loadedImgs[0]?.naturalWidth || 200;
    const cellH = loadedImgs[0]?.naturalHeight || 200;

    const sheetCanvas = document.createElement('canvas');
    sheetCanvas.width = cols * cellW;
    sheetCanvas.height = rows * cellH;
    const ctx = sheetCanvas.getContext('2d');
    if (!ctx) return;

    frames.forEach((_, idx) => {
      const img = loadedImgs[idx];
      if (!img) return;
      const col = idx % cols;
      const row = Math.floor(idx / cols);
      ctx.drawImage(img, col * cellW, row * cellH, cellW, cellH);
    });

    const link = document.createElement('a');
    link.download = `video_spritesheet_${Date.now()}.png`;
    link.href = sheetCanvas.toDataURL('image/png');
    link.click();
  }, [frames, previewDisplayMode]);

  // Reset workspace
  const handleReset = useCallback(() => {
    setFrames([]);
    setFrameOrder([]);
    setSelectedFrameIndex(null);
    setIsAnimationPlaying(false);
    setViewMode('video');
  }, [setFrames, setFrameOrder, setSelectedFrameIndex, setViewMode]);

  const selectedFrame = selectedFrameIndex !== null ? frames[selectedFrameIndex] : null;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
        height: '100%',
        minHeight: 0,
        background: '#0b0f19',
        overflow: 'hidden',
        boxSizing: 'border-box',
      }}
    >
      {/* ─── TOP HEADER BAR ────────────────────────────────────── */}
      <VideoSlicerHeaderBar
        videoMetadata={videoMetadata}
        framesCount={frames.length}
        viewMode={viewMode}
        onToggleViewMode={setViewMode}
        isAnimationPlaying={isAnimationPlaying}
        onTogglePlayAnimation={() => setIsAnimationPlaying(!isAnimationPlaying)}
        playbackFps={playbackFps}
        onChangePlaybackFps={setPlaybackFps}
        onionSkinMode={onionSkinMode}
        onToggleOnionSkin={() => {
          setOnionSkinMode((prev) => (prev === 'off' ? 'sequential' : prev === 'sequential' ? 'all' : 'off'));
        }}
        previewDisplayMode={previewDisplayMode}
        onTogglePreviewMode={() => {
          setPreviewDisplayMode((prev) => (prev === 'transparent' ? 'original' : 'transparent'));
        }}
        checkerTheme={checkerTheme}
        onToggleCheckerTheme={() => {
          setCheckerTheme((prev) => (prev === 'dark' ? 'light' : 'dark'));
        }}
        onExportSpriteSheet={handleExportSpriteSheet}
        onTransferToAnimSlicer={
          onTransferToAnimSlicer && frames.length > 0
            ? () => {
                const urls = frames.map((f) =>
                  previewDisplayMode === 'transparent' ? f.transparentDataUrl : f.originalDataUrl
                );
                onTransferToAnimSlicer({ frames: urls });
              }
            : undefined
        }
        onReset={handleReset}
      />

      {/* ─── MAIN WORKSPACE (LEFT SIDEBAR + CENTER CANVAS + RIGHT PROPERTIES) ── */}
      <div
        style={{
          flex: '1 1 0%',
          display: 'grid',
          gridTemplateColumns: '320px minmax(0, 1fr) 280px',
          minHeight: 0,
          gap: 6,
          padding: '6px 0',
          overflow: 'hidden',
        }}
      >
        {/* Column 1: Left Controls */}
        <VideoSlicerSidebar
          videoMetadata={videoMetadata}
          videoFileInputRef={videoFileInputRef}
          onSelectVideoFile={handleLoadVideoFile}
          isPipelineMode={isPipelineMode}
          setIsPipelineMode={setIsPipelineMode}
          pipelineIncludeExtract={pipelineIncludeExtract}
          setPipelineIncludeExtract={setPipelineIncludeExtract}
          pipelineIncludeBBox={pipelineIncludeBBox}
          setPipelineIncludeBBox={setPipelineIncludeBBox}
          pipelineIncludeChroma={pipelineIncludeChroma}
          setPipelineIncludeChroma={setPipelineIncludeChroma}
          isPipelineRunning={isPipelineRunning}
          pipelineStatusText={pipelineStatusText}
          onRunAllInOnePipeline={handleRunAllInOnePipeline}
          startTime={startTime}
          setStartTime={setStartTime}
          endTime={endTime}
          setEndTime={setEndTime}
          targetFps={targetFps}
          setTargetFps={setTargetFps}
          maxFrames={maxFrames}
          setMaxFrames={setMaxFrames}
          estimatedTotalFrames={estimatedTotalFrames}
          isExtracting={isExtracting}
          extractProgress={extractProgress}
          extractStatusText={extractStatusText}
          onApplyExtractOnly={handleApplyExtractOnly}
          framesCount={frames.length}
          keyColorType={keyColorType}
          setKeyColorType={setKeyColorType}
          keyColorHex={keyColorHex}
          setKeyColorHex={setKeyColorHex}
          isolationMode={isolationMode}
          setIsolationMode={setIsolationMode}
          tolerance={tolerance}
          setTolerance={setTolerance}
          feather={feather}
          setFeather={setFeather}
          shadowRetention={shadowRetention}
          setShadowRetention={setShadowRetention}
          despeckleSize={despeckleSize}
          setDespeckleSize={setDespeckleSize}
          defringeStrength={defringeStrength}
          setDefringeStrength={setDefringeStrength}
          isApplyingAll={isApplyingAll}
          isApplyingSingle={isApplyingSingle}
          peelStatusText={peelStatusText}
          onApplyChromaSingleFrame={handleApplyChromaSingle}
          onApplyChromaOnly={handleApplyChromaAll}
          onTriggerEyedropper={handleTriggerEyedropper}
          isEyedropperActive={isEyedropperActive}
          isBBoxCropMode={isBBoxCropMode}
          setIsBBoxCropMode={setIsBBoxCropMode}
          onApplyBBoxCropOnly={() => {
            if (videoCropBBox) handleApplyCropBoxToAllFrames(videoCropBBox);
          }}
          onAutoTrimAllBBox={handleAutoTrimAllFramesBBox}
        />

        {/* Column 2: Center Interactive Viewport with 58px 3-Pin Timeline Bar */}
        <VideoSlicerCanvasStage
          viewMode={viewMode}
          videoMetadata={videoMetadata}
          videoElementRef={videoElementRef}
          videoCurrentTime={videoCurrentTime}
          onVideoTimeUpdate={handleVideoTimeUpdate}
          onUserSeekVideoTime={handleUserSeekTime}
          startTime={startTime}
          setStartTime={setStartTime}
          endTime={endTime}
          setEndTime={setEndTime}
          isVideoPlaying={isVideoPlaying}
          onTogglePlayVideo={handleTogglePlayVideo}
          isLooping={isLooping}
          setIsLooping={setIsLooping}
          isAutoFindEnd={isAutoFindEnd}
          setIsAutoFindEnd={setIsAutoFindEnd}
          maxSearchDuration={maxSearchDuration}
          setMaxSearchDuration={setMaxSearchDuration}
          isSearchingEnd={isSearchingEnd}
          searchProgress={searchProgress}
          searchStatusText={searchStatusText}
          onTriggerSearchEnd={handlePerformSearchEnd}
          onStopSearch={handleStopSearch}
          onStartPinReleased={handleStartPinReleaseAction}
          frames={frames}
          frameOrder={frameOrder}
          selectedFrameIndex={selectedFrameIndex}
          activePlaybackIndex={activePlaybackIndex}
          isAnimationPlaying={isAnimationPlaying}
          demoPeeledUrl={demoPeeledUrl}
          onionSkinMode={onionSkinMode}
          previewDisplayMode={previewDisplayMode}
          checkerTheme={checkerTheme}
          isBBoxCropMode={isBBoxCropMode}
          activeBBox={videoCropBBox}
          onUpdateActiveBBox={setVideoCropBBox}
        />

        {/* Column 3: Right Properties Panel */}
        <VideoSlicerPropertiesPanel
          selectedFrame={selectedFrame}
          selectedFrameIndex={selectedFrameIndex}
          totalFramesCount={frames.length}
          onUpdateFrameTransform={handleUpdateFrameTransform}
          onApplyTransformToAll={handleApplyTransformToAll}
          onSetAllDuration={handleSetAllDuration}
          onDuplicateFrame={handleDuplicateFrame}
          onDeleteFrame={handleDeleteFrame}
        />
      </div>

      {/* ─── BOTTOM FILMSTRIP SEQUENCE BAR ────────────────────── */}
      <VideoSlicerFilmstripBar
        frames={frames}
        frameOrder={frameOrder}
        selectedFrameIndex={selectedFrameIndex}
        activePlaybackIndex={activePlaybackIndex}
        isPlaying={isAnimationPlaying}
        previewDisplayMode={previewDisplayMode}
        onSelectFrameIndex={(idx) => {
          setSelectedFrameIndex(idx);
          setIsAnimationPlaying(false);
          setViewMode('frames');
        }}
        onMoveFrame={handleMoveFrame}
        onDuplicateFrame={handleDuplicateFrame}
        onDeleteFrame={handleDeleteFrame}
      />
    </div>
  );
};
