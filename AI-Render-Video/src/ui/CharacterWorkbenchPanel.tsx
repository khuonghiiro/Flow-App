import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import {
  Wrench,
  Shirt,
  Map as MapIcon,
  Play,
  Pause,
  RotateCcw,
  Sparkles,
  Eye,
  EyeOff,
  CheckCircle,
  X,
  Loader,
  Grid,
  Box,
} from 'lucide-react';
import { MasterSceneConfig, CharacterAssembly } from '../types/scene';
import { AutoRigEngine, AutoRigResult } from '../core/actors/AutoRigEngine';
import { AssetLoaderRegistry } from '../core/assets/AssetLoaderRegistry';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { ModularOutfitVerticalTabs } from './ModularOutfitVerticalTabs';
import { MapDesignerPanel } from './MapDesignerPanel';
import { FaceSliderConfig, DEFAULT_FACE_SLIDERS, fetchLiveCharacterCategories, CharacterCategory } from './CharacterAssetRegistry';

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
  const [activeTab, setActiveTab] = useState<'rigging' | 'modular' | 'map'>('modular');
  const [isPreviewLoading, setIsPreviewLoading] = useState<boolean>(false);
  const [availableCategories, setAvailableCategories] = useState<CharacterCategory[]>([]);

  const firstActor = scene.actors[0];
  const firstAss = firstActor?.assembly || firstActor?.profile?.assembly || {
    than_co_ban: firstActor?.model || '',
    base_body: firstActor?.model || '',
  };

  // --- 1. MODULAR OUTFIT STATE (lifted for 3D preview sync) ---
  const [assembly, setAssembly] = useState<CharacterAssembly>(() => ({ ...firstAss }));
  const [sceneReadyToken, setSceneReadyToken] = useState<number>(0);

  // --- 2. AUTO-RIG STATE ---
  const [modelToRig, setModelToRig] = useState<string>(firstActor?.model || '');
  const [isRigged, setIsRigged] = useState<boolean>(false);
  const [isRiggingLoading, setIsRiggingLoading] = useState<boolean>(false);
  const [showJoints, setShowJoints] = useState<boolean>(true);
  const [activePose, setActivePose] = useState<string>('t_pose');
  const [isPosePlaying, setIsPosePlaying] = useState<boolean>(false);
  const [poseProgress, setPoseProgress] = useState<number>(0);

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

  const [showFloorGrid, setShowFloorGrid] = useState<boolean>(true);
  const [showWireframe, setShowWireframe] = useState<boolean>(false);

  // Facial slider state with LocalStorage persistence
  const [faceSliders, setFaceSliders] = useState<FaceSliderConfig>(() => {
    try {
      const cached = localStorage.getItem('flow_character_face_sliders');
      if (cached) return { ...DEFAULT_FACE_SLIDERS, ...JSON.parse(cached) };
    } catch {}
    return { ...DEFAULT_FACE_SLIDERS };
  });
  const { baseFaceOpacity, eyebrowOpacity, pupilOpacity, noseOpacity, mouthOpacity, skinSmoothness, costumeOpacity } = faceSliders;

  const handleFaceSlidersChange = (updated: FaceSliderConfig) => {
    setFaceSliders(updated);
    try {
      localStorage.setItem('flow_character_face_sliders', JSON.stringify(updated));
    } catch {}
  };

  const floorGridRef = useRef<THREE.GridHelper | null>(null);

  // Sync Floor Grid visibility
  useEffect(() => {
    if (floorGridRef.current) {
      floorGridRef.current.visible = showFloorGrid;
    }
  }, [showFloorGrid]);

  // Sync Facial Sliders & Wireframe to 3D Preview Models
  useEffect(() => {
    if (previewSceneRef.current) {
      applySlidersToModelGroup(previewSceneRef.current, faceSliders, showWireframe);
    }
  }, [showWireframe, baseFaceOpacity, eyebrowOpacity, pupilOpacity, noseOpacity, mouthOpacity, skinSmoothness, costumeOpacity]);

  // Khi chọn face mới: Tự động cho mặt cũ, mắt, mũi, miệng, lông mày gốc về 0% để dùng trọn vẹn Face mới
  const handleSelectFace = (newFacePath: string) => {
    const currentFace = assembly.khuon_mat || assembly.face || '';
    const nextFace = currentFace === newFacePath || newFacePath === '' ? '' : newFacePath;
    const nextAss = { ...assembly };
    if (nextFace) {
      nextAss.khuon_mat = nextFace;
      nextAss.face = nextFace;
      const updated = {
        ...faceSliders,
        baseFaceOpacity: 0.0,
        eyebrowOpacity: 0.0,
        pupilOpacity: 0.0,
        noseOpacity: 0.0,
        mouthOpacity: 0.0,
      };
      setFaceSliders(updated);
      try { localStorage.setItem('flow_character_face_sliders', JSON.stringify(updated)); } catch {}
    } else {
      delete nextAss.khuon_mat;
      delete nextAss.face;
      const updated = {
        ...faceSliders,
        baseFaceOpacity: 0.0,
        noseOpacity: 0.0,
        mouthOpacity: 0.0,
      };
      setFaceSliders(updated);
      try { localStorage.setItem('flow_character_face_sliders', JSON.stringify(updated)); } catch {}
    }
    setAssembly(nextAss);
  };

  // Initialize Three.js 3D Preview Canvas
  useEffect(() => {
    if (!canvasContainerRef.current) return;

    const width = Math.max(320, canvasContainerRef.current.clientWidth || 420);
    const height = Math.max(240, canvasContainerRef.current.clientHeight || 360);

    const previewScene = new THREE.Scene();
    previewScene.background = new THREE.Color(0x0a0f1d);
    previewSceneRef.current = previewScene;

    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 100);
    camera.position.set(0, 1.15, 2.6);
    camera.lookAt(0, 0.85, 0);
    previewCameraRef.current = camera;

    const renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: true,
      preserveDrawingBuffer: true,
      powerPreference: 'high-performance',
    });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;

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

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.6);
    previewScene.add(ambientLight);

    const dirLight = new THREE.DirectionalLight(0xffffff, 2.4);
    dirLight.position.set(3, 5, 4);
    dirLight.castShadow = true;
    previewScene.add(dirLight);

    const fillLight = new THREE.DirectionalLight(0x38bdf8, 1.2);
    fillLight.position.set(-3, 3, 2);
    previewScene.add(fillLight);

    const backLight = new THREE.DirectionalLight(0xa855f7, 0.9);
    backLight.position.set(0, 4, -4);
    previewScene.add(backLight);

    // Floor Grid
    const grid = new THREE.GridHelper(6, 12, 0x38bdf8, 0x1e293b);
    grid.position.y = 0;
    grid.visible = showFloorGrid;
    floorGridRef.current = grid;
    previewScene.add(grid);

    // Animation Loop
    let lastTime = performance.now();
    let currentProgress = 0;

    const animate = () => {
      animFrameRef.current = requestAnimationFrame(animate);
      const now = performance.now();
      const delta = (now - lastTime) / 1000;
      lastTime = now;

      controls.update();

      if (isPosePlaying && rigResultRef.current) {
        currentProgress = (currentProgress + delta * 0.8) % 1.0;
        setPoseProgress(currentProgress);
        AutoRigEngine.applyTestPose(rigResultRef.current.bonesMap, activePose, currentProgress);
      }

      renderer.render(previewScene, camera);
    };
    animate();

    // Zoom To Cursor under Mouse Pointer
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
      const hits = raycaster
        .intersectObjects(previewScene.children, true)
        .filter((h) => (h.object as THREE.Mesh).isMesh && h.object.visible && !(h.object as any).isGridHelper && !(h.object as any).isLine);

      if (hits.length > 0) {
        const hitPoint = hits[0].point;
        // Smoothly pull OrbitControls target towards the exact point on the mesh where the cursor is hovering
        controls.target.lerp(hitPoint, 0.28);
      }
    };

    const domEl = renderer.domElement;
    domEl.addEventListener('pointermove', onPointerMove as any);
    domEl.addEventListener('wheel', onWheel, { passive: true });

    const resizeObserver = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const w = entry.contentRect.width;
        const h = entry.contentRect.height;
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
      floorGridRef.current = null;
      currentPreviewGroupRef.current = null;
    };
  }, [activeTab]);

  // Update 3D Preview when Modular Selection changes (with Face Z-Fighting Fix)
  useEffect(() => {
    if (!previewSceneRef.current || activeTab !== 'modular') return;

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
        if (key === 'sliders' || key === 'skin_color' || key === 'hair_color' || key === 'eye_color') continue;
        if (key === 'base_body' && assembly.than_co_ban) continue;
        if (key === 'costume' && assembly.trang_phuc) continue;
        if (key === 'face' && assembly.khuon_mat) continue;
        if (key === 'hairstyle' && assembly.kieu_toc) continue;

        if (typeof val === 'string' && val.trim()) {
          partsToLoad.push({ key, path: val });
        } else if (Array.isArray(val)) {
          val.forEach((p, idx) => {
            if (typeof p === 'string' && p.trim()) {
              partsToLoad.push({ key: `${key}_${idx}`, path: p });
            }
          });
        }
      }
    }

    if (!partsToLoad.some(p => p.key === 'than_co_ban' || p.key === 'base_body' || p.key === 'body')) {
      const bodyPath = firstActor?.model || availableCategories.find(c => c.id === 'than_co_ban')?.items[0]?.path || '';
      if (bodyPath) {
        partsToLoad.unshift({ key: 'than_co_ban', path: bodyPath });
      }
    }

    Promise.all(
      partsToLoad.map(async (p) => {
        try {
          const model = await AssetLoaderRegistry.loadCharacterPart(p.path);
          return { key: p.key, model };
        } catch (err) {
          console.warn(`[Workbench] Không thể tải bộ phận ${p.key} (${p.path}):`, err);
          return null;
        }
      })
    )
      .then((results) => {
        if (!isMounted) return;
        const loadedList = results.filter((item): item is { key: string; model: THREE.Group } => item !== null);

        const hasFace = loadedList.some((item) => item.key === 'khuon_mat' || item.key === 'face' || item.key.includes('face'));
        const bodyItem = loadedList.find((item) => item.key === 'than_co_ban' || item.key === 'base_body' || item.key === 'body');
        const faceItem = loadedList.find((item) => item.key === 'khuon_mat' || item.key === 'face' || item.key.includes('face'));

        // Dynamic Anatomical Snapping: Khớp chính xác vị trí và chiều cao của Face theo Body (Nam/Nữ)
        if (bodyItem && faceItem) {
          bodyItem.model.updateMatrixWorld(true);
          faceItem.model.updateMatrixWorld(true);

          let bodyFaceMesh: THREE.Mesh | null = null;
          bodyItem.model.traverse((c) => {
            if ((c as THREE.Mesh).isMesh && !bodyFaceMesh) {
              const mesh = c as THREE.Mesh;
              const n = mesh.name.toLowerCase();
              const p = (mesh.parent?.name || '').toLowerCase();
              const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
              const m = mats.map((mat: any) => mat?.name?.toLowerCase() || '').join(' ');
              if (n.includes('face') || p.includes('face') || m.includes('face')) {
                bodyFaceMesh = mesh;
              }
            }
          });

          let addonFaceMesh: THREE.Mesh | null = null;
          faceItem.model.traverse((c) => {
            if ((c as THREE.Mesh).isMesh && !addonFaceMesh) {
              addonFaceMesh = c as THREE.Mesh;
            }
          });

          if (bodyFaceMesh && addonFaceMesh) {
            const bodyFaceBox = new THREE.Box3().setFromObject(bodyFaceMesh);
            const bodyFaceCenter = new THREE.Vector3();
            bodyFaceBox.getCenter(bodyFaceCenter);

            const faceBox = new THREE.Box3().setFromObject(addonFaceMesh);
            const faceCenter = new THREE.Vector3();
            faceBox.getCenter(faceCenter);

            // Bù độ lệch chuẩn xác 100% (Delta X, Y, Z) để Face mới khớp khít vào đầu Body
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

              const nodeName = mesh.name.toLowerCase();
              const parentName = (mesh.parent?.name || '').toLowerCase();
              const matName = Array.isArray(mesh.material)
                ? mesh.material.map((m) => m.name.toLowerCase()).join(' ')
                : (mesh.material?.name || '').toLowerCase();

              const isFaceDetail =
                nodeName.includes('face') ||
                parentName.includes('face') ||
                matName.includes('face') ||
                nodeName.includes('pupil') ||
                parentName.includes('pupil') ||
                matName.includes('pupil') ||
                nodeName.includes('eyebrow') ||
                parentName.includes('eyebrow') ||
                matName.includes('eyebrow');

              // Lớp trước đè lớp sau: Khi chọn Face mới, ẩn triệt để mặt và mắt/mày của thân gốc
              if (hasFace && (key === 'than_co_ban' || key === 'base_body' || key === 'body') && isFaceDetail) {
                mesh.visible = false;
              }

              // Smooth texture filtering and crisp clean rendering with FrontSide
              if (mesh.material) {
                const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
                mats.forEach((mat) => {
                  const m = mat as any;
                  m.depthTest = true;
                  m.depthWrite = true;
                  m.side = THREE.FrontSide; // CRITICAL: FrontSide prevents seeing inside the mouth cavity/tongue

                  if (m.map) {
                    m.map.minFilter = THREE.LinearMipmapLinearFilter;
                    m.map.magFilter = THREE.LinearFilter;
                    m.anisotropy = 16;
                  }
                });
              }
            }
          });
          group.add(model);
        });

        // Center group and align feet directly on the ground (Y = 0)
        const bbox = new THREE.Box3().setFromObject(group);
        if (!bbox.isEmpty()) {
          const center = new THREE.Vector3();
          bbox.getCenter(center);
          group.position.set(-center.x, -bbox.min.y, -center.z);
        }

        // Instantly apply face sliders to newly loaded model without needing manual slider drag!
        applySlidersToModelGroup(group, faceSliders, showWireframe);

        setIsPreviewLoading(false);
      })
      .catch((err) => {
        console.warn('Lỗi tải preview modular:', err);
        if (isMounted) setIsPreviewLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [assembly, activeTab, sceneReadyToken]);

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
      previewScene.add(rigResult.rootGroup);

      if (showJoints) {
        previewScene.add(rigResult.jointVisualizer);
      }

      setIsRigged(true);
      setActivePose('t_pose');
    } catch (err) {
      console.error('Lỗi thực hiện Auto-Rig:', err);
      alert('Không thể thực hiện Auto-Rig trên mô hình này. Vui lòng kiểm tra lại định dạng tệp .glb.');
    } finally {
      setIsRiggingLoading(false);
    }
  };

  // Change Pose in Auto-Rig Studio
  const handleSelectPose = (pose: string) => {
    setActivePose(pose);
    setIsPosePlaying(pose !== 't_pose');
    if (rigResultRef.current) {
      AutoRigEngine.applyTestPose(rigResultRef.current.bonesMap, pose, poseProgress);
    }
  };


  // Apply Map Preset
  const handleApplyMapPreset = () => {
    if (onSelectMap) {
      onSelectMap(selectedMapPath);
    }

    const updatedScene: MasterSceneConfig = {
      ...scene,
      environment: {
        ...scene.environment,
        map: selectedMapPath,
        sky_time: selectedSkyTime as any,
      },
    };
    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  // Capture high-definition snapshot directly from 3D Character Viewport
  const handleCaptureSnapshot = (): string => {
    if (previewRendererRef.current && previewSceneRef.current && previewCameraRef.current) {
      previewRendererRef.current.render(previewSceneRef.current, previewCameraRef.current);
      return previewRendererRef.current.domElement.toDataURL('image/png');
    }
    return '';
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#090d16',
        color: '#f1f5f9',
        fontSize: 12,
        fontFamily: 'Inter, system-ui, sans-serif',
        overflow: 'hidden',
      }}
    >
      {/* ─── TOP WORKBENCH HEADER & TAB NAVIGATION ────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '8px 16px',
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          background: 'rgba(15, 23, 42, 0.95)',
          zIndex: 10,
          flexShrink: 0,
        }}
      >
        {/* Main Tab Switcher */}
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <button
            onClick={() => setActiveTab('modular')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 14px',
              fontSize: 12,
              fontWeight: 700,
              borderRadius: 6,
              border: activeTab === 'modular' ? '1px solid rgba(56, 189, 248, 0.5)' : '1px solid rgba(255,255,255,0.08)',
              background: activeTab === 'modular' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
              color: activeTab === 'modular' ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
          >
            <Shirt size={14} /> 👘 Lắp Ráp Nhân Vật Modular
          </button>

          <button
            onClick={() => setActiveTab('rigging')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 14px',
              fontSize: 12,
              fontWeight: 700,
              borderRadius: 6,
              border: activeTab === 'rigging' ? '1px solid rgba(168, 85, 247, 0.5)' : '1px solid rgba(255,255,255,0.08)',
              background: activeTab === 'rigging' ? 'rgba(168, 85, 247, 0.2)' : 'rgba(255,255,255,0.03)',
              color: activeTab === 'rigging' ? '#c084fc' : '#94a3b8',
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
          >
            <Wrench size={14} /> 🦴 Auto-Rig Studio
          </button>

          <button
            onClick={() => setActiveTab('map')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 14px',
              fontSize: 12,
              fontWeight: 700,
              borderRadius: 6,
              border: activeTab === 'map' ? '1px solid rgba(74, 222, 128, 0.5)' : '1px solid rgba(255,255,255,0.08)',
              background: activeTab === 'map' ? 'rgba(74, 222, 128, 0.2)' : 'rgba(255,255,255,0.03)',
              color: activeTab === 'map' ? '#4ade80' : '#94a3b8',
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
          >
            <MapIcon size={14} /> 🗺️ Thiết Kế Map & Bối Cảnh
          </button>
        </div>

        {/* Close Button */}
        {onClose && (
          <button
            onClick={onClose}
            title="Đóng Xưởng 3D"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '5px 12px',
              fontSize: 11,
              fontWeight: 600,
              borderRadius: 6,
              border: '1px solid rgba(239, 68, 68, 0.3)',
              background: 'rgba(239, 68, 68, 0.15)',
              color: '#f87171',
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
          >
            <X size={14} /> Đóng Xưởng
          </button>
        )}
      </div>

      {/* ─── MAIN CONTENT BODY ───────────────────────────────────────────── */}
      <div style={{ flex: 1, display: 'flex', minHeight: 0, overflow: 'hidden' }}>
        {/* CASE A: TAB THIẾT KẾ MAP & BỐI CẢNH ───────────────────────────── */}
        {activeTab === 'map' ? (
          <MapDesignerPanel
            scene={scene}
            onUpdateScene={onUpdateScene}
            onSelectMap={onSelectMap}
          />
        ) : (
          /* CASE B: TAB MODULAR HOẶC AUTO-RIG ───────────────────────────── */
          <div style={{ display: 'flex', width: '100%', height: '100%', overflow: 'hidden' }}>
            {/* 1. LEFT: 3D CHARACTER PREVIEW CANVAS */}
            <div
              style={{
                flex: '0 0 520px',
                borderRight: '1px solid rgba(255,255,255,0.08)',
                display: 'flex',
                flexDirection: 'column',
                background: '#060911',
                position: 'relative',
              }}
            >
              <div
                style={{
                  padding: '8px 12px',
                  borderBottom: '1px solid rgba(255,255,255,0.08)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  background: 'rgba(255,255,255,0.02)',
                }}
              >
                <span style={{ fontWeight: 600, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Sparkles size={14} /> 3D Character Viewport
                </span>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <button
                    onClick={() => setShowFloorGrid((prev) => !prev)}
                    title={showFloorGrid ? "Ẩn lưới sàn" : "Hiện lưới sàn"}
                    style={{
                      padding: '2px 8px',
                      fontSize: 10,
                      borderRadius: 4,
                      border: showFloorGrid ? '1px solid rgba(56, 189, 248, 0.4)' : '1px solid rgba(255,255,255,0.1)',
                      background: showFloorGrid ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.05)',
                      color: showFloorGrid ? '#38bdf8' : '#cbd5e1',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                    }}
                  >
                    <Grid size={11} /> {showFloorGrid ? 'Ẩn Lưới Sàn' : 'Hiện Lưới Sàn'}
                  </button>

                  <button
                    onClick={() => setShowWireframe((prev) => !prev)}
                    title="Bật/Tắt khung lưới tam giác (Wireframe)"
                    style={{
                      padding: '2px 8px',
                      fontSize: 10,
                      borderRadius: 4,
                      border: showWireframe ? '1px solid rgba(168, 85, 247, 0.4)' : '1px solid rgba(255,255,255,0.1)',
                      background: showWireframe ? 'rgba(168, 85, 247, 0.15)' : 'rgba(255,255,255,0.05)',
                      color: showWireframe ? '#c084fc' : '#cbd5e1',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                    }}
                  >
                    <Box size={11} /> Wireframe
                  </button>

                  <button
                    onClick={() => {
                      if (previewCameraRef.current && previewControlsRef.current) {
                        previewCameraRef.current.position.set(0, 1.15, 2.6);
                        previewControlsRef.current.target.set(0, 0.85, 0);
                        previewControlsRef.current.update();
                      }
                    }}
                    title="Đặt lại Camera"
                    style={{
                      padding: '2px 8px',
                      fontSize: 10,
                      borderRadius: 4,
                      border: '1px solid rgba(255,255,255,0.1)',
                      background: 'rgba(255,255,255,0.05)',
                      color: '#cbd5e1',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                  >
                    <RotateCcw size={10} /> Reset
                  </button>
                </div>
              </div>

              {/* 3D Canvas Container */}
              <div style={{ flex: 1, position: 'relative', width: '100%', minHeight: 0, overflow: 'hidden' }}>
                <div ref={canvasContainerRef} style={{ width: '100%', height: '100%' }} />

                {isPreviewLoading && (
                  <div
                    style={{
                      position: 'absolute',
                      inset: 0,
                      background: 'rgba(10, 15, 29, 0.7)',
                      backdropFilter: 'blur(4px)',
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: 8,
                      color: '#38bdf8',
                      fontWeight: 600,
                      fontSize: 12,
                    }}
                  >
                    <Loader size={24} className="animate-spin" />
                    <span>Đang tải mô hình 3D...</span>
                  </div>
                )}

                {/* Mouse Orbit Hint */}
                <div
                  style={{
                    position: 'absolute',
                    top: 8,
                    left: 8,
                    fontSize: 10,
                    color: 'rgba(255,255,255,0.5)',
                    pointerEvents: 'none',
                    background: 'rgba(0,0,0,0.4)',
                    padding: '3px 8px',
                    borderRadius: 4,
                  }}
                >
                  🖱️ Chuột trái: Xoay 360° | Cuộn: Zoom
                </div>
              </div>

              {/* Floating Viewport Overlays for Auto-Rig */}
              {activeTab === 'rigging' && isRigged && (
                <div
                  style={{
                    position: 'absolute',
                    bottom: 12,
                    left: 12,
                    right: 12,
                    background: 'rgba(15, 23, 42, 0.85)',
                    backdropFilter: 'blur(8px)',
                    padding: '6px 10px',
                    borderRadius: 8,
                    border: '1px solid rgba(255,255,255,0.1)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: 6,
                  }}
                >
                  <button
                    onClick={() => {
                      setShowJoints(!showJoints);
                      if (rigResultRef.current && previewSceneRef.current) {
                        if (!showJoints) previewSceneRef.current.add(rigResultRef.current.jointVisualizer);
                        else previewSceneRef.current.remove(rigResultRef.current.jointVisualizer);
                      }
                    }}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                      padding: '3px 8px',
                      fontSize: 11,
                      borderRadius: 4,
                      background: showJoints ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.05)',
                      color: showJoints ? '#38bdf8' : '#94a3b8',
                      border: 'none',
                      cursor: 'pointer',
                    }}
                  >
                    {showJoints ? <Eye size={12} /> : <EyeOff size={12} />} Khớp Xương
                  </button>

                  <button
                    onClick={() => setIsPosePlaying(!isPosePlaying)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                      padding: '3px 8px',
                      fontSize: 11,
                      borderRadius: 4,
                      background: isPosePlaying ? 'rgba(239, 68, 68, 0.2)' : 'rgba(34, 197, 94, 0.2)',
                      color: isPosePlaying ? '#f87171' : '#4ade80',
                      border: 'none',
                      cursor: 'pointer',
                    }}
                  >
                    {isPosePlaying ? <Pause size={12} /> : <Play size={12} />}
                    {isPosePlaying ? 'Tạm Dừng' : 'Chạy Thử'}
                  </button>
                </div>
              )}
            </div>

            {/* 2. RIGHT: CONFIGURATION / MODULAR VERTICAL TABS */}
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
              {activeTab === 'modular' && (
                <ModularOutfitVerticalTabs
                  scene={scene}
                  onUpdateScene={onUpdateScene}
                  assembly={assembly}
                  onAssemblyChange={(updatedAssembly) => {
                    setAssembly(updatedAssembly);
                  }}
                  sliders={faceSliders}
                  onSlidersChange={handleFaceSlidersChange}
                  onCaptureSnapshot={handleCaptureSnapshot}
                />
              )}

              {activeTab === 'rigging' && (
                <div style={{ flex: 1, padding: 16, overflowY: 'auto' }}>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 14, maxWidth: 720 }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span style={{ fontSize: 14, fontWeight: 700, color: '#c084fc' }}>
                        Auto-Rigging Studio (Gắn Xương Chuẩn Giải Phẫu 3D)
                      </span>
                      <span style={{ fontSize: 11, color: '#94a3b8' }}>17 Khớp Xương Tiêu Chuẩn Three.js</span>
                    </div>

                    <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
                      <label style={{ display: 'block', fontWeight: 600, marginBottom: 8, color: '#e2e8f0' }}>
                        Chọn Mô Hình 3D Cần Gắn Xương (.glb)
                      </label>
                      <select
                        value={modelToRig}
                        onChange={(e) => {
                          setModelToRig(e.target.value);
                          setIsRigged(false);
                        }}
                        style={{
                          width: '100%',
                          padding: '8px 12px',
                          borderRadius: 6,
                          background: '#0f172a',
                          border: '1px solid rgba(255,255,255,0.15)',
                          color: '#fff',
                          outline: 'none',
                          fontSize: 12,
                        }}
                      >
                        {(availableCategories.find((c) => c.id === 'than_co_ban')?.items || []).map((item) => (
                          <option key={item.id} value={item.path}>
                            [Thân] {item.name} ({item.path})
                          </option>
                        ))}
                        {(availableCategories.find((c) => c.id === 'trang_phuc')?.items || []).map((item) => (
                          <option key={item.id} value={item.path}>
                            [Trang Phục] {item.name} ({item.path})
                          </option>
                        ))}
                        {availableCategories
                          .filter((c) => c.id !== 'than_co_ban' && c.id !== 'trang_phuc' && c.id !== '_lap_rap')
                          .flatMap((c) => c.items)
                          .map((item) => (
                            <option key={item.id} value={item.path}>
                              {item.name} ({item.path})
                            </option>
                          ))}
                      </select>

                      <button
                        onClick={handleRunAutoRig}
                        disabled={isRiggingLoading}
                        style={{
                          marginTop: 12,
                          width: '100%',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          gap: 8,
                          padding: '11px',
                          borderRadius: 6,
                          background: 'linear-gradient(135deg, #9333ea, #7e22ce)',
                          color: '#fff',
                          fontWeight: 700,
                          fontSize: 13,
                          border: 'none',
                          cursor: isRiggingLoading ? 'not-allowed' : 'pointer',
                          boxShadow: '0 4px 12px rgba(147, 51, 234, 0.3)',
                        }}
                      >
                        <Sparkles size={16} />
                        {isRiggingLoading ? 'Đang phân tích cấu trúc xương...' : '⚡ Kích Hoạt Auto-Rig 1-Click'}
                      </button>
                    </div>

                    {/* Test Animation Poses */}
                    {isRigged && (
                      <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
                        <label style={{ display: 'block', fontWeight: 600, marginBottom: 10, color: '#e2e8f0' }}>
                          Chạy Thử Nghiệm Hoạt Cảnh (Pose & Motion Test)
                        </label>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                          {[
                            { id: 't_pose', label: 'T-Pose' },
                            { id: 'walk', label: '🚶 Bước Đi (Walk)' },
                            { id: 'slash', label: '⚔️ Vung Kiếm (Slash)' },
                            { id: 'defend', label: '🛡️ Thủ Thế (Defend)' },
                            { id: 'wave', label: '👋 Vẫy Tay (Wave)' },
                            { id: 'sit', label: '🪑 Ngồi Nghỉ (Sit)' },
                          ].map((p) => (
                            <button
                              key={p.id}
                              onClick={() => handleSelectPose(p.id)}
                              style={{
                                padding: '7px 14px',
                                borderRadius: 6,
                                fontSize: 12,
                                fontWeight: 600,
                                border: activePose === p.id ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.1)',
                                background: activePose === p.id ? 'rgba(168, 85, 247, 0.25)' : 'rgba(255,255,255,0.05)',
                                color: activePose === p.id ? '#c084fc' : '#cbd5e1',
                                cursor: 'pointer',
                              }}
                            >
                              {p.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

/**
 * Apply facial sliders and wireframe settings to any Three.js 3D model hierarchy
 */
export function applySlidersToModelGroup(
  group: THREE.Object3D,
  sliders: FaceSliderConfig,
  showWireframe: boolean = false
): void {
  const { baseFaceOpacity, eyebrowOpacity, pupilOpacity, noseOpacity, mouthOpacity, skinSmoothness, costumeOpacity } = sliders;

  group.traverse((child) => {
    if ((child as THREE.Mesh).isMesh) {
      const mesh = child as THREE.Mesh;
      const name = mesh.name.toLowerCase();
      const parentName = (mesh.parent?.name || '').toLowerCase();
      const matName = Array.isArray(mesh.material)
        ? mesh.material.map((m) => m.name.toLowerCase()).join(' ')
        : (mesh.material?.name || '').toLowerCase();

      const isBaseFace =
        (name.includes('face') || parentName.includes('face') || matName.includes('face')) &&
        !name.includes('p0054') && !name.includes('p0052') && !matName.includes('p0054') && !matName.includes('p0052');

      const isEyebrow = name.includes('eyebrow') || parentName.includes('eyebrow') || matName.includes('eyebrow');
      const isPupil = name.includes('pupil') || parentName.includes('pupil') || matName.includes('pupil');
      const isNose = name.includes('nose') || parentName.includes('nose');
      const isMouth = name.includes('mouth') || parentName.includes('mouth') || name.includes('lip') || parentName.includes('lip');

      if (mesh.material) {
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        mats.forEach((m: any) => {
          m.wireframe = showWireframe;
          m.side = THREE.FrontSide; // Backface culling prevents inner mouth/tongue bleeding through

          // Base Face / Mặt Cũ
          if (isBaseFace) {
            mesh.visible = baseFaceOpacity > 0.02;
            m.transparent = baseFaceOpacity < 0.98;
            m.opacity = baseFaceOpacity;
          }

          // Eyebrow Opacity
          if (isEyebrow) {
            mesh.visible = eyebrowOpacity > 0.02;
            m.transparent = eyebrowOpacity < 0.98;
            m.opacity = eyebrowOpacity;
          }

          // Pupil Opacity
          if (isPupil) {
            mesh.visible = pupilOpacity > 0.02;
            m.transparent = pupilOpacity < 0.98;
            m.opacity = pupilOpacity;
          }

          // Nose Opacity
          if (isNose) {
            mesh.visible = noseOpacity > 0.02;
            m.transparent = noseOpacity < 0.98;
            m.opacity = noseOpacity;
          }

          // Mouth & Lip Opacity
          if (isMouth) {
            mesh.visible = mouthOpacity > 0.02;
            m.transparent = mouthOpacity < 0.98;
            m.opacity = mouthOpacity;
          }

          // Skin Smoothness (Roughness)
          if (name.includes('body') || name.includes('face') || parentName.includes('face') || matName.includes('face')) {
            if (m.roughness !== undefined) {
              m.roughness = Math.max(0.1, 1.0 - skinSmoothness * 0.45);
            }
          }

          // Costume Opacity
          if (!name.includes('body') && !name.includes('face') && !isPupil && !isEyebrow && !isBaseFace && !isNose && !isMouth) {
            mesh.visible = costumeOpacity > 0.02;
            m.transparent = costumeOpacity < 0.98;
            m.opacity = costumeOpacity;
          }
        });
      }
    }
  });
}
