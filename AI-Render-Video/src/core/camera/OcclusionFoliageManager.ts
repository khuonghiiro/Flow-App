import * as THREE from 'three';

export interface OccludableMesh {
  mesh: THREE.Mesh;
  originalOpacity: number;
  targetOpacity: number;
}

export interface FoliageFocusActor {
  id: string;
  headPosition: THREE.Vector3;
  isClimbingOrOnTree: boolean;
  isFocused: boolean;
}

export class OcclusionFoliageManager {
  private occludables: OccludableMesh[] = [];

  public registerSceneFoliage(scene3D: THREE.Scene): void {
    this.occludables = [];

    scene3D.traverse((obj) => {
      if ((obj as THREE.Mesh).isMesh) {
        const mesh = obj as THREE.Mesh;
        const name = mesh.name.toLowerCase();
        const parentName = (mesh.parent?.name || '').toLowerCase();

        // Exclude ground terrain, sky, helpers, and actors
        const isExcluded =
          name.includes('ground') ||
          name.includes('terrain') ||
          name.includes('floor') ||
          name.includes('actor_') ||
          name.includes('avatar') ||
          name.includes('skybox') ||
          name.includes('helper') ||
          name.includes('grid') ||
          parentName.includes('actor_') ||
          parentName.includes('avatar');

        if (!isExcluded && mesh.material) {
          const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
          mats.forEach((mat) => {
            mat.transparent = true;
            mat.depthWrite = true;
          });

          this.occludables.push({
            mesh,
            originalOpacity: 1.0,
            targetOpacity: 1.0,
          });
        }
      }
    });
  }

  public update(
    camera: THREE.PerspectiveCamera,
    actors: FoliageFocusActor[],
    delta: number
  ): void {
    if (this.occludables.length === 0) return;

    // Default: 100% solid opacity to preserve natural foreground framing, trees, pillars, and cinematic depth
    for (const item of this.occludables) {
      item.targetOpacity = 1.0;
    }

    const camPos = camera.position;
    const tempBox = new THREE.Box3();
    const tempVec = new THREE.Vector3();

    // 1. Proximity Check: ONLY fade objects that are EXTREMELY close to camera (< 0.65m) and blocking the lens
    const PROXIMITY_THRESHOLD = 0.65;

    for (const item of this.occludables) {
      // Compute bounding box distance to camera for accurate mesh proximity
      if (!item.mesh.geometry) continue;

      if (!item.mesh.geometry.boundingBox) {
        item.mesh.geometry.computeBoundingBox();
      }

      if (item.mesh.geometry.boundingBox) {
        tempBox.copy(item.mesh.geometry.boundingBox).applyMatrix4(item.mesh.matrixWorld);
        tempBox.clampPoint(camPos, tempVec);
        const dist = tempVec.distanceTo(camPos);

        if (dist < PROXIMITY_THRESHOLD) {
          // Object is right in front of camera lens: fade it out smoothly so camera can pass through without clipping
          const factor = THREE.MathUtils.clamp(dist / PROXIMITY_THRESHOLD, 0.15, 0.4);
          item.targetOpacity = Math.min(item.targetOpacity, factor);
        }
      } else {
        // Fallback: world position distance
        item.mesh.getWorldPosition(tempVec);
        const dist = tempVec.distanceTo(camPos);
        if (dist < PROXIMITY_THRESHOLD) {
          item.targetOpacity = 0.25;
        }
      }
    }

    // 2. Climbing Tree Leaves Special Case (only if actor is actively on a tree canopy and focused)
    const treeClimbers = actors.filter((a) => a.isClimbingOrOnTree && a.isFocused);
    if (treeClimbers.length > 0) {
      for (const actor of treeClimbers) {
        for (const item of this.occludables) {
          const name = item.mesh.name.toLowerCase();
          if (name.includes('leaves') || name.includes('foliage') || name.includes('leaf')) {
            item.mesh.getWorldPosition(tempVec);
            if (tempVec.distanceTo(actor.headPosition) < 1.8) {
              item.targetOpacity = Math.min(item.targetOpacity, 0.3);
            }
          }
        }
      }
    }

    // 3. Smoothly lerp opacities
    const lerpSpeed = Math.min(1.0, delta * 10.0);
    for (const item of this.occludables) {
      const mats = Array.isArray(item.mesh.material)
        ? item.mesh.material
        : [item.mesh.material];

      mats.forEach((mat) => {
        mat.opacity = THREE.MathUtils.lerp(
          mat.opacity,
          item.targetOpacity,
          lerpSpeed
        );
      });
    }
  }
}
