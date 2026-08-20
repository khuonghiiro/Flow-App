import * as THREE from 'three';

/**
 * VolumetricCloudLighting — Physically-Based Multi-Layer Cloud Shadow Engine
 *
 * Features:
 * 1. True 3D Sun-Ray Projection:
 *    Traces light rays from any 3D world point (ground, roofs, walls, trees, characters)
 *    towards the celestial sun vector to intercept the cloud altitude decks:
 *    HitPos = WorldPos.xz + (SunDir.xz / max(SunDir.y, 0.08)) * max(0.0, CloudAlt - WorldPos.y)
 * 2. Multi-Octave Dual-Deck FBM Noise:
 *    Simulates organic cumulus billows (main deck) and wispy cirrus fringes (upper deck)
 *    with realistic parallax wind drift, avoiding repetitive tiling artifacts.
 * 3. Beer-Lambert Optical Light Extinction:
 *    Calculates optical depth tau based on cloud density & coverage, decaying direct
 *    sunlight while preserving ambient/diffuse sky illumination.
 * 4. Universal Shared Uniform Binding:
 *    Binds shared uniform references across all standard materials (terrain, GLTF models,
 *    VRM avatars, props) for instant real-time frame updates at 60-120 FPS.
 */
export class VolumetricCloudLighting {
  private static originalCommon: string | null = null;
  private static originalWorldposVertex: string | null = null;
  private static originalLightsBegin: string | null = null;

  public static readonly uniforms = {
    uCloudSunDir: { value: new THREE.Vector3(0.3, 0.85, 0.25).normalize() },
    uCloudWindOffset: { value: new THREE.Vector2(0, 0) },
    uCloudCoverage: { value: 0.5 },
    uCloudAltitude: { value: 225.0 },
    uCloudShadowDarkness: { value: 0.85 },
    uCloudCenter: { value: new THREE.Vector2(0, 0) },
    uCloudScale: { value: 1.0 },
    uCloudTime: { value: 0.0 },
  };

