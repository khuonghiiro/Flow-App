import * as THREE from 'three';
import { VolumetricCloudLighting } from './VolumetricCloudLighting';

export interface SpriteData {
  sprite: THREE.Sprite;
  baseX: number;
  baseZ: number;
  baseY: number;
  scaleW: number;
  scaleH: number;
  baseOpacity: number;
  activationThreshold: number;
  windPhase: number;
  densityWeight: number;
  layer: number;
  clusterOffset: number;
}

function smoothstep(min: number, max: number, value: number): number {
  const x = Math.max(0, Math.min(1, (value - min) / (max - min)));
  return x * x * (3 - 2 * x);
}

/**
 * Advanced Multi-Layered Volumetric Cloud System
 *
 * Features:
 * 1. Stratified Random Altitudes: 6 distinct layers spanning 150m – 300m.
 * 2. Randomized Fractal Cloud Distribution: As coverage increases, random organic
 *    cloud clusters appear naturally across different quadrants of the sky.
 * 3. Expansive Skybox Radius (1500m): High-altitude clouds stretch gracefully to horizon.
 * 4. Dynamic Real-Time Wind Drift & Parallax Speeds.
 * 5. Ground Shadow Projection from 150m-300m Altitude Decks.
 */
export class CloudSystem {
  private scene: THREE.Scene;
  private cloudGroup: THREE.Group;
  private sprites: SpriteData[] = [];
  private cloudTextures: THREE.Texture[] = [];

  // Random altitudes per layer within 150m – 300m
  private layerAltitudes: number[] = [];
  // Parallax speed multiplier per layer
  private layerSpeedMults: number[] = [0.85, 0.98, 1.12, 1.28, 1.45, 1.65];

  private animTimer = 0;
  // Accumulated 2D wind drift offsets (meters)
  private windDriftX: number = 0;
  private windDriftZ: number = 0;

  private readonly toneColors = {
    sun: new THREE.Color(),
    sky: new THREE.Color(),
    ambient: new THREE.Color(),
  };

  /** Half-extent of the cloud simulation box (meters from camera) */
  private static readonly RANGE = 1500;
  /** Number of puffy sprites per layer deck for dense, rich sky coverage */
  private static readonly SPRITES_PER_LAYER = 80;

  constructor(scene: THREE.Scene) {
    this.scene = scene;

    this.cloudGroup = new THREE.Group();
    this.cloudGroup.name = 'volumetric_clouds';
    this.cloudGroup.renderOrder = 100;
    this.scene.add(this.cloudGroup);

    VolumetricCloudLighting.init();

    this.generateLayerAltitudes();
    this.generateCloudTextures();
    this.createCloudDecks();
  }

  // ══════════════════════════════════════════════════
  // 1. Layer Altitude Generation (150m – 300m with natural jitter)
  // ══════════════════════════════════════════════════

  private generateLayerAltitudes(): void {
    let seed = 74123;
    const rng = () => {
      seed = (seed * 1103515245 + 12345) & 0x7fffffff;
      return seed / 0x7fffffff;
    };

    const minAlt = 150;
    const maxAlt = 300;
    const numLayers = 6;
    const step = (maxAlt - minAlt) / (numLayers - 1); // ~30m between layer decks

    this.layerAltitudes = [];
    for (let i = 0; i < numLayers; i++) {
      const base = minAlt + i * step;
      const jitter = (rng() - 0.5) * 12.0; // +/- 6m natural jitter
      this.layerAltitudes.push(Math.max(145, Math.min(310, base + jitter)));
    }
  }

  // ══════════════════════════════════════════════════
  // 2. High-Quality Cloud Texture Generation (Soft 3D Puffs)
  // ══════════════════════════════════════════════════

  private generateCloudTextures(): void {
    for (let v = 0; v < 4; v++) {
      const s = 256;
      const canvas = document.createElement('canvas');
      canvas.width = s;
      canvas.height = s;
      const ctx = canvas.getContext('2d')!;
      ctx.clearRect(0, 0, s, s);

      const puffs: Array<{ x: number; y: number; r: number; a: number }> = [
        { x: 128, y: 128, r: 88, a: 0.95 },
        { x: 92, y: 135, r: 68, a: 0.88 },
        { x: 165, y: 130, r: 72, a: 0.90 },
        { x: 110, y: 95, r: 62, a: 0.82 },
        { x: 148, y: 92, r: 66, a: 0.85 },
        { x: 75, y: 115, r: 52, a: 0.70 },
        { x: 182, y: 110, r: 55, a: 0.72 },
        { x: 130, y: 160, r: 58, a: 0.65 },
      ];

      for (const p of puffs) {
        const rad = ctx.createRadialGradient(p.x, p.y, p.r * 0.05, p.x, p.y, p.r);
        rad.addColorStop(0, `rgba(255,255,255,${p.a})`);
        rad.addColorStop(0.35, `rgba(255,255,255,${p.a * 0.85})`);
        rad.addColorStop(0.7, `rgba(250,252,255,${p.a * 0.45})`);
        rad.addColorStop(0.9, `rgba(240,245,255,${p.a * 0.15})`);
        rad.addColorStop(1, 'rgba(230,240,255,0)');

        ctx.fillStyle = rad;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx.fill();
      }

      const tex = new THREE.CanvasTexture(canvas);
      tex.wrapS = THREE.ClampToEdgeWrapping;
      tex.wrapT = THREE.ClampToEdgeWrapping;
      this.cloudTextures.push(tex);
    }
  }

