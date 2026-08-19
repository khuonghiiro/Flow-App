import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';

export type AnimationAction =
  // Basic Movement
  | 'idle'
  | 'walk'
  | 'run'
  | 'talk_gesture'
  | 'sit'
  | 'climb'
  // Advanced Movement
  | 'fly_to'
  | 'dash_to'
  | 'teleport'
  // Combat
  | 'heavy_slash_combo'
  | 'fast_slash'
  | 'magic_blast'
  | 'punch_kick'
  | 'fly_back_knockdown'
  | 'stagger_back'
  | 'block_defend'
  | 'dodge'
  // Xianxia Poses
  | 'arms_crossed'
  | 'hands_behind_back'
  | 'kneel'
  | 'bow'
  | 'meditate'
  | 'fist_salute'
  | 'finger_spell'
  | 'power_charge'
  | 'flying_stance'
  // Object Interaction
  | 'pickup_right'
  | 'carry_two_hands'
  | 'drink'
  | 'pour'
  | 'dig'
  | 'water_plants'
  | 'plant_seed'
  | 'harvest'
  | 'wave'
  | 'dance'
  | 'throw';

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
    const {
      leftArm,
      leftElbow,
      rightArm,
      rightElbow,
      leftLeg,
      leftKnee,
      rightLeg,
      rightKnee,
      spineBone,
      headBone,
    } = this.avatar;

    // Reset default rotations & positions
    leftArm.rotation.set(0, 0, 0);
    leftElbow.rotation.set(0, 0, 0);
    rightArm.rotation.set(0, 0, 0);
    rightElbow.rotation.set(0, 0, 0);

    leftLeg.rotation.set(0, 0, 0);
    leftKnee.rotation.set(0, 0, 0);
    rightLeg.rotation.set(0, 0, 0);
    rightKnee.rotation.set(0, 0, 0);

    spineBone.rotation.set(0, 0, 0);
    headBone.rotation.set(0, 0, 0);

    leftLeg.position.set(-0.16, 0.85, 0);
    rightLeg.position.set(0.16, 0.85, 0);
    spineBone.position.set(0, 0.9, 0);

    switch (this.currentAction) {
      case 'walk': {
        const speed = 4.2;
        const swing = Math.sin(time * speed);

        // Natural hip & knee articulation during walking stride
        leftLeg.rotation.x = swing * 0.55;
        leftKnee.rotation.x = Math.max(0, -swing * 0.65);

        rightLeg.rotation.x = -swing * 0.55;
        rightKnee.rotation.x = Math.max(0, swing * 0.65);

        // Natural arm counter-swing with slight elbow bend
        leftArm.rotation.x = -swing * 0.45;
        leftArm.rotation.z = 0.08;
        leftElbow.rotation.x = -0.2 - Math.abs(swing) * 0.2;

        rightArm.rotation.x = swing * 0.45;
        rightArm.rotation.z = -0.08;
        rightElbow.rotation.x = -0.2 - Math.abs(swing) * 0.2;

        spineBone.position.y = 0.9 + Math.abs(Math.sin(time * speed)) * 0.04;
        spineBone.rotation.y = -swing * 0.08;
        break;
      }

      case 'run': {
        const speed = 7.5;
        const swing = Math.sin(time * speed);

        leftLeg.rotation.x = swing * 0.85;
        leftKnee.rotation.x = Math.max(0, -swing * 1.1);

        rightLeg.rotation.x = -swing * 0.85;
        rightKnee.rotation.x = Math.max(0, swing * 1.1);

        leftArm.rotation.x = -swing * 0.75;
        leftArm.rotation.z = 0.12;
        leftElbow.rotation.x = -0.85;

        rightArm.rotation.x = swing * 0.75;
        rightArm.rotation.z = -0.12;
        rightElbow.rotation.x = -0.85;

        spineBone.rotation.x = 0.18;
        spineBone.position.y = 0.9 + Math.abs(Math.sin(time * speed)) * 0.08;
        break;
      }

      case 'talk_gesture': {
        const t = time * 2;
        rightArm.rotation.set(-0.55 + Math.sin(t) * 0.2, 0, -0.25 + Math.cos(t * 0.7) * 0.1);
        rightElbow.rotation.set(-0.5 + Math.sin(t * 1.2) * 0.2, 0, 0);

        leftArm.rotation.set(-0.2 + Math.sin(t * 0.8) * 0.15, 0, 0.1);
        leftElbow.rotation.set(-0.25, 0, 0);

        headBone.rotation.y = Math.sin(t * 0.5) * 0.15;
        headBone.rotation.x = Math.sin(t * 0.9) * 0.08;
        break;
      }

      case 'heavy_slash_combo':
      case 'fast_slash': {
        if (actionProgress < 0.35) {
          const p = actionProgress / 0.35;
          rightArm.rotation.x = -Math.PI * 0.85 * p;
          rightArm.rotation.z = -0.4 * p;
          rightElbow.rotation.x = -0.5 * p;
          spineBone.rotation.y = -0.5 * p;
          spineBone.rotation.x = -0.15 * p;
        } else if (actionProgress < 0.65) {
          const p = (actionProgress - 0.35) / 0.3;
          rightArm.rotation.x = -Math.PI * 0.85 + (Math.PI * 0.85 + 0.6) * p;
          rightArm.rotation.z = -0.4 + 0.6 * p;
          rightElbow.rotation.x = -0.5 + 0.5 * p;
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
        const p = Math.min(1, Math.max(0, actionProgress));
        spineBone.rotation.x = -Math.PI * 0.38 * Math.min(1, p * 1.6);
        spineBone.position.y = 0.9 - 0.5 * Math.min(1, p * 1.6);
        headBone.rotation.x = -0.5;

        leftArm.rotation.set(-1.4, 0, 0.4);
        rightArm.rotation.set(-1.4, 0, -0.4);
        leftElbow.rotation.x = -0.4;
        rightElbow.rotation.x = -0.4;

        leftLeg.position.y = 0.85 - 0.45 * Math.min(1, p * 1.6);
        rightLeg.position.y = 0.85 - 0.45 * Math.min(1, p * 1.6);
        leftLeg.rotation.x = 0.7 * Math.min(1, p * 1.6);
        leftKnee.rotation.x = 0.4 * Math.min(1, p * 1.6);
        rightLeg.rotation.x = 0.5 * Math.min(1, p * 1.6);
        rightKnee.rotation.x = 0.6 * Math.min(1, p * 1.6);
        break;
      }

      case 'stagger_back': {
        const p = Math.min(1, Math.max(0, actionProgress));
        spineBone.rotation.x = -0.35 * (1 - p);
        headBone.rotation.x = -0.3 * (1 - p);
        rightArm.rotation.x = -0.8;
        leftArm.rotation.x = -0.8;
        rightElbow.rotation.x = -0.6;
        leftElbow.rotation.x = -0.6;
        break;
      }

      case 'sit': {
        // Continuous smooth transition from standing (0.0) to seated (1.0)
        const sitWeight = actionProgress > 0 ? THREE.MathUtils.clamp(actionProgress, 0, 1) : 1.0;
        const breath = Math.sin(time * 1.5);

        // Torso lowers smoothly to seat height (0.9 -> 0.52)
        spineBone.position.set(
          0,
          THREE.MathUtils.lerp(0.9, 0.52, sitWeight),
          THREE.MathUtils.lerp(0, -0.05, sitWeight)
        );
        spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.06 + breath * 0.015, sitWeight);
        headBone.rotation.x = THREE.MathUtils.lerp(0, 0.04 + breath * 0.015, sitWeight);

        // Thighs bend smoothly from standing (0) to horizontal (-90 deg)
        leftLeg.position.set(
          -0.16,
          THREE.MathUtils.lerp(0.85, 0.52, sitWeight),
          THREE.MathUtils.lerp(0, 0.05, sitWeight)
        );
        rightLeg.position.set(
          0.16,
          THREE.MathUtils.lerp(0.85, 0.52, sitWeight),
          THREE.MathUtils.lerp(0, 0.05, sitWeight)
        );

        leftLeg.rotation.set(
          THREE.MathUtils.lerp(0, -Math.PI / 2, sitWeight),
          THREE.MathUtils.lerp(0, -0.06, sitWeight),
          0
        );
        rightLeg.rotation.set(
          THREE.MathUtils.lerp(0, -Math.PI / 2, sitWeight),
          THREE.MathUtils.lerp(0, 0.06, sitWeight),
          0
        );

        // Knees bend smoothly from straight (0) to 90 degrees down (+90 deg)
        leftKnee.rotation.set(THREE.MathUtils.lerp(0, Math.PI / 2, sitWeight), 0, 0);
        rightKnee.rotation.set(THREE.MathUtils.lerp(0, Math.PI / 2, sitWeight), 0, 0);

        // Arms rest on lap/thighs
        leftArm.rotation.set(
          THREE.MathUtils.lerp(0, -0.35, sitWeight),
          0,
          THREE.MathUtils.lerp(0.08, 0.12, sitWeight)
        );
        leftElbow.rotation.set(
          THREE.MathUtils.lerp(-0.1, -0.75, sitWeight),
          0,
          THREE.MathUtils.lerp(0, 0.15, sitWeight)
        );

        rightArm.rotation.set(
          THREE.MathUtils.lerp(0, -0.35, sitWeight),
          0,
          THREE.MathUtils.lerp(-0.08, -0.12, sitWeight)
        );
        rightElbow.rotation.set(
          THREE.MathUtils.lerp(-0.1, -0.75, sitWeight),
          0,
          THREE.MathUtils.lerp(0, -0.15, sitWeight)
        );
        break;
      }

      case 'climb': {
        const climbTime = time * 4;
        const swing = Math.sin(climbTime);

        leftArm.rotation.set(-Math.PI * 0.75 + swing * 0.35, 0, 0.15);
        leftElbow.rotation.set(-0.9 + swing * 0.25, 0, 0);

        rightArm.rotation.set(-Math.PI * 0.75 - swing * 0.35, 0, -0.15);
        rightElbow.rotation.set(-0.9 - swing * 0.25, 0, 0);

        leftLeg.rotation.x = -0.4 + swing * 0.3;
        leftKnee.rotation.x = Math.max(0, 0.6 + swing * 0.4);

        rightLeg.rotation.x = -0.4 - swing * 0.3;
        rightKnee.rotation.x = Math.max(0, 0.6 - swing * 0.4);
        break;
      }

      case 'idle':
      default: {
        const breath = Math.sin(time * 1.5);
        spineBone.position.y = 0.9 + breath * 0.015;
        headBone.rotation.x = breath * 0.02;
        leftArm.rotation.z = 0.08 + breath * 0.02;
        leftElbow.rotation.x = -0.1;
        rightArm.rotation.z = -0.08 - breath * 0.02;
        rightElbow.rotation.x = -0.1;
        break;
      }
    }
  }
}
