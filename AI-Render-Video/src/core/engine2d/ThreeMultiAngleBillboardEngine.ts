import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DAngle,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../assets/Asset2DRegistry';
import { PuppetAnimationState } from './Canvas2DPuppetEngine';

export type Background2DMode = 'checkerboard' | 'dark' | 'slate' | 'white' | 'chroma';

export interface AngleDetectionResult {
  angleDeg: number;             // 0 to 360
  discreteAngle: Character2DAngle;
  angleLabel: string;
  compassDirection: string;     // 'N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'
}

export class ThreeMultiAngleBillboardEngine {
  private container: HTMLElement;
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private renderer: THREE.WebGLRenderer;
  private controls: OrbitControls;
  private characterGroup: THREE.Group;
  private partMeshes: Map<Character2DPartType, THREE.Mesh> = new Map();
  private textureCache: Map<string, THREE.Texture> = new Map();
  private textureLoader: THREE.TextureLoader = new THREE.TextureLoader();

  private currentAssembly: Character2DAssembly | null = null;
  private currentDiscreteAngle: Character2DAngle = 'front';
  private onAngleChangeCallback?: (result: AngleDetectionResult) => void;
  private animFrameId: number | null = null;
  private gridHelper!: THREE.GridHelper;
  private checkerboardTexture: THREE.CanvasTexture | null = null;

  public currentBgMode: Background2DMode = 'checkerboard';
  public currentTimeOfDay: number = 0; // for backwards-compatibility

  constructor(container: HTMLElement, onAngleChange?: (res: AngleDetectionResult) => void) {
    this.container = container;
    this.onAngleChangeCallback = onAngleChange;

    const width = container.clientWidth || 800;
    const height = container.clientHeight || 500;

    // 1. Scene & Camera Setup
    this.scene = new THREE.Scene();

    this.camera = new THREE.PerspectiveCamera(35, width / height, 0.1, 100);
    this.camera.position.set(0, 0.2, 3.2);

    // 2. WebGL Renderer (True 2D flat color rendering)
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.toneMapping = THREE.NoToneMapping; // Preserve 100% 2D flat vibrancy
    this.container.appendChild(this.renderer.domElement);

    // 3. OrbitControls
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.target.set(0, 0.52, 0); // Focus directly on the Head & Hair center
    this.controls.maxPolarAngle = Math.PI / 2 + 0.15;
    this.controls.minDistance = 0.8;
    this.controls.maxDistance = 6.0;

    // 4. Character Group & 2D Background
    this.characterGroup = new THREE.Group();
    this.scene.add(this.characterGroup);

    // Ground Grid Helper
    this.gridHelper = new THREE.GridHelper(6, 12, 0x38bdf8, 0x334155);
    this.gridHelper.position.y = -1.1;
    this.scene.add(this.gridHelper);

    // Shadow plane
    const shadowGeo = new THREE.CircleGeometry(0.8, 32);
    const shadowMat = new THREE.MeshBasicMaterial({
      color: 0x000000,
      transparent: true,
      opacity: 0.25,
    });
    const shadowMesh = new THREE.Mesh(shadowGeo, shadowMat);
    shadowMesh.rotation.x = -Math.PI / 2;
    shadowMesh.position.y = -1.09;
    this.scene.add(shadowMesh);

    // Initialize 2D Background
    this.setBackgroundMode('checkerboard');

    // 5. Start Render Loop
    this.startLoop();
  }

