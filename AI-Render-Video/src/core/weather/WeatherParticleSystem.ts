import * as THREE from 'three';

interface RainDropData {
  pos: THREE.Vector3;
  speed: number;
  length: number;
  width: number;
  phase: number;
}

interface SplashParticle {
  pos: THREE.Vector3;
  vel: THREE.Vector3;
  scale: number;
  life: number;
  maxLife: number;
  active: boolean;
}

interface RippleParticle {
  pos: THREE.Vector3;
  currentScale: number;
  maxScale: number;
  life: number;
  maxLife: number;
  active: boolean;
}

/**
 * WeatherParticleSystem — Spatial Grid Cached Raycasting Rain Collision
 *
 * Chia khu vực mưa thành lưới ô 0.5m x 0.5m.
 * Mỗi ô chỉ raycast 1 LẦN DUY NHẤT rồi cache surface Y.
 * Các giọt mưa cùng ô dùng chung kết quả → gần như 0 cost sau khi fill.
 *
 * Chỉ raycast ô NẰM TRONG camera frustum (ô ngoài tầm nhìn bỏ qua).
 * Giới hạn 8 raycasts/frame để giữ 60fps trên model 2M+ polygon.
 */
export class WeatherParticleSystem {
  private scene: THREE.Scene;
  private rainGroup: THREE.Group;

  // Instanced Rain Streaks
  private instancedRain: THREE.InstancedMesh | null = null;
  private maxRainCount: number = 4000;
  private rainDrops: RainDropData[] = [];
  private rainMaterial: THREE.MeshBasicMaterial | null = null;

  // Instanced Water Splashes
  private instancedSplashes: THREE.InstancedMesh | null = null;
  private maxSplashCount: number = 600;
  private splashParticles: SplashParticle[] = [];
  private splashIndex: number = 0;
  private splashMaterial: THREE.MeshBasicMaterial | null = null;

  // Instanced Water Ripples
  private instancedRipples: THREE.InstancedMesh | null = null;
  private maxRippleCount: number = 400;
  private rippleParticles: RippleParticle[] = [];
  private rippleIndex: number = 0;
  private rippleMaterial: THREE.MeshBasicMaterial | null = null;

  // ── Spatial Grid Height Cache ──
  // Grid cell size in meters (each cell caches 1 raycast result)
  private static readonly GRID_CELL = 0.5;
  // Cache: key = "gridX,gridZ" → surface Y height
  private heightCache: Map<string, number> = new Map();
  // Raycaster + colliders
  private raycaster: THREE.Raycaster = new THREE.Raycaster();
  private colliderMeshes: THREE.Mesh[] = [];
  // Max NEW grid cells to raycast per frame (keeps FPS high)
  private static readonly MAX_NEW_RAYCASTS_PER_FRAME = 8;
  // Track pending raycast cells this frame
  private newRaycastsThisFrame: number = 0;

  private dummyObj: THREE.Object3D = new THREE.Object3D();

  // Weather state
  private activeRainIntensity: number = 0;
  private activeWindIntensity: number = 0.3;
  private activeWindDirection: number = 45;
  private gustTimer: number = 0;

  // Rain volume (centered on camera)
  private boxWidth: number = 36;
  private boxHeight: number = 28;
  private boxDepth: number = 36;

  constructor(scene: THREE.Scene) {
    this.scene = scene;
    this.rainGroup = new THREE.Group();
    this.rainGroup.name = 'optical_rain_engine';
    this.scene.add(this.rainGroup);

    this.raycaster.far = 60;
    this.raycaster.near = 0;

    this.initInstancedRain();
    this.initInstancedSplashes();
    this.initInstancedRipples();
  }

  // ══════════════════════════════════════════════════
  // Collider Management
  // ══════════════════════════════════════════════════

  public updateColliders(rootGroup?: THREE.Object3D): void {
    this.colliderMeshes = [];
    this.heightCache.clear(); // Map changed → invalidate all cached heights
    if (!rootGroup) return;

    rootGroup.traverse((child) => {
      if ((child as THREE.Mesh).isMesh && child.visible
        && child.name !== 'dynamic_cloud_ground_shadow') {
        const m = child as THREE.Mesh;
        // Skip extremely large meshes (>50K verts) to avoid raycast stalls
        if (m.geometry && m.geometry.attributes.position
          && m.geometry.attributes.position.count < 50000) {
          this.colliderMeshes.push(m);
        }
      }
    });
  }

  // ══════════════════════════════════════════════════
  // Spatial Grid Height Cache
  // ══════════════════════════════════════════════════

