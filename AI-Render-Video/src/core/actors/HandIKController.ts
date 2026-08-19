import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';

// ============================================================
// HandIKController - Two-Bone IK cho tay
// Tự động duỗi tay tới vị trí đồ vật
// ============================================================

export type HandSide = 'left' | 'right';

export interface HandIKTarget {
  position: THREE.Vector3;
  weight: number;        // 0-1 blend giữa animation gốc và IK
}

export class HandIKController {
  private avatar: VRMAvatar;
  private targets: Map<HandSide, HandIKTarget | null> = new Map([
    ['left', null],
    ['right', null],
  ]);

  // Reusable temp vectors
  private tmpVec = new THREE.Vector3();
  private tmpVec2 = new THREE.Vector3();

  constructor(avatar: VRMAvatar) {
    this.avatar = avatar;
  }

  /** Đặt target cho tay */
  public setTarget(side: HandSide, position: THREE.Vector3, weight: number = 1.0): void {
    this.targets.set(side, { position: position.clone(), weight });
  }

  /** Xóa target */
  public clearTarget(side: HandSide): void {
    this.targets.set(side, null);
  }

  /** Xóa tất cả targets */
  public clearAll(): void {
    this.targets.set('left', null);
    this.targets.set('right', null);
  }

  /** Apply IK cho cả 2 tay */
  public update(): void {
    const leftTarget = this.targets.get('left');
    if (leftTarget) {
      this.solveArm(
        this.avatar.leftArm,
        this.avatar.leftElbow,
        leftTarget.position,
        leftTarget.weight,
        'left'
      );
    }

    const rightTarget = this.targets.get('right');
    if (rightTarget) {
      this.solveArm(
        this.avatar.rightArm,
        this.avatar.rightElbow,
        rightTarget.position,
        rightTarget.weight,
        'right'
      );
    }
  }

  /**
   * Two-Bone IK solver cho cánh tay
   * Shoulder → Elbow → Hand (weapon socket)
   */
  private solveArm(
    shoulder: THREE.Object3D,
    elbow: THREE.Object3D,
    targetWorld: THREE.Vector3,
    weight: number,
    side: HandSide
  ): void {
    if (weight <= 0) return;

    // Lấy chiều dài 2 đoạn xương
    const upperArmLen = 0.28; // Khoảng cách shoulder → elbow
    const forearmLen = 0.26;  // Khoảng cách elbow → hand (weapon socket)
    const totalLen = upperArmLen + forearmLen;

    // Vị trí world của shoulder
    const shoulderWorld = this.tmpVec;
    shoulder.getWorldPosition(shoulderWorld);

    // Direction từ shoulder tới target
    const toTarget = this.tmpVec2.copy(targetWorld).sub(shoulderWorld);
    const targetDist = Math.min(toTarget.length(), totalLen * 0.98); // Clamp để không kéo quá

    // Chuyển target direction sang local space của shoulder parent
    const parentInverse = shoulder.parent
      ? new THREE.Quaternion().copy(shoulder.parent.getWorldQuaternion(new THREE.Quaternion())).invert()
      : new THREE.Quaternion();

    const localDir = toTarget.normalize().applyQuaternion(parentInverse);

    // Tính góc shoulder pitch/yaw hướng tới target
    const targetPitchX = -Math.asin(Math.max(-1, Math.min(1, localDir.y)));
    const targetYawY = Math.atan2(localDir.x, localDir.z);
    const sideSign = side === 'left' ? 1 : -1;

    // Tính góc elbow bend bằng cosine rule
    const cosAngle = (upperArmLen * upperArmLen + forearmLen * forearmLen - targetDist * targetDist)
      / (2 * upperArmLen * forearmLen);
    const elbowAngle = Math.PI - Math.acos(THREE.MathUtils.clamp(cosAngle, -1, 1));

    // Lưu current animation rotations
    const currShoulderX = shoulder.rotation.x;
    const currShoulderY = shoulder.rotation.y;
    const currShoulderZ = shoulder.rotation.z;
    const currElbowX = elbow.rotation.x;

    // Blend giữa animation gốc và IK result
    shoulder.rotation.x = THREE.MathUtils.lerp(currShoulderX, targetPitchX, weight);
    shoulder.rotation.y = THREE.MathUtils.lerp(currShoulderY, targetYawY, weight);
    shoulder.rotation.z = THREE.MathUtils.lerp(currShoulderZ, sideSign * 0.1, weight);
    elbow.rotation.x = THREE.MathUtils.lerp(currElbowX, -elbowAngle, weight);
  }
}
