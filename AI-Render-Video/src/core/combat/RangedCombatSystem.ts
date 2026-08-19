import * as THREE from 'three';
import { RangedCombatAction, ProjectileConfig } from '../../types/combat';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { ActorMorphController } from '../actors/ActorMorphController';
import { CombatVFXTrigger } from './CombatVFXTrigger';
import { PostProcessor } from '../engine/PostProcessor';

// ============================================================
// RangedCombatSystem - Combat từ xa (phép thuật, cung tên)
// ============================================================

interface ActiveProjectile {
  actionId: string;
  mesh: THREE.Object3D;
  trail: THREE.Object3D | null;
  startPos: THREE.Vector3;
  targetPos: THREE.Vector3;
  speed: number;
  progress: number;
  arcHeight: number;
  homing: boolean;
  config: ProjectileConfig;
}

interface RangedState {
  hasCasted: boolean;
  hasReleased: boolean;
  hasImpacted: boolean;
}

export class RangedCombatSystem {
  private scene: THREE.Scene;
  private vfxTrigger: CombatVFXTrigger;
  private postProcessor: PostProcessor;
  private projectiles: ActiveProjectile[] = [];
  private states: Map<string, RangedState> = new Map();

  constructor(
    scene: THREE.Scene,
    vfxTrigger: CombatVFXTrigger,
    postProcessor: PostProcessor
  ) {
    this.scene = scene;
    this.vfxTrigger = vfxTrigger;
    this.postProcessor = postProcessor;
  }

  /** Evaluate ranged combat action */
  public evaluateRanged(
    action: RangedCombatAction,
    caster: { avatar: VRMAvatar; animator: ActorAnimator },
    target: {
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
      spawnPoint: [number, number, number];
    },
    currentTime: number,
    delta: number
  ): void {
    const stateKey = `${action.caster_id}_${action.start_time}`;
    let state = this.states.get(stateKey);
    if (!state) {
      state = { hasCasted: false, hasReleased: false, hasImpacted: false };
      this.states.set(stateKey, state);
    }

    // Reset khi scrub backwards
    if (currentTime < action.start_time) {
      this.resetState(stateKey);
      return;
    }

    // Phase 1: Cast / Charge (start_time → release_time)
    if (currentTime >= action.start_time && currentTime < action.release_time) {
      this.executeCastPhase(action, caster, state, currentTime);
    }

    // Phase 2: Release Projectile (release_time)
    if (currentTime >= action.release_time && !state.hasReleased) {
      state.hasReleased = true;
      this.spawnProjectile(action, caster, target);
    }

    // Phase 3: Projectile flight (release_time → impact_time)
    if (currentTime >= action.release_time && currentTime < action.impact_time) {
      const flightProgress = (currentTime - action.release_time)
        / (action.impact_time - action.release_time);
      this.updateProjectileFlight(stateKey, flightProgress, target);
      caster.animator.setAction(action.release_anim as any);
      caster.animator.update(currentTime);
    }

    // Phase 4: Impact
    if (currentTime >= action.impact_time && !state.hasImpacted) {
      state.hasImpacted = true;
      this.triggerImpact(action, target, stateKey);
    }

    // Phase 5: Target reaction after impact
    if (currentTime >= action.impact_time) {
      const reactionElapsed = currentTime - action.impact_time;
      this.applyTargetReaction(action, target, reactionElapsed);
    }

    // Update projectile physics
    this.updateProjectiles(delta);
  }

  /** Phase 1: Cast animation + charge VFX */
  private executeCastPhase(
    action: RangedCombatAction,
    caster: { avatar: VRMAvatar; animator: ActorAnimator },
    state: RangedState,
    currentTime: number
  ): void {
    const castProgress = (currentTime - action.start_time) / action.cast_time;

    caster.animator.setAction(action.cast_anim as any);
    caster.animator.update(currentTime, castProgress);

    // Spawn charge VFX (once)
    if (!state.hasCasted && action.charge_vfx) {
      state.hasCasted = true;
      const pos = new THREE.Vector3();
      const weaponTip = caster.avatar.rootObject.getObjectByName('weapon_tip');
      if (weaponTip) {
        weaponTip.getWorldPosition(pos);
      } else {
        caster.avatar.rootObject.getWorldPosition(pos);
        pos.y += 1.2;
      }
      // Charge aura effect
      this.vfxTrigger.spawnMagicShield(caster.avatar.rootObject, action.cast_time);
    }
  }

  /** Spawn projectile mesh */
  private spawnProjectile(
    action: RangedCombatAction,
    caster: { avatar: VRMAvatar },
    target: { avatar: VRMAvatar }
  ): void {
    const startPos = new THREE.Vector3();
    const weaponTip = caster.avatar.rootObject.getObjectByName('weapon_tip');
    if (weaponTip) {
      weaponTip.getWorldPosition(startPos);
    } else {
      caster.avatar.rootObject.getWorldPosition(startPos);
      startPos.y += 1.2;
    }

    const targetPos = new THREE.Vector3();
    target.avatar.rootObject.getWorldPosition(targetPos);
    targetPos.y += 1.0;

    const proj = action.projectile;
    const mesh = this.createProjectileMesh(proj);
    mesh.position.copy(startPos);
    this.scene.add(mesh);

    const stateKey = `${action.caster_id}_${action.start_time}`;
    this.projectiles.push({
      actionId: stateKey,
      mesh,
      trail: null,
      startPos: startPos.clone(),
      targetPos: targetPos.clone(),
      speed: proj.speed,
      progress: 0,
      arcHeight: proj.arc_height || 0,
      homing: proj.homing || false,
      config: proj,
    });
  }

