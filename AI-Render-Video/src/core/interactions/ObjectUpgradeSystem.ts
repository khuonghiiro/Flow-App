import * as THREE from 'three';
import { UpgradeEvent, UpgradeVFXType } from '../../types/interactions';
import { CombatVFXTrigger } from '../combat/CombatVFXTrigger';
import { PostProcessor } from '../engine/PostProcessor';

// ============================================================
// ObjectUpgradeSystem - Nâng cấp nhà cửa, cây trồng, vật thể
// ============================================================

interface UpgradeState {
  eventKey: string;
  startedVFX: boolean;
  completedVisual: boolean;
  originalScale: THREE.Vector3;
  originalColor: THREE.Color | null;
}

export class ObjectUpgradeSystem {
  private vfxTrigger: CombatVFXTrigger;
  private postProcessor: PostProcessor;
  private scene: THREE.Scene;
  private states: Map<string, UpgradeState> = new Map();

  constructor(
    scene: THREE.Scene,
    vfxTrigger: CombatVFXTrigger,
    postProcessor: PostProcessor
  ) {
    this.scene = scene;
    this.vfxTrigger = vfxTrigger;
    this.postProcessor = postProcessor;
  }

  /** Evaluate upgrade event */
  public evaluate(
    event: UpgradeEvent,
    targetObject: THREE.Object3D,
    currentTime: number
  ): void {
    const key = `${event.target}_${event.trigger_time}`;
    const endTime = event.trigger_time + event.upgrade_duration;

    // Chưa đến
    if (currentTime < event.trigger_time) {
      this.resetState(key, targetObject);
      return;
    }

    // Đã hoàn tất
    if (currentTime > endTime + 0.5) {
      this.ensureComplete(key, event, targetObject);
      return;
    }

    let state = this.states.get(key);
    if (!state) {
      state = {
        eventKey: key,
        startedVFX: false,
        completedVisual: false,
        originalScale: targetObject.scale.clone(),
        originalColor: this.getFirstColor(targetObject),
      };
      this.states.set(key, state);
    }

    const progress = (currentTime - event.trigger_time) / event.upgrade_duration;

    // Phase 1 (0-30%): Start VFX
    if (!state.startedVFX) {
      state.startedVFX = true;
      this.spawnUpgradeVFX(event, targetObject);
      if (event.camera_focus) {
        this.postProcessor.triggerScreenShake(0.1, 0.5);
      }
    }

    // Phase 2 (0-100%): Animate visual changes
    this.animateVisualChanges(event, targetObject, state, progress);
  }

  /** Spawn VFX cho upgrade event */
  private spawnUpgradeVFX(event: UpgradeEvent, target: THREE.Object3D): void {
    const pos = new THREE.Vector3();
    target.getWorldPosition(pos);

    switch (event.vfx.type) {
      case 'construction_dust':
        this.spawnConstructionDust(pos, event.vfx.color, event.vfx.intensity);
        break;
      case 'magic_sparkle':
        this.spawnMagicSparkle(pos, event.vfx.color, event.vfx.intensity);
        break;
      case 'growth_burst':
        this.spawnGrowthBurst(pos, event.vfx.color);
        break;
      case 'light_pillar':
        this.spawnLightPillar(pos, event.vfx.color, event.vfx.duration || 2);
        break;
      default:
        this.vfxTrigger.spawnHitSparks(pos);
        break;
    }
  }

  /** Animate visual changes theo progress */
  private animateVisualChanges(
    event: UpgradeEvent,
    target: THREE.Object3D,
    state: UpgradeState,
    progress: number
  ): void {
    const changes = event.visual_changes;
    if (!changes) return;

    // Scale change
    if (changes.scale_change) {
      const targetScale = new THREE.Vector3(...changes.scale_change);
      target.scale.lerpVectors(state.originalScale, targetScale, progress);
    }

    // Color change
    if (changes.color_change) {
      const targetColor = new THREE.Color(changes.color_change);
      this.applyColorLerp(target, state.originalColor, targetColor, progress);
    }

    // Emissive glow during transformation
    if (changes.emissive_color) {
      const emColor = new THREE.Color(changes.emissive_color);
      const intensity = (changes.emissive_intensity || 0.5) * Math.sin(progress * Math.PI);
      this.applyEmissive(target, emColor, intensity);
    }

    // Material change at 50%
    if (changes.material_change && progress > 0.5 && !state.completedVisual) {
      state.completedVisual = true;
      this.applyMaterialPreset(target, changes.material_change);
    }
  }

  /** Ensure final state is correct */
  private ensureComplete(
    key: string, event: UpgradeEvent, target: THREE.Object3D
  ): void {
    const changes = event.visual_changes;
    if (changes?.scale_change) {
      target.scale.set(...changes.scale_change);
    }
    if (changes?.color_change) {
      const color = new THREE.Color(changes.color_change);
      this.applyColorLerp(target, null, color, 1);
    }
    // Clear emissive
    this.applyEmissive(target, new THREE.Color(0), 0);
  }

  // ============================================================
  // VFX Spawners
  // ============================================================

