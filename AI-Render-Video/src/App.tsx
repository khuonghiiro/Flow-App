import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { MasterSceneConfig, DialogueManifestItem, EnvironmentOverride } from './types/scene';
import { defaultScene, sampleScenes } from './core/scenes/SceneRegistry';
import { ThreeRenderer } from './core/engine/ThreeRenderer';
import { SceneLighting } from './core/engine/SceneLighting';
import { PostProcessor } from './core/engine/PostProcessor';
import { CombatVFXTrigger } from './core/combat/CombatVFXTrigger';
import { CombatSyncEngine } from './core/combat/CombatSyncEngine';
import { NavMeshManager } from './core/navigation/NavMeshManager';
import { PathNavigator } from './core/navigation/PathNavigator';
import { CameraDirector, InspectCameraAngle } from './core/camera/CameraDirector';
import { OcclusionFoliageManager, FoliageFocusActor } from './core/camera/OcclusionFoliageManager';
import { TrackEvaluator, ActorRuntime } from './core/timeline/TrackEvaluator';
import { MasterClock } from './core/timeline/MasterClock';
import { PlayerController } from './core/navigation/PlayerController';
import { VRMAvatar } from './core/actors/VRMAvatar';
import { ActorAnimator } from './core/actors/ActorAnimator';
import { ActorMorphController } from './core/actors/ActorMorphController';
import { ActorLipSync } from './core/actors/ActorLipSync';
import { ActorLookAt } from './core/actors/ActorLookAt';
import { SubtitleSynchronizer, ActiveSubtitle } from './core/subtitles/SubtitleSynchronizer';
import { WebCodecsRecorder } from './core/export/WebCodecsRecorder';
import { VideoMuxer } from './core/export/VideoMuxer';
import { StudioLayout } from './ui/StudioLayout';
import { ActorVisualState } from './core/camera/CameraFraming';
import { AssetLoaderRegistry } from './core/assets/AssetLoaderRegistry';
import { MapPresetManager } from './core/maps/MapPresetManager';
import { GifOverlayManager } from './core/vfx/GifOverlayManager';
import { PlacedProp } from './types/map_preset';
import { SelectedSceneObject } from './ui/TransformInspector';
import { WeatherParticleSystem } from './core/weather/WeatherParticleSystem';
import { CloudSystem } from './core/weather/CloudSystem';
import { LightningSystem } from './core/weather/LightningSystem';
import { VolumetricCloudLighting } from './core/weather/VolumetricCloudLighting';
import { getSavedViewportSettings, saveViewportSetting } from './core/storage/ViewportSettingsStorage';

// Early initialization of volumetric cloud shader chunks
VolumetricCloudLighting.init();