  /**
   * Generates a repeating checkered canvas texture for transparency inspection
   */
  private getOrCreateCheckerboardTexture(): THREE.CanvasTexture {
    if (this.checkerboardTexture) return this.checkerboardTexture;

    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 64;
    const ctx = canvas.getContext('2d')!;

    // 2D Studio dark checker squares
    ctx.fillStyle = '#0b0f19';
    ctx.fillRect(0, 0, 64, 64);
    ctx.fillStyle = '#162032';
    ctx.fillRect(0, 0, 32, 32);
    ctx.fillRect(32, 32, 32, 32);

    this.checkerboardTexture = new THREE.CanvasTexture(canvas);
    this.checkerboardTexture.wrapS = THREE.RepeatWrapping;
    this.checkerboardTexture.wrapT = THREE.RepeatWrapping;
    this.checkerboardTexture.repeat.set(16, 16);
    return this.checkerboardTexture;
  }

  /**
   * Sets pure 2D Background Mode (Industrial 2D Studio Standard)
   */
  public setBackgroundMode(mode: Background2DMode): void {
    this.currentBgMode = mode;

    switch (mode) {
      case 'checkerboard':
        this.scene.background = this.getOrCreateCheckerboardTexture();
        this.gridHelper.visible = false;
        break;
      case 'dark':
        this.scene.background = new THREE.Color(0x090d16);
        this.gridHelper.visible = true;
        (this.gridHelper.material as THREE.LineBasicMaterial).color.setHex(0x38bdf8);
        break;
      case 'slate':
        this.scene.background = new THREE.Color(0x1e293b);
        this.gridHelper.visible = true;
        (this.gridHelper.material as THREE.LineBasicMaterial).color.setHex(0x64748b);
        break;
      case 'white':
        this.scene.background = new THREE.Color(0xffffff);
        this.gridHelper.visible = true;
        (this.gridHelper.material as THREE.LineBasicMaterial).color.setHex(0xcccccc);
        break;
      case 'chroma':
        this.scene.background = new THREE.Color(0x00ff00);
        this.gridHelper.visible = false;
        break;
    }
  }

  /**
   * Backwards compatible bridge for time of day
   */
  public setTimeOfDay(hour: number): void {
    this.currentTimeOfDay = hour;
    if (hour === 0 || hour === 24) {
      this.setBackgroundMode('dark');
    } else if (hour === 6) {
      this.setBackgroundMode('slate');
    } else if (hour === 12) {
      this.setBackgroundMode('checkerboard');
    } else {
      this.setBackgroundMode('dark');
    }
  }

  /**
   * Loads or returns cached Three.js Texture
   */
  private loadTexture(url: string): THREE.Texture {
    if (this.textureCache.has(url)) {
      return this.textureCache.get(url)!;
    }
    const tex = this.textureLoader.load(url, () => {
      // Re-apply transforms with correct aspect ratio once texture loads
      if (this.currentAssembly) {
        this.applyTransformsForAngle(this.currentDiscreteAngle);
      }
    });
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.generateMipmaps = true;
    tex.minFilter = THREE.LinearMipmapLinearFilter;
    tex.magFilter = THREE.LinearFilter;
    this.textureCache.set(url, tex);
    return tex;
  }

  /**
   * Rebuilds all 2D mesh planes from Character2DAssembly with flat MeshBasicMaterial
   */
  public setAssembly(assembly: Character2DAssembly): void {
    this.currentAssembly = assembly;
    const layerSpacing = assembly.layer_depth_spacing ?? 1.0;
    const currentSlots = new Set(Object.keys(assembly.parts));

    // Remove meshes for parts that no longer exist
    for (const [slot, mesh] of this.partMeshes.entries()) {
      if (!currentSlots.has(slot)) {
        this.characterGroup.remove(mesh);
        mesh.geometry.dispose();
        (mesh.material as THREE.Material).dispose();
        this.partMeshes.delete(slot);
      }
    }

    // Create or update Plane Mesh for each part
    for (const [slotKey, part] of Object.entries(assembly.parts)) {
      if (!part) continue;
      const slot = slotKey as Character2DPartType;

      const activeUrl = this.getTextureForAngle(part, this.currentDiscreteAngle) || part.path || '';
      if (!activeUrl) continue;

      const hierarchy = PART_HIERARCHY_CONFIG[slot];
      const zDepth = (part.z_depth_3d ?? hierarchy?.defaultZDepth3D ?? 0) * layerSpacing;

      let mesh = this.partMeshes.get(slot);
      if (!mesh) {
        const texture = this.loadTexture(activeUrl);
        const geo = new THREE.PlaneGeometry(1.0, 1.0);
        const mat = new THREE.MeshBasicMaterial({
          map: texture,
          transparent: true,
          alphaTest: 0.01,
          side: THREE.DoubleSide,
          depthWrite: true,
        });

        mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(0, 0, zDepth);
        this.characterGroup.add(mesh);
        this.partMeshes.set(slot, mesh);
      }
    }

    // Apply per-angle transform overrides immediately
    this.applyTransformsForAngle(this.currentDiscreteAngle);
  }

