import * as THREE from 'three';
import { CameraTrack, MasterSceneConfig } from '../../types/scene';
import { CameraFraming, CameraPose } from './CameraFraming';

export class CameraDirector {
  private camera: THREE.PerspectiveCamera;
  private currentPose: CameraPose = {
    position: new THREE.Vector3(0, 3, 10),
    target: new THREE.Vector3(0, 1.2, 0),
    fov: 50,
  };
  private inspectTarget: { actorId: string; untilTime: number } | null = null;

  constructor(camera: THREE.PerspectiveCamera) {
    this.camera = camera;
  }

  public setInspectMode(actorId: string, durationSeconds: number = 5.0): void {
    this.inspectTarget = {
      actorId,
      untilTime: performance.now() / 1000 + durationSeconds,
    };
  }

  public clearInspectMode(): void {
    this.inspectTarget = null;
  }

  public update(
    scene: MasterSceneConfig,
    currentTime: number,
    actorPositions: Map<string, THREE.Vector3>,
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
          fov: 35,
        };
        const pose = CameraFraming.evaluatePose(inspectTrack, currentTime, actorPositions);
        this.applyPose(pose, delta * 5);
        return;
      } else {
        this.inspectTarget = null;
      }
    }

    // Evaluate current CameraTrack from scene
    const tracks = scene.camera_tracks || [];
    const activeTrack = tracks.find((t) => currentTime >= t.start && currentTime <= t.end) || tracks[0];

    if (activeTrack) {
      const targetPose = CameraFraming.evaluatePose(activeTrack, currentTime, actorPositions);
      this.applyPose(targetPose, delta * 4);
    }
  }

  private applyPose(targetPose: CameraPose, lerpFactor: number): void {
    this.currentPose.position.lerp(targetPose.position, THREE.MathUtils.clamp(lerpFactor, 0, 1));
    this.currentPose.target.lerp(targetPose.target, THREE.MathUtils.clamp(lerpFactor, 0, 1));
    this.currentPose.fov = THREE.MathUtils.lerp(this.currentPose.fov, targetPose.fov, THREE.MathUtils.clamp(lerpFactor, 0, 1));

    this.camera.position.copy(this.currentPose.position);
    this.camera.lookAt(this.currentPose.target);
    if (Math.abs(this.camera.fov - this.currentPose.fov) > 0.1) {
      this.camera.fov = this.currentPose.fov;
      this.camera.updateProjectionMatrix();
    }
  }
}
