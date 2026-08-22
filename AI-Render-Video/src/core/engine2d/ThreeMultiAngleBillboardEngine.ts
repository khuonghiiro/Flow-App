import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DAngle,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../assets/Asset2DRegistry';
import { PuppetAnimationState } from './Canvas2DPuppetEngine';

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

  constructor(container: HTMLElement, onAngleChange?: (res: AngleDetectionResult) => void) {
    this.container = container;
    this.onAngleChangeCallback = onAngleChange;

    const width = container.clientWidth || 800;
    const height = container.clientHeight || 500;

    // 1. Scene & Camera Setup
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x090d16);

    this.camera = new THREE.PerspectiveCamera(35, width / height, 0.1, 100);
    this.camera.position.set(0, 0.2, 3.2);

    // 2. WebGL Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.1;
    this.container.appendChild(this.renderer.domElement);

    // 3. OrbitControls
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.target.set(0, 0.1, 0);
    this.controls.maxPolarAngle = Math.PI / 2 + 0.1;
    this.controls.minDistance = 1.0;
    this.controls.maxDistance = 8.0;

    // 4. Character Group & Lighting
    this.characterGroup = new THREE.Group();
    this.scene.add(this.characterGroup);
    this.setupLightingAndEnvironment();

    // 5. Start Render Loop
    this.startLoop();
  }

  private setupLightingAndEnvironment(): void {
    // Ambient light
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.2);
    this.scene.add(ambientLight);

    // Key Directional Light
    const keyLight = new THREE.DirectionalLight(0xe0f2fe, 1.5);
    keyLight.position.set(3, 5, 4);
    this.scene.add(keyLight);

    // Back rim light
    const rimLight = new THREE.DirectionalLight(0xa855f7, 0.8);
    rimLight.position.set(-3, 3, -4);
    this.scene.add(rimLight);

    // Subtle Ground Grid & Pedestal
    const grid = new THREE.GridHelper(6, 12, 0x38bdf8, 0x1e293b);
    grid.position.y = -1.1;
    this.scene.add(grid);

    // Circular shadow base
    const shadowGeo = new THREE.CircleGeometry(0.8, 32);
    const shadowMat = new THREE.MeshBasicMaterial({
      color: 0x000000,
      transparent: true,
      opacity: 0.35,
    });
    const shadowMesh = new THREE.Mesh(shadowGeo, shadowMat);
    shadowMesh.rotation.x = -Math.PI / 2;
    shadowMesh.position.y = -1.09;
    this.scene.add(shadowMesh);
  }

  /**
   * Loads or returns cached Three.js Texture
   */
  private loadTexture(url: string): THREE.Texture {
    if (this.textureCache.has(url)) {
      return this.textureCache.get(url)!;
    }
    const tex = this.textureLoader.load(url);
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.generateMipmaps = true;
    tex.minFilter = THREE.LinearMipmapLinearFilter;
    tex.magFilter = THREE.LinearFilter;
    this.textureCache.set(url, tex);
    return tex;
  }

  /**
   * Rebuilds all 3D mesh planes from Character2DAssembly
   */
  public setAssembly(assembly: Character2DAssembly): void {
    this.currentAssembly = assembly;

    // Remove old meshes
    for (const mesh of this.partMeshes.values()) {
      this.characterGroup.remove(mesh);
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.partMeshes.clear();

    const layerSpacing = assembly.layer_depth_spacing ?? 1.0;
    const baseScale = assembly.base_scale || 1.0;

    // Create a Plane Mesh for each part
    for (const [slotKey, part] of Object.entries(assembly.parts)) {
      if (!part?.path) continue;
      const slot = slotKey as Character2DPartType;

      const hierarchy = PART_HIERARCHY_CONFIG[slot];
      const zDepth = (part.z_depth_3d ?? hierarchy?.defaultZDepth3D ?? 0) * layerSpacing;

      // Initial active texture for current camera angle
      const activeUrl = this.getTextureForAngle(part, this.currentDiscreteAngle);
      const texture = this.loadTexture(activeUrl);

      // Plane aspect ratio ~ based on standard dimensions
      const planeW = 1.0;
      const planeH = 1.0;
      const geo = new THREE.PlaneGeometry(planeW, planeH);

      // Shift geometry vertices according to 2D pivot point [px, py]
      const pivot = part.pivot || hierarchy?.defaultPivot || [0.5, 0.5];
      geo.translate((0.5 - pivot[0]) * planeW, (pivot[1] - 0.5) * planeH, 0);

      const mat = new THREE.MeshStandardMaterial({
        map: texture,
        transparent: true,
        alphaTest: 0.05,
        side: THREE.DoubleSide,
        roughness: 0.6,
      });

      const mesh = new THREE.Mesh(geo, mat);

      // Position in 3D: Convert 2D pixel offset to 3D units
      const posX = (part.offset?.[0] || 0) * 0.005 * baseScale;
      const posY = (part.offset?.[1] || 0) * -0.005 * baseScale;
      mesh.position.set(posX, posY, zDepth);

      // Rotation & Scale
      mesh.rotation.z = -(part.rotation || 0) * (Math.PI / 180);
      const sx = (part.scale?.[0] ?? 1) * (part.flipX ? -1 : 1) * baseScale;
      const sy = (part.scale?.[1] ?? 1) * (part.flipY ? -1 : 1) * baseScale;
      mesh.scale.set(sx, sy, 1);

      this.characterGroup.add(mesh);
      this.partMeshes.set(slot, mesh);
    }
  }

  /**
   * Selects appropriate image URL from part.angles based on discrete angle
   */
  private getTextureForAngle(part: any, angle: Character2DAngle): string {
    if (part.angles?.[angle]) return part.angles[angle];
    // Fallbacks
    if (angle === 'three_quarter_left' && part.angles?.three_quarter_left) return part.angles.three_quarter_left;
    if (angle === 'three_quarter_right' && (part.angles?.three_quarter_right || part.angles?.three_quarter_left)) {
      return part.angles.three_quarter_right || part.angles.three_quarter_left;
    }
    if (angle === 'profile_left' && part.angles?.profile_left) return part.angles.profile_left;
    if (angle === 'profile_right' && (part.angles?.profile_right || part.angles?.profile_left)) {
      return part.angles.profile_right || part.angles.profile_left;
    }
    if (angle === 'back' && part.angles?.back) return part.angles.back;
    if (part.angles?.front) return part.angles.front;
    return part.path || '';
  }

  /**
   * Calculates Camera Azimuth Angle & classifies into 8 directions
   */
  public updateCameraAngleDetection(): AngleDetectionResult {
    const camPos = this.camera.position;
    const target = this.controls.target;

    // Azimuth angle in radians in XZ plane
    const dx = camPos.x - target.x;
    const dz = camPos.z - target.z;
    let rad = Math.atan2(dx, dz); // 0 facing front (+Z), PI/2 at +X (right), PI facing back (-Z), -PI/2 at -X (left)
    if (rad < 0) rad += Math.PI * 2;

    const angleDeg = (rad * 180) / Math.PI;

    let discreteAngle: Character2DAngle = 'front';
    let angleLabel = 'Chính Diện (0°)';
    let compass = 'N';

    if (angleDeg >= 337.5 || angleDeg < 22.5) {
      discreteAngle = 'front';
      angleLabel = 'Chính Diện (0°)';
      compass = 'N';
    } else if (angleDeg >= 22.5 && angleDeg < 67.5) {
      discreteAngle = 'three_quarter_left';
      angleLabel = 'Nghiêng 3/4 Trái (45°)';
      compass = 'NE';
    } else if (angleDeg >= 67.5 && angleDeg < 112.5) {
      discreteAngle = 'profile_left';
      angleLabel = 'Nghiêng Ngang 90° (Cằm/Mũi/Tai)';
      compass = 'E';
    } else if (angleDeg >= 112.5 && angleDeg < 157.5) {
      discreteAngle = 'back_three_quarter_left';
      angleLabel = 'Nghiêng Sau Trái (135°)';
      compass = 'SE';
    } else if (angleDeg >= 157.5 && angleDeg < 202.5) {
      discreteAngle = 'back';
      angleLabel = 'Sau Lưng (180°)';
      compass = 'S';
    } else if (angleDeg >= 202.5 && angleDeg < 247.5) {
      discreteAngle = 'back_three_quarter_right';
      angleLabel = 'Nghiêng Sau Phải (225°)';
      compass = 'SW';
    } else if (angleDeg >= 247.5 && angleDeg < 292.5) {
      discreteAngle = 'profile_right';
      angleLabel = 'Nghiêng Ngang Phải (270°)';
      compass = 'W';
    } else {
      discreteAngle = 'three_quarter_right';
      angleLabel = 'Nghiêng 3/4 Phải (315°)';
      compass = 'NW';
    }

    // If angle changed, swap textures across all parts
    if (discreteAngle !== this.currentDiscreteAngle && this.currentAssembly) {
      this.currentDiscreteAngle = discreteAngle;
      this.swapTexturesForAngle(discreteAngle);
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

  /**
   * Swaps mesh materials to the texture of the newly active angle
   */
  private swapTexturesForAngle(angle: Character2DAngle): void {
    if (!this.currentAssembly) return;

    for (const [slotKey, mesh] of this.partMeshes.entries()) {
      const part = this.currentAssembly.parts[slotKey];
      if (!part) continue;

      const targetUrl = this.getTextureForAngle(part, angle);
      if (targetUrl) {
        const tex = this.loadTexture(targetUrl);
        const mat = mesh.material as THREE.MeshStandardMaterial;
        mat.map = tex;
        mat.needsUpdate = true;
      }
    }
  }

  /**
   * Smoothly / directly jumps camera to target azimuth angle (degrees)
   */
  public jumpToAngle(targetDeg: number): void {
    const radius = 3.2;
    const rad = (targetDeg * Math.PI) / 180;
    const x = Math.sin(rad) * radius;
    const z = Math.cos(rad) * radius;
    this.camera.position.set(x, 0.2, z);
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
        mesh.position.y = ((this.currentAssembly?.parts[slot]?.offset?.[1] || 0) * -0.005) + breatheY;
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
