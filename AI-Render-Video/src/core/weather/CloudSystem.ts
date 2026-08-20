import * as THREE from 'three';

/**
 * Advanced Multi-Layered Volumetric Particle Cloud System
 *
 * Features:
 * 1. Layered Fluffy 3D Sprites: 6 distinct vertical layers spaced between 65m – 120m.
 * 2. Coverage Slider Behavior: At max coverage (100%), layers form a contiguous blanket
 *    of soft, organic "mây bồng".
 * 3. Dynamic Real-Time Wind Drift: 2D incremental accumulation ensures instant reactivity
 *    to wind strength (0 to 32 m/s) and wind direction (0° to 360°) with parallax drift.
 * 4. Ultra-Smooth Simplex Atmospheric Ground Shadow: Uses isotropic Simplex noise with
 *    wide smoothstep penumbra to project silky-smooth, organic cloud shadows on terrain.
 * 5. Beer-Lambert Atmospheric Darkening: Lower layers darken realistically when covered
 *    by upper cloud decks.
 */
export class CloudSystem {
  private scene: THREE.Scene;
  private cloudGroup: THREE.Group;
  private sprites: SpriteData[] = [];
  private cloudTextures: THREE.Texture[] = [];

  // Random altitudes per layer within 65m – 120m
  private layerAltitudes: number[] = [];
  // Parallax speed multiplier per layer
  private layerSpeedMults: number[] = [1.0, 1.12, 1.25, 1.38, 1.52, 1.68];

  // Ground Shadow Mesh & Material
  private shadowMesh: THREE.Mesh | null = null;
  private shadowMaterial: THREE.ShaderMaterial | null = null;

  private animTimer = 0;
  // Accumulated 2D wind drift offsets (meters)
  private windDriftX = 0;
  private windDriftZ = 0;

  private readonly toneColors = {
    sun: new THREE.Color(),
    sky: new THREE.Color(),
    ambient: new THREE.Color(),
  };

  /** Half-extent of the cloud simulation box (meters from camera) */
  private static readonly RANGE = 500;
  /** Number of puffy sprites per layer deck for dense sky coverage */
  private static readonly SPRITES_PER_LAYER = 64;

  constructor(scene: THREE.Scene) {
    this.scene = scene;

    this.cloudGroup = new THREE.Group();
    this.cloudGroup.name = 'volumetric_clouds';
    this.cloudGroup.renderOrder = 100;
    this.scene.add(this.cloudGroup);

    this.generateLayerAltitudes();
    this.generateCloudTextures();
    this.createCloudDecks();
    this.buildDynamicShadow();
  }

  // ══════════════════════════════════════════════════
  // 1. Layer Altitude Generation (65m – 120m with jitter)
  // ══════════════════════════════════════════════════