  /**
   * Applies transforms, per-angle offsets, scales, rotations & textures for the active angle
   */
  public applyTransformsForAngle(angle: Character2DAngle): void {
    if (!this.currentAssembly) return;
    const baseScale = this.currentAssembly.base_scale || 1.0;
    const layerSpacing = this.currentAssembly.layer_depth_spacing ?? 1.0;

    const isRightSymmetryAngle =
      angle === 'three_quarter_right' ||
      angle === 'profile_right' ||
      angle === 'back_three_quarter_right' ||
      angle === 'top_down_three_quarter_right' ||
      angle === 'top_down_profile_right' ||
      angle === 'top_down_back_three_quarter_right';

    for (const [slotKey, mesh] of this.partMeshes.entries()) {
      const part = this.currentAssembly.parts[slotKey];
      if (!part) continue;

      const override = part.angle_overrides?.[angle];

      // 1. Visibility Check
      if (override?.visible === false) {
        mesh.visible = false;
        continue;
      }

      // If slot is `dau` (Đỉnh đầu / Crown), only show it in top_down angles (unless explicitly configured)
      if (slotKey === 'dau' && !angle.startsWith('top_down') && !part.angles?.[angle]) {
        mesh.visible = false;
        continue;
      }
      mesh.visible = true;

      // 2. Texture Swap
      const targetUrl = this.getTextureForAngle(part, angle);
      if (targetUrl) {
        const tex = this.loadTexture(targetUrl);
        const mat = mesh.material as THREE.MeshBasicMaterial;
        mat.map = tex;
        mat.needsUpdate = true;
      }

      // 3. Offset Position
      const hierarchy = PART_HIERARCHY_CONFIG[slotKey];
      const defaultOff = hierarchy?.defaultOffset ?? [0, 0];
      const offsetX = override?.offset?.[0] ?? (part.offset && (part.offset[0] !== 0 || part.offset[1] !== 0) ? part.offset[0] : defaultOff[0]);
      const offsetY = override?.offset?.[1] ?? (part.offset && (part.offset[0] !== 0 || part.offset[1] !== 0) ? part.offset[1] : defaultOff[1]);
      const posX = offsetX * 0.0035 * baseScale;
      const posY = offsetY * -0.0035 * baseScale;

      const zDepth = (override?.z_depth_3d ?? part.z_depth_3d ?? hierarchy?.defaultZDepth3D ?? 0) * layerSpacing;
      mesh.position.set(posX, posY, zDepth);

      // 4. Rotation
      const rot = override?.rotation ?? part.rotation ?? 0;
      mesh.rotation.z = -rot * (Math.PI / 180);

      // 5. Scale & Flip (Preserving Native Aspect Ratio)
      const mat = mesh.material as THREE.MeshBasicMaterial;
      const texImg = mat.map?.image;
      const aspect = (texImg && texImg.width && texImg.height) ? (texImg.width / texImg.height) : 1.0;

      const sxBase = (override?.scale?.[0] ?? part.scale?.[0] ?? 1) * baseScale;
      const syBase = (override?.scale?.[1] ?? part.scale?.[1] ?? 1) * baseScale;
      const flipX = override?.flipX ?? part.flipX ?? false;
      const flipY = override?.flipY ?? part.flipY ?? false;

      const hasExplicitRight = Boolean(part.angles?.[angle]);
      const shouldMirror = isRightSymmetryAngle && !hasExplicitRight;
      const finalSx = (shouldMirror !== flipX) ? -Math.abs(sxBase) * aspect : Math.abs(sxBase) * aspect;
      const finalSy = flipY ? -Math.abs(syBase) : Math.abs(syBase);

      mesh.scale.set(finalSx, finalSy, 1);
    }
  }

