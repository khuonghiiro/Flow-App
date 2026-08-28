import React, { useState, useEffect, useRef, useMemo } from 'react';
import * as THREE from 'three';
import {
  Wrench,
  Shirt,
  Map as MapIcon,
  Film,
  X,
  CheckCircle,
} from 'lucide-react';
import { MasterSceneConfig, CharacterAssembly, PartMaterialCustomization } from '../types/scene';
import { AutoRigEngine, AutoRigResult } from '../core/actors/AutoRigEngine';
import { AssetLoaderRegistry } from '../core/assets/AssetLoaderRegistry';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { ModularOutfitVerticalTabs } from './ModularOutfitVerticalTabs';
import { MapDesignerPanel } from './MapDesignerPanel';
import { FaceSliderConfig, DEFAULT_FACE_SLIDERS, fetchLiveCharacterCategories, CharacterCategory } from './CharacterAssetRegistry';
import { Interactive3DPartSelector, SelectedPartInfo } from './workbench/Interactive3DPartSelector';
import { AvailablePartItem } from './character/PartMaterialPanel';
import { MaterialOverrideEngine } from '../core/materials/MaterialOverrideEngine';
import { applySlidersToModelGroup } from './character/ModelSliderApplier';
import { Character3DViewport } from './character/Character3DViewport';
import { CharacterAnimationTab } from './character/CharacterAnimationTab';
import { CharacterAutoRigTab } from './character/CharacterAutoRigTab';
import { FacialBlinkEngine } from '../core/actors/FacialBlinkEngine';

export { applySlidersToModelGroup };

interface CharacterWorkbenchPanelProps {
  scene: MasterSceneConfig;
  onUpdateScene: (updatedScene: MasterSceneConfig) => void;
  onSelectAvatar?: (actorId: string, vrmUrl: string) => void;
  onSelectMap?: (mapId: string) => void;
  onClose?: () => void;
  isModal?: boolean;
}

