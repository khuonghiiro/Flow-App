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
  private maxRainCount: number = 7000;
  private rainDrops: RainDropData[] = [];
  private rainMaterial: THREE.MeshBasicMaterial | null = null;

  // Instanced Water Splashes
  private instancedSplashes: THREE.InstancedMesh | null = null;
  private maxSplashCount: number = 700;
  private splashParticles: SplashParticle[] = [];
  private splashIndex: number = 0;
  private splashMaterial: THREE.MeshBasicMaterial | null = null;

  // Instanced Water Ripples
  private instancedRipples: THREE.InstancedMesh | null = null;
  private maxRippleCount: number = 500;
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
  private static readonly _planeDefaultNormal = new THREE.Vector3(0, 1, 0);
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
   * Collect mesh colliders from scene.
   * Zero upfront processing for instant, freeze-free map switching.
   */
  public updateColliders(rootGroup?: THREE.Object3D): void {
    this.candidateMeshes = [];
    this.heightCache.clear();
    if (!rootGroup) return;

    rootGroup.traverse((child) => {
      if ((child as THREE.Mesh).isMesh && child.visible
        && child.name !== 'dynamic_cloud_ground_shadow'
        && child.name !== 'cloud_shadow_caster_3d') {
        const m = child as THREE.Mesh;
        if (m.geometry && m.geometry.attributes.position) {
          this.candidateMeshes.push(m);
        }
      }
    });
  }

  // ══════════════════════════════════════════════════
  // Spatial Grid Height Cache with Mesh Raycasting
  // ══════════════════════════════════════════════════

  private activeCameraMeshes: THREE.Mesh[] = [];

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

    // Budget exhausted this frame → return ground until next frame fills this cell smoothly
    if (this.newRaycastsThisFrame >= this.maxNewRaycasts) {
      return WeatherParticleSystem._defaultSurface;
    }

    // No colliders → ground at 0 with straight up normal
    const testMeshes = this.activeCameraMeshes.length > 0 ? this.activeCameraMeshes : this.candidateMeshes;
    if (testMeshes.length === 0) {
      this.heightCache.set(key, WeatherParticleSystem._defaultSurface);
      return WeatherParticleSystem._defaultSurface;
    }

    // ── Raycast straight down from high sky (Y = 350m, covering tall roofs, spires & trees) ──
    const cellCenterX = (Math.floor(worldX / WeatherParticleSystem.GRID_CELL) + 0.5)
      * WeatherParticleSystem.GRID_CELL;
    const cellCenterZ = (Math.floor(worldZ / WeatherParticleSystem.GRID_CELL) + 0.5)
      * WeatherParticleSystem.GRID_CELL;

    WeatherParticleSystem._rayOrigin.set(cellCenterX, 350.0, cellCenterZ);
    this.raycaster.far = 400.0;
    this.raycaster.set(WeatherParticleSystem._rayOrigin, WeatherParticleSystem._rayDir);

    let surfaceY = 0;
    let nx = 0, ny = 1, nz = 0;
    const hits = this.raycaster.intersectObjects(testMeshes, false);
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

    // Soft, realistic, single-circle water drop ripple
    const grad = ctx.createRadialGradient(64, 64, 38, 64, 64, 58);
    grad.addColorStop(0, 'rgba(180, 220, 255, 0)');
    grad.addColorStop(0.5, 'rgba(215, 240, 255, 0.70)');
    grad.addColorStop(0.8, 'rgba(180, 220, 255, 0.30)');
    grad.addColorStop(1, 'rgba(160, 210, 255, 0)');

    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(64, 64, 58, 0, Math.PI * 2);
    ctx.fill();

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

    // Fully randomized 3D pseudo-random spatial distribution (zero sequential/grid bias)
    for (let i = 0; i < this.maxRainCount; i++) {
      this.rainDrops.push({
        pos: new THREE.Vector3(
          (Math.random() - 0.5) * this.boxWidth,
          Math.random() * (this.boxHeight + 12),
          (Math.random() - 0.5) * this.boxDepth
        ),
        speed: 22.0 + Math.random() * 18.0,
        length: 0.85 + Math.random() * 0.65,
        width: 0.85 + Math.random() * 0.55,
        phase: Math.random() * Math.PI * 2,
      });
      this.dummyObj.position.set(0, -9999, 0);
      this.dummyObj.scale.set(0, 0, 0);
      this.dummyObj.updateMatrix();
      this.instancedRain.setMatrixAt(i, this.dummyObj.matrix);
    }
    this.instancedRain.instanceMatrix.needsUpdate = true;
  }

  private initInstancedSplashes(): void {
    // Micro water droplet bead (tiny realistic size, physical normal blending)
    const geom = new THREE.SphereGeometry(0.009, 6, 4);
    this.splashMaterial = new THREE.MeshBasicMaterial({
      color: 0xf1f8ff,
      transparent: true,
      opacity: 0.65,
      blending: THREE.NormalBlending,
      depthWrite: false,
    });

    this.instancedSplashes = new THREE.InstancedMesh(geom, this.splashMaterial, this.maxSplashCount);
    this.instancedSplashes.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.instancedSplashes.frustumCulled = false;
    this.rainGroup.add(this.instancedSplashes);

    for (let i = 0; i < this.maxSplashCount; i++) {
      this.splashParticles.push({
        pos: new THREE.Vector3(0, -9999, 0), vel: new THREE.Vector3(),
        scale: 0.15, life: 0, maxLife: 0.15, active: false,
      });
      this.dummyObj.position.set(0, -9999, 0);
      this.dummyObj.scale.set(0, 0, 0);
      this.dummyObj.updateMatrix();
      this.instancedSplashes.setMatrixAt(i, this.dummyObj.matrix);
    }
    this.instancedSplashes.instanceMatrix.needsUpdate = true;
  }

  private initInstancedRipples(): void {
    const geom = new THREE.PlaneGeometry(1.0, 1.0);
    geom.rotateX(-Math.PI / 2);

    this.rippleMaterial = new THREE.ShaderMaterial({
      vertexShader: `
        varying vec2 vUv;
        void main() {
          vUv = uv;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        varying vec2 vUv;
        void main() {
          vec2 p = (vUv - vec2(0.5)) * 2.0;
          float r = length(p);
          if (r > 1.0 || r < 0.15) discard;
          
          // Soft, natural, translucent water puddle wave ring
          float ring = smoothstep(0.09, 0.0, abs(r - 0.82));
          float alpha = ring * 0.35 * (1.0 - r);
          if (alpha < 0.005) discard;
          
          vec3 col = vec3(0.95, 0.98, 1.0);
          gl_FragColor = vec4(col, alpha);
        }
      `,
      transparent: true,
      blending: THREE.NormalBlending,
      depthWrite: false,
      side: THREE.DoubleSide,
    }) as any;

    this.instancedRipples = new THREE.InstancedMesh(geom, this.rippleMaterial as any, this.maxRippleCount);
    this.instancedRipples.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.instancedRipples.frustumCulled = false;
    this.rainGroup.add(this.instancedRipples);

    for (let i = 0; i < this.maxRippleCount; i++) {
      this.rippleParticles.push({
        pos: new THREE.Vector3(0, -9999, 0),
        quaternion: new THREE.Quaternion(),
        currentScale: 0.02,
        maxScale: 0.16,
        life: 0,
        maxLife: 0.20,
        active: false,
      });
      this.dummyObj.position.set(0, -9999, 0);
      this.dummyObj.scale.set(0, 0, 0);
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
    wX: number, wZ: number,
    rainIntensity: number = 0.5,
    windIntensity: number = 0.3,
    collisionQuality: number = 2
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

    // 1. Splash micro-droplets: quantity directly scaled by collisionQuality (1 to 5 droplets)
    const count = Math.min(5, Math.max(1, Math.round(1 + (collisionQuality / 10.0) * 3.5 * rainIntensity)));
    for (let s = 0; s < count; s++) {
      const sp = this.splashParticles[this.splashIndex];
      this.splashIndex = (this.splashIndex + 1) % this.maxSplashCount;

      // Position slightly offset along normal to avoid surface clipping
      sp.pos.set(
        x + (Math.random() - 0.5) * 0.04 + norm.x * 0.015,
        y + (Math.random() - 0.5) * 0.04 + norm.y * 0.015,
        z + (Math.random() - 0.5) * 0.04 + norm.z * 0.015
      );

      const sprayAngle = Math.random() * Math.PI * 2;
      const normalSpeed = (0.45 + rainIntensity * 0.75) * (0.8 + Math.random() * 0.4);
      const tangentSpeed = (0.35 + rainIntensity * 0.55) * (0.8 + Math.random() * 0.4);

      sp.vel.set(0, 0, 0)
        .addScaledVector(norm, normalSpeed)
        .addScaledVector(WeatherParticleSystem._tangent1, Math.cos(sprayAngle) * tangentSpeed)
        .addScaledVector(WeatherParticleSystem._tangent2, Math.sin(sprayAngle) * tangentSpeed);

      // Subtle lateral wind push on flying droplets
      const windPush = 0.06 + windIntensity * 0.15;
      sp.vel.x += wX * windPush;
      sp.vel.z += wZ * windPush;

      sp.scale = 0.12;
      sp.maxLife = 0.10 + rainIntensity * 0.06;
      sp.life = sp.maxLife;
      sp.active = true;
    }

    // 2. Ripple rings disabled per user preference (only realistic 3D splash impact droplets are shown)
    // No artificial 2D white/black circles
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
    collisionQuality: number = 2,
    splashDistance: number = 45,
    cloudCoverage: number = 0.5,
    cloudLayers: number = 1,
    rainAreaCoverage: number = 1.0
  ): void {
    this.activeRainIntensity = Math.max(0, Math.min(1, rainIntensity));
    this.activeWindIntensity = Math.max(0, Math.min(1, windIntensity));
    this.activeWindDirection = windDirectionDeg;
    this.gustTimer += delta * (0.8 + this.activeWindIntensity * 1.6);

    // Stable Cinematic Camera Rain Volume (constant volumetric density, prevents flying artifacts & dilution)
    this.boxWidth = 42.0;
    this.boxDepth = 42.0;
    this.boxHeight = 28.0;

    const isCollisionEnabled = collisionQuality > 0;
    // Dynamic raycast budget (strictly 2 to 6 raycasts/frame to guarantee 60 FPS without frame drops)
    this.maxNewRaycasts = isCollisionEnabled ? Math.min(6, Math.max(2, Math.round(collisionQuality * 1.5))) : 0;
    this.newRaycastsThisFrame = 0;

    // Filter candidate meshes only within camera active volume (chỉ tính toán khi camera soi đến)
    this.activeCameraMeshes = [];
    if (isCollisionEnabled && this.candidateMeshes.length > 0) {
      const activeRadius = 35.0;
      const activeRadiusSq = activeRadius * activeRadius;
      for (let i = 0; i < this.candidateMeshes.length; i++) {
        const m = this.candidateMeshes[i];
        const wx = m.matrixWorld.elements[12];
        const wz = m.matrixWorld.elements[14];
        const dx = cameraPos.x - wx;
        const dz = cameraPos.z - wz;
        if (dx * dx + dz * dz <= activeRadiusSq) {
          this.activeCameraMeshes.push(m);
        }
      }
    }

    if (this.activeRainIntensity <= 0.01) {
      if (this.instancedRain?.visible) this.instancedRain.visible = false;
      if (this.instancedSplashes?.visible) this.instancedSplashes.visible = false;
      if (this.instancedRipples?.visible) this.instancedRipples.visible = false;
      return;
    }

    if (this.instancedRain && !this.instancedRain.visible) this.instancedRain.visible = true;
    if (this.instancedSplashes) this.instancedSplashes.visible = isCollisionEnabled;
    if (this.instancedRipples) this.instancedRipples.visible = false; // Always false, no 2D circle hoops

    // Dynamically modulate rain streak material opacity for realistic soft drizzle vs heavy downpour
    if (this.rainMaterial) {
      this.rainMaterial.opacity = 0.35 + this.activeRainIntensity * 0.58;
    }

    // Uniform spatial distribution: keep a rich base particle grid (min 55% = ~3850 drops) so space is never empty or patchy
    const densityRatio = 0.55 + this.activeRainIntensity * 0.45;
    const coverageFactor = THREE.MathUtils.clamp(0.60 + cloudCoverage * 0.40, 0.60, 1.0);
    const activeCount = Math.min(
      this.maxRainCount,
      Math.floor(this.maxRainCount * densityRatio * coverageFactor)
    );
    if (this.instancedRain) this.instancedRain.count = activeCount;

    // Wind velocity & Rain Fall Acceleration with multi-layer turbulence
    const windRad = (this.activeWindDirection * Math.PI) / 180;
    const gustSpread = (1.0 - cloudCoverage * 0.55);
    const gustWave = 1.0 + (Math.sin(this.gustTimer * 1.2) * 0.18 + Math.cos(this.gustTimer * 0.7) * 0.10) * gustSpread;
    const effectiveWind = this.activeWindIntensity * gustWave;
    const windVelX = Math.sin(windRad) * (effectiveWind * 30.0);
    const windVelZ = Math.cos(windRad) * (effectiveWind * 30.0);
    const fallAccel = 1.0 + this.activeWindIntensity * 0.85 + this.activeRainIntensity * 0.45;
    const slantAngleX = Math.atan2(windVelZ, 28 * fallAccel);
    const slantAngleZ = -Math.atan2(windVelX, 28 * fallAccel);

    const halfW = this.boxWidth * 0.5;
    const halfD = this.boxDepth * 0.5;
    const camX = cameraPos.x;
    const camY = cameraPos.y;
    const camZ = cameraPos.z;

    // Direct proportional hit chance: 1 = 10% (sparse), 5 = 50% (balanced), 10 = 100% (max dense)
    const hitChance = isCollisionEnabled ? (collisionQuality / 10.0) : 0;
    const intensityScale = 0.65 + this.activeRainIntensity * 0.35;

    // ── Raindrop update with spatial grid cached mesh collision ──
    if (this.instancedRain) {
      for (let i = 0; i < activeCount; i++) {
        const drop = this.rainDrops[i];

        // Individual fall speed and micro-flutter
        drop.pos.y -= drop.speed * fallAccel * delta;
        drop.pos.x += windVelX * delta;
        drop.pos.z += windVelZ * delta;

        // Wrap around camera smoothly with dynamic lateral shuffling
        if (drop.pos.x < camX - halfW) {
          drop.pos.x = camX + halfW - Math.random() * 2.0;
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        }
        if (drop.pos.x > camX + halfW) {
          drop.pos.x = camX - halfW + Math.random() * 2.0;
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        }
        if (drop.pos.z < camZ - halfD) {
          drop.pos.z = camZ + halfD - Math.random() * 2.0;
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
        }
        if (drop.pos.z > camZ + halfD) {
          drop.pos.z = camZ - halfD + Math.random() * 2.0;
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
        }

        // Multi-zone harmonic weather interference:
        // When rainAreaCoverage >= 0.98 (100%): 100% drops are full strength across entire map, zero holes.
        // When rainAreaCoverage < 0.98: Heavy rain cells transition into soft floating micro-mist/drizzle in the outer zones!
        let cellDensity = 1.0;
        if (rainAreaCoverage < 0.98) {
          const cellScale = 0.035;
          const wx = drop.pos.x * cellScale - windVelX * 0.012 * this.gustTimer;
          const wz = drop.pos.z * cellScale - windVelZ * 0.012 * this.gustTimer;

          const wave1 = Math.sin(wx * 0.9 + wz * 0.5) * 0.5 + 0.5;
          const wave2 = Math.sin(-wx * 0.7 + wz * 0.8 + 2.1) * 0.5 + 0.5;
          const wave3 = Math.cos(wx * 1.3 - wz * 0.6 + 4.2) * 0.5 + 0.5;

          const weatherField = wave1 * 0.45 + wave2 * 0.35 + wave3 * 0.20;
          const threshold = (1.0 - rainAreaCoverage) * 0.80;

          if (weatherField >= threshold) {
            // Main heavy rain front
            cellDensity = 1.0;
          } else {
            // Infill with fine atmospheric micro-drizzle (bù hạt mưa nhỏ mờ vi mô, zero empty holes!)
            cellDensity = 0.28 + ((weatherField / Math.max(0.01, threshold)) * 0.35);
          }
        }

        // Respawn uniformly at randomized top coordinates when reaching ground
        const resetDropToTop = () => {
          drop.pos.y = camY + (this.boxHeight * 0.5) + Math.random() * (this.boxHeight * 0.35);
          drop.pos.x = camX + (Math.random() - 0.5) * this.boxWidth;
          drop.pos.z = camZ + (Math.random() - 0.5) * this.boxDepth;
        };

        if (isCollisionEnabled) {
          const hitInfo = this.getSurfaceInfo(drop.pos.x, drop.pos.z);
          if (drop.pos.y <= hitInfo.y) {
            const distToCamSq =
              (drop.pos.x - camX) * (drop.pos.x - camX) +
              (drop.pos.z - camZ) * (drop.pos.z - camZ);

            // Only full/active rain drops trigger splashes
            if (distToCamSq <= splashDistance * splashDistance && Math.random() < hitChance * cellDensity) {
              this.spawnSplashAndRipple(
                drop.pos.x,
                hitInfo.y,
                drop.pos.z,
                hitInfo.nx,
                hitInfo.ny,
                hitInfo.nz,
                windVelX,
                windVelZ,
                this.activeRainIntensity * cellDensity,
                this.activeWindIntensity,
                collisionQuality
              );
            }
            resetDropToTop();
          }
        } else {
          // Free fall without collision check
          if (drop.pos.y < camY - 2.0) {
            resetDropToTop();
          }
        }

        const distScale = Math.min(1.0, 0.4 + (Math.abs(drop.pos.y - camY) / (this.boxHeight * 0.5)) * 0.6);

        this.dummyObj.position.copy(drop.pos);
        this.dummyObj.rotation.set(slantAngleX, 0, slantAngleZ);
        this.dummyObj.scale.set(
          drop.width * distScale * intensityScale * cellDensity,
          drop.length * (1.0 + this.activeWindIntensity * 0.65) * distScale * intensityScale * cellDensity,
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
          this.dummyObj.position.set(0, -9999, 0);
          this.dummyObj.scale.set(0, 0, 0);
          this.dummyObj.updateMatrix();
          this.instancedSplashes.setMatrixAt(i, this.dummyObj.matrix);
          continue;
        }
        sp.vel.y -= 9.8 * delta;
        sp.vel.x *= Math.max(0.01, 1.0 - 4.0 * delta);
        sp.vel.z *= Math.max(0.01, 1.0 - 4.0 * delta);
        sp.pos.addScaledVector(sp.vel, delta);
        const s = Math.pow(sp.life / sp.maxLife, 0.6) * 0.85;
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
        if (!rp.active) {
          this.dummyObj.position.set(0, -9999, 0);
          this.dummyObj.scale.set(0, 0, 0);
          this.dummyObj.updateMatrix();
          this.instancedRipples.setMatrixAt(i, this.dummyObj.matrix);
          continue;
        }
        rp.life -= delta;
        if (rp.life <= 0) {
          rp.active = false;
          this.dummyObj.position.set(0, -9999, 0);
          this.dummyObj.scale.set(0, 0, 0);
          this.dummyObj.updateMatrix();
          this.instancedRipples.setMatrixAt(i, this.dummyObj.matrix);
          continue;
        }
        const progress = 1.0 - (rp.life / rp.maxLife);
        const fade = Math.pow(rp.life / rp.maxLife, 0.7);
        const scale = (rp.currentScale + progress * (rp.maxScale - rp.currentScale)) * Math.min(1.0, fade * 1.8);
        this.dummyObj.position.copy(rp.pos);
        this.dummyObj.quaternion.copy(rp.quaternion);
        this.dummyObj.scale.set(scale, 1.0, scale);
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
