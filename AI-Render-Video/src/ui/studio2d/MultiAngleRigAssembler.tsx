/**
 * MultiAngleRigAssembler — Main tab component for 2D→3D bone rigging.
 * Layout: [Angle Slot Wing] | [Interactive Viewport Canvas] | [Bone Editor Panel]
 * Supports on-canvas bone dragging, joint hierarchy editing, and live FK pose testing.
 */
import React, { useState, useCallback, useEffect } from 'react';
import {
  Character2DAngle,
  Character2DPartType,
  AngleSlotEntry,
  BoneNode,
  BoneRigDefinition,
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

  // Bone rig state - auto initialized with hand template so bones are immediately ready
  const [boneRig, setBoneRig] = useState<BoneRigDefinition | null>(() =>
    getBonePresetTemplate('hand_5_fingers', 'ban_tay')
  );
  const [selectedBoneId, setSelectedBoneId] = useState<string | null>('wrist_root');
  const [showBones, setShowBones] = useState(true);

  // Editor mode: Edit Rest Rig vs Pose Testing
  const [editorMode, setEditorMode] = useState<'rig_edit' | 'pose_test'>('rig_edit');

  // Pose overrides: boneId → rotation delta (degrees)
  const [poseOverrides, setPoseOverrides] = useState<Record<string, number>>({});

  // Angle slider (for smooth rotation preview)
  const [angleSliderValue, setAngleSliderValue] = useState(0);
  const [demoLoading, setDemoLoading] = useState(false);

  // ─── Handlers ───────────────────────────────────────────────────

  const handlePartChange = useCallback((partType: Character2DPartType) => {
    setSelectedPart(partType);
    setAngleSlots(getDefaultAngleSlots(partType));
    const suggested = suggestBonePreset(partType);
    const defaultRig = getBonePresetTemplate(suggested, partType);
    setBoneRig(defaultRig);
    setSelectedBoneId(defaultRig.bones[0]?.id || null);
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
        if (slot.mirrorSourceAngle === angle) {
          return { ...slot, textureUrl: null, isMirrored: false, mirrorSourceAngle: undefined };
        }
        return slot;
      })
    );
  }, []);

  const handleApplyPreset = useCallback((rig: BoneRigDefinition) => {
    setBoneRig(rig);
    setSelectedBoneId(rig.bones[0]?.id || null);
    setShowBones(true);
    setPoseOverrides({});
  }, []);

  const handleUpdateBone = useCallback((boneId: string, updates: Partial<BoneNode>) => {
    setBoneRig((prev) => {
      if (!prev) return null;
      return {
        ...prev,
        bones: prev.bones.map((b) => (b.id === boneId ? { ...b, ...updates } : b)),
      };
    });
  }, []);

  const handleAddBone = useCallback((parentId: string | null) => {
    setBoneRig((prev) => {
      if (!prev) return null;
      const targetParent = parentId || (prev.bones.length > 0 ? prev.bones[0].id : null);
      const newId = `bone_${Date.now()}`;
      const newBone: BoneNode = {
        id: newId,
        name: `Khớp ${prev.bones.length + 1}`,
        parentId: targetParent,
        position: targetParent ? [0.0, 0.0] : [0.5, 0.5],
        rotation: 0,
        length: 0.12,
        color: '#38bdf8',
      };
      setSelectedBoneId(newId);
      return {
        ...prev,
        bones: [...prev.bones, newBone],
      };
    });
  }, []);

  const handleRemoveBone = useCallback((boneId: string) => {
    setBoneRig((prev) => {
      if (!prev) return null;
      const toRemove = new Set<string>();
      const findChildren = (pid: string) => {
        toRemove.add(pid);
        prev.bones.filter((b) => b.parentId === pid).forEach((c) => findChildren(c.id));
      };
      findChildren(boneId);
      const nextBones = prev.bones.filter((b) => !toRemove.has(b.id));
      setSelectedBoneId(nextBones[0]?.id || null);
      return {
        ...prev,
        bones: nextBones,
      };
    });
  }, []);

  const handleClearRig = useCallback(() => {
    setBoneRig((prev) => (prev ? { ...prev, bones: [] } : null));
    setSelectedBoneId(null);
    setPoseOverrides({});
  }, []);

  // Pose change handler: set rotation delta for a bone
  const handlePoseChange = useCallback((boneId: string, rotationDelta: number) => {
    setPoseOverrides((prev) => ({
      ...prev,
      [boneId]: rotationDelta,
    }));
  }, []);

  // Batch pose change
  const handleBatchPoseChange = useCallback((overrides: Record<string, number>) => {
    setPoseOverrides(overrides);
  }, []);

  const handleResetPose = useCallback(() => {
    setPoseOverrides({});
  }, []);

  // Auto-switch angle based on slider
  const handleAngleSliderChange = useCallback((deg: number) => {
    setAngleSliderValue(deg);
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
      setSelectedPart('ban_tay');
      const defaultSlots = getDefaultAngleSlots('ban_tay');

      const demoImages: { angle: Character2DAngle; file: string }[] = [
        { angle: 'front', file: '/demo_rig/hand_000_front.jpg' },
        { angle: 'three_quarter_left', file: '/demo_rig/hand_045_three_quarter.jpg' },
        { angle: 'profile_left', file: '/demo_rig/hand_090_profile.jpg' },
        { angle: 'back', file: '/demo_rig/hand_180_back.jpg' },
      ];

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

      const rig = getBonePresetTemplate('hand_5_fingers', 'ban_tay');
      setBoneRig(rig);
      setSelectedBoneId('wrist_root');
      setShowBones(true);
    } finally {
      setDemoLoading(false);
    }
  }, []);

  // ─── Auto-Rig AI via MediaPipe / Silhouette Fitting ─────────────
  const [autoRigLoading, setAutoRigLoading] = useState(false);
  const [autoRigStatus, setAutoRigStatus] = useState<string | null>(null);

  const handleAutoRig = useCallback(async () => {
    const currentSlot = angleSlots.find((s) => s.angle === selectedAngle && s.textureUrl) || angleSlots.find((s) => s.textureUrl);
    if (!currentSlot?.textureUrl) {
      alert('Vui lòng tải ảnh texture lên trước (hoặc bấm "Tải Mẫu Demo Bàn Tay") để AI nhận diện khớp xương!');
      return;
    }
    setAutoRigLoading(true);
    setAutoRigStatus('Đang quét khớp xương AI...');
    try {
      const { autoRigFromImage } = await import('../../core/engine2d/AutoRigDetector');
      const result = await autoRigFromImage(currentSlot.textureUrl, selectedPart);
      if (result.success && result.boneRig) {
        setBoneRig(result.boneRig);
        setSelectedBoneId(result.boneRig.bones[0]?.id || null);
        setShowBones(true);
        setPoseOverrides({});
        setAutoRigStatus(`✨ ${result.engine} (${result.landmarksDetected} khớp)`);
      }
    } catch (err: any) {
      alert(`Lỗi Auto-Rig: ${err?.message || err}`);
    } finally {
      setAutoRigLoading(false);
    }
  }, [angleSlots, selectedAngle, selectedPart]);

  const uploadedCount = angleSlots.filter((s) => s.textureUrl).length;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', gap: 6, overflow: 'hidden' }}>
      {/* ─── Top Bar: Part Selector + Stats ─────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '4px 8px',
          background: 'rgba(15, 23, 42, 0.7)',
          borderRadius: 8,
          border: '1px solid rgba(255, 255, 255, 0.08)',
          flexShrink: 0,
        }}
      >
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
                    ? 'linear-gradient(135deg, rgba(245, 158, 11, 0.35), rgba(234, 88, 12, 0.25))'
                    : 'rgba(0, 0, 0, 0.3)',
                  border: isActive
                    ? '1px solid rgba(245, 158, 11, 0.7)'
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

        {/* Stats badges & Demo Button */}
        <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexShrink: 0 }}>
          <span
            style={{
              padding: '3px 8px',
              borderRadius: 4,
              background: uploadedCount > 0 ? 'rgba(16, 185, 129, 0.15)' : 'rgba(100, 116, 139, 0.15)',
              border: `1px solid ${uploadedCount > 0 ? 'rgba(16, 185, 129, 0.35)' : 'rgba(100, 116, 139, 0.2)'}`,
              color: uploadedCount > 0 ? '#10b981' : '#64748b',
              fontSize: 9,
              fontWeight: 700,
            }}
          >
            📷 {uploadedCount}/{angleSlots.length} góc
          </span>
          <span
            style={{
              padding: '3px 8px',
              borderRadius: 4,
              background: boneRig ? 'rgba(245, 158, 11, 0.15)' : 'rgba(100, 116, 139, 0.15)',
              border: `1px solid ${boneRig ? 'rgba(245, 158, 11, 0.35)' : 'rgba(100, 116, 139, 0.2)'}`,
              color: boneRig ? '#f59e0b' : '#64748b',
              fontSize: 9,
              fontWeight: 700,
            }}
          >
            🦴 {boneRig?.bones.length || 0} bones
          </span>
          {/* Auto-Rig AI Button */}
          <button
            onClick={handleAutoRig}
            disabled={autoRigLoading}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 10px',
              borderRadius: 5,
              background: 'linear-gradient(135deg, #0284c7, #06b6d4)',
              border: 'none',
              color: '#ffffff',
              fontSize: 9.5,
              fontWeight: 700,
              cursor: autoRigLoading ? 'wait' : 'pointer',
              boxShadow: '0 0 10px rgba(6, 182, 212, 0.4)',
              transition: 'all 0.15s',
              opacity: autoRigLoading ? 0.6 : 1,
            }}
            title="Tự động nhận diện và ghim khớp xương chuẩn xác vào ảnh bằng MediaPipe AI"
          >
            {autoRigLoading ? '⏳ Đang quét AI...' : '🤖 AI Bắt Khớp (Auto-Rig)'}
          </button>

          <button
            onClick={handleLoadDemoHand}
            disabled={demoLoading}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 10px',
              borderRadius: 5,
              background: 'linear-gradient(135deg, #7c3aed, #a855f7)',
              border: 'none',
              color: '#ffffff',
              fontSize: 9.5,
              fontWeight: 700,
              cursor: demoLoading ? 'wait' : 'pointer',
              boxShadow: '0 0 10px rgba(168, 85, 247, 0.4)',
              transition: 'all 0.15s',
              opacity: demoLoading ? 0.6 : 1,
            }}
            title="Tải mẫu demo bàn tay 4 góc + xương 16 joints"
          >
            {demoLoading ? '⏳ Đang tải...' : '🖐️ Tải Demo Bàn Tay'}
          </button>
        </div>
      </div>

      {/* Auto-Rig Notification Banner if active */}
      {autoRigStatus && (
        <div
          style={{
            padding: '3px 10px',
            background: 'rgba(2, 132, 199, 0.15)',
            border: '1px solid rgba(2, 132, 199, 0.3)',
            borderRadius: 6,
            fontSize: 9,
            color: '#38bdf8',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <span>{autoRigStatus}</span>
          <button
            onClick={() => setAutoRigStatus(null)}
            style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer', fontSize: 10 }}
          >
            ✕
          </button>
        </div>
      )}

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

        {/* Center: Interactive Viewport Canvas */}
        <RigViewportCanvas
          selectedAngle={selectedAngle}
          angleSlots={angleSlots}
          boneRig={boneRig}
          showBones={showBones}
          selectedBoneId={selectedBoneId}
          onSelectBoneId={setSelectedBoneId}
          onUpdateBone={handleUpdateBone}
          editorMode={editorMode}
          onSetEditorMode={setEditorMode}
          poseOverrides={poseOverrides}
          onPoseChange={handlePoseChange}
          onResetPose={handleResetPose}
          angleSliderValue={angleSliderValue}
          onAngleSliderChange={handleAngleSliderChange}
        />

        {/* Right: Bone Editor Panel */}
        <RigBoneEditorPanel
          boneRig={boneRig}
          targetPart={selectedPart}
          showBones={showBones}
          selectedBoneId={selectedBoneId}
          onSelectBoneId={setSelectedBoneId}
          poseOverrides={poseOverrides}
          onToggleShowBones={() => setShowBones(!showBones)}
          onApplyPreset={handleApplyPreset}
          onAutoRig={handleAutoRig}
          autoRigLoading={autoRigLoading}
          onUpdateBone={handleUpdateBone}
          onAddBone={handleAddBone}
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
