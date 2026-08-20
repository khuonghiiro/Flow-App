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
  quaternion: THREE.Quaternion;
  currentScale: number;
  maxScale: number;
  life: number;
  maxLife: number;
  active: boolean;
}

interface SurfaceHitInfo {
  y: number;
  nx: number;
  ny: number;
  nz: number;
}

/**
 * WeatherParticleSystem — Mesh Raycasting Rain Collision with Spatial Grid Cache
 *
 * Giọt mưa va chạm bằng tia raycast thực sự (giống nhân vật rơi tự do):
 * - Tia chiếu thẳng xuống từ trên cao → tìm đúng bề mặt vật thể 3D
 * - Kết quả cache vào lưới ô 1m × 1m → chỉ cần raycast 1 lần/ô
 * - 4 raycast mới/frame → grid fill dần, 0 cost sau khi đầy
 * - Mưa rơi đúng giữa các tòa nhà, xuyên qua khe hở, dừng ở mặt đất thực
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

  // ── Spatial Grid Height & Normal Cache + Mesh Raycasting ──
  // Grid cell size (2m × 2m — fewer cells, faster fill, still accurate enough for rain)
  private static readonly GRID_CELL = 2.0;
  // Cache: key = "gridX,gridZ" → surface Y height and normal (from raycast)
  private heightCache: Map<string, SurfaceHitInfo> = new Map();
  // Raycaster (same approach as character ground snapping)
  private raycaster: THREE.Raycaster = new THREE.Raycaster();
  // Mesh colliders for raycasting — ONLY small meshes (<10K verts) for speed
  private candidateMeshes: THREE.Mesh[] = [];
  // Max NEW cells to raycast per frame — set dynamically from collisionQuality slider
  private maxNewRaycasts: number = 2;
  private newRaycastsThisFrame: number = 0;
  // Reusable vectors to avoid GC pressure
  private static readonly _rayOrigin = new THREE.Vector3();
  private static readonly _rayDir = new THREE.Vector3(0, -1, 0);
  private static readonly _tempNormal = new THREE.Vector3();
  private static readonly _worldUp = new THREE.Vector3(0, 1, 0);
  private static readonly _worldRight = new THREE.Vector3(1, 0, 0);
  private static readonly _planeDefaultNormal = new THREE.Vector3(0, 0, 1);
  private static readonly _tangent1 = new THREE.Vector3();
  private static readonly _tangent2 = new THREE.Vector3();
  private static readonly _defaultSurface: SurfaceHitInfo = { y: 0, nx: 0, ny: 1, nz: 0 };

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

    this.raycaster.far = 80;
    this.raycaster.near = 0;

    this.initInstancedRain();
    this.initInstancedSplashes();
    this.initInstancedRipples();
  }

  // ══════════════════════════════════════════════════
  // Collider Management (called when map loads)
  // ══════════════════════════════════════════════════

  /**
   * Collect ALL mesh colliders from scene for raycasting.
   * Bounding sphere pre-filter in getSurfaceY ensures only nearby meshes are tested.
   */
  public updateColliders(rootGroup?: THREE.Object3D): void {
    this.candidateMeshes = [];
    this.heightCache.clear();
    if (!rootGroup) return;

    rootGroup.traverse((child) => {
      if ((child as THREE.Mesh).isMesh && child.visible
        && child.name !== 'dynamic_cloud_ground_shadow') {
        const m = child as THREE.Mesh;
        if (m.geometry && m.geometry.attributes.position) {
          // Pre-compute bounding sphere for fast distance filtering later
          if (!m.geometry.boundingSphere) m.geometry.computeBoundingSphere();
          this.candidateMeshes.push(m);
        }
      }
    });
  }

  // ══════════════════════════════════════════════════
  // Spatial Grid Height Cache with Mesh Raycasting
  // ══════════════════════════════════════════════════

  /** Grid key from world XZ */
  private gridKey(x: number, z: number): string {
    const gx = Math.floor(x / WeatherParticleSystem.GRID_CELL);
    const gz = Math.floor(z / WeatherParticleSystem.GRID_CELL);
    return `${gx},${gz}`;
  }

  /**
   * Get surface height and normal at world XZ position.
   * Uses cache if available; otherwise performs raycast (budget limited).
   */
  private getSurfaceInfo(worldX: number, worldZ: number): SurfaceHitInfo {
    const key = this.gridKey(worldX, worldZ);

    // Cache hit → instant O(1)
    const cached = this.heightCache.get(key);
    if (cached !== undefined) return cached;

    // Budget exhausted this frame → return ground until next frame fills this cell
    if (this.newRaycastsThisFrame >= this.maxNewRaycasts) {
      return WeatherParticleSystem._defaultSurface;
    }

    // No colliders → ground at 0 with straight up normal
    if (this.candidateMeshes.length === 0) {
      this.heightCache.set(key, WeatherParticleSystem._defaultSurface);
      return WeatherParticleSystem._defaultSurface;
    }

    // ── Raycast straight down (same as character ground snap) ──
    const cellCenterX = (Math.floor(worldX / WeatherParticleSystem.GRID_CELL) + 0.5)
      * WeatherParticleSystem.GRID_CELL;
    const cellCenterZ = (Math.floor(worldZ / WeatherParticleSystem.GRID_CELL) + 0.5)
      * WeatherParticleSystem.GRID_CELL;

    WeatherParticleSystem._rayOrigin.set(cellCenterX, 60, cellCenterZ);
    this.raycaster.set(WeatherParticleSystem._rayOrigin, WeatherParticleSystem._rayDir);

    // Only test meshes whose bounding sphere is near the ray XZ (skip distant meshes)
    const nearMeshes: THREE.Mesh[] = [];
    for (let i = 0; i < this.candidateMeshes.length; i++) {
      const m = this.candidateMeshes[i];
      if (!m.geometry.boundingSphere) m.geometry.computeBoundingSphere();
      const bs = m.geometry.boundingSphere!;
      // Quick XZ distance check (world space approximation)
      const wx = m.matrixWorld.elements[12];
      const wz = m.matrixWorld.elements[14];
      const dx = cellCenterX - wx;
      const dz = cellCenterZ - wz;
      const r = bs.radius + 5;
      if (dx * dx + dz * dz < r * r) {
        nearMeshes.push(m);
      }
    }

    let surfaceY = 0;
    let nx = 0, ny = 1, nz = 0;
    if (nearMeshes.length > 0) {
      const hits = this.raycaster.intersectObjects(nearMeshes, false);
      if (hits.length > 0) {
        const hit = hits[0];
        surfaceY = hit.point.y;
        if (hit.face) {
          WeatherParticleSystem._tempNormal.copy(hit.face.normal);
          WeatherParticleSystem._tempNormal.transformDirection(hit.object.matrixWorld).normalize();
          nx = WeatherParticleSystem._tempNormal.x;
          ny = WeatherParticleSystem._tempNormal.y;
          nz = WeatherParticleSystem._tempNormal.z;
        } else if (hit.normal) {
          WeatherParticleSystem._tempNormal.copy(hit.normal).normalize();
          nx = WeatherParticleSystem._tempNormal.x;
          ny = WeatherParticleSystem._tempNormal.y;
          nz = WeatherParticleSystem._tempNormal.z;
        }
      }
    }

    const hitInfo: SurfaceHitInfo = { y: surfaceY, nx, ny, nz };
    this.heightCache.set(key, hitInfo);
    this.newRaycastsThisFrame++;
    return hitInfo;
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
        pos: new THREE.Vector3(0, -999, 0),
        quaternion: new THREE.Quaternion(),
        currentScale: 0.12,
        maxScale: 0.65,
        life: 0,
        maxLife: 0.32,
        active: false,
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

  private spawnSplashAndRipple(
    x: number, y: number, z: number,
    nx: number, ny: number, nz: number,
    wX: number, wZ: number
  ): void {
    WeatherParticleSystem._tempNormal.set(nx, ny, nz);
    if (WeatherParticleSystem._tempNormal.lengthSq() < 0.01) {
      WeatherParticleSystem._tempNormal.set(0, 1, 0);
    } else {
      WeatherParticleSystem._tempNormal.normalize();
    }
    const norm = WeatherParticleSystem._tempNormal;

    // Calculate tangent basis for natural bounce dispersion along the surface
    if (Math.abs(norm.y) < 0.95) {
      WeatherParticleSystem._tangent1.crossVectors(norm, WeatherParticleSystem._worldUp).normalize();
    } else {
      WeatherParticleSystem._tangent1.crossVectors(norm, WeatherParticleSystem._worldRight).normalize();
    }
    WeatherParticleSystem._tangent2.crossVectors(norm, WeatherParticleSystem._tangent1).normalize();

    // 1. Splash droplets rebound along surface normal with lateral spray
    const count = 2 + Math.floor(Math.random() * 2);
    for (let s = 0; s < count; s++) {
      const sp = this.splashParticles[this.splashIndex];
      this.splashIndex = (this.splashIndex + 1) % this.maxSplashCount;

      // Position slightly offset along normal to avoid surface clipping
      sp.pos.set(
        x + (Math.random() - 0.5) * 0.08 + norm.x * 0.03,
        y + (Math.random() - 0.5) * 0.08 + norm.y * 0.03,
        z + (Math.random() - 0.5) * 0.08 + norm.z * 0.03
      );

      const sprayAngle = Math.random() * Math.PI * 2;
      const normalSpeed = 2.0 + Math.random() * 2.6;
      const tangentSpeed = 1.0 + Math.random() * 1.8;

      sp.vel.set(0, 0, 0)
        .addScaledVector(norm, normalSpeed)
        .addScaledVector(WeatherParticleSystem._tangent1, Math.cos(sprayAngle) * tangentSpeed)
        .addScaledVector(WeatherParticleSystem._tangent2, Math.sin(sprayAngle) * tangentSpeed);

      // Add wind influence
      sp.vel.x += wX * 0.12;
      sp.vel.z += wZ * 0.12;

      sp.scale = 0.15 + Math.random() * 0.12;
      sp.maxLife = 0.17 + Math.random() * 0.08;
      sp.life = sp.maxLife;
      sp.active = true;
    }

    // 2. Ripple rings align perfectly flush to the surface orientation (roof tilt, rock slope, ground)
    const rp = this.rippleParticles[this.rippleIndex];
    this.rippleIndex = (this.rippleIndex + 1) % this.maxRippleCount;

    // Offset slightly along normal to avoid Z-fighting on roofs/slopes
    rp.pos.set(
      x + norm.x * 0.015,
      y + norm.y * 0.015,
      z + norm.z * 0.015
    );

    // Align PlaneGeometry (which faces +Z in local space) to the world normal
    rp.quaternion.setFromUnitVectors(WeatherParticleSystem._planeDefaultNormal, norm);

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
    _sceneObstaclesGroup?: THREE.Object3D,
    collisionQuality: number = 2
  ): void {
    this.activeRainIntensity = Math.max(0, Math.min(1, rainIntensity));
    this.activeWindIntensity = Math.max(0, Math.min(1, windIntensity));
    this.activeWindDirection = windDirectionDeg;
    this.gustTimer += delta * (1.1 + this.activeWindIntensity * 2.2);

    // Dynamic raycast budget from slider (0 = no collision, 10 = max quality)
    this.maxNewRaycasts = Math.max(0, Math.min(10, Math.round(collisionQuality)));

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

    // ── Raindrop update with spatial grid cached mesh collision ──
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

        // Surface collision via spatial grid cache (mesh raycast with surface normal)
        const surfInfo = this.getSurfaceInfo(drop.pos.x, drop.pos.z);

        if (drop.pos.y <= surfInfo.y) {
          // Hit surface → splash + ripple with surface normal orientation
          if (distFromCam < 14.0 && Math.random() < 0.45 + this.activeRainIntensity * 0.30) {
            this.spawnSplashAndRipple(
              drop.pos.x, surfInfo.y, drop.pos.z,
              surfInfo.nx, surfInfo.ny, surfInfo.nz,
              windVelX, windVelZ
            );
          }
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
          drop.pos.y = Math.max(camY + 8, 14) + Math.random() * (this.boxHeight * 0.70);
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        } else if (relX < -halfW || relX > halfW || relZ < -halfD || relZ > halfD || drop.pos.y > camY + this.boxHeight + 10) {
          // Out of bounds
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
        this.dummyObj.quaternion.copy(rp.quaternion);
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
