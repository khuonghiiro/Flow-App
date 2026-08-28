import * as THREE from 'three';

export interface FingerJointDef {
  name: string;
  parentName: string;
  radius: number;
}

export type FingerCountMode = '5_fingers' | '3_fingers' | '0_fingers';

/**
 * HandFingerRigEngine
 * Chuyên trách tạo cấu trúc xương 5 ngón tay (Humanoid 15 khớp / bàn tay)
 * và điều khiển các tư thế cử động ngón tay chuẩn (Nắm tay, Xòe tay, Chữ V, Like...)
 */
export class HandFingerRigEngine {
  // 15 khớp ngón tay chuẩn Humanoid cho mỗi bên (Trái / Phải)
  public static readonly FINGER_HIERARCHY_LEFT: FingerJointDef[] = [
    // Ngón cái (Thumb)
    { name: 'LeftHandThumb1', parentName: 'LeftHand', radius: 0.04 },
    { name: 'LeftHandThumb2', parentName: 'LeftHandThumb1', radius: 0.035 },
    { name: 'LeftHandThumb3', parentName: 'LeftHandThumb2', radius: 0.03 },

    // Ngón trỏ (Index)
    { name: 'LeftHandIndex1', parentName: 'LeftHand', radius: 0.035 },
    { name: 'LeftHandIndex2', parentName: 'LeftHandIndex1', radius: 0.03 },
    { name: 'LeftHandIndex3', parentName: 'LeftHandIndex2', radius: 0.025 },

    // Ngón giữa (Middle)
    { name: 'LeftHandMiddle1', parentName: 'LeftHand', radius: 0.035 },
    { name: 'LeftHandMiddle2', parentName: 'LeftHandMiddle1', radius: 0.03 },
    { name: 'LeftHandMiddle3', parentName: 'LeftHandMiddle2', radius: 0.025 },

    // Ngón áp út (Ring)
    { name: 'LeftHandRing1', parentName: 'LeftHand', radius: 0.035 },
    { name: 'LeftHandRing2', parentName: 'LeftHandRing1', radius: 0.03 },
    { name: 'LeftHandRing3', parentName: 'LeftHandRing2', radius: 0.025 },

    // Ngón út (Pinky)
    { name: 'LeftHandPinky1', parentName: 'LeftHand', radius: 0.03 },
    { name: 'LeftHandPinky2', parentName: 'LeftHandPinky1', radius: 0.025 },
    { name: 'LeftHandPinky3', parentName: 'LeftHandPinky2', radius: 0.02 },
  ];

  public static readonly FINGER_HIERARCHY_RIGHT: FingerJointDef[] = [
    // Ngón cái (Thumb)
    { name: 'RightHandThumb1', parentName: 'RightHand', radius: 0.04 },
    { name: 'RightHandThumb2', parentName: 'RightHandThumb1', radius: 0.035 },
    { name: 'RightHandThumb3', parentName: 'RightHandThumb2', radius: 0.03 },

    // Ngón trỏ (Index)
    { name: 'RightHandIndex1', parentName: 'RightHand', radius: 0.035 },
    { name: 'RightHandIndex2', parentName: 'RightHandIndex1', radius: 0.03 },
    { name: 'RightHandIndex3', parentName: 'RightHandIndex2', radius: 0.025 },

    // Ngón giữa (Middle)
    { name: 'RightHandMiddle1', parentName: 'RightHand', radius: 0.035 },
    { name: 'RightHandMiddle2', parentName: 'RightHandMiddle1', radius: 0.03 },
    { name: 'RightHandMiddle3', parentName: 'RightHandMiddle2', radius: 0.025 },

    // Ngón áp út (Ring)
    { name: 'RightHandRing1', parentName: 'RightHand', radius: 0.035 },
    { name: 'RightHandRing2', parentName: 'RightHandRing1', radius: 0.03 },
    { name: 'RightHandRing3', parentName: 'RightHandRing2', radius: 0.025 },

    // Ngón út (Pinky)
    { name: 'RightHandPinky1', parentName: 'RightHand', radius: 0.03 },
    { name: 'RightHandPinky2', parentName: 'RightHandPinky1', radius: 0.025 },
    { name: 'RightHandPinky3', parentName: 'RightHandPinky2', radius: 0.02 },
  ];

  /**
   * Tự động tính toán các mốc tọa độ 3D của 5 ngón tay từ vị trí Cổ tay (Wrist) và Đầu ngón tay (Hand Tip)
   */
  public static calculateFingerPositions(
    wristPos: THREE.Vector3,
    handTipPos: THREE.Vector3,
    isLeft: boolean
  ): Map<string, THREE.Vector3> {
    const map = new Map<string, THREE.Vector3>();
    const sign = isLeft ? -1 : 1;
    const prefix = isLeft ? 'Left' : 'Right';

    // Vector hướng từ cổ tay đến ngón tay
    const handDir = new THREE.Vector3().subVectors(handTipPos, wristPos);
    const handLength = Math.max(0.08, handDir.length());
    handDir.normalize();

    // Vector ngang của bàn tay (vuông góc với hướng tay và trục thẳng đứng)
    const handUp = new THREE.Vector3(0, 1, 0);
    const handSide = new THREE.Vector3().crossVectors(handDir, handUp).normalize().multiplyScalar(sign);

    // Cấu hình góc mở xòe và độ dài cho 5 ngón
    const fingerConfigs = [
      { name: 'Thumb', lateralOffset: 0.35, lengthRatio: 0.65, forwardAngle: 0.35, upOffset: -0.015 },
      { name: 'Index', lateralOffset: 0.18, lengthRatio: 0.90, forwardAngle: 0.08, upOffset: 0.005 },
      { name: 'Middle', lateralOffset: 0.00, lengthRatio: 1.00, forwardAngle: 0.00, upOffset: 0.008 },
      { name: 'Ring', lateralOffset: -0.16, lengthRatio: 0.92, forwardAngle: -0.06, upOffset: 0.004 },
      { name: 'Pinky', lateralOffset: -0.32, lengthRatio: 0.78, forwardAngle: -0.14, upOffset: -0.002 },
    ];

    for (const cfg of fingerConfigs) {
      const palmBase = wristPos.clone().addScaledVector(handDir, handLength * 0.35);
      palmBase.addScaledVector(handSide, cfg.lateralOffset * handLength * 0.4);
      palmBase.y += cfg.upOffset;

      const fDir = handDir.clone();
      fDir.addScaledVector(handSide, cfg.forwardAngle);
      fDir.normalize();

      const fLength = handLength * 0.65 * cfg.lengthRatio;
      const segLength = fLength / 3;

      const p1 = palmBase.clone().addScaledVector(fDir, segLength * 0.8);
      const p2 = p1.clone().addScaledVector(fDir, segLength * 1.0);
      const p3 = p2.clone().addScaledVector(fDir, segLength * 1.0);

      map.set(`${prefix}Hand${cfg.name}1`, p1);
      map.set(`${prefix}Hand${cfg.name}2`, p2);
      map.set(`${prefix}Hand${cfg.name}3`, p3);
    }

    return map;
  }

