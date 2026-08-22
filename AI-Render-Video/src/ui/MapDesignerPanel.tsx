/**
 * MapDesignerPanel.tsx
 *
 * Full-featured 3D Map & World Designer Panel.
 * Features:
 *  - Wide 3D Viewport with clear blue sky, sunlight shadows, grid floor (no sample gray ground plane)
 *  - Multi-Map Layering: Add multiple maps and props into the scene simultaneously
 *  - Drag & Drop: Drag any asset card directly into the 3D viewport with auto-raycast positioning
 *  - Transform Controls: Adjust Position (X,Y,Z), Rotation (Y), and Scale of any placed map/prop
 *  - Map Preset Management: Save, Export JSON, Import JSON, and Apply all layers directly to Master Scene
 *  - Dynamic-width Vertical Tabs with Vietnamese labels and auto-subtabs
 */
import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  Map as MapIcon,
  Sun,
  Grid,
  RotateCcw,
  Check,
  Search,
  CheckCircle,
  Plus,
  Compass,
  Layers,
  Sparkles,
  Eye,
  EyeOff,
  Trash2,
  Save,
  Download,
  Upload,
  Copy,
  Sliders,
  Move,
  X,
} from 'lucide-react';
import { MasterSceneConfig, PlacedProp } from '../types/scene';
import { AssetLoaderRegistry } from '../core/assets/AssetLoaderRegistry';
import {
  MapAssetItem,
  MapCategory,
  fetchLiveMapCategories,
} from './MapAssetRegistry';
import { Live3DThumbnail } from './Live3DThumbnail';

export interface PlacedObject {
  instanceId: string;
  assetId: string;
  name: string;
  path: string;
  category: string;
  position: [number, number, number];
  rotationY: number; // in degrees
  scale: number;
  visible: boolean;
  orientation?: 'horizontal' | 'vertical';
}

export interface MapPresetJSON {
  version: '2.0';
  name: string;
  description?: string;
  sky_time: string;
  time_of_day?: number;
  sun_direction?: number;
  sun_elevation?: number;
  preview_image?: string;
  placed_objects: PlacedObject[];
  created_at: string;
}

interface MapDesignerPanelProps {
  scene: MasterSceneConfig;
  onUpdateScene: (updatedScene: MasterSceneConfig) => void;
  onSelectMap?: (mapPath: string) => void;
}

