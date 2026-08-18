import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { SmartSocketRegistry } from './SmartSocketRegistry';

export class ChairInteraction {
  public static executeSitting(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    chairSocketId: string,
    progress: number // 0.0 (start) to 1.0 (end of track)
  ): void {
    const socket = SmartSocketRegistry.getSocket(chairSocketId);
    if (!socket || socket.type !== 'chair') return;

    const startPos = new THREE.Vector3(...avatar.config.spawn_point);
    const entry = new THREE.Vector3(...socket.entryPosition);
    const target = new THREE.Vector3(...socket.targetPosition);
    const targetRotY = socket.targetRotationY ?? 0;

    if (progress < 0.25) {
      // Phase 1 (0% - 25%): Walk from spawn point to in front of chair
      const p = progress / 0.25;
      avatar.rootObject.position.lerpVectors(startPos, entry, p);
      
      const dir = entry.clone().sub(startPos);
      if (dir.lengthSq() > 0.01) {
        avatar.rootObject.rotation.y = Math.atan2(dir.x, dir.z);
      }
      animator.setAction('walk');
    } else if (progress < 0.35) {
      // Phase 2 (25% - 35%): Rotate 180 degrees to face forward with back to seat
      const p = (progress - 0.25) / 0.10;
      avatar.rootObject.position.copy(entry);
      avatar.rootObject.rotation.y = THREE.MathUtils.lerp(avatar.rootObject.rotation.y, targetRotY, p);
      animator.setAction('idle');
    } else if (progress < 0.45) {
      // Phase 3 (35% - 45%): Step back onto the chair seat
      const p = (progress - 0.35) / 0.10;
      avatar.rootObject.position.lerpVectors(entry, target, p);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
    } else {
      // Phase 4 (45% - 100%): Fully seated on the chair
      avatar.rootObject.position.copy(target);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
    }
  }
}
