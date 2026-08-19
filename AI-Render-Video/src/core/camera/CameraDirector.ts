import * as THREE from 'three';
import { CameraTrack, MasterSceneConfig } from '../../types/scene';
import { CameraFraming, CameraPose, ActorVisualState, InspectCameraAngle } from './CameraFraming';
export type { InspectCameraAngle };

export class CameraDirector {
  private camera: THREE.PerspectiveCamera;
  private currentPose: CameraPose;

  // Inspect mode state: Fixed 3D anchor that NEVER moves
  private inspectTargetActorId: string | null = null;
  private inspectAngle: InspectCameraAngle = 'front';
  private lockedInspectPose: CameraPose | null = null;

  // Track anchor cache
  private lastTrackKey: string = '';
  private latchedActorStates: Map<string, ActorVisualState> = new Map();

  constructor(camera: THREE.PerspectiveCamera) {
    this.camera = camera;
    this.currentPose = {
      position: new THREE.Vector3(0, 3, 10),
      target: new THREE.Vector3(0, 1.2, 0),
      fov: 50,
    };
  }

  public setInspectMode(
    actorId: string,
    angle: InspectCameraAngle = 'front'
  ): void {
    this.inspectTargetActorId = actorId;
    this.inspectAngle = angle;
    this.lockedInspectPose = null; // Recompute locked 3D anchor on next frame
  }

  public toggleInspectMode(
    actorId: string,
    angle: InspectCameraAngle = 'front'
  ): boolean {
    if (this.isInspecting() && this.inspectTargetActorId === actorId) {
      this.clearInspectMode();
      return false; // Turned off
    } else {
      this.setInspectMode(actorId, angle);
      return true; // Turned on
    }
  }

  public setInspectAngle(angle: InspectCameraAngle): void {
    if (this.inspectTargetActorId) {
      this.inspectAngle = angle;
      this.lockedInspectPose = null; // Recompute locked 3D anchor for new angle
    }
  }

  public clearInspectMode(): void {
    this.inspectTargetActorId = null;
    this.lockedInspectPose = null;
  }

  public isInspecting(): boolean {
    return this.inspectTargetActorId !== null;
  }

  public getInspectTargetId(): string | null {
    return this.inspectTargetActorId;
  }

  public getInspectAngle(): InspectCameraAngle {
    return this.inspectAngle;
  }

  /**
   * Filter raw actor states: Lock the position into a fixed anchor for the shot.
   * Prevents head bone talking movements, breathing bobbing, or terrain micro-snapping from shaking the camera.
   */
  private getStableActorStates(
    trackKey: string,
    rawActorStates: Map<string, ActorVisualState>,
    delta: number
  ): Map<string, ActorVisualState> {
    const isNewTrack = this.lastTrackKey !== trackKey;
    if (isNewTrack) {
      this.lastTrackKey = trackKey;
      this.latchedActorStates.clear();
    }

    const stableMap = new Map<string, ActorVisualState>();

    for (const [id, raw] of rawActorStates.entries()) {
      let latched = this.latchedActorStates.get(id);
      if (!latched || isNewTrack) {
        latched = {
          position: raw.position.clone(),
          headPosition: new THREE.Vector3(raw.position.x, raw.position.y + 1.77, raw.position.z),
          rotationY: raw.rotationY,
        };
        this.latchedActorStates.set(id, latched);
      } else {
        // Deadzone filter: Only follow if actor actually walked or displaced (> 0.25m)
        const distSq = latched.position.distanceToSquared(raw.position);
        if (distSq > 0.0625) {
          latched.position.lerp(raw.position, 1 - Math.exp(-6.0 * delta));
          latched.headPosition.set(latched.position.x, latched.position.y + 1.77, latched.position.z);
        }

        // Deadzone filter for rotation: Only follow if actor rotated > 15 degrees
        const rotDiff = Math.abs(latched.rotationY - raw.rotationY);
        if (rotDiff > 0.26) {
          latched.rotationY = THREE.MathUtils.lerp(latched.rotationY, raw.rotationY, 1 - Math.exp(-6.0 * delta));
        }
      }

      stableMap.set(id, latched);
    }

    return stableMap;
  }

