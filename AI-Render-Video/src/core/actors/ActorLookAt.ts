import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';

export class ActorLookAt {
  private avatar: VRMAvatar;
  private targetPosition: THREE.Vector3 | null = null;
  private currentYaw: number = 0;
  private currentPitch: number = 0;

  constructor(avatar: VRMAvatar) {
    this.avatar = avatar;
  }

  public setLookAtTarget(target: THREE.Vector3 | null): void {
    this.targetPosition = target ? target.clone() : null;
  }

  public update(delta: number): void {
    if (!this.targetPosition) {
      // Return head smoothly to center
      this.currentYaw = THREE.MathUtils.lerp(this.currentYaw, 0, delta * 4);
      this.currentPitch = THREE.MathUtils.lerp(this.currentPitch, 0, delta * 4);
      this.avatar.headBone.rotation.y = this.currentYaw;
      this.avatar.headBone.rotation.x = this.currentPitch;
      return;
    }

    const headWorldPos = new THREE.Vector3();
    this.avatar.headBone.getWorldPosition(headWorldPos);

    const dir = this.targetPosition.clone().sub(headWorldPos).normalize();
    // Convert world direction to actor local space
    const localDir = dir.applyQuaternion(this.avatar.rootObject.quaternion.clone().invert());

    const targetYaw = Math.atan2(localDir.x, localDir.z);
    const targetPitch = -Math.asin(Math.max(-1, Math.min(1, localDir.y)));

    // Clamp angles to natural neck limits (+- 60 deg yaw, +- 35 deg pitch)
    const clampedYaw = THREE.MathUtils.clamp(targetYaw, -Math.PI / 3, Math.PI / 3);
    const clampedPitch = THREE.MathUtils.clamp(targetPitch, -Math.PI / 5, Math.PI / 5);

    this.currentYaw = THREE.MathUtils.lerp(this.currentYaw, clampedYaw, delta * 6);
    this.currentPitch = THREE.MathUtils.lerp(this.currentPitch, clampedPitch, delta * 6);

    this.avatar.headBone.rotation.y = this.currentYaw;
    this.avatar.headBone.rotation.x = this.currentPitch;
  }
}
