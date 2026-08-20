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
    targetActor?: {
      config: ActorConfig;
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
    },
    currentTime?: number
  ): void {
    const time = currentTime ?? 0;
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
    if (time >= start_time && time <= end_time) {
      const progress = (time - start_time) / duration;
      attacker.animator.setAction(anim);
      attacker.animator.update(time, progress);
    }

    // Trigger Weapon VFX (e.g. flaming sword trail)
    if (weapon_vfx && time >= weapon_vfx.start && time <= weapon_vfx.end) {
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

    // Exact Impact Frame & Sustained Knockback Reaction (if target & targetActor exist)
    if (target && targetActor && time >= impact_time) {
      const hitElapsed = time - impact_time;
      const hitProgress = Math.min(1, hitElapsed / 1.5);

      // Reaction animation
      if (target.reaction_anim) {
        targetActor.animator.setAction(target.reaction_anim);
        targetActor.animator.update(time, hitProgress);
      }

      // Set Pain Facial Expression
      if (target.facial_expression) {
        const painWeight = Math.max(0.2, 1 - hitElapsed / 4.0);
        targetActor.morph.setExpression(target.facial_expression, painWeight);
      }

      // Calculate knockback displacement
      if (target.knockback_distance) {
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
      }

      // Trigger Impact Hit Sparks & Screen Shake once on impact frame
      if (!state.hasTriggeredImpact) {
        state.hasTriggeredImpact = true;
        const hitCalc = HitBoxDetector.calculateHit(attacker.avatar, targetActor.avatar);
        this.vfxTrigger.spawnHitSparks(hitCalc.hitPoint);
        if (target.screen_shake) {
          this.postProcessor.triggerScreenShake(
            target.screen_shake.intensity || 0.3,
            target.screen_shake.duration || 0.3,
            time
          );
        }
      }
    }

    // Reset state if scrubbing backwards
    if (time < start_time) {
      state.hasTriggeredWeaponVFX = false;
      state.hasTriggeredImpact = false;
      if (targetActor) {
        targetActor.avatar.rootObject.position.set(...targetActor.config.spawn_point);
        targetActor.morph.setExpression('neutral', 1.0);
      }
    } else if (time < impact_time) {
      state.hasTriggeredImpact = false;
      if (targetActor) {
        targetActor.avatar.rootObject.position.set(...targetActor.config.spawn_point);
        targetActor.morph.setExpression('neutral', 1.0);
      }
    }
  }

  public reset(): void {
    this.triggeredStates.clear();
  }
}