  /**
   * Áp dụng cử động bàn tay (Hand Poses) lên Map các khớp xương
   */
  public static applyHandPose(
    bonesMap: Map<string, THREE.Bone>,
    poseName: 'relaxed' | 'fist' | 'open' | 'victory' | 'point' | 'thumbs_up' | 'rock'
  ): void {
    const fingerNames = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky'];
    const sides = ['Left', 'Right'];

    for (const side of sides) {
      const curl = (finger: string, joint: number, angleX: number, angleY: number = 0, angleZ: number = 0) => {
        const bone = bonesMap.get(`${side}Hand${finger}${joint}`);
        if (bone) {
          bone.rotation.set(angleX, angleY, angleZ);
        }
      };

      // Reset
      for (const finger of fingerNames) {
        for (let j = 1; j <= 3; j++) {
          const bone = bonesMap.get(`${side}Hand${finger}${j}`);
          if (bone) bone.rotation.set(0, 0, 0);
        }
      }

      switch (poseName) {
        case 'fist': // Nắm đấm
          for (const finger of ['Index', 'Middle', 'Ring', 'Pinky']) {
            curl(finger, 1, 1.2, 0, 0);
            curl(finger, 2, 1.4, 0, 0);
            curl(finger, 3, 1.1, 0, 0);
          }
          curl('Thumb', 1, 0.4, 0.4, 0.4);
          curl('Thumb', 2, 0.9, 0, 0);
          curl('Thumb', 3, 0.8, 0, 0);
          break;

        case 'open': // Xòe bàn tay
          curl('Thumb', 1, -0.2, 0.3, -0.2);
          curl('Index', 1, -0.1, 0, 0.08);
          curl('Middle', 1, -0.1, 0, 0);
          curl('Ring', 1, -0.1, 0, -0.06);
          curl('Pinky', 1, -0.15, 0, -0.12);
          break;

        case 'victory': // Chữ V (Peace)
          // Ngón cái, áp út, út gập lại
          for (const finger of ['Ring', 'Pinky']) {
            curl(finger, 1, 1.3, 0, 0);
            curl(finger, 2, 1.4, 0, 0);
            curl(finger, 3, 1.1, 0, 0);
          }
          curl('Thumb', 1, 0.6, 0.3, 0.4);
          curl('Thumb', 2, 0.9, 0, 0);
          // Ngón trỏ & giữa giương thẳng xòe chữ V
          curl('Index', 1, 0, 0, 0.12);
          curl('Middle', 1, 0, 0, -0.12);
          break;

        case 'point': // Chỉ ngón tay
          for (const finger of ['Middle', 'Ring', 'Pinky']) {
            curl(finger, 1, 1.3, 0, 0);
            curl(finger, 2, 1.4, 0, 0);
            curl(finger, 3, 1.1, 0, 0);
          }
          curl('Thumb', 1, 0.6, 0.3, 0.4);
          curl('Index', 1, 0, 0, 0);
          break;

        case 'thumbs_up': // Like / Thumbs Up
          for (const finger of ['Index', 'Middle', 'Ring', 'Pinky']) {
            curl(finger, 1, 1.3, 0, 0);
            curl(finger, 2, 1.4, 0, 0);
            curl(finger, 3, 1.1, 0, 0);
          }
          curl('Thumb', 1, -0.3, 0.2, 0.5);
          curl('Thumb', 2, -0.2, 0, 0);
          break;

        case 'rock': // Rock / Love Sign (Ngón trỏ & ngón út giơ cao)
          for (const finger of ['Middle', 'Ring']) {
            curl(finger, 1, 1.3, 0, 0);
            curl(finger, 2, 1.4, 0, 0);
            curl(finger, 3, 1.1, 0, 0);
          }
          curl('Thumb', 1, 0.5, 0.3, 0.4);
          curl('Index', 1, 0, 0, 0.08);
          curl('Pinky', 1, 0, 0, -0.1);
          break;

        case 'relaxed': // Thả lỏng tự nhiên
        default:
          for (const finger of ['Index', 'Middle', 'Ring', 'Pinky']) {
            curl(finger, 1, 0.25, 0, 0);
            curl(finger, 2, 0.35, 0, 0);
            curl(finger, 3, 0.2, 0, 0);
          }
          curl('Thumb', 1, 0.15, 0.15, 0.1);
          break;
      }
    }
  }
}
