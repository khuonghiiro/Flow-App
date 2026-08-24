import React, { useState } from 'react';
import {
  Layers,
  Grid,
  Sparkles,
  ArrowRight,
  Eye,
  EyeOff,
  Trash2,
  Save,
  Download,
  CheckCircle2,
  Box,
} from 'lucide-react';
import { DecomposedPartItem } from '../../../core/utils/AntigravityDecomposerService';

interface DecomposedPartsStagingColumnProps {
  decomposedParts: DecomposedPartItem[];
  onTogglePartVisibility: (partId: string) => void;
  onDeletePart: (partId: string) => void;
  onTransferToGridSlicer: () => void;
  isStitching: boolean;
  spriteSheetPreviewUrl: string | null;
}

export const DecomposedPartsStagingColumn: React.FC<DecomposedPartsStagingColumnProps> = ({
  decomposedParts,
  onTogglePartVisibility,
  onDeletePart,
  onTransferToGridSlicer,
  isStitching,
  spriteSheetPreviewUrl,
}) => {
  const [viewMode, setViewMode] = useState<'layers' | 'grid'>('layers');

  const visiblePartsCount = decomposedParts.filter((p) => p.selected).length;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#090d16',
        borderRadius: 10,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflow: 'hidden',
      }}
    >
      {/* Column Header */}
      <div
        style={{
          padding: '10px 14px',
          background: 'rgba(15, 23, 42, 0.8)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div
            style={{
              width: 26,
              height: 26,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #10b981, #06b6d4)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              fontSize: 12,
              fontWeight: 800,
            }}
          >
            3
          </div>
          <div>
            <div style={{ fontSize: 12.5, fontWeight: 700, color: '#f8fafc' }}>
              Bộ Linh Kiện Đã Tách ({decomposedParts.length})
            </div>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              Kiểm tra layer & xuất sang Tab Cắt Lưới 3D
            </div>
          </div>
        </div>

        {/* View Mode Switch */}
        <div
          style={{
            display: 'flex',
            gap: 2,
            background: 'rgba(0,0,0,0.4)',
            padding: 2,
            borderRadius: 5,
            border: '1px solid rgba(255,255,255,0.08)',
          }}
        >
          <button
            onClick={() => setViewMode('layers')}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              border: 'none',
              background: viewMode === 'layers' ? '#0284c7' : 'transparent',
              color: viewMode === 'layers' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Layers size={11} /> Layer
          </button>
          <button
            onClick={() => setViewMode('grid')}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              border: 'none',
              background: viewMode === 'grid' ? '#0284c7' : 'transparent',
              color: viewMode === 'grid' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Grid size={11} /> Lưới
          </button>
        </div>
      </div>

      {/* Column Body: Scrollable */}
      <div
        style={{
          flex: 1,
          padding: 12,
          display: 'flex',
          flexDirection: 'column',
          gap: 10,
          overflowY: 'auto',
        }}
      >
        {decomposedParts.length === 0 ? (
          <div
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#64748b',
              textAlign: 'center',
              padding: 20,
            }}
          >
            <Box size={36} style={{ marginBottom: 8, opacity: 0.4 }} />
            <div style={{ fontSize: 11.5, fontWeight: 600, color: '#94a3b8' }}>
              Chưa có linh kiện nào được tách
            </div>
            <div style={{ fontSize: 9.5, marginTop: 4, maxWidth: 220 }}>
              Hãy chọn nhân vật ở Cột 1 và bấm Tách Chi Tiết ở Cột 2 để xem danh sách linh kiện tại đây.
            </div>
          </div>
        ) : viewMode === 'layers' ? (
          /* Layers View: Stack of Individual Component Cards */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {decomposedParts.map((part) => (
              <div
                key={part.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  padding: '6px 10px',
                  borderRadius: 6,
                  background: part.selected ? '#040711' : 'rgba(0,0,0,0.2)',
                  border: part.selected
                    ? '1px solid rgba(255,255,255,0.1)'
                    : '1px dashed rgba(255,255,255,0.06)',
                  opacity: part.selected ? 1 : 0.5,
                }}
              >
                {/* Thumbnail */}
                <div
                  style={{
                    width: 38,
                    height: 38,
                    borderRadius: 4,
                    backgroundImage: `url(${part.imageUrl})`,
                    backgroundSize: 'contain',
                    backgroundRepeat: 'no-repeat',
                    backgroundPosition: 'center',
                    backgroundColor: '#00FF00',
                    border: '1px solid rgba(255,255,255,0.15)',
                    flexShrink: 0,
                  }}
                />

                {/* Name & Metadata */}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      fontSize: 10.5,
                      fontWeight: 600,
                      color: '#f8fafc',
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {part.name}
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 2 }}>
                    <span
                      style={{
                        fontSize: 8.5,
                        padding: '1px 5px',
                        borderRadius: 3,
                        background: 'rgba(56, 189, 248, 0.15)',
                        color: '#38bdf8',
                        fontWeight: 600,
                      }}
                    >
                      Slot: {part.slotName}
                    </span>
                    <span
                      style={{
                        fontSize: 8.5,
                        padding: '1px 5px',
                        borderRadius: 3,
                        background: 'rgba(168, 85, 247, 0.15)',
                        color: '#c084fc',
                        fontWeight: 600,
                      }}
                    >
                      Z: {part.zIndex}
                    </span>
                  </div>
                </div>

                {/* Actions: Visibility & Delete */}
                <button
                  onClick={() => onTogglePartVisibility(part.id)}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: part.selected ? '#38bdf8' : '#64748b',
                    cursor: 'pointer',
                    padding: 4,
                  }}
                  title={part.selected ? 'Ẩn linh kiện' : 'Hiện linh kiện'}
                >
                  {part.selected ? <Eye size={13} /> : <EyeOff size={13} />}
                </button>

                <button
                  onClick={() => onDeletePart(part.id)}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: '#ef4444',
                    cursor: 'pointer',
                    padding: 4,
                  }}
                  title="Xóa linh kiện"
                >
                  <Trash2 size={13} />
                </button>
              </div>
            ))}
          </div>
        ) : (
          /* Grid View: Sprite Sheet Composite Preview */
          <div
            style={{
              flex: 1,
              minHeight: 200,
              borderRadius: 8,
              border: '1px solid rgba(255,255,255,0.1)',
              background: '#040711',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              overflow: 'hidden',
              position: 'relative',
            }}
          >
            {spriteSheetPreviewUrl ? (
              <img
                src={spriteSheetPreviewUrl}
                alt="Sprite Sheet Grid Preview"
                style={{
                  maxWidth: '100%',
                  maxHeight: '100%',
                  objectFit: 'contain',
                }}
              />
            ) : (
              <div style={{ fontSize: 10, color: '#64748b' }}>
                Đang tổng hợp lưới linh kiện...
              </div>
            )}
          </div>
        )}
      </div>

      {/* Column Footer: Bridge to Grid Slicer & 3D Assembler */}
      <div
        style={{
          padding: '10px 12px',
          background: 'rgba(15, 23, 42, 0.9)',
          borderTop: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <button
          onClick={onTransferToGridSlicer}
          disabled={decomposedParts.length === 0 || isStitching}
          style={{
            width: '100%',
            height: 38,
            borderRadius: 6,
            background:
              decomposedParts.length > 0
                ? 'linear-gradient(135deg, #0284c7 0%, #2563eb 50%, #7c3aed 100%)'
                : 'rgba(255,255,255,0.06)',
            color: decomposedParts.length > 0 ? '#ffffff' : '#64748b',
            fontSize: 11.5,
            fontWeight: 700,
            border:
              decomposedParts.length > 0
                ? '1px solid rgba(255,255,255,0.25)'
                : 'none',
            cursor: decomposedParts.length > 0 ? 'pointer' : 'not-allowed',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 7,
            boxShadow:
              decomposedParts.length > 0
                ? '0 4px 16px rgba(37, 99, 235, 0.4)'
                : 'none',
          }}
        >
          <Sparkles size={15} />
          <span>🚀 Đưa Sang Tab Cắt Lưới & Lắp Ráp 3D ({visiblePartsCount} ô)</span>
          <ArrowRight size={14} />
        </button>
      </div>
    </div>
  );
};
