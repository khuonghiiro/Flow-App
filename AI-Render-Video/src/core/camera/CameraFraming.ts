import * as THREE from 'three';
import { CameraTrack, Vec3Tuple } from '../../types/scene';

export type InspectCameraAngle = 'front' | 'three_quarter' | 'side' | 'low_angle';

export interface CameraPose {
  position: THREE.Vector3;
  target: THREE.Vector3;
  fov: number;
  roll?: number; // Dutch angle (radians)
}

export interface ActorVisualState {
  position: THREE.Vector3;
  headPosition: THREE.Vector3;
  rotationY: number;
}

export class CameraFraming {
  /**
   * Evaluate camera pose based on shot type and timeline progress
   */
  public static evaluatePose(
    track: CameraTrack,
    time: number,
    actorStates: Map<string, ActorVisualState>,
    inspectAngle: InspectCameraAngle = 'front'
  ): CameraPose {
    const rawProgress = (time - track.start) / Math.max(0.01, track.end - track.start);
    const progress = THREE.MathUtils.clamp(rawProgress, 0, 1);

    const pos = new THREE.Vector3();
    const target = new THREE.Vector3(0, 1.2, 0);
    let fov = track.fov || 50;
    let roll = track.dutch_angle ? THREE.MathUtils.degToRad(track.dutch_angle) : 0;

    switch (track.shot_type) {
      // 1. Cinematic Dolly
      case 'cinematic_dolly': {
        const from = track.from ? new THREE.Vector3(...track.from) : new THREE.Vector3(-5, 3, 10);
        const to = track.to ? new THREE.Vector3(...track.to) : new THREE.Vector3(-1, 1.8, 4);
        pos.lerpVectors(from, to, progress);

        if (typeof track.look_at === 'string') {
          const actorId = track.look_at.replace('.head', '');
          const actorState = actorStates.get(actorId);
          if (actorState) target.copy(actorState.headPosition);
        } else if (Array.isArray(track.look_at)) {
          target.set(...(track.look_at as Vec3Tuple));
        }
        break;
      }

      // 2. Combat Action Cam
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

      // 3. Face Close-up
      case 'face_close_up': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);

        if (actorState) {
          const headPos = actorState.headPosition;
          const facingY = actorState.rotationY;

          let angleOffset = 0;
          let dist = 0.95;
          let heightOffset = 0.02;

          if (inspectAngle === 'three_quarter') {
            angleOffset = Math.PI * 0.22;
            dist = 1.05;
            heightOffset = 0.04;
          } else if (inspectAngle === 'side') {
            angleOffset = Math.PI * 0.5;
            dist = 0.95;
            heightOffset = 0.0;
          } else if (inspectAngle === 'low_angle') {
            angleOffset = Math.PI * 0.08;
            dist = 1.15;
            heightOffset = -0.28;
          }

          const totalAngle = facingY + angleOffset;
          pos.set(
            headPos.x + Math.sin(totalAngle) * dist,
            headPos.y + heightOffset,
            headPos.z + Math.cos(totalAngle) * dist
          );
          target.copy(headPos);
        } else {
          pos.set(0, 1.6, 1.2);
          target.set(0, 1.5, 0);
        }
        fov = 32;
        break;
      }

