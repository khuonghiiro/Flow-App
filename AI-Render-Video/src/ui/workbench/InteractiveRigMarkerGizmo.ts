import * as THREE from 'three';

export interface MarkerJointInfo {
  id: string;
  label: string;
  color: number;
  symmetryPartner?: string;
  parentJoint?: string;
}

export const MARKER_DEFINITIONS: MarkerJointInfo[] = [
  { id: 'Head', label: 'Đỉnh Đầu / Cranium', color: 0x38bdf8, parentJoint: 'Neck' },
  { id: 'Neck', label: 'Cổ / Chin Base', color: 0x38bdf8, parentJoint: 'Chest' },
  { id: 'Chest', label: 'Ngực / Chest', color: 0x38bdf8, parentJoint: 'Spine' },
  { id: 'Spine', label: 'Thắt Lưng / Spine', color: 0x38bdf8, parentJoint: 'Hips' },
  { id: 'Hips', label: 'Hông / Pelvis (Gốc)', color: 0xa855f7 },

  // Tay Trái & Phải
  { id: 'LeftShoulder', label: 'Khớp Vai Trái', color: 0x34d399, symmetryPartner: 'RightShoulder', parentJoint: 'Chest' },
  { id: 'LeftUpperArm', label: 'Bắp Tay Trái', color: 0x34d399, symmetryPartner: 'RightUpperArm', parentJoint: 'LeftShoulder' },
  { id: 'LeftLowerArm', label: 'Khuỷu Tay Trái', color: 0x34d399, symmetryPartner: 'RightLowerArm', parentJoint: 'LeftUpperArm' },
  { id: 'LeftHand', label: 'Cổ Tay Trái', color: 0x34d399, symmetryPartner: 'RightHand', parentJoint: 'LeftLowerArm' },

  { id: 'RightShoulder', label: 'Khớp Vai Phải', color: 0xf43f5e, symmetryPartner: 'LeftShoulder', parentJoint: 'Chest' },
  { id: 'RightUpperArm', label: 'Bắp Tay Phải', color: 0xf43f5e, symmetryPartner: 'LeftUpperArm', parentJoint: 'RightShoulder' },
  { id: 'RightLowerArm', label: 'Khuỷu Tay Phải', color: 0xf43f5e, symmetryPartner: 'LeftLowerArm', parentJoint: 'RightUpperArm' },
  { id: 'RightHand', label: 'Cổ Tay Phải', color: 0xf43f5e, symmetryPartner: 'LeftHand', parentJoint: 'RightLowerArm' },

  // Chân Trái & Phải
  { id: 'LeftUpperLeg', label: 'Khớp Háng Trái', color: 0x34d399, symmetryPartner: 'RightUpperLeg', parentJoint: 'Hips' },
  { id: 'LeftLowerLeg', label: 'Đầu Gối Trái', color: 0x34d399, symmetryPartner: 'RightLowerLeg', parentJoint: 'LeftUpperLeg' },
  { id: 'LeftFoot', label: 'Mắt Cá Chân Trái', color: 0x34d399, symmetryPartner: 'RightFoot', parentJoint: 'LeftLowerLeg' },

  { id: 'RightUpperLeg', label: 'Khớp Háng Phải', color: 0xf43f5e, symmetryPartner: 'LeftUpperLeg', parentJoint: 'Hips' },
  { id: 'RightLowerLeg', label: 'Đầu Gối Phải', color: 0xf43f5e, symmetryPartner: 'LeftLowerLeg', parentJoint: 'RightUpperLeg' },
  { id: 'RightFoot', label: 'Mắt Cá Chân Phải', color: 0xf43f5e, symmetryPartner: 'LeftFoot', parentJoint: 'RightLowerLeg' },
];

/**
 * InteractiveRigMarkerGizmo
 * Hệ thống hiển thị và kéo thả các điểm mốc 3D (AccuRIG Style 3D Gizmo)
 * Tự động hỗ trợ X-Axis Symmetry (kéo mốc trái, mốc phải tự dịch chuyển đối xứng)
 */
