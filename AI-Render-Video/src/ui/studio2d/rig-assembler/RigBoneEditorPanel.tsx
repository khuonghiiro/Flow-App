/**
 * RigBoneEditorPanel — Right-side panel for selecting bone presets,
 * viewing bone list, editing individual bone properties, adding new bones,
 * and testing Forward Kinematics posing.
 */
import React, { useState, useEffect } from 'react';
import {
  Bone,
  Wand2,
  ChevronDown,
  ChevronRight,
  Trash2,
  Plus,
  RotateCcw,
  Eye,
  EyeOff,
  Layers,
  SlidersHorizontal,
} from 'lucide-react';
import {
  BoneNode,
  BoneRigDefinition,
  BoneRigPresetId,
  Character2DPartType,
} from '../../../types/scene2d';
import {
  getBonePresetTemplate,
  suggestBonePreset,
} from '../../../core/engine2d/BoneRig2DEngine';

interface RigBoneEditorPanelProps {
  boneRig: BoneRigDefinition | null;
  targetPart: Character2DPartType;
  showBones: boolean;
  selectedBoneId: string | null;
  onSelectBoneId: (boneId: string | null) => void;
  poseOverrides?: Record<string, number>;
  onToggleShowBones: () => void;
  onApplyPreset: (rig: BoneRigDefinition) => void;
  onAutoRig?: () => void;
  autoRigLoading?: boolean;
  onUpdateBone: (boneId: string, updates: Partial<BoneNode>) => void;
  onAddBone?: (parentId: string | null) => void;
  onRemoveBone: (boneId: string) => void;
  onClearRig: () => void;
  onPoseChange?: (boneId: string, rotationDelta: number) => void;
  onBatchPoseChange?: (overrides: Record<string, number>) => void;
  onResetPose?: () => void;
}

const PRESET_OPTIONS: { id: BoneRigPresetId; label: string; icon: string }[] = [
  { id: 'hand_5_fingers', label: 'Bàn Tay (5 Ngón)', icon: '🖐️' },
  { id: 'arm_3_segments', label: 'Cánh Tay (3 Đoạn)', icon: '💪' },
  { id: 'leg_3_segments', label: 'Chân (4 Đoạn)', icon: '🦵' },
  { id: 'head_jaw_eyes', label: 'Đầu (Hàm + Mắt)', icon: '🗣️' },
  { id: 'torso_spine', label: 'Thân (Cột Sống)', icon: '🦴' },
  { id: 'full_body_simple', label: 'Toàn Thân', icon: '🧍' },
];

