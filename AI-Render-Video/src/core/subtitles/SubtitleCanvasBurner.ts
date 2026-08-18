import { ActiveSubtitle } from './SubtitleSynchronizer';
import { SubtitlesConfig } from '../../types/scene';

export class SubtitleCanvasBurner {
  public static burnSubtitleToCanvas(
    ctx: CanvasRenderingContext2D,
    subtitle: ActiveSubtitle | null,
    config: SubtitlesConfig,
    width: number,
    height: number
  ): void {
    if (!subtitle || !config.enable_overlay) return;

    ctx.save();

    const fontSize = config.font_size || Math.max(20, Math.floor(height * 0.035));
    const posY = config.position === 'top' ? height * 0.12 : height * 0.88;

    ctx.font = `600 ${fontSize}px 'Plus Jakarta Sans', system-ui, sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';

    const speakerPrefix = config.show_speaker_name ? `[${subtitle.speaker_name}]: ` : '';
    const fullText = `${speakerPrefix}${subtitle.text}`;

    // Measure text
    const metrics = ctx.measureText(fullText);
    const boxPaddingX = fontSize * 0.8;
    const boxPaddingY = fontSize * 0.5;
    const boxWidth = metrics.width + boxPaddingX * 2;
    const boxHeight = fontSize * 1.6;

    // Semi-transparent rounded background plate
    const bgOpacity = config.background_opacity ?? 0.65;
    ctx.fillStyle = `rgba(10, 12, 20, ${bgOpacity})`;
    const radius = 8;
    const boxX = width / 2 - boxWidth / 2;
    const boxY = posY - boxHeight / 2;

    ctx.beginPath();
    ctx.roundRect(boxX, boxY, boxWidth, boxHeight, radius);
    ctx.fill();

    // Border glow line
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
    ctx.stroke();

    // Draw Speaker tag with specific speaker color
    if (config.show_speaker_name) {
      const prefixMetrics = ctx.measureText(speakerPrefix);
      const textStartX = width / 2 - metrics.width / 2;

      // Text Shadow for high contrast readability
      ctx.shadowColor = 'rgba(0, 0, 0, 0.9)';
      ctx.shadowBlur = 6;
      ctx.shadowOffsetX = 0;
      ctx.shadowOffsetY = 2;

      // Draw Speaker Name in speaker_color
      ctx.fillStyle = subtitle.speaker_color || '#eab308';
      ctx.textAlign = 'left';
      ctx.fillText(speakerPrefix, textStartX, posY);

      // Draw Dialogue Text in white
      ctx.fillStyle = config.text_color || '#ffffff';
      ctx.fillText(subtitle.text, textStartX + prefixMetrics.width, posY);
    } else {
      ctx.shadowColor = 'rgba(0, 0, 0, 0.9)';
      ctx.shadowBlur = 6;
      ctx.fillStyle = config.text_color || '#ffffff';
      ctx.fillText(subtitle.text, width / 2, posY);
    }

    ctx.restore();
  }
}
