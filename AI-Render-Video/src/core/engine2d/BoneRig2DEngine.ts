/**
 * BoneRig2DEngine — Forward Kinematics engine for 2D bone rigging.
 * Handles bone hierarchy, transforms, preset templates, and angle sync.
 */
import {
  BoneNode,
  BoneRigDefinition,
  BoneRigPresetId,
  Character2DPartType,
  Character2DAngle,
  AngleSlotEntry,
} from '../../types/scene2d';

// ─── Preset Bone Templates ────────────────────────────────────────

/** Hand bone rig: wrist root → 5 fingers × 2-3 joints each */
export function createHandBoneTemplate(): BoneNode[] {
  return [
    // Root: wrist at bottom center of hand image (stump)
    { id: 'wrist_root', name: 'Cổ Tay (Wrist)', parentId: null, position: [0.50, 0.92], rotation: 0, length: 0.18, color: '#f59e0b' },
    // Palm: extends from wrist tip upward to center of palm / knuckle line
    { id: 'palm_center', name: 'Tâm Bàn Tay (Palm)', parentId: 'wrist_root', position: [0.0, 0.0], rotation: 0, length: 0.26, color: '#f59e0b' },

    // ─── Thumb ───
    { id: 'thumb_base', name: 'Gốc Ngón Cái', parentId: 'palm_center', position: [-0.18, 0.20], rotation: -52, length: 0.16, color: '#ef4444' },
    { id: 'thumb_tip', name: 'Đầu Ngón Cái', parentId: 'thumb_base', position: [0.0, 0.0], rotation: 0, length: 0.15, color: '#ef4444' },

    // ─── Index Finger ───
    { id: 'index_base', name: 'Gốc Ngón Trỏ', parentId: 'palm_center', position: [-0.14, 0.01], rotation: -15, length: 0.15, color: '#3b82f6' },
    { id: 'index_mid', name: 'Giữa Ngón Trỏ', parentId: 'index_base', position: [0.0, 0.0], rotation: 0, length: 0.13, color: '#3b82f6' },
    { id: 'index_tip', name: 'Đầu Ngón Trỏ', parentId: 'index_mid', position: [0.0, 0.0], rotation: 0, length: 0.10, color: '#3b82f6' },

    // ─── Middle Finger ───
    { id: 'middle_base', name: 'Gốc Ngón Giữa', parentId: 'palm_center', position: [0.0, 0.0], rotation: 0, length: 0.16, color: '#10b981' },
    { id: 'middle_mid', name: 'Giữa Ngón Giữa', parentId: 'middle_base', position: [0.0, 0.0], rotation: 0, length: 0.15, color: '#10b981' },
    { id: 'middle_tip', name: 'Đầu Ngón Giữa', parentId: 'middle_mid', position: [0.0, 0.0], rotation: 0, length: 0.12, color: '#10b981' },

    // ─── Ring Finger ───
    { id: 'ring_base', name: 'Gốc Ngón Áp Út', parentId: 'palm_center', position: [0.14, 0.01], rotation: 18, length: 0.15, color: '#8b5cf6' },
    { id: 'ring_mid', name: 'Giữa Ngón Áp Út', parentId: 'ring_base', position: [0.0, 0.0], rotation: 0, length: 0.13, color: '#8b5cf6' },
    { id: 'ring_tip', name: 'Đầu Ngón Áp Út', parentId: 'ring_mid', position: [0.0, 0.0], rotation: 0, length: 0.10, color: '#8b5cf6' },

    // ─── Pinky Finger ───
    { id: 'pinky_base', name: 'Gốc Ngón Út', parentId: 'palm_center', position: [0.24, 0.05], rotation: 38, length: 0.12, color: '#ec4899' },
    { id: 'pinky_mid', name: 'Giữa Ngón Út', parentId: 'pinky_base', position: [0.0, 0.0], rotation: 0, length: 0.10, color: '#ec4899' },
    { id: 'pinky_tip', name: 'Đầu Ngón Út', parentId: 'pinky_mid', position: [0.0, 0.0], rotation: 0, length: 0.08, color: '#ec4899' },
  ];
}

/** Arm bone rig: shoulder → elbow → wrist */
export function createArmBoneTemplate(): BoneNode[] {
  return [
    { id: 'shoulder', name: 'Vai (Shoulder)', parentId: null, position: [0.5, 0.15], rotation: 0, length: 0.15, color: '#f59e0b' },
    { id: 'elbow', name: 'Khuỷu Tay (Elbow)', parentId: 'shoulder', position: [0.0, 0.30], rotation: 0, length: 0.15, color: '#3b82f6' },
    { id: 'wrist', name: 'Cổ Tay (Wrist)', parentId: 'elbow', position: [0.0, 0.30], rotation: 0, length: 0.08, color: '#10b981' },
  ];
}

