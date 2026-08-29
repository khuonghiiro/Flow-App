// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useRef, useEffect, useCallback, useState } from 'react';
import {
  Move,
  ZoomIn,
  ZoomOut,
  Hand,
  Compass,
  Rotate3d,
  Layers,
} from 'lucide-react';
import {
  Director2DProject,
  MultiAngleDirectorShot,
  STANDARD_8_ANGLES,
  TOP_DOWN_ANGLES,
  StandardHorizontalAngle,
  LayerPartConfig,
  ScenePropItem,
} from '../../../../types/studio2d_director';

interface Stage2DCanvasProps {
  project: Director2DProject;
  activeShot: MultiAngleDirectorShot;
  shotProgress: number; // 0..1 (progress inside current shot)
  currentTime?: number;
  isPlaying: boolean;
  selectedActorId: string | null;
  selectedPartId?: string | null;
  selectedPropId?: string | null;
  onSelectActor: (id: string) => void;
  onSelectPart?: (partId: string) => void;
  onSelectProp?: (propId: string) => void;
  onUpdateCameraAngle: (yawDeg: number, pitchDeg?: number) => void;
  onUpdateActorPosition?: (actorId: string, pos: [number, number]) => void;
  onUpdateActorScale?: (actorId: string, scale: number) => void;
  onUpdateActorZIndex?: (actorId: string, delta: number) => void;
  onUpdatePropPosition?: (propId: string, pos: [number, number]) => void;
  onUpdatePropScale?: (propId: string, scale: number) => void;
  onUpdatePropZIndex?: (propId: string, delta: number) => void;
  showTrajectoryLine?: boolean;
}

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

interface BBoxCorner {
  x: number;
  y: number;
  cursor: 'nwse-resize' | 'nesw-resize';
}

