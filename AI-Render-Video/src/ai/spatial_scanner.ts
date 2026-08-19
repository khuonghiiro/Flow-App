import { NavMeshManager } from '../core/navigation/NavMeshManager';
import { SmartSocketRegistry } from '../core/interactions/SmartSocketRegistry';
import { MapPresetManager } from '../core/maps/MapPresetManager';

export class SpatialScanner {
  public static scanEnvironment(): {
    obstacles: ReturnType<NavMeshManager['getObstacles']>;
    smartSockets: ReturnType<typeof SmartSocketRegistry.getAll>;
    savedMapPresets: ReturnType<typeof MapPresetManager.getAllPresets>;
  } {
    const navMesh = new NavMeshManager();
    return {
      obstacles: navMesh.getObstacles(),
      smartSockets: SmartSocketRegistry.getAll(),
      savedMapPresets: MapPresetManager.getAllPresets(),
    };
  }

  public static exportSpatialDataForPrompt(): string {
    return JSON.stringify(this.scanEnvironment(), null, 2);
  }
}