export const CharacterWorkbenchPanel: React.FC<CharacterWorkbenchPanelProps> = ({
  scene,
  onUpdateScene,
  onSelectAvatar,
  onSelectMap,
  onClose,
  isModal = false,
}) => {
  const [activeTab, setActiveTab] = useState<'modular' | 'rigging' | 'animation' | 'map'>('modular');
  const [isPreviewLoading, setIsPreviewLoading] = useState<boolean>(false);
  const [availableCategories, setAvailableCategories] = useState<CharacterCategory[]>([]);

  const firstActor = scene.actors[0];
  const firstAss = firstActor?.assembly || firstActor?.profile?.assembly || {
    than_co_ban: firstActor?.model || '',
    base_body: firstActor?.model || '',
  };

  // --- 1. MODULAR OUTFIT STATE ---
  const [assembly, setAssembly] = useState<CharacterAssembly>(() => ({ ...firstAss }));
  const [sceneReadyToken, setSceneReadyToken] = useState<number>(0);

  // --- 2. AUTO-RIG & SKELETAL ANIMATION STATE ---
  const [modelToRig, setModelToRig] = useState<string>(firstActor?.model || '');
  const [isRigged, setIsRigged] = useState<boolean>(false);
  const [isRiggingLoading, setIsRiggingLoading] = useState<boolean>(false);
  const [showJoints, setShowJoints] = useState<boolean>(true);
  const [activePose, setActivePose] = useState<string>('walk');
  const [isPosePlaying, setIsPosePlaying] = useState<boolean>(false);
  const [poseProgress, setPoseProgress] = useState<number>(0);

  // Animation Mixer & Native Skeleton State
  const animationMixerRef = useRef<THREE.AnimationMixer | null>(null);
  const currentActionRef = useRef<THREE.AnimationAction | null>(null);
  const skeletonHelperRef = useRef<THREE.SkeletonHelper | null>(null);
  const nativeRigResultRef = useRef<AutoRigResult | null>(null);

  const [availableAnimations, setAvailableAnimations] = useState<string[]>([]);
  const [selectedAnimClip, setSelectedAnimClip] = useState<string>('');
  const [isPlayingAnim, setIsPlayingAnim] = useState<boolean>(true);
  const [animSpeed, setAnimSpeed] = useState<number>(1.0);
  const [showSkeletonHelper, setShowSkeletonHelper] = useState<boolean>(false);
  const [hasBones, setHasBones] = useState<boolean>(false);
  const [totalBonesCount, setTotalBonesCount] = useState<number>(0);

  // Live Refs to prevent stale closure in 60 FPS requestAnimationFrame loop
  const activeTabRef = useRef(activeTab);
  activeTabRef.current = activeTab;

  const isRiggedRef = useRef(isRigged);
  isRiggedRef.current = isRigged;

  const isPosePlayingRef = useRef(isPosePlaying);
  isPosePlayingRef.current = isPosePlaying;

  const isPlayingAnimRef = useRef(isPlayingAnim);
  isPlayingAnimRef.current = isPlayingAnim;

  const activePoseRef = useRef(activePose);
  activePoseRef.current = activePose;

  const animSpeedRef = useRef(animSpeed);
  animSpeedRef.current = animSpeed;

  const showSkeletonHelperRef = useRef(showSkeletonHelper);
  showSkeletonHelperRef.current = showSkeletonHelper;

  // --- 3. MAP BUILDER STATE ---
  const [selectedMapPath, setSelectedMapPath] = useState<string>((scene.environment?.map || 'assets/ban_do/cathedral.glb').replace(/^assets\/maps\//, 'assets/ban_do/'));
  const [selectedSkyTime, setSelectedSkyTime] = useState<string>(scene.environment?.sky_time || 'noon');
  const [isAppliedSuccess, setIsAppliedSuccess] = useState<boolean>(false);

  // Three.js Preview Canvas Refs
  const canvasContainerRef = useRef<HTMLDivElement>(null);
  const previewSceneRef = useRef<THREE.Scene | null>(null);
  const previewRendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const previewCameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const previewControlsRef = useRef<OrbitControls | null>(null);
  const rigResultRef = useRef<AutoRigResult | null>(null);
  const currentPreviewGroupRef = useRef<THREE.Group | null>(null);
  const animFrameRef = useRef<number | null>(null);
  const floorGridRef = useRef<THREE.GridHelper | null>(null);

  const [showFloorGrid, setShowFloorGrid] = useState<boolean>(true);
  const [showWireframe, setShowWireframe] = useState<boolean>(false);

  // 3D Viewport Interaction Mode: 'orbit' vs 'select'
  const [viewportMode, setViewportMode] = useState<'orbit' | 'select'>('orbit');
  const [selectedPartInfo, setSelectedPartInfo] = useState<SelectedPartInfo | null>(null);
  const partSelectorRef = useRef<Interactive3DPartSelector>(new Interactive3DPartSelector());
  const [availableParts, setAvailableParts] = useState<AvailablePartItem[]>([]);

  // Facial slider state
  const [faceSliders, setFaceSliders] = useState<FaceSliderConfig>(() => {
    try {
      const cached = localStorage.getItem('flow_character_face_sliders');
      if (cached) return { ...DEFAULT_FACE_SLIDERS, ...JSON.parse(cached) };
    } catch {}
    return { ...DEFAULT_FACE_SLIDERS };
  });

  const handleFaceSlidersChange = (updated: FaceSliderConfig) => {
    setFaceSliders(updated);
    try {
      localStorage.setItem('flow_character_face_sliders', JSON.stringify(updated));
    } catch {}
  };

  useEffect(() => {
    fetchLiveCharacterCategories().then((cats) => {
      setAvailableCategories(cats);
      const bodies = cats.find((c) => c.id === 'than_co_ban')?.items || [];
      if (bodies.length > 0) {
        setModelToRig((prev) => prev || bodies[0].path);
        setAssembly((prev) => {
          if (!prev.than_co_ban && !prev.base_body) {
            return { ...prev, than_co_ban: bodies[0].path, base_body: bodies[0].path };
          }
          return prev;
        });
      }
    });
  }, []);

  // Sync Floor Grid & Controls
  useEffect(() => {
    if (floorGridRef.current) floorGridRef.current.visible = showFloorGrid;
  }, [showFloorGrid]);

  useEffect(() => {
    if (skeletonHelperRef.current) skeletonHelperRef.current.visible = showSkeletonHelper;
  }, [showSkeletonHelper]);

  useEffect(() => {
    if (previewControlsRef.current) previewControlsRef.current.enabled = viewportMode === 'orbit';
  }, [viewportMode]);

  // Switch animation clip helper
  const handleSelectAnimationClip = (clipName: string) => {
    setSelectedAnimClip(clipName);
    // Stop procedural pose when switching to embedded animation
    setIsPosePlaying(false);
    if (!currentPreviewGroupRef.current || !animationMixerRef.current) return;
    const group = currentPreviewGroupRef.current;
    const allClips: THREE.AnimationClip[] = [];
    if (group.animations) allClips.push(...group.animations);
    group.traverse((c) => {
      if (c.animations) allClips.push(...c.animations);
    });
    const clip = allClips.find((c) => (c.name || '') === clipName) || allClips[0];
    if (clip && animationMixerRef.current) {
      const prevAction = currentActionRef.current;
      const nextAction = animationMixerRef.current.clipAction(clip);
      nextAction.reset().setEffectiveTimeScale(1).setEffectiveWeight(1).play();
      // Apply smooth crossfade transition if there was a previous animation playing
      if (prevAction && prevAction !== nextAction) {
        nextAction.crossFadeFrom(prevAction, 0.4, true);
      }
      currentActionRef.current = nextAction;
      setIsPlayingAnim(true);
    }
  };

  const handleTogglePlayPause = () => {
    if (availableAnimations.length > 0) {
      if (isPlayingAnim) {
        if (currentActionRef.current) currentActionRef.current.paused = true;
        setIsPlayingAnim(false);
      } else {
        if (currentActionRef.current) {
          currentActionRef.current.paused = false;
          currentActionRef.current.play();
        }
        setIsPlayingAnim(true);
      }
    } else {
      setIsPosePlaying((prev) => !prev);
    }
  };

  // Canvas Click Handler
  const handleCanvasClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (viewportMode !== 'select' || !canvasContainerRef.current || !previewCameraRef.current || !previewSceneRef.current) return;
    const hit = partSelectorRef.current.hitTest(
      e,
      canvasContainerRef.current,
      previewCameraRef.current,
      previewSceneRef.current
    );
    if (hit) {
      setSelectedPartInfo(hit);
      partSelectorRef.current.attachHighlight(hit.mesh, previewSceneRef.current);
    } else {
      setSelectedPartInfo(null);
      partSelectorRef.current.removeHighlight(previewSceneRef.current);
    }
  };

  // Material Override Application
  const handleApplyMaterialOverride = (meshKey: string, override: PartMaterialCustomization) => {
    const nextOverrides = { ...(assembly.material_overrides || {}), [meshKey]: override };
    const nextAss = { ...assembly, material_overrides: nextOverrides };
    setAssembly(nextAss);
    MaterialOverrideEngine.applyMaterialOverrides(previewSceneRef.current, nextOverrides);
  };

  const handleResetMaterialOverride = (meshKey: string) => {
    const nextOverrides = { ...(assembly.material_overrides || {}) };
    delete nextOverrides[meshKey];
    const nextAss = { ...assembly, material_overrides: nextOverrides };
    setAssembly(nextAss);
    MaterialOverrideEngine.applyMaterialOverrides(previewSceneRef.current, nextOverrides);
  };

  // Collect mesh parts from 3D scene
  useEffect(() => {
    if (!previewSceneRef.current) return;
    const parts: AvailablePartItem[] = [];
    const seen = new Set<string>();
    previewSceneRef.current.traverse((c) => {
      if ((c as THREE.Mesh).isMesh) {
        const mesh = c as THREE.Mesh;
        const rawKey = mesh.name || mesh.parent?.name || '';
        if (!rawKey || seen.has(rawKey) || (mesh as any).isGridHelper || (mesh as any).isBoxHelper || (mesh as any).isLine) return;
        seen.add(rawKey);
        const friendly = Interactive3DPartSelector.getPartFriendlyInfo(mesh);
        parts.push({ key: rawKey, name: friendly.displayName, categoryLabel: friendly.categoryLabel, categoryIcon: friendly.categoryIcon });
      }
    });
    setAvailableParts(parts);
  }, [assembly, isPreviewLoading]);

  const handleSelectPartKey = (partKey: string) => {
    if (!previewSceneRef.current) return;
    let foundMesh: THREE.Mesh | null = null;
    previewSceneRef.current.traverse((c) => {
      if (!foundMesh && (c as THREE.Mesh).isMesh) {
        const mesh = c as THREE.Mesh;
        if (mesh.name === partKey || mesh.parent?.name === partKey) foundMesh = mesh;
      }
    });
    if (foundMesh) {
      const friendly = Interactive3DPartSelector.getPartFriendlyInfo(foundMesh);
      const override = (assembly.material_overrides || {})[partKey] || {};
      setSelectedPartInfo({
        mesh: foundMesh,
        meshKey: partKey,
        displayName: friendly.displayName,
        categoryLabel: friendly.categoryLabel,
        categoryIcon: friendly.categoryIcon,
        initialColor: override.color || '#ffffff',
        initialRoughness: override.roughness ?? 0.6,
        initialMetalness: override.metalness ?? 0.05,
        initialEmissive: override.emissive || '#000000',
        initialEmissiveIntensity: override.emissiveIntensity ?? 0.0,
        initialWireframe: override.wireframe ?? false,
        initialVisible: override.visible ?? true,
      });
      partSelectorRef.current.attachHighlight(foundMesh, previewSceneRef.current);
    }
  };

  // Sync Sliders & Wireframe
  useEffect(() => {
    if (previewSceneRef.current) {
      applySlidersToModelGroup(previewSceneRef.current, faceSliders, showWireframe);
      MaterialOverrideEngine.applyMaterialOverrides(previewSceneRef.current, assembly.material_overrides);
    }
  }, [showWireframe, faceSliders, assembly.material_overrides]);

  // Three.js Canvas Init
  useEffect(() => {
    if (!canvasContainerRef.current) return;
    const width = Math.max(320, canvasContainerRef.current.clientWidth || 420);
    const height = Math.max(240, canvasContainerRef.current.clientHeight || 360);

    const previewScene = new THREE.Scene();
    previewScene.background = new THREE.Color(0x0a0f1d);
    previewScene.fog = null;
    previewSceneRef.current = previewScene;

    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 100);
    camera.position.set(0, 1.15, 2.6);
    camera.lookAt(0, 0.85, 0);
    previewCameraRef.current = camera;

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true, powerPreference: 'high-performance' });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.15;

    canvasContainerRef.current.innerHTML = '';
    canvasContainerRef.current.appendChild(renderer.domElement);
    previewRendererRef.current = renderer;

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 0.85, 0);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 0.6;
    controls.maxDistance = 8.0;
    controls.maxPolarAngle = Math.PI / 2 + 0.05;
    previewControlsRef.current = controls;

    // Lighting
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.2);
    const hemiLight = new THREE.HemisphereLight(0xffffff, 0x334155, 1.1);
    hemiLight.position.set(0, 10, 0);
    const keyLight = new THREE.DirectionalLight(0xfffbeb, 1.9);
    keyLight.position.set(3, 5, 4);
    keyLight.castShadow = true;
    const fillLight = new THREE.DirectionalLight(0xbae6fd, 1.2);
    fillLight.position.set(-3, 3, 3);
    const rimLight = new THREE.DirectionalLight(0xf1f5f9, 0.9);
    rimLight.position.set(0, 4, -4);
    const groundBounceLight = new THREE.DirectionalLight(0x475569, 0.5);
    groundBounceLight.position.set(0, -3, 2);

    previewScene.add(ambientLight, hemiLight, keyLight, fillLight, rimLight, groundBounceLight);

    const grid = new THREE.GridHelper(6, 12, 0x38bdf8, 0x1e293b);
    grid.position.y = 0;
    grid.visible = showFloorGrid;
    floorGridRef.current = grid;
    previewScene.add(grid);

    // Animation Loop
    const clock = new THREE.Clock();
    let currentProgress = 0;

    const animate = () => {
      animFrameRef.current = requestAnimationFrame(animate);
      const now = performance.now();
      let delta = clock.getDelta();
      // Cap delta at 0.1s to prevent huge jumps if thread is blocked or tab is inactive
      if (delta > 0.1) delta = 0.1;

      controls.update();

      const speed = animSpeedRef.current;
      const isPlayingClip = isPlayingAnimRef.current;
      const isPlayingMotion = isPosePlayingRef.current;
      const curTab = activeTabRef.current;
      const rigged = isRiggedRef.current;
      const pose = activePoseRef.current;

      if (animationMixerRef.current && isPlayingClip) {
        animationMixerRef.current.update(delta * speed);
      }

      const activeRig = curTab === 'rigging' && rigged ? rigResultRef.current : nativeRigResultRef.current;
      if (activeRig && isPlayingMotion && pose !== 't_pose') {
        currentProgress = (currentProgress + delta * 0.8 * speed) % 1.0;
        const barEl = document.getElementById('character-anim-progress-bar');
        const textEl = document.getElementById('character-anim-progress-text');
        if (barEl) barEl.style.width = `${Math.round(currentProgress * 100)}%`;
        if (textEl) textEl.textContent = `${Math.round(currentProgress * 100)}%`;
        AutoRigEngine.applyTestPose(activeRig.bonesMap, pose, currentProgress);
      }

      // Natural eye blinking & facial dynamics update
      if (currentPreviewGroupRef.current) {
        FacialBlinkEngine.update(currentPreviewGroupRef.current, now / 1000);
      }

      renderer.render(previewScene, camera);
    };
    animate();

    const raycaster = new THREE.Raycaster();
    const mouse = new THREE.Vector2();
    const onPointerMove = (e: MouseEvent) => {
      if (!canvasContainerRef.current) return;
      const rect = canvasContainerRef.current.getBoundingClientRect();
      mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
    };
    const onWheel = () => {
      if (!camera || !controls || !previewScene) return;
      raycaster.setFromCamera(mouse, camera);
      const hits = raycaster.intersectObjects(previewScene.children, true).filter((h) => (h.object as THREE.Mesh).isMesh && h.object.visible && !(h.object as any).isGridHelper && !(h.object as any).isLine);
      if (hits.length > 0) controls.target.lerp(hits[0].point, 0.28);
    };

    const domEl = renderer.domElement;
    domEl.addEventListener('pointermove', onPointerMove as any);
    domEl.addEventListener('wheel', onWheel, { passive: true });

    const resizeObserver = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const w = entry.contentRect.width, h = entry.contentRect.height;
        if (w > 0 && h > 0 && renderer && camera) {
          camera.aspect = w / h;
          camera.updateProjectionMatrix();
          renderer.setSize(w, h);
        }
      }
    });
    resizeObserver.observe(canvasContainerRef.current);
    setSceneReadyToken((t) => t + 1);

    return () => {
      domEl.removeEventListener('pointermove', onPointerMove as any);
      domEl.removeEventListener('wheel', onWheel);
      resizeObserver.disconnect();
      if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
      controls.dispose();
      renderer.dispose();
      previewRendererRef.current = null;
      previewSceneRef.current = null;
      previewCameraRef.current = null;
      previewControlsRef.current = null;
    };
  }, []);

  const assetPartsSignature = useMemo(() => {
    if (!assembly || typeof assembly !== 'object') return '';
    return Object.entries(assembly)
      .filter(([k]) => k !== 'material_overrides' && k !== 'sliders' && k !== 'skin_color' && k !== 'hair_color' && k !== 'eye_color')
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([k, v]) => `${k}:${Array.isArray(v) ? v.join(',') : v}`)
      .join('|');
  }, [assembly]);

  // Update 3D Preview when Modular Selection or Animation Model changes
  useEffect(() => {
    if (!previewSceneRef.current || (activeTab !== 'modular' && activeTab !== 'animation' && activeTab !== 'rigging')) return;
    let isMounted = true;
    const previewScene = previewSceneRef.current;
    setIsPreviewLoading(true);

    if (currentPreviewGroupRef.current) {
      previewScene.remove(currentPreviewGroupRef.current);
      currentPreviewGroupRef.current = null;
    }

    const group = new THREE.Group();
    group.name = 'Preview_Modular_Group';
    currentPreviewGroupRef.current = group;
    previewScene.add(group);

    const partsToLoad: { key: string; path: string }[] = [];
    if (assembly && typeof assembly === 'object') {
      for (const [key, val] of Object.entries(assembly)) {
        if (key === 'sliders' || key === 'skin_color' || key === 'hair_color' || key === 'eye_color' || key === 'material_overrides') continue;
        if (key === 'base_body' && assembly.than_co_ban) continue;
        if (key === 'costume' && assembly.trang_phuc) continue;
        if (key === 'face' && assembly.khuon_mat) continue;
        if (key === 'hairstyle' && assembly.kieu_toc) continue;
        if (key === 'beard' && assembly.kieu_rau) continue;
        if (key === 'shoes' && assembly.giay_dep) continue;
        if (key === 'hat' && assembly.mu_non) continue;
        if (key === 'eyebrow' && assembly.long_may) continue;
        if (key === 'eye' && assembly.mat) continue;
        if (key === 'nose' && assembly.mui) continue;
        if (key === 'mouth' && assembly.mieng) continue;

        if (Array.isArray(val)) {
          val.forEach((p, idx) => {
            if (typeof p === 'string' && p.trim()) partsToLoad.push({ key: `${key}_${idx}`, path: p });
          });
        } else if (typeof val === 'string' && val.trim()) {
          partsToLoad.push({ key, path: val });
        }
      }
    }

    if (!partsToLoad.some((p) => p.key === 'than_co_ban' || p.key === 'base_body' || p.key === 'body')) {
      const bodyPath = firstActor?.model || availableCategories.find((c) => c.id === 'than_co_ban')?.items[0]?.path || '';
      if (bodyPath) partsToLoad.unshift({ key: 'than_co_ban', path: bodyPath });
    }

    if (partsToLoad.length === 0) {
      setIsPreviewLoading(false);
      return;
    }

    Promise.all(
      partsToLoad.map(async ({ key, path }) => {
        try {
          const model = await AssetLoaderRegistry.loadCharacterPart(path);
          return { key, path, model };
        } catch (err) {
          console.warn(`[Workbench] Không thể tải bộ phận ${key} (${path}):`, err);
          return null;
        }
      })
    )
      .then((results) => {
        if (!isMounted) return;
        const loadedList = results.filter((item): item is { key: string; path: string; model: THREE.Group } => item !== null);
        if (loadedList.length === 0) { setIsPreviewLoading(false); return; }

        // Face attachment auto-snap
        const faceItem = loadedList.find((item) => item.key === 'khuon_mat' || item.key === 'face');
        const hasFace = Boolean(faceItem);
        if (faceItem) {
          const bodyItem = loadedList.find((item) => item.key === 'than_co_ban' || item.key === 'base_body' || item.key === 'body');
          let bodyFaceMesh: THREE.Mesh | null = null;
          if (bodyItem) {
            bodyItem.model.traverse((c) => {
              if ((c as THREE.Mesh).isMesh) {
                const n = c.name.toLowerCase(), pn = (c.parent?.name || '').toLowerCase();
                if (n.includes('face') || n.includes('head') || pn.includes('face') || pn.includes('head')) {
                  if (!bodyFaceMesh) bodyFaceMesh = c as THREE.Mesh;
                }
              }
            });
          }
          let addonFaceMesh: THREE.Mesh | null = null;
          faceItem.model.traverse((c) => {
            if ((c as THREE.Mesh).isMesh && !addonFaceMesh) addonFaceMesh = c as THREE.Mesh;
          });
          if (bodyFaceMesh && addonFaceMesh) {
            const bodyFaceBox = new THREE.Box3().setFromObject(bodyFaceMesh);
            const bodyFaceCenter = new THREE.Vector3();
            bodyFaceBox.getCenter(bodyFaceCenter);
            const faceBox = new THREE.Box3().setFromObject(addonFaceMesh);
            const faceCenter = new THREE.Vector3();
            faceBox.getCenter(faceCenter);
            const delta = bodyFaceCenter.clone().sub(faceCenter);
            faceItem.model.position.add(delta);
            faceItem.model.updateMatrixWorld(true);
          }
        }

        loadedList.forEach(({ key, model }) => {
          model.traverse((c) => {
            if ((c as THREE.Mesh).isMesh) {
              const mesh = c as THREE.Mesh;
              mesh.castShadow = true;
              mesh.receiveShadow = true;
              const n = mesh.name.toLowerCase(), pn = (mesh.parent?.name || '').toLowerCase();
              const matName = Array.isArray(mesh.material) ? mesh.material.map((m) => m.name.toLowerCase()).join(' ') : (mesh.material?.name || '').toLowerCase();
              const isFaceDetail = n.includes('face') || pn.includes('face') || matName.includes('face') || n.includes('pupil') || pn.includes('pupil') || matName.includes('pupil') || n.includes('eyebrow') || pn.includes('eyebrow') || matName.includes('eyebrow');
              if (hasFace && (key === 'than_co_ban' || key === 'base_body' || key === 'body') && isFaceDetail) mesh.visible = false;

              if (mesh.material) {
                const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
                mats.forEach((mat) => {
                  const m = mat as any;
                  m.depthTest = true;
                  m.depthWrite = true;
                  m.side = THREE.DoubleSide;
                  if (m.color && m.color.r === 0 && m.color.g === 0 && m.color.b === 0) m.color.setHex(0xffffff);
                  if (m.map) {
                    m.map.colorSpace = THREE.SRGBColorSpace;
                    m.map.minFilter = THREE.LinearMipmapLinearFilter;
                    m.map.magFilter = THREE.LinearFilter;
                    m.anisotropy = 16;
                    m.map.needsUpdate = true;
                  }
                  m.needsUpdate = true;
                });
              }
            }
          });
          group.add(model);
        });

        // 1. Calculate bounding box and normalize scale to 1.7m standard human height
        group.updateMatrixWorld(true);
        const bbox = new THREE.Box3().setFromObject(group);
        if (!bbox.isEmpty()) {
          const size = new THREE.Vector3();
          bbox.getSize(size);
          const modelHeight = size.y;
          if (modelHeight > 0.01) {
            const targetHeight = 1.7; // Standard 1.7m Humanoid Studio Height
            const scaleFactor = targetHeight / modelHeight;
            group.scale.set(scaleFactor, scaleFactor, scaleFactor);
            group.updateMatrixWorld(true);
          }

          // 2. Center X, Z and align feet directly on the ground (Y = 0)
          const scaledBbox = new THREE.Box3().setFromObject(group);
          const scaledCenter = new THREE.Vector3();
          scaledBbox.getCenter(scaledCenter);
          group.position.set(-scaledCenter.x, -scaledBbox.min.y, -scaledCenter.z);
          group.updateMatrixWorld(true);
        }

        // 3. Reset OrbitControls camera target to chest height
        if (previewControlsRef.current) {
          previewControlsRef.current.target.set(0, 0.85, 0);
          previewControlsRef.current.update();
        }

        applySlidersToModelGroup(group, faceSliders, showWireframe);
        MaterialOverrideEngine.applyMaterialOverrides(group, assembly?.material_overrides);

        // Detect Native Skeleton Bones (Columbina 743 joints)
        let bCount = 0;
        group.traverse((c) => {
          if ((c as THREE.Bone).isBone) bCount++;
        });
        setHasBones(bCount > 0);
        setTotalBonesCount(bCount);

        if (bCount > 0) {
          nativeRigResultRef.current = AutoRigEngine.rigModel(group);
          if (skeletonHelperRef.current && previewScene) {
            previewScene.remove(skeletonHelperRef.current);
            skeletonHelperRef.current = null;
          }
          const helper = new THREE.SkeletonHelper(group);
          const mat = (helper as any).material;
          if (mat) { mat.depthTest = false; mat.transparent = true; mat.opacity = 0.85; mat.linewidth = 2; }
          helper.visible = showSkeletonHelper;
          skeletonHelperRef.current = helper;
          previewScene.add(helper);
        } else {
          nativeRigResultRef.current = null;
          if (skeletonHelperRef.current && previewScene) {
            previewScene.remove(skeletonHelperRef.current);
            skeletonHelperRef.current = null;
          }
        }

        // Detect Embedded Animation Clips & Force Smooth Interpolation
        const allClips: THREE.AnimationClip[] = [];
        
        // Fix for "stiff" animations: Force linear interpolation instead of discrete/step
        const ensureSmoothInterpolation = (clip: THREE.AnimationClip) => {
          clip.tracks.forEach((track) => {
            // Override discrete step interpolation which causes robotic/rigid movements
            track.setInterpolation(THREE.InterpolateLinear);
          });
          return clip;
        };

        if (group.animations && group.animations.length > 0) {
          allClips.push(...group.animations.map(ensureSmoothInterpolation));
        }
        
        group.traverse((c) => {
          if (c.animations && c.animations.length > 0) {
            c.animations.forEach((a) => {
              if (!allClips.some((existing) => existing.name === a.name)) {
                allClips.push(ensureSmoothInterpolation(a));
              }
            });
          }
        });

        if (animationMixerRef.current) {
          animationMixerRef.current.stopAllAction();
          animationMixerRef.current = null;
          currentActionRef.current = null;
        }

        if (allClips.length > 0) {
          const mixer = new THREE.AnimationMixer(group);
          animationMixerRef.current = mixer;
          const clipNames = allClips.map((c, i) => c.name || `Animation_${i + 1}`);
          setAvailableAnimations(clipNames);
          setSelectedAnimClip(clipNames[0]);
          const action = mixer.clipAction(allClips[0]);
          action.reset().setEffectiveTimeScale(1).setEffectiveWeight(1).play();
          currentActionRef.current = action;
          setIsPlayingAnim(true);
        } else {
          setAvailableAnimations([]);
          setSelectedAnimClip('');
        }

        setIsPreviewLoading(false);
      })
      .catch((err) => {
        console.warn('Lỗi tải preview modular:', err);
        if (isMounted) setIsPreviewLoading(false);
      });

    return () => { isMounted = false; };
  }, [assetPartsSignature, activeTab, sceneReadyToken]);

  // Execute Auto-Rigging on Selected Model
  const handleRunAutoRig = async () => {
    if (!previewSceneRef.current) return;
    setIsRiggingLoading(true);
    try {
      const previewScene = previewSceneRef.current;
      if (currentPreviewGroupRef.current) {
        previewScene.remove(currentPreviewGroupRef.current);
        currentPreviewGroupRef.current = null;
      }
      const rawModel = await AssetLoaderRegistry.loadCharacterPart(modelToRig);
      const rigResult = AutoRigEngine.rigModel(rawModel);
      rigResultRef.current = rigResult;
      currentPreviewGroupRef.current = rigResult.rootGroup;

      // Scale and center rigged model to 1.7m standard human height
      rigResult.rootGroup.updateMatrixWorld(true);
      const bbox = new THREE.Box3().setFromObject(rigResult.rootGroup);
      if (!bbox.isEmpty()) {
        const size = new THREE.Vector3();
        bbox.getSize(size);
        const modelHeight = size.y;
        if (modelHeight > 0.01) {
          const scaleFactor = 1.7 / modelHeight;
          rigResult.rootGroup.scale.set(scaleFactor, scaleFactor, scaleFactor);
          rigResult.rootGroup.updateMatrixWorld(true);
        }
        const scaledBbox = new THREE.Box3().setFromObject(rigResult.rootGroup);
        const scaledCenter = new THREE.Vector3();
        scaledBbox.getCenter(scaledCenter);
        rigResult.rootGroup.position.set(-scaledCenter.x, -scaledBbox.min.y, -scaledCenter.z);
        rigResult.rootGroup.updateMatrixWorld(true);
      }

      previewScene.add(rigResult.rootGroup);
      if (showJoints) previewScene.add(rigResult.jointVisualizer);
      setIsRigged(true);
      setActivePose('t_pose');
    } catch (err) {
      console.error('Lỗi thực hiện Auto-Rig:', err);
      alert('Không thể thực hiện Auto-Rig trên mô hình này. Vui lòng kiểm tra lại định dạng tệp .glb.');
    } finally {
      setIsRiggingLoading(false);
    }
  };

  const handleSelectPose = (pose: string) => {
    setActivePose(pose);
    // Stop embedded animation when switching to procedural pose
    if (animationMixerRef.current && currentActionRef.current) {
      currentActionRef.current.stop();
      setIsPlayingAnim(false);
    }
    setIsPosePlaying(pose !== 't_pose');
    const activeRig = activeTab === 'rigging' && isRigged ? rigResultRef.current : nativeRigResultRef.current;
    if (activeRig) AutoRigEngine.applyTestPose(activeRig.bonesMap, pose, poseProgress);
  };

  const handleApplyMapPreset = () => {
    if (onSelectMap) onSelectMap(selectedMapPath);
    const updatedScene: MasterSceneConfig = {
      ...scene,
      environment: { ...scene.environment, map: selectedMapPath, sky_time: selectedSkyTime as any },
    };
    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  const handleCaptureSnapshot = (): string => {
    if (!previewRendererRef.current) return '';
    try {
      return previewRendererRef.current.domElement.toDataURL('image/png');
    } catch {
      return '';
    }
  };

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(5, 7, 15, 0.85)', backdropFilter: 'blur(8px)' }}>
      <div style={{ width: '96vw', height: '94vh', background: '#0b0f19', border: '1px solid rgba(255, 255, 255, 0.1)', borderRadius: 16, display: 'flex', flexDirection: 'column', overflow: 'hidden', boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.7)' }}>
        {/* TOP BAR */}
        <div style={{ padding: '10px 18px', background: 'rgba(15, 23, 42, 0.8)', borderBottom: '1px solid rgba(255, 255, 255, 0.08)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <div style={{ width: 32, height: 32, borderRadius: 8, background: 'linear-gradient(135deg, #0284c7, #38bdf8)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Wrench size={18} color="#fff" />
              </div>
              <span style={{ fontSize: 15, fontWeight: 800, color: '#f8fafc', letterSpacing: '0.3px' }}>
                XƯỞNG TẠO HÌNH NHÂN VẬT & MÔI TRƯỜNG 3D
              </span>
            </div>

            {/* 4 Studio Tabs */}
            <div style={{ display: 'flex', background: 'rgba(0,0,0,0.3)', padding: 3, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)' }}>
              <button
                onClick={() => setActiveTab('modular')}
                style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: '5px 12px', fontSize: 12, fontWeight: 700, borderRadius: 6,
                  background: activeTab === 'modular' ? '#0284c7' : 'transparent', color: activeTab === 'modular' ? '#fff' : '#94a3b8', border: 'none', cursor: 'pointer',
                }}
              >
                <Shirt size={14} /> Lắp Ráp Modular
              </button>

              <button
                onClick={() => setActiveTab('rigging')}
                style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: '5px 12px', fontSize: 12, fontWeight: 700, borderRadius: 6,
                  background: activeTab === 'rigging' ? '#7c3aed' : 'transparent', color: activeTab === 'rigging' ? '#fff' : '#94a3b8', border: 'none', cursor: 'pointer',
                }}
              >
                <Wrench size={14} /> Auto-Rig (Gắn Xương)
              </button>

              <button
                onClick={() => setActiveTab('animation')}
                style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: '5px 12px', fontSize: 12, fontWeight: 700, borderRadius: 6,
                  background: activeTab === 'animation' ? '#d97706' : 'transparent', color: activeTab === 'animation' ? '#fff' : '#94a3b8', border: 'none', cursor: 'pointer',
                }}
              >
                <Film size={14} /> Animation & Cử Động
              </button>

              <button
                onClick={() => setActiveTab('map')}
                style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: '5px 12px', fontSize: 12, fontWeight: 700, borderRadius: 6,
                  background: activeTab === 'map' ? '#059669' : 'transparent', color: activeTab === 'map' ? '#fff' : '#94a3b8', border: 'none', cursor: 'pointer',
                }}
              >
                <MapIcon size={14} /> Bản Đồ & Thời Gian
              </button>
            </div>
          </div>

          <button onClick={onClose} style={{ background: 'rgba(255,255,255,0.06)', border: 'none', color: '#94a3b8', width: 28, height: 28, borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer' }}>
            <X size={16} />
          </button>
        </div>

        {/* WORKBENCH BODY: 2 Columns */}
        <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
          {/* Column 1: 3D Viewport with Studio Lighting & Animation Loop */}
          <Character3DViewport
            canvasContainerRef={canvasContainerRef}
            previewSceneRef={previewSceneRef}
            previewCameraRef={previewCameraRef}
            previewRendererRef={previewRendererRef}
            previewControlsRef={previewControlsRef}
            floorGridRef={floorGridRef}
            showFloorGrid={showFloorGrid}
            onToggleFloorGrid={() => setShowFloorGrid((prev) => !prev)}
            showWireframe={showWireframe}
            onToggleWireframe={() => setShowWireframe((prev) => !prev)}
            viewportMode={viewportMode}
            onToggleViewportMode={() => setViewportMode((prev) => (prev === 'orbit' ? 'select' : 'orbit'))}
            onCanvasClick={handleCanvasClick}
            isPreviewLoading={isPreviewLoading}
            hasBones={hasBones}
            totalBonesCount={totalBonesCount}
            showSkeletonHelper={showSkeletonHelper}
            onToggleSkeletonHelper={() => setShowSkeletonHelper((prev) => !prev)}
            availableAnimations={availableAnimations}
            selectedAnimClip={selectedAnimClip}
            onSelectAnimationClip={handleSelectAnimationClip}
            activePose={activePose}
            onSelectPose={handleSelectPose}
            isPlayingAnim={isPlayingAnim}
            isPosePlaying={isPosePlaying}
            onTogglePlayPause={handleTogglePlayPause}
            animSpeed={animSpeed}
            onChangeAnimSpeed={setAnimSpeed}
            isRigged={isRigged}
          />

          {/* Column 2: Studio Tab Switching (60% width) */}
          <div style={{ flex: '0 0 60%', width: '60%', maxWidth: '60%', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
            {activeTab === 'modular' && (
              <ModularOutfitVerticalTabs
                scene={scene}
                onUpdateScene={onUpdateScene}
                assembly={assembly}
                onAssemblyChange={(updatedAssembly) => setAssembly(updatedAssembly)}
                sliders={faceSliders}
                onSlidersChange={handleFaceSlidersChange}
                onCaptureSnapshot={handleCaptureSnapshot}
                selectedPartInfo={selectedPartInfo}
                onSelectPartKey={handleSelectPartKey}
                isTouchSelectActive={viewportMode === 'select'}
                availableParts={availableParts}
                onApplyMaterialOverride={handleApplyMaterialOverride}
                onResetMaterialOverride={handleResetMaterialOverride}
              />
            )}

            {activeTab === 'rigging' && (
              <CharacterAutoRigTab
                availableCategories={availableCategories}
                modelToRig={modelToRig}
                onSelectModelToRig={setModelToRig}
                isRigged={isRigged}
                isRiggingLoading={isRiggingLoading}
                onRunAutoRig={handleRunAutoRig}
                showJoints={showJoints}
                onToggleJoints={() => {
                  setShowJoints(!showJoints);
                  if (rigResultRef.current && previewSceneRef.current) {
                    if (!showJoints) previewSceneRef.current.add(rigResultRef.current.jointVisualizer);
                    else previewSceneRef.current.remove(rigResultRef.current.jointVisualizer);
                  }
                }}
                activePose={activePose}
                onSelectPose={handleSelectPose}
                isPosePlaying={isPosePlaying}
                onTogglePosePlay={() => setIsPosePlaying(!isPosePlaying)}
              />
            )}

            {activeTab === 'animation' && (
              <CharacterAnimationTab
                availableCategories={availableCategories}
                availableAnimations={availableAnimations}
                selectedAnimClip={selectedAnimClip}
                onSelectAnimationClip={handleSelectAnimationClip}
                isPlayingAnim={isPlayingAnim}
                onTogglePlayPause={handleTogglePlayPause}
                animSpeed={animSpeed}
                onChangeAnimSpeed={setAnimSpeed}
                activePose={activePose}
                onSelectPose={handleSelectPose}
                isPosePlaying={isPosePlaying}
                poseProgress={poseProgress}
                showSkeletonHelper={showSkeletonHelper}
                onToggleSkeletonHelper={() => setShowSkeletonHelper((prev) => !prev)}
                hasBones={hasBones}
                totalBonesCount={totalBonesCount}
                currentModelPath={assembly?.than_co_ban || assembly?.base_body || modelToRig}
                onSelectModel={(modelPath) => {
                  setAssembly((prev) => ({
                    ...prev,
                    than_co_ban: modelPath,
                    base_body: modelPath,
                  }));
                  setModelToRig(modelPath);
                }}
              />
            )}

            {activeTab === 'map' && (
              <MapDesignerPanel
                scene={scene}
                selectedMapPath={selectedMapPath}
                selectedSkyTime={selectedSkyTime}
                onSelectMapPath={setSelectedMapPath}
                onSelectSkyTime={setSelectedSkyTime}
                onApplyMapPreset={handleApplyMapPreset}
                isAppliedSuccess={isAppliedSuccess}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
