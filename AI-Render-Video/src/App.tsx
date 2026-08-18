import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { MasterSceneConfig, DialogueManifestItem } from './types/scene';
import { villageClashScene } from './samples/villageClashScene';
import { ThreeRenderer } from './core/engine/ThreeRenderer';
import { SceneLighting } from './core/engine/SceneLighting';
import { PostProcessor } from './core/engine/PostProcessor';
import { CombatVFXTrigger } from './core/combat/CombatVFXTrigger';
import { CombatSyncEngine } from './core/combat/CombatSyncEngine';
import { NavMeshManager } from './core/navigation/NavMeshManager';
import { PathNavigator } from './core/navigation/PathNavigator';
import { CameraDirector } from './core/camera/CameraDirector';
import { InspectCameraAngle, ActorVisualState } from './core/camera/CameraFraming';
import { OcclusionFoliageManager } from './core/camera/OcclusionFoliageManager';
import { TrackEvaluator, ActorRuntime } from './core/timeline/TrackEvaluator';
import { MasterClock } from './core/timeline/MasterClock';
import { VRMAvatar } from './core/actors/VRMAvatar';
import { ActorAnimator } from './core/actors/ActorAnimator';
import { ActorMorphController } from './core/actors/ActorMorphController';
import { ActorLipSync } from './core/actors/ActorLipSync';
import { ActorLookAt } from './core/actors/ActorLookAt';
import { AssetLoaderRegistry } from './core/assets/AssetLoaderRegistry';
import { SubtitleSynchronizer, ActiveSubtitle } from './core/subtitles/SubtitleSynchronizer';
import { WebCodecsRecorder } from './core/export/WebCodecsRecorder';
import { VideoMuxer } from './core/export/VideoMuxer';
import { StudioLayout } from './ui/StudioLayout';
import './styles/studio.css';

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

    // Build 3D Environment (Ground, Trees, Chair, Farm plot)
    const ground = AssetLoaderRegistry.createGround();
    renderer.scene.add(ground);

    const tree = AssetLoaderRegistry.createTree([4, 0, -3]);
    renderer.scene.add(tree);

    const chair = AssetLoaderRegistry.createChair([-4, 0, -2]);
    renderer.scene.add(chair);

    const farm = AssetLoaderRegistry.createFarmPlot([0, 0, -5]);
    renderer.scene.add(farm);
    const crop = farm.getObjectByName('crop');
    if (crop) {
      sceneObjectsRef.current.set('props.farm_plot_01.crop', crop);
    }

    // Register foliage for Genshin-style X-Ray occlusion transparency
    occlusionFoliageRef.current.registerSceneFoliage(renderer.scene);

    // Build Actors
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

      // Collect actor visual states & head positions for camera & foliage
      const actorStates = new Map<string, ActorVisualState>();
      const targetPositions: THREE.Vector3[] = [];

      for (const [id, runtime] of actorsMapRef.current.entries()) {
        const p = new THREE.Vector3();
        runtime.avatar.rootObject.getWorldPosition(p);
        const head = runtime.avatar.getHeadPosition();
        const rotY = runtime.avatar.rootObject.rotation.y;

        actorStates.set(id, { position: p, headPosition: head, rotationY: rotY });
        targetPositions.push(head);
      }

      // Camera Director Update
      if (cameraDirectorRef.current) {
        cameraDirectorRef.current.update(activeScene, t, actorStates, delta);
      }

      // Occlusion Transparency (Genshin Impact foliage X-Ray)
      if (occlusionFoliageRef.current) {
        occlusionFoliageRef.current.update(renderer.camera, targetPositions, delta);
      }

      // Screen Shake Post-processing
      if (postProcessorRef.current) {
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
      initActors(newScene, rendererRef.current.scene);
      if (occlusionFoliageRef.current) {
        occlusionFoliageRef.current.registerSceneFoliage(rendererRef.current.scene);
      }
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
      onUpdateScene={handleUpdateScene}
      onExportVideo={handleExportVideo}
      isExporting={isExporting}
      exportProgressMsg={exportProgressMsg}
    />
  );
};