  /**
   * Selects appropriate image URL from part.angles based on discrete angle
   */
  private getTextureForAngle(part: any, angle: Character2DAngle): string {
    if (part.angles?.[angle]) return part.angles[angle];

    // Top-down multi-angle fallbacks
    if (angle.startsWith('top_down')) {
      if (angle === 'top_down_three_quarter_right') {
        if (part.angles?.top_down_three_quarter_right) return part.angles.top_down_three_quarter_right;
        if (part.angles?.top_down_three_quarter_left) return part.angles.top_down_three_quarter_left;
        return this.getTextureForAngle(part, 'three_quarter_right');
      }
      if (angle === 'top_down_profile_right') {
        if (part.angles?.top_down_profile_right) return part.angles.top_down_profile_right;
        if (part.angles?.top_down_profile_left) return part.angles.top_down_profile_left;
        return this.getTextureForAngle(part, 'profile_right');
      }
      if (angle === 'top_down_back_three_quarter_right') {
        if (part.angles?.top_down_back_three_quarter_right) return part.angles.top_down_back_three_quarter_right;
        if (part.angles?.top_down_back_three_quarter_left) return part.angles.top_down_back_three_quarter_left;
        return this.getTextureForAngle(part, 'back_three_quarter_right');
      }
      if (angle === 'top_down_three_quarter_left') {
        if (part.angles?.top_down_three_quarter_left) return part.angles.top_down_three_quarter_left;
        return this.getTextureForAngle(part, 'three_quarter_left');
      }
      if (angle === 'top_down_profile_left') {
        if (part.angles?.top_down_profile_left) return part.angles.top_down_profile_left;
        return this.getTextureForAngle(part, 'profile_left');
      }
      if (angle === 'top_down_back_three_quarter_left') {
        if (part.angles?.top_down_back_three_quarter_left) return part.angles.top_down_back_three_quarter_left;
        return this.getTextureForAngle(part, 'back_three_quarter_left');
      }
      if (angle === 'top_down_back') {
        if (part.angles?.top_down_back) return part.angles.top_down_back;
        return this.getTextureForAngle(part, 'back');
      }
      if (part.angles?.top_down) return part.angles.top_down;
      return this.getTextureForAngle(part, 'front');
    }

    // Standard horizontal fallbacks
    if (angle === 'three_quarter_left' && part.angles?.three_quarter_left) return part.angles.three_quarter_left;
    if (angle === 'three_quarter_right' && (part.angles?.three_quarter_right || part.angles?.three_quarter_left)) {
      return part.angles.three_quarter_right || part.angles.three_quarter_left;
    }
    if (angle === 'profile_left' && part.angles?.profile_left) return part.angles.profile_left;
    if (angle === 'profile_right') {
      return part.angles?.profile_right || part.angles?.profile_left || part.angles?.front;
    }
    if (angle === 'back_three_quarter_left' && part.angles?.back_three_quarter_left) return part.angles.back_three_quarter_left;
    if (angle === 'back_three_quarter_right') {
      return part.angles?.back_three_quarter_right || part.angles?.back_three_quarter_left;
    }
    if (angle === 'back' && part.angles?.back) return part.angles.back;
    if (part.angles?.front) return part.angles.front;
    return part.path || '';
  }