  /** Create projectile 3D mesh */
  private createProjectileMesh(config: ProjectileConfig): THREE.Group {
    const group = new THREE.Group();
    group.name = 'projectile';

    const color = parseInt(config.color.replace('#', ''), 16) || 0xff4400;
    const size = config.size || 0.15;

    const coreGeo = new THREE.SphereGeometry(size, 12, 12);
    const coreMat = new THREE.MeshStandardMaterial({
      color,
      emissive: color,
      emissiveIntensity: 1.2,
      transparent: true,
      opacity: 0.9,
    });
    const core = new THREE.Mesh(coreGeo, coreMat);
    group.add(core);

    // Outer glow
    const glowGeo = new THREE.SphereGeometry(size * 2, 8, 8);
    const glowMat = new THREE.MeshBasicMaterial({
      color,
      transparent: true,
      opacity: 0.3,
      blending: THREE.AdditiveBlending,
    });
    group.add(new THREE.Mesh(glowGeo, glowMat));

    return group;
  }

  /** Update projectile flight position */
  private updateProjectileFlight(
    stateKey: string,
    progress: number,
    target: { avatar: VRMAvatar }
  ): void {
    const proj = this.projectiles.find((p) => p.actionId === stateKey);
    if (!proj) return;

    // Update target position if homing
    if (proj.homing) {
      target.avatar.rootObject.getWorldPosition(proj.targetPos);
      proj.targetPos.y += 1.0;
    }

    const p = Math.min(1, progress);
    proj.progress = p;

    // Lerp position with optional arc
    const pos = new THREE.Vector3().lerpVectors(proj.startPos, proj.targetPos, p);
    if (proj.arcHeight > 0) {
      pos.y += Math.sin(p * Math.PI) * proj.arcHeight;
    }

    proj.mesh.position.copy(pos);

    // Rotate to face direction of travel
    const dir = proj.targetPos.clone().sub(proj.startPos).normalize();
    proj.mesh.lookAt(proj.mesh.position.clone().add(dir));

    // Scale pulse
    const pulse = 1 + Math.sin(p * Math.PI * 8) * 0.1;
    proj.mesh.scale.setScalar(pulse);
  }

  /** Trigger impact effects */
  private triggerImpact(
    action: RangedCombatAction,
    target: { avatar: VRMAvatar; morph: ActorMorphController },
    stateKey: string
  ): void {
    // Remove projectile mesh
    const proj = this.projectiles.find((p) => p.actionId === stateKey);
    if (proj) {
      this.scene.remove(proj.mesh);
      this.projectiles = this.projectiles.filter((p) => p.actionId !== stateKey);
    }

    // Impact VFX
    const impactPos = new THREE.Vector3();
    target.avatar.rootObject.getWorldPosition(impactPos);
    impactPos.y += 1.0;
    this.vfxTrigger.spawnHitSparks(impactPos);

    // Screen shake
    if (action.screen_shake) {
      this.postProcessor.triggerScreenShake(
        action.screen_shake.intensity,
        action.screen_shake.duration
      );
    }

    // Facial
    target.morph.setExpression(action.target_facial as any, 1.0);
  }

  /** Apply sustained reaction to target */
  private applyTargetReaction(
    action: RangedCombatAction,
    target: {
      avatar: VRMAvatar;
      animator: ActorAnimator;
      morph: ActorMorphController;
      spawnPoint: [number, number, number];
    },
    elapsed: number
  ): void {
    const reactionDuration = 1.5;
    const progress = Math.min(1, elapsed / reactionDuration);

    // Map reaction to animation
    const reactionAnim = action.target_reaction === 'fly_back'
      ? 'fly_back_knockdown' : 'stagger_back';
    target.animator.setAction(reactionAnim as any);
    target.animator.update(0, progress);

    // Knockback
    if (action.target_knockback > 0) {
      const kbStep = action.target_knockback * Math.min(1, progress * 2);
      const dir = new THREE.Vector3();
      target.avatar.rootObject.getWorldPosition(dir);
      const casterPos = new THREE.Vector3(...target.spawnPoint);
      dir.sub(casterPos).normalize();
      dir.y = 0;

      target.avatar.rootObject.position.set(
        target.spawnPoint[0] + dir.x * kbStep,
        target.spawnPoint[1],
        target.spawnPoint[2] + dir.z * kbStep
      );
    }

    target.morph.setExpression('pain', Math.max(0.3, 1 - progress * 0.5));
  }

  /** Update tất cả projectiles đang bay */
  private updateProjectiles(delta: number): void {
    // Cleanup finished projectiles
    this.projectiles = this.projectiles.filter((p) => {
      if (p.progress >= 1) {
        this.scene.remove(p.mesh);
        return false;
      }
      return true;
    });
  }

  /** Reset state cho action cụ thể */
  private resetState(stateKey: string): void {
    this.states.delete(stateKey);
    const proj = this.projectiles.find((p) => p.actionId === stateKey);
    if (proj) {
      this.scene.remove(proj.mesh);
      this.projectiles = this.projectiles.filter((p) => p.actionId !== stateKey);
    }
  }

  /** Reset all */
  public reset(): void {
    for (const p of this.projectiles) {
      this.scene.remove(p.mesh);
    }
    this.projectiles = [];
    this.states.clear();
  }
}
