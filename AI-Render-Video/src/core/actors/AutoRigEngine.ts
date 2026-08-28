import * as THREE from 'three';

export interface RigJointDefinition {
  name: string;
  parentName: string | null;
  childName: string | null;
  relativePos: [number, number, number]; // [xRatio, yRatio, zRatio] relative to bounding box
  radius: number;
}

export interface AutoRigResult {
  rootGroup: THREE.Group;
  skeleton: THREE.Skeleton;
  skinnedMeshes: THREE.SkinnedMesh[];
  bonesMap: Map<string, THREE.Bone>;
  jointVisualizer: THREE.Group;
}

/**
 * AutoRigEngine
 * Tự động phân tích hình học của mô hình 3D tĩnh (Static Mesh),
 * tự động sinh cây xương Humanoid chuẩn giải phẫu (Anatomical Humanoid Rig)
 * và tính toán ma trận trọng số da (Capsule Linear Blend Skinning) mượt mà,
 * triệt tiêu hoàn toàn hiện tượng méo mó (Distortion/Dị tật).
 */
export class AutoRigEngine {
  // Cây phân cấp 17 xương Humanoid chuẩn giải phẫu học
  public static readonly HUMANOID_JOINTS: RigJointDefinition[] = [
    { name: 'Hips', parentName: null, childName: 'Spine', relativePos: [0.0, 0.53, 0.0], radius: 0.26 },
    { name: 'Spine', parentName: 'Hips', childName: 'Chest', relativePos: [0.0, 0.63, 0.0], radius: 0.22 },
    { name: 'Chest', parentName: 'Spine', childName: 'Neck', relativePos: [0.0, 0.74, 0.0], radius: 0.24 },
    { name: 'Neck', parentName: 'Chest', childName: 'Head', relativePos: [0.0, 0.86, 0.0], radius: 0.16 },
    { name: 'Head', parentName: 'Neck', childName: null, relativePos: [0.0, 0.95, 0.0], radius: 0.18 },

    // Tay Trái (Chuẩn A-Pose nghiêng tự nhiên)
    { name: 'LeftShoulder', parentName: 'Chest', childName: 'LeftUpperArm', relativePos: [-0.10, 0.76, 0.0], radius: 0.14 },
    { name: 'LeftUpperArm', parentName: 'LeftShoulder', childName: 'LeftLowerArm', relativePos: [-0.25, 0.70, 0.0], radius: 0.15 },
    { name: 'LeftLowerArm', parentName: 'LeftUpperArm', childName: 'LeftHand', relativePos: [-0.44, 0.52, 0.0], radius: 0.13 },
    { name: 'LeftHand', parentName: 'LeftLowerArm', childName: null, relativePos: [-0.58, 0.36, 0.0], radius: 0.12 },

    // Tay Phải (Chuẩn A-Pose nghiêng tự nhiên)
    { name: 'RightShoulder', parentName: 'Chest', childName: 'RightUpperArm', relativePos: [0.10, 0.76, 0.0], radius: 0.14 },
    { name: 'RightUpperArm', parentName: 'RightShoulder', childName: 'RightLowerArm', relativePos: [0.25, 0.70, 0.0], radius: 0.15 },
    { name: 'RightLowerArm', parentName: 'RightUpperArm', childName: 'RightHand', relativePos: [0.44, 0.52, 0.0], radius: 0.13 },
    { name: 'RightHand', parentName: 'RightLowerArm', childName: null, relativePos: [0.58, 0.36, 0.0], radius: 0.12 },

    // Chân Trái
    { name: 'LeftUpperLeg', parentName: 'Hips', childName: 'LeftLowerLeg', relativePos: [-0.11, 0.49, 0.0], radius: 0.18 },
    { name: 'LeftLowerLeg', parentName: 'LeftUpperLeg', childName: 'LeftFoot', relativePos: [-0.11, 0.26, 0.0], radius: 0.15 },
    { name: 'LeftFoot', parentName: 'LeftLowerLeg', childName: null, relativePos: [-0.11, 0.04, 0.08], radius: 0.14 },

    // Chân Phải
    { name: 'RightUpperLeg', parentName: 'Hips', childName: 'RightLowerLeg', relativePos: [0.11, 0.49, 0.0], radius: 0.18 },
    { name: 'RightLowerLeg', parentName: 'RightUpperLeg', childName: 'RightFoot', relativePos: [0.11, 0.26, 0.0], radius: 0.15 },
    { name: 'RightFoot', parentName: 'RightLowerLeg', childName: null, relativePos: [0.11, 0.04, 0.08], radius: 0.14 },
  ];

  /**
   * Tính khoảng cách ngắn nhất từ một điểm P tới đoạn thẳng AB (Bone Capsule Distance)
   */
  private static distanceToSegment(p: THREE.Vector3, a: THREE.Vector3, b: THREE.Vector3): number {
    const ab = new THREE.Vector3().subVectors(b, a);
    const ap = new THREE.Vector3().subVectors(p, a);
    const abLenSq = ab.lengthSq();
    if (abLenSq < 0.00001) return p.distanceTo(a);

    let t = ap.dot(ab) / abLenSq;
    t = Math.max(0, Math.min(1, t));

    const projection = new THREE.Vector3().copy(a).addScaledVector(ab, t);
    return p.distanceTo(projection);
  }

