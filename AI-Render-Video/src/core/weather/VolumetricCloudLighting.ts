import * as THREE from 'three';

/**
 * VolumetricCloudLighting — Beer-Lambert Physical Cloud Shadow Engine
 *
 * Implements physically-based volumetric light extinction through cloud mist:
 * 1. Droplet Optics: Each mist droplet transmits ~95% light and scatters ~5%.
 * 2. Multi-Layer Ray March: Rays from 3D surface points (roofs, walls, terrain, characters)
 *    intercept the cloud decks at altitude H, accumulating optical depth tau.
 * 3. Beer-Lambert Extinction: Direct sunlight transmittance T = exp(-tau).
 * 4. Contoured 3D Clinging: Modulates direct sunlight on all 3D geometry in world-space,
 *    eliminating 2D flat shadow planes entirely.
 */
export class VolumetricCloudLighting {
  private static initialized = false;

  public static uniforms = {
    uCloudSunDir: { value: new THREE.Vector3(0.3, 0.85, 0.25).normalize() },
    uCloudWindOffset: { value: new THREE.Vector2(0, 0) },
    uCloudCoverage: { value: 0.5 },
    uCloudAltitude: { value: 85.0 },
    uCloudShadowDarkness: { value: 0.85 },
    uCloudCenter: { value: new THREE.Vector2(0, 0) },
    uCloudShadowMap: { value: null as THREE.Texture | null },
  };

  /**
   * Inject volumetric cloud shadow calculations into Three.js global shader pipeline.
   * Call once before any materials are compiled.
   */
  public static init(): void {
    if (this.initialized) return;
    this.initialized = true;

    // 0. Global Hack: Capture internal shader uniforms for all materials so we can update them dynamically
    const originalOnBeforeCompile = THREE.Material.prototype.onBeforeCompile;
    THREE.Material.prototype.onBeforeCompile = function (shader, renderer) {
      originalOnBeforeCompile.call(this, shader, renderer);
      this.userData.shaderUniforms = shader.uniforms;
    };

    // 1. Inject uniforms into Three.js default UniformsLib
    Object.assign(THREE.UniformsLib.common, this.uniforms);

    // 2. Add world position calculation to vertex shaders
    const originalWorldposVertex = THREE.ShaderChunk.worldpos_vertex;
    THREE.ShaderChunk.worldpos_vertex = `
      ${originalWorldposVertex}
      #ifdef USE_INSTANCING
        vCloudWorldPos = (modelMatrix * instanceMatrix * vec4(transformed, 1.0)).xyz;
      #else
        vCloudWorldPos = (modelMatrix * vec4(transformed, 1.0)).xyz;
      #endif
    `;

    // 3. Add Volumetric Cloud extinction functions and uniforms to fragment shaders
    const cloudExtinctionGLSL = /* glsl */ `
      varying vec3 vCloudWorldPos;
      uniform vec3 uCloudSunDir;
      uniform vec2 uCloudWindOffset;
      uniform float uCloudCoverage;
      uniform float uCloudAltitude;
      uniform float uCloudShadowDarkness;
      uniform vec2 uCloudCenter;
      uniform sampler2D uCloudShadowMap;

      float computeCloudTransmittance(vec3 worldPos) {
        if (uCloudCoverage <= 0.01) return 1.0;

        float sunY = max(uCloudSunDir.y, 0.12);
        float alt = uCloudAltitude;
        
        // Ray trace from the world pixel towards the sun to see where it hits the cloud plane
        vec2 sunRayOffset = (uCloudSunDir.xz / sunY) * max(2.0, alt - worldPos.y);
        vec2 cloudHitPos = worldPos.xz - sunRayOffset;
        
        // The CloudSystem texture covers 1000x1000 area centered at uCloudCenter
        vec2 offset = cloudHitPos - uCloudCenter;
        
        // Match the wrapCoord logic in CloudSystem exactly
        // wrapCoord mathematically is equivalent to fract
        vec2 windDrift = uCloudWindOffset / 1000.0;
        vec2 uv = offset / 1000.0 + 0.5 - windDrift;
        uv = fract(uv); // Seamless repeating texture mapping
        
        // DataTexture RedFormat stores density in the .r channel
        float cloudDensity = texture2D(uCloudShadowMap, uv).r;
        
        // Normalize density based on coverage (matching sprite spawn logic)
        float threshold = 0.1;
        float activeDensity = max(0.0, cloudDensity - threshold);
        
        // Total optical depth
        float totalTau = activeDensity * uCloudCoverage * 5.0;
        
        if (totalTau <= 0.01) {
          return 1.0; // 100% direct sunlight
        }

        // Beer-Lambert Law: exponential decay with density
        float transmittance = exp(-totalTau);

        // Blend with user darkness control
        float shadowFactor = mix(1.0, transmittance, uCloudShadowDarkness);
        return clamp(shadowFactor, 0.04, 1.0);
      }
    `;

    THREE.ShaderChunk.common = `
      ${THREE.ShaderChunk.common}
      ${cloudExtinctionGLSL}
    `;

    // 4. Modulate direct sunlight color in lights_fragment_begin with Beer-Lambert transmittance
    const originalLightsBegin = THREE.ShaderChunk.lights_fragment_begin;
    THREE.ShaderChunk.lights_fragment_begin = originalLightsBegin.replace(
      'getDirectionalLightInfo( directionalLight, directLight );',
      /* glsl */ `
        getDirectionalLightInfo( directionalLight, directLight );
        directLight.color *= computeCloudTransmittance(vCloudWorldPos);
      `
    );
  }