export class InteractiveRigMarkerGizmo {
  public group: THREE.Group;
  private markersMap: Map<string, THREE.Mesh> = new Map();
  private linesGroup: THREE.Group;
  private landmarks: Map<string, THREE.Vector3> = new Map();
  private isSymmetryEnabled: boolean = true;

  private selectedMarkerId: string | null = null;
  private hoveredMarkerId: string | null = null;
  private raycaster = new THREE.Raycaster();
  private dragPlane = new THREE.Plane();
  private intersectionPoint = new THREE.Vector3();
  private planeIntersect = new THREE.Vector3();

  private onLandmarksChange?: (landmarks: Map<string, THREE.Vector3>) => void;
  private onSelectMarker?: (jointId: string | null) => void;

  constructor() {
    this.group = new THREE.Group();
    this.group.name = 'InteractiveRigMarkerGizmoGroup';
    this.linesGroup = new THREE.Group();
    this.linesGroup.name = 'MarkerLinesGroup';
    this.group.add(this.linesGroup);
  }

  public setSymmetryEnabled(enabled: boolean): void {
    this.isSymmetryEnabled = enabled;
  }

  public setCallbacks(
    onLandmarksChange?: (landmarks: Map<string, THREE.Vector3>) => void,
    onSelectMarker?: (jointId: string | null) => void
  ): void {
    this.onLandmarksChange = onLandmarksChange;
    this.onSelectMarker = onSelectMarker;
  }

  /**
   * Khởi tạo hoặc cập nhật danh sách các điểm mốc 3D
   */
  public updateLandmarks(landmarks: Map<string, THREE.Vector3>): void {
    this.landmarks = new Map(landmarks);

    // Dọn dẹp markers cũ
    const toRemove: THREE.Object3D[] = [];
    this.group.children.forEach((c) => {
      if (c !== this.linesGroup) toRemove.push(c);
    });
    toRemove.forEach((c) => this.group.remove(c));
    this.markersMap.clear();

    const sphereGeo = new THREE.SphereGeometry(0.024, 16, 16);

    for (const def of MARKER_DEFINITIONS) {
      const pos = this.landmarks.get(def.id);
      if (!pos) continue;

      const mat = new THREE.MeshBasicMaterial({
        color: def.color,
        depthTest: false,
        transparent: true,
        opacity: 0.92,
      });

      const sphere = new THREE.Mesh(sphereGeo, mat);
      sphere.position.copy(pos);
      sphere.renderOrder = 1000;
      sphere.userData = { jointId: def.id, label: def.label };

      this.markersMap.set(def.id, sphere);
      this.group.add(sphere);
    }

    this.rebuildBoneLines();
  }

  /**
   * Vẽ lại các đường nối xương giữa các điểm mốc
   */
  private rebuildBoneLines(): void {
    while (this.linesGroup.children.length > 0) {
      this.linesGroup.remove(this.linesGroup.children[0]);
    }

    const lineMat = new THREE.LineBasicMaterial({
      color: 0x38bdf8,
      depthTest: false,
      transparent: true,
      opacity: 0.65,
      linewidth: 2,
    });

    for (const def of MARKER_DEFINITIONS) {
      if (def.parentJoint && this.landmarks.has(def.parentJoint) && this.landmarks.has(def.id)) {
        const parentPos = this.landmarks.get(def.parentJoint)!;
        const childPos = this.landmarks.get(def.id)!;

        const geo = new THREE.BufferGeometry().setFromPoints([parentPos, childPos]);
        const line = new THREE.Line(geo, lineMat);
        line.renderOrder = 999;
        this.linesGroup.add(line);
      }
    }
  }

