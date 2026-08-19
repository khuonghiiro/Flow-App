import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';
import { AnimationAction } from './ActorAnimator';

// ============================================================
// Bone Pose Snapshot - Lưu trạng thái xương tại 1 thời điểm
// ============================================================

export interface BonePoseSnapshot {
  leftArm: THREE.Euler;
  leftElbow: THREE.Euler;
  rightArm: THREE.Euler;
  rightElbow: THREE.Euler;
  leftLeg: THREE.Euler;
  leftKnee: THREE.Euler;
  rightLeg: THREE.Euler;
  rightKnee: THREE.Euler;
  spineBone: THREE.Euler;
  spinePosition: THREE.Vector3;
  headBone: THREE.Euler;
  leftLegPosition: THREE.Vector3;
  rightLegPosition: THREE.Vector3;
}

// ============================================================
// Additive Animation Layer
// ============================================================

export interface AdditiveLayer {
  action: AnimationAction;
  weight: number;                 // 0-1 blend weight
  boneMask: Set<string>;          // Bones affected ('leftArm', 'rightArm', 'head', etc.)
  fadeState: 'in' | 'active' | 'out';
  fadeProgress: number;           // 0-1
  fadeDuration: number;           // seconds
}

// ============================================================
// AnimationBlender - Crossfade mượt giữa animations
// ============================================================

export class AnimationBlender {
  private avatar: VRMAvatar;
  private previousPose: BonePoseSnapshot | null = null;
  private blendWeight: number = 1.0;     // 0 = previous, 1 = current
  private blendDuration: number = 0.3;   // 300ms default crossfade
  private blendTimer: number = 0;
  private isBlending: boolean = false;
  private layers: AdditiveLayer[] = [];

  constructor(avatar: VRMAvatar) {
    this.avatar = avatar;
  }

  /** Bắt đầu transition sang animation mới với crossfade */
  public startTransition(duration: number = 0.3): void {
    // Snapshot current pose trước khi chuyển animation
    this.previousPose = this.capturePose();
    this.blendDuration = Math.max(0.05, duration);
    this.blendTimer = 0;
    this.blendWeight = 0;
    this.isBlending = true;
  }

  /** Snapshot pose hiện tại của avatar */
  public capturePose(): BonePoseSnapshot {
    const a = this.avatar;
    return {
      leftArm: a.leftArm.rotation.clone(),
      leftElbow: a.leftElbow.rotation.clone(),
      rightArm: a.rightArm.rotation.clone(),
      rightElbow: a.rightElbow.rotation.clone(),
      leftLeg: a.leftLeg.rotation.clone(),
      leftKnee: a.leftKnee.rotation.clone(),
      rightLeg: a.rightLeg.rotation.clone(),
      rightKnee: a.rightKnee.rotation.clone(),
      spineBone: a.spineBone.rotation.clone(),
      spinePosition: a.spineBone.position.clone(),
      headBone: a.headBone.rotation.clone(),
      leftLegPosition: a.leftLeg.position.clone(),
      rightLegPosition: a.rightLeg.position.clone(),
    };
  }

