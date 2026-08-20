import * as THREE from 'three';
import { PropLightConfig, PlacedProp } from '../../types/map_preset';

export interface ManagedLightItem {
  id: string;
  light: THREE.PointLight | THREE.SpotLight;
  targetObject?: THREE.Object3D;
  helperMesh?: THREE.Mesh;
  config: PropLightConfig;
  parentObject?: THREE.Object3D;
  baseIntensity: number;
  flickerSeed: number;
  isAutoDetected?: boolean;
}

/**
 * High-Performance 3D Local Light Source Manager (Unity/Unreal Style)
 * Supports PointLights, SpotLights (Flashlight / Street Lamp), Tree Auras,
 * Prop Light Sockets, Auto-detection in GLTF maps, and Flame Flickering.
 */
export class LightSourceManager {
  private scene: THREE.Scene;
  private lightsGroup: THREE.Group;
  private managedLights: Map<string, ManagedLightItem> = new Map();
  private animTimer: number = 0;

  constructor(scene: THREE.Scene) {
    this.scene = scene;
    this.lightsGroup = new THREE.Group();
    this.lightsGroup.name = 'managed_3d_local_lights_group';
    this.scene.add(this.lightsGroup);
  }

  /**
   * Scan a GLTF map or model hierarchy to automatically attach realistic PointLights to light fixtures
   */
  public scanAndAttachGLTFLights(root: THREE.Object3D): void {
    if (!root) return;

    const keywords = ['lamp', 'lantern', 'chandelier', 'torch', 'candle', 'fire', 'brazier', 'sconce', 'street_light'];

    root.traverse((node) => {
      const nodeName = (node.name || '').toLowerCase();
      const hasMatchingName = keywords.some((k) => nodeName.includes(k));

      let isEmissiveFixture = false;
      if ((node as THREE.Mesh).isMesh && (node as THREE.Mesh).material) {
        const mat = (node as THREE.Mesh).material;
        const mats = Array.isArray(mat) ? mat : [mat];
        isEmissiveFixture = mats.some((m: any) => {
          const matName = (m.name || '').toLowerCase();
          return keywords.some((k) => matName.includes(k)) || (m.emissive && m.emissive.getHex() > 0x222222);
        });
      }

      if (hasMatchingName || isEmissiveFixture) {
        const lightId = `autolight_${node.id}_${nodeName || 'node'}`;
        if (!this.managedLights.has(lightId)) {
          const isChandelier = nodeName.includes('chandelier');
          const isFire = nodeName.includes('fire') || nodeName.includes('torch') || nodeName.includes('brazier');
          const lightColor = isFire ? '#ff7722' : (isChandelier ? '#ffdd88' : '#ffaa44');
          const lightIntensity = isChandelier ? 3.5 : (isFire ? 2.5 : 2.0);
          const lightDistance = isChandelier ? 18.0 : (isFire ? 12.0 : 8.0);

          const config: PropLightConfig = {
            type: 'point',
            color: lightColor,
            intensity: lightIntensity,
            distance: lightDistance,
            decay: 2.0,
            flicker: isFire,
            offset: [0, 0, 0],
          };

          this.addOrUpdateLight(lightId, config, node);
        }
      }
    });
  }

  /**
   * Add or update a 3D PointLight / SpotLight
   */
  public addOrUpdateLight(id: string, config: PropLightConfig, parent?: THREE.Object3D): THREE.Light {
    const existing = this.managedLights.get(id);

    const color = new THREE.Color(config.color || '#ffaa33');
    const intensity = config.intensity !== undefined ? config.intensity : 2.5;
    const distance = config.distance !== undefined ? config.distance : 12.0;
    const decay = config.decay !== undefined ? config.decay : 2.0;
    const isSpot = config.type === 'spot';

    // If light already exists and is of the same type and parent, update properties directly
    if (existing && ((isSpot && (existing.light as THREE.SpotLight).isSpotLight) || (!isSpot && (existing.light as THREE.PointLight).isPointLight)) && existing.parentObject === parent) {
      existing.light.color.copy(color);
      existing.baseIntensity = intensity;
      existing.light.distance = distance;
      existing.light.decay = decay;
      existing.config = config;

      const offset = config.offset || [0, 0, 0];
      existing.light.position.set(offset[0], offset[1], offset[2]);

      if (isSpot && (existing.light as THREE.SpotLight).isSpotLight) {
        const spot = existing.light as THREE.SpotLight;
        spot.angle = config.spot_angle || (config.preset === 'flashlight' ? 0.35 : 0.75);
        spot.penumbra = config.spot_penumbra !== undefined ? config.spot_penumbra : 0.5;

        if (existing.targetObject) {
          const dir = config.target_direction || (config.preset === 'street_lamp' ? [0, -10, 0] : [0, -2, 10]);
          existing.targetObject.position.set(offset[0] + dir[0], offset[1] + dir[1], offset[2] + dir[2]);
        }
      }

      return existing.light;
    }

    // Otherwise clean up existing and recreate
    this.removeLight(id);

    let light: THREE.PointLight | THREE.SpotLight;
    let targetObject: THREE.Object3D | undefined;

    if (isSpot) {
      const angle = config.spot_angle || (config.preset === 'flashlight' ? 0.35 : 0.75);
      const penumbra = config.spot_penumbra !== undefined ? config.spot_penumbra : 0.5;
      const spot = new THREE.SpotLight(color, intensity, distance, angle, penumbra, decay);

      targetObject = new THREE.Object3D();
      targetObject.name = `spot_target_${id}`;
      const offset = config.offset || [0, 0, 0];
      const dir = config.target_direction || (config.preset === 'street_lamp' ? [0, -10, 0] : [0, -2, 10]);
      targetObject.position.set(offset[0] + dir[0], offset[1] + dir[1], offset[2] + dir[2]);

      spot.target = targetObject;
      light = spot;
    } else {
      light = new THREE.PointLight(color, intensity, distance, decay);
    }

    light.name = `light_source_${id}`;
    light.castShadow = Boolean(config.cast_shadow);
    if (light.castShadow) {
      light.shadow.bias = -0.0005;
      light.shadow.mapSize.width = 512;
      light.shadow.mapSize.height = 512;
    }

    const offset = config.offset || [0, 0, 0];
    light.position.set(offset[0], offset[1], offset[2]);

    if (parent) {
      parent.add(light);
      if (targetObject) parent.add(targetObject);
    } else {
      this.lightsGroup.add(light);
      if (targetObject) this.lightsGroup.add(targetObject);
    }

    const item: ManagedLightItem = {
      id,
      light,
      targetObject,
      config,
      parentObject: parent,
      baseIntensity: intensity,
      flickerSeed: Math.random() * 100,
    };

    this.managedLights.set(id, item);
    return light;
  }

