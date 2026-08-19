import { SlowMotionConfig } from '../../types/combat';
import { MasterClock } from '../timeline/MasterClock';

// ============================================================
// CombatSlowMotion - Slow motion cho khoảnh khắc va chạm
// Giảm MasterClock speed toàn cục cho dramatic effect
// ============================================================

interface ActiveSlowMotion {
  config: SlowMotionConfig;
  startRealTime: number;
  originalTimeScale: number;
  isActive: boolean;
}

export class CombatSlowMotion {
  private clock: MasterClock;
  private activeEffects: ActiveSlowMotion[] = [];
  private currentTimeScale: number = 1.0;

  constructor(clock: MasterClock) {
    this.clock = clock;
  }

  /** Check và trigger slow motion events */
  public evaluate(
    events: SlowMotionConfig[],
    currentTime: number,
    realTime: number
  ): void {
    if (!events || events.length === 0) return;

    for (const config of events) {
      const key = `${config.trigger_time}`;
      const existing = this.activeEffects.find(
        (e) => e.config.trigger_time === config.trigger_time
      );

      // Trigger khi đến thời điểm
      if (currentTime >= config.trigger_time && !existing) {
        this.triggerSlowMotion(config, realTime);
      }
    }

    // Update active effects
    this.updateActiveEffects(realTime);
  }

  /** Kích hoạt slow motion */
  private triggerSlowMotion(config: SlowMotionConfig, realTime: number): void {
    this.activeEffects.push({
      config,
      startRealTime: realTime,
      originalTimeScale: this.currentTimeScale,
      isActive: true,
    });

    this.currentTimeScale = config.time_scale;
  }

  /** Update trạng thái slow motion */
  private updateActiveEffects(realTime: number): void {
    let anyActive = false;

    this.activeEffects = this.activeEffects.filter((effect) => {
      if (!effect.isActive) return false;

      const elapsed = realTime - effect.startRealTime;

      if (elapsed >= effect.config.duration) {
        effect.isActive = false;
        return false; // Remove
      }

      anyActive = true;

      // Smooth ease-in and ease-out
      const normalizedTime = elapsed / effect.config.duration;
      const easeInDuration = 0.15;  // 15% ease in
      const easeOutDuration = 0.25; // 25% ease out

      let scale = effect.config.time_scale;

      if (normalizedTime < easeInDuration) {
        // Ease in: gradually slow down
        const t = normalizedTime / easeInDuration;
        scale = 1.0 + (effect.config.time_scale - 1.0) * this.easeOutCubic(t);
      } else if (normalizedTime > 1 - easeOutDuration) {
        // Ease out: gradually speed up
        const t = (normalizedTime - (1 - easeOutDuration)) / easeOutDuration;
        scale = effect.config.time_scale + (1.0 - effect.config.time_scale) * this.easeInCubic(t);
      }

      this.currentTimeScale = Math.max(0.05, scale);
      return true;
    });

    // Restore normal speed khi không còn effect nào active
    if (!anyActive && this.currentTimeScale !== 1.0) {
      this.currentTimeScale = 1.0;
    }
  }

  /** Lấy time scale hiện tại */
  public getTimeScale(): number {
    return this.currentTimeScale;
  }

  /** Có đang slow motion? */
  public isSlowMotion(): boolean {
    return this.currentTimeScale < 0.95;
  }

  /** Lấy blur intensity cho slow motion */
  public getBlurIntensity(): number {
    if (!this.isSlowMotion()) return 0;

    const activeEffect = this.activeEffects.find((e) => e.isActive);
    return activeEffect?.config.blur_intensity || 0;
  }

  /** Lấy desaturation cho slow motion */
  public getDesaturation(): number {
    if (!this.isSlowMotion()) return 0;

    const activeEffect = this.activeEffects.find((e) => e.isActive);
    return activeEffect?.config.desaturation || 0;
  }

  /** Lấy focus actor cho camera */
  public getFocusActor(): string | null {
    const activeEffect = this.activeEffects.find((e) => e.isActive);
    return activeEffect?.config.focus_actor || null;
  }

  /** Reset */
  public reset(): void {
    this.activeEffects = [];
    this.currentTimeScale = 1.0;
  }

  // ============================================================
  // Easing functions
  // ============================================================

  private easeOutCubic(t: number): number {
    return 1 - Math.pow(1 - t, 3);
  }

  private easeInCubic(t: number): number {
    return t * t * t;
  }
}