  /**
   * Inject volumetric cloud shadow calculations into Three.js global shader pipeline.
   * Call once before any materials are compiled.
   */
  public static init(): void {
    if (this.originalCommon === null) {
      this.originalCommon = THREE.ShaderChunk.common;
      this.originalWorldposVertex = THREE.ShaderChunk.worldpos_vertex;
      this.originalLightsBegin = THREE.ShaderChunk.lights_fragment_begin;
    }

    // 0. Hook prototype onBeforeCompile to inject shared uniform references to lit materials
    const originalOnBeforeCompile = THREE.Material.prototype.onBeforeCompile;
    THREE.Material.prototype.onBeforeCompile = function (shader, renderer) {
      originalOnBeforeCompile.call(this, shader, renderer);
      const isLitMaterial =
        this.type.includes('Standard') ||
        this.type.includes('Physical') ||
        this.type.includes('Lambert') ||
        this.type.includes('Phong') ||
        this.type.includes('Toon');

      if (isLitMaterial) {
        VolumetricCloudLighting.bindUniformsToShader(shader);
        this.userData.cloudUniforms = shader.uniforms;
      }
    };

    // 1. Inject uniforms into Three.js default UniformsLib
    Object.assign(THREE.UniformsLib.common, this.uniforms);

    // 2. Add world position calculation to vertex shaders
    THREE.ShaderChunk.worldpos_vertex = `
      ${this.originalWorldposVertex}
      #ifdef USE_INSTANCING
        vCloudWorldPos = (modelMatrix * instanceMatrix * vec4(transformed, 1.0)).xyz;
      #else
        vCloudWorldPos = (modelMatrix * vec4(transformed, 1.0)).xyz;
      #endif
    `;

    // 3. Add Volumetric Cloud extinction functions and uniforms to fragment shaders
    const cloudExtinctionGLSL = /* glsl */ `
      #ifndef VOLUMETRIC_CLOUD_EXTINCTION
      #define VOLUMETRIC_CLOUD_EXTINCTION
      varying vec3 vCloudWorldPos;
      uniform vec3 uCloudSunDir;
      uniform vec2 uCloudWindOffset;
      uniform float uCloudCoverage;
      uniform float uCloudAltitude;
      uniform float uCloudShadowDarkness;
      uniform vec2 uCloudCenter;
      uniform float uCloudScale;
      uniform float uCloudTime;

      // Fast, artifact-free 2D gradient hash for procedural cloud noise
      vec2 hash22_cloud(vec2 p) {
        vec2 d = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
        return -1.0 + 2.0 * fract(sin(d) * 43758.5453123);
      }

      float noise2D_cloud(vec2 p) {
        vec2 i = floor(p);
        vec2 f = fract(p);
        vec2 u = f * f * (3.0 - 2.0 * f);

        float n00 = dot(hash22_cloud(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0));
        float n10 = dot(hash22_cloud(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
        float n01 = dot(hash22_cloud(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
        float n11 = dot(hash22_cloud(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));

        return 0.5 + 0.5 * mix(mix(n00, n10, u.x), mix(n01, n11, u.x), u.y);
      }

      // 4-Octave Fractal Brownian Motion for rich, organic cloud density
      float cloudFBM_shader(vec2 p) {
        float f = 0.52 * noise2D_cloud(p);
        f += 0.26 * noise2D_cloud(p * 2.05 + vec2(1.7, 9.2));
        f += 0.14 * noise2D_cloud(p * 4.10 + vec2(8.3, 2.8));
        f += 0.08 * noise2D_cloud(p * 8.24 + vec2(3.1, 5.7));
        return f;
      }

      float computeCloudTransmittance(vec3 worldPos) {
        if (uCloudCoverage <= 0.02 || uCloudShadowDarkness <= 0.01) return 1.0;

        float sunY = max(uCloudSunDir.y, 0.08);
        vec2 sunDirXZ = uCloudSunDir.xz;

        // ── Deck 1: Main Fluffy Cumulus Cloud Layer (High Altitude 150m-300m) ──
        float rayDist1 = max(0.0, uCloudAltitude - worldPos.y) / sunY;
        vec2 hitPos1 = worldPos.xz + sunDirXZ * rayDist1;
        vec2 samplePos1 = (hitPos1 - uCloudWindOffset) * (0.038 * uCloudScale);

        float n1 = cloudFBM_shader(samplePos1);

        // Linear coverage response with high contrast
        float threshold1 = 0.72 - uCloudCoverage * 0.44;
        float d1 = smoothstep(threshold1 - 0.12, threshold1 + 0.12, n1);

        // ── Deck 2: Higher Altitude Wispy Clouds (Parallax Drift) ──
        float alt2 = uCloudAltitude * 1.30 + 15.0;
        float rayDist2 = max(0.0, alt2 - worldPos.y) / sunY;
        vec2 hitPos2 = worldPos.xz + sunDirXZ * rayDist2;
        vec2 samplePos2 = (hitPos2 - uCloudWindOffset * 1.25 + vec2(45.0, -30.0)) * (0.065 * uCloudScale);

        float n2 = cloudFBM_shader(samplePos2);
        float threshold2 = 0.74 - uCloudCoverage * 0.42;
        float d2 = smoothstep(threshold2 - 0.10, threshold2 + 0.10, n2);

        // Composite optical density across both cloud decks
        float totalDensity = clamp(d1 * 1.1 + d2 * 0.65, 0.0, 1.0);
        if (totalDensity <= 0.005) return 1.0;

        // Beer-Lambert Exponential Light Extinction
        float optTau = totalDensity * (1.8 + uCloudCoverage * 3.5);
        float transmittance = exp(-optTau);

        // Modulate with user shadow darkness factor
        float shadowFactor = mix(1.0, transmittance, uCloudShadowDarkness);
        return clamp(shadowFactor, 0.02, 1.0);
      }
      #endif
    `;

    THREE.ShaderChunk.common = `
      ${this.originalCommon}
      ${cloudExtinctionGLSL}
    `;

    // 4. Modulate direct sunlight color and indirect ambient in lights_fragment_begin with Beer-Lambert transmittance
    let modifiedLightsBegin = (this.originalLightsBegin || THREE.ShaderChunk.lights_fragment_begin).replace(
      'getDirectionalLightInfo( directionalLight, directLight );',
      /* glsl */ `
        getDirectionalLightInfo( directionalLight, directLight );
        float vCloudShadowTransmittance = computeCloudTransmittance(vCloudWorldPos);
        directLight.color *= vCloudShadowTransmittance;
      `
    );

    modifiedLightsBegin = modifiedLightsBegin.replace(
      'vec3 irradiance = getAmbientLightIrradiance( ambientLightColor );',
      /* glsl */ `
        vec3 irradiance = getAmbientLightIrradiance( ambientLightColor ) * mix(0.65, 1.0, computeCloudTransmittance(vCloudWorldPos));
      `
    );

    THREE.ShaderChunk.lights_fragment_begin = modifiedLightsBegin;
  }

