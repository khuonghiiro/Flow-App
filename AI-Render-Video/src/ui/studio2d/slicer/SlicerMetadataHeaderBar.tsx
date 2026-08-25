import React from 'react';
import { ParsedPartFilenameInfo } from '../../../core/assets/Asset2DRegistry';

export interface SlicerMetadataHeaderBarProps {
  metadata: ParsedPartFilenameInfo | null;
}

export const SlicerMetadataHeaderBar: React.FC<SlicerMetadataHeaderBarProps> = ({ metadata }) => {
  if (!metadata) return null;

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '5px 12px',
        background:
          'linear-gradient(90deg, rgba(2, 132, 199, 0.18) 0%, rgba(139, 92, 246, 0.18) 100%)',
        borderRadius: 6,
        border: '1px solid rgba(56, 189, 248, 0.35)',
        fontSize: 11,
        color: '#e0f2fe',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <span style={{ fontWeight: 800, color: '#38bdf8' }}>🏷️ TỰ ĐỘNG NHẬN DIỆN TỆP:</span>
        <span
          style={{
            background: 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)',
            color: '#fff',
            padding: '2px 8px',
            borderRadius: 4,
            fontWeight: 700,
          }}
        >
          {metadata.part_name} ({metadata.part_id})
        </span>
        <span>
          • Góc quay: <b>{metadata.angle_name}</b>
        </span>
        {metadata.variant_index && (
          <span
            style={{
              background: 'rgba(245, 158, 11, 0.25)',
              color: '#fbbf24',
              border: '1px solid rgba(245, 158, 11, 0.4)',
              padding: '1px 6px',
              borderRadius: 4,
              fontWeight: 700,
              fontSize: 10,
            }}
          >
            Biến thể #{metadata.variant_index}
          </span>
        )}
        <span>
          • Nhóm: <b>{metadata.group_name}</b>
        </span>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        {metadata.original_filename && metadata.original_filename !== metadata.canonical_filename && (
          <span style={{ fontSize: 9.5, color: '#64748b', fontFamily: 'monospace' }} title="Tệp nguồn thực tế">
            [{metadata.original_filename}] ➔
          </span>
        )}
        <span
          style={{ fontSize: 10, color: '#38bdf8', fontFamily: 'monospace', fontWeight: 600 }}
          title="Tên tệp chuẩn dùng trong Pipeline"
        >
          {metadata.canonical_filename || metadata.save_filename}
        </span>
      </div>
    </div>
  );
};
