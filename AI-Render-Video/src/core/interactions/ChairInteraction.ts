import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { SmartSocketRegistry } from './SmartSocketRegistry';

export class ChairInteraction {
  public static executeSitting(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    chairSocketId: string,
    progress: number // 0.0 (approaching) to 1.0 (seated)
  ): void {
    const socket = SmartSocketRegistry.getSocket(chairSocketId);
    if (!socket || socket.type !== 'chair') return;

    const entry = new THREE.Vector3(...socket.entryPosition);
    const target = new THREE.Vector3(...socket.targetPosition);
    const targetRotY = socket.targetRotationY ?? Math.PI;

    if (progress < 0.5) {
      // Walk towards entry point and turn around
      const p = progress / 0.5;
      avatar.rootObject.position.lerpVectors(entry, target, p * 0.5);
      avatar.rootObject.rotation.y = THREE.MathUtils.lerp(0, targetRotY, p);
      animator.setAction('walk');
    } else {
      // Dock into chair & switch to sitting pose
      const p = (progress - 0.5) / 0.5;
      avatar.rootObject.position.lerpVectors(
        new THREE.Vector3().lerpVectors(entry, target, 0.5),
        target,
        p
      );
      avatar.rootObject.rotation.y = targetRotY;
      animator.setAction('sit');
    }
  }
}
