import * as THREE from 'three';
import { ComboChain, ComboHit, DamageType } from '../../types/combat';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { ActorMorphController } from '../actors/ActorMorphController';
import { CombatVFXTrigger } from './CombatVFXTrigger';
import { HitBoxDetector } from './HitBoxDetector';
import { PostProcessor } from '../engine/PostProcessor';

// ============================================================
// CombatComboSystem - Chuỗi đòn combo liên hoàn
// ============================================================

interface ComboState {
  comboId: string;
  currentHitIndex: number;
  triggeredHits: Set<number>;  // Index các đòn đã trigger
}

export class CombatComboSystem {
  private vfxTrigger: CombatVFXTrigger;
  private postProcessor: PostProcessor;
  private comboStates: Map<string, ComboState> = new Map();

  constructor(vfxTrigger: CombatVFXTrigger, postProcessor: PostProcessor) {
    this.vfxTrigger = vfxTrigger;
    this.postProcessor = postProcessor;
  }

  /**
   * Evaluate combo chain tại thời điểm currentTime.
   * Xử lý từng hit trong chuỗi combo.
   */
  public evaluateCombo(
    combo: ComboChain,
    attacker: { avatar: VRMAvatar; animator: ActorAnimator },
    target: {
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
      spawnPoint: [number, number, number];
    },
    currentTime: number
  ): void {
    const comboStart = combo.start_time;
    const comboEnd = comboStart + combo.total_duration;

    // Chưa đến lúc hoặc đã qua
    if (currentTime < comboStart || currentTime > comboEnd + 1.0) return;

    // Lấy/tạo combo state
    let state = this.comboStates.get(combo.combo_id);
    if (!state) {
      state = { comboId: combo.combo_id, currentHitIndex: 0, triggeredHits: new Set() };
      this.comboStates.set(combo.combo_id, state);
    }

    // Reset nếu scrub backwards
    if (currentTime < comboStart) {
      state.triggeredHits.clear();
      state.currentHitIndex = 0;
      target.avatar.rootObject.position.set(...target.spawnPoint);
      target.morph.setExpression('neutral', 1.0);
      return;
    }

    // Evaluate từng hit trong combo
    for (let i = 0; i < combo.hits.length; i++) {
      const hit = combo.hits[i];
      const hitStart = comboStart + hit.relative_start;
      const hitImpact = hitStart + hit.impact_delay;
      const hitEnd = hitImpact + this.getReactionDuration(hit.damage_type);

      // Attacker animation cho hit này
      if (currentTime >= hitStart && currentTime <= hitEnd) {
        this.applyAttackerAnimation(attacker, hit, currentTime, hitStart, hitEnd);
      }

      // Impact & reaction
      if (currentTime >= hitImpact && !state.triggeredHits.has(i)) {
        state.triggeredHits.add(i);
        state.currentHitIndex = i;
        this.triggerHitImpact(hit, attacker, target, i === combo.hits.length - 1);
      }

      // Sustained reaction animation
      if (currentTime >= hitImpact && currentTime <= hitEnd) {
        const reactionProgress = (currentTime - hitImpact) / (hitEnd - hitImpact);
        this.applyReactionAnimation(target, hit, reactionProgress);
      }
    }
  }

  /** Animation cho attacker cho 1 đòn trong combo */
  private applyAttackerAnimation(
    attacker: { avatar: VRMAvatar; animator: ActorAnimator },
    hit: ComboHit,
    currentTime: number,
    hitStart: number,
    hitEnd: number
  ): void {
    const progress = (currentTime - hitStart) / (hitEnd - hitStart);
    attacker.animator.setAction(hit.anim as any);
    attacker.animator.update(currentTime, progress);
  }

  /** Trigger impact: VFX, screen shake, facial */
  private triggerHitImpact(
    hit: ComboHit,
    attacker: { avatar: VRMAvatar; animator: ActorAnimator },
    target: {
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
    },
    isFinisher: boolean
  ): void {
    const hitCalc = HitBoxDetector.calculateHit(attacker.avatar, target.avatar);

    // VFX tại điểm va chạm
    if (hit.hit_vfx || isFinisher) {
      this.vfxTrigger.spawnHitSparks(hitCalc.hitPoint);
    }

    // Screen shake (mạnh hơn cho finisher)
    const shakeIntensity = hit.screen_shake_intensity
      || (isFinisher ? 0.5 : this.getShakeByDamage(hit.damage_type));
    this.postProcessor.triggerScreenShake(shakeIntensity, isFinisher ? 0.4 : 0.2);

    // Facial expression
    target.morph.setExpression('pain', 1.0);
  }

  /** Animation phản ứng khi bị đánh */
  private applyReactionAnimation(
    target: {
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
      spawnPoint: [number, number, number];
    },
    hit: ComboHit,
    progress: number
  ): void {
    // Set reaction animation
    const reactionAnim = this.mapReactionToAnim(hit.target_reaction);
    target.animator.setAction(reactionAnim);
    target.animator.update(0, progress);

    // Knockback displacement
    if (hit.knockback > 0) {
      const knockbackStep = hit.knockback * Math.min(1, progress * 2.0);
      const hitCalc = HitBoxDetector.calculateHit(
        { rootObject: target.avatar.rootObject } as VRMAvatar,
        target.avatar
      );

      // Arc flight for heavy hits
      const flyHeight = this.shouldLaunch(hit.target_reaction)
        ? Math.sin(progress * Math.PI) * 0.8
        : 0;

      target.avatar.rootObject.position.set(
        target.spawnPoint[0] + hitCalc.knockbackDirection.x * knockbackStep,
        target.spawnPoint[1] + flyHeight,
        target.spawnPoint[2] + hitCalc.knockbackDirection.z * knockbackStep
      );
    }

    // Pain expression decay
    const painWeight = Math.max(0.3, 1 - progress * 0.7);
    target.morph.setExpression('pain', painWeight);
  }

  /** Map hit reaction → AnimationAction */
  private mapReactionToAnim(reaction: string): any {
    switch (reaction) {
      case 'flinch': return 'stagger_back';
      case 'stagger': return 'stagger_back';
      case 'stagger_back': return 'stagger_back';
      case 'fly_back': return 'fly_back_knockdown';
      case 'launch_air': return 'fly_back_knockdown';
      case 'ground_bounce': return 'fly_back_knockdown';
      case 'spin_fall': return 'fly_back_knockdown';
      case 'block_impact': return 'block_defend';
      case 'dodge_evade': return 'dodge';
      default: return 'stagger_back';
    }
  }

  /** Có nên launch lên không? */
  private shouldLaunch(reaction: string): boolean {
    return ['launch_air', 'ground_bounce', 'fly_back'].includes(reaction);
  }

  /** Thời gian reaction theo damage type */
  private getReactionDuration(type: DamageType): number {
    switch (type) {
      case 'light': return 0.4;
      case 'medium': return 0.7;
      case 'heavy': return 1.0;
      case 'finisher': return 1.5;
      case 'special': return 1.2;
      default: return 0.5;
    }
  }

  /** Screen shake intensity theo damage type */
  private getShakeByDamage(type: DamageType): number {
    switch (type) {
      case 'light': return 0.1;
      case 'medium': return 0.2;
      case 'heavy': return 0.35;
      case 'finisher': return 0.5;
      case 'special': return 0.4;
      default: return 0.15;
    }
  }

  /** Reset tất cả combo states */
  public reset(): void {
    this.comboStates.clear();
  }
}
