import { MasterSceneConfig } from '../../types/scene';

export class SubtitleSRTExporter {
  public static exportToSRT(scene: MasterSceneConfig): string {
    const dialogues = scene.dialogues_manifest || [];
    const sorted = [...dialogues].sort((a, b) => a.start_time - b.start_time);

    let srtContent = '';
    sorted.forEach((item, index) => {
      const duration = item.actual_duration || item.estimated_duration || 3.0;
      const start = item.start_time;
      const end = start + duration;

      srtContent += `${index + 1}\n`;
      srtContent += `${this.formatSRTTime(start)} --> ${this.formatSRTTime(end)}\n`;
      srtContent += `[${item.speaker_name}]: ${item.text}\n\n`;
    });

    return srtContent.trim();
  }

  public static exportToVTT(scene: MasterSceneConfig): string {
    const dialogues = scene.dialogues_manifest || [];
    const sorted = [...dialogues].sort((a, b) => a.start_time - b.start_time);

    let vttContent = 'WEBVTT\n\n';
    sorted.forEach((item, index) => {
      const duration = item.actual_duration || item.estimated_duration || 3.0;
      const start = item.start_time;
      const end = start + duration;

      vttContent += `${index + 1}\n`;
      vttContent += `${this.formatVTTTime(start)} --> ${this.formatVTTTime(end)}\n`;
      vttContent += `<v ${item.speaker_name}>${item.text}\n\n`;
    });

    return vttContent.trim();
  }

  public static downloadFile(filename: string, content: string, mimeType: string = 'text/plain'): void {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  private static formatSRTTime(seconds: number): string {
    const h = Math.floor(seconds / 3600).toString().padStart(2, '0');
    const m = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
    const s = Math.floor(seconds % 60).toString().padStart(2, '0');
    const ms = Math.floor((seconds % 1) * 1000).toString().padStart(3, '0');
    return `${h}:${m}:${s},${ms}`;
  }

  private static formatVTTTime(seconds: number): string {
    const h = Math.floor(seconds / 3600).toString().padStart(2, '0');
    const m = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
    const s = Math.floor(seconds % 60).toString().padStart(2, '0');
    const ms = Math.floor((seconds % 1) * 1000).toString().padStart(3, '0');
    return `${h}:${m}:${s}.${ms}`;
  }
}
