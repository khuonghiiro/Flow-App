import * as THREE from 'three';
import { CropGrowthStage } from '../../types/scene';

export class FarmingSystem {
  public static updateCropGrowth(
    cropObject: THREE.Object3D,
    timeline: CropGrowthStage[],
    currentTime: number
  ): void {
    if (!timeline || timeline.length === 0) return;

    if (currentTime <= timeline[0].time) {
      const s = timeline[0].scale;
      cropObject.scale.set(s, s, s);
      return;
    }

    if (currentTime >= timeline[timeline.length - 1].time) {
      const s = timeline[timeline.length - 1].scale;
      cropObject.scale.set(s, s, s);
      return;
    }

    // Interpolate between keyframe stages
    for (let i = 0; i < timeline.length - 1; i++) {
      const curr = timeline[i];
      const next = timeline[i + 1];

      if (currentTime >= curr.time && currentTime <= next.time) {
        const factor = (currentTime - curr.time) / (next.time - curr.time);
        const scale = THREE.MathUtils.lerp(curr.scale, next.scale, factor);
        cropObject.scale.set(scale, scale, scale);
        break;
      }
    }
  }
}
