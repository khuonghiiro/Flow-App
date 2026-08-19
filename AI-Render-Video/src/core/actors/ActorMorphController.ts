import { VRMAvatar } from './VRMAvatar';

export type FacialExpressionType =
  | 'neutral'
  | 'angry'
  | 'pain'
  | 'smile'
  | 'smirk'
  | 'sad'
  | 'serious'
  | 'surprised'
  | 'shock'
  // Xianxia / Tiên hiệp biểu cảm
  | 'cold'           // Lạnh lùng (mắt hẹp, miệng thẳng)
  | 'arrogant'       // Kiêu ngạo (lông mày nhướng, miệng cong)
  | 'contempt'       // Khinh thường (1 lông mày nhướng, miệng méo)
  | 'wise'           // Uyên bác (mắt hiền, miệng hơi cười)
  | 'fierce'         // Hung dữ (mắt trợn, miệng nghiến)
  | 'meditative'     // Thiền định (mắt nhắm, bình yên)
  | 'menacing'       // Đe dọa (mắt nheo, miệng cười nham hiểm)
  | 'compassionate'  // Từ bi (mắt dịu, miệng nhẹ)
  | 'determined';    // Quyết tâm (lông mày chau, mắt sáng)

export class ActorMorphController {
  private avatar: VRMAvatar;
  private currentExpression: FacialExpressionType = 'neutral';
  private expressionWeight: number = 1.0;
  private blinkTimer: number = 0;

  constructor(avatar: VRMAvatar) {
    this.avatar = avatar;
  }

  public setExpression(type: FacialExpressionType, weight: number = 1.0): void {
    this.currentExpression = type;
    this.expressionWeight = weight;
  }

