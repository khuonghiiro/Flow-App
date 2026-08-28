import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js';
import * as SkeletonUtils from 'three/examples/jsm/utils/SkeletonUtils.js';
import { VolumetricCloudLighting } from '../weather/VolumetricCloudLighting';

export class AssetLoaderRegistry {
  private static gltfLoader: GLTFLoader | null = null;
  private static fbxLoader: FBXLoader | null = null;
  private static objLoader: OBJLoader | null = null;
  private static modelCache = new Map<string, THREE.Group>();
  private static textureBlobMap = new Map<string, { filename: string; url: string; tokens: string[] }>();

  private static tokenize(str: string): string[] {
    return (str || '')
      .toLowerCase()
      .replace(/[^a-z0-9]/g, ' ')
      .split(/\s+/)
      .filter((t) => t && t !== 'mi' && t !== 'm' && t !== 'mat' && t !== 'material' && t !== 't' && t !== 'd' && t !== 'd1' && t !== 'tex' && t !== 'texture' && t !== 'png' && t !== 'jpg' && t !== 'jpeg');
  }

  public static registerTextureBlob(filename: string, blobUrl: string): void {
    const clean = filename.toLowerCase().trim();
    const basename = clean.split(/[/\\]/).pop() || '';
    const tokens = this.tokenize(basename);
    const entry = { filename: basename, url: blobUrl, tokens };

    this.textureBlobMap.set(clean, entry);
    if (basename) this.textureBlobMap.set(basename, entry);
    const nameWithoutExt = basename.substring(0, basename.lastIndexOf('.')) || basename;
    if (nameWithoutExt) this.textureBlobMap.set(nameWithoutExt, entry);
  }

  public static getTextureBlob(filename: string): string | undefined {
    const clean = (filename || '').toLowerCase().trim();
    if (!clean) return undefined;
    const basename = clean.split(/[/\\]/).pop() || '';
    const nameWithoutExt = basename.substring(0, basename.lastIndexOf('.')) || basename;

    // 1. Direct match
    if (this.textureBlobMap.has(clean)) return this.textureBlobMap.get(clean)!.url;
    if (this.textureBlobMap.has(basename)) return this.textureBlobMap.get(basename)!.url;
    if (this.textureBlobMap.has(nameWithoutExt)) return this.textureBlobMap.get(nameWithoutExt)!.url;

    // 2. Semantic token matching
    return this.findBestTextureMatch(basename);
  }

  /**
   * Blender-grade Semantic Material-to-Texture Matcher
   * Calculates token overlap and domain heuristics (hair, eyes, face, swimsuit, cloth)
   * to guarantee 100% accurate texture attachment.
   */
  public static findBestTextureMatch(matName: string, meshName: string = ''): string | undefined {
    if (this.textureBlobMap.size === 0) return undefined;

    const uniqueEntries = Array.from(
      new Map(Array.from(this.textureBlobMap.values()).map((e) => [e.url, e])).values()
    );

    if (uniqueEntries.length === 1) {
      return uniqueEntries[0].url;
    }

    const queryTokens = [...this.tokenize(matName), ...this.tokenize(meshName)];
    const lowerMat = (matName || '').toLowerCase();
    const lowerMesh = (meshName || '').toLowerCase();

    let bestScore = 0;
    let bestUrl: string | undefined = undefined;

    for (const entry of uniqueEntries) {
      let score = 0;
      const texTokens = entry.tokens;
      const lowerTex = entry.filename.toLowerCase();

      for (const qt of queryTokens) {
        if (texTokens.includes(qt)) {
          score += 4;
        } else if (texTokens.some((tt) => tt.includes(qt) || qt.includes(tt))) {
          score += 2;
        }
      }

      // Domain heuristics for anime & game characters
      if (lowerMat.includes('jiemao') && (lowerTex.includes('face') || lowerTex.includes('eyes'))) score += 6;
      if (lowerMat.includes('gaoguang') && (lowerTex.includes('face') || lowerTex.includes('eyes'))) score += 6;
      if (lowerMat.includes('hair_2') && lowerTex.includes('hair_02')) score += 10;
      if (lowerMat.includes('hair_1') && lowerTex.includes('hair_01')) score += 10;
      if (lowerMat.includes('hair') && lowerTex.includes('hair')) score += 5;
      if ((lowerMat.includes('eye') || lowerMesh.includes('eye')) && lowerTex.includes('eye')) score += 6;
      if ((lowerMat.includes('face') || lowerMesh.includes('face') || lowerMat.includes('head')) && (lowerTex.includes('face') || lowerTex.includes('mask'))) score += 6;
      if (lowerMat.includes('swimsuit_04') && lowerTex.includes('swimsuit_02')) score += 8;

      if (score > bestScore) {
        bestScore = score;
        bestUrl = entry.url;
      }
    }

    return bestScore >= 2 ? bestUrl : undefined;
  }

  public static hasRegisteredTextures(): boolean {
    return this.textureBlobMap.size > 0;
  }

  public static getRegisteredTextureUrls(): string[] {
    return Array.from(new Set(Array.from(this.textureBlobMap.values()).map((e) => e.url)));
  }

