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
  UserCheck,
  UserPlus,
  Eye,
  EyeOff,
  Layers,
  Save,
  CheckCircle,
  HelpCircle,
  Lightbulb,
  X,
  Loader,
  Plus,
  Trash2,
  Check,
  Grid,
  Box,
  Sliders,
} from 'lucide-react';
import { MasterSceneConfig, ActorConfig, CharacterAssembly } from '../types/scene';
import { AutoRigEngine, AutoRigResult } from '../core/actors/AutoRigEngine';
import { AssetLoaderRegistry } from '../core/assets/AssetLoaderRegistry';
import { fetchLiveAssetManifest } from '../core/assets/AssetManifestLoader';
import { AssetItem } from './AssetBrowserPanel';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

interface CharacterWorkbenchPanelProps {
  scene: MasterSceneConfig;
  onUpdateScene: (updatedScene: MasterSceneConfig) => void;
  onSelectAvatar?: (actorId: string, vrmUrl: string) => void;
  onSelectMap?: (mapId: string) => void;
  onClose?: () => void;
  isModal?: boolean;
}

interface CustomPreset {
  id: string;
  name: string;
  body: string;
  costume: string;
  face: string;
  gender: 'male' | 'female';
}

const DEFAULT_PRESETS: CustomPreset[] = [
  {
    id: 'preset_amber_nectar',
    name: '🧑 Nam: Lý Tiên Sinh (Amber Nectar)',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/amber_nectar_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
  {
    id: 'preset_precision_strike',
    name: '👩 Nữ: Võ Khách (Precision Strike)',
    body: 'assets/characters/base_bodies/male/body_base_-_manekina.glb',
    costume: 'assets/characters/costumes/male/precision_strike_-_manekina.glb',
    face: '',
    gender: 'female',
  },
  {
    id: 'preset_scary_cat',
    name: '🐱 Nam: Hắc Miêu Hiệp Sĩ (Scary Cat)',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/scary_cat_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
  {
    id: 'preset_sleuth_verdict',
    name: '🕵️ Nam: Thám Tử (Sleuth Verdict)',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/sleuths_verdict_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
];

// Card definition items
const BASE_BODY_ITEMS = [
  {
    id: 'body_male',
    name: 'Manekin Body Base (Nam)',
    path: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    preview: '/assets/characters/base_bodies/man/body_base_-_manekin.png',
    gender: 'male' as const,
  },
  {
    id: 'body_female',
    name: 'Manekina Body Base (Nữ)',
    path: 'assets/characters/base_bodies/male/body_base_-_manekina.glb',
    preview: '/assets/characters/base_bodies/male/body_base_-_manekina.png',
    gender: 'female' as const,
  },
];

const COSTUME_ITEMS = [
  {
    id: 'costume_amber_man',
    name: 'Amber Nectar (Hổ Phách Nam)',
    path: 'assets/characters/costumes/man/amber_nectar_-_manekin.glb',
    preview: '/assets/characters/costumes/man/amber_nectar_-_manekin.png',
    gender: 'male' as const,
  },
  {
    id: 'costume_precision_female',
    name: 'Precision Strike (Tinh Nhuệ Nữ)',
    path: 'assets/characters/costumes/male/precision_strike_-_manekina.glb',
    preview: '/assets/characters/costumes/male/precision_strike_-_manekina.png',
    gender: 'female' as const,
  },
  {
    id: 'costume_scary_cat_man',
    name: 'Scary Cat (Hắc Miêu Nam)',
    path: 'assets/characters/costumes/man/scary_cat_-_manekin.glb',
    preview: '/assets/characters/costumes/man/scary_cat_-_manekin.png',
    gender: 'male' as const,
  },
  {
    id: 'costume_sleuth_man',
    name: 'Sleuth\'s Verdict (Thám Tử Nam)',
    path: 'assets/characters/costumes/man/sleuths_verdict_-_manekin.glb',
    preview: '/assets/characters/costumes/man/sleuths_verdict_-_manekin.png',
    gender: 'male' as const,
  },
];

const FACE_ITEMS = [
  {
    id: 'face_default_base',
    name: 'Khuôn Mặt Gốc (Thân Liền)',
    path: '',
    preview: '/assets/characters/base_bodies/man/body_base_-_manekin.png',
    gender: 'male' as const,
  },
  {
    id: 'face_dawnbreaker_man',
    name: 'Dawnbreaker (Bình Minh Nam)',
    path: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    preview: '/assets/characters/faces/man/dawnbreaker_-_manekin.png',
    gender: 'male' as const,
  },
  {
    id: 'face_starlight_female',
    name: 'Starlight Fragments (Tinh Tú)',
    path: 'assets/characters/faces/male/starlight_fragments_-_manekin.glb',
    preview: '/assets/characters/faces/male/starlight_fragments_-_manekin.png',
    gender: 'female' as const,
  },
];

export const CharacterWorkbenchPanel: React.FC<CharacterWorkbenchPanelProps> = ({
  scene,
  onUpdateScene,
  onSelectMap,
  onClose,
  isModal = false,
}) => {
  const [activeTab, setActiveTab] = useState<'rigging' | 'modular' | 'map'>('modular');
  const [isPreviewLoading, setIsPreviewLoading] = useState<boolean>(false);

  // --- 1. MODULAR OUTFIT STATE ---
  const [selectedActorId, setSelectedActorId] = useState<string>(scene.actors[0]?.id || '');
  const [baseBody, setBaseBody] = useState<string>('assets/characters/base_bodies/man/body_base_-_manekin.glb');
  const [costume, setCostume] = useState<string>('assets/characters/costumes/man/amber_nectar_-_manekin.glb');
  const [face, setFace] = useState<string>('assets/characters/faces/man/dawnbreaker_-_manekin.glb');
  const [hairstyle, setHairstyle] = useState<string>('');
  const [newActorName, setNewActorName] = useState<string>('Lý Tiên Sinh');
  const [isAppliedSuccess, setIsAppliedSuccess] = useState<boolean>(false);

  // Custom Presets
  const [customPresets, setCustomPresets] = useState<CustomPreset[]>(() => {
    try {
      const saved = localStorage.getItem('custom_character_presets');
      return saved ? JSON.parse(saved) : DEFAULT_PRESETS;
    } catch {
      return DEFAULT_PRESETS;
    }
  });

  // --- 2. AUTO-RIG STATE ---
  const [modelToRig, setModelToRig] = useState<string>('assets/characters/base_bodies/man/body_base_-_manekin.glb');
  const [isRigged, setIsRigged] = useState<boolean>(false);
  const [isRiggingLoading, setIsRiggingLoading] = useState<boolean>(false);
  const [showJoints, setShowJoints] = useState<boolean>(true);
  const [activePose, setActivePose] = useState<string>('t_pose');
  const [isPosePlaying, setIsPosePlaying] = useState<boolean>(false);
  const [poseProgress, setPoseProgress] = useState<number>(0);

  // --- 3. MAP BUILDER STATE ---
  const [selectedMapPath, setSelectedMapPath] = useState<string>(scene.environment?.map || 'assets/maps/cathedral.glb');
  const [selectedSkyTime, setSelectedSkyTime] = useState<string>(scene.environment?.sky_time || 'noon');

  // Three.js Preview Canvas Refs
  const canvasContainerRef = useRef<HTMLDivElement>(null);
  const previewSceneRef = useRef<THREE.Scene | null>(null);
  const previewRendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const previewCameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const previewControlsRef = useRef<OrbitControls | null>(null);
  const rigResultRef = useRef<AutoRigResult | null>(null);
  const currentPreviewGroupRef = useRef<THREE.Group | null>(null);
  const animFrameRef = useRef<number | null>(null);

  const [presetSavedToast, setPresetSavedToast] = useState<string>('');
  const [showFloorGrid, setShowFloorGrid] = useState<boolean>(true);
  const [showWireframe, setShowWireframe] = useState<boolean>(false);
  const [baseFaceOpacity, setBaseFaceOpacity] = useState<number>(1.0);
  const [eyebrowOpacity, setEyebrowOpacity] = useState<number>(1.0);
  const [pupilOpacity, setPupilOpacity] = useState<number>(1.0);
  const [noseOpacity, setNoseOpacity] = useState<number>(1.0);
  const [mouthOpacity, setMouthOpacity] = useState<number>(1.0);
  const [skinSmoothness, setSkinSmoothness] = useState<number>(0.75);
  const [costumeOpacity, setCostumeOpacity] = useState<number>(1.0);

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
      previewSceneRef.current.traverse((child) => {
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
          const isNoseOrMouth = name.includes('nose') || name.includes('mouth') || parentName.includes('nose') || parentName.includes('mouth');

          if (mesh.material) {
            const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
            mats.forEach((m: any) => {
              // Wireframe toggle
              m.wireframe = showWireframe;
              m.side = THREE.FrontSide; // Backface culling prevents inner mouth/tongue bleeding through

              // Base Face / Mặt Cũ (kéo về 0 là ẩn hoàn toàn mặt cũ đi)
              if (isBaseFace) {
                mesh.visible = baseFaceOpacity > 0.02;
                m.transparent = baseFaceOpacity < 0.98;
                m.opacity = baseFaceOpacity;
              }

              // Eyebrow Opacity (kéo về 0 là ẩn hoàn toàn)
              if (isEyebrow) {
                mesh.visible = eyebrowOpacity > 0.02;
                m.transparent = eyebrowOpacity < 0.98;
                m.opacity = eyebrowOpacity;
              }

              // Pupil Opacity (kéo về 0 là ẩn hoàn toàn)
              if (isPupil) {
                mesh.visible = pupilOpacity > 0.02;
                m.transparent = pupilOpacity < 0.98;
                m.opacity = pupilOpacity;
              }

              // Nose / Mouth
              if (isNoseOrMouth) {
                const faceDetailOpacity = Math.min(noseOpacity, mouthOpacity);
                mesh.visible = faceDetailOpacity > 0.02;
                m.transparent = faceDetailOpacity < 0.98;
                m.opacity = faceDetailOpacity;
              }

              // Skin Smoothness (Roughness)
              if (name.includes('body') || name.includes('face') || parentName.includes('face') || matName.includes('face')) {
                if (m.roughness !== undefined) {
                  m.roughness = Math.max(0.1, 1.0 - skinSmoothness * 0.45);
                }
              }

              // Costume Opacity
              if (!name.includes('body') && !name.includes('face') && !isPupil && !isEyebrow && !isBaseFace) {
                mesh.visible = costumeOpacity > 0.02;
                m.transparent = costumeOpacity < 0.98;
                m.opacity = costumeOpacity;
              }
            });
          }
        }
      });
    }
  }, [showWireframe, baseFaceOpacity, eyebrowOpacity, pupilOpacity, noseOpacity, mouthOpacity, skinSmoothness, costumeOpacity]);

  // Khi chọn face mới: Tự động cho mặt cũ, mắt, mũi, miệng, lông mày gốc về 0% để dùng trọn vẹn Face mới
  const handleSelectFace = (newFacePath: string) => {
    if (face === newFacePath || newFacePath === '') {
      // Bỏ chọn hoặc chọn mặt mặc định gốc -> khôi phục 100% face cũ, mắt, mũi, miệng, lông mày
      setFace('');
      setBaseFaceOpacity(1.0);
      setEyebrowOpacity(1.0);
      setPupilOpacity(1.0);
      setNoseOpacity(1.0);
      setMouthOpacity(1.0);
    } else {
      // Chọn face mới -> đặt các chi tiết mặt cũ về 0% để dùng hẳn face mới
      setFace(newFacePath);
      setBaseFaceOpacity(0.0);
      setEyebrowOpacity(0.0);
      setPupilOpacity(0.0);
      setNoseOpacity(0.0);
      setMouthOpacity(0.0);
    }
  };

  // Save Custom Preset
  const handleSaveCustomPreset = () => {
    let name = '';
    try {
      name = prompt('Nhập tên cho mẫu phối đồ này:', newActorName) || '';
    } catch {}
    if (!name) {
      name = newActorName || (baseBody.includes('manekina') ? 'Nữ Võ Khách' : 'Nam Hiệp Sĩ');
    }

    const newPreset: CustomPreset = {
      id: `preset_${Date.now()}`,
      name: `${baseBody.includes('manekina') ? '👩' : '🧑'} ${name}`,
      body: baseBody,
      costume,
      face,
      gender: baseBody.includes('manekina') ? 'female' : 'male',
    };

    const updated = [newPreset, ...customPresets];
    setCustomPresets(updated);
    try {
      localStorage.setItem('custom_character_presets', JSON.stringify(updated));
    } catch {}
    setPresetSavedToast(`Đã lưu mẫu "${name}" thành công!`);
    setTimeout(() => setPresetSavedToast(''), 3500);
  };

  const handleDeletePreset = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const updated = customPresets.filter((p) => p.id !== id);
    setCustomPresets(updated);
    try {
      localStorage.setItem('custom_character_presets', JSON.stringify(updated));
    } catch {}
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

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
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

    return () => {
      domEl.removeEventListener('pointermove', onPointerMove as any);
      domEl.removeEventListener('wheel', onWheel);
      resizeObserver.disconnect();
      if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
      controls.dispose();
      renderer.dispose();
    };
  }, []);

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

    const parts = [
      { key: 'body', path: baseBody },
      { key: 'costume', path: costume },
      { key: 'face', path: face },
      { key: 'hair', path: hairstyle },
    ].filter((p) => Boolean(p.path));

    Promise.all(parts.map((p) => AssetLoaderRegistry.loadCharacterPart(p.path).then((model) => ({ key: p.key, model }))))
      .then((loadedList) => {
        if (!isMounted) return;

        const hasFace = loadedList.some((item) => item.key === 'face');

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
              if (hasFace && key === 'body' && isFaceDetail) {
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
                    m.map.anisotropy = 16;
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
        setIsPreviewLoading(false);
      })
      .catch((err) => {
        console.warn('Lỗi tải preview modular:', err);
        if (isMounted) setIsPreviewLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [baseBody, costume, face, hairstyle, activeTab]);

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

  // Apply Modular Character to Active Scene Actor
  const handleApplyToCurrentActor = () => {
    const targetActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
    if (!targetActor) return;

    const assembly: CharacterAssembly = {
      base_body: baseBody,
      costume: costume,
      face: face,
      hairstyle: hairstyle || undefined,
    };

    targetActor.model = baseBody;
    targetActor.assembly = assembly;

    const updatedScene: MasterSceneConfig = {
      ...scene,
      actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)),
    };

    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  // Add As New Actor into Scene
  const handleAddNewActor = () => {
    const newId = `actor_${Math.random().toString(36).substring(2, 7)}`;
    const newActor: ActorConfig = {
      id: newId,
      name: newActorName || 'Võ Hiệp Mới',
      model: baseBody,
      assembly: {
        base_body: baseBody,
        costume: costume,
        face: face,
        hairstyle: hairstyle || undefined,
      },
      spawn_point: [0.0, 0, 1.5],
      rotation_y: 0,
      tracks: {
        movement: [{ start: 0, end: 10, action: 'idle' }],
        speech: [],
      },
    };

    const updatedScene: MasterSceneConfig = {
      ...scene,
      actors: [...scene.actors, newActor],
    };

    onUpdateScene(updatedScene);
    setSelectedActorId(newId);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
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

  return (
    <div
      style={{
        display: 'flex',
        height: '100%',
        background: '#090d16',
        color: '#f1f5f9',
        fontSize: 12,
        fontFamily: 'Inter, system-ui, sans-serif',
        overflow: 'hidden',
      }}
    >
      {/* 1. LEFT: 3D PREVIEW CANVAS */}
      <div
        style={{
          flex: '0 0 420px',
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
            <Sparkles size={14} /> 3D Workbench Viewport
          </span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 10, color: '#94a3b8' }}>
              {activeTab === 'rigging' ? 'Auto-Rig Mode' : activeTab === 'modular' ? 'Outfit Assembly' : 'Map Preset'}
            </span>
            <button
              onClick={() => setShowFloorGrid((prev) => !prev)}
              title={showFloorGrid ? "Ẩn lưới sàn để nhìn chân thực hơn" : "Hiện lưới sàn"}
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
              title={showWireframe ? "Tắt hiển thị vector tam giác" : "Bật hiển thị vector tam giác của nhân vật"}
              style={{
                padding: '2px 8px',
                fontSize: 10,
                borderRadius: 4,
                border: showWireframe ? '1px solid rgba(168, 85, 247, 0.4)' : '1px solid rgba(255,255,255,0.1)',
                background: showWireframe ? 'rgba(168, 85, 247, 0.2)' : 'rgba(255,255,255,0.05)',
                color: showWireframe ? '#c084fc' : '#cbd5e1',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
            >
              <Box size={11} /> {showWireframe ? 'Ẩn Vector' : 'Hiện Vector'}
            </button>

            <button
              onClick={() => {
                if (previewCameraRef.current && previewControlsRef.current) {
                  previewCameraRef.current.position.set(0, 1.15, 2.6);
                  previewControlsRef.current.target.set(0, 0.85, 0);
                  previewControlsRef.current.update();
                }
              }}
              title="Đặt lại góc nhìn Camera"
              style={{
                padding: '2px 6px',
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
              <RotateCcw size={10} /> Đặt lại Camera
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
              <span>Đang tải & khử rỗ da mô hình 3D...</span>
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
                padding: '3px 10px',
                fontSize: 11,
                fontWeight: 600,
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

      {/* 2. RIGHT: CONFIGURATION & WORKBENCH TABS */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        {/* Sub-Tabs Navigation */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 4,
            padding: '8px 14px',
            borderBottom: '1px solid rgba(255,255,255,0.08)',
            background: 'rgba(255,255,255,0.015)',
          }}
        >
          <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
            <button
              onClick={() => setActiveTab('modular')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                fontSize: 12,
                fontWeight: 600,
                borderRadius: 6,
                border: activeTab === 'modular' ? '1px solid rgba(56, 189, 248, 0.4)' : '1px solid transparent',
                background: activeTab === 'modular' ? 'rgba(56, 189, 248, 0.15)' : 'transparent',
                color: activeTab === 'modular' ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <Shirt size={14} /> 👘 Phối Đồ & Lắp Ghép Modular
            </button>

            <button
              onClick={() => setActiveTab('rigging')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                fontSize: 12,
                fontWeight: 600,
                borderRadius: 6,
                border: activeTab === 'rigging' ? '1px solid rgba(168, 85, 247, 0.4)' : '1px solid transparent',
                background: activeTab === 'rigging' ? 'rgba(168, 85, 247, 0.15)' : 'transparent',
                color: activeTab === 'rigging' ? '#c084fc' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <Wrench size={14} /> 🦴 Auto-Rig Studio (Gắn Xương 1-Click)
            </button>

            <button
              onClick={() => setActiveTab('map')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                fontSize: 12,
                fontWeight: 600,
                borderRadius: 6,
                border: activeTab === 'map' ? '1px solid rgba(34, 197, 94, 0.4)' : '1px solid transparent',
                background: activeTab === 'map' ? 'rgba(34, 197, 94, 0.15)' : 'transparent',
                color: activeTab === 'map' ? '#4ade80' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <MapIcon size={14} /> 🗺️ Thiết Kế Map & Preset
            </button>
          </div>

          {onClose && (
            <button
              onClick={onClose}
              title="Đóng Xưởng 3D"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 6,
                border: '1px solid rgba(255,255,255,0.1)',
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

        {/* Tab Content Container */}
        <div style={{ flex: 1, padding: 16, overflowY: 'auto' }}>
          {/* TAB 1: MODULAR OUTFIT WORKBENCH WITH VISUAL CARDS */}
          {activeTab === 'modular' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 850 }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ fontSize: 14, fontWeight: 700, color: '#38bdf8' }}>
                  Xưởng Lắp Ráp & Phối Đồ Nhân Vật (Visual Item Cards)
                </span>
                {isAppliedSuccess && (
                  <span style={{ fontSize: 12, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
                    <CheckCircle size={14} /> Đã áp dụng thành công vào Scene!
                  </span>
                )}
              </div>

              {/* Quick Presets Bar with Custom Save Button */}
              <div style={{ background: 'rgba(56, 189, 248, 0.05)', padding: 12, borderRadius: 10, border: '1px solid rgba(56, 189, 248, 0.2)' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                  <span style={{ fontSize: 12, fontWeight: 600, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                    <Sparkles size={14} /> Bộ Phối Mẫu Sẵn (Character Presets):
                  </span>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    {presetSavedToast && (
                      <span style={{ fontSize: 11, color: '#4ade80', fontWeight: 600 }}>
                        {presetSavedToast}
                      </span>
                    )}
                    <button
                      onClick={handleSaveCustomPreset}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 4,
                        padding: '4px 10px',
                        fontSize: 11,
                        fontWeight: 600,
                        borderRadius: 6,
                        background: 'rgba(34, 197, 94, 0.2)',
                        border: '1px solid rgba(34, 197, 94, 0.4)',
                        color: '#4ade80',
                        cursor: 'pointer',
                      }}
                    >
                      <Plus size={13} /> Lưu Bộ Hiện Tại Thành Mẫu Mới
                    </button>
                  </div>
                </div>

                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {customPresets.map((p) => (
                    <div
                      key={p.id}
                      onClick={() => {
                        setBaseBody(p.body);
                        setCostume(p.costume);
                        setFace(p.face);
                        setNewActorName(p.name.replace(/^[^\s]+\s+/, ''));
                      }}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 6,
                        padding: '4px 10px',
                        fontSize: 11,
                        fontWeight: 600,
                        borderRadius: 6,
                        border: costume === p.costume ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                        background: costume === p.costume ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.04)',
                        color: costume === p.costume ? '#38bdf8' : '#cbd5e1',
                        cursor: 'pointer',
                        transition: 'all 0.15s',
                      }}
                    >
                      <span>{p.name}</span>
                      {!DEFAULT_PRESETS.some((dp) => dp.id === p.id) && (
                        <span
                          onClick={(e) => handleDeletePreset(p.id, e)}
                          title="Xóa mẫu này"
                          style={{ marginLeft: 4, display: 'flex', alignItems: 'center' }}
                        >
                          <Trash2 size={12} color="#f87171" />
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              </div>

              {/* 1. VISUAL CARDS: THÂN NGƯỜI CƠ BẢN (BASE BODIES) */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                  <label style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 13 }}>
                    1. Thân Người Cơ Bản (Base Body)
                  </label>
                  {baseBody && (
                    <button
                      onClick={() => setBaseBody('')}
                      style={{ fontSize: 11, color: '#94a3b8', background: 'transparent', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
                    >
                      Bỏ chọn thân
                    </button>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))', gap: 10 }}>
                  {BASE_BODY_ITEMS.map((item) => {
                    const isSelected = baseBody === item.path;
                    return (
                      <div
                        key={item.id}
                        onClick={() => setBaseBody(baseBody === item.path ? '' : item.path)}
                        title={isSelected ? 'Nhấn để bỏ chọn' : 'Nhấn để chọn'}
                        style={{
                          background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
                          border: isSelected ? '2px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                          borderRadius: 8,
                          padding: 8,
                          cursor: 'pointer',
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'center',
                          gap: 6,
                          position: 'relative',
                          transition: 'all 0.15s',
                          boxShadow: isSelected ? '0 0 14px rgba(56, 189, 248, 0.3)' : 'none',
                        }}
                      >
                        {isSelected && (
                          <div
                            style={{
                              position: 'absolute',
                              top: 4,
                              right: 4,
                              background: '#38bdf8',
                              color: '#000',
                              borderRadius: '50%',
                              width: 16,
                              height: 16,
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                            }}
                          >
                            <Check size={11} strokeWidth={3} />
                          </div>
                        )}
                        <img
                          src={item.preview}
                          alt={item.name}
                          style={{ width: '100%', height: 90, objectFit: 'contain', borderRadius: 4 }}
                          onError={(e) => {
                            (e.target as HTMLElement).style.display = 'none';
                          }}
                        />
                        <span style={{ fontSize: 11, fontWeight: 600, textAlign: 'center', color: isSelected ? '#38bdf8' : '#e2e8f0' }}>
                          {item.name}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* 2. VISUAL CARDS: BỘ TRANG PHỤC / GIÁP CHIẾN (COSTUMES) */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                  <label style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 13 }}>
                    2. Trang Phục & Giáp Chiến (Costumes)
                  </label>
                  {costume && (
                    <button
                      onClick={() => setCostume('')}
                      style={{ fontSize: 11, color: '#94a3b8', background: 'transparent', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
                    >
                      Bỏ chọn trang phục
                    </button>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))', gap: 10 }}>
                  {/* None Costume Card */}
                  <div
                    onClick={() => setCostume('')}
                    style={{
                      background: costume === '' ? 'rgba(239, 68, 68, 0.15)' : 'rgba(255,255,255,0.03)',
                      border: costume === '' ? '2px solid #f87171' : '1px solid rgba(255,255,255,0.08)',
                      borderRadius: 8,
                      padding: 8,
                      cursor: 'pointer',
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      justifyContent: 'center',
                      minHeight: 125,
                      gap: 8,
                      position: 'relative',
                      transition: 'all 0.15s',
                    }}
                  >
                    <div style={{ width: 44, height: 44, borderRadius: '50%', background: 'rgba(255,255,255,0.05)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <X size={20} color={costume === '' ? '#f87171' : '#94a3b8'} />
                    </div>
                    <span style={{ fontSize: 11, fontWeight: 600, textAlign: 'center', color: costume === '' ? '#f87171' : '#94a3b8' }}>
                      Không Dùng Trang Phục
                    </span>
                  </div>

                  {COSTUME_ITEMS.map((item) => {
                    const isSelected = costume === item.path;
                    return (
                      <div
                        key={item.id}
                        onClick={() => setCostume(costume === item.path ? '' : item.path)}
                        title={isSelected ? 'Nhấn để bỏ chọn' : 'Nhấn để chọn'}
                        style={{
                          background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
                          border: isSelected ? '2px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                          borderRadius: 8,
                          padding: 8,
                          cursor: 'pointer',
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'center',
                          gap: 6,
                          position: 'relative',
                          transition: 'all 0.15s',
                          boxShadow: isSelected ? '0 0 14px rgba(56, 189, 248, 0.3)' : 'none',
                        }}
                      >
                        {isSelected && (
                          <div
                            style={{
                              position: 'absolute',
                              top: 4,
                              right: 4,
                              background: '#38bdf8',
                              color: '#000',
                              borderRadius: '50%',
                              width: 16,
                              height: 16,
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                            }}
                          >
                            <Check size={11} strokeWidth={3} />
                          </div>
                        )}
                        <img
                          src={item.preview}
                          alt={item.name}
                          style={{ width: '100%', height: 90, objectFit: 'contain', borderRadius: 4 }}
                          onError={(e) => {
                            (e.target as HTMLElement).style.display = 'none';
                          }}
                        />
                        <span style={{ fontSize: 11, fontWeight: 600, textAlign: 'center', color: isSelected ? '#38bdf8' : '#e2e8f0' }}>
                          {item.name}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* 3. VISUAL CARDS: KHUÔN MẶT & BIỂU CẢM (FACES) */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                  <label style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 13 }}>
                    3. Khuôn Mặt & Biểu Cảm (Faces)
                  </label>
                  {face && (
                    <button
                      onClick={() => setFace('')}
                      style={{ fontSize: 11, color: '#94a3b8', background: 'transparent', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
                    >
                      Dùng mặt mặc định
                    </button>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))', gap: 10 }}>
                  {/* None Face Card */}
                  <div
                    onClick={() => handleSelectFace('')}
                    style={{
                      background: face === '' ? 'rgba(239, 68, 68, 0.15)' : 'rgba(255,255,255,0.03)',
                      border: face === '' ? '2px solid #f87171' : '1px solid rgba(255,255,255,0.08)',
                      borderRadius: 8,
                      padding: 8,
                      cursor: 'pointer',
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      justifyContent: 'center',
                      minHeight: 125,
                      gap: 8,
                      position: 'relative',
                      transition: 'all 0.15s',
                    }}
                  >
                    <div style={{ width: 44, height: 44, borderRadius: '50%', background: 'rgba(255,255,255,0.05)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <X size={20} color={face === '' ? '#f87171' : '#94a3b8'} />
                    </div>
                    <span style={{ fontSize: 11, fontWeight: 600, textAlign: 'center', color: face === '' ? '#f87171' : '#94a3b8' }}>
                      Khuôn Mặt Mặc Định (Thân Gốc)
                    </span>
                  </div>

                  {FACE_ITEMS.filter((item) => item.path !== '').map((item) => {
                    const isSelected = face === item.path;
                    return (
                      <div
                        key={item.id}
                        onClick={() => handleSelectFace(item.path)}
                        title={isSelected ? 'Nhấn để bỏ chọn' : 'Nhấn để chọn'}
                        style={{
                          background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
                          border: isSelected ? '2px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                          borderRadius: 8,
                          padding: 8,
                          cursor: 'pointer',
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'center',
                          gap: 6,
                          position: 'relative',
                          transition: 'all 0.15s',
                          boxShadow: isSelected ? '0 0 14px rgba(56, 189, 248, 0.3)' : 'none',
                        }}
                      >
                        {isSelected && (
                          <div
                            style={{
                              position: 'absolute',
                              top: 4,
                              right: 4,
                              background: '#38bdf8',
                              color: '#000',
                              borderRadius: '50%',
                              width: 16,
                              height: 16,
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                            }}
                          >
                            <Check size={11} strokeWidth={3} />
                          </div>
                        )}
                        <img
                          src={item.preview}
                          alt={item.name}
                          style={{ width: '100%', height: 90, objectFit: 'contain', borderRadius: 4 }}
                          onError={(e) => {
                            (e.target as HTMLElement).style.display = 'none';
                          }}
                        />
                        <span style={{ fontSize: 11, fontWeight: 600, textAlign: 'center', color: isSelected ? '#38bdf8' : '#e2e8f0' }}>
                          {item.name}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* 4. FACIAL & DETAIL SLIDERS (CẤU HÌNH CHI TIẾT KHUÔN MẶT & ĐỘ MỊN) */}
              <div style={{ background: 'rgba(255,255,255,0.03)', padding: 12, borderRadius: 10, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <label style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                    <Sliders size={14} color="#38bdf8" /> 4. Cấu Hình Chi Tiết Khuôn Mặt & Độ Mịn Da
                  </label>
                  <button
                    onClick={() => {
                      setBaseFaceOpacity(1.0);
                      setEyebrowOpacity(1.0);
                      setPupilOpacity(1.0);
                      setNoseOpacity(1.0);
                      setMouthOpacity(1.0);
                      setSkinSmoothness(0.75);
                      setCostumeOpacity(1.0);
                    }}
                    style={{ fontSize: 11, color: '#94a3b8', background: 'transparent', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
                  >
                    Khôi phục mặc định
                  </button>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 10 }}>
                  {/* Face Opacity Slider (Kéo về 0 ẩn hoàn toàn face cũ) */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>🎭 Độ Hiện Face Cũ / Mặt Gốc:</span>
                      <span style={{ fontWeight: 700, color: baseFaceOpacity <= 0.05 ? '#f87171' : '#38bdf8' }}>
                        {Math.round(baseFaceOpacity * 100)}% {baseFaceOpacity <= 0.05 ? '(Ẩn Face Cũ)' : ''}
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.05"
                      value={baseFaceOpacity}
                      onChange={(e) => setBaseFaceOpacity(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>
                  {/* Eyebrow Opacity Slider */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>👁️ Độ Đậm Lông Mày:</span>
                      <span style={{ fontWeight: 700, color: eyebrowOpacity <= 0.05 ? '#f87171' : '#38bdf8' }}>
                        {Math.round(eyebrowOpacity * 100)}% {eyebrowOpacity <= 0.05 ? '(Ẩn)' : ''}
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.05"
                      value={eyebrowOpacity}
                      onChange={(e) => setEyebrowOpacity(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>

                  {/* Pupil Opacity Slider */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>✨ Độ Sáng Tròng Mắt:</span>
                      <span style={{ fontWeight: 700, color: pupilOpacity <= 0.05 ? '#f87171' : '#38bdf8' }}>
                        {Math.round(pupilOpacity * 100)}% {pupilOpacity <= 0.05 ? '(Ẩn)' : ''}
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.05"
                      value={pupilOpacity}
                      onChange={(e) => setPupilOpacity(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>

                  {/* Nose Opacity Slider */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>👃 Độ Nổi Mũi:</span>
                      <span style={{ fontWeight: 700, color: noseOpacity <= 0.05 ? '#f87171' : '#38bdf8' }}>
                        {Math.round(noseOpacity * 100)}% {noseOpacity <= 0.05 ? '(Ẩn)' : ''}
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.05"
                      value={noseOpacity}
                      onChange={(e) => setNoseOpacity(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>

                  {/* Mouth Opacity Slider */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>👄 Độ Rõ Miệng & Môi:</span>
                      <span style={{ fontWeight: 700, color: mouthOpacity <= 0.05 ? '#f87171' : '#38bdf8' }}>
                        {Math.round(mouthOpacity * 100)}% {mouthOpacity <= 0.05 ? '(Ẩn)' : ''}
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.05"
                      value={mouthOpacity}
                      onChange={(e) => setMouthOpacity(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>

                  {/* Skin Smoothness Slider */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>🌸 Độ Mịn & Bóng Da Mặt:</span>
                      <span style={{ fontWeight: 700, color: '#38bdf8' }}>
                        {Math.round(skinSmoothness * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0.1"
                      max="1"
                      step="0.05"
                      value={skinSmoothness}
                      onChange={(e) => setSkinSmoothness(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>

                  {/* Costume Opacity Slider */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                      <span style={{ color: '#cbd5e1' }}>🥋 Độ Đậm Trang Phục:</span>
                      <span style={{ fontWeight: 700, color: costumeOpacity <= 0.05 ? '#f87171' : '#38bdf8' }}>
                        {Math.round(costumeOpacity * 100)}% {costumeOpacity <= 0.05 ? '(Ẩn)' : ''}
                      </span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="1"
                      step="0.05"
                      value={costumeOpacity}
                      onChange={(e) => setCostumeOpacity(parseFloat(e.target.value))}
                      style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                    />
                  </div>
                </div>
              </div>

              {/* 5. Action Bar with Character Name Textbox */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 10, background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.08)' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <label style={{ fontWeight: 700, color: '#38bdf8', fontSize: 12, minWidth: 100, display: 'flex', alignItems: 'center', gap: 6 }}>
                    🏷️ Tên Nhân Vật:
                  </label>
                  <input
                    type="text"
                    value={newActorName}
                    onChange={(e) => setNewActorName(e.target.value)}
                    placeholder="Nhập tên nhân vật (ví dụ: Lý Tiên Sinh, Nữ Hiệp Sĩ...)"
                    style={{
                      flex: 1,
                      padding: '8px 14px',
                      borderRadius: 6,
                      background: '#0f172a',
                      border: '1px solid rgba(56, 189, 248, 0.4)',
                      color: '#fff',
                      fontSize: 13,
                      fontWeight: 600,
                      outline: 'none',
                    }}
                  />
                </div>

                <div style={{ display: 'flex', gap: 10 }}>
                  <div style={{ flex: 1, display: 'flex', gap: 8 }}>
                    <select
                      value={selectedActorId}
                      onChange={(e) => setSelectedActorId(e.target.value)}
                      style={{
                        padding: '8px 12px',
                        borderRadius: 6,
                        background: '#1e293b',
                        border: '1px solid rgba(255,255,255,0.2)',
                        color: '#fff',
                        outline: 'none',
                        fontSize: 12,
                      }}
                    >
                      {scene.actors.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.name || a.id}
                        </option>
                      ))}
                    </select>

                    <button
                      onClick={handleApplyToCurrentActor}
                      style={{
                        flex: 1,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: 6,
                        padding: '10px 16px',
                        borderRadius: 6,
                        background: 'linear-gradient(135deg, #0284c7, #0369a1)',
                        color: '#fff',
                        fontWeight: 700,
                        border: 'none',
                        cursor: 'pointer',
                        boxShadow: '0 2px 8px rgba(2, 132, 199, 0.3)',
                      }}
                    >
                      <UserCheck size={15} /> Gán Cho Nhân Vật Này
                    </button>
                  </div>

                  <button
                    onClick={handleAddNewActor}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                      padding: '10px 18px',
                      borderRadius: 6,
                      background: 'rgba(34, 197, 94, 0.2)',
                      border: '1px solid rgba(34, 197, 94, 0.4)',
                      color: '#4ade80',
                      fontWeight: 700,
                      cursor: 'pointer',
                    }}
                  >
                    <UserPlus size={15} /> Thêm Vào Cảnh Mới
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* TAB 2: AUTO-RIG STUDIO */}
          {activeTab === 'rigging' && (
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
                  <option value="assets/characters/base_bodies/man/body_base_-_manekin.glb">
                    assets/characters/base_bodies/man/body_base_-_manekin.glb (Nam)
                  </option>
                  <option value="assets/characters/base_bodies/male/body_base_-_manekina.glb">
                    assets/characters/base_bodies/male/body_base_-_manekina.glb (Nữ)
                  </option>
                  <option value="assets/characters/costumes/man/amber_nectar_-_manekin.glb">
                    assets/characters/costumes/man/amber_nectar_-_manekin.glb
                  </option>
                  <option value="assets/characters/costumes/male/precision_strike_-_manekina.glb">
                    assets/characters/costumes/male/precision_strike_-_manekina.glb
                  </option>
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
          )}

          {/* TAB 3: MAP BUILDER PRESET */}
          {activeTab === 'map' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14, maxWidth: 720 }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ fontSize: 14, fontWeight: 700, color: '#4ade80' }}>
                  Thiết Kế Map & Bối Cảnh (Map Presets)
                </span>
                {isAppliedSuccess && (
                  <span style={{ fontSize: 12, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
                    <CheckCircle size={14} /> Đã chuyển Map thành công!
                  </span>
                )}
              </div>

              {/* 1. Chọn Map Nền */}
              <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
                <label style={{ display: 'block', fontWeight: 600, marginBottom: 8, color: '#e2e8f0' }}>
                  1. Chọn Bản Đồ 3D (Map Asset)
                </label>
                <select
                  value={selectedMapPath}
                  onChange={(e) => setSelectedMapPath(e.target.value)}
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
                  <option value="assets/maps/cathedral.glb">
                    Cathedral (Thánh Đường Tráng Lệ - 8.5M Triangles)
                  </option>
                  <option value="assets/maps/game_pirate_adventure_map.glb">
                    Pirate Adventure Map (Đảo Hải Tặc - Biển Xanh)
                  </option>
                  <option value="farming_village">Farming Village (Làng Quê Sakura)</option>
                  <option value="cyberpunk_city">Cyberpunk City (Thành Phố Tương Lai)</option>
                </select>
              </div>

              {/* 2. Bầu Trời & Thời Gian */}
              <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
                <label style={{ display: 'block', fontWeight: 600, marginBottom: 8, color: '#e2e8f0' }}>
                  2. Khung Giờ Bầu Trời (Skybox Time)
                </label>
                <div style={{ display: 'flex', gap: 8 }}>
                  {[
                    { id: 'dawn', label: '🌅 Bình Minh (Dawn)' },
                    { id: 'noon', label: '☀️ Trưa Nắng (Noon)' },
                    { id: 'sunset', label: '🌇 Hoàng Hôn (Sunset)' },
                    { id: 'night', label: '🌙 Ban Đêm (Night)' },
                  ].map((s) => (
                    <button
                      key={s.id}
                      onClick={() => setSelectedSkyTime(s.id)}
                      style={{
                        flex: 1,
                        padding: '8px 10px',
                        borderRadius: 6,
                        fontSize: 11,
                        fontWeight: 600,
                        border: selectedSkyTime === s.id ? '1px solid #4ade80' : '1px solid rgba(255,255,255,0.1)',
                        background: selectedSkyTime === s.id ? 'rgba(34, 197, 94, 0.25)' : 'rgba(255,255,255,0.05)',
                        color: selectedSkyTime === s.id ? '#4ade80' : '#cbd5e1',
                        cursor: 'pointer',
                      }}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>
              </div>

              <button
                onClick={handleApplyMapPreset}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 8,
                  padding: '11px 18px',
                  borderRadius: 6,
                  background: 'linear-gradient(135deg, #16a34a, #15803d)',
                  color: '#fff',
                  fontWeight: 700,
                  fontSize: 13,
                  border: 'none',
                  cursor: 'pointer',
                  boxShadow: '0 4px 12px rgba(22, 163, 74, 0.3)',
                }}
              >
                <MapIcon size={16} /> 🗺️ Áp Dụng Bản Đồ Này Cho Cảnh
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
