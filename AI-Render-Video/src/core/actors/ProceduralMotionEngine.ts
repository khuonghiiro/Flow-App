import * as THREE from 'three';

export interface MotionItem {
  id: string;
  category: 'female' | 'male' | 'universal';
  label: string;
  icon: string;
  desc: string;
  speedMultiplier?: number;
}

export class ProceduralMotionEngine {
  public static readonly MOTION_LIBRARY: MotionItem[] = [
    // 🌸 1. NHÓM ĐỘNG TÁC NỮ TÍNH & ANIME (FEMALE / ELEGANT / ANIME)
    {
      id: 'female_idle',
      category: 'female',
      label: 'Dáng Nữ Đứng Duyên Dáng',
      icon: '🌸',
      desc: 'Khép chân tinh tế, hai tay giữ nhẹ trước bụng, nghiêng đầu duyên dáng & thở êm',
    },
    {
      id: 'female_walk',
      category: 'female',
      label: 'Bước Đi Nữ Tính Catwalk',
      icon: '👠',
      desc: 'Bước chân thẳng hàng, hông lắc nhịp nhàng uyển chuyển, tay vung mềm mại chuẩn Runway',
    },
    {
      id: 'female_run',
      category: 'female',
      label: 'Chạy Nữ Anime Đáng Yêu',
      icon: '🎀',
      desc: 'Sải bước nhanh nhẹn, hai tay co nhẹ hoặc vung kiểu anime dễ thương',
    },
    {
      id: 'female_dance',
      category: 'female',
      label: 'Vũ Đạo Idol K-Pop / Anime',
      icon: '💃',
      desc: 'Uốn lượn sóng hông, đánh nhịp vai và vung tay điệu đà sôi động',
    },
    {
      id: 'female_cast_spell',
      category: 'female',
      label: 'Niệm Phép Tiên Nữ Ma Thuật',
      icon: '✨',
      desc: 'Chắp tay tụ hào quang ma pháp, nâng tay vòng cung & ngửa thân thanh thoát kiêu sa',
    },
    {
      id: 'female_wave',
      category: 'female',
      label: 'Nữ Vẫy Tay E Thẹn',
      icon: '👋',
      desc: 'Nghiêng đầu nhẹ nhàng, vẫy tay nhỏ nhắn đáng yêu & mỉm cười thân thiện',
    },
    {
      id: 'female_sit',
      category: 'female',
      label: 'Ngồi Bắt Chéo Chân Duyên Dáng',
      icon: '🪑',
      desc: 'Khép gối bắt chéo chân tinh tế, hai tay đặt trên đùi lịch thiệp',
    },
    {
      id: 'female_archer',
      category: 'female',
      label: 'Thế Nữ Cung Thủ Giương Cung',
      icon: '🏹',
      desc: 'Xoay thân nghiêng, giương cung ngắm bắn dứt khoát với tư thế thanh lịch',
    },

    // ⚔️ 2. NHÓM ĐỘNG TÁC NAM TÍNH & CHIẾN BINH (MALE / HEROIC / COMBAT)
    {
      id: 'male_idle',
      category: 'male',
      label: 'Dáng Nam Đứng Vững Chãi',
      icon: '🛡️',
      desc: 'Chân rộng bằng vai, ngực nở kiêu hùng, hai tay buông lỏng hoặc nắm nhẹ oai nghiêm',
    },
    {
      id: 'male_walk',
      category: 'male',
      label: 'Sải Bước Oai Phong (Power Walk)',
      icon: '🚶‍♂️',
      desc: 'Bước dài dứt khoát, vai đánh nhịp uy lực, trọng tâm đầm chắc chuẩn chiến binh',
    },
    {
      id: 'male_run',
      category: 'male',
      label: 'Chạy Nhanh Chiến Binh (Combat Sprint)',
      icon: '🏃‍♂️',
      desc: 'Đổ người về trước, tay đánh mạnh, gập gối tạo lực bứt tốc dũng mãnh',
    },
    {
      id: 'male_slash',
      category: 'male',
      label: 'Trảm Kích Kiếm Pháp Uy Lực',
      icon: '⚔️',
      desc: 'Tích lực chém kiếm 4 giai đoạn uy lực cao, xoay hông dồn trọng tâm bộc phá',
    },
    {
      id: 'male_guard',
      category: 'male',
      label: 'Thế Thủ Quyền Cước Võ Thuật',
      icon: '🥊',
      desc: 'Hạ trọng tâm, hai tay thủ thế che cằm, nhấp nhổm chuyển trụ linh hoạt',
    },
    {
      id: 'male_charge',
      category: 'male',
      label: 'Tụ Khí Gồng Lực Chấn Động',
      icon: '🔥',
      desc: 'Tấn trụ vững, hai tay gồng tụ kình khí bộc phá sức mạnh cơ bắp',
    },
    {
      id: 'male_salute',
      category: 'male',
      label: 'Bái Quyền Kỵ Sĩ / Hiệp Sĩ',
      icon: '🤝',
      desc: 'Chắp tay bái quyền trang trọng chuẩn võ hiệp & kỵ sĩ hoàng gia',
    },
    {
      id: 'male_sit',
      category: 'male',
      label: 'Ngồi Tướng Quân Bệ Vệ',
      icon: '👑',
      desc: 'Mở rộng hai gối, hai tay chống gối hoặc đặt trên vũ khí oai phong lẫm liệt',
    },

    // 🎭 3. NHÓM CHUNG & HÀNH ĐỘNG (UNIVERSAL & EMOTES)
    {
      id: 'victory_jump',
      category: 'universal',
      label: 'Nhảy Mừng Chiến Thắng',
      icon: '🎉',
      desc: 'Nhún người bật nhảy cao, giơ hai tay chữ V ăn mừng phấn khích',
    },
    {
      id: 'exhausted',
      category: 'universal',
      label: 'Thở Dốc Hổn Hển Mệt Mỏi',
      icon: '😮‍💨',
      desc: 'Gập người chống hai tay lên gối, lồng ngực phập phồng thở dốc',
    },
    {
      id: 't_pose',
      category: 'universal',
      label: 'T-Pose (Khung Chuẩn)',
      icon: '🤸',
      desc: 'Tư thế đứng chữ T chuẩn kỹ thuật để kiểm tra và gắn xương',
    },
  ];

