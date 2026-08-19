import * as THREE from 'three';
import { VRMAvatar } from './VRMAvatar';

// ============================================================
// Xianxia Pose Library - Thư viện tư thế tiên hiệp
// Các tư thế đặc trưng phim Wuxia / Xianxia cultivation
// ============================================================

export type XianxiaPose =
  | 'arms_crossed'        // Khoanh tay trước ngực (lạnh lùng)
  | 'hands_behind_back'   // 2 tay sau lưng, ngẩng đầu (kiêu ngạo)
  | 'fist_salute'         // Bao quyền lễ (chào võ lâm)
  | 'meditation_sit'      // Ngồi thiền (bắt chéo chân)
  | 'meditation_float'    // Bay lơ lửng ngồi thiền
  | 'sword_behind_back'   // Cắm kiếm sau lưng đứng hiên ngang
  | 'finger_spell'        // Bắt ấn quyết (2 ngón tay trước mặt)
  | 'power_charge'        // Vận công (nắm tay, cơ thể rung)
  | 'palm_strike_pose'    // Tư thế chưởng lực (tay duỗi phía trước)
  | 'flying_stance'       // Tư thế bay (đạp kiếm / đạp mây)
  | 'kneeling_bow'        // Quỳ gối cúi đầu (bái sư)
  | 'drinking_wine'       // Uống rượu kiểu hiệp khách
  | 'contemplation';      // Đứng ngắm trời, tay chắp sau

export class XianxiaPoseLibrary {

  /** Apply tư thế xianxia lên avatar */
  public static applyPose(
    avatar: VRMAvatar, pose: XianxiaPose, time: number, progress: number = 1.0
  ): void {
    switch (pose) {
      case 'arms_crossed':
        this.poseArmsCrossed(avatar, time, progress);
        break;
      case 'hands_behind_back':
        this.poseHandsBehindBack(avatar, time, progress);
        break;
      case 'fist_salute':
        this.poseFistSalute(avatar, time, progress);
        break;
      case 'meditation_sit':
        this.poseMeditationSit(avatar, time, progress);
        break;
      case 'meditation_float':
        this.poseMeditationFloat(avatar, time, progress);
        break;
      case 'sword_behind_back':
        this.poseSwordBehindBack(avatar, time, progress);
        break;
      case 'finger_spell':
        this.poseFingerSpell(avatar, time, progress);
        break;
      case 'power_charge':
        this.posePowerCharge(avatar, time, progress);
        break;
      case 'palm_strike_pose':
        this.posePalmStrike(avatar, time, progress);
        break;
      case 'flying_stance':
        this.poseFlyingStance(avatar, time, progress);
        break;
      case 'kneeling_bow':
        this.poseKneelingBow(avatar, time, progress);
        break;
      case 'drinking_wine':
        this.poseDrinkingWine(avatar, time, progress);
        break;
      case 'contemplation':
        this.poseContemplation(avatar, time, progress);
        break;
    }
  }

