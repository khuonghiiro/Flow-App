import { MasterSceneConfig } from '../../types/scene';
import { SubtitleSynchronizer } from '../subtitles/SubtitleSynchronizer';
import { SubtitleCanvasBurner } from '../subtitles/SubtitleCanvasBurner';
import { Muxer, ArrayBufferTarget } from 'mp4-muxer';

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
    fps: number = 60,
    renderFrameFn: (time: number) => void,
    onProgress?: (progress: ExportProgress) => void
  ): Promise<Blob> {
    if (this.isRecording) {
      throw new Error('Recording already in progress');
    }

    if (typeof VideoEncoder === 'undefined') {
      throw new Error('Trình duyệt của bạn không hỗ trợ WebCodecs VideoEncoder API. Vui lòng dùng Chrome hoặc Edge mới nhất.');
    }

    this.isRecording = true;
    this.cancelRequested = false;

    // Enforce even dimensions required by H.264 / WebCodecs hardware encoders
    let exportWidth = sourceCanvas.width || 1920;
    let exportHeight = sourceCanvas.height || 1080;
    exportWidth = Math.floor(exportWidth / 2) * 2;
    exportHeight = Math.floor(exportHeight / 2) * 2;
    if (exportWidth < 320) exportWidth = 1920;
    if (exportHeight < 240) exportHeight = 1080;

    // Create a composition canvas to burn in crisp subtitles
    const compCanvas = document.createElement('canvas');
    compCanvas.width = exportWidth;
    compCanvas.height = exportHeight;
    const compCtx = compCanvas.getContext('2d', { willReadFrequently: true })!;

    const targetFps = Math.max(24, Math.min(120, fps));
    const totalFrames = Math.max(1, Math.floor(durationSeconds * targetFps));

    // Dynamic codec detection for cross-GPU stability
    let selectedCodec = 'avc1.4d002a'; // Main Profile (widely supported)
    let muxerVideoCodec: 'avc' | 'hevc' | 'vp9' | 'av1' = 'avc';

    const codecCandidates: Array<{ codec: string; muxerCodec: 'avc' | 'hevc' | 'vp9' | 'av1' }> = [
      { codec: 'avc1.640028', muxerCodec: 'avc' }, // High Profile Level 4.0
      { codec: 'avc1.4d002a', muxerCodec: 'avc' }, // Main Profile Level 4.2
      { codec: 'avc1.42001f', muxerCodec: 'avc' }, // Baseline Profile
      { codec: 'vp09.00.10.08', muxerCodec: 'vp9' }, // VP9 Fallback
    ];

    const targetBitrate = targetFps >= 60 ? 12_000_000 : 8_000_000;

    for (const candidate of codecCandidates) {
      try {
        const support = await VideoEncoder.isConfigSupported({
          codec: candidate.codec,
          width: exportWidth,
          height: exportHeight,
          framerate: targetFps,
          bitrate: targetBitrate,
        });
        if (support.supported) {
          selectedCodec = candidate.codec;
          muxerVideoCodec = candidate.muxerCodec;
          break;
        }
      } catch {
        // test next candidate
      }
    }

    const muxer = new Muxer({
      target: new ArrayBufferTarget(),
      video: {
        codec: muxerVideoCodec,
        width: exportWidth,
        height: exportHeight,
      },
      fastStart: 'in-memory',
      firstTimestampBehavior: 'offset',
    });

    let encoderError: Error | null = null;
    const videoEncoder = new VideoEncoder({
      output: (chunk, meta) => {
        try {
          muxer.addVideoChunk(chunk, meta as any);
        } catch (err: any) {
          encoderError = err;
        }
      },
      error: (e) => {
        console.error('VideoEncoder error:', e);
        encoderError = e instanceof Error ? e : new Error(String(e));
      },
    });

    try {
      videoEncoder.configure({
        codec: selectedCodec,
        width: exportWidth,
        height: exportHeight,
        framerate: targetFps,
        bitrate: targetBitrate,
      });
    } catch (confErr: any) {
      this.isRecording = false;
      throw new Error(`Không thể khởi tạo VideoEncoder (${selectedCodec}): ${confErr?.message || confErr}`);
    }

    for (let frame = 0; frame < totalFrames; frame++) {
      if (this.cancelRequested) break;

      if ((videoEncoder.state as string) === 'closed' || encoderError) {
        this.isRecording = false;
        throw encoderError || new Error('VideoEncoder bị đóng bất ngờ bởi trình duyệt.');
      }

      const currentTime = frame / targetFps;

      // 1. Tell Three.js to render the frame at exactly `currentTime`
      renderFrameFn(currentTime);

      // Clear composition canvas before drawing each frame to avoid ghosting / overlapping trails
      compCtx.clearRect(0, 0, exportWidth, exportHeight);

      // 2. Draw 3D scene from WebGL canvas
      compCtx.drawImage(sourceCanvas, 0, 0, exportWidth, exportHeight);

      // 3. Burn-in Subtitles if enabled
      if (scene.subtitles_config && scene.subtitles_config.burn_in_export) {
        const activeSub = SubtitleSynchronizer.getActiveSubtitle(scene, currentTime);
        SubtitleCanvasBurner.burnSubtitleToCanvas(
          compCtx,
          activeSub,
          scene.subtitles_config,
          exportWidth,
          exportHeight
        );
      }

      // 4. Create VideoFrame and encode
      const timestampMicroseconds = Math.round((frame * 1_000_000) / targetFps);
      const videoFrame = new VideoFrame(compCanvas, { timestamp: timestampMicroseconds });

      const keyFrame = frame % (targetFps * 2) === 0; // Keyframe every 2 seconds
      videoEncoder.encode(videoFrame, { keyFrame });
      videoFrame.close();

      // Report progress and yield to event loop
      if (frame % 5 === 0 || frame === totalFrames - 1) {
        onProgress?.({
          currentFrame: frame + 1,
          totalFrames,
          percent: Math.round(((frame + 1) / totalFrames) * 100),
          status: `Đang render GPU (${frame + 1}/${totalFrames} frames)...`,
        });

        await new Promise((resolve) => setTimeout(resolve, 0));
      }

      // Throttle if encoder queue is busy
      while (videoEncoder.encodeQueueSize > 4 && !this.cancelRequested) {
        if ((videoEncoder.state as string) === 'closed' || encoderError) {
          this.isRecording = false;
          throw encoderError || new Error('VideoEncoder bị đóng bất ngờ bởi trình duyệt.');
        }
        await new Promise((resolve) => setTimeout(resolve, 4));
      }
    }

    if (this.cancelRequested) {
      this.isRecording = false;
      throw new Error('Đã hủy xuất video.');
    }

    if ((videoEncoder.state as string) !== 'closed') {
      await videoEncoder.flush();
      muxer.finalize();
      this.isRecording = false;

      const buffer = muxer.target.buffer;
      return new Blob([buffer], { type: 'video/mp4' });
    } else {
      this.isRecording = false;
      throw encoderError || new Error('VideoEncoder bị đóng trước khi hoàn tất flush.');
    }
  }

  public cancel(): void {
    this.cancelRequested = true;
  }
}
