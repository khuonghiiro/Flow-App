import * as THREE from 'three';

interface RainDropData {
  pos: THREE.Vector3;
  speed: number;
  length: number;
  width: number;
  phase: number;
}

/**
 * WeatherParticleSystem (AAA High-Contrast 3D Water Droplet Engine)
 * Features:
 * - 10,000+ dense instanced water droplet planes
 * - High-contrast refractive water core (clearly visible in daylight, noon, sunset, and night)
 * - True continuous asynchronous fall loop (zero banding, zero empty gaps)
 * - Aerodynamic velocity alignment and wind slanting
 * - Removed distracting snowflake-like mist dots and fake splash rings
 */
export class WeatherParticleSystem {
  private scene: THREE.Scene;
  private rainGroup: THREE.Group;

  // Instanced 3D Water Droplet Quads
  private instancedRain: THREE.InstancedMesh | null = null;
  private maxRainCount: number = 10000;
  private rainDrops: RainDropData[] = [];
  private rainTexture: THREE.CanvasTexture | null = null;
  private rainMaterial: THREE.MeshBasicMaterial | null = null;
  private dummyObj: THREE.Object3D = new THREE.Object3D();

  // Weather state
  private activeRainIntensity: number = 0;
  private activeWindIntensity: number = 0.3;
  private activeWindDirection: number = 45;
  private gustTimer: number = 0;

  // Volume bounds around active camera
  private boxWidth: number = 42;
  private boxHeight: number = 30;
  private boxDepth: number = 42;

  constructor(scene: THREE.Scene) {
    this.scene = scene;
    this.rainGroup = new THREE.Group();
    this.rainGroup.name = 'optical_rain_engine';
    this.scene.add(this.rainGroup);

    this.rainTexture = this.generateHighContrastRainTexture();
    this.initInstancedRain();
  }

  /**
   * Generates a high-contrast, photorealistic optical water drop texture
   * clearly visible against bright sky, green terrain, and dark nights.
   */
  private generateHighContrastRainTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    ctx.clearRect(0, 0, 64, 256);

    // Draw high-contrast tapered water drop streak
    // Leading water head near bottom (Y = 225), trailing tail fading to top (Y = 8)
    for (let y = 8; y < 235; y += 2) {
      const progress = (y - 8) / 227; // 0 (tail) -> 1 (head)
      const halfW = 1.8 + Math.sin(progress * Math.PI * 0.85) * 8.0;
      
      // High-contrast alpha: strong water core
      const alpha = Math.pow(progress, 1.3) * 0.92;

      const grad = ctx.createLinearGradient(32 - halfW, y, 32 + halfW, y);
      grad.addColorStop(0, 'rgba(180, 210, 240, 0)');
      grad.addColorStop(0.25, `rgba(200, 225, 255, ${alpha * 0.55})`);
      grad.addColorStop(0.5, `rgba(255, 255, 255, ${alpha})`);
      grad.addColorStop(0.75, `rgba(200, 225, 255, ${alpha * 0.55})`);
      grad.addColorStop(1, 'rgba(180, 210, 240, 0)');

      ctx.fillStyle = grad;
      ctx.fillRect(32 - halfW, y, halfW * 2, 2);
    }

    // Leading refraction highlight
    const headGrad = ctx.createRadialGradient(32, 228, 1, 32, 228, 7.5);
    headGrad.addColorStop(0, 'rgba(255, 255, 255, 1.0)');
    headGrad.addColorStop(0.4, 'rgba(220, 240, 255, 0.85)');
    headGrad.addColorStop(0.8, 'rgba(180, 215, 255, 0.35)');
    headGrad.addColorStop(1, 'rgba(180, 215, 255, 0)');
    ctx.fillStyle = headGrad;
    ctx.beginPath();
    ctx.arc(32, 228, 7.5, 0, Math.PI * 2);
    ctx.fill();

