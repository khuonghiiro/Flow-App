import * as THREE from 'three';

export interface OccludableMesh {
  mesh: THREE.Mesh;
  originalOpacity: number;
  targetOpacity: number;
}

export class OcclusionFoliageManager {
  private occludables: OccludableMesh[] = [];
  private raycaster: THREE.Raycaster = new THREE.Raycaster();

  public registerSceneFoliage(scene3D: THREE.Scene): void {
    this.occludables = [];

    scene3D.traverse((obj) => {
      if ((obj as THREE.Mesh).isMesh) {
        const mesh = obj as THREE.Mesh;
        const name = mesh.name.toLowerCase();
        const parentName = mesh.parent?.name.toLowerCase() || '';

        // Identify foliage, leaves, tree crowns, and branches
        const isFoliage =
          name.includes('leaves') ||
          name.includes('foliage') ||
          name.includes('branch') ||
          parentName.includes('tree');

        if (isFoliage && mesh.material) {
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
    targetPositions: THREE.Vector3[],
    delta: number
  ): void {
    if (this.occludables.length === 0 || targetPositions.length === 0) return;

    // Reset all target opacities to full
    for (const item of this.occludables) {
      item.targetOpacity = 1.0;
    }

    const camPos = camera.position;

    // Raycast from camera to each active actor position
    for (const targetPos of targetPositions) {
      const dir = targetPos.clone().sub(camPos);
      const dist = dir.length();
      if (dist < 0.1) continue;

      dir.normalize();
      this.raycaster.set(camPos, dir);
      this.raycaster.far = dist;

      const meshes = this.occludables.map((o) => o.mesh);
      const hits = this.raycaster.intersectObjects(meshes, false);

      for (const hit of hits) {
        const item = this.occludables.find((o) => o.mesh === hit.object);
        if (item) {
          // Genshin Impact style: Fade occluding foliage to 0.22 transparency
          item.targetOpacity = 0.22;
        }
      }

      // Proximity check: If actor is inside/on tree, fade near leaves
      for (const item of this.occludables) {
        const meshPos = new THREE.Vector3();
        item.mesh.getWorldPosition(meshPos);
        if (meshPos.distanceTo(targetPos) < 2.2) {
          item.targetOpacity = Math.min(item.targetOpacity, 0.35);
        }
      }
    }

    // Smoothly lerp opacities
    const lerpSpeed = delta * 8;
    for (const item of this.occludables) {
      const mats = Array.isArray(item.mesh.material)
        ? item.mesh.material
        : [item.mesh.material];

      mats.forEach((mat) => {
        mat.opacity = THREE.MathUtils.lerp(mat.opacity, item.targetOpacity, Math.min(1, lerpSpeed));
      });
    }
  }
}
