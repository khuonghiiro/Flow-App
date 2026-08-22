import {
  Character2DAssembly,
  Character2DPartType,
  Map2DPreset,
  Camera2DShotType,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../assets/Asset2DRegistry';

export interface PuppetAnimationState {
  mode: 'idle' | 'breathe' | 'talk' | 'combat_slash' | 'walk' | 'shocked';
  time: number;
  isBlinking: boolean;
  isTalking: boolean;
  slashProgress: number;          // 0 to 1
  shakeOffset: [number, number];  // [dx, dy]
  zoomFactor: number;
}

export class Canvas2DPuppetEngine {
  private imageCache: Map<string, HTMLImageElement> = new Map();
  private pendingLoads: Map<string, Promise<HTMLImageElement>> = new Map();

  /**
   * Loads and caches an image URL (PNG, SVG data URL, WebP, etc.)
   */
  public async preloadImage(url: string): Promise<HTMLImageElement> {
    if (!url) throw new Error('Empty URL');
    if (this.imageCache.has(url)) {
      return this.imageCache.get(url)!;
    }
    if (this.pendingLoads.has(url)) {
      return this.pendingLoads.get(url)!;
    }

    const promise = new Promise<HTMLImageElement>((resolve, reject) => {
      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => {
        this.imageCache.set(url, img);
        this.pendingLoads.delete(url);
        resolve(img);
      };
      img.onerror = (err) => {
        this.pendingLoads.delete(url);
        reject(err);
      };
      img.src = url;
    });

    this.pendingLoads.set(url, promise);
    return promise;
  }

  /**
   * Preloads all image parts of a character assembly
   */
  public async preloadAssembly(assembly: Character2DAssembly): Promise<void> {
    const urls: string[] = [];
    for (const part of Object.values(assembly.parts)) {
      if (part?.path) urls.push(part.path);
      if (part?.states) {
        for (const s of Object.values(part.states)) {
          if (s) urls.push(s);
        }
      }
    }
    await Promise.all(urls.map((u) => this.preloadImage(u).catch(() => null)));
  }

  /**
   * Renders the 2D Character Puppet onto the given Canvas 2D context
   */
  public renderCharacter(
    ctx: CanvasRenderingContext2D,
    assembly: Character2DAssembly,
    animState: PuppetAnimationState,
    centerX: number,
    centerY: number,
    renderScale = 1.0,
    selectedSlot: Character2DPartType | null = null
  ): void {
    ctx.save();
    ctx.translate(centerX, centerY);

    // Calculate procedural joint oscillations based on animation mode
    const t = animState.time;
    const breatheY = animState.mode === 'breathe' || animState.mode === 'idle' ? Math.sin(t * 3) * 4 : 0;
    const breatheHeadRot = animState.mode === 'breathe' ? Math.sin(t * 3) * 1.5 : 0;
    const breatheArmRot = animState.mode === 'breathe' ? Math.sin(t * 3) * 3 : 0;

    // Combat slash keyframe calculations
    let slashArmAngle = 0;
    let slashWeaponAngle = 0;
    if (animState.mode === 'combat_slash') {
      const p = animState.slashProgress;
      if (p < 0.3) {
        // Wind-up
        slashArmAngle = -(p / 0.3) * 45;
        slashWeaponAngle = -(p / 0.3) * 60;
      } else if (p < 0.6) {
        // Fast forward slash
        const slashP = (p - 0.3) / 0.3;
        slashArmAngle = -45 + slashP * 120;
        slashWeaponAngle = -60 + slashP * 150;
      } else {
        // Recover
        const recP = (p - 0.6) / 0.4;
        slashArmAngle = 75 - recP * 75;
        slashWeaponAngle = 90 - recP * 90;
      }
    }

    // Sort parts by Z-Index
    const sortedEntries = (Object.entries(assembly.parts) as [Character2DPartType, any][])
      .filter(([_, config]) => Boolean(config?.path))
      .sort((a, b) => (a[1].z_index || 0) - (b[1].z_index || 0));

    for (const [slot, part] of sortedEntries) {
      this.drawSinglePart(
        ctx,
        slot,
        part,
        animState,
        renderScale * (assembly.base_scale || 1.0),
        breatheY,
        breatheHeadRot,
        breatheArmRot,
        slashArmAngle,
        slashWeaponAngle,
        slot === selectedSlot
      );
    }

    // Draw slash trail VFX if in combat slash
    if (animState.mode === 'combat_slash' && animState.slashProgress > 0.25 && animState.slashProgress < 0.65) {
      this.drawSlashVfxTrail(ctx, animState.slashProgress);
    }

    ctx.restore();
  }

  /**
   * Draws a single puppet part with proper pivot, offsets, transforms, and state swaps
   */
  private drawSinglePart(
    ctx: CanvasRenderingContext2D,
    slot: Character2DPartType,
    part: any,
    animState: PuppetAnimationState,
    scaleFactor: number,
    breatheY: number,
    breatheHeadRot: number,
    breatheArmRot: number,
    slashArmAngle: number,
    slashWeaponAngle: number,
    isSelected: boolean
  ): void {
    let imgUrl = part.path;

    // Dynamic state switching (Blinking eyes, Talking mouth)
    if (slot === 'mat' && animState.isBlinking && part.states?.blink) {
      imgUrl = part.states.blink;
    }
    if (slot === 'mieng' && animState.isTalking && part.states?.talk) {
      imgUrl = part.states.talk;
    }

    const img = this.imageCache.get(imgUrl);
    if (!img || !img.complete) {
      this.preloadImage(imgUrl).catch(() => null);
      return;
    }

    ctx.save();

    // Procedural hierarchy animations
    let dynamicY = (part.offset?.[0] ? 0 : 0);
    let dynamicRot = part.rotation || 0;

    if (slot === 'dau' || slot === 'mat' || slot === 'mui' || slot === 'mieng' || slot === 'toc_truoc') {
      dynamicY += breatheY * 0.7;
      dynamicRot += breatheHeadRot;
    } else if (slot === 'than_co_ban' || slot === 'trang_phuc') {
      dynamicY += breatheY * 0.4;
    } else if (slot === 'canh_tay_trai' || slot === 'cang_tay_trai') {
      dynamicRot += breatheArmRot;
    } else if (slot === 'canh_tay_phai' || slot === 'cang_tay_phai') {
      dynamicRot += -breatheArmRot + slashArmAngle;
    } else if (slot === 'vu_khi') {
      dynamicRot += -breatheArmRot + slashWeaponAngle;
      dynamicY += breatheY * 0.3;
    }

    const posX = (part.offset?.[0] || 0) * scaleFactor;
    const posY = ((part.offset?.[1] || 0) + dynamicY) * scaleFactor;

    ctx.translate(posX, posY);
    ctx.rotate((dynamicRot * Math.PI) / 180);

    const sx = (part.scale?.[0] ?? 1) * (part.flipX ? -1 : 1) * scaleFactor;
    const sy = (part.scale?.[1] ?? 1) * (part.flipY ? -1 : 1) * scaleFactor;
    ctx.scale(sx, sy);
    ctx.globalAlpha = part.opacity ?? 1;

    // Pivot point offset
    const pivot = part.pivot || PART_HIERARCHY_CONFIG[slot]?.defaultPivot || [0.5, 0.5];
    const drawW = img.width || 100;
    const drawH = img.height || 100;
    const originX = -drawW * pivot[0];
    const originY = -drawH * pivot[1];

    ctx.drawImage(img, originX, originY, drawW, drawH);

    // If selected, draw bounding wireframe box & pivot anchor indicator
    if (isSelected) {
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2 / Math.abs(scaleFactor);
      ctx.strokeRect(originX, originY, drawW, drawH);

      // Pivot point dot
      ctx.fillStyle = '#ef4444';
      ctx.beginPath();
      ctx.arc(0, 0, 4 / Math.abs(scaleFactor), 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = '#ffffff';
      ctx.stroke();
    }

    ctx.restore();
  }

  /**
   * Draws a dynamic combat slash curve trail VFX
   */
  private drawSlashVfxTrail(ctx: CanvasRenderingContext2D, progress: number): void {
    ctx.save();
    ctx.strokeStyle = '#38bdf8';
    ctx.lineWidth = 12;
    ctx.lineCap = 'round';
    ctx.shadowColor = '#00f0ff';
    ctx.shadowBlur = 20;

    const startAngle = -Math.PI * 0.8;
    const endAngle = startAngle + (progress - 0.25) * 4 * Math.PI;

    ctx.beginPath();
    ctx.arc(40, -40, 180, startAngle, endAngle);
    ctx.stroke();

    // Inner bright arc
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 4;
    ctx.beginPath();
    ctx.arc(40, -40, 180, startAngle, endAngle);
    ctx.stroke();

    ctx.restore();
  }

  /**
   * Renders the Parallax Map background and layers
   */
  public renderMap(
    ctx: CanvasRenderingContext2D,
    mapPreset: Map2DPreset,
    cameraX: number,
    cameraY: number,
    cameraZoom: number,
    viewW: number,
    viewH: number,
    animTime: number
  ): void {
    ctx.save();

    // Apply Camera transforms & Zoom
    ctx.translate(viewW / 2, viewH / 2);
    ctx.scale(cameraZoom, cameraZoom);
    ctx.translate(-viewW / 2, -viewH / 2);

    // Sort map layers by Z-Index
    const layers = [...mapPreset.layers].sort((a, b) => a.z_index - b.z_index);

    for (const layer of layers) {
      const img = this.imageCache.get(layer.path);
      if (!img || !img.complete) {
        this.preloadImage(layer.path).catch(() => null);
        continue;
      }

      ctx.save();
      const parallaxFactor = layer.parallax_factor ?? 1.0;
      const scrollX = (layer.scroll_speed_x || 0) * animTime;
      const scrollY = (layer.scroll_speed_y || 0) * animTime;

      const layerX = (layer.offset[0] - cameraX * parallaxFactor + scrollX) % (viewW || 1920);
      const layerY = layer.offset[1] - cameraY * parallaxFactor + scrollY;

      ctx.globalAlpha = layer.opacity ?? 1;
      if (layer.blend_mode && layer.blend_mode !== 'normal') {
        ctx.globalCompositeOperation = layer.blend_mode;
      }

      // Draw primary tile & adjacent tile if scrolling
      ctx.drawImage(img, layerX, layerY, viewW, viewH);
      if (scrollX > 0) {
        ctx.drawImage(img, layerX - viewW, layerY, viewW, viewH);
      } else if (scrollX < 0) {
        ctx.drawImage(img, layerX + viewW, layerY, viewW, viewH);
      }

      ctx.restore();
    }

    // Atmosphere Lighting Tint
    if (mapPreset.atmosphere.lighting_tint) {
      ctx.fillStyle = mapPreset.atmosphere.lighting_tint;
      ctx.globalAlpha = 0.12;
      ctx.fillRect(0, 0, viewW, viewH);
    }

    ctx.restore();
  }
}
