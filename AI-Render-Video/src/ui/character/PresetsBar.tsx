import React from 'react';
import { Trash2 } from 'lucide-react';

export interface CustomPreset {
  id: string;
  name: string;
  body: string;
  costume: string;
  face: string;
  gender: 'male' | 'female';
}

interface PresetsBarProps {
  presets: CustomPreset[];
  onApply: (preset: CustomPreset) => void;
  onDelete: (id: string, e: React.MouseEvent) => void;
}

export const PresetsBar: React.FC<PresetsBarProps> = ({ presets, onApply, onDelete }) => {
  return (
    <div
      style={{
        flexShrink: 0,
        height: 34,
        padding: '0 10px',
        background: 'rgba(0, 0, 0, 0.4)',
        borderTop: '1px solid rgba(255, 255, 255, 0.06)',
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        overflowX: 'auto',
        whiteSpace: 'nowrap',
      }}
    >
      <span style={{ fontSize: 9, fontWeight: 700, color: '#94a3b8', textTransform: 'uppercase', flexShrink: 0 }}>
        Mẫu Phối Sẵn:
      </span>
      <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
        {presets.map((p) => (
          <div
            key={p.id}
            onClick={() => onApply(p)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              padding: '2px 7px',
              borderRadius: 4,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#cbd5e1',
              fontSize: 10,
              fontWeight: 600,
              cursor: 'pointer',
              transition: 'all 0.15s',
            }}
          >
            <span>{p.name}</span>
            {!['preset_amber_nectar', 'preset_precision_strike', 'preset_scary_cat', 'preset_sleuth_verdict'].includes(p.id) && (
              <span
                onClick={(e) => onDelete(p.id, e)}
                title="Xóa mẫu"
                style={{ display: 'flex', alignItems: 'center' }}
              >
                <Trash2 size={9} color="#f87171" />
              </span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};
