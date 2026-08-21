/**
 * Live3DThumbnail.tsx
 *
 * Lightweight, high-performance on-demand 3D snapshot thumbnail renderer.
 * Multi-Tier Caching Architecture:
 *   Tier 1: In-Memory RAM Cache (Instant synchronous access during runtime)
 *   Tier 2: Persistent IndexedDB Disk Cache (Preserves generated snapshots across reboots & re-launches)
 *   Tier 3: Lazy Headless WebGL Offscreen Snapshot (Only generated ONCE if never cached before)
 */
import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Loader2 } from 'lucide-react';
import { AssetLoaderRegistry } from '../core/assets/AssetLoaderRegistry';

// Global memory cache for generated 3D snapshots (Tier 1)
const snapshotCache = new Map<string, string>();
const loadingPromises = new Map<string, Promise<string>>();

// Persistent IndexedDB Cache Setup (Tier 2)
const DB_NAME = 'flowmy_asset_snapshots_v1';
const STORE_NAME = 'snapshots';

let dbPromise: Promise<IDBDatabase> | null = null;

function getDB(): Promise<IDBDatabase> {
  if (dbPromise) return dbPromise;
  dbPromise = new Promise((resolve, reject) => {
    if (typeof window === 'undefined' || !window.indexedDB) {
      reject(new Error('IndexedDB not supported'));
      return;
    }
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME);
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => {
      dbPromise = null;
      reject(request.error);
    };
  });
  return dbPromise;
}

async function getPersistentSnapshot(key: string): Promise<string | null> {
  try {
    const db = await getDB();
    return new Promise((resolve) => {
      const tx = db.transaction(STORE_NAME, 'readonly');
      const store = tx.objectStore(STORE_NAME);
      const req = store.get(key);
      req.onsuccess = () => resolve(req.result || null);
      req.onerror = () => resolve(null);
    });
  } catch {
    return null;
  }
}

async function savePersistentSnapshot(key: string, dataUrl: string): Promise<void> {
  try {
    const db = await getDB();
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const store = tx.objectStore(STORE_NAME);
    store.put(dataUrl, key);
  } catch (err) {
    console.warn('Could not persist snapshot to IndexedDB:', err);
  }
}

// Shared headless renderer for thumbnail generation (Tier 3)
let sharedRenderer: THREE.WebGLRenderer | null = null;
let sharedScene: THREE.Scene | null = null;
let sharedCamera: THREE.PerspectiveCamera | null = null;

function getSharedRenderer(): {
  renderer: THREE.WebGLRenderer;
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
} {
  if (!sharedRenderer) {
    const canvas = document.createElement('canvas');
    canvas.width = 160;
    canvas.height = 110;

    sharedRenderer = new THREE.WebGLRenderer({
      canvas,
      antialias: true,
      alpha: true,
      preserveDrawingBuffer: true,
      powerPreference: 'low-power',
    });
    sharedRenderer.setSize(160, 110);
    sharedRenderer.toneMapping = THREE.ACESFilmicToneMapping;
    sharedRenderer.toneMappingExposure = 1.4;

    sharedScene = new THREE.Scene();

    // Studio lighting setup for crisp, vivid 3D thumbnail preview
    const amb = new THREE.AmbientLight(0xffffff, 1.5);
    sharedScene.add(amb);

    const hemi = new THREE.HemisphereLight(0xbae6fd, 0x1e293b, 1.2);
    sharedScene.add(hemi);

    const dir1 = new THREE.DirectionalLight(0xfffbeb, 2.5);
    dir1.position.set(12, 18, 15);
    sharedScene.add(dir1);

    const dir2 = new THREE.DirectionalLight(0x38bdf8, 1.2);
    dir2.position.set(-12, -6, -10);
    sharedScene.add(dir2);

    sharedCamera = new THREE.PerspectiveCamera(45, 160 / 110, 0.01, 1000);
  }

  return {
    renderer: sharedRenderer,
    scene: sharedScene!,
    camera: sharedCamera!,
  };
}

/**
 * Capture or retrieve 3D model snapshot with persistent IndexedDB & RAM caching
 */
