import * as THREE from 'three';
import { CameraTrack, Vec3Tuple } from '../../types/scene';

export interface CameraPose {
  position: THREE.Vector3;
  target: THREE.Vector3;
  fov: number;
}

export class CameraFraming {
  public static evaluatePose(
    track: CameraTrack,
    time: number,
    actorPositions: Map<string, THREE.Vector3>
  ): CameraPose {
    const progress = THREE.MathUtils.clamp((time - track.start) / Math.max(0.01, track.end - track.start), 0, 1);
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
          const actorPos = actorPositions.get(actorId);
          if (actorPos) {
            target.copy(actorPos);
            target.y += 1.4; // head level
          }
        } else if (Array.isArray(track.look_at)) {
          target.set(...(track.look_at as Vec3Tuple));
        }
        break;
      }

      case 'combat_action_cam': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorPos = actorPositions.get(targetId) || new THREE.Vector3(0, 0, 0);
        const dist = track.distance || 3.5;
        const height = track.height || 1.6;

        // Orbit slightly around the fight
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
        const actorPos = actorPositions.get(targetId) || new THREE.Vector3(0, 0, 0);
        pos.set(actorPos.x, actorPos.y + 1.45, actorPos.z + 1.1);
        target.set(actorPos.x, actorPos.y + 1.4, actorPos.z);
        break;
      }

      case 'wide_overview':
      default: {
        pos.set(0, 8, 14);
        target.set(0, 1, 0);
        break;
      }
    }

    return { position: pos, target, fov };
  }
}
