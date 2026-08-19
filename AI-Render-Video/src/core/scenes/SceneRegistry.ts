import { MasterSceneConfig } from '../../types/scene';

// Auto-scan all JSON scene configurations in the /scenes directory
const sceneModules = import.meta.glob<MasterSceneConfig>('../../../scenes/*.json', {
  eager: true,
});

/**
 * All loaded scene presets from JSON files
 */
export const sampleScenes: MasterSceneConfig[] = Object.values(sceneModules).map(
  (mod: any) => mod.default || mod
);

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