async function capture3DModelSnapshot(assetPath: string): Promise<string> {
  const cleanKey = assetPath.trim();

  // Tier 1: Check In-Memory RAM Cache
  if (snapshotCache.has(cleanKey)) {
    return snapshotCache.get(cleanKey)!;
  }

  // Check In-Flight Promises to avoid duplicate rendering
  if (loadingPromises.has(cleanKey)) {
    return loadingPromises.get(cleanKey)!;
  }

  const promise = (async () => {
    try {
      // Tier 2: Check Persistent IndexedDB Disk Cache
      const persistentUrl = await getPersistentSnapshot(cleanKey);
      if (persistentUrl) {
        snapshotCache.set(cleanKey, persistentUrl);
        loadingPromises.delete(cleanKey);
        return persistentUrl;
      }

      // Tier 3: Render 3D Model Offscreen Once
      const { renderer, scene, camera } = getSharedRenderer();

      // Load model clone via registry
      const model = await AssetLoaderRegistry.loadCharacterPart(cleanKey);

      // Force DoubleSide and proper visibility on all child meshes
      model.traverse((child) => {
        if ((child as THREE.Mesh).isMesh) {
          const mesh = child as THREE.Mesh;
          mesh.frustumCulled = false;
          if (mesh.material) {
            const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
            mats.forEach((m) => {
              m.side = THREE.DoubleSide;
              m.needsUpdate = true;
            });
          }
        }
      });

      // Create isolated preview container
      const container = new THREE.Group();
      container.add(model);
      scene.add(container);

      // Compute bounding box and auto-center
      const bbox = new THREE.Box3().setFromObject(model, true);
      const size = new THREE.Vector3();
      bbox.getSize(size);
      const center = new THREE.Vector3();
      bbox.getCenter(center);

      // Center model at origin
      model.position.x = -center.x;
      model.position.y = -center.y;
      model.position.z = -center.z;

      // Slight natural 3D isometric angle
      container.rotation.y = Math.PI / 5.5;
      container.rotation.x = Math.PI / 16;

      // Fit camera distance to bounding sphere
      const maxDim = Math.max(size.x, size.y, size.z, 0.5);
      const fov = camera.fov * (Math.PI / 180);
      let cameraZ = Math.abs((maxDim / 2) / Math.tan(fov / 2)) * 1.45;
      if (!isFinite(cameraZ) || cameraZ < 0.2) cameraZ = 1.5;

      camera.position.set(0, 0, cameraZ);
      camera.lookAt(0, 0, 0);
      camera.updateProjectionMatrix();

      // Render 1 frame
      renderer.render(scene, camera);
      const dataUrl = renderer.domElement.toDataURL('image/webp', 0.85);

      // Cleanup
      scene.remove(container);
      container.remove(model);

      // Save to RAM Cache & Persistent Disk Cache
      snapshotCache.set(cleanKey, dataUrl);
      savePersistentSnapshot(cleanKey, dataUrl);

      loadingPromises.delete(cleanKey);
      return dataUrl;
    } catch (err) {
      console.warn(`Could not generate 3D preview snapshot for ${cleanKey}:`, err);
      loadingPromises.delete(cleanKey);
      throw err;
    }
  })();

  loadingPromises.set(cleanKey, promise);
  return promise;
}

export interface Live3DThumbnailProps {
  assetPath: string;
  previewUrl?: string;
  altText?: string;
  fallbackIcon?: string;
  format?: string;
  height?: number | string;
}