  // ══════════════════════════════════════════════════
  // 3. Multi-Deck Cloud Sprites with Random Cluster Activation
  // ══════════════════════════════════════════════════

  private createCloudDecks(): void {
    let seed = 91823;
    const rng = () => {
      seed = (seed * 1664525 + 1013904223) & 0x7fffffff;
      return seed / 0x7fffffff;
    };

    const numLayers = 6;
    const perLayer = CloudSystem.SPRITES_PER_LAYER;
    const R = CloudSystem.RANGE;

    for (let layer = 0; layer < numLayers; layer++) {
      const baseAlt = this.layerAltitudes[layer] ?? (150 + layer * 30);

      for (let i = 0; i < perLayer; i++) {
        const mat = new THREE.SpriteMaterial({
          map: this.cloudTextures[(layer + i) % this.cloudTextures.length],
          transparent: true,
          opacity: 0,
          depthWrite: false,
          blending: THREE.NormalBlending,
        });

        const sprite = new THREE.Sprite(mat);

        // Circular cloud distribution across high altitude dome
        const r = Math.sqrt(rng()) * R;
        const theta = rng() * Math.PI * 2;
        const posX = Math.cos(theta) * r;
        const posZ = Math.sin(theta) * r;
        const posY = baseAlt + (rng() - 0.5) * 16.0;

        // Proportional scale for high altitude (150m - 300m)
        const w = 150 + rng() * 180;
        const h = 70 + rng() * 80;

        sprite.position.set(posX, posY, posZ);
        sprite.scale.set(w, h, 1);

        // Randomized organic threshold: scattered clusters appear progressively
        const clusterNoise = Math.sin(posX * 0.0025) * Math.cos(posZ * 0.0025);
        const randomBase = rng() * 0.75;
        const activationThreshold = Math.max(0.02, Math.min(0.88, randomBase + clusterNoise * 0.15));

        this.sprites.push({
          sprite,
          baseX: posX,
          baseZ: posZ,
          baseY: posY,
          scaleW: w,
          scaleH: h,
          baseOpacity: 0.45 + rng() * 0.35,
          activationThreshold,
          windPhase: rng() * Math.PI * 2,
          densityWeight: 0.5 + rng() * 0.5,
          layer,
          clusterOffset: clusterNoise,
        });

        this.cloudGroup.add(sprite);
      }
    }
  }

  // ══════════════════════════════════════════════════
  // 4. Wrap Coordinate for Seamless Infinite Movement
  // ══════════════════════════════════════════════════

  private static wrapCoord(val: number, range: number): number {
    const d = range * 2;
    return ((val % d) + d + range) % d - range;
  }

  // ══════════════════════════════════════════════════
  // 5. Sky Color Palettes
  // ══════════════════════════════════════════════════

  private applyTones(skyTime: string): void {
    switch (skyTime) {
      case 'dawn':
        this.toneColors.sun.set('#ffd1a4');
        this.toneColors.sky.set('#7e6382');
        this.toneColors.ambient.set('#ffb088');
        break;
      case 'dusk':
        this.toneColors.sun.set('#ff914d');
        this.toneColors.sky.set('#4a3b69');
        this.toneColors.ambient.set('#ff6b6b');
        break;
      case 'sunset':
        this.toneColors.sun.set('#ff5722');
        this.toneColors.sky.set('#311b92');
        this.toneColors.ambient.set('#e91e63');
        break;
      case 'night':
        this.toneColors.sun.set('#1a243b');
        this.toneColors.sky.set('#060913');
        this.toneColors.ambient.set('#0e1526');
        break;
      case 'overcast':
        this.toneColors.sun.set('#a0aab8');
        this.toneColors.sky.set('#687588');
        this.toneColors.ambient.set('#526074');
        break;
      default: // noon
        this.toneColors.sun.set('#ffffff');
        this.toneColors.sky.set('#93c5fd');
        this.toneColors.ambient.set('#dbeafe');
        break;
    }
  }

