import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GizmoTransformManager } from './GizmoTransformManager';

export interface RendererOptions {
  antialias?: boolean;
  powerPreference?: 'high-performance' | 'default' | 'low-power';
  preserveDrawingBuffer?: boolean;
  alpha?: boolean;
}

export class ThreeRenderer {
  public scene: THREE.Scene;
  public camera: THREE.PerspectiveCamera;
  public renderer: THREE.WebGLRenderer;
  public controls: OrbitControls | null = null;
  public gizmo: GizmoTransformManager | null = null;
  public container: HTMLElement | null = null;
  public isRunning: boolean = false;
  private animationFrameId: number | null = null;
  private renderCallbacks: Array<(delta: number, time: number) => void> = [];
  private clock: THREE.Clock;
  public fps: number = 60;
  private frameCount: number = 0;
  private lastFpsUpdate: number = 0;
  private isFreeCamEnabled: boolean = false;

  constructor(options: RendererOptions = {}) {
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color('#0f111a');

    this.camera = new THREE.PerspectiveCamera(50, 16 / 9, 0.1, 3000);
    this.camera.position.set(0, 3, 10);

    this.renderer = new THREE.WebGLRenderer({
      antialias: options.antialias ?? true,
      powerPreference: options.powerPreference ?? 'high-performance',
      preserveDrawingBuffer: options.preserveDrawingBuffer ?? true,
      alpha: options.alpha ?? false,
    });

    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.0));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.1;

    this.clock = new THREE.Clock();
  }

  public mount(container: HTMLElement): void {
    this.container = container;
    this.container.innerHTML = '';
    this.container.appendChild(this.renderer.domElement);

    // Initialize OrbitControls for Free Cam mode
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.05;
    // Apply saved free cam state (setFreeCam may have been called before mount)
    this.controls.enabled = this.isFreeCamEnabled;
    if (this.isFreeCamEnabled) {
      this.controls.target.set(0, 1.2, 0);
    }

    // Initialize Unity-Style Transform Gizmo Controls
    this.gizmo = new GizmoTransformManager(this.camera, this.renderer.domElement, this.scene);
    this.gizmo.onDraggingChange((isDragging) => {
      if (this.controls) {
        this.controls.enabled = !isDragging && this.isFreeCamEnabled;
      }
    });

    this.handleResize();
    window.addEventListener('resize', this.onWindowResize);
  }

  public unmount(): void {
    this.stop();
    window.removeEventListener('resize', this.onWindowResize);
    if (this.gizmo) {
      this.gizmo.dispose();
      this.gizmo = null;
    }
    if (this.controls) {
      this.controls.dispose();
      this.controls = null;
    }
    if (this.container && this.renderer.domElement.parentElement === this.container) {
      this.container.removeChild(this.renderer.domElement);
    }
  }

  public setFreeCam(enabled: boolean): void {
    this.isFreeCamEnabled = enabled;
    if (this.controls) {
      this.controls.enabled = enabled;
      if (enabled) {
        this.controls.target.set(0, 1.2, 0);
      }
    }
  }

  private onWindowResize = (): void => {
    this.handleResize();
  };

  public handleResize(): void {
    if (!this.container) return;
    const width = this.container.clientWidth || 800;
    const height = this.container.clientHeight || 450;
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  public onRender(callback: (delta: number, time: number) => void): void {
    this.renderCallbacks.push(callback);
  }

  public removeRenderCallback(callback: (delta: number, time: number) => void): void {
    this.renderCallbacks = this.renderCallbacks.filter((cb) => cb !== callback);
  }

  public start(): void {
    if (this.isRunning) return;
    this.isRunning = true;
    this.clock.start();
    this.lastFpsUpdate = performance.now();
    this.animate();
  }

  public stop(): void {
    this.isRunning = false;
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
  }

  private animate = (): void => {
    if (!this.isRunning) return;
    this.animationFrameId = requestAnimationFrame(this.animate);

    const delta = Math.min(this.clock.getDelta(), 0.1);
    const elapsed = this.clock.getElapsedTime();

    this.frameCount++;
    const now = performance.now();
    if (now - this.lastFpsUpdate >= 1000) {
      this.fps = Math.round((this.frameCount * 1000) / (now - this.lastFpsUpdate));
      this.frameCount = 0;
      this.lastFpsUpdate = now;
    }

    if (this.controls && this.controls.enabled) {
      this.controls.update();
    }

    for (let i = 0; i < this.renderCallbacks.length; i++) {
      this.renderCallbacks[i](delta, elapsed);
    }

    this.renderer.render(this.scene, this.camera);
  };

  public is2DMode: boolean = false;

  public set2DMode(enabled: boolean): void {
    this.is2DMode = enabled;
    if (!this.controls) return;

    if (enabled) {
      // Switch to 2D Top-Down View
      const target = this.controls.target;
      this.camera.position.set(target.x, target.y + 22.0, target.z + 0.0001);
      this.camera.lookAt(target);
      this.controls.maxPolarAngle = 0.01;
      this.controls.minPolarAngle = 0.00;
      this.controls.update();
    } else {
      // Switch to 3D Perspective View
      const target = this.controls.target;
      this.camera.position.set(target.x, target.y + 6.0, target.z + 14.0);
      this.camera.lookAt(target);
      this.controls.maxPolarAngle = Math.PI - 0.05;
      this.controls.minPolarAngle = 0.0;
      this.controls.update();
    }
  }

  public setCameraPresetView(view: 'top' | 'front' | 'right' | 'perspective'): void {
    if (!this.controls) return;
    const target = this.controls.target;

    this.controls.maxPolarAngle = Math.PI - 0.01;
    this.controls.minPolarAngle = 0.0;

    switch (view) {
      case 'top':
        this.camera.position.set(target.x, target.y + 25.0, target.z + 0.0001);
        break;
      case 'front':
        this.camera.position.set(target.x, target.y + 2.0, target.z + 18.0);
        break;
      case 'right':
        this.camera.position.set(target.x + 18.0, target.y + 2.0, target.z);
        break;
      case 'perspective':
        this.camera.position.set(target.x + 10.0, target.y + 8.0, target.z + 14.0);
        break;
    }
    this.camera.lookAt(target);
    this.controls.update();
  }

  /**
   * Rotate / Look around 360 in-place from the current camera position
   * (Like a person standing still at the camera location and looking around 360 degrees)
   */
  public rotateCameraInPlace(deltaYawDeg: number, deltaPitchDeg: number = 0): void {
    if (!this.controls) return;

    // Current forward vector from camera position towards look target
    const forward = this.controls.target.clone().sub(this.camera.position);
    const dist = Math.max(1.0, forward.length());

    // Rotate forward direction around world Y-axis (Yaw)
    const yawRad = (deltaYawDeg * Math.PI) / 180;
    forward.applyAxisAngle(new THREE.Vector3(0, 1, 0), yawRad);

    // If pitch delta is provided, rotate around camera local right axis
    if (deltaPitchDeg !== 0) {
      const pitchRad = (deltaPitchDeg * Math.PI) / 180;
      const right = new THREE.Vector3(1, 0, 0).applyQuaternion(this.camera.quaternion).normalize();
      forward.applyAxisAngle(right, pitchRad);
    }

    forward.normalize().multiplyScalar(dist);

    // Update target point in front of the camera
    this.controls.target.copy(this.camera.position).add(forward);
    this.camera.lookAt(this.controls.target);
    this.controls.update();
  }

  public orbitCamera(deltaYawDeg: number, deltaPitchDeg: number = 0): void {
    if (!this.controls) return;
    const target = this.controls.target;
    const offset = this.camera.position.clone().sub(target);

    // Spherical rotation
    const spherical = new THREE.Spherical().setFromVector3(offset);
    spherical.theta += (deltaYawDeg * Math.PI) / 180;
    spherical.phi = THREE.MathUtils.clamp(
      spherical.phi + (deltaPitchDeg * Math.PI) / 180,
      0.05,
      Math.PI - 0.05
    );

    offset.setFromSpherical(spherical);
    this.camera.position.copy(target).add(offset);
    this.camera.lookAt(target);
    this.controls.update();
  }

  public panCamera(deltaX: number, deltaY: number): void {
    if (!this.controls) return;
    const matrix = new THREE.Matrix4().extractRotation(this.camera.matrix);
    const right = new THREE.Vector3(1, 0, 0).applyMatrix4(matrix).normalize();
    const up = new THREE.Vector3(0, 1, 0).applyMatrix4(matrix).normalize();

    const panOffset = right.multiplyScalar(deltaX).add(up.multiplyScalar(deltaY));
    this.camera.position.add(panOffset);
    this.controls.target.add(panOffset);
    this.controls.update();
  }

  public zoomCamera(deltaZoom: number): void {
    if (!this.controls) return;
    const target = this.controls.target;
    const dir = this.camera.position.clone().sub(target);
    const dist = dir.length();
    const newDist = THREE.MathUtils.clamp(dist + deltaZoom, 2.0, 150.0);
    dir.normalize().multiplyScalar(newDist);
    this.camera.position.copy(target).add(dir);
    this.controls.update();
  }

  public renderDirect(): void {
    this.renderer.render(this.scene, this.camera);
  }

  public getDomElement(): HTMLCanvasElement {
    return this.renderer.domElement;
  }
}
