import * as THREE from 'three';

/**
 * Volumetric 3D Cloud System & Physically Projected Ground Shadow
 *
 * Features:
 * 1. Infinite-feeling Horizon Cloud Deck (Mặt phẳng biển mây 3D bao quanh chân trời):
 *    - Large 800m x 800m volume following camera XZ.
 *    - Flat cumulus cloud base with organic billowy tops (Sea of Clouds).
 *    - Smooth radial distance fade to blend seamlessly into the horizon atmosphere.
 *    - 4-octave 3D FBM procedural noise with quintic interpolation.
 *
 * 2. True Dynamic Projected Ground Shadows (Đổ bóng mây thời gian thực theo độ dày):
 *    - Ground shadow projector plane centered on camera at ground level (Y ≈ 0.04m).
 *    - Shader samples identical cloud noise field offset by sunlight vector & cloud altitude.
 *    - Shadow darkness on the ground is physically proportional to cloud thickness:
 *      * Thick dense clouds -> Deep dark shadows.
 *      * Thin/wispy cloud edges -> Faint soft shadows.
 *      * Clear sky gaps -> Zero shadow (full sunlight).
 *    - Lowering cloud altitude or tilting sun angle naturally stretches/moves the ground shadows.
 *    - Wind smoothly drifts the clouds and ground shadows in perfect sync.
 */
export class CloudSystem {
  private scene: THREE.Scene;
  
  // Cloud Volume
  private mesh: THREE.Mesh | null = null;
  private material: THREE.ShaderMaterial | null = null;
  private boxScale = new THREE.Vector3(800, 60, 800);

  // Dynamic Ground Shadow Plane
  private shadowMesh: THREE.Mesh | null = null;
  private shadowMaterial: THREE.ShaderMaterial | null = null;
  
  private animTimer = 0;

  constructor(scene: THREE.Scene) {
    this.scene = scene;
    this.buildVolume();
    this.buildDynamicShadow();
  }

  // ══════════════════════════════════════════════════
  // 1. Volumetric Cloud Mesh Construction
  // ══════════════════════════════════════════════════

