import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';

// ============================================================
// FootIKController - IK cho chân
// Tự động điều chỉnh chân theo địa hình
// ============================================================

export interface FootIKConfig {
  raycastHeight: number;    // Chiều cao raycast (từ hip xuống)
  maxCorrection: number;    // Điều chỉnh tối đa (mét)
  blendSpeed: number;       // Tốc độ lerp
  enabled: boolean;
}

export class FootIKController {
  private avatar: VRMAvatar;
  private config: FootIKConfig;
  private currentLeftCorrection: number = 0;
  private currentRightCorrection: number = 0;
  private raycaster: THREE.Raycaster;
  private downDirection: THREE.Vector3;
  private groundMeshes: THREE.Object3D[] = [];

  constructor(avatar: VRMAvatar, config?: Partial<FootIKConfig>) {
    this.avatar = avatar;
    this.config = {
      raycastHeight: 2.0,
      maxCorrection: 0.3,
      blendSpeed: 8,
      enabled: true,
      ...config,
    };
    this.raycaster = new THREE.Raycaster();
    this.downDirection = new THREE.Vector3(0, -1, 0);
  }

  /** Set danh sách mesh mặt đất để raycast */
  public setGroundMeshes(meshes: THREE.Object3D[]): void {
    this.groundMeshes = meshes;
  }

  /** Enable/disable foot IK */
  public setEnabled(enabled: boolean): void {
    this.config.enabled = enabled;
  }

  /** Update foot IK - gọi sau khi ActorAnimator đã set pose */
  public update(delta: number): void {
    if (!this.config.enabled || this.groundMeshes.length === 0) return;

    // Raycast cho chân trái
    const leftCorrection = this.raycastFootHeight(this.avatar.leftFoot);
    this.currentLeftCorrection = THREE.MathUtils.lerp(
      this.currentLeftCorrection,
      leftCorrection,
      delta * this.config.blendSpeed
    );

    // Raycast cho chân phải
    const rightCorrection = this.raycastFootHeight(this.avatar.rightFoot);
    this.currentRightCorrection = THREE.MathUtils.lerp(
      this.currentRightCorrection,
      rightCorrection,
      delta * this.config.blendSpeed
    );

    // Apply corrections
    if (Math.abs(this.currentLeftCorrection) > 0.001) {
      this.avatar.leftFoot.position.y += this.currentLeftCorrection;
    }
    if (Math.abs(this.currentRightCorrection) > 0.001) {
      this.avatar.rightFoot.position.y += this.currentRightCorrection;
    }

    // Điều chỉnh hip height dựa trên sự khác biệt chân
    const hipCorrection = Math.min(this.currentLeftCorrection, this.currentRightCorrection);
    if (Math.abs(hipCorrection) > 0.01) {
      this.avatar.spineBone.position.y += hipCorrection * 0.5;
    }
  }

  /** Raycast xuống mặt đất từ vị trí chân */
  private raycastFootHeight(foot: THREE.Object3D): number {
    const footWorldPos = new THREE.Vector3();
    foot.getWorldPosition(footWorldPos);

    // Bắn ray từ trên chân xuống
    const rayOrigin = footWorldPos.clone();
    rayOrigin.y += this.config.raycastHeight;

    this.raycaster.set(rayOrigin, this.downDirection);
    this.raycaster.far = this.config.raycastHeight * 2;

    const intersects = this.raycaster.intersectObjects(this.groundMeshes, true);

    if (intersects.length > 0) {
      const groundY = intersects[0].point.y;
      const footY = footWorldPos.y;
      const diff = groundY - footY;

      // Clamp correction
      return THREE.MathUtils.clamp(diff, -this.config.maxCorrection, this.config.maxCorrection);
    }

    return 0;
  }

  /** Reset corrections */
  public reset(): void {
    this.currentLeftCorrection = 0;
    this.currentRightCorrection = 0;
  }
}
