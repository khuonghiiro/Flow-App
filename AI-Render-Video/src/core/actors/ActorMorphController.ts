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
  | 'shock';

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

      case 'neutral':
      default:
        break;
    }
  }
}
