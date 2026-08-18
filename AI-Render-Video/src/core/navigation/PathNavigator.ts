import * as THREE from 'three';
import { Vec3Tuple } from '../../types/scene';
import { NavMeshManager } from './NavMeshManager';

export class PathNavigator {
  private navMesh: NavMeshManager;

  constructor(navMesh: NavMeshManager) {
    this.navMesh = navMesh;
  }

  public findPath(start: Vec3Tuple, destination: Vec3Tuple): Vec3Tuple[] {
    // Direct line check
    const obstacles = this.navMesh.getObstacles();
    const startVec = new THREE.Vector2(start[0], start[2]);
    const endVec = new THREE.Vector2(destination[0], destination[2]);
    const lineDir = endVec.clone().sub(startVec);
    const lineDist = lineDir.length();

    let directCollision = false;
    let collisionObstacle = null;

    for (const obs of obstacles) {
      const obsPos = new THREE.Vector2(obs.position[0], obs.position[2]);
      const toObs = obsPos.clone().sub(startVec);
      const proj = toObs.dot(lineDir.clone().normalize());

      if (proj > 0 && proj < lineDist) {
        const closestPoint = startVec.clone().add(lineDir.clone().normalize().multiplyScalar(proj));
        const distToLine = closestPoint.distanceTo(obsPos);
        if (distToLine < obs.radius + 0.6) {
          directCollision = true;
          collisionObstacle = obs;
          break;
        }
      }
    }

    if (!directCollision || !collisionObstacle) {
      return [start, destination];
    }

    // Generate waypoint to curve around obstacle
    const obsPos = new THREE.Vector2(collisionObstacle.position[0], collisionObstacle.position[2]);
    const normal = new THREE.Vector2(-lineDir.y, lineDir.x).normalize();
    const waypoint2D = obsPos.clone().add(normal.multiplyScalar(collisionObstacle.radius + 1.0));

    return [start, [waypoint2D.x, start[1], waypoint2D.y], destination];
  }

  public samplePathPosition(path: Vec3Tuple[], progress: number): { position: Vec3Tuple; rotationY: number } {
    if (path.length === 0) return { position: [0, 0, 0], rotationY: 0 };
    if (path.length === 1) return { position: path[0], rotationY: 0 };

    const p = Math.max(0, Math.min(1, progress));
    const totalSegments = path.length - 1;
    const segmentIndex = Math.min(Math.floor(p * totalSegments), totalSegments - 1);
    const segmentProgress = (p * totalSegments) - segmentIndex;

    const p1 = path[segmentIndex];
    const p2 = path[segmentIndex + 1];

    const posX = THREE.MathUtils.lerp(p1[0], p2[0], segmentProgress);
    const posY = THREE.MathUtils.lerp(p1[1], p2[1], segmentProgress);
    const posZ = THREE.MathUtils.lerp(p1[2], p2[2], segmentProgress);

    const dx = p2[0] - p1[0];
    const dz = p2[2] - p1[2];
    const rotationY = Math.atan2(dx, dz);

    return {
      position: [posX, posY, posZ],
      rotationY,
    };
  }
}