export const App: React.FC = () => {

  const [scene, setScene] = useState<MasterSceneConfig>(defaultScene);
  const sceneRef = useRef<MasterSceneConfig>(defaultScene);
  const [selectedObject, setSelectedObject] = useState<SelectedSceneObject | null>(null);
  const selectedObjectRef = useRef<SelectedSceneObject | null>(null);
  const [currentTime, setCurrentTime] = useState(0);

  useEffect(() => {
    selectedObjectRef.current = selectedObject;
  }, [selectedObject]);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackRate, setPlaybackRate] = useState(1.0);
  const [isLooping, setIsLooping] = useState(true);
  const [fps, setFps] = useState(60);
  const [activeSubtitle, setActiveSubtitle] = useState<ActiveSubtitle | null>(null);
  const [inspectAngle, setInspectAngle] = useState<InspectCameraAngle>('front');
  const [inspectingActorId, setInspectingActorId] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [exportProgressMsg, setExportProgressMsg] = useState('');
  const [isFreeCam, setIsFreeCam] = useState(() => getSavedViewportSettings().isFreeCam);
  const isFreeCamRef = useRef(getSavedViewportSettings().isFreeCam);
  const [isLoadingMap, setIsLoadingMap] = useState(false);
  const [envOverride, setEnvOverride] = useState<EnvironmentOverride>({
    enabled: false,
    sky_time: 'sunset',
    sun_position: 0.5,
    fog_density: 0.012,
    wind_intensity: 0.3,
  });
  const envOverrideRef = useRef(envOverride);

  // Sync ref
  useEffect(() => {
    envOverrideRef.current = envOverride;
  }, [envOverride]);

  // Engine references
  const rendererRef = useRef<ThreeRenderer | null>(null);
  const lightingRef = useRef<SceneLighting | null>(null);
  const postProcessorRef = useRef<PostProcessor | null>(null);
  const vfxTriggerRef = useRef<CombatVFXTrigger | null>(null);
  const combatSyncRef = useRef<CombatSyncEngine | null>(null);
  const navMeshRef = useRef<NavMeshManager>(new NavMeshManager());
  const cameraDirectorRef = useRef<CameraDirector | null>(null);
  const occlusionFoliageRef = useRef<OcclusionFoliageManager | null>(null);
  const trackEvaluatorRef = useRef<TrackEvaluator | null>(null);
  const clockRef = useRef<MasterClock>(new MasterClock(defaultScene.duration));
  const actorsMapRef = useRef<Map<string, ActorRuntime>>(new Map());
  const sceneObjectsRef = useRef<Map<string, THREE.Object3D>>(new Map());
  const mapGroupRef = useRef<THREE.Group>(new THREE.Group());
  const mapMixerRef = useRef<THREE.AnimationMixer | null>(null);
  const mapCollidersRef = useRef<THREE.Object3D[]>([]);
  const playerControllerRef = useRef<PlayerController | null>(null);
  const gifOverlayRef = useRef<GifOverlayManager | null>(null);
  const weatherParticlesRef = useRef<WeatherParticleSystem | null>(null);
  const cloudSystemRef = useRef<CloudSystem | null>(null);
  const lightningSystemRef = useRef<LightningSystem | null>(null);
  const lastStateSyncRef = useRef({ time: 0, fps: 0, subLineId: '' });
  const lastMapLightingModeRef = useRef<boolean | undefined>(undefined);

  // Default Initial Village Props with unique IDs and initial positions
  const defaultVillageProps: PlacedProp[] = [
    { id: 'placed_tree_oak_01', asset_path: 'props/nature/tree_sakura.glb', position: [4, 0, -3], type: 'nature', is_obstacle: true },
    { id: 'placed_chair_01', asset_path: 'props/furniture/chair_wooden.glb', position: [-4, 0, -2], type: 'furniture', is_obstacle: true, smart_socket: { socket_type: 'sit', entry_offset: [0, 0, 0.8], target_offset: [0, 0.5, 0], target_rotation_y: 0 } },
    { id: 'placed_farm_plot_01', asset_path: 'props/nature/farm_plot.glb', position: [0, 0, -5], type: 'nature', is_obstacle: true },
    { id: 'placed_duck_01', asset_path: 'props/nature/duck.glb', position: [0.8, 0, -2.5], type: 'animal', is_obstacle: false },
    { id: 'placed_lantern_stand_01', asset_path: 'props/furniture/lantern_stand.glb', position: [-3.2, 0, -2.0], type: 'furniture', is_obstacle: true },
  ];

  // Populate complete village props (ground, tree, chair, farm, duck, lantern)
  const populateVillageProps = (group: THREE.Group, propsList?: PlacedProp[]) => {
    const ground = AssetLoaderRegistry.createGround();
    group.add(ground);

    const getPos = (id: string, def: [number, number, number]): [number, number, number] => {
      const found = (propsList || []).find((p) => p.id === id);
      return found ? found.position : def;
    };

    const tree = AssetLoaderRegistry.createTree(getPos('placed_tree_oak_01', [4, 0, -3]));
    tree.name = 'placed_tree_oak_01';
    group.add(tree);
    sceneObjectsRef.current.set('placed_tree_oak_01', tree);

    const chair = AssetLoaderRegistry.createChair(getPos('placed_chair_01', [-4, 0, -2]));
    chair.name = 'placed_chair_01';
    group.add(chair);
    sceneObjectsRef.current.set('placed_chair_01', chair);

    const farm = AssetLoaderRegistry.createFarmPlot(getPos('placed_farm_plot_01', [0, 0, -5]));
    farm.name = 'placed_farm_plot_01';
    group.add(farm);
    sceneObjectsRef.current.set('placed_farm_plot_01', farm);
    const crop = farm.getObjectByName('crop');
    if (crop) {
      sceneObjectsRef.current.set('props.farm_plot_01.crop', crop);
    }

    const duck = AssetLoaderRegistry.createDuckProp(getPos('placed_duck_01', [0.8, 0, -2.5]));
    duck.name = 'placed_duck_01';
    group.add(duck);
    sceneObjectsRef.current.set('placed_duck_01', duck);

    const lanternStand = AssetLoaderRegistry.createLanternStand(getPos('placed_lantern_stand_01', [-3.2, 0, -2.0]));
    lanternStand.name = 'placed_lantern_stand_01';
    group.add(lanternStand);
    sceneObjectsRef.current.set('placed_lantern_stand_01', lanternStand);
  };

  // Dynamic Environment Loader (supports .glb maps and procedural village)
  const initEnvironment = async (newScene: MasterSceneConfig, scene3D: THREE.Scene) => {
    if (mapGroupRef.current.parent !== scene3D) {
      scene3D.add(mapGroupRef.current);
    }
    mapGroupRef.current.position.set(0, 0, 0);
    mapGroupRef.current.rotation.set(0, 0, 0);
    mapGroupRef.current.scale.set(1, 1, 1);

    while (mapGroupRef.current.children.length > 0) {
      const child = mapGroupRef.current.children[0];
      mapGroupRef.current.remove(child);
    }
    sceneObjectsRef.current.clear();

    const presetId = newScene.environment.map_preset || newScene.environment.map;
    const preset = MapPresetManager.getPreset(presetId);

    const baseMapName = (preset ? (preset.base_map || '') : (newScene.environment.map || '')).toLowerCase();
    const isCustomMap =
      (baseMapName.endsWith('.glb') || baseMapName.endsWith('.gltf')) &&
      !baseMapName.includes('village');

    if (isCustomMap) {
      const glbSource = preset ? (preset.base_map || '') : newScene.environment.map;
      const glbUrl = glbSource.startsWith('assets/') || glbSource.startsWith('/assets/')
        ? (glbSource.startsWith('/') ? glbSource : `/${glbSource}`)
        : `/assets/maps/${glbSource}`;

      try {
        setIsLoadingMap(true);
        const mapModel = await AssetLoaderRegistry.loadGLTF(glbUrl);
        mapModel.name = 'custom_glb_map_mesh';

        mapModel.position.set(0, 0, 0);
        mapGroupRef.current.add(mapModel);

        if (mapModel.animations && mapModel.animations.length > 0) {
          mapMixerRef.current = new THREE.AnimationMixer(mapModel);
          mapModel.animations.forEach((clip: any) => {
            mapMixerRef.current?.clipAction(clip).play();
          });
        } else {
          mapMixerRef.current = null;
        }
      } catch (err) {
        console.warn('Không tải được model map tùy chỉnh, chuyển về map làng quê mẫu:', err);
        if (!preset) {
          populateVillageProps(mapGroupRef.current);
        } else {
          const ground = AssetLoaderRegistry.createGround();
          mapGroupRef.current.add(ground);
        }
      } finally {
        setIsLoadingMap(false);
      }
    } else {
      if (!preset) {
        populateVillageProps(mapGroupRef.current);
      } else {
        const ground = AssetLoaderRegistry.createGround();
        mapGroupRef.current.add(ground);
      }
    }

    // Build placed props from preset if available
    if (preset) {
      MapPresetManager.buildPlacedProps(preset, mapGroupRef.current, sceneObjectsRef.current);
    }

    // Build additional scene-specific placed props if available
    if (newScene.environment.placed_props && newScene.environment.placed_props.length > 0) {
      MapPresetManager.buildPlacedProps(
        {
          map_id: 'scene_props',
          name: 'Scene Props',
          placed_props: newScene.environment.placed_props,
        },
        mapGroupRef.current,
        sceneObjectsRef.current
      );
    }

    // For procedural village without custom GLB, provide the default physics ground plane
    let groundPlane: THREE.Mesh | null = null;
    if (!isCustomMap) {
      groundPlane = new THREE.Mesh(
        new THREE.PlaneGeometry(80, 80),
        new THREE.MeshBasicMaterial({ visible: false })
      );
      groundPlane.rotation.x = -Math.PI / 2;
      groundPlane.position.y = 0;
      groundPlane.name = 'fast_physics_ground_plane';
      mapGroupRef.current.add(groundPlane);
    }

    // Collect Map Colliders for Ground Snapping (custom GLB terrain/stairs/buildings or default village ground)
    mapCollidersRef.current = groundPlane ? [groundPlane] : [];
    mapGroupRef.current.traverse((child) => {
      if ((child as THREE.Mesh).isMesh && child !== groundPlane && child.name !== 'dynamic_cloud_ground_shadow' && child.name !== 'cloud_shadow_caster_3d') {
        const m = child as THREE.Mesh;
        if (m.geometry && (!m.geometry.attributes.position || m.geometry.attributes.position.count < 45000)) {
          mapCollidersRef.current.push(m);
        }
      }
    });

    // Update 3D rain collision obstacles directly from true map geometry
    if (weatherParticlesRef.current) {
      weatherParticlesRef.current.updateColliders(mapGroupRef.current);
    }

    // Immediately snap all existing actors to the newly loaded terrain surface
    const snapRay = new THREE.Raycaster();
    snapRay.far = 60.0;
    for (const [, runtime] of actorsMapRef.current.entries()) {
      const pos = runtime.avatar.rootObject.position;
      snapRay.set(new THREE.Vector3(pos.x, 35.0, pos.z), new THREE.Vector3(0, -1, 0));
      const hits = snapRay.intersectObjects(mapCollidersRef.current, false);
      for (const hit of hits) {
        if (hit.face && hit.point.y > -5.0 && hit.point.y < 30.0) {
          pos.y = hit.point.y;
          break;
        }
      }
    }

    if (occlusionFoliageRef.current) {
      occlusionFoliageRef.current.registerSceneFoliage(scene3D);
    }

    // Ensure all environment meshes and props receive cloud shadow lighting hooks
    VolumetricCloudLighting.applyToScene(mapGroupRef.current);

    const mapDynamicLighting = envOverrideRef.current.enabled
      ? (envOverrideRef.current.map_dynamic_lighting ?? true)
      : (newScene.environment.weather?.map_dynamic_lighting ?? true);
    AssetLoaderRegistry.applyMapLightingMode(mapGroupRef.current, mapDynamicLighting);
  };

  // Setup 3D Scene once
  useEffect(() => {
    const renderer = new ThreeRenderer();
    rendererRef.current = renderer;

    const lighting = new SceneLighting(renderer.scene);
    lightingRef.current = lighting;

    const postProcessor = new PostProcessor();
    postProcessorRef.current = postProcessor;

    const vfxTrigger = new CombatVFXTrigger(renderer.scene);
    vfxTriggerRef.current = vfxTrigger;

    const combatSync = new CombatSyncEngine(vfxTrigger, postProcessor);
    combatSyncRef.current = combatSync;

    const pathNav = new PathNavigator(navMeshRef.current);
    cameraDirectorRef.current = new CameraDirector(renderer.camera);
    occlusionFoliageRef.current = new OcclusionFoliageManager();
    gifOverlayRef.current = new GifOverlayManager(renderer.scene, renderer.camera);
    trackEvaluatorRef.current = new TrackEvaluator(combatSync, pathNav);
    playerControllerRef.current = new PlayerController();
    weatherParticlesRef.current = new WeatherParticleSystem(renderer.scene);
    cloudSystemRef.current = new CloudSystem(renderer.scene);
    lightningSystemRef.current = new LightningSystem(renderer.scene);

    if (isFreeCamRef.current) {
      renderer.setFreeCam(true);
    }

    // Build Environment & Actors
    initEnvironment(sceneRef.current, renderer.scene);
    initActors(sceneRef.current, renderer.scene);

    // Apply scene lighting from the loaded scene's environment config (sky_time, fog, etc.)
    lighting.applyEnvironment(sceneRef.current.environment);
    lighting.update(0, sceneRef.current.duration);

    // Connect Unity-Style 3D Transform Gizmo changes to state
    if (renderer.gizmo) {
      renderer.gizmo.onTransformChange((data) => {
        const current = selectedObjectRef.current;
        if (!current || current.id !== data.id) return;
        const updated: SelectedSceneObject = {
          ...current,
          position: data.position,
          rotation: data.rotation,
          scale: data.scale,
        };
        handleUpdateTransform(updated);
      });
    }


    // Main Render & Evaluation Loop (60-120fps GPU)
    renderer.onRender((delta) => {
      clockRef.current.update(delta);
      const t = clockRef.current.currentTime;

      // Throttle React UI timeline scrubber update (~30 FPS) to prevent UI thread thrashing
      const now = performance.now();
      if (now - lastStateSyncRef.current.time >= 33) {
        lastStateSyncRef.current.time = now;
        setCurrentTime(t);
      }

      if (renderer.fps !== lastStateSyncRef.current.fps) {
        lastStateSyncRef.current.fps = renderer.fps;
        setFps(renderer.fps);
      }

      const activeScene = sceneRef.current;

      // Evaluate Tracks
      if (trackEvaluatorRef.current) {
        trackEvaluatorRef.current.evaluate(
          activeScene,
          t,
          delta,
          actorsMapRef.current,
          sceneObjectsRef.current,
          playerControllerRef.current?.controlledActorId || null
        );
      }

      // Auto-assign player controller to the first actor if none selected
      if (playerControllerRef.current && activeScene.actors.length > 0 && !playerControllerRef.current.controlledActorId) {
        playerControllerRef.current.controlledActorId = activeScene.actors[0].id;
      }

      // Player Controller Update
      if (playerControllerRef.current && playerControllerRef.current.controlledActorId) {
        const controlledRuntime = actorsMapRef.current.get(playerControllerRef.current.controlledActorId);
        if (controlledRuntime) {
          playerControllerRef.current.update(delta, controlledRuntime.avatar, controlledRuntime.animator, renderer.camera, mapCollidersRef.current);
        }
      }

      // Ground Snapping (Physics)
      if (mapCollidersRef.current.length > 0) {
        const raycaster = new THREE.Raycaster();
        raycaster.far = 30.0;

        for (const [id, runtime] of actorsMapRef.current.entries()) {
          const isClimbing = (runtime.avatar.config.tracks.movement || []).some(
            (m) => m.action === 'climb' && t >= m.start && t <= m.end
          );
          if (isClimbing) continue;

          const isPlayer = id === playerControllerRef.current?.controlledActorId;
          const pos = runtime.avatar.rootObject.position;

          raycaster.set(new THREE.Vector3(pos.x, Math.max(pos.y + 12.0, 16.0), pos.z), new THREE.Vector3(0, -1, 0));
          const hits = raycaster.intersectObjects(mapCollidersRef.current, false);

          let validGroundY: number | null = null;
          for (const hit of hits) {
            if (hit.face) {
              const normalMatrix = new THREE.Matrix3().getNormalMatrix(hit.object.matrixWorld);
              const worldNormal = hit.face.normal.clone().applyMatrix3(normalMatrix).normalize();
              if (worldNormal.y > 0.25 && hit.point.y > -10.0 && hit.point.y < 35.0) {
                validGroundY = hit.point.y;
                break;
              }
            }
          }

          if (validGroundY !== null) {
            const groundY = validGroundY;

            if (isPlayer && playerControllerRef.current) {
              if (Math.abs(pos.y - groundY) <= 0.08) {
                pos.y = groundY;
                playerControllerRef.current.isGrounded = true;
                playerControllerRef.current.velocityY = 0;
              } else if (pos.y > groundY + 0.08) {
                playerControllerRef.current.isGrounded = false;
                // Realistic gravity falling when in mid-air
                playerControllerRef.current.velocityY -= 20.0 * delta;
                pos.y += playerControllerRef.current.velocityY * delta;
                if (pos.y <= groundY) {
                  pos.y = groundY;
                  playerControllerRef.current.isGrounded = true;
                  playerControllerRef.current.velocityY = 0;
                }
              } else {
                // Stepping up to elevated terrain/stairs
                pos.y = THREE.MathUtils.lerp(pos.y, groundY, 1 - Math.exp(-22 * delta));
                playerControllerRef.current.isGrounded = true;
                playerControllerRef.current.velocityY = 0;
              }
            } else {
              // Natural gravity drop & snap to ground surface for all actors
              if (Math.abs(pos.y - groundY) > 2.0) {
                pos.y = groundY;
              } else {
                pos.y = THREE.MathUtils.lerp(pos.y, groundY, 1 - Math.exp(-22 * delta));
              }
            }
          } else {
            // Out of map bounds: fallback to y = 0
            if (pos.y > 0.05) {
              pos.y = Math.max(0, THREE.MathUtils.lerp(pos.y, 0, 1 - Math.exp(-22 * delta)));
            } else if (pos.y < 0) {
              pos.y = 0;
            }
            if (isPlayer && playerControllerRef.current) {
              playerControllerRef.current.isGrounded = false;
            }
          }
        }
      }

      // Collect actor visual states & foliage focus states
      const actorStates = new Map<string, ActorVisualState>();
      const foliageActors: FoliageFocusActor[] = [];

      const activeDialogues = activeScene.dialogues_manifest || [];
      const currentSpeakingSpeakerId = activeDialogues.find(
        (d) =>
          t >= d.start_time &&
          t <= d.start_time + (d.actual_duration || d.estimated_duration || 3.0)
      )?.speaker_id;

      const currentInspectId = cameraDirectorRef.current?.getInspectTargetId();

      const currentCameraTrack =
        (activeScene.camera_tracks || []).find((c) => t >= c.start && t <= c.end) ||
        (activeScene.camera_tracks || [])[0];

      const currentCameraTargetId =
        currentCameraTrack?.follow_target ||
        (typeof currentCameraTrack?.look_at === 'string'
          ? currentCameraTrack.look_at.replace('.head', '')
          : null);

      for (const [id, runtime] of actorsMapRef.current.entries()) {
        const p = new THREE.Vector3();
        runtime.avatar.rootObject.getWorldPosition(p);
        const head = runtime.avatar.getHeadPosition();
        const rotY = runtime.avatar.rootObject.rotation.y;

        actorStates.set(id, { position: p, headPosition: head, rotationY: rotY });

        // Evaluate Animated GIF Emotes / Stickers
        if (gifOverlayRef.current) {
          gifOverlayRef.current.evaluateActorOverlays(
            id,
            runtime.avatar.config.tracks.gif_overlays,
            runtime.avatar.rootObject,
            t
          );
        }

        // Actor is on tree if climbing track is active or altitude is up in the canopy

        const isClimbingOrOnTree =
          p.y > 1.2 ||
          (runtime.avatar.config.tracks.movement || []).some(
            (m) => m.action === 'climb' && t >= m.start && t <= m.end
          );

        // Foliage only turns transparent if this actor is actively focused / speaking / inspected
        const isFocused =
          currentInspectId === id ||
          currentSpeakingSpeakerId === id ||
          currentCameraTargetId === id;

        foliageActors.push({
          id,
          headPosition: head,
          isClimbingOrOnTree,
          isFocused,
        });
      }

      // Camera Director Update (only active when Free Cam is NOT enabled)
      if (!isFreeCamRef.current && cameraDirectorRef.current) {
        cameraDirectorRef.current.update(activeScene, t, actorStates, delta);
      }

      // Occlusion Transparency
      if (occlusionFoliageRef.current) {
        occlusionFoliageRef.current.update(renderer.camera, foliageActors, delta);
      }

      // Screen Shake Post-processing (only in Director Cam and ONLY when playing)
      if (!isFreeCamRef.current && postProcessorRef.current) {
        postProcessorRef.current.applyToCamera(renderer.camera, t, clockRef.current.isPlaying);
      }

      // Combat VFX particles update
      if (vfxTriggerRef.current) {
        vfxTriggerRef.current.update(delta);
      }

      // Map animations
      if (mapMixerRef.current) {
        mapMixerRef.current.update(delta);
      }

      // Active Subtitle Update (only trigger React state update when dialogue line actually changes)
      const sub = SubtitleSynchronizer.getActiveSubtitle(activeScene, t);
      const subKey = sub ? `${sub.line_id}_${sub.speaker_id}` : '';
      if (subKey !== lastStateSyncRef.current.subLineId) {
        lastStateSyncRef.current.subLineId = subKey;
        setActiveSubtitle(sub);
      }

      // Dynamic Sun & Lighting update across timeline
      if (lightingRef.current) {
        if (envOverrideRef.current.enabled) {
          lightingRef.current.updateManual(envOverrideRef.current);
        } else {
          lightingRef.current.update(t, activeScene.duration);
        }
        lightingRef.current.updateShadowTarget(renderer.camera.position);
      }

      // Sync map real-time dynamic lighting mode
      const mapDynamicLighting = envOverrideRef.current.enabled
        ? (envOverrideRef.current.map_dynamic_lighting ?? true)
        : (activeScene.environment.weather?.map_dynamic_lighting ?? true);

      if (lastMapLightingModeRef.current !== mapDynamicLighting && mapGroupRef.current) {
        lastMapLightingModeRef.current = mapDynamicLighting;
        AssetLoaderRegistry.applyMapLightingMode(mapGroupRef.current, mapDynamicLighting);
      }

      // 3D Rain & Wind Weather Particle Simulation Update (with 3D Collision Occlusion & Splash VFX)
      if (weatherParticlesRef.current) {
        const isRainActive = envOverrideRef.current.enabled
          ? (envOverrideRef.current.rain_enabled ?? (envOverrideRef.current.rain_intensity !== undefined && envOverrideRef.current.rain_intensity > 0.01))
          : ((activeScene.environment.weather?.rain ?? 0) > 0.01);

        const rainIntensity = isRainActive
          ? (envOverrideRef.current.enabled
            ? (envOverrideRef.current.rain_intensity ?? 0)
            : (activeScene.environment.weather?.rain ?? 0))
          : 0;

        const windIntensity = envOverrideRef.current.enabled
          ? (envOverrideRef.current.wind_intensity ?? 0.3)
          : (activeScene.environment.weather?.wind ?? 0.3);

        const windDirection = envOverrideRef.current.enabled
          ? (envOverrideRef.current.wind_direction ?? 45)
          : (activeScene.environment.weather?.wind_direction ?? 45);

        const collisionQuality = envOverrideRef.current.enabled
          ? (envOverrideRef.current.rain_collision_quality ?? 2)
          : 2;

        weatherParticlesRef.current.update(
          delta,
          renderer.camera.position,
          rainIntensity,
          windIntensity,
          windDirection,
          mapGroupRef.current,
          collisionQuality
        );
      }

      // 3D Cinematic Cloud Simulation Update
      if (cloudSystemRef.current) {
        const cloudCoverage = envOverrideRef.current.enabled
          ? (envOverrideRef.current.cloud_coverage ?? 0.5)
          : (activeScene.environment.weather?.cloud_coverage ?? 0.5);

        const cloudType = envOverrideRef.current.enabled
          ? (envOverrideRef.current.cloud_type || 'cumulus')
          : (activeScene.environment.weather?.cloud_type || 'cumulus');

        const cloudAltitude = envOverrideRef.current.enabled
          ? (envOverrideRef.current.cloud_altitude ?? 1.0)
          : (activeScene.environment.weather?.cloud_altitude ?? 1.0);

        const cloudLayers = envOverrideRef.current.enabled
          ? (envOverrideRef.current.cloud_layers ?? 3)
          : (activeScene.environment.weather?.cloud_layers ?? 3);

        const skyTime = envOverrideRef.current.enabled
          ? envOverrideRef.current.sky_time
          : activeScene.environment.sky_time;

        const windIntensity = envOverrideRef.current.enabled
          ? (envOverrideRef.current.wind_intensity ?? 0.3)
          : (activeScene.environment.weather?.wind ?? 0.3);

        const windDirection = envOverrideRef.current.enabled
          ? (envOverrideRef.current.wind_direction ?? 45)
          : (activeScene.environment.weather?.wind_direction ?? 45);

        const isRainActive = envOverrideRef.current.enabled
          ? (envOverrideRef.current.rain_enabled ?? (envOverrideRef.current.rain_intensity !== undefined && envOverrideRef.current.rain_intensity > 0.01))
          : ((activeScene.environment.weather?.rain ?? 0) > 0.01);

        const rainIntensity = isRainActive
          ? (envOverrideRef.current.enabled
            ? (envOverrideRef.current.rain_intensity ?? 0)
            : (activeScene.environment.weather?.rain ?? 0))
          : 0;

        const isLightningActive = isRainActive && (
          envOverrideRef.current.enabled
            ? (envOverrideRef.current.lightning_enabled !== false && envOverrideRef.current.lightning_preset !== 'none')
            : true
        );

        const lightningFrequency = envOverrideRef.current.enabled
          ? (envOverrideRef.current.lightning_frequency ?? 0)
          : 0;

        const lightningCloudIntensity = envOverrideRef.current.enabled
          ? (envOverrideRef.current.lightning_cloud_intensity ?? 1.0)
          : 1.0;

        const lightningStrikeIntensity = envOverrideRef.current.enabled
          ? (envOverrideRef.current.lightning_strike_intensity ?? 1.0)
          : 1.0;

        let cloudFlashIntensity = 0;
        let sceneFlashIntensity = 0;
        let strikeOrigin: THREE.Vector3 | undefined;

        if (lightningSystemRef.current && isLightningActive) {
          const res = lightningSystemRef.current.update(
            delta,
            renderer.camera.position,
            rainIntensity,
            cloudAltitude * 90,
            lightningFrequency,
            lightningCloudIntensity,
            lightningStrikeIntensity
          );
          cloudFlashIntensity = res.cloudFlashIntensity;
          sceneFlashIntensity = res.sceneFlashIntensity;
          strikeOrigin = res.strikeOrigin;
        }

        if (lightingRef.current) {
          lightingRef.current.updateManual(
            envOverrideRef.current.enabled ? envOverrideRef.current : undefined,
            sceneFlashIntensity
          );
        }

        const customRainDarkness = envOverrideRef.current.enabled
          ? envOverrideRef.current.rain_darkness
          : undefined;

        const customShadowDarkness = envOverrideRef.current.enabled
          ? envOverrideRef.current.cloud_shadow_darkness
          : activeScene.environment.weather?.cloud_shadow_darkness;

        const customShadowScale = envOverrideRef.current.enabled
          ? envOverrideRef.current.cloud_shadow_scale
          : activeScene.environment.weather?.cloud_shadow_scale;

        cloudSystemRef.current.update(
          delta,
          renderer.camera.position,
          cloudCoverage,
          cloudType as any,
          windIntensity,
          windDirection,
          skyTime,
          cloudAltitude,
          cloudLayers,
          lightingRef.current?.sunLight.position,
          rainIntensity,
          cloudFlashIntensity,
          strikeOrigin,
          customRainDarkness,
          customShadowDarkness,
          customShadowScale
        );
      }
    });

    return () => {
      renderer.unmount();
      playerControllerRef.current?.dispose();
      weatherParticlesRef.current?.dispose();
      cloudSystemRef.current?.dispose();
      lightningSystemRef.current?.dispose();
    };
  }, []);

  // Re-init actors when scene changes
  const initActors = (newScene: MasterSceneConfig, scene3D: THREE.Scene) => {
    // Remove previous actors
    for (const runtime of actorsMapRef.current.values()) {
      scene3D.remove(runtime.avatar.rootObject);
    }
    actorsMapRef.current.clear();

    const snapRay = new THREE.Raycaster();
    snapRay.far = 60.0;

    for (const actorConfig of newScene.actors) {
      const avatar = new VRMAvatar(actorConfig);
      const animator = new ActorAnimator(avatar);
      const morph = new ActorMorphController(avatar);
      const lipSync = new ActorLipSync(avatar);
      const lookAt = new ActorLookAt(avatar);

      // Snap newly instantiated avatar directly onto the terrain surface
      if (mapCollidersRef.current.length > 0) {
        snapRay.set(new THREE.Vector3(avatar.rootObject.position.x, 35.0, avatar.rootObject.position.z), new THREE.Vector3(0, -1, 0));
        const hits = snapRay.intersectObjects(mapCollidersRef.current, false);
        for (const hit of hits) {
          if (hit.face && hit.point.y > -5.0 && hit.point.y < 30.0) {
            avatar.rootObject.position.y = hit.point.y;
            break;
          }
        }
      }

      scene3D.add(avatar.rootObject);
      actorsMapRef.current.set(actorConfig.id, {
        avatar,
        animator,
        morph,
        lipSync,
        lookAt,
      });
    }

    // Ensure newly created actors receive cloud shadow lighting hooks
    VolumetricCloudLighting.applyToScene(scene3D);
  };

  const handleTogglePlay = () => {
    clockRef.current.toggle();
    setIsPlaying(clockRef.current.isPlaying);
  };

  const handleSeek = (time: number) => {
    clockRef.current.seek(time);
    setCurrentTime(time);
    if (trackEvaluatorRef.current) {
      trackEvaluatorRef.current.evaluate(
        sceneRef.current,
        time,
        0.016,
        actorsMapRef.current,
        sceneObjectsRef.current
      );
    }
  };

  const handleToggleLoop = () => {
    clockRef.current.isLooping = !clockRef.current.isLooping;
    setIsLooping(clockRef.current.isLooping);
  };

  const handleChangePlaybackRate = (rate: number) => {
    clockRef.current.playbackRate = rate;
    setPlaybackRate(rate);
  };

  const handleInspectDialogue = (dlg: DialogueManifestItem) => {
    if (inspectingActorId === dlg.speaker_id) {
      // Toggle off: clear inspect mode and restore camera
      cameraDirectorRef.current?.clearInspectMode();
      setInspectingActorId(null);
    } else {
      // Toggle on: seek to dialogue timestamp without overriding pause state
      setIsFreeCam(false);
      isFreeCamRef.current = false;
      rendererRef.current?.setFreeCam(false);
      cameraDirectorRef.current?.setInspectMode(dlg.speaker_id, inspectAngle);
      setInspectingActorId(dlg.speaker_id);
    }
  };

  const handleResetCamera = () => {
    if (cameraDirectorRef.current) {
      cameraDirectorRef.current.clearInspectMode();
    }
    setInspectingActorId(null);
    setIsFreeCam(false);
    isFreeCamRef.current = false;
    if (rendererRef.current) rendererRef.current.setFreeCam(false);
  };

  const handleToggleFreeCam = () => {
    const next = !isFreeCam;
    setIsFreeCam(next);
    isFreeCamRef.current = next;
    saveViewportSetting('isFreeCam', next);
    if (rendererRef.current) {
      rendererRef.current.setFreeCam(next);
    }
  };

  const handleChangeInspectAngle = (angle: InspectCameraAngle) => {
    setInspectAngle(angle);
    if (cameraDirectorRef.current) {
      cameraDirectorRef.current.setInspectAngle(angle);
    }
  };

  const handlePreviewSpeech = (dlg: DialogueManifestItem) => {
    clockRef.current.seek(dlg.start_time);
  };

  const handleUpdateScene = async (newScene: MasterSceneConfig) => {
    const safeScene: MasterSceneConfig = {
      ...newScene,
      subtitles_config: newScene.subtitles_config || {
        enable_overlay: true,
        burn_in_export: true,
        font_size: 20,
        show_speaker_name: true,
        position: 'bottom',
        text_color: '#ffffff',
      },
    };
    sceneRef.current = safeScene;
    setScene(safeScene);
    clockRef.current.seek(0);
    clockRef.current.setDuration(safeScene.duration);
    setCurrentTime(0);
    setIsPlaying(false);
    setInspectingActorId(null);

    if (combatSyncRef.current) {
      combatSyncRef.current.reset();
    }
    if (lightingRef.current) {
      lightingRef.current.applyEnvironment(newScene.environment);
      lightingRef.current.update(0, newScene.duration);
    }
    if (rendererRef.current) {
      await initEnvironment(newScene, rendererRef.current.scene);
      initActors(newScene, rendererRef.current.scene);
      if (trackEvaluatorRef.current) {
        trackEvaluatorRef.current.evaluate(
          newScene,
          0,
          0.016,
          actorsMapRef.current,
          sceneObjectsRef.current
        );
      }
    }
  };

  // Place Prop from Asset Browser into Scene
  const handlePlaceProp = (asset: any) => {
    if (!rendererRef.current) return;

    // Find active actor or camera center
    const activeActor = actorsMapRef.current.get(playerControllerRef.current?.controlledActorId || '')
      || Array.from(actorsMapRef.current.values())[0];

    let targetPos: [number, number, number] = [0, 0, 0];
    if (activeActor && activeActor.avatar.rootObject) {
      const p = activeActor.avatar.rootObject.position;
      const rotY = activeActor.avatar.rootObject.rotation.y;
      // Spawn 2 meters in front of actor
      targetPos = [
        parseFloat((p.x + Math.sin(rotY) * 2.0).toFixed(2)),
        0,
        parseFloat((p.z + Math.cos(rotY) * 2.0).toFixed(2)),
      ];
    }

    const propId = `placed_${asset.id}_${Date.now().toString().slice(-4)}`;
    const newProp: PlacedProp = {
      id: propId,
      asset_path: asset.path,
      position: targetPos,
      scale: asset.propData?.scale || 1.0,
      type: asset.propData?.type || 'furniture',
      is_obstacle: asset.propData?.is_obstacle !== undefined ? asset.propData.is_obstacle : true,
      obstacle_radius: asset.propData?.obstacle_radius || 0.6,
      smart_socket: asset.propData?.smart_socket,
    };

    const updatedScene: MasterSceneConfig = {
      ...sceneRef.current,
      environment: {
        ...sceneRef.current.environment,
        placed_props: [...(sceneRef.current.environment.placed_props || []), newProp],
      },
    };

    sceneRef.current = updatedScene;
    setScene(updatedScene);

    // Instantiate 3D Prop directly in mapGroupRef
    MapPresetManager.buildPlacedProps(
      {
        map_id: 'custom_runtime_props',
        name: 'Runtime Props',
        placed_props: [newProp],
      },
      mapGroupRef.current,
      sceneObjectsRef.current
    );
  };

  // Switch Map from Asset Browser
  const handleSelectMap = (mapIdOrGlb: string) => {
    const isGlb = mapIdOrGlb.endsWith('.glb') || mapIdOrGlb.endsWith('.gltf');
    const updatedScene: MasterSceneConfig = {
      ...sceneRef.current,
      environment: {
        ...sceneRef.current.environment,
        map: isGlb ? mapIdOrGlb : 'farming_village',
        map_preset: isGlb ? undefined : mapIdOrGlb,
      },
    };

    handleUpdateScene(updatedScene);
  };

  // Switch Avatar from Asset Browser
  const handleSelectAvatar = async (actorId: string, vrmUrl: string) => {
    const targetActor = sceneRef.current.actors.find((a) => a.id === actorId) || sceneRef.current.actors[0];
    if (!targetActor) return;

    targetActor.model = vrmUrl;
    const updatedScene = { ...sceneRef.current };
    handleUpdateScene(updatedScene);
  };

  // Play Animation Preview from Asset Browser
  const handlePlayAnimationPreview = (animName: string) => {
    const targetId = playerControllerRef.current?.controlledActorId || sceneRef.current.actors[0]?.id;
    if (targetId) {
      const runtime = actorsMapRef.current.get(targetId);
      if (runtime && runtime.animator) {
        runtime.animator.setAction(animName as any);
      }
    }
  };

  // Update Transform from Inspector in Real-Time
  const handleUpdateTransform = (updated: SelectedSceneObject) => {
    setSelectedObject(updated);
    selectedObjectRef.current = updated;

    if (updated.category === 'actor') {
      const actorIndex = sceneRef.current.actors.findIndex((a) => a.id === updated.id);
      if (actorIndex !== -1) {
        const actor = { ...sceneRef.current.actors[actorIndex] };
        actor.spawn_point = [...updated.position];
        actor.rotation_y = (updated.rotation[1] * Math.PI) / 180;

        // Apply immediately to live Three.js avatar
        const runtime = actorsMapRef.current.get(updated.id);
        if (runtime && runtime.avatar.rootObject) {
          runtime.avatar.rootObject.position.set(...updated.position);
          runtime.avatar.rootObject.rotation.y = actor.rotation_y;
        }

        const newActors = [...sceneRef.current.actors];
        newActors[actorIndex] = actor;

        const nextScene: MasterSceneConfig = {
          ...sceneRef.current,
          actors: newActors,
        };
        sceneRef.current = nextScene;
        setScene(nextScene);
      }
    } else if (updated.category === 'prop') {
      const placedProps = sceneRef.current.environment.placed_props || [];
      const propIndex = placedProps.findIndex((p) => p.id === updated.id);
      if (propIndex !== -1) {
        const prop: PlacedProp = {
          ...placedProps[propIndex],
          position: [...updated.position],
          rotation: [
            (updated.rotation[0] * Math.PI) / 180,
            (updated.rotation[1] * Math.PI) / 180,
            (updated.rotation[2] * Math.PI) / 180,
          ],
          scale: updated.scale,
          is_obstacle: updated.isObstacle,
          obstacle_radius: updated.obstacleRadius || 0.6,
          smart_socket: updated.socketType && updated.socketType !== 'none' ? {
            socket_type: updated.socketType,
            entry_offset: [0, 0, 0.8],
            target_offset: [0, 0.5, 0],
            target_rotation_y: 0,
          } : undefined,
        };

        const newPlacedProps = [...placedProps];
        newPlacedProps[propIndex] = prop;

        // Apply immediately to live Three.js 3D prop object
        const threeObj = sceneObjectsRef.current.get(updated.id) || mapGroupRef.current.getObjectByName(updated.id) || rendererRef.current?.scene.getObjectByName(updated.id);
        if (threeObj) {
          threeObj.position.set(updated.position[0], updated.position[1], updated.position[2]);
          if (prop.rotation) {
            threeObj.rotation.set(prop.rotation[0], prop.rotation[1], prop.rotation[2]);
          }
          if (typeof prop.scale === 'number') {
            threeObj.scale.setScalar(prop.scale);
          }
        }

        const nextScene: MasterSceneConfig = {
          ...sceneRef.current,
          environment: {
            ...sceneRef.current.environment,
            placed_props: newPlacedProps,
          },
        };
        sceneRef.current = nextScene;
        setScene(nextScene);
      }
    }
  };

  // Delete Prop from Scene
  const handleDeleteProp = (propId: string) => {
    const placedProps = sceneRef.current.environment.placed_props || [];
    const filteredProps = placedProps.filter((p) => p.id !== propId);

    // Remove from 3D Map Group
    const threeObj = sceneObjectsRef.current.get(propId) || mapGroupRef.current.getObjectByName(propId);
    if (threeObj) {
      mapGroupRef.current.remove(threeObj);
      sceneObjectsRef.current.delete(propId);
    }

    if (selectedObjectRef.current?.id === propId) {
      rendererRef.current?.gizmo?.detach();
    }

    const nextScene: MasterSceneConfig = {
      ...sceneRef.current,
      environment: {
        ...sceneRef.current.environment,
        placed_props: filteredProps,
      },
    };
    sceneRef.current = nextScene;
    setScene(nextScene);
    setSelectedObject(null);
  };

  // Duplicate Prop in Scene
  const handleDuplicateProp = (prop: PlacedProp) => {
    if (!rendererRef.current) return;
    const newId = `placed_${prop.id.replace('placed_', '')}_copy_${Date.now().toString().slice(-3)}`;
    const clonedProp: PlacedProp = {
      ...prop,
      id: newId,
      position: [
        parseFloat((prop.position[0] + 1.2).toFixed(2)),
        prop.position[1],
        parseFloat((prop.position[2] + 1.2).toFixed(2)),
      ],
    };

    const nextScene: MasterSceneConfig = {
      ...sceneRef.current,
      environment: {
        ...sceneRef.current.environment,
        placed_props: [...(sceneRef.current.environment.placed_props || []), clonedProp],
      },
    };
    sceneRef.current = nextScene;
    setScene(nextScene);

    // Instantiate in 3D
    MapPresetManager.buildPlacedProps(
      {
        map_id: 'custom_runtime_props',
        name: 'Runtime Props',
        placed_props: [clonedProp],
      },
      mapGroupRef.current,
      sceneObjectsRef.current
    );
  };

  // Focus Camera on Object
  const handleFocusObject = (position: [number, number, number]) => {
    if (!rendererRef.current) return;
    rendererRef.current.camera.position.set(position[0] + 3.0, position[1] + 2.5, position[2] + 4.0);
    rendererRef.current.camera.lookAt(position[0], position[1] + 0.8, position[2]);
    if (rendererRef.current.controls) {
      rendererRef.current.controls.target.set(position[0], position[1] + 0.8, position[2]);
      rendererRef.current.controls.update();
    }
  };

  // Select Object and attach Unity 3D Transform Gizmo
  const handleSelectObject = (obj: SelectedSceneObject | null) => {
    setSelectedObject(obj);
    selectedObjectRef.current = obj;
    if (!rendererRef.current || !rendererRef.current.gizmo) return;

    if (!obj) {
      rendererRef.current.gizmo.detach();
      return;
    }

    if (obj.category === 'actor') {
      const runtime = actorsMapRef.current.get(obj.id);
      if (runtime && runtime.avatar.rootObject) {
        rendererRef.current.gizmo.attach(obj.id, runtime.avatar.rootObject);
      }
    } else if (obj.category === 'prop') {
      const threeObj = sceneObjectsRef.current.get(obj.id) || mapGroupRef.current.getObjectByName(obj.id);
      if (threeObj) {
        rendererRef.current.gizmo.attach(obj.id, threeObj);
      }
    }
  };

  const handleExportVideo = async (targetFps: number = 60) => {
    if (!rendererRef.current) return;
    setIsExporting(true);
    setExportProgressMsg(`Đang chuẩn bị WebCodecs GPU encoder (${targetFps} FPS)...`);

    // 1. Pause live requestAnimationFrame loop to prevent race conditions during offline export
    rendererRef.current.stop();
    if (combatSyncRef.current) combatSyncRef.current.reset();
    if (vfxTriggerRef.current) vfxTriggerRef.current.clear();
    if (gifOverlayRef.current) gifOverlayRef.current.reset();
    if (postProcessorRef.current) postProcessorRef.current.clear();

    try {
      const recorder = new WebCodecsRecorder();
      clockRef.current.seek(0);
      clockRef.current.pause();

      const blob = await recorder.recordOffline(
        rendererRef.current.getDomElement(),
        sceneRef.current,
        sceneRef.current.duration,
        targetFps,
        (time: number) => {
          const delta = 1 / targetFps;
          const activeScene = sceneRef.current;

          // Explicitly update engine for this timestamp and render
          if (trackEvaluatorRef.current) {
            trackEvaluatorRef.current.evaluate(
              activeScene,
              time,
              delta,
              actorsMapRef.current,
              sceneObjectsRef.current,
              playerControllerRef.current?.controlledActorId || null
            );
          }

          // Player Controller Update (if needed)
          if (playerControllerRef.current && playerControllerRef.current.controlledActorId) {
            const controlledRuntime = actorsMapRef.current.get(playerControllerRef.current.controlledActorId);
            if (controlledRuntime && rendererRef.current) {
              playerControllerRef.current.update(delta, controlledRuntime.avatar, controlledRuntime.animator, rendererRef.current.camera, mapCollidersRef.current);
            }
          }

          // Ground Snapping (Physics) with Fast Spatial Filter
          if (mapCollidersRef.current.length > 0) {
            const raycaster = new THREE.Raycaster();
            raycaster.far = 30.0;

            for (const [id, runtime] of actorsMapRef.current.entries()) {
              const isClimbing = (runtime.avatar.config.tracks.movement || []).some(
                (m) => m.action === 'climb' && time >= m.start && time <= m.end
              );
              if (isClimbing) continue;

              const pos = runtime.avatar.rootObject.position;

              raycaster.set(new THREE.Vector3(pos.x, pos.y + 6.0, pos.z), new THREE.Vector3(0, -1, 0));
              const hits = raycaster.intersectObjects(mapCollidersRef.current, false);

              let validGroundY: number | null = null;
              for (const hit of hits) {
                if (hit.face) {
                  const normalMatrix = new THREE.Matrix3().getNormalMatrix(hit.object.matrixWorld);
                  const worldNormal = hit.face.normal.clone().applyMatrix3(normalMatrix).normalize();
                  if (worldNormal.y > 0.35 && hit.point.y <= pos.y + 5.5 && hit.point.y > -5.0) {
                    validGroundY = hit.point.y;
                    break;
                  }
                }
              }
              if (validGroundY !== null) {
                pos.y = validGroundY;
              } else if (pos.y < 0) {
                pos.y = 0;
              }
            }
          }

          // Collect actor visual states
          const actorStates = new Map<string, any>();
          for (const [id, runtime] of actorsMapRef.current.entries()) {
            const p = new THREE.Vector3();
            runtime.avatar.rootObject.getWorldPosition(p);
            const head = runtime.avatar.getHeadPosition();
            const rotY = runtime.avatar.rootObject.rotation.y;
            actorStates.set(id, { position: p, headPosition: head, rotationY: rotY });
          }

          // Camera Director Update
          if (!isFreeCamRef.current && cameraDirectorRef.current) {
            cameraDirectorRef.current.update(activeScene, time, actorStates as any, delta);
          }

          // Post processing
          if (!isFreeCamRef.current && postProcessorRef.current && rendererRef.current) {
            postProcessorRef.current.applyToCamera(rendererRef.current.camera, time);
          }

          // VFX Update
          if (vfxTriggerRef.current) {
            vfxTriggerRef.current.update(delta);
          }

          if (lightingRef.current) {
            if (envOverrideRef.current.enabled) {
              lightingRef.current.updateManual(envOverrideRef.current);
            } else {
              lightingRef.current.update(time, activeScene.duration);
            }
          }

          // Update map animation
          if (mapMixerRef.current) {
            mapMixerRef.current.update(delta);
          }

          rendererRef.current?.renderDirect();
        },
        (p) => {
          setExportProgressMsg(`${p.percent}% (${p.currentFrame}/${p.totalFrames})`);
        }
      );

      VideoMuxer.downloadVideoBlob(blob, `${sceneRef.current.scene_id}_${targetFps}fps`);
      setExportProgressMsg('Xuất thành công!');
    } catch (e) {
      console.error(e);
      alert(`Lỗi xuất video: ${e}`);
    } finally {
      setIsExporting(false);
      // Resume live render loop
      if (rendererRef.current) {
        rendererRef.current.start();
      }
    }
  };

  const handleImportCustomMap = async (files: FileList | File[]) => {
    if (!rendererRef.current) return;
    setIsLoadingMap(true);
    try {
      const fileList = Array.from(files);
      if (fileList.length === 0) return;

      let customModel: THREE.Group | null = null;
      let loadedGltf: any = null;
      let mapTitle = 'Custom Map';

      const glbFile = fileList.find((f) => f.name.toLowerCase().endsWith('.glb'));
      const gltfFile = fileList.find((f) => f.name.toLowerCase().endsWith('.gltf'));

      if (glbFile && fileList.length === 1) {
        // 1. Single .glb standalone file
        mapTitle = glbFile.name;
        const arrayBuffer = await glbFile.arrayBuffer();
        const loader = new GLTFLoader();
        loadedGltf = await loader.parseAsync(arrayBuffer, '');
        customModel = loadedGltf.scene;
      } else if (gltfFile || glbFile) {
        // 2. Folder containing .gltf + .bin + textures or .glb with textures
        const mainFile = gltfFile || glbFile!;
        mapTitle = mainFile.name;

        const binFile = fileList.find((f) => f.name.toLowerCase().endsWith('.bin'));
        if (!binFile && gltfFile && fileList.length === 1) {
          throw new Error(
            `File '${gltfFile.name}' là file cấu trúc JSON rời, cần có file '.bin' và thư mục 'textures/' đi kèm.\n\n👉 Vui lòng bấm nút "📂 Chọn Folder Map (.gltf)" để chọn cả thư mục '${gltfFile.name.replace('.gltf', '')}', hoặc chọn file '.glb' đóng gói sẵn!`
          );
        }

        // Create Object URLs for all files in the folder
        const manager = new THREE.LoadingManager();
        const fileMap = new Map<string, string>();

        for (const file of fileList) {
          const url = URL.createObjectURL(file);
          fileMap.set(file.name.toLowerCase(), url);

          if (file.webkitRelativePath) {
            const parts = file.webkitRelativePath.split('/');
            parts.shift(); // remove root folder
            const subPath = parts.join('/').toLowerCase();
            fileMap.set(subPath, url);
            fileMap.set(`./${subPath}`, url);
            fileMap.set(file.webkitRelativePath.toLowerCase(), url);
          }
        }

        manager.setURLModifier((url) => {
          const decoded = decodeURIComponent(url).split('?')[0];
          const fileName = decoded.split(/[/\\]/).pop()?.toLowerCase() || '';
          const subMatch = decoded.toLowerCase().match(/(textures[/\\][^/\\]+)$/);
          const subPath = subMatch ? subMatch[1].replace(/\\/g, '/') : '';

          const resolved =
            (subPath ? fileMap.get(subPath) : null) ||
            fileMap.get(fileName) ||
            (subPath ? fileMap.get(`./${subPath}`) : null) ||
            fileMap.get(`./${fileName}`) ||
            url;

          return resolved;
        });

        const loader = new GLTFLoader(manager);
        const mainUrl = fileMap.get(mainFile.name.toLowerCase()) || URL.createObjectURL(mainFile);
        loadedGltf = await loader.loadAsync(mainUrl);
        customModel = loadedGltf.scene;
      } else {
        throw new Error('Không tìm thấy file 3D hợp lệ (.gltf hoặc .glb) trong thư mục đã chọn!');
      }

      if (!customModel) throw new Error('Không thể khởi tạo model 3D từ dữ liệu đã chọn.');

      customModel.name = 'imported_custom_map';

      if (mapGroupRef.current.parent !== rendererRef.current.scene) {
        rendererRef.current.scene.add(mapGroupRef.current);
      }

      // Clear previous map objects
      while (mapGroupRef.current.children.length > 0) {
        const child = mapGroupRef.current.children[0];
        mapGroupRef.current.remove(child);
      }

      // Auto-scale to ideal 3D world footprint
      // Removed forced scaling down to 26 units. Maps should retain their authored scale 
      // and position (0,0,0) so characters stand correctly on the ground.
      customModel.position.set(0, 0, 0);

      // Extract and play animations (if map has built-in animations like wind, water)
      if (loadedGltf && loadedGltf.animations && loadedGltf.animations.length > 0) {
        mapMixerRef.current = new THREE.AnimationMixer(customModel);
        loadedGltf.animations.forEach((clip: any) => {
          mapMixerRef.current?.clipAction(clip).play();
        });
      } else {
        mapMixerRef.current = null;
      }

      mapGroupRef.current.add(customModel);

      // Ensure fast ground collision plane
      const groundPlane = new THREE.Mesh(
        new THREE.PlaneGeometry(500, 500),
        new THREE.MeshBasicMaterial({ visible: false })
      );
      groundPlane.rotation.x = -Math.PI / 2;
      groundPlane.position.y = 0;
      groundPlane.name = 'fast_physics_ground_plane';
      mapGroupRef.current.add(groundPlane);

      // Collect Map Colliders for Ground Snapping (ground plane + lightweight props only)
      mapCollidersRef.current = [groundPlane];
      customModel.traverse((child) => {
        if ((child as THREE.Mesh).isMesh && child !== groundPlane) {
          const mesh = child as THREE.Mesh;
          if (mesh.geometry && mesh.geometry.attributes.position && mesh.geometry.attributes.position.count < 10000) {
            mapCollidersRef.current.push(child);
          }
        }
      });

      // Update scene config
      const updatedScene: MasterSceneConfig = {
        ...sceneRef.current,
        title: `🗺️ Map Tùy Chỉnh (${mapTitle})`,
        environment: {
          ...sceneRef.current.environment,
          map: mapTitle,
          weather: {
            ...sceneRef.current.environment.weather,
            fog: 0.000,
          },
        },
      };
      sceneRef.current = updatedScene;
      setScene(updatedScene);
    } catch (e: any) {
      console.error(e);
      alert(`Lỗi khi nạp file map:\n${e?.message || e}`);
    } finally {
      setIsLoadingMap(false);
    }
  };

  // Convert actor map for child components
  const actorsOnlyMap = new Map<string, VRMAvatar>();
  for (const [id, runtime] of actorsMapRef.current.entries()) {
    actorsOnlyMap.set(id, runtime.avatar);
  }

  return (
    <StudioLayout
      scene={scene}
      renderer={rendererRef.current}
      fps={fps}
      currentTime={currentTime}
      isPlaying={isPlaying}
      playbackRate={playbackRate}
      isLooping={isLooping}
      activeSubtitle={activeSubtitle}
      actors={actorsOnlyMap}
      navMesh={navMeshRef.current}
      onTogglePlay={handleTogglePlay}
      onSeek={handleSeek}
      onToggleLoop={handleToggleLoop}
      onChangePlaybackRate={handleChangePlaybackRate}
      onInspectDialogue={handleInspectDialogue}
      onPreviewSpeech={handlePreviewSpeech}
      inspectAngle={inspectAngle}
      inspectingActorId={inspectingActorId}
      onChangeInspectAngle={handleChangeInspectAngle}
      onResetCamera={handleResetCamera}
      isFreeCam={isFreeCam}
      isLoadingMap={isLoadingMap}
      onToggleFreeCam={handleToggleFreeCam}
      onUpdateScene={handleUpdateScene}
      onImportCustomMap={handleImportCustomMap}
      onExportVideo={handleExportVideo}
      isExporting={isExporting}
      exportProgressMsg={exportProgressMsg}
      envOverride={envOverride}
      onUpdateEnvOverride={setEnvOverride}
      onPlaceProp={handlePlaceProp}
      onSelectMap={handleSelectMap}
      onSelectAvatar={handleSelectAvatar}
      onPlayAnimationPreview={handlePlayAnimationPreview}
      selectedObject={selectedObject}
      onSelectObject={handleSelectObject}
      onUpdateTransform={handleUpdateTransform}
      onDeleteProp={handleDeleteProp}
      onDuplicateProp={handleDuplicateProp}
      onFocusObject={handleFocusObject}
    />
  );
};
