/**
 * MultiAngleRigAssembler — Main tab component for 2D→3D bone rigging.
 * Layout: [Angle Slot Wing] | [Viewport Canvas] | [Bone Editor Panel]
 * Users upload multi-angle textures and apply bone rigs with preset templates.
 */
import React, { useState, useCallback } from 'react';
import {
  Character2DAngle,
  Character2DPartType,
  AngleSlotEntry,
  BoneNode,
  BoneRigDefinition,
  MultiAngleRigAssembly,
} from '../../types/scene2d';
import {
  getDefaultAngleSlots,
  suggestBonePreset,
  getBonePresetTemplate,
} from '../../core/engine2d/BoneRig2DEngine';
import { RigAngleSlotWing } from './rig-assembler/RigAngleSlotWing';
import { RigViewportCanvas } from './rig-assembler/RigViewportCanvas';
import { RigBoneEditorPanel } from './rig-assembler/RigBoneEditorPanel';

// ─── Body Part Selector Items ─────────────────────────────────────

interface PartSelectorItem {
  partType: Character2DPartType;
  label: string;
  icon: string;
}

const PART_SELECTOR_ITEMS: PartSelectorItem[] = [
  { partType: 'ban_tay', label: 'Bàn Tay', icon: '🖐️' },
  { partType: 'bap_tay', label: 'Bắp Tay', icon: '💪' },
  { partType: 'cang_tay', label: 'Cẳng Tay', icon: '🦾' },
  { partType: 'dau', label: 'Đầu', icon: '🗣️' },
  { partType: 'than_mannequin', label: 'Thân', icon: '🧍' },
  { partType: 'dui', label: 'Đùi', icon: '🦵' },
  { partType: 'cang_chan', label: 'Cẳng Chân', icon: '🦿' },
  { partType: 'ban_chan', label: 'Bàn Chân', icon: '🦶' },
];