  /**
   * Remove a managed light by ID
   */
  public removeLight(id: string): void {
    const item = this.managedLights.get(id);
    if (item) {
      if (item.targetObject && item.targetObject.parent) {
        item.targetObject.parent.remove(item.targetObject);
      }
      if (item.light.parent) {
        item.light.parent.remove(item.light);
      }
      item.light.dispose();
      this.managedLights.delete(id);
    }
  }

  /**
   * Synchronize lights for placed props in the scene
   */
  public syncPropLights(placedProps: PlacedProp[] = [], sceneObjects: Map<string, THREE.Object3D>): void {
    const activePropIds = new Set<string>();

    for (const prop of placedProps) {
      const propObj = sceneObjects.get(prop.id);
      const hasExplicitLight = Boolean(prop.light);
      const isKnownLightProp = prop.asset_path.includes('lantern') || 
                               prop.asset_path.includes('torch') || 
                               prop.asset_path.includes('fire') || 
                               prop.asset_path.includes('lamp');

      if (hasExplicitLight || isKnownLightProp) {
        const lightId = `prop_light_${prop.id}`;
        activePropIds.add(lightId);

        const isTree = prop.asset_path.includes('tree') || prop.id.includes('tree');

        const config: PropLightConfig = prop.light || (isTree ? {
          type: 'point',
          preset: 'tree_aura',
          color: '#34d399',
          intensity: 3.5,
          distance: 28.0,
          decay: 1.2,
          flicker: false,
          offset: [0, 3.5, 0],
        } : {
          type: 'point',
          preset: 'lantern',
          color: prop.asset_path.includes('fire') ? '#ff6611' : '#ffaa33',
          intensity: prop.asset_path.includes('fire') ? 3.0 : 2.2,
          distance: prop.asset_path.includes('fire') ? 14.0 : 9.0,
          decay: 2.0,
          flicker: true,
          offset: [0, 1.8, 0],
        });

        if (propObj) {
          this.addOrUpdateLight(lightId, config, propObj);
        }
      }
    }

    // Clean up removed prop lights
    for (const [id] of Array.from(this.managedLights.entries())) {
      if (id.startsWith('prop_light_') && !activePropIds.has(id)) {
        this.removeLight(id);
      }
    }
  }

  /**
   * Synchronize custom environment lights (e.g. cathedral altar/nave lights, street lamps, flashlights)
   */
  public syncEnvironmentLights(customLights: PropLightConfig[] = []): void {
    const activeCustomIds = new Set<string>();

    customLights.forEach((config, idx) => {
      const lightId = `custom_env_light_${idx}`;
      activeCustomIds.add(lightId);
      this.addOrUpdateLight(lightId, config);
    });

    for (const [id] of Array.from(this.managedLights.entries())) {
      if (id.startsWith('custom_env_light_') && !activeCustomIds.has(id)) {
        this.removeLight(id);
      }
    }
  }

  /**
   * Per-frame update: organic flame flickering, intensity multiplier, and master enable switch
   */
  public update(delta: number, masterMultiplier: number = 1.0, enabled: boolean = true): void {
    this.animTimer += delta;

    for (const item of this.managedLights.values()) {
      if (!enabled || masterMultiplier <= 0.001) {
        item.light.intensity = 0;
        item.light.visible = false;
        continue;
      }

      item.light.visible = true;
      let currentIntensity = item.baseIntensity * masterMultiplier;

      // Natural organic flame flicker for torches/lanterns
      if (item.config.flicker) {
        const seed = item.flickerSeed;
        const t = this.animTimer * 9.0 + seed;
        const f1 = Math.sin(t * 1.3) * 0.12;
        const f2 = Math.sin(t * 3.7 + 1.2) * 0.08;
        const f3 = Math.sin(t * 7.1 + 2.8) * 0.05;
        const flickerFactor = 1.0 + f1 + f2 + f3;
        currentIntensity *= Math.max(0.65, Math.min(1.4, flickerFactor));
      }

      item.light.intensity = currentIntensity;
    }
  }

  /**
   * Dispose all lights and remove group from scene
   */
  public dispose(): void {
    for (const id of Array.from(this.managedLights.keys())) {
      this.removeLight(id);
    }
    if (this.lightsGroup.parent) {
      this.lightsGroup.parent.remove(this.lightsGroup);
    }
  }
}
