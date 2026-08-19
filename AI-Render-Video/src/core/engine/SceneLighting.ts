import * as THREE from 'three';
import { EnvironmentConfig, EnvironmentOverride } from '../../types/scene';

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
    this.ambientLight = new THREE.AmbientLight(0xffffff, 0.9);
    this.scene.add(this.ambientLight);

    // Hemisphere Light
    this.hemiLight = new THREE.HemisphereLight(0x93c5fd, 0x334155, 0.6);
    this.hemiLight.position.set(0, 50, 0);
    this.scene.add(this.hemiLight);

    // Directional Sun Light with Shadows
    this.sunLight = new THREE.DirectionalLight(0xfffaed, 2.6);
    this.sunLight.position.set(25, 120, 25);
    this.sunLight.castShadow = true;
    this.sunLight.shadow.mapSize.width = 1024;
    this.sunLight.shadow.mapSize.height = 1024;
    this.sunLight.shadow.camera.near = 0.5;
    this.sunLight.shadow.camera.far = 250;
    const d = 35;
    this.sunLight.shadow.camera.left = -d;
    this.sunLight.shadow.camera.right = d;
    this.sunLight.shadow.camera.top = d;
    this.sunLight.shadow.camera.bottom = -d;
    this.sunLight.shadow.bias = -0.0003;
    this.sunLight.shadow.normalBias = 0.02;
    this.scene.add(this.sunLight);

    // Fog
    this.fog = new THREE.FogExp2(0x93c5fd, 0.012);
    this.scene.fog = this.fog;

    // Visible Glowing Sun Sprite on Celestial Sphere
    const sunTex = this.createSunTexture();
    const sunMat = new THREE.SpriteMaterial({
      map: sunTex,
      color: 0xffffff,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    this.sunSprite = new THREE.Sprite(sunMat);
    this.sunSprite.scale.set(65, 65, 1);
    this.scene.add(this.sunSprite);
  }

  private createSunTexture(): THREE.Texture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    const grad = ctx.createRadialGradient(128, 128, 8, 128, 128, 128);
    grad.addColorStop(0, 'rgba(255, 255, 255, 1)');
    grad.addColorStop(0.2, 'rgba(255, 248, 210, 0.95)');
    grad.addColorStop(0.5, 'rgba(255, 200, 100, 0.45)');
    grad.addColorStop(1, 'rgba(255, 140, 40, 0)');

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

  public updateManual(override: EnvironmentOverride): void {
    let progress = override.sun_position !== undefined ? override.sun_position : 0.5;
    
    if (override.sky_time === 'sunrise' && override.sun_position === undefined) progress = 0.15;
    if (override.sky_time === 'noon' && override.sun_position === undefined) progress = 0.5;
    if (override.sky_time === 'sunset' && override.sun_position === undefined) progress = 0.8;
    if (override.sky_time === 'night' && override.sun_position === undefined) progress = 0.95;

    this.applyLightingState(progress, override);
  }

  private applyLightingState(progress: number, overrideEnv?: EnvironmentOverride): void {
    // Celestial Sphere Sun Trajectory: High above the entire cloud deck (Y = 140m - 240m)
    // progress 0.0 (East Dawn) -> 0.5 (Zenith Noon High) -> 0.85 (West Dusk) -> 1.0 (Night Below Horizon)
    const angle = Math.PI * (0.06 + progress * 0.88);
    const sunDist = 260; // High in the celestial hemisphere
    const sunX = -Math.cos(angle) * sunDist; // East (-X) to West (+X)
    const sunY = Math.sin(angle) * (sunDist * 0.82) + 25; // Always above clouds (Y = 80m - 240m at daytime)
    const sunZ = -45 + Math.sin(progress * Math.PI) * 40;

    this.sunSprite.position.set(sunX, sunY, sunZ);
    // Sun light source follows high celestial angle
    this.sunLight.position.set(sunX * 0.65, Math.max(75, sunY * 0.65), sunZ * 0.65);

    const altitude = Math.max(0, (sunY - 25) / (sunDist * 0.82));

    if (overrideEnv && overrideEnv.fog_density !== undefined) {
      this.fog.density = overrideEnv.fog_density;
    } else {
      this.fog.density = this.currentEnv.weather?.fog || 0.012;
    }

    const explicitMode = overrideEnv ? overrideEnv.sky_time : this.currentEnv.sky_time;
    
    if (explicitMode === 'overcast') {
      this.scene.background = new THREE.Color(0x475569);
      this.fog.color.set(0x64748b);
      this.sunLight.color.set(0x94a3b8);
      this.sunLight.intensity = 1.4;
      this.ambientLight.color.set(0x64748b);
      this.ambientLight.intensity = 0.95;
      this.sunSprite.material.color.set(0x94a3b8);
      this.sunSprite.material.opacity = 0.25;
    } else if (progress >= 0.88 || explicitMode === 'night') {
      this.scene.background = new THREE.Color(0x0a1120);
      this.fog.color.set(0x0f172a);
      this.sunLight.color.set(0x93c5fd);
      this.sunLight.intensity = 1.2;
      this.ambientLight.color.set(0x334155);
      this.ambientLight.intensity = 0.75;
      this.sunSprite.material.color.set(0xd0e2ff);
      this.sunSprite.material.opacity = 0.85;
    } else if (progress >= 0.68 || explicitMode === 'sunset') {
      const sunRatio = Math.max(0, Math.min(1, (progress - 0.68) / 0.2));
      const skyR = THREE.MathUtils.lerp(0.95, 0.82, sunRatio);
      const skyG = THREE.MathUtils.lerp(0.55, 0.32, sunRatio);
      const skyB = THREE.MathUtils.lerp(0.35, 0.42, sunRatio);
      this.scene.background = new THREE.Color(skyR, skyG, skyB);
      this.fog.color.setRGB(skyR, skyG, skyB);

      this.sunLight.color.setRGB(1.0, 0.65, 0.35);
      this.sunLight.intensity = 2.4;
      this.ambientLight.color.setRGB(1.0, 0.82, 0.7);
      this.ambientLight.intensity = 1.1;
      this.sunSprite.material.color.set(0xff7733);
      this.sunSprite.material.opacity = 0.95;
    } else if (progress <= 0.24 || explicitMode === 'sunrise') {
      const dawnRatio = Math.max(0, Math.min(1, progress / 0.24));
      const skyR = THREE.MathUtils.lerp(0.85, 0.96, dawnRatio);
      const skyG = THREE.MathUtils.lerp(0.58, 0.82, dawnRatio);
      const skyB = THREE.MathUtils.lerp(0.65, 0.95, dawnRatio);
      this.scene.background = new THREE.Color(skyR, skyG, skyB);
      this.fog.color.setRGB(skyR, skyG, skyB);

      this.sunLight.color.setRGB(1.0, 0.88, 0.72);
      this.sunLight.intensity = 2.3;
      this.ambientLight.color.setRGB(0.95, 0.85, 0.92);
      this.ambientLight.intensity = 1.1;
      this.sunSprite.material.color.set(0xffbb66);
      this.sunSprite.material.opacity = 0.95;
    } else {
      this.scene.background = new THREE.Color(0x5ea5fb);
      this.fog.color.set(0x93c5fd);
      this.sunLight.color.set(0xffffff);
      this.sunLight.intensity = 2.8;
      this.ambientLight.color.set(0xffffff);
      this.ambientLight.intensity = 1.25;
      this.sunSprite.material.color.set(0xffee88);
      this.sunSprite.material.opacity = 1.0;
    }

    const rain = overrideEnv?.rain_intensity ?? this.currentEnv.weather?.rain ?? 0;
    if (rain > 0.05) {
      const rainFactor = Math.min(1, rain * 1.2);
      this.sunLight.intensity *= (1.0 - rainFactor * 0.55);
      this.ambientLight.intensity *= (1.0 - rainFactor * 0.35);

      const rainSkyColor = new THREE.Color(0x334155).lerp(new THREE.Color(0x1e293b), rainFactor);
      if (this.scene.background instanceof THREE.Color) {
        this.scene.background.lerp(rainSkyColor, rainFactor * 0.7);
      }
      this.fog.color.lerp(rainSkyColor, rainFactor * 0.7);
      this.fog.density = Math.max(this.fog.density, 0.012 + rainFactor * 0.018);
    }
  }
}