/** Head bone rig: neck → skull → jaw + eye pivots */
export function createHeadBoneTemplate(): BoneNode[] {
  return [
    { id: 'neck_base', name: 'Gốc Cổ (Neck Base)', parentId: null, position: [0.5, 0.85], rotation: 0, length: 0.1, color: '#f59e0b' },
    { id: 'skull', name: 'Hộp Sọ (Skull)', parentId: 'neck_base', position: [0.0, -0.3], rotation: 0, length: 0.2, color: '#3b82f6' },
    { id: 'jaw', name: 'Hàm Dưới (Jaw)', parentId: 'skull', position: [0.0, 0.15], rotation: 0, length: 0.08, color: '#ef4444' },
    { id: 'eye_left', name: 'Mắt Trái', parentId: 'skull', position: [-0.12, -0.05], rotation: 0, length: 0.03, color: '#8b5cf6' },
    { id: 'eye_right', name: 'Mắt Phải', parentId: 'skull', position: [0.12, -0.05], rotation: 0, length: 0.03, color: '#8b5cf6' },
  ];
}

/** Leg bone rig: hip → knee → ankle → toe */
export function createLegBoneTemplate(): BoneNode[] {
  return [
    { id: 'hip', name: 'Hông (Hip)', parentId: null, position: [0.5, 0.1], rotation: 0, length: 0.15, color: '#f59e0b' },
    { id: 'knee', name: 'Đầu Gối (Knee)', parentId: 'hip', position: [0.0, 0.30], rotation: 0, length: 0.15, color: '#3b82f6' },
    { id: 'ankle', name: 'Mắt Cá (Ankle)', parentId: 'knee', position: [0.0, 0.30], rotation: 0, length: 0.08, color: '#10b981' },
    { id: 'toe', name: 'Ngón Chân (Toe)', parentId: 'ankle', position: [0.0, 0.08], rotation: 0, length: 0.05, color: '#ec4899' },
  ];
}

/** Torso bone rig: pelvis → spine → chest → neck */
export function createTorsoBoneTemplate(): BoneNode[] {
  return [
    { id: 'pelvis', name: 'Xương Chậu (Pelvis)', parentId: null, position: [0.5, 0.75], rotation: 0, length: 0.1, color: '#f59e0b' },
    { id: 'spine', name: 'Cột Sống (Spine)', parentId: 'pelvis', position: [0.0, -0.2], rotation: 0, length: 0.15, color: '#3b82f6' },
    { id: 'chest', name: 'Ngực (Chest)', parentId: 'spine', position: [0.0, -0.15], rotation: 0, length: 0.12, color: '#10b981' },
    { id: 'neck_top', name: 'Đỉnh Cổ (Neck)', parentId: 'chest', position: [0.0, -0.12], rotation: 0, length: 0.06, color: '#8b5cf6' },
    { id: 'shoulder_l', name: 'Vai Trái', parentId: 'chest', position: [-0.18, -0.02], rotation: -10, length: 0.05, color: '#ec4899' },
    { id: 'shoulder_r', name: 'Vai Phải', parentId: 'chest', position: [0.18, -0.02], rotation: 10, length: 0.05, color: '#ec4899' },
  ];
}

// ─── Preset Factory ───────────────────────────────────────────────

/** Get bone template by preset ID */
export function getBonePresetTemplate(
  presetId: BoneRigPresetId,
  targetPart: Character2DPartType
): BoneRigDefinition {
  const presetMap: Record<BoneRigPresetId, { bones: BoneNode[]; name: string; nameVi: string; category: BoneRigDefinition['category'] }> = {
    hand_5_fingers: { bones: createHandBoneTemplate(), name: 'Hand (5 Fingers)', nameVi: 'Bàn Tay (5 Ngón)', category: 'hand' },
    arm_3_segments: { bones: createArmBoneTemplate(), name: 'Arm (3 Segments)', nameVi: 'Cánh Tay (3 Đoạn)', category: 'arm' },
    leg_3_segments: { bones: createLegBoneTemplate(), name: 'Leg (4 Segments)', nameVi: 'Chân (4 Đoạn)', category: 'leg' },
    head_jaw_eyes: { bones: createHeadBoneTemplate(), name: 'Head (Jaw + Eyes)', nameVi: 'Đầu (Hàm + Mắt)', category: 'head' },
    torso_spine: { bones: createTorsoBoneTemplate(), name: 'Torso (Spine)', nameVi: 'Thân (Cột Sống)', category: 'torso' },
    full_body_simple: { bones: [...createTorsoBoneTemplate()], name: 'Full Body', nameVi: 'Toàn Thân', category: 'full_body' },
  };

  const preset = presetMap[presetId];
  return {
    id: `${presetId}_${Date.now()}`,
    name: preset.name,
    nameVi: preset.nameVi,
    targetPart,
    bones: preset.bones,
    category: preset.category,
  };
}

// ─── Forward Kinematics ───────────────────────────────────────────

