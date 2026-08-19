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
  const scene: MasterSceneConfig = (moduleObj as any).default || moduleObj;
  sampleScenes.push(scene);
  
  const parts = path.split('/');
  let categoryId = parts[parts.length - 2];
  if (categoryId === 'scenes') {
    categoryId = 'root';
  }
  
  if (!categoryMap.has(categoryId)) {
    categoryMap.set(categoryId, {
      id: categoryId,
      title: categoryId.charAt(0).toUpperCase() + categoryId.slice(1),
      scenes: []
    });
  }
  
  categoryMap.get(categoryId)!.scenes.push(scene);
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