export const MapDesignerPanel: React.FC<MapDesignerPanelProps> = ({
  scene,
  onUpdateScene,
  onSelectMap,
}) => {
  // ─── Categories & Items State ─────────────────────────────────
  const [categories, setCategories] = useState<MapCategory[]>([]);
  const [activeCategoryId, setActiveCategoryId] = useState('ban_do');
  const [activeSubCategoryId, setActiveSubCategoryId] = useState('all');
  const [hideEmptyCategories, setHideEmptyCategories] = useState<boolean>(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoadingManifest, setIsLoadingManifest] = useState(true);

  // ─── Environment, Astronomical Time & Sun/Shadow State ───────
  const [selectedSkyTime, setSelectedSkyTime] = useState<string>(
    scene.environment?.sky_time || 'noon'
  );
  const [timeOfDay, setTimeOfDay] = useState<number>(12.0); // 5.0 (05:00) to 23.0 (23:00)
  const [sunDirection, setSunDirection] = useState<number>(180); // 0° - 360° (180 = South at noon)
  const [sunElevation, setSunElevation] = useState<number>(65); // 5° - 85°
  const [sunIntensity, setSunIntensity] = useState<number>(2.6); // 0.5x - 5.0x
  const [showSunControls, setShowSunControls] = useState<boolean>(false);
  const [showFloorGrid, setShowFloorGrid] = useState(true);
  const [isAppliedSuccess, setIsAppliedSuccess] = useState(false);
  const [toastMessage, setToastMessage] = useState('');

  // ─── Placed Objects / Multi-Map Layers State ──────────────────
  const initialBaseMap = (scene.environment?.map || 'assets/ban_do/cathedral.glb').replace(/^assets\/maps\//, 'assets/ban_do/');
  const [placedObjects, setPlacedObjects] = useState<PlacedObject[]>([
    {
      instanceId: `layer_base_map`,
      assetId: 'base_map',
      name: 'Bản Đồ Nền (Base Map)',
      path: initialBaseMap,
      category: 'ban_do',
      position: [0, 0, 0],
      rotationY: 0,
      scale: 1.0,
      visible: true,
    },
  ]);
  const [selectedInstanceId, setSelectedInstanceId] = useState<string>('layer_base_map');
  const [showLayersInspector, setShowLayersInspector] = useState(true);

  // ─── Presets State ────────────────────────────────────────────
  const [presetList, setPresetList] = useState<MapPresetJSON[]>(() => {
    try {
      const saved = localStorage.getItem('custom_map_designer_presets');
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });

  const jsonImportRef = useRef<HTMLInputElement>(null);

  // ─── Three.js 3D Viewport Refs ────────────────────────────────
  const canvasContainerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const objectsGroupRef = useRef<THREE.Group | null>(null);
  const floorGridRef = useRef<THREE.GridHelper | null>(null);
  const sunLightRef = useRef<THREE.DirectionalLight | null>(null);
  const sunMeshRef = useRef<THREE.Mesh | null>(null);
  const sunHaloRef = useRef<THREE.Mesh | null>(null);
  const animFrameRef = useRef<number | null>(null);
  const loadedMeshesMapRef = useRef<Map<string, THREE.Object3D>>(new Map());

  // ─── 1. Load Live Categories from Manifest ───────────────────
  useEffect(() => {
    fetchLiveMapCategories().then((cats) => {
      setCategories(cats);
      if (cats.length > 0) {
        setActiveCategoryId((prev) => cats.some((c) => c.id === prev) ? prev : cats[0].id);
      }
      setIsLoadingManifest(false);
    });
  }, []);

  // ─── 2. Initialize Three.js 3D Viewport (No sample gray plane) ─
  useEffect(() => {
    if (!canvasContainerRef.current) return;

    const width = Math.max(320, canvasContainerRef.current.clientWidth || 540);
    const height = Math.max(240, canvasContainerRef.current.clientHeight || 480);

    const previewScene = new THREE.Scene();
    previewScene.background = new THREE.Color(0x38bdf8); // Sunny blue sky
    sceneRef.current = previewScene;

    const camera = new THREE.PerspectiveCamera(45, width / height, 0.05, 2500);
    camera.position.set(0, 12, 28);
    camera.lookAt(0, 2, 0);
    cameraRef.current = camera;

    const renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: false,
      preserveDrawingBuffer: true,
      powerPreference: 'high-performance',
    });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.25;

    canvasContainerRef.current.innerHTML = '';
    canvasContainerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 1.5, 0);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 0.5;
    controls.maxDistance = 600.0;
    controls.maxPolarAngle = Math.PI / 2 + 0.05;
    controlsRef.current = controls;

    // ─── Lighting Setup (Natural Sunlight & Dynamic Shadows) ───
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.25);
    previewScene.add(ambientLight);

    const hemiLight = new THREE.HemisphereLight(0x7dd3fc, 0x1e293b, 1.4);
    previewScene.add(hemiLight);

    const sunLight = new THREE.DirectionalLight(0xfffbeb, sunIntensity);
    sunLight.position.set(35, 55, 40);
    sunLight.castShadow = true;
    sunLight.shadow.mapSize.width = 2048;
    sunLight.shadow.mapSize.height = 2048;
    sunLight.shadow.camera.near = 0.5;
    sunLight.shadow.camera.far = 500;
    sunLight.shadow.camera.left = -75;
    sunLight.shadow.camera.right = 75;
    sunLight.shadow.camera.top = 75;
    sunLight.shadow.camera.bottom = -75;
    sunLight.shadow.bias = -0.0004;
    sunLightRef.current = sunLight;
    previewScene.add(sunLight);

    // ─── 3D Visible Glowing Sun Sphere with Corona Halo ────────
    const sunGeo = new THREE.SphereGeometry(5.0, 32, 32);
    const sunMat = new THREE.MeshBasicMaterial({ color: 0xfffbeb, fog: false });
    const sunMesh = new THREE.Mesh(sunGeo, sunMat);

    const haloGeo = new THREE.SphereGeometry(9.5, 32, 32);
    const haloMat = new THREE.MeshBasicMaterial({
      color: 0xfde047,
      transparent: true,
      opacity: 0.35,
      side: THREE.BackSide,
      fog: false,
    });
    const haloMesh = new THREE.Mesh(haloGeo, haloMat);
    sunMesh.add(haloMesh);
    sunMeshRef.current = sunMesh;
    sunHaloRef.current = haloMesh;
    previewScene.add(sunMesh);

    // ─── Floor Grid (Cyan / Slate) — Clean without gray plane ───
    const grid = new THREE.GridHelper(120, 60, 0x38bdf8, 0x334155);
    grid.position.y = 0;
    grid.visible = showFloorGrid;
    floorGridRef.current = grid;
    previewScene.add(grid);

    // ─── Main Group for Placed Objects & Maps ───────────────────
    const objectsGroup = new THREE.Group();
    objectsGroup.name = 'Placed_Objects_Group';
    objectsGroupRef.current = objectsGroup;
    previewScene.add(objectsGroup);

    // ─── Render Animation Loop ──────────────────────────────────
    const animate = () => {
      animFrameRef.current = requestAnimationFrame(animate);
      controls.update();
      renderer.render(previewScene, camera);
    };
    animate();

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
      resizeObserver.disconnect();
      if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
      controls.dispose();
      renderer.dispose();
    };
  }, []);

  // ─── Sync Sun Mesh, Direction, Sky Color & Real-time Shadows ──
  useEffect(() => {
    if (!sunLightRef.current || !sceneRef.current) return;
    const radAzimuth = (sunDirection * Math.PI) / 180;
    const radElevation = (sunElevation * Math.PI) / 180;
    const distance = 160; // Position high in sky dome
    const x = distance * Math.cos(radElevation) * Math.sin(radAzimuth);
    const y = distance * Math.sin(radElevation);
    const z = distance * Math.cos(radElevation) * Math.cos(radAzimuth);

    sunLightRef.current.position.set(x, Math.max(2, y), z);
    sunLightRef.current.intensity = sunIntensity;

    if (sunMeshRef.current) {
      sunMeshRef.current.position.set(x, y, z);
      sunMeshRef.current.visible = y > -15;
    }

    // Dynamic sky and sun color palette based on elevation & time
    if (sunElevation <= 12) {
      // Dawn / Sunset golden-red glow
      sceneRef.current.background = new THREE.Color(0xf43f5e);
      if (sunMeshRef.current) (sunMeshRef.current.material as THREE.MeshBasicMaterial).color.set(0xf97316);
      if (sunHaloRef.current) (sunHaloRef.current.material as THREE.MeshBasicMaterial).color.set(0xef4444);
    } else if (sunElevation <= 30) {
      // Warm Morning / Afternoon
      sceneRef.current.background = new THREE.Color(0x7dd3fc);
      if (sunMeshRef.current) (sunMeshRef.current.material as THREE.MeshBasicMaterial).color.set(0xfef08a);
      if (sunHaloRef.current) (sunHaloRef.current.material as THREE.MeshBasicMaterial).color.set(0xf59e0b);
    } else {
      // Bright Blue Day
      sceneRef.current.background = new THREE.Color(0x38bdf8);
      if (sunMeshRef.current) (sunMeshRef.current.material as THREE.MeshBasicMaterial).color.set(0xffffff);
      if (sunHaloRef.current) (sunHaloRef.current.material as THREE.MeshBasicMaterial).color.set(0xfde047);
    }
  }, [sunDirection, sunElevation, sunIntensity]);

  // ─── Sync Floor Grid Visibility ───────────────────────────────
  useEffect(() => {
    if (floorGridRef.current) {
      floorGridRef.current.visible = showFloorGrid;
    }
  }, [showFloorGrid]);

  // ─── Sync Sky Time Background ─────────────────────────────────
  useEffect(() => {
    if (!sceneRef.current) return;
    const s = sceneRef.current;
    switch (selectedSkyTime) {
      case 'dawn':
        s.background = new THREE.Color(0xfdba74);
        break;
      case 'noon':
        s.background = new THREE.Color(0x38bdf8);
        break;
      case 'sunset':
        s.background = new THREE.Color(0xf43f5e);
        break;
      case 'night':
        s.background = new THREE.Color(0x090d16);
        break;
      default:
        s.background = new THREE.Color(0x38bdf8);
    }
  }, [selectedSkyTime]);

  // ─── Calculate Natural Real-World Target Size (in meters) ────
  const getNaturalTargetSize = (category: string, isBaseMap: boolean): number => {
    if (isBaseMap) return 60.0;
    if (category === 'ban_do' || category === 'maps' || category === '_custom_ban_do') return 40.0;
    if (category === 'cong_trinh' || category === 'buildings') return 8.0;
    if (category === 'cay_coi' || category === 'trees') return 4.5;
    if (category === 'da_dia_hinh' || category === 'rocks') return 3.0;
    if (category === 'phuong_tien' || category === 'vehicles') return 3.5;
    if (category === 'dong_vat' || category === 'animals') return 2.0;
    if (category === 'noi_that' || category === 'furniture') return 1.5;
    if (
      category === 'than_co_ban' ||
      category === 'characters' ||
      category === '_lap_rap' ||
      category === 'nhan_vat_da_rap'
    )
      return 1.75;
    if (
      category === 'vu_khi' ||
      category === 'dung_cu' ||
      category === 'do_tieu_hao' ||
      category === 'phu_kien' ||
      category === 'accessories' ||
      category === 'weapons' ||
      category === 'tools'
    )
      return 1.0;
    return 2.5; // General props
  };

  // ─── 3. Sync 3D Models in Scene with placedObjects State ──────
  useEffect(() => {
    if (!objectsGroupRef.current) return;
    const group = objectsGroupRef.current;

    // Remove objects that were deleted
    const currentInstanceIds = new Set(placedObjects.map((o) => o.instanceId));
    for (const [instId, mesh] of loadedMeshesMapRef.current.entries()) {
      if (!currentInstanceIds.has(instId)) {
        group.remove(mesh);
        loadedMeshesMapRef.current.delete(instId);
      }
    }

    // Update or Load Each Object
    placedObjects.forEach((obj) => {
      const existingMesh = loadedMeshesMapRef.current.get(obj.instanceId);

      if (existingMesh) {
        // Update transforms
        const baseScale = (existingMesh as any).userData?.baseScale || 1.0;
        const totalScale = obj.scale * baseScale;
        existingMesh.position.set(obj.position[0], obj.position[1], obj.position[2]);
        existingMesh.rotation.y = (obj.rotationY * Math.PI) / 180;
        existingMesh.scale.set(totalScale, totalScale, totalScale);
        existingMesh.visible = obj.visible;

        // If it's a 2D material/texture plane, update orientation (horizontal vs vertical)
        const childMesh = existingMesh.children[0] as THREE.Mesh;
        if (childMesh && childMesh.isMesh && (childMesh.geometry as THREE.PlaneGeometry)) {
          const isVert = obj.orientation === 'vertical';
          if (isVert) {
            childMesh.rotation.x = 0;
            childMesh.position.y = 4.0;
          } else {
            childMesh.rotation.x = -Math.PI / 2;
            childMesh.position.y = 0.05;
          }
        }
      } else {
        // Load new 3D model or material texture
        if (!obj.path) return;

        const pathLower = obj.path.toLowerCase();
        const isImage =
          pathLower.endsWith('.png') ||
          pathLower.endsWith('.jpg') ||
          pathLower.endsWith('.jpeg') ||
          pathLower.endsWith('.webp') ||
          pathLower.endsWith('.bmp');

        // CASE 1: 2D Images / Textures / Materials / Skyboxes
        if (isImage) {
          const cleanUrl = obj.path.startsWith('http') ? obj.path : (obj.path.startsWith('/') ? obj.path : `/${obj.path}`);
          const loader = new THREE.TextureLoader();

          if (obj.category === 'bau_troi') {
            // Equirectangular Skybox
            loader.load(cleanUrl, (tex) => {
              if (sceneRef.current) {
                tex.mapping = THREE.EquirectangularReflectionMapping;
                tex.colorSpace = THREE.SRGBColorSpace;
                sceneRef.current.background = tex;
              }
            });
            return;
          }

          // 2D Material Quad / Ground Slab / Decal / Poster / Wall
          loader.load(
            cleanUrl,
            (tex) => {
              tex.colorSpace = THREE.SRGBColorSpace;
              tex.wrapS = THREE.RepeatWrapping;
              tex.wrapT = THREE.RepeatWrapping;

              const aspect = (tex.image && tex.image.width && tex.image.height)
                ? tex.image.width / tex.image.height
                : 1.0;

              const width = 8.0 * aspect;
              const depth = 8.0;
              const isVertical = obj.orientation === 'vertical';

              const geo = new THREE.PlaneGeometry(width, depth);
              const mat = new THREE.MeshStandardMaterial({
                map: tex,
                transparent: true,
                side: THREE.DoubleSide,
                roughness: 0.7,
                metalness: 0.05,
                depthWrite: true,
                depthTest: true,
              });
              const mesh = new THREE.Mesh(geo, mat);
              mesh.castShadow = true;
              mesh.receiveShadow = true;

              if (isVertical) {
                mesh.position.y = depth / 2;
              } else {
                mesh.rotation.x = -Math.PI / 2;
                mesh.position.y = 0.05;
              }

              const wrapper = new THREE.Group();
              wrapper.name = `wrapper_${obj.instanceId}`;
              wrapper.add(mesh);
              (wrapper as any).userData = { baseScale: 1.0 };

              const totalScale = obj.scale;
              wrapper.position.set(obj.position[0], obj.position[1], obj.position[2]);
              wrapper.rotation.y = (obj.rotationY * Math.PI) / 180;
              wrapper.scale.set(totalScale, totalScale, totalScale);
              wrapper.visible = obj.visible;

              group.add(wrapper);
              loadedMeshesMapRef.current.set(obj.instanceId, wrapper);
            },
            undefined,
            (err) => console.warn(`Lỗi tải texture vật liệu cho ${obj.name}:`, err)
          );
          return;
        }

        // CASE 2: JSON Assembled Characters or Custom Map Presets
        if (pathLower.endsWith('.json')) {
          const cleanUrl = obj.path.startsWith('http') ? obj.path : (obj.path.startsWith('/') ? obj.path : `/${obj.path}`);

          const loadJSONData = async (): Promise<any> => {
            try {
              const res = await fetch(encodeURI(cleanUrl));
              if (res.ok) return await res.json();
            } catch {}

            // Fallback: Check local storage presets in memory
            try {
              const localChars = JSON.parse(localStorage.getItem('custom_character_presets') || '[]');
              const foundChar = localChars.find((c: any) =>
                obj.name.includes(c.name) || c.name.includes(obj.name) ||
                obj.path.includes((c.name || '').replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_').toLowerCase())
              );
              if (foundChar) return foundChar.profile || foundChar;

              const localMaps = JSON.parse(localStorage.getItem('custom_map_designer_presets') || '[]');
              const foundMap = localMaps.find((m: any) => obj.name.includes(m.name) || m.name.includes(obj.name));
              if (foundMap) return foundMap;
            } catch {}

            return null;
          };

          loadJSONData()
            .then(async (data) => {
              if (!data) return;

              // Subcase 2A: Character Assembly (.json)
              const assembly = data.assembly || data.profileData?.assembly || {
                than_co_ban: data.than_co_ban || data.base_body,
                trang_phuc: data.trang_phuc || data.costume,
                khuon_mat: data.khuon_mat || data.face,
                kieu_toc: data.kieu_toc || data.hairstyle,
              };

              if (assembly && (assembly.than_co_ban || assembly.base_body || data.than_co_ban || data.base_body)) {
                const charGroup = new THREE.Group();
                charGroup.name = `char_assembly_${obj.instanceId}`;

                const partsToLoad: Array<{ key: string; path: string }> = [];
                const rawBody = assembly.than_co_ban || assembly.base_body || data.than_co_ban || data.base_body;
                if (rawBody) partsToLoad.push({ key: 'than_co_ban', path: rawBody });

                const rawCostume = assembly.trang_phuc || assembly.costume || data.trang_phuc || data.costume;
                if (rawCostume) partsToLoad.push({ key: 'trang_phuc', path: rawCostume });

                const rawFace = assembly.khuon_mat || assembly.face || data.khuon_mat || data.face;
                if (rawFace) partsToLoad.push({ key: 'khuon_mat', path: rawFace });

                const rawHair = assembly.kieu_toc || assembly.hairstyle || data.kieu_toc || data.hairstyle;
                if (rawHair) partsToLoad.push({ key: 'kieu_toc', path: rawHair });

                // Extra parts: non_mu, giay_dep, rau_ria, long_may, etc.
                for (const [k, v] of Object.entries(assembly)) {
                  if (['than_co_ban', 'base_body', 'trang_phuc', 'costume', 'khuon_mat', 'face', 'kieu_toc', 'hairstyle', 'sliders'].includes(k)) continue;
                  if (typeof v === 'string' && v.trim()) partsToLoad.push({ key: k, path: v });
                }

                const loadedParts: Array<{ key: string; model: THREE.Group }> = [];
                for (const p of partsToLoad) {
                  try {
                    const m = await AssetLoaderRegistry.loadCharacterPart(p.path);
                    m.traverse((child) => {
                      if ((child as THREE.Mesh).isMesh) {
                        const cm = child as THREE.Mesh;
                        cm.castShadow = true;
                        cm.receiveShadow = true;
                        cm.frustumCulled = false;
                        if (cm.material) {
                          const mats = Array.isArray(cm.material) ? cm.material : [cm.material];
                          mats.forEach((mat) => {
                            mat.side = THREE.DoubleSide;
                            mat.depthWrite = true;
                            mat.needsUpdate = true;
                          });
                        }
                      }
                    });
                    charGroup.add(m);
                    loadedParts.push({ key: p.key, model: m });
                  } catch (err) {
                    console.warn(`Lỗi nạp bộ phận nhân vật ${p.key} (${p.path}):`, err);
                  }
                }

                // Anatomical Snapping for Head/Face
                const bodyItem = loadedParts.find((item) => item.key === 'than_co_ban');
                const faceItem = loadedParts.find((item) => item.key === 'khuon_mat');
                if (bodyItem && faceItem) {
                  bodyItem.model.updateMatrixWorld(true);
                  faceItem.model.updateMatrixWorld(true);

                  let bodyFaceMesh: THREE.Mesh | null = null;
                  bodyItem.model.traverse((c) => {
                    if ((c as THREE.Mesh).isMesh && !bodyFaceMesh) {
                      const n = c.name.toLowerCase();
                      if (n.includes('face') || n.includes('head')) bodyFaceMesh = c as THREE.Mesh;
                    }
                  });

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

                // Scale & Center
                const box = new THREE.Box3().setFromObject(charGroup);
                const size = box.getSize(new THREE.Vector3());
                const maxDim = Math.max(size.x, size.y, size.z, 0.01);
                const targetSize = 1.8;
                const baseScale = targetSize / maxDim;

                const center = box.getCenter(new THREE.Vector3());
                charGroup.position.x = -center.x;
                charGroup.position.z = -center.z;
                charGroup.position.y = -box.min.y;

                const wrapper = new THREE.Group();
                wrapper.name = `wrapper_${obj.instanceId}`;
                wrapper.add(charGroup);
                (wrapper as any).userData = { baseScale };

                const totalScale = obj.scale * baseScale;
                wrapper.position.set(obj.position[0], obj.position[1], obj.position[2]);
                wrapper.rotation.y = (obj.rotationY * Math.PI) / 180;
                wrapper.scale.set(totalScale, totalScale, totalScale);
                wrapper.visible = obj.visible;

                group.add(wrapper);
                loadedMeshesMapRef.current.set(obj.instanceId, wrapper);
                return;
              }

              // Subcase 2B: Custom Map Preset with placed_objects (.json)
              if (data.placed_objects && Array.isArray(data.placed_objects)) {
                const subMapGroup = new THREE.Group();
                subMapGroup.name = `submap_${obj.instanceId}`;

                for (const subObj of data.placed_objects) {
                  if (!subObj.path) continue;
                  try {
                    const subModel = await AssetLoaderRegistry.loadCharacterPart(subObj.path);
                    subModel.position.set(subObj.position[0] || 0, subObj.position[1] || 0, subObj.position[2] || 0);
                    subModel.rotation.y = ((subObj.rotationY || 0) * Math.PI) / 180;
                    const s = subObj.scale || 1.0;
                    subModel.scale.set(s, s, s);
                    subMapGroup.add(subModel);
                  } catch (e) {
                    console.warn(`Lỗi nạp vật thể con trong submap:`, e);
                  }
                }

                const wrapper = new THREE.Group();
                wrapper.name = `wrapper_${obj.instanceId}`;
                wrapper.add(subMapGroup);
                (wrapper as any).userData = { baseScale: 1.0 };

                wrapper.position.set(obj.position[0], obj.position[1], obj.position[2]);
                wrapper.rotation.y = (obj.rotationY * Math.PI) / 180;
                wrapper.scale.set(obj.scale, obj.scale, obj.scale);
                wrapper.visible = obj.visible;

                group.add(wrapper);
                loadedMeshesMapRef.current.set(obj.instanceId, wrapper);
              }
            })
            .catch((err) => console.warn(`Lỗi đọc file JSON ${obj.name}:`, err));
          return;
        }

        // CASE 3: Standard 3D Models (.glb, .gltf, .fbx, .obj)
        AssetLoaderRegistry.loadCharacterPart(obj.path)
          .then((model) => {
            model.traverse((c) => {
              if ((c as THREE.Mesh).isMesh) {
                const mesh = c as THREE.Mesh;
                mesh.castShadow = true;
                mesh.receiveShadow = true;
                mesh.frustumCulled = false;
                if (mesh.material) {
                  const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
                  mats.forEach((mat) => {
                    mat.side = THREE.DoubleSide;
                    mat.depthWrite = true;
                    mat.needsUpdate = true;
                  });
                }
              }
            });

            // 1. Calculate natural bounding box dimensions
            const isBaseMap = obj.instanceId === 'layer_base_map' || (obj.category === 'ban_do' && obj.position[0] === 0 && obj.position[1] === 0 && obj.position[2] === 0 && placedObjects[0]?.instanceId === obj.instanceId);

            let baseScale = 1.0;
            if (!isBaseMap) {
              const box = new THREE.Box3().setFromObject(model);
              const size = box.getSize(new THREE.Vector3());
              const maxDim = Math.max(size.x, size.y, size.z);
              const targetSize = getNaturalTargetSize(obj.category, false);
              if (maxDim > 0) {
                baseScale = targetSize / maxDim;
              }

              // Align bottom center to origin (0, 0, 0) so props rest nicely on floor
              const center = box.getCenter(new THREE.Vector3());
              model.position.x = -center.x;
              model.position.z = -center.z;
              model.position.y = -box.min.y;
            } else {
              // Base Map retains 1:1 authoring coordinate space
              model.position.set(0, 0, 0);
            }

            // 3. Put in wrapper group with baseScale metadata
            const wrapper = new THREE.Group();
            wrapper.name = `wrapper_${obj.instanceId}`;
            wrapper.add(model);
            (wrapper as any).userData = { baseScale };

            const totalScale = obj.scale * baseScale;
            wrapper.position.set(obj.position[0], obj.position[1], obj.position[2]);
            wrapper.rotation.y = (obj.rotationY * Math.PI) / 180;
            wrapper.scale.set(totalScale, totalScale, totalScale);
            wrapper.visible = obj.visible;

            group.add(wrapper);
            loadedMeshesMapRef.current.set(obj.instanceId, wrapper);
          })
          .catch((err) => {
            console.warn(`Lỗi tải model 3D cho ${obj.name}:`, err);
          });
      }
    });
  }, [placedObjects]);

  // ─── 4. Add / Place Object Logic (1-Click or Drag & Drop) ─────
  const handleAddObject = (item: MapAssetItem, dropPos: [number, number, number] = [0, 0, 0]) => {
    // 1. If Skybox: Load into scene background directly
    if (item.category === 'bau_troi' || item.subCategory?.includes('buoi_') || item.path.includes('bau_troi') || item.path.includes('SkyBoxs')) {
      const cleanUrl = item.path.startsWith('http') ? item.path : (item.path.startsWith('/') ? item.path : `/${item.path}`);
      const loader = new THREE.TextureLoader();
      loader.load(cleanUrl, (tex) => {
        if (sceneRef.current) {
          tex.mapping = THREE.EquirectangularReflectionMapping;
          tex.colorSpace = THREE.SRGBColorSpace;
          sceneRef.current.background = tex;
          triggerToast(`🌌 Đã đổi bầu trời thành "${item.name}"`);
        }
      });
      return;
    }

    // 2. Add ANY Map, Sub-map, Material, Prop, Building, Vehicle, Tree, Animal, Weapon, Character as a new layer!
    // No restrictions! Multi-Map layering is fully supported!
    const isMapCategory = item.category === 'ban_do' || item.category === 'maps' || item.category === '_custom_ban_do';
    const isImageMaterial = item.path.toLowerCase().endsWith('.png') ||
      item.path.toLowerCase().endsWith('.jpg') ||
      item.path.toLowerCase().endsWith('.webp') ||
      item.path.toLowerCase().endsWith('.jpeg') ||
      item.path.toLowerCase().endsWith('.bmp');

    // Smart initial position offset if spawned at origin to avoid complete overlap
    let finalPos: [number, number, number] = [...dropPos];
    if (dropPos[0] === 0 && dropPos[2] === 0 && placedObjects.length > 0) {
      if (isMapCategory && placedObjects.length >= 1) {
        finalPos = [placedObjects.length * 30, 0, 0];
      } else if (!isMapCategory) {
        finalPos = [(placedObjects.length % 6) * 3 - 6, 0, Math.floor(placedObjects.length / 6) * 3 - 3];
      }
    }

    const newInstance: PlacedObject = {
      instanceId: `inst_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`,
      assetId: item.id,
      name: item.name,
      path: item.path,
      category: item.category,
      position: finalPos,
      rotationY: 0,
      scale: 1.0,
      visible: true,
      orientation: isImageMaterial ? 'horizontal' : undefined,
    };

    setPlacedObjects((prev) => [...prev, newInstance]);
    setSelectedInstanceId(newInstance.instanceId);
    setShowLayersInspector(true);
    triggerToast(`✅ Đã thêm lớp "${item.name}" vào 3D Multi-Map!`);
  };

  // ─── 5. Drag & Drop Handler with Raycasting on Ground ────────
  const handleCanvasDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  };

  const handleCanvasDrop = (e: React.DragEvent) => {
    e.preventDefault();
    const raw = e.dataTransfer.getData('application/json') || e.dataTransfer.getData('text/plain');
    if (!raw) return;

    try {
      const item: MapAssetItem = JSON.parse(raw);
      const container = canvasContainerRef.current;
      if (!container || !cameraRef.current) {
        handleAddObject(item, [0, 0, 0]);
        return;
      }

      const rect = container.getBoundingClientRect();
      const mouseX = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      const mouseY = -((e.clientY - rect.top) / rect.height) * 2 + 1;

      const raycaster = new THREE.Raycaster();
      raycaster.setFromCamera(new THREE.Vector2(mouseX, mouseY), cameraRef.current);
      const groundPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
      const intersectPoint = new THREE.Vector3();
      raycaster.ray.intersectPlane(groundPlane, intersectPoint);

      const posX = intersectPoint ? Math.round(intersectPoint.x * 10) / 10 : 0;
      const posZ = intersectPoint ? Math.round(intersectPoint.z * 10) / 10 : 0;

      handleAddObject(item, [posX, 0, posZ]);
    } catch (err) {
      console.warn('Lỗi Drag & Drop:', err);
    }
  };

  // ─── 6. Transform Updates for Selected Object ─────────────────
  const updateSelectedObject = (updates: Partial<PlacedObject>) => {
    setPlacedObjects((prev) =>
      prev.map((o) => (o.instanceId === selectedInstanceId ? { ...o, ...updates } : o))
    );
  };

  const toggleObjectVisibility = (instanceId: string) => {
    setPlacedObjects((prev) =>
      prev.map((o) => (o.instanceId === instanceId ? { ...o, visible: !o.visible } : o))
    );
  };

  const handleDeleteObject = (instanceId: string) => {
    setPlacedObjects((prev) => prev.filter((o) => o.instanceId !== instanceId));
    if (selectedInstanceId === instanceId) {
      setSelectedInstanceId(placedObjects[0]?.instanceId || '');
    }
  };

  const handleDuplicateObject = (obj: PlacedObject) => {
    const duplicate: PlacedObject = {
      ...obj,
      instanceId: `inst_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`,
      name: `${obj.name} (Bản Sao)`,
      position: [obj.position[0] + 1.5, obj.position[1], obj.position[2] + 1.5],
    };
    setPlacedObjects((prev) => [...prev, duplicate]);
    setSelectedInstanceId(duplicate.instanceId);
  };

  // ─── 7. Save / Export / Import Presets ────────────────────────
  const triggerToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(''), 3000);
  };

  // ─── Astronomical Time-of-Day Handler ───────────────────────
  const handleTimeOfDayChange = (newHour: number) => {
    setTimeOfDay(newHour);
    // Day cycle from 05:00 (Dawn, 60° East) to 12:00 (Noon, 180° South) to 19:00 (Sunset, 280° West)
    const hourFrac = Math.max(0, Math.min(1, (newHour - 5) / 14));
    const calculatedElevation = Math.max(5, Math.sin(hourFrac * Math.PI) * 75);
    const calculatedAzimuth = 60 + hourFrac * 220; // 60° -> 280°
    setSunElevation(Math.round(calculatedElevation));
    setSunDirection(Math.round(calculatedAzimuth));

    if (newHour < 7.5) setSelectedSkyTime('dawn');
    else if (newHour < 15.5) setSelectedSkyTime('noon');
    else if (newHour < 19.5) setSelectedSkyTime('sunset');
    else setSelectedSkyTime('night');
  };

  const handleSaveMapPreset = () => {
    let name = '';
    try {
      name = prompt('Nhập tên để lưu cấu hình Map này:', 'Bản Đồ Bối Cảnh Mới') || '';
    } catch {}
    if (!name) name = 'Bản Đồ Tùy Chỉnh';

    // Capture high quality 3D preview snapshot
    let snapshotDataUrl = '';
    if (rendererRef.current && sceneRef.current && cameraRef.current) {
      rendererRef.current.render(sceneRef.current, cameraRef.current);
      snapshotDataUrl = rendererRef.current.domElement.toDataURL('image/png');
    }

    const safeName = name.replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_').toLowerCase();
    const newPreset: MapPresetJSON = {
      version: '2.0',
      name,
      sky_time: selectedSkyTime,
      time_of_day: timeOfDay,
      sun_direction: sunDirection,
      sun_elevation: sunElevation,
      preview_image: `assets/ban_do/_custom_ban_do/${safeName}.png`,
      placed_objects: placedObjects,
      created_at: new Date().toISOString(),
    };

    const updatedList = [newPreset, ...presetList.filter((p) => p.name !== name)];
    setPresetList(updatedList);
    try {
      localStorage.setItem('custom_map_designer_presets', JSON.stringify(updatedList));
    } catch {}

    // Post to server API to save directly in assets/ban_do/_custom_ban_do/
    fetch('/api/save-map', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filename: safeName,
        mapData: newPreset,
        previewImageBase64: snapshotDataUrl,
      }),
    }).catch((err) => console.warn('Lỗi lưu bản đồ qua API:', err));

    // Download JSON & PNG
    const jsonBlob = new Blob([JSON.stringify(newPreset, null, 2)], { type: 'application/json' });
    const jsonUrl = URL.createObjectURL(jsonBlob);
    const aJson = document.createElement('a');
    aJson.href = jsonUrl;
    aJson.download = `${safeName}.json`;
    aJson.click();
    URL.revokeObjectURL(jsonUrl);

    if (snapshotDataUrl && snapshotDataUrl.includes('base64,')) {
      const aImg = document.createElement('a');
      aImg.href = snapshotDataUrl;
      aImg.download = `${safeName}.png`;
      aImg.click();
    }

    triggerToast(`💾 Đã lưu vào assets/ban_do/_custom_ban_do/ & tải về "${safeName}.json" + "${safeName}.png"!`);
  };

  const handleExportJSON = () => {
    let snapshotDataUrl = '';
    if (rendererRef.current && sceneRef.current && cameraRef.current) {
      rendererRef.current.render(sceneRef.current, cameraRef.current);
      snapshotDataUrl = rendererRef.current.domElement.toDataURL('image/png');
    }

    const filename = `map_preset_${Date.now()}`;
    const presetData: MapPresetJSON = {
      version: '2.0',
      name: 'Custom_Map_World',
      sky_time: selectedSkyTime,
      time_of_day: timeOfDay,
      sun_direction: sunDirection,
      sun_elevation: sunElevation,
      preview_image: `assets/ban_do/_custom_ban_do/${filename}.png`,
      placed_objects: placedObjects,
      created_at: new Date().toISOString(),
    };
    const jsonFilename = `${filename}.json`;
    const blob = new Blob([JSON.stringify(presetData, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = jsonFilename;
    a.click();
    URL.revokeObjectURL(url);
    triggerToast(`Đã xuất tệp ${jsonFilename}!`);
  };

  const handleImportJSON = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => {
      try {
        const parsed = JSON.parse(ev.target?.result as string);
        if (parsed.placed_objects && Array.isArray(parsed.placed_objects)) {
          setPlacedObjects(parsed.placed_objects);
          if (parsed.sky_time) setSelectedSkyTime(parsed.sky_time);
          triggerToast(`Đã nạp cấu hình map từ "${file.name}"!`);
        } else {
          alert('Tệp JSON không đúng định dạng Map Preset.');
        }
      } catch {
        alert('Không thể đọc tệp JSON.');
      }
    };
    reader.readAsText(file);
  };

  // ─── 8. Apply All Placed Maps & Props to Master Scene ─────────
  const handleApplyToMasterScene = () => {
    const primaryMapObj = placedObjects.find((o) => o.category === 'ban_do' || o.category === 'maps') || placedObjects[0];
    const primaryMapPath = (primaryMapObj?.path || 'assets/ban_do/cathedral.glb').replace(/^assets\/maps\//, 'assets/ban_do/');

    if (onSelectMap) {
      onSelectMap(primaryMapPath);
    }

    // Convert placed objects into Master Scene placed_props
    const convertedProps: PlacedProp[] = placedObjects
      .filter((o) => o.instanceId !== primaryMapObj?.instanceId)
      .map((o) => ({
        id: o.instanceId,
        asset_path: o.path,
        position: o.position,
        rotation: [0, (o.rotationY * Math.PI) / 180, 0],
        scale: o.scale,
        type: (o.category as any) || 'prop',
      }));

    const updatedScene: MasterSceneConfig = {
      ...scene,
      environment: {
        ...scene.environment,
        map: primaryMapPath,
        sky_time: selectedSkyTime as any,
        placed_props: convertedProps,
      },
    };

    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    triggerToast('Đã áp dụng toàn bộ bản đồ và các lớp vật thể vào Scene!');
    setTimeout(() => setIsAppliedSuccess(false), 3500);
  };

  // ─── Current Active Category & Subcategory Items ──────────────
  const visibleCategories = React.useMemo(() => {
    return hideEmptyCategories
      ? categories.filter((cat) => cat.items.length > 0)
      : categories;
  }, [categories, hideEmptyCategories]);

  const currentCategory = React.useMemo(() => {
    return categories.find((c) => c.id === activeCategoryId) || categories[0] || null;
  }, [categories, activeCategoryId]);

  const rawSubCategories = React.useMemo(() => {
    return currentCategory?.subCategories || [];
  }, [currentCategory]);

  const subCategories = React.useMemo(() => {
    return hideEmptyCategories
      ? rawSubCategories.filter((s) => s.items.length > 0)
      : rawSubCategories;
  }, [rawSubCategories, hideEmptyCategories]);

  const hasSubCategories = subCategories.length > 0;

  useEffect(() => {
    if (visibleCategories.length > 0 && !visibleCategories.some((c) => c.id === activeCategoryId)) {
      setActiveCategoryId(visibleCategories[0].id);
      setActiveSubCategoryId('all');
    }
  }, [visibleCategories, activeCategoryId]);

  const rawItems = React.useMemo(() => {
    if (!currentCategory) return [];
    if (activeSubCategoryId === 'all' || !hasSubCategories) {
      return currentCategory.items || [];
    }
    const targetSub = subCategories.find((s) => s.id === activeSubCategoryId);
    return targetSub?.items || [];
  }, [currentCategory, activeSubCategoryId, hasSubCategories, subCategories]);

  const displayItems = React.useMemo(() => {
    const seen = new Set<string>();
    const unique: MapAssetItem[] = [];
    for (const item of rawItems) {
      const key = item.path || item.id;
      if (!seen.has(key)) {
        seen.add(key);
        unique.push(item);
      }
    }
    if (!searchQuery.trim()) return unique;
    const q = searchQuery.toLowerCase();
    return unique.filter(
      (item) => item.name.toLowerCase().includes(q) || item.path.toLowerCase().includes(q)
    );
  }, [rawItems, searchQuery]);

  const selectedObject = placedObjects.find((o) => o.instanceId === selectedInstanceId);

  return (
    <div style={{ display: 'flex', height: '100%', width: '100%', overflow: 'hidden', background: '#090d16' }}>
      {/* ─── 1. LEFT: 3D VIEWPORT & MULTI-MAP LAYERS INSPECTOR ─── */}
      <div
        style={{
          flex: '0 0 560px',
          display: 'flex',
          flexDirection: 'column',
          borderRight: '1px solid rgba(255,255,255,0.08)',
          background: '#060911',
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        {/* Top Viewport Header */}
        <div
          style={{
            padding: '8px 12px',
            borderBottom: '1px solid rgba(255,255,255,0.06)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(0,0,0,0.3)',
            flexShrink: 0,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <Compass size={14} color="#38bdf8" />
            <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8' }}>
              3D Multi-Map Workspace ({placedObjects.length} lớp)
            </span>
          </div>

          {/* Viewport Control Buttons */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <button
              onClick={() => setShowSunControls(!showSunControls)}
              title="Bật/Tắt Thanh Điều Chỉnh Hướng Nắng & Bóng Đổ"
              style={{
                background: showSunControls ? 'rgba(251, 191, 36, 0.2)' : 'rgba(255,255,255,0.05)',
                border: `1px solid ${showSunControls ? 'rgba(251, 191, 36, 0.4)' : 'rgba(255,255,255,0.1)'}`,
                color: showSunControls ? '#fbbf24' : '#94a3b8',
                borderRadius: 4,
                padding: '3px 8px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                fontSize: 10,
                fontWeight: 600,
              }}
            >
              <Sun size={12} /> Hướng Nắng ({sunDirection}°)
            </button>

            <button
              onClick={() => setShowLayersInspector(!showLayersInspector)}
              title="Bật/Tắt Bảng Điều Chỉnh Lớp"
              style={{
                background: showLayersInspector ? 'rgba(56,189,248,0.2)' : 'rgba(255,255,255,0.05)',
                border: `1px solid ${showLayersInspector ? 'rgba(56,189,248,0.4)' : 'rgba(255,255,255,0.1)'}`,
                color: showLayersInspector ? '#38bdf8' : '#94a3b8',
                borderRadius: 4,
                padding: '3px 8px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                fontSize: 10,
                fontWeight: 600,
              }}
            >
              <Layers size={12} /> Lớp ({placedObjects.length})
            </button>

            <button
              onClick={() => setShowFloorGrid(!showFloorGrid)}
              title="Bật/Tắt Lưới Sàn"
              style={{
                background: showFloorGrid ? 'rgba(56,189,248,0.2)' : 'rgba(255,255,255,0.05)',
                border: `1px solid ${showFloorGrid ? 'rgba(56,189,248,0.4)' : 'rgba(255,255,255,0.1)'}`,
                color: showFloorGrid ? '#38bdf8' : '#94a3b8',
                borderRadius: 4,
                padding: '3px 6px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                fontSize: 10,
              }}
            >
              <Grid size={12} /> Lưới
            </button>

            <button
              onClick={() => {
                if (cameraRef.current && controlsRef.current) {
                  cameraRef.current.position.set(0, 12, 28);
                  controlsRef.current.target.set(0, 1.5, 0);
                  controlsRef.current.update();
                }
              }}
              title="Đặt Lại Camera"
              style={{
                background: 'rgba(255,255,255,0.05)',
                border: '1px solid rgba(255,255,255,0.1)',
                color: '#cbd5e1',
                borderRadius: 4,
                padding: '3px 6px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                fontSize: 10,
              }}
            >
              <RotateCcw size={12} /> Reset
            </button>
          </div>
        </div>

        {/* 3D Canvas Mount with Drag & Drop */}
        <div
          ref={canvasContainerRef}
          onDragOver={handleCanvasDragOver}
          onDrop={handleCanvasDrop}
          style={{ flex: 1, width: '100%', height: '100%', position: 'relative', minHeight: 200 }}
        >
          {/* Drag & Drop Overlay Hint */}
          <div
            style={{
              position: 'absolute',
              top: 8,
              left: 8,
              fontSize: 10,
              color: 'rgba(255,255,255,0.7)',
              pointerEvents: 'none',
              background: 'rgba(0,0,0,0.5)',
              backdropFilter: 'blur(4px)',
              padding: '4px 8px',
              borderRadius: 4,
              display: 'flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <Move size={11} color="#38bdf8" />
            <span>Kéo thả tài nguyên vào đây để đặt vị trí 3D</span>
          </div>

          {/* Floating Sun Direction & Shadow Sliders Panel */}
          {showSunControls && (
            <div
              style={{
                position: 'absolute',
                top: 8,
                right: 8,
                width: 250,
                background: 'rgba(15, 23, 42, 0.95)',
                backdropFilter: 'blur(8px)',
                border: '1px solid rgba(251, 191, 36, 0.3)',
                borderRadius: 8,
                padding: '10px 12px',
                display: 'flex',
                flexDirection: 'column',
                gap: 8,
                zIndex: 10,
                boxShadow: '0 4px 16px rgba(0,0,0,0.5)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255,255,255,0.1)', paddingBottom: 4 }}>
                <span style={{ fontSize: 11, fontWeight: 700, color: '#fbbf24', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <Sun size={13} /> Điều Chỉnh Hướng Nắng & Bóng
                </span>
                <button
                  onClick={() => setShowSunControls(false)}
                  style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 0 }}
                >
                  <X size={12} />
                </button>
              </div>

              {/* Time of Day Astronomical Cycle Slider */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 3, background: 'rgba(255,255,255,0.04)', padding: '6px 8px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10 }}>
                  <span style={{ color: '#cbd5e1', fontWeight: 600 }}>⏰ Chu Kỳ Giờ (Bình Minh ➔ Đêm):</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>
                    {Math.floor(timeOfDay).toString().padStart(2, '0')}:{Math.round((timeOfDay % 1) * 60).toString().padStart(2, '0')}
                  </span>
                </div>
                <input
                  type="range"
                  min="5.0"
                  max="23.0"
                  step="0.25"
                  value={timeOfDay}
                  onChange={(e) => handleTimeOfDayChange(parseFloat(e.target.value))}
                  style={{ accentColor: '#38bdf8', width: '100%', cursor: 'pointer' }}
                />
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                  <span>🌅 05:00</span>
                  <span>☀️ 08:00</span>
                  <span>🌞 12:00</span>
                  <span>🌇 17:00</span>
                  <span>🌆 19:00</span>
                  <span>🌙 23:00</span>
                </div>
              </div>

              {/* Sun Direction Slider */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10 }}>
                  <span style={{ color: '#cbd5e1' }}>☀️ Hướng Mặt Trời (Azimuth):</span>
                  <span style={{ color: '#fbbf24', fontWeight: 700 }}>{sunDirection}°</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="360"
                  value={sunDirection}
                  onChange={(e) => setSunDirection(parseFloat(e.target.value))}
                  style={{ accentColor: '#fbbf24', width: '100%' }}
                />
              </div>

              {/* Sun Elevation Slider */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10 }}>
                  <span style={{ color: '#cbd5e1' }}>🌅 Độ Cao (Elevation):</span>
                  <span style={{ color: '#f59e0b', fontWeight: 700 }}>{sunElevation}°</span>
                </div>
                <input
                  type="range"
                  min="5"
                  max="85"
                  value={sunElevation}
                  onChange={(e) => setSunElevation(parseFloat(e.target.value))}
                  style={{ accentColor: '#f59e0b', width: '100%' }}
                />
              </div>

              {/* Sun Intensity Slider */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10 }}>
                  <span style={{ color: '#cbd5e1' }}>💡 Cường Độ Ánh Sáng:</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{sunIntensity.toFixed(1)}x</span>
                </div>
                <input
                  type="range"
                  min="0.5"
                  max="5.0"
                  step="0.1"
                  value={sunIntensity}
                  onChange={(e) => setSunIntensity(parseFloat(e.target.value))}
                  style={{ accentColor: '#38bdf8', width: '100%' }}
                />
              </div>
            </div>
          )}
        </div>

        {/* ─── Placed Objects / Layers Inspector Drawer ───────── */}
        {showLayersInspector && (
          <div
            style={{
              height: 260,
              borderTop: '1px solid rgba(255,255,255,0.1)',
              background: 'rgba(15, 23, 42, 0.98)',
              display: 'flex',
              flexDirection: 'column',
              flexShrink: 0,
            }}
          >
            {/* Layers Header */}
            <div
              style={{
                padding: '6px 10px',
                borderBottom: '1px solid rgba(255,255,255,0.06)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                background: 'rgba(0,0,0,0.2)',
              }}
            >
              <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                <Layers size={13} /> Danh Sách Lớp & Vật Thể ({placedObjects.length})
              </span>
              {selectedObject && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <button
                    onClick={() => handleDuplicateObject(selectedObject)}
                    title="Nhân bản vật thể này"
                    style={{
                      background: 'rgba(255,255,255,0.06)',
                      border: '1px solid rgba(255,255,255,0.12)',
                      color: '#cbd5e1',
                      borderRadius: 4,
                      padding: '2px 6px',
                      cursor: 'pointer',
                      fontSize: 10,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                  >
                    <Copy size={11} /> Nhân Bản
                  </button>
                  <button
                    onClick={() => handleDeleteObject(selectedObject.instanceId)}
                    title="Xóa vật thể này"
                    style={{
                      background: 'rgba(239,68,68,0.15)',
                      border: '1px solid rgba(239,68,68,0.3)',
                      color: '#f87171',
                      borderRadius: 4,
                      padding: '2px 6px',
                      cursor: 'pointer',
                      fontSize: 10,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 3,
                    }}
                  >
                    <Trash2 size={11} /> Xóa
                  </button>
                </div>
              )}
            </div>

            {/* Split: Left List / Right Transform Inspector */}
            <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
              {/* Layers List */}
              <div
                style={{
                  width: 220,
                  borderRight: '1px solid rgba(255,255,255,0.08)',
                  overflowY: 'auto',
                  padding: 4,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 2,
                }}
              >
                {placedObjects.map((obj) => {
                  const isSelected = selectedInstanceId === obj.instanceId;
                  return (
                    <div
                      key={obj.instanceId}
                      onClick={() => setSelectedInstanceId(obj.instanceId)}
                      style={{
                        padding: '4px 8px',
                        borderRadius: 4,
                        fontSize: 11,
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
                        border: isSelected ? '1px solid #38bdf8' : '1px solid transparent',
                        color: isSelected ? '#38bdf8' : '#cbd5e1',
                      }}
                    >
                      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 150 }}>
                        {obj.name}
                      </span>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          toggleObjectVisibility(obj.instanceId);
                        }}
                        title={obj.visible ? 'Ẩn vật thể' : 'Hiện vật thể'}
                        style={{
                          background: 'none',
                          border: 'none',
                          color: obj.visible ? '#38bdf8' : '#64748b',
                          cursor: 'pointer',
                          padding: '2px 4px',
                          display: 'flex',
                          alignItems: 'center',
                        }}
                      >
                        {obj.visible ? <Eye size={13} /> : <EyeOff size={13} />}
                      </button>
                    </div>
                  );
                })}
              </div>

              {/* Transform Controls */}
              {selectedObject ? (
                <div style={{ flex: 1, padding: '8px 12px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {/* Position X, Y, Z with Sliders & Numeric Inputs */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span style={{ fontSize: 10, fontWeight: 700, color: '#cbd5e1' }}>Vị Trí Tọa Độ (Position):</span>
                      <button
                        onClick={() => updateSelectedObject({ position: [0, 0, 0] })}
                        style={{
                          fontSize: 9,
                          padding: '1px 5px',
                          background: 'rgba(255,255,255,0.06)',
                          border: '1px solid rgba(255,255,255,0.1)',
                          color: '#94a3b8',
                          borderRadius: 3,
                          cursor: 'pointer',
                        }}
                      >
                        Về Gốc [0, 0, 0]
                      </button>
                    </div>

                    {/* X Axis */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span style={{ fontSize: 10, fontWeight: 700, color: '#f87171', width: 14 }}>X:</span>
                      <input
                        type="range"
                        min="-60"
                        max="60"
                        step="0.5"
                        value={selectedObject.position[0]}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          const newPos = [...selectedObject.position] as [number, number, number];
                          newPos[0] = val;
                          updateSelectedObject({ position: newPos });
                        }}
                        style={{ flex: 1, accentColor: '#f87171' }}
                      />
                      <input
                        type="number"
                        step="0.5"
                        value={selectedObject.position[0]}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value);
                          if (!isNaN(val)) {
                            const newPos = [...selectedObject.position] as [number, number, number];
                            newPos[0] = val;
                            updateSelectedObject({ position: newPos });
                          }
                        }}
                        style={{
                          width: 52,
                          padding: '2px 4px',
                          background: '#090d16',
                          border: '1px solid rgba(248, 113, 113, 0.35)',
                          color: '#f87171',
                          borderRadius: 4,
                          fontSize: 10,
                          textAlign: 'right',
                        }}
                      />
                    </div>

                    {/* Y Axis */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span style={{ fontSize: 10, fontWeight: 700, color: '#4ade80', width: 14 }}>Y:</span>
                      <input
                        type="range"
                        min="-15"
                        max="40"
                        step="0.2"
                        value={selectedObject.position[1]}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          const newPos = [...selectedObject.position] as [number, number, number];
                          newPos[1] = val;
                          updateSelectedObject({ position: newPos });
                        }}
                        style={{ flex: 1, accentColor: '#4ade80' }}
                      />
                      <input
                        type="number"
                        step="0.2"
                        value={selectedObject.position[1]}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value);
                          if (!isNaN(val)) {
                            const newPos = [...selectedObject.position] as [number, number, number];
                            newPos[1] = val;
                            updateSelectedObject({ position: newPos });
                          }
                        }}
                        style={{
                          width: 52,
                          padding: '2px 4px',
                          background: '#090d16',
                          border: '1px solid rgba(74, 222, 128, 0.35)',
                          color: '#4ade80',
                          borderRadius: 4,
                          fontSize: 10,
                          textAlign: 'right',
                        }}
                      />
                    </div>

                    {/* Z Axis */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8', width: 14 }}>Z:</span>
                      <input
                        type="range"
                        min="-60"
                        max="60"
                        step="0.5"
                        value={selectedObject.position[2]}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          const newPos = [...selectedObject.position] as [number, number, number];
                          newPos[2] = val;
                          updateSelectedObject({ position: newPos });
                        }}
                        style={{ flex: 1, accentColor: '#38bdf8' }}
                      />
                      <input
                        type="number"
                        step="0.5"
                        value={selectedObject.position[2]}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value);
                          if (!isNaN(val)) {
                            const newPos = [...selectedObject.position] as [number, number, number];
                            newPos[2] = val;
                            updateSelectedObject({ position: newPos });
                          }
                        }}
                        style={{
                          width: 52,
                          padding: '2px 4px',
                          background: '#090d16',
                          border: '1px solid rgba(56, 189, 248, 0.35)',
                          color: '#38bdf8',
                          borderRadius: 4,
                          fontSize: 10,
                          textAlign: 'right',
                        }}
                      />
                    </div>
                  </div>

                  {/* Orientation Toggle for 2D Material Textures */}
                  {(selectedObject.path.toLowerCase().endsWith('.png') ||
                    selectedObject.path.toLowerCase().endsWith('.jpg') ||
                    selectedObject.path.toLowerCase().endsWith('.jpeg') ||
                    selectedObject.path.toLowerCase().endsWith('.webp')) && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '3px 0' }}>
                      <span style={{ fontSize: 10, color: '#94a3b8', width: 55 }}>Kiểu Đặt:</span>
                      <button
                        onClick={() => updateSelectedObject({ orientation: 'horizontal' })}
                        style={{
                          flex: 1,
                          padding: '3px 6px',
                          borderRadius: 4,
                          fontSize: 10,
                          fontWeight: 600,
                          cursor: 'pointer',
                          background:
                            selectedObject.orientation !== 'vertical'
                              ? 'rgba(56, 189, 248, 0.25)'
                              : 'rgba(255,255,255,0.05)',
                          border:
                            selectedObject.orientation !== 'vertical'
                              ? '1px solid #38bdf8'
                              : '1px solid rgba(255,255,255,0.1)',
                          color: selectedObject.orientation !== 'vertical' ? '#38bdf8' : '#94a3b8',
                        }}
                      >
                        🟫 Mặt Sàn (Nằm Ngang)
                      </button>
                      <button
                        onClick={() => updateSelectedObject({ orientation: 'vertical' })}
                        style={{
                          flex: 1,
                          padding: '3px 6px',
                          borderRadius: 4,
                          fontSize: 10,
                          fontWeight: 600,
                          cursor: 'pointer',
                          background:
                            selectedObject.orientation === 'vertical'
                              ? 'rgba(168, 85, 247, 0.25)'
                              : 'rgba(255,255,255,0.05)',
                          border:
                            selectedObject.orientation === 'vertical'
                              ? '1px solid #a855f7'
                              : '1px solid rgba(255,255,255,0.1)',
                          color: selectedObject.orientation === 'vertical' ? '#c084fc' : '#94a3b8',
                        }}
                      >
                        🖼️ Bức Tường (Đứng Thẳng)
                      </button>
                    </div>
                  )}

                  {/* Rotation Y with Slider & Numeric Input */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ fontSize: 10, color: '#94a3b8', width: 55 }}>Góc Xoay:</span>
                    <input
                      type="range"
                      min="0"
                      max="360"
                      value={selectedObject.rotationY}
                      onChange={(e) => updateSelectedObject({ rotationY: parseFloat(e.target.value) || 0 })}
                      style={{ flex: 1, accentColor: '#c084fc' }}
                    />
                    <input
                      type="number"
                      min="0"
                      max="360"
                      value={selectedObject.rotationY}
                      onChange={(e) => {
                        const val = parseFloat(e.target.value);
                        if (!isNaN(val)) updateSelectedObject({ rotationY: val });
                      }}
                      style={{
                        width: 52,
                        padding: '2px 4px',
                        background: '#090d16',
                        border: '1px solid rgba(192, 132, 252, 0.35)',
                        color: '#c084fc',
                        borderRadius: 4,
                        fontSize: 10,
                        textAlign: 'right',
                      }}
                    />
                    <span style={{ fontSize: 10, color: '#c084fc' }}>°</span>
                  </div>

                  {/* Scale with Slider, Number Input, and Quick Presets */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 10, color: '#94a3b8', width: 45 }}>Tỉ Lệ:</span>
                      <input
                        type="range"
                        min="0.05"
                        max="10.0"
                        step="0.05"
                        value={selectedObject.scale}
                        onChange={(e) => updateSelectedObject({ scale: parseFloat(e.target.value) || 0.05 })}
                        style={{ flex: 1, accentColor: '#4ade80' }}
                      />
                      <input
                        type="number"
                        min="0.01"
                        max="100"
                        step="0.1"
                        value={selectedObject.scale}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value);
                          if (!isNaN(val) && val > 0) updateSelectedObject({ scale: val });
                        }}
                        style={{
                          width: 48,
                          padding: '2px 4px',
                          background: '#090d16',
                          border: '1px solid rgba(255,255,255,0.15)',
                          color: '#4ade80',
                          borderRadius: 4,
                          fontSize: 10,
                          textAlign: 'right',
                        }}
                      />
                      <span style={{ fontSize: 10, color: '#4ade80' }}>x</span>
                    </div>
                    {/* Quick Scale Presets */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginLeft: 53, flexWrap: 'wrap' }}>
                      {[0.25, 0.5, 1.0, 2.0, 5.0, 10.0].map((sVal) => (
                        <button
                          key={sVal}
                          onClick={() => updateSelectedObject({ scale: sVal })}
                          style={{
                            padding: '1px 5px',
                            borderRadius: 3,
                            fontSize: 9,
                            cursor: 'pointer',
                            background:
                              Math.abs(selectedObject.scale - sVal) < 0.01
                                ? 'rgba(74, 222, 128, 0.25)'
                                : 'rgba(255,255,255,0.05)',
                            border:
                              Math.abs(selectedObject.scale - sVal) < 0.01
                                ? '1px solid #4ade80'
                                : '1px solid rgba(255,255,255,0.1)',
                            color: Math.abs(selectedObject.scale - sVal) < 0.01 ? '#4ade80' : '#94a3b8',
                          }}
                        >
                          {sVal === 1.0 ? '1.0x (Chuẩn)' : `${sVal}x`}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              ) : (
                <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#64748b', fontSize: 11 }}>
                  Chọn một lớp để chỉnh sửa vị trí và góc xoay
                </div>
              )}
            </div>
          </div>
        )}

        {/* Bottom Actions Bar (Preset Saving & Apply to Scene) */}
        <div
          style={{
            padding: 10,
            borderTop: '1px solid rgba(255,255,255,0.08)',
            background: 'rgba(15, 23, 42, 0.95)',
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            flexShrink: 0,
          }}
        >
          {/* Preset Actions */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 6 }}>
            <div style={{ display: 'flex', gap: 6 }}>
              <button
                onClick={handleSaveMapPreset}
                title="Lưu cấu hình Map này"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '4px 10px',
                  borderRadius: 4,
                  background: 'rgba(16, 185, 129, 0.15)',
                  border: '1px solid rgba(16, 185, 129, 0.4)',
                  color: '#34d399',
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <Save size={12} /> Lưu Cấu Hình
              </button>

              <button
                onClick={handleExportJSON}
                title="Xuất tệp JSON cấu hình map"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '4px 10px',
                  borderRadius: 4,
                  background: 'rgba(56, 189, 248, 0.15)',
                  border: '1px solid rgba(56, 189, 248, 0.4)',
                  color: '#38bdf8',
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <Download size={12} /> Xuất JSON
              </button>

              <button
                onClick={() => jsonImportRef.current?.click()}
                title="Nạp tệp JSON cấu hình map"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '4px 10px',
                  borderRadius: 4,
                  background: 'rgba(168, 85, 247, 0.15)',
                  border: '1px solid rgba(168, 85, 247, 0.4)',
                  color: '#c084fc',
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <Upload size={12} /> Nạp JSON
              </button>
              <input
                ref={jsonImportRef}
                type="file"
                accept=".json"
                style={{ display: 'none' }}
                onChange={handleImportJSON}
              />
            </div>

            {/* Sky Time Buttons */}
            <div style={{ display: 'flex', gap: 4 }}>
              {[
                { id: 'noon', label: '☀️ Trưa' },
                { id: 'dawn', label: '🌅 Sáng' },
                { id: 'sunset', label: '🌇 Chiều' },
                { id: 'night', label: '🌙 Đêm' },
              ].map((s) => (
                <button
                  key={s.id}
                  onClick={() => setSelectedSkyTime(s.id)}
                  style={{
                    padding: '3px 7px',
                    borderRadius: 4,
                    fontSize: 10,
                    fontWeight: 600,
                    cursor: 'pointer',
                    border: selectedSkyTime === s.id ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: selectedSkyTime === s.id ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.04)',
                    color: selectedSkyTime === s.id ? '#38bdf8' : '#94a3b8',
                  }}
                >
                  {s.label}
                </button>
              ))}
            </div>
          </div>

          {/* Apply Button */}
          <button
            onClick={handleApplyToMasterScene}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              padding: '8px 14px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #16a34a, #15803d)',
              color: '#fff',
              fontWeight: 700,
              fontSize: 12,
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 2px 8px rgba(22, 163, 74, 0.3)',
            }}
          >
            <MapIcon size={14} /> 🗺️ Áp Dụng Toàn Bộ Map & Các Lớp Vào Cảnh
          </button>
        </div>
      </div>

      {/* ─── 2. RIGHT: ASSET CATALOG (DYNAMIC-WIDTH VERTICAL TABS) ─── */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
        {/* Top Header & Search Bar */}
        <div
          style={{
            padding: '8px 14px',
            borderBottom: '1px solid rgba(255,255,255,0.06)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(255,255,255,0.02)',
            gap: 10,
            flexShrink: 0,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Layers size={14} color="#4ade80" />
              <span style={{ fontSize: 13, fontWeight: 700, color: '#4ade80' }}>
                Kho Tài Nguyên Bối Cảnh & Map
              </span>
            </div>

            {/* Hide Empty Items (Count = 0) Toggle */}
            <div
              onClick={() => setHideEmptyCategories(!hideEmptyCategories)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '3px 9px',
                borderRadius: 20,
                cursor: 'pointer',
                background: hideEmptyCategories ? 'rgba(74, 222, 128, 0.15)' : 'rgba(255, 255, 255, 0.05)',
                border: `1px solid ${hideEmptyCategories ? 'rgba(74, 222, 128, 0.4)' : 'rgba(255, 255, 255, 0.15)'}`,
                color: hideEmptyCategories ? '#4ade80' : '#94a3b8',
                fontSize: 11,
                fontWeight: 600,
                userSelect: 'none',
                transition: 'all 0.15s ease',
              }}
              title="Bật/Tắt ẩn danh mục không có tài nguyên (số lượng = 0)"
            >
              {hideEmptyCategories ? <EyeOff size={13} color="#4ade80" /> : <Eye size={13} color="#94a3b8" />}
              <span>{hideEmptyCategories ? 'Đang Ẩn mục (0)' : 'Hiện tất cả'}</span>
            </div>

            {toastMessage && (
              <span style={{ fontSize: 11, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
                <CheckCircle size={13} /> {toastMessage}
              </span>
            )}
          </div>

          {/* Search Box */}
          <div style={{ position: 'relative', width: 200 }}>
            <Search size={12} color="#64748b" style={{ position: 'absolute', left: 8, top: '50%', transform: 'translateY(-50%)' }} />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Tìm kiếm tài nguyên..."
              style={{
                width: '100%',
                padding: '4px 8px 4px 26px',
                borderRadius: 6,
                background: '#0f172a',
                border: '1px solid rgba(255,255,255,0.15)',
                color: '#fff',
                fontSize: 11,
                outline: 'none',
              }}
            />
          </div>
        </div>

        {/* Catalog Layout: Single-Column Vertical Tabs + Content Grid */}
        <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
          {/* Single-Column Clean Vertical Sidebar Tabs */}
          <div
            style={{
              width: 140,
              height: '100%',
              flexShrink: 0,
              display: 'flex',
              flexDirection: 'column',
              borderRight: '1px solid rgba(255, 255, 255, 0.08)',
              background: 'rgba(15, 23, 42, 0.95)',
              gap: 4,
              padding: '8px 6px',
              overflowY: 'auto',
              overflowX: 'hidden',
            }}
          >
            {visibleCategories.map((cat) => {
              const isActive = activeCategoryId === cat.id;
              const count = cat.items.length;
              return (
                <button
                  key={cat.id}
                  onClick={() => {
                    setActiveCategoryId(cat.id);
                    setActiveSubCategoryId('all');
                  }}
                  title={cat.label}
                  style={{
                    width: '100%',
                    minHeight: 40,
                    display: 'flex',
                    flexDirection: 'row',
                    alignItems: 'center',
                    gap: 6,
                    padding: '6px 8px',
                    border: 'none',
                    cursor: 'pointer',
                    borderRadius: 6,
                    borderLeft: isActive ? '3px solid #38bdf8' : '3px solid transparent',
                    background: isActive ? 'rgba(56, 189, 248, 0.18)' : 'rgba(255, 255, 255, 0.03)',
                    color: isActive ? '#38bdf8' : count > 0 ? '#cbd5e1' : '#64748b',
                    opacity: count === 0 ? 0.6 : 1.0,
                    transition: 'all 0.15s ease',
                    boxSizing: 'border-box',
                  }}
                >
                  <span style={{ fontSize: 16 }}>{cat.icon}</span>
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
                    <span
                      style={{
                        fontSize: 11,
                        fontWeight: 700,
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        width: '100%',
                        textAlign: 'left',
                      }}
                    >
                      {cat.label}
                    </span>
                    <span style={{ fontSize: 8, color: isActive ? '#38bdf8' : '#64748b', fontWeight: 600 }}>
                      {count} mục
                    </span>
                  </div>
                  {/* Badge */}
                  <span
                    style={{
                      fontSize: 9,
                      fontWeight: 700,
                      padding: '1px 5px',
                      borderRadius: 8,
                      background: count > 0 ? (isActive ? '#38bdf8' : 'rgba(148, 163, 184, 0.25)') : 'rgba(255,255,255,0.06)',
                      color: count > 0 ? (isActive ? '#090d16' : '#cbd5e1') : '#475569',
                    }}
                  >
                    {count}
                  </span>
                </button>
              );
            })}
          </div>

          {/* Content Column */}
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
            {/* Horizontal Sub-Tabs Bar (Only if subcategories exist) */}
            {hasSubCategories && (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '8px 14px',
                  borderBottom: '1px solid rgba(255,255,255,0.06)',
                  background: 'rgba(0,0,0,0.15)',
                  overflowX: 'auto',
                  flexShrink: 0,
                }}
              >
                <button
                  onClick={() => setActiveSubCategoryId('all')}
                  style={{
                    padding: '4px 10px',
                    borderRadius: 14,
                    fontSize: 11,
                    fontWeight: 600,
                    cursor: 'pointer',
                    border: activeSubCategoryId === 'all' ? '1px solid #4ade80' : '1px solid rgba(255,255,255,0.1)',
                    background: activeSubCategoryId === 'all' ? 'rgba(74, 222, 128, 0.2)' : 'rgba(255,255,255,0.04)',
                    color: activeSubCategoryId === 'all' ? '#4ade80' : '#cbd5e1',
                    transition: 'all 0.15s',
                  }}
                >
                  🐾 Tất Cả ({currentCategory?.items.length || 0})
                </button>

                {subCategories.map((sub) => {
                  const isActive = activeSubCategoryId === sub.id;
                  return (
                    <button
                      key={sub.id}
                      onClick={() => setActiveSubCategoryId(sub.id)}
                      style={{
                        padding: '4px 10px',
                        borderRadius: 14,
                        fontSize: 11,
                        fontWeight: 600,
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: 4,
                        border: isActive ? '1px solid #4ade80' : '1px solid rgba(255,255,255,0.1)',
                        background: isActive ? 'rgba(74, 222, 128, 0.2)' : 'rgba(255,255,255,0.04)',
                        color: isActive ? '#4ade80' : '#cbd5e1',
                        transition: 'all 0.15s',
                      }}
                    >
                      {sub.icon && <span>{sub.icon}</span>}
                      <span>{sub.label}</span>
                      <span style={{ fontSize: 9, opacity: 0.7 }}>({sub.items.length})</span>
                    </button>
                  );
                })}
              </div>
            )}

            {/* Items Grid with Drag & Drop */}
            <div style={{ flex: 1, overflowY: 'auto', padding: 14 }}>
              {displayItems.length === 0 ? (
                <div
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    height: '100%',
                    gap: 8,
                    color: '#475569',
                  }}
                >
                  <span style={{ fontSize: 40, opacity: 0.4 }}>{currentCategory?.icon || '📦'}</span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: '#94a3b8' }}>
                    Chưa có tài nguyên trong mục "{currentCategory?.label}"
                  </span>
                  <span style={{ fontSize: 11, color: '#475569' }}>
                    Thả tệp .glb vào thư mục tương ứng trong assets/
                  </span>
                </div>
              ) : (
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))',
                    gap: 10,
                    alignItems: 'stretch',
                  }}
                >
                  {displayItems.map((item, idx) => {
                    const isAlreadyPlaced = placedObjects.some((o) => o.path === item.path);

                    return (
                      <div
                        key={`${item.id}_${item.path || idx}`}
                        draggable={true}
                        onDragStart={(e) => {
                          e.dataTransfer.setData('application/json', JSON.stringify(item));
                          e.dataTransfer.setData('text/plain', JSON.stringify(item));
                        }}
                        onClick={() => handleAddObject(item)}
                        title="Nhấn hoặc Kéo thả vào khung 3D bên trái"
                        style={{
                          background: isAlreadyPlaced ? 'rgba(74, 222, 128, 0.12)' : 'rgba(255,255,255,0.03)',
                          border: isAlreadyPlaced ? '1.5px solid #4ade80' : '1px solid rgba(255,255,255,0.08)',
                          borderRadius: 8,
                          padding: 7,
                          cursor: 'grab',
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'stretch',
                          justifyContent: 'space-between',
                          gap: 6,
                          position: 'relative',
                          transition: 'all 0.15s ease',
                        }}
                      >
                        {/* Placed Indicator Badge */}
                        {isAlreadyPlaced && (
                          <div
                            style={{
                              position: 'absolute',
                              top: 5,
                              left: 5,
                              background: '#16a34a',
                              color: '#fff',
                              fontSize: 8,
                              fontWeight: 700,
                              padding: '1px 5px',
                              borderRadius: 4,
                              zIndex: 2,
                            }}
                          >
                            Đã Trong Map
                          </div>
                        )}

                        {/* Quick Spawn Button */}
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            handleAddObject(item);
                          }}
                          title="Thêm nhanh vào bản đồ"
                          style={{
                            position: 'absolute',
                            top: 5,
                            right: 5,
                            background: '#38bdf8',
                            color: '#000',
                            border: 'none',
                            borderRadius: '50%',
                            width: 18,
                            height: 18,
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            cursor: 'pointer',
                            zIndex: 2,
                            boxShadow: '0 2px 4px rgba(0,0,0,0.5)',
                          }}
                        >
                          <Plus size={11} strokeWidth={3} />
                        </button>

                        {/* Live 3D Thumbnail / 2D Companion Preview */}
                        <div style={{ width: '100%', height: 80, overflow: 'hidden', borderRadius: 6 }}>
                          <Live3DThumbnail
                            assetPath={item.path}
                            previewUrl={item.previewUrl}
                            altText={item.name}
                            fallbackIcon={currentCategory?.icon || '📦'}
                            format={item.format}
                            height={80}
                          />
                        </div>

                        {/* Item Title & Info */}
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 2, textAlign: 'left' }}>
                          <span
                            style={{
                              fontSize: 11,
                              fontWeight: 600,
                              color: isAlreadyPlaced ? '#4ade80' : '#e2e8f0',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                          >
                            {item.name}
                          </span>
                          <span style={{ fontSize: 9, color: '#64748b' }}>
                            {item.format} • {item.sizeMB} MB
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