  /**
   * Áp dụng cử động trực tiếp lên các khớp xương giải phẫu học
   */
  public static applyMotion(
    bonesMap: Map<string, THREE.Bone>,
    poseId: string,
    progress: number = 0
  ): void {
    // 1. Reset các khớp chính về trạng thái ban đầu an toàn
    for (const bone of bonesMap.values()) {
      const initR = bone.userData.initialRotation;
      if (initR instanceof THREE.Euler) {
        bone.rotation.copy(initR);
      } else if (initR && typeof initR.x === 'number') {
        bone.rotation.set(initR.x, initR.y, initR.z, initR.order || 'XYZ');
      } else if (initR && typeof (initR as any)._x === 'number') {
        bone.rotation.set(
          (initR as any)._x,
          (initR as any)._y,
          (initR as any)._z,
          (initR as any)._order || 'XYZ'
        );
      } else {
        bone.rotation.set(0, 0, 0);
      }

      const initP = bone.userData.initialPosition;
      if (initP instanceof THREE.Vector3) {
        bone.position.copy(initP);
      } else if (initP && typeof initP.x === 'number') {
        bone.position.set(initP.x, initP.y, initP.z);
      }
    }

    if (poseId === 't_pose') return;

    const t = progress * Math.PI * 2;

    const rot = (jointName: string, dx: number, dy: number, dz: number) => {
      const bone = bonesMap.get(jointName);
      if (!bone) return;
      const init = bone.userData.initialRotation;
      const ix = init ? (typeof init.x === 'number' ? init.x : (init as any)._x || 0) : 0;
      const iy = init ? (typeof init.y === 'number' ? init.y : (init as any)._y || 0) : 0;
      const iz = init ? (typeof init.z === 'number' ? init.z : (init as any)._z || 0) : 0;
      bone.rotation.set(ix + dx, iy + dy, iz + dz);
    };

    const trans = (jointName: string, dx: number, dy: number, dz: number) => {
      const bone = bonesMap.get(jointName);
      if (!bone) return;
      const init = bone.userData.initialPosition;
      const ix = init ? (typeof init.x === 'number' ? init.x : 0) : 0;
      const iy = init ? (typeof init.y === 'number' ? init.y : 0) : 0;
      const iz = init ? (typeof init.z === 'number' ? init.z : 0) : 0;
      bone.position.set(ix + dx, iy + dy, iz + dz);
    };

    // Ánh xạ các tên pose cũ (backward compatibility)
    const normalizedPose = this.normalizePoseId(poseId);

    switch (normalizedPose) {
      // 🌸 Female Poses
      case 'female_idle':
        this.femaleIdle(rot, trans, t);
        break;
      case 'female_walk':
        this.femaleWalk(rot, trans, t);
        break;
      case 'female_run':
        this.femaleRun(rot, trans, t);
        break;
      case 'female_dance':
        this.femaleDance(rot, trans, t);
        break;
      case 'female_cast_spell':
        this.femaleCastSpell(rot, trans, t);
        break;
      case 'female_wave':
        this.femaleWave(rot, trans, t);
        break;
      case 'female_sit':
        this.femaleSit(rot, trans, t);
        break;
      case 'female_archer':
        this.femaleArcher(rot, trans, t);
        break;

      // ⚔️ Male Poses
      case 'male_idle':
        this.maleIdle(rot, trans, t);
        break;
      case 'male_walk':
        this.maleWalk(rot, trans, t);
        break;
      case 'male_run':
        this.maleRun(rot, trans, t);
        break;
      case 'male_slash':
        this.maleSlash(rot, trans, progress);
        break;
      case 'male_guard':
        this.maleGuard(rot, trans, t);
        break;
      case 'male_charge':
        this.maleCharge(rot, trans, t);
        break;
      case 'male_salute':
        this.maleSalute(rot, trans, t);
        break;
      case 'male_sit':
        this.maleSit(rot, trans, t);
        break;

      // 🎭 Universal Poses
      case 'victory_jump':
        this.victoryJump(rot, trans, progress);
        break;
      case 'exhausted':
        this.exhausted(rot, trans, t);
        break;

      default:
        this.femaleIdle(rot, trans, t);
        break;
    }

    // Force GPU matrix update for all transformed bones
    for (const bone of bonesMap.values()) {
      bone.updateMatrix();
      bone.updateMatrixWorld(true);
    }
  }

