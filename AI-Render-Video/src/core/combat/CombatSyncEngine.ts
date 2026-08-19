import * as THREE from 'three';
import { CombatActionItem, ActorConfig } from '../../types/scene';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { ActorMorphController } from '../actors/ActorMorphController';
import { CombatVFXTrigger } from './CombatVFXTrigger';
import { HitBoxDetector } from './HitBoxDetector';
import { PostProcessor } from '../engine/PostProcessor';

export interface CombatEventState {
  hasTriggeredWeaponVFX: boolean;
  hasTriggeredImpact: boolean;
}

export class CombatSyncEngine {
  private vfxTrigger: CombatVFXTrigger;
  private postProcessor: PostProcessor;
  private triggeredStates: Map<string, CombatEventState> = new Map();

  constructor(vfxTrigger: CombatVFXTrigger, postProcessor: PostProcessor) {
    this.vfxTrigger = vfxTrigger;
    this.postProcessor = postProcessor;
  }

  public evaluateCombat(
    action: CombatActionItem,
    attacker: { config: ActorConfig; avatar: VRMAvatar; animator: ActorAnimator },
    targetActor: {
      config: ActorConfig;
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
    },
    currentTime: number
  ): void {
    const actionKey = `${attacker.config.id}_${action.start_time}_${action.impact_time}`;
    let state = this.triggeredStates.get(actionKey);
    if (!state) {
      state = { hasTriggeredWeaponVFX: false, hasTriggeredImpact: false };
      this.triggeredStates.set(actionKey, state);
    }

    const { start_time, impact_time, anim, weapon_vfx, target } = action;
    const duration = (impact_time - start_time) * 1.6; // Full animation duration
    const end_time = start_time + duration;

    // Attacker Action Animation
    if (currentTime >= start_time && currentTime <= end_time) {
      const progress = (currentTime - start_time) / duration;
      attacker.animator.setAction(anim);
      attacker.animator.update(currentTime, progress);
    }

    // Trigger Weapon VFX (e.g. flaming sword trail)
    if (weapon_vfx && currentTime >= weapon_vfx.start && currentTime <= weapon_vfx.end) {
      if (!state.hasTriggeredWeaponVFX) {
        state.hasTriggeredWeaponVFX = true;
        const weaponTip = attacker.avatar.rootObject.getObjectByName('weapon_tip');
        const pos = new THREE.Vector3();
        if (weaponTip) {
          weaponTip.getWorldPosition(pos);
        } else {
          attacker.avatar.rootObject.getWorldPosition(pos);
          pos.y += 1.2;
        }
        this.vfxTrigger.spawnSlashTrail(pos, new THREE.Vector3(1, 0, 0));
      }
    }

    // Exact Impact Frame & Sustained Knockback Reaction
    if (currentTime >= impact_time) {
      const hitElapsed = currentTime - impact_time;
      const hitProgress = Math.min(1, hitElapsed / 1.5);

      // Reaction animation
      targetActor.animator.setAction(target.reaction_anim);
      targetActor.animator.update(currentTime, hitProgress);

      // Set Pain Facial Expression
      const painWeight = Math.max(0.2, 1 - hitElapsed / 4.0);
      targetActor.morph.setExpression(target.facial_expression, painWeight);

      // Calculate knockback displacement
      const hitCalc = HitBoxDetector.calculateHit(attacker.avatar, targetActor.avatar);
      const knockbackStep = target.knockback_distance * Math.min(1, hitProgress * 2.5);
      const originalSpawn = targetActor.config.spawn_point;

      // Arc flight during knockback
      const flyHeight = hitProgress < 0.6 ? Math.sin(hitProgress * Math.PI * (1 / 0.6)) * 0.7 : 0;
      targetActor.avatar.rootObject.position.set(
        originalSpawn[0] + hitCalc.knockbackDirection.x * knockbackStep,
        originalSpawn[1] + flyHeight,
        originalSpawn[2] + hitCalc.knockbackDirection.z * knockbackStep
      );

      // Trigger Impact Hit Sparks & Screen Shake once on impact frame
      if (!state.hasTriggeredImpact) {
        state.hasTriggeredImpact = true;
        this.vfxTrigger.spawnHitSparks(hitCalc.hitPoint);
        this.postProcessor.triggerScreenShake(
          target.screen_shake.intensity,
          target.screen_shake.duration,
          currentTime
        );
      }
    }

    // Reset state if scrubbing backwards
    if (currentTime < start_time) {
      state.hasTriggeredWeaponVFX = false;
      state.hasTriggeredImpact = false;
      targetActor.avatar.rootObject.position.set(...targetActor.config.spawn_point);
      targetActor.morph.setExpression('neutral', 1.0);
    } else if (currentTime < impact_time) {
      state.hasTriggeredImpact = false;
      targetActor.avatar.rootObject.position.set(...targetActor.config.spawn_point);
      targetActor.morph.setExpression('neutral', 1.0);
    }
  }

  public reset(): void {
    this.triggeredStates.clear();
  }
}