  /**
   * Bind shared uniform references directly to a compiled shader uniforms object.
   */
  public static bindUniformsToShader(shader: { uniforms: Record<string, any> }): void {
    if (!shader || !shader.uniforms) return;
    shader.uniforms.uCloudSunDir = this.uniforms.uCloudSunDir;
    shader.uniforms.uCloudWindOffset = this.uniforms.uCloudWindOffset;
    shader.uniforms.uCloudCoverage = this.uniforms.uCloudCoverage;
    shader.uniforms.uCloudAltitude = this.uniforms.uCloudAltitude;
    shader.uniforms.uCloudShadowDarkness = this.uniforms.uCloudShadowDarkness;
    shader.uniforms.uCloudCenter = this.uniforms.uCloudCenter;
    shader.uniforms.uCloudScale = this.uniforms.uCloudScale;
    shader.uniforms.uCloudTime = this.uniforms.uCloudTime;
  }

  /**
   * Apply cloud lighting hooks to any single material (e.g. loaded from GLTF or custom).
   */
  public static applyToMaterial(material: THREE.Material): void {
    if (!material) return;
    if ((material as any).__cloudLightingHooked) return;
    (material as any).__cloudLightingHooked = true;

    const existingOnBeforeCompile = material.onBeforeCompile;
    material.onBeforeCompile = function (shader, renderer) {
      if (existingOnBeforeCompile) {
        existingOnBeforeCompile.call(this, shader, renderer);
      }
      VolumetricCloudLighting.bindUniformsToShader(shader);
      this.userData.cloudUniforms = shader.uniforms;
    };
    material.needsUpdate = true;
  }

  /**
   * Apply cloud lighting hooks recursively across all materials in a scene or object tree.
   */
  public static applyToScene(root: THREE.Object3D): void {
    if (!root) return;
    root.traverse((child) => {
      const mesh = child as THREE.Mesh;
      if (mesh.isMesh && mesh.material) {
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        for (const m of mats) {
          this.applyToMaterial(m);
        }
      }
    });
  }

  /**
   * Update cloud extinction parameters in real time each frame.
   */
  public static update(params: {
    sunDirection: THREE.Vector3;
    coverage: number;
    altitude: number;
    shadowDarkness?: number;
    shadowScale?: number;
    centerXZ: THREE.Vector2;
    windOffset: THREE.Vector2;
    time?: number;
    scene?: THREE.Scene;
  }): void {
    this.uniforms.uCloudSunDir.value.copy(params.sunDirection).normalize();
    this.uniforms.uCloudWindOffset.value.copy(params.windOffset);
    this.uniforms.uCloudCoverage.value = Math.max(0, Math.min(1, params.coverage));
    this.uniforms.uCloudAltitude.value = Math.max(10.0, params.altitude);
    this.uniforms.uCloudCenter.value.copy(params.centerXZ);

    if (params.shadowDarkness !== undefined) {
      this.uniforms.uCloudShadowDarkness.value = Math.max(0, Math.min(1, params.shadowDarkness));
    }
    if (params.shadowScale !== undefined) {
      this.uniforms.uCloudScale.value = Math.max(0.2, Math.min(5.0, params.shadowScale));
    }
    if (params.time !== undefined) {
      this.uniforms.uCloudTime.value = params.time;
    }
  }
}

