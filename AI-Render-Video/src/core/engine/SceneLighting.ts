import * as THREE from 'three';
import { EnvironmentConfig, EnvironmentOverride } from '../../types/scene';
import { SkyboxManager } from '../assets/SkyboxManager';

export class SceneLighting {
  public ambientLight: THREE.AmbientLight;
  public sunLight: THREE.DirectionalLight;
  public hemiLight: THREE.HemisphereLight;
  public fog: THREE.FogExp2;
  public sunSprite: THREE.Sprite;
  private scene: THREE.Scene;
  private currentEnv: EnvironmentConfig = {
    map: 'farming_village',
    sky_time: 'noon',
    weather: { fog: 0.012, wind: 0.3 },
  };

  constructor(scene: THREE.Scene) {
    this.scene = scene;

    // Ambient Light
    this.ambientLight = new THREE.AmbientLight(0xffffff, 1.3);
    this.scene.add(this.ambientLight);

    // Hemisphere Light
    this.hemiLight = new THREE.HemisphereLight(0xbae6fd, 0x475569, 1.0);
    this.hemiLight.position.set(0, 50, 0);
    this.scene.add(this.hemiLight);

    // Directional Sun Light with Shadows (high precision shadow map)
    this.sunLight = new THREE.DirectionalLight(0xfff1d2, 3.2);
    this.sunLight.position.set(50, 120, 50);
    this.sunLight.castShadow = true;
    this.sunLight.shadow.mapSize.width = 2048;
    this.sunLight.shadow.mapSize.height = 2048;
    this.sunLight.shadow.camera.near = 1.0;
    this.sunLight.shadow.camera.far = 350.0;
    const d = 80;
    this.sunLight.shadow.camera.left = -d;
    this.sunLight.shadow.camera.right = d;
    this.sunLight.shadow.camera.top = d;
    this.sunLight.shadow.camera.bottom = -d;
    this.sunLight.shadow.bias = -0.0001;
    this.sunLight.shadow.normalBias = 0.02;
    this.scene.add(this.sunLight);
    this.scene.add(this.sunLight.target);

    // Fog (Soft atmospheric horizon haze)
    this.fog = new THREE.FogExp2(0x93c5fd, 0.001);
    this.scene.fog = this.fog;

    // Visible Glowing Sun Sprite on Celestial Sphere
    const sunTex = this.createSunTexture();
    const sunMat = new THREE.SpriteMaterial({
      map: sunTex,
      color: 0xffffff,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false, // Celestial sun is never obscured by ground fog
    });
    this.sunSprite = new THREE.Sprite(sunMat);
    this.sunSprite.scale.set(320, 320, 1);
    this.sunSprite.renderOrder = 1; // Behind 3D clouds (renderOrder 100) so clouds pass in front of the sun
    this.scene.add(this.sunSprite);
  }

  private createSunTexture(): THREE.Texture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    // Rayleigh scattering solar disk: warm golden core with amber corona and atmospheric flare
    const grad = ctx.createRadialGradient(128, 128, 6, 128, 128, 128);
    grad.addColorStop(0, 'rgba(255, 252, 205, 1.0)');
    grad.addColorStop(0.20, 'rgba(255, 220, 90, 0.98)');
    grad.addColorStop(0.45, 'rgba(255, 175, 40, 0.65)');
    grad.addColorStop(0.75, 'rgba(255, 130, 20, 0.25)');
    grad.addColorStop(1, 'rgba(255, 90, 0, 0)');

    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(128, 128, 128, 0, Math.PI * 2);
    ctx.fill();

