import React, { useState, Suspense } from 'react';
import {
  Scissors,
  Sparkles,
  Layers,
  Map as MapIcon,
  Clapperboard,
  X,
  Grid,
  Bot,
  Loader,
} from 'lucide-react';
import type {
  Character2DAssembly,
  Character2DPartType,
  Map2DPreset,
} from '../../types/scene2d';
import {
  DEFAULT_SAMPLE_CHARACTERS_2D,
  DEFAULT_SAMPLE_MAPS_2D,
} from '../../core/assets/Asset2DRegistry';

// ─── Lazy-loaded sub-tabs (code-split from 654KB monolith) ───
const ImageSegmenterCropper = React.lazy(() =>
  import('./ImageSegmenterCropper').then(m => ({ default: m.ImageSegmenterCropper }))
);
const AIPromptGenerator2D = React.lazy(() =>
  import('./AIPromptGenerator2D').then(m => ({ default: m.AIPromptGenerator2D }))
);
const Character2DAssembler = React.lazy(() =>
  import('./Character2DAssembler').then(m => ({ default: m.Character2DAssembler }))
);
const AutoGridSlicer3DAssembler = React.lazy(() =>
  import('./AutoGridSlicer3DAssembler').then(m => ({ default: m.AutoGridSlicer3DAssembler }))
);
const AnimationSlicerTab = React.lazy(() =>
  import('./animation_slicer/AnimationSlicerTab').then(m => ({ default: m.AnimationSlicerTab }))
);
const Map2DAssembler = React.lazy(() =>
  import('./Map2DAssembler').then(m => ({ default: m.Map2DAssembler }))
);
const ActionSequence2DDirector = React.lazy(() =>
  import('./ActionSequence2DDirector').then(m => ({ default: m.ActionSequence2DDirector }))
);
const MultiAngleRigAssembler = React.lazy(() =>
  import('./MultiAngleRigAssembler').then(m => ({ default: m.MultiAngleRigAssembler }))
);
const ImageToSvgVectorizerTab = React.lazy(() =>
  import('./vectorizer/ImageToSvgVectorizerTab').then(m => ({ default: m.ImageToSvgVectorizerTab }))
);
const AIAntigravityDecomposerPanel = React.lazy(() =>
  import('./agent/AIAntigravityDecomposerPanel').then(m => ({ default: m.AIAntigravityDecomposerPanel }))
);
const DetailPartAssemblerTab = React.lazy(() =>
  import('./detail/DetailPartAssemblerTab').then(m => ({ default: m.DetailPartAssemblerTab }))
);

const TabLoader: React.FC = () => (
  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#64748b', fontSize: 12, gap: 8 }}>
    <Loader size={14} className="spin" /> Đang tải module...
  </div>
);

interface Studio2DWorkbenchModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const Studio2DWorkbenchModal: React.FC<Studio2DWorkbenchModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [activeTab, setActiveTab] = useState<
    'grid_slicer' | 'anim_slicer' | 'rig_assembler' | 'vectorizer' | 'detail_part' | 'character' | 'cropper' | 'prompt' | 'map' | 'director'
  >('grid_slicer');

  // Shared 2D Assembly & Map State
  const [characterAssembly, setCharacterAssembly] = useState<Character2DAssembly>(
    DEFAULT_SAMPLE_CHARACTERS_2D[0]
  );
  const [mapPreset, setMapPreset] = useState<Map2DPreset>(
    DEFAULT_SAMPLE_MAPS_2D[0]
  );

  // Transferred Sprite Sheet & Frames from Tab 1 / Tab 0
  const [transferredSpriteSheetUrl, setTransferredSpriteSheetUrl] = useState<string | null>(null);
  const [transferredCategoryId, setTransferredCategoryId] = useState<string | undefined>(undefined);
  const [transferredAnimationFrames, setTransferredAnimationFrames] = useState<string[] | null>(null);

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
              onClick={() => setActiveTab('anim_slicer')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: activeTab === 'anim_slicer' ? 700 : 600,
                border: 'none',
                background: activeTab === 'anim_slicer' ? 'linear-gradient(135deg, #0284c7, #a855f7)' : 'transparent',
                color: activeTab === 'anim_slicer' ? '#ffffff' : '#38bdf8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Clapperboard size={13} /> 1.2 🎬 Cắt & Ghép Hoạt Ảnh
            </button>

            <button
              onClick={() => setActiveTab('detail_part')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: activeTab === 'detail_part' ? 700 : 600,
                border: 'none',
                background: activeTab === 'detail_part' ? '#7c3aed' : 'transparent',
                color: activeTab === 'detail_part' ? '#ffffff' : '#a78bfa',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Sparkles size={13} /> 2.1 ⚙️ Lắp Chi Tiết
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
              <Layers size={13} /> 2.2 Lắp Ráp Nhân Vật
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
          <Suspense fallback={<TabLoader />}>
          {/* Tab 1: Cắt Lưới & Tách Nền Sprite Sheet (Giữ nguyên trạng thái không bị mất dữ liệu khi chuyển sang tab khác) */}
          <div style={{ display: activeTab === 'grid_slicer' ? 'flex' : 'none', width: '100%', height: '100%', flexDirection: 'column' }}>
            <AutoGridSlicer3DAssembler
              currentAssembly={characterAssembly}
              onApplyAssembly={setCharacterAssembly}
              onSwitchToAssemblyTab={() => setActiveTab('character')}
              onTransferToAnimationSlicer={(data) => {
                if (data.frames && data.frames.length > 0) {
                  setTransferredAnimationFrames(data.frames);
                } else if (data.spriteSheetUrl) {
                  setTransferredSpriteSheetUrl(data.spriteSheetUrl);
                }
                setActiveTab('anim_slicer');
              }}
              externalImageUrl={transferredSpriteSheetUrl}
              externalCategoryId={transferredCategoryId}
            />
          </div>

          {/* Tab 1.2: Animation Sequencer & Preview */}
          <div style={{ display: activeTab === 'anim_slicer' ? 'flex' : 'none', width: '100%', height: '100%', flexDirection: 'column' }}>
            <AnimationSlicerTab
              initialFrames={transferredAnimationFrames}
              externalSpriteSheetUrl={transferredSpriteSheetUrl}
            />
          </div>

          {activeTab === 'rig_assembler' && (
            <MultiAngleRigAssembler />
          )}

          {activeTab === 'vectorizer' && (
            <ImageToSvgVectorizerTab
              onTransferToRigAssembler={() => {
                setActiveTab('rig_assembler');
              }}
              onTransferToGridSlicer={(svgUrl) => {
                setTransferredSpriteSheetUrl(svgUrl);
                setActiveTab('grid_slicer');
              }}
            />
          )}

          {activeTab === 'detail_part' && (
            <DetailPartAssemblerTab />
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
          </Suspense>
        </div>
      </div>
    </div>
  );
};
