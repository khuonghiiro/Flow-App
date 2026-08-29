import React, { useRef, useEffect, useCallback, useState } from 'react';
import {
  Compass,
  RotateCw,
  Eye,
  Crown,
  Maximize2,
  Minimize2,
  Sparkles,
  Move,
  ZoomIn,
  ZoomOut,
  Maximize,
  Hand,
} from 'lucide-react';
import {
  Director2DProject,
  MultiAngleDirectorShot,
  STANDARD_8_ANGLES,
  TOP_DOWN_ANGLES,
  StandardHorizontalAngle,
  LayerPartConfig,
} from '../../../../types/studio2d_director';

interface Stage2DCanvasProps {
  project: Director2DProject;
  activeShot: MultiAngleDirectorShot;
  shotProgress: number; // 0..1 (progress inside current shot)
  isPlaying: boolean;
  selectedActorId: string | null;
  selectedPartId?: string | null;
  onSelectActor: (id: string) => void;
  onSelectPart?: (partId: string) => void;
  onUpdateCameraAngle: (yawDeg: number, pitchDeg?: number) => void;
  onUpdateActorPosition?: (actorId: string, pos: [number, number]) => void;
  showTrajectoryLine?: boolean;
}

// Cubic Smoothstep Easing for cinematic camera motion
function easeInOutCubic(t: number): number {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  rot: number;
  vRot: number;
  opacity: number;
  type: 'leaf' | 'sparkle' | 'mist';
}

