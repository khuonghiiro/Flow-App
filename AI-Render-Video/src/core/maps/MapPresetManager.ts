import * as THREE from 'three';
import { MapPresetConfig, PlacedProp } from '../../types/map_preset';
import { MasterSceneConfig, EnvironmentConfig } from '../../types/scene';
import { SmartSocketRegistry } from '../interactions/SmartSocketRegistry';
import { AssetLoaderRegistry } from '../assets/AssetLoaderRegistry';

// Auto-scan all JSON map presets in the /assets/maps/presets directory
const presetModules = import.meta.glob<MapPresetConfig>(
  '../../../assets/maps/presets/**/*.json',
  { eager: true }
);

/**
 * MapPresetManager
 * Manages saving, loading, and spatial mapping of custom environment presets.
 */
export class MapPresetManager {
  private static presets: Map<string, MapPresetConfig> = new Map();

  static {
    for (const moduleObj of Object.values(presetModules)) {
      const preset: MapPresetConfig = (moduleObj as any).default || moduleObj;
      if (preset && preset.map_id) {
        this.registerPreset(preset);
      }
    }
  }

  /**
   * Register a preset in-memory
   */
  public static registerPreset(preset: MapPresetConfig): void {
    this.presets.set(preset.map_id, preset);
  }

  /**
   * Get a registered preset by ID
   */
  public static getPreset(mapId: string): MapPresetConfig | undefined {
    return this.presets.get(mapId);
  }

  /**
   * Get all registered presets
   */
  public static getAllPresets(): MapPresetConfig[] {
    return Array.from(this.presets.values());
  }


  /**
   * Export the current scene environment + placed objects as a MapPresetConfig JSON
   */
  public static createPresetFromScene(
    sceneConfig: MasterSceneConfig,
    placedProps: PlacedProp[],
    mapId: string,
    name: string,
    description: string = '',
    defaultSpawnPoints?: Record<string, [number, number, number]>
  ): MapPresetConfig {
    const env = sceneConfig.environment;

    return {
      map_id: mapId,
      name: name,
      description: description,
      base_map: env.map,
      sky_time: env.sky_time,
      weather: { ...env.weather },
      default_spawn_points: defaultSpawnPoints || {
        center: [0, 0, 0],
        actor_1: [-2, 0, 1],
        actor_2: [2, 0, 1],
      },
      placed_props: placedProps,
      created_at: new Date().toISOString(),
      tags: ['custom_map', env.map],
    };
  }

  /**
   * Download a preset as a JSON file to user disk
   */
  public static downloadPresetJson(preset: MapPresetConfig): void {
    const jsonStr = JSON.stringify(preset, null, 2);
    const blob = new Blob([jsonStr], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${preset.map_id}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  /**
   * Build 3D objects from a preset's placed_props and add them to a target Three.js group.
   * Also registers smart sockets and obstacles.
   */
  public static buildPlacedProps(
    preset: MapPresetConfig,
    targetGroup: THREE.Group,
    sceneObjectsRef?: Map<string, THREE.Object3D>
  ): void {
    if (!preset.placed_props || preset.placed_props.length === 0) return;

    for (const prop of preset.placed_props) {
      const objGroup = new THREE.Group();
      objGroup.name = prop.id;
      objGroup.position.set(...prop.position);

      if (prop.rotation) {
        objGroup.rotation.set(...prop.rotation);
      }

      if (prop.scale !== undefined) {
        if (typeof prop.scale === 'number') {
          objGroup.scale.setScalar(prop.scale);
        } else {
          objGroup.scale.set(...prop.scale);
        }
      }

      // Instantiate model or procedural geometry based on asset_path / type
      const mesh = this.createPropMesh(prop);
      objGroup.add(mesh);
      targetGroup.add(objGroup);

      if (sceneObjectsRef) {
        sceneObjectsRef.set(`props.${prop.id}`, objGroup);
      }

      // Register Smart Sockets if defined
      if (prop.smart_socket) {
        const socket = prop.smart_socket;
        const entryPos: [number, number, number] = socket.entry_offset
          ? [
              prop.position[0] + socket.entry_offset[0],
              prop.position[1] + socket.entry_offset[1],
              prop.position[2] + socket.entry_offset[2],
            ]
          : [prop.position[0], prop.position[1], prop.position[2] + 1];

        const targetPos: [number, number, number] = socket.target_offset
          ? [
              prop.position[0] + socket.target_offset[0],
              prop.position[1] + socket.target_offset[1],
              prop.position[2] + socket.target_offset[2],
            ]
          : [...prop.position];

        SmartSocketRegistry.registerSocket({
          id: `props.${prop.id}`,
          type: socket.socket_type as any,
          entryPosition: entryPos,
          targetPosition: targetPos,
          targetRotationY: socket.target_rotation_y ?? 0,
        });
      }
    }
  }

  /**
   * Helper to create meshes for placed props
   */
  private static createPropMesh(prop: PlacedProp): THREE.Object3D {
    const path = prop.asset_path.toLowerCase();

    if (path.includes('tree')) {
      return AssetLoaderRegistry.createTree([0, 0, 0]);
    } else if (path.includes('chair') || path.includes('bench')) {
      return AssetLoaderRegistry.createChair([0, 0, 0]);
    } else if (path.includes('farm')) {
      return AssetLoaderRegistry.createFarmPlot([0, 0, 0]);
    } else if (path.includes('duck')) {
      return AssetLoaderRegistry.createDuckProp([0, 0, 0]);
    } else if (path.includes('lantern')) {
      return AssetLoaderRegistry.createLanternStand([0, 0, 0]);
    }

    // Default primitive box placeholder if custom asset is not yet loaded
    const geo = new THREE.BoxGeometry(0.8, 0.8, 0.8);
    const mat = new THREE.MeshStandardMaterial({
      color: 0x88aa77,
      roughness: 0.7,
    });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  /**
   * Export spatial summary of a map preset for AI prompts
   */
  public static exportPresetSummaryForAI(preset: MapPresetConfig): string {
    const propsList = preset.placed_props
      .map(
        (p) =>
          `- [${p.id}] Type: ${p.type || 'prop'}, Position: [${p.position.join(', ')}], Asset: ${p.asset_path}`
      )
      .join('\n');

    const spawnList = preset.default_spawn_points
      ? Object.entries(preset.default_spawn_points)
          .map(([name, pos]) => `- Spawn "${name}": [${pos.join(', ')}]`)
          .join('\n')
      : '- Spawn "center": [0, 0, 0]';

    return `### Saved Map Preset: \`${preset.map_id}\` (${preset.name})
- **Description**: ${preset.description || 'Custom map configuration.'}
- **Base Map**: \`${preset.base_map || 'default'}\`
- **Default Sky/Weather**: ${preset.sky_time || 'sunset'}, Fog: ${preset.weather?.fog ?? 0.01}
- **Recommended Spawn Points**:
${spawnList}
- **Placed Objects & Interactables**:
${propsList || 'None'}
`;
  }
}
