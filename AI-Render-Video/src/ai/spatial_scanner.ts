import { NavMeshManager } from '../core/navigation/NavMeshManager';
import { SmartSocketRegistry } from '../core/interactions/SmartSocketRegistry';

export class SpatialScanner {
  public static scanEnvironment(): {
    obstacles: ReturnType<NavMeshManager['getObstacles']>;
    smartSockets: ReturnType<typeof SmartSocketRegistry.getAll>;
  } {
    const navMesh = new NavMeshManager();
    return {
      obstacles: navMesh.getObstacles(),
      smartSockets: SmartSocketRegistry.getAll(),
    };
  }

  public static exportSpatialDataForPrompt(): string {
    return JSON.stringify(this.scanEnvironment(), null, 2);
  }
}
