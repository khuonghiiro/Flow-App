import { SpatialObstacle, Vec3Tuple } from '../../types/scene';

export class NavMeshManager {
  private obstacles: SpatialObstacle[] = [
    { id: 'tree_01', name: 'Cây Làng', type: 'tree', position: [4, 0, -3], radius: 1.2, height: 4.0 },
    { id: 'chair_01', name: 'Ghế Gỗ', type: 'chair', position: [-4, 0, -2], radius: 0.8, height: 1.0 },
    { id: 'farm_01', name: 'Ô Ruộng', type: 'farm_plot', position: [0, 0, -5], radius: 1.5, height: 0.4 },
    { id: 'fence_l', name: 'Hàng Rào Trái', type: 'wall', position: [-6, 0, 0], radius: 0.6, height: 1.0 },
    { id: 'fence_r', name: 'Hàng Rào Phải', type: 'wall', position: [6, 0, 0], radius: 0.6, height: 1.0 },
  ];

  public getObstacles(): SpatialObstacle[] {
    return this.obstacles;
  }

  public isPositionWalkable(pos: Vec3Tuple, agentRadius: number = 0.4): boolean {
    for (const obs of this.obstacles) {
      const dx = pos[0] - obs.position[0];
      const dz = pos[2] - obs.position[2];
      const dist = Math.sqrt(dx * dx + dz * dz);
      if (dist < obs.radius + agentRadius) {
        return false;
      }
    }
    return true;
  }

  public addObstacle(obstacle: SpatialObstacle): void {
    this.obstacles.push(obstacle);
  }
}