export const MultiAngleRigAssembler: React.FC = () => {
  // Selected body part
  const [selectedPart, setSelectedPart] = useState<Character2DPartType>('ban_tay');

  // Angle state
  const [selectedAngle, setSelectedAngle] = useState<Character2DAngle>('front');
  const [angleSlots, setAngleSlots] = useState<AngleSlotEntry[]>(
    getDefaultAngleSlots('ban_tay')
  );

  // Bone rig state
  const [boneRig, setBoneRig] = useState<BoneRigDefinition | null>(null);
  const [showBones, setShowBones] = useState(true);

  // Pose overrides: boneId → rotation delta (degrees)
  const [poseOverrides, setPoseOverrides] = useState<Record<string, number>>({});

  // Angle slider (for smooth rotation preview)
  const [angleSliderValue, setAngleSliderValue] = useState(0);
  const [demoLoading, setDemoLoading] = useState(false);

  // ─── Handlers ───────────────────────────────────────────────────

  const handlePartChange = useCallback((partType: Character2DPartType) => {
    setSelectedPart(partType);
    setAngleSlots(getDefaultAngleSlots(partType));
    setBoneRig(null);
    setPoseOverrides({});
    setSelectedAngle('front');
    setAngleSliderValue(0);
  }, []);

  const handleUploadTexture = useCallback((angle: Character2DAngle, dataUrl: string) => {
    setAngleSlots((prev) =>
      prev.map((slot) => {
        if (slot.angle === angle) {
          return { ...slot, textureUrl: dataUrl, isMirrored: false };
        }
        // Auto-update mirrored slots
        const mirrorMap: Partial<Record<Character2DAngle, Character2DAngle>> = {
          three_quarter_left: 'three_quarter_right',
          profile_left: 'profile_right',
        };
        const mirrorTarget = mirrorMap[angle];
        if (mirrorTarget && slot.angle === mirrorTarget && !slot.textureUrl) {
          return { ...slot, textureUrl: dataUrl, isMirrored: true, mirrorSourceAngle: angle };
        }
        return slot;
      })
    );
  }, []);

  const handleRemoveTexture = useCallback((angle: Character2DAngle) => {
    setAngleSlots((prev) =>
      prev.map((slot) => {
        if (slot.angle === angle) {
          return { ...slot, textureUrl: null, isMirrored: false, mirrorSourceAngle: undefined };
        }
        // Also clear mirrored slot if its source was removed
        if (slot.mirrorSourceAngle === angle) {
          return { ...slot, textureUrl: null, isMirrored: false, mirrorSourceAngle: undefined };
        }
        return slot;
      })
    );
  }, []);

  const handleApplyPreset = useCallback((rig: BoneRigDefinition) => {
    setBoneRig(rig);
    setShowBones(true);
    setPoseOverrides({}); // Reset pose when applying new preset
  }, []);

  const handleUpdateBone = useCallback((boneId: string, updates: Partial<BoneNode>) => {
    if (!boneRig) return;
    setBoneRig({
      ...boneRig,
      bones: boneRig.bones.map((b) =>
        b.id === boneId ? { ...b, ...updates } : b
      ),
    });
  }, [boneRig]);

  const handleRemoveBone = useCallback((boneId: string) => {
    if (!boneRig) return;
    // Also remove children of this bone
    const toRemove = new Set<string>();
    const findChildren = (parentId: string) => {
      toRemove.add(parentId);
      boneRig.bones.filter((b) => b.parentId === parentId).forEach((child) => findChildren(child.id));
    };
    findChildren(boneId);
    setBoneRig({
      ...boneRig,
      bones: boneRig.bones.filter((b) => !toRemove.has(b.id)),
    });
  }, [boneRig]);

  const handleClearRig = useCallback(() => {
    setBoneRig(null);
    setPoseOverrides({});
  }, []);

  // Pose change handler: set rotation delta for a bone
  const handlePoseChange = useCallback((boneId: string, rotationDelta: number) => {
    setPoseOverrides((prev) => ({
      ...prev,
      [boneId]: rotationDelta,
    }));
  }, []);

  // Batch pose change: set multiple bone rotations at once
  const handleBatchPoseChange = useCallback((overrides: Record<string, number>) => {
    setPoseOverrides(overrides);
  }, []);

  const handleResetPose = useCallback(() => {
    setPoseOverrides({});
  }, []);

  const handleBonePositionChange = useCallback((boneId: string, newPos: [number, number]) => {
    if (!boneRig) return;
    const bone = boneRig.bones.find((b) => b.id === boneId);
    if (!bone) return;

    if (bone.parentId === null) {
      // Root bone: set absolute normalized position
      handleUpdateBone(boneId, { position: newPos });
    } else {
      // Child bone: compute relative position from parent
      // For simplicity, set position directly (FK will resolve)
      const parent = boneRig.bones.find((b) => b.id === bone.parentId);
      if (parent) {
        const relX = newPos[0] - (parent.parentId === null ? parent.position[0] : 0);
        const relY = newPos[1] - (parent.parentId === null ? parent.position[1] : 0);
        handleUpdateBone(boneId, { position: [relX, relY] });
      }
    }
  }, [boneRig, handleUpdateBone]);

  // Auto-switch angle based on slider
  const handleAngleSliderChange = useCallback((deg: number) => {
    setAngleSliderValue(deg);
    // Map degree ranges to angles
    if (deg < 23 || deg >= 338) setSelectedAngle('front');
    else if (deg >= 23 && deg < 68) setSelectedAngle('three_quarter_left');
    else if (deg >= 68 && deg < 113) setSelectedAngle('profile_left');
    else if (deg >= 113 && deg < 203) setSelectedAngle('back');
    else if (deg >= 203 && deg < 293) setSelectedAngle('profile_right');
    else if (deg >= 293 && deg < 338) setSelectedAngle('three_quarter_right');
  }, []);

  // ─── Load Demo Hand Preset ──────────────────────────────────────
  const handleLoadDemoHand = useCallback(async () => {
    setDemoLoading(true);
    try {
      // Set part to hand
      setSelectedPart('ban_tay');
      const defaultSlots = getDefaultAngleSlots('ban_tay');

      // Demo image mapping
      const demoImages: { angle: Character2DAngle; file: string }[] = [
        { angle: 'front', file: '/demo_rig/hand_000_front.jpg' },
        { angle: 'three_quarter_left', file: '/demo_rig/hand_045_three_quarter.jpg' },
        { angle: 'profile_left', file: '/demo_rig/hand_090_profile.jpg' },
        { angle: 'back', file: '/demo_rig/hand_180_back.jpg' },
      ];

      // Fetch images and convert to data URLs
      const loadedSlots = await Promise.all(
        defaultSlots.map(async (slot) => {
          const demo = demoImages.find((d) => d.angle === slot.angle);
          if (demo) {
            try {
              const resp = await fetch(demo.file);
              const blob = await resp.blob();
              const dataUrl = await new Promise<string>((resolve) => {
                const reader = new FileReader();
                reader.onload = () => resolve(reader.result as string);
                reader.readAsDataURL(blob);
              });
              return { ...slot, textureUrl: dataUrl, isMirrored: false };
            } catch {
              return slot;
            }
          }
          // Auto-mirror for right-side angles
          const mirrorSource = demoImages.find((d) => {
            if (slot.angle === 'three_quarter_right') return d.angle === 'three_quarter_left';
            if (slot.angle === 'profile_right') return d.angle === 'profile_left';
            return false;
          });
          if (mirrorSource) {
            try {
              const resp = await fetch(mirrorSource.file);
              const blob = await resp.blob();
              const dataUrl = await new Promise<string>((resolve) => {
                const reader = new FileReader();
                reader.onload = () => resolve(reader.result as string);
                reader.readAsDataURL(blob);
              });
              return { ...slot, textureUrl: dataUrl, isMirrored: true, mirrorSourceAngle: mirrorSource.angle };
            } catch {
              return slot;
            }
          }
          return slot;
        })
      );

      setAngleSlots(loadedSlots);
      setSelectedAngle('front');
      setAngleSliderValue(0);

      // Auto-apply hand bone preset
      const rig = getBonePresetTemplate('hand_5_fingers', 'ban_tay');
      setBoneRig(rig);
      setShowBones(true);
    } finally {
      setDemoLoading(false);
    }
  }, []);

  // Count uploaded textures
  const uploadedCount = angleSlots.filter((s) => s.textureUrl).length;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', gap: 6, overflow: 'hidden' }}>
      {/* ─── Top Bar: Part Selector + Stats ─────────────────────────── */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '4px 8px',
        background: 'rgba(15, 23, 42, 0.6)',
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.06)',
        flexShrink: 0,
      }}>
        {/* Part Selector Pills */}
        <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', flex: 1 }}>
          {PART_SELECTOR_ITEMS.map((item) => {
            const isActive = selectedPart === item.partType;
            return (
              <button
                key={item.partType}
                onClick={() => handlePartChange(item.partType)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '4px 8px',
                  borderRadius: 5,
                  background: isActive
                    ? 'linear-gradient(135deg, rgba(245, 158, 11, 0.3), rgba(234, 88, 12, 0.2))'
                    : 'rgba(0, 0, 0, 0.3)',
                  border: isActive
                    ? '1px solid rgba(245, 158, 11, 0.6)'
                    : '1px solid rgba(255, 255, 255, 0.08)',
                  color: isActive ? '#fbbf24' : '#94a3b8',
                  fontSize: 10,
                  fontWeight: isActive ? 700 : 500,
                  cursor: 'pointer',
                  transition: 'all 0.15s',
                }}
              >
                <span style={{ fontSize: 12 }}>{item.icon}</span>
                {item.label}
              </button>
            );
          })}
        </div>

        {/* Stats badges */}
        <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexShrink: 0 }}>
          <span style={{
            padding: '3px 8px',
            borderRadius: 4,
            background: uploadedCount > 0 ? 'rgba(16, 185, 129, 0.15)' : 'rgba(100, 116, 139, 0.15)',
            border: `1px solid ${uploadedCount > 0 ? 'rgba(16, 185, 129, 0.35)' : 'rgba(100, 116, 139, 0.2)'}`,
            color: uploadedCount > 0 ? '#10b981' : '#64748b',
            fontSize: 9,
            fontWeight: 700,
          }}>
            📷 {uploadedCount}/{angleSlots.length} góc
          </span>
          <span style={{
            padding: '3px 8px',
            borderRadius: 4,
            background: boneRig ? 'rgba(245, 158, 11, 0.15)' : 'rgba(100, 116, 139, 0.15)',
            border: `1px solid ${boneRig ? 'rgba(245, 158, 11, 0.35)' : 'rgba(100, 116, 139, 0.2)'}`,
            color: boneRig ? '#f59e0b' : '#64748b',
            fontSize: 9,
            fontWeight: 700,
          }}>
            🦴 {boneRig?.bones.length || 0} bones
          </span>
          <button
            onClick={handleLoadDemoHand}
            disabled={demoLoading}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '3px 10px',
              borderRadius: 5,
              background: 'linear-gradient(135deg, rgba(168, 85, 247, 0.3), rgba(139, 92, 246, 0.2))',
              border: '1px solid rgba(168, 85, 247, 0.5)',
              color: '#c084fc',
              fontSize: 9,
              fontWeight: 700,
              cursor: demoLoading ? 'wait' : 'pointer',
              transition: 'all 0.15s',
              opacity: demoLoading ? 0.6 : 1,
            }}
            title="Tải mẫu demo bàn tay 4 góc + xương 16 joints"
          >
            {demoLoading ? '⏳ Đang tải...' : '🖐️ Tải Mẫu Demo Bàn Tay'}
          </button>
        </div>
      </div>

      {/* ─── Main 3-Column Layout ──────────────────────────────────── */}
      <div style={{ flex: 1, display: 'flex', gap: 6, overflow: 'hidden', minHeight: 0 }}>
        {/* Left: Angle Slot Wing */}
        <RigAngleSlotWing
          angleSlots={angleSlots}
          selectedAngle={selectedAngle}
          onSelectAngle={setSelectedAngle}
          onUploadTexture={handleUploadTexture}
          onRemoveTexture={handleRemoveTexture}
        />

        {/* Center: Viewport Canvas */}
        <RigViewportCanvas
          selectedAngle={selectedAngle}
          angleSlots={angleSlots}
          boneRig={boneRig}
          showBones={showBones}
          poseOverrides={poseOverrides}
          onBonePositionChange={handleBonePositionChange}
          angleSliderValue={angleSliderValue}
          onAngleSliderChange={handleAngleSliderChange}
        />

        {/* Right: Bone Editor Panel */}
        <RigBoneEditorPanel
          boneRig={boneRig}
          targetPart={selectedPart}
          showBones={showBones}
          poseOverrides={poseOverrides}
          onToggleShowBones={() => setShowBones(!showBones)}
          onApplyPreset={handleApplyPreset}
          onUpdateBone={handleUpdateBone}
          onRemoveBone={handleRemoveBone}
          onClearRig={handleClearRig}
          onPoseChange={handlePoseChange}
          onBatchPoseChange={handleBatchPoseChange}
          onResetPose={handleResetPose}
        />
      </div>
    </div>
  );
};
