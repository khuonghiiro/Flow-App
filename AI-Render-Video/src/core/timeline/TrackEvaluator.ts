import * as THREE from 'three';
import { MasterSceneConfig } from '../../types/scene';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { ActorMorphController } from '../actors/ActorMorphController';
import { ActorLipSync } from '../actors/ActorLipSync';
import { ActorLookAt } from '../actors/ActorLookAt';
import { CombatSyncEngine } from '../combat/CombatSyncEngine';
import { PathNavigator } from '../navigation/PathNavigator';
import { FarmingSystem } from '../interactions/FarmingSystem';
import { ChairInteraction } from '../interactions/ChairInteraction';
import { ClimbingInteraction } from '../interactions/ClimbingInteraction';

export interface ActorRuntime {
  avatar: VRMAvatar;
  animator: ActorAnimator;
  morph: ActorMorphController;
  lipSync: ActorLipSync;
  lookAt: ActorLookAt;
}

export class TrackEvaluator {
  private combatSync: CombatSyncEngine;
  private pathNavigator: PathNavigator;

  constructor(combatSync: CombatSyncEngine, pathNavigator: PathNavigator) {
    this.combatSync = combatSync;
    this.pathNavigator = pathNavigator;
  }

  public evaluate(
    scene: MasterSceneConfig,
    currentTime: number,
    delta: number,
    actorsMap: Map<string, ActorRuntime>,
    sceneObjects: Map<string, THREE.Object3D>,
    controlledActorId: string | null = null
  ): void {
    const actorPositions = new Map<string, THREE.Vector3>();

    // Collect positions
    for (const [id, runtime] of actorsMap.entries()) {
      const pos = new THREE.Vector3();
      runtime.avatar.rootObject.getWorldPosition(pos);
      actorPositions.set(id, pos);
    }

    // 1. Evaluate Actor Tracks (Movement & Speech)
    for (const actorConfig of scene.actors) {
      const runtime = actorsMap.get(actorConfig.id);
      if (!runtime) continue;

      const { avatar, animator, morph, lipSync, lookAt } = runtime;
      const tracks = actorConfig.tracks;

      // Check if this actor is currently targeted by an active combat hit
      const isTargetOfCombatHit = scene.actors.some((a) =>
        (a.tracks.combat_actions || []).some(
          (cb) => cb.target.actor_id === actorConfig.id && currentTime >= cb.impact_time
        )
      );

      // A. Movement Track (if not actively being knocked back and NOT player-controlled)
      let movementHandled = false;
      const isPlayerControlled = actorConfig.id === controlledActorId;

      if (!isTargetOfCombatHit && tracks.movement && !isPlayerControlled) {
        for (const mov of tracks.movement) {
          if (currentTime >= mov.start && currentTime <= mov.end) {
            movementHandled = true;
            const progress = (currentTime - mov.start) / Math.max(0.01, mov.end - mov.start);

            if (mov.action === 'walk' && mov.destination) {
              const path = this.pathNavigator.findPath(actorConfig.spawn_point, mov.destination);
              const sampled = this.pathNavigator.samplePathPosition(path, progress);
              avatar.rootObject.position.set(...sampled.position);
              avatar.rootObject.rotation.y = sampled.rotationY;
              animator.setAction('walk');
              animator.update(currentTime);
            } else if (mov.action === 'sit' && mov.target_object) {
              ChairInteraction.executeSitting(avatar, animator, mov.target_object, progress, currentTime);
            } else if (mov.action === 'climb' && mov.target_object) {
              ClimbingInteraction.executeClimb(avatar, animator, mov.target_object, progress, currentTime);
            } else {
              animator.setAction(mov.action);
              animator.update(currentTime);
            }

            // LookAt tracking
            if (mov.look_at) {
              const targetActorId = mov.look_at.replace('.head', '');
              const targetPos = actorPositions.get(targetActorId);
              if (targetPos) {
                lookAt.setLookAtTarget(targetPos);
              }
            }
            break;
          }
        }
      }

      // If idle (not moving, not actively executing an attack animation, and not hit by combat)
      const isActivelyAttacking = (tracks.combat_actions || []).some((cb) => {
        const duration = (cb.impact_time - cb.start_time) * 1.6;
        return currentTime >= cb.start_time && currentTime <= cb.start_time + duration;
      });

      if (!isTargetOfCombatHit && !movementHandled && !isActivelyAttacking && !isPlayerControlled) {
        animator.setAction('idle');
        animator.update(currentTime);
      }

      // B. Speech Track & Lip-Sync
      let isSpeaking = false;
      if (tracks.speech && scene.dialogues_manifest) {
        for (const sp of tracks.speech) {
          const dlg = scene.dialogues_manifest.find((d) => d.line_id === sp.line_ref);
          if (dlg) {
            const duration = dlg.actual_duration || dlg.estimated_duration || 3.0;
            if (currentTime >= dlg.start_time && currentTime <= dlg.start_time + duration) {
              isSpeaking = true;

              // Apply Expression Keyframes
              if (!isTargetOfCombatHit && sp.expressions) {
                const offset = currentTime - dlg.start_time;
                for (const exp of sp.expressions) {
                  if (offset >= exp.time_offset) {
                    morph.setExpression(exp.type, exp.weight);
                  }
                }
              }
            }
          }
        }
      }

      if (!isTargetOfCombatHit) {
        lipSync.setSpeaking(isSpeaking, currentTime);
        lipSync.update();
      }

      morph.update(delta);
      lookAt.update(delta);
    }

    // 2. Evaluate Combat Actions across all actors
    for (const actorConfig of scene.actors) {
      const runtime = actorsMap.get(actorConfig.id);
      if (!runtime || !actorConfig.tracks.combat_actions) continue;

      for (const action of actorConfig.tracks.combat_actions) {
        const targetRuntime = actorsMap.get(action.target.actor_id);
        const targetConfig = scene.actors.find((a) => a.id === action.target.actor_id);

        if (targetRuntime && targetConfig) {
          this.combatSync.evaluateCombat(
            action,
            { config: actorConfig, avatar: runtime.avatar, animator: runtime.animator },
            {
              config: targetConfig,
              avatar: targetRuntime.avatar,
              animator: targetRuntime.animator,
              morph: targetRuntime.morph,
            },
            currentTime
          );
        }
      }
    }

    // 3. Dynamic World Events (e.g. Farming crop growth)
    if (scene.dynamic_world_events) {
      for (const ev of scene.dynamic_world_events) {
        if (ev.growth_timeline && ev.target) {
          const obj = sceneObjects.get(ev.target);
          if (obj) {
            FarmingSystem.updateCropGrowth(obj, ev.growth_timeline, currentTime);
          }
        }
      }
    }
  }
}
