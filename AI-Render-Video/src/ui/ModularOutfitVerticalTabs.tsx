/**
 * ModularOutfitVerticalTabs.tsx
 *
 * Vertical-tab layout for character modular outfit assembly.
 * Features:
 *  - Vertical icon tabs with badges showing item count
 *  - Gender toggle (Nam / Nữ)
 *  - Shared vs per-actor face slider toggle
 *  - JSON character profile export / import
 *  - AI description field for cross-scene consistency
 */
import React, { useState, useEffect, useRef } from 'react';
import {
  Check,
  X,
  Save,
  Upload,
  Sliders,
  CheckCircle,
  UserCheck,
  UserPlus,
  Plus,
  Trash2,
  Sparkles,
  ToggleLeft,
  ToggleRight,
  Download,
} from 'lucide-react';
import {
  CHARACTER_CATEGORIES,
  fetchLiveCharacterCategories,
  CharacterCategory,
  filterByGender,
  CharacterPartItem,
  FaceSliderConfig,
  DEFAULT_FACE_SLIDERS,
  CharacterProfileJSON,
  buildCharacterProfile,
  downloadCharacterProfile,
} from './CharacterAssetRegistry';
import { MasterSceneConfig, ActorConfig, CharacterAssembly } from '../types/scene';
import { Live3DThumbnail } from './Live3DThumbnail';

// ─── Types ──────────────────────────────────────────────

interface CustomPreset {
  id: string;
  name: string;
  body: string;
  costume: string;
  face: string;
  gender: 'male' | 'female';
}

interface ModularOutfitVerticalTabsProps {
  scene: MasterSceneConfig;
  onUpdateScene: (updatedScene: MasterSceneConfig) => void;
  /** Callback for selected part changes — drives the 3D preview */
  baseBody: string;
  costume: string;
  face: string;
  hairstyle: string;
  onBaseBodyChange: (path: string) => void;
  onCostumeChange: (path: string) => void;
  onFaceChange: (path: string) => void;
  onHairstyleChange: (path: string) => void;
  /** Facial slider values */
  sliders: FaceSliderConfig;
  onSlidersChange: (sliders: FaceSliderConfig) => void;
}

// ─── Default Presets ────────────────────────────────────

