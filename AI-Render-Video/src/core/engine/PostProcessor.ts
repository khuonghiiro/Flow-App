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

  public triggerScreenShake(intensity: number = 0.35, duration: number = 0.25, timeStamp?: number): void {
    this.activeShakes.push({
      intensity,
      duration,
      startTime: timeStamp !== undefined ? timeStamp : (performance.now() / 1000),
    });
  }

  public update(currentTimeSeconds: number, isPlaying: boolean = true): THREE.Vector3 {
    this.shakeOffset.set(0, 0, 0);

    // If playback is paused or stopped, NEVER apply random camera shakes
    if (!isPlaying || this.activeShakes.length === 0) {
      if (!isPlaying) this.activeShakes = [];
      return this.shakeOffset;
    }

    const nowRealTime = performance.now() / 1000;

    // Filter and accumulate active screen shakes
    this.activeShakes = this.activeShakes.filter((shake) => {
      const isTimelineTime = shake.startTime < 1000.0;
      const elapsed = isTimelineTime
        ? currentTimeSeconds - shake.startTime
        : nowRealTime - shake.startTime;

      if (elapsed >= 0 && elapsed < shake.duration) {
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

  public applyToCamera(camera: THREE.Camera, currentTimeSeconds: number, isPlaying: boolean = true): void {
    if (!isPlaying) return;
    const offset = this.update(currentTimeSeconds, isPlaying);
    camera.position.add(offset);
  }

  public clear(): void {
    this.activeShakes = [];
    this.shakeOffset.set(0, 0, 0);
  }
}