export const Live3DThumbnail: React.FC<Live3DThumbnailProps> = ({
  assetPath,
  previewUrl,
  altText = 'Tài nguyên 3D',
  fallbackIcon = '📦',
  format = 'GLB',
  height = 85,
}) => {
  const [imgSrc, setImgSrc] = useState<string | undefined>(previewUrl || undefined);
  const [isGenerating3D, setIsGenerating3D] = useState(false);
  const [hasFailed, setHasFailed] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Reset state if asset changes
    setHasFailed(false);
  }, [assetPath, previewUrl]);

  useEffect(() => {
    const cleanKey = (assetPath || '').trim();
    if (!cleanKey) return;

    const p = cleanKey.toLowerCase();
    const isImage =
      p.endsWith('.png') ||
      p.endsWith('.jpg') ||
      p.endsWith('.jpeg') ||
      p.endsWith('.webp') ||
      p.endsWith('.gif') ||
      p.endsWith('.bmp');

    // If it's a 2D image (skybox, background, texture), load directly as image
    if (isImage) {
      const url = cleanKey.startsWith('/') || cleanKey.startsWith('http') ? cleanKey : `/${cleanKey}`;
      setImgSrc(url);
      return;
    }

    // If explicit preview URL is provided and has not failed, use it
    if (previewUrl && previewUrl.trim() !== '' && !hasFailed) {
      const pUrl = previewUrl.startsWith('/') || previewUrl.startsWith('http') ? previewUrl : `/${previewUrl}`;
      setImgSrc(pUrl);
      return;
    }

    // 1. Check RAM Cache (Instant)
    if (snapshotCache.has(cleanKey)) {
      setImgSrc(snapshotCache.get(cleanKey));
      return;
    }

    // 2. Check Persistent IndexedDB Cache (Fast)
    let isMounted = true;
    getPersistentSnapshot(cleanKey).then((cached) => {
      if (!isMounted) return;
      if (cached) {
        snapshotCache.set(cleanKey, cached);
        setImgSrc(cached);
      }
    });

    // Only generate for 3D model formats (including folder-bundle paths like scene.gltf)
    const is3DModel =
      p.endsWith('.glb') ||
      p.endsWith('.gltf') ||
      p.endsWith('.vrm') ||
      p.endsWith('.fbx') ||
      p.endsWith('.obj') ||
      p.endsWith('.dae');

    if (!is3DModel) return;

    let observer: IntersectionObserver | null = null;

    const triggerSnapshot = () => {
      // If already resolved by IndexedDB, do not re-render
      if (snapshotCache.has(cleanKey)) {
        setImgSrc(snapshotCache.get(cleanKey));
        return;
      }

      setIsGenerating3D(true);
      capture3DModelSnapshot(cleanKey)
        .then((url) => {
          if (isMounted) {
            setImgSrc(url);
            setIsGenerating3D(false);
          }
        })
        .catch(() => {
          if (isMounted) {
            setIsGenerating3D(false);
            setHasFailed(true);
          }
        });
    };

    if (containerRef.current && window.IntersectionObserver) {
      observer = new IntersectionObserver(
        (entries) => {
          if (entries[0].isIntersecting) {
            triggerSnapshot();
            if (containerRef.current) observer?.unobserve(containerRef.current);
          }
        },
        { rootMargin: '120px' }
      );

      observer.observe(containerRef.current);
    } else {
      triggerSnapshot();
    }

    return () => {
      isMounted = false;
      if (observer && containerRef.current) {
        observer.unobserve(containerRef.current);
      }
    };
  }, [assetPath, previewUrl, hasFailed]);

  return (
    <div
      ref={containerRef}
      style={{
        width: '100%',
        height,
        borderRadius: 6,
        overflow: 'hidden',
        background: 'rgba(0,0,0,0.4)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
      }}
    >
      {imgSrc ? (
        <img
          src={imgSrc}
          alt={altText}
          onError={() => {
            // If the 2D preview image failed (404), trigger 3D generation fallback
            if (previewUrl && imgSrc === previewUrl) {
              setImgSrc(undefined);
              setHasFailed(true);
            }
          }}
          style={{
            width: '100%',
            height: '100%',
            objectFit: 'contain',
            transition: 'opacity 0.2s ease-in-out',
          }}
        />
      ) : isGenerating3D ? (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
          <Loader2 size={18} color="#38bdf8" style={{ animation: 'spin 1s linear infinite' }} />
          <span style={{ fontSize: 9, color: '#38bdf8', fontWeight: 600 }}>Đang nạp 3D...</span>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
          <span style={{ fontSize: 28 }}>{fallbackIcon}</span>
          <span
            style={{
              fontSize: 9,
              fontWeight: 700,
              color: '#38bdf8',
              background: 'rgba(56, 189, 248, 0.15)',
              padding: '1px 6px',
              borderRadius: 4,
            }}
          >
            {format}
          </span>
        </div>
      )}
    </div>
  );
};