  /**
   * Tự động tạo bộ khung xương và gắn da (Capsule Linear Blend Skinning) cho một Group hoặc Mesh.
   * Nếu mô hình đã có sẵn khung xương (Native Bones / SkinnedMesh như Columbina 743 joints),
   * hệ thống sẽ tái sử dụng và đồng bộ trực tiếp khung xương gốc!
   */
  public static rigModel(modelGroup: THREE.Group | THREE.Object3D): AutoRigResult {
    // 0. Kiểm tra nếu mô hình đã có sẵn khung xương (Native Rigged Model)
    const existingBones: THREE.Bone[] = [];
    const existingSkinnedMeshes: THREE.SkinnedMesh[] = [];
    modelGroup.traverse((c) => {
      if ((c as THREE.Bone).isBone) existingBones.push(c as THREE.Bone);
      if ((c as THREE.SkinnedMesh).isSkinnedMesh) existingSkinnedMeshes.push(c as THREE.SkinnedMesh);
    });

    if (existingBones.length > 0) {
      modelGroup.updateMatrixWorld(true);

      // Cache initial rest orientation (T-pose) for each bone
      existingBones.forEach((b) => {
        if (!b.userData.initialRotation) {
          b.userData.initialRotation = b.rotation.clone();
        }
        if (!b.userData.initialPosition) {
          b.userData.initialPosition = b.position.clone();
        }
      });

      // Mô hình đã có sẵn xương: Map các khớp giải phẫu chính theo tên hoặc tọa độ
      const bonesMap = new Map<string, THREE.Bone>();
      const bonesLower = new Map<string, THREE.Bone>();
      existingBones.forEach((b) => {
        bonesLower.set(b.name.toLowerCase(), b);
        bonesMap.set(b.name, b);
      });

      // Map chuẩn Humanoid Alias sang Native Bones (Hỗ trợ Mixamo, Blender, Unity, Unreal, MMD & Genshin)
      const aliasMap: Record<string, string[]> = {
        Hips: ['hips', 'pelvis', 'root', 'waist', 'center', 'hip', 'bip001_pelvis', '07', '08', '09'],
        Spine: ['spine1', 'spine01', 'bip001_spine', 'torso', 'back', '010', 'spine'],
        Chest: ['chest', 'spine2', 'upperbody', 'bip001_spine2', '011'],
        Neck: ['neck', 'bip001_neck', '0589'],
        Head: ['head', 'bip001_head', 'face', '0590'],
        LeftShoulder: ['leftshoulder', 'shoulder_l', 'l_shoulder', 'bip001_l_clavicle', '0459'],
        LeftUpperArm: ['leftupperarm', 'upperarm_l', 'arm_l', 'l_arm', 'leftarm', 'bip001_l_upperarm', '0460'],
        LeftLowerArm: ['leftlowerarm', 'lowerarm_l', 'forearm_l', 'l_forearm', 'bip001_l_forearm', '0461'],
        LeftHand: ['lefthand', 'hand_l', 'l_hand', 'bip001_l_hand', '0462'],
        RightShoulder: ['rightshoulder', 'shoulder_r', 'r_shoulder', 'bip001_r_clavicle', '0289'],
        RightUpperArm: ['rightupperarm', 'upperarm_r', 'arm_r', 'r_arm', 'rightarm', 'bip001_r_upperarm', '0290'],
        RightLowerArm: ['rightlowerarm', 'lowerarm_r', 'forearm_r', 'r_forearm', 'bip001_r_forearm', '0291'],
        RightHand: ['righthand', 'hand_r', 'r_hand', 'bip001_r_hand', '0292'],
        LeftUpperLeg: ['leftupperleg', 'thigh_l', 'upperleg_l', 'l_thigh', 'bip001_l_thigh', 'leftleg', '0698', '0699'],
        LeftLowerLeg: ['leftlowerleg', 'calf_l', 'lowerleg_l', 'l_calf', 'bip001_l_calf', 'l_shin', '0720'],
        LeftFoot: ['leftfoot', 'foot_l', 'l_foot', 'bip001_l_foot', '0719', '0721'],
        RightUpperLeg: ['rightupperleg', 'thigh_r', 'upperleg_r', 'r_thigh', 'bip001_r_thigh', 'rightleg', '0728', '0729'],
        RightLowerLeg: ['rightlowerleg', 'calf_r', 'lowerleg_r', 'r_calf', 'bip001_r_calf', 'r_shin', '0730'],
        RightFoot: ['rightfoot', 'foot_r', 'r_foot', 'bip001_r_foot', '0731', '0739'],
      };

      for (const [standardName, aliases] of Object.entries(aliasMap)) {
        for (const alias of aliases) {
          const match = Array.from(bonesLower.entries()).find(([k]) => k.includes(alias));
          if (match) {
            bonesMap.set(standardName, match[1]);
            break;
          }
        }
      }

      // Fallback: Với các model có tên xương tiếng Nhật, MMD, hoặc mã số (như Columbina):
      // Lọc bỏ các xương vật lý phụ (tóc, váy, dây ruy băng) và ánh xạ chính xác vào khung xương thân chính
      const isDanglePhysics = (name: string) =>
        /^(q_|pf_|pj_|x_|hair|let|skirt|ribbon|cloth|tail|wing|ear|bone_let|bone_hair)/i.test(name.toLowerCase());
      const trunkBones = existingBones.filter((b) => !isDanglePhysics(b.name));
      const candidates = trunkBones.length >= 15 ? trunkBones : existingBones;

      let minY = Infinity, maxY = -Infinity;
      const boneWorldPositions = candidates.map((b) => {
        const wp = new THREE.Vector3();
        b.getWorldPosition(wp);
        if (wp.y < minY) minY = wp.y;
        if (wp.y > maxY) maxY = wp.y;
        return { bone: b, pos: wp };
      });

      const skeletonHeight = maxY - minY || 1;

      const targets: Record<string, [number, number, number]> = {
        Hips: [0, 0.50, 0],
        Spine: [0, 0.58, 0],
        Chest: [0, 0.68, 0],
        Neck: [0, 0.82, 0],
        Head: [0, 0.90, 0],
        LeftUpperArm: [0.08, 0.80, 0],
        LeftLowerArm: [0.18, 0.76, 0],
        LeftHand: [0.26, 0.74, 0],
        RightUpperArm: [-0.08, 0.80, 0],
        RightLowerArm: [-0.18, 0.76, 0],
        RightHand: [-0.26, 0.74, 0],
        LeftUpperLeg: [-0.05, 0.52, 0],
        LeftLowerLeg: [-0.04, 0.28, 0],
        LeftFoot: [-0.03, 0.05, 0],
        RightUpperLeg: [0.05, 0.52, 0],
        RightLowerLeg: [0.04, 0.28, 0],
        RightFoot: [0.03, 0.05, 0],
      };

      for (const [jointName, rel] of Object.entries(targets)) {
        if (!bonesMap.has(jointName) || isDanglePhysics(bonesMap.get(jointName)!.name)) {
          const targetWorld = new THREE.Vector3(
            rel[0] * skeletonHeight,
            minY + rel[1] * skeletonHeight,
            rel[2] * skeletonHeight
          );

          let closestBone: THREE.Bone | null = null;
          let minDist = Infinity;

          for (const item of boneWorldPositions) {
            const d = item.pos.distanceTo(targetWorld);
            if (d < minDist) {
              minDist = d;
              closestBone = item.bone;
            }
          }

          if (closestBone) {
            bonesMap.set(jointName, closestBone);
          }
        }
      }

      // Tạo SkeletonHelper 3D cho toàn bộ Native Bones
      const helper = new THREE.SkeletonHelper(modelGroup);
      const helperMat = (helper as any).material;
      if (helperMat) {
        helperMat.linewidth = 2;
        helperMat.depthTest = false;
        helperMat.transparent = true;
        helperMat.opacity = 0.9;
      }
      const visualizerGroup = new THREE.Group();
      visualizerGroup.name = 'NativeSkeletonHelperGroup';
      visualizerGroup.add(helper);

      return {
        rootGroup: modelGroup as THREE.Group,
        skeleton: new THREE.Skeleton(existingBones),
        skinnedMeshes: existingSkinnedMeshes,
        bonesMap,
        jointVisualizer: visualizerGroup,
      };
    }

    // 1. Tính toán Bounding Box tổng thể
    const bbox = new THREE.Box3().setFromObject(modelGroup);
    const size = new THREE.Vector3();
    const center = new THREE.Vector3();
    bbox.getSize(size);
    bbox.getCenter(center);

    const height = Math.max(1.0, size.y);
    const width = Math.max(0.4, size.x);
    const depth = Math.max(0.3, size.z);
    const minY = bbox.min.y;

    // 2. Tạo các Node THREE.Bone
    const bonesMap = new Map<string, THREE.Bone>();
    const bonesList: THREE.Bone[] = [];
    const jointPositions = new Map<string, THREE.Vector3>();

    for (const def of this.HUMANOID_JOINTS) {
      const bone = new THREE.Bone();
      bone.name = def.name;

      const worldX = center.x + def.relativePos[0] * (width * 0.95);
      const worldY = minY + def.relativePos[1] * height;
      const worldZ = center.z + def.relativePos[2] * depth;
      const worldPos = new THREE.Vector3(worldX, worldY, worldZ);

      jointPositions.set(def.name, worldPos);
      bonesMap.set(def.name, bone);
      bonesList.push(bone);
    }

    // 3. Xây dựng cây phân cấp xương (Hierarchy) và tính tọa độ tương đối (Local Position)
    const rootBone = bonesMap.get('Hips')!;
    const rootPos = jointPositions.get('Hips')!;
    rootBone.position.copy(rootPos);

    for (const def of this.HUMANOID_JOINTS) {
      if (def.parentName) {
        const parentBone = bonesMap.get(def.parentName)!;
        const childBone = bonesMap.get(def.name)!;
        const parentPos = jointPositions.get(def.parentName)!;
        const childPos = jointPositions.get(def.name)!;

        const localPos = new THREE.Vector3().subVectors(childPos, parentPos);
        childBone.position.copy(localPos);
        parentBone.add(childBone);
      }
    }

    const skeleton = new THREE.Skeleton(bonesList);

    // Cache initial rest orientation (T-pose) and position for each bone
    for (const b of bonesList) {
      if (!b.userData.initialRotation) {
        b.userData.initialRotation = b.rotation.clone();
      }
      if (!b.userData.initialPosition) {
        b.userData.initialPosition = b.position.clone();
      }
    }

    // 4. Tạo mô hình hiển thị trực quan các khớp xương (Joint Visualizer chuẩn Studio)
    const jointVisualizer = this.createJointVisualizer(jointPositions, this.HUMANOID_JOINTS);

    // 5. Chuyển đổi Mesh tĩnh thành SkinnedMesh với thuật toán Phân Vùng Giải Phẫu (Anatomical Masking)
    const skinnedMeshes: THREE.SkinnedMesh[] = [];
    const riggedRootGroup = new THREE.Group();
    riggedRootGroup.name = 'Rigged_Humanoid_Character';
    riggedRootGroup.add(rootBone);

    modelGroup.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const staticMesh = child as THREE.Mesh;
        const skinnedMesh = this.convertMeshToSkinnedMesh(staticMesh, bonesList, jointPositions, center, height, width, minY);
        skinnedMesh.bind(skeleton);
        skinnedMeshes.push(skinnedMesh);
        riggedRootGroup.add(skinnedMesh);
      }
    });

    return {
      rootGroup: riggedRootGroup,
      skeleton,
      skinnedMeshes,
      bonesMap,
      jointVisualizer,
    };
  }

  /**
   * Chuyển đổi một THREE.Mesh tĩnh thành THREE.SkinnedMesh với Capsule Linear Blend Skinning
   */
  private static convertMeshToSkinnedMesh(
    mesh: THREE.Mesh,
    bones: THREE.Bone[],
    jointPositions: Map<string, THREE.Vector3>,
    center: THREE.Vector3,
    height: number,
    width: number,
    minY: number
  ): THREE.SkinnedMesh {
    const geo = mesh.geometry.clone();
    const posAttr = geo.attributes.position;
    const vertexCount = posAttr.count;

    const skinIndices: number[] = [];
    const skinWeights: number[] = [];

    const vertex = new THREE.Vector3();
    const meshWorldMatrix = mesh.matrixWorld;

    // Chuẩn bị danh sách đoạn xương (Bone Segments)
    const boneSegments = this.HUMANOID_JOINTS.map((j, idx) => {
      const startPos = jointPositions.get(j.name)!;
      let endPos: THREE.Vector3;

      if (j.childName && jointPositions.has(j.childName)) {
        endPos = jointPositions.get(j.childName)!;
      } else {
        // Tip bone: mở rộng nhẹ theo hướng tự nhiên
        if (j.name === 'Head') endPos = new THREE.Vector3(startPos.x, startPos.y + height * 0.08, startPos.z);
        else if (j.name === 'LeftHand') endPos = new THREE.Vector3(startPos.x - width * 0.1, startPos.y - height * 0.05, startPos.z);
        else if (j.name === 'RightHand') endPos = new THREE.Vector3(startPos.x + width * 0.1, startPos.y - height * 0.05, startPos.z);
        else if (j.name.includes('Foot')) endPos = new THREE.Vector3(startPos.x, startPos.y, startPos.z + 0.12);
        else endPos = startPos.clone();
      }

      return {
        index: idx,
        name: j.name,
        startPos,
        endPos,
        radius: j.radius,
      };
    });

    for (let i = 0; i < vertexCount; i++) {
      vertex.fromBufferAttribute(posAttr, i);
      vertex.applyMatrix4(meshWorldMatrix);

      const relX = (vertex.x - center.x) / width;
      const relY = (vertex.y - minY) / height;

      // 1. Phân vùng giải phẫu (Anatomical Region Masking) để tránh biến dạng kéo chéo
      let allowedBoneNames: string[];

      if (relY >= 0.83) {
        // Đầu & Cổ
        allowedBoneNames = ['Head', 'Neck', 'Chest'];
      } else if (relX <= -0.10 && relY >= 0.32) {
        // Tay Trái
        allowedBoneNames = ['LeftShoulder', 'LeftUpperArm', 'LeftLowerArm', 'LeftHand', 'Chest'];
      } else if (relX >= 0.10 && relY >= 0.32) {
        // Tay Phải
        allowedBoneNames = ['RightShoulder', 'RightUpperArm', 'RightLowerArm', 'RightHand', 'Chest'];
      } else if (relX < -0.01 && relY < 0.52) {
        // Chân Trái
        allowedBoneNames = ['Hips', 'LeftUpperLeg', 'LeftLowerLeg', 'LeftFoot'];
      } else if (relX > 0.01 && relY < 0.52) {
        // Chân Phải
        allowedBoneNames = ['Hips', 'RightUpperLeg', 'RightLowerLeg', 'RightFoot'];
      } else {
        // Thân Mình (Torso)
        allowedBoneNames = ['Hips', 'Spine', 'Chest', 'Neck'];
      }

      const distList: { index: number; weight: number }[] = [];

      for (const seg of boneSegments) {
        if (!allowedBoneNames.includes(seg.name)) continue;

        const dist = this.distanceToSegment(vertex, seg.startPos, seg.endPos);
        const weight = Math.exp(-Math.pow(dist / Math.max(0.10, seg.radius), 2));
        distList.push({ index: seg.index, weight });
      }

      distList.sort((a, b) => b.weight - a.weight);
      const top4 = distList.slice(0, 4);

      let totalWeight = top4.reduce((acc, cur) => acc + cur.weight, 0);
      if (totalWeight < 0.0001) totalWeight = 1.0;

      skinIndices.push(top4[0]?.index || 0, top4[1]?.index || 0, top4[2]?.index || 0, top4[3]?.index || 0);
      skinWeights.push(
        (top4[0]?.weight || 0) / totalWeight,
        (top4[1]?.weight || 0) / totalWeight,
        (top4[2]?.weight || 0) / totalWeight,
        (top4[3]?.weight || 0) / totalWeight
      );
    }

    geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(skinIndices, 4));
    geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(skinWeights, 4));

    const mat = Array.isArray(mesh.material) ? mesh.material.map((m) => m.clone()) : mesh.material.clone();
    const skinnedMesh = new THREE.SkinnedMesh(geo, mat);
    skinnedMesh.name = `Skinned_${mesh.name || 'Part'}`;
    skinnedMesh.castShadow = true;
    skinnedMesh.receiveShadow = true;

    return skinnedMesh;
  }

  /**
   * Tạo mô hình hiển thị trực quan các khớp xương (Joint Visualizer chuẩn Studio)
   */
  private static createJointVisualizer(
    jointPositions: Map<string, THREE.Vector3>,
    jointsDef: RigJointDefinition[]
  ): THREE.Group {
    const group = new THREE.Group();
    group.name = 'Joint_Visualizer_Group';

    const sphereGeo = new THREE.SphereGeometry(0.016, 12, 12);
    const jointMat = new THREE.MeshBasicMaterial({ color: 0x38bdf8, depthTest: false, transparent: true, opacity: 0.95 });
    const lineMat = new THREE.LineBasicMaterial({ color: 0x34d399, depthTest: false, transparent: true, opacity: 0.85, linewidth: 2 });

    for (const [name, pos] of jointPositions.entries()) {
      const sphere = new THREE.Mesh(sphereGeo, jointMat);
      sphere.position.copy(pos);
      sphere.renderOrder = 999;
      sphere.name = `VisualJoint_${name}`;
      group.add(sphere);
    }

    for (const def of jointsDef) {
      if (def.parentName) {
        const parentPos = jointPositions.get(def.parentName)!;
        const childPos = jointPositions.get(def.name)!;

        const lineGeo = new THREE.BufferGeometry().setFromPoints([parentPos, childPos]);
        const line = new THREE.Line(lineGeo, lineMat);
        line.renderOrder = 998;
        group.add(line);
      }
    }

    return group;
  }

  /**
   * Áp dụng cử động Animation thử nghiệm trực tiếp lên khung xương (AAA Cinematic Biomechanics Engine)
   */
  public static applyTestPose(bonesMap: Map<string, THREE.Bone>, poseName: string, progress: number = 0): void {
    // Reset all bones to their initial rest pose & position
    for (const bone of bonesMap.values()) {
      if (bone.userData.initialRotation) {
        bone.rotation.copy(bone.userData.initialRotation);
      } else {
        bone.rotation.set(0, 0, 0);
      }
      if (bone.userData.initialPosition) {
        bone.position.copy(bone.userData.initialPosition);
      }
    }

    if (poseName === 't_pose') return;

    const t = progress * Math.PI * 2;
    const rotateBone = (jointName: string, dx: number, dy: number, dz: number) => {
      const bone = bonesMap.get(jointName);
      if (!bone) return;
      const init = bone.userData.initialRotation || new THREE.Euler();
      bone.rotation.set(init.x + dx, init.y + dy, init.z + dz);
    };

    const translateBone = (jointName: string, dx: number, dy: number, dz: number) => {
      const bone = bonesMap.get(jointName);
      if (!bone) return;
      const init = bone.userData.initialPosition || new THREE.Vector3();
      bone.position.set(init.x + dx, init.y + dy, init.z + dz);
    };

    switch (poseName) {
      case 'idle':
        this.applyIdleKinematics(rotateBone, translateBone, t);
        break;
      case 'walk':
        this.applyWalkKinematics(rotateBone, translateBone, t);
        break;
      case 'run':
        this.applyRunKinematics(rotateBone, translateBone, t);
        break;
      case 'slash':
        this.applySlashKinematics(rotateBone, translateBone, progress);
        break;
      case 'cast_spell':
        this.applySpellKinematics(rotateBone, translateBone, t);
        break;
      case 'defend':
        this.applyDefendKinematics(rotateBone, translateBone, t);
        break;
      case 'dance':
        this.applyDanceKinematics(rotateBone, translateBone, t);
        break;
      case 'wave':
        this.applyWaveKinematics(rotateBone, translateBone, t);
        break;
      case 'sit':
        this.applySitKinematics(rotateBone, translateBone, t);
        break;
      default:
        break;
    }

    // Force GPU matrix transformation update on all animated bones
    for (const bone of bonesMap.values()) {
      bone.updateMatrix();
      bone.updateMatrixWorld(true);
    }
  }

  private static applyIdleKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t * 1.5) * 0.04;
    const weightShift = Math.sin(t * 0.5) * 0.025;

    trans('Hips', weightShift * 0.04, Math.sin(t * 1.5) * 0.008, 0);
    rot('Hips', 0.02, weightShift * 0.08, weightShift * 0.03);
    rot('Spine', 0.03 + breathe, -weightShift * 0.06, -weightShift * 0.02);
    rot('Chest', 0.04 + breathe * 1.2, -weightShift * 0.04, 0);
    rot('Neck', -0.02 - breathe * 0.3, 0, 0);
    rot('Head', 0.03 - breathe * 0.4, -weightShift * 0.05, weightShift * 0.02);

    rot('LeftShoulder', 0, 0, breathe * 0.03);
    rot('RightShoulder', 0, 0, -breathe * 0.03);
    rot('LeftUpperArm', 0.05 + breathe * 0.2, 0.02, -0.08 - breathe * 0.04);
    rot('LeftLowerArm', -0.22 - breathe * 0.1, 0, 0);
    rot('RightUpperArm', 0.05 + breathe * 0.2, -0.02, 0.08 + breathe * 0.04);
    rot('RightLowerArm', -0.22 - breathe * 0.1, 0, 0);

    rot('LeftUpperLeg', -0.02, 0, -weightShift * 0.04);
    rot('LeftLowerLeg', 0.04, 0, 0);
    rot('RightUpperLeg', -0.02, 0, weightShift * 0.04);
    rot('RightLowerLeg', 0.04, 0, 0);
  }

  private static applyWalkKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsY = -Math.abs(Math.sin(t)) * 0.035;
    const hipsX = Math.sin(t) * 0.028;
    const hipsYaw = -Math.sin(t) * 0.12;
    const hipsRoll = Math.sin(t) * 0.045;

    trans('Hips', hipsX, hipsY, 0);
    rot('Hips', 0.04, hipsYaw, hipsRoll);
    rot('Spine', 0.03, -hipsYaw * 0.6, -hipsRoll * 0.6);
    rot('Chest', 0.04, -hipsYaw * 0.7, -hipsRoll * 0.4);
    rot('Neck', 0, hipsYaw * 0.3, 0);
    rot('Head', -0.03, hipsYaw * 0.2, -hipsRoll * 0.3);

    const legL = Math.sin(t) * 0.48;
    const kneeL = Math.max(0, -Math.sin(t - 0.25)) * 0.95 + 0.05;
    const footL = Math.sin(t - 0.5) * 0.35;
    rot('LeftUpperLeg', legL, 0, -hipsRoll * 0.5);
    rot('LeftLowerLeg', -kneeL, 0, 0);
    rot('LeftFoot', footL, 0, 0);

    const legR = -legL;
    const kneeR = Math.max(0, Math.sin(t - 0.25)) * 0.95 + 0.05;
    const footR = -footL;
    rot('RightUpperLeg', legR, 0, hipsRoll * 0.5);
    rot('RightLowerLeg', -kneeR, 0, 0);
    rot('RightFoot', footR, 0, 0);

    const armL = legR * 0.8;
    rot('LeftShoulder', 0, armL * 0.15, -0.02);
    rot('LeftUpperArm', armL, 0.05, -0.12);
    rot('LeftLowerArm', -0.25 - Math.max(0, armL) * 0.45, 0, 0);
    rot('LeftHand', Math.sin(t + 0.4) * 0.15, 0, 0);

    const armR = legL * 0.8;
    rot('RightShoulder', 0, armR * 0.15, 0.02);
    rot('RightUpperArm', armR, -0.05, 0.12);
    rot('RightLowerArm', -0.25 - Math.max(0, armR) * 0.45, 0, 0);
    rot('RightHand', -Math.sin(t + 0.4) * 0.15, 0, 0);
  }

  private static applyRunKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsY = -Math.abs(Math.sin(t)) * 0.06;
    trans('Hips', Math.sin(t) * 0.035, hipsY, 0);
    rot('Hips', 0.22, -Math.sin(t) * 0.18, Math.sin(t) * 0.06);
    rot('Spine', 0.12, Math.sin(t) * 0.12, -Math.sin(t) * 0.04);
    rot('Chest', 0.10, Math.sin(t) * 0.15, 0);
    rot('Head', -0.18, -Math.sin(t) * 0.05, 0);

    const legL = Math.sin(t) * 0.85;
    const kneeL = Math.max(0, -Math.sin(t - 0.2)) * 1.55 + 0.15;
    const footL = Math.sin(t - 0.4) * 0.45;
    rot('LeftUpperLeg', legL, 0, -0.05);
    rot('LeftLowerLeg', -kneeL, 0, 0);
    rot('LeftFoot', footL, 0, 0);

    const legR = -legL;
    const kneeR = Math.max(0, Math.sin(t - 0.2)) * 1.55 + 0.15;
    const footR = -footL;
    rot('RightUpperLeg', legR, 0, 0.05);
    rot('RightLowerLeg', -kneeR, 0, 0);
    rot('RightFoot', footR, 0, 0);

    const armL = -legL * 0.95;
    rot('LeftUpperArm', armL, 0.1, -0.15);
    rot('LeftLowerArm', -1.35, 0, 0);

    const armR = -legR * 0.95;
    rot('RightUpperArm', armR, -0.1, 0.15);
    rot('RightLowerArm', -1.35, 0, 0);
  }

  private static applySlashKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    progress: number
  ): void {
    const p = progress;
    if (p < 0.32) {
      const k = p / 0.32;
      trans('Hips', 0.04 * k, -0.06 * k, -0.05 * k);
      rot('Hips', 0.05 * k, -0.45 * k, 0);
      rot('Spine', 0.08 * k, -0.55 * k, 0);
      rot('Chest', 0.06 * k, -0.45 * k, 0);
      rot('Head', 0.04, 0.35 * k, 0);

      rot('RightShoulder', 0.15 * k, 0, 0.15 * k);
      rot('RightUpperArm', -1.2 * k, 0.4 * k, 0.6 * k);
      rot('RightLowerArm', -1.4 * k, 0, 0);
      rot('LeftUpperArm', 0.3 * k, 0, -0.4 * k);
      rot('LeftLowerArm', -0.6 * k, 0, 0);

      rot('LeftUpperLeg', -0.2 * k, 0, -0.15 * k);
      rot('LeftLowerLeg', 0.3 * k, 0, 0);
      rot('RightUpperLeg', 0.25 * k, 0, 0.15 * k);
      rot('RightLowerLeg', 0.2 * k, 0, 0);
    } else if (p < 0.52) {
      const k = (p - 0.32) / 0.20;
      trans('Hips', 0.04 - 0.08 * k, -0.06 - 0.02 * k, -0.05 + 0.13 * k);
      rot('Hips', 0.05, -0.45 + 0.95 * k, 0);
      rot('Spine', 0.08 + 0.08 * k, -0.55 + 1.2 * k, 0);
      rot('Chest', 0.06 + 0.06 * k, -0.45 + 1.0 * k, 0);
      rot('Head', 0.04, 0.35 - 0.5 * k, 0);

      rot('RightUpperArm', -1.2 + 1.8 * k, 0.4 - 0.8 * k, 0.6 - 1.1 * k);
      rot('RightLowerArm', -1.4 + 0.9 * k, 0, 0);
      rot('RightHand', 0.6 * k, 0, 0);
      rot('LeftUpperArm', 0.3 - 0.5 * k, 0, -0.4 + 0.2 * k);

      rot('LeftUpperLeg', -0.2 - 0.25 * k, 0, -0.15);
      rot('LeftLowerLeg', 0.3 + 0.25 * k, 0, 0);
      rot('RightUpperLeg', 0.25 - 0.1 * k, 0, 0.15);
      rot('RightLowerLeg', 0.2 - 0.3 * k, 0, 0);
    } else {
      const k = (p - 0.52) / 0.48;
      const settle = (1 - k);
      trans('Hips', -0.04 * settle, -0.08 * settle, 0.08 * settle);
      rot('Hips', 0.05 * settle, 0.50 * settle, 0);
      rot('Spine', 0.16 * settle, 0.65 * settle, 0);
      rot('Chest', 0.12 * settle, 0.55 * settle, 0);

      rot('RightUpperArm', 0.6 * settle, -0.4 * settle, -0.5 * settle);
      rot('RightLowerArm', -0.5 * settle, 0, 0);
      rot('RightHand', 0.6 * settle, 0, 0);

      rot('LeftUpperLeg', -0.45 * settle, 0, -0.15 * settle);
      rot('LeftLowerLeg', 0.55 * settle, 0, 0);
      rot('RightUpperLeg', 0.15 * settle, 0, 0.15 * settle);
      rot('RightLowerLeg', -0.1 * settle, 0, 0);
    }
  }

  private static applySpellKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    trans('Hips', 0, 0.04 + Math.sin(t) * 0.03, 0);
    rot('Hips', -0.08, Math.sin(t * 0.5) * 0.05, 0);
    rot('Spine', -0.15 + Math.sin(t) * 0.03, 0, 0);
    rot('Chest', -0.18 + Math.sin(t) * 0.04, 0, 0);
    rot('Head', -0.28, 0, 0);

    rot('LeftUpperLeg', 0.25, 0, -0.12);
    rot('LeftLowerLeg', -0.45, 0, 0);
    rot('LeftFoot', -0.55, 0, 0);
    rot('RightUpperLeg', 0.15, 0, 0.12);
    rot('RightLowerLeg', -0.35, 0, 0);
    rot('RightFoot', -0.55, 0, 0);

    rot('LeftUpperArm', -0.85 + Math.sin(t) * 0.15, 0.35, -0.65 + Math.cos(t) * 0.1);
    rot('LeftLowerArm', -0.85 + Math.cos(t) * 0.2, 0.3, 0);
    rot('LeftHand', Math.sin(t * 2) * 0.25, Math.cos(t * 2) * 0.25, 0.15);
    rot('RightUpperArm', -0.85 + Math.sin(t) * 0.15, -0.35, 0.65 - Math.cos(t) * 0.1);
    rot('RightLowerArm', -0.85 + Math.cos(t) * 0.2, -0.3, 0);
    rot('RightHand', Math.sin(t * 2) * 0.25, -Math.cos(t * 2) * 0.25, -0.15);
  }

  private static applyDefendKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t * 2) * 0.03;
    const bob = Math.abs(Math.sin(t * 2)) * 0.02;

    trans('Hips', 0, -0.08 - bob, 0);
    rot('Hips', 0.12, 0.25, 0);
    rot('Spine', 0.15 + breathe, -0.12, 0);
    rot('Chest', 0.10 + breathe, -0.10, 0);
    rot('Head', -0.12, -0.15, 0);

    rot('LeftUpperLeg', -0.45, 0.15, -0.22);
    rot('LeftLowerLeg', 0.65, 0, 0);
    rot('LeftFoot', -0.15, 0, 0);
    rot('RightUpperLeg', 0.25, -0.15, 0.22);
    rot('RightLowerLeg', 0.55, 0, 0);
    rot('RightFoot', 0.15, 0, 0);

    rot('LeftShoulder', 0.12, 0, 0.1);
    rot('LeftUpperArm', -0.75 + breathe, 0.45, -0.28);
    rot('LeftLowerArm', -1.55, 0.35, 0);
    rot('LeftHand', 0.35, 0.15, 0);
    rot('RightShoulder', 0.12, 0, -0.1);
    rot('RightUpperArm', -0.65 + breathe, -0.40, 0.32);
    rot('RightLowerArm', -1.65, -0.35, 0);
    rot('RightHand', 0.35, -0.15, 0);
  }

  private static applyDanceKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const hipsX = Math.sin(t * 2) * 0.05;
    const hipsY = -Math.abs(Math.sin(t * 2)) * 0.04;
    trans('Hips', hipsX, hipsY, 0);
    rot('Hips', 0.05, Math.sin(t) * 0.25, -Math.sin(t * 2) * 0.15);
    rot('Spine', 0.04, -Math.sin(t) * 0.20, Math.sin(t * 2) * 0.12);
    rot('Chest', 0.08, -Math.sin(t) * 0.15, Math.sin(t * 2) * 0.08);
    rot('Head', -0.05, Math.sin(t) * 0.15, -Math.sin(t * 2) * 0.10);

    rot('LeftUpperLeg', Math.sin(t) * 0.25, 0, -0.15 - Math.sin(t * 2) * 0.1);
    rot('LeftLowerLeg', Math.abs(Math.sin(t)) * 0.45, 0, 0);
    rot('RightUpperLeg', -Math.sin(t) * 0.25, 0, 0.15 + Math.sin(t * 2) * 0.1);
    rot('RightLowerLeg', Math.abs(Math.cos(t)) * 0.45, 0, 0);

    rot('LeftUpperArm', -0.65 + Math.sin(t) * 0.45, 0.2, -0.45 + Math.cos(t) * 0.35);
    rot('LeftLowerArm', -0.85 + Math.sin(t * 2) * 0.4, 0, 0);
    rot('RightUpperArm', -0.65 - Math.sin(t) * 0.45, -0.2, 0.45 - Math.cos(t) * 0.35);
    rot('RightLowerArm', -0.85 - Math.sin(t * 2) * 0.4, 0, 0);
  }

  private static applyWaveKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const wave = Math.sin(t * 3.5) * 0.42;
    const breathe = Math.sin(t) * 0.03;

    trans('Hips', -0.03, -0.01, 0);
    rot('Hips', 0.02, 0.08, -0.04);
    rot('Spine', 0.04 + breathe, -0.06, 0.03);
    rot('Chest', 0.05 + breathe, -0.05, 0.02);
    rot('Head', 0.04, -0.12, 0.12);

    rot('RightShoulder', 0.15, 0, -0.1);
    rot('RightUpperArm', -1.55, 0.32, 0.72);
    rot('RightLowerArm', -0.65, wave * 0.85, 0.2);
    rot('RightHand', 0.1, wave * 0.9, 0.15);

    rot('LeftUpperArm', 0.08, 0, -0.12);
    rot('LeftLowerArm', -0.18, 0, 0);
  }

  private static applySitKinematics(
    rot: (j: string, x: number, y: number, z: number) => void,
    trans: (j: string, x: number, y: number, z: number) => void,
    t: number
  ): void {
    const breathe = Math.sin(t) * 0.03;
    trans('Hips', 0, -0.45, 0);
    rot('Hips', -0.08, 0, 0);
    rot('Spine', 0.06 + breathe, 0, 0);
    rot('Chest', 0.04 + breathe * 0.8, 0, 0);
    rot('Head', 0.02, 0, 0);

    rot('LeftUpperLeg', -1.57, 0, -0.08);
    rot('LeftLowerLeg', 1.57, 0, 0);
    rot('LeftFoot', 0.05, 0, 0);
    rot('RightUpperLeg', -1.57, 0, 0.08);
    rot('RightLowerLeg', 1.57, 0, 0);
    rot('RightFoot', 0.05, 0, 0);

    rot('LeftUpperArm', -0.25, 0, -0.15);
    rot('LeftLowerArm', -0.55, 0, 0);
    rot('RightUpperArm', -0.25, 0, 0.15);
    rot('RightLowerArm', -0.55, 0, 0);
  }
}