    return new THREE.CanvasTexture(canvas);
  }

  public setEnvironment(env: EnvironmentConfig): void {
    this.currentEnv = env;
  }

  public applyEnvironment(env: EnvironmentConfig): void {
    this.currentEnv = env;
  }

  public update(currentTime: number, duration: number): void {
    const progress = Math.max(0, Math.min(1, currentTime / Math.max(1, duration)));
    this.applyLightingState(progress);
  }

  public updateManual(override?: EnvironmentOverride, flashIntensity: number = 0): void {
    let progress = override?.sun_position !== undefined ? override.sun_position : 0.5;

    if (override?.sky_time === 'sunrise' && override.sun_position === undefined) progress = 0.15;
    if (override?.sky_time === 'noon' && override.sun_position === undefined) progress = 0.5;
    if (override?.sky_time === 'sunset' && override.sun_position === undefined) progress = 0.8;
    if (override?.sky_time === 'night' && override.sun_position === undefined) progress = 0.95;

    this.applyLightingState(progress, override, flashIntensity);
  }

  private applyLightingState(progress: number, overrideEnv?: EnvironmentOverride, flashIntensity: number = 0): void {
    // Celestial Sphere Sun Trajectory
    const angle = Math.PI * (0.06 + progress * 0.88);
    const sunDist = 2400;
    const sunX = -Math.cos(angle) * (sunDist * 0.85);
    const sunY = Math.sin(angle) * 1800 + 400;
    const sunZ = -300 + Math.sin(progress * Math.PI) * 300;

    this.sunSprite.position.set(sunX, sunY, sunZ);

    // Directional shadow caster light positioned close (120m) for millimeter-precision shadow maps
    const sunDir = new THREE.Vector3(sunX, sunY, sunZ).normalize();
    this.sunLight.position.set(sunDir.x * 140, Math.max(35, sunDir.y * 140), sunDir.z * 140);
    this.sunLight.target.position.set(0, 0, 0);

    const altitude = Math.max(0, (sunY - 25) / (sunDist * 0.82));

    if (overrideEnv && overrideEnv.fog_density !== undefined) {
      this.fog.density = Math.min(0.002, overrideEnv.fog_density);
    } else {
      this.fog.density = Math.min(0.002, this.currentEnv.weather?.fog || 0.001);
    }

    const explicitMode = overrideEnv ? overrideEnv.sky_time : this.currentEnv.sky_time;
    const skyboxType = overrideEnv?.skybox_type ?? this.currentEnv.weather?.skybox_type ?? 'none';
    const isSkyboxActive = skyboxType !== 'none';

    // ── 1. Calculate lighting & colors based on time/mode (Rayleigh & Mie Scattering) ──
    let targetBgColor = new THREE.Color(0x5ea5fb);
    let targetFogColor = new THREE.Color(0x93c5fd);
    let targetSunColor = new THREE.Color(0xfff1d2); // Warm golden sunlight (Rayleigh filtered)
    let targetSunIntensity = 3.2;                   // Radiant direct sunlight for crisp contrast
    let targetAmbColor = new THREE.Color(0x6b8cb8); // Cool blue sky diffuse scatter
    let targetAmbIntensity = 0.42;                  // Balanced ambient floor for rich shadow contrast
    let targetSpriteColor = new THREE.Color(0xffe888); // Radiant golden solar core
    let targetSpriteOpacity = isSkyboxActive ? 0.0 : 1.0; // Hide 2D procedural sun when using photo skybox

    if (explicitMode === 'overcast') {
      targetBgColor.set(0x475569);
      targetFogColor.set(0x64748b);
      targetSunColor.set(0x94a3b8);
      targetSunIntensity = 1.4;
      targetAmbColor.set(0x64748b);
      targetAmbIntensity = 0.95;
      targetSpriteColor.set(0x94a3b8);
      targetSpriteOpacity = isSkyboxActive ? 0.0 : 0.25;
    } else if (progress >= 0.88 || explicitMode === 'night') {
      targetBgColor.set(0x0a1120);
      targetFogColor.set(0x0f172a);
      targetSunColor.set(0x93c5fd);
      targetSunIntensity = 1.2;
      targetAmbColor.set(0x334155);
      targetAmbIntensity = 0.75;
      targetSpriteColor.set(0xd0e2ff);
      targetSpriteOpacity = isSkyboxActive ? 0.0 : 0.85;
    } else if (progress >= 0.68 || explicitMode === 'sunset') {
      const sunRatio = Math.max(0, Math.min(1, (progress - 0.68) / 0.2));
      const skyR = THREE.MathUtils.lerp(0.95, 0.82, sunRatio);
      const skyG = THREE.MathUtils.lerp(0.55, 0.32, sunRatio);
      const skyB = THREE.MathUtils.lerp(0.35, 0.42, sunRatio);
      targetBgColor.setRGB(skyR, skyG, skyB);
      targetFogColor.setRGB(skyR, skyG, skyB);

      targetSunColor.setRGB(1.0, 0.65, 0.35);
      targetSunIntensity = 2.4;
      targetAmbColor.setRGB(1.0, 0.82, 0.7);
      targetAmbIntensity = 1.1;
      targetSpriteColor.set(0xff7733);
      targetSpriteOpacity = isSkyboxActive ? 0.0 : 0.95;
    } else if (progress <= 0.24 || explicitMode === 'sunrise') {
      const dawnRatio = Math.max(0, Math.min(1, progress / 0.24));
      const skyR = THREE.MathUtils.lerp(0.85, 0.96, dawnRatio);
      const skyG = THREE.MathUtils.lerp(0.58, 0.82, dawnRatio);
      const skyB = THREE.MathUtils.lerp(0.65, 0.95, dawnRatio);
      targetBgColor.setRGB(skyR, skyG, skyB);
      targetFogColor.setRGB(skyR, skyG, skyB);

      targetSunColor.setRGB(1.0, 0.88, 0.72);
      targetSunIntensity = 2.3;
      targetAmbColor.setRGB(0.95, 0.85, 0.92);
      targetAmbIntensity = 1.1;
      targetSpriteColor.set(0xffbb66);
      targetSpriteOpacity = isSkyboxActive ? 0.0 : 0.95;
    }

    // Skybox-specific color & lighting harmonies
    if (skyboxType === 'night' || skyboxType === 'space') {
      targetFogColor.set(0x0a1020);
      targetSunColor.set(0x93c5fd);
      targetSunIntensity = 1.1;
      targetAmbColor.set(0x223355);
      targetAmbIntensity = 0.7;
    } else if (skyboxType === 'alien') {
      targetFogColor.set(0x2a1538);
      targetSunColor.set(0xf472b6);
      targetSunIntensity = 2.2;
      targetAmbColor.set(0x818cf8);
      targetAmbIntensity = 1.1;
    } else if (skyboxType === 'morning') {
      targetFogColor.set(0xd4a882);
      targetSunColor.set(0xffbc75);
      targetSunIntensity = 2.4;
      targetAmbColor.set(0xfed7aa);
      targetAmbIntensity = 1.15;
    }

    // Rain & Cloud Darkness factor
    const rain = overrideEnv?.rain_intensity ?? this.currentEnv.weather?.rain ?? 0;
    const rainDarkness = overrideEnv?.rain_darkness ?? 0.5;
    const cloudShadowDarkness = overrideEnv?.cloud_shadow_darkness ?? this.currentEnv.weather?.cloud_shadow_darkness ?? 0.4;
    const cloudCov = overrideEnv?.cloud_coverage ?? this.currentEnv.weather?.cloud_coverage ?? 0.0;

    if (rain > 0.05) {
      // Light rain (<= 0.35): sunlight still gently pierces through fluffy clouds with warm soft rays
      // Heavy rain (> 0.35 to 1.0): sunlight smoothly dims and extinguishes during intense storms
      const rainSunDimming = rain <= 0.35 ? (rain * 0.55) : (0.19 + (rain - 0.35) * 1.25);
      targetSunIntensity *= Math.max(0.0, 1.0 - rainSunDimming);

      // Ambient light stays vibrant and readable (atmospheric diffuse scattering)
      const rainAmbTint = new THREE.Color(0x768fae);
      targetAmbColor.lerp(rainAmbTint, rain * 0.65);
      targetAmbIntensity = THREE.MathUtils.lerp(targetAmbIntensity, 0.88, rain * 0.40);

      // Dark storm clouds: dynamically darken fog from soft slate to deep charcoal black
      const darkStormColor = new THREE.Color(0x1e293b).lerp(new THREE.Color(0x0a0f1d), rainDarkness * 0.95);
      targetBgColor.lerp(darkStormColor, Math.min(1.0, rain * 0.88));
      targetFogColor.lerp(darkStormColor, Math.min(1.0, rain * 0.92));

      if (overrideEnv?.fog_density === undefined) {
        this.fog.density = 0.005 + rain * 0.008;
      }
    }

    // Cloud coverage and shadow darkness dynamically tint atmospheric fog
    if (cloudCov > 0.03 && explicitMode !== 'overcast') {
      targetAmbIntensity = Math.max(0.95, targetAmbIntensity * (1.0 - cloudCov * 0.12));
      
      // When clouds are thick and dark, tint fog towards cloud shadow color
      const overcastFogTint = new THREE.Color(0x334155).lerp(new THREE.Color(0x0f172a), cloudShadowDarkness * 0.8);
      targetFogColor.lerp(overcastFogTint, cloudCov * 0.45 * (0.3 + cloudShadowDarkness * 0.7));
    }

    // Atomic lightning flash applied on top of clean base frame lighting (No whiteout accumulation)
    if (flashIntensity > 0.005) {
      // Direct sunlight with shadow map gets the main flash (respects shadows, roofs & walls)
      targetSunIntensity += flashIntensity * 5.0;
      targetSunColor.lerp(new THREE.Color(0xdbeafe), flashIntensity * 0.95);

      // Zero ambient flooding so dark interiors beneath ceilings stay 100% dark!
      targetAmbIntensity += flashIntensity * 0.02;
      this.hemiLight.intensity = 0.85;
      targetFogColor.lerp(new THREE.Color(0xb0e0ff), flashIntensity * 0.35);
    } else {
      // Balanced hemisphere sky-to-ground fill keeps all 3D characters, houses, and roofs clearly readable
      this.hemiLight.intensity = 1.05;
      this.hemiLight.color.set(0xbae6fd);
      this.hemiLight.groundColor.set(0x475569);
    }

    // Apply lights & sprite
    this.sunLight.color.copy(targetSunColor);
    this.sunLight.intensity = targetSunIntensity;
    this.ambientLight.color.copy(targetAmbColor);
    this.ambientLight.intensity = targetAmbIntensity;
    this.sunSprite.material.color.copy(targetSpriteColor);
    this.sunSprite.material.opacity = targetSpriteOpacity;

    // ── 2. Background handling: Skybox Texture VS Procedural Color ──
    if (isSkyboxActive) {
      // Keep fog subtle so skybox remains crystal clear like in Unity
      if (overrideEnv?.fog_density === undefined && !this.currentEnv.weather?.fog) {
        this.fog.density = 0.004; // Atmospheric ground haze
      }
      this.fog.color.copy(targetFogColor);
      this.updateSkybox(overrideEnv);
    } else {
      // Procedural Sky Color
      this.scene.background = targetBgColor;
      this.scene.environment = null;
      this.fog.color.copy(targetFogColor);
      this.activeSkyboxKey = 'none';
    }
  }

  // ══════════════════════════════════════════════════
  // Skybox Texture Management (Equirectangular 360°)
  // ══════════════════════════════════════════════════

  private textureLoader = new THREE.TextureLoader();
  private skyboxCache = new Map<string, THREE.Texture>();
  private activeSkyboxKey = 'none';

  private static SKYBOX_PRESETS: Record<string, string> = {
    day: '/assets/SkyBoxs/skybox-day.png',
    morning: '/assets/SkyBoxs/skybox-morning.png',
    night: '/assets/SkyBoxs/skybox-night.png',
    space: '/assets/SkyBoxs/skybox-space.png',
    alien: '/assets/SkyBoxs/skybox-alien.png',
  };

  private updateSkybox(overrideEnv?: EnvironmentOverride): void {
    const skyboxType = overrideEnv?.skybox_type ?? this.currentEnv.weather?.skybox_type ?? 'none';
    const customUrl = overrideEnv?.skybox_url ?? this.currentEnv.weather?.skybox_url ?? '';
    const rotationDeg = overrideEnv?.skybox_rotation ?? this.currentEnv.weather?.skybox_rotation ?? 0;
    const exposure = overrideEnv?.skybox_exposure ?? this.currentEnv.weather?.skybox_exposure ?? 1.0;
    const blur = overrideEnv?.skybox_blur ?? this.currentEnv.weather?.skybox_blur ?? 0.0;

    if (skyboxType === 'none') {
      if (this.activeSkyboxKey !== 'none') {
        this.activeSkyboxKey = 'none';
        this.scene.background = null;
        this.scene.environment = null;
      }
      return;
    }

    let targetUrl = '';
    if (skyboxType === 'auto') {
      const matched = SkyboxManager.getMatchingSkybox({
        skyTime: overrideEnv?.sky_time ?? this.currentEnv.sky_time,
        sunPosition: overrideEnv?.sun_position,
        cloudCoverage: overrideEnv?.cloud_coverage ?? this.currentEnv.weather?.cloud_coverage,
        rainIntensity: overrideEnv?.rain_intensity ?? this.currentEnv.weather?.rain,
      });
      targetUrl = matched?.url || '';
    } else if (skyboxType === 'custom') {
      targetUrl = customUrl;
    } else if (SceneLighting.SKYBOX_PRESETS[skyboxType]) {
      targetUrl = SceneLighting.SKYBOX_PRESETS[skyboxType];
    } else {
      // Check if skyboxType is a direct URL or catalog item
      const fromCatalog = SkyboxManager.CATALOG.find((item) => item.id === skyboxType || item.url === skyboxType);
      targetUrl = fromCatalog ? fromCatalog.url : (skyboxType.startsWith('/') || skyboxType.startsWith('blob:') || skyboxType.startsWith('http') ? skyboxType : '');
    }

    if (!targetUrl) return;

    this.activeSkyboxKey = targetUrl;
    const cached = this.skyboxCache.get(targetUrl);

    if (cached) {
      // Apply immediately from memory cache
      this.applySkyboxTexture(cached, rotationDeg, exposure, blur);
    } else {
      // Load asynchronously and cache
      this.textureLoader.load(
        targetUrl,
        (tex) => {
          tex.mapping = THREE.EquirectangularReflectionMapping;
          tex.colorSpace = THREE.SRGBColorSpace;
          tex.generateMipmaps = true;
          tex.minFilter = THREE.LinearMipmapLinearFilter;
          tex.magFilter = THREE.LinearFilter;
          this.skyboxCache.set(targetUrl, tex);

          if (this.activeSkyboxKey === targetUrl) {
            this.applySkyboxTexture(tex, rotationDeg, exposure, blur);
          }
        },
        undefined,
        (err) => console.error('Failed to load skybox texture:', targetUrl, err)
      );
    }
  }

  private applySkyboxTexture(tex: THREE.Texture, rotationDeg: number, exposure: number, blur: number): void {
    this.scene.background = tex;
    this.scene.environment = tex; // Enables full Unity HDRP-style IBL reflections!
    this.applySkyboxParams(rotationDeg, exposure, blur);
  }

  private applySkyboxParams(rotationDeg: number, exposure: number, blur: number): void {
    const rotRad = THREE.MathUtils.degToRad(rotationDeg);
    this.scene.backgroundRotation.y = rotRad;
    this.scene.environmentRotation.y = rotRad;
    this.scene.backgroundIntensity = exposure;
    this.scene.backgroundBlurriness = blur;
    this.scene.environmentIntensity = exposure * 0.85;
  }

  public updateShadowTarget(cameraPos: THREE.Vector3): void {
    this.sunLight.target.position.set(cameraPos.x, 0, cameraPos.z);
    this.sunLight.target.updateMatrixWorld();
  }
}