  private generateLayerAltitudes(): void {
    let seed = 74123;
    const rng = () => {
      seed = (seed * 1103515245 + 12345) & 0x7fffffff;
      return seed / 0x7fffffff;
    };

    const minAlt = 65;
    const maxAlt = 120;
    const step = (maxAlt - minAlt) / 5; // ~11m between layers

    this.layerAltitudes = [];
    for (let i = 0; i < 6; i++) {
      const base = minAlt + i * step;
      const jitter = (rng() - 0.5) * 5.0; // +/- 2.5m jitter
      this.layerAltitudes.push(Math.max(62, Math.min(125, base + jitter)));
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
        { x: 128, y: 128, r: 85, a: 0.95 },
        { x: 92, y: 135, r: 65, a: 0.85 },
        { x: 165, y: 130, r: 68, a: 0.88 },
        { x: 110, y: 95, r: 58, a: 0.78 },
        { x: 148, y: 92, r: 62, a: 0.82 },
        { x: 75, y: 115, r: 48, a: 0.65 },
        { x: 182, y: 110, r: 50, a: 0.68 },
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
  // 3. Multi-Deck Cloud Sprites
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
      const baseAlt = this.layerAltitudes[layer] ?? (65 + layer * 11);

      for (let i = 0; i < perLayer; i++) {
        const mat = new THREE.SpriteMaterial({
          map: this.cloudTextures[(layer + i) % this.cloudTextures.length],
          transparent: true,
          opacity: 0,
          depthWrite: false,
          blending: THREE.NormalBlending,
        });

        const sprite = new THREE.Sprite(mat);

        const r = Math.sqrt(rng()) * R;
        const theta = rng() * Math.PI * 2;
        const posX = Math.cos(theta) * r;
        const posZ = Math.sin(theta) * r;
        const posY = baseAlt + (rng() - 0.5) * 8.0;

        const w = 45 + rng() * 55;
        const h = 22 + rng() * 28;

        sprite.position.set(posX, posY, posZ);
        sprite.scale.set(w, h, 1);

        const activationThreshold = (i / perLayer) * 0.85;

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
        });

        this.cloudGroup.add(sprite);
      }
    }
  }

  // ══════════════════════════════════════════════════
  // 4. Ultra-Smooth Simplex Ground Cloud Shadow
  // ══════════════════════════════════════════════════

  private buildDynamicShadow(): void {
    const geom = new THREE.PlaneGeometry(1600, 1600, 1, 1);
    this.shadowMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uWorldOffset: { value: new THREE.Vector2(0, 0) },
        uWindOffset: { value: new THREE.Vector2(0, 0) },
        uSunDir: { value: new THREE.Vector3(0.3, 0.85, 0.25).normalize() },
        uCloudAltitude: { value: 90.0 },
        uCoverage: { value: 0.5 },
        uLayerCount: { value: 3.0 },
        uShadowDarkness: { value: 0.75 },
      },
      vertexShader: /* glsl */ `
        varying vec2 vWorldXZ;
        void main() {
          vec4 worldPos = modelMatrix * vec4(position, 1.0);
          vWorldXZ = worldPos.xz;
          gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
      `,
      fragmentShader: /* glsl */ `
        precision mediump float;
        varying vec2 vWorldXZ;
        uniform vec2 uWorldOffset;
        uniform vec2 uWindOffset;
        uniform vec3 uSunDir;
        uniform float uCloudAltitude;
        uniform float uCoverage;
        uniform float uLayerCount;
        uniform float uShadowDarkness;

        // 2D Simplex Noise for Ultra-Smooth Organic Cloud Shadows
        vec3 mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        vec2 mod289(vec2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        vec3 permute(vec3 x) { return mod289(((x * 34.0) + 1.0) * x); }

        float snoise(vec2 v) {
          const vec4 C = vec4(0.211324865405187,
                              0.366025403784439,
                             -0.577350269189626,
                              0.024390243902439);
          vec2 i  = floor(v + dot(v, C.yy));
          vec2 x0 = v - i + dot(i, C.xx);
          vec2 i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
          vec4 x12 = x0.xyxy + C.xxzz;
          x12.xy -= i1;
          i = mod289(i);
          vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));
          vec3 m = max(0.5 - vec3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
          m = m * m;
          m = m * m;
          vec3 x = 2.0 * fract(p * C.www) - 1.0;
          vec3 h = abs(x) - 0.5;
          vec3 ox = floor(x + 0.5);
          vec3 a0 = x - ox;
          m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
          vec3 g;
          g.x  = a0.x  * x0.x  + h.x  * x0.y;
          g.yz = a0.yz * x12.xz + h.yz * x12.yw;
          return 130.0 * dot(m, g);
        }

        // Multi-octave organic cumulus cloud FBM
        float cloudFBM(vec2 p) {
          float f = 0.52 * (snoise(p) * 0.5 + 0.5);
          f += 0.28 * (snoise(p * 2.03 + vec2(1.7, 3.1)) * 0.5 + 0.5);
          f += 0.14 * (snoise(p * 4.08 + vec2(5.2, 2.8)) * 0.5 + 0.5);
          f += 0.06 * (snoise(p * 8.15 + vec2(8.4, 4.3)) * 0.5 + 0.5);
          return f;
        }

        void main() {
          vec2 localPos = vWorldXZ - uWorldOffset;
          float dc = length(localPos) / 750.0;
          if (dc > 1.0) discard;
          float fade = smoothstep(1.0, 0.60, dc);

          float sunY = max(uSunDir.y, 0.12);
          vec2 sunOff = (uSunDir.xz / sunY) * (uCloudAltitude - 0.08);
          vec2 cp = (vWorldXZ - sunOff) * 0.0028 + uWindOffset;

          float n1 = cloudFBM(cp * 2.8);
          float n2 = cloudFBM(cp * 3.1 + vec2(3.7, 6.1));

          float thr = 0.56 - uCoverage * 0.50;
          float d1 = smoothstep(thr, thr + 0.32, n1);
          float d2 = smoothstep(thr, thr + 0.32, n2);

          float layerMultiplier = clamp(uLayerCount / 2.0, 0.8, 2.2);
          float solidBlanket = smoothstep(0.70, 0.98, uCoverage);
          float thickness = mix((d1 * 0.58 + d2 * 0.42) * layerMultiplier, 1.0, solidBlanket) * fade;
          if (thickness < 0.005) discard;

          float shadowAlpha = clamp(thickness * uShadowDarkness * 0.52, 0.0, 0.40);
          gl_FragColor = vec4(0.03, 0.06, 0.14, shadowAlpha);
        }
      `,
      transparent: true,
      depthWrite: false,
      depthTest: true,
      polygonOffset: true,
      polygonOffsetFactor: -2,
      polygonOffsetUnits: -2,
      side: THREE.DoubleSide,
    });

    this.shadowMesh = new THREE.Mesh(geom, this.shadowMaterial);
    this.shadowMesh.rotation.x = -Math.PI / 2;
    this.shadowMesh.position.set(0, 0.05, 0);
    this.shadowMesh.name = 'dynamic_cloud_ground_shadow';
    this.shadowMesh.frustumCulled = false;
    this.shadowMesh.renderOrder = 900;
    this.scene.add(this.shadowMesh);
  }

  // ══════════════════════════════════════════════════
  // 5. Wrap Coordinate for Seamless Infinite Movement
  // ══════════════════════════════════════════════════

  private static wrapCoord(val: number, range: number): number {
    const d = range * 2;
    return ((val % d) + d + range) % d - range;
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
    layerCount: number = 3,
    sunLightPos?: THREE.Vector3,
    rainIntensity: number = 0,
    lightningFlash: number = 0,
    lightningOrigin?: THREE.Vector3,
    customRainDarkness?: number
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
      sumAlt += (this.layerAltitudes[l] ?? 70) * altitudeMult;
    }
    const avgAlt = sumAlt / activeLayers;

    // ── 3. Update Each Sprite Deck ──
    for (const sd of this.sprites) {
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
      const activation = smoothstep(sd.activationThreshold, sd.activationThreshold + 0.15, cov);

      if (activation <= 0.001 || edgeFade <= 0.001) {
        sd.sprite.visible = false;
        continue;
      }

      sd.sprite.visible = true;

      const baseOpacity = sd.baseOpacity * shape.opacityMult;
      const blanketBoost = smoothstep(0.70, 1.0, cov) * 0.35;
      const finalOpacity = Math.min(1.0, (baseOpacity + blanketBoost) * activation * edgeFade);

      const mat = sd.sprite.material as THREE.SpriteMaterial;
      mat.opacity = finalOpacity;

      const expansionProgress = smoothstep(0.40, 1.0, cov);
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
      const toSun = worldSunDir.clone();
      const dotSun = Math.max(0, toSun.dot(new THREE.Vector3(0, 1, 0)));

      const layerDepthFactor = (sd.layer / Math.max(1, activeLayers - 1));
      const beerLambertShadow = Math.exp(-layerDepthFactor * cov * 1.8);

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

    // ── 4. Ground Atmospheric Shadow Projection ──
    if (this.shadowMesh && this.shadowMaterial) {
      const isNight = skyTime === 'night';
      this.shadowMesh.visible = cov > 0.04 && !isNight;

      if (this.shadowMesh.visible) {
        this.shadowMesh.position.set(cameraPos.x, 0.05, cameraPos.z);
        const su = this.shadowMaterial.uniforms;
        su.uWorldOffset.value.set(cameraPos.x, cameraPos.z);
        su.uWindOffset.value.set(-this.windDriftX * 0.0028, -this.windDriftZ * 0.0028);
        su.uSunDir.value.copy(worldSunDir);
        su.uCloudAltitude.value = avgAlt;
        su.uCoverage.value = cov;
        su.uLayerCount.value = activeLayers;
        su.uShadowDarkness.value = 0.85;
      }
    }
  }

  private applyTones(skyTime: string): void {
    const { sun, sky } = this.toneColors;
    if (skyTime === 'overcast') {
      sun.set(0x94a3b8); sky.set(0x64748b);
    } else if (skyTime === 'sunset') {
      sun.set(0xff9a4d); sky.set(0xc87d65);
    } else if (skyTime === 'sunrise') {
      sun.set(0xffc585); sky.set(0xdbad87);
    } else if (skyTime === 'night') {
      sun.set(0x5c79a0); sky.set(0x18243e);
    } else {
      sun.set(0xffffff); sky.set(0xa0c8f0);
    }
  }

  // ══════════════════════════════════════════════════
  // Helper: Cloud Shape Profiles
  // ══════════════════════════════════════════════════

  private getShapeConfig(shapeType: string): ShapeConfig {
    switch (shapeType) {
      case 'cumulonimbus':
      case 'storm':
        return { widthMult: 1.35, heightScale: 1.6, heightMult: 1.25, opacityMult: 1.2 };
      case 'sunset_glow':
        return { widthMult: 1.2, heightScale: 0.85, heightMult: 1.0, opacityMult: 0.95 };
      case 'multi_layered':
        return { widthMult: 1.15, heightScale: 1.1, heightMult: 1.1, opacityMult: 1.05 };
      case 'cumulus':
      default:
        return { widthMult: 1.0, heightScale: 1.0, heightMult: 1.0, opacityMult: 1.0 };
    }
  }

  // ══════════════════════════════════════════════════
  // Disposal
  // ══════════════════════════════════════════════════

  public dispose(): void {
    this.scene.remove(this.cloudGroup);

    for (const sd of this.sprites) {
      (sd.sprite.material as THREE.SpriteMaterial).dispose();
    }
    this.sprites = [];
    for (const tex of this.cloudTextures) tex.dispose();
    this.cloudTextures = [];

    if (this.shadowMesh) {
      this.scene.remove(this.shadowMesh);
      this.shadowMesh.geometry.dispose();
      this.shadowMesh = null;
    }
    if (this.shadowMaterial) {
      this.shadowMaterial.dispose();
      this.shadowMaterial = null;
    }
  }
}

function smoothstep(min: number, max: number, value: number): number {
  const x = Math.max(0, Math.min(1, (value - min) / (max - min)));
  return x * x * (3 - 2 * x);
}

interface ShapeConfig {
  widthMult: number;
  heightScale: number;
  heightMult: number;
  opacityMult: number;
}

interface SpriteData {
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
}
