import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { HandIKController } from '../actors/HandIKController';
import {
  InteractionType,
  ObjectInteractionTrack,
  InteractionPhases,
} from '../../types/interactions';

// ============================================================
// Default phase timing cho mỗi loại interaction
// ============================================================

const DEFAULT_PHASES: Record<InteractionType, InteractionPhases> = {
  pickup:      { reach: 0.25, grab: 0.35, action: 0.55, release: 1.0 },
  carry:       { reach: 0.2,  grab: 0.3,  action: 0.9,  release: 1.0 },
  drink:       { reach: 0.2,  grab: 0.3,  action: 0.8,  release: 1.0 },
  eat:         { reach: 0.2,  grab: 0.3,  action: 0.8,  release: 1.0 },
  place:       { reach: 0.1,  grab: 0.15, action: 0.7,  release: 1.0 },
  throw:       { reach: 0.15, grab: 0.25, action: 0.55, release: 0.7 },
  give:        { reach: 0.2,  grab: 0.3,  action: 0.7,  release: 1.0 },
  pour:        { reach: 0.2,  grab: 0.3,  action: 0.8,  release: 1.0 },
  dig:         { reach: 0.15, grab: 0.2,  action: 0.85, release: 1.0 },
  water:       { reach: 0.15, grab: 0.2,  action: 0.85, release: 1.0 },
  plant_seed:  { reach: 0.2,  grab: 0.25, action: 0.75, release: 1.0 },
  harvest:     { reach: 0.2,  grab: 0.3,  action: 0.7,  release: 1.0 },
  read:        { reach: 0.15, grab: 0.2,  action: 0.9,  release: 1.0 },
  open_door:   { reach: 0.2,  grab: 0.3,  action: 0.7,  release: 1.0 },
  close_door:  { reach: 0.2,  grab: 0.3,  action: 0.7,  release: 1.0 },
  push:        { reach: 0.2,  grab: 0.3,  action: 0.85, release: 1.0 },
  pull:        { reach: 0.2,  grab: 0.3,  action: 0.85, release: 1.0 },
};

// ============================================================
// ObjectInteractionSystem
// ============================================================

export class ObjectInteractionSystem {

  /**
   * Execute 1 interaction track tại progress hiện tại.
   * Gọi mỗi frame từ TrackEvaluator.
   */
  public static execute(
    track: ObjectInteractionTrack,
    avatar: VRMAvatar,
    animator: ActorAnimator,
    handIK: HandIKController,
    objectWorldPos: THREE.Vector3,
    progress: number,  // 0.0 → 1.0 normalized progress of this track
    currentTime: number
  ): InteractionPhaseResult {
    const phases = track.phases || DEFAULT_PHASES[track.interaction];
    const phase = this.getCurrentPhase(progress, phases);

    switch (phase) {
      case 'reach':
        return this.executeReach(avatar, animator, handIK, objectWorldPos, progress, phases, track, currentTime);
      case 'grab':
        return this.executeGrab(avatar, animator, handIK, objectWorldPos, progress, phases, track, currentTime);
      case 'action':
        return this.executeAction(avatar, animator, handIK, objectWorldPos, progress, phases, track, currentTime);
      case 'release':
        return this.executeRelease(avatar, animator, handIK, progress, phases, track, currentTime);
    }
  }

  /** Xác định phase hiện tại */
  private static getCurrentPhase(
    progress: number, phases: InteractionPhases
  ): 'reach' | 'grab' | 'action' | 'release' {
    if (progress < phases.reach) return 'reach';
    if (progress < phases.grab) return 'grab';
    if (progress < phases.action) return 'action';
    return 'release';
  }