  public update(delta: number): void {
    const { eyebrowL, eyebrowR, eyeLMesh, eyeRMesh, mouthMesh } = this.avatar;

    // Reset default transforms
    eyebrowL.position.set(-0.08, 0.1, 0.17);
    eyebrowR.position.set(0.08, 0.1, 0.17);
    eyebrowL.rotation.z = 0;
    eyebrowR.rotation.z = 0;
    eyeLMesh.scale.set(1, 1, 1);
    eyeRMesh.scale.set(1, 1, 1);
    mouthMesh.scale.set(1, 1, 1);
    mouthMesh.rotation.z = 0;

    const w = this.expressionWeight;

    // Natural blinking cycle
    this.blinkTimer += delta;
    if (this.blinkTimer > 3.5) {
      if (this.blinkTimer < 3.65) {
        eyeLMesh.scale.y = 0.1;
        eyeRMesh.scale.y = 0.1;
      } else {
        this.blinkTimer = 0;
      }
    }

    switch (this.currentExpression) {
      case 'angry':
        // Slanted inward eyebrows
        eyebrowL.rotation.z = 0.4 * w;
        eyebrowR.rotation.z = -0.4 * w;
        eyebrowL.position.y = (0.1 - 0.03 * w);
        eyebrowR.position.y = (0.1 - 0.03 * w);
        break;

      case 'pain':
        // Tightly shut squinting eyes, arched distressed distressed brows
        eyebrowL.rotation.z = -0.45 * w;
        eyebrowR.rotation.z = 0.45 * w;
        eyebrowL.position.y = (0.1 + 0.04 * w);
        eyebrowR.position.y = (0.1 + 0.04 * w);
        eyeLMesh.scale.set(1.1, Math.max(0.08, 1 - 0.9 * w), 1);
        eyeRMesh.scale.set(1.1, Math.max(0.08, 1 - 0.9 * w), 1);
        mouthMesh.scale.set(1.6, 2.5, 1); // wide open grimace in pain
        break;

      case 'smirk':
        // One eyebrow raised, mouth lopsided
        eyebrowL.position.y = (0.1 + 0.03 * w);
        eyebrowR.rotation.z = -0.15 * w;
        mouthMesh.rotation.z = 0.15 * w;
        break;

      case 'smile':
        eyebrowL.position.y = (0.1 + 0.02 * w);
        eyebrowR.position.y = (0.1 + 0.02 * w);
        mouthMesh.scale.x = 1.3 * w;
        break;

      case 'sad':
        eyebrowL.rotation.z = -0.25 * w;
        eyebrowR.rotation.z = 0.25 * w;
        break;

      case 'surprised':
      case 'shock':
        eyebrowL.position.y = (0.1 + 0.05 * w);
        eyebrowR.position.y = (0.1 + 0.05 * w);
        eyeLMesh.scale.set(1.3, 1.3, 1.3);
        eyeRMesh.scale.set(1.3, 1.3, 1.3);
        mouthMesh.scale.set(0.8, 2.2, 1);
        break;

      case 'serious':
        eyebrowL.rotation.z = 0.15 * w;
        eyebrowR.rotation.z = -0.15 * w;
        break;

      // ========== XIANXIA BIỂU CẢM ==========

      case 'cold':
        // Mắt hẹp, lông mày phẳng, miệng thẳng
        eyeLMesh.scale.set(1, Math.max(0.4, 1 - 0.5 * w), 1);
        eyeRMesh.scale.set(1, Math.max(0.4, 1 - 0.5 * w), 1);
        eyebrowL.rotation.z = 0.08 * w;
        eyebrowR.rotation.z = -0.08 * w;
        mouthMesh.scale.set(0.9, 0.7, 1);
        break;

      case 'arrogant':
        // Lông mày nhướng lên, miệng cong kiêu ngạo
        eyebrowL.position.y = (0.1 + 0.04 * w);
        eyebrowR.position.y = (0.1 + 0.04 * w);
        eyebrowL.rotation.z = -0.1 * w;
        eyebrowR.rotation.z = 0.1 * w;
        mouthMesh.scale.set(1.1, 0.8, 1);
        mouthMesh.rotation.z = 0.08 * w; // Miệng hơi méo khinh
        break;

      case 'contempt':
        // 1 mày nhướng, miệng méo khinh thường
        eyebrowL.position.y = (0.1 + 0.05 * w);
        eyebrowR.rotation.z = -0.2 * w;
        mouthMesh.rotation.z = 0.15 * w;
        mouthMesh.scale.set(1.1, 0.7, 1);
        break;

      case 'wise':
        // Mắt hiền dịu, miệng hơi cười nhẹ
        eyeLMesh.scale.set(0.9, 0.85, 1);
        eyeRMesh.scale.set(0.9, 0.85, 1);
        eyebrowL.position.y = (0.1 + 0.02 * w);
        eyebrowR.position.y = (0.1 + 0.02 * w);
        mouthMesh.scale.set(1.15, 0.9, 1);
        break;

      case 'fierce':
        // Mắt trợn to, nghiến răng, lông mày chau
        eyebrowL.rotation.z = 0.35 * w;
        eyebrowR.rotation.z = -0.35 * w;
        eyebrowL.position.y = (0.1 - 0.04 * w);
        eyebrowR.position.y = (0.1 - 0.04 * w);
        eyeLMesh.scale.set(1.25, 1.25, 1);
        eyeRMesh.scale.set(1.25, 1.25, 1);
        mouthMesh.scale.set(1.3, 1.8, 1);
        break;

      case 'meditative':
        // Mắt nhắm, khuôn mặt bình yên
        eyeLMesh.scale.set(1, Math.max(0.05, 1 - 0.95 * w), 1);
        eyeRMesh.scale.set(1, Math.max(0.05, 1 - 0.95 * w), 1);
        eyebrowL.position.y = (0.1 + 0.01 * w);
        eyebrowR.position.y = (0.1 + 0.01 * w);
        mouthMesh.scale.set(0.85, 0.8, 1);
        break;

      case 'menacing':
        // Mắt nheo, miệng cười nham hiểm
        eyeLMesh.scale.set(1.1, Math.max(0.5, 1 - 0.4 * w), 1);
        eyeRMesh.scale.set(1.1, Math.max(0.5, 1 - 0.4 * w), 1);
        eyebrowL.rotation.z = 0.2 * w;
        eyebrowR.rotation.z = -0.2 * w;
        mouthMesh.scale.set(1.3, 1.0, 1);
        mouthMesh.rotation.z = 0.1 * w;
        break;

      case 'compassionate':
        // Mắt dịu dàng, miệng mỉm cười nhẹ
        eyeLMesh.scale.set(0.95, 0.9, 1);
        eyeRMesh.scale.set(0.95, 0.9, 1);
        eyebrowL.position.y = (0.1 + 0.02 * w);
        eyebrowR.position.y = (0.1 + 0.02 * w);
        eyebrowL.rotation.z = -0.08 * w;
        eyebrowR.rotation.z = 0.08 * w;
        mouthMesh.scale.set(1.2, 0.85, 1);
        break;

      case 'determined':
        // Lông mày chau lại, mắt sáng rực, miệng nghiến
        eyebrowL.rotation.z = 0.3 * w;
        eyebrowR.rotation.z = -0.3 * w;
        eyebrowL.position.y = (0.1 - 0.02 * w);
        eyebrowR.position.y = (0.1 - 0.02 * w);
        eyeLMesh.scale.set(1.15, 1.1, 1);
        eyeRMesh.scale.set(1.15, 1.1, 1);
        mouthMesh.scale.set(1.0, 0.6, 1);
        break;

      case 'neutral':
      default:
        break;
    }
  }
}
