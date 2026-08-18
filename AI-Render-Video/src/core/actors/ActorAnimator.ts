import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';

export type AnimationAction =
  | 'idle'
  | 'walk'
  | 'run'
  | 'talk_gesture'
  | 'heavy_slash_combo'
  | 'fast_slash'
  | 'magic_blast'
  | 'punch_kick'
  | 'fly_back_knockdown'
  | 'stagger_back'
  | 'block_defend'
  | 'dodge'
  | 'sit'
  | 'climb';

export class ActorAnimator {
  private avatar: VRMAvatar;
  private currentAction: AnimationAction = 'idle';

  constructor(avatar: VRMAvatar) {
    this.avatar = avatar;
  }

  public setAction(action: AnimationAction): void {
    this.currentAction = action;
  }

  public update(time: number, actionProgress: number = 0): void {
    const { leftArm, rightArm, leftLeg, rightLeg, spineBone, headBone } = this.avatar;

    // Reset default rotations & positions
    leftArm.rotation.set(0, 0, 0);
    rightArm.rotation.set(0, 0, 0);
    leftLeg.rotation.set(0, 0, 0);
    rightLeg.rotation.set(0, 0, 0);
    spineBone.rotation.set(0, 0, 0);
    headBone.rotation.set(0, 0, 0);

    leftLeg.position.set(-0.16, 0.85, 0);
    rightLeg.position.set(0.16, 0.85, 0);
    spineBone.position.set(0, 0.9, 0);

    switch (this.currentAction) {
      case 'walk': {
        const speed = 4;
        const swing = Math.sin(time * speed);
        leftLeg.rotation.x = swing * 0.6;
        rightLeg.rotation.x = -swing * 0.6;
        leftArm.rotation.x = -swing * 0.5;
        rightArm.rotation.x = swing * 0.5;
        spineBone.position.y = 0.9 + Math.abs(Math.sin(time * speed)) * 0.05;
        break;
      }

      case 'run': {
        const speed = 7;
        const swing = Math.sin(time * speed);
        leftLeg.rotation.x = swing * 0.9;
        rightLeg.rotation.x = -swing * 0.9;
        leftArm.rotation.x = -swing * 0.8;
        rightArm.rotation.x = swing * 0.8;
        spineBone.rotation.x = 0.2; // Leaning forward
        spineBone.position.y = 0.9 + Math.abs(Math.sin(time * speed)) * 0.08;
        break;
      }

      case 'talk_gesture': {
        // Natural talking gestures
        const t = time * 2;
        rightArm.rotation.x = -0.6 + Math.sin(t) * 0.25;
        rightArm.rotation.z = -0.3 + Math.cos(t * 0.7) * 0.15;
        leftArm.rotation.x = -0.2 + Math.sin(t * 0.8) * 0.15;
        headBone.rotation.y = Math.sin(t * 0.5) * 0.15;
        headBone.rotation.x = Math.sin(t * 0.9) * 0.08;
        break;
      }

      case 'heavy_slash_combo':
      case 'fast_slash': {
        // Attack animation: windup -> slash down -> follow through
        if (actionProgress < 0.35) {
          const p = actionProgress / 0.35;
          rightArm.rotation.x = -Math.PI * 0.85 * p;
          rightArm.rotation.z = -0.4 * p;
          spineBone.rotation.y = -0.5 * p;
          spineBone.rotation.x = -0.15 * p;
        } else if (actionProgress < 0.65) {
          const p = (actionProgress - 0.35) / 0.3;
          rightArm.rotation.x = -Math.PI * 0.85 + (Math.PI * 0.85 + 0.6) * p;
          rightArm.rotation.z = -0.4 + 0.6 * p;
          spineBone.rotation.y = -0.5 + 0.9 * p;
          spineBone.rotation.x = -0.15 + 0.45 * p;
          leftArm.rotation.x = 0.5 * p;
        } else {
          const p = (actionProgress - 0.65) / 0.35;
          rightArm.rotation.x = 0.6 * (1 - p);
          spineBone.rotation.y = 0.4 * (1 - p);
          spineBone.rotation.x = 0.3 * (1 - p);
        }
        break;
      }

      case 'fly_back_knockdown': {
        // Hit reaction: fly back & fall down
        const p = Math.min(1, Math.max(0, actionProgress));
        spineBone.rotation.x = -Math.PI * 0.4 * Math.min(1, p * 1.6);
        spineBone.position.y = 0.9 - 0.5 * Math.min(1, p * 1.6);
        headBone.rotation.x = -0.6;
        leftArm.rotation.x = -1.5;
        rightArm.rotation.x = -1.5;
        leftLeg.position.y = 0.85 - 0.45 * Math.min(1, p * 1.6);
        rightLeg.position.y = 0.85 - 0.45 * Math.min(1, p * 1.6);
        leftLeg.rotation.x = 0.8 * Math.min(1, p * 1.6);
        rightLeg.rotation.x = 0.6 * Math.min(1, p * 1.6);
        break;
      }

      case 'stagger_back': {
        const p = Math.min(1, Math.max(0, actionProgress));
        spineBone.rotation.x = -0.35 * (1 - p);
        headBone.rotation.x = -0.3 * (1 - p);
        rightArm.rotation.x = -0.8;
        leftArm.rotation.x = -0.8;
        break;
      }

      case 'sit': {
        // Sitting pose: 90-degree bent legs, torso lowered onto seat
        leftLeg.position.set(-0.16, 0.5, 0);
        rightLeg.position.set(0.16, 0.5, 0);
        spineBone.position.set(0, 0.5, 0);

        leftLeg.rotation.x = -Math.PI / 2;
        rightLeg.rotation.x = -Math.PI / 2;
        leftArm.rotation.x = -Math.PI / 4;
        rightArm.rotation.x = -Math.PI / 4;
        break;
      }

      case 'climb': {
        const climbTime = time * 4;
        const swing = Math.sin(climbTime);
        leftArm.rotation.x = -Math.PI * 0.85 + swing * 0.4;
        rightArm.rotation.x = -Math.PI * 0.85 - swing * 0.4;
        leftLeg.rotation.x = -0.5 + swing * 0.35;
        rightLeg.rotation.x = -0.5 - swing * 0.35;
        break;
      }

      case 'idle':
      default: {
        // Gentle breathing & resting posture
        const breath = Math.sin(time * 1.5);
        spineBone.position.y = 0.9 + breath * 0.015;
        headBone.rotation.x = breath * 0.02;
        leftArm.rotation.z = 0.08 + breath * 0.02;
        rightArm.rotation.z = -0.08 - breath * 0.02;
        break;
      }
    }
  }
}
