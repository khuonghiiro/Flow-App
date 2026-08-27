import React, { useState, useEffect, useRef } from 'react';
import { ArrowLeft, Loader2, ImageIcon, Check, AlertCircle } from 'lucide-react';
import type { Character2DPartType, Character2DAssembly } from '../../../types/scene2d';
import type { CompositePartDef, SubPartDef } from '../../../core/assets/PartAssemblyHierarchyRegistry';
import { loadSubPartAssets } from '../../../core/assets/PartAssemblyHierarchyRegistry';
import type { Asset2DItem } from '../../../core/assets/Asset2DStructureRegistry';
import { PART_HIERARCHY_CONFIG } from '../../../core/assets/Asset2DRegistry';

interface AssemblerSubPartPanelProps {
  composite: CompositePartDef;
  assembly: Character2DAssembly;
  onChangeAssembly: (updated: Character2DAssembly) => void;
  onBack: () => void;
}

/**
 * Drill-down panel for assembling sub-parts of a composite.
 * Replaces the Material Drawer when user clicks a composite slot.
 */
export const AssemblerSubPartPanel: React.FC<AssemblerSubPartPanelProps> = ({
  composite,
  assembly,
  onChangeAssembly,
  onBack,
}) => {
  const [activeSubPart, setActiveSubPart] = useState<SubPartDef>(composite.sub_parts[0]);
  const [subPartAssets, setSubPartAssets] = useState<Record<string, Asset2DItem[]>>({});
  const [initialLoading, setInitialLoading] = useState(true);
  const loadedRef = useRef<Set<string>>(new Set());

  // Load all sub-parts once when composite changes
  useEffect(() => {
    let cancelled = false;
    loadedRef.current = new Set();
    setSubPartAssets({});
    setInitialLoading(true);

    const loadAll = async () => {
      const results: Record<string, Asset2DItem[]> = {};
      await Promise.all(
        composite.sub_parts.map(async (sp) => {
          try {
            const items = await loadSubPartAssets(sp);
            results[sp.part_type] = items;
            loadedRef.current.add(sp.part_type);
          } catch {
            results[sp.part_type] = [];
          }
        })
      );
      if (!cancelled) {
        setSubPartAssets(results);
        setInitialLoading(false);
      }
    };

    loadAll();
    return () => { cancelled = true; };
  }, [composite.id]);

  // Apply an asset to the assembly
  const handleApplyAsset = (partType: Character2DPartType, asset: Asset2DItem) => {
    const hierarchyDef = PART_HIERARCHY_CONFIG[partType];
    const existing = assembly.parts[partType];

    const updatedParts = {
      ...assembly.parts,
      [partType]: {
        ...(existing || {
          offset: hierarchyDef?.defaultOffset || [0, 0],
          scale: [1, 1] as [number, number],
          rotation: 0,
          pivot: hierarchyDef?.defaultPivot || [0.5, 0.5],
          flipX: false,
          flipY: false,
          z_index: hierarchyDef?.defaultZ || 5,
          z_depth_3d: hierarchyDef?.defaultZDepth3D || 0,
          opacity: 1,
        }),
        path: `/${asset.path}`,
      },
    };

    onChangeAssembly({
      ...assembly,
      parts: updatedParts,
      updated_at: new Date().toISOString(),
    });
  };

  // Clear a sub-part from assembly
  const handleClearSubPart = (partType: Character2DPartType) => {
    const updatedParts = { ...assembly.parts };
    delete updatedParts[partType];
    onChangeAssembly({
      ...assembly,
      parts: updatedParts,
      updated_at: new Date().toISOString(),
    });
  };

  const currentAssets = subPartAssets[activeSubPart.part_type] || [];
  const isLoading = initialLoading && currentAssets.length === 0;
  const currentPartPath = assembly.parts[activeSubPart.part_type as Character2DPartType]?.path;

  return (
    <div style={{
      display: 'flex', flexDirection: 'column', height: '100%',
      background: 'rgba(8, 13, 26, 0.95)', borderRadius: 8,
      border: '1px solid rgba(255, 255, 255, 0.08)', overflow: 'hidden',
    }}>
      {/* ─── Header: Back + Composite Title ─── */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px',
        borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        background: 'rgba(15, 23, 42, 0.8)',
      }}>
        <button
          onClick={onBack}
          style={{
            display: 'flex', alignItems: 'center', gap: 4,
            padding: '4px 10px', borderRadius: 6, fontSize: 11, fontWeight: 700,
            background: 'rgba(255, 255, 255, 0.06)', border: '1px solid rgba(255, 255, 255, 0.12)',
            color: '#94a3b8', cursor: 'pointer', transition: 'all 0.2s',
          }}
        >
          <ArrowLeft size={14} /> Quay Lại
        </button>
        <div style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
          {composite.icon} {composite.label}
        </div>
        <div style={{ fontSize: 10, color: '#64748b', marginLeft: 'auto' }}>
          {composite.sub_parts.length} chi tiết con
        </div>
      </div>

      {/* ─── Sub-Part Tabs ─── */}
      <div style={{
        display: 'flex', gap: 3, padding: '6px 8px', overflowX: 'auto',
        borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
        background: 'rgba(0, 0, 0, 0.3)',
      }}>
        {composite.sub_parts.map((sp) => {
          const isActive = activeSubPart.part_type === sp.part_type;
          const hasPart = Boolean(assembly.parts[sp.part_type as Character2DPartType]?.path);
          const assetCount = subPartAssets[sp.part_type]?.length || 0;

          return (
            <button
              key={sp.part_type}
              onClick={() => setActiveSubPart(sp)}
              style={{
                display: 'flex', alignItems: 'center', gap: 4,
                padding: '5px 10px', borderRadius: 6, fontSize: 10.5,
                fontWeight: isActive ? 700 : 600, whiteSpace: 'nowrap',
                border: isActive ? '1px solid #38bdf8' : '1px solid transparent',
                background: isActive
                  ? 'rgba(56, 189, 248, 0.2)'
                  : hasPart
                  ? 'rgba(16, 185, 129, 0.12)'
                  : 'rgba(255, 255, 255, 0.04)',
                color: isActive ? '#38bdf8' : hasPart ? '#34d399' : '#94a3b8',
                cursor: 'pointer', transition: 'all 0.15s', position: 'relative',
              }}
              title={sp.description}
            >
              <span>{sp.icon}</span>
              <span>{sp.label}</span>
              {hasPart && <Check size={10} style={{ color: '#10b981' }} />}
              {sp.required && !hasPart && (
                <AlertCircle size={10} style={{ color: '#f59e0b' }} />
              )}
              {assetCount > 0 && (
                <span style={{
                  fontSize: 8, fontWeight: 700, color: '#64748b',
                  background: 'rgba(255,255,255,0.06)', padding: '1px 4px', borderRadius: 3,
                }}>
                  {assetCount}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* ─── Sub-Part Info Bar ─── */}
      <div style={{
        padding: '6px 12px', fontSize: 10, color: '#64748b',
        borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <span>
          {activeSubPart.icon} {activeSubPart.description}
          {activeSubPart.required && <span style={{ color: '#f59e0b', marginLeft: 4 }}>• Bắt buộc</span>}
        </span>
        {currentPartPath && (
          <button
            onClick={() => handleClearSubPart(activeSubPart.part_type as Character2DPartType)}
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

      {/* ─── Asset Grid ─── */}
      <div style={{ flex: 1, overflow: 'auto', padding: 8 }}>
        {isLoading ? (
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            height: '100%', gap: 8, color: '#64748b', fontSize: 12,
          }}>
            <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} />
            Đang quét thư mục...
          </div>
        ) : currentAssets.length === 0 ? (
          <div style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center',
            justifyContent: 'center', height: '100%', gap: 8, color: '#475569',
          }}>
            <ImageIcon size={32} />
            <div style={{ fontSize: 11, textAlign: 'center' }}>
              Chưa có tài nguyên trong<br />
              <code style={{ fontSize: 10, color: '#38bdf8' }}>{activeSubPart.folder}/</code>
            </div>
            <div style={{ fontSize: 9.5, color: '#64748b' }}>
              Bỏ ảnh vào thư mục trên hoặc cắt từ Tab 1
            </div>
          </div>
        ) : (
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(80px, 1fr))',
            gap: 6,
          }}>
            {currentAssets.map((asset) => {
              const isSelected = currentPartPath === `/${asset.path}` || currentPartPath === asset.path;

              return (
                <div
                  key={asset.id}
                  onClick={() => handleApplyAsset(activeSubPart.part_type as Character2DPartType, asset)}
                  style={{
                    aspectRatio: '1',
                    borderRadius: 6,
                    border: isSelected
                      ? '2px solid #38bdf8'
                      : '1px solid rgba(255, 255, 255, 0.08)',
                    background: isSelected
                      ? 'rgba(56, 189, 248, 0.15)'
                      : 'rgba(15, 23, 42, 0.6)',
                    cursor: 'pointer',
                    overflow: 'hidden',
                    display: 'flex',
                    flexDirection: 'column',
                    transition: 'all 0.15s',
                    position: 'relative',
                  }}
                  title={asset.name}
                >
                  {/* Thumbnail */}
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
                      <ImageIcon size={20} color="#475569" />
                    )}
                  </div>

                  {/* Name */}
                  <div style={{
                    fontSize: 8.5, fontWeight: 600, padding: '2px 4px',
                    color: isSelected ? '#38bdf8' : '#cbd5e1',
                    textAlign: 'center', whiteSpace: 'nowrap',
                    overflow: 'hidden', textOverflow: 'ellipsis',
                    background: 'rgba(0, 0, 0, 0.4)',
                  }}>
                    {asset.name}
                  </div>

                  {/* Selected check */}
                  {isSelected && (
                    <div style={{
                      position: 'absolute', top: 3, right: 3,
                      width: 16, height: 16, borderRadius: '50%',
                      background: '#0284c7', display: 'flex',
                      alignItems: 'center', justifyContent: 'center',
                    }}>
                      <Check size={10} color="#fff" />
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* ─── Assembly Status Footer ─── */}
      <div style={{
        padding: '6px 12px', borderTop: '1px solid rgba(255, 255, 255, 0.06)',
        background: 'rgba(0, 0, 0, 0.3)', display: 'flex', gap: 6, flexWrap: 'wrap',
      }}>
        {composite.sub_parts.map((sp) => {
          const hasPart = Boolean(assembly.parts[sp.part_type as Character2DPartType]?.path);
          return (
            <div
              key={sp.part_type}
              style={{
                fontSize: 9, padding: '2px 6px', borderRadius: 4,
                background: hasPart ? 'rgba(16, 185, 129, 0.15)' : 'rgba(255, 255, 255, 0.04)',
                color: hasPart ? '#34d399' : '#475569',
                border: `1px solid ${hasPart ? 'rgba(16, 185, 129, 0.3)' : 'rgba(255, 255, 255, 0.06)'}`,
              }}
            >
              {sp.icon} {hasPart ? '✓' : '—'}
            </div>
          );
        })}
      </div>
    </div>
  );
};
