import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { SmartSocketRegistry } from './SmartSocketRegistry';

export class ClimbingInteraction {
  public static executeClimb(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    treeSocketId: string,
    progress: number, // 0.0 (start) to 1.0 (end of track)
    currentTime: number = 0
  ): void {
    const socket = SmartSocketRegistry.getSocket(treeSocketId);
    if (!socket || socket.type !== 'tree') return;

    const startPos = new THREE.Vector3(...avatar.config.spawn_point);
    const base = new THREE.Vector3(...socket.entryPosition);
    const branch = new THREE.Vector3(...socket.targetPosition);
    const targetRotY = socket.targetRotationY ?? 0;
    const trunkFacingAngle = Math.PI; // Face toward the tree trunk while climbing

    if (progress < 0.2) {
      // Phase 1 (0% - 20%): Walk towards tree trunk base
      const p = progress / 0.2;
      avatar.rootObject.position.lerpVectors(startPos, base, p);
      const dir = base.clone().sub(startPos);
      if (dir.lengthSq() > 0.01) {
        avatar.rootObject.rotation.y = Math.atan2(dir.x, dir.z);
      }
      animator.setAction('walk');
      animator.update(currentTime);
    } else if (progress < 0.55) {
      // Phase 2 (20% - 55%): Climb up the tree trunk vertically with realistic gripping cadence
      const p = (progress - 0.2) / 0.35;
      avatar.rootObject.position.lerpVectors(base, branch, p);

      // Face the trunk while climbing, then rotate to face out when near the branch
      if (p < 0.85) {
        avatar.rootObject.rotation.y = trunkFacingAngle;
      } else {
        const turnProgress = (p - 0.85) / 0.15;
        avatar.rootObject.rotation.y = THREE.MathUtils.lerp(
          trunkFacingAngle,
          targetRotY,
          turnProgress
        );
      }

      animator.setAction('climb');
      animator.update(currentTime);
    } else if (progress < 0.65) {
      // Phase 3 (55% - 65%): Smoothly settle onto the branch
      const p = (progress - 0.55) / 0.10;
      avatar.rootObject.position.copy(branch);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
      animator.update(currentTime, p);
    } else {
      // Phase 4 (65% - 100%): Seated comfortably on the branch
      avatar.rootObject.position.copy(branch);
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
      animator.update(currentTime, 1.0);
    }
  }
}
