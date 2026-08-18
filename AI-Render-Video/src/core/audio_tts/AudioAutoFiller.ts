import { MasterSceneConfig } from '../../types/scene';
import { TTSGenerationResult } from './TTSBatchGenerator';

export class AudioAutoFiller {
  public static autoFillSceneAudio(
    scene: MasterSceneConfig,
    ttsResults: Map<string, TTSGenerationResult>
  ): MasterSceneConfig {
    const updatedScene: MasterSceneConfig = JSON.parse(JSON.stringify(scene));

    if (!updatedScene.dialogues_manifest) return updatedScene;

    for (const item of updatedScene.dialogues_manifest) {
      const generated = ttsResults.get(item.line_id);
      if (generated) {
        // Resolve naming rule: e.g. audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3
        const resolvedPath = item.audio_naming_rule
          .replace('{scene_id}', scene.scene_id)
          .replace('{speaker_id}', item.speaker_id)
          .replace('{line_id}', item.line_id);

        item.audio_path = resolvedPath;
        item.actual_duration = generated.duration_seconds;
        item.status = 'ready';
      }
    }

    return updatedScene;
  }
}
