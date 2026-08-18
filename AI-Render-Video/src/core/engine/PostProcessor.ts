import * as THREE from 'three';

export interface ShakeEvent {
  intensity: number;
  duration: number;
  startTime: number;
}

export class PostProcessor {
  private activeShakes: ShakeEvent[] = [];
  public shakeOffset: THREE.Vector3 = new THREE.Vector3();
  public bloomStrength: number = 0.5;
  public vignetteDarkness: number = 0.8;

  public triggerScreenShake(intensity: number = 0.35, duration: number = 0.25): void {
    this.activeShakes.push({
      intensity,
      duration,
      startTime: performance.now() / 1000,
    });
  }

  public update(currentTimeSeconds: number): THREE.Vector3 {
    this.shakeOffset.set(0, 0, 0);

    // Filter and accumulate active screen shakes
    this.activeShakes = this.activeShakes.filter((shake) => {
      const elapsed = currentTimeSeconds - shake.startTime;
      if (elapsed < shake.duration && elapsed >= 0) {
        const decay = 1 - elapsed / shake.duration;
        const currentIntensity = shake.intensity * decay;
        this.shakeOffset.x += (Math.random() - 0.5) * 2 * currentIntensity;
        this.shakeOffset.y += (Math.random() - 0.5) * 2 * currentIntensity;
        this.shakeOffset.z += (Math.random() - 0.5) * 1 * currentIntensity;
        return true;
      }
      return false;
    });

    return this.shakeOffset;
  }

  public applyToCamera(camera: THREE.Camera, currentTimeSeconds: number): void {
    const offset = this.update(currentTimeSeconds);
    camera.position.add(offset);
  }

  public clear(): void {
    this.activeShakes = [];
    this.shakeOffset.set(0, 0, 0);
  }
}
