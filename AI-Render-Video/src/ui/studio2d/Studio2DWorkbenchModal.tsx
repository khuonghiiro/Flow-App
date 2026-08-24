import React, { useState } from 'react';
import {
  Scissors,
  Sparkles,
  Layers,
  Map as MapIcon,
  Clapperboard,
  X,
  Grid,
  Bot,
} from 'lucide-react';
import { ImageSegmenterCropper } from './ImageSegmenterCropper';
import { AIPromptGenerator2D } from './AIPromptGenerator2D';
import { Character2DAssembler } from './Character2DAssembler';
import { AutoGridSlicer3DAssembler } from './AutoGridSlicer3DAssembler';
import { Map2DAssembler } from './Map2DAssembler';
import { ActionSequence2DDirector } from './ActionSequence2DDirector';
import { AIAntigravityDecomposerPanel } from './agent/AIAntigravityDecomposerPanel';
import {
  Character2DAssembly,
  Character2DPartType,
  Map2DPreset,
} from '../../types/scene2d';
import {
  DEFAULT_SAMPLE_CHARACTERS_2D,
  DEFAULT_SAMPLE_MAPS_2D,
} from '../../core/assets/Asset2DRegistry';

interface Studio2DWorkbenchModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const Studio2DWorkbenchModal: React.FC<Studio2DWorkbenchModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [activeTab, setActiveTab] = useState<
    'grid_slicer' | 'character' | 'cropper' | 'prompt' | 'map' | 'director'
  >('grid_slicer');

  // Shared 2D Assembly & Map State
  const [characterAssembly, setCharacterAssembly] = useState<Character2DAssembly>(
    DEFAULT_SAMPLE_CHARACTERS_2D[0]
  );
  const [mapPreset, setMapPreset] = useState<Map2DPreset>(
    DEFAULT_SAMPLE_MAPS_2D[0]
  );

  // Transferred Sprite Sheet from Tab 0 (Antigravity AI Agent)
  const [transferredSpriteSheetUrl, setTransferredSpriteSheetUrl] = useState<string | null>(null);
  const [transferredCategoryId, setTransferredCategoryId] = useState<string | undefined>(undefined);

  if (!isOpen) return null;

  /**
   * Applies cropped image part directly into current Character Assembly
   */
  const handleApplyCroppedPart = (slot: Character2DPartType, dataUrl: string) => {
    setCharacterAssembly((prev) => ({
      ...prev,
      parts: {
        ...prev.parts,
        [slot]: {
          ...(prev.parts[slot] || {
            offset: [0, 0],
            scale: [1, 1],
            rotation: 0,
            pivot: [0.5, 0.5],
            flipX: false,
            flipY: false,
            z_index: 5,
            opacity: 1,
          }),
          path: dataUrl,
        },
      },
    }));
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'rgba(3, 7, 18, 0.88)',
        backdropFilter: 'blur(16px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 6,
      }}
    >
      <div
        style={{
          width: '99vw',
          maxWidth: '1850px',
          height: '97vh',
          background: '#0b0f19',
          border: '1px solid rgba(255, 255, 255, 0.14)',
          borderRadius: 12,
          boxShadow: '0 24px 60px rgba(0, 0, 0, 0.8), 0 0 40px rgba(56, 189, 248, 0.18)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* ─── MODAL HEADER ────────────────────────────────────────── */}
        <div
          style={{
            height: 50,
            background: 'rgba(15, 23, 42, 0.9)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0 14px',
            flexShrink: 0,
          }}
        >
          {/* Title & Brand */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #0284c7, #a855f7)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
                boxShadow: '0 0 12px rgba(56, 189, 248, 0.4)',
              }}
            >
              <Scissors size={18} />
            </div>
            <div>
              <div style={{ fontSize: 14, fontWeight: 700, color: '#f8fafc' }}>
                Xưởng Lắp Ráp 2D & Motion Comic Studio
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                Tách Nền • Khung Cắt Chuẩn • Ghép Linh Kiện • Parallax • Nhảy Góc Máy
              </div>
            </div>
          </div>

          {/* Navigation Tabs */}
          <div
            style={{
              display: 'flex',
              gap: 4,
              background: 'rgba(0, 0, 0, 0.3)',
              padding: 4,
              borderRadius: 8,
              border: '1px solid rgba(255, 255, 255, 0.05)',
            }}
          >
            <button
              onClick={() => setActiveTab('grid_slicer')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 700,
                border: 'none',
                background: activeTab === 'grid_slicer' ? '#0284c7' : 'transparent',
                color: activeTab === 'grid_slicer' ? '#ffffff' : '#38bdf8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Grid size={13} /> 1. ⚡ Cắt Lưới & Lắp Ráp 3D
            </button>

            <button
              onClick={() => setActiveTab('character')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: activeTab === 'character' ? '#0284c7' : 'transparent',
                color: activeTab === 'character' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Layers size={13} /> 2. Bàn Lắp Ráp Nhân Vật
            </button>

            <button
              onClick={() => setActiveTab('cropper')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: activeTab === 'cropper' ? '#0284c7' : 'transparent',
                color: activeTab === 'cropper' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Scissors size={13} /> 3. Cắt Khung Đơn Lẻ
            </button>

            <button
              onClick={() => setActiveTab('prompt')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: activeTab === 'prompt' ? '#0284c7' : 'transparent',
                color: activeTab === 'prompt' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Sparkles size={13} /> 4. Trợ Lý Prompt AI
            </button>

            <button
              onClick={() => setActiveTab('map')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: activeTab === 'map' ? '#0284c7' : 'transparent',
                color: activeTab === 'map' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <MapIcon size={13} /> 5. Lắp Ráp Map Parallax
            </button>

            <button
              onClick={() => setActiveTab('director')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: activeTab === 'director' ? '#0284c7' : 'transparent',
                color: activeTab === 'director' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Clapperboard size={13} /> 6. Kịch Bản & Cắt Cảnh
            </button>
          </div>

          {/* Close Button */}
          <button
            onClick={onClose}
            title="Đóng Xưởng 2D (Esc)"
            style={{
              width: 32,
              height: 32,
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#94a3b8',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
          >
            <X size={16} />
          </button>
        </div>

        {/* ─── MODAL BODY (Active Tab Component) ───────────────────── */}
        <div style={{ flex: 1, padding: 14, overflow: 'hidden', minHeight: 0 }}>
          {activeTab === 'grid_slicer' && (
            <AutoGridSlicer3DAssembler
              currentAssembly={characterAssembly}
              onApplyAssembly={setCharacterAssembly}
              onSwitchToAssemblyTab={() => setActiveTab('character')}
              externalImageUrl={transferredSpriteSheetUrl}
              externalCategoryId={transferredCategoryId}
            />
          )}

          {activeTab === 'character' && (
            <Character2DAssembler
              assembly={characterAssembly}
              onChangeAssembly={setCharacterAssembly}
            />
          )}

          {activeTab === 'cropper' && (
            <ImageSegmenterCropper onApplyPartToAssembly={handleApplyCroppedPart} />
          )}

          {activeTab === 'prompt' && <AIPromptGenerator2D />}

          {activeTab === 'map' && (
            <Map2DAssembler currentMap={mapPreset} onChangeMap={setMapPreset} />
          )}

          {activeTab === 'director' && <ActionSequence2DDirector />}
        </div>
      </div>
    </div>
  );
};
