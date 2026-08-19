import { MasterSceneConfig } from '../../types/scene';
import { SubtitleSynchronizer } from '../subtitles/SubtitleSynchronizer';
import { SubtitleCanvasBurner } from '../subtitles/SubtitleCanvasBurner';
import * as Mp4Muxer from 'mp4-muxer';

export interface ExportProgress {
  currentFrame: number;
  totalFrames: number;
  percent: number;
  status: string;
}

export class WebCodecsRecorder {
  private isRecording: boolean = false;
  private cancelRequested: boolean = false;

  public async recordOffline(
    sourceCanvas: HTMLCanvasElement,
    scene: MasterSceneConfig,
    durationSeconds: number,
    fps: number = 30,
    renderFrameFn: (time: number) => void,
    onProgress?: (progress: ExportProgress) => void
  ): Promise<Blob> {
    if (this.isRecording) {
      throw new Error('Recording already in progress');
    }

    this.isRecording = true;
    this.cancelRequested = false;

    // Create a composition canvas to burn in crisp subtitles
    const compCanvas = document.createElement('canvas');
    compCanvas.width = sourceCanvas.width || 1920;
    compCanvas.height = sourceCanvas.height || 1080;
    const compCtx = compCanvas.getContext('2d', { willReadFrequently: true })!;

    const targetFps = Math.max(30, Math.min(120, fps));
    const totalFrames = Math.floor(durationSeconds * targetFps);

    const muxer = new Mp4Muxer.Muxer({
      target: new Mp4Muxer.ArrayBufferTarget(),
      video: {
        codec: 'avc',
        width: compCanvas.width,
        height: compCanvas.height,
      },
      fastStart: 'in-memory',
      firstTimestampBehavior: 'offset',
    });

    const videoEncoder = new VideoEncoder({
      output: (chunk, meta) => muxer.addVideoChunk(chunk, meta as any),
      error: (e) => console.error('VideoEncoder error:', e),
    });

    videoEncoder.configure({
      codec: 'avc1.640028',
      width: compCanvas.width,
      height: compCanvas.height,
      framerate: targetFps,
      bitrate: targetFps >= 120 ? 25_000_000 : 16_000_000,
    });

    for (let frame = 0; frame < totalFrames; frame++) {
      if (this.cancelRequested) break;

      const currentTime = frame / targetFps;

      // 1. Tell Three.js to render the frame at exactly `currentTime`
      renderFrameFn(currentTime);

      // 2. Draw 3D scene from WebGL canvas
      compCtx.drawImage(sourceCanvas, 0, 0, compCanvas.width, compCanvas.height);

      // 3. Burn-in Subtitles if enabled
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

      // 4. Create VideoFrame and encode
      const timestampMicroseconds = (frame * 1_000_000) / targetFps;
      const videoFrame = new VideoFrame(compCanvas, { timestamp: timestampMicroseconds });
      
      const keyFrame = frame % (targetFps * 2) === 0; // Keyframe every 2 seconds
      videoEncoder.encode(videoFrame, { keyFrame });
      videoFrame.close();

      // Report progress and yield to event loop
      if (frame % 5 === 0) {
        onProgress?.({
          currentFrame: frame,
          totalFrames,
          percent: Math.round((frame / totalFrames) * 100),
          status: `Đang render GPU offline (${frame}/${totalFrames} frames)...`,
        });
        
        await new Promise((resolve) => setTimeout(resolve, 0));
      }

      // Throttle if encoder queue is too large
      if (videoEncoder.encodeQueueSize > 30) {
        await new Promise((resolve) => setTimeout(resolve, 10));
      }
    }

    await videoEncoder.flush();
    muxer.finalize();
    this.isRecording = false;

    const buffer = muxer.target.buffer;
    return new Blob([buffer], { type: 'video/mp4' });
  }

  public cancel(): void {
    this.cancelRequested = true;
  }
}
