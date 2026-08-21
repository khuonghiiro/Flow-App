/**
 * Live3DThumbnail.tsx
 *
 * Lightweight, high-performance on-demand 3D snapshot thumbnail renderer.
 * When an asset does not have a 2D companion image (*.png), this component:
 * 1. Checks memory cache for previously generated snapshot.
 * 2. Uses a shared headless WebGLRenderer to load the .glb/.vrm model offscreen.
 * 3. Auto-centers and auto-frames the camera to capture a clean 3D preview snapshot.
 * 4. Caches the resulting data URL for instantaneous re-renders without overhead.
 */
import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Loader2 } from 'lucide-react';
import { AssetLoaderRegistry } from '../core/assets/AssetLoaderRegistry';

// Global memory cache for generated 3D snapshots
const snapshotCache = new Map<string, string>();
const loadingPromises = new Map<string, Promise<string>>();

// Shared headless renderer for thumbnail generation
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
    sharedRenderer.toneMappingExposure = 1.35;

    sharedScene = new THREE.Scene();

    // Lighting setup for crisp, studio-quality 3D thumbnail preview
    const amb = new THREE.AmbientLight(0xffffff, 1.4);
    sharedScene.add(amb);

    const dir1 = new THREE.DirectionalLight(0xfffbeb, 2.2);
    dir1.position.set(10, 15, 12);
    sharedScene.add(dir1);

    const dir2 = new THREE.DirectionalLight(0x38bdf8, 1.0);
    dir2.position.set(-10, -5, -8);
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
 * Capture a 3D model snapshot as a lightweight base64 image URL
 */
async function capture3DModelSnapshot(assetPath: string): Promise<string> {
  if (snapshotCache.has(assetPath)) {
    return snapshotCache.get(assetPath)!;
  }

  if (loadingPromises.has(assetPath)) {
    return loadingPromises.get(assetPath)!;
  }

  const promise = (async () => {
    try {
      const { renderer, scene, camera } = getSharedRenderer();

      // Load model clone via registry
      const model = await AssetLoaderRegistry.loadCharacterPart(assetPath);

      // Create isolated preview container
      const container = new THREE.Group();
      container.add(model);
      scene.add(container);

      // Compute bounding box and auto-center
      const bbox = new THREE.Box3().setFromObject(model);
      const size = new THREE.Vector3();
      bbox.getSize(size);
      const center = new THREE.Vector3();
      bbox.getCenter(center);

      // Center model
      model.position.x = -center.x;
      model.position.y = -center.y;
      model.position.z = -center.z;

      // Slight natural 3D isometric angle
      container.rotation.y = Math.PI / 5;
      container.rotation.x = Math.PI / 16;

      // Fit camera distance to bounding sphere
      const maxDim = Math.max(size.x, size.y, size.z) || 1;
      const fov = camera.fov * (Math.PI / 180);
      let cameraZ = Math.abs((maxDim / 2) / Math.tan(fov / 2)) * 1.35;
      cameraZ = Math.max(cameraZ, 0.2);

      camera.position.set(0, 0, cameraZ);
      camera.lookAt(0, 0, 0);
      camera.updateProjectionMatrix();

      // Render 1 frame
      renderer.render(scene, camera);
      const dataUrl = renderer.domElement.toDataURL('image/webp', 0.85);

      // Cleanup
      scene.remove(container);
      container.remove(model);

      snapshotCache.set(assetPath, dataUrl);
      loadingPromises.delete(assetPath);
      return dataUrl;
    } catch (err) {
      console.warn(`Could not generate 3D preview snapshot for ${assetPath}:`, err);
      loadingPromises.delete(assetPath);
      throw err;
    }
  })();

  loadingPromises.set(assetPath, promise);
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
  const [imgSrc, setImgSrc] = useState<string | undefined>(previewUrl);
  const [isGenerating3D, setIsGenerating3D] = useState(false);
  const [hasFailed, setHasFailed] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // If explicit preview URL is provided and valid, use it
    if (previewUrl && !hasFailed) {
      setImgSrc(previewUrl);
      return;
    }

    // Check if snapshot is already cached
    if (assetPath && snapshotCache.has(assetPath)) {
      setImgSrc(snapshotCache.get(assetPath));
      return;
    }

    // Only generate for 3D formats
    const is3DModel =
      assetPath &&
      (assetPath.endsWith('.glb') ||
        assetPath.endsWith('.gltf') ||
        assetPath.endsWith('.vrm') ||
        assetPath.endsWith('.fbx') ||
        assetPath.endsWith('.obj'));

    if (!is3DModel) return;

    // Use IntersectionObserver to lazily load when scrolled into view
    let isMounted = true;
    let observer: IntersectionObserver | null = null;

    const triggerSnapshot = () => {
      setIsGenerating3D(true);
      capture3DModelSnapshot(assetPath)
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
      observer = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting) {
          triggerSnapshot();
          if (containerRef.current) observer?.unobserve(containerRef.current);
        }
      }, { rootMargin: '100px' });

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
            // If the 2D preview image failed, trigger 3D generation fallback
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
