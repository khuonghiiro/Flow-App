import { DialogueManifestItem, MasterSceneConfig } from '../../types/scene';

export class DialogueExtractor {
  public static extractDialogues(scene: MasterSceneConfig): DialogueManifestItem[] {
    return scene.dialogues_manifest || [];
  }

  public static getPendingTTS(scene: MasterSceneConfig): DialogueManifestItem[] {
    return (scene.dialogues_manifest || []).filter(
      (item) => item.status === 'pending_tts' || !item.audio_path
    );
  }

  public static findLineById(
    scene: MasterSceneConfig,
    lineId: string
  ): DialogueManifestItem | undefined {
    return (scene.dialogues_manifest || []).find((item) => item.line_id === lineId);
  }

  public static getSpeakerLines(
    scene: MasterSceneConfig,
    speakerId: string
  ): DialogueManifestItem[] {
    return (scene.dialogues_manifest || []).filter((item) => item.speaker_id === speakerId);
  }
}
