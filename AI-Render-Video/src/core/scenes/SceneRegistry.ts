import { MasterSceneConfig } from '../../types/scene';

// Auto-scan all JSON scene configurations in the /scenes directory (recursive)
const sceneModules = import.meta.glob<MasterSceneConfig>('../../../scenes/**/*.json', {
  eager: true,
});

export interface SceneCategory {
  id: string;
  title: string;
  scenes: MasterSceneConfig[];
}

export const sceneCategories: SceneCategory[] = [];
export const sampleScenes: MasterSceneConfig[] = [];

const categoryMap = new Map<string, SceneCategory>();

for (const [path, moduleObj] of Object.entries(sceneModules)) {
  const rawScene: any = (moduleObj as any).default || moduleObj;
  if (!rawScene || !rawScene.scene_id) continue;

  const rawSubs = rawScene.subtitles_config || rawScene.subtitles;
  const scene: MasterSceneConfig = {
    ...rawScene,
    fps: rawScene.fps || 60,
    duration: rawScene.duration || 10.0,
    environment: rawScene.environment || {
      map: 'farming_village',
      sky_time: 'noon',
      weather: { fog: 0, wind: 0 },
    },
    subtitles_config: {
      enable_overlay: rawSubs?.enable_overlay ?? true,
      burn_in_export: rawSubs?.burn_in_export ?? true,
      font_size: rawSubs?.font_size || 20,
      show_speaker_name: rawSubs?.show_speaker_name ?? true,
      position: rawSubs?.position || 'bottom',
      text_color: rawSubs?.text_color || '#ffffff',
    },
    dialogues_manifest: (rawScene.dialogues_manifest || []).map((d: any) => ({
      line_id: d.line_id || d.id || `dlg_${Math.random().toString(36).substring(2, 7)}`,
      speaker_id: d.speaker_id || 'narrator',
      speaker_name: d.speaker_name || d.speaker_id || 'Người dẫn truyện',
      speaker_color: d.speaker_color || '#38bdf8',
      text: d.text || '',
      voice_config: d.voice_config || { voice_id: 'vi-VN-NamMinhNeural', speed: 1.0 },
      audio_path: d.audio_path || null,
      audio_naming_rule: d.audio_naming_rule || 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: d.status || 'pending_tts',
      start_time: d.start_time ?? 0,
      estimated_duration: d.estimated_duration || d.actual_duration || 3.0,
      actual_duration: d.actual_duration,
    })),
    camera_tracks: rawScene.camera_tracks || [],
    actors: rawScene.actors || [],
    dynamic_world_events: rawScene.dynamic_world_events || [],
  };

  const parts = path.split('/');
  let categoryId = parts[parts.length - 2];
  if (categoryId === 'scenes') {
    categoryId = 'samples';
  }

  // Deduplicate scene if already registered
  const existingIdx = sampleScenes.findIndex((s) => s.scene_id === scene.scene_id);
  if (existingIdx >= 0) {
    sampleScenes[existingIdx] = scene;
  } else {
    sampleScenes.push(scene);
  }

  if (!categoryMap.has(categoryId)) {
    categoryMap.set(categoryId, {
      id: categoryId,
      title: categoryId.charAt(0).toUpperCase() + categoryId.slice(1),
      scenes: [],
    });
  }

  const catScenes = categoryMap.get(categoryId)!.scenes;
  const existingCatIdx = catScenes.findIndex((s) => s.scene_id === scene.scene_id);
  if (existingCatIdx >= 0) {
    catScenes[existingCatIdx] = scene;
  } else {
    catScenes.push(scene);
  }
}

for (const cat of categoryMap.values()) {
  sceneCategories.push(cat);
}

/**
 * Default fallback scene to load when studio launches
 */
export const defaultScene: MasterSceneConfig =
  sampleScenes.find((s) => s.scene_id === 'scene_village_clash_01') ||
  sampleScenes[0] ||
  ({
    scene_id: 'scene_empty_fallback',
    title: 'Trống',
    fps: 30,
    duration: 10.0,
    environment: { map: 'farming_village', sky_time: 'noon', weather: { fog: 0, wind: 0 } },
    subtitles_config: { enable_overlay: true, burn_in_export: true, font_size: 20, show_speaker_name: true },
    dialogues_manifest: [],
    camera_tracks: [],
    actors: [],
  } as MasterSceneConfig);

/**
 * Find scene by ID or title keyword
 */
export function findSceneById(id: string): MasterSceneConfig | undefined {
  return sampleScenes.find((s) => s.scene_id === id);
}

/**
 * Find scene matching text keyword (for AI Director)
 */
export function findSceneByKeyword(keyword: string): MasterSceneConfig | undefined {
  const kw = keyword.toLowerCase();
  return sampleScenes.find(
    (s) =>
      s.scene_id.toLowerCase().includes(kw) ||
      (s.title || '').toLowerCase().includes(kw)
  );
}
