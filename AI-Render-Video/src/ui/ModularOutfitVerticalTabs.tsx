/**
 * ModularOutfitVerticalTabs.tsx
 *
 * Vertical-tab layout for character modular outfit assembly.
 * Modularized Architecture (< 450 lines):
 *  - PresetsBar: Subcomponent for pinned horizontal presets strip
 *  - CharacterHUDCard: Subcomponent for character status badge
 *  - CharacterProfileModal: Subcomponent for dynamic JSON-driven profile modal
 */
import React, { useState, useEffect, useRef } from 'react';
import {
  Check,
  Save,
  Upload,
  Sliders,
  CheckCircle,
  UserCheck,
  UserPlus,
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
  CharacterSkillItem,
  buildCharacterProfile,
} from './CharacterAssetRegistry';
import { MasterSceneConfig, ActorConfig, CharacterAssembly, CharacterProfileData } from '../types/scene';
import { Live3DThumbnail } from './Live3DThumbnail';
import { PresetsBar, CustomPreset } from './character/PresetsBar';
import { CharacterHUDCard } from './character/CharacterHUDCard';
import { CharacterProfileModal, CustomAttributeItem } from './character/CharacterProfileModal';

interface ModularOutfitVerticalTabsProps {
  scene: MasterSceneConfig;
  onUpdateScene: (updatedScene: MasterSceneConfig) => void;
  baseBody: string;
  costume: string;
  face: string;
  hairstyle: string;
  onBaseBodyChange: (path: string) => void;
  onCostumeChange: (path: string) => void;
  onFaceChange: (path: string) => void;
  onHairstyleChange: (path: string) => void;
  sliders: FaceSliderConfig;
  onSlidersChange: (sliders: FaceSliderConfig) => void;
}