  private getShapeConfig(shape: string): {
    widthMult: number;
    heightMult: number;
    heightScale: number;
    opacityMult: number;
  } {
    switch (shape) {
      case 'multi_layered':
        // Broad, stratified cloud decks with multi-tier overlapping coverage
        return { widthMult: 2.2, heightMult: 0.75, heightScale: 0.8, opacityMult: 1.05 };
      case 'sunset_glow':
        // Long, dramatic horizontal sunset clouds with vibrant glowing edges
        return { widthMult: 2.6, heightMult: 0.55, heightScale: 0.6, opacityMult: 0.92 };
      case 'cumulonimbus':
        // Towering, dramatic storm clouds
        return { widthMult: 1.35, heightMult: 1.85, heightScale: 1.7, opacityMult: 1.35 };
      case 'stratus':
        return { widthMult: 1.8, heightMult: 0.6, heightScale: 0.7, opacityMult: 0.9 };
      case 'cirrus':
        return { widthMult: 2.0, heightMult: 0.4, heightScale: 0.5, opacityMult: 0.6 };
      default: // cumulus
        // Standard fluffy puffy 3D cloud formation
        return { widthMult: 1.0, heightMult: 1.0, heightScale: 1.0, opacityMult: 1.0 };
    }
  }

  // ══════════════════════════════════════════════════
  // 6. Simulation & Update Loop
  // ══════════════════════════════════════════════════