export interface BoneWorldTransform {
  boneId: string;
  /** Absolute pixel position on canvas */
  worldX: number;
  worldY: number;
  /** Accumulated world rotation (degrees) */
  worldRotation: number;
  /** End point of the bone (tip) */
  tipX: number;
  tipY: number;
}

/**
 * Compute forward kinematics: resolve all bone world positions
 * from the hierarchy tree, given canvas dimensions.
 */
export function computeForwardKinematics(
  bones: BoneNode[],
  canvasWidth: number,
  canvasHeight: number
): BoneWorldTransform[] {
  const transforms: BoneWorldTransform[] = [];
  const boneMap = new Map<string, BoneNode>();
  const transformMap = new Map<string, BoneWorldTransform>();

  bones.forEach((b) => boneMap.set(b.id, b));

  // Topological sort: process parents before children
  const processed = new Set<string>();
  const queue = bones.filter((b) => b.parentId === null);

  while (queue.length > 0) {
    const bone = queue.shift()!;
    if (processed.has(bone.id)) continue;

    let worldX: number;
    let worldY: number;
    let worldRotation: number;

    if (bone.parentId === null) {
      // Root bone: position is in normalized coords [0..1]
      worldX = bone.position[0] * canvasWidth;
      worldY = bone.position[1] * canvasHeight;
      worldRotation = bone.rotation;
    } else {
      const parentTransform = transformMap.get(bone.parentId);
      if (!parentTransform) {
        // Parent not yet processed, re-queue
        queue.push(bone);
        continue;
      }
      // Child bone attaches at parent's TIP position
      const offsetX = bone.position[0] * canvasWidth;
      const offsetY = bone.position[1] * canvasHeight;
      const parentRad = (parentTransform.worldRotation * Math.PI) / 180;
      worldX = parentTransform.tipX + Math.cos(parentRad) * offsetX - Math.sin(parentRad) * offsetY;
      worldY = parentTransform.tipY + Math.sin(parentRad) * offsetX + Math.cos(parentRad) * offsetY;
      worldRotation = parentTransform.worldRotation + bone.rotation;
    }

    const rad = (worldRotation * Math.PI) / 180;
    const boneLen = bone.length * Math.min(canvasWidth, canvasHeight);
    const tipX = worldX + Math.sin(rad) * boneLen;
    const tipY = worldY - Math.cos(rad) * boneLen;

    const transform: BoneWorldTransform = {
      boneId: bone.id,
      worldX,
      worldY,
      worldRotation,
      tipX,
      tipY,
    };

    transforms.push(transform);
    transformMap.set(bone.id, transform);
    processed.add(bone.id);

    // Queue children
    bones
      .filter((b) => b.parentId === bone.id)
      .forEach((child) => queue.push(child));
  }

  return transforms;
}

// ─── Angle Slot Defaults ──────────────────────────────────────────

/** Required angles for a body part type */
export function getDefaultAngleSlots(partType: Character2DPartType): AngleSlotEntry[] {
  const baseAngles: { angle: Character2DAngle; required: boolean }[] = [
    { angle: 'front', required: true },
    { angle: 'three_quarter_left', required: true },
    { angle: 'profile_left', required: true },
    { angle: 'back', required: false },
    { angle: 'three_quarter_right', required: false },
    { angle: 'profile_right', required: false },
  ];

  return baseAngles.map((entry) => {
    // Auto-mirror right-side angles from left-side
    const mirrorMap: Partial<Record<Character2DAngle, Character2DAngle>> = {
      three_quarter_right: 'three_quarter_left',
      profile_right: 'profile_left',
    };
    const mirrorSource = mirrorMap[entry.angle];

    return {
      angle: entry.angle,
      textureUrl: null,
      isMirrored: !!mirrorSource,
      mirrorSourceAngle: mirrorSource,
    };
  });
}

/** Suggest which bone preset to use for a given part type */
export function suggestBonePreset(partType: Character2DPartType): BoneRigPresetId {
  switch (partType) {
    case 'ban_tay':
    case 'ban_tay_trai':
    case 'ban_tay_phai':
      return 'hand_5_fingers';
    case 'bap_tay':
    case 'cang_tay':
    case 'canh_tay_trai':
    case 'canh_tay_phai':
    case 'cang_tay_trai':
    case 'cang_tay_phai':
      return 'arm_3_segments';
    case 'dui':
    case 'cang_chan':
    case 'ban_chan':
    case 'dui_trai':
    case 'dui_phai':
    case 'cang_chan_trai':
    case 'cang_chan_phai':
      return 'leg_3_segments';
    case 'dau':
    case 'khuon_mat':
    case 'khuon_mat_no_face':
      return 'head_jaw_eyes';
    case 'than_mannequin':
    case 'than_co_ban':
      return 'torso_spine';
    default:
      return 'full_body_simple';
  }
}
