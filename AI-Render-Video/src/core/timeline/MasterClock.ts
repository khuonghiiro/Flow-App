export class MasterClock {
  public currentTime: number = 0;
  public duration: number = 25.0;
  public isPlaying: boolean = false;
  public playbackRate: number = 1.0;
  public isLooping: boolean = true;
  private listeners: Array<(time: number) => void> = [];

  constructor(duration: number = 25.0) {
    this.duration = duration;
  }

  public play(): void {
    this.isPlaying = true;
  }

  public pause(): void {
    this.isPlaying = false;
  }

  public toggle(): void {
    this.isPlaying = !this.isPlaying;
  }

  public seek(time: number): void {
    this.currentTime = Math.max(0, Math.min(this.duration, time));
    this.notify();
  }

  public setDuration(duration: number): void {
    this.duration = Math.max(1, duration);
    if (this.currentTime > this.duration) {
      this.currentTime = this.duration;
    }
  }

  public update(delta: number): void {
    if (!this.isPlaying) return;

    this.currentTime += delta * this.playbackRate;

    if (this.currentTime >= this.duration) {
      if (this.isLooping) {
        this.currentTime = 0;
      } else {
        this.currentTime = this.duration;
        this.isPlaying = false;
      }
    }

    this.notify();
  }

  public onTimeUpdate(callback: (time: number) => void): void {
    this.listeners.push(callback);
  }

  public removeTimeUpdate(callback: (time: number) => void): void {
    this.listeners = this.listeners.filter((cb) => cb !== callback);
  }

  private notify(): void {
    for (let i = 0; i < this.listeners.length; i++) {
      this.listeners[i](this.currentTime);
    }
  }
}
