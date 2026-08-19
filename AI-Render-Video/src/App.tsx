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

export const App: React.FC = () => {
  const [scene, setScene] = useState<MasterSceneConfig>(defaultScene);
  const sceneRef = useRef<MasterSceneConfig>(defaultScene);
  const [currentTime, setCurrentTime] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackRate, setPlaybackRate] = useState(1.0);
  const [isLooping, setIsLooping] = useState(true);
  const [fps, setFps] = useState(60);
  const [activeSubtitle, setActiveSubtitle] = useState<ActiveSubtitle | null>(null);
  const [inspectAngle, setInspectAngle] = useState<InspectCameraAngle>('front');
  const [inspectingActorId, setInspectingActorId] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [exportProgressMsg, setExportProgressMsg] = useState('');
  const [isFreeCam, setIsFreeCam] = useState(false);
  const isFreeCamRef = useRef(false);
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

  // Populate complete village props (ground, tree, chair, farm, duck, lantern)
  const populateVillageProps = (group: THREE.Group) => {
    const ground = AssetLoaderRegistry.createGround();
    group.add(ground);

    const tree = AssetLoaderRegistry.createTree([4, 0, -3]);
    group.add(tree);

    const chair = AssetLoaderRegistry.createChair([-4, 0, -2]);
    group.add(chair);

    const farm = AssetLoaderRegistry.createFarmPlot([0, 0, -5]);
    group.add(farm);
    const crop = farm.getObjectByName('crop');
    if (crop) {
      sceneObjectsRef.current.set('props.farm_plot_01.crop', crop);
    }

    const duck = AssetLoaderRegistry.createDuckProp([0.8, 0, -2.5]);
    group.add(duck);

    const lanternStand = AssetLoaderRegistry.createLanternStand([-3.2, 0, -2.0]);
    group.add(lanternStand);
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

    const mapName = (newScene.environment.map || '').toLowerCase();
    const isCustomMap =
      (mapName.endsWith('.glb') || mapName.endsWith('.gltf')) &&
      !mapName.includes('village');

    if (isCustomMap) {
      const glbUrl = mapName.startsWith('assets/') || mapName.startsWith('/assets/')
        ? (mapName.startsWith('/') ? mapName : `/${mapName}`)
        : `/assets/maps/${newScene.environment.map}`;

      try {
        setIsLoadingMap(true);
        const mapModel = await AssetLoaderRegistry.loadGLTF(glbUrl);
        mapModel.name = 'custom_glb_map_mesh';

        // Removed forced scaling down to 26 units. Maps should retain their authored scale 
        // and position (0,0,0) so characters stand correctly on the ground.
        mapModel.position.set(0, 0, 0);
        mapGroupRef.current.add(mapModel);
      } catch (err) {
        console.warn('Không tải được model map tùy chỉnh, chuyển về map làng quê mẫu:', err);
        populateVillageProps(mapGroupRef.current);
      } finally {
        setIsLoadingMap(false);
      }
    } else {
      populateVillageProps(mapGroupRef.current);
    }

    // Collect Map Colliders for Ground Snapping
    mapCollidersRef.current = [];
    mapGroupRef.current.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        mapCollidersRef.current.push(child);
      }
    });

    if (occlusionFoliageRef.current) {
      occlusionFoliageRef.current.registerSceneFoliage(scene3D);
    }
  };

  // Setup 3D Scene once
  useEffect(() => {
    const renderer = new ThreeRenderer();
    rendererRef.current = renderer;

    const lighting = new SceneLighting(renderer.scene);
    lightingRef.current = lighting;
    lighting.applyEnvironment(sceneRef.current.environment);

    const postProcessor = new PostProcessor();
    postProcessorRef.current = postProcessor;

    const vfxTrigger = new CombatVFXTrigger(renderer.scene);
    vfxTriggerRef.current = vfxTrigger;

    const combatSync = new CombatSyncEngine(vfxTrigger, postProcessor);
    combatSyncRef.current = combatSync;

    const pathNav = new PathNavigator(navMeshRef.current);
    cameraDirectorRef.current = new CameraDirector(renderer.camera);
    occlusionFoliageRef.current = new OcclusionFoliageManager();
    trackEvaluatorRef.current = new TrackEvaluator(combatSync, pathNav);
    playerControllerRef.current = new PlayerController();

    // Build Environment & Actors
    initEnvironment(sceneRef.current, renderer.scene);
    initActors(sceneRef.current, renderer.scene);

    // Main Render & Evaluation Loop (60-120fps GPU)
    renderer.onRender((delta) => {
      clockRef.current.update(delta);
      const t = clockRef.current.currentTime;
      setCurrentTime(t);
      setFps(renderer.fps);

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
        for (const [id, runtime] of actorsMapRef.current.entries()) {
          const isClimbing = (runtime.avatar.config.tracks.movement || []).some(
            (m) => m.action === 'climb' && t >= m.start && t <= m.end
          );
          if (isClimbing) continue;
          
          const isPlayer = id === playerControllerRef.current?.controlledActorId;
          const pos = runtime.avatar.rootObject.position;
          
          raycaster.set(new THREE.Vector3(pos.x, pos.y + 2.0, pos.z), new THREE.Vector3(0, -1, 0));
          const hits = raycaster.intersectObjects(mapCollidersRef.current, false);
          
          let validGroundY: number | null = null;
          for (const hit of hits) {
            if (hit.face) {
              const normalMatrix = new THREE.Matrix3().getNormalMatrix(hit.object.matrixWorld);
              const worldNormal = hit.face.normal.clone().applyMatrix3(normalMatrix).normalize();
              // Only consider it ground if the slope is not too steep (worldNormal.y > 0.6 is < ~53 degrees)
              if (worldNormal.y > 0.6) {
                validGroundY = hit.point.y;
                break;
              }
            }
          }

          if (validGroundY !== null) {
            const groundY = validGroundY;
            
            if (isPlayer && playerControllerRef.current) {
              if (pos.y <= groundY + 0.05) {
                pos.y = groundY;
                playerControllerRef.current.isGrounded = true;
                playerControllerRef.current.velocityY = 0;
              } else {
                playerControllerRef.current.isGrounded = false;
              }
            } else {
              pos.y = groundY;
            }
          } else {
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

      // Screen Shake Post-processing (only in Director Cam)
      if (!isFreeCamRef.current && postProcessorRef.current) {
        postProcessorRef.current.applyToCamera(renderer.camera, t);
      }

      // Combat VFX particles update
      if (vfxTriggerRef.current) {
        vfxTriggerRef.current.update(delta);
      }

      // Map animations
      if (mapMixerRef.current) {
        mapMixerRef.current.update(delta);
      }

      // Active Subtitle Update
      const sub = SubtitleSynchronizer.getActiveSubtitle(activeScene, t);
      setActiveSubtitle(sub);

      // Dynamic Sun & Lighting update across timeline
      if (lightingRef.current) {
        if (envOverrideRef.current.enabled) {
          lightingRef.current.updateManual(envOverrideRef.current);
        } else {
          lightingRef.current.update(t, activeScene.duration);
        }
      }
    });

    return () => {
      renderer.unmount();
      playerControllerRef.current?.dispose();
    };
  }, []);

  // Re-init actors when scene changes
  const initActors = (newScene: MasterSceneConfig, scene3D: THREE.Scene) => {
    // Remove previous actors
    for (const runtime of actorsMapRef.current.values()) {
      scene3D.remove(runtime.avatar.rootObject);
    }
    actorsMapRef.current.clear();

    for (const actorConfig of newScene.actors) {
      const avatar = new VRMAvatar(actorConfig);
      const animator = new ActorAnimator(avatar);
      const morph = new ActorMorphController(avatar);
      const lipSync = new ActorLipSync(avatar);
      const lookAt = new ActorLookAt(avatar);

      scene3D.add(avatar.rootObject);
      actorsMapRef.current.set(actorConfig.id, {
        avatar,
        animator,
        morph,
        lipSync,
        lookAt,
      });
    }
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
      // Toggle on: seek, play, and inspect
      setIsFreeCam(false);
      isFreeCamRef.current = false;
      if (rendererRef.current) rendererRef.current.setFreeCam(false);

      clockRef.current.seek(dlg.start_time);
      clockRef.current.play();
      setIsPlaying(true);
      if (cameraDirectorRef.current) {
        cameraDirectorRef.current.setInspectMode(dlg.speaker_id, 12.0, inspectAngle);
        setInspectingActorId(dlg.speaker_id);
      }
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
    setInspectingActorId(null);
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

  const handleUpdateScene = (newScene: MasterSceneConfig) => {
    sceneRef.current = newScene;
    setScene(newScene);
    clockRef.current.seek(0);
    clockRef.current.setDuration(newScene.duration);
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
      initEnvironment(newScene, rendererRef.current.scene);
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

  const handleExportVideo = async (targetFps: number = 120) => {
    if (!rendererRef.current) return;
    setIsExporting(true);
    setExportProgressMsg(`Đang chuẩn bị WebCodecs GPU encoder (${targetFps} FPS)...`);

    try {
      const recorder = new WebCodecsRecorder();
      clockRef.current.seek(0);
      clockRef.current.play();

      const blob = await recorder.recordCanvasLive(
        rendererRef.current.getDomElement(),
        sceneRef.current,
        sceneRef.current.duration,
        targetFps,
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

      // Collect Map Colliders for Ground Snapping
      mapCollidersRef.current = [];
      customModel.traverse((child) => {
        if ((child as THREE.Mesh).isMesh) {
          mapCollidersRef.current.push(child);
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
    />
  );
};