  /**
   * Apply all available textures onto a 3D model using Semantic Material-to-Texture matching
   */
  public static applyTexturesToGroup(group: THREE.Group, textureBlobUrls?: string[]): void {
    if (!group) return;

    if (textureBlobUrls && textureBlobUrls.length > 0) {
      textureBlobUrls.forEach((url, idx) => {
        AssetLoaderRegistry.registerTextureBlob(`texture_${idx}.png`, url);
      });
    }

    const texLoader = new THREE.TextureLoader();
    const loadedTextureCache = new Map<string, THREE.Texture>();

    const getOrLoadTexture = (url: string): THREE.Texture => {
      if (loadedTextureCache.has(url)) return loadedTextureCache.get(url)!;
      const tex = texLoader.load(url);
      tex.colorSpace = THREE.SRGBColorSpace;
      tex.minFilter = THREE.LinearMipmapLinearFilter;
      tex.magFilter = THREE.LinearFilter;
      tex.wrapS = THREE.RepeatWrapping;
      tex.wrapT = THREE.RepeatWrapping;
      loadedTextureCache.set(url, tex);
      return tex;
    };

    group.traverse((c) => {
      if ((c as THREE.Mesh).isMesh) {
        const mesh = c as THREE.Mesh;
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        mats.forEach((mat) => {
          if (!mat) return;
          const m = mat as any;
          const bestUrl = AssetLoaderRegistry.findBestTextureMatch(m.name || '', mesh.name || '');

          if (bestUrl) {
            const tex = getOrLoadTexture(bestUrl);
            m.map = tex;
            if (m.color) m.color.setHex(0xffffff);
            m.side = THREE.DoubleSide;
            m.alphaTest = 0.1;
            m.depthWrite = true;
            m.transparent = true;
            m.needsUpdate = true;
          }
        });
      }
    });
  }

  public static getGLTFLoader(): GLTFLoader {
    if (!this.gltfLoader) {
      this.gltfLoader = new GLTFLoader();
    }
    return this.gltfLoader;
  }

  public static getFBXLoader(): FBXLoader {
    if (!this.fbxLoader) {
      this.fbxLoader = new FBXLoader();
    }
    return this.fbxLoader;
  }

  public static getOBJLoader(): OBJLoader {
    if (!this.objLoader) {
      this.objLoader = new OBJLoader();
    }
    return this.objLoader;
  }

  public static clearModelCache(url?: string): void {
    if (url) {
      const cleanUrl = url.startsWith('http://') || url.startsWith('https://') 
        ? url 
        : (url.startsWith('/') ? url : `/${url}`);
      this.modelCache.delete(cleanUrl);
    } else {
      this.modelCache.clear();
    }
  }

