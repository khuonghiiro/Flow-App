import * as THREE from 'three';
import { CameraTrack, MasterSceneConfig } from '../../types/scene';
import { CameraFraming, CameraPose, ActorVisualState, InspectCameraAngle } from './CameraFraming';
export type { InspectCameraAngle };

export class CameraDirector {
  private camera: THREE.PerspectiveCamera;
  private currentPose: CameraPose;
  private currentRoll: number = 0;

  // Inspect mode state: Fixed 3D anchor that NEVER moves
  private inspectTargetActorId: string | null = null;
  private inspectAngle: InspectCameraAngle = 'front';
  private lockedInspectPose: CameraPose | null = null;

  // Track anchor cache & transition handling
  private lastTrackKey: string = '';
  private latchedActorStates: Map<string, ActorVisualState> = new Map();

  constructor(camera: THREE.PerspectiveCamera) {
    this.camera = camera;
    this.currentPose = {
      position: new THREE.Vector3(0, 3, 10),
      target: new THREE.Vector3(0, 1.2, 0),
      fov: 50,
      roll: 0,
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
   * Filter raw actor states: Smooth position into a stable anchor for the shot.
   * Isolates the camera from raw skeleton bone talking, breathing bobbing, or mesh jitter.
   */
  private getStableActorStates(
    trackKey: string,
    rawActorStates: Map<string, ActorVisualState>,
    delta: number
  ): Map<string, ActorVisualState> {
    const isNewTrack = this.lastTrackKey !== trackKey;
    if (isNewTrack) {
      this.lastTrackKey = trackKey;
    }

    const stableMap = new Map<string, ActorVisualState>();

    for (const [id, raw] of rawActorStates.entries()) {
      let latched = this.latchedActorStates.get(id);
      if (!latched || isNewTrack) {
        latched = {
          position: raw.position.clone(),
          headPosition: new THREE.Vector3(raw.position.x, raw.position.y + 1.68, raw.position.z),
          rotationY: raw.rotationY,
        };
        this.latchedActorStates.set(id, latched);
      } else {
        // Continuous exponential smoothing without jarring threshold step-discontinuities
        const smoothRate = 1 - Math.exp(-8.0 * delta);
        latched.position.lerp(raw.position, smoothRate);
        latched.headPosition.set(latched.position.x, latched.position.y + 1.68, latched.position.z);

        // Smooth rotation without 360 wrap-around glitch
        let diff = raw.rotationY - latched.rotationY;
        while (diff < -Math.PI) diff += Math.PI * 2;
        while (diff > Math.PI) diff -= Math.PI * 2;
        latched.rotationY += diff * smoothRate;
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
          const stableHeadY = p.y + 1.68;
          const target = new THREE.Vector3(p.x, stableHeadY, p.z);

          let angleOffset = 0;
          let dist = 0.85;
          let heightOffset = 0.0;

          if (this.inspectAngle === 'three_quarter') {
            angleOffset = Math.PI * 0.22;
            dist = 0.95;
            heightOffset = 0.02;
          } else if (this.inspectAngle === 'side') {
            angleOffset = Math.PI * 0.5;
            dist = 0.85;
            heightOffset = 0.0;
          } else if (this.inspectAngle === 'low_angle') {
            angleOffset = Math.PI * 0.08;
            dist = 1.05;
            heightOffset = -0.20;
          }

          const totalAngle = rotY + angleOffset;
          const pos = new THREE.Vector3(
            p.x + Math.sin(totalAngle) * dist,
            stableHeadY + heightOffset,
            p.z + Math.cos(totalAngle) * dist
          );

          this.lockedInspectPose = {
            position: pos,
            target: target,
            fov: 30,
            roll: 0,
          };
        }
      }

      if (this.lockedInspectPose) {
        this.applyPose(this.lockedInspectPose, 8.0, delta, false);
      }
      return;
    }

    // 2. Evaluate current CameraTrack from scene
    const tracks = scene.camera_tracks || [];
    const activeTrack =
      tracks.find((t) => currentTime >= t.start && currentTime <= t.end) || tracks[0];

    if (activeTrack) {
      const trackKey = `track_${activeTrack.start}_${activeTrack.end}_${activeTrack.shot_type}`;
      const isNewTrack = this.lastTrackKey !== trackKey;
      const stableStates = this.getStableActorStates(trackKey, actorStates, delta);

      const targetPose = CameraFraming.evaluatePose(activeTrack, currentTime, stableStates);
      
      // On track cut (first frame of a new track), snap instantly unless within track animation
      this.applyPose(targetPose, 6.0, delta, isNewTrack);
    }
  }

  private applyPose(targetPose: CameraPose, speed: number, delta: number, isInstant: boolean = false): void {
    if (isInstant) {
      this.currentPose.position.copy(targetPose.position);
      this.currentPose.target.copy(targetPose.target);
      this.currentPose.fov = targetPose.fov;
      this.currentRoll = targetPose.roll || 0;
    } else {
      const smoothFactor = 1 - Math.exp(-speed * delta);
      this.currentPose.position.lerp(targetPose.position, smoothFactor);
      this.currentPose.target.lerp(targetPose.target, smoothFactor);
      this.currentPose.fov = THREE.MathUtils.lerp(
        this.currentPose.fov,
        targetPose.fov,
        smoothFactor
      );
      this.currentRoll = THREE.MathUtils.lerp(
        this.currentRoll,
        targetPose.roll || 0,
        smoothFactor
      );
    }

    // Apply strictly aligned world up vector and lookAt (guarantees level horizon)
    this.camera.position.copy(this.currentPose.position);
    this.camera.up.set(0, 1, 0);
    this.camera.lookAt(this.currentPose.target);

    // Apply local roll (Dutch angle) only around the view axis if roll != 0
    if (Math.abs(this.currentRoll) > 0.0001) {
      this.camera.rotateZ(this.currentRoll);
    }

    if (Math.abs(this.camera.fov - this.currentPose.fov) > 0.01) {
      this.camera.fov = this.currentPose.fov;
      this.camera.updateProjectionMatrix();
    }
  }
}