  /**
   * Xử lý sự kiện Pointer Down (Bắt đầu kéo thả mốc)
   */
  public handlePointerDown(
    mouseNorm: THREE.Vector2,
    camera: THREE.Camera
  ): boolean {
    this.raycaster.setFromCamera(mouseNorm, camera);
    const meshes = Array.from(this.markersMap.values());
    const intersects = this.raycaster.intersectObjects(meshes, false);

    if (intersects.length > 0) {
      const hit = intersects[0];
      const jointId = hit.object.userData.jointId;
      this.selectedMarkerId = jointId;

      // Tạo mặt phẳng kéo vuông góc với hướng nhìn camera đi qua vị trí marker
      const markerWorldPos = hit.object.position;
      const cameraDir = new THREE.Vector3();
      camera.getWorldDirection(cameraDir);
      this.dragPlane.setFromNormalAndCoplanarPoint(cameraDir.negate(), markerWorldPos);

      if (this.onSelectMarker) this.onSelectMarker(jointId);
      return true; // Chặn OrbitControls khi đang kéo mốc
    }

    this.selectedMarkerId = null;
    if (this.onSelectMarker) this.onSelectMarker(null);
    return false;
  }

  /**
   * Xử lý sự kiện Pointer Move (Đang di chuyển mốc trong 3D)
   */
  public handlePointerMove(
    mouseNorm: THREE.Vector2,
    camera: THREE.Camera
  ): { isDragging: boolean; hoveredJoint: string | null } {
    this.raycaster.setFromCamera(mouseNorm, camera);

    if (this.selectedMarkerId) {
      // Đang kéo thả marker
      if (this.raycaster.ray.intersectPlane(this.dragPlane, this.planeIntersect)) {
        const newPos = this.planeIntersect.clone();

        // 1. Cập nhật marker đang kéo
        this.landmarks.set(this.selectedMarkerId, newPos);
        const markerMesh = this.markersMap.get(this.selectedMarkerId);
        if (markerMesh) markerMesh.position.copy(newPos);

        // 2. X-Axis Symmetry Mirror: Tự động cập nhật bên đối diện
        if (this.isSymmetryEnabled) {
          const def = MARKER_DEFINITIONS.find((d) => d.id === this.selectedMarkerId);
          if (def?.symmetryPartner) {
            const symPos = new THREE.Vector3(-newPos.x, newPos.y, newPos.z);
            this.landmarks.set(def.symmetryPartner, symPos);
            const symMesh = this.markersMap.get(def.symmetryPartner);
            if (symMesh) symMesh.position.copy(symPos);
          } else if (this.selectedMarkerId === 'Head' || this.selectedMarkerId === 'Neck' || this.selectedMarkerId === 'Chest' || this.selectedMarkerId === 'Spine' || this.selectedMarkerId === 'Hips') {
            // Khớp trung tâm giữ nguyên trên trục X=0
            newPos.x = 0;
            this.landmarks.set(this.selectedMarkerId, newPos);
            if (markerMesh) markerMesh.position.copy(newPos);
          }
        }

        this.rebuildBoneLines();
        if (this.onLandmarksChange) this.onLandmarksChange(this.landmarks);
      }

      return { isDragging: true, hoveredJoint: this.selectedMarkerId };
    }

    // Hover detection
    const meshes = Array.from(this.markersMap.values());
    const intersects = this.raycaster.intersectObjects(meshes, false);
    if (intersects.length > 0) {
      const jointId = intersects[0].object.userData.jointId;
      this.hoveredMarkerId = jointId;
      return { isDragging: false, hoveredJoint: jointId };
    }

    this.hoveredMarkerId = null;
    return { isDragging: false, hoveredJoint: null };
  }

  /**
   * Xử lý sự kiện Pointer Up (Thả chuột)
   */
  public handlePointerUp(): void {
    this.selectedMarkerId = null;
  }

  public getLandmarks(): Map<string, THREE.Vector3> {
    return new Map(this.landmarks);
  }

  public dispose(): void {
    while (this.group.children.length > 0) {
      this.group.remove(this.group.children[0]);
    }
    this.markersMap.clear();
  }
}
