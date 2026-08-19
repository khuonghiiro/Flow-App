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

    this.camera = new THREE.PerspectiveCamera(50, 16 / 9, 0.1, 500);
    this.camera.position.set(0, 3, 10);

    this.renderer = new THREE.WebGLRenderer({
      antialias: options.antialias ?? true,
      powerPreference: options.powerPreference ?? 'high-performance',
      preserveDrawingBuffer: options.preserveDrawingBuffer ?? true,
      alpha: options.alpha ?? false,
    });

    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.0));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
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

  public renderDirect(): void {
    this.renderer.render(this.scene, this.camera);
  }

  public getDomElement(): HTMLCanvasElement {
    return this.renderer.domElement;
  }
}