  /** Apply blending vào avatar sau khi ActorAnimator đã set pose mới */
  public applyBlend(delta: number): void {
    if (!this.isBlending || !this.previousPose) return;

    this.blendTimer += delta;
    this.blendWeight = Math.min(1.0, this.blendTimer / this.blendDuration);

    // Nếu hoàn tất blend thì dừng
    if (this.blendWeight >= 1.0) {
      this.isBlending = false;
      this.previousPose = null;
      return;
    }

    // Lerp giữa previous pose và current pose (đã được set bởi ActorAnimator)
    const w = this.blendWeight;
    const prev = this.previousPose;
    const a = this.avatar;

    this.lerpEuler(a.leftArm.rotation, prev.leftArm, a.leftArm.rotation, w);
    this.lerpEuler(a.leftElbow.rotation, prev.leftElbow, a.leftElbow.rotation, w);
    this.lerpEuler(a.rightArm.rotation, prev.rightArm, a.rightArm.rotation, w);
    this.lerpEuler(a.rightElbow.rotation, prev.rightElbow, a.rightElbow.rotation, w);
    this.lerpEuler(a.leftLeg.rotation, prev.leftLeg, a.leftLeg.rotation, w);
    this.lerpEuler(a.leftKnee.rotation, prev.leftKnee, a.leftKnee.rotation, w);
    this.lerpEuler(a.rightLeg.rotation, prev.rightLeg, a.rightLeg.rotation, w);
    this.lerpEuler(a.rightKnee.rotation, prev.rightKnee, a.rightKnee.rotation, w);
    this.lerpEuler(a.spineBone.rotation, prev.spineBone, a.spineBone.rotation, w);
    this.lerpEuler(a.headBone.rotation, prev.headBone, a.headBone.rotation, w);

    a.spineBone.position.lerpVectors(prev.spinePosition, a.spineBone.position, w);
    a.leftLeg.position.lerpVectors(prev.leftLegPosition, a.leftLeg.position, w);
    a.rightLeg.position.lerpVectors(prev.rightLegPosition, a.rightLeg.position, w);
  }

  /** Thêm additive layer (ví dụ: vừa đi vừa vẫy tay) */
  public addLayer(
    action: AnimationAction,
    weight: number,
    boneMask: string[],
    fadeDuration: number = 0.2
  ): void {
    // Remove existing layer with same action
    this.layers = this.layers.filter((l) => l.action !== action);

    this.layers.push({
      action,
      weight: Math.min(1, Math.max(0, weight)),
      boneMask: new Set(boneMask),
      fadeState: 'in',
      fadeProgress: 0,
      fadeDuration,
    });
  }

  /** Xóa additive layer */
  public removeLayer(action: AnimationAction, fadeDuration: number = 0.2): void {
    const layer = this.layers.find((l) => l.action === action);
    if (layer) {
      layer.fadeState = 'out';
      layer.fadeProgress = 0;
      layer.fadeDuration = fadeDuration;
    }
  }

  /** Update tất cả additive layers */
  public updateLayers(delta: number): void {
    this.layers = this.layers.filter((layer) => {
      if (layer.fadeState === 'in') {
        layer.fadeProgress += delta / layer.fadeDuration;
        if (layer.fadeProgress >= 1) {
          layer.fadeState = 'active';
          layer.fadeProgress = 1;
        }
      } else if (layer.fadeState === 'out') {
        layer.fadeProgress += delta / layer.fadeDuration;
        if (layer.fadeProgress >= 1) {
          return false; // Remove layer
        }
      }
      return true;
    });
  }

  /** Lấy effective weight của layer (bao gồm fade) */
  public getLayerWeight(layer: AdditiveLayer): number {
    if (layer.fadeState === 'in') {
      return layer.weight * layer.fadeProgress;
    } else if (layer.fadeState === 'out') {
      return layer.weight * (1 - layer.fadeProgress);
    }
    return layer.weight;
  }

  /** Kiểm tra bone có bị ảnh hưởng bởi layer nào không */
  public isBonesAffectedByLayer(boneName: string): AdditiveLayer | undefined {
    return this.layers.find((l) => l.boneMask.has(boneName));
  }

  /** Đang trong quá trình blending? */
  public get blending(): boolean {
    return this.isBlending;
  }

  /** Có additive layers active? */
  public get hasActiveLayers(): boolean {
    return this.layers.length > 0;
  }

  /** Reset blender */
  public reset(): void {
    this.previousPose = null;
    this.isBlending = false;
    this.blendWeight = 1.0;
    this.layers = [];
  }

  // ============================================================
  // Private Helpers
  // ============================================================

  private lerpEuler(
    out: THREE.Euler, from: THREE.Euler, to: THREE.Euler, t: number
  ): void {
    out.x = THREE.MathUtils.lerp(from.x, to.x, t);
    out.y = THREE.MathUtils.lerp(from.y, to.y, t);
    out.z = THREE.MathUtils.lerp(from.z, to.z, t);
  }
}