  public update(
    delta: number,
    cameraPos: THREE.Vector3,
    coverage: number = 0.5,
    cloudShape: string = 'cumulus',
    windIntensity: number = 0.3,
    windDirectionDeg: number = 45,
    skyTime: string = 'noon',
    altitudeMult: number = 1.0,
    layerCount: number = 4,
    sunLightPos?: THREE.Vector3,
    rainIntensity: number = 0,
    lightningFlash: number = 0,
    lightningOrigin?: THREE.Vector3,
    customRainDarkness?: number,
    customShadowDarkness?: number,
    customShadowScale?: number
  ): void {
    this.animTimer += delta;

    const rain = Math.max(0, Math.min(1, rainIntensity));
    const cov = Math.max(0, Math.min(1, coverage));
    const activeLayers = Math.max(1, Math.min(6, Math.round(layerCount)));
    const R = CloudSystem.RANGE;

    // ── 1. Wind Movement: Direct 2D vector accumulation ──
    const windRad = (windDirectionDeg * Math.PI) / 180;
    const windSpeed = windIntensity * 32.0;

    this.windDriftX += Math.sin(windRad) * windSpeed * delta;
    this.windDriftZ += Math.cos(windRad) * windSpeed * delta;

    // ── 2. Atmospheric Lighting & Tone Colors ──
    this.applyTones(skyTime);
    const shape = this.getShapeConfig(cloudShape);

    const worldSunDir = sunLightPos
      ? sunLightPos.clone().normalize()
      : new THREE.Vector3(0.3, 0.85, 0.25).normalize();

    const sunColor = this.toneColors.sun;
    const skyColor = this.toneColors.sky;

    const effectiveDarkness = customRainDarkness !== undefined
      ? Math.max(0, Math.min(1, customRainDarkness))
      : (rain * 0.95);

    const rainDarknessMultiplier = 1.0 - effectiveDarkness * 0.82;
    const electricFlashColor = new THREE.Color(0xdbeafe);

    let sumAlt = 0;
    for (let l = 0; l < activeLayers; l++) {
      sumAlt += (this.layerAltitudes[l] ?? 200) * altitudeMult;
    }
    const avgAlt = sumAlt / activeLayers;

    // ── 3. Update Each Sprite Deck ──
    for (let i = 0; i < this.sprites.length; i++) {
      const sd = this.sprites[i];
      if (sd.layer >= activeLayers) {
        sd.sprite.visible = false;
        continue;
      }

      const layerSpeed = this.layerSpeedMults[sd.layer] ?? 1.0;
      const effectiveWindX = this.windDriftX * layerSpeed;
      const effectiveWindZ = this.windDriftZ * layerSpeed;

      const layerAlt = (this.layerAltitudes[sd.layer] ?? sd.baseY) * altitudeMult;
      const currentWorldX = sd.baseX + effectiveWindX;
      const currentWorldZ = sd.baseZ + effectiveWindZ;

      const relX = currentWorldX - cameraPos.x;
      const relZ = currentWorldZ - cameraPos.z;
      const wrappedRelX = CloudSystem.wrapCoord(relX, R);
      const wrappedRelZ = CloudSystem.wrapCoord(relZ, R);

      const localX = wrappedRelX;
      const localZ = wrappedRelZ;
      const localY = (sd.baseY - (this.layerAltitudes[sd.layer] ?? sd.baseY)) + (layerAlt - avgAlt);

      sd.sprite.position.set(localX, localY, localZ);

      const distFromCenter = Math.sqrt(localX * localX + localZ * localZ);
      if (distFromCenter > R * 0.98) {
        sd.sprite.visible = false;
        continue;
      }

      const edgeFade = smoothstep(R * 0.98, R * 0.70, distFromCenter);
      // Dynamic random activation: as slider increases, more random clusters awaken
      const activation = smoothstep(sd.activationThreshold, sd.activationThreshold + 0.16, cov);

      if (activation <= 0.001 || edgeFade <= 0.001) {
        sd.sprite.visible = false;
        continue;
      }

      sd.sprite.visible = true;

      const baseOpacity = sd.baseOpacity * shape.opacityMult;
      const blanketBoost = smoothstep(0.65, 1.0, cov) * 0.35;
      const finalOpacity = Math.min(1.0, (baseOpacity + blanketBoost) * activation * edgeFade);

      const mat = sd.sprite.material as THREE.SpriteMaterial;
      mat.opacity = finalOpacity;

      const expansionProgress = smoothstep(0.35, 1.0, cov);
      const coverageWidthMult = 1.0 + expansionProgress * 0.65;
      const coverageHeightMult = 1.0 + expansionProgress * 0.40;

      const curW = sd.scaleW * shape.widthMult * coverageWidthMult;
      const curH = sd.scaleH * shape.heightMult * shape.heightScale * coverageHeightMult;
      sd.sprite.scale.set(curW, curH, 1);
      
      const spriteWorldPos = new THREE.Vector3(
        cameraPos.x + localX,
        avgAlt + localY,
        cameraPos.z + localZ
      );

      const dotSun = Math.max(0, spriteWorldPos.clone().sub(cameraPos).normalize().dot(worldSunDir));
      
      const beerLambertShadow = smoothstep(0, 0.4, sd.densityWeight * (1.0 - effectiveDarkness));
      const litColor = sunColor.clone().multiplyScalar(0.85 + dotSun * 0.35);
      const shadeColor = skyColor.clone().multiplyScalar(0.45);
      let finalColor = shadeColor.lerp(litColor, beerLambertShadow);

      if (effectiveDarkness > 0.01) {
        const stormGrey = new THREE.Color(0x0c0f16);
        finalColor = finalColor.lerp(stormGrey, effectiveDarkness * 0.92);
      }
      finalColor = finalColor.multiplyScalar(rainDarknessMultiplier);

      if (lightningFlash > 0.01) {
        let flashFactor = lightningFlash;
        if (lightningOrigin) {
          const distToLightning = Math.sqrt(
            Math.pow(localX - (lightningOrigin.x - cameraPos.x), 2) +
            Math.pow(localZ - (lightningOrigin.z - cameraPos.z), 2)
          );
          const spatialFalloff = 1.0 - smoothstep(20.0, 180.0, distToLightning);
          flashFactor = lightningFlash * (0.45 + spatialFalloff * 0.55);
        }
        finalColor = finalColor.lerp(electricFlashColor, flashFactor * 0.95);
        mat.opacity = Math.min(1.0, mat.opacity + flashFactor * 0.2);
      }

      mat.color.copy(finalColor);
    }

    this.cloudGroup.position.set(cameraPos.x, avgAlt, cameraPos.z);
    this.cloudGroup.visible = cov > 0.01;

    // ── 4. Volumetric Physical Beer-Lambert Light Extinction on ALL 3D Scene Geometry ──
    const isNight = skyTime === 'night';
    const defaultDarkness = customRainDarkness !== undefined
      ? Math.max(0.70, 0.75 + effectiveDarkness * 0.22)
      : (skyTime === 'overcast' ? 0.90 : 0.82);

    const shadowDarkness = customShadowDarkness !== undefined ? customShadowDarkness : defaultDarkness;
    const shadowScale = customShadowScale !== undefined ? customShadowScale : 1.0;

    VolumetricCloudLighting.update({
      sunDirection: worldSunDir,
      coverage: cov,
      altitude: avgAlt,
      shadowDarkness,
      shadowScale,
      centerXZ: new THREE.Vector2(cameraPos.x, cameraPos.z),
      windOffset: new THREE.Vector2(this.windDriftX, this.windDriftZ),
      time: this.animTimer,
      scene: this.scene,
    });
  }

  public dispose(): void {
    for (const sd of this.sprites) {
      sd.sprite.geometry.dispose();
      (sd.sprite.material as THREE.Material).dispose();
    }
    for (const tex of this.cloudTextures) {
      tex.dispose();
    }
    if (this.cloudGroup.parent) {
      this.cloudGroup.parent.remove(this.cloudGroup);
    }
  }
}