export const Stage2DCanvas: React.FC<Stage2DCanvasProps> = ({
  project,
  activeShot,
  shotProgress,
  isPlaying,
  selectedActorId,
  selectedPartId,
  onSelectActor,
  onSelectPart,
  onUpdateCameraAngle,
  onUpdateActorPosition,
  showTrajectoryLine = true,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());
  const particlesRef = useRef<Particle[]>([]);

  // Interactive Viewport Zoom & Pan State
  const [viewportZoom, setViewportZoom] = useState<number>(1.0);
  const [viewportPan, setViewportPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isPanToolActive, setIsPanToolActive] = useState<boolean>(false);

  const isDraggingRef = useRef(false);
  const dragModeRef = useRef<'orbit' | 'pan' | 'actor'>('orbit');
  const dragStartRef = useRef<{ x: number; y: number; startAngle: number; startPitch: number; startPanX: number; startPanY: number }>({
    x: 0,
    y: 0,
    startAngle: 0,
    startPitch: 0,
    startPanX: 0,
    startPanY: 0,
  });

  // Initialize Atmospheric Particle System (Bamboo leaves & spirit embers)
  useEffect(() => {
    const arr: Particle[] = [];
    for (let i = 0; i < 28; i++) {
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
  const isTopDownMode = currentPitch >= 45;

  // Preload Images Cache
  const getImage = useCallback((url: string): HTMLImageElement | null => {
    if (!url) return null;
    if (imageCacheRef.current.has(url)) {
      return imageCacheRef.current.get(url)!;
    }
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.src = url;
    imageCacheRef.current.set(url, img);
    return img;
  }, []);

  // Compute Relative Angle
  const resolveAngleSprite = (
    actorId: string,
    worldAngle: number,
    camAngle: number,
    pitch: number
  ): { url: string; flipX: boolean } => {
    const actor = project.actors.find((a) => a.id === actorId);
    if (!actor) return { url: '', flipX: false };

    const relAngle = ((worldAngle - camAngle) % 360 + 360) % 360;

    if (pitch >= 45) {
      const closestTop = TOP_DOWN_ANGLES.reduce((prev, curr) =>
        Math.abs(curr.deg - relAngle) < Math.abs(prev.deg - relAngle) ? curr : prev
      );

      let topUrl = actor.sprites[closestTop.id] || '';
      let topFlipX = false;
      if (!topUrl && actor.autoMirrorSymmetry && closestTop.mirroredFrom) {
        topUrl = actor.sprites[closestTop.mirroredFrom] || '';
        topFlipX = true;
      }
      if (topUrl) return { url: topUrl, flipX: topFlipX };
    }

    const closest = STANDARD_8_ANGLES.reduce((prev, curr) =>
      Math.abs(curr.deg - relAngle) < Math.abs(prev.deg - relAngle) ? curr : prev
    );

    let url = actor.sprites[closest.id] || '';
    let flipX = false;

    if (!url && actor.autoMirrorSymmetry && closest.mirroredFrom) {
      url = actor.sprites[closest.mirroredFrom] || '';
      flipX = true;
    }

    if (!url) {
      url = actor.sprites.front || Object.values(actor.sprites)[0] || '';
    }

    return { url, flipX };
  };

  // Main Render Loop (60 FPS with Particles, Physics, Shading & Transitions)
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;
    const rawP = Math.max(0, Math.min(1, shotProgress));
    const smoothP = easeInOutCubic(rawP);
    const nowSec = performance.now() / 1000;

    // Cinematic Camera Pan & Zoom with Smooth Easing
    const currentSceneZoom = cam.zoomStart + (cam.zoomEnd - cam.zoomStart) * smoothP;
    const currentScenePanX = cam.panStart[0] + (cam.panEnd[0] - cam.panStart[0]) * smoothP;
    const currentScenePanY = cam.panStart[1] + (cam.panEnd[1] - cam.panStart[1]) * smoothP;
    const animatedCamAngle = cam.angleStart + (cam.angleEnd - cam.angleStart) * smoothP;
    const animatedPitch = (cam.pitchStart ?? 0) + ((cam.pitchEnd ?? 0) - (cam.pitchStart ?? 0)) * smoothP;

    ctx.clearRect(0, 0, w, h);

    // Apply Workspace Viewport Pan & Zoom
    ctx.save();
    ctx.translate(w / 2 + viewportPan.x, h / 2 + viewportPan.y);
    ctx.scale(viewportZoom, viewportZoom);
    ctx.translate(-w / 2, -h / 2);

    // Apply Scene Camera Pan & Zoom
    ctx.save();
    ctx.translate(w / 2 + currentScenePanX, h / 2 + currentScenePanY);
    ctx.scale(currentSceneZoom, currentSceneZoom);

    // 1. Render Background Layers with Smooth Parallax Depth
    for (const bg of project.backgroundLayers) {
      const bgImg = getImage(bg.path);
      if (bgImg && bgImg.complete) {
        ctx.save();
        ctx.globalAlpha = bg.opacity ?? 1.0;
        const bgW = 1280;
        const bgH = 720;
        const parallaxX = bg.offset[0] - currentScenePanX * bg.parallaxFactor;
        const parallaxY = bg.offset[1] - currentScenePanY * bg.parallaxFactor;
        ctx.drawImage(bgImg, -bgW / 2 + parallaxX, -bgH / 2 + parallaxY, bgW, bgH);
        ctx.restore();
      }
    }

    // 2. Render Perspective 3D Orientation Aura on Stage Ground
    ctx.save();
    ctx.translate(0, 80);
    const pitchScaleY = animatedPitch >= 45 ? 0.75 : 0.28;
    ctx.scale(1, pitchScaleY);
    const rad = ((animatedCamAngle - 90) * Math.PI) / 180;

    // Outer subtle rings
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.12)';
    ctx.lineWidth = 1.5;
    for (let r = 90; r <= 360; r += 90) {
      ctx.beginPath();
      ctx.ellipse(0, 0, r, r * 0.5, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
    // Azimuth Pointer line
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.35)';
    ctx.setLineDash([6, 6]);
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(Math.cos(rad) * 320, Math.sin(rad) * 160);
    ctx.stroke();
    ctx.restore();

    // 3. Render Actors sorted by Z-Index with Natural Puppet Physics
    const actorEntries = Object.values(activeShot.actors).sort((a, b) => a.zIndex - b.zIndex);

    for (const actState of actorEntries) {
      const actorProf = project.actors.find((a) => a.id === actState.actorId);
      if (!actorProf) continue;

      // Position interpolation with smooth easing
      const posX = actState.positionStart[0] + (actState.positionEnd[0] - actState.positionStart[0]) * smoothP;
      const posY = actState.positionStart[1] + (actState.positionEnd[1] - actState.positionStart[1]) * smoothP;

      // Natural Breathing & Action Oscillations (60 FPS smooth sine physics)
      let animOffsetY = Math.sin(nowSec * 3.5) * 2.5; // Natural subtle breathing
      let animRot = Math.sin(nowSec * 2.0) * 0.015; // Gentle wind sway

      if (actState.actionPose === 'combat_slash') {
        const slashP = (rawP * 2) % 1;
        animOffsetY = Math.sin(slashP * Math.PI) * -22;
        animRot = (slashP - 0.5) * 0.35;
      } else if (actState.actionPose === 'talk_dialogue') {
        animOffsetY += Math.sin(nowSec * 7) * 1.5;
      } else if (actState.actionPose === 'shocked_back') {
        animOffsetY = Math.sin(rawP * Math.PI * 10) * -4;
      }

      ctx.save();
      ctx.translate(posX, posY + animOffsetY);
      ctx.rotate(animRot);

      const totalScale = (actorProf.baseScale || 1.0) * (actState.scale || 1.0);

      // Resolve Sprite
      const { url, flipX } = resolveAngleSprite(
        actState.actorId,
        actState.worldFacingAngle,
        animatedCamAngle,
        animatedPitch
      );
      const spriteImg = getImage(url);

      const isMirrored = flipX || actState.flipX;
      ctx.scale(isMirrored ? -totalScale : totalScale, totalScale);

      if (spriteImg && spriteImg.complete) {
        const sprW = 120;
        const sprH = animatedPitch >= 45 ? 180 : 240;
        ctx.drawImage(spriteImg, -sprW / 2, -sprH + 20, sprW, sprH);
      }

      // Selection ring
      if (selectedActorId === actState.actorId) {
        ctx.strokeStyle = '#38bdf8';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.ellipse(0, 10, 38, 11, 0, 0, Math.PI * 2);
        ctx.stroke();
      }

      ctx.restore();

      // Motion Trajectory Line
      if (
        showTrajectoryLine &&
        (actState.positionStart[0] !== actState.positionEnd[0] ||
          actState.positionStart[1] !== actState.positionEnd[1])
      ) {
        ctx.save();
        ctx.strokeStyle = selectedActorId === actState.actorId ? '#f59e0b' : 'rgba(255,255,255,0.3)';
        ctx.lineWidth = 2;
        ctx.setLineDash([4, 4]);
        ctx.beginPath();
        ctx.moveTo(actState.positionStart[0], actState.positionStart[1]);
        ctx.lineTo(actState.positionEnd[0], actState.positionEnd[1]);
        ctx.stroke();
        ctx.fillStyle = '#f59e0b';
        ctx.beginPath();
        ctx.arc(actState.positionEnd[0], actState.positionEnd[1], 4, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
    }

    // 4. Render Atmospheric Floating Particles (Bamboo Leaves & Energy Dust)
    for (const p of particlesRef.current) {
      p.x += p.vx;
      p.y += p.vy;
      p.rot += p.vRot;
      if (p.x < -100) p.x = 1060;
      if (p.y > 600) p.y = -40;

      ctx.save();
      ctx.translate(p.x - 480, p.y - 270);
      ctx.rotate(p.rot);
      ctx.globalAlpha = p.opacity;

      if (p.type === 'leaf') {
        ctx.fillStyle = '#10b981';
        ctx.beginPath();
        ctx.ellipse(0, 0, p.size * 2, p.size * 0.8, 0, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.fillStyle = '#38bdf8';
        ctx.shadowColor = '#38bdf8';
        ctx.shadowBlur = 6;
        ctx.beginPath();
        ctx.arc(0, 0, p.size * 0.6, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    }

    // 5. Cinematic Speedlines during Combat / Slash Action
    const hasCombat = Object.values(activeShot.actors).some((a) => a.actionPose === 'combat_slash');
    if (hasCombat) {
      ctx.save();
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.25)';
      ctx.lineWidth = 2;
      for (let i = 0; i < 8; i++) {
        const lineY = -200 + i * 55 + Math.sin(nowSec * 15 + i) * 10;
        ctx.beginPath();
        ctx.moveTo(-450, lineY);
        ctx.lineTo(450, lineY - 30);
        ctx.stroke();
      }
      ctx.restore();
    }

    ctx.restore(); // Restore Scene Camera Transform

    // 6. Cinematic Shot Transitions (Flash White, Whip Pan, Fade Black)
    const transitionType = activeShot.transitionIn || 'none';
    if (rawP < 0.2) {
      const transP = rawP / 0.2; // 0..1 during entry
      if (transitionType === 'flash_white') {
        ctx.fillStyle = `rgba(255, 255, 255, ${(1 - transP) * 0.85})`;
        ctx.fillRect(0, 0, w, h);
      } else if (transitionType === 'fade_black') {
        ctx.fillStyle = `rgba(2, 6, 23, ${1 - transP})`;
        ctx.fillRect(0, 0, w, h);
      } else if (transitionType === 'whip_pan') {
        ctx.fillStyle = `rgba(56, 189, 248, ${(1 - transP) * 0.35})`;
        ctx.fillRect(0, 0, w, h);
      }
    }

    // 7. Cinematic Vignette (Dark Soft Corners)
    const vigGrad = ctx.createRadialGradient(w / 2, h / 2, w * 0.35, w / 2, h / 2, w * 0.7);
    vigGrad.addColorStop(0, 'rgba(0,0,0,0)');
    vigGrad.addColorStop(1, 'rgba(2,6,23,0.65)');
    ctx.fillStyle = vigGrad;
    ctx.fillRect(0, 0, w, h);

    // 8. Safe Zone Frame Box (16:9 Border)
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.4)';
    ctx.lineWidth = 1.5;
    ctx.strokeRect(12, 12, w - 24, h - 24);

    // 9. Dialogue Subtitle Banner (Chinese Donghua Glassmorphism)
    if (activeShot.dialogueText) {
      const bannerH = 50;
      ctx.fillStyle = 'rgba(9, 13, 22, 0.92)';
      ctx.fillRect(24, h - bannerH - 24, w - 48, bannerH);
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.4)';
      ctx.strokeRect(24, h - bannerH - 24, w - 48, bannerH);

      const speaker = project.actors.find((a) => a.id === activeShot.speakerActorId);
      const speakerName = speaker?.name || 'Nhân vật';

      ctx.fillStyle = '#38bdf8';
      ctx.font = 'bold 12.5px sans-serif';
      ctx.fillText(`${speaker?.avatarIcon || '💬'} [${speakerName}]:`, 42, h - bannerH / 2 - 14);

      ctx.fillStyle = '#f8fafc';
      ctx.font = '13.5px sans-serif';
      ctx.fillText(activeShot.dialogueText, 42, h - bannerH / 2 + 8);
    }

    ctx.restore(); // Restore Workspace Viewport Transform
  }, [project, activeShot, shotProgress, selectedActorId, selectedPartId, showTrajectoryLine, viewportZoom, viewportPan, getImage]);

  // Mouse Wheel Zoom
  const handleWheel = (e: React.WheelEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    const zoomFactor = e.deltaY < 0 ? 1.1 : 0.9;
    setViewportZoom((prev) => Math.max(0.25, Math.min(4.0, prev * zoomFactor)));
  };

  // Mouse Down: Orbit 360° or Pan Workspace
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    isDraggingRef.current = true;
    const isPan = isPanToolActive || e.button === 1 || e.button === 2 || e.shiftKey;
    dragModeRef.current = isPan ? 'pan' : 'orbit';

    dragStartRef.current = {
      x: e.clientX,
      y: e.clientY,
      startAngle: cam.angleStart,
      startPitch: cam.pitchStart ?? 0,
      startPanX: viewportPan.x,
      startPanY: viewportPan.y,
    };
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!isDraggingRef.current) return;
    const dx = e.clientX - dragStartRef.current.x;
    const dy = e.clientY - dragStartRef.current.y;

    if (dragModeRef.current === 'pan') {
      setViewportPan({
        x: dragStartRef.current.startPanX + dx,
        y: dragStartRef.current.startPanY + dy,
      });
    } else if (dragModeRef.current === 'orbit') {
      const newAngle = ((dragStartRef.current.startAngle + dx * 0.6) % 360 + 360) % 360;
      const newPitch = Math.max(0, Math.min(90, dragStartRef.current.startPitch - dy * 0.4));
      onUpdateCameraAngle(Math.round(newAngle), Math.round(newPitch));
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
        onWheel={handleWheel}
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
          cursor: isPanToolActive ? 'grab' : isDraggingRef.current ? 'grabbing' : 'crosshair',
          userSelect: 'none',
        }}
      />

      {/* ─── FLOATING VIEWPORT ZOOM & PAN CONTROLS (Top-Left HUD) ─────────── */}
      <div
        style={{
          position: 'absolute',
          top: 14,
          left: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          background: 'rgba(9, 13, 22, 0.88)',
          backdropFilter: 'blur(12px)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 8,
          padding: '4px 6px',
          boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
          zIndex: 20,
        }}
      >
        <button
          onClick={() => setViewportZoom((z) => Math.max(0.25, z * 0.85))}
          title="Thu nhỏ Viewport (Zoom Out)"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 26,
            height: 26,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.06)',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <ZoomOut size={13} />
        </button>

        <button
          onClick={() => {
            setViewportZoom(1.0);
            setViewportPan({ x: 0, y: 0 });
          }}
          title="Khôi phục tỉ lệ 100%"
          style={{
            padding: '2px 6px',
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
          onClick={() => setViewportZoom((z) => Math.min(4.0, z * 1.15))}
          title="Phóng to Viewport (Zoom In)"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 26,
            height: 26,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.06)',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <ZoomIn size={13} />
        </button>

        <div style={{ width: 1, height: 16, background: 'rgba(255,255,255,0.1)', margin: '0 2px' }} />

        {/* Pan Hand Tool Toggle */}
        <button
          onClick={() => setIsPanToolActive(!isPanToolActive)}
          title={isPanToolActive ? 'Đang bật Pan (Kéo di chuyển khung tranh)' : 'Bật chế độ Pan di chuyển'}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 26,
            height: 26,
            borderRadius: 4,
            background: isPanToolActive ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255,255,255,0.06)',
            border: isPanToolActive ? '1px solid #38bdf8' : 'none',
            color: isPanToolActive ? '#38bdf8' : '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <Hand size={13} />
        </button>
      </div>

      {/* ─── FLOATING 360° ORBIT GIMBAL HUD (Top-Right HUD) ──────────────── */}
      <div
        style={{
          position: 'absolute',
          top: 14,
          right: 14,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(9, 13, 22, 0.88)',
          backdropFilter: 'blur(12px)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 12,
          padding: '8px 10px',
          boxShadow: '0 8px 24px rgba(0,0,0,0.6), 0 0 15px rgba(56, 189, 248, 0.15)',
          zIndex: 20,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%', gap: 8 }}>
          <span style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Compass size={12} /> CAMERA 360°
          </span>
          <span style={{ fontSize: 10, fontWeight: 700, color: isTopDownMode ? '#facc15' : '#4ade80' }}>
            {isTopDownMode ? '👑 TOP-DOWN' : `${Math.round(currentCamAngle)}°`}
          </span>
        </div>

        {/* Rotary Visual Compass Wheel inside HUD */}
        <div
          onMouseDown={(e) => e.stopPropagation()}
          style={{
            position: 'relative',
            width: 84,
            height: 84,
            borderRadius: '50%',
            background: 'radial-gradient(circle, #090d16 0%, #030712 100%)',
            border: '2px solid rgba(56, 189, 248, 0.4)',
            boxShadow: '0 0 10px rgba(56, 189, 248, 0.2) inset',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
          }}
        >
          {/* Needle Pointer */}
          <div
            style={{
              position: 'absolute',
              width: 3,
              height: 32,
              background: 'linear-gradient(to top, #38bdf8, #ec4899)',
              borderRadius: 2,
              bottom: 42,
              left: 'calc(50% - 1.5px)',
              transformOrigin: 'bottom center',
              transform: `rotate(${currentCamAngle}deg)`,
              transition: 'transform 0.05s linear',
              zIndex: 5,
            }}
          />

          {/* 4 Cardinal Angle Dots */}
          {[0, 90, 180, 270].map((deg) => {
            const rad = ((deg - 90) * Math.PI) / 180;
            const x = 42 + 30 * Math.cos(rad) - 7;
            const y = 42 + 30 * Math.sin(rad) - 7;
            const isSelected = Math.abs(deg - currentCamAngle) < 22.5;

            return (
              <button
                key={deg}
                onClick={() => onUpdateCameraAngle(deg, currentPitch)}
                style={{
                  position: 'absolute',
                  left: x,
                  top: y,
                  width: 14,
                  height: 14,
                  borderRadius: '50%',
                  background: isSelected ? '#38bdf8' : 'rgba(255,255,255,0.15)',
                  border: isSelected ? '1px solid #fff' : 'none',
                  fontSize: 7,
                  color: isSelected ? '#000' : '#fff',
                  fontWeight: 700,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  zIndex: 8,
                }}
              >
                {deg === 0 ? 'S' : deg === 90 ? 'E' : deg === 180 ? 'N' : 'W'}
              </button>
            );
          })}
        </div>

        {/* Top-Down View Toggle Button */}
        <button
          onClick={() => onUpdateCameraAngle(currentCamAngle, isTopDownMode ? 0 : 60)}
          title="Bật/Tắt Góc quay nhìn từ trên đỉnh đầu xuống (Top-down / Bird's eye)"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
            width: '100%',
            padding: '4px 6px',
            borderRadius: 6,
            background: isTopDownMode ? 'linear-gradient(135deg, #eab308, #ca8a04)' : 'rgba(255,255,255,0.06)',
            border: isTopDownMode ? '1px solid #facc15' : '1px solid rgba(255,255,255,0.1)',
            color: isTopDownMode ? '#000' : '#e2e8f0',
            fontSize: 9.5,
            fontWeight: 700,
            cursor: 'pointer',
            transition: 'all 0.2s',
          }}
        >
          <Crown size={11} /> {isTopDownMode ? 'Đỉnh Đầu (60°)' : 'Ngang Tầm Mắt (0°)'}
        </button>

        {/* Quick 8 Angles Snap Pills */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 3, width: '100%' }}>
          {[0, 45, 90, 135, 180, 225, 270, 315].map((deg) => (
            <button
              key={deg}
              onClick={() => onUpdateCameraAngle(deg, currentPitch)}
              style={{
                padding: '2px 0',
                fontSize: 8.5,
                fontWeight: Math.abs(deg - currentCamAngle) < 22.5 ? 700 : 500,
                borderRadius: 3,
                background: Math.abs(deg - currentCamAngle) < 22.5 ? '#0284c7' : 'rgba(255,255,255,0.04)',
                border: 'none',
                color: '#fff',
                cursor: 'pointer',
                textAlign: 'center',
              }}
            >
              {deg}°
            </button>
          ))}
        </div>
      </div>

      {/* Direct Drag Instructions Hint in Canvas Corner */}
      <div
        style={{
          position: 'absolute',
          bottom: 14,
          left: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(2, 6, 23, 0.75)',
          backdropFilter: 'blur(8px)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 6,
          padding: '4px 8px',
          color: '#94a3b8',
          fontSize: 9.5,
          pointerEvents: 'none',
        }}
      >
        <Move size={11} color="#38bdf8" /> Cuộn chuột để Zoom • Kéo để xoay 360°
      </div>
    </div>
  );
};
