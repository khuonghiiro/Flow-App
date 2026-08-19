import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

export class AssetLoaderRegistry {
  private static gltfLoader: GLTFLoader | null = null;
  private static modelCache = new Map<string, THREE.Group>();

  public static getGLTFLoader(): GLTFLoader {
    if (!this.gltfLoader) {
      this.gltfLoader = new GLTFLoader();
    }
    return this.gltfLoader;
  }

  public static async loadGLTF(url: string): Promise<THREE.Group> {
    if (this.modelCache.has(url)) {
      const cached = this.modelCache.get(url)!;
      const clone = cached.clone(true);
      clone.animations = cached.animations;
      return clone;
    }

    const loader = this.getGLTFLoader();
    return new Promise((resolve, reject) => {
      loader.load(
        url,
        (gltf) => {
          const model = gltf.scene;

          // Decompose all matrix-only nodes so Three.js transforms and scales work accurately
          model.traverse((child) => {
            if (child.matrix && !child.matrixAutoUpdate) {
              child.position.setFromMatrixPosition(child.matrix);
              child.quaternion.setFromRotationMatrix(child.matrix);
              child.scale.setFromMatrixScale(child.matrix);
              child.matrixAutoUpdate = true;
            }

            if ((child as THREE.Mesh).isMesh) {
              const mesh = child as THREE.Mesh;
              // Map/Environment models only receive shadows; characters/props cast shadows
              mesh.castShadow = false;
              mesh.receiveShadow = true;
              mesh.frustumCulled = true;
              mesh.matrixAutoUpdate = false; // Static mesh optimization - don't recompute 2.1M matrices every frame
              mesh.updateMatrix();

              if (mesh.geometry) {
                mesh.geometry.computeBoundingBox();
                mesh.geometry.computeBoundingSphere();
              }
              if (mesh.material) {
                const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
                mats.forEach((mat) => {
                  mat.side = THREE.FrontSide; // Enable GPU backface culling for high FPS
                  mat.depthWrite = true;
                  // Enable Early-Z depth testing on transparent materials to eliminate heavy overdraw
                  if (mat.transparent) {
                    mat.alphaTest = 0.05;
                  }
                  if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
                    const stdMat = mat as THREE.MeshStandardMaterial;
                    // If map is missing but emissiveMap exists (common Sketchfab unlit conversion)
                    if (stdMat.emissiveMap && !stdMat.map) {
                      stdMat.map = stdMat.emissiveMap;
                    }
                    stdMat.roughness = Math.max(0.6, stdMat.roughness || 0.6);
                    stdMat.metalness = Math.min(0.2, stdMat.metalness || 0.0);
                  }
                });
              }
            }
          });

          model.animations = gltf.animations || [];
          this.modelCache.set(url, model);
          const initialClone = model.clone(true);
          initialClone.animations = model.animations;
          resolve(initialClone);
        },
        undefined,
        (err) => {
          console.error(`Lỗi tải model GLTF từ ${url}:`, err);
          reject(err);
        }
      );
    });
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
              mesh.castShadow = true;
              mesh.receiveShadow = true;
              mesh.frustumCulled = false;
              if (mesh.material) {
                const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
                mats.forEach((mat) => {
                  mat.side = THREE.DoubleSide;
                  mat.depthWrite = true;
                  mat.needsUpdate = true;
                  if ((mat as THREE.MeshStandardMaterial).isMeshStandardMaterial) {
                    const stdMat = mat as THREE.MeshStandardMaterial;
                    if (stdMat.emissiveMap && !stdMat.map) {
                      stdMat.map = stdMat.emissiveMap;
                      stdMat.needsUpdate = true;
                    }
                  }
                });
              }
            }
          });

          resolve(model);
        },
        (err) => {
          console.error('Lỗi phân tích model GLTF từ buffer:', err);
          reject(err);
        }
      );
    });
  }

  public static createGround(): THREE.Group {
    const ground = new THREE.Group();
    ground.name = 'environment_ground';

    // Grass terrain
    const grassGeo = new THREE.PlaneGeometry(80, 80);
    const grassMat = new THREE.MeshStandardMaterial({
      color: 0x3d7038,
      roughness: 0.8,
      metalness: 0.05,
    });
    const grass = new THREE.Mesh(grassGeo, grassMat);
    grass.rotation.x = -Math.PI / 2;
    grass.receiveShadow = true;
    ground.add(grass);

    // Dirt Path
    const pathGeo = new THREE.PlaneGeometry(3.5, 30);
    const pathMat = new THREE.MeshStandardMaterial({
      color: 0x5c4033,
      roughness: 0.95,
    });
    const path = new THREE.Mesh(pathGeo, pathMat);
    path.rotation.x = -Math.PI / 2;
    path.position.set(0, 0.01, 0);
    path.receiveShadow = true;
    ground.add(path);

    // Fences
    const fenceMat = new THREE.MeshStandardMaterial({ color: 0x6b4f35, roughness: 0.8 });
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