  /**
   * Calculates Camera Azimuth & Elevation Angles and classifies into distinct 3D directions
   */
  private isTopDownMode: boolean = false;

  public updateCameraAngleDetection(): AngleDetectionResult {
    const camPos = this.camera.position;
    const target = this.controls.target;

    // Check Polar Angle (Elevation / Pitch)
    const polarAngle = this.controls.getPolarAngle();
    this.isTopDownMode = polarAngle < 0.68;
    const isTopDown = this.isTopDownMode;

    // Azimuth angle in radians in XZ plane relative to head center target
    const dx = camPos.x - target.x;
    const dz = camPos.z - target.z;
    let rad = Math.atan2(dx, dz);
    if (rad < 0) rad += Math.PI * 2;
    const angleDeg = (rad * 180) / Math.PI;

    let discreteAngle: Character2DAngle = 'front';
    let angleLabel = 'Chính Diện (0°)';
    let compass = 'S';

    if (isTopDown) {
      if (angleDeg >= 337.5 || angleDeg < 22.5) {
        discreteAngle = 'top_down';
        angleLabel = '👑 Đỉnh Đầu (0° Chính Diện)';
        compass = 'TOP-S';
      } else if (angleDeg >= 22.5 && angleDeg < 67.5) {
        discreteAngle = 'top_down_three_quarter_left';
        angleLabel = '👑 Đỉnh Đầu (45° Nghiêng Trái)';
        compass = 'TOP-SE';
      } else if (angleDeg >= 67.5 && angleDeg < 112.5) {
        discreteAngle = 'top_down_profile_left';
        angleLabel = '👑 Đỉnh Đầu (90° Ngang Tai Trái)';
        compass = 'TOP-E';
      } else if (angleDeg >= 112.5 && angleDeg < 157.5) {
        discreteAngle = 'top_down_back_three_quarter_left';
        angleLabel = '👑 Đỉnh Đầu (135° Sau Chéo Trái)';
        compass = 'TOP-NE';
      } else if (angleDeg >= 157.5 && angleDeg < 202.5) {
        discreteAngle = 'top_down_back';
        angleLabel = '👑 Đỉnh Đầu (180° Sau Gáy)';
        compass = 'TOP-N';
      } else if (angleDeg >= 202.5 && angleDeg < 247.5) {
        discreteAngle = 'top_down_back_three_quarter_right';
        angleLabel = '👑 Đỉnh Đầu (225° Sau Chéo Phải)';
        compass = 'TOP-NW';
      } else if (angleDeg >= 247.5 && angleDeg < 292.5) {
        discreteAngle = 'top_down_profile_right';
        angleLabel = '👑 Đỉnh Đầu (270° Ngang Tai Phải)';
        compass = 'TOP-W';
      } else {
        discreteAngle = 'top_down_three_quarter_right';
        angleLabel = '👑 Đỉnh Đầu (315° Nghiêng Phải)';
        compass = 'TOP-SW';
      }
    } else {
      if (angleDeg >= 337.5 || angleDeg < 22.5) {
        discreteAngle = 'front';
        angleLabel = 'Chính Diện (0°)';
        compass = 'S';
      } else if (angleDeg >= 22.5 && angleDeg < 67.5) {
        discreteAngle = 'three_quarter_left';
        angleLabel = 'Nghiêng 3/4 Trái (45°)';
        compass = 'SE';
      } else if (angleDeg >= 67.5 && angleDeg < 112.5) {
        discreteAngle = 'profile_left';
        angleLabel = '👂 Nhìn Thẳng Tai Trái (90° Profile)';
        compass = 'E';
      } else if (angleDeg >= 112.5 && angleDeg < 157.5) {
        discreteAngle = 'back_three_quarter_left';
        angleLabel = 'Nghiêng Sau Trái (135°)';
        compass = 'NE';
      } else if (angleDeg >= 157.5 && angleDeg < 202.5) {
        discreteAngle = 'back';
        angleLabel = 'Sau Lưng (180°)';
        compass = 'N';
      } else if (angleDeg >= 202.5 && angleDeg < 247.5) {
        discreteAngle = 'back_three_quarter_right';
        angleLabel = 'Nghiêng Sau Phải (225°)';
        compass = 'NW';
      } else if (angleDeg >= 247.5 && angleDeg < 292.5) {
        discreteAngle = 'profile_right';
        angleLabel = '👂 Nhìn Thẳng Tai Phải (270° Profile)';
        compass = 'W';
      } else {
        discreteAngle = 'three_quarter_right';
        angleLabel = 'Nghiêng 3/4 Phải (315°)';
        compass = 'SW';
      }
    }

    // If angle changed, apply transforms & swap textures for all parts
    if (discreteAngle !== this.currentDiscreteAngle && this.currentAssembly) {
      this.currentDiscreteAngle = discreteAngle;
      this.applyTransformsForAngle(discreteAngle);
    }

    const result: AngleDetectionResult = {
      angleDeg: Math.round(angleDeg),
      discreteAngle,
      angleLabel,
      compassDirection: compass,
    };

    if (this.onAngleChangeCallback) {
      this.onAngleChangeCallback(result);
    }

    return result;
  }

