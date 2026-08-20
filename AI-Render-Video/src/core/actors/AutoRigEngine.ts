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
   * Tự động tạo bộ khung xương và gắn da (Capsule Linear Blend Skinning) cho một Group hoặc Mesh
   */
  public static rigModel(modelGroup: THREE.Group | THREE.Object3D): AutoRigResult {
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
   * Áp dụng cử động Animation thử nghiệm trực tiếp lên khung xương (Natural Biomechanics Pose Testing)
   */
  public static applyTestPose(bonesMap: Map<string, THREE.Bone>, poseName: string, progress: number = 0): void {
    // Reset all bones
    for (const bone of bonesMap.values()) {
      bone.rotation.set(0, 0, 0);
    }

    const t = progress * Math.PI * 2;

    switch (poseName) {
      case 'walk': {
        // Chu kỳ bước đi tự nhiên (Walk Cycle)
        const leftLegAngle = Math.sin(t) * 0.45;
        const rightLegAngle = -leftLegAngle;

        bonesMap.get('LeftUpperLeg')?.rotation.set(leftLegAngle, 0, 0);
        bonesMap.get('LeftLowerLeg')?.rotation.set(leftLegAngle < 0 ? -leftLegAngle * 1.1 : 0, 0, 0);

        bonesMap.get('RightUpperLeg')?.rotation.set(rightLegAngle, 0, 0);
        bonesMap.get('RightLowerLeg')?.rotation.set(rightLegAngle < 0 ? -rightLegAngle * 1.1 : 0, 0, 0);

        // Tay đánh nhịp đối xứng
        const armSwing = Math.sin(t) * 0.35;
        bonesMap.get('LeftUpperArm')?.rotation.set(-armSwing, 0, -0.15);
        bonesMap.get('LeftLowerArm')?.rotation.set(-0.2, 0, 0);
        bonesMap.get('RightUpperArm')?.rotation.set(armSwing, 0, 0.15);
        bonesMap.get('RightLowerArm')?.rotation.set(-0.2, 0, 0);

        bonesMap.get('Spine')?.rotation.set(0, Math.sin(t) * 0.08, 0);
        bonesMap.get('Head')?.rotation.set(0, -Math.sin(t) * 0.04, 0);
        break;
      }

      case 'slash': {
        // Hoạt cảnh vung kiếm chém ngang uy lực
        const cycle = Math.sin(t);
        bonesMap.get('Spine')?.rotation.set(0.08, cycle * 0.4, 0);
        bonesMap.get('Chest')?.rotation.set(0.04, cycle * 0.25, 0);

        bonesMap.get('RightUpperArm')?.rotation.set(-0.6 + cycle * 0.8, 0.2, 0.4);
        bonesMap.get('RightLowerArm')?.rotation.set(-0.5, 0, 0);
        bonesMap.get('LeftUpperArm')?.rotation.set(0.2, 0, -0.3);

        // Thế chân tấn
        bonesMap.get('LeftUpperLeg')?.rotation.set(-0.25, 0, -0.1);
        bonesMap.get('LeftLowerLeg')?.rotation.set(0.3, 0, 0);
        bonesMap.get('RightUpperLeg')?.rotation.set(0.2, 0, 0.1);
        break;
      }

      case 'defend': {
        // Thế thủ phòng vệ (Martial Arts Guard Stance)
        const breathe = Math.sin(t) * 0.03;
        bonesMap.get('Spine')?.rotation.set(0.08 + breathe, 0.15, 0);

        bonesMap.get('LeftUpperArm')?.rotation.set(-0.6, 0.3, -0.2);
        bonesMap.get('LeftLowerArm')?.rotation.set(-0.9, 0, 0);
        bonesMap.get('RightUpperArm')?.rotation.set(-0.5, -0.3, 0.2);
        bonesMap.get('RightLowerArm')?.rotation.set(-0.9, 0, 0);

        // Hạ trọng tâm
        bonesMap.get('LeftUpperLeg')?.rotation.set(-0.25, 0, -0.1);
        bonesMap.get('LeftLowerLeg')?.rotation.set(0.3, 0, 0);
        bonesMap.get('RightUpperLeg')?.rotation.set(0.15, 0, 0.1);
        bonesMap.get('RightLowerLeg')?.rotation.set(0.2, 0, 0);
        break;
      }

      case 'wave': {
        // Cử động giơ tay vẫy chào thân thiện
        const wave = Math.sin(t * 3) * 0.3;
        bonesMap.get('RightUpperArm')?.rotation.set(0, 0, 1.6);
        bonesMap.get('RightLowerArm')?.rotation.set(0, 0, 0.5 + wave);
        bonesMap.get('Head')?.rotation.set(0, 0, -0.1);
        break;
      }

      case 'sit': {
        // Tư thế ngồi nghỉ ngơi trên ghế
        bonesMap.get('LeftUpperLeg')?.rotation.set(-1.4, 0, -0.08);
        bonesMap.get('LeftLowerLeg')?.rotation.set(1.4, 0, 0);
        bonesMap.get('RightUpperLeg')?.rotation.set(-1.4, 0, 0.08);
        bonesMap.get('RightLowerLeg')?.rotation.set(1.4, 0, 0);

        bonesMap.get('LeftUpperArm')?.rotation.set(-0.2, 0, -0.12);
        bonesMap.get('LeftLowerArm')?.rotation.set(-0.4, 0, 0);
        bonesMap.get('RightUpperArm')?.rotation.set(-0.2, 0, 0.12);
        bonesMap.get('RightLowerArm')?.rotation.set(-0.4, 0, 0);
        break;
      }

      case 't_pose':
      default:
        break;
    }
  }
}