  private buildVolume(): void {
    const geom = new THREE.BoxGeometry(1, 1, 1);

    this.material = new THREE.ShaderMaterial({
      uniforms: {
        uCamLocal: { value: new THREE.Vector3(0, -1, 0) },
        uSunDirLocal: { value: new THREE.Vector3(0, 1, 0) },
        uSunColor: { value: new THREE.Color(0xfffaf0) },
        uSkyColor: { value: new THREE.Color(0x93c5fd) },
        uAmbientColor: { value: new THREE.Color(0x385c88) },
        uCoverage: { value: 0.5 },
        uTime: { value: 0 },
        uWindOffset: { value: new THREE.Vector2(0, 0) },
        uWorldOffset: { value: new THREE.Vector2(0, 0) },
        uLayerCount: { value: 3.0 },
      },
      vertexShader: /* glsl */ `
        uniform vec3 uCamLocal;
        varying vec3 vOrigin;
        varying vec3 vDirection;

        void main() {
          vOrigin = uCamLocal;
          vDirection = position - uCamLocal;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: /* glsl */ `
        precision highp float;

        varying vec3 vOrigin;
        varying vec3 vDirection;

        uniform vec3 uSunDirLocal;
        uniform vec3 uSunColor;
        uniform vec3 uSkyColor;
        uniform vec3 uAmbientColor;
        uniform float uCoverage;
        uniform float uTime;
        uniform vec2 uWindOffset;
        uniform vec2 uWorldOffset;
        uniform float uLayerCount;

        #define STEPS 28
        #define LIGHT_STEPS 4
        #define PI 3.14159265

        // ── 3D Value Noise with Quintic Interpolation ──
        float hash3(vec3 p) {
          p = fract(p * vec3(0.1031, 0.1030, 0.0973));
          p += dot(p, p.yxz + 33.33);
          return fract((p.x + p.y) * p.z);
        }

        float noise3D(vec3 p) {
          vec3 i = floor(p);
          vec3 f = fract(p);
          // Quintic Hermite curve for smooth, organic clouds
          f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

          float n000 = hash3(i);
          float n100 = hash3(i + vec3(1.0, 0.0, 0.0));
          float n010 = hash3(i + vec3(0.0, 1.0, 0.0));
          float n110 = hash3(i + vec3(1.0, 1.0, 0.0));
          float n001 = hash3(i + vec3(0.0, 0.0, 1.0));
          float n101 = hash3(i + vec3(1.0, 0.0, 1.0));
          float n011 = hash3(i + vec3(0.0, 1.0, 1.0));
          float n111 = hash3(i + vec3(1.0, 1.0, 1.0));

          return mix(
            mix(mix(n000, n100, f.x), mix(n010, n110, f.x), f.y),
            mix(mix(n001, n101, f.x), mix(n011, n111, f.x), f.y),
            f.z
          );
        }

        // 4-octave FBM for rich, fluffy volumetric details
        float fbm4(vec3 p) {
          float v = 0.0;
          float a = 0.5;
          for (int i = 0; i < 4; i++) {
            v += a * noise3D(p);
            p = p * 2.02 + vec3(1.7, 3.1, 2.3);
            a *= 0.5;
          }
          return v;
        }

        // Henyey-Greenstein light scattering phase function
        float hg(float cosT, float g) {
          float g2 = g * g;
          return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosT, 1.5));
        }

        // Ray-box intersection with epsilon safety
        vec2 hitBox(vec3 orig, vec3 dir) {
          vec3 d = dir;
          if (abs(d.x) < 1e-5) d.x = 1e-5;
          if (abs(d.y) < 1e-5) d.y = 1e-5;
          if (abs(d.z) < 1e-5) d.z = 1e-5;

          vec3 inv = 1.0 / d;
          vec3 t1 = (-0.5 - orig) * inv;
          vec3 t2 = (0.5 - orig) * inv;
          vec3 tNear = min(t1, t2);
          vec3 tFar = max(t1, t2);
          float near = max(tNear.x, max(tNear.y, tNear.z));
          float far = min(tFar.x, min(tFar.y, tFar.z));
          return vec2(near, far);
        }

        // Sample cloud density at local position p ∈ [-0.5, 0.5]
        float sampleDensity(vec3 p) {
          // 1. Cloud Deck Height Gradient:
          // Flat distinct base at bottom (cumulus deck look), billowy fluffy tops
          float h = p.y + 0.5; // 0 = bottom, 1 = top

          float maxH = 0.35 + (uLayerCount - 1.0) * 0.13;
          maxH = clamp(maxH, 0.35, 1.0);
          
          // Sharp flat bottom, soft undulating top
          float hGrad = smoothstep(0.02, 0.18, h) * smoothstep(maxH, maxH - 0.22, h);
          if (hGrad < 0.005) return 0.0;

          // 2. Horizon Radial Distance Falloff:
          // Eliminates square box edges -> creates an infinite circular cloud blanket
          float radialDist = length(p.xz);
          float radialFade = smoothstep(0.5, 0.25, radialDist);
          if (radialFade < 0.005) return 0.0;

          // 3. World-Space 3D FBM Sampling:
          // Clouds stay anchored in the world while wind offset blows them naturally
          vec3 noiseP;
          noiseP.x = p.x * 5.0 + uWorldOffset.x * 0.008 + uWindOffset.x;
          noiseP.y = p.y * 3.5;
          noiseP.z = p.z * 5.0 + uWorldOffset.y * 0.008 + uWindOffset.y;

          float n = fbm4(noiseP);

          // 4. Coverage Threshold with soft density transitions
          float threshold = 0.51 - uCoverage * 0.38;
          float d = smoothstep(threshold, threshold + 0.18, n);
          d *= hGrad * radialFade * 2.3;

          return max(0.0, d);
        }

        // March towards sun to calculate inner self-shadowing / light extinction
        float lightMarch(vec3 pos) {
          float totalD = 0.0;
          float stepLen = 0.07;
          for (int i = 0; i < LIGHT_STEPS; i++) {
            vec3 p = pos + uSunDirLocal * stepLen * (float(i) + 1.0);
            if (any(greaterThan(abs(p), vec3(0.5)))) break;
            totalD += sampleDensity(p) * stepLen;
          }
          return exp(-totalD * 4.5);
        }

        void main() {
          vec3 rd = normalize(vDirection);
          vec2 bounds = hitBox(vOrigin, rd);
          if (bounds.x >= bounds.y) discard;
          bounds.x = max(bounds.x, 0.0);

          float rayLen = bounds.y - bounds.x;
          if (rayLen < 0.001) discard;
          float stepSize = rayLen / float(STEPS);

          vec3 accColor = vec3(0.0);
          float transmittance = 1.0;
          float cosA = dot(rd, uSunDirLocal);
          // Dual-lobe Henyey-Greenstein for silver lining & backscatter
          float phase = mix(hg(cosA, -0.2), hg(cosA, 0.65), 0.72);

          for (int i = 0; i < STEPS; i++) {
            vec3 p = vOrigin + rd * (bounds.x + (float(i) + 0.5) * stepSize);

            float d = sampleDensity(p);
            if (d > 0.001) {
              float light = lightMarch(p);
              float h = p.y + 0.5;

              // Sunlit highlight vs ambient underside / horizon tone
              vec3 sunLit = uSunColor * light * phase * 1.7;
              vec3 ambient = mix(uAmbientColor * 0.55, uSkyColor * 0.35, h);
              vec3 sampleCol = sunLit + ambient;

              // Beer-Lambert absorption
              float absorption = exp(-d * stepSize * 6.5);
              float contrib = (1.0 - absorption) * transmittance;
              accColor += sampleCol * contrib;
              transmittance *= absorption;

              if (transmittance < 0.015) break;
            }
          }

          float alpha = 1.0 - transmittance;
          if (alpha < 0.005) discard;

          gl_FragColor = vec4(accColor, alpha);
        }
      `,
      transparent: true,
      depthWrite: false,
      side: THREE.BackSide,
    });

    this.mesh = new THREE.Mesh(geom, this.material);
    this.mesh.scale.copy(this.boxScale);
    this.mesh.position.set(0, 65, 0);
    this.mesh.name = 'volumetric_clouds';
    this.mesh.frustumCulled = false;
    this.mesh.renderOrder = 100;
    this.scene.add(this.mesh);
  }

  // ══════════════════════════════════════════════════
  // 2. Dynamic Projected Ground Shadow Shader
  // ══════════════════════════════════════════════════

  private buildDynamicShadow(): void {
    // 600m ground projection plane
    const geom = new THREE.PlaneGeometry(600, 600, 1, 1);

    this.shadowMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uWorldOffset: { value: new THREE.Vector2(0, 0) },
        uWindOffset: { value: new THREE.Vector2(0, 0) },
        uSunDir: { value: new THREE.Vector3(0.3, 0.85, 0.25).normalize() },
        uCloudAltitude: { value: 65.0 },
        uCoverage: { value: 0.5 },
        uLayerCount: { value: 3.0 },
        uShadowDarkness: { value: 0.55 },
        uSkyTime: { value: 0 }, // 0: noon, 1: sunrise/sunset, 2: overcast, 3: night
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
        precision highp float;

        varying vec2 vWorldXZ;

        uniform vec2 uWorldOffset;
        uniform vec2 uWindOffset;
        uniform vec3 uSunDir;
        uniform float uCloudAltitude;
        uniform float uCoverage;
        uniform float uLayerCount;
        uniform float uShadowDarkness;
        uniform float uSkyTime;

        // ── Same 2D/3D Noise used by clouds for 100% coherence ──
        float hash3(vec3 p) {
          p = fract(p * vec3(0.1031, 0.1030, 0.0973));
          p += dot(p, p.yxz + 33.33);
          return fract((p.x + p.y) * p.z);
        }

        float noise3D(vec3 p) {
          vec3 i = floor(p);
          vec3 f = fract(p);
          f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

          float n000 = hash3(i);
          float n100 = hash3(i + vec3(1.0, 0.0, 0.0));
          float n010 = hash3(i + vec3(0.0, 1.0, 0.0));
          float n110 = hash3(i + vec3(1.0, 1.0, 0.0));
          float n001 = hash3(i + vec3(0.0, 0.0, 1.0));
          float n101 = hash3(i + vec3(1.0, 0.0, 1.0));
          float n011 = hash3(i + vec3(0.0, 1.0, 1.0));
          float n111 = hash3(i + vec3(1.0, 1.0, 1.0));

          return mix(
            mix(mix(n000, n100, f.x), mix(n010, n110, f.x), f.y),
            mix(mix(n001, n101, f.x), mix(n011, n111, f.x), f.y),
            f.z
          );
        }

        float fbm4(vec3 p) {
          float v = 0.0;
          float a = 0.5;
          for (int i = 0; i < 4; i++) {
            v += a * noise3D(p);
            p = p * 2.02 + vec3(1.7, 3.1, 2.3);
            a *= 0.5;
          }
          return v;
        }

        void main() {
          // 1. Ray trace from ground point up towards sun intersection with cloud deck
          float sunY = max(uSunDir.y, 0.12);
          vec2 sunOffset = (uSunDir.xz / sunY) * (uCloudAltitude - 0.04);
          vec2 cloudWorldPos = vWorldXZ - sunOffset;

          // 2. Sample multiple vertical slices to compute true optical cloud thickness
          vec2 noiseCoord = cloudWorldPos * 0.008 + uWindOffset;
          
          float threshold = 0.51 - uCoverage * 0.38;
          
          // Sample bottom, middle, and upper cloud layers
          float nBase = fbm4(vec3(noiseCoord, -0.3));
          float nMid  = fbm4(vec3(noiseCoord * 1.05 + vec2(0.5, 0.8), 0.0));
          float nTop  = fbm4(vec3(noiseCoord * 1.1 - vec2(0.7, 0.4), 0.3));

          float dBase = smoothstep(threshold, threshold + 0.18, nBase);
          float dMid  = smoothstep(threshold, threshold + 0.18, nMid);
          float dTop  = smoothstep(threshold, threshold + 0.18, nTop);

          // Weight by layer count (thicker clouds with more layers)
          float layerFactor = clamp(uLayerCount / 3.0, 0.6, 1.6);
          float opticalThickness = (dBase * 0.45 + dMid * 0.40 + dTop * 0.25) * layerFactor;

          if (opticalThickness < 0.01) discard;

          // 3. Distance fade near the edges of the ground projector plane
          vec2 localPos = vWorldXZ - uWorldOffset;
          float distToCenter = length(localPos) / 300.0;
          float groundFade = smoothstep(1.0, 0.7, distToCenter);
          opticalThickness *= groundFade;

          if (opticalThickness < 0.005) discard;

          // 4. Physical Shadow Alpha based on Cloud Thickness & Height
          // Low clouds create crisper, darker shadows; high clouds create softer ambient shadows
          float heightFactor = clamp(100.0 / max(uCloudAltitude, 30.0), 0.7, 1.4);
          float shadowAlpha = opticalThickness * uShadowDarkness * heightFactor;
          shadowAlpha = clamp(shadowAlpha, 0.0, 0.75);

          // Deep ambient tone (slate / cool navy shadow color for realism)
          vec3 shadowColor = vec3(0.04, 0.07, 0.14);

          gl_FragColor = vec4(shadowColor, shadowAlpha);
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
    this.shadowMesh.position.set(0, 0.04, 0); // slightly above ground to prevent z-fighting
    this.shadowMesh.name = 'dynamic_cloud_ground_shadow';
    this.shadowMesh.frustumCulled = false;
    this.shadowMesh.renderOrder = 990;
    this.scene.add(this.shadowMesh);
  }

  // ══════════════════════════════════════════════════
  // 3. Animation & Simulation Update Loop
  // ══════════════════════════════════════════════════

  public update(
    delta: number,
    cameraPos: THREE.Vector3,
    coverage: number = 0.5,
    _cloudShape: string = 'cumulus',
    windIntensity: number = 0.3,
    windDirectionDeg: number = 45,
    skyTime: string = 'noon',
    altitudeMult: number = 1.0,
    layerCount: number = 3,
    sunLightPos?: THREE.Vector3
  ): void {
    if (!this.mesh || !this.material) return;

    this.animTimer += delta;
    const cov = Math.max(0, Math.min(1, coverage));
    const layers = Math.max(1, Math.min(6, Math.round(layerCount)));

    // ── Box height based on layer count (25m -> 110m depth) ──
    const heightMap = [25, 42, 60, 78, 95, 115];
    this.boxScale.y = heightMap[layers - 1];
    this.mesh.scale.copy(this.boxScale);

    // ── Cloud Deck Position: follow camera XZ ──
    const cloudAltitudeY = 65 * altitudeMult;
    this.mesh.position.set(cameraPos.x, cloudAltitudeY, cameraPos.z);

    // ── Camera in volume local space ──
    const localCamY = (cameraPos.y - cloudAltitudeY) / this.boxScale.y;
    this.material.uniforms.uCamLocal.value.set(0, localCamY, 0);

    // ── Sun Direction in World & Local Space ──
    let worldSunDir: THREE.Vector3;
    if (sunLightPos) {
      worldSunDir = sunLightPos.clone().normalize();
    } else {
      worldSunDir = skyTime === 'sunset' ? new THREE.Vector3(0.9, 0.35, 0.2).normalize()
        : skyTime === 'sunrise' ? new THREE.Vector3(-0.9, 0.35, 0.2).normalize()
        : skyTime === 'night' ? new THREE.Vector3(0.1, 0.8, 0.1).normalize()
        : new THREE.Vector3(0.3, 0.85, 0.25).normalize();
    }

    const localSun = new THREE.Vector3(
      worldSunDir.x / this.boxScale.x,
      worldSunDir.y / this.boxScale.y,
      worldSunDir.z / this.boxScale.z
    ).normalize();
    this.material.uniforms.uSunDirLocal.value.copy(localSun);

    // ── Wind Simulation ──
    const windRad = (windDirectionDeg * Math.PI) / 180;
    const windSpeed = 0.3 + windIntensity * 2.0;
    const windX = Math.sin(windRad) * windSpeed * this.animTimer * 0.035;
    const windZ = Math.cos(windRad) * windSpeed * this.animTimer * 0.035;
    this.material.uniforms.uWindOffset.value.set(windX, windZ);

    // ── World Offset for Camera Anchor ──
    this.material.uniforms.uWorldOffset.value.set(cameraPos.x, cameraPos.z);

    // ── Atmosphere Uniforms ──
    this.material.uniforms.uCoverage.value = cov;
    this.material.uniforms.uTime.value = this.animTimer;
    this.material.uniforms.uLayerCount.value = layers;

    // ── Atmospheric Colors ──
    const tones = this.getTones(skyTime);
    this.material.uniforms.uSunColor.value.copy(tones.sun);
    this.material.uniforms.uSkyColor.value.copy(tones.sky);
    this.material.uniforms.uAmbientColor.value.copy(tones.ambient);

    this.mesh.visible = cov > 0.02;

    // ══════════════════════════════════════════════════
    // Dynamic Ground Shadow Projection Update
    // ══════════════════════════════════════════════════
    if (this.shadowMesh && this.shadowMaterial) {
      const isNight = skyTime === 'night';
      const shadowVisible = cov > 0.05 && !isNight;
      this.shadowMesh.visible = shadowVisible;

      if (shadowVisible) {
        // Shadow plane follows camera on the ground
        this.shadowMesh.position.set(cameraPos.x, 0.04, cameraPos.z);

        const su = this.shadowMaterial.uniforms;
        su.uWorldOffset.value.set(cameraPos.x, cameraPos.z);
        su.uWindOffset.value.set(windX, windZ);
        su.uSunDir.value.copy(worldSunDir);
        su.uCloudAltitude.value = cloudAltitudeY;
        su.uCoverage.value = cov;
        su.uLayerCount.value = layers;
        
        // Overcast skies diffuse shadows; clear sunny skies make crisp darker shadows
        const baseDarkness = skyTime === 'overcast' ? 0.35 : 0.65;
        su.uShadowDarkness.value = baseDarkness;
      }
    }
  }

  private getTones(skyTime: string): {
    sun: THREE.Color; sky: THREE.Color; ambient: THREE.Color;
  } {
    if (skyTime === 'overcast')
      return { sun: new THREE.Color(0x94a3b8), sky: new THREE.Color(0x64748b), ambient: new THREE.Color(0x475569) };
    if (skyTime === 'sunset')
      return { sun: new THREE.Color(0xff8c42), sky: new THREE.Color(0xc4856e), ambient: new THREE.Color(0x8b4513) };
    if (skyTime === 'sunrise')
      return { sun: new THREE.Color(0xffbc75), sky: new THREE.Color(0xd4a882), ambient: new THREE.Color(0x6b4226) };
    if (skyTime === 'night')
      return { sun: new THREE.Color(0x6b88b0), sky: new THREE.Color(0x1a2744), ambient: new THREE.Color(0x0f172a) };
    return { sun: new THREE.Color(0xfffaf0), sky: new THREE.Color(0x93c5fd), ambient: new THREE.Color(0x5a7fa8) };
  }

  // ══════════════════════════════════════════════════
  // Disposal
  // ══════════════════════════════════════════════════

  public dispose(): void {
    if (this.mesh) {
      this.scene.remove(this.mesh);
      this.mesh.geometry.dispose();
      this.mesh = null;
    }
    if (this.material) {
      this.material.dispose();
      this.material = null;
    }
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
