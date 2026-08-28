import * as THREE from 'three';
import { ProceduralMotionEngine } from './ProceduralMotionEngine';

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
    // Dọn dẹp bất kỳ visualizer hoặc helper cũ nào khỏi modelGroup trước khi xử lý
    const toRemove: THREE.Object3D[] = [];
    modelGroup.traverse((c) => {
      if (
        c.name.startsWith('VisualJoint_') ||
        c.name === 'NativeSkeletonHelperGroup' ||
        (c as any).isSkeletonHelper ||
        (c as any).isLine
      ) {
        toRemove.push(c);
      }
    });
    toRemove.forEach((c) => {
      if (c.parent) c.parent.remove(c);
    });

    // 0. Kiểm tra nếu mô hình đã có sẵn khung xương (Native Rigged Model)
    const existingBones: THREE.Bone[] = [];
    const existingSkinnedMeshes: THREE.SkinnedMesh[] = [];
    modelGroup.traverse((c) => {
      if ((c as THREE.Bone).isBone) existingBones.push(c as THREE.Bone);
      if ((c as THREE.SkinnedMesh).isSkinnedMesh) existingSkinnedMeshes.push(c as THREE.SkinnedMesh);
    });

    if (existingBones.length > 0) {
      modelGroup.updateMatrixWorld(true);

      // Cache initial rest orientation (T-pose) for each bone safely
      existingBones.forEach((b) => {
        let initRot: THREE.Euler;
        const rawRot = b.userData.initialRotation;
        if (rawRot instanceof THREE.Euler) {
          initRot = rawRot.clone();
        } else if (rawRot && typeof rawRot.x === 'number') {
          initRot = new THREE.Euler(rawRot.x, rawRot.y, rawRot.z, rawRot.order || 'XYZ');
        } else if (rawRot && typeof (rawRot as any)._x === 'number') {
          initRot = new THREE.Euler(
            (rawRot as any)._x,
            (rawRot as any)._y,
            (rawRot as any)._z,
            (rawRot as any)._order || 'XYZ'
          );
        } else {
          initRot = b.rotation.clone();
        }
        b.userData.initialRotation = initRot;

        let initPos: THREE.Vector3;
        const rawPos = b.userData.initialPosition;
        if (rawPos instanceof THREE.Vector3) {
          initPos = rawPos.clone();
        } else if (rawPos && typeof rawPos.x === 'number') {
          initPos = new THREE.Vector3(rawPos.x, rawPos.y, rawPos.z);
        } else {
          initPos = b.position.clone();
        }
        b.userData.initialPosition = initPos;
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
   * Tự động phát hiện và trích xuất bonesMap từ một mô hình đã có sẵn xương (Native Skeletal Model)
   */
  public static buildBonesMapFromExistingModel(group: THREE.Group): AutoRigResult {
    const bonesMap = new Map<string, THREE.Bone>();
    const allBones: THREE.Bone[] = [];
    const skinnedMeshes: THREE.SkinnedMesh[] = [];

    group.traverse((c) => {
      if ((c as THREE.Bone).isBone) {
        const bone = c as THREE.Bone;
        allBones.push(bone);
        const lower = bone.name.toLowerCase();

        if (lower.includes('hips') || lower.includes('pelvis') || lower.includes('root')) bonesMap.set('Hips', bone);
        else if (lower.includes('spine')) bonesMap.set('Spine', bone);
        else if (lower.includes('chest') || lower.includes('spine1') || lower.includes('spine2')) bonesMap.set('Chest', bone);
        else if (lower.includes('neck')) bonesMap.set('Neck', bone);
        else if (lower.includes('head')) bonesMap.set('Head', bone);

        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('shoulder') || lower.includes('clavicle'))) bonesMap.set('LeftShoulder', bone);
        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('arm') && !lower.includes('fore') && !lower.includes('lower') && !lower.includes('hand'))) bonesMap.set('LeftUpperArm', bone);
        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('forearm') || lower.includes('lowerarm') || lower.includes('elbow'))) bonesMap.set('LeftLowerArm', bone);
        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('hand') || lower.includes('wrist'))) bonesMap.set('LeftHand', bone);

        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('shoulder') || lower.includes('clavicle'))) bonesMap.set('RightShoulder', bone);
        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('arm') && !lower.includes('fore') && !lower.includes('lower') && !lower.includes('hand'))) bonesMap.set('RightUpperArm', bone);
        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('forearm') || lower.includes('lowerarm') || lower.includes('elbow'))) bonesMap.set('RightLowerArm', bone);
        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('hand') || lower.includes('wrist'))) bonesMap.set('RightHand', bone);

        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('upleg') || lower.includes('upperleg') || lower.includes('thigh'))) bonesMap.set('LeftUpperLeg', bone);
        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('leg') || lower.includes('lowerleg') || lower.includes('calf') || lower.includes('knee'))) bonesMap.set('LeftLowerLeg', bone);
        else if ((lower.includes('left') || lower.startsWith('l_') || lower.endsWith('_l')) && (lower.includes('foot') || lower.includes('ankle'))) bonesMap.set('LeftFoot', bone);

        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('upleg') || lower.includes('upperleg') || lower.includes('thigh'))) bonesMap.set('RightUpperLeg', bone);
        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('leg') || lower.includes('lowerleg') || lower.includes('calf') || lower.includes('knee'))) bonesMap.set('RightLowerLeg', bone);
        else if ((lower.includes('right') || lower.startsWith('r_') || lower.endsWith('_r')) && (lower.includes('foot') || lower.includes('ankle'))) bonesMap.set('RightFoot', bone);
      } else if ((c as THREE.SkinnedMesh).isSkinnedMesh) {
        skinnedMeshes.push(c as THREE.SkinnedMesh);
      }
    });

    const skeleton = allBones.length > 0 ? new THREE.Skeleton(allBones) : new THREE.Skeleton([]);
    const jointVisualizer = new THREE.Group();

    return {
      rootGroup: group,
      skeleton,
      skinnedMeshes,
      bonesMap,
      jointVisualizer,
    };
  }

  /**
   * Áp dụng cử động Animation thử nghiệm trực tiếp lên khung xương
   * (Ủy quyền toàn bộ cho ProceduralMotionEngine chuẩn giải phẫu học AAA)
   */
  public static applyTestPose(bonesMap: Map<string, THREE.Bone>, poseName: string, progress: number = 0): void {
    ProceduralMotionEngine.applyMotion(bonesMap, poseName, progress);
  }
}

