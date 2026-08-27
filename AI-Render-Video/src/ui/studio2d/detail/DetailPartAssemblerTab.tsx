/**
 * DetailPartAssemblerTab.tsx
 *
 * Tab 2.1 — "Bàn Lắp Ráp Chi Tiết"
 *
 * Allows assembling individual composite detail parts (eyes, mouth, nose, ears)
 * with multi-angle support and expression/state variants.
 * Each composite is built from sub-parts loaded dynamically from asset_2ds.
 *
 * Flow: Select composite (e.g. "Mắt") → Configure sub-parts at each angle →
 *       Add states (blink, talk) → Save detail → Use in Tab 2.2 character assembler.
 */

import React, { useState, useEffect, useRef } from 'react';
import {
  Eye, Smile, Ear, Wind, ChevronDown, ChevronRight, Save,
  RotateCcw, Plus, Trash2, Check, ImageIcon, Loader2, ArrowRight,
} from 'lucide-react';
import type {
  Character2DPartType,
  Character2DAngle,
  Character2DPartConfig,
} from '../../../types/scene2d';
import {
  fetchPartHierarchy,
  loadSubPartAssets,
  type CompositePartDef,
  type SubPartDef,
} from '../../../core/assets/PartAssemblyHierarchyRegistry';
import type { Asset2DItem } from '../../../core/assets/Asset2DStructureRegistry';
import { PART_HIERARCHY_CONFIG } from '../../../core/assets/Asset2DRegistry';

// ─── Types ───────────────────────────────────────────────────────

/** A single sub-part configuration at one angle */
interface SubPartAngleConfig {
  path: string;
  offset: [number, number];
  scale: [number, number];
  rotation: number;
  pivot: [number, number];
  z_index: number;
  opacity: number;
}

/** State variant for a sub-part (e.g. blink, talk phoneme) */
interface SubPartState {
  id: string;
  label: string;
  path: string;
}

/** Full detail assembly for one composite */
interface DetailAssembly {
  id: string;
  compositeId: string;
  name: string;
  icon: string;
  /** Per-angle configuration for each sub-part */
  angles: Partial<Record<Character2DAngle, Record<string, SubPartAngleConfig>>>;
  /** Expression/state variants for specific sub-parts */
  states: Record<string, SubPartState[]>;
  createdAt: string;
  updatedAt: string;
}

// ─── Constants ───────────────────────────────────────────────────

const SUPPORTED_ANGLES: { id: Character2DAngle; label: string; deg: number }[] = [
  { id: 'front', label: 'Chính diện', deg: 0 },
  { id: 'three_quarter_left', label: '3/4 Trái', deg: 45 },
  { id: 'profile_left', label: 'Nghiêng Trái', deg: 90 },
  { id: 'back_three_quarter_left', label: '3/4 Sau Trái', deg: 135 },
  { id: 'back', label: 'Sau Lưng', deg: 180 },
];

/** State templates per composite type */
const STATE_TEMPLATES: Record<string, { id: string; label: string; icon: string }[]> = {
  mat_composite: [
    { id: 'idle', label: 'Mở bình thường', icon: '👁️' },
    { id: 'blink', label: 'Nhắm mắt (Chớp)', icon: '😑' },
    { id: 'angry', label: 'Giận dữ (Nhíu)', icon: '😡' },
    { id: 'happy', label: 'Vui vẻ (Cong)', icon: '😊' },
    { id: 'shocked', label: 'Sốc (Mở to)', icon: '😲' },
  ],
  khuon_mat_composite: [
    { id: 'idle', label: 'Bình thường', icon: '😐' },
    { id: 'blush', label: 'Ửng hồng', icon: '😊' },
  ],
  toc_composite: [],
  trang_phuc_composite: [],
};

const MOUTH_PHONEMES = [
  { id: 'idle', label: 'Ngậm miệng', icon: '😶' },
  { id: 'talk_a', label: 'Khẩu hình A', icon: '😮' },
  { id: 'talk_i', label: 'Khẩu hình I', icon: '😁' },
  { id: 'talk_u', label: 'Khẩu hình U', icon: '😗' },
  { id: 'talk_e', label: 'Khẩu hình E', icon: '😄' },
  { id: 'talk_o', label: 'Khẩu hình O', icon: '😯' },
  { id: 'smile', label: 'Cười', icon: '😊' },
  { id: 'angry', label: 'Nghiến răng', icon: '😬' },
];

