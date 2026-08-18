import { DialogueManifestItem, MasterSceneConfig } from '../../types/scene';

export interface ActiveSubtitle {
  line_id: string;
  speaker_id: string;
  speaker_name: string;
  speaker_color: string;
  text: string;
  start_time: number;
  end_time: number;
  progress: number; // 0 to 1
}

export class SubtitleSynchronizer {
  public static getActiveSubtitle(
    scene: MasterSceneConfig,
    currentTime: number
  ): ActiveSubtitle | null {
    if (!scene.dialogues_manifest) return null;

    for (const item of scene.dialogues_manifest) {
      const duration = item.actual_duration || item.estimated_duration || 3.0;
      const start = item.start_time;
      const end = start + duration;

      if (currentTime >= start && currentTime <= end) {
        const progress = Math.min(1, Math.max(0, (currentTime - start) / duration));
        return {
          line_id: item.line_id,
          speaker_id: item.speaker_id,
          speaker_name: item.speaker_name,
          speaker_color: item.speaker_color || '#eab308',
          text: item.text,
          start_time: start,
          end_time: end,
          progress,
        };
      }
    }

    return null;
  }
}
