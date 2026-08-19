import * as THREE from 'three';
import { TransformConfig } from '../../types/interactions';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';
import { CombatVFXTrigger } from '../combat/CombatVFXTrigger';
import { PostProcessor } from '../engine/PostProcessor';

// ============================================================
// TransformationSystem - Biến thân nhân vật
// Đổi costume, morph body, spawn VFX aura
// ============================================================

interface TransformState {
  configId: string;
  phase: 'pre' | 'during' | 'post' | 'complete';
  startedVFX: boolean;
  completedSwap: boolean;
  completedFlash: boolean;
}

export class TransformationSystem {
  private vfxTrigger: CombatVFXTrigger;
  private postProcessor: PostProcessor;
  private states: Map<string, TransformState> = new Map();

  constructor(vfxTrigger: CombatVFXTrigger, postProcessor: PostProcessor) {
    this.vfxTrigger = vfxTrigger;
    this.postProcessor = postProcessor;
  }

  /** Evaluate transformation tại currentTime */
  public evaluate(
    config: TransformConfig,
    avatar: VRMAvatar,
    animator: ActorAnimator,
    currentTime: number
  ): void {
    const stateKey = `${config.actor_id}_${config.trigger_time}`;
    const triggerEnd = config.trigger_time + config.duration;

    // Chưa đến lúc
    if (currentTime < config.trigger_time) {
      this.resetState(stateKey);
      return;
    }

    // Đã qua
    if (currentTime > triggerEnd + 1.0) return;

    let state = this.states.get(stateKey);
    if (!state) {
      state = {
        configId: stateKey,
        phase: 'pre',
        startedVFX: false,
        completedSwap: false,
        completedFlash: false,
      };
      this.states.set(stateKey, state);
    }

    const elapsed = currentTime - config.trigger_time;
    const progress = Math.min(1, elapsed / config.duration);

    // Determine phase
    if (progress < 0.3) {
      state.phase = 'pre';
      this.executePrePhase(config, avatar, animator, state, progress, currentTime);
    } else if (progress < 0.7) {
      state.phase = 'during';
      this.executeDuringPhase(config, avatar, animator, state, progress, currentTime);
    } else if (progress < 1.0) {
      state.phase = 'post';
      this.executePostPhase(config, avatar, state, progress);
    } else {
      state.phase = 'complete';
    }
  }

  /**
   * Pre-transform phase (0% - 30%):
   * - Bắt đầu VFX aura charge
   * - Animation power up / concentration
   * - Camera focus vào nhân vật
   */
  private executePrePhase(
    config: TransformConfig,
    avatar: VRMAvatar,
    animator: ActorAnimator,
    state: TransformState,
    progress: number,
    time: number
  ): void {
    // Spawn charge VFX (once)
    if (!state.startedVFX) {
      state.startedVFX = true;
      this.vfxTrigger.spawnMagicShield(avatar.rootObject, config.duration);
    }

    // Power charge animation
    if (config.anim_during) {
      animator.setAction(config.anim_during as any);
    } else {
      // Default: tư thế vận công
      const preProgress = progress / 0.3;
      this.applyPowerChargeAnim(avatar, preProgress, time);
    }

    // Body rung nhẹ tăng dần
    const shake = progress * 0.01;
    avatar.rootObject.position.x += (Math.random() - 0.5) * shake;
    avatar.rootObject.position.z += (Math.random() - 0.5) * shake;
  }

  /**
   * During-transform phase (30% - 70%):
   * - Flash trắng toàn màn hình
   * - Swap costume meshes
   * - Scale body morph
   * - Light explosion VFX
   */
  private executeDuringPhase(
    config: TransformConfig,
    avatar: VRMAvatar,
    animator: ActorAnimator,
    state: TransformState,
    progress: number,
    time: number
  ): void {
    const duringProgress = (progress - 0.3) / 0.4;

    // Flash trắng (once)
    if (!state.completedFlash && config.screen_flash) {
      state.completedFlash = true;
      // PostProcessor flash effect
      this.postProcessor.triggerScreenShake(0.5, 0.5);
    }

    // Body morph: scale change
    if (config.body_morph) {
      const targetScale = config.body_morph.scale_multiplier;
      const currentScale = THREE.MathUtils.lerp(1.0, targetScale, duringProgress);
      avatar.rootObject.scale.setScalar(currentScale);
    }

    // Costume swap (at 50% of during phase)
    if (duringProgress > 0.5 && !state.completedSwap) {
      state.completedSwap = true;
      this.swapCostumeVisual(avatar, config);
    }

    // Glow effect on body
    if (config.body_morph?.aura_color) {
      this.applyBodyGlow(avatar, config.body_morph.aura_color, duringProgress);
    }
  }

  /**
   * Post-transform phase (70% - 100%):
   * - Aura loop VFX
   * - Settle into new costume
   * - Return to idle with new appearance
   */
  private executePostPhase(
    config: TransformConfig,
    avatar: VRMAvatar,
    state: TransformState,
    progress: number
  ): void {
    // Final scale settle
    if (config.body_morph) {
      avatar.rootObject.scale.setScalar(config.body_morph.scale_multiplier);
    }

    // Fade body glow
    const postProgress = (progress - 0.7) / 0.3;
    if (config.body_morph?.aura_color) {
      this.applyBodyGlow(avatar, config.body_morph.aura_color, 1 - postProgress);
    }
  }

  /** Default power charge animation (nếu không có anim_during) */
  private applyPowerChargeAnim(
    avatar: VRMAvatar, progress: number, time: number
  ): void {
    const shake = Math.sin(time * 25) * 0.005 * progress;

    avatar.spineBone.rotation.x = 0.1 * progress + shake;
    avatar.leftArm.rotation.set(-0.6 * progress + shake, 0, 0.35 * progress);
    avatar.leftElbow.rotation.set(-1.5 * progress, 0, 0);
    avatar.rightArm.rotation.set(-0.6 * progress + shake, 0, -0.35 * progress);
    avatar.rightElbow.rotation.set(-1.5 * progress, 0, 0);
  }

  /** Swap visual appearance (thay đổi màu sắc material) */
  private swapCostumeVisual(avatar: VRMAvatar, config: TransformConfig): void {
    // Traverse tất cả mesh và đổi emissive
    avatar.rootObject.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mesh = child as THREE.Mesh;
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        mats.forEach((mat) => {
          if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
            const stdMat = mat as THREE.MeshStandardMaterial;
            // Boost metalness cho "super" costume
            if (config.to_costume.includes('super')) {
              stdMat.metalness = Math.min(1, stdMat.metalness + 0.3);
              stdMat.roughness = Math.max(0, stdMat.roughness - 0.2);
            }
            stdMat.needsUpdate = true;
          }
        });
      }
    });
  }

  /** Apply glowing aura effect to body meshes */
  private applyBodyGlow(
    avatar: VRMAvatar, colorHex: string, intensity: number
  ): void {
    const color = new THREE.Color(colorHex);

    avatar.rootObject.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mesh = child as THREE.Mesh;
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        mats.forEach((mat) => {
          if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
            const stdMat = mat as THREE.MeshStandardMaterial;
            stdMat.emissive.copy(color);
            stdMat.emissiveIntensity = intensity * (avatar.config.power_level || 1) * 0.5;
            stdMat.needsUpdate = true;
          }
        });
      }
    });
  }

  /** Reset state */
  private resetState(stateKey: string): void {
    this.states.delete(stateKey);
  }

  /** Reset all */
  public reset(): void {
    this.states.clear();
  }
}
