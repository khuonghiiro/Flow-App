import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { SmartSocketRegistry } from './SmartSocketRegistry';

export class ChairInteraction {
  public static executeSitting(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    chairSocketId: string,
    progress: number, // 0.0 (start) to 1.0 (end of track)
    currentTime: number = 0
  ): void {
    const socket = SmartSocketRegistry.getSocket(chairSocketId);
    if (!socket || socket.type !== 'chair') return;

    const startPos = new THREE.Vector3(...avatar.config.spawn_point);
    const entry = new THREE.Vector3(...socket.entryPosition);
    const target = new THREE.Vector3(...socket.targetPosition);
    const targetRotY = socket.targetRotationY ?? 0;

    if (progress < 0.22) {
      // Phase 1 (0% - 22%): Walk from spawn to right in front of seat cushion
      const p = progress / 0.22;
      avatar.rootObject.position.lerpVectors(startPos, entry, p);

      const dir = entry.clone().sub(startPos);
      if (dir.lengthSq() > 0.01) {
        avatar.rootObject.rotation.y = Math.atan2(dir.x, dir.z);
      }
      animator.setAction('walk');
      animator.update(currentTime);
    } else if (progress < 0.32) {
      // Phase 2 (22% - 32%): Turn around in place to face forward (+Z)
      const p = (progress - 0.22) / 0.10;
      avatar.rootObject.position.copy(entry);
      avatar.rootObject.rotation.y = THREE.MathUtils.lerp(
        avatar.rootObject.rotation.y,
        targetRotY,
        p
      );
      animator.setAction('idle');
      animator.update(currentTime);
    } else if (progress < 0.44) {
      // Phase 3 (32% - 44%): Bend knees and lower body into seat smoothly in place
      const p = (progress - 0.32) / 0.12;
      avatar.rootObject.position.lerpVectors(entry, target, p);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
      animator.update(currentTime, p);
    } else {
      // Phase 4 (44% - 100%): Fully seated on the chair with relaxed posture
      avatar.rootObject.position.copy(target);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
      animator.update(currentTime, 1.0);
    }
  }
}