  // ============================================================
  // Phase 1: REACH - Duỗi tay tới đồ vật
  // ============================================================
  private static executeReach(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    handIK: HandIKController,
    objectPos: THREE.Vector3,
    progress: number,
    phases: InteractionPhases,
    track: ObjectInteractionTrack,
    time: number
  ): InteractionPhaseResult {
    const phaseProgress = progress / phases.reach;

    // IK: tay dần vươn tới vị trí đồ vật
    const side = track.attach_socket === 'weapon_l' ? 'left' : 'right';
    handIK.setTarget(side, objectPos, phaseProgress);

    // Cúi nhẹ nếu đồ vật ở thấp
    const objHeight = objectPos.y;
    if (objHeight < 0.5) {
      avatar.spineBone.rotation.x = 0.3 * phaseProgress;
    }

    animator.setAction('idle');
    animator.update(time);
    handIK.update();

    return { phase: 'reach', attached: false };
  }

  // ============================================================
  // Phase 2: GRAB - Nắm chặt đồ vật
  // ============================================================
  private static executeGrab(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    handIK: HandIKController,
    objectPos: THREE.Vector3,
    progress: number,
    phases: InteractionPhases,
    track: ObjectInteractionTrack,
    time: number
  ): InteractionPhaseResult {
    const side = track.attach_socket === 'weapon_l' ? 'left' : 'right';
    handIK.setTarget(side, objectPos, 1.0);

    animator.setAction('idle');
    animator.update(time);
    handIK.update();

    // Gắn đồ vật vào socket tay
    return { phase: 'grab', attached: true };
  }

  // ============================================================
  // Phase 3: ACTION - Thực hiện hành động chính
  // ============================================================
  private static executeAction(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    handIK: HandIKController,
    objectPos: THREE.Vector3,
    progress: number,
    phases: InteractionPhases,
    track: ObjectInteractionTrack,
    time: number
  ): InteractionPhaseResult {
    const phaseProgress = (progress - phases.grab) / (phases.action - phases.grab);

    // Xóa IK, để animation procedural control
    handIK.clearAll();

    // Delegate tới animation cụ thể theo loại interaction
    this.applyInteractionAnimation(avatar, animator, track.interaction, phaseProgress, time);

    return { phase: 'action', attached: true };
  }

  // ============================================================
  // Phase 4: RELEASE - Thả / đặt xuống
  // ============================================================
  private static executeRelease(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    handIK: HandIKController,
    progress: number,
    phases: InteractionPhases,
    track: ObjectInteractionTrack,
    time: number
  ): InteractionPhaseResult {
    const phaseProgress = (progress - phases.action) / (phases.release - phases.action);

    handIK.clearAll();

    // Quay lại pose idle mượt mà
    animator.setAction('idle');
    animator.update(time);

    // Detach khi phase release hoàn tất
    const detached = phaseProgress > 0.5;

    return { phase: 'release', attached: !detached };
  }

  // ============================================================
  // Animation cụ thể cho từng loại interaction
  // ============================================================
  private static applyInteractionAnimation(
    avatar: VRMAvatar,
    animator: ActorAnimator,
    interaction: InteractionType,
    progress: number,
    time: number
  ): void {
    switch (interaction) {
      case 'drink':
        this.animDrink(avatar, progress, time);
        break;
      case 'carry':
        this.animCarry(avatar, progress, time);
        break;
      case 'pour':
        this.animPour(avatar, progress, time);
        break;
      case 'dig':
        this.animDig(avatar, progress, time);
        break;
      case 'water':
        this.animWater(avatar, progress, time);
        break;
      case 'plant_seed':
        this.animPlantSeed(avatar, progress, time);
        break;
      case 'throw':
        this.animThrow(avatar, progress, time);
        break;
      default:
        animator.setAction('idle');
        animator.update(time);
        break;
    }
  }