// ─── Local Storage ───────────────────────────────────────────────

const DETAIL_STORAGE_KEY = 'flowmy_detail_assemblies_v1';

function loadSavedDetails(): DetailAssembly[] {
  try {
    const raw = localStorage.getItem(DETAIL_STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch { return []; }
}

function saveDetailToStorage(detail: DetailAssembly) {
  const existing = loadSavedDetails();
  const idx = existing.findIndex((d) => d.id === detail.id);
  if (idx >= 0) existing[idx] = detail;
  else existing.unshift(detail);
  localStorage.setItem(DETAIL_STORAGE_KEY, JSON.stringify(existing));
}

function deleteDetailFromStorage(id: string) {
  const existing = loadSavedDetails().filter((d) => d.id !== id);
  localStorage.setItem(DETAIL_STORAGE_KEY, JSON.stringify(existing));
}

// ─── Component ───────────────────────────────────────────────────

export const DetailPartAssemblerTab: React.FC = () => {
  const [composites, setComposites] = useState<CompositePartDef[]>([]);
  const [selectedComposite, setSelectedComposite] = useState<CompositePartDef | null>(null);
  const [activeAngle, setActiveAngle] = useState<Character2DAngle>('front');
  const [activeSubPart, setActiveSubPart] = useState<SubPartDef | null>(null);
  const [subPartAssets, setSubPartAssets] = useState<Record<string, Asset2DItem[]>>({});
  const [loading, setLoading] = useState(true);
  const [savedDetails, setSavedDetails] = useState<DetailAssembly[]>(loadSavedDetails());

  // Current working detail assembly
  const [currentDetail, setCurrentDetail] = useState<DetailAssembly | null>(null);
  const [showStatesPanel, setShowStatesPanel] = useState(false);

  // Load composites on mount
  useEffect(() => {
    let cancelled = false;
    fetchPartHierarchy().then((list) => {
      if (!cancelled) {
        // Filter to only "detail-worthy" composites
        const detailComposites = list.filter((c) =>
          ['mat_composite', 'khuon_mat_composite', 'toc_composite', 'trang_phuc_composite'].includes(c.id)
          || c.sub_parts.length >= 2
        );
        setComposites(detailComposites);
        setLoading(false);
      }
    });
    return () => { cancelled = true; };
  }, []);

  // Load assets when composite changes
  useEffect(() => {
    if (!selectedComposite) return;
    let cancelled = false;

    const loadAll = async () => {
      const results: Record<string, Asset2DItem[]> = {};
      await Promise.all(
        selectedComposite.sub_parts.map(async (sp) => {
          try {
            results[sp.part_type] = await loadSubPartAssets(sp);
          } catch {
            results[sp.part_type] = [];
          }
        })
      );
      if (!cancelled) {
        setSubPartAssets(results);
        setActiveSubPart(selectedComposite.sub_parts[0]);
      }
    };
    loadAll();
    return () => { cancelled = true; };
  }, [selectedComposite?.id]);

  // Select a composite and create/load detail
  const handleSelectComposite = (comp: CompositePartDef) => {
    setSelectedComposite(comp);
    setActiveAngle('front');
    setShowStatesPanel(false);

    // Check if there's a saved detail for this composite
    const saved = savedDetails.find((d) => d.compositeId === comp.id);
    if (saved) {
      setCurrentDetail(saved);
    } else {
      setCurrentDetail({
        id: `detail_${comp.id}_${Date.now()}`,
        compositeId: comp.id,
        name: comp.label,
        icon: comp.icon,
        angles: {},
        states: {},
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      });
    }
  };

  // Apply an asset to a sub-part at the current angle
  const handleApplyAssetToSubPart = (subPartType: string, asset: Asset2DItem) => {
    if (!currentDetail) return;
    const hierDef = PART_HIERARCHY_CONFIG[subPartType as Character2DPartType];

    setCurrentDetail((prev) => {
      if (!prev) return prev;
      const angleConfigs = { ...(prev.angles[activeAngle] || {}) };
      angleConfigs[subPartType] = {
        path: `/${asset.path}`,
        offset: hierDef?.defaultOffset || [0, 0],
        scale: [1, 1],
        rotation: 0,
        pivot: hierDef?.defaultPivot || [0.5, 0.5],
        z_index: hierDef?.defaultZ || 5,
        opacity: 1,
      };
      return {
        ...prev,
        angles: { ...prev.angles, [activeAngle]: angleConfigs },
        updatedAt: new Date().toISOString(),
      };
    });
  };

  // Clear a sub-part at current angle
  const handleClearSubPart = (subPartType: string) => {
    if (!currentDetail) return;
    setCurrentDetail((prev) => {
      if (!prev) return prev;
      const angleConfigs = { ...(prev.angles[activeAngle] || {}) };
      delete angleConfigs[subPartType];
      return {
        ...prev,
        angles: { ...prev.angles, [activeAngle]: angleConfigs },
        updatedAt: new Date().toISOString(),
      };
    });
  };

  // Add a state variant
  const handleAddState = (subPartType: string, stateId: string, label: string, asset: Asset2DItem) => {
    if (!currentDetail) return;
    setCurrentDetail((prev) => {
      if (!prev) return prev;
      const key = `${subPartType}_${stateId}`;
      const states = { ...prev.states };
      if (!states[subPartType]) states[subPartType] = [];
      states[subPartType] = [
        ...states[subPartType].filter((s) => s.id !== stateId),
        { id: stateId, label, path: `/${asset.path}` },
      ];
      return { ...prev, states, updatedAt: new Date().toISOString() };
    });
  };

  // Save detail
  const handleSave = () => {
    if (!currentDetail) return;
    saveDetailToStorage(currentDetail);
    setSavedDetails(loadSavedDetails());
  };

  // Delete detail
  const handleDelete = (id: string) => {
    deleteDetailFromStorage(id);
    setSavedDetails(loadSavedDetails());
    if (currentDetail?.id === id) setCurrentDetail(null);
  };

  // Get composite icon
  const getCompositeIcon = (compId: string) => {
    if (compId.includes('mat')) return <Eye size={14} />;
    if (compId.includes('khuon_mat')) return <Smile size={14} />;
    if (compId.includes('toc')) return <Wind size={14} />;
    return <ImageIcon size={14} />;
  };

  if (loading) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#64748b' }}>
        <Loader2 size={20} style={{ animation: 'spin 1s linear infinite', marginRight: 8 }} />
        Đang tải cấu hình chi tiết...
      </div>
    );
  }

  // ─── Render ────────────────────────────────────────────────────

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '200px 1fr', gap: 10, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: Composite Selector ─── */}
      <div style={{
        display: 'flex', flexDirection: 'column', gap: 6,
        background: 'rgba(8, 13, 26, 0.95)', borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)', padding: 8, overflow: 'auto',
      }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', padding: '4px 0' }}>
          ⚙️ Chọn Chi Tiết Để Lắp Ráp
        </div>

        {composites.map((comp) => {
          const isActive = selectedComposite?.id === comp.id;
          const hasSaved = savedDetails.some((d) => d.compositeId === comp.id);
          return (
            <button
              key={comp.id}
              onClick={() => handleSelectComposite(comp)}
              style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '8px 10px', borderRadius: 6, fontSize: 11,
                fontWeight: isActive ? 700 : 600, textAlign: 'left',
                border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                background: isActive ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
                color: isActive ? '#38bdf8' : '#cbd5e1',
                cursor: 'pointer', transition: 'all 0.15s',
              }}
            >
              <span style={{ fontSize: 16 }}>{comp.icon}</span>
              <div style={{ flex: 1 }}>
                <div>{comp.label}</div>
                <div style={{ fontSize: 9, color: '#64748b' }}>
                  {comp.sub_parts.length} chi tiết
                </div>
              </div>
              {hasSaved && <Check size={12} style={{ color: '#10b981' }} />}
            </button>
          );
        })}

        {/* Saved Details List */}
        {savedDetails.length > 0 && (
          <>
            <div style={{ fontSize: 10, fontWeight: 700, color: '#10b981', padding: '8px 0 4px', borderTop: '1px solid rgba(255,255,255,0.06)' }}>
              💾 Chi Tiết Đã Lưu ({savedDetails.length})
            </div>
            {savedDetails.map((d) => (
              <div key={d.id} style={{
                display: 'flex', alignItems: 'center', gap: 6,
                padding: '5px 8px', borderRadius: 5, fontSize: 10,
                background: 'rgba(16, 185, 129, 0.08)',
                border: '1px solid rgba(16, 185, 129, 0.15)',
                color: '#a7f3d0',
              }}>
                <span>{d.icon}</span>
                <span style={{ flex: 1 }}>{d.name}</span>
                <button
                  onClick={(e) => { e.stopPropagation(); handleDelete(d.id); }}
                  style={{ background: 'none', border: 'none', color: '#f87171', cursor: 'pointer', padding: 2 }}
                >
                  <Trash2 size={10} />
                </button>
              </div>
            ))}
          </>
        )}
      </div>

      {/* ─── RIGHT: Assembly Workspace ─── */}
      <div style={{
        display: 'flex', flexDirection: 'column', gap: 8, height: '100%', overflow: 'hidden',
      }}>
        {!selectedComposite ? (
          <div style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            height: '100%', color: '#475569', gap: 12,
          }}>
            <Eye size={48} />
            <div style={{ fontSize: 14, fontWeight: 700 }}>Chọn chi tiết bên trái để bắt đầu</div>
            <div style={{ fontSize: 11, color: '#64748b', textAlign: 'center', maxWidth: 400 }}>
              Lắp ráp từng chi tiết con (mắt, mũi, miệng, tai...) với đa góc độ và trạng thái biểu cảm.
              Sau khi hoàn tất → dùng ở Tab 2.2 để ghép vào nhân vật.
            </div>
          </div>
        ) : (
          <>
            {/* ─── Angle Tabs ─── */}
            <div style={{
              display: 'flex', alignItems: 'center', gap: 6,
              background: 'rgba(8, 13, 26, 0.95)', borderRadius: 8, padding: '6px 10px',
              border: '1px solid rgba(255, 255, 255, 0.08)',
            }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#f8fafc', marginRight: 8 }}>
                {selectedComposite.icon} {selectedComposite.label}
              </span>
              <div style={{ display: 'flex', gap: 3, flex: 1 }}>
                {SUPPORTED_ANGLES.map((ang) => {
                  const isActive = activeAngle === ang.id;
                  const hasData = Boolean(currentDetail?.angles[ang.id]);
                  return (
                    <button
                      key={ang.id}
                      onClick={() => setActiveAngle(ang.id)}
                      style={{
                        padding: '4px 10px', borderRadius: 5, fontSize: 10, fontWeight: isActive ? 700 : 600,
                        border: isActive ? '1px solid #38bdf8' : '1px solid transparent',
                        background: isActive ? 'rgba(56, 189, 248, 0.2)' : hasData ? 'rgba(16, 185, 129, 0.1)' : 'rgba(255,255,255,0.04)',
                        color: isActive ? '#38bdf8' : hasData ? '#34d399' : '#94a3b8',
                        cursor: 'pointer', transition: 'all 0.15s',
                      }}
                    >
                      {ang.deg}° {ang.label}
                      {hasData && !isActive && <Check size={8} style={{ marginLeft: 3 }} />}
                    </button>
                  );
                })}
              </div>

              {/* Actions */}
              <button
                onClick={() => setShowStatesPanel(!showStatesPanel)}
                style={{
                  padding: '4px 10px', borderRadius: 5, fontSize: 10, fontWeight: 700,
                  border: '1px solid rgba(168, 85, 247, 0.4)',
                  background: showStatesPanel ? 'rgba(168, 85, 247, 0.25)' : 'rgba(168, 85, 247, 0.1)',
                  color: '#c084fc', cursor: 'pointer',
                }}
              >
                🎭 Trạng Thái
              </button>
              <button
                onClick={handleSave}
                style={{
                  display: 'flex', alignItems: 'center', gap: 4,
                  padding: '4px 12px', borderRadius: 5, fontSize: 10, fontWeight: 700,
                  border: '1px solid rgba(16, 185, 129, 0.4)',
                  background: 'rgba(16, 185, 129, 0.15)',
                  color: '#34d399', cursor: 'pointer',
                }}
              >
                <Save size={11} /> Lưu
              </button>
            </div>

            {/* ─── Main Content: Sub-Parts + Asset Grid ─── */}
            <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '160px 1fr', gap: 8, overflow: 'hidden' }}>
              {/* Sub-Part List */}
              <div style={{
                display: 'flex', flexDirection: 'column', gap: 4,
                background: 'rgba(8, 13, 26, 0.95)', borderRadius: 8, padding: 6,
                border: '1px solid rgba(255, 255, 255, 0.08)', overflow: 'auto',
              }}>
                <div style={{ fontSize: 9, fontWeight: 700, color: '#64748b', padding: '2px 4px' }}>
                  CHI TIẾT CON ({activeAngle === 'front' ? '0°' : `${SUPPORTED_ANGLES.find(a => a.id === activeAngle)?.deg}°`})
                </div>
                {selectedComposite.sub_parts.map((sp) => {
                  const isActive = activeSubPart?.part_type === sp.part_type;
                  const angleConfig = currentDetail?.angles[activeAngle]?.[sp.part_type];
                  const hasPart = Boolean(angleConfig);
                  const assetCount = subPartAssets[sp.part_type]?.length || 0;

                  return (
                    <button
                      key={sp.part_type}
                      onClick={() => setActiveSubPart(sp)}
                      style={{
                        display: 'flex', alignItems: 'center', gap: 6,
                        padding: '6px 8px', borderRadius: 5, fontSize: 10,
                        fontWeight: isActive ? 700 : 600, textAlign: 'left',
                        border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                        background: isActive ? 'rgba(56, 189, 248, 0.2)' : hasPart ? 'rgba(16, 185, 129, 0.08)' : 'rgba(0,0,0,0.2)',
                        color: isActive ? '#38bdf8' : hasPart ? '#34d399' : '#94a3b8',
                        cursor: 'pointer', transition: 'all 0.15s',
                      }}
                    >
                      <span style={{ fontSize: 13 }}>{sp.icon}</span>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{sp.label}</div>
                        <div style={{ fontSize: 8, color: '#475569' }}>
                          {assetCount} ảnh {sp.required ? '• Bắt buộc' : ''}
                        </div>
                      </div>
                      {hasPart && <Check size={10} style={{ color: '#10b981', flexShrink: 0 }} />}
                    </button>
                  );
                })}
              </div>

              {/* Asset Grid + States */}
              <div style={{
                display: 'flex', flexDirection: 'column', gap: 8,
                background: 'rgba(8, 13, 26, 0.95)', borderRadius: 8, padding: 8,
                border: '1px solid rgba(255, 255, 255, 0.08)', overflow: 'hidden',
              }}>
                {showStatesPanel ? (
                  /* ─── States/Expressions Panel ─── */
                  <div style={{ flex: 1, overflow: 'auto' }}>
                    <div style={{ fontSize: 11, fontWeight: 700, color: '#c084fc', padding: '4px 0 8px' }}>
                      🎭 Trạng Thái & Biểu Cảm — {selectedComposite.label}
                    </div>
                    <div style={{ fontSize: 10, color: '#64748b', marginBottom: 10 }}>
                      {selectedComposite.id === 'mat_composite'
                        ? 'Cấu hình trạng thái mắt: chớp mắt, giận, vui, sốc...'
                        : selectedComposite.id.includes('mieng')
                        ? 'Cấu hình khẩu hình nói A/I/U/E/O và biểu cảm miệng'
                        : 'Cấu hình trạng thái biểu cảm cho chi tiết này'}
                    </div>

                    {/* State templates */}
                    {(selectedComposite.id.includes('mieng') ? MOUTH_PHONEMES : STATE_TEMPLATES[selectedComposite.id] || []).map((tmpl) => {
                      const existingState = currentDetail?.states[activeSubPart?.part_type || '']?.find((s) => s.id === tmpl.id);
                      return (
                        <div key={tmpl.id} style={{
                          display: 'flex', alignItems: 'center', gap: 8,
                          padding: '6px 10px', marginBottom: 4, borderRadius: 6,
                          background: existingState ? 'rgba(16, 185, 129, 0.08)' : 'rgba(255,255,255,0.03)',
                          border: `1px solid ${existingState ? 'rgba(16, 185, 129, 0.2)' : 'rgba(255,255,255,0.06)'}`,
                        }}>
                          <span style={{ fontSize: 16 }}>{tmpl.icon}</span>
                          <div style={{ flex: 1 }}>
                            <div style={{ fontSize: 11, fontWeight: 600, color: '#f8fafc' }}>{tmpl.label}</div>
                            {existingState && (
                              <div style={{ fontSize: 9, color: '#34d399' }}>✓ Đã gán ảnh</div>
                            )}
                          </div>
                          {existingState ? (
                            <Check size={14} style={{ color: '#10b981' }} />
                          ) : (
                            <span style={{ fontSize: 9, color: '#64748b' }}>Kéo thả ảnh hoặc chọn từ thư mục</span>
                          )}
                        </div>
                      );
                    })}
                  </div>
                ) : (
                  /* ─── Asset Grid ─── */
                  <>
                    {activeSubPart && (
                      <div style={{
                        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                        padding: '4px 6px', borderBottom: '1px solid rgba(255,255,255,0.06)',
                      }}>
                        <div style={{ fontSize: 11, fontWeight: 700, color: '#f8fafc' }}>
                          {activeSubPart.icon} {activeSubPart.label}
                          <span style={{ fontSize: 9, color: '#64748b', marginLeft: 6 }}>
                            Góc {SUPPORTED_ANGLES.find(a => a.id === activeAngle)?.deg}°
                          </span>
                        </div>
                        {currentDetail?.angles[activeAngle]?.[activeSubPart.part_type] && (
                          <button
                            onClick={() => handleClearSubPart(activeSubPart.part_type)}
                            style={{
                              fontSize: 9, padding: '2px 8px', borderRadius: 4,
                              background: 'rgba(239, 68, 68, 0.15)', border: '1px solid rgba(239, 68, 68, 0.3)',
                              color: '#f87171', cursor: 'pointer',
                            }}
                          >
                            Xóa
                          </button>
                        )}
                      </div>
                    )}

                    <div style={{ flex: 1, overflow: 'auto' }}>
                      {activeSubPart && (subPartAssets[activeSubPart.part_type] || []).length === 0 ? (
                        <div style={{
                          display: 'flex', flexDirection: 'column', alignItems: 'center',
                          justifyContent: 'center', height: '100%', gap: 8, color: '#475569',
                        }}>
                          <ImageIcon size={32} />
                          <div style={{ fontSize: 11, textAlign: 'center' }}>
                            Chưa có ảnh trong<br />
                            <code style={{ fontSize: 10, color: '#38bdf8' }}>{activeSubPart.folder}/</code>
                          </div>
                          <div style={{ fontSize: 9.5, color: '#64748b' }}>
                            Bỏ ảnh vào thư mục hoặc cắt từ Tab 1
                          </div>
                        </div>
                      ) : (
                        <div style={{
                          display: 'grid',
                          gridTemplateColumns: 'repeat(auto-fill, minmax(85px, 1fr))',
                          gap: 6,
                        }}>
                          {activeSubPart && (subPartAssets[activeSubPart.part_type] || []).map((asset) => {
                            const currentPath = currentDetail?.angles[activeAngle]?.[activeSubPart.part_type]?.path;
                            const isSelected = currentPath === `/${asset.path}`;
                            return (
                              <div
                                key={asset.id}
                                onClick={() => handleApplyAssetToSubPart(activeSubPart.part_type, asset)}
                                style={{
                                  aspectRatio: '1', borderRadius: 6, cursor: 'pointer',
                                  border: isSelected ? '2px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                                  background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(15, 23, 42, 0.6)',
                                  overflow: 'hidden', display: 'flex', flexDirection: 'column',
                                  transition: 'all 0.15s', position: 'relative',
                                }}
                              >
                                <div style={{
                                  flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
                                  background: 'repeating-conic-gradient(rgba(255,255,255,0.03) 0% 25%, transparent 0% 50%) 50% / 12px 12px',
                                }}>
                                  {asset.previewUrl ? (
                                    <img
                                      src={`/${asset.previewUrl}`}
                                      alt={asset.name}
                                      style={{ maxWidth: '90%', maxHeight: '90%', objectFit: 'contain' }}
                                      loading="lazy"
                                    />
                                  ) : (
                                    <ImageIcon size={18} color="#475569" />
                                  )}
                                </div>
                                <div style={{
                                  fontSize: 8, fontWeight: 600, padding: '2px 4px',
                                  color: isSelected ? '#38bdf8' : '#cbd5e1',
                                  textAlign: 'center', whiteSpace: 'nowrap',
                                  overflow: 'hidden', textOverflow: 'ellipsis',
                                  background: 'rgba(0, 0, 0, 0.4)',
                                }}>
                                  {asset.name}
                                </div>
                                {isSelected && (
                                  <div style={{
                                    position: 'absolute', top: 3, right: 3,
                                    width: 14, height: 14, borderRadius: '50%',
                                    background: '#0284c7', display: 'flex',
                                    alignItems: 'center', justifyContent: 'center',
                                  }}>
                                    <Check size={8} color="#fff" />
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  </>
                )}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