interface ActiveBBoxInfo {
  type: 'actor' | 'prop';
  id: string;
  centerX: number;
  centerY: number;
  initDist: number;
  initScale: number;
  corners: BBoxCorner[];
}

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
  onUpdateActorZIndex,
  onUpdatePropPosition,
  onUpdatePropScale,
  onUpdatePropZIndex,
  showTrajectoryLine = true,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());
  const particlesRef = useRef<Particle[]>([]);

  // Refs for stable keyboard listener access (avoids interval teardown during holding)
  const projectRef = useRef(project);
  projectRef.current = project;
  const activeShotRef = useRef(activeShot);
  activeShotRef.current = activeShot;
  const selectedActorIdRef = useRef(selectedActorId);
  selectedActorIdRef.current = selectedActorId;
  const selectedPropIdRef = useRef(selectedPropId);
  selectedPropIdRef.current = selectedPropId;
  const onUpdateActorScaleRef = useRef(onUpdateActorScale);
  onUpdateActorScaleRef.current = onUpdateActorScale;
  const onUpdatePropScaleRef = useRef(onUpdatePropScale);
  onUpdatePropScaleRef.current = onUpdatePropScale;
  const onUpdateActorZIndexRef = useRef(onUpdateActorZIndex);
  onUpdateActorZIndexRef.current = onUpdateActorZIndex;
  const onUpdatePropZIndexRef = useRef(onUpdatePropZIndex);
  onUpdatePropZIndexRef.current = onUpdatePropZIndex;

  // Interactive Viewport Tools: 'hand' (Default: Move & Transform) vs 'orbit360' (360° Angle Orbit)
  const [activeTool, setActiveTool] = useState<'hand' | 'orbit360'>('hand');
  const [viewportZoom, setViewportZoom] = useState<number>(1.0);
  const [viewportPan, setViewportPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [zToast, setZToast] = useState<{ text: string; time: number } | null>(null);

  // Exact screen-space bounding box handles
  const activeBBoxRef = useRef<ActiveBBoxInfo | null>(null);

  const isDraggingRef = useRef(false);
  const dragModeRef = useRef<'orbit' | 'pan' | 'move_actor' | 'move_prop' | 'scale_actor' | 'scale_prop'>('pan');
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
  });

  const scaleHoldTimerRef = useRef<number | null>(null);

  // ─── STABLE KEYBOARD SHORTCUT LISTENER ──────────────────────────────────
  useEffect(() => {
    const stepScale = (delta: number) => {
      const curActorId = selectedActorIdRef.current;
      const curPropId = selectedPropIdRef.current;
      const curShot = activeShotRef.current;
      const curProj = projectRef.current;

      if (curActorId && onUpdateActorScaleRef.current && curShot) {
        const curScale = curShot.actors[curActorId]?.scale ?? 1.6;
        const newScale = Math.max(0.2, Math.min(5.0, Number((curScale + delta).toFixed(2))));
        onUpdateActorScaleRef.current(curActorId, newScale);
        setZToast({
          text: `🔍 Tỉ lệ (Scale): ${newScale.toFixed(2)}x ${delta > 0 ? '🔼' : '🔽'}`,
          time: Date.now(),
        });
      } else if (curPropId && onUpdatePropScaleRef.current && curProj) {
        const curProp = curProj.props.find((p) => p.id === curPropId);
        const curScale = curProp?.scale[0] ?? 1.0;
        const newScale = Math.max(0.2, Math.min(5.0, Number((curScale + delta).toFixed(2))));
        onUpdatePropScaleRef.current(curPropId, newScale);
        setZToast({
          text: `🔍 Tỉ lệ (Scale): ${newScale.toFixed(2)}x ${delta > 0 ? '🔼' : '🔽'}`,
          time: Date.now(),
        });
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes((e.target as HTMLElement)?.tagName)) return;

      const isPlus = e.key === '+' || e.key === '=' || e.code === 'NumpadAdd' || e.code === 'Equal';
      const isMinus = e.key === '-' || e.key === '_' || e.code === 'NumpadSubtract' || e.code === 'Minus';

      if (!isPlus && !isMinus) return;

      if (e.shiftKey) {
        // Continuous Scaling Mode
        e.preventDefault();
        const delta = isPlus ? 0.05 : -0.05;

        // If OS repeat or timer already active, still perform step if not in timer
        if (!scaleHoldTimerRef.current) {
          stepScale(delta); // initial immediate step
          scaleHoldTimerRef.current = window.setInterval(() => {
            stepScale(delta);
          }, 45);
        }
      } else {
        // Single Step Z-Index Mode
        const curActorId = selectedActorIdRef.current;
        const curPropId = selectedPropIdRef.current;
        const curShot = activeShotRef.current;
        const curProj = projectRef.current;

        if (isPlus) {
          if (curActorId && onUpdateActorZIndexRef.current && curShot) {
            onUpdateActorZIndexRef.current(curActorId, 1);
            const curZ = (curShot.actors[curActorId]?.zIndex || 10) + 1;
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬆️`, time: Date.now() });
          } else if (curPropId && onUpdatePropZIndexRef.current && curProj) {
            onUpdatePropZIndexRef.current(curPropId, 1);
            const curProp = curProj.props.find((p) => p.id === curPropId);
            const curZ = (curProp?.zIndex || 5) + 1;
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬆️`, time: Date.now() });
          }
        } else if (isMinus) {
          if (curActorId && onUpdateActorZIndexRef.current && curShot) {
            onUpdateActorZIndexRef.current(curActorId, -1);
            const curZ = Math.max(1, (curShot.actors[curActorId]?.zIndex || 10) - 1);
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬇️`, time: Date.now() });
          } else if (curPropId && onUpdatePropZIndexRef.current && curProj) {
            onUpdatePropZIndexRef.current(curPropId, -1);
            const curProp = curProj.props.find((p) => p.id === curPropId);
            const curZ = Math.max(1, (curProp?.zIndex || 5) - 1);
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬇️`, time: Date.now() });
          }
        }
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      const isPlus = e.key === '+' || e.key === '=' || e.code === 'NumpadAdd' || e.code === 'Equal';
      const isMinus = e.key === '-' || e.key === '_' || e.code === 'NumpadSubtract' || e.code === 'Minus';
      if (isPlus || isMinus || e.key === 'Shift') {
        if (scaleHoldTimerRef.current) {
          clearInterval(scaleHoldTimerRef.current);
          scaleHoldTimerRef.current = null;
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      if (scaleHoldTimerRef.current) clearInterval(scaleHoldTimerRef.current);
    };
  }, []);

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
    worldFacingAngle: number,
    camAngle: number,
    pitch: number
  ): { url: string; flipX: boolean } => {
    const actor = project.actors.find((a) => a.id === actorId);
    if (!actor) return { url: '', flipX: false };

    let relYaw = (worldFacingAngle - camAngle) % 360;
    if (relYaw < 0) relYaw += 360;

    let nearest = STANDARD_8_ANGLES[0];
    let minDiff = 999;
    for (const opt of STANDARD_8_ANGLES) {
      const diff = Math.min(Math.abs(relYaw - opt.deg), 360 - Math.abs(relYaw - opt.deg));
      if (diff < minDiff) {
        minDiff = diff;
        nearest = opt;
      }
    }

    if (pitch >= 45) {
      let topNearest = TOP_DOWN_ANGLES[0];
      let topMin = 999;
      for (const opt of TOP_DOWN_ANGLES) {
        const diff = Math.min(Math.abs(relYaw - opt.deg), 360 - Math.abs(relYaw - opt.deg));
        if (diff < topMin) {
          topMin = diff;
          topNearest = opt;
        }
      }
      const topSprite = actor.topDownSprites?.[topNearest.id];
      if (topSprite) return { url: topSprite, flipX: false };
    }

    const sprite = actor.sprites[nearest.id];
    if (sprite) return { url: sprite, flipX: false };

    const flipFallbackMap: Partial<Record<StandardHorizontalAngle, StandardHorizontalAngle>> = {
      front_left: 'front_right',
      front_right: 'front_left',
      side_left: 'side_right',
      side_right: 'side_left',
      back_left: 'back_right',
      back_right: 'back_left',
    };
    const paired = flipFallbackMap[nearest.id];
    if (paired && actor.sprites[paired]) {
      return { url: actor.sprites[paired]!, flipX: true };
    }

    return { url: actor.sprites.front || Object.values(actor.sprites)[0] || '', flipX: false };
  };

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
    // When paused, nowSec is fixed to currentTime so objects never vibrate/jitter!
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
    for (const bg of project.backgroundLayers) {
      const bgImg = getImage(bg.path);
      if (bgImg && bgImg.complete) {
        ctx.save();
        ctx.globalAlpha = bg.opacity ?? 1.0;
        const pFactor = bg.parallaxFactor ?? 0.2;
        const dx = currentScenePanX * pFactor + (bg.offset?.[0] || 0);
        const dy = currentScenePanY * pFactor + (bg.offset?.[1] || 0);

        const imgW = bgImg.width || 1920;
        const imgH = bgImg.height || 1080;
        const scale = Math.max(w / imgW, h / imgH) * 1.35;
        const drawW = imgW * scale;
        const drawH = imgH * scale;

        ctx.drawImage(bgImg, -drawW / 2 - dx, -drawH / 2 - dy, drawW, drawH);
        ctx.restore();
      }
    }

    // Ground Plane Grid in 360 / Top-Down Mode
    if (animatedPitch > 25) {
      ctx.save();
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.12)';
      ctx.lineWidth = 1;
      for (let r = 80; r <= 320; r += 80) {
        ctx.beginPath();
        ctx.ellipse(0, 0, r, r * 0.5, 0, 0, Math.PI * 2);
        ctx.stroke();
      }
      ctx.restore();
    }

    // 3. Render Combined Stage Elements (Props + Actors)
    type RenderItem =
      | { type: 'actor'; zIndex: number; data: any }
      | { type: 'prop'; zIndex: number; data: ScenePropItem };

    const renderList: RenderItem[] = [];
    const currentEvalTime = currentTime ?? (shotProgress * (activeShot?.durationSeconds || 3));

    // Filter by Visibility Time Window
    for (const actState of Object.values(activeShot.actors)) {
      const visFrom = actState.visibleFrom ?? 0;
      const visTo = actState.visibleTo ?? 999999;
      if (currentEvalTime >= visFrom && currentEvalTime <= visTo) {
        renderList.push({ type: 'actor', zIndex: actState.zIndex ?? 10, data: actState });
      }
    }

    if (project.props && Array.isArray(project.props)) {
      for (const p of project.props) {
        const propShotOverride = activeShot.props?.[p.id];
        const activeP = propShotOverride || p;
        const visFrom = activeP.visibleFrom ?? 0;
        const visTo = activeP.visibleTo ?? 999999;
        if (activeP.visible !== false && currentEvalTime >= visFrom && currentEvalTime <= visTo) {
          renderList.push({ type: 'prop', zIndex: activeP.zIndex ?? 5, data: p });
        }
      }
    }

    renderList.sort((a, b) => a.zIndex - b.zIndex);
    activeBBoxRef.current = null;

    for (const item of renderList) {
      if (item.type === 'prop') {
        const prop = item.data;
        const propImg = getImage(prop.path);
        const propShotOverride = activeShot.props?.[prop.id];
        const activeProp = propShotOverride || prop;
        const growthStage = activeProp.growthStage || 'normal';

        ctx.save();
        const parallaxDx = currentScenePanX * (1 - (activeProp.parallaxFactor || 1.0));
        ctx.translate(activeProp.position[0] - parallaxDx, activeProp.position[1]);

        let extraScaleX = 1.0;
        let extraScaleY = 1.0;
        let extraRotation = 0;

        if (isPlaying) {
          if (growthStage === 'seed_sprout') {
            extraScaleX = 0.35;
            extraScaleY = 0.35 + Math.sin(nowSec * 3) * 0.03;
          } else if (growthStage === 'grow_big') {
            extraScaleX = 1.25 + Math.sin(nowSec * 2) * 0.03;
            extraScaleY = 1.25 + Math.sin(nowSec * 2) * 0.03;
          } else if (growthStage === 'bloom_flowers') {
            extraScaleX = 1.15;
            extraScaleY = 1.15;
            ctx.shadowColor = '#f472b6';
            ctx.shadowBlur = 16;
          } else if (growthStage === 'bear_fruit') {
            extraScaleX = 1.1;
            extraScaleY = 1.1;
            ctx.shadowColor = '#ef4444';
            ctx.shadowBlur = 12;
          } else if (growthStage === 'sway_wind') {
            extraRotation = Math.sin(nowSec * 4 + activeProp.position[0] * 0.01) * 0.08;
          } else if (growthStage === 'glow_magic') {
            ctx.shadowColor = '#38bdf8';
            ctx.shadowBlur = 20;
          }
        }

        ctx.rotate(((activeProp.rotation * Math.PI) / 180) + extraRotation);
        ctx.scale((activeProp.flipX ? -activeProp.scale[0] : activeProp.scale[0]) * extraScaleX, activeProp.scale[1] * extraScaleY);
        ctx.globalAlpha = activeProp.opacity ?? 1.0;

        if (propImg && propImg.complete) {
          const pw = propImg.width || 120;
          const ph = propImg.height || 120;
          ctx.drawImage(propImg, -pw / 2, -ph / 2, pw, ph);

          // ─── INTERACTIVE BOUNDING BOX FOR SELECTED PROP ────────────────
          if (selectedPropId === prop.id) {
            // Compute 100% exact screen coordinates via matrix transform
            const m = ctx.getTransform();
            const pTL = m.transformPoint(new DOMPoint(-pw / 2 - 6, -ph / 2 - 6));
            const pTR = m.transformPoint(new DOMPoint(pw / 2 + 6, -ph / 2 - 6));
            const pBL = m.transformPoint(new DOMPoint(-pw / 2 - 6, ph / 2 + 6));
            const pBR = m.transformPoint(new DOMPoint(pw / 2 + 6, ph / 2 + 6));
            const pC = m.transformPoint(new DOMPoint(0, 0));

            activeBBoxRef.current = {
              type: 'prop',
              id: prop.id,
              centerX: pC.x,
              centerY: pC.y,
              initDist: Math.hypot(pTR.x - pC.x, pTR.y - pC.y),
              initScale: activeProp.scale[0],
              corners: [
                { x: pTL.x, y: pTL.y, cursor: 'nwse-resize' },
                { x: pTR.x, y: pTR.y, cursor: 'nesw-resize' },
                { x: pBL.x, y: pBL.y, cursor: 'nesw-resize' },
                { x: pBR.x, y: pBR.y, cursor: 'nwse-resize' },
              ],
            };

            ctx.strokeStyle = '#4ade80';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([5, 5]);
            ctx.strokeRect(-pw / 2 - 6, -ph / 2 - 6, pw + 12, ph + 12);
            ctx.setLineDash([]);

            // 4 Corner Resize Handles
            ctx.fillStyle = '#4ade80';
            ctx.fillRect(-pw / 2 - 10, -ph / 2 - 10, 8, 8);
            ctx.fillRect(pw / 2 + 2, -ph / 2 - 10, 8, 8);
            ctx.fillRect(-pw / 2 - 10, ph / 2 + 2, 8, 8);
            ctx.fillRect(pw / 2 + 2, ph / 2 + 2, 8, 8);

            // BBox Title Tag
            ctx.fillStyle = 'rgba(15, 23, 42, 0.9)';
            ctx.fillRect(-pw / 2 - 6, -ph / 2 - 28, pw + 12, 18);
            ctx.fillStyle = '#4ade80';
            ctx.font = 'bold 9px sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(
              `🌲 ${activeProp.name} • (${activeProp.position[0]}, ${activeProp.position[1]}) • Tỉ lệ: ${activeProp.scale[0]}x • Lớp(Z): ${activeProp.zIndex ?? 5}`,
              0,
              -ph / 2 - 16
            );
            ctx.textAlign = 'start';
          }
        }
        ctx.restore();
      } else {
        // Render Actor
        const actState = item.data;
        const actorProf = project.actors.find((a) => a.id === actState.actorId);
        if (!actorProf) continue;

        const posX = actState.positionStart[0] + (actState.positionEnd[0] - actState.positionStart[0]) * smoothP;
        const posY = actState.positionStart[1] + (actState.positionEnd[1] - actState.positionStart[1]) * smoothP;

        let animOffsetY = 0;
        let animRot = 0;

        if (isPlaying) {
          animOffsetY = Math.sin(nowSec * 3.5) * 2.5;
          animRot = Math.sin(nowSec * 2.0) * 0.015;

          if (actState.actionPose === 'combat_slash') {
            const slashP = (rawP * 2) % 1;
            animOffsetY = Math.sin(slashP * Math.PI) * -22;
            animRot = (slashP - 0.5) * 0.35;
          } else if (actState.actionPose === 'combat_cast') {
            animOffsetY = Math.sin(nowSec * 5) * 8 - 10;
          } else if (actState.actionPose === 'talk_dialogue') {
            animOffsetY += Math.sin(nowSec * 7) * 2.0;
          } else if (actState.actionPose === 'shocked_back') {
            animOffsetY = Math.sin(rawP * Math.PI * 10) * -6;
          } else if (actState.actionPose === 'fly_dash') {
            animOffsetY = Math.sin(nowSec * 6) * 5 - 15;
            animRot = 0.12;
          } else if (actState.actionPose === 'walk_cycle') {
            animOffsetY = Math.abs(Math.sin(nowSec * 8)) * -7;
          }
        }

        ctx.save();
        ctx.translate(posX, posY + animOffsetY);
        ctx.rotate(animRot);

        const totalScale = (actorProf.baseScale || 1.0) * (actState.scale || 1.0);

        const { url, flipX } = resolveAngleSprite(
          actState.actorId,
          actState.worldFacingAngle,
          animatedCamAngle,
          animatedPitch
        );
        const spriteImg = getImage(url);

        if (spriteImg && spriteImg.complete) {
          const sw = (spriteImg.width || 200) * totalScale * 0.45;
          const sh = (spriteImg.height || 300) * totalScale * 0.45;

          ctx.save();
          if (flipX || actState.flipX) ctx.scale(-1, 1);
          ctx.drawImage(spriteImg, -sw / 2, -sh + 20, sw, sh);
          ctx.restore();

          // ─── INTERACTIVE BOUNDING BOX FOR SELECTED ACTOR ───────────────
          if (selectedActorId === actState.actorId) {
            // Compute 100% exact screen coordinates via matrix transform
            const m = ctx.getTransform();
            const pTL = m.transformPoint(new DOMPoint(-sw / 2 - 6, -sh + 14));
            const pTR = m.transformPoint(new DOMPoint(sw / 2 + 6, -sh + 14));
            const pBL = m.transformPoint(new DOMPoint(-sw / 2 - 6, 26));
            const pBR = m.transformPoint(new DOMPoint(sw / 2 + 6, 26));
            const pC = m.transformPoint(new DOMPoint(0, -sh / 2 + 20));

            activeBBoxRef.current = {
              type: 'actor',
              id: actState.actorId,
              centerX: pC.x,
              centerY: pC.y,
              initDist: Math.hypot(pTR.x - pC.x, pTR.y - pC.y),
              initScale: actState.scale || 1.6,
              corners: [
                { x: pTL.x, y: pTL.y, cursor: 'nwse-resize' },
                { x: pTR.x, y: pTR.y, cursor: 'nesw-resize' },
                { x: pBL.x, y: pBL.y, cursor: 'nesw-resize' },
                { x: pBR.x, y: pBR.y, cursor: 'nwse-resize' },
              ],
            };

            ctx.strokeStyle = '#38bdf8';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([5, 5]);
            ctx.strokeRect(-sw / 2 - 6, -sh + 14, sw + 12, sh + 12);
            ctx.setLineDash([]);

            // 4 Corner Resize Handles
            ctx.fillStyle = '#38bdf8';
            ctx.fillRect(-sw / 2 - 10, -sh + 10, 8, 8);
            ctx.fillRect(sw / 2 + 2, -sh + 10, 8, 8);
            ctx.fillRect(-sw / 2 - 10, 22, 8, 8);
            ctx.fillRect(sw / 2 + 2, 22, 8, 8);

            // BBox Title Tag
            ctx.fillStyle = 'rgba(15, 23, 42, 0.9)';
            ctx.fillRect(-sw / 2 - 6, -sh - 8, sw + 12, 18);
            ctx.fillStyle = '#38bdf8';
            ctx.font = 'bold 9px sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(
              `👤 ${actorProf.name} • (${Math.round(posX)}, ${Math.round(posY)}) • Tỉ lệ: ${(actState.scale || 1.6).toFixed(2)}x • Lớp(Z): ${actState.zIndex ?? 10}`,
              0,
              -sh + 4
            );
            ctx.textAlign = 'start';
          }
        }
        ctx.restore();
      }
    }

    // 4. Atmospheric Particles
    for (const p of particlesRef.current) {
      if (isPlaying) {
        p.x += p.vx;
        p.y += p.vy;
        p.rot += p.vRot;
        if (p.x < -100) p.x = 1060;
        if (p.y > 600) p.y = -40;
      }

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
        ctx.beginPath();
        ctx.arc(0, 0, p.size * 0.6, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    }

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

  // Mouse Wheel Zoom
  const handleWheel = (e: React.WheelEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    const zoomFactor = e.deltaY < 0 ? 1.1 : 0.9;
    setViewportZoom((prev) => Math.max(0.25, Math.min(4.0, prev * zoomFactor)));
  };

  // Mouse Down
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    isDraggingRef.current = true;
    const cornerHit = checkCornerHandleHit(e);
    const canvas = canvasRef.current;
    const rect = canvas ? canvas.getBoundingClientRect() : { left: 0, top: 0, width: 960, height: 540 };
    const mouseCanvasX = (e.clientX - rect.left) * (960 / rect.width);
    const mouseCanvasY = (e.clientY - rect.top) * (540 / rect.height);

    if (activeTool === 'orbit360') {
      dragModeRef.current = 'orbit';
    } else if (cornerHit && activeBBoxRef.current) {
      // Corner scaling mode
      const bbox = activeBBoxRef.current;
      dragModeRef.current = bbox.type === 'actor' ? 'scale_actor' : 'scale_prop';
      dragStartRef.current.itemInitScale = bbox.initScale;
      dragStartRef.current.itemInitDist = Math.max(20, Math.hypot(mouseCanvasX - bbox.centerX, mouseCanvasY - bbox.centerY));
    } else {
      // Hand / Move mode
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
    const corner = checkCornerHandleHit(e);

    if (!isDraggingRef.current) {
      if (canvas) {
        if (activeTool === 'orbit360') {
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
          cursor: activeTool === 'orbit360' ? 'crosshair' : isDraggingRef.current ? 'grabbing' : 'grab',
          userSelect: 'none',
        }}
      />

      {/* ─── TOP-RIGHT INTERACTIVE TOOLBAR (Hand / 360° / Zoom) ───────────── */}
      <div
        style={{
          position: 'absolute',
          top: 14,
          right: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 5,
          background: 'rgba(9, 13, 22, 0.92)',
          backdropFilter: 'blur(12px)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          borderRadius: 8,
          padding: '4px 6px',
          boxShadow: '0 8px 24px rgba(0,0,0,0.7)',
          zIndex: 20,
        }}
      >
        {/* 1. Hand / Transform Tool (DEFAULT ACTIVE) */}
        <button
          onClick={() => setActiveTool('hand')}
          title="Bàn tay: Kéo đối tượng để di chuyển, kéo 4 góc BBox để co dãn size (Phím Shift +/- chỉnh Scale, +/- chỉnh Z-Index)"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 8px',
            borderRadius: 5,
            background: activeTool === 'hand' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.05)',
            border: activeTool === 'hand' ? '1px solid #38bdf8' : '1px solid transparent',
            color: activeTool === 'hand' ? '#38bdf8' : '#94a3b8',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <Hand size={13} />
          <span>Bàn tay</span>
        </button>

        {/* 2. 360° Orbit Tool */}
        <button
          onClick={() => setActiveTool('orbit360')}
          title="Chế độ 360°: Kéo chuột trên khung tranh để xoay 360 độ góc camera quanh sân khấu"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 8px',
            borderRadius: 5,
            background: activeTool === 'orbit360' ? 'linear-gradient(135deg, #7c3aed, #a855f7)' : 'rgba(255, 255, 255, 0.05)',
            border: activeTool === 'orbit360' ? '1px solid #c084fc' : '1px solid transparent',
            color: activeTool === 'orbit360' ? '#ffffff' : '#94a3b8',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            boxShadow: activeTool === 'orbit360' ? '0 0 12px rgba(168, 85, 247, 0.4)' : 'none',
            transition: 'all 0.15s ease',
          }}
        >
          <Rotate3d size={13} />
          <span>360°</span>
        </button>

        <div style={{ width: 1, height: 16, background: 'rgba(255,255,255,0.12)', margin: '0 2px' }} />

        {/* 3. Zoom Controls */}
        <button
          onClick={() => setViewportZoom((z) => Math.max(0.25, z * 0.85))}
          title="Thu nhỏ Viewport"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 24,
            height: 24,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.06)',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <ZoomOut size={12} />
        </button>

        <button
          onClick={() => {
            setViewportZoom(1.0);
            setViewportPan({ x: 0, y: 0 });
          }}
          title="Tỉ lệ 100%"
          style={{
            padding: '2px 5px',
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
          title="Phóng to Viewport"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 24,
            height: 24,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.06)',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
          }}
        >
          <ZoomIn size={12} />
        </button>
      </div>

      {/* ─── LIVE 360° COMPASS OVERLAY (Top-Center when active) ──────────── */}
      {activeTool === 'orbit360' && (
        <div
          style={{
            position: 'absolute',
            top: 14,
            left: '50%',
            transform: 'translateX(-50%)',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid rgba(168, 85, 247, 0.5)',
            borderRadius: 20,
            padding: '4px 12px',
            color: '#e9d5ff',
            fontSize: 11,
            fontWeight: 700,
            boxShadow: '0 4px 16px rgba(0,0,0,0.6)',
            zIndex: 15,
            pointerEvents: 'none',
          }}
        >
          <Compass size={14} color="#c084fc" />
          <span>360° Xoay Góc: <b>{Math.round(currentCamAngle)}°</b> (Cao: {Math.round(currentPitch)}°)</span>
        </div>
      )}

      {/* ─── TRANSIENT HUD TOAST ─────────────────────────────────────────── */}
      {zToast && Date.now() - zToast.time < 1800 && (
        <div
          style={{
            position: 'absolute',
            bottom: 20,
            left: '50%',
            transform: 'translateX(-50%)',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid #38bdf8',
            borderRadius: 8,
            padding: '6px 14px',
            color: '#38bdf8',
            fontSize: 12,
            fontWeight: 800,
            boxShadow: '0 8px 24px rgba(0,0,0,0.7)',
            zIndex: 30,
            pointerEvents: 'none',
          }}
        >
          <Layers size={14} />
          <span>{zToast.text}</span>
        </div>
      )}

      {/* Keyboard Shortcut Helper Hint */}
      <div
        style={{
          position: 'absolute',
          bottom: 14,
          left: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(2, 6, 23, 0.85)',
          backdropFilter: 'blur(8px)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 6,
          padding: '4px 8px',
          color: '#cbd5e1',
          fontSize: 9.5,
          pointerEvents: 'none',
        }}
      >
        <Move size={11} color="#38bdf8" /> Kéo góc hoặc giữ <b>[Shift + / -]</b> để Co Dãn • Phím <b>[+]</b> / <b>[-]</b> chỉnh Z-Index
      </div>
    </div>
  );
};
