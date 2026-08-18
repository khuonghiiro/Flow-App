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
  isFocused: boolean; // is being inspected, is speaking, or is camera target
}

export class OcclusionFoliageManager {
  private occludables: OccludableMesh[] = [];
  private raycaster: THREE.Raycaster;

  constructor() {
    this.raycaster = new THREE.Raycaster();
  }

  public registerSceneFoliage(scene3D: THREE.Scene): void {
    this.occludables = [];

    scene3D.traverse((obj) => {
      if ((obj as THREE.Mesh).isMesh) {
        const mesh = obj as THREE.Mesh;
        const name = mesh.name.toLowerCase();

        // ONLY leaf clusters/foliage can become translucent!
        // Solid wood trunks and wooden branches are NEVER foliage and stay 100% solid/opaque!
        const isFoliage =
          (name.includes('leaves') || name.includes('foliage') || name.includes('leaf')) &&
          !name.includes('trunk') &&
          !name.includes('branch');

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
    actors: FoliageFocusActor[],
    delta: number
  ): void {
    if (this.occludables.length === 0) return;

    // Default: All foliage is 100% solid/opaque (che kín cây tự nhiên)
    for (const item of this.occludables) {
      item.targetOpacity = 1.0;
    }

    const camPos = camera.position;

    // ONLY activate X-Ray transparency on leaves if the actor is on the tree AND currently focused/speaking/inspected
    const activeTreeClimbers = actors.filter((a) => a.isClimbingOrOnTree && a.isFocused);

    for (const actor of activeTreeClimbers) {
      const targetPos = actor.headPosition;
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
          // Fade occluding foliage to 0.22 transparency smoothly
          item.targetOpacity = 0.22;
        }
      }

      // Proximity check: Fade leaves immediately enveloping the climber or in front of climber's camera POV
      for (const item of this.occludables) {
        const meshPos = new THREE.Vector3();
        item.mesh.getWorldPosition(meshPos);
        const nearClimber = meshPos.distanceTo(targetPos) < 2.0;
        const nearCameraPOV = camPos.distanceTo(targetPos) < 2.2 && meshPos.distanceTo(camPos) < 1.8;
        if (nearClimber || nearCameraPOV) {
          item.targetOpacity = Math.min(item.targetOpacity, 0.25);
        }
      }
    }

    // Smoothly lerp opacities
    const lerpSpeed = delta * 6;
    for (const item of this.occludables) {
      const mats = Array.isArray(item.mesh.material)
        ? item.mesh.material
        : [item.mesh.material];

      mats.forEach((mat) => {
        mat.opacity = THREE.MathUtils.lerp(
          mat.opacity,
          item.targetOpacity,
          Math.min(1, lerpSpeed)
        );
      });
    }
  }
}