  private static normalizePoseId(id: string): string {
    switch (id) {
      case 'idle':
        return 'female_idle';
      case 'walk':
        return 'female_walk';
      case 'run':
        return 'female_run';
      case 'slash':
        return 'male_slash';
      case 'cast_spell':
        return 'female_cast_spell';
      case 'defend':
        return 'male_guard';
      case 'dance':
        return 'female_dance';
      case 'wave':
        return 'female_wave';
      case 'sit':
        return 'female_sit';
      default:
        return id;
    }
  }

  // ==========================================
  // 🌸 FEMALE KINEMATICS IMPLEMENTATION
  // ==========================================

  private static femaleIdle(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t * 1.6) * 0.035;
    const hipSway = Math.sin(t * 0.8) * 0.025;

    trans('Hips', hipSway * 0.03, Math.sin(t * 1.6) * 0.006, 0);
    rot('Hips', 0.02, hipSway * 0.06, hipSway * 0.04);
    rot('Spine', 0.02 + breathe, -hipSway * 0.05, -hipSway * 0.02);
    rot('Chest', 0.03 + breathe * 1.1, -hipSway * 0.03, 0);
    rot('Neck', -0.01 - breathe * 0.2, 0, 0);
    rot('Head', 0.02 - breathe * 0.3, -hipSway * 0.04, 0.04 + hipSway * 0.03); // Nghiêng đầu duyên dáng

    // Hai tay khép nhẹ trước bụng
    rot('LeftShoulder', 0, 0, breathe * 0.02);
    rot('LeftUpperArm', 0.25 + breathe * 0.1, 0.15, -0.22);
    rot('LeftLowerArm', -0.85 - breathe * 0.1, 0.25, 0);
    rot('LeftHand', 0.15, 0.1, 0);

    rot('RightShoulder', 0, 0, -breathe * 0.02);
    rot('RightUpperArm', 0.25 + breathe * 0.1, -0.15, 0.22);
    rot('RightLowerArm', -0.85 - breathe * 0.1, -0.25, 0);
    rot('RightHand', 0.15, -0.1, 0);