const DEFAULT_PRESETS: CustomPreset[] = [
  {
    id: 'preset_amber_nectar',
    name: '🧑 Nam: Lý Tiên Sinh (Amber Nectar)',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/amber_nectar_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
  {
    id: 'preset_precision_strike',
    name: '👩 Nữ: Võ Khách (Precision Strike)',
    body: 'assets/characters/base_bodies/male/body_base_-_manekina.glb',
    costume: 'assets/characters/costumes/male/precision_strike_-_manekina.glb',
    face: '',
    gender: 'female',
  },
  {
    id: 'preset_scary_cat',
    name: '🐱 Nam: Hắc Miêu Hiệp Sĩ (Scary Cat)',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/scary_cat_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
  {
    id: 'preset_sleuth_verdict',
    name: '🕵️ Nam: Thám Tử (Sleuth Verdict)',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/sleuths_verdict_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
];

// ─── Component ──────────────────────────────────────────

export const ModularOutfitVerticalTabs: React.FC<ModularOutfitVerticalTabsProps> = ({
  scene,
  onUpdateScene,
  baseBody,
  costume,
  face,
  hairstyle,
  onBaseBodyChange,
  onCostumeChange,
  onFaceChange,
  onHairstyleChange,
  sliders,
  onSlidersChange,
}) => {
  // ─── Categories & Items State ─────────────────────────
  const [categories, setCategories] = useState<CharacterCategory[]>(CHARACTER_CATEGORIES);
  const [activeCategoryId, setActiveCategoryId] = useState('body');
  const [genderFilter, setGenderFilter] = useState<'male' | 'female'>('male');
  const [useSharedFaceSliders, setUseSharedFaceSliders] = useState(true);
  const [selectedActorId, setSelectedActorId] = useState(scene.actors[0]?.id || '');
  const [newActorName, setNewActorName] = useState('Lý Tiên Sinh');
  const [aiDescription, setAiDescription] = useState('');
  const [isAppliedSuccess, setIsAppliedSuccess] = useState(false);
  const [presetSavedToast, setPresetSavedToast] = useState('');
  const [jsonImportToast, setJsonImportToast] = useState('');

  const jsonImportRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    fetchLiveCharacterCategories().then(setCategories);
  }, []);

  // Presets
  const [customPresets, setCustomPresets] = useState<CustomPreset[]>(() => {
    try {
      const saved = localStorage.getItem('custom_character_presets');
      return saved ? JSON.parse(saved) : DEFAULT_PRESETS;
    } catch {
      return DEFAULT_PRESETS;
    }
  });

  // ─── Per-actor slider storage ───────────────────────
  const [perActorSliders, setPerActorSliders] = useState<Record<string, FaceSliderConfig>>({});

  const currentSliders = useSharedFaceSliders
    ? sliders
    : (perActorSliders[selectedActorId] || sliders);

  const handleSliderChange = (key: keyof FaceSliderConfig, value: number) => {
    if (useSharedFaceSliders) {
      onSlidersChange({ ...sliders, [key]: value });
    } else {
      const updated = { ...(perActorSliders[selectedActorId] || sliders), [key]: value };
      setPerActorSliders((prev) => ({ ...prev, [selectedActorId]: updated }));
      onSlidersChange(updated);
    }
  };

  const handleResetSliders = () => {
    onSlidersChange({ ...DEFAULT_FACE_SLIDERS });
    if (!useSharedFaceSliders) {
      setPerActorSliders((prev) => ({ ...prev, [selectedActorId]: { ...DEFAULT_FACE_SLIDERS } }));
    }
  };

  // ─── Preset Handlers ───────────────────────────────
  const handleSaveCustomPreset = () => {
    let name = '';
    try { name = prompt('Nhập tên cho mẫu phối đồ này:', newActorName) || ''; } catch {}
    if (!name) name = newActorName || 'Nhân Vật Mới';
    const newPreset: CustomPreset = {
      id: `preset_${Date.now()}`,
      name: `${baseBody.includes('manekina') ? '👩' : '🧑'} ${name}`,
      body: baseBody,
      costume,
      face,
      gender: baseBody.includes('manekina') ? 'female' : 'male',
    };
    const updated = [newPreset, ...customPresets];
    setCustomPresets(updated);
    try { localStorage.setItem('custom_character_presets', JSON.stringify(updated)); } catch {}
    setPresetSavedToast(`Đã lưu mẫu "${name}" thành công!`);
    setTimeout(() => setPresetSavedToast(''), 3500);
  };

  const handleDeletePreset = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const updated = customPresets.filter((p) => p.id !== id);
    setCustomPresets(updated);
    try { localStorage.setItem('custom_character_presets', JSON.stringify(updated)); } catch {}
  };

  // ─── JSON Export / Import with 3D Preview Snapshot ──
  const handleExportJSON = () => {
    // Attempt to grab canvas preview from 3D viewport
    let snapshotDataUrl = '';
    const canvas = document.querySelector('canvas');
    if (canvas) {
      try {
        snapshotDataUrl = canvas.toDataURL('image/png');
      } catch {}
    }

    const safeName = (newActorName || 'nhan_vat_lap_rap')
      .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF]/g, '_')
      .toLowerCase();

    const profile: CharacterProfileJSON = {
      ...buildCharacterProfile(
        newActorName,
        baseBody,
        costume,
        face,
        hairstyle,
        currentSliders,
        aiDescription
      ),
      preview_image: snapshotDataUrl,
    };

    // Download JSON
    const jsonBlob = new Blob([JSON.stringify(profile, null, 2)], { type: 'application/json' });
    const jsonUrl = URL.createObjectURL(jsonBlob);
    const aJson = document.createElement('a');
    aJson.href = jsonUrl;
    aJson.download = `${safeName}.json`;
    aJson.click();
    URL.revokeObjectURL(jsonUrl);

    // Download PNG preview
    if (snapshotDataUrl) {
      const aImg = document.createElement('a');
      aImg.href = snapshotDataUrl;
      aImg.download = `${safeName}.png`;
      aImg.click();
    }

    setJsonImportToast(`✅ Đã xuất "${safeName}.json" + "${safeName}.png" (cho thư mục assets/nhan_vat/_lap_rap/)!`);
    setTimeout(() => setJsonImportToast(''), 5000);
  };

  const handleImportJSON = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (evt) => {
      try {
        const profile: CharacterProfileJSON = JSON.parse(evt.target?.result as string);
        if (profile.version !== '1.0') throw new Error('Unsupported version');
        onBaseBodyChange(profile.base_body);
        onCostumeChange(profile.costume);
        onFaceChange(profile.face);
        onHairstyleChange(profile.hairstyle);
        onSlidersChange(profile.face_sliders);
        setNewActorName(profile.name);
        setAiDescription(profile.ai_description || '');
        setGenderFilter(profile.gender);
        setJsonImportToast(`✅ Đã nhập cấu hình "${profile.name}" thành công!`);
        setTimeout(() => setJsonImportToast(''), 4000);
      } catch (err) {
        setJsonImportToast('❌ File JSON không hợp lệ!');
        setTimeout(() => setJsonImportToast(''), 4000);
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  };

  // ─── Apply to Scene ────────────────────────────────
  const handleApplyToCurrentActor = () => {
    const targetActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
    if (!targetActor) return;
    const assembly: CharacterAssembly = {
      base_body: baseBody, costume, face, hairstyle: hairstyle || undefined,
    };
    targetActor.model = baseBody;
    targetActor.assembly = assembly;
    const updatedScene: MasterSceneConfig = {
      ...scene,
      actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)),
    };
    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  const handleAddNewActor = () => {
    const newId = `actor_${Math.random().toString(36).substring(2, 7)}`;
    const newActor: ActorConfig = {
      id: newId,
      name: newActorName || 'Võ Hiệp Mới',
      model: baseBody,
      assembly: {
        base_body: baseBody, costume, face, hairstyle: hairstyle || undefined,
      },
      spawn_point: [0.0, 0, 1.5],
      rotation_y: 0,
      tracks: { movement: [{ start: 0, end: 10, action: 'idle' }], speech: [] },
    };
    const updatedScene: MasterSceneConfig = { ...scene, actors: [...scene.actors, newActor] };
    onUpdateScene(updatedScene);
    setSelectedActorId(newId);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  // ─── Item selection mapping ────────────────────────
  const getSelectionForCategory = (catId: string): string => {
    switch (catId) {
      case 'body': return baseBody;
      case 'face': return face;
      case 'costume': return costume;
      case 'hairstyle': return hairstyle;
      default: return '';
    }
  };

  const handleSelectItem = (catId: string, path: string) => {
    const current = getSelectionForCategory(catId);
    const newVal = current === path ? '' : path;
    switch (catId) {
      case 'body': onBaseBodyChange(newVal); break;
      case 'face': onFaceChange(newVal); break;
      case 'costume': onCostumeChange(newVal); break;
      case 'hairstyle': onHairstyleChange(newVal); break;
    }
  };

  // ─── Filtered items for current tab ────────────────
  const activeCategory = categories.find((c) => c.id === activeCategoryId);
  const filteredItems = activeCategory
    ? filterByGender(activeCategory.items, genderFilter)
    : [];

  // ─── Slider definitions ────────────────────────────
  const SLIDER_DEFS: { key: keyof FaceSliderConfig; label: string; icon: string; min: number }[] = [
    { key: 'baseFaceOpacity', label: 'Độ Hiện Face Cũ', icon: '🎭', min: 0 },
    { key: 'eyebrowOpacity', label: 'Độ Đậm Lông Mày', icon: '👁️', min: 0 },
    { key: 'pupilOpacity', label: 'Độ Sáng Tròng Mắt', icon: '✨', min: 0 },
    { key: 'noseOpacity', label: 'Độ Nổi Mũi', icon: '👃', min: 0 },
    { key: 'mouthOpacity', label: 'Độ Rõ Miệng & Môi', icon: '👄', min: 0 },
    { key: 'skinSmoothness', label: 'Độ Mịn Da', icon: '🌸', min: 0.1 },
    { key: 'costumeOpacity', label: 'Độ Đậm Trang Phục', icon: '🥋', min: 0 },
  ];

  // ─── Render ────────────────────────────────────────
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 0, height: '100%', overflow: 'hidden' }}>
      {/* Top bar: Title + Gender Toggle + Presets + JSON buttons */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '8px 12px', gap: 8, flexShrink: 0,
        borderBottom: '1px solid rgba(255,255,255,0.06)',
        background: 'rgba(255,255,255,0.02)',
      }}>
        {/* Left: Title + Gender toggle */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: '#38bdf8' }}>
            Xưởng Lắp Ráp Nhân Vật
          </span>

          {/* Gender Toggle */}
          <div
            onClick={() => setGenderFilter(genderFilter === 'male' ? 'female' : 'male')}
            style={{
              display: 'flex', alignItems: 'center', gap: 6,
              padding: '3px 10px', borderRadius: 20, cursor: 'pointer',
              background: genderFilter === 'male'
                ? 'rgba(56, 189, 248, 0.15)'
                : 'rgba(236, 72, 153, 0.15)',
              border: `1px solid ${genderFilter === 'male' ? 'rgba(56, 189, 248, 0.4)' : 'rgba(236, 72, 153, 0.4)'}`,
              transition: 'all 0.2s',
            }}
          >
            {genderFilter === 'male'
              ? <ToggleLeft size={14} color="#38bdf8" />
              : <ToggleRight size={14} color="#ec4899" />
            }
            <span style={{
              fontSize: 11, fontWeight: 700,
              color: genderFilter === 'male' ? '#38bdf8' : '#ec4899',
            }}>
              {genderFilter === 'male' ? '♂ Nam' : '♀ Nữ'}
            </span>
          </div>

          {isAppliedSuccess && (
            <span style={{ fontSize: 11, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
              <CheckCircle size={13} /> Đã áp dụng!
            </span>
          )}
          {presetSavedToast && (
            <span style={{ fontSize: 11, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
              <CheckCircle size={13} /> {presetSavedToast}
            </span>
          )}
          {jsonImportToast && (
            <span style={{ fontSize: 11, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 4 }}>
              <CheckCircle size={13} /> {jsonImportToast}
            </span>
          )}
        </div>

        {/* Right: Preset + JSON buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button onClick={handleSaveCustomPreset} style={btnStyle('#10b981', '#34d399')}>
            <Save size={12} /> Lưu Mẫu
          </button>
          <button onClick={handleExportJSON} style={btnStyle('#0ea5e9', '#38bdf8')}>
            <Download size={12} /> Xuất JSON
          </button>
          <button onClick={() => jsonImportRef.current?.click()} style={btnStyle('#a855f7', '#c084fc')}>
            <Upload size={12} /> Nhập JSON
          </button>
          <input
            ref={jsonImportRef}
            type="file"
            accept=".json"
            style={{ display: 'none' }}
            onChange={handleImportJSON}
          />
        </div>
      </div>

      {/* Main area: Vertical Tabs + Content */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
        {/* Tabs Sidebar with Multi-Column Overflow Grid (Col 1 -> Col 2 -> Col 3) */}
        <div
          onWheel={(e) => {
            if (e.deltaY !== 0) {
              e.currentTarget.scrollLeft += e.deltaY;
            }
          }}
          style={{
            maxHeight: '100%',
            height: '100%',
            flexShrink: 0,
            display: 'flex',
            flexDirection: 'column',
            flexWrap: 'wrap',
            alignContent: 'flex-start',
            borderRight: '1px solid rgba(255, 255, 255, 0.08)',
            background: 'rgba(15, 23, 42, 0.95)',
            gap: 4,
            padding: '6px',
            overflowX: 'auto',
            overflowY: 'hidden',
            scrollBehavior: 'smooth',
          }}
        >
          {categories.map((cat) => {
            const isActive = activeCategoryId === cat.id;
            const count = filterByGender(cat.items, genderFilter).length;
            return (
              <button
                key={cat.id}
                onClick={() => setActiveCategoryId(cat.id)}
                title={cat.label}
                style={{
                  width: 112,
                  height: 42,
                  display: 'flex',
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: 6,
                  padding: '4px 6px',
                  border: 'none',
                  cursor: 'pointer',
                  borderRadius: 6,
                  borderLeft: isActive ? '3px solid #38bdf8' : '3px solid transparent',
                  background: isActive ? 'rgba(56, 189, 248, 0.18)' : 'rgba(255, 255, 255, 0.03)',
                  color: isActive ? '#38bdf8' : '#94a3b8',
                  transition: 'all 0.15s ease',
                  position: 'relative',
                  boxSizing: 'border-box',
                }}
              >
                <span style={{ fontSize: 16 }}>{cat.icon}</span>
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
                  <span
                    style={{
                      fontSize: 10,
                      fontWeight: 700,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      width: '100%',
                      textAlign: 'left',
                    }}
                  >
                    {cat.label}
                  </span>
                  <span style={{ fontSize: 8, color: isActive ? '#38bdf8' : '#64748b', fontWeight: 600 }}>
                    {count} món
                  </span>
                </div>
                {/* Badge */}
                <span
                  style={{
                    fontSize: 8,
                    fontWeight: 700,
                    padding: '1px 4px',
                    borderRadius: 8,
                    background: count > 0 ? (isActive ? '#38bdf8' : 'rgba(148, 163, 184, 0.25)') : 'rgba(255,255,255,0.06)',
                    color: count > 0 ? (isActive ? '#090d16' : '#cbd5e1') : '#475569',
                  }}
                >
                  {count}
                </span>
              </button>
            );
          })}

          {/* Sliders Tab Button */}
          <button
            onClick={() => setActiveCategoryId('_sliders')}
            title="Cấu Hình Slider Khuôn Mặt"
            style={{
              width: 112,
              height: 42,
              display: 'flex',
              flexDirection: 'row',
              alignItems: 'center',
              gap: 6,
              padding: '4px 6px',
              border: 'none',
              cursor: 'pointer',
              borderRadius: 6,
              borderLeft: activeCategoryId === '_sliders' ? '3px solid #fbbf24' : '3px solid transparent',
              background: activeCategoryId === '_sliders' ? 'rgba(251, 191, 36, 0.18)' : 'rgba(255, 255, 255, 0.03)',
              color: activeCategoryId === '_sliders' ? '#fbbf24' : '#94a3b8',
              transition: 'all 0.15s ease',
              boxSizing: 'border-box',
            }}
          >
            <Sliders size={16} />
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
              <span style={{ fontSize: 10, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                Thanh Trượt
              </span>
              <span style={{ fontSize: 8, color: activeCategoryId === '_sliders' ? '#fbbf24' : '#64748b', fontWeight: 600 }}>
                Chi tiết mặt
              </span>
            </div>
          </button>
        </div>

        {/* Content Area */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
          {/* Content: Items Grid or Sliders */}
          <div style={{ flex: 1, overflowY: 'auto', padding: 12 }}>
            {activeCategoryId === '_sliders' ? (
              <SlidersPanel
                sliders={currentSliders}
                onSliderChange={handleSliderChange}
                onReset={handleResetSliders}
                useShared={useSharedFaceSliders}
                onToggleShared={() => {
                  const next = !useSharedFaceSliders;
                  setUseSharedFaceSliders(next);
                  if (!next && !perActorSliders[selectedActorId]) {
                    setPerActorSliders((prev) => ({ ...prev, [selectedActorId]: { ...sliders } }));
                  } else if (next && perActorSliders[selectedActorId]) {
                    onSlidersChange(perActorSliders[selectedActorId]);
                  }
                }}
                sliderDefs={SLIDER_DEFS}
                aiDescription={aiDescription}
                onAiDescriptionChange={setAiDescription}
              />
            ) : (
              <ItemsGrid
                items={filteredItems}
                categoryId={activeCategoryId}
                selectedPath={getSelectionForCategory(activeCategoryId)}
                onSelect={(path) => handleSelectItem(activeCategoryId, path)}
                categoryLabel={activeCategory?.label || ''}
                categoryIcon={activeCategory?.icon || '🧍'}
              />
            )}
          </div>

          {/* Presets Row */}
          <PresetsBar
            presets={customPresets}
            costume={costume}
            onSelect={(p) => {
              onBaseBodyChange(p.body);
              onCostumeChange(p.costume);
              onFaceChange(p.face);
              setNewActorName(p.name.replace(/^[^\s]+\s+/, ''));
              setGenderFilter(p.gender);
            }}
            onDelete={handleDeletePreset}
          />

          {/* Action Bar */}
          <ActionBar
            scene={scene}
            selectedActorId={selectedActorId}
            onSelectActorId={setSelectedActorId}
            newActorName={newActorName}
            onNewActorNameChange={setNewActorName}
            onApply={handleApplyToCurrentActor}
            onAddNew={handleAddNewActor}
          />
        </div>
      </div>
    </div>
  );
};

// ─── Sub-Components ─────────────────────────────────────

/** Items Grid for a category tab */
function ItemsGrid({ items, categoryId, selectedPath, onSelect, categoryLabel, categoryIcon = '🧍' }: {
  items: CharacterPartItem[];
  categoryId: string;
  selectedPath: string;
  onSelect: (path: string) => void;
  categoryLabel: string;
  categoryIcon?: string;
}) {
  if (items.length === 0) {
    return (
      <div style={{
        display: 'flex', flexDirection: 'column',
        alignItems: 'center', justifyContent: 'center',
        height: '100%', gap: 8, color: '#475569',
      }}>
        <span style={{ fontSize: 36, opacity: 0.4 }}>{categoryIcon}</span>
        <span style={{ fontSize: 12, fontWeight: 600 }}>
          Chưa có tài nguyên "{categoryLabel}"
        </span>
        <span style={{ fontSize: 11, color: '#334155' }}>
          Thả file .glb vào thư mục tương ứng trong assets/characters/
        </span>
      </div>
    );
  }

  return (
    <div>
      <div style={{ marginBottom: 10, fontSize: 12, fontWeight: 700, color: '#e2e8f0' }}>
        {categoryLabel} ({items.length} tài nguyên)
      </div>

      {/* "None" card for categories that support deselection */}
      {(categoryId === 'face' || categoryId === 'costume' || categoryId === 'hairstyle') && (
        <div style={{ marginBottom: 10 }}>
          <div
            onClick={() => onSelect('')}
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 6,
              padding: '6px 14px', borderRadius: 6, cursor: 'pointer',
              background: selectedPath === '' ? 'rgba(239, 68, 68, 0.15)' : 'rgba(255,255,255,0.03)',
              border: selectedPath === '' ? '2px solid #f87171' : '1px solid rgba(255,255,255,0.08)',
              color: selectedPath === '' ? '#f87171' : '#94a3b8',
              fontSize: 11, fontWeight: 600, transition: 'all 0.15s',
            }}
          >
            <X size={14} /> Không Sử Dụng
          </div>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: 10 }}>
        {items.map((item) => {
          const isSelected = selectedPath === item.path;
          return (
            <div
              key={item.id}
              onClick={() => onSelect(item.path)}
              title={isSelected ? 'Nhấn để bỏ chọn' : 'Nhấn để chọn'}
              style={{
                background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
                border: isSelected ? '2px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                borderRadius: 8, padding: 8, cursor: 'pointer',
                display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
                position: 'relative', transition: 'all 0.15s',
                boxShadow: isSelected ? '0 0 14px rgba(56, 189, 248, 0.3)' : 'none',
              }}
            >
              {isSelected && (
                <div style={{
                  position: 'absolute', top: 4, right: 4,
                  background: '#38bdf8', color: '#000',
                  borderRadius: '50%', width: 16, height: 16,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  zIndex: 2,
                }}>
                  <Check size={11} strokeWidth={3} />
                </div>
              )}
              {/* Live 3D Thumbnail / 2D Companion Preview */}
              <Live3DThumbnail
                assetPath={item.path}
                previewUrl={item.preview}
                altText={item.name}
                fallbackIcon={categoryIcon}
                format={item.format || 'GLB'}
                height={90}
              />
              <span style={{
                fontSize: 11, fontWeight: 600, textAlign: 'center',
                color: isSelected ? '#38bdf8' : '#e2e8f0',
                whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', width: '100%',
              }}>
                {item.name}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/** Sliders Panel with shared/per-actor toggle + AI description */
function SlidersPanel({ sliders, onSliderChange, onReset, useShared, onToggleShared, sliderDefs, aiDescription, onAiDescriptionChange }: {
  sliders: FaceSliderConfig;
  onSliderChange: (key: keyof FaceSliderConfig, value: number) => void;
  onReset: () => void;
  useShared: boolean;
  onToggleShared: () => void;
  sliderDefs: { key: keyof FaceSliderConfig; label: string; icon: string; min: number }[];
  aiDescription: string;
  onAiDescriptionChange: (desc: string) => void;
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {/* Header: Toggle shared + Reset */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '8px 12px', borderRadius: 8,
        background: 'rgba(251, 191, 36, 0.06)',
        border: '1px solid rgba(251, 191, 36, 0.2)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Sliders size={14} color="#fbbf24" />
          <span style={{ fontSize: 13, fontWeight: 700, color: '#fbbf24' }}>
            Cấu Hình Chi Tiết Khuôn Mặt & Độ Mịn Da
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          {/* Shared/Per-actor toggle */}
          <div
            onClick={onToggleShared}
            style={{
              display: 'flex', alignItems: 'center', gap: 5,
              padding: '3px 10px', borderRadius: 16, cursor: 'pointer',
              background: useShared ? 'rgba(56, 189, 248, 0.15)' : 'rgba(168, 85, 247, 0.15)',
              border: `1px solid ${useShared ? 'rgba(56, 189, 248, 0.3)' : 'rgba(168, 85, 247, 0.3)'}`,
              transition: 'all 0.2s',
            }}
          >
            {useShared
              ? <ToggleRight size={14} color="#38bdf8" />
              : <ToggleLeft size={14} color="#c084fc" />
            }
            <span style={{
              fontSize: 10, fontWeight: 700,
              color: useShared ? '#38bdf8' : '#c084fc',
            }}>
              {useShared ? '🔗 Dùng Chung Tất Cả' : '🔓 Riêng Từng Nhân Vật'}
            </span>
          </div>

          <button onClick={onReset} style={{
            fontSize: 10, color: '#94a3b8', background: 'transparent',
            border: 'none', cursor: 'pointer', textDecoration: 'underline',
          }}>
            Khôi phục mặc định
          </button>
        </div>
      </div>

      {/* Slider Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 8 }}>
        {sliderDefs.map((def) => {
          const val = sliders[def.key];
          return (
            <div key={def.key} style={{
              display: 'flex', flexDirection: 'column', gap: 4,
              background: 'rgba(0,0,0,0.25)', padding: '8px 10px', borderRadius: 6,
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11 }}>
                <span style={{ color: '#cbd5e1' }}>{def.icon} {def.label}:</span>
                <span style={{ fontWeight: 700, color: val <= 0.05 ? '#f87171' : '#38bdf8' }}>
                  {Math.round(val * 100)}%{val <= 0.05 ? ' (Ẩn)' : ''}
                </span>
              </div>
              <input
                type="range"
                min={String(def.min)}
                max="1"
                step="0.05"
                value={val}
                onChange={(e) => onSliderChange(def.key, parseFloat(e.target.value))}
                style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
              />
            </div>
          );
        })}
      </div>

      {/* AI Description Field */}
      <div style={{
        background: 'rgba(168, 85, 247, 0.06)', padding: 12, borderRadius: 8,
        border: '1px solid rgba(168, 85, 247, 0.2)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 8 }}>
          <Sparkles size={14} color="#c084fc" />
          <span style={{ fontSize: 12, fontWeight: 700, color: '#c084fc' }}>
            Mô Tả Nhận Diện AI (Đồng Nhất Xuyên Cảnh)
          </span>
        </div>
        <textarea
          value={aiDescription}
          onChange={(e) => onAiDescriptionChange(e.target.value)}
          placeholder="Ví dụ: Nam giới, tóc dài đen buông vai, áo giáp vàng hổ phách, khuôn mặt trẻ trung, mắt sắc lẹm..."
          rows={3}
          style={{
            width: '100%', padding: '8px 12px', borderRadius: 6,
            background: '#0f172a', border: '1px solid rgba(168, 85, 247, 0.3)',
            color: '#e2e8f0', fontSize: 12, resize: 'vertical',
            outline: 'none', fontFamily: 'Inter, system-ui, sans-serif',
          }}
        />
        <div style={{ marginTop: 6, fontSize: 10, color: '#64748b' }}>
          💡 Mô tả này sẽ được lưu vào file JSON cấu hình và dùng làm prompt cho AI khi tạo cảnh mới
          — giúp nhân vật giữ ngoại hình nhất quán dù qua nhiều scene khác nhau.
        </div>
      </div>
    </div>
  );
}

/** Presets bar at bottom */
function PresetsBar({ presets, costume, onSelect, onDelete }: {
  presets: CustomPreset[];
  costume: string;
  onSelect: (p: CustomPreset) => void;
  onDelete: (id: string, e: React.MouseEvent) => void;
}) {
  return (
    <div style={{
      flexShrink: 0, padding: '6px 12px',
      borderTop: '1px solid rgba(255,255,255,0.06)',
      background: 'rgba(56, 189, 248, 0.03)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
        <Sparkles size={12} color="#38bdf8" />
        <span style={{ fontSize: 10, fontWeight: 600, color: '#38bdf8' }}>Preset Nhanh:</span>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
        {presets.map((p) => (
          <div
            key={p.id}
            onClick={() => onSelect(p)}
            style={{
              display: 'flex', alignItems: 'center', gap: 4,
              padding: '3px 8px', fontSize: 10, fontWeight: 600,
              borderRadius: 5, cursor: 'pointer', transition: 'all 0.15s',
              border: costume === p.costume ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
              background: costume === p.costume ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
              color: costume === p.costume ? '#38bdf8' : '#cbd5e1',
            }}
          >
            <span>{p.name}</span>
            {!['preset_amber_nectar', 'preset_precision_strike', 'preset_scary_cat', 'preset_sleuth_verdict'].includes(p.id) && (
              <span
                onClick={(e) => onDelete(p.id, e)}
                title="Xóa mẫu"
                style={{ display: 'flex', alignItems: 'center' }}
              >
                <Trash2 size={10} color="#f87171" />
              </span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

/** Action bar: actor name, select actor, apply, add new */
function ActionBar({ scene, selectedActorId, onSelectActorId, newActorName, onNewActorNameChange, onApply, onAddNew }: {
  scene: MasterSceneConfig;
  selectedActorId: string;
  onSelectActorId: (id: string) => void;
  newActorName: string;
  onNewActorNameChange: (name: string) => void;
  onApply: () => void;
  onAddNew: () => void;
}) {
  return (
    <div style={{
      flexShrink: 0, padding: '8px 12px',
      borderTop: '1px solid rgba(255,255,255,0.06)',
      background: 'rgba(255,255,255,0.02)',
      display: 'flex', flexDirection: 'column', gap: 6,
    }}>
      {/* Name input */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <label style={{ fontWeight: 700, color: '#38bdf8', fontSize: 11, minWidth: 80, display: 'flex', alignItems: 'center', gap: 4 }}>
          🏷️ Tên:
        </label>
        <input
          type="text"
          value={newActorName}
          onChange={(e) => onNewActorNameChange(e.target.value)}
          placeholder="Tên nhân vật..."
          style={{
            flex: 1, padding: '6px 10px', borderRadius: 6,
            background: '#0f172a', border: '1px solid rgba(56, 189, 248, 0.4)',
            color: '#fff', fontSize: 12, fontWeight: 600, outline: 'none',
          }}
        />
      </div>

      {/* Actor select + buttons */}
      <div style={{ display: 'flex', gap: 6 }}>
        <select
          value={selectedActorId}
          onChange={(e) => onSelectActorId(e.target.value)}
          style={{
            padding: '6px 10px', borderRadius: 6,
            background: '#1e293b', border: '1px solid rgba(255,255,255,0.2)',
            color: '#fff', outline: 'none', fontSize: 11,
          }}
        >
          {scene.actors.map((a) => (
            <option key={a.id} value={a.id}>{a.name || a.id}</option>
          ))}
        </select>

        <button onClick={onApply} style={{
          flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 5,
          padding: '7px 12px', borderRadius: 6,
          background: 'linear-gradient(135deg, #0284c7, #0369a1)',
          color: '#fff', fontWeight: 700, fontSize: 11,
          border: 'none', cursor: 'pointer',
          boxShadow: '0 2px 6px rgba(2, 132, 199, 0.3)',
        }}>
          <UserCheck size={13} /> Gán Cho Nhân Vật
        </button>

        <button onClick={onAddNew} style={{
          display: 'flex', alignItems: 'center', gap: 5,
          padding: '7px 14px', borderRadius: 6,
          background: 'rgba(34, 197, 94, 0.2)',
          border: '1px solid rgba(34, 197, 94, 0.4)',
          color: '#4ade80', fontWeight: 700, fontSize: 11,
          cursor: 'pointer',
        }}>
          <UserPlus size={13} /> Thêm Mới
        </button>
      </div>
    </div>
  );
}

// ─── Helpers ─────────────────────────────────────────────

function btnStyle(borderColor: string, textColor: string): React.CSSProperties {
  return {
    display: 'flex', alignItems: 'center', gap: 4,
    padding: '3px 8px', fontSize: 10, fontWeight: 600,
    borderRadius: 5, cursor: 'pointer',
    background: `rgba(${hexToRgb(borderColor)}, 0.15)`,
    border: `1px solid rgba(${hexToRgb(borderColor)}, 0.4)`,
    color: textColor,
    transition: 'all 0.15s',
  };
}

function hexToRgb(hex: string): string {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!result) return '255, 255, 255';
  return `${parseInt(result[1], 16)}, ${parseInt(result[2], 16)}, ${parseInt(result[3], 16)}`;
}