      // 4. Hero Low Angle (Dramatic upward view)
      case 'hero_low_angle': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);
        const actorPos = actorState ? actorState.position : new THREE.Vector3(0, 0, 0);
        const dist = track.distance || 2.2;

        const facingY = actorState ? actorState.rotationY : 0;
        pos.set(
          actorPos.x + Math.sin(facingY + 0.2) * dist,
          actorPos.y + 0.35, // Knee level
          actorPos.z + Math.cos(facingY + 0.2) * dist
        );
        target.copy(actorPos);
        target.y += 1.4; // Look up at chest/head
        fov = track.fov || 58; // Wider FOV for heroic perspective distortion
        break;
      }

      // 5. Crash Zoom (Anime sudden shock zoom)
      case 'crash_zoom': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);
        const headPos = actorState ? actorState.headPosition : new THREE.Vector3(0, 1.5, 0);

        // Exponential snap in the first 0.35 fraction of track
        const zoomProgress = Math.min(1, Math.pow(progress * 2.8, 3));
        const startDist = track.distance || 4.0;
        const endDist = 0.85;
        const currentDist = THREE.MathUtils.lerp(startDist, endDist, zoomProgress);

        const facingY = actorState ? actorState.rotationY : 0;
        pos.set(
          headPos.x + Math.sin(facingY) * currentDist,
          headPos.y + 0.05,
          headPos.z + Math.cos(facingY) * currentDist
        );
        target.copy(headPos);

        const startFov = track.fov || 55;
        const endFov = track.fov_end || 26;
        fov = THREE.MathUtils.lerp(startFov, endFov, zoomProgress);
        break;
      }

      // 6. Over-the-Shoulder Dialogue (OTS)
      case 'over_the_shoulder': {
        const speakerA = actorStates.get(track.follow_target || '');
        const speakerB = actorStates.get(track.second_target || track.look_at as string || '');

        if (speakerA && speakerB) {
          const dir = new THREE.Vector3().subVectors(speakerB.position, speakerA.position).normalize();
          const right = new THREE.Vector3(-dir.z, 0, dir.x);

          // Position camera just behind speaker A's shoulder
          pos.copy(speakerA.headPosition)
            .addScaledVector(dir, -0.65) // Behind
            .addScaledVector(right, 0.42) // Over right shoulder
            .add(new THREE.Vector3(0, 0.08, 0));

          target.copy(speakerB.headPosition);
        } else if (speakerA) {
          pos.set(speakerA.position.x + 0.4, speakerA.position.y + 1.5, speakerA.position.z - 0.7);
          target.set(speakerA.position.x, speakerA.position.y + 1.4, speakerA.position.z + 2.0);
        }
        fov = track.fov || 38;
        break;
      }

      // 7. Bullet-Time Orbit (Slow-mo 360/180 spin)
      case 'bullet_time_orbit': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);
        const center = actorState ? actorState.position : new THREE.Vector3(0, 0, 0);
        const dist = track.distance || 3.2;
        const height = track.height || 1.5;

        const startAngle = 0;
        const endAngle = Math.PI * 1.5; // 270 degree sweep
        const angle = THREE.MathUtils.lerp(startAngle, endAngle, progress);

        pos.set(
          center.x + Math.sin(angle) * dist,
          center.y + height + Math.sin(progress * Math.PI) * 0.4,
          center.z + Math.cos(angle) * dist
        );
        target.copy(center);
        target.y += 1.1;
        break;
      }

      // 8. Dutch Tilt Cam (Dramatic combat angle)
      case 'dutch_tilt_cam': {
        const from = track.from ? new THREE.Vector3(...track.from) : new THREE.Vector3(-3, 1.8, 4);
        const to = track.to ? new THREE.Vector3(...track.to) : new THREE.Vector3(2, 2.2, 3);
        pos.lerpVectors(from, to, progress);

        if (typeof track.look_at === 'string') {
          const actorState = actorStates.get(track.look_at.replace('.head', ''));
          if (actorState) target.copy(actorState.headPosition);
        } else {
          target.set(0, 1.2, 0);
        }
        roll = THREE.MathUtils.degToRad(track.dutch_angle || 14);
        break;
      }

      // 9. Tracking Lead (Camera leads ahead of moving character)
      case 'tracking_lead': {
        const targetId = track.follow_target || 'actor_warrior';
        const actorState = actorStates.get(targetId);
        if (actorState) {
          const facingY = actorState.rotationY;
          const dist = track.distance || 3.0;

          // Camera floats in front of character as they advance
          pos.set(
            actorState.position.x + Math.sin(facingY) * dist,
            actorState.position.y + (track.height || 1.3),
            actorState.position.z + Math.cos(facingY) * dist
          );
          target.copy(actorState.headPosition);
        }
        fov = track.fov || 52;
        break;
      }

      // 10. Action Whip Pan
      case 'action_whip_pan': {
        const targetA = actorStates.get(track.follow_target || '');
        const targetB = actorStates.get(track.second_target || '');
        if (targetA && targetB) {
          // Rapid pan with ease-in-out snap
          const smoothP = progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.pow(-2 * progress + 2, 3) / 2;
          const midPos = new THREE.Vector3().lerpVectors(targetA.position, targetB.position, 0.5);
          pos.set(midPos.x, midPos.y + (track.height || 1.8), midPos.z + (track.distance || 4.5));
          target.lerpVectors(targetA.headPosition, targetB.headPosition, smoothP);
        }
        break;
      }

      // 11. Bird's Eye View
      case 'birds_eye_view':
      case 'wide_overview':
      default: {
        pos.set(0, track.height || 10, track.distance || 14);
        target.set(0, 0.8, 0);
        break;
      }
    }

    return { position: pos, target, fov, roll };
  }
}
