import * as THREE from 'three';
import { TransformControls } from 'three/examples/jsm/controls/TransformControls.js';

export type GizmoMode = 'translate' | 'rotate' | 'scale';
export type GizmoSpace = 'world' | 'local';

export interface GizmoTransformData {
  id: string;
  position: [number, number, number];
  rotation: [number, number, number]; // in degrees
  scale: number;
}

export class GizmoTransformManager {
  private transformControls: TransformControls;
  private gizmoHelper: THREE.Object3D;
  private scene: THREE.Scene;
  private attachedObject: THREE.Object3D | null = null;
  private attachedId: string | null = null;
  private isDragging: boolean = false;
  private currentMode: GizmoMode = 'translate';
  private currentSpace: GizmoSpace = 'world';

  private onTransformChangeCallback: ((data: GizmoTransformData) => void) | null = null;
  private onDraggingChangeCallback: ((isDragging: boolean) => void) | null = null;

  constructor(camera: THREE.Camera, domElement: HTMLElement, scene: THREE.Scene) {
    this.scene = scene;
    this.transformControls = new TransformControls(camera, domElement);
    this.transformControls.size = 0.85;
    this.transformControls.setSpace('world');
    this.transformControls.enabled = false;

    this.gizmoHelper = this.transformControls.getHelper();
    this.gizmoHelper.visible = false;
    this.scene.add(this.gizmoHelper);

    // Event: Dragging changed (disable OrbitControls while dragging gizmo)
    this.transformControls.addEventListener('dragging-changed', (event: any) => {
      this.isDragging = !!event.value;
      if (this.onDraggingChangeCallback) {
        this.onDraggingChangeCallback(this.isDragging);
      }
    });

    // Event: Object transformed in 3D Viewport
    this.transformControls.addEventListener('change', () => {
      if (!this.attachedObject || !this.attachedId) return;

      const p = this.attachedObject.position;
      const r = this.attachedObject.rotation;
      const s = (this.attachedObject.scale.x + this.attachedObject.scale.y + this.attachedObject.scale.z) / 3;

      const transformData: GizmoTransformData = {
        id: this.attachedId,
        position: [
          parseFloat(p.x.toFixed(2)),
          parseFloat(p.y.toFixed(2)),
          parseFloat(p.z.toFixed(2)),
        ],
        rotation: [
          Math.round((r.x * 180) / Math.PI),
          Math.round((r.y * 180) / Math.PI),
          Math.round((r.z * 180) / Math.PI),
        ],
        scale: parseFloat(s.toFixed(2)) || 1.0,
      };

      if (this.onTransformChangeCallback) {
        this.onTransformChangeCallback(transformData);
      }
    });
  }

  public attach(id: string, object3D: THREE.Object3D): void {
    this.attachedId = id;
    this.attachedObject = object3D;
    this.transformControls.attach(object3D);
    this.gizmoHelper.visible = true;
    this.transformControls.enabled = true;
  }

  public detach(): void {
    this.attachedId = null;
    this.attachedObject = null;
    this.transformControls.detach();
    this.gizmoHelper.visible = false;
    this.transformControls.enabled = false;
  }

  public updateCamera(camera: THREE.Camera): void {
    this.transformControls.camera = camera;
  }

  public setMode(mode: GizmoMode): void {
    this.currentMode = mode;
    this.transformControls.setMode(mode);
  }

  public getMode(): GizmoMode {
    return this.currentMode;
  }

  public setSpace(space: GizmoSpace): void {
    this.currentSpace = space;
    this.transformControls.setSpace(space);
  }

  public getSpace(): GizmoSpace {
    return this.currentSpace;
  }

  public setSize(size: number): void {
    this.transformControls.size = size;
  }

  public getIsDragging(): boolean {
    return this.isDragging;
  }

  public getAttachedId(): string | null {
    return this.attachedId;
  }

  public onTransformChange(cb: (data: GizmoTransformData) => void): void {
    this.onTransformChangeCallback = cb;
  }

  public onDraggingChange(cb: (isDragging: boolean) => void): void {
    this.onDraggingChangeCallback = cb;
  }

  public dispose(): void {
    this.detach();
    this.transformControls.dispose();
    this.scene.remove(this.gizmoHelper);
  }
}