  /** Uống nước: nâng tay lên miệng, nghiêng, nuốt */
  private static animDrink(a: VRMAvatar, progress: number, time: number): void {
    const liftPhase = Math.min(1, progress * 2);   // 0→0.5: nâng lên
    const drinkPhase = Math.max(0, (progress - 0.3) * 2); // 0.3→0.8: nghiêng uống

    a.rightArm.rotation.set(-1.4 * liftPhase, 0, -0.3);
    a.rightElbow.rotation.set(-1.6 * liftPhase, 0, 0);
    a.headBone.rotation.x = -0.15 * drinkPhase; // Ngẩng đầu nhẹ khi uống

    // Nuốt: miệng mở nhẹ
    if (drinkPhase > 0.2 && drinkPhase < 0.8) {
      a.mouthMesh.scale.set(0.8, 1.5, 1);
    }
  }

  /** Bưng bê 2 tay: 2 tay phía trước, khuỷu gập */
  private static animCarry(a: VRMAvatar, progress: number, time: number): void {
    const walkSway = Math.sin(time * 3) * 0.03;

    a.leftArm.rotation.set(-0.7, 0, 0.4);
    a.leftElbow.rotation.set(-1.2, 0, 0);
    a.rightArm.rotation.set(-0.7, 0, -0.4);
    a.rightElbow.rotation.set(-1.2, 0, 0);
    a.spineBone.rotation.x = -0.05 + walkSway;
  }

  /** Rót: nghiêng tay cầm bình */
  private static animPour(a: VRMAvatar, progress: number, time: number): void {
    const pourAngle = Math.sin(progress * Math.PI) * 0.8;

    a.rightArm.rotation.set(-1.0, 0, -0.3);
    a.rightElbow.rotation.set(-0.8 - pourAngle, 0, 0);
    a.spineBone.rotation.x = 0.1;
  }

  /** Đào đất: vung cuốc lên xuống */
  private static animDig(a: VRMAvatar, progress: number, time: number): void {
    const digCycle = Math.sin(progress * Math.PI * 4); // 2 nhát cuốc

    a.rightArm.rotation.set(-1.8 + digCycle * 0.8, 0, -0.2);
    a.rightElbow.rotation.set(-0.5 + digCycle * 0.3, 0, 0);
    a.spineBone.rotation.x = 0.2 + digCycle * 0.15;
  }

  /** Tưới cây: nghiêng bình tưới */
  private static animWater(a: VRMAvatar, progress: number, time: number): void {
    const sweepAngle = Math.sin(progress * Math.PI * 2) * 0.4; // Quét qua lại

    a.rightArm.rotation.set(-0.8, sweepAngle, -0.3);
    a.rightElbow.rotation.set(-0.6, 0, 0);
    a.spineBone.rotation.x = 0.15;
    a.spineBone.rotation.y = sweepAngle * 0.3;
  }

  /** Gieo hạt: cúi xuống, rải từ tay */
  private static animPlantSeed(a: VRMAvatar, progress: number, time: number): void {
    const bendDown = Math.sin(progress * Math.PI);

    a.spineBone.rotation.x = 0.4 * bendDown;
    a.rightArm.rotation.set(-0.5 * bendDown, 0, -0.3);
    a.rightElbow.rotation.set(-0.8 * bendDown, 0, 0);
    a.leftArm.rotation.set(-0.3 * bendDown, 0, 0.15);
  }

  /** Ném: wind up → release */
  private static animThrow(a: VRMAvatar, progress: number, time: number): void {
    if (progress < 0.5) {
      // Wind up
      const p = progress / 0.5;
      a.rightArm.rotation.set(0.8 * p, 0, -0.4 * p);
      a.rightElbow.rotation.set(-0.3, 0, 0);
      a.spineBone.rotation.y = -0.3 * p;
    } else {
      // Release
      const p = (progress - 0.5) / 0.5;
      a.rightArm.rotation.set(-1.8 * p, 0, -0.2);
      a.rightElbow.rotation.set(-0.1, 0, 0);
      a.spineBone.rotation.y = 0.4 * p;
    }
  }
}

/** Kết quả trả về sau mỗi frame */
export interface InteractionPhaseResult {
  phase: 'reach' | 'grab' | 'action' | 'release';
  attached: boolean;  // true = đồ vật đang gắn trên tay
}