  public update(
    scene: MasterSceneConfig,
    currentTime: number,
    actorStates: Map<string, ActorVisualState>,
    delta: number
  ): void {
    // 1. Inspect Mode: 100% Fixed Static Camera Anchor
    if (this.inspectTargetActorId) {
      if (!this.lockedInspectPose) {
        const actorState = actorStates.get(this.inspectTargetActorId);
        if (actorState) {
          const p = actorState.position;
          const rotY = actorState.rotationY;
          const target = new THREE.Vector3(p.x, p.y + 1.77, p.z);

          let angleOffset = 0;
          let dist = 0.95;
          let heightOffset = 0.0;

          if (this.inspectAngle === 'three_quarter') {
            angleOffset = Math.PI * 0.22;
            dist = 1.05;
            heightOffset = 0.02;
          } else if (this.inspectAngle === 'side') {
            angleOffset = Math.PI * 0.5;
            dist = 0.95;
            heightOffset = 0.0;
          } else if (this.inspectAngle === 'low_angle') {
            angleOffset = Math.PI * 0.08;
            dist = 1.15;
            heightOffset = -0.22;
          }

          const totalAngle = rotY + angleOffset;
          const pos = new THREE.Vector3(
            p.x + Math.sin(totalAngle) * dist,
            p.y + 1.77 + heightOffset,
            p.z + Math.cos(totalAngle) * dist
          );

          this.lockedInspectPose = {
            position: pos,
            target: target,
            fov: 32,
          };
        }
      }

      if (this.lockedInspectPose) {
        this.applyPose(this.lockedInspectPose, 6.0, delta);
      }
      return;
    }

    // 2. Evaluate current CameraTrack from scene
    const tracks = scene.camera_tracks || [];
    const activeTrack =
      tracks.find((t) => currentTime >= t.start && currentTime <= t.end) || tracks[0];

    if (activeTrack) {
      const trackKey = `track_${activeTrack.start}_${activeTrack.end}_${activeTrack.shot_type}`;
      const stableStates = this.getStableActorStates(trackKey, actorStates, delta);

      const targetPose = CameraFraming.evaluatePose(activeTrack, currentTime, stableStates);
      this.applyPose(targetPose, 4.5, delta);
    }
  }

  private applyPose(targetPose: CameraPose, speed: number, delta: number): void {
    const smoothFactor = 1 - Math.exp(-speed * delta);
    
    this.currentPose.position.lerp(targetPose.position, smoothFactor);
    this.currentPose.target.lerp(targetPose.target, smoothFactor);
    this.currentPose.fov = THREE.MathUtils.lerp(
      this.currentPose.fov,
      targetPose.fov,
      smoothFactor
    );

    // If within 0.5mm, snap to target to stop micro-floating
    if (this.currentPose.position.distanceToSquared(targetPose.position) < 0.000001) {
      this.currentPose.position.copy(targetPose.position);
    }
    if (this.currentPose.target.distanceToSquared(targetPose.target) < 0.000001) {
      this.currentPose.target.copy(targetPose.target);
    }

    this.camera.position.copy(this.currentPose.position);
    this.camera.lookAt(this.currentPose.target);
    
    if (targetPose.roll !== undefined && targetPose.roll !== 0) {
      this.camera.rotation.z = THREE.MathUtils.lerp(
        this.camera.rotation.z,
        targetPose.roll,
        smoothFactor
      );
    } else if (Math.abs(this.camera.rotation.z) > 0.001) {
      this.camera.rotation.z = THREE.MathUtils.lerp(this.camera.rotation.z, 0, smoothFactor);
    }

    if (Math.abs(this.camera.fov - this.currentPose.fov) > 0.1) {
      this.camera.fov = this.currentPose.fov;
      this.camera.updateProjectionMatrix();
    }
  }
}
