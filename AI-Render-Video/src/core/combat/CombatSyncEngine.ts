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

    // Trigger Weapon VFX
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

    // Exact Impact Frame Trigger!
    if (currentTime >= impact_time && currentTime <= impact_time + 1.5) {
      const hitProgress = (currentTime - impact_time) / 1.5;

      // Reaction animation
      targetActor.animator.setAction(target.reaction_anim);
      targetActor.animator.update(currentTime, hitProgress);

      // Facial expression
      targetActor.morph.setExpression(target.facial_expression, Math.max(0, 1 - hitProgress * 0.8));

      // Calculate knockback displacement
      const hitCalc = HitBoxDetector.calculateHit(attacker.avatar, targetActor.avatar);
      const knockbackStep = target.knockback_distance * Math.min(1, hitProgress * 2);
      const originalSpawn = targetActor.config.spawn_point;
      targetActor.avatar.rootObject.position.set(
        originalSpawn[0] + hitCalc.knockbackDirection.x * knockbackStep,
        originalSpawn[1] + (hitProgress < 0.3 ? Math.sin(hitProgress * Math.PI * 3) * 0.4 : 0),
        originalSpawn[2] + hitCalc.knockbackDirection.z * knockbackStep
      );

      // Trigger Impact VFX & Screen Shake once on impact
      if (!state.hasTriggeredImpact) {
        state.hasTriggeredImpact = true;
        this.vfxTrigger.spawnHitSparks(hitCalc.hitPoint);
        this.postProcessor.triggerScreenShake(
          target.screen_shake.intensity,
          target.screen_shake.duration
        );
      }
    }

    // Reset state if scrubbing backwards before start_time
    if (currentTime < start_time) {
      state.hasTriggeredWeaponVFX = false;
      state.hasTriggeredImpact = false;
      targetActor.avatar.rootObject.position.set(...targetActor.config.spawn_point);
    }
  }

  public reset(): void {
    this.triggeredStates.clear();
  }
}
