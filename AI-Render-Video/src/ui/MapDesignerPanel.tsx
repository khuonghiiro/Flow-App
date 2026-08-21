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
}

export interface MapPresetJSON {
  version: '2.0';
  name: string;
  description?: string;
  sky_time: string;
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
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoadingManifest, setIsLoadingManifest] = useState(true);

  // ─── Environment State ────────────────────────────────────────
  const [selectedSkyTime, setSelectedSkyTime] = useState<string>(
    scene.environment?.sky_time || 'noon'
  );
  const [showFloorGrid, setShowFloorGrid] = useState(true);
  const [isAppliedSuccess, setIsAppliedSuccess] = useState(false);
  const [toastMessage, setToastMessage] = useState('');

  // ─── Placed Objects / Multi-Map Layers State ──────────────────
  const initialBaseMap = scene.environment?.map || 'assets/maps/cathedral.glb';
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

    // ─── Lighting Setup (Natural Sunlight) ──────────────────────
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.25);
    previewScene.add(ambientLight);

    const hemiLight = new THREE.HemisphereLight(0x7dd3fc, 0x1e293b, 1.4);
    previewScene.add(hemiLight);

    const sunLight = new THREE.DirectionalLight(0xfffbeb, 2.6);
    sunLight.position.set(35, 55, 40);
    sunLight.castShadow = true;
    sunLight.shadow.mapSize.width = 2048;
    sunLight.shadow.mapSize.height = 2048;
    sunLight.shadow.camera.near = 0.5;
    sunLight.shadow.camera.far = 400;
    previewScene.add(sunLight);

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
        existingMesh.position.set(obj.position[0], obj.position[1], obj.position[2]);
        existingMesh.rotation.y = (obj.rotationY * Math.PI) / 180;
        existingMesh.scale.set(obj.scale, obj.scale, obj.scale);
        existingMesh.visible = obj.visible;
      } else {
        // Load new 3D model
        if (!obj.path) return;

        // If skybox image, load as background texture
        if (obj.path.endsWith('.png') || obj.path.endsWith('.jpg')) {
          const loader = new THREE.TextureLoader();
          loader.load(obj.path.startsWith('/') ? obj.path : `/${obj.path}`, (tex) => {
            if (sceneRef.current) {
              tex.mapping = THREE.EquirectangularReflectionMapping;
              sceneRef.current.background = tex;
            }
          });
          return;
        }

        AssetLoaderRegistry.loadCharacterPart(obj.path)
          .then((model) => {
            model.traverse((c) => {
              if ((c as THREE.Mesh).isMesh) {
                const mesh = c as THREE.Mesh;
                mesh.castShadow = true;
                mesh.receiveShadow = true;
              }
            });

            model.position.set(obj.position[0], obj.position[1], obj.position[2]);
            model.rotation.y = (obj.rotationY * Math.PI) / 180;
            model.scale.set(obj.scale, obj.scale, obj.scale);
            model.visible = obj.visible;

            group.add(model);
            loadedMeshesMapRef.current.set(obj.instanceId, model);
          })
          .catch((err) => {
            console.warn(`Lỗi tải model 3D cho ${obj.name}:`, err);
          });
      }
    });
  }, [placedObjects]);

  // ─── 4. Add / Place Object Logic (1-Click or Drag & Drop) ─────
  const handleAddObject = (item: MapAssetItem, dropPos: [number, number, number] = [0, 0, 0]) => {
    const newInstance: PlacedObject = {
      instanceId: `inst_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`,
      assetId: item.id,
      name: item.name,
      path: item.path,
      category: item.category,
      position: dropPos,
      rotationY: 0,
      scale: item.category === 'ban_do' || item.category === 'maps' ? 1.0 : 1.0,
      visible: true,
    };

    setPlacedObjects((prev) => [...prev, newInstance]);
    setSelectedInstanceId(newInstance.instanceId);
    triggerToast(`Đã thêm "${item.name}" vào bản đồ tại [${dropPos[0]}, ${dropPos[2]}]`);
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

  const handleSaveMapPreset = () => {
    let name = '';
    try {
      name = prompt('Nhập tên để lưu cấu hình Map này:', 'Bối Cảnh Tùy Chỉnh') || '';
    } catch {}
    if (!name) name = 'Bối Cảnh Mới';

    const newPreset: MapPresetJSON = {
      version: '2.0',
      name,
      sky_time: selectedSkyTime,
      placed_objects: placedObjects,
      created_at: new Date().toISOString(),
    };

    const updatedList = [newPreset, ...presetList.filter((p) => p.name !== name)];
    setPresetList(updatedList);
    try {
      localStorage.setItem('custom_map_designer_presets', JSON.stringify(updatedList));
    } catch {}
    triggerToast(`Đã lưu cấu hình Map "${name}" thành công!`);
  };

  const handleExportJSON = () => {
    const presetData: MapPresetJSON = {
      version: '2.0',
      name: 'Custom_Map_World',
      sky_time: selectedSkyTime,
      placed_objects: placedObjects,
      created_at: new Date().toISOString(),
    };
    const filename = `map_preset_${Date.now()}.json`;
    const blob = new Blob([JSON.stringify(presetData, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
    triggerToast(`Đã xuất tệp ${filename}!`);
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
    const primaryMapPath = primaryMapObj?.path || 'assets/maps/cathedral.glb';

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
  const currentCategory = categories.find((c) => c.id === activeCategoryId);
  const subCategories = currentCategory?.subCategories || [];
  const hasSubCategories = subCategories.length > 0;

  const rawItems =
    activeSubCategoryId === 'all' || !hasSubCategories
      ? currentCategory?.items || []
      : subCategories.find((s) => s.id === activeSubCategoryId)?.items || [];

  const displayItems = rawItems.filter((item) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return item.name.toLowerCase().includes(q) || item.path.toLowerCase().includes(q);
  });

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
        </div>

        {/* ─── Placed Objects / Layers Inspector Drawer ───────── */}
        {showLayersInspector && (
          <div
            style={{
              height: 180,
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
                          updateSelectedObject({ visible: !obj.visible });
                        }}
                        style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 0 }}
                      >
                        {obj.visible ? <Eye size={12} /> : <EyeOff size={12} color="#64748b" />}
                      </button>
                    </div>
                  );
                })}
              </div>

              {/* Transform Controls */}
              {selectedObject ? (
                <div style={{ flex: 1, padding: '8px 12px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {/* Position X, Y, Z */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontSize: 10, color: '#94a3b8', width: 45 }}>Tọa độ:</span>
                    <div style={{ display: 'flex', gap: 4, flex: 1 }}>
                      {['X', 'Y', 'Z'].map((axis, idx) => (
                        <div key={axis} style={{ display: 'flex', alignItems: 'center', gap: 2, flex: 1 }}>
                          <span style={{ fontSize: 9, color: axis === 'X' ? '#f87171' : axis === 'Y' ? '#4ade80' : '#38bdf8' }}>
                            {axis}:
                          </span>
                          <input
                            type="number"
                            step="0.5"
                            value={selectedObject.position[idx]}
                            onChange={(e) => {
                              const val = parseFloat(e.target.value) || 0;
                              const newPos = [...selectedObject.position] as [number, number, number];
                              newPos[idx] = val;
                              updateSelectedObject({ position: newPos });
                            }}
                            style={{
                              width: '100%',
                              padding: '2px 4px',
                              background: '#090d16',
                              border: '1px solid rgba(255,255,255,0.15)',
                              color: '#fff',
                              borderRadius: 4,
                              fontSize: 10,
                            }}
                          />
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Rotation Y & Scale */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontSize: 10, color: '#94a3b8', width: 45 }}>Góc Xoay:</span>
                    <input
                      type="range"
                      min="0"
                      max="360"
                      value={selectedObject.rotationY}
                      onChange={(e) => updateSelectedObject({ rotationY: parseFloat(e.target.value) })}
                      style={{ flex: 1, accentColor: '#38bdf8' }}
                    />
                    <span style={{ fontSize: 10, color: '#38bdf8', width: 35, textAlign: 'right' }}>
                      {selectedObject.rotationY}°
                    </span>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontSize: 10, color: '#94a3b8', width: 45 }}>Tỉ Lệ:</span>
                    <input
                      type="range"
                      min="0.1"
                      max="5"
                      step="0.1"
                      value={selectedObject.scale}
                      onChange={(e) => updateSelectedObject({ scale: parseFloat(e.target.value) })}
                      style={{ flex: 1, accentColor: '#4ade80' }}
                    />
                    <span style={{ fontSize: 10, color: '#4ade80', width: 35, textAlign: 'right' }}>
                      {selectedObject.scale.toFixed(1)}x
                    </span>
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
            gap: 12,
            flexShrink: 0,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Layers size={14} color="#4ade80" />
            <span style={{ fontSize: 13, fontWeight: 700, color: '#4ade80' }}>
              Kho Tài Nguyên Bối Cảnh & Map
            </span>
            {toastMessage && (
              <span style={{ fontSize: 11, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
                <CheckCircle size={13} /> {toastMessage}
              </span>
            )}
          </div>

          {/* Search Box */}
          <div style={{ position: 'relative', width: 220 }}>
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

        {/* Catalog Layout: Dynamic Width Vertical Tabs + Content Grid */}
        <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
          {/* Vertical Tabs Sidebar with DYNAMIC WIDTH */}
          <div
            style={{
              minWidth: 84,
              maxWidth: 130,
              width: 'auto',
              flexShrink: 0,
              display: 'flex',
              flexDirection: 'column',
              borderRight: '1px solid rgba(255,255,255,0.06)',
              background: 'rgba(0,0,0,0.2)',
              overflowY: 'auto',
              overflowX: 'hidden',
              gap: 2,
              padding: '6px 4px',
            }}
          >
            {categories.map((cat) => {
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
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 3,
                    padding: '8px 6px',
                    border: 'none',
                    cursor: 'pointer',
                    borderRadius: 6,
                    borderLeft: isActive ? '3px solid #4ade80' : '3px solid transparent',
                    background: isActive ? 'rgba(74, 222, 128, 0.14)' : 'transparent',
                    color: isActive ? '#4ade80' : '#94a3b8',
                    transition: 'all 0.15s',
                    position: 'relative',
                    textAlign: 'center',
                  }}
                >
                  <span style={{ fontSize: 18, lineHeight: 1 }}>{cat.icon}</span>
                  <span
                    style={{
                      fontSize: 10,
                      fontWeight: 600,
                      lineHeight: 1.2,
                      wordBreak: 'break-word',
                      maxWidth: 110,
                    }}
                  >
                    {cat.label}
                  </span>
                  {/* Badge */}
                  <span
                    style={{
                      position: 'absolute',
                      top: 3,
                      right: 3,
                      fontSize: 9,
                      fontWeight: 700,
                      minWidth: 15,
                      height: 15,
                      lineHeight: '15px',
                      textAlign: 'center',
                      borderRadius: 8,
                      background: count > 0
                        ? (isActive ? '#4ade80' : 'rgba(148, 163, 184, 0.3)')
                        : 'rgba(255,255,255,0.06)',
                      color: count > 0 ? (isActive ? '#000' : '#cbd5e1') : '#475569',
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
                    gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))',
                    gap: 10,
                  }}
                >
                  {displayItems.map((item) => {
                    const isAlreadyPlaced = placedObjects.some((o) => o.path === item.path);

                    return (
                      <div
                        key={item.id}
                        draggable={true}
                        onDragStart={(e) => {
                          e.dataTransfer.setData('application/json', JSON.stringify(item));
                          e.dataTransfer.setData('text/plain', JSON.stringify(item));
                        }}
                        onClick={() => handleAddObject(item)}
                        title="Nhấn hoặc Kéo thả vào khung 3D bên trái"
                        style={{
                          background: isAlreadyPlaced ? 'rgba(74, 222, 128, 0.12)' : 'rgba(255,255,255,0.03)',
                          border: isAlreadyPlaced ? '1px solid #4ade80' : '1px solid rgba(255,255,255,0.08)',
                          borderRadius: 8,
                          padding: 8,
                          cursor: 'grab',
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'center',
                          gap: 6,
                          position: 'relative',
                          transition: 'all 0.15s',
                        }}
                      >
                        {/* Placed Indicator Badge */}
                        {isAlreadyPlaced && (
                          <div
                            style={{
                              position: 'absolute',
                              top: 4,
                              left: 4,
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
                            top: 4,
                            right: 4,
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
                          }}
                        >
                          <Plus size={12} strokeWidth={3} />
                        </button>

                        {/* Thumbnail or Fallback Badge */}
                        <div
                          style={{
                            width: '100%',
                            height: 85,
                            borderRadius: 6,
                            overflow: 'hidden',
                            background: 'rgba(0,0,0,0.35)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                          }}
                        >
                          {item.previewUrl ? (
                            <img
                              src={item.previewUrl}
                              alt={item.name}
                              style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                              onError={(e) => {
                                (e.target as HTMLElement).style.display = 'none';
                              }}
                            />
                          ) : (
                            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
                              <span style={{ fontSize: 28 }}>{currentCategory?.icon || '📦'}</span>
                              <span style={{ fontSize: 9, fontWeight: 700, color: '#38bdf8', background: 'rgba(56, 189, 248, 0.15)', padding: '1px 6px', borderRadius: 4 }}>
                                {item.format}
                              </span>
                            </div>
                          )}
                        </div>

                        {/* Item Title & Info */}
                        <span
                          style={{
                            fontSize: 11,
                            fontWeight: 600,
                            textAlign: 'center',
                            color: isAlreadyPlaced ? '#4ade80' : '#e2e8f0',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            width: '100%',
                          }}
                        >
                          {item.name}
                        </span>

                        <span style={{ fontSize: 9, color: '#64748b' }}>
                          {item.format} • {item.sizeMB} MB
                        </span>
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
