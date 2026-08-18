import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { MasterSceneConfig, DialogueManifestItem } from './types/scene';
import { villageClashScene } from './samples/villageClashScene';
import { cathedralScene } from './samples/cathedralScene';
import { pirateMapScene } from './samples/pirateMapScene';
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
  const [scene, setScene] = useState<MasterSceneConfig>(villageClashScene);
  const sceneRef = useRef<MasterSceneConfig>(villageClashScene);
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
  const clockRef = useRef<MasterClock>(new MasterClock(villageClashScene.duration));
  const actorsMapRef = useRef<Map<string, ActorRuntime>>(new Map());
  const sceneObjectsRef = useRef<Map<string, THREE.Object3D>>(new Map());
  const mapGroupRef = useRef<THREE.Group>(new THREE.Group());

  // Dynamic Environment Loader (supports .glb maps and procedural village)
  const initEnvironment = async (newScene: MasterSceneConfig, scene3D: THREE.Scene) => {
    if (!mapGroupRef.current.parent) {
      scene3D.add(mapGroupRef.current);
    }
    while (mapGroupRef.current.children.length > 0) {
      const child = mapGroupRef.current.children[0];
      mapGroupRef.current.remove(child);
    }
    sceneObjectsRef.current.clear();

    const mapName = (newScene.environment.map || '').toLowerCase();

    if (mapName.includes('cathedral') || mapName.includes('pirate') || (mapName.endsWith('.glb') && !mapName.includes('village')) || mapName.endsWith('.gltf')) {
      // Add immediate ground plane so it's never a dark void
      const tempFloorGeo = new THREE.PlaneGeometry(80, 80);
      const tempFloorMat = new THREE.MeshStandardMaterial({ color: 0x334455, roughness: 0.8 });
      const tempFloor = new THREE.Mesh(tempFloorGeo, tempFloorMat);
      tempFloor.rotation.x = -Math.PI / 2;
      tempFloor.position.y = -0.01;
      tempFloor.receiveShadow = true;
      mapGroupRef.current.add(tempFloor);

      // High-Intensity Multi-Angle Directional & Ambient Lighting
      const dirLight = new THREE.DirectionalLight(0xfffaed, 3.0);
      dirLight.position.set(20, 35, 20);
      mapGroupRef.current.add(dirLight);

      const hemiLight = new THREE.HemisphereLight(0xddf0ff, 0x554433, 2.0);
      mapGroupRef.current.add(hemiLight);

      const fillLight = new THREE.AmbientLight(0xffffff, 1.8);
      mapGroupRef.current.add(fillLight);

      const glbUrl = mapName.startsWith('assets/') || mapName.startsWith('/assets/')
        ? (mapName.startsWith('/') ? mapName : `/${mapName}`)
        : `/assets/maps/${newScene.environment.map}`;

      try {
        setIsLoadingMap(true);
        const mapModel = await AssetLoaderRegistry.loadGLTF(glbUrl);
        mapModel.name = 'custom_glb_map_mesh';

        // Auto-scale to ideal 3D world footprint
        mapModel.updateMatrixWorld(true);
        const bbox = new THREE.Box3().setFromObject(mapModel);
        const size = bbox.getSize(new THREE.Vector3());
        const center = bbox.getCenter(new THREE.Vector3());

        const maxDim = Math.max(size.x, size.y, size.z);
        if (maxDim > 150) {
          const s = 60.0 / maxDim;
          mapModel.scale.set(s, s, s);
          mapModel.updateMatrixWorld(true);
          bbox.setFromObject(mapModel);
          bbox.getSize(size);
          bbox.getCenter(center);
        } else if (maxDim < 3) {
          const s = 30.0 / maxDim;
          mapModel.scale.set(s, s, s);
          mapModel.updateMatrixWorld(true);
          bbox.setFromObject(mapModel);
          bbox.getSize(size);
          bbox.getCenter(center);
        }

        // Center on X and Z, align base to y=0
        mapModel.position.x = -center.x;
        mapModel.position.y = -bbox.min.y;
        mapModel.position.z = -center.z;

        mapGroupRef.current.add(mapModel);
      } catch (err) {
        console.warn('Fallback to procedural ground for map:', err);
        const ground = AssetLoaderRegistry.createGround();
        mapGroupRef.current.add(ground);
      } finally {
        setIsLoadingMap(false);
      }
    } else {
      const ground = AssetLoaderRegistry.createGround();
      mapGroupRef.current.add(ground);

      const tree = AssetLoaderRegistry.createTree([4, 0, -3]);
      mapGroupRef.current.add(tree);

      const chair = AssetLoaderRegistry.createChair([-4, 0, -2]);
      mapGroupRef.current.add(chair);

      const farm = AssetLoaderRegistry.createFarmPlot([0, 0, -5]);
      mapGroupRef.current.add(farm);
      const crop = farm.getObjectByName('crop');
      if (crop) {
        sceneObjectsRef.current.set('props.farm_plot_01.crop', crop);
      }

      const duck = AssetLoaderRegistry.createDuckProp([0.8, 0, -2.5]);
      mapGroupRef.current.add(duck);

      const lanternStand = AssetLoaderRegistry.createLanternStand([-3.2, 0, -2.0]);
      mapGroupRef.current.add(lanternStand);
    }

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
          sceneObjectsRef.current
        );
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

      // Active Subtitle Update
      const sub = SubtitleSynchronizer.getActiveSubtitle(activeScene, t);
      setActiveSubtitle(sub);

      // Dynamic Sun & Lighting update across timeline
      if (lightingRef.current) {
        lightingRef.current.update(t, activeScene.duration);
      }
    });

    return () => {
      renderer.stop();
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

  const handleImportCustomMap = async (file: File) => {
    if (!rendererRef.current) return;
    setIsLoadingMap(true);
    try {
      const arrayBuffer = await file.arrayBuffer();
      const customModel = await AssetLoaderRegistry.loadGLTFFromBuffer(arrayBuffer);
      customModel.name = 'imported_custom_map';

      // Clear previous map objects
      while (mapGroupRef.current.children.length > 0) {
        const child = mapGroupRef.current.children[0];
        mapGroupRef.current.remove(child);
      }

      // Add ground plane
      const tempFloorGeo = new THREE.PlaneGeometry(80, 80);
      const tempFloorMat = new THREE.MeshStandardMaterial({ color: 0x334455, roughness: 0.8 });
      const tempFloor = new THREE.Mesh(tempFloorGeo, tempFloorMat);
      tempFloor.rotation.x = -Math.PI / 2;
      tempFloor.position.y = -0.01;
      tempFloor.receiveShadow = true;
      mapGroupRef.current.add(tempFloor);

      // High-Intensity Multi-Angle Directional & Ambient Lighting
      const dirLight = new THREE.DirectionalLight(0xfffaed, 3.0);
      dirLight.position.set(20, 35, 20);
      mapGroupRef.current.add(dirLight);

      const hemiLight = new THREE.HemisphereLight(0xddf0ff, 0x554433, 2.0);
      mapGroupRef.current.add(hemiLight);

      const fillLight = new THREE.AmbientLight(0xffffff, 2.0);
      mapGroupRef.current.add(fillLight);

      // Auto-scale to ideal 3D world footprint
      customModel.updateMatrixWorld(true);
      const bbox = new THREE.Box3().setFromObject(customModel);
      const size = bbox.getSize(new THREE.Vector3());
      const center = bbox.getCenter(new THREE.Vector3());

      const maxDim = Math.max(size.x, size.y, size.z);
      if (maxDim > 150) {
        const s = 60.0 / maxDim;
        customModel.scale.set(s, s, s);
        customModel.updateMatrixWorld(true);
        bbox.setFromObject(customModel);
        bbox.getSize(size);
        bbox.getCenter(center);
      } else if (maxDim < 3) {
        const s = 30.0 / maxDim;
        customModel.scale.set(s, s, s);
        customModel.updateMatrixWorld(true);
        bbox.setFromObject(customModel);
        bbox.getSize(size);
        bbox.getCenter(center);
      }

      customModel.position.x = -center.x;
      customModel.position.y = -bbox.min.y;
      customModel.position.z = -center.z;

      mapGroupRef.current.add(customModel);

      // Update scene config
      const updatedScene: MasterSceneConfig = {
        ...sceneRef.current,
        title: `🗺️ Map Tùy Chỉnh (${file.name})`,
        environment: {
          ...sceneRef.current.environment,
          map: file.name,
          weather: {
            ...sceneRef.current.environment.weather,
            fog: 0.000,
          },
        },
      };
      sceneRef.current = updatedScene;
      setScene(updatedScene);
    } catch (e) {
      console.error(e);
      alert(`Lỗi khi nạp file map ${file.name}: ${e}`);
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
    />
  );
};
