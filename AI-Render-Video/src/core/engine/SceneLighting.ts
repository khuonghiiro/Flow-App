import * as THREE from 'three';
import { EnvironmentConfig } from '../../types/scene';

export class SceneLighting {
  public ambientLight: THREE.AmbientLight;
  public sunLight: THREE.DirectionalLight;
  public hemiLight: THREE.HemisphereLight;
  public fog: THREE.FogExp2;
  public sunMesh: THREE.Mesh;
  public sunGlow: THREE.Mesh;
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

    // Visible 3D Glowing Sun Sphere in the sky
    const sunGeo = new THREE.SphereGeometry(2.5, 24, 24);
    const sunMat = new THREE.MeshBasicMaterial({
      color: 0xffdd66,
    });
    this.sunMesh = new THREE.Mesh(sunGeo, sunMat);
    this.sunMesh.position.set(20, 22, -25);
    this.scene.add(this.sunMesh);

    // Sun Corona Glow Ring
    const glowGeo = new THREE.RingGeometry(2.6, 5.0, 32);
    const glowMat = new THREE.MeshBasicMaterial({
      color: 0xff8833,
      transparent: true,
      opacity: 0.6,
      side: THREE.DoubleSide,
    });
    this.sunGlow = new THREE.Mesh(glowGeo, glowMat);
    this.sunGlow.position.set(20, 22, -24.9);
    this.scene.add(this.sunGlow);
  }

  public applyEnvironment(env: EnvironmentConfig): void {
    this.currentEnv = env;
    if (env.weather?.fog !== undefined) {
      this.fog.density = env.weather.fog;
    }
  }

  public update(currentTime: number, duration: number): void {
    // Dynamic Sun trajectory along sky dome from East to West
    const progress = Math.max(0, Math.min(1, currentTime / Math.max(1, duration)));
    
    // Sun trajectory angle
    const angle = Math.PI * (0.15 + progress * 0.7); // arcs across sky
    const sunDist = 45;
    const sunX = -Math.cos(angle) * sunDist;
    const sunY = Math.sin(angle) * (sunDist * 0.7) - 2;
    const sunZ = -20 + Math.sin(progress * Math.PI) * 10;

    this.sunMesh.position.set(sunX, Math.max(2, sunY), sunZ);
    this.sunGlow.position.set(sunX, Math.max(2, sunY), sunZ + 0.1);
    this.sunGlow.lookAt(this.scene.position);

    this.sunLight.position.set(sunX * 0.7, Math.max(5, sunY * 0.7), Math.max(10, sunZ * -0.5));

    // Dynamic Color Transition based on Sun Altitude & preset
    const altitude = Math.max(0, sunY / (sunDist * 0.7)); // 0 = horizon, 1 = noon

    if (this.currentEnv.sky_time === 'sunset') {
      // Warm golden hour into twilight
      const sunsetFactor = 1 - progress;
      const skyR = THREE.MathUtils.lerp(0.08, 0.25, altitude);
      const skyG = THREE.MathUtils.lerp(0.06, 0.12, altitude);
      const skyB = THREE.MathUtils.lerp(0.15, 0.22, altitude);
      this.scene.background = new THREE.Color(skyR, skyG, skyB);
      this.fog.color.setRGB(skyR * 1.1, skyG * 0.9, skyB * 1.2);

      this.sunLight.color.setRGB(1.0, 0.65 + altitude * 0.3, 0.35 + altitude * 0.4);
      this.sunLight.intensity = 1.2 + altitude * 1.0;
      this.ambientLight.color.setRGB(0.9, 0.6, 0.5);
      this.ambientLight.intensity = 0.4 + altitude * 0.3;
    } else if (this.currentEnv.sky_time === 'sunrise') {
      const skyR = THREE.MathUtils.lerp(0.15, 0.45, altitude);
      const skyG = THREE.MathUtils.lerp(0.12, 0.6, altitude);
      const skyB = THREE.MathUtils.lerp(0.25, 0.85, altitude);
      this.scene.background = new THREE.Color(skyR, skyG, skyB);
      this.fog.color.setRGB(skyR, skyG, skyB);
      this.sunLight.color.setRGB(1.0, 0.85, 0.7);
      this.sunLight.intensity = 1.5 + altitude * 0.8;
    } else if (this.currentEnv.sky_time === 'night') {
      this.scene.background = new THREE.Color(0x060814);
      this.fog.color.set(0x080a18);
      this.sunLight.color.set(0x7799ee);
      this.sunLight.intensity = 0.6;
      this.ambientLight.color.set(0x223355);
      this.ambientLight.intensity = 0.25;
      (this.sunMesh.material as THREE.MeshBasicMaterial).color.set(0xccddee); // Moon
    } else {
      // Noon
      this.scene.background = new THREE.Color(0x66aadd);
      this.fog.color.set(0x88bbdd);
      this.sunLight.color.set(0xffffff);
      this.sunLight.intensity = 2.2;
      this.ambientLight.color.set(0xffffff);
      this.ambientLight.intensity = 0.5;
    }
  }
}
