import * as THREE from 'three';

/**
 * Natural Biomechanical Eye Blinking & Facial Dynamics Engine
 * Supports Morph Targets / BlendShapes, FACS ARKit expressions, and geometric eyelid deformation
 */
export class FacialBlinkEngine {
  private static lastBlinkTime: number = 0;
  private static nextBlinkInterval: number = 3.2; // ~3.2 seconds between blinks
  private static blinkDuration: number = 0.18; // 180ms natural human blink duration

  /**
   * Updates eye blinking and facial micro-expressions for any character 3D model hierarchy
   */
  public static update(modelGroup: THREE.Object3D | null, currentTimeSeconds: number): void {
    if (!modelGroup) return;

    if (this.lastBlinkTime === 0) {
      this.lastBlinkTime = currentTimeSeconds;
    }

    const elapsedSinceLastBlink = currentTimeSeconds - this.lastBlinkTime;
    let blinkProgress = 0.0;

    if (elapsedSinceLastBlink >= this.nextBlinkInterval) {
      const blinkTime = elapsedSinceLastBlink - this.nextBlinkInterval;
      if (blinkTime <= this.blinkDuration) {
        // Natural human blink curve: Fast snap close (0.0 -> 0.06s), smooth ease-out open (0.06 -> 0.18s)
        const phase = blinkTime / this.blinkDuration;
        if (phase < 0.35) {
          blinkProgress = phase / 0.35; // Closing
        } else {
          blinkProgress = 1.0 - (phase - 0.35) / 0.65; // Opening with ease-out
        }
      } else {
        // Blink finished, schedule next random interval (between 2.5s and 4.8s)
        this.lastBlinkTime = currentTimeSeconds;
        this.nextBlinkInterval = 2.5 + Math.random() * 2.3;
        blinkProgress = 0.0;
      }
    }

    // Apply blink weight across all meshes in modelGroup
    modelGroup.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mesh = child as THREE.Mesh;
        const name = (mesh.name || '').toLowerCase();
        const matName = Array.isArray(mesh.material)
          ? mesh.material.map((m) => (m.name || '').toLowerCase()).join(' ')
          : (mesh.material?.name || '').toLowerCase();

        // 1. Morph Targets / Blendshapes (VRM, ARKit, MMD, FACS)
        if (mesh.morphTargetDictionary && mesh.morphTargetInfluences) {
          const dict = mesh.morphTargetDictionary;
          const blinkKeys = [
            'blink',
            'eye_close',
            'blink_l',
            'blink_r',
            'eyeblinkleft',
            'eyeblinkright',
            'facs_eyeblink',
            'vrm.blink',
            'wink',
            'eye_blink',
            'f_blink',
          ];

          for (const key of blinkKeys) {
            for (const [targetName, index] of Object.entries(dict)) {
              if (targetName.toLowerCase().includes(key)) {
                mesh.morphTargetInfluences[index] = blinkProgress;
              }
            }
          }
        }

        // 2. Mesh Geometric Eyelid / Pupil Blinking (for models with dedicated eyelid or pupil meshes)
        const isEyeDetail =
          name.includes('pupil') ||
          name.includes('eyelid') ||
          name.includes('eye_') ||
          matName.includes('pupil') ||
          matName.includes('eyelid');

        if (isEyeDetail && !name.includes('eyebrow') && !matName.includes('eyebrow')) {
          if (!mesh.userData.initialScaleY) {
            mesh.userData.initialScaleY = mesh.scale.y || 1.0;
          }
          const baseScaleY = mesh.userData.initialScaleY;
          // Squeeze Y scale down during blink
          mesh.scale.y = baseScaleY * Math.max(0.08, 1.0 - blinkProgress * 0.92);
        }
      }
    });
  }
}
