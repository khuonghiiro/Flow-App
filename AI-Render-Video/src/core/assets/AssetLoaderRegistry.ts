import * as THREE from 'three';

export class AssetLoaderRegistry {
  private static cache: Map<string, THREE.Object3D> = new Map();

  public static createGround(size: number = 60): THREE.Group {
    const group = new THREE.Group();
    group.name = 'Environment_Ground';

    // Grass & Terrain Base
    const groundGeo = new THREE.PlaneGeometry(size, size, 32, 32);
    const groundMat = new THREE.MeshStandardMaterial({
      color: 0x2e4a2b,
      roughness: 0.85,
      metalness: 0.1,
    });
    const groundMesh = new THREE.Mesh(groundGeo, groundMat);
    groundMesh.rotation.x = -Math.PI / 2;
    groundMesh.receiveShadow = true;
    group.add(groundMesh);

    // Stone Path in the middle
    const pathGeo = new THREE.PlaneGeometry(4, size * 0.8);
    const pathMat = new THREE.MeshStandardMaterial({
      color: 0x4f4943,
      roughness: 0.9,
      metalness: 0.1,
    });
    const pathMesh = new THREE.Mesh(pathGeo, pathMat);
    pathMesh.rotation.x = -Math.PI / 2;
    pathMesh.position.set(0, 0.02, 0);
    pathMesh.receiveShadow = true;
    group.add(pathMesh);

    // Village boundaries / low fence markers
    const fenceGeo = new THREE.BoxGeometry(0.3, 0.8, 0.3);
    const fenceMat = new THREE.MeshStandardMaterial({ color: 0x6b4423, roughness: 0.8 });
    for (let i = -15; i <= 15; i += 3) {
      if (Math.abs(i) > 2) {
        const postL = new THREE.Mesh(fenceGeo, fenceMat);
        postL.position.set(-6, 0.4, i);
        postL.castShadow = true;
        group.add(postL);

        const postR = new THREE.Mesh(fenceGeo, fenceMat);
        postR.position.set(6, 0.4, i);
        postR.castShadow = true;
        group.add(postR);
      }
    }

    return group;
  }

  public static createTree(position: [number, number, number]): THREE.Group {
    const tree = new THREE.Group();
    tree.position.set(...position);
    tree.name = 'prop_village_tree';

    // Trunk
    const trunkGeo = new THREE.CylinderGeometry(0.35, 0.55, 3.2, 8);
    const trunkMat = new THREE.MeshStandardMaterial({ color: 0x4a2e18, roughness: 0.9 });
    const trunk = new THREE.Mesh(trunkGeo, trunkMat);
    trunk.position.y = 1.6;
    trunk.castShadow = true;
    trunk.receiveShadow = true;
    tree.add(trunk);

    // Branch seat
    const branchGeo = new THREE.CylinderGeometry(0.18, 0.22, 1.6, 6);
    const branch = new THREE.Mesh(branchGeo, trunkMat);
    branch.rotation.z = Math.PI / 3;
    branch.position.set(0.6, 2.3, 0);
    branch.castShadow = true;
    tree.add(branch);

    // Leaves Foliage
    const leavesGeo1 = new THREE.DodecahedronGeometry(1.6, 1);
    const leavesMat = new THREE.MeshStandardMaterial({ color: 0x1f6629, roughness: 0.7 });
    const leaves1 = new THREE.Mesh(leavesGeo1, leavesMat);
    leaves1.position.y = 3.6;
    leaves1.castShadow = true;
    tree.add(leaves1);

    const leavesGeo2 = new THREE.DodecahedronGeometry(1.2, 1);
    const leavesMat2 = new THREE.MeshStandardMaterial({ color: 0x2e853a, roughness: 0.7 });
    const leaves2 = new THREE.Mesh(leavesGeo2, leavesMat2);
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
    seat.position.y = 0.5;
    seat.castShadow = true;
    chair.add(seat);

    // Backrest
    const backGeo = new THREE.BoxGeometry(0.7, 0.8, 0.08);
    const back = new THREE.Mesh(backGeo, woodMat);
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
}