    // Chân khép tinh tế
    rot('LeftUpperLeg', -0.02, 0.04, 0.02);
    rot('LeftLowerLeg', 0.03, 0, 0);
    rot('RightUpperLeg', -0.02, -0.04, -0.02);
    rot('RightLowerLeg', 0.03, 0, 0);
  }

  private static femaleWalk(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsY = -Math.abs(Math.sin(t)) * 0.028;
    const hipsX = Math.sin(t) * 0.032;
    const hipsYaw = -Math.sin(t) * 0.16;
    const hipsRoll = Math.sin(t) * 0.08; // Lắc hông nữ tính

    trans('Hips', hipsX, hipsY, 0);
    rot('Hips', 0.03, hipsYaw, hipsRoll);
    rot('Spine', 0.02, -hipsYaw * 0.7, -hipsRoll * 0.7);
    rot('Chest', 0.03, -hipsYaw * 0.8, -hipsRoll * 0.5);
    rot('Neck', 0, hipsYaw * 0.3, 0);
    rot('Head', -0.02, hipsYaw * 0.2, -hipsRoll * 0.4);

    const legL = Math.sin(t) * 0.42;
    const kneeL = Math.max(0, -Math.sin(t - 0.2)) * 0.85 + 0.05;
    const footL = Math.sin(t - 0.45) * 0.3;
    rot('LeftUpperLeg', legL, 0.03, -hipsRoll * 0.4);
    rot('LeftLowerLeg', -kneeL, 0, 0);
    rot('LeftFoot', footL, 0, 0);

    const legR = -legL;
    const kneeR = Math.max(0, Math.sin(t - 0.2)) * 0.85 + 0.05;
    const footR = -footL;
    rot('RightUpperLeg', legR, -0.03, hipsRoll * 0.4);
    rot('RightLowerLeg', -kneeR, 0, 0);
    rot('RightFoot', footR, 0, 0);

    const armL = legR * 0.65;
    rot('LeftUpperArm', armL, 0.08, -0.18);
    rot('LeftLowerArm', -0.35 - Math.max(0, armL) * 0.35, 0, 0);

    const armR = legL * 0.65;
    rot('RightUpperArm', armR, -0.08, 0.18);
    rot('RightLowerArm', -0.35 - Math.max(0, armR) * 0.35, 0, 0);
  }

  private static femaleRun(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsY = -Math.abs(Math.sin(t)) * 0.05;
    trans('Hips', Math.sin(t) * 0.025, hipsY, 0);
    rot('Hips', 0.15, -Math.sin(t) * 0.14, Math.sin(t) * 0.05);
    rot('Spine', 0.08, Math.sin(t) * 0.10, -Math.sin(t) * 0.03);
    rot('Chest', 0.06, Math.sin(t) * 0.12, 0);
    rot('Head', -0.12, -Math.sin(t) * 0.04, 0);

    const legL = Math.sin(t) * 0.75;
    const kneeL = Math.max(0, -Math.sin(t - 0.2)) * 1.35 + 0.12;
    rot('LeftUpperLeg', legL, 0.04, -0.04);
    rot('LeftLowerLeg', -kneeL, 0, 0);
    rot('LeftFoot', Math.sin(t - 0.4) * 0.35, 0, 0);

    const legR = -legL;
    const kneeR = Math.max(0, Math.sin(t - 0.2)) * 1.35 + 0.12;
    rot('RightUpperLeg', legR, -0.04, 0.04);
    rot('RightLowerLeg', -kneeR, 0, 0);
    rot('RightFoot', -Math.sin(t - 0.4) * 0.35, 0, 0);

    // Tay co nhẹ kiểu anime đáng yêu
    const armL = -legL * 0.55;
    rot('LeftUpperArm', 0.2 + armL * 0.4, 0.15, -0.32);
    rot('LeftLowerArm', -1.25, 0.2, 0);

    const armR = -legR * 0.55;
    rot('RightUpperArm', 0.2 + armR * 0.4, -0.15, 0.32);
    rot('RightLowerArm', -1.25, -0.2, 0);
  }

  private static femaleDance(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsX = Math.sin(t * 2) * 0.06;
    const hipsY = -Math.abs(Math.sin(t * 2)) * 0.04;
    trans('Hips', hipsX, hipsY, 0);
    rot('Hips', 0.04, Math.sin(t) * 0.30, -Math.sin(t * 2) * 0.20);
    rot('Spine', 0.03, -Math.sin(t) * 0.24, Math.sin(t * 2) * 0.15);
    rot('Chest', 0.06, -Math.sin(t) * 0.18, Math.sin(t * 2) * 0.10);
    rot('Head', -0.04, Math.sin(t) * 0.18, -Math.sin(t * 2) * 0.12);

    rot('LeftUpperLeg', Math.sin(t) * 0.28, 0, -0.12 - Math.sin(t * 2) * 0.1);
    rot('LeftLowerLeg', Math.abs(Math.sin(t)) * 0.5, 0, 0);
    rot('RightUpperLeg', -Math.sin(t) * 0.28, 0, 0.12 + Math.sin(t * 2) * 0.1);
    rot('RightLowerLeg', Math.abs(Math.cos(t)) * 0.5, 0, 0);

    rot('LeftUpperArm', -0.65 + Math.sin(t) * 0.5, 0.25, -0.55 + Math.cos(t) * 0.4);
    rot('LeftLowerArm', -0.95 + Math.sin(t * 2) * 0.45, 0, 0);
    rot('RightUpperArm', -0.65 - Math.sin(t) * 0.5, -0.25, 0.55 - Math.cos(t) * 0.4);
    rot('RightLowerArm', -0.95 - Math.sin(t * 2) * 0.45, 0, 0);
  }

  private static femaleCastSpell(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    trans('Hips', 0, 0.04 + Math.sin(t) * 0.03, 0);
    rot('Hips', -0.06, Math.sin(t * 0.6) * 0.06, 0);
    rot('Spine', -0.12 + Math.sin(t) * 0.03, 0, 0);
    rot('Chest', -0.15 + Math.sin(t) * 0.04, 0, 0);
    rot('Head', -0.22, 0, 0.04);

    rot('LeftUpperLeg', 0.2, 0.04, -0.08);
    rot('LeftLowerLeg', -0.4, 0, 0);
    rot('RightUpperLeg', 0.12, -0.04, 0.08);
    rot('RightLowerLeg', -0.3, 0, 0);

    rot('LeftUpperArm', -0.95 + Math.sin(t) * 0.18, 0.4, -0.55 + Math.cos(t) * 0.12);
    rot('LeftLowerArm', -0.75 + Math.cos(t) * 0.2, 0.3, 0);
    rot('RightUpperArm', -0.95 + Math.sin(t) * 0.18, -0.4, 0.55 - Math.cos(t) * 0.12);
    rot('RightLowerArm', -0.75 + Math.cos(t) * 0.2, -0.3, 0);
  }

  private static femaleWave(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const wave = Math.sin(t * 3.8) * 0.38;
    const breathe = Math.sin(t) * 0.03;

    trans('Hips', -0.02, -0.01, 0);
    rot('Hips', 0.02, 0.06, -0.03);
    rot('Spine', 0.03 + breathe, -0.05, 0.02);
    rot('Chest', 0.04 + breathe, -0.04, 0.02);
    rot('Head', 0.03, -0.1, 0.15); // Nghiêng đầu e thẹn

    rot('RightShoulder', 0.12, 0, -0.08);
    rot('RightUpperArm', -1.5, 0.3, 0.65);
    rot('RightLowerArm', -0.7, wave * 0.8, 0.15);
    rot('RightHand', 0.1, wave * 0.85, 0.1);

    rot('LeftUpperArm', 0.25, 0.1, -0.22);
    rot('LeftLowerArm', -0.75, 0.15, 0);
  }

  private static femaleSit(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t) * 0.03;
    trans('Hips', 0, -0.45, 0);
    rot('Hips', -0.06, 0, 0);
    rot('Spine', 0.05 + breathe, 0, 0);
    rot('Chest', 0.04 + breathe * 0.8, 0, 0);
    rot('Head', 0.02, 0, 0.03);

    // Chân khép bắt chéo nhẹ
    rot('LeftUpperLeg', -1.55, 0.1, 0.06);
    rot('LeftLowerLeg', 1.55, -0.08, 0);
    rot('RightUpperLeg', -1.55, -0.06, -0.08);
    rot('RightLowerLeg', 1.55, 0.06, 0);

    rot('LeftUpperArm', -0.15, 0.15, -0.2);
    rot('LeftLowerArm', -0.75, 0.25, 0);
    rot('RightUpperArm', -0.15, -0.15, 0.2);
    rot('RightLowerArm', -0.75, -0.25, 0);
  }

  private static femaleArcher(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breath = Math.sin(t * 1.5) * 0.02;
    trans('Hips', 0, -0.04, 0);
    rot('Hips', 0.05, 0.75, 0);
    rot('Spine', 0.06 + breath, 0.65, 0);
    rot('Chest', 0.04 + breath, 0.55, 0);
    rot('Head', 0, -0.85, 0); // Nhìn thẳng hướng bia ngắm

    rot('LeftUpperLeg', -0.3, 0.2, -0.15);
    rot('LeftLowerLeg', 0.45, 0, 0);
    rot('RightUpperLeg', 0.25, -0.2, 0.15);
    rot('RightLowerLeg', 0.35, 0, 0);

    // Tay trái giữ cung thẳng
    rot('LeftShoulder', 0.1, 0.2, 0);
    rot('LeftUpperArm', -1.45, 0.15, -0.25);
    rot('LeftLowerArm', -0.15, 0, 0);
    rot('LeftHand', 0.2, 0, 0);

    // Tay phải kéo dây cung sát mang tai
    rot('RightShoulder', 0.15, -0.1, 0.15);
    rot('RightUpperArm', -1.45, -0.45, 0.85);
    rot('RightLowerArm', -1.75, -0.2, 0);
    rot('RightHand', 0.35, 0, 0);
  }

  // ==========================================
  // ⚔️ MALE KINEMATICS IMPLEMENTATION
  // ==========================================

  private static maleIdle(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t * 1.2) * 0.045;
    const weightShift = Math.sin(t * 0.5) * 0.02;

    trans('Hips', weightShift * 0.04, Math.sin(t * 1.2) * 0.008, 0);
    rot('Hips', 0.03, weightShift * 0.06, weightShift * 0.02);
    rot('Spine', 0.04 + breathe, -weightShift * 0.04, 0);
    rot('Chest', 0.06 + breathe * 1.3, -weightShift * 0.03, 0); // Ngực nở kiêu hùng
    rot('Head', 0.02 - breathe * 0.3, -weightShift * 0.04, 0);

    // Hai tay buông lỏng tự nhiên, vai rộng
    rot('LeftShoulder', 0.05, 0, 0.04);
    rot('LeftUpperArm', 0.08 + breathe * 0.15, 0.02, -0.16);
    rot('LeftLowerArm', -0.25 - breathe * 0.1, 0, 0);

    rot('RightShoulder', 0.05, 0, -0.04);
    rot('RightUpperArm', 0.08 + breathe * 0.15, -0.02, 0.16);
    rot('RightLowerArm', -0.25 - breathe * 0.1, 0, 0);

    // Chân mở rộng bằng vai vững chãi
    rot('LeftUpperLeg', -0.02, 0, -0.08);
    rot('LeftLowerLeg', 0.04, 0, 0);
    rot('RightUpperLeg', -0.02, 0, 0.08);
    rot('RightLowerLeg', 0.04, 0, 0);
  }

  private static maleWalk(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsY = -Math.abs(Math.sin(t)) * 0.04;
    const hipsX = Math.sin(t) * 0.025;
    const hipsYaw = -Math.sin(t) * 0.12;

    trans('Hips', hipsX, hipsY, 0);
    rot('Hips', 0.05, hipsYaw, Math.sin(t) * 0.03);
    rot('Spine', 0.04, -hipsYaw * 0.6, 0);
    rot('Chest', 0.05, -hipsYaw * 0.7, 0);
    rot('Head', -0.03, hipsYaw * 0.2, 0);

    const legL = Math.sin(t) * 0.52;
    const kneeL = Math.max(0, -Math.sin(t - 0.25)) * 1.05 + 0.05;
    rot('LeftUpperLeg', legL, 0, -0.06);
    rot('LeftLowerLeg', -kneeL, 0, 0);
    rot('LeftFoot', Math.sin(t - 0.5) * 0.38, 0, 0);

    const legR = -legL;
    const kneeR = Math.max(0, Math.sin(t - 0.25)) * 1.05 + 0.05;
    rot('RightUpperLeg', legR, 0, 0.06);
    rot('RightLowerLeg', -kneeR, 0, 0);
    rot('RightFoot', -Math.sin(t - 0.5) * 0.38, 0, 0);

    const armL = legR * 0.85;
    rot('LeftShoulder', 0, armL * 0.18, 0);
    rot('LeftUpperArm', armL, 0.05, -0.15);
    rot('LeftLowerArm', -0.25 - Math.max(0, armL) * 0.45, 0, 0);

    const armR = legL * 0.85;
    rot('RightShoulder', 0, armR * 0.18, 0);
    rot('RightUpperArm', armR, -0.05, 0.15);
    rot('RightLowerArm', -0.25 - Math.max(0, armR) * 0.45, 0, 0);
  }

  private static maleRun(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsY = -Math.abs(Math.sin(t)) * 0.07;
    trans('Hips', Math.sin(t) * 0.04, hipsY, 0);
    rot('Hips', 0.24, -Math.sin(t) * 0.2, Math.sin(t) * 0.07);
    rot('Spine', 0.14, Math.sin(t) * 0.14, -Math.sin(t) * 0.04);
    rot('Chest', 0.12, Math.sin(t) * 0.16, 0);
    rot('Head', -0.2, -Math.sin(t) * 0.06, 0);

    const legL = Math.sin(t) * 0.95;
    const kneeL = Math.max(0, -Math.sin(t - 0.2)) * 1.65 + 0.15;
    rot('LeftUpperLeg', legL, 0, -0.06);
    rot('LeftLowerLeg', -kneeL, 0, 0);
    rot('LeftFoot', Math.sin(t - 0.4) * 0.5, 0, 0);

    const legR = -legL;
    const kneeR = Math.max(0, Math.sin(t - 0.2)) * 1.65 + 0.15;
    rot('RightUpperLeg', legR, 0, 0.06);
    rot('RightLowerLeg', -kneeR, 0, 0);
    rot('RightFoot', -Math.sin(t - 0.4) * 0.5, 0, 0);

    const armL = -legL * 1.05;
    rot('LeftUpperArm', armL, 0.12, -0.18);
    rot('LeftLowerArm', -1.45, 0, 0);

    const armR = -legR * 1.05;
    rot('RightUpperArm', armR, -0.12, 0.18);
    rot('RightLowerArm', -1.45, 0, 0);
  }

  private static maleSlash(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    progress: number
  ): void {
    const p = progress;
    if (p < 0.32) {
      const k = p / 0.32;
      trans('Hips', 0.04 * k, -0.06 * k, -0.05 * k);
      rot('Hips', 0.05 * k, -0.45 * k, 0);
      rot('Spine', 0.08 * k, -0.55 * k, 0);
      rot('Chest', 0.06 * k, -0.45 * k, 0);
      rot('Head', 0.04, 0.35 * k, 0);

      rot('RightShoulder', 0.15 * k, 0, 0.15 * k);
      rot('RightUpperArm', -1.2 * k, 0.4 * k, 0.6 * k);
      rot('RightLowerArm', -1.4 * k, 0, 0);
      rot('LeftUpperArm', 0.3 * k, 0, -0.4 * k);
      rot('LeftLowerArm', -0.6 * k, 0, 0);

      rot('LeftUpperLeg', -0.2 * k, 0, -0.15 * k);
      rot('LeftLowerLeg', 0.3 * k, 0, 0);
      rot('RightUpperLeg', 0.25 * k, 0, 0.15 * k);
      rot('RightLowerLeg', 0.2 * k, 0, 0);
    } else if (p < 0.52) {
      const k = (p - 0.32) / 0.2;
      trans('Hips', 0.04 - 0.08 * k, -0.06 - 0.02 * k, -0.05 + 0.13 * k);
      rot('Hips', 0.05, -0.45 + 0.95 * k, 0);
      rot('Spine', 0.08 + 0.08 * k, -0.55 + 1.2 * k, 0);
      rot('Chest', 0.06 + 0.06 * k, -0.45 + 1.0 * k, 0);
      rot('Head', 0.04, 0.35 - 0.5 * k, 0);

      rot('RightUpperArm', -1.2 + 1.8 * k, 0.4 - 0.8 * k, 0.6 - 1.1 * k);
      rot('RightLowerArm', -1.4 + 0.9 * k, 0, 0);
      rot('RightHand', 0.6 * k, 0, 0);
      rot('LeftUpperArm', 0.3 - 0.5 * k, 0, -0.4 + 0.2 * k);

      rot('LeftUpperLeg', -0.2 - 0.25 * k, 0, -0.15);
      rot('LeftLowerLeg', 0.3 + 0.25 * k, 0, 0);
      rot('RightUpperLeg', 0.25 - 0.1 * k, 0, 0.15);
      rot('RightLowerLeg', 0.2 - 0.3 * k, 0, 0);
    } else {
      const k = (p - 0.52) / 0.48;
      const settle = 1 - k;
      trans('Hips', -0.04 * settle, -0.08 * settle, 0.08 * settle);
      rot('Hips', 0.05 * settle, 0.5 * settle, 0);
      rot('Spine', 0.16 * settle, 0.65 * settle, 0);
      rot('Chest', 0.12 * settle, 0.55 * settle, 0);

      rot('RightUpperArm', 0.6 * settle, -0.4 * settle, -0.5 * settle);
      rot('RightLowerArm', -0.5 * settle, 0, 0);
      rot('RightHand', 0.6 * settle, 0, 0);

      rot('LeftUpperLeg', -0.45 * settle, 0, -0.15 * settle);
      rot('LeftLowerLeg', 0.55 * settle, 0, 0);
      rot('RightUpperLeg', 0.15 * settle, 0, 0.15 * settle);
      rot('RightLowerLeg', -0.1 * settle, 0, 0);
    }
  }

  private static maleGuard(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t * 2) * 0.03;
    const bob = Math.abs(Math.sin(t * 2)) * 0.02;

    trans('Hips', 0, -0.09 - bob, 0);
    rot('Hips', 0.14, 0.28, 0);
    rot('Spine', 0.16 + breathe, -0.14, 0);
    rot('Chest', 0.12 + breathe, -0.12, 0);
    rot('Head', -0.14, -0.16, 0);

    rot('LeftUpperLeg', -0.48, 0.18, -0.25);
    rot('LeftLowerLeg', 0.7, 0, 0);
    rot('LeftFoot', -0.18, 0, 0);
    rot('RightUpperLeg', 0.28, -0.18, 0.25);
    rot('RightLowerLeg', 0.6, 0, 0);
    rot('RightFoot', 0.18, 0, 0);

    rot('LeftShoulder', 0.14, 0, 0.12);
    rot('LeftUpperArm', -0.8 + breathe, 0.48, -0.3);
    rot('LeftLowerArm', -1.65, 0.38, 0);
    rot('LeftHand', 0.4, 0.15, 0);

    rot('RightShoulder', 0.14, 0, -0.12);
    rot('RightUpperArm', -0.7 + breathe, -0.42, 0.35);
    rot('RightLowerArm', -1.75, -0.38, 0);
    rot('RightHand', 0.4, -0.15, 0);
  }

  private static maleCharge(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const vibration = Math.sin(t * 12) * 0.015;
    trans('Hips', 0, -0.12, 0);
    rot('Hips', 0.15, 0, 0);
    rot('Spine', 0.2 + vibration, 0, 0);
    rot('Chest', 0.18 + vibration, 0, 0);
    rot('Head', -0.22, 0, 0);

    rot('LeftUpperLeg', -0.5, 0, -0.3);
    rot('LeftLowerLeg', 0.75, 0, 0);
    rot('RightUpperLeg', -0.5, 0, 0.3);
    rot('RightLowerLeg', 0.75, 0, 0);

    // Gồng hai nắm đấm
    rot('LeftShoulder', 0.2, 0, 0.15);
    rot('LeftUpperArm', -0.4, 0.3, -0.55 + vibration * 2);
    rot('LeftLowerArm', -1.6, 0.4, 0);
    rot('RightShoulder', 0.2, 0, -0.15);
    rot('RightUpperArm', -0.4, -0.3, 0.55 - vibration * 2);
    rot('RightLowerArm', -1.6, -0.4, 0);
  }

  private static maleSalute(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t) * 0.03;
    trans('Hips', 0, 0, 0);
    rot('Hips', 0.02, 0, 0);
    rot('Spine', 0.04 + breathe, 0, 0);
    rot('Chest', 0.06 + breathe, 0, 0);
    rot('Head', 0.02, 0, 0);

    rot('LeftUpperLeg', -0.02, 0, -0.06);
    rot('LeftLowerLeg', 0.03, 0, 0);
    rot('RightUpperLeg', -0.02, 0, 0.06);
    rot('RightLowerLeg', 0.03, 0, 0);

    // Chắp tay bái quyền trước ngực
    rot('RightShoulder', 0.15, 0, -0.1);
    rot('RightUpperArm', -0.85, -0.35, 0.65);
    rot('RightLowerArm', -1.65, -0.3, 0);
    rot('RightHand', 0.3, 0, 0);

    rot('LeftShoulder', 0.15, 0, 0.1);
    rot('LeftUpperArm', -0.85, 0.35, -0.65);
    rot('LeftLowerArm', -1.65, 0.3, 0);
    rot('LeftHand', 0.3, 0, 0);
  }

  private static maleSit(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t) * 0.03;
    trans('Hips', 0, -0.45, 0);
    rot('Hips', -0.08, 0, 0);
    rot('Spine', 0.06 + breathe, 0, 0);
    rot('Chest', 0.05 + breathe * 0.8, 0, 0);
    rot('Head', 0.02, 0, 0);

    // Chân mở rộng bệ vệ
    rot('LeftUpperLeg', -1.57, 0, -0.35);
    rot('LeftLowerLeg', 1.57, 0, 0);
    rot('RightUpperLeg', -1.57, 0, 0.35);
    rot('RightLowerLeg', 1.57, 0, 0);

    rot('LeftUpperArm', -0.3, 0, -0.3);
    rot('LeftLowerArm', -0.75, 0, 0);
    rot('RightUpperArm', -0.3, 0, 0.3);
    rot('RightLowerArm', -0.75, 0, 0);
  }

  // ==========================================
  // 🎭 UNIVERSAL & EMOTES IMPLEMENTATION
  // ==========================================

  private static victoryJump(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    progress: number
  ): void {
    const p = progress;
    if (p < 0.25) {
      // Nhún người lấy đà
      const k = p / 0.25;
      trans('Hips', 0, -0.15 * k, 0);
      rot('Hips', 0.2 * k, 0, 0);
      rot('Spine', 0.15 * k, 0, 0);
      rot('LeftUpperLeg', -0.5 * k, 0, -0.1);
      rot('LeftLowerLeg', 0.8 * k, 0, 0);
      rot('RightUpperLeg', -0.5 * k, 0, 0.1);
      rot('RightLowerLeg', 0.8 * k, 0, 0);
      rot('LeftUpperArm', 0.3 * k, 0, -0.2);
      rot('RightUpperArm', 0.3 * k, 0, 0.2);
    } else if (p < 0.65) {
      // Bật nhảy lên cao
      const k = (p - 0.25) / 0.4;
      const jumpArc = Math.sin(k * Math.PI);
      trans('Hips', 0, 0.35 * jumpArc, 0);
      rot('Hips', -0.1 * jumpArc, 0, 0);
      rot('Spine', -0.1 * jumpArc, 0, 0);
      rot('LeftUpperLeg', 0.3 * jumpArc, 0, -0.2);
      rot('LeftLowerLeg', -0.4 * jumpArc, 0, 0);
      rot('RightUpperLeg', 0.1 * jumpArc, 0, 0.2);
      rot('RightLowerLeg', -0.5 * jumpArc, 0, 0);

      // Giơ hai tay chữ V
      rot('LeftUpperArm', -1.75 * jumpArc, 0, -0.65 * jumpArc);
      rot('LeftLowerArm', -0.2 * jumpArc, 0, 0);
      rot('RightUpperArm', -1.75 * jumpArc, 0, 0.65 * jumpArc);
      rot('RightLowerArm', -0.2 * jumpArc, 0, 0);
    } else {
      // Tiếp đất
      const k = (p - 0.65) / 0.35;
      const settle = 1 - k;
      trans('Hips', 0, -0.08 * settle, 0);
      rot('LeftUpperLeg', -0.2 * settle, 0, 0);
      rot('LeftLowerLeg', 0.3 * settle, 0, 0);
      rot('RightUpperLeg', -0.2 * settle, 0, 0);
      rot('RightLowerLeg', 0.3 * settle, 0, 0);
      rot('LeftUpperArm', -0.3 * settle, 0, -0.2);
      rot('RightUpperArm', -0.3 * settle, 0, 0.2);
    }
  }

  private static exhausted(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const pant = Math.sin(t * 3.2) * 0.06;
    trans('Hips', 0, -0.2, 0);
    rot('Hips', 0.45, 0, 0);
    rot('Spine', 0.35 + pant, 0, 0);
    rot('Chest', 0.25 + pant * 1.5, 0, 0);
    rot('Head', 0.2 - pant * 0.5, 0, 0);

    rot('LeftUpperLeg', -0.75, 0, -0.2);
    rot('LeftLowerLeg', 0.85, 0, 0);
    rot('RightUpperLeg', -0.75, 0, 0.2);
    rot('RightLowerLeg', 0.85, 0, 0);

    // Chống tay lên đầu gối
    rot('LeftShoulder', 0.2, 0, 0.1);
    rot('LeftUpperArm', -0.85, 0.15, -0.3);
    rot('LeftLowerArm', -0.75, 0, 0);

    rot('RightShoulder', 0.2, 0, -0.1);
    rot('RightUpperArm', -0.85, -0.15, 0.3);
    rot('RightLowerArm', -0.75, 0, 0);
  }
}
