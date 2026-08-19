import * as THREE from 'three';
import { GifOverlayTrackItem } from '../../types/scene';

interface ActiveGifOverlay {
  id: string;
  config: GifOverlayTrackItem;
  mesh: THREE.Object3D;
  parentObj: THREE.Object3D | null;
  startTime: number;
  endTime: number;
}

/**
 * GifOverlayManager
 * Manages 2D animated GIF overlays, anime emote stickers (sweat drop, anger vein, sparkles),
 * and screen-space speed lines for 2D/3D hybrid animation.
 */
export class GifOverlayManager {
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private activeOverlays: Map<string, ActiveGifOverlay> = new Map();
  private textureLoader: THREE.TextureLoader = new THREE.TextureLoader();
  private textureCache: Map<string, THREE.Texture> = new Map();

  constructor(scene: THREE.Scene, camera: THREE.PerspectiveCamera) {
    this.scene = scene;
    this.camera = camera;
  }

  /**
   * Evaluate GIF overlays for an actor or screen at currentTime
   */
  public evaluateActorOverlays(
    actorId: string,
    tracks: GifOverlayTrackItem[] | undefined,
    actorRoot: THREE.Object3D,
    currentTime: number
  ): void {
    if (!tracks || tracks.length === 0) return;

    for (let i = 0; i < tracks.length; i++) {
      const track = tracks[i];
      const key = `${actorId}_gif_${i}_${track.start}`;

      const isActive = currentTime >= track.start && currentTime <= track.end;
      const existing = this.activeOverlays.get(key);

      if (isActive && !existing) {
        this.spawnOverlay(key, track, actorRoot);
      } else if (!isActive && existing) {
        this.removeOverlay(key);
      } else if (isActive && existing) {
        this.updateOverlay(existing, currentTime);
      }
    }
  }

  /**
   * Spawn a GIF / animated emote billboard
   */
  private spawnOverlay(
    key: string,
    config: GifOverlayTrackItem,
    actorRoot: THREE.Object3D
  ): void {
    let parentObj: THREE.Object3D = actorRoot;

    if (config.attach_to === 'head') {
      const head = actorRoot.getObjectByName('head') || actorRoot;
      parentObj = head;
    } else if (config.attach_to === 'weapon_r') {
      const weapon = actorRoot.getObjectByName('weapon_r') || actorRoot;
      parentObj = weapon;
    }

    // Create billboard sprite or screen quad
    const sprite = this.createEmoteSprite(config);

    const offset = config.offset || (config.attach_to === 'head' ? [0, 0.45, 0] : [0, 0, 0]);
    sprite.position.set(...offset);

    parentObj.add(sprite);

    this.activeOverlays.set(key, {
      id: key,
      config,
      mesh: sprite,
      parentObj,
      startTime: config.start,
      endTime: config.end,
    });
  }

  /**
   * Create an emote sprite billboard (with procedural fallback canvas if texture loading)
   */
  private createEmoteSprite(config: GifOverlayTrackItem): THREE.Sprite {
    const canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 128;
    const ctx = canvas.getContext('2d');

    if (ctx) {
      this.drawProceduralEmote(ctx, config.gif_path);
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;

    const mat = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      blending: config.blend_mode === 'additive' ? THREE.AdditiveBlending : THREE.NormalBlending,
      depthTest: config.attach_to !== 'screen_overlay',
    });

    const sprite = new THREE.Sprite(mat);
    const scale = typeof config.scale === 'number' ? config.scale : 0.5;
    sprite.scale.set(scale, scale, 1);
    sprite.name = `gif_overlay_${config.gif_path}`;

    return sprite;
  }

  /**
   * Draw anime procedural emote if GIF is rendering (e.g. sweat drop, anger vein, sparkles)
   */
  private drawProceduralEmote(ctx: CanvasRenderingContext2D, path: string): void {
    ctx.clearRect(0, 0, 128, 128);
    const p = path.toLowerCase();

    if (p.includes('sweat') || p.includes('water')) {
      // 💧 Anime Blue Sweat Drop
      ctx.fillStyle = '#38bdf8';
      ctx.beginPath();
      ctx.moveTo(64, 20);
      ctx.bezierCurveTo(30, 70, 35, 110, 64, 110);
      ctx.bezierCurveTo(93, 110, 98, 70, 64, 20);
      ctx.fill();
      ctx.fillStyle = '#ffffff';
      ctx.beginPath();
      ctx.arc(52, 70, 8, 0, Math.PI * 2);
      ctx.fill();
    } else if (p.includes('anger') || p.includes('vein') || p.includes('rage')) {
      // 💢 Anime Red Anger Cross Vein
      ctx.strokeStyle = '#ef4444';
      ctx.lineWidth = 14;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.arc(44, 44, 24, 0, Math.PI * 0.5);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(84, 44, 24, Math.PI * 0.5, Math.PI);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(84, 84, 24, Math.PI, Math.PI * 1.5);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(44, 84, 24, Math.PI * 1.5, Math.PI * 2);
      ctx.stroke();
    } else if (p.includes('sparkle') || p.includes('star')) {
      // ✨ Sparkles
      ctx.fillStyle = '#fbbf24';
      ctx.beginPath();
      ctx.moveTo(64, 15);
      ctx.quadraticCurveTo(64, 64, 113, 64);
      ctx.quadraticCurveTo(64, 64, 64, 113);
      ctx.quadraticCurveTo(64, 64, 15, 64);
      ctx.quadraticCurveTo(64, 64, 64, 15);
      ctx.fill();
    } else if (p.includes('question')) {
      // ❓ Question Mark
      ctx.fillStyle = '#a855f7';
      ctx.font = 'bold 84px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('?', 64, 96);
    } else {
      // Default anime sparkle burst
      ctx.fillStyle = '#e2e8f0';
      ctx.beginPath();
      ctx.arc(64, 64, 30, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  /**
   * Update active overlay animation (subtle breathing/bobbing pulse)
   */
  private updateOverlay(overlay: ActiveGifOverlay, time: number): void {
    const elapsed = time - overlay.startTime;
    const pulse = 1 + Math.sin(elapsed * 12) * 0.08;
    const baseScale = typeof overlay.config.scale === 'number' ? overlay.config.scale : 0.5;
    overlay.mesh.scale.set(baseScale * pulse, baseScale * pulse, 1);
  }

  /**
   * Remove an active overlay
   */
  private removeOverlay(key: string): void {
    const overlay = this.activeOverlays.get(key);
    if (overlay) {
      if (overlay.parentObj) {
        overlay.parentObj.remove(overlay.mesh);
      } else {
        this.scene.remove(overlay.mesh);
      }
      this.activeOverlays.delete(key);
    }
  }

  /**
   * Clean up all overlays
   */
  public reset(): void {
    for (const [key] of this.activeOverlays) {
      this.removeOverlay(key);
    }
    this.activeOverlays.clear();
  }
}
