import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { SmartSocketRegistry } from './SmartSocketRegistry';

export class ClimbingInteraction {
  public static executeClimb(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    treeSocketId: string,
    progress: number // 0.0 (start) to 1.0 (end of track)
  ): void {
    const socket = SmartSocketRegistry.getSocket(treeSocketId);
    if (!socket || socket.type !== 'tree') return;

    const startPos = new THREE.Vector3(...avatar.config.spawn_point);
    const base = new THREE.Vector3(...socket.entryPosition);
    const branch = new THREE.Vector3(...socket.targetPosition);
    const targetRotY = socket.targetRotationY ?? 0;

    if (progress < 0.2) {
      // Phase 1 (0% - 20%): Walk towards tree base
      const p = progress / 0.2;
      avatar.rootObject.position.lerpVectors(startPos, base, p);
      const dir = base.clone().sub(startPos);
      if (dir.lengthSq() > 0.01) {
        avatar.rootObject.rotation.y = Math.atan2(dir.x, dir.z);
      }
      animator.setAction('walk');
    } else if (progress < 0.55) {
      // Phase 2 (20% - 55%): Climb up the tree trunk vertically
      const p = (progress - 0.2) / 0.35;
      avatar.rootObject.position.lerpVectors(base, branch, p);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('climb');
    } else {
      // Phase 3 (55% - 100%): Seated comfortably on the high branch
      avatar.rootObject.position.copy(branch);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
    }
  }
}