const DEFAULT_PRESETS: CustomPreset[] = [
  {
    id: 'preset_amber_nectar',
    name: '🧑 Lý Tiên Sinh',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/amber_nectar_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
  {
    id: 'preset_precision_strike',
    name: '👩 Nữ Võ Khách',
    body: 'assets/characters/base_bodies/male/body_base_-_manekina.glb',
    costume: 'assets/characters/costumes/male/precision_strike_-_manekina.glb',
    face: '',
    gender: 'female',
  },
  {
    id: 'preset_scary_cat',
    name: '🐱 Hắc Miêu Hiệp Sĩ',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/scary_cat_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
  {
    id: 'preset_sleuth_verdict',
    name: '🕵️ Thám Tử',
    body: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    costume: 'assets/characters/costumes/man/sleuths_verdict_-_manekin.glb',
    face: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    gender: 'male',
  },
];

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
  const [categories, setCategories] = useState<CharacterCategory[]>(CHARACTER_CATEGORIES);
  const [activeCategoryId, setActiveCategoryId] = useState('than_co_ban');
  const [genderFilter, setGenderFilter] = useState<'male' | 'female'>('male');
  const [useSharedFaceSliders, setUseSharedFaceSliders] = useState(true);
  const [selectedActorId, setSelectedActorId] = useState(scene.actors[0]?.id || '');

  // Character Profile State
  const currentActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
  const [charName, setCharName] = useState(currentActor?.name || 'Lý Tiên Sinh');
  const [charAge, setCharAge] = useState<number | ''>(currentActor?.profile?.age !== undefined ? currentActor.profile.age : 22);
  const [charGender, setCharGender] = useState<'male' | 'female' | 'unisex'>(currentActor?.profile?.gender || 'male');
  const [charHeightCm, setCharHeightCm] = useState<number>(currentActor?.profile?.height_cm || 178);
  const [charEducation, setCharEducation] = useState<string>(currentActor?.profile?.education_level || 'Đại Học');
  const [charOccupation, setCharOccupation] = useState<string>(currentActor?.profile?.occupation || 'Kiếm Khách');
  const [charFaction, setCharFaction] = useState<string>(currentActor?.profile?.faction || 'Thục Sơn Kiếm Tông');
  const [charPersonality, setCharPersonality] = useState<string>(currentActor?.profile?.personality || 'Điềm đạm, cương trực, trọng tình cảm');
  const [charVoiceStyle, setCharVoiceStyle] = useState<string>(currentActor?.profile?.voice_style || 'Trầm ấm, uy nghiêm');
  const [charPowerLevel, setCharPowerLevel] = useState<number>(currentActor?.profile?.power_level || 100);
  const [charElement, setCharElement] = useState<string>(currentActor?.profile?.element || 'Kiếm Khí');
  const [charBiography, setCharBiography] = useState<string>(currentActor?.profile?.biography || 'Hiệp khách phiêu bạt giang hồ...');
  const [charSkills, setCharSkills] = useState<CharacterSkillItem[]>(currentActor?.profile?.skills || [
    { name: 'Vạn Kiếm Quy Tông', level: 5, type: 'Kiếm Pháp', description: 'Ngự kiếm phi hành' },
  ]);
  const [customAttributes, setCustomAttributes] = useState<CustomAttributeItem[]>(() => {
    if (currentActor?.profile?.custom_attributes) {
      return Object.entries(currentActor.profile.custom_attributes).map(([key, value]) => ({ key, value: String(value) }));
    }
    return [{ key: 'Vũ Khí', value: 'Thanh Vân Kiếm' }];
  });

  // Modal State
  const [showProfileModal, setShowProfileModal] = useState<boolean>(false);
  const [profileModalMode, setProfileModalMode] = useState<'create' | 'edit'>('create');
  const [validationError, setValidationError] = useState<string | null>(null);

  const [isAppliedSuccess, setIsAppliedSuccess] = useState(false);
  const [presetSavedToast, setPresetSavedToast] = useState('');
  const [jsonImportToast, setJsonImportToast] = useState('');
  const jsonImportRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    fetchLiveCharacterCategories().then((cats) => {
      setCategories(cats);
      if (cats.length > 0 && !cats.some((c) => c.id === activeCategoryId)) {
        setActiveCategoryId(cats[0].id);
      }
    });
  }, []);

  useEffect(() => {
    const actor = scene.actors.find((a) => a.id === selectedActorId);
    if (actor) {
      setCharName(actor.name || 'Nhân Vật');
      if (actor.profile) {
        setCharAge(actor.profile.age !== undefined ? actor.profile.age : 22);
        setCharGender(actor.profile.gender || 'male');
        setCharHeightCm(actor.profile.height_cm || 178);
        setCharEducation(actor.profile.education_level || 'Đại Học');
        setCharOccupation(actor.profile.occupation || 'Kiếm Khách');
        setCharFaction(actor.profile.faction || 'Thục Sơn Kiếm Tông');
        setCharPersonality(actor.profile.personality || 'Điềm đạm, cương trực');
        setCharVoiceStyle(actor.profile.voice_style || 'Trầm ấm');
        setCharPowerLevel(actor.profile.power_level || 100);
        setCharElement(actor.profile.element || 'Kiếm Khí');
        setCharBiography(actor.profile.biography || '');
        setCharSkills(actor.profile.skills || []);
        if (actor.profile.custom_attributes) {
          setCustomAttributes(
            Object.entries(actor.profile.custom_attributes).map(([key, value]) => ({ key, value: String(value) }))
          );
        }
      }
    }
  }, [selectedActorId, scene.actors]);

  const [customPresets, setCustomPresets] = useState<CustomPreset[]>(() => {
    try {
      const saved = localStorage.getItem('custom_character_presets');
      return saved ? JSON.parse(saved) : DEFAULT_PRESETS;
    } catch {
      return DEFAULT_PRESETS;
    }
  });

  const [perActorSliders, setPerActorSliders] = useState<Record<string, FaceSliderConfig>>({});
  const currentSliders = useSharedFaceSliders ? sliders : (perActorSliders[selectedActorId] || sliders);

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

  const handleSaveCustomPreset = () => {
    let name = '';
    try { name = prompt('Nhập tên cho mẫu phối đồ này:', charName) || ''; } catch {}
    if (!name) name = charName || 'Nhân Vật Mới';
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

  const handleExportJSON = () => {
    let snapshotDataUrl = '';
    const canvas = document.querySelector('canvas');
    if (canvas) {
      try { snapshotDataUrl = canvas.toDataURL('image/png'); } catch {}
    }
    const safeName = (charName || 'nhan_vat_lap_rap').replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF]/g, '_').toLowerCase();
    const customAttrMap = customAttributes.reduce((acc, curr) => {
      if (curr.key.trim()) acc[curr.key.trim()] = curr.value;
      return acc;
    }, {} as Record<string, any>);

    const profile: CharacterProfileJSON = {
      ...buildCharacterProfile(charName, baseBody, costume, face, hairstyle, currentSliders, charBiography, {
        age: typeof charAge === 'number' ? charAge : 20,
        gender: charGender,
        height_cm: charHeightCm,
        education_level: charEducation,
        occupation: charOccupation,
        faction: charFaction,
        personality: charPersonality,
        biography: charBiography,
        voice_style: charVoiceStyle,
        power_level: charPowerLevel,
        element: charElement,
        skills: charSkills,
        custom_attributes: customAttrMap,
      }),
      preview_image: snapshotDataUrl,
    };

    const jsonBlob = new Blob([JSON.stringify(profile, null, 2)], { type: 'application/json' });
    const jsonUrl = URL.createObjectURL(jsonBlob);
    const aJson = document.createElement('a');
    aJson.href = jsonUrl;
    aJson.download = `${safeName}.json`;
    aJson.click();
    URL.revokeObjectURL(jsonUrl);

    if (snapshotDataUrl) {
      const aImg = document.createElement('a');
      aImg.href = snapshotDataUrl;
      aImg.download = `${safeName}.png`;
      aImg.click();
    }
    setJsonImportToast(`✅ Đã xuất "${safeName}.json" + "${safeName}.png"!`);
    setTimeout(() => setJsonImportToast(''), 5000);
  };

  const handleImportJSON = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (evt) => {
      try {
        const profile: CharacterProfileJSON = JSON.parse(evt.target?.result as string);
        if (profile.base_body) onBaseBodyChange(profile.base_body);
        if (profile.costume) onCostumeChange(profile.costume);
        if (profile.face) onFaceChange(profile.face);
        if (profile.hairstyle) onHairstyleChange(profile.hairstyle);
        if (profile.face_sliders) onSlidersChange(profile.face_sliders);
        if (profile.name) setCharName(profile.name);
        if (profile.age !== undefined) setCharAge(profile.age);
        if (profile.height_cm !== undefined) setCharHeightCm(profile.height_cm);
        if (profile.education_level) setCharEducation(profile.education_level);
        if (profile.occupation) setCharOccupation(profile.occupation);
        if (profile.faction) setCharFaction(profile.faction);
        if (profile.personality) setCharPersonality(profile.personality);
        if (profile.voice_style) setCharVoiceStyle(profile.voice_style);
        if (profile.power_level !== undefined) setCharPowerLevel(profile.power_level);
        if (profile.element) setCharElement(profile.element);
        if (profile.biography || profile.ai_description) setCharBiography(profile.biography || profile.ai_description);
        if (profile.skills) setCharSkills(profile.skills);
        if (profile.custom_attributes) {
          setCustomAttributes(
            Object.entries(profile.custom_attributes).map(([key, value]) => ({ key, value: String(value) }))
          );
        }
        if (profile.gender && profile.gender !== 'unisex') setGenderFilter(profile.gender);
        setJsonImportToast(`✅ Đã nhập hồ sơ "${profile.name}" thành công!`);
        setTimeout(() => setJsonImportToast(''), 4000);
      } catch (err) {
        setJsonImportToast('❌ File JSON không hợp lệ!');
        setTimeout(() => setJsonImportToast(''), 4000);
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  };

  const handleApplyToCurrentActor = () => {
    const targetActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
    if (!targetActor) return;
    const customAttrMap = customAttributes.reduce((acc, curr) => {
      if (curr.key.trim()) acc[curr.key.trim()] = curr.value;
      return acc;
    }, {} as Record<string, any>);

    const profileData: CharacterProfileData = {
      age: typeof charAge === 'number' ? charAge : 20,
      gender: charGender,
      height_cm: charHeightCm,
      education_level: charEducation,
      occupation: charOccupation,
      faction: charFaction,
      personality: charPersonality,
      biography: charBiography,
      voice_style: charVoiceStyle,
      power_level: charPowerLevel,
      element: charElement,
      skills: charSkills,
      custom_attributes: customAttrMap,
    };

    targetActor.name = charName;
    targetActor.model = baseBody;
    targetActor.assembly = { base_body: baseBody, costume, face, hairstyle: hairstyle || undefined };
    targetActor.profile = profileData;

    const updatedScene: MasterSceneConfig = {
      ...scene,
      actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)),
    };
    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  const handleSaveProfileModal = () => {
    if (!charName.trim()) { setValidationError('Vui lòng nhập Tên Nhân Vật!'); return; }
    if (charAge === '' || Number(charAge) <= 0) { setValidationError('Vui lòng nhập Số Tuổi hợp lệ (> 0)!'); return; }

    const customAttrMap = customAttributes.reduce((acc, curr) => {
      if (curr.key.trim()) acc[curr.key.trim()] = curr.value;
      return acc;
    }, {} as Record<string, any>);

    const profileData: CharacterProfileData = {
      age: Number(charAge),
      gender: charGender,
      height_cm: charHeightCm,
      education_level: charEducation,
      occupation: charOccupation,
      faction: charFaction,
      personality: charPersonality,
      biography: charBiography,
      voice_style: charVoiceStyle,
      power_level: charPowerLevel,
      element: charElement,
      skills: charSkills,
      custom_attributes: customAttrMap,
    };

    if (profileModalMode === 'create') {
      const newId = `actor_${Math.random().toString(36).substring(2, 7)}`;
      const newActor: ActorConfig = {
        id: newId,
        name: charName.trim(),
        model: baseBody,
        assembly: { base_body: baseBody, costume, face, hairstyle: hairstyle || undefined },
        profile: profileData,
        spawn_point: [0.0, 0, 1.5],
        rotation_y: 0,
        tracks: { movement: [{ start: 0, end: 10, action: 'idle' }], speech: [] },
      };
      onUpdateScene({ ...scene, actors: [...scene.actors, newActor] });
      setSelectedActorId(newId);
    } else {
      const targetActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
      if (targetActor) {
        targetActor.name = charName.trim();
        targetActor.profile = profileData;
        onUpdateScene({ ...scene, actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)) });
      }
    }
    setShowProfileModal(false);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  const getSelectionForCategory = (catId: string): string => {
    switch (catId) {
      case 'than_co_ban': return baseBody;
      case 'khuon_mat': return face;
      case 'trang_phuc': return costume;
      case 'kieu_toc': return hairstyle;
      default: return '';
    }
  };

  const handleSelectItem = (catId: string, path: string) => {
    const current = getSelectionForCategory(catId);
    const newVal = current === path ? '' : path;
    switch (catId) {
      case 'than_co_ban': onBaseBodyChange(newVal); break;
      case 'khuon_mat': onFaceChange(newVal); break;
      case 'trang_phuc': onCostumeChange(newVal); break;
      case 'kieu_toc': onHairstyleChange(newVal); break;
    }
  };

  const activeCategory = categories.find((c) => c.id === activeCategoryId);
  const filteredItems = activeCategory ? filterByGender(activeCategory.items, genderFilter) : [];

  const SLIDER_DEFS: { key: keyof FaceSliderConfig; label: string; icon: string; min: number }[] = [
    { key: 'baseFaceOpacity', label: 'Độ Hiện Face Cũ', icon: '🎭', min: 0 },
    { key: 'eyebrowOpacity', label: 'Độ Đậm Lông Mày', icon: '👁️', min: 0 },
    { key: 'pupilOpacity', label: 'Độ Sáng Tròng Mắt', icon: '✨', min: 0 },
    { key: 'noseOpacity', label: 'Độ Nổi Mũi', icon: '👃', min: 0 },
    { key: 'mouthOpacity', label: 'Độ Rõ Miệng & Môi', icon: '👄', min: 0 },
    { key: 'skinSmoothness', label: 'Độ Mịn Da', icon: '🌸', min: 0.1 },
    { key: 'costumeOpacity', label: 'Độ Đậm Trang Phục', icon: '🥋', min: 0 },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', position: 'relative' }}>
      {/* ─── TOP BAR ────────────────────────────────────────────────────────── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 12px', gap: 8, flexShrink: 0, borderBottom: '1px solid rgba(255,255,255,0.06)', background: 'rgba(255,255,255,0.02)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: '#38bdf8' }}>Xưởng Lắp Ráp & Hồ Sơ Nhân Vật</span>
          <div
            onClick={() => { const nextG = genderFilter === 'male' ? 'female' : 'male'; setGenderFilter(nextG); setCharGender(nextG); }}
            style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '3px 10px', borderRadius: 20, cursor: 'pointer', background: genderFilter === 'male' ? 'rgba(56, 189, 248, 0.15)' : 'rgba(236, 72, 153, 0.15)', border: `1px solid ${genderFilter === 'male' ? 'rgba(56, 189, 248, 0.4)' : 'rgba(236, 72, 153, 0.4)'}` }}
          >
            {genderFilter === 'male' ? <ToggleLeft size={14} color="#38bdf8" /> : <ToggleRight size={14} color="#ec4899" />}
            <span style={{ fontSize: 11, fontWeight: 700, color: genderFilter === 'male' ? '#38bdf8' : '#ec4899' }}>{genderFilter === 'male' ? '♂ Nam' : '♀ Nữ'}</span>
          </div>
          {isAppliedSuccess && <span style={{ fontSize: 11, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}><CheckCircle size={13} /> Đã áp dụng!</span>}
          {presetSavedToast && <span style={{ fontSize: 11, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}><CheckCircle size={13} /> {presetSavedToast}</span>}
          {jsonImportToast && <span style={{ fontSize: 11, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 4 }}><CheckCircle size={13} /> {jsonImportToast}</span>}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button onClick={handleSaveCustomPreset} style={btnStyle('#10b981', '#34d399')}><Save size={12} /> Lưu Mẫu</button>
          <button onClick={handleExportJSON} style={btnStyle('#0ea5e9', '#38bdf8')}><Download size={12} /> Xuất JSON</button>
          <button onClick={() => jsonImportRef.current?.click()} style={btnStyle('#a855f7', '#c084fc')}><Upload size={12} /> Nhập JSON</button>
          <input ref={jsonImportRef} type="file" accept=".json" style={{ display: 'none' }} onChange={handleImportJSON} />
        </div>
      </div>

      {/* ─── MAIN AREA ──────────────────────────────────────────────────────── */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
        {/* Sidebar Tabs */}
        <div
          onWheel={(e) => { if (e.deltaY !== 0) e.currentTarget.scrollLeft += e.deltaY; }}
          style={{ maxHeight: '100%', height: '100%', flexShrink: 0, display: 'flex', flexDirection: 'column', flexWrap: 'wrap', alignContent: 'flex-start', borderRight: '1px solid rgba(255, 255, 255, 0.08)', background: 'rgba(15, 23, 42, 0.95)', gap: 4, padding: '6px', overflowX: 'auto', overflowY: 'hidden', scrollBehavior: 'smooth' }}
        >
          {categories.map((cat) => {
            const isActive = activeCategoryId === cat.id;
            const count = filterByGender(cat.items, genderFilter).length;
            return (
              <button
                key={cat.id}
                onClick={() => setActiveCategoryId(cat.id)}
                title={cat.label}
                style={{ width: 112, height: 42, display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 6, padding: '4px 6px', border: 'none', cursor: 'pointer', borderRadius: 6, borderLeft: isActive ? '3px solid #38bdf8' : '3px solid transparent', background: isActive ? 'rgba(56, 189, 248, 0.18)' : 'rgba(255, 255, 255, 0.03)', color: isActive ? '#38bdf8' : '#94a3b8', boxSizing: 'border-box' }}
              >
                <span style={{ fontSize: 16 }}>{cat.icon}</span>
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
                  <span style={{ fontSize: 10, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', width: '100%', textAlign: 'left' }}>{cat.label}</span>
                  <span style={{ fontSize: 8, color: isActive ? '#38bdf8' : '#64748b', fontWeight: 600 }}>{count} món</span>
                </div>
                <span style={{ fontSize: 8, fontWeight: 700, padding: '1px 4px', borderRadius: 8, background: count > 0 ? (isActive ? '#38bdf8' : 'rgba(148, 163, 184, 0.25)') : 'rgba(255,255,255,0.06)', color: count > 0 ? (isActive ? '#090d16' : '#cbd5e1') : '#475569' }}>{count}</span>
              </button>
            );
          })}
          <button
            onClick={() => setActiveCategoryId('_sliders')}
            title="Cấu Hình Slider Khuôn Mặt"
            style={{ width: 112, height: 42, display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 6, padding: '4px 6px', border: 'none', cursor: 'pointer', borderRadius: 6, borderLeft: activeCategoryId === '_sliders' ? '3px solid #fbbf24' : '3px solid transparent', background: activeCategoryId === '_sliders' ? 'rgba(251, 191, 36, 0.18)' : 'rgba(255, 255, 255, 0.03)', color: activeCategoryId === '_sliders' ? '#fbbf24' : '#94a3b8', boxSizing: 'border-box' }}
          >
            <Sliders size={16} />
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
              <span style={{ fontSize: 10, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Thanh Trượt</span>
              <span style={{ fontSize: 8, color: activeCategoryId === '_sliders' ? '#fbbf24' : '#64748b', fontWeight: 600 }}>Chi tiết mặt</span>
            </div>
          </button>
        </div>

        {/* Content Area */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
          {/* 1. Items Grid / Sliders Area */}
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}>
            {activeCategoryId === '_sliders' ? (
              <SlidersPanel sliders={currentSliders} sliderDefs={SLIDER_DEFS} useShared={useSharedFaceSliders} onToggleShared={() => setUseSharedFaceSliders(!useSharedFaceSliders)} onChange={handleSliderChange} onReset={handleResetSliders} />
            ) : (
              <ItemsGrid items={filteredItems} selectedPath={getSelectionForCategory(activeCategoryId)} onSelect={(path) => handleSelectItem(activeCategoryId, path)} fallbackIcon={activeCategory?.icon || '📦'} />
            )}
          </div>

          {/* 2. Pinned Presets Bar at Bottom (34px fixed height) */}
          <PresetsBar presets={customPresets} onApply={(p) => { onBaseBodyChange(p.body); onCostumeChange(p.costume); onFaceChange(p.face); setGenderFilter(p.gender); setCharGender(p.gender); }} onDelete={handleDeletePreset} />

          {/* 3. Pinned Character HUD Badge */}
          <CharacterHUDCard
            charName={charName}
            charAge={charAge}
            charHeightCm={charHeightCm}
            charGender={charGender}
            charEducation={charEducation}
            charOccupation={charOccupation}
            charFaction={charFaction}
            charPersonality={charPersonality}
            skills={charSkills}
            onOpenEditModal={() => { setProfileModalMode('edit'); setValidationError(null); setShowProfileModal(true); }}
          />

          {/* 4. Action Bar */}
          <ActionBar scene={scene} selectedActorId={selectedActorId} onSelectActorId={setSelectedActorId} onApply={handleApplyToCurrentActor} onAddNew={() => { setProfileModalMode('create'); setValidationError(null); setShowProfileModal(true); }} />
        </div>
      </div>

      {/* ─── DYNAMIC PROFILE MODAL ────────────────────────────────────────── */}
      {showProfileModal && (
        <CharacterProfileModal
          mode={profileModalMode}
          charName={charName}
          setCharName={setCharName}
          charAge={charAge}
          setCharAge={setCharAge}
          charGender={charGender}
          setCharGender={setCharGender}
          charHeightCm={charHeightCm}
          setCharHeightCm={setCharHeightCm}
          charEducation={charEducation}
          setCharEducation={setCharEducation}
          charOccupation={charOccupation}
          setCharOccupation={setCharOccupation}
          charFaction={charFaction}
          setCharFaction={setCharFaction}
          charPersonality={charPersonality}
          setCharPersonality={setCharPersonality}
          charVoiceStyle={charVoiceStyle}
          setCharVoiceStyle={setCharVoiceStyle}
          charPowerLevel={charPowerLevel}
          setCharPowerLevel={setCharPowerLevel}
          charElement={charElement}
          setCharElement={setCharElement}
          charBiography={charBiography}
          setCharBiography={setCharBiography}
          charSkills={charSkills}
          setCharSkills={setCharSkills}
          customAttributes={customAttributes}
          setCustomAttributes={setCustomAttributes}
          validationError={validationError}
          onClose={() => setShowProfileModal(false)}
          onSave={handleSaveProfileModal}
          onAutoMeasureHeight={() => setCharHeightCm(charGender === 'female' ? 165 : 178)}
          onGenderFilterChange={(g) => setGenderFilter(g)}
        />
      )}
    </div>
  );
};

// ─── Subcomponents ───────────────────────────────────────

function ItemsGrid({ items, selectedPath, onSelect, fallbackIcon }: {
  items: CharacterPartItem[];
  selectedPath: string;
  onSelect: (path: string) => void;
  fallbackIcon: string;
}) {
  if (items.length === 0) {
    return <div style={{ textAlign: 'center', padding: '32px 16px', color: '#64748b', fontSize: 12 }}>Chưa có tài nguyên nào trong mục này.</div>;
  }

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(105px, 1fr))', gap: 8 }}>
      {items.map((item) => {
        const isSelected = selectedPath === item.path;
        return (
          <div
            key={item.id || item.path}
            onClick={() => onSelect(item.path)}
            title={item.name}
            style={{ cursor: 'pointer', borderRadius: 6, padding: 5, display: 'flex', flexDirection: 'column', gap: 4, border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)', background: isSelected ? 'rgba(56, 189, 248, 0.12)' : 'rgba(255,255,255,0.02)', position: 'relative' }}
          >
            <Live3DThumbnail assetPath={item.path} previewUrl={item.preview} altText={item.name} fallbackIcon={fallbackIcon} format={item.format || 'GLB'} height={70} />
            <span style={{ fontSize: 10, fontWeight: 600, color: isSelected ? '#38bdf8' : '#e2e8f0', lineHeight: 1.2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', textAlign: 'center' }}>
              {item.name}
            </span>
            {isSelected && (
              <div style={{ position: 'absolute', top: 4, right: 4, width: 14, height: 14, borderRadius: 7, background: '#38bdf8', color: '#000', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Check size={9} strokeWidth={3} />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

function SlidersPanel({ sliders, sliderDefs, useShared, onToggleShared, onChange, onReset }: {
  sliders: FaceSliderConfig;
  sliderDefs: { key: keyof FaceSliderConfig; label: string; icon: string; min: number }[];
  useShared: boolean;
  onToggleShared: () => void;
  onChange: (key: keyof FaceSliderConfig, value: number) => void;
  onReset: () => void;
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '6px 8px', borderRadius: 6, background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.06)' }}>
        <div onClick={onToggleShared} style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
          {useShared ? <ToggleRight size={16} color="#38bdf8" /> : <ToggleLeft size={16} color="#94a3b8" />}
          <span style={{ fontSize: 11, fontWeight: 600, color: useShared ? '#38bdf8' : '#94a3b8' }}>{useShared ? 'Dùng Chung Sliders' : 'Riêng Actor Này'}</span>
        </div>
        <button onClick={onReset} style={{ padding: '2px 8px', borderRadius: 4, fontSize: 10, fontWeight: 600, background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.1)', color: '#94a3b8', cursor: 'pointer' }}>Đặt Lại</button>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {sliderDefs.map((def) => {
          const val = sliders[def.key] ?? 1.0;
          return (
            <div key={def.key} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10 }}>
                <span style={{ color: '#cbd5e1', display: 'flex', alignItems: 'center', gap: 4 }}><span>{def.icon}</span> {def.label}</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{Math.round(val * 100)}%</span>
              </div>
              <input type="range" min={def.min} max={1.0} step={0.01} value={val} onChange={(e) => onChange(def.key, parseFloat(e.target.value))} style={{ width: '100%', accentColor: '#38bdf8' }} />
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ActionBar({ scene, selectedActorId, onSelectActorId, onApply, onAddNew }: {
  scene: MasterSceneConfig;
  selectedActorId: string;
  onSelectActorId: (id: string) => void;
  onApply: () => void;
  onAddNew: () => void;
}) {
  return (
    <div style={{ flexShrink: 0, padding: '6px 12px', borderTop: '1px solid rgba(255,255,255,0.06)', background: 'rgba(255,255,255,0.02)', display: 'flex', alignItems: 'center', gap: 6 }}>
      <select value={selectedActorId} onChange={(e) => onSelectActorId(e.target.value)} style={{ width: 140, padding: '6px 10px', borderRadius: 6, background: '#1e293b', border: '1px solid rgba(255,255,255,0.2)', color: '#fff', outline: 'none', fontSize: 11 }}>
        {scene.actors.map((a) => (<option key={a.id} value={a.id}>{a.name || a.id}</option>))}
      </select>
      <button onClick={onApply} style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 5, padding: '7px 12px', borderRadius: 6, background: 'linear-gradient(135deg, #0284c7, #0369a1)', color: '#fff', fontWeight: 700, fontSize: 11, border: 'none', cursor: 'pointer', boxShadow: '0 2px 6px rgba(2, 132, 199, 0.3)' }}>
        <UserCheck size={13} /> Gán Hồ Sơ Vào Cảnh
      </button>
      <button onClick={onAddNew} style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '7px 14px', borderRadius: 6, background: 'rgba(34, 197, 94, 0.2)', border: '1px solid rgba(34, 197, 94, 0.4)', color: '#4ade80', fontWeight: 700, fontSize: 11, cursor: 'pointer' }}>
        <UserPlus size={13} /> ➕ Thêm Mới
      </button>
    </div>
  );
}

function btnStyle(borderColor: string, textColor: string): React.CSSProperties {
  return {
    display: 'flex', alignItems: 'center', gap: 4, padding: '3px 8px', fontSize: 10, fontWeight: 600, borderRadius: 5, cursor: 'pointer',
    background: `rgba(${hexToRgb(borderColor)}, 0.15)`, border: `1px solid rgba(${hexToRgb(borderColor)}, 0.4)`, color: textColor, transition: 'all 0.15s',
  };
}

function hexToRgb(hex: string): string {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!result) return '255, 255, 255';
  return `${parseInt(result[1], 16)}, ${parseInt(result[2], 16)}, ${parseInt(result[3], 16)}`;
}
