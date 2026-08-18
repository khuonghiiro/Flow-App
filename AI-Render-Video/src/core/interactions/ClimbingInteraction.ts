import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { SmartSocketRegistry } from './SmartSocketRegistry';

export class ClimbingInteraction {
  public static executeClimb(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    treeSocketId: string,
    progress: number // 0.0 (ground) to 1.0 (on branch)
  ): void {
    const socket = SmartSocketRegistry.getSocket(treeSocketId);
    if (!socket || socket.type !== 'tree') return;

    const base = new THREE.Vector3(...socket.entryPosition);
    const branch = new THREE.Vector3(...socket.targetPosition);

    if (progress < 0.8) {
      // Climbing up the trunk
      const p = progress / 0.8;
      avatar.rootObject.position.lerpVectors(base, branch, p);
      animator.setAction('climb');
    } else {
      // Arrived on branch, resting in seated pose
      avatar.rootObject.position.copy(branch);
      animator.setAction('sit');
    }
  }
}