    return new THREE.CanvasTexture(canvas);
  }

  /**
   * Initializes Instanced 3D Water Droplet Planes
   */
  private initInstancedRain(): void {
    if (!this.rainTexture) return;

    // Unit Quad centered at bottom head
    const geom = new THREE.PlaneGeometry(0.05, 0.95);
    geom.translate(0, 0.475, 0); // pivot at head

    this.rainMaterial = new THREE.MeshBasicMaterial({
      map: this.rainTexture,
      transparent: true,
      opacity: 0.85,
      blending: THREE.NormalBlending,
      depthWrite: false,
      side: THREE.DoubleSide,
    });

    this.instancedRain = new THREE.InstancedMesh(geom, this.rainMaterial, this.maxRainCount);
    this.instancedRain.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.instancedRain.frustumCulled = false;
    this.instancedRain.visible = false;
    this.rainGroup.add(this.instancedRain);

    // Populate droplet particle data with continuous height distribution
    for (let i = 0; i < this.maxRainCount; i++) {
      const x = (Math.random() - 0.5) * this.boxWidth;
      // Staggered continuous height distribution across entire height
      const y = Math.random() * (this.boxHeight + 10);
      const z = (Math.random() - 0.5) * this.boxDepth;

      this.rainDrops.push({
        pos: new THREE.Vector3(x, y, z),
        speed: 22 + Math.random() * 18, // 22 to 40 m/s
        length: 0.85 + Math.random() * 0.55,
        width: 0.85 + Math.random() * 0.5,
        phase: Math.random() * Math.PI * 2,
      });

      this.dummyObj.position.set(x, y, z);
      this.dummyObj.updateMatrix();
      this.instancedRain.setMatrixAt(i, this.dummyObj.matrix);
    }

    this.instancedRain.instanceMatrix.needsUpdate = true;
  }

  /**
   * Update rain droplets with 3D orientation, camera tracking, and wind drift
   */
  public update(
    delta: number,
    cameraPos: THREE.Vector3,
    rainIntensity: number = 0,
    windIntensity: number = 0.3,
    windDirectionDeg: number = 45
  ): void {
    this.activeRainIntensity = Math.max(0, Math.min(1, rainIntensity));
    this.activeWindIntensity = Math.max(0, Math.min(1, windIntensity));
    this.activeWindDirection = windDirectionDeg;
    this.gustTimer += delta * (1.1 + this.activeWindIntensity * 2.2);

    if (this.activeRainIntensity <= 0.01) {
      if (this.instancedRain && this.instancedRain.visible) this.instancedRain.visible = false;
      return;
    }

    if (this.instancedRain && !this.instancedRain.visible) this.instancedRain.visible = true;

    const activeCount = Math.floor(this.maxRainCount * this.activeRainIntensity);
    if (this.instancedRain) {
      this.instancedRain.count = activeCount;
    }

    // Wind velocities & natural gusts
    const windRad = (this.activeWindDirection * Math.PI) / 180;
    const gustWave = 1.0 + Math.sin(this.gustTimer) * 0.2 + Math.cos(this.gustTimer * 0.75) * 0.12;
    const effectiveWind = this.activeWindIntensity * gustWave;

    const windVelX = Math.sin(windRad) * (effectiveWind * 26.0);
    const windVelZ = Math.cos(windRad) * (effectiveWind * 26.0);
    const fallAccel = 1.0 + this.activeWindIntensity * 0.4 + this.activeRainIntensity * 0.35;

    // Slanted rotation angle of raindrops along wind
    const slantAngleX = Math.atan2(windVelZ, 28 * fallAccel);
    const slantAngleZ = -Math.atan2(windVelX, 28 * fallAccel);

    const halfW = this.boxWidth * 0.5;
    const halfD = this.boxDepth * 0.5;
    const camX = cameraPos.x;
    const camY = cameraPos.y;
    const camZ = cameraPos.z;

    // Update 3D Water Droplet Instanced Meshes
    if (this.instancedRain) {
      for (let i = 0; i < activeCount; i++) {
        const drop = this.rainDrops[i];

        // Fall downward & drift with wind
        drop.pos.x += windVelX * delta;
        drop.pos.y -= drop.speed * fallAccel * delta;
        drop.pos.z += windVelZ * delta;

        // Continuous individual reset when hitting ground (Y < 0) or leaving camera view
        const relX = drop.pos.x - camX;
        const relZ = drop.pos.z - camZ;

        if (
          drop.pos.y < 0 ||
          relX < -halfW ||
          relX > halfW ||
          relZ < -halfD ||
          relZ > halfD ||
          drop.pos.y > camY + this.boxHeight + 10
        ) {
          // Asynchronous continuous height offset to completely prevent grouped wave banding
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
          drop.pos.y = Math.max(camY + 8, 14) + Math.random() * (this.boxHeight * 0.65);
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        }

        this.dummyObj.position.copy(drop.pos);
        // Slant along wind vector
        this.dummyObj.rotation.set(slantAngleX, 0, slantAngleZ);
        // Scale droplet length based on speed
        this.dummyObj.scale.set(drop.width, drop.length * (1.0 + this.activeWindIntensity * 0.65), 1.0);
        this.dummyObj.updateMatrix();

        this.instancedRain.setMatrixAt(i, this.dummyObj.matrix);
      }

      this.instancedRain.instanceMatrix.needsUpdate = true;
    }

    if (this.rainMaterial) {
      // High visibility opacity across all lighting conditions
      this.rainMaterial.opacity = 0.55 + this.activeRainIntensity * 0.42;
    }
  }

  /**
   * Dispose resources
   */
  public dispose(): void {
    if (this.instancedRain) {
      this.instancedRain.geometry.dispose();
      (this.instancedRain.material as THREE.Material).dispose();
    }
    if (this.rainTexture) this.rainTexture.dispose();

    if (this.rainGroup.parent) {
      this.rainGroup.parent.remove(this.rainGroup);
    }
  }
}
