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
    sky_time: 'sunset',
    weather: { fog: 0.012, wind: 0.3 },
  };

  constructor(scene: THREE.Scene) {
    this.scene = scene;

    // Ambient
    this.ambientLight = new THREE.AmbientLight(0xff9977, 0.5);
    this.scene.add(this.ambientLight);

    // Hemisphere
    this.hemiLight = new THREE.HemisphereLight(0xff7755, 0x221133, 0.5);
    this.hemiLight.position.set(0, 50, 0);
    this.scene.add(this.hemiLight);

    // Directional Sun Light with Soft Shadows
    this.sunLight = new THREE.DirectionalLight(0xffaa55, 2.0);
    this.sunLight.position.set(15, 20, 15);
    this.sunLight.castShadow = true;
    this.sunLight.shadow.mapSize.width = 2048;
    this.sunLight.shadow.mapSize.height = 2048;
    this.sunLight.shadow.camera.near = 0.5;
    this.sunLight.shadow.camera.far = 120;
    const d = 25;
    this.sunLight.shadow.camera.left = -d;
    this.sunLight.shadow.camera.right = d;
    this.sunLight.shadow.camera.top = d;
    this.sunLight.shadow.camera.bottom = -d;
    this.sunLight.shadow.bias = -0.0005;
    this.scene.add(this.sunLight);

    // Fog
    this.fog = new THREE.FogExp2(0x2d172e, 0.012);
    this.scene.fog = this.fog;

    // Visible 3D Glowing Sun Sprite (Lens Flare effect)
    const sunTex = this.createSunTexture();
    const sunMat = new THREE.SpriteMaterial({
      map: sunTex,
      color: 0xffffff,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    this.sunSprite = new THREE.Sprite(sunMat);
    this.sunSprite.scale.set(30, 30, 1);
    this.sunSprite.position.set(20, 22, -25);
    this.scene.add(this.sunSprite);
  }

  private createSunTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 512;
    const ctx = canvas.getContext('2d')!;
    
    const gradient = ctx.createRadialGradient(256, 256, 0, 256, 256, 256);
    gradient.addColorStop(0, 'rgba(255, 255, 255, 1)');
    gradient.addColorStop(0.1, 'rgba(255, 255, 220, 0.9)');
    gradient.addColorStop(0.3, 'rgba(255, 200, 100, 0.6)');
    gradient.addColorStop(0.6, 'rgba(255, 100, 50, 0.2)');
    gradient.addColorStop(0.8, 'rgba(100, 20, 0, 0.05)');
    gradient.addColorStop(1, 'rgba(0, 0, 0, 0)');
    
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, 512, 512);
    
    return new THREE.CanvasTexture(canvas);
  }

  public applyEnvironment(env: EnvironmentConfig): void {
    this.currentEnv = env;
    if (env.weather?.fog !== undefined) {
      this.fog.density = env.weather.fog;
    }
  }

  public update(currentTime: number, duration: number): void {
    const progress = Math.max(0, Math.min(1, currentTime / Math.max(1, duration)));
    this.applyLightingState(progress);
  }

  public updateManual(override: EnvironmentOverride): void {
    const progress = override.sun_position !== undefined ? override.sun_position : 0.5;
    this.applyLightingState(progress, override);
  }

  private applyLightingState(progress: number, overrideEnv?: EnvironmentOverride): void {
    // Dynamic Sun trajectory along sky dome from East to West
    const angle = Math.PI * (0.15 + progress * 0.7); // arcs across sky
    const sunDist = 45;
    const sunX = -Math.cos(angle) * sunDist;
    const sunY = Math.sin(angle) * (sunDist * 0.7) - 2;
    const sunZ = -20 + Math.sin(progress * Math.PI) * 10;

    this.sunSprite.position.set(sunX, Math.max(2, sunY), sunZ);
    this.sunLight.position.set(sunX * 0.7, Math.max(5, sunY * 0.7), Math.max(10, sunZ * -0.5));

    // Dynamic Color Transition based on Sun Altitude & preset
    const altitude = Math.max(0, sunY / (sunDist * 0.7)); // 0 = horizon, 1 = noon
    
    const activeEnvMode = overrideEnv ? overrideEnv.sky_time : this.currentEnv.sky_time;
    
    if (overrideEnv && overrideEnv.fog_density !== undefined) {
      this.fog.density = overrideEnv.fog_density;
    } else {
      this.fog.density = this.currentEnv.weather?.fog || 0.012;
    }

    if (activeEnvMode === 'sunset') {
      // Warm golden hour into rich twilight
      const skyR = THREE.MathUtils.lerp(0.85, 0.95, altitude);
      const skyG = THREE.MathUtils.lerp(0.42, 0.58, altitude);
      const skyB = THREE.MathUtils.lerp(0.28, 0.45, altitude);
      this.scene.background = new THREE.Color(skyR, skyG, skyB);
      this.fog.color.setRGB(skyR, skyG, skyB);

      this.sunLight.color.setRGB(1.0, 0.75 + altitude * 0.2, 0.45 + altitude * 0.4);
      this.sunLight.intensity = 2.2 + altitude * 0.8;
      this.ambientLight.color.setRGB(1.0, 0.85, 0.75);
      this.ambientLight.intensity = 1.0 + altitude * 0.3;
      this.sunSprite.material.color.set(0xffaa44);
    } else if (activeEnvMode === 'sunrise') {
      const skyR = THREE.MathUtils.lerp(0.92, 0.96, altitude);
      const skyG = THREE.MathUtils.lerp(0.65, 0.82, altitude);
      const skyB = THREE.MathUtils.lerp(0.48, 0.72, altitude);
      this.scene.background = new THREE.Color(skyR, skyG, skyB);
      this.fog.color.setRGB(skyR, skyG, skyB);
      this.sunLight.color.setRGB(1.0, 0.9, 0.75);
      this.sunLight.intensity = 2.2 + altitude * 0.8;
      this.ambientLight.color.setRGB(0.95, 0.85, 0.9);
      this.ambientLight.intensity = 1.0 + altitude * 0.3;
      this.sunSprite.material.color.set(0xffbb55);
    } else if (activeEnvMode === 'night') {
      this.scene.background = new THREE.Color(0x0f172a);
      this.fog.color.set(0x0f172a);
      this.sunLight.color.set(0x93c5fd);
      this.sunLight.intensity = 1.5;
      this.ambientLight.color.set(0x475569);
      this.ambientLight.intensity = 0.9;
      this.sunSprite.material.color.set(0xccddee); // Moon
    } else {
      // Noon / Daytime
      this.scene.background = new THREE.Color(0x60a5fa);
      this.fog.color.set(0x93c5fd);
      this.sunLight.color.set(0xffffff);
      this.sunLight.intensity = 2.8;
      this.ambientLight.color.set(0xffffff);
      this.ambientLight.intensity = 1.2;
      this.sunSprite.material.color.set(0xffee88);
    }
  }
}