  private spawnConstructionDust(
    pos: THREE.Vector3, colorHex: string, intensity: number
  ): void {
    const count = Math.round(20 * intensity);
    const geo = new THREE.BufferGeometry();
    const positions = new Float32Array(count * 3);
    const velocities: THREE.Vector3[] = [];

    for (let i = 0; i < count; i++) {
      positions[i * 3] = pos.x + (Math.random() - 0.5) * 2;
      positions[i * 3 + 1] = pos.y + Math.random() * 0.5;
      positions[i * 3 + 2] = pos.z + (Math.random() - 0.5) * 2;
      velocities.push(new THREE.Vector3(
        (Math.random() - 0.5) * 2, Math.random() * 2 + 0.5, (Math.random() - 0.5) * 2
      ));
    }

    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const mat = new THREE.PointsMaterial({
      color: new THREE.Color(colorHex),
      size: 0.12,
      transparent: true,
      opacity: 0.7,
    });

    const particles = new THREE.Points(geo, mat);
    this.scene.add(particles);

    // Auto-remove after 2 seconds
    setTimeout(() => this.scene.remove(particles), 2000);
  }

  private spawnMagicSparkle(
    pos: THREE.Vector3, colorHex: string, intensity: number
  ): void {
    const count = Math.round(15 * intensity);
    const geo = new THREE.BufferGeometry();
    const positions = new Float32Array(count * 3);

    for (let i = 0; i < count; i++) {
      const angle = (i / count) * Math.PI * 2;
      const radius = 0.5 + Math.random() * 1.5;
      positions[i * 3] = pos.x + Math.cos(angle) * radius;
      positions[i * 3 + 1] = pos.y + Math.random() * 2;
      positions[i * 3 + 2] = pos.z + Math.sin(angle) * radius;
    }

    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const mat = new THREE.PointsMaterial({
      color: new THREE.Color(colorHex),
      size: 0.08,
      transparent: true,
      opacity: 0.9,
      blending: THREE.AdditiveBlending,
    });

    const particles = new THREE.Points(geo, mat);
    this.scene.add(particles);
    setTimeout(() => this.scene.remove(particles), 3000);
  }

  private spawnGrowthBurst(pos: THREE.Vector3, colorHex: string): void {
    // Green expanding ring
    const ringGeo = new THREE.RingGeometry(0.1, 0.3, 16);
    const ringMat = new THREE.MeshBasicMaterial({
      color: new THREE.Color(colorHex),
      transparent: true,
      opacity: 0.8,
      side: THREE.DoubleSide,
    });
    const ring = new THREE.Mesh(ringGeo, ringMat);
    ring.position.copy(pos);
    ring.position.y += 0.1;
    ring.rotation.x = -Math.PI / 2;
    this.scene.add(ring);

    // Animate expansion
    let life = 1.5;
    const animate = () => {
      life -= 0.016;
      if (life <= 0) {
        this.scene.remove(ring);
        return;
      }
      ring.scale.multiplyScalar(1.03);
      ringMat.opacity = Math.max(0, life / 1.5);
      requestAnimationFrame(animate);
    };
    animate();
  }

  private spawnLightPillar(
    pos: THREE.Vector3, colorHex: string, duration: number
  ): void {
    const pillarGeo = new THREE.CylinderGeometry(0.3, 0.3, 15, 8, 1, true);
    const pillarMat = new THREE.MeshBasicMaterial({
      color: new THREE.Color(colorHex),
      transparent: true,
      opacity: 0.4,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const pillar = new THREE.Mesh(pillarGeo, pillarMat);
    pillar.position.set(pos.x, pos.y + 7, pos.z);
    this.scene.add(pillar);

    setTimeout(() => this.scene.remove(pillar), duration * 1000);
  }

  // ============================================================
  // Material helpers
  // ============================================================

  private getFirstColor(obj: THREE.Object3D): THREE.Color | null {
    let color: THREE.Color | null = null;
    obj.traverse((child) => {
      if (!color && (child as THREE.Mesh).isMesh) {
        const mat = (child as THREE.Mesh).material as THREE.MeshStandardMaterial;
        if (mat?.color) color = mat.color.clone();
      }
    });
    return color;
  }

  private applyColorLerp(
    obj: THREE.Object3D,
    from: THREE.Color | null,
    to: THREE.Color,
    t: number
  ): void {
    obj.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mat = (child as THREE.Mesh).material as THREE.MeshStandardMaterial;
        if (mat?.color) {
          if (from) {
            mat.color.lerpColors(from, to, t);
          } else {
            mat.color.copy(to);
          }
          mat.needsUpdate = true;
        }
      }
    });
  }

  private applyEmissive(
    obj: THREE.Object3D, color: THREE.Color, intensity: number
  ): void {
    obj.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mat = (child as THREE.Mesh).material as THREE.MeshStandardMaterial;
        if (mat && 'emissive' in mat) {
          mat.emissive.copy(color);
          mat.emissiveIntensity = intensity;
          mat.needsUpdate = true;
        }
      }
    });
  }

  private applyMaterialPreset(
    obj: THREE.Object3D, preset: string
  ): void {
    const presets: Record<string, { metalness: number; roughness: number }> = {
      wood: { metalness: 0.1, roughness: 0.8 },
      stone: { metalness: 0.2, roughness: 0.7 },
      metal: { metalness: 0.7, roughness: 0.3 },
      crystal: { metalness: 0.5, roughness: 0.1 },
      gold: { metalness: 0.9, roughness: 0.15 },
    };

    const p = presets[preset];
    if (!p) return;

    obj.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mat = (child as THREE.Mesh).material as THREE.MeshStandardMaterial;
        if (mat && 'metalness' in mat) {
          mat.metalness = p.metalness;
          mat.roughness = p.roughness;
          mat.needsUpdate = true;
        }
      }
    });
  }

  private resetState(key: string, target: THREE.Object3D): void {
    const state = this.states.get(key);
    if (state) {
      target.scale.copy(state.originalScale);
      this.applyEmissive(target, new THREE.Color(0), 0);
      this.states.delete(key);
    }
  }

  public reset(): void {
    this.states.clear();
  }
}
