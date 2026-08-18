import { MasterSceneConfig } from '../../types/scene';
import { SubtitleSynchronizer } from '../subtitles/SubtitleSynchronizer';
import { SubtitleCanvasBurner } from '../subtitles/SubtitleCanvasBurner';

export interface ExportProgress {
  currentFrame: number;
  totalFrames: number;
  percent: number;
  status: string;
}

export class WebCodecsRecorder {
  private isRecording: boolean = false;
  private mediaRecorder: MediaRecorder | null = null;
  private recordedChunks: Blob[] = [];

  public async recordCanvasLive(
    sourceCanvas: HTMLCanvasElement,
    scene: MasterSceneConfig,
    durationSeconds: number,
    fps: number = 30,
    onProgress?: (progress: ExportProgress) => void
  ): Promise<Blob> {
    if (this.isRecording) {
      throw new Error('Recording already in progress');
    }

    this.isRecording = true;
    this.recordedChunks = [];

    // Create a composition canvas to burn in crisp subtitles
    const compCanvas = document.createElement('canvas');
    compCanvas.width = sourceCanvas.width || 1920;
    compCanvas.height = sourceCanvas.height || 1080;
    const compCtx = compCanvas.getContext('2d')!;

    const stream = compCanvas.captureStream(fps);
    const mimeTypes = [
      'video/mp4;codecs=avc1',
      'video/webm;codecs=vp9',
      'video/webm;codecs=vp8',
      'video/webm',
    ];
    let selectedMime = 'video/webm';
    for (const mime of mimeTypes) {
      if (MediaRecorder.isTypeSupported(mime)) {
        selectedMime = mime;
        break;
      }
    }

    this.mediaRecorder = new MediaRecorder(stream, {
      mimeType: selectedMime,
      videoBitsPerSecond: 12_000_000, // 12 Mbps crystal clear
    });

    this.mediaRecorder.ondataavailable = (e) => {
      if (e.data && e.data.size > 0) {
        this.recordedChunks.push(e.data);
      }
    };

    return new Promise((resolve, reject) => {
      if (!this.mediaRecorder) {
        reject(new Error('MediaRecorder initialization failed'));
        return;
      }

      this.mediaRecorder.onstop = () => {
        this.isRecording = false;
        const blob = new Blob(this.recordedChunks, { type: selectedMime });
        resolve(blob);
      };

      this.mediaRecorder.onerror = (e) => {
        this.isRecording = false;
        reject(e);
      };

      this.mediaRecorder.start();

      const totalFrames = Math.floor(durationSeconds * fps);
      let frame = 0;
      const intervalMs = 1000 / fps;

      const recordInterval = setInterval(() => {
        if (!this.isRecording || frame >= totalFrames) {
          clearInterval(recordInterval);
          if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
          }
          return;
        }

        const currentTime = (frame / totalFrames) * durationSeconds;

        // Draw 3D scene from WebGL canvas
        compCtx.drawImage(sourceCanvas, 0, 0, compCanvas.width, compCanvas.height);

        // Burn-in Subtitles if enabled
        if (scene.subtitles_config && scene.subtitles_config.burn_in_export) {
          const activeSub = SubtitleSynchronizer.getActiveSubtitle(scene, currentTime);
          SubtitleCanvasBurner.burnSubtitleToCanvas(
            compCtx,
            activeSub,
            scene.subtitles_config,
            compCanvas.width,
            compCanvas.height
          );
        }

        frame++;
        onProgress?.({
          currentFrame: frame,
          totalFrames,
          percent: Math.round((frame / totalFrames) * 100),
          status: `Đang render GPU (${frame}/${totalFrames} frames)...`,
        });
      }, intervalMs);
    });
  }

  public cancel(): void {
    if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
      this.mediaRecorder.stop();
    }
    this.isRecording = false;
  }
}