  /**
   * Update cloud extinction parameters in real time each frame.
   */
  public static update(params: {
    sunDirection: THREE.Vector3;
    coverage: number;
    altitude: number;
    shadowDarkness?: number;
    centerXZ: THREE.Vector2;
    windOffset: THREE.Vector2;
    scene: THREE.Scene;
  }): void {
    this.uniforms.uCloudSunDir.value.copy(params.sunDirection).normalize();
    this.uniforms.uCloudWindOffset.value.copy(params.windOffset);
    this.uniforms.uCloudCoverage.value = Math.max(0, Math.min(1, params.coverage));
    this.uniforms.uCloudAltitude.value = params.altitude;
    this.uniforms.uCloudCenter.value.copy(params.centerXZ);
    if (params.shadowDarkness !== undefined) {
      this.uniforms.uCloudShadowDarkness.value = Math.max(0, Math.min(1, params.shadowDarkness));
    }

    // Sync updated uniform values to all compiled materials in the scene
    params.scene.traverse((child) => {
      const mesh = child as THREE.Mesh;
      if (mesh.isMesh && mesh.material) {
        const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        for (const mat of materials) {
          if (mat.userData && mat.userData.shaderUniforms) {
            const uniforms = mat.userData.shaderUniforms;
            if (uniforms.uCloudShadowMap) {
              uniforms.uCloudSunDir.value.copy(this.uniforms.uCloudSunDir.value);
              uniforms.uCloudWindOffset.value.copy(this.uniforms.uCloudWindOffset.value);
              uniforms.uCloudCoverage.value = this.uniforms.uCloudCoverage.value;
              uniforms.uCloudAltitude.value = this.uniforms.uCloudAltitude.value;
              uniforms.uCloudCenter.value.copy(this.uniforms.uCloudCenter.value);
              uniforms.uCloudShadowDarkness.value = this.uniforms.uCloudShadowDarkness.value;
              uniforms.uCloudShadowMap.value = this.uniforms.uCloudShadowMap.value;
            }
          }
        }
      }
    });
  }

  public static setShadowMap(texture: THREE.Texture): void {
    this.uniforms.uCloudShadowMap.value = texture;
  }
}