  /** Convert world XZ to grid key */
  private gridKey(x: number, z: number): string {
    const gx = Math.floor(x / WeatherParticleSystem.GRID_CELL);
    const gz = Math.floor(z / WeatherParticleSystem.GRID_CELL);
    return `${gx},${gz}`;
  }

  /**
   * Get surface height at world XZ using the spatial grid cache.
   * If the grid cell has been raycasted before, returns cached value instantly.
   * If not, performs a single raycast and caches the result.
   * Returns 0 if budget exhausted this frame (drop continues falling).
   */
  private getSurfaceY(x: number, y: number, z: number): number {
    const key = this.gridKey(x, z);

    // Cache hit → instant O(1) lookup
    const cached = this.heightCache.get(key);
    if (cached !== undefined) return cached;

    // Budget exhausted this frame → skip, try next frame
    if (this.newRaycastsThisFrame >= WeatherParticleSystem.MAX_NEW_RAYCASTS_PER_FRAME) {
      return -999; // Sentinel: no data yet, drop keeps falling
    }

    // No colliders → ground at Y=0
    if (this.colliderMeshes.length === 0) {
      this.heightCache.set(key, 0);
      return 0;
    }

    // Raycast straight down from high above
    this.raycaster.set(
      new THREE.Vector3(x, Math.max(y + 5, 40), z),
      new THREE.Vector3(0, -1, 0)
    );

    const hits = this.raycaster.intersectObjects(this.colliderMeshes, false);
    const surfaceY = hits.length > 0 ? hits[0].point.y : 0;

    this.heightCache.set(key, surfaceY);
    this.newRaycastsThisFrame++;
    return surfaceY;
  }

  // ══════════════════════════════════════════════════
  // Texture Generation
  // ══════════════════════════════════════════════════

  private createRainTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;
    ctx.clearRect(0, 0, 64, 256);

    for (let y = 4; y < 242; y += 2) {
      const progress = (y - 4) / 238;
      const halfW = 2.0 + Math.sin(progress * Math.PI * 0.90) * 11.0;
      const alpha = Math.pow(progress, 1.1) * 0.98;
      const grad = ctx.createLinearGradient(32 - halfW, y, 32 + halfW, y);
      grad.addColorStop(0, 'rgba(180, 215, 255, 0)');
      grad.addColorStop(0.25, `rgba(215, 240, 255, ${alpha * 0.70})`);
      grad.addColorStop(0.5, `rgba(255, 255, 255, ${alpha})`);
      grad.addColorStop(0.75, `rgba(215, 240, 255, ${alpha * 0.70})`);
      grad.addColorStop(1, 'rgba(180, 215, 255, 0)');
      ctx.fillStyle = grad;
      ctx.fillRect(32 - halfW, y, halfW * 2, 2);
    }

    const headGrad = ctx.createRadialGradient(32, 236, 1, 32, 236, 9.5);
    headGrad.addColorStop(0, 'rgba(255, 255, 255, 1.0)');
    headGrad.addColorStop(0.35, 'rgba(235, 248, 255, 0.96)');
    headGrad.addColorStop(0.7, 'rgba(195, 230, 255, 0.60)');
    headGrad.addColorStop(1, 'rgba(180, 220, 255, 0)');
    ctx.fillStyle = headGrad;
    ctx.beginPath();
    ctx.arc(32, 236, 9.5, 0, Math.PI * 2);
    ctx.fill();