  // ============================================================
  // Khoanh tay trước ngực - Lạnh lùng / Tự tin
  // ============================================================
  private static poseArmsCrossed(a: VRMAvatar, time: number, w: number): void {
    const breath = Math.sin(time * 1.2) * 0.01;

    a.spineBone.position.y = 0.9 + breath;
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.03, w);
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, 0.02 + breath, w);

    // Tay trái gập trước ngực, bàn tay hướng sang phải
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -1.2, w),
      THREE.MathUtils.lerp(0, 0.3, w),
      THREE.MathUtils.lerp(0.08, 0.5, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.6, w), 0, 0
    );

    // Tay phải gập trước ngực, bàn tay hướng sang trái
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -1.2, w),
      THREE.MathUtils.lerp(0, -0.3, w),
      THREE.MathUtils.lerp(-0.08, -0.5, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.7, w), 0, 0
    );
  }

  // ============================================================
  // 2 tay sau lưng, đầu ngẩng - Kiêu ngạo / Quyền uy
  // ============================================================
  private static poseHandsBehindBack(a: VRMAvatar, time: number, w: number): void {
    const breath = Math.sin(time * 1.0) * 0.012;

    a.spineBone.position.y = 0.9 + breath * 0.5;
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.08, w); // Ưỡn ngực nhẹ
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, -0.15, w);  // Ngẩng đầu cao

    // Tay trái vòng ra sau lưng
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.6, w),
      THREE.MathUtils.lerp(0, 0.3, w),
      THREE.MathUtils.lerp(0.08, 0.15, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.5, w), 0, 0
    );

    // Tay phải vòng ra sau lưng
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.6, w),
      THREE.MathUtils.lerp(0, -0.3, w),
      THREE.MathUtils.lerp(-0.08, -0.15, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.5, w), 0, 0
    );
  }

  // ============================================================
  // Bao quyền lễ - Chào kiểu võ lâm (nắm tay + bàn tay)
  // ============================================================
  private static poseFistSalute(a: VRMAvatar, time: number, w: number): void {
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, 0.1, w); // Cúi nhẹ
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, 0.08, w);

    // Tay trái (bàn tay mở) chụm trước ngực
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.8, w), 0,
      THREE.MathUtils.lerp(0.08, 0.6, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.3, w), 0, 0
    );

    // Tay phải (nắm đấm) chụm trước ngực
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.8, w), 0,
      THREE.MathUtils.lerp(-0.08, -0.6, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.3, w), 0, 0
    );
  }

  // ============================================================
  // Ngồi thiền - Bắt chéo chân, 2 tay trên đùi
  // ============================================================
  private static poseMeditationSit(a: VRMAvatar, time: number, w: number): void {
    const breath = Math.sin(time * 0.8) * 0.01;

    // Hạ thấp spine (ngồi)
    a.spineBone.position.set(0, THREE.MathUtils.lerp(0.9, 0.45, w), 0);
    a.spineBone.rotation.x = breath;
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, 0.05, w); // Cúi nhẹ

    // Chân bắt chéo
    a.leftLeg.position.set(-0.16, THREE.MathUtils.lerp(0.85, 0.45, w), 0);
    a.rightLeg.position.set(0.16, THREE.MathUtils.lerp(0.85, 0.45, w), 0);

    a.leftLeg.rotation.set(
      THREE.MathUtils.lerp(0, -Math.PI / 2.2, w),
      THREE.MathUtils.lerp(0, 0.4, w), 0
    );
    a.rightLeg.rotation.set(
      THREE.MathUtils.lerp(0, -Math.PI / 2.2, w),
      THREE.MathUtils.lerp(0, -0.4, w), 0
    );
    a.leftKnee.rotation.x = THREE.MathUtils.lerp(0, Math.PI / 2.5, w);
    a.rightKnee.rotation.x = THREE.MathUtils.lerp(0, Math.PI / 2.5, w);

    // Tay đặt trên đùi (ấn quyết)
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.3, w), 0,
      THREE.MathUtils.lerp(0.08, 0.2, w)
    );
    a.leftElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -0.8, w), 0, 0);

    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.3, w), 0,
      THREE.MathUtils.lerp(-0.08, -0.2, w)
    );
    a.rightElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -0.8, w), 0, 0);
  }

  // ============================================================
  // Bay lơ lửng ngồi thiền - Như meditation_sit nhưng body nâng lên
  // ============================================================
  private static poseMeditationFloat(a: VRMAvatar, time: number, w: number): void {
    // Dùng pose ngồi thiền cơ bản
    this.poseMeditationSit(a, time, w);

    // Nâng toàn bộ body lên khỏi mặt đất + dao động nhẹ
    const floatY = Math.sin(time * 0.6) * 0.08 + 0.8;
    a.rootObject.position.y = THREE.MathUtils.lerp(
      a.config.spawn_point[1], a.config.spawn_point[1] + floatY, w
    );
  }

  // ============================================================
  // Cắm kiếm sau lưng, đứng hiên ngang
  // ============================================================
  private static poseSwordBehindBack(a: VRMAvatar, time: number, w: number): void {
    const breath = Math.sin(time * 1.2) * 0.01;
    a.spineBone.position.y = 0.9 + breath;
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.05, w);
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, -0.08, w);

    // Tay phải nắm chuôi kiếm (sau lưng vai phải)
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.3, w),
      THREE.MathUtils.lerp(0, -0.2, w),
      THREE.MathUtils.lerp(-0.08, -0.15, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -0.6, w), 0, 0
    );

    // Tay trái thả tự nhiên bên hông
    a.leftArm.rotation.set(0, 0, THREE.MathUtils.lerp(0.08, 0.12, w));
    a.leftElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -0.15, w), 0, 0);
  }

  // ============================================================
  // Bắt ấn quyết - 2 ngón tay trước mặt (thi triển phép)
  // ============================================================
  private static poseFingerSpell(a: VRMAvatar, time: number, w: number): void {
    const pulse = Math.sin(time * 3) * 0.03;

    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.05, w);

    // Tay phải giơ lên trước mặt, 2 ngón chỉ thẳng
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -1.3 + pulse, w), 0,
      THREE.MathUtils.lerp(-0.08, -0.3, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -0.8, w), 0, 0
    );

    // Tay trái bắt ấn phụ
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.9, w), 0,
      THREE.MathUtils.lerp(0.08, 0.4, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.2, w), 0, 0
    );
  }

  // ============================================================
  // Vận công - Nắm tay, cơ thể rung, tập trung năng lượng
  // ============================================================
  private static posePowerCharge(a: VRMAvatar, time: number, w: number): void {
    const shake = Math.sin(time * 25) * 0.008 * w; // Rung nhanh
    const pulse = Math.sin(time * 4) * 0.03;

    a.spineBone.position.y = 0.9 + pulse * w;
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, 0.1, w) + shake;

    // 2 tay nắm chặt ở 2 bên hông, khuỷu tay gập
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.6, w) + shake,
      0, THREE.MathUtils.lerp(0.08, 0.35, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.5, w), 0, 0
    );

    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.6, w) + shake,
      0, THREE.MathUtils.lerp(-0.08, -0.35, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.5, w), 0, 0
    );
  }

  // ============================================================
  // Tư thế chưởng lực - Đẩy tay phía trước
  // ============================================================
  private static posePalmStrike(a: VRMAvatar, time: number, w: number): void {
    a.spineBone.rotation.set(
      THREE.MathUtils.lerp(0, 0.15, w), 0,
      THREE.MathUtils.lerp(0, -0.1, w)
    );

    // Tay phải duỗi thẳng phía trước (đẩy chưởng)
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -1.4, w), 0,
      THREE.MathUtils.lerp(-0.08, -0.2, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -0.15, w), 0, 0
    );

    // Tay trái rút về hông (tích lực)
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.2, w), 0,
      THREE.MathUtils.lerp(0.08, 0.3, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.5, w), 0, 0
    );

    // Chân mở rộng
    a.leftLeg.rotation.x = THREE.MathUtils.lerp(0, -0.3, w);
    a.rightLeg.rotation.x = THREE.MathUtils.lerp(0, 0.15, w);
  }

  // ============================================================
  // Tư thế bay - Đạp kiếm / đứng trên mây
  // ============================================================
  private static poseFlyingStance(a: VRMAvatar, time: number, w: number): void {
    const sway = Math.sin(time * 1.5) * 0.05;

    a.spineBone.position.y = 0.9;
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.1, w) + sway * 0.3;

    // 2 tay hơi dang sang 2 bên, áo bay phấp phới
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.3, w) + sway,
      0, THREE.MathUtils.lerp(0.08, 0.45, w)
    );
    a.leftElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -0.25, w), 0, 0);

    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.3, w) - sway,
      0, THREE.MathUtils.lerp(-0.08, -0.45, w)
    );
    a.rightElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -0.25, w), 0, 0);

    // Chân khép, hơi cong gối
    a.leftLeg.rotation.x = THREE.MathUtils.lerp(0, -0.1, w);
    a.rightLeg.rotation.x = THREE.MathUtils.lerp(0, -0.05, w);
    a.leftKnee.rotation.x = THREE.MathUtils.lerp(0, 0.15, w);
    a.rightKnee.rotation.x = THREE.MathUtils.lerp(0, 0.1, w);
  }

  // ============================================================
  // Quỳ gối cúi đầu - Bái sư / Kính lễ
  // ============================================================
  private static poseKneelingBow(a: VRMAvatar, time: number, w: number): void {
    // Hạ thấp
    a.spineBone.position.set(0, THREE.MathUtils.lerp(0.9, 0.35, w), 0);
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, 0.5, w); // Cúi sâu
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, 0.3, w);

    // Chân quỳ
    a.leftLeg.position.set(-0.16, THREE.MathUtils.lerp(0.85, 0.35, w), 0);
    a.rightLeg.position.set(0.16, THREE.MathUtils.lerp(0.85, 0.35, w), 0);
    a.leftLeg.rotation.x = THREE.MathUtils.lerp(0, -Math.PI / 2, w);
    a.rightLeg.rotation.x = THREE.MathUtils.lerp(0, -Math.PI / 2, w);
    a.leftKnee.rotation.x = THREE.MathUtils.lerp(0, Math.PI / 1.8, w);
    a.rightKnee.rotation.x = THREE.MathUtils.lerp(0, Math.PI / 1.8, w);

    // Tay chắp trước ngực
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.9, w), 0,
      THREE.MathUtils.lerp(0.08, 0.5, w)
    );
    a.leftElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -1.2, w), 0, 0);

    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -0.9, w), 0,
      THREE.MathUtils.lerp(-0.08, -0.5, w)
    );
    a.rightElbow.rotation.set(THREE.MathUtils.lerp(-0.1, -1.2, w), 0, 0);
  }

  // ============================================================
  // Uống rượu hiệp khách - Nâng bình/ly lên miệng
  // ============================================================
  private static poseDrinkingWine(a: VRMAvatar, time: number, w: number): void {
    const sway = Math.sin(time * 0.8) * 0.02;

    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.08, w) + sway;
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, -0.2, w); // Ngẩng nhẹ

    // Tay phải nâng ly lên miệng
    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, -1.6, w), 0,
      THREE.MathUtils.lerp(-0.08, -0.3, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.8, w), 0, 0
    );

    // Tay trái chống hông
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.15, w), 0,
      THREE.MathUtils.lerp(0.08, 0.35, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.2, w), 0, 0
    );
  }

  // ============================================================
  // Đứng ngắm trời, tay chắp sau - Suy tư / Chiêm nghiệm
  // ============================================================
  private static poseContemplation(a: VRMAvatar, time: number, w: number): void {
    const breath = Math.sin(time * 0.7) * 0.012;

    a.spineBone.position.y = 0.9 + breath;
    a.spineBone.rotation.x = THREE.MathUtils.lerp(0, -0.06, w);
    a.headBone.rotation.x = THREE.MathUtils.lerp(0, -0.25, w); // Nhìn lên trời
    a.headBone.rotation.y = Math.sin(time * 0.3) * 0.05 * w;   // Quay nhẹ

    // Tay chắp sau lưng (nhẹ hơn hands_behind_back)
    a.leftArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.5, w),
      THREE.MathUtils.lerp(0, 0.2, w),
      THREE.MathUtils.lerp(0.08, 0.1, w)
    );
    a.leftElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.3, w), 0, 0
    );

    a.rightArm.rotation.set(
      THREE.MathUtils.lerp(0, 0.5, w),
      THREE.MathUtils.lerp(0, -0.2, w),
      THREE.MathUtils.lerp(-0.08, -0.1, w)
    );
    a.rightElbow.rotation.set(
      THREE.MathUtils.lerp(-0.1, -1.3, w), 0, 0
    );
  }
}
