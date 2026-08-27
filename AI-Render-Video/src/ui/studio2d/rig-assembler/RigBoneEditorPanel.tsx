/**
 * RigBoneEditorPanel — Right-side panel for selecting bone presets,
 * viewing bone list, and editing individual bone properties.
 */
import React, { useState } from 'react';
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
  poseOverrides?: Record<string, number>;
  onToggleShowBones: () => void;
  onApplyPreset: (rig: BoneRigDefinition) => void;
  onUpdateBone: (boneId: string, updates: Partial<BoneNode>) => void;
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
  poseOverrides = {},
  onToggleShowBones,
  onApplyPreset,
  onUpdateBone,
  onRemoveBone,
  onClearRig,
  onPoseChange,
  onBatchPoseChange,
  onResetPose,
}) => {
  const [expandedBone, setExpandedBone] = useState<string | null>(null);
  const [presetDropdownOpen, setPresetDropdownOpen] = useState(false);
  const [showPoseMode, setShowPoseMode] = useState(false);

  const suggestedPreset = suggestBonePreset(targetPart);

  const handleApplyPreset = (presetId: BoneRigPresetId) => {
    const rig = getBonePresetTemplate(presetId, targetPart);
    onApplyPreset(rig);
    setPresetDropdownOpen(false);
  };

  return (
    <div
      style={{
        width: 260,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(8, 13, 26, 0.92)',
        padding: 8,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflowY: 'auto',
      }}
    >
      {/* Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '2px 0',
      }}>
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

      {/* Preset Selector */}
      <div style={{
        background: 'rgba(15, 23, 42, 0.6)',
        borderRadius: 6,
        border: '1px solid rgba(255, 255, 255, 0.06)',
        overflow: 'hidden',
      }}>
        <div
          style={{
            fontSize: 9,
            fontWeight: 700,
            color: '#94a3b8',
            padding: '5px 8px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
          }}
        >
          🎯 CHỌN BỘ XƯƠNG MẪU
        </div>

        <div style={{ padding: 6, display: 'flex', flexDirection: 'column', gap: 4 }}>
          {/* Quick apply suggested preset */}
          <button
            onClick={() => handleApplyPreset(suggestedPreset)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              width: '100%',
              padding: '7px 10px',
              borderRadius: 5,
              background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.2), rgba(234, 88, 12, 0.15))',
              border: '1px solid rgba(245, 158, 11, 0.4)',
              color: '#fbbf24',
              fontSize: 10,
              fontWeight: 700,
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
          >
            <Wand2 size={12} />
            ⚡ Áp Dụng Mẫu Gợi Ý: {PRESET_OPTIONS.find((p) => p.id === suggestedPreset)?.label}
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
              <div style={{
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
              }}>
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
                    onMouseEnter={(e) => { (e.target as HTMLElement).style.background = 'rgba(245, 158, 11, 0.15)'; }}
                    onMouseLeave={(e) => { (e.target as HTMLElement).style.background = preset.id === suggestedPreset ? 'rgba(245, 158, 11, 0.12)' : 'transparent'; }}
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

      {/* Bone List */}
      <div style={{
        flex: 1,
        background: 'rgba(15, 23, 42, 0.6)',
        borderRadius: 6,
        border: '1px solid rgba(255, 255, 255, 0.06)',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
      }}>
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          fontSize: 9,
          fontWeight: 700,
          color: '#94a3b8',
          padding: '5px 8px',
          borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
        }}>
          <span>📋 DANH SÁCH XƯƠNG ({boneRig?.bones.length || 0})</span>
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

        <div style={{ flex: 1, overflowY: 'auto', padding: 4 }}>
          {!boneRig || boneRig.bones.length === 0 ? (
            <div style={{
              padding: 16,
              textAlign: 'center',
              color: 'rgba(255, 255, 255, 0.2)',
              fontSize: 10,
            }}>
              Chưa có xương nào.<br />
              Chọn mẫu xương ở trên để bắt đầu.
            </div>
          ) : (
            boneRig.bones.map((bone) => {
              const isExpanded = expandedBone === bone.id;
              return (
                <div key={bone.id} style={{ marginBottom: 2 }}>
                  {/* Bone row header */}
                  <div
                    onClick={() => setExpandedBone(isExpanded ? null : bone.id)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 5,
                      padding: '4px 6px',
                      borderRadius: 4,
                      background: isExpanded ? 'rgba(245, 158, 11, 0.1)' : 'transparent',
                      cursor: 'pointer',
                      transition: 'background 0.1s',
                    }}
                  >
                    {isExpanded ? <ChevronDown size={9} color="#f59e0b" /> : <ChevronRight size={9} color="#64748b" />}
                    <div
                      style={{
                        width: 8,
                        height: 8,
                        borderRadius: '50%',
                        background: bone.color || '#f59e0b',
                        flexShrink: 0,
                      }}
                    />
                    <span style={{
                      fontSize: 9.5,
                      fontWeight: isExpanded ? 700 : 500,
                      color: isExpanded ? '#fbbf24' : '#cbd5e1',
                      flex: 1,
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                    }}>
                      {bone.name}
                    </span>
                    <span style={{ fontSize: 7.5, color: '#475569' }}>
                      {bone.parentId ? `↳ ${bone.parentId}` : 'ROOT'}
                    </span>
                  </div>

                  {/* Expanded bone details */}
                  {isExpanded && (
                    <div style={{
                      padding: '6px 8px 6px 22px',
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 4,
                    }}>
                      {/* Position */}
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        <span style={{ fontSize: 8, color: '#64748b', width: 24 }}>Pos:</span>
                        <input
                          type="number"
                          step={0.01}
                          value={bone.position[0].toFixed(3)}
                          onChange={(e) => onUpdateBone(bone.id, { position: [parseFloat(e.target.value) || 0, bone.position[1]] })}
                          style={inputStyle}
                          title="X position (0-1)"
                        />
                        <input
                          type="number"
                          step={0.01}
                          value={bone.position[1].toFixed(3)}
                          onChange={(e) => onUpdateBone(bone.id, { position: [bone.position[0], parseFloat(e.target.value) || 0] })}
                          style={inputStyle}
                          title="Y position (0-1)"
                        />
                      </div>
                      {/* Rotation */}
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        <span style={{ fontSize: 8, color: '#64748b', width: 24 }}>Rot:</span>
                        <input
                          type="number"
                          step={1}
                          value={bone.rotation}
                          onChange={(e) => onUpdateBone(bone.id, { rotation: parseFloat(e.target.value) || 0 })}
                          style={{ ...inputStyle, width: 60 }}
                          title="Rotation (degrees)"
                        />
                        <span style={{ fontSize: 8, color: '#475569' }}>deg</span>
                      </div>
                      {/* Length */}
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        <span style={{ fontSize: 8, color: '#64748b', width: 24 }}>Len:</span>
                        <input
                          type="number"
                          step={0.01}
                          value={bone.length.toFixed(3)}
                          onChange={(e) => onUpdateBone(bone.id, { length: parseFloat(e.target.value) || 0 })}
                          style={{ ...inputStyle, width: 60 }}
                          title="Bone length (normalized)"
                        />
                      </div>
                      {/* Delete bone */}
                      <button
                        onClick={() => onRemoveBone(bone.id)}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 4,
                          padding: '3px 6px',
                          borderRadius: 3,
                          background: 'rgba(239, 68, 68, 0.1)',
                          border: '1px solid rgba(239, 68, 68, 0.25)',
                          color: '#f87171',
                          fontSize: 8,
                          fontWeight: 600,
                          cursor: 'pointer',
                          marginTop: 2,
                          width: 'fit-content',
                        }}
                      >
                        <Trash2 size={8} /> Xóa Xương Này
                      </button>
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
        <div style={{
          background: 'rgba(15, 23, 42, 0.6)',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.06)',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
        }}>
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            fontSize: 9,
            fontWeight: 700,
            color: '#94a3b8',
            padding: '5px 8px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
          }}>
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
              🎭 POSE EDITOR
              {Object.values(poseOverrides).some((v) => Math.abs(v) > 0.1) && (
                <span style={{
                  fontSize: 7,
                  background: 'rgba(192, 132, 252, 0.3)',
                  color: '#c084fc',
                  borderRadius: 3,
                  padding: '1px 4px',
                  fontWeight: 800,
                }}>
                  ACTIVE
                </span>
              )}
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

          {showPoseMode && (
            <div style={{ padding: 6, maxHeight: 200, overflowY: 'auto' }}>
              {boneRig.bones.map((bone) => {
                const poseVal = poseOverrides[bone.id] || 0;
                return (
                  <div key={bone.id} style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 4,
                    padding: '3px 2px',
                    borderBottom: '1px solid rgba(255,255,255,0.03)',
                  }}>
                    <div
                      style={{
                        width: 6,
                        height: 6,
                        borderRadius: '50%',
                        background: bone.color || '#f59e0b',
                        flexShrink: 0,
                      }}
                    />
                    <span style={{
                      fontSize: 8,
                      color: Math.abs(poseVal) > 0.1 ? '#c084fc' : '#94a3b8',
                      fontWeight: Math.abs(poseVal) > 0.1 ? 700 : 500,
                      width: 60,
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                      flexShrink: 0,
                    }}>
                      {bone.name.split(' ')[0]}
                    </span>
                    <input
                      type="range"
                      min={-180}
                      max={180}
                      step={1}
                      value={poseVal}
                      onChange={(e) => onPoseChange?.(bone.id, Number(e.target.value))}
                      style={{
                        flex: 1,
                        height: 10,
                        accentColor: '#c084fc',
                        cursor: 'pointer',
                      }}
                      title={`${bone.name}: ${poseVal.toFixed(0)}°`}
                    />
                    <span style={{
                      fontSize: 8,
                      color: Math.abs(poseVal) > 0.1 ? '#c084fc' : '#475569',
                      fontWeight: 600,
                      minWidth: 28,
                      textAlign: 'right',
                      fontFamily: 'JetBrains Mono, monospace',
                    }}>
                      {poseVal.toFixed(0)}°
                    </span>
                  </div>
                );
              })}

              {/* Quick Pose Presets */}
              <div style={{
                display: 'flex',
                gap: 4,
                marginTop: 6,
                flexWrap: 'wrap',
              }}>
                <button
                  onClick={() => {
                    // Natural Fist: fingers curl inward toward palm center
                    const pose: Record<string, number> = {
                      thumb_base: 28, thumb_tip: 32,
                      index_base: 14, index_mid: 18, index_tip: 22,
                      middle_base: 2, middle_mid: 18, middle_tip: 22,
                      ring_base: -14, ring_mid: -18, ring_tip: -22,
                      pinky_base: -24, pinky_mid: -22, pinky_tip: -26,
                    };
                    onBatchPoseChange?.(pose);
                  }}
                  style={quickPoseButtonStyle}
                  title="Nắm tay tự nhiên"
                >
                  ✊ Nắm tay
                </button>
                <button
                  onClick={() => {
                    // Pointing: index finger straight, others curled
                    const pose: Record<string, number> = {
                      thumb_base: 25, thumb_tip: 30,
                      index_base: 0, index_mid: 0, index_tip: 0,
                      middle_base: 2, middle_mid: 18, middle_tip: 22,
                      ring_base: -14, ring_mid: -18, ring_tip: -22,
                      pinky_base: -24, pinky_mid: -22, pinky_tip: -26,
                    };
                    onBatchPoseChange?.(pose);
                  }}
                  style={quickPoseButtonStyle}
                  title="Chỉ tay"
                >
                  ☝️ Chỉ tay
                </button>
                <button
                  onClick={() => {
                    // Peace / Victory sign: index & middle open in V shape
                    const pose: Record<string, number> = {
                      thumb_base: 25, thumb_tip: 30,
                      index_base: -8, index_mid: 0, index_tip: 0,
                      middle_base: 8, middle_mid: 0, middle_tip: 0,
                      ring_base: -14, ring_mid: -18, ring_tip: -22,
                      pinky_base: -24, pinky_mid: -22, pinky_tip: -26,
                    };
                    onBatchPoseChange?.(pose);
                  }}
                  style={quickPoseButtonStyle}
                  title="Chữ V (Peace)"
                >
                  ✌️ Chữ V
                </button>
                <button
                  onClick={() => onResetPose?.()}
                  style={quickPoseButtonStyle}
                  title="Xòe tay (Thả lỏng)"
                >
                  🖐️ Xòe tay
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Info Footer */}
      {boneRig && (
        <div style={{
          padding: '4px 8px',
          background: 'rgba(15, 23, 42, 0.4)',
          borderRadius: 4,
          fontSize: 8,
          color: '#475569',
          textAlign: 'center',
        }}>
          💡 Kéo slider trong Pose Editor để thay đổi tư thế
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