  /**
   * Universal 3D Model Loader:
   * Supports .glb, .vrm, .gltf (including multi-file folder bundles like medieval_fantasy_book/scene.gltf),
   * .fbx, and .obj formats with memory caching and clone isolation.
   */
  public static async loadModel(url: string): Promise<THREE.Group> {
    const isBlobOrData = url.startsWith('blob:') || url.startsWith('data:');
    const cleanUrl = isBlobOrData || url.startsWith('http://') || url.startsWith('https://') 
      ? url 
      : (url.startsWith('/') ? url : `/${url}`);

    if (this.modelCache.has(cleanUrl) && !isBlobOrData) {
      const cached = this.modelCache.get(cleanUrl)!;
      const clone = SkeletonUtils.clone(cached) as THREE.Group;
      clone.animations = cached.animations || [];
      return clone;
    }

    const [fetchUrl] = cleanUrl.split('#');
    const lower = cleanUrl.toLowerCase();

    // 1. FBX Model Loader — with robust texture path remap, texture load synchronization & auto-scale (cm → m)
    if (lower.endsWith('.fbx') || lower.includes('.fbx')) {
      const basePath = cleanUrl.substring(0, cleanUrl.lastIndexOf('/') + 1);
      const manager = new THREE.LoadingManager();

      manager.setURLModifier((u) => {
        if (u.startsWith('blob:') || u.startsWith('data:')) {
          return u;
        }
        const decoded = decodeURIComponent(u).split('?')[0];
        const filename = decoded.split(/[/\\]/).pop() || '';
        if (!filename) return u;

        // 1. Check registered in-memory blob textures from multi-file upload
        const matchedBlob = AssetLoaderRegistry.getTextureBlob(filename);
        if (matchedBlob) return matchedBlob;

        // 2. Remap to the model's base folder, textures subfolder, or sibling textures folder
        if (!cleanUrl.startsWith('blob:') && !cleanUrl.startsWith('data:')) {
          if (basePath.endsWith('/source/')) {
            const parentPath = basePath.substring(0, basePath.length - 7);
            return `${parentPath}textures/${encodeURIComponent(filename)}`;
          }
          return `${basePath}${encodeURIComponent(filename)}`;
        }

        return u;
      });

      const fbxLoader = new FBXLoader(manager);
      return new Promise((resolve, reject) => {
        let loadedFbx: THREE.Group | null = null;
        let isManagerDone = false;
        let isResolved = false;

        const finalizeAndResolve = () => {
          if (!loadedFbx || !isManagerDone || isResolved) return;
          isResolved = true;

          const textureLoader = new THREE.TextureLoader();

          loadedFbx.traverse((child) => {
            if ((child as THREE.Mesh).isMesh) {
              const mesh = child as THREE.Mesh;
              mesh.castShadow = true;
              mesh.receiveShadow = true;
              mesh.frustumCulled = false;

              if (mesh.geometry) {
                if (!mesh.geometry.attributes.normal) {
                  mesh.geometry.computeVertexNormals();
                }
                if (mesh.geometry.attributes.color) {
                  mesh.geometry.deleteAttribute('color');
                }
                mesh.geometry.computeBoundingBox();
                mesh.geometry.computeBoundingSphere();
              }

              if (mesh.material) {
                const rawMats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
                const convertedMats = rawMats.map((mat) => {
                  if (!mat) return mat;
                  const m = mat as any;

                  // Convert legacy MeshPhongMaterial to MeshStandardMaterial
                  const stdMat = new THREE.MeshStandardMaterial({
                    name: m.name || `Mat_${mesh.name}`,
                    map: m.map || null,
                    normalMap: m.normalMap || null,
                    bumpMap: m.bumpMap || null,
                    alphaMap: m.alphaMap || null,
                    transparent: m.transparent || (m.opacity !== undefined && m.opacity < 0.98),
                    opacity: m.opacity ?? 1.0,
                    alphaTest: 0.1,
                    side: THREE.DoubleSide,
                    shadowSide: THREE.DoubleSide,
                    roughness: 0.55,
                    metalness: 0.0,
                    vertexColors: false,
                    depthTest: true,
                    depthWrite: true,
                  });

                  if (!stdMat.map && AssetLoaderRegistry.hasRegisteredTextures()) {
                    const matchedUrl = AssetLoaderRegistry.findBestTextureMatch(m.name || '', mesh.name || '');
                    if (matchedUrl) {
                      const tex = textureLoader.load(matchedUrl, () => {
                        tex.colorSpace = THREE.SRGBColorSpace;
                        stdMat.needsUpdate = true;
                      });
                      tex.colorSpace = THREE.SRGBColorSpace;
                      tex.minFilter = THREE.LinearMipmapLinearFilter;
                      tex.magFilter = THREE.LinearFilter;
                      stdMat.map = tex;
                    }
                  }

                  if (stdMat.map) {
                    stdMat.color.setHex(0xffffff);
                    stdMat.map.colorSpace = THREE.SRGBColorSpace;
                    stdMat.map.minFilter = THREE.LinearMipmapLinearFilter;
                    stdMat.map.magFilter = THREE.LinearFilter;
                    stdMat.map.needsUpdate = true;
                  } else if (m.color) {
                    if (m.color.r === 0 && m.color.g === 0 && m.color.b === 0) {
                      stdMat.color.setHex(0xd0d0d0);
                    } else {
                      stdMat.color.copy(m.color);
                    }
                  } else {
                    stdMat.color.setHex(0xffffff);
                  }

                  if (m.emissive && (m.emissive.r > 0 || m.emissive.g > 0 || m.emissive.b > 0)) {
                    stdMat.emissive.copy(m.emissive);
                    stdMat.emissiveIntensity = m.emissiveIntensity ?? 1.0;
                  }

                  stdMat.needsUpdate = true;
                  return stdMat;
                });

                mesh.material = Array.isArray(mesh.material) ? convertedMats : convertedMats[0];
              }
            }
          });

          // Auto-scale: FBX files often use cm units (100x larger than GLB's meters)
          const bbox = new THREE.Box3().setFromObject(loadedFbx);
          const size = new THREE.Vector3();
          bbox.getSize(size);
          const maxDim = Math.max(size.x, size.y, size.z);
          if (maxDim > 10) {
            const targetSize = 1.8; // ~human height in meters
            const scaleFactor = targetSize / maxDim;
            loadedFbx.scale.multiplyScalar(scaleFactor);
          }

          this.modelCache.set(cleanUrl, loadedFbx);
          const initialClone = SkeletonUtils.clone(loadedFbx) as THREE.Group;
          initialClone.animations = loadedFbx.animations || [];
          resolve(initialClone);
        };

        manager.onLoad = () => {
          isManagerDone = true;
          finalizeAndResolve();
        };

        // Safety fallback timer if there are no external textures or manager fires immediately
        setTimeout(() => {
          isManagerDone = true;
          finalizeAndResolve();
        }, 1200);

        fbxLoader.load(
          fetchUrl,
          (fbx) => {
            loadedFbx = fbx;
            finalizeAndResolve();
          },
          undefined,
          (err) => {
            console.warn(`[AssetLoaderRegistry] Lỗi tải model FBX từ ${fetchUrl}:`, err);
            reject(err);
          }
        );
      });
    }

    // 2. OBJ Model Loader
    if (lower.endsWith('.obj') || lower.includes('.obj')) {
      const objLoader = this.getOBJLoader();
      return new Promise((resolve, reject) => {
        objLoader.load(
          fetchUrl,
          (obj) => {
            const group = new THREE.Group();
            group.add(obj);
            group.traverse((child) => {
              if ((child as THREE.Mesh).isMesh) {
                const mesh = child as THREE.Mesh;
                mesh.castShadow = true;
                mesh.receiveShadow = true;
                mesh.frustumCulled = false;
              }
            });
            this.modelCache.set(cleanUrl, group);
            resolve(group.clone(true));
          },
          undefined,
          (err) => {
            console.warn(`[AssetLoaderRegistry] Lỗi tải model OBJ từ ${fetchUrl}:`, err);
            reject(err);
          }
        );
      });
    }

    // 3. GLTF / GLB / VRM / Folder Bundle Loader (.gltf + .bin + textures)
    const loader = this.getGLTFLoader();
    return new Promise((resolve, reject) => {
      loader.load(
        fetchUrl,
        (gltf) => {
          const model = gltf.scene;

          model.traverse((child) => {
            if (child.matrix && !child.matrixAutoUpdate) {
              child.position.setFromMatrixPosition(child.matrix);
              child.quaternion.setFromRotationMatrix(child.matrix);
              child.scale.setFromMatrixScale(child.matrix);
              child.matrixAutoUpdate = true;
            }

            if ((child as THREE.Mesh).isMesh) {
              const mesh = child as THREE.Mesh;
              mesh.castShadow = true; // Solid walls, roofs, and pillars cast shadows to block sun & lightning from penetrating
              mesh.receiveShadow = true;
              mesh.frustumCulled = false; // Keep map visible at all times, prevent chunk culling disappearance

              const mats = mesh.material ? (Array.isArray(mesh.material) ? mesh.material : [mesh.material]) : [];
              const hasTransparentMat = mats.some(
                (m) => m && (m.transparent || m.name === 'transparent' || (m as any).alphaMode === 'BLEND')
              );

              // Filter out fake artificial polygon godray / sunbeam quads (ONLY on transparent/alpha-blend materials)
              // Solid terrain, large vehicles, props, and character bodies are 100% untouched and safe!
              if (hasTransparentMat) {
                const geom = mesh.geometry;
                if (geom && geom.attributes.position && geom.index) {
                  const pos = geom.attributes.position;
                  const index = geom.index;
                  const indices = index.array;
                  const newIndices: number[] = [];
                  let removedHugeCount = 0;

                  for (let i = 0; i < indices.length; i += 3) {
                    const i1 = indices[i];
                    const i2 = indices[i + 1];
                    const i3 = indices[i + 2];

                    const x1 = pos.getX(i1), y1 = pos.getY(i1), z1 = pos.getZ(i1);
                    const x2 = pos.getX(i2), y2 = pos.getY(i2), z2 = pos.getZ(i2);
                    const x3 = pos.getX(i3), y3 = pos.getY(i3), z3 = pos.getZ(i3);

                    const d12 = Math.hypot(x2 - x1, y2 - y1, z2 - z1);
                    const d23 = Math.hypot(x3 - x2, y3 - y2, z3 - z2);
                    const d31 = Math.hypot(x1 - x3, y1 - y3, z1 - z3);
                    const maxEdge = Math.max(d12, d23, d31);

                    // Standard voxel structures have block edge lengths 1.0 to 8.0.
                    // Fake baked sunbeam polygon quads spanning the nave have gigantic edge lengths > 25.0 (up to 1200 units)!
                    if (maxEdge > 25.0) {
                      removedHugeCount++;
                      continue; // Discard fake white sunbeam quad!
                    }

                    newIndices.push(i1, i2, i3);
                  }

                  if (removedHugeCount > 0) {
                    geom.setIndex(newIndices);
                    geom.computeVertexNormals();
                  }
                }
              }

              if (mesh.material) {
                mats.forEach((mat) => {
                  const isTransparent =
                    mat.transparent ||
                    mat.name === 'transparent' ||
                    Boolean((mat as any).alphaMode === 'BLEND');

                  mat.depthTest = true;
                  mat.depthWrite = true; // Always write depth so geometry is 100% solid and occludes interiors

                  if (isTransparent) {
                    // Alpha cutout rendering: MUST be transparent=false + alphaTest to render in Opaque Pass
                    // alphaTest=0.25 cleanly discards fake polygon sunbeam quads (which have alpha < 0.1) while preserving 100% of leaves, stained glass, chandeliers, torches, and lanterns
                    mat.transparent = false;
                    mat.alphaTest = 0.25;
                    mat.side = THREE.DoubleSide;
                  } else {
                    mat.transparent = false;
                    mat.alphaTest = 0.0;
                    mat.side = THREE.FrontSide;
                  }

                  if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
                    const stdMat = mat as THREE.MeshStandardMaterial;

                    // Cache original authored emissive/color values from GLB
                    if (stdMat.userData.origEmissiveMap === undefined) {
                      stdMat.userData.origEmissiveMap = stdMat.emissiveMap;
                      stdMat.userData.origMap = stdMat.map;
                      stdMat.userData.origColor = stdMat.color ? stdMat.color.clone() : new THREE.Color(0xffffff);
                    }

                    if (isTransparent) {
                      // Foliage, leaves, stained glass, torches, candles, chandeliers, water:
                      // If model stored RGB color in emissiveMap (texture 2) and alpha mask in map (texture 1):
                      if (stdMat.emissiveMap) {
                        if (stdMat.map && stdMat.map !== stdMat.emissiveMap) {
                          stdMat.alphaMap = stdMat.map; // Texture 1 is the Alpha Cutout Mask
                        }
                        stdMat.map = stdMat.emissiveMap; // Texture 2 is the true full-color RGB diffuse map!
                      }
                      
                      // Only fix black multiplier bug if authored color was black
                      if (stdMat.color && stdMat.color.r === 0 && stdMat.color.g === 0 && stdMat.color.b === 0) {
                        stdMat.color.setHex(0xffffff);
                      }
                      
                      stdMat.transparent = false;
                      stdMat.alphaTest = 0.25;
                      stdMat.depthWrite = true;
                      stdMat.depthTest = true;
                      stdMat.side = THREE.DoubleSide;

                      // Emissive: 0 in dynamic lighting so cloud shadows and sun cast on them naturally
                      stdMat.emissive.setHex(0x000000);
                      stdMat.emissiveIntensity = 0.0;
                    } else {
                      // Solid surface materials (stone walls, pillars, roofs, props, vehicles):
                      if (stdMat.emissiveMap && !stdMat.map) {
                        stdMat.map = stdMat.emissiveMap;
                      }
                      
                      // Only fix black multiplier bug if authored color was black with a texture
                      if (stdMat.color && stdMat.color.r === 0 && stdMat.color.g === 0 && stdMat.color.b === 0 && stdMat.map) {
                        stdMat.color.setHex(0xffffff);
                      }
                      
                      stdMat.transparent = false;
                      stdMat.alphaTest = 0.0;
                      stdMat.depthWrite = true;
                      stdMat.depthTest = true;
                      stdMat.side = THREE.FrontSide;
                      stdMat.emissive.setHex(0x000000);
                      stdMat.emissiveIntensity = 0.0;
                    }

                    stdMat.roughness = Math.max(0.65, stdMat.roughness || 0.65);
                    stdMat.metalness = Math.min(0.15, stdMat.metalness || 0.0);
                  }
                });
              }
            }
          });

          model.animations = gltf.animations || [];
          VolumetricCloudLighting.applyToScene(model);
          this.modelCache.set(cleanUrl, model);
          const initialClone = SkeletonUtils.clone(model) as THREE.Group;
          initialClone.animations = model.animations || [];
          resolve(initialClone);
        },
        undefined,
        (err) => {
          console.warn(`[AssetLoaderRegistry] Lỗi tải model GLTF từ ${cleanUrl}:`, err);
          reject(err);
        }
      );
    });
  }

  public static async loadGLTF(url: string): Promise<THREE.Group> {
    return this.loadModel(url);
  }

  /**
   * Tải mô hình nhân vật hoặc bộ phận modular (quần áo, mặt, tóc, thân người)
   */
  public static async loadCharacterPart(url: string): Promise<THREE.Group> {
    return this.loadModel(url);
  }

  public static async loadGLTFFromBuffer(buffer: ArrayBuffer): Promise<THREE.Group> {
    const loader = this.getGLTFLoader();
    return new Promise((resolve, reject) => {
      loader.parse(
        buffer,
        '',
        (gltf) => {
          const model = gltf.scene;

          model.traverse((child) => {
            if (child.matrix && !child.matrixAutoUpdate) {
              child.position.setFromMatrixPosition(child.matrix);
              child.quaternion.setFromRotationMatrix(child.matrix);
              child.scale.setFromMatrixScale(child.matrix);
              child.matrixAutoUpdate = true;
            }

            if ((child as THREE.Mesh).isMesh) {
              const mesh = child as THREE.Mesh;
              mesh.castShadow = false;
              mesh.receiveShadow = true;
              mesh.frustumCulled = false;

              const mats = mesh.material ? (Array.isArray(mesh.material) ? mesh.material : [mesh.material]) : [];
              const hasTransparentMat = mats.some(
                (m) => m && (m.transparent || m.name === 'transparent' || (m as any).alphaMode === 'BLEND')
              );

              if (hasTransparentMat) {
                const geom = mesh.geometry;
                if (geom && geom.attributes.position && geom.index) {
                  const pos = geom.attributes.position;
                  const index = geom.index;
                  const indices = index.array;
                  const newIndices: number[] = [];
                  let removedHugeCount = 0;

                  for (let i = 0; i < indices.length; i += 3) {
                    const i1 = indices[i];
                    const i2 = indices[i + 1];
                    const i3 = indices[i + 2];

                    const x1 = pos.getX(i1), y1 = pos.getY(i1), z1 = pos.getZ(i1);
                    const x2 = pos.getX(i2), y2 = pos.getY(i2), z2 = pos.getZ(i2);
                    const x3 = pos.getX(i3), y3 = pos.getY(i3), z3 = pos.getZ(i3);

                    const d12 = Math.hypot(x2 - x1, y2 - y1, z2 - z1);
                    const d23 = Math.hypot(x3 - x2, y3 - y2, z3 - z2);
                    const d31 = Math.hypot(x1 - x3, y1 - y3, z1 - z3);
                    const maxEdge = Math.max(d12, d23, d31);

                    if (maxEdge > 25.0) {
                      removedHugeCount++;
                      continue;
                    }

                    newIndices.push(i1, i2, i3);
                  }

                  if (removedHugeCount > 0) {
                    geom.setIndex(newIndices);
                    geom.computeVertexNormals();
                  }
                }
              }

              if (mesh.material) {
                mats.forEach((mat) => {
                  const isTransparent =
                    mat.transparent ||
                    mat.name === 'transparent' ||
                    Boolean((mat as any).alphaMode === 'BLEND');

                  mat.depthTest = true;
                  mat.depthWrite = true;

                  if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
                    const stdMat = mat as THREE.MeshStandardMaterial;
                    if (stdMat.userData.origEmissiveMap === undefined) {
                      stdMat.userData.origEmissiveMap = stdMat.emissiveMap;
                      stdMat.userData.origMap = stdMat.map;
                      stdMat.userData.origColor = stdMat.color ? stdMat.color.clone() : new THREE.Color(0xffffff);
                    }
                    if (isTransparent) {
                      if (stdMat.emissiveMap) {
                        if (stdMat.map && stdMat.map !== stdMat.emissiveMap) {
                          stdMat.alphaMap = stdMat.map;
                        }
                        stdMat.map = stdMat.emissiveMap;
                      }
                      if (stdMat.color && stdMat.color.r === 0 && stdMat.color.g === 0 && stdMat.color.b === 0) {
                        stdMat.color.setHex(0xffffff);
                      }
                      stdMat.transparent = false;
                      stdMat.alphaTest = 0.25;
                      stdMat.side = THREE.DoubleSide;
                      stdMat.emissive.setHex(0x000000);
                      stdMat.emissiveIntensity = 0.0;
                    } else {
                      if (stdMat.emissiveMap && !stdMat.map) {
                        stdMat.map = stdMat.emissiveMap;
                      }
                      if (stdMat.color && stdMat.color.r === 0 && stdMat.color.g === 0 && stdMat.color.b === 0 && stdMat.map) {
                        stdMat.color.setHex(0xffffff);
                      }
                      stdMat.transparent = false;
                      stdMat.alphaTest = 0.0;
                      stdMat.side = THREE.FrontSide;
                      stdMat.emissive.setHex(0x000000);
                      stdMat.emissiveIntensity = 0.0;
                    }
                    stdMat.roughness = Math.max(0.65, stdMat.roughness || 0.65);
                    stdMat.metalness = Math.min(0.15, stdMat.metalness || 0.0);
                  }
                });
              }
            }
          });

          VolumetricCloudLighting.applyToScene(model);
          resolve(model);
        },
        (err) => {
          console.error('Lỗi phân tích model GLTF từ buffer:', err);
          reject(err);
        }
      );
    });
  }

  /**
   * Toggle between Dynamic Real-time Sun Lighting & Shadows vs Original Baked Lightmap
   */
  public static applyMapLightingMode(mapGroup: THREE.Object3D, dynamicLighting: boolean = true): void {
    if (!mapGroup) return;

    mapGroup.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mesh = child as THREE.Mesh;
        mesh.receiveShadow = true;
        mesh.castShadow = true; // Roofs, walls, and structures cast shadows to block light from penetrating ceilings
        mesh.frustumCulled = false;

        const mats = mesh.material ? (Array.isArray(mesh.material) ? mesh.material : [mesh.material]) : [];
        const hasTransparentMat = mats.some(
          (m) => m && (m.transparent || m.name === 'transparent' || (m as any).alphaMode === 'BLEND')
        );

        if (hasTransparentMat) {
          const geom = mesh.geometry;
          if (geom && geom.attributes.position && geom.index) {
            const pos = geom.attributes.position;
            const index = geom.index;
            const indices = index.array;
            const newIndices: number[] = [];
            let removedHugeCount = 0;

            for (let i = 0; i < indices.length; i += 3) {
              const i1 = indices[i];
              const i2 = indices[i + 1];
              const i3 = indices[i + 2];

              const x1 = pos.getX(i1), y1 = pos.getY(i1), z1 = pos.getZ(i1);
              const x2 = pos.getX(i2), y2 = pos.getY(i2), z2 = pos.getZ(i2);
              const x3 = pos.getX(i3), y3 = pos.getY(i3), z3 = pos.getZ(i3);

              const d12 = Math.hypot(x2 - x1, y2 - y1, z2 - z1);
              const d23 = Math.hypot(x3 - x2, y3 - y2, z3 - z2);
              const d31 = Math.hypot(x1 - x3, y1 - y3, z1 - z3);
              const maxEdge = Math.max(d12, d23, d31);

              if (maxEdge > 25.0) {
                removedHugeCount++;
                continue;
              }

              newIndices.push(i1, i2, i3);
            }

            if (removedHugeCount > 0) {
              geom.setIndex(newIndices);
              geom.computeVertexNormals();
            }
          }
        }

        if (mesh.material) {
          mats.forEach((mat) => {
            if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
              const stdMat = mat as THREE.MeshStandardMaterial;

              // Store original authored emissive/color values on first encounter
              if (stdMat.userData.origEmissiveMap === undefined) {
                stdMat.userData.origEmissiveMap = stdMat.emissiveMap;
                stdMat.userData.origMap = stdMat.map;
                stdMat.userData.origColor = stdMat.color ? stdMat.color.clone() : new THREE.Color(0xffffff);
              }

              const isTransparent =
                stdMat.name === 'transparent' ||
                stdMat.transparent ||
                Boolean((mat as any).alphaMode === 'BLEND');

              stdMat.depthTest = true;
              stdMat.depthWrite = true;

              if (isTransparent) {
                // Transparent materials (foliage, leaves, stained glass, torches, candles, chandeliers, water)
                if (stdMat.emissiveMap) {
                  if (stdMat.map && stdMat.map !== stdMat.emissiveMap) {
                    stdMat.alphaMap = stdMat.map;
                  }
                  stdMat.map = stdMat.emissiveMap;
                }
                if (stdMat.color && stdMat.color.r === 0 && stdMat.color.g === 0 && stdMat.color.b === 0) {
                  stdMat.color.setHex(0xffffff);
                }
                stdMat.transparent = false;
                stdMat.alphaTest = 0.25; // Cleanly discards fake white sunbeam quads while keeping all 3D objects solid!
                stdMat.side = THREE.DoubleSide;
                stdMat.shadowSide = THREE.DoubleSide;

                if (dynamicLighting) {
                  // Dynamic Sunlight Mode: Emissive=0 allows natural sun lighting & cloud shadows!
                  stdMat.emissive.setHex(0x000000);
                  stdMat.emissiveIntensity = 0.0;
                } else {
                  // Original Baked Lightmap Mode (as configured in .glb): Full-bright self-illumination
                  stdMat.emissive.setHex(0xffffff);
                  stdMat.emissiveIntensity = 1.0;
                  stdMat.roughness = 1.0;
                  stdMat.metalness = 0.0;
                }
              } else {
                // Solid surface materials (stone walls, pillars, floors, roofs, props, vehicles)
                if (stdMat.emissiveMap && !stdMat.map) {
                  stdMat.map = stdMat.emissiveMap;
                }
                if (stdMat.color && stdMat.color.r === 0 && stdMat.color.g === 0 && stdMat.color.b === 0 && stdMat.map) {
                  stdMat.color.setHex(0xffffff);
                }
                stdMat.transparent = false;
                stdMat.alphaTest = 0.0;
                stdMat.side = THREE.DoubleSide; // DoubleSide ensures terrain, floors, and props are always visible from any angle
                stdMat.shadowSide = THREE.DoubleSide; // Solid shadows from single-sided roofs/walls

                if (dynamicLighting) {
                  // Dynamic Sunlight & Weather Shadow Mode: Stone surfaces react to sun and cloud shadows!
                  stdMat.emissive.setHex(0x000000);
                  stdMat.emissiveIntensity = 0.0;
                  stdMat.roughness = Math.max(0.65, stdMat.roughness || 0.65);
                  stdMat.metalness = Math.min(0.15, stdMat.metalness || 0.0);
                } else {
                  // Original Baked Lightmap Mode (only for GLB models with textures/lightmaps)
                  if (stdMat.map || stdMat.emissiveMap) {
                    stdMat.emissive.setHex(0xffffff);
                    stdMat.emissiveIntensity = 1.0;
                    stdMat.roughness = 1.0;
                    stdMat.metalness = 0.0;
                  }
                }
              }
              stdMat.needsUpdate = true;
            }
          });
        }
      }
    });

    VolumetricCloudLighting.applyToScene(mapGroup);
  }

  public static createGround(): THREE.Group {
    const ground = new THREE.Group();
    ground.name = 'environment_ground';

    // Grass terrain (Solid 3D Terrain Block - never culled or lost from any angle)
    const grassGeo = new THREE.BoxGeometry(120, 0.5, 120);
    const grassMat = new THREE.MeshStandardMaterial({
      color: 0x427d3b,
      roughness: 0.75,
      metalness: 0.02,
      side: THREE.DoubleSide,
      fog: false, // Terrain is always clearly visible and never washed out by fog
    });
    const grass = new THREE.Mesh(grassGeo, grassMat);
    grass.name = 'village_grass_terrain';
    grass.position.set(0, -0.25, 0);
    grass.receiveShadow = true;
    ground.add(grass);

    // Dirt Path
    const pathGeo = new THREE.BoxGeometry(3.5, 0.06, 30);
    const pathMat = new THREE.MeshStandardMaterial({
      color: 0x5c4033,
      roughness: 0.95,
      side: THREE.DoubleSide,
      fog: false,
    });
    const path = new THREE.Mesh(pathGeo, pathMat);
    path.name = 'village_dirt_path';
    path.position.set(0, 0.03, 0);
    path.receiveShadow = true;
    ground.add(path);

    // Fences
    const fenceMat = new THREE.MeshStandardMaterial({ color: 0x6b4f35, roughness: 0.8, side: THREE.DoubleSide });
    const postGeo = new THREE.CylinderGeometry(0.06, 0.06, 1.1, 6);
    const railGeo = new THREE.BoxGeometry(2.0, 0.08, 0.04);

    for (let i = -4; i <= 4; i += 2) {
      const post = new THREE.Mesh(postGeo, fenceMat);
      post.position.set(2.2, 0.55, i);
      post.castShadow = true;
      ground.add(post);

      if (i < 4) {
        const rail1 = new THREE.Mesh(railGeo, fenceMat);
        rail1.position.set(2.2, 0.8, i + 1);
        rail1.castShadow = true;
        ground.add(rail1);

        const rail2 = new THREE.Mesh(railGeo, fenceMat);
        rail2.position.set(2.2, 0.4, i + 1);
        rail2.castShadow = true;
        ground.add(rail2);
      }
    }

    return ground;
  }

  public static createTree(position: [number, number, number]): THREE.Group {
    const tree = new THREE.Group();
    tree.position.set(...position);
    tree.name = 'prop_village_tree';

    // Solid Wood Trunk (NEVER transparent)
    const trunkGeo = new THREE.CylinderGeometry(0.35, 0.55, 3.2, 8);
    const trunkMat = new THREE.MeshStandardMaterial({ color: 0x4a2e18, roughness: 0.9 });
    const trunk = new THREE.Mesh(trunkGeo, trunkMat);
    trunk.name = 'tree_trunk';
    trunk.position.y = 1.6;
    trunk.castShadow = true;
    trunk.receiveShadow = true;
    tree.add(trunk);

    // Solid Wood Branch seat (NEVER transparent)
    const branchGeo = new THREE.CylinderGeometry(0.18, 0.22, 1.6, 6);
    const branch = new THREE.Mesh(branchGeo, trunkMat);
    branch.name = 'tree_branch';
    branch.rotation.z = Math.PI / 3;
    branch.position.set(0.6, 2.3, 0);
    branch.castShadow = true;
    tree.add(branch);

    // Leaves Foliage (Original clean standard material)
    const leavesGeo1 = new THREE.DodecahedronGeometry(1.6, 1);
    const leavesMat1 = new THREE.MeshStandardMaterial({
      color: 0x1f6629,
      roughness: 0.7,
      transparent: true,
      opacity: 1.0,
      depthWrite: true,
    });
    const leaves1 = new THREE.Mesh(leavesGeo1, leavesMat1);
    leaves1.name = 'tree_leaves_1';
    leaves1.position.y = 3.6;
    leaves1.castShadow = true;
    tree.add(leaves1);

    const leavesGeo2 = new THREE.DodecahedronGeometry(1.2, 1);
    const leavesMat2 = new THREE.MeshStandardMaterial({
      color: 0x2e853a,
      roughness: 0.7,
      transparent: true,
      opacity: 1.0,
      depthWrite: true,
    });
    const leaves2 = new THREE.Mesh(leavesGeo2, leavesMat2);
    leaves2.name = 'tree_leaves_2';
    leaves2.position.set(0.6, 4.4, 0.4);
    leaves2.castShadow = true;
    tree.add(leaves2);

    return tree;
  }

  public static createChair(position: [number, number, number]): THREE.Group {
    const chair = new THREE.Group();
    chair.position.set(...position);
    chair.name = 'prop_wooden_chair';

    const woodMat = new THREE.MeshStandardMaterial({ color: 0x7c471b, roughness: 0.8 });

    // Seat
    const seatGeo = new THREE.BoxGeometry(0.7, 0.08, 0.7);
    const seat = new THREE.Mesh(seatGeo, woodMat);
    seat.name = 'chair_seat';
    seat.position.y = 0.5;
    seat.castShadow = true;
    chair.add(seat);

    // Backrest
    const backGeo = new THREE.BoxGeometry(0.7, 0.8, 0.08);
    const back = new THREE.Mesh(backGeo, woodMat);
    back.name = 'chair_back';
    back.position.set(0, 0.9, -0.31);
    back.castShadow = true;
    chair.add(back);

    // Legs
    const legGeo = new THREE.CylinderGeometry(0.04, 0.04, 0.5, 6);
    const offsets = [
      [-0.28, 0.25, -0.28],
      [0.28, 0.25, -0.28],
      [-0.28, 0.25, 0.28],
      [0.28, 0.25, 0.28],
    ];
    offsets.forEach(([x, y, z]) => {
      const leg = new THREE.Mesh(legGeo, woodMat);
      leg.position.set(x, y, z);
      leg.castShadow = true;
      chair.add(leg);
    });

    return chair;
  }

  public static createFarmPlot(position: [number, number, number]): THREE.Group {
    const farm = new THREE.Group();
    farm.position.set(...position);
    farm.name = 'props.farm_plot_01';

    // Soil Mound
    const soilGeo = new THREE.BoxGeometry(2.4, 0.15, 2.4);
    const soilMat = new THREE.MeshStandardMaterial({ color: 0x3d2817, roughness: 0.95 });
    const soil = new THREE.Mesh(soilGeo, soilMat);
    soil.position.y = 0.075;
    soil.receiveShadow = true;
    farm.add(soil);

    // Crop Container
    const cropContainer = new THREE.Group();
    cropContainer.name = 'crop';
    cropContainer.position.set(0, 0.15, 0);

    // Sprout Mesh
    const stemGeo = new THREE.CylinderGeometry(0.04, 0.06, 0.8, 6);
    const stemMat = new THREE.MeshStandardMaterial({ color: 0x44aa33, roughness: 0.6 });
    const stem = new THREE.Mesh(stemGeo, stemMat);
    stem.position.y = 0.4;
    stem.castShadow = true;
    cropContainer.add(stem);

    const leafGeo = new THREE.SphereGeometry(0.2, 6, 6);
    leafGeo.scale(1.5, 0.4, 0.8);
    const leafMat = new THREE.MeshStandardMaterial({ color: 0x66cc33, roughness: 0.5 });
    const leaf1 = new THREE.Mesh(leafGeo, leafMat);
    leaf1.position.set(0.18, 0.7, 0);
    leaf1.rotation.z = Math.PI / 6;
    leaf1.castShadow = true;
    cropContainer.add(leaf1);

    const leaf2 = new THREE.Mesh(leafGeo, leafMat);
    leaf2.position.set(-0.18, 0.65, 0);
    leaf2.rotation.z = -Math.PI / 6;
    leaf2.castShadow = true;
    cropContainer.add(leaf2);

    // Golden Wheat Head for mature crop
    const grainGeo = new THREE.ConeGeometry(0.15, 0.4, 6);
    const grainMat = new THREE.MeshStandardMaterial({ color: 0xddaa22, roughness: 0.4 });
    const grain = new THREE.Mesh(grainGeo, grainMat);
    grain.position.set(0, 0.9, 0);
    grain.castShadow = true;
    cropContainer.add(grain);

    // Initial scale is 0.1
    cropContainer.scale.set(0.1, 0.1, 0.1);
    farm.add(cropContainer);

    return farm;
  }

  public static createDuckProp(position: [number, number, number]): THREE.Group {
    const duck = new THREE.Group();
    duck.position.set(...position);
    duck.name = 'props.duck_prop_01';

    // Yellow Duck Body
    const bodyGeo = new THREE.SphereGeometry(0.22, 12, 12);
    bodyGeo.scale(1.2, 0.9, 1.0);
    const yellowMat = new THREE.MeshStandardMaterial({ color: 0xffcc00, roughness: 0.3 });
    const body = new THREE.Mesh(bodyGeo, yellowMat);
    body.position.y = 0.2;
    body.castShadow = true;
    duck.add(body);

    // Head
    const headGeo = new THREE.SphereGeometry(0.14, 10, 10);
    const head = new THREE.Mesh(headGeo, yellowMat);
    head.position.set(0.16, 0.35, 0);
    head.castShadow = true;
    duck.add(head);

    // Orange Beak
    const beakGeo = new THREE.ConeGeometry(0.06, 0.12, 6);
    const orangeMat = new THREE.MeshStandardMaterial({ color: 0xff6600, roughness: 0.4 });
    const beak = new THREE.Mesh(beakGeo, orangeMat);
    beak.position.set(0.3, 0.34, 0);
    beak.rotation.z = -Math.PI / 2;
    duck.add(beak);

    // Eyes
    const eyeGeo = new THREE.SphereGeometry(0.02, 6, 6);
    const eyeMat = new THREE.MeshBasicMaterial({ color: 0x000000 });
    const eyeL = new THREE.Mesh(eyeGeo, eyeMat);
    eyeL.position.set(0.22, 0.38, 0.09);
    duck.add(eyeL);
    const eyeR = new THREE.Mesh(eyeGeo, eyeMat);
    eyeR.position.set(0.22, 0.38, -0.09);
    duck.add(eyeR);

    return duck;
  }

  public static createLanternStand(position: [number, number, number]): THREE.Group {
    const stand = new THREE.Group();
    stand.position.set(...position);
    stand.name = 'props.lantern_stand_01';

    // Wooden Pole
    const poleGeo = new THREE.CylinderGeometry(0.06, 0.08, 2.6, 8);
    const poleMat = new THREE.MeshStandardMaterial({ color: 0x3d2716, roughness: 0.8 });
    const pole = new THREE.Mesh(poleGeo, poleMat);
    pole.position.y = 1.3;
    pole.castShadow = true;
    stand.add(pole);

    // Cross Arm
    const armGeo = new THREE.BoxGeometry(0.7, 0.08, 0.08);
    const arm = new THREE.Mesh(armGeo, poleMat);
    arm.position.set(0.25, 2.45, 0);
    stand.add(arm);

    // Hanging Large Lantern
    const lanternGeo = new THREE.CylinderGeometry(0.18, 0.22, 0.45, 8);
    const glowMat = new THREE.MeshStandardMaterial({
      color: 0xff4411,
      emissive: 0xff7711,
      emissiveIntensity: 0.9,
      roughness: 0.2,
    });
    const lantern = new THREE.Mesh(lanternGeo, glowMat);
    lantern.position.set(0.5, 2.0, 0);
    stand.add(lantern);

    // Point Light emitting warm glow
    const light = new THREE.PointLight(0xffaa44, 2.0, 8);
    light.position.set(0.5, 2.0, 0);
    stand.add(light);

    return stand;
  }
}
