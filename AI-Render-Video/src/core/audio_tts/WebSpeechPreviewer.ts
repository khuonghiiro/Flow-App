import { DialogueManifestItem } from '../../types/scene';

export class WebSpeechPreviewer {
  private static synth = typeof window !== 'undefined' ? window.speechSynthesis : null;
  private static currentUtterance: SpeechSynthesisUtterance | null = null;

  public static isSupported(): boolean {
    return !!this.synth;
  }

  public static preview(
    item: DialogueManifestItem,
    onBoundary?: (charIndex: number, charLength?: number) => void,
    onEnd?: () => void
  ): void {
    if (!this.synth) {
      console.warn('SpeechSynthesis is not supported on this browser.');
      onEnd?.();
      return;
    }

    this.stop();

    const utterance = new SpeechSynthesisUtterance(item.text);
    utterance.rate = item.voice_config?.speed || 1.0;
    utterance.pitch = (item.voice_config?.pitch || 0) + 1.0;

    // Detect Vietnamese or English voices
    const voices = this.synth.getVoices();
    const viVoice = voices.find((v) => v.lang.startsWith('vi'));
    if (viVoice) {
      utterance.voice = viVoice;
    }

    if (onBoundary) {
      utterance.onboundary = (event) => {
        onBoundary(event.charIndex, event.charLength);
      };
    }

    utterance.onend = () => {
      this.currentUtterance = null;
      onEnd?.();
    };

    utterance.onerror = (e) => {
      console.error('SpeechSynthesis error:', e);
      this.currentUtterance = null;
      onEnd?.();
    };

    this.currentUtterance = utterance;
    this.synth.speak(utterance);
  }

  public static stop(): void {
    if (this.synth) {
      this.synth.cancel();
      this.currentUtterance = null;
    }
  }
}
