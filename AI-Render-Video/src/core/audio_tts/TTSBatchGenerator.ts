import { DialogueManifestItem } from '../../types/scene';

export interface TTSGenerationResult {
  line_id: string;
  audio_url: string;
  duration_seconds: number;
  audio_buffer?: AudioBuffer;
}

export class TTSBatchGenerator {
  private static audioCtx: AudioContext | null = null;

  private static getAudioContext(): AudioContext {
    if (!this.audioCtx) {
      const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.audioCtx = new AudioContextClass();
    }
    if (this.audioCtx.state === 'suspended') {
      this.audioCtx.resume();
    }
    return this.audioCtx;
  }

  public static async generateTTSAudio(item: DialogueManifestItem): Promise<TTSGenerationResult> {
    const ctx = this.getAudioContext();
    const words = item.text.split(/\s+/).length;
    // Calculate realistic duration based on word count and speech rate
    const speed = item.voice_config?.speed || 1.0;
    const baseDuration = Math.max(1.8, (words / 3.0) / speed);
    const duration = Math.round(baseDuration * 100) / 100;

    // Synthesize procedural audio buffer for playback & lip-sync
    const sampleRate = ctx.sampleRate;
    const buffer = ctx.createBuffer(1, Math.floor(sampleRate * duration), sampleRate);
    const channelData = buffer.getChannelData(0);

    const baseFreq = item.speaker_id.includes('warrior') ? 140 : 220;
    for (let i = 0; i < channelData.length; i++) {
      const t = i / sampleRate;
      // Modulate voice waveform to simulate speech syllables
      const syllableMod = 0.5 + 0.5 * Math.sin(t * 12);
      const carrier = Math.sin(2 * Math.PI * baseFreq * t);
      const harmonic = 0.3 * Math.sin(4 * Math.PI * baseFreq * t);
      const noise = (Math.random() * 2 - 1) * 0.05;
      const envelope = Math.sin((Math.PI * t) / duration); // Smooth fade in and out
      channelData[i] = (carrier + harmonic + noise) * syllableMod * envelope * 0.4;
    }

    // Convert AudioBuffer to WAV Blob URL
    const wavBlob = this.bufferToWave(buffer, buffer.length);
    const audioUrl = URL.createObjectURL(wavBlob);

    return {
      line_id: item.line_id,
      audio_url: audioUrl,
      duration_seconds: duration,
      audio_buffer: buffer,
    };
  }

  public static async batchGenerate(
    items: DialogueManifestItem[],
    onProgress?: (index: number, total: number) => void
  ): Promise<Map<string, TTSGenerationResult>> {
    const results = new Map<string, TTSGenerationResult>();
    for (let i = 0; i < items.length; i++) {
      const res = await this.generateTTSAudio(items[i]);
      results.set(items[i].line_id, res);
      onProgress?.(i + 1, items.length);
    }
    return results;
  }

  private static bufferToWave(abuffer: AudioBuffer, len: number): Blob {
    const numOfChan = abuffer.numberOfChannels;
    const length = len * numOfChan * 2 + 44;
    const out = new DataView(new ArrayBuffer(length));
    let pos = 0;

    function setUint16(data: number) {
      out.setUint16(pos, data, true);
      pos += 2;
    }
    function setUint32(data: number) {
      out.setUint32(pos, data, true);
      pos += 4;
    }

    // RIFF identifier & length
    out.setUint32(0, 0x46464952, true); // "RIFF"
    out.setUint32(4, length - 8, true);
    out.setUint32(8, 0x45564157, true); // "WAVE"

    // format chunk identifier & format header length
    out.setUint32(12, 0x20746d66, true); // "fmt "
    out.setUint32(16, 16, true); // size
    out.setUint16(20, 1, true); // PCM
    out.setUint16(22, numOfChan, true);
    out.setUint32(24, abuffer.sampleRate, true);
    out.setUint32(28, abuffer.sampleRate * 2 * numOfChan, true);
    out.setUint16(32, numOfChan * 2, true);
    out.setUint16(34, 16, true); // 16-bit

    // data chunk identifier
    out.setUint32(36, 0x61746164, true); // "data"
    out.setUint32(40, length - 44, true);
    pos = 44;

    const channels: Float32Array[] = [];
    for (let i = 0; i < numOfChan; i++) {
      channels.push(abuffer.getChannelData(i));
    }

    let offset = 0;
    while (offset < len) {
      for (let i = 0; i < numOfChan; i++) {
        let sample = Math.max(-1, Math.min(1, channels[i][offset]));
        sample = (0.5 + sample < 0 ? sample * 32768 : sample * 32767) | 0;
        out.setInt16(pos, sample, true);
        pos += 2;
      }
      offset++;
    }

    return new Blob([out.buffer], { type: 'audio/wav' });
  }
}
