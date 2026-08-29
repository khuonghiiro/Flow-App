// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import {
  Director2DProject,
  MultiAngleDirectorShot,
  STANDARD_8_ANGLES,
  TOP_DOWN_ANGLES,
  StandardHorizontalAngle,
  ScenePropItem,
} from '../../../../../types/studio2d_director';
import { ActiveBBoxInfo, Particle } from './stage2dTypes';

export function resolveAngleSprite(
  project: Director2DProject,
  actorId: string,
  worldFacingAngle: number,
  camAngle: number,
  pitch: number
): { url: string; flipX: boolean } {
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
}

export function renderBackgroundLayers(
  ctx: CanvasRenderingContext2D,
  project: Director2DProject,
  w: number,
  h: number,
  currentScenePanX: number,
  currentScenePanY: number,
  getImage: (url: string) => HTMLImageElement | null
) {
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
}

export function renderGroundGrid(ctx: CanvasRenderingContext2D, animatedPitch: number) {
  if (animatedPitch <= 25) return;
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

interface RenderStageEntitiesParams {
  ctx: CanvasRenderingContext2D;
  project: Director2DProject;
  activeShot: MultiAngleDirectorShot;
  currentTime?: number;
  shotProgress: number;
  smoothP: number;
  rawP: number;
  nowSec: number;
  isPlaying: boolean;
  animatedCamAngle: number;
  animatedPitch: number;
  currentScenePanX: number;
  selectedActorId: string | null;
  selectedPropId?: string | null;
  getImage: (url: string) => HTMLImageElement | null;
}

export function renderStageEntities({
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
}: RenderStageEntitiesParams): ActiveBBoxInfo | null {
  type RenderItem =
    | { type: 'actor'; zIndex: number; data: any }
    | { type: 'prop'; zIndex: number; data: ScenePropItem };

  const renderList: RenderItem[] = [];
  const currentEvalTime = currentTime ?? (shotProgress * (activeShot?.durationSeconds || 3));

  // Filter by Visibility Window
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
  let activeBBox: ActiveBBoxInfo | null = null;

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

        // Interactive Bounding Box for Prop
        if (selectedPropId === prop.id) {
          const m = ctx.getTransform();
          const pTL = m.transformPoint(new DOMPoint(-pw / 2 - 6, -ph / 2 - 6));
          const pTR = m.transformPoint(new DOMPoint(pw / 2 + 6, -ph / 2 - 6));
          const pBL = m.transformPoint(new DOMPoint(-pw / 2 - 6, ph / 2 + 6));
          const pBR = m.transformPoint(new DOMPoint(pw / 2 + 6, ph / 2 + 6));
          const pC = m.transformPoint(new DOMPoint(0, 0));
          const pRot = m.transformPoint(new DOMPoint(0, -ph / 2 - 28));

          activeBBox = {
            type: 'prop',
            id: prop.id,
            centerX: pC.x,
            centerY: pC.y,
            initDist: Math.hypot(pTR.x - pC.x, pTR.y - pC.y),
            initScale: activeProp.scale[0],
            initRotation: activeProp.rotation || 0,
            corners: [
              { x: pTL.x, y: pTL.y, cursor: 'nwse-resize' },
              { x: pTR.x, y: pTR.y, cursor: 'nesw-resize' },
              { x: pBL.x, y: pBL.y, cursor: 'nesw-resize' },
              { x: pBR.x, y: pBR.y, cursor: 'nwse-resize' },
            ],
            rotateHandle: { x: pRot.x, y: pRot.y },
          };

          ctx.strokeStyle = '#4ade80';
          ctx.lineWidth = 1.5;
          ctx.setLineDash([5, 5]);
          ctx.strokeRect(-pw / 2 - 6, -ph / 2 - 6, pw + 12, ph + 12);
          ctx.setLineDash([]);

          // 4 Corner Handles
          ctx.fillStyle = '#4ade80';
          ctx.fillRect(-pw / 2 - 10, -ph / 2 - 10, 8, 8);
          ctx.fillRect(pw / 2 + 2, -ph / 2 - 10, 8, 8);
          ctx.fillRect(-pw / 2 - 10, ph / 2 + 2, 8, 8);
          ctx.fillRect(pw / 2 + 2, ph / 2 + 2, 8, 8);

          // Rotate Handle Pin
          ctx.beginPath();
          ctx.moveTo(0, -ph / 2 - 6);
          ctx.lineTo(0, -ph / 2 - 28);
          ctx.strokeStyle = '#4ade80';
          ctx.lineWidth = 1.5;
          ctx.stroke();

          ctx.beginPath();
          ctx.arc(0, -ph / 2 - 28, 5.5, 0, Math.PI * 2);
          ctx.fillStyle = '#4ade80';
          ctx.fill();
          ctx.strokeStyle = '#0f172a';
          ctx.lineWidth = 1.5;
          ctx.stroke();
        }
      }
      ctx.restore();
    } else {
      // Actor Rendering
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
      const actorRot = ((actState.rotation || 0) * Math.PI) / 180;
      ctx.rotate(actorRot + animRot);

      const totalScale = (actorProf.baseScale || 1.0) * (actState.scale || 1.0);

      const { url, flipX } = resolveAngleSprite(
        project,
        actState.actorId,
        actState.worldFacingAngle,
        animatedCamAngle,
        animatedPitch
      );
      let spriteImg = getImage(url);
      if (!spriteImg || !spriteImg.complete) {
        // Fallback to front sprite or first available complete sprite so canvas never flashes blank
        const fallbackUrl = actorProf.sprites.front || Object.values(actorProf.sprites)[0] || '';
        if (fallbackUrl) {
          const fallbackImg = getImage(fallbackUrl);
          if (fallbackImg && fallbackImg.complete) {
            spriteImg = fallbackImg;
          }
        }
      }

      if (spriteImg && spriteImg.complete) {
        const sw = (spriteImg.width || 200) * totalScale * 0.45;
        const sh = (spriteImg.height || 300) * totalScale * 0.45;

        const finalFlipX = !!(flipX !== !!actState.flipX);
        ctx.save();
        if (finalFlipX) ctx.scale(-1, 1);
        ctx.drawImage(spriteImg, -sw / 2, -sh + 20, sw, sh);
        ctx.restore();

        // Interactive Bounding Box for Actor
        if (selectedActorId === actState.actorId) {
          const m = ctx.getTransform();
          const pTL = m.transformPoint(new DOMPoint(-sw / 2 - 6, -sh + 14));
          const pTR = m.transformPoint(new DOMPoint(sw / 2 + 6, -sh + 14));
          const pBL = m.transformPoint(new DOMPoint(-sw / 2 - 6, 26));
          const pBR = m.transformPoint(new DOMPoint(sw / 2 + 6, 26));
          const pC = m.transformPoint(new DOMPoint(0, -sh / 2 + 20));
          const pRot = m.transformPoint(new DOMPoint(0, -sh - 18));

          activeBBox = {
            type: 'actor',
            id: actState.actorId,
            centerX: pC.x,
            centerY: pC.y,
            initDist: Math.hypot(pTR.x - pC.x, pTR.y - pC.y),
            initScale: actState.scale || 1.6,
            initRotation: actState.rotation || 0,
            corners: [
              { x: pTL.x, y: pTL.y, cursor: 'nwse-resize' },
              { x: pTR.x, y: pTR.y, cursor: 'nesw-resize' },
              { x: pBL.x, y: pBL.y, cursor: 'nesw-resize' },
              { x: pBR.x, y: pBR.y, cursor: 'nwse-resize' },
            ],
            rotateHandle: { x: pRot.x, y: pRot.y },
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

          // Rotate Handle Pin
          ctx.beginPath();
          ctx.moveTo(0, -sh + 14);
          ctx.lineTo(0, -sh - 18);
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 1.5;
          ctx.stroke();

          ctx.beginPath();
          ctx.arc(0, -sh - 18, 5.5, 0, Math.PI * 2);
          ctx.fillStyle = '#38bdf8';
          ctx.fill();
          ctx.strokeStyle = '#0f172a';
          ctx.lineWidth = 1.5;
          ctx.stroke();
        }
      }
      ctx.restore();
    }
  }

  return activeBBox;
}

export function renderParticles(
  ctx: CanvasRenderingContext2D,
  particles: Particle[],
  isPlaying: boolean
) {
  for (const p of particles) {
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
}
