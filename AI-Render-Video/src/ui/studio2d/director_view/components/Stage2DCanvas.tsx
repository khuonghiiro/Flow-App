// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useRef, useEffect, useCallback, useState } from 'react';
import { Stage2DCanvasProps, easeInOutCubic, Particle, ActiveBBoxInfo } from './stage_canvas/stage2dTypes';
import { useStageKeyboardShortcuts } from './stage_canvas/useStageKeyboardShortcuts';
import { Stage2DCanvasToolbar } from './stage_canvas/Stage2DCanvasToolbar';
import {
  renderBackgroundLayers,
  renderGroundGrid,
  renderStageEntities,
  renderParticles,
  render16to9CameraFrame,
} from './stage_canvas/stage2dCanvasRenderer';

export const Stage2DCanvas: React.FC<Stage2DCanvasProps> = ({
  project,
  activeShot,
  shotProgress,
  currentTime,
  isPlaying,
  selectedActorId,
  selectedPartId,
  selectedPropId,
  onSelectActor,
  onSelectPart,
  onSelectProp,
  onUpdateCameraAngle,
  onUpdateActorPosition,
  onUpdateActorScale,
  onUpdateActorRotation,
  onUpdateActorFacingAngle,
  onUpdateActorFlipX,
  onUpdateActorZIndex,
  onUpdatePropPosition,
  onUpdatePropScale,
  onUpdatePropRotation,
  onUpdatePropFlipX,
  onUpdatePropZIndex,
  onUpdateCameraFrame,
  isCameraSelected,
  onSelectCamera,
  showTrajectoryLine = true,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());
  const particlesRef = useRef<Particle[]>([]);

  // Interactive Viewport Tool Mode
  const [activeTool, setActiveTool] = useState<'hand' | 'orbit360'>('hand');
  const [showCameraFrame, setShowCameraFrame] = useState<boolean>(true);
  const [viewportZoom, setViewportZoom] = useState<number>(1.0);
  const [viewportPan, setViewportPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // Exact screen-space bounding box handles
  const activeBBoxRef = useRef<ActiveBBoxInfo | null>(null);

  const isDraggingRef = useRef(false);
  const dragModeRef = useRef<
    | 'orbit'
    | 'pan'
    | 'move_actor'
    | 'move_prop'
    | 'scale_actor'
    | 'scale_prop'
    | 'scale_camera'
    | 'rotate_actor'
    | 'rotate_prop'
  >('pan');

  const dragStartRef = useRef<{
    x: number;
    y: number;
    startAngle: number;
    startPitch: number;
    startPanX: number;
    startPanY: number;
    itemStartX: number;
    itemStartY: number;
    itemInitScale: number;
    itemInitDist: number;
    itemInitRotation: number;
  }>({
    x: 0,
    y: 0,
    startAngle: 0,
    startPitch: 0,
    startPanX: 0,
    startPanY: 0,
    itemStartX: 0,
    itemStartY: 0,
    itemInitScale: 1.0,
    itemInitDist: 100,
    itemInitRotation: 0,
  });

  // Custom hook for facing angle numbers 0..9, FlipX, Shift +/- scale, rotation [ / ], and Z-Index
  const { zToast } = useStageKeyboardShortcuts({
    project,
    activeShot,
    selectedActorId,
    selectedPropId,
    onUpdateActorScale,
    onUpdateActorRotation,
    onUpdateActorFacingAngle,
    onUpdateActorFlipX,
    onUpdatePropScale,
    onUpdatePropRotation,
    onUpdatePropFlipX,
    onUpdateActorZIndex,
    onUpdatePropZIndex,
    onUpdateCameraFrame,
  });

  // Initialize Atmospheric Particle System
  useEffect(() => {
    const arr: Particle[] = [];
    for (let i = 0; i < 24; i++) {
      arr.push({
        x: Math.random() * 960,
        y: Math.random() * 540,
        vx: -0.4 - Math.random() * 0.8,
        vy: 0.3 + Math.random() * 0.7,
        size: 3 + Math.random() * 6,
        rot: Math.random() * Math.PI * 2,
        vRot: (Math.random() - 0.5) * 0.04,
        opacity: 0.3 + Math.random() * 0.5,
        type: i % 3 === 0 ? 'leaf' : 'sparkle',
      });
    }
    particlesRef.current = arr;
  }, []);

  const cam = activeShot.camera;
  const currentCamAngle = cam.angleStart + (cam.angleEnd - cam.angleStart) * shotProgress;
  const currentPitch = (cam.pitchStart ?? 0) + ((cam.pitchEnd ?? 0) - (cam.pitchStart ?? 0)) * shotProgress;

  // Force canvas repaint on async image decode
  const [, setForceRepaint] = useState(0);

  // Preload Images Cache with automatic SVG debug text sanitizer and repaint trigger
  const getImage = useCallback((url: string): HTMLImageElement | null => {
    if (!url) return null;
    let cleanUrl = url;
    if (cleanUrl.startsWith('data:image/svg+xml') && (cleanUrl.includes('%3Ctext') || cleanUrl.includes('<text'))) {
      try {
        let decoded = decodeURIComponent(cleanUrl.replace(/^data:image\/svg\+xml;utf8,/, ''));
        decoded = decoded.replace(/<rect[^>]*width="(?:80|110|240|400)"[^>]*\/>/gi, '');
        decoded = decoded.replace(/<text[^>]*>.*?<\/text>/gi, '');
        cleanUrl = `data:image/svg+xml;utf8,${encodeURIComponent(decoded)}`;
      } catch {}
    }
    if (imageCacheRef.current.has(cleanUrl)) {
      return imageCacheRef.current.get(cleanUrl)!;
    }
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      setForceRepaint((c) => (c + 1) % 100000);
    };
    img.src = cleanUrl;
    imageCacheRef.current.set(cleanUrl, img);
    return img;
  }, []);

  // Eagerly preload all character, prop, and background sprites on mount/project change
  useEffect(() => {
    project.actors.forEach((actor) => {
      if (actor.sprites) {
        Object.values(actor.sprites).forEach((url) => {
          if (url) getImage(url);
        });
      }
      if (actor.topDownSprites) {
        Object.values(actor.topDownSprites).forEach((url) => {
          if (url) getImage(url);
        });
      }
    });
    (project.props || []).forEach((prop) => {
      if (prop.url) getImage(prop.url);
      if (prop.growthSprites) {
        Object.values(prop.growthSprites).forEach((url) => {
          if (url) getImage(url);
        });
      }
    });
    if (project.backgroundPreset?.layers) {
      project.backgroundPreset.layers.forEach((l) => {
        if (l.imageUrl) getImage(l.imageUrl);
      });
    }
  }, [project, getImage]);

  // ─── MAIN CANVAS RENDER LOOP ──────────────────────────────────────────
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;
    const rawP = shotProgress;
    const smoothP = easeInOutCubic(rawP);
    const nowSec = isPlaying ? performance.now() / 1000 : currentTime ?? 0;

    // 1. Camera Calculations
    const animatedCamAngle = cam.angleStart + (cam.angleEnd - cam.angleStart) * smoothP;
    const animatedPitch = (cam.pitchStart ?? 0) + ((cam.pitchEnd ?? 0) - (cam.pitchStart ?? 0)) * smoothP;
    const currentZoom = cam.zoomStart + (cam.zoomEnd - cam.zoomStart) * smoothP;
    const currentPanX = cam.panStart[0] + (cam.panEnd[0] - cam.panStart[0]) * smoothP;
    const currentPanY = cam.panStart[1] + (cam.panEnd[1] - cam.panStart[1]) * smoothP;

    let shakeX = 0;
    let shakeY = 0;
    if (isPlaying && cam.shakeIntensity && cam.shakeIntensity > 0) {
      const shakeMag = cam.shakeIntensity * 12;
      shakeX = (Math.sin(nowSec * 35) + Math.sin(nowSec * 73)) * shakeMag * 0.5;
      shakeY = (Math.cos(nowSec * 41) + Math.cos(nowSec * 61)) * shakeMag * 0.5;
    }

    ctx.clearRect(0, 0, w, h);

    // Apply Viewport Zoom & Pan
    ctx.save();
    ctx.translate(w / 2 + viewportPan.x, h / 2 + viewportPan.y);
    ctx.scale(viewportZoom, viewportZoom);
    ctx.translate(-w / 2, -h / 2);

    // Camera Center Origin
    ctx.save();
    ctx.translate(w / 2 + shakeX, h / 2 + shakeY);
    ctx.scale(currentZoom, currentZoom);

    const currentScenePanX = currentPanX + ((animatedCamAngle % 360) / 360) * 450;
    const currentScenePanY = currentPanY + (animatedPitch / 90) * 120;

    // 2. Background Layers with Parallax
    renderBackgroundLayers(ctx, project, w, h, currentScenePanX, currentScenePanY, getImage);

    // Ground Plane Grid in 360 / Top-Down Mode
    renderGroundGrid(ctx, animatedPitch);

    // 3. Render Combined Stage Elements (Props + Actors + BBox calculations)
    activeBBoxRef.current = renderStageEntities({
      ctx,
      project,
      activeShot,
      currentTime,
      shotProgress,
      smoothP,
      rawP,
      nowSec,
      isPlaying,
      animatedCamAngle,
      animatedPitch,
      currentScenePanX,
      selectedActorId,
      selectedPropId,
      getImage,
    });

    // 4. Render 16:9 Camera Viewport Frame (Cinema Safe Area & Scalable View)
    const baseCamW = cam.frameWidth || 720;
    const baseCamH = cam.frameHeight || Math.round((baseCamW * 9) / 16);
    const isCamActive = !!isCameraSelected || (!selectedActorId && !selectedPropId);
    const cameraBBox = render16to9CameraFrame(
      ctx,
      w,
      h,
      baseCamW,
      baseCamH,
      0,
      0,
      isCamActive,
      showCameraFrame
    );

    if (cameraBBox && isCamActive && !selectedActorId && !selectedPropId) {
      activeBBoxRef.current = cameraBBox;
    }

    // 5. Atmospheric Particles
    renderParticles(ctx, particlesRef.current, isPlaying);

    ctx.restore(); // Camera restore
    ctx.restore(); // Viewport restore
  }, [
    project,
    activeShot,
    shotProgress,
    currentTime,
    isPlaying,
    selectedActorId,
    selectedPartId,
    selectedPropId,
    showTrajectoryLine,
    showCameraFrame,
    isCameraSelected,
    viewportZoom,
    viewportPan,
    getImage,
  ]);

  // Helper to test if mouse click/hover is near BBox Corner Handles
  const checkCornerHandleHit = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    const bbox = activeBBoxRef.current;
    if (!canvas || !bbox || !bbox.corners) return null;

    const rect = canvas.getBoundingClientRect();
    const mouseCanvasX = (e.clientX - rect.left) * (canvas.width / rect.width);
    const mouseCanvasY = (e.clientY - rect.top) * (canvas.height / rect.height);

    for (const corner of bbox.corners) {
      if (Math.hypot(mouseCanvasX - corner.x, mouseCanvasY - corner.y) <= 16) {
        return corner.cursor;
      }
    }
    return null;
  };

  // Helper to test if mouse click/hover is near BBox Top Rotate Pin
  const checkRotateHandleHit = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    const bbox = activeBBoxRef.current;
    if (!canvas || !bbox || !bbox.rotateHandle) return false;

    const rect = canvas.getBoundingClientRect();
    const mouseCanvasX = (e.clientX - rect.left) * (canvas.width / rect.width);
    const mouseCanvasY = (e.clientY - rect.top) * (canvas.height / rect.height);

    return Math.hypot(mouseCanvasX - bbox.rotateHandle.x, mouseCanvasY - bbox.rotateHandle.y) <= 14;
  };

  // Mouse Down
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    isDraggingRef.current = true;
    const isRotateHit = checkRotateHandleHit(e);
    const cornerHit = checkCornerHandleHit(e);
    const canvas = canvasRef.current;
    const rect = canvas ? canvas.getBoundingClientRect() : { left: 0, top: 0, width: 960, height: 540 };
    const mouseCanvasX = (e.clientX - rect.left) * (960 / rect.width);
    const mouseCanvasY = (e.clientY - rect.top) * (540 / rect.height);

    if (activeTool === 'orbit360') {
      dragModeRef.current = 'orbit';
    } else if (isRotateHit && activeBBoxRef.current && activeBBoxRef.current.type !== 'camera') {
      const bbox = activeBBoxRef.current;
      dragModeRef.current = bbox.type === 'actor' ? 'rotate_actor' : 'rotate_prop';
      dragStartRef.current.itemInitRotation = bbox.initRotation;
    } else if (cornerHit && activeBBoxRef.current) {
      const bbox = activeBBoxRef.current;
      if (bbox.type === 'camera') {
        dragModeRef.current = 'scale_camera';
        dragStartRef.current.itemInitScale = bbox.camWidth || 720;
      } else {
        dragModeRef.current = bbox.type === 'actor' ? 'scale_actor' : 'scale_prop';
        dragStartRef.current.itemInitScale = bbox.initScale;
      }
      dragStartRef.current.itemInitDist = Math.max(20, Math.hypot(mouseCanvasX - bbox.centerX, mouseCanvasY - bbox.centerY));
    } else {
      if (selectedActorId && onUpdateActorPosition) {
        const curActorState = activeShot.actors[selectedActorId];
        dragModeRef.current = 'move_actor';
        dragStartRef.current.itemStartX = curActorState?.positionStart[0] ?? 0;
        dragStartRef.current.itemStartY = curActorState?.positionStart[1] ?? 50;
      } else if (selectedPropId && onUpdatePropPosition) {
        const curProp = project.props.find((p) => p.id === selectedPropId);
        dragModeRef.current = 'move_prop';
        dragStartRef.current.itemStartX = curProp?.position[0] ?? 0;
        dragStartRef.current.itemStartY = curProp?.position[1] ?? 0;
      } else {
        dragModeRef.current = 'pan';
      }
    }

    dragStartRef.current = {
      ...dragStartRef.current,
      x: e.clientX,
      y: e.clientY,
      startAngle: cam.angleStart,
      startPitch: cam.pitchStart ?? 0,
      startPanX: viewportPan.x,
      startPanY: viewportPan.y,
    };
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    const isRotateHit = checkRotateHandleHit(e);
    const corner = checkCornerHandleHit(e);

    if (!isDraggingRef.current) {
      if (canvas) {
        if (activeTool === 'orbit360') {
          canvas.style.cursor = 'crosshair';
        } else if (isRotateHit) {
          canvas.style.cursor = 'crosshair';
        } else if (corner) {
          canvas.style.cursor = corner;
        } else {
          canvas.style.cursor = 'grab';
        }
      }
      return;
    }

    const dx = e.clientX - dragStartRef.current.x;
    const dy = e.clientY - dragStartRef.current.y;
    const scaleAdj = viewportZoom * (cam.zoomStart || 1.0);

    const rect = canvas ? canvas.getBoundingClientRect() : { left: 0, top: 0, width: 960, height: 540 };
    const mouseCanvasX = (e.clientX - rect.left) * (960 / rect.width);
    const mouseCanvasY = (e.clientY - rect.top) * (540 / rect.height);

    if (dragModeRef.current === 'orbit') {
      const newAngle = ((dragStartRef.current.startAngle + dx * 0.6) % 360 + 360) % 360;
      const newPitch = Math.max(0, Math.min(90, dragStartRef.current.startPitch - dy * 0.4));
      onUpdateCameraAngle(Math.round(newAngle), Math.round(newPitch));
    } else if (dragModeRef.current === 'rotate_actor' && selectedActorId && onUpdateActorRotation && activeBBoxRef.current) {
      const bbox = activeBBoxRef.current;
      const angleRad = Math.atan2(mouseCanvasY - bbox.centerY, mouseCanvasX - bbox.centerX);
      let angleDeg = Math.round(angleRad * (180 / Math.PI) + 90);
      if (angleDeg > 180) angleDeg -= 360;
      if (angleDeg < -180) angleDeg += 360;
      if (e.shiftKey) {
        angleDeg = Math.round(angleDeg / 15) * 15;
      }
      onUpdateActorRotation(selectedActorId, angleDeg);
    } else if (dragModeRef.current === 'rotate_prop' && selectedPropId && onUpdatePropRotation && activeBBoxRef.current) {
      const bbox = activeBBoxRef.current;
      const angleRad = Math.atan2(mouseCanvasY - bbox.centerY, mouseCanvasX - bbox.centerX);
      let angleDeg = Math.round(angleRad * (180 / Math.PI) + 90);
      if (angleDeg > 180) angleDeg -= 360;
      if (angleDeg < -180) angleDeg += 360;
      if (e.shiftKey) {
        angleDeg = Math.round(angleDeg / 15) * 15;
      }
      onUpdatePropRotation(selectedPropId, angleDeg);
    } else if (dragModeRef.current === 'scale_actor' && selectedActorId && onUpdateActorScale && activeBBoxRef.current) {
      const bbox = activeBBoxRef.current;
      const currentDist = Math.hypot(mouseCanvasX - bbox.centerX, mouseCanvasY - bbox.centerY);
      const ratio = currentDist / Math.max(20, dragStartRef.current.itemInitDist);
      const newScale = Math.max(0.2, Math.min(5.0, Number((dragStartRef.current.itemInitScale * ratio).toFixed(2))));
      onUpdateActorScale(selectedActorId, newScale);
    } else if (dragModeRef.current === 'scale_prop' && selectedPropId && onUpdatePropScale && activeBBoxRef.current) {
      const bbox = activeBBoxRef.current;
      const currentDist = Math.hypot(mouseCanvasX - bbox.centerX, mouseCanvasY - bbox.centerY);
      const ratio = currentDist / Math.max(20, dragStartRef.current.itemInitDist);
      const newScale = Math.max(0.2, Math.min(5.0, Number((dragStartRef.current.itemInitScale * ratio).toFixed(2))));
      onUpdatePropScale(selectedPropId, newScale);
    } else if (dragModeRef.current === 'scale_camera' && onUpdateCameraFrame && activeBBoxRef.current) {
      const bbox = activeBBoxRef.current;
      const currentDist = Math.hypot(mouseCanvasX - bbox.centerX, mouseCanvasY - bbox.centerY);
      const ratio = currentDist / Math.max(20, dragStartRef.current.itemInitDist);
      const newWidth = Math.max(280, Math.min(1800, Math.round(dragStartRef.current.itemInitScale * ratio)));
      const newHeight = Math.round((newWidth * 9) / 16);
      onUpdateCameraFrame(newWidth, newHeight);
    } else if (dragModeRef.current === 'move_actor' && selectedActorId && onUpdateActorPosition) {
      const newX = Math.round(dragStartRef.current.itemStartX + dx / Math.max(0.1, scaleAdj));
      const newY = Math.round(dragStartRef.current.itemStartY + dy / Math.max(0.1, scaleAdj));
      onUpdateActorPosition(selectedActorId, [newX, newY]);
    } else if (dragModeRef.current === 'move_prop' && selectedPropId && onUpdatePropPosition) {
      const newX = Math.round(dragStartRef.current.itemStartX + dx / Math.max(0.1, scaleAdj));
      const newY = Math.round(dragStartRef.current.itemStartY + dy / Math.max(0.1, scaleAdj));
      onUpdatePropPosition(selectedPropId, [newX, newY]);
    } else if (dragModeRef.current === 'pan') {
      setViewportPan({
        x: dragStartRef.current.startPanX + dx,
        y: dragStartRef.current.startPanY + dy,
      });
    }
  };

  const handleMouseUp = () => {
    isDraggingRef.current = false;
  };

  return (
    <div
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: '#020617',
        borderRadius: 10,
        overflow: 'hidden',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        boxShadow: '0 20px 40px rgba(0,0,0,0.8)',
      }}
    >
      <canvas
        ref={canvasRef}
        width={960}
        height={540}
        onWheel={(e) => {
          e.preventDefault();
          const zoomFactor = e.deltaY < 0 ? 1.1 : 0.9;
          setViewportZoom((prev) => Math.max(0.25, Math.min(4.0, prev * zoomFactor)));
        }}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseUp}
        onContextMenu={(e) => e.preventDefault()}
        style={{
          width: '100%',
          maxHeight: '100%',
          aspectRatio: '16/9',
          objectFit: 'contain',
          cursor: activeTool === 'orbit360' ? 'crosshair' : isDraggingRef.current ? 'grabbing' : 'grab',
          userSelect: 'none',
        }}
      />

      <Stage2DCanvasToolbar
        activeTool={activeTool}
        showCameraFrame={showCameraFrame}
        camFrameWidth={cam.frameWidth || 720}
        viewportZoom={viewportZoom}
        currentCamAngle={currentCamAngle}
        currentPitch={currentPitch}
        zToast={zToast}
        onSelectTool={setActiveTool}
        onToggleCameraFrame={() => setShowCameraFrame((v) => !v)}
        onSetCameraFrameSize={onUpdateCameraFrame}
        onZoomIn={() => setViewportZoom((z) => Math.min(4.0, z * 1.15))}
        onZoomOut={() => setViewportZoom((z) => Math.max(0.25, z * 0.85))}
        onResetZoom={() => {
          setViewportZoom(1.0);
          setViewportPan({ x: 0, y: 0 });
        }}
      />
    </div>
  );
};