    return new THREE.CanvasTexture(canvas);
  }

  private createSplashTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 64;
    const ctx = canvas.getContext('2d')!;
    ctx.clearRect(0, 0, 64, 64);
    const grad = ctx.createRadialGradient(32, 32, 1, 32, 32, 30);
    grad.addColorStop(0, 'rgba(255, 255, 255, 1.0)');
    grad.addColorStop(0.25, 'rgba(225, 245, 255, 0.92)');
    grad.addColorStop(0.6, 'rgba(180, 220, 255, 0.55)');
    grad.addColorStop(1, 'rgba(160, 210, 255, 0)');
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(32, 32, 30, 0, Math.PI * 2);
    ctx.fill();
    return new THREE.CanvasTexture(canvas);
  }

  private createRippleTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 128;
    const ctx = canvas.getContext('2d')!;
    ctx.clearRect(0, 0, 128, 128);
    ctx.lineWidth = 5.0;
    ctx.strokeStyle = 'rgba(240, 250, 255, 0.90)';
    ctx.beginPath();
    ctx.arc(64, 64, 36, 0, Math.PI * 2);
    ctx.stroke();
    ctx.lineWidth = 3.0;
    ctx.strokeStyle = 'rgba(195, 230, 255, 0.60)';
    ctx.beginPath();
    ctx.arc(64, 64, 50, 0, Math.PI * 2);
    ctx.stroke();
    return new THREE.CanvasTexture(canvas);
  }

  // ══════════════════════════════════════════════════
  // Instanced Mesh Initialization
  // ══════════════════════════════════════════════════

  private initInstancedRain(): void {
    const tex = this.createRainTexture();
    const geom = new THREE.PlaneGeometry(0.07, 1.15);
    geom.translate(0, 0.575, 0);

    this.rainMaterial = new THREE.MeshBasicMaterial({
      map: tex, transparent: true, opacity: 0.92,
      depthWrite: false, side: THREE.DoubleSide,
    });

    this.instancedRain = new THREE.InstancedMesh(geom, this.rainMaterial, this.maxRainCount);
    this.instancedRain.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.instancedRain.frustumCulled = false;
    this.instancedRain.visible = false;
    this.rainGroup.add(this.instancedRain);

    for (let i = 0; i < this.maxRainCount; i++) {
      this.rainDrops.push({
        pos: new THREE.Vector3(
          (Math.random() - 0.5) * this.boxWidth,
          Math.random() * (this.boxHeight + 10),
          (Math.random() - 0.5) * this.boxDepth
        ),
        speed: 24 + Math.random() * 16,
        length: 0.95 + Math.random() * 0.55,
        width: 0.95 + Math.random() * 0.45,
        phase: Math.random() * Math.PI * 2,
      });
      this.dummyObj.position.set(0, -999, 0);
      this.dummyObj.updateMatrix();
      this.instancedRain.setMatrixAt(i, this.dummyObj.matrix);
    }
    this.instancedRain.instanceMatrix.needsUpdate = true;
  }

  private initInstancedSplashes(): void {
    const tex = this.createSplashTexture();
    const geom = new THREE.PlaneGeometry(0.24, 0.24);
    this.splashMaterial = new THREE.MeshBasicMaterial({
      map: tex, transparent: true, opacity: 0.92,
      depthWrite: false, side: THREE.DoubleSide,
    });

    this.instancedSplashes = new THREE.InstancedMesh(geom, this.splashMaterial, this.maxSplashCount);
    this.instancedSplashes.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.instancedSplashes.frustumCulled = false;
    this.rainGroup.add(this.instancedSplashes);

    for (let i = 0; i < this.maxSplashCount; i++) {
      this.splashParticles.push({
        pos: new THREE.Vector3(0, -999, 0), vel: new THREE.Vector3(),
        scale: 0.2, life: 0, maxLife: 0.22, active: false,
      });
      this.dummyObj.position.set(0, -999, 0);
      this.dummyObj.updateMatrix();
      this.instancedSplashes.setMatrixAt(i, this.dummyObj.matrix);
    }
    this.instancedSplashes.instanceMatrix.needsUpdate = true;
  }

  private initInstancedRipples(): void {
    const tex = this.createRippleTexture();
    const geom = new THREE.PlaneGeometry(0.65, 0.65);
    this.rippleMaterial = new THREE.MeshBasicMaterial({
      map: tex, transparent: true, opacity: 0.85,
      depthWrite: false, side: THREE.DoubleSide,
    });

    this.instancedRipples = new THREE.InstancedMesh(geom, this.rippleMaterial, this.maxRippleCount);
    this.instancedRipples.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.instancedRipples.frustumCulled = false;
    this.rainGroup.add(this.instancedRipples);

    for (let i = 0; i < this.maxRippleCount; i++) {
      this.rippleParticles.push({
        pos: new THREE.Vector3(0, -999, 0), currentScale: 0.12,
        maxScale: 0.65, life: 0, maxLife: 0.32, active: false,
      });
      this.dummyObj.position.set(0, -999, 0);
      this.dummyObj.updateMatrix();
      this.instancedRipples.setMatrixAt(i, this.dummyObj.matrix);
    }
    this.instancedRipples.instanceMatrix.needsUpdate = true;
  }

  // ══════════════════════════════════════════════════
  // Splash & Ripple VFX
  // ══════════════════════════════════════════════════

  private spawnSplashAndRipple(x: number, y: number, z: number, wX: number, wZ: number): void {
    const count = 2 + Math.floor(Math.random() * 2);
    for (let s = 0; s < count; s++) {
      const sp = this.splashParticles[this.splashIndex];
      this.splashIndex = (this.splashIndex + 1) % this.maxSplashCount;
      sp.pos.set(x + (Math.random() - 0.5) * 0.10, y + 0.05, z + (Math.random() - 0.5) * 0.10);
      const angle = Math.random() * Math.PI * 2;
      const speed = 1.5 + Math.random() * 2.2;
      sp.vel.set(
        Math.cos(angle) * speed + wX * 0.12,
        2.4 + Math.random() * 2.5,
        Math.sin(angle) * speed + wZ * 0.12
      );
      sp.scale = 0.15 + Math.random() * 0.12;
      sp.maxLife = 0.17 + Math.random() * 0.08;
      sp.life = sp.maxLife;
      sp.active = true;
    }

    const rp = this.rippleParticles[this.rippleIndex];
    this.rippleIndex = (this.rippleIndex + 1) % this.maxRippleCount;
    rp.pos.set(x, y + 0.015, z);
    rp.currentScale = 0.10;
    rp.maxScale = 0.40 + Math.random() * 0.30;
    rp.maxLife = 0.26 + Math.random() * 0.10;
    rp.life = rp.maxLife;
    rp.active = true;
  }

  // ══════════════════════════════════════════════════
  // Main Update Loop
  // ══════════════════════════════════════════════════

  public update(
    delta: number,
    cameraPos: THREE.Vector3,
    rainIntensity: number = 0,
    windIntensity: number = 0.3,
    windDirectionDeg: number = 45,
    _sceneObstaclesGroup?: THREE.Object3D
  ): void {
    this.activeRainIntensity = Math.max(0, Math.min(1, rainIntensity));
    this.activeWindIntensity = Math.max(0, Math.min(1, windIntensity));
    this.activeWindDirection = windDirectionDeg;
    this.gustTimer += delta * (1.1 + this.activeWindIntensity * 2.2);

    // Reset per-frame raycast budget
    this.newRaycastsThisFrame = 0;

    if (this.activeRainIntensity <= 0.01) {
      if (this.instancedRain?.visible) this.instancedRain.visible = false;
      if (this.instancedSplashes?.visible) this.instancedSplashes.visible = false;
      if (this.instancedRipples?.visible) this.instancedRipples.visible = false;
      return;
    }

    if (this.instancedRain && !this.instancedRain.visible) this.instancedRain.visible = true;
    if (this.instancedSplashes && !this.instancedSplashes.visible) this.instancedSplashes.visible = true;
    if (this.instancedRipples && !this.instancedRipples.visible) this.instancedRipples.visible = true;

    const activeCount = Math.floor(this.maxRainCount * this.activeRainIntensity);
    if (this.instancedRain) this.instancedRain.count = activeCount;

    // Wind
    const windRad = (this.activeWindDirection * Math.PI) / 180;
    const gustWave = 1.0 + Math.sin(this.gustTimer) * 0.2 + Math.cos(this.gustTimer * 0.75) * 0.12;
    const effectiveWind = this.activeWindIntensity * gustWave;
    const windVelX = Math.sin(windRad) * (effectiveWind * 26.0);
    const windVelZ = Math.cos(windRad) * (effectiveWind * 26.0);
    const fallAccel = 1.0 + this.activeWindIntensity * 0.4 + this.activeRainIntensity * 0.35;
    const slantAngleX = Math.atan2(windVelZ, 28 * fallAccel);
    const slantAngleZ = -Math.atan2(windVelX, 28 * fallAccel);

    const halfW = this.boxWidth * 0.5;
    const halfD = this.boxDepth * 0.5;
    const camX = cameraPos.x;
    const camY = cameraPos.y;
    const camZ = cameraPos.z;

    // ── Raindrop update with spatial grid cached collision ──
    if (this.instancedRain) {
      for (let i = 0; i < activeCount; i++) {
        const drop = this.rainDrops[i];

        drop.pos.x += windVelX * delta;
        drop.pos.y -= drop.speed * fallAccel * delta;
        drop.pos.z += windVelZ * delta;

        const relX = drop.pos.x - camX;
        const relZ = drop.pos.z - camZ;
        const distFromCam = Math.sqrt(relX * relX + relZ * relZ);

        // Distance LOD
        let distScale = 1.0;
        if (distFromCam > 10.0) {
          distScale = Math.max(0.001, 1.0 - (distFromCam - 10.0) / 12.0);
        }

        // Surface collision via spatial grid cache
        // Only check collision for drops within camera range (performance)
        let surfaceY = 0;
        if (distFromCam < 20.0) {
          const result = this.getSurfaceY(drop.pos.x, drop.pos.y, drop.pos.z);
          surfaceY = result === -999 ? 0 : result; // -999 = no data yet
        }

        if (drop.pos.y <= surfaceY) {
          if (distFromCam < 14.0 && Math.random() < 0.45 + this.activeRainIntensity * 0.30) {
            this.spawnSplashAndRipple(drop.pos.x, surfaceY, drop.pos.z, windVelX, windVelZ);
          }
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
          drop.pos.y = Math.max(camY + 8, 14) + Math.random() * (this.boxHeight * 0.70);
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        } else if (relX < -halfW || relX > halfW || relZ < -halfD || relZ > halfD || drop.pos.y > camY + this.boxHeight + 10) {
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
          drop.pos.y = Math.max(camY + 8, 14) + Math.random() * (this.boxHeight * 0.70);
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        }

        this.dummyObj.position.copy(drop.pos);
        this.dummyObj.rotation.set(slantAngleX, 0, slantAngleZ);
        this.dummyObj.scale.set(
          drop.width * distScale,
          drop.length * (1.0 + this.activeWindIntensity * 0.65) * distScale,
          1.0
        );
        this.dummyObj.updateMatrix();
        this.instancedRain.setMatrixAt(i, this.dummyObj.matrix);
      }
      this.instancedRain.instanceMatrix.needsUpdate = true;
    }

    // ── Splash particles ──
    if (this.instancedSplashes) {
      for (let i = 0; i < this.maxSplashCount; i++) {
        const sp = this.splashParticles[i];
        if (!sp.active) continue;
        sp.life -= delta;
        if (sp.life <= 0) {
          sp.active = false;
          this.dummyObj.position.set(0, -999, 0);
          this.dummyObj.scale.set(0.001, 0.001, 0.001);
          this.dummyObj.updateMatrix();
          this.instancedSplashes.setMatrixAt(i, this.dummyObj.matrix);
          continue;
        }
        sp.vel.y -= 13.0 * delta;
        sp.pos.addScaledVector(sp.vel, delta);
        const s = sp.scale * (sp.life / sp.maxLife);
        this.dummyObj.position.copy(sp.pos);
        this.dummyObj.rotation.set(0, 0, 0);
        this.dummyObj.scale.set(s, s, s);
        this.dummyObj.updateMatrix();
        this.instancedSplashes.setMatrixAt(i, this.dummyObj.matrix);
      }
      this.instancedSplashes.instanceMatrix.needsUpdate = true;
    }

    // ── Water ripples ──
    if (this.instancedRipples) {
      for (let i = 0; i < this.maxRippleCount; i++) {
        const rp = this.rippleParticles[i];
        if (!rp.active) continue;
        rp.life -= delta;
        if (rp.life <= 0) {
          rp.active = false;
          this.dummyObj.position.set(0, -999, 0);
          this.dummyObj.scale.set(0.001, 0.001, 0.001);
          this.dummyObj.updateMatrix();
          this.instancedRipples.setMatrixAt(i, this.dummyObj.matrix);
          continue;
        }
        const progress = 1.0 - (rp.life / rp.maxLife);
        const scale = rp.currentScale + progress * (rp.maxScale - rp.currentScale);
        this.dummyObj.position.copy(rp.pos);
        this.dummyObj.rotation.set(-Math.PI / 2, 0, 0);
        this.dummyObj.scale.set(scale, scale, 1.0);
        this.dummyObj.updateMatrix();
        this.instancedRipples.setMatrixAt(i, this.dummyObj.matrix);
      }
      this.instancedRipples.instanceMatrix.needsUpdate = true;
    }

    if (this.rainMaterial) {
      this.rainMaterial.opacity = 0.65 + this.activeRainIntensity * 0.35;
    }
  }

  public dispose(): void {
    if (this.instancedRain) {
      this.instancedRain.geometry.dispose();
      (this.instancedRain.material as THREE.Material).dispose();
    }
    if (this.instancedSplashes) {
      this.instancedSplashes.geometry.dispose();
      (this.instancedSplashes.material as THREE.Material).dispose();
    }
    if (this.instancedRipples) {
      this.instancedRipples.geometry.dispose();
      (this.instancedRipples.material as THREE.Material).dispose();
    }
    if (this.rainGroup.parent) {
      this.rainGroup.parent.remove(this.rainGroup);
    }
  }
}
