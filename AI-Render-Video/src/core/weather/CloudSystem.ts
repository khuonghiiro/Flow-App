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

      let seed = v * 8191 + 57;
      const rng = () => {
        seed = (seed * 1103515245 + 12345) & 0x7fffffff;
        return seed / 0x7fffffff;
      };

      const cx = s / 2;
      const cy = s / 2;

      // Outer smooth atmospheric envelope
      const baseGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, s * 0.46);
      baseGrad.addColorStop(0, 'rgba(255,255,255,0.85)');
      baseGrad.addColorStop(0.28, 'rgba(255,255,255,0.65)');
      baseGrad.addColorStop(0.55, 'rgba(255,255,255,0.25)');
      baseGrad.addColorStop(0.85, 'rgba(255,255,255,0.05)');
      baseGrad.addColorStop(1.0, 'rgba(255,255,255,0.0)');
      ctx.fillStyle = baseGrad;
      ctx.fillRect(0, 0, s, s);

      // Multiple dense overlapping organic lobes ("mây bồng")
      const puffCount = 8 + v * 3;
      for (let i = 0; i < puffCount; i++) {
        const px = cx + (rng() - 0.5) * s * 0.48;
        const py = cy + (rng() - 0.5) * s * 0.32;
        const r = s * (0.12 + rng() * 0.18);
        const alpha = 0.35 + rng() * 0.35;

        const puffGrad = ctx.createRadialGradient(px, py, 0, px, py, r);
        puffGrad.addColorStop(0, `rgba(255,255,255,${alpha})`);
        puffGrad.addColorStop(0.45, `rgba(255,255,255,${alpha * 0.5})`);
        puffGrad.addColorStop(1, 'rgba(255,255,255,0.0)');
        ctx.fillStyle = puffGrad;
        ctx.beginPath();
        ctx.arc(px, py, r, 0, Math.PI * 2);
        ctx.fill();
      }

      const tex = new THREE.CanvasTexture(canvas);
      tex.needsUpdate = true;
      this.cloudTextures.push(tex);
    }
  }

  // ══════════════════════════════════════════════════
  // 3. Cloud Deck Creation (Each Layer is a Full Sky Blanket)
  // ══════════════════════════════════════════════════

  private createCloudDecks(): void {
    let seed = 31415;
    const rng = () => {
      seed = (seed * 1103515245 + 12345) & 0x7fffffff;
      return seed / 0x7fffffff;
    };

    const R = CloudSystem.RANGE;
    const perLayer = CloudSystem.SPRITES_PER_LAYER;
    const gridSize = Math.ceil(Math.sqrt(perLayer)); // 8x8 grid
    const cellWidth = (R * 2) / gridSize;

    for (let layer = 0; layer < 6; layer++) {
      for (let i = 0; i < perLayer; i++) {
        const gx = i % gridSize;
        const gz = Math.floor(i / gridSize);

        // Jittered grid placement guarantees uniform distribution with organic randomness
        const posX = -R + (gx + 0.5) * cellWidth + (rng() - 0.5) * cellWidth * 0.85;
        const posZ = -R + (gz + 0.5) * cellWidth + (rng() - 0.5) * cellWidth * 0.85;
        const posY = (rng() - 0.5) * 6.0; // Local layer variation

        // Randomly choose one of the soft cloud puff textures
        const texIdx = Math.floor(rng() * this.cloudTextures.length);
        const mat = new THREE.SpriteMaterial({
          map: this.cloudTextures[texIdx],
          transparent: true,
          opacity: 0,
          depthWrite: false,
          sizeAttenuation: true,
        });

        const sprite = new THREE.Sprite(mat);
        sprite.position.set(posX, posY, posZ);

        // Large puffy clouds that easily overlap and create solid blankets at 100%
        const w = 140 + rng() * 160; // 140m – 300m width
        const h = 45 + rng() * 60;   // 45m – 105m height
        sprite.scale.set(w, h, 1);

        // Activation threshold: lower threshold = cloud appears earlier as coverage slider increases
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

        // ── 2D Simplex Noise (Zero grid artifacts, perfectly smooth curves) ──
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
          // Smooth radial fade to edge
          float fade = smoothstep(1.0, 0.60, dc);

          // Sunlight projection offset
          float sunY = max(uSunDir.y, 0.12);
          vec2 sunOff = (uSunDir.xz / sunY) * (uCloudAltitude - 0.08);
          // Coordinates moving synchronously with wind
          vec2 cp = (vWorldXZ - sunOff) * 0.0028 + uWindOffset;

          // Multi-layer organic cloud density sample
          float n1 = cloudFBM(cp * 2.8);
          float n2 = cloudFBM(cp * 3.1 + vec2(3.7, 6.1));

          // Soft feathered threshold (at max coverage/rain, coverage expands to 100% full blanket)
          float thr = 0.56 - uCoverage * 0.50;
          float d1 = smoothstep(thr, thr + 0.32, n1);
          float d2 = smoothstep(thr, thr + 0.32, n2);

          float layerMultiplier = clamp(uLayerCount / 2.0, 0.8, 2.2);
          // Solid blanket factor when coverage is near 100%
          float solidBlanket = smoothstep(0.70, 0.98, uCoverage);
          float thickness = mix((d1 * 0.58 + d2 * 0.42) * layerMultiplier, 1.0, solidBlanket) * fade;
          if (thickness < 0.005) discard;

          // Natural atmospheric diffuse shadow opacity (30% to 40% contrast, perfectly balanced with 3D models)
          float shadowAlpha = clamp(thickness * uShadowDarkness * 0.52, 0.0, 0.40);

          // Soft atmospheric cool blue-slate shadow tone
          gl_FragColor = vec4(0.03, 0.06, 0.14, shadowAlpha);
        }
      `,
      transparent: true,
      depthWrite: false,
      depthTest: true,
      polygonOffset: true,
      polygonOffsetFactor: -4,
      polygonOffsetUnits: -4,
      side: THREE.DoubleSide,
    });

    this.shadowMesh = new THREE.Mesh(geom, this.shadowMaterial);
    this.shadowMesh.rotation.x = -Math.PI / 2;
    this.shadowMesh.position.set(0, 0.08, 0);
    this.shadowMesh.name = 'dynamic_cloud_ground_shadow';
    this.shadowMesh.frustumCulled = false;
    this.shadowMesh.renderOrder = 990;
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
    lightningOrigin?: THREE.Vector3
  ): void {
    this.animTimer += delta;

    // ── Rain-to-Cloud Integration ──
    // Rain dynamically affects cloud coverage, cloud darkness, and cloud size:
    // - Light rain (0.1 - 0.3): small, sparse, dark-gray clouds
    // - Max rain/storm (0.7 - 1.0): massive, pitch-black storm clouds covering the whole sky
    const rain = Math.max(0, Math.min(1, rainIntensity));
    const effectiveCoverage = Math.max(coverage, rain * 0.94);
    const cov = Math.max(0, Math.min(1, effectiveCoverage));
    const activeLayers = Math.max(1, Math.min(6, Math.round(layerCount)));
    const R = CloudSystem.RANGE;

    // ── 1. Wind Movement: Direct 2D vector accumulation ──
    const windRad = (windDirectionDeg * Math.PI) / 180;
    // Speed: 0 m/s when wind = 0; up to 32 m/s when wind = 100%
    const windSpeed = windIntensity * 32.0;

    // Continuous 2D accumulation allows real-time direction and speed changes without jumping
    this.windDriftX += Math.sin(windRad) * windSpeed * delta;
    this.windDriftZ += Math.cos(windRad) * windSpeed * delta;

    // ── 2. Atmospheric Lighting & Tone Colors ──
    this.applyTones(skyTime);
    const shape = this.getShapeConfig(cloudShape);

    // Calculate normalized sunlight direction
    let worldSunDir: THREE.Vector3;
    if (sunLightPos) {
      worldSunDir = sunLightPos.clone().normalize();
    } else {
      worldSunDir = skyTime === 'sunset' ? new THREE.Vector3(0.9, 0.35, 0.2).normalize()
        : skyTime === 'sunrise' ? new THREE.Vector3(-0.9, 0.35, 0.2).normalize()
          : skyTime === 'night' ? new THREE.Vector3(0.1, 0.8, 0.1).normalize()
            : new THREE.Vector3(0.3, 0.85, 0.25).normalize();
    }

    // Base sunlit color dims to stormy pitch-dark when raining
    const rainDarkness = Math.min(1.0, rain * 1.4);
    const baseSunlit = this.toneColors.sun.clone().lerp(this.toneColors.sky, 0.2);
    const sunlitColor = baseSunlit.lerp(new THREE.Color(0x18202c), rainDarkness * 0.92);

    // Deep pitch-black storm cloud underbelly (near black obsidian #03050a at max rain)
    const darkUnderbellyColor = new THREE.Color(0x182230).lerp(new THREE.Color(0x03050a), rainDarkness);

    // Center altitude of active layers
    let avgAlt = 0;
    for (let i = 0; i < activeLayers; i++) {
      avgAlt += this.layerAltitudes[i];
    }
    avgAlt = (avgAlt / activeLayers) * altitudeMult;

    // Electric cyan-white flash color for lightning
    const electricFlashColor = new THREE.Color(0xe0f2fe);

    // ── 3. Update Every Cloud Sprite in Every Active Layer ──
    for (const sd of this.sprites) {
      // Layer culling: Only render layers within the active layerCount slider
      if (sd.layer >= activeLayers) {
        sd.sprite.visible = false;
        continue;
      }

      // Coverage activation:
      // When coverage = 1.0 (or heavy rain), all sprites are 100% active.
      // When coverage is lower, sprites fade out according to their activationThreshold.
      let spriteActivity = 0;
      if (cov > sd.activationThreshold) {
        spriteActivity = Math.min(1.0, (cov - sd.activationThreshold) / 0.18);
      }

      if (spriteActivity <= 0.005 || cov < 0.01) {
        sd.sprite.visible = false;
        continue;
      }

      sd.sprite.visible = true;

      // Parallax wind drift: Higher layers move slightly faster for depth
      const layerSpeed = this.layerSpeedMults[sd.layer];
      const layerDriftX = this.windDriftX * layerSpeed;
      const layerDriftZ = this.windDriftZ * layerSpeed;

      // Subtle organic floating wobble
      const wobbleX = Math.sin(this.animTimer * 0.12 + sd.windPhase) * 4.0;
      const wobbleZ = Math.cos(this.animTimer * 0.09 + sd.windPhase) * 3.5;
      const wobbleY = Math.sin(this.animTimer * 0.07 + sd.windPhase * 2.0) * 2.0;

      // Wrap positions seamlessly within [-R, +R] around camera
      const localX = CloudSystem.wrapCoord(sd.baseX + layerDriftX + wobbleX, R);
      const localZ = CloudSystem.wrapCoord(sd.baseZ + layerDriftZ + wobbleZ, R);

      // Relative Y from group center (avgAlt)
      const layerAlt = this.layerAltitudes[sd.layer] * altitudeMult;
      const localY = (layerAlt - avgAlt) + sd.baseY * shape.heightMult + wobbleY;

      sd.sprite.position.set(localX, localY, localZ);

      // ── 4. Dynamic Scale & Density Expansion ──
      // At light rain: clouds are smaller and sparser; at heavy rain: clouds billow and expand
      const rainScaleFactor = rain > 0 ? (0.85 + rain * 0.45) : 1.0;
      const coverageScaleMult = (0.75 + cov * 0.55) * rainScaleFactor;
      sd.sprite.scale.set(
        sd.scaleW * shape.widthMult * coverageScaleMult,
        sd.scaleH * shape.heightScale * coverageScaleMult,
        1
      );

      // Distance-to-edge fade so clouds wrap seamlessly without popping
      const distFromCenter = Math.sqrt(localX * localX + localZ * localZ);
      const edgeFade = 1.0 - smoothstep(R * 0.75, R * 0.98, distFromCenter);

      // ── 5. Opacity & Darkness (Beer-Lambert Absorption + Storm Darkening) ──
      // Lower layers + heavy rain create pitch-black stormy undersides
      const layersAbove = (activeLayers - 1) - sd.layer;
      const absorptionFactor = Math.min(1.0, (layersAbove * 0.22 + cov * 0.35) * (cov * 1.1));
      const totalDarkness = Math.min(1.0, absorptionFactor + rainDarkness * 0.85);

      const mat = sd.sprite.material as THREE.SpriteMaterial;

      // Opacity: heavy rain + dense deck = solid dark storm cloud feel
      const rainOpacityBoost = rain * 0.30;
      const targetOpacity = (sd.baseOpacity + cov * 0.35 + rainOpacityBoost) * spriteActivity * edgeFade * shape.opacityMult;
      mat.opacity = Math.min(0.96, Math.max(0.0, targetOpacity));

      // Color interpolation: Sunlit white/gray -> Pitch-black stormy charcoal
      let finalColor = sunlitColor.clone().lerp(darkUnderbellyColor, totalDarkness);

      // ── 6. Intra-Cloud Lightning Flash Illumination ──
      if (lightningFlash > 0.01) {
        // Clouds flash with brilliant electric white-cyan from inside
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

    // Set cloud group to follow camera position and current average altitude
    this.cloudGroup.position.set(cameraPos.x, avgAlt, cameraPos.z);
    this.cloudGroup.visible = cov > 0.01;

    // ── 6. Ground Atmospheric Shadow Projection ──
    if (this.shadowMesh && this.shadowMaterial) {
      const isNight = skyTime === 'night';
      this.shadowMesh.visible = cov > 0.04 && !isNight;

      if (this.shadowMesh.visible) {
        // Place shadow plane right above ground plane
        this.shadowMesh.position.set(cameraPos.x, 0.08, cameraPos.z);
        const su = this.shadowMaterial.uniforms;
        su.uWorldOffset.value.set(cameraPos.x, cameraPos.z);
        // Ground shadow coordinates move in direct lockstep with clouds overhead
        su.uWindOffset.value.set(-this.windDriftX * 0.0028, -this.windDriftZ * 0.0028);
        su.uSunDir.value.copy(worldSunDir);
        su.uCloudAltitude.value = avgAlt;
        su.uCoverage.value = cov;
        su.uLayerCount.value = activeLayers;
        su.uShadowDarkness.value = Math.min(0.95, 0.70 + rain * 0.22);
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