  public getCurrentDiscreteAngle(): Character2DAngle {
    return this.currentDiscreteAngle;
  }

  /**
   * Smoothly / directly jumps camera to target azimuth angle (degrees) or top-down view centered on head
   */
  public jumpToAngle(targetDeg: number, isTopDown?: boolean): void {
    if (isTopDown !== undefined) {
      this.isTopDownMode = isTopDown;
    }
    const headCenterY = 0.52;
    this.controls.target.set(0, headCenterY, 0);

    const rad = (targetDeg * Math.PI) / 180;
    if (this.isTopDownMode) {
      // Orbit around the crown of the head in top-down mode
      const orbitRadius = 0.8;
      const cameraHeight = 2.4;
      const x = Math.sin(rad) * orbitRadius;
      const z = Math.cos(rad) * orbitRadius;
      this.camera.position.set(x, headCenterY + cameraHeight, z);
    } else {
      // Orbit horizontally around head center
      const orbitRadius = 2.8;
      const x = Math.sin(rad) * orbitRadius;
      const z = Math.cos(rad) * orbitRadius;
      this.camera.position.set(x, headCenterY, z);
    }
    this.controls.update();
  }

  /**
   * Updates 3D procedural breathing & limb animations
   */
  public updateAnimations(animState: PuppetAnimationState): void {
    const t = animState.time;
    const breatheY = animState.mode === 'breathe' ? Math.sin(t * 3) * 0.02 : 0;

    for (const [slot, mesh] of this.partMeshes.entries()) {
      if (slot === 'dau' || slot === 'mat' || slot === 'mui' || slot === 'mieng' || slot === 'toc_truoc') {
        const baseOffsetY = this.currentAssembly?.parts[slot]?.offset?.[1] || 0;
        mesh.position.y = (baseOffsetY * -0.005) + breatheY;
      }
    }
  }

  /**
   * Resize Handler
   */
  public resize(width: number, height: number): void {
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  }

  private startLoop(): void {
    const render = () => {
      this.controls.update();
      this.updateCameraAngleDetection();
      this.renderer.render(this.scene, this.camera);
      this.animFrameId = requestAnimationFrame(render);
    };
    this.animFrameId = requestAnimationFrame(render);
  }

  public dispose(): void {
    if (this.animFrameId) cancelAnimationFrame(this.animFrameId);
    this.controls.dispose();
    this.renderer.dispose();
    if (this.renderer.domElement.parentElement) {
      this.renderer.domElement.parentElement.removeChild(this.renderer.domElement);
    }
  }
}