export const RigBoneEditorPanel: React.FC<RigBoneEditorPanelProps> = ({
  boneRig,
  targetPart,
  showBones,
  selectedBoneId,
  onSelectBoneId,
  poseOverrides = {},
  onToggleShowBones,
  onApplyPreset,
  onAutoRig,
  autoRigLoading = false,
  onUpdateBone,
  onAddBone,
  onRemoveBone,
  onClearRig,
  onPoseChange,
  onBatchPoseChange,
  onResetPose,
}) => {
  const [expandedBone, setExpandedBone] = useState<string | null>(selectedBoneId);
  const [presetDropdownOpen, setPresetDropdownOpen] = useState(false);
  const [showPoseMode, setShowPoseMode] = useState(false);

  // Sync expanded bone when selected from canvas
  useEffect(() => {
    if (selectedBoneId) {
      setExpandedBone(selectedBoneId);
    }
  }, [selectedBoneId]);

  const suggestedPreset = suggestBonePreset(targetPart);

  const handleApplyPreset = (presetId: BoneRigPresetId) => {
    const rig = getBonePresetTemplate(presetId, targetPart);
    onApplyPreset(rig);
    setPresetDropdownOpen(false);
  };

  return (
    <div
      style={{
        width: 270,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(8, 13, 26, 0.95)',
        padding: 8,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflowY: 'auto',
      }}
    >
      {/* ─── Header ──────────────────────────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '2px 0',
        }}
      >
        <div style={{ fontSize: 10, fontWeight: 700, color: '#f59e0b', letterSpacing: 0.5 }}>
          🦴 BONE EDITOR
        </div>
        <button
          onClick={onToggleShowBones}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '3px 8px',
            borderRadius: 4,
            background: showBones ? 'rgba(245, 158, 11, 0.2)' : 'rgba(100, 116, 139, 0.2)',
            border: `1px solid ${showBones ? 'rgba(245, 158, 11, 0.5)' : 'rgba(100, 116, 139, 0.3)'}`,
            color: showBones ? '#f59e0b' : '#94a3b8',
            fontSize: 9,
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          {showBones ? <Eye size={10} /> : <EyeOff size={10} />}
          {showBones ? 'Hiện' : 'Ẩn'}
        </button>
      </div>

      {/* ─── Preset Selector ─────────────────────────────────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.6)',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.06)',
          overflow: 'hidden',
        }}
      >
        <div
          style={{
            fontSize: 9,
            fontWeight: 700,
            color: '#94a3b8',
            padding: '5px 8px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
          }}
        >
          🎯 CHỌN BỘ XƯƠNG MẪU & AUTO-RIG
        </div>

        <div style={{ padding: 6, display: 'flex', flexDirection: 'column', gap: 4 }}>
          {/* AI Auto-Rig 1-Click Button */}
          {onAutoRig && (
            <button
              onClick={onAutoRig}
              disabled={autoRigLoading}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                width: '100%',
                padding: '7px 10px',
                borderRadius: 5,
                background: 'linear-gradient(135deg, rgba(2, 132, 199, 0.3), rgba(6, 182, 212, 0.25))',
                border: '1px solid rgba(56, 189, 248, 0.6)',
                color: '#38bdf8',
                fontSize: 10,
                fontWeight: 700,
                cursor: autoRigLoading ? 'wait' : 'pointer',
                transition: 'all 0.15s',
              }}
              title="Quét ảnh texture đang chọn và tự động ghim khung xương vào đúng khớp bằng MediaPipe AI"
            >
              <span>{autoRigLoading ? '⏳' : '🤖'}</span>
              <span>{autoRigLoading ? 'Đang Bắt Khớp AI...' : 'AI Tự Động Bắt Khớp (Auto-Rig)'}</span>
            </button>
          )}

          {/* Quick apply suggested preset */}
          <button
            onClick={() => handleApplyPreset(suggestedPreset)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              width: '100%',
              padding: '6px 10px',
              borderRadius: 5,
              background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.2), rgba(234, 88, 12, 0.15))',
              border: '1px solid rgba(245, 158, 11, 0.4)',
              color: '#fbbf24',
              fontSize: 9.5,
              fontWeight: 700,
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
          >
            <Wand2 size={11} />
            ⚡ Mẫu Gợi Ý: {PRESET_OPTIONS.find((p) => p.id === suggestedPreset)?.label}
          </button>

          {/* Dropdown for all presets */}
          <div style={{ position: 'relative' }}>
            <button
              onClick={() => setPresetDropdownOpen(!presetDropdownOpen)}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                width: '100%',
                padding: '5px 8px',
                borderRadius: 4,
                background: 'rgba(0, 0, 0, 0.3)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#94a3b8',
                fontSize: 9,
                cursor: 'pointer',
              }}
            >
              <span>Chọn mẫu xương khác…</span>
              <ChevronDown size={10} />
            </button>

            {presetDropdownOpen && (
              <div
                style={{
                  position: 'absolute',
                  top: '100%',
                  left: 0,
                  right: 0,
                  zIndex: 10,
                  background: '#1e293b',
                  border: '1px solid rgba(255, 255, 255, 0.15)',
                  borderRadius: 6,
                  marginTop: 2,
                  boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
                  overflow: 'hidden',
                }}
              >
                {PRESET_OPTIONS.map((preset) => (
                  <button
                    key={preset.id}
                    onClick={() => handleApplyPreset(preset.id)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                      width: '100%',
                      padding: '6px 10px',
                      background: preset.id === suggestedPreset ? 'rgba(245, 158, 11, 0.12)' : 'transparent',
                      border: 'none',
                      borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                      color: '#e2e8f0',
                      fontSize: 10,
                      cursor: 'pointer',
                      transition: 'background 0.1s',
                    }}
                  >
                    <span style={{ fontSize: 13 }}>{preset.icon}</span>
                    {preset.label}
                    {preset.id === suggestedPreset && (
                      <span style={{ fontSize: 8, color: '#f59e0b', marginLeft: 'auto' }}>✦ Gợi ý</span>
                    )}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ─── Bone List & Hierarchy ────────────────────────────────── */}
      <div
        style={{
          flex: 1,
          background: 'rgba(15, 23, 42, 0.6)',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.06)',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
          minHeight: 180,
        }}
      >
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            fontSize: 9,
            fontWeight: 700,
            color: '#94a3b8',
            padding: '5px 8px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
          }}
        >
          <span>📋 DANH SÁCH XƯƠNG ({boneRig?.bones.length || 0})</span>
          <div style={{ display: 'flex', gap: 4 }}>
            {onAddBone && (
              <button
                onClick={() => onAddBone(selectedBoneId || null)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 6px',
                  borderRadius: 3,
                  background: 'rgba(56, 189, 248, 0.15)',
                  border: '1px solid rgba(56, 189, 248, 0.35)',
                  color: '#38bdf8',
                  fontSize: 8,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
                title="Thêm một đoạn xương mới"
              >
                <Plus size={8} /> Thêm Xương
              </button>
            )}
            {boneRig && (
              <button
                onClick={onClearRig}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 6px',
                  borderRadius: 3,
                  background: 'rgba(239, 68, 68, 0.15)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  color: '#ef4444',
                  fontSize: 8,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <Trash2 size={8} /> Xóa Hết
              </button>
            )}
          </div>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', padding: 4 }}>
          {!boneRig || boneRig.bones.length === 0 ? (
            <div
              style={{
                padding: 16,
                textAlign: 'center',
                color: 'rgba(255, 255, 255, 0.3)',
                fontSize: 10,
              }}
            >
              Chưa có xương nào.<br />
              Chọn mẫu xương ở trên để bắt đầu.
            </div>
          ) : (
            boneRig.bones.map((bone) => {
              const isSelected = selectedBoneId === bone.id;
              const isExpanded = expandedBone === bone.id;

              return (
                <div
                  key={bone.id}
                  style={{
                    marginBottom: 3,
                    borderRadius: 4,
                    border: isSelected ? '1px solid #38bdf8' : '1px solid transparent',
                    background: isSelected ? 'rgba(56, 189, 248, 0.08)' : 'transparent',
                    transition: 'all 0.1s',
                  }}
                >
                  {/* Bone row header */}
                  <div
                    onClick={() => {
                      onSelectBoneId(bone.id);
                      setExpandedBone(isExpanded ? null : bone.id);
                    }}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 5,
                      padding: '4px 6px',
                      borderRadius: 4,
                      cursor: 'pointer',
                    }}
                  >
                    {isExpanded ? <ChevronDown size={9} color="#38bdf8" /> : <ChevronRight size={9} color="#64748b" />}
                    <div
                      style={{
                        width: 8,
                        height: 8,
                        borderRadius: '50%',
                        background: bone.color || '#f59e0b',
                        flexShrink: 0,
                      }}
                    />
                    <span
                      style={{
                        fontSize: 9.5,
                        fontWeight: isSelected ? 700 : 500,
                        color: isSelected ? '#38bdf8' : '#cbd5e1',
                        flex: 1,
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {bone.name}
                    </span>
                    <span style={{ fontSize: 7.5, color: '#64748b' }}>
                      {bone.parentId ? `↳ ${bone.parentId.slice(0, 8)}` : 'ROOT'}
                    </span>
                  </div>

                  {/* Expanded bone properties */}
                  {isExpanded && (
                    <div
                      style={{
                        padding: '6px 8px 6px 18px',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 6,
                        background: 'rgba(0, 0, 0, 0.25)',
                        borderTop: '1px solid rgba(255, 255, 255, 0.04)',
                      }}
                    >
                      {/* Name input */}
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        <span style={{ fontSize: 8, color: '#64748b', width: 28 }}>Tên:</span>
                        <input
                          type="text"
                          value={bone.name}
                          onChange={(e) => onUpdateBone(bone.id, { name: e.target.value })}
                          style={{ ...inputStyle, width: '100%', textAlign: 'left' }}
                        />
                      </div>

                      {/* Position X/Y Sliders */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                          <span>Vị trí X / Y:</span>
                          <span style={{ fontFamily: 'monospace' }}>
                            {bone.position[0].toFixed(2)}, {bone.position[1].toFixed(2)}
                          </span>
                        </div>
                        <div style={{ display: 'flex', gap: 4 }}>
                          <input
                            type="range"
                            min={bone.parentId ? -0.5 : 0}
                            max={bone.parentId ? 0.5 : 1}
                            step={0.01}
                            value={bone.position[0]}
                            onChange={(e) => onUpdateBone(bone.id, { position: [parseFloat(e.target.value), bone.position[1]] })}
                            style={{ flex: 1, accentColor: '#38bdf8' }}
                            title="Tọa độ X"
                          />
                          <input
                            type="range"
                            min={bone.parentId ? -0.5 : 0}
                            max={bone.parentId ? 0.5 : 1}
                            step={0.01}
                            value={bone.position[1]}
                            onChange={(e) => onUpdateBone(bone.id, { position: [bone.position[0], parseFloat(e.target.value)] })}
                            style={{ flex: 1, accentColor: '#38bdf8' }}
                            title="Tọa độ Y"
                          />
                        </div>
                      </div>

                      {/* Rotation Slider */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                          <span>Góc xoay:</span>
                          <span style={{ fontFamily: 'monospace' }}>{bone.rotation}°</span>
                        </div>
                        <input
                          type="range"
                          min={-180}
                          max={180}
                          step={1}
                          value={bone.rotation}
                          onChange={(e) => onUpdateBone(bone.id, { rotation: parseInt(e.target.value) })}
                          style={{ width: '100%', accentColor: '#f59e0b' }}
                        />
                      </div>

                      {/* Length Slider */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                          <span>Độ dài xương:</span>
                          <span style={{ fontFamily: 'monospace' }}>{(bone.length * 100).toFixed(0)}%</span>
                        </div>
                        <input
                          type="range"
                          min={0.02}
                          max={0.8}
                          step={0.01}
                          value={bone.length}
                          onChange={(e) => onUpdateBone(bone.id, { length: parseFloat(e.target.value) })}
                          style={{ width: '100%', accentColor: '#10b981' }}
                        />
                      </div>

                      {/* Action buttons */}
                      <div style={{ display: 'flex', gap: 4, marginTop: 2 }}>
                        {onAddBone && (
                          <button
                            onClick={() => onAddBone(bone.id)}
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: 3,
                              padding: '3px 6px',
                              borderRadius: 3,
                              background: 'rgba(56, 189, 248, 0.15)',
                              border: '1px solid rgba(56, 189, 248, 0.3)',
                              color: '#38bdf8',
                              fontSize: 8,
                              fontWeight: 600,
                              cursor: 'pointer',
                            }}
                          >
                            <Plus size={8} /> Nối Khớp Con
                          </button>
                        )}
                        <button
                          onClick={() => onRemoveBone(bone.id)}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 3,
                            padding: '3px 6px',
                            borderRadius: 3,
                            background: 'rgba(239, 68, 68, 0.1)',
                            border: '1px solid rgba(239, 68, 68, 0.25)',
                            color: '#f87171',
                            fontSize: 8,
                            fontWeight: 600,
                            cursor: 'pointer',
                          }}
                        >
                          <Trash2 size={8} /> Xóa
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>
      </div>

      {/* ─── Pose Editor Section ────────────────────────────────── */}
      {boneRig && boneRig.bones.length > 0 && (
        <div
          style={{
            background: 'rgba(15, 23, 42, 0.6)',
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.06)',
            overflow: 'hidden',
            display: 'flex',
            flexDirection: 'column',
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              fontSize: 9,
              fontWeight: 700,
              color: '#94a3b8',
              padding: '5px 8px',
              borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
            }}
          >
            <button
              onClick={() => setShowPoseMode(!showPoseMode)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                background: 'transparent',
                border: 'none',
                color: showPoseMode ? '#c084fc' : '#94a3b8',
                cursor: 'pointer',
                padding: 0,
                fontSize: 9,
                fontWeight: 700,
              }}
            >
              <SlidersHorizontal size={10} />
              🎭 POSE PRESETS & SLIDERS
            </button>
            {onResetPose && (
              <button
                onClick={onResetPose}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 6px',
                  borderRadius: 3,
                  background: 'rgba(168, 85, 247, 0.12)',
                  border: '1px solid rgba(168, 85, 247, 0.25)',
                  color: '#a78bfa',
                  fontSize: 8,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
                title="Reset tất cả pose về gốc"
              >
                <RotateCcw size={8} /> Reset
              </button>
            )}
          </div>

          <div style={{ padding: 6 }}>
            {/* Quick Pose Presets */}
            <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
              <button
                onClick={() => {
                  const pose: Record<string, number> = {
                    thumb_base: 28,
                    thumb_tip: 32,
                    index_base: 14,
                    index_mid: 18,
                    index_tip: 22,
                    middle_base: 2,
                    middle_mid: 18,
                    middle_tip: 22,
                    ring_base: -14,
                    ring_mid: -18,
                    ring_tip: -22,
                    pinky_base: -24,
                    pinky_mid: -22,
                    pinky_tip: -26,
                  };
                  onBatchPoseChange?.(pose);
                }}
                style={quickPoseButtonStyle}
              >
                ✊ Nắm tay
              </button>
              <button
                onClick={() => {
                  const pose: Record<string, number> = {
                    thumb_base: 25,
                    thumb_tip: 30,
                    index_base: 0,
                    index_mid: 0,
                    index_tip: 0,
                    middle_base: 2,
                    middle_mid: 18,
                    middle_tip: 22,
                    ring_base: -14,
                    ring_mid: -18,
                    ring_tip: -22,
                    pinky_base: -24,
                    pinky_mid: -22,
                    pinky_tip: -26,
                  };
                  onBatchPoseChange?.(pose);
                }}
                style={quickPoseButtonStyle}
              >
                ☝️ Chỉ tay
              </button>
              <button
                onClick={() => {
                  const pose: Record<string, number> = {
                    thumb_base: 25,
                    thumb_tip: 30,
                    index_base: -8,
                    index_mid: 0,
                    index_tip: 0,
                    middle_base: 8,
                    middle_mid: 0,
                    middle_tip: 0,
                    ring_base: -14,
                    ring_mid: -18,
                    ring_tip: -22,
                    pinky_base: -24,
                    pinky_mid: -22,
                    pinky_tip: -26,
                  };
                  onBatchPoseChange?.(pose);
                }}
                style={quickPoseButtonStyle}
              >
                ✌️ Chữ V
              </button>
              <button onClick={() => onResetPose?.()} style={quickPoseButtonStyle}>
                🖐️ Xòe tay
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

const inputStyle: React.CSSProperties = {
  width: 52,
  padding: '2px 4px',
  borderRadius: 3,
  background: '#0f172a',
  border: '1px solid rgba(255, 255, 255, 0.1)',
  color: '#e2e8f0',
  fontSize: 9,
  fontFamily: 'JetBrains Mono, monospace',
  textAlign: 'right' as const,
};

const quickPoseButtonStyle: React.CSSProperties = {
  padding: '3px 8px',
  borderRadius: 4,
  background: 'rgba(168, 85, 247, 0.15)',
  border: '1px solid rgba(168, 85, 247, 0.3)',
  color: '#c084fc',
  fontSize: 9,
  fontWeight: 700,
  cursor: 'pointer',
  transition: 'all 0.15s',
};
