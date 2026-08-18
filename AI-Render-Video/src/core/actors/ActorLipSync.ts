import { VRMAvatar } from './VRMAvatar';

export type VisemeType = 'aa' | 'ih' | 'ou' | 'ee' | 'oh' | 'sil';

export class ActorLipSync {
  private avatar: VRMAvatar;
  private currentViseme: VisemeType = 'sil';
  private mouthOpenAmount: number = 0;

  constructor(avatar: VRMAvatar) {
    this.avatar = avatar;
  }

  public setSpeaking(isSpeaking: boolean, time: number, text?: string): void {
    if (!isSpeaking) {
      this.currentViseme = 'sil';
      this.mouthOpenAmount = 0;
      return;
    }

    // Dynamic viseme cycling based on speech cadence
    const syllableFreq = 8;
    const wave = (Math.sin(time * syllableFreq) + 1) / 2; // 0 to 1

    if (wave < 0.15) {
      this.currentViseme = 'sil';
      this.mouthOpenAmount = 0.1;
    } else if (wave < 0.4) {
      this.currentViseme = 'aa';
      this.mouthOpenAmount = 0.8;
    } else if (wave < 0.65) {
      this.currentViseme = 'oh';
      this.mouthOpenAmount = 0.7;
    } else if (wave < 0.85) {
      this.currentViseme = 'ee';
      this.mouthOpenAmount = 0.5;
    } else {
      this.currentViseme = 'ou';
      this.mouthOpenAmount = 0.6;
    }
  }

  public update(): void {
    const { mouthMesh } = this.avatar;

    switch (this.currentViseme) {
      case 'aa':
        mouthMesh.scale.set(1.4, 1.0 + this.mouthOpenAmount * 2.5, 1);
        break;
      case 'oh':
        mouthMesh.scale.set(0.9, 1.0 + this.mouthOpenAmount * 2.0, 1);
        break;
      case 'ee':
        mouthMesh.scale.set(1.6, 1.0 + this.mouthOpenAmount * 0.8, 1);
        break;
      case 'ou':
        mouthMesh.scale.set(0.7, 1.0 + this.mouthOpenAmount * 1.5, 1);
        break;
      case 'ih':
        mouthMesh.scale.set(1.2, 1.0 + this.mouthOpenAmount * 1.2, 1);
        break;
      case 'sil':
      default:
        mouthMesh.scale.set(1.0, 1.0, 1.0);
        break;
    }
  }
}
