import * as THREE from 'three';
import { CameraTrack, MasterSceneConfig } from '../../types/scene';
import { CameraFraming, CameraPose, ActorVisualState, InspectCameraAngle } from './CameraFraming';
export type { InspectCameraAngle };

export class CameraDirector {
  private camera: THREE.PerspectiveCamera;
  private currentPose: CameraPose;
  private inspectTarget: {
    actorId: string;
    untilTime: number;
    angle: InspectCameraAngle;
  } | null = null;

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
    durationSeconds: number = 6.0,
    angle: InspectCameraAngle = 'front'
  ): void {
    this.inspectTarget = {
      actorId,
      untilTime: performance.now() / 1000 + durationSeconds,
      angle,
    };
  }

  public toggleInspectMode(
    actorId: string,
    durationSeconds: number = 6.0,
    angle: InspectCameraAngle = 'front'
  ): boolean {
    if (this.isInspecting() && this.inspectTarget?.actorId === actorId) {
      this.clearInspectMode();
      return false; // Turned off
    } else {
      this.setInspectMode(actorId, durationSeconds, angle);
      return true; // Turned on
    }
  }

  public setInspectAngle(angle: InspectCameraAngle): void {
    if (this.inspectTarget) {
      this.inspectTarget.angle = angle;
      this.inspectTarget.untilTime = performance.now() / 1000 + 8.0; // extend duration
    }
  }

  public clearInspectMode(): void {
    this.inspectTarget = null;
  }

  public isInspecting(): boolean {
    return this.inspectTarget !== null && performance.now() / 1000 < this.inspectTarget.untilTime;
  }

  public getInspectTargetId(): string | null {
    if (this.inspectTarget && performance.now() / 1000 < this.inspectTarget.untilTime) {
      return this.inspectTarget.actorId;
    }
    return null;
  }

  public getInspectAngle(): InspectCameraAngle {
    return this.inspectTarget?.angle || 'front';
  }

  public update(
    scene: MasterSceneConfig,
    currentTime: number,
    actorStates: Map<string, ActorVisualState>,
    delta: number
  ): void {
    // Check if user is inspecting face close-up
    if (this.inspectTarget) {
      if (performance.now() / 1000 < this.inspectTarget.untilTime) {
        const inspectTrack: CameraTrack = {
          start: 0,
          end: 100,
          shot_type: 'face_close_up',
          follow_target: this.inspectTarget.actorId,
          fov: 32,
        };
        const pose = CameraFraming.evaluatePose(
          inspectTrack,
          currentTime,
          actorStates,
          this.inspectTarget.angle
        );
        this.applyPose(pose, delta * 6);
        return;
      } else {
        this.inspectTarget = null;
      }
    }

    // Evaluate current CameraTrack from scene
    const tracks = scene.camera_tracks || [];
    const activeTrack =
      tracks.find((t) => currentTime >= t.start && currentTime <= t.end) || tracks[0];

    if (activeTrack) {
      const targetPose = CameraFraming.evaluatePose(activeTrack, currentTime, actorStates);
      // Use higher speed (6.0) for face close-ups, lower (4.0) for standard tracking
      this.applyPose(targetPose, 4.0, delta);
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

