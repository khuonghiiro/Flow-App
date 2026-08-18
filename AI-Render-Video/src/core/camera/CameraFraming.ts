import * as THREE from 'three';
import { CameraTrack, Vec3Tuple } from '../../types/scene';

export type InspectCameraAngle = 'front' | 'three_quarter' | 'side' | 'low_angle';

export interface CameraPose {
  position: THREE.Vector3;
  target: THREE.Vector3;
  fov: number;
}

export interface ActorVisualState {
  position: THREE.Vector3;
  headPosition: THREE.Vector3;
  rotationY: number;
}

export class CameraFraming {
  public static evaluatePose(
    track: CameraTrack,
    time: number,
    actorStates: Map<string, ActorVisualState>,
    inspectAngle: InspectCameraAngle = 'front'
  ): CameraPose {
    const progress = THREE.MathUtils.clamp(
      (time - track.start) / Math.max(0.01, track.end - track.start),
      0,
      1
    );
    const pos = new THREE.Vector3();
    const target = new THREE.Vector3(0, 1.2, 0);
    const fov = track.fov || 50;

    switch (track.shot_type) {
      case 'cinematic_dolly': {
        const from = track.from ? new THREE.Vector3(...track.from) : new THREE.Vector3(-5, 3, 10);
        const to = track.to ? new THREE.Vector3(...track.to) : new THREE.Vector3(-1, 1.8, 4);
        pos.lerpVectors(from, to, progress);

        if (typeof track.look_at === 'string') {
          const actorId = track.look_at.replace('.head', '');
          const actorState = actorStates.get(actorId);
          if (actorState) {
            target.copy(actorState.headPosition);
          }
        } else if (Array.isArray(track.look_at)) {
          target.set(...(track.look_at as Vec3Tuple));
        }
        break;
      }

      case 'combat_action_cam': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);
        const actorPos = actorState ? actorState.position : new THREE.Vector3(0, 0, 0);
        const dist = track.distance || 3.5;
        const height = track.height || 1.6;

        const angle = Math.PI * 0.2 + Math.sin(time * 0.5) * 0.2;
        pos.set(
          actorPos.x + Math.sin(angle) * dist,
          actorPos.y + height,
          actorPos.z + Math.cos(angle) * dist
        );
        target.copy(actorPos);
        target.y += 1.2;
        break;
      }

      case 'face_close_up': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);

        if (actorState) {
          const headPos = actorState.headPosition;
          const facingY = actorState.rotationY;

          let angleOffset = 0; // 'front'
          let dist = 0.95;
          let heightOffset = 0.02;

          if (inspectAngle === 'three_quarter') {
            angleOffset = Math.PI * 0.22; // 40 degrees
            dist = 1.05;
            heightOffset = 0.04;
          } else if (inspectAngle === 'side') {
            angleOffset = Math.PI * 0.5; // 90 degrees
            dist = 0.95;
            heightOffset = 0.0;
          } else if (inspectAngle === 'low_angle') {
            angleOffset = Math.PI * 0.08;
            dist = 1.15;
            heightOffset = -0.28;
          }

          // Compute camera position relative to the avatar's facing angle
          const totalAngle = facingY + angleOffset;
          pos.set(
            headPos.x + Math.sin(totalAngle) * dist,
            headPos.y + heightOffset,
            headPos.z + Math.cos(totalAngle) * dist
          );

          // Focus right at the eye/nose center with slight headroom
          target.copy(headPos);
        } else {
          pos.set(0, 1.6, 1.2);
          target.set(0, 1.5, 0);
        }
        break;
      }

      case 'wide_overview':
      default: {
        pos.set(0, 8, 14);
        target.set(0, 1, 0);
        break;
      }
    }

    return { position: pos, target, fov: track.shot_type === 'face_close_up' ? 32 : fov };
  }
}
