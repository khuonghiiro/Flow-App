/**
 * ModularOutfitVerticalTabs.tsx
 *
 * Vertical-tab layout for character modular outfit assembly.
 * Features:
 *  - Vertical icon tabs with badges showing item count (Multi-column wrap layout)
 *  - Gender toggle (Nam / Nữ)
 *  - Dynamic JSON-Driven Character Profile & Lore System
 *  - Support for Skills list (Level, Type, Description) & Custom Key-Value Attributes
 *  - Pinned Presets bar at bottom with horizontal scroll (no layout stretching)
 *  - Interactive Character Lore HUD Card & Dynamic Profile Modal
 *  - Strict validation on creating new actor (Required Name & Age)
 *  - Shared vs per-actor face slider toggle
 *  - JSON character profile export / import
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
  Edit3,
  User,
  GraduationCap,
  Briefcase,
  Ruler,
  Heart,
  Volume2,
  FileText,
  AlertCircle,
  Zap,
  Shield,
  Layers,
  Tag,
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
  downloadCharacterProfile,
} from './CharacterAssetRegistry';
import { MasterSceneConfig, ActorConfig, CharacterAssembly, CharacterProfileData } from '../types/scene';
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

// ─── Default Presets ────────────────────────────────────

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

const EDUCATION_PRESETS = [
  'Không có trình độ',
  'Cấp 1 (Tiểu Học)',
  'Cấp 2 (THCS)',
  'Cấp 3 (THPT)',
  'Đại Học',
  'Thạc Sĩ',
  'Tiến Sĩ',
  'Luyện Khí Kỳ',
  'Trúc Cơ Kỳ',
  'Kim Đan Kỳ',
  'Nguyên Anh Kỳ',
  'Hóa Thần Kỳ',
  'Kiếm Tông Đệ Tử',
  'Chưởng Môn Tiên Phái',
];

const ELEMENT_PRESETS = [
  'Không',
  'Hỏa (Lửa)',
  'Băng (Hàn Băng)',
  'Lôi (Sấm Sét)',
  'Phong (Gió)',
  'Thủy (Nước)',
  'Thổ (Đất)',
  'Kim (Kim Loại)',
  'Mộc (Thực Vật)',
  'Quang (Ánh Sáng)',
  'Ám (Bóng Tối)',
];

const SKILL_TYPE_PRESETS = [
  'Chủ Động',
  'Bị Động',
  'Kiếm Pháp',
  'Thần Thông',
  'Phép Thuật',
  'Nội Công',
  'Thân Pháp',
  'Trận Pháp',
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
  const [activeCategoryId, setActiveCategoryId] = useState('than_co_ban');
  const [genderFilter, setGenderFilter] = useState<'male' | 'female'>('male');
  const [useSharedFaceSliders, setUseSharedFaceSliders] = useState(true);
  const [selectedActorId, setSelectedActorId] = useState(scene.actors[0]?.id || '');

  // ─── Character Profile & Dynamic Lore Metadata State ───
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
  const [charBiography, setCharBiography] = useState<string>(currentActor?.profile?.biography || 'Hiệp khách phiêu bạt giang hồ, tinh thông kiếm pháp...');
  const [charSkills, setCharSkills] = useState<CharacterSkillItem[]>(currentActor?.profile?.skills || [
    { name: 'Vạn Kiếm Quy Tông', level: 5, type: 'Kiếm Pháp', description: 'Ngự kiếm phi hành phóng ra ngàn thanh kiếm khí' },
    { name: 'Khí Thuẫn Hộ Thể', level: 3, type: 'Bị Động', description: 'Tự động kích hoạt khi nhận sát thương chí mạng' },
  ]);
  const [customAttributes, setCustomAttributes] = useState<{ key: string; value: string }[]>(() => {
    if (currentActor?.profile?.custom_attributes) {
      return Object.entries(currentActor.profile.custom_attributes).map(([key, value]) => ({
        key,
        value: String(value),
      }));
    }
    return [
      { key: 'Vũ Khí', value: 'Thanh Vân Kiếm' },
      { key: 'Pháp Bảo', value: 'Tử Kim Hồ Lô' },
    ];
  });

  // Modal State
  const [showProfileModal, setShowProfileModal] = useState<boolean>(false);
  const [modalTab, setModalTab] = useState<'basic' | 'combat' | 'lore'>('basic');
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

  // Sync state when selected actor changes
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
            Object.entries(actor.profile.custom_attributes).map(([key, value]) => ({
              key,
              value: String(value),
            }))
          );
        }
      }
    }
  }, [selectedActorId, scene.actors]);

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

  // ─── Auto Estimate Height ───────────────────────────
  const handleAutoMeasureHeight = () => {
    const defaultH = charGender === 'female' ? 165 : 178;
    setCharHeightCm(defaultH);
  };

  // ─── Preset Handlers ───────────────────────────────
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

  // ─── JSON Export / Import with Dynamic Metadata ─────
  const handleExportJSON = () => {
    let snapshotDataUrl = '';
    const canvas = document.querySelector('canvas');
    if (canvas) {
      try {
        snapshotDataUrl = canvas.toDataURL('image/png');
      } catch {}
    }

    const safeName = (charName || 'nhan_vat_lap_rap')
      .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF]/g, '_')
      .toLowerCase();

    const customAttrMap = customAttributes.reduce((acc, curr) => {
      if (curr.key.trim()) acc[curr.key.trim()] = curr.value;
      return acc;
    }, {} as Record<string, any>);

    const profile: CharacterProfileJSON = {
      ...buildCharacterProfile(
        charName,
        baseBody,
        costume,
        face,
        hairstyle,
        currentSliders,
        charBiography,
        {
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
        }
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

    setJsonImportToast(`✅ Đã xuất "${safeName}.json" + "${safeName}.png" (đầy đủ hồ sơ nhân vật cho AI)!`);
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
            Object.entries(profile.custom_attributes).map(([key, value]) => ({
              key,
              value: String(value),
            }))
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

  // ─── Apply to Current Actor in Scene ────────────────
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

    const assembly: CharacterAssembly = {
      base_body: baseBody,
      costume,
      face,
      hairstyle: hairstyle || undefined,
    };

    targetActor.name = charName;
    targetActor.model = baseBody;
    targetActor.assembly = assembly;
    targetActor.profile = profileData;

    const updatedScene: MasterSceneConfig = {
      ...scene,
      actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)),
    };

    onUpdateScene(updatedScene);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  // ─── Create & Add New Actor Handler ─────────────────
  const handleOpenCreateModal = () => {
    setProfileModalMode('create');
    setValidationError(null);
    setShowProfileModal(true);
  };

  const handleOpenEditModal = () => {
    setProfileModalMode('edit');
    setValidationError(null);
    setShowProfileModal(true);
  };

  const handleSaveProfileModal = () => {
    if (!charName.trim()) {
      setValidationError('Vui lòng nhập Tên Nhân Vật!');
      return;
    }
    if (charAge === '' || Number(charAge) <= 0) {
      setValidationError('Vui lòng nhập Số Tuổi hợp lệ (> 0)!');
      return;
    }

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
        assembly: {
          base_body: baseBody,
          costume,
          face,
          hairstyle: hairstyle || undefined,
        },
        profile: profileData,
        spawn_point: [0.0, 0, 1.5],
        rotation_y: 0,
        tracks: { movement: [{ start: 0, end: 10, action: 'idle' }], speech: [] },
      };

      const updatedScene: MasterSceneConfig = { ...scene, actors: [...scene.actors, newActor] };
      onUpdateScene(updatedScene);
      setSelectedActorId(newId);
    } else {
      const targetActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
      if (targetActor) {
        targetActor.name = charName.trim();
        targetActor.profile = profileData;
        const updatedScene: MasterSceneConfig = {
          ...scene,
          actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)),
        };
        onUpdateScene(updatedScene);
      }
    }

    setShowProfileModal(false);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  // ─── Item selection mapping ────────────────────────
  const getSelectionForCategory = (catId: string): string => {
    switch (catId) {
      case 'body':
      case 'than_co_ban': return baseBody;
      case 'face':
      case 'khuon_mat': return face;
      case 'costume':
      case 'trang_phuc': return costume;
      case 'hairstyle':
      case 'kieu_toc': return hairstyle;
      default: return '';
    }
  };

  const handleSelectItem = (catId: string, path: string) => {
    const current = getSelectionForCategory(catId);
    const newVal = current === path ? '' : path;
    switch (catId) {
      case 'body':
      case 'than_co_ban': onBaseBodyChange(newVal); break;
      case 'face':
      case 'khuon_mat': onFaceChange(newVal); break;
      case 'costume':
      case 'trang_phuc': onCostumeChange(newVal); break;
      case 'hairstyle':
      case 'kieu_toc': onHairstyleChange(newVal); break;
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
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', position: 'relative' }}>
      {/* ─── TOP BAR ────────────────────────────────────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '8px 12px',
          gap: 8,
          flexShrink: 0,
          borderBottom: '1px solid rgba(255,255,255,0.06)',
          background: 'rgba(255,255,255,0.02)',
        }}
      >
        {/* Left: Title + Gender toggle */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: '#38bdf8' }}>
            Xưởng Lắp Ráp & Hồ Sơ Nhân Vật
          </span>

          {/* Gender Toggle */}
          <div
            onClick={() => {
              const nextG = genderFilter === 'male' ? 'female' : 'male';
              setGenderFilter(nextG);
              setCharGender(nextG);
            }}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '3px 10px',
              borderRadius: 20,
              cursor: 'pointer',
              background: genderFilter === 'male' ? 'rgba(56, 189, 248, 0.15)' : 'rgba(236, 72, 153, 0.15)',
              border: `1px solid ${genderFilter === 'male' ? 'rgba(56, 189, 248, 0.4)' : 'rgba(236, 72, 153, 0.4)'}`,
              transition: 'all 0.2s',
            }}
          >
            {genderFilter === 'male' ? (
              <ToggleLeft size={14} color="#38bdf8" />
            ) : (
              <ToggleRight size={14} color="#ec4899" />
            )}
            <span
              style={{
                fontSize: 11,
                fontWeight: 700,
                color: genderFilter === 'male' ? '#38bdf8' : '#ec4899',
              }}
            >
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

      {/* ─── MAIN AREA: SIDEBAR + CONTENT ───────────────────────────────────── */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
        {/* Tabs Sidebar with Multi-Column Overflow Grid */}
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

          {/* Sliders Tab */}
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

        {/* Content Area (Vertical Stack: Items Grid [flex 1] -> Presets Bar [pinned] -> HUD [pinned] -> Action Bar [pinned]) */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
          {/* 1. Items Grid / Sliders Area (Fully Scrollable, Takes 100% Remaining Height) */}
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}>
            {activeCategoryId === '_sliders' ? (
              <SlidersPanel
                sliders={currentSliders}
                sliderDefs={SLIDER_DEFS}
                useShared={useSharedFaceSliders}
                onToggleShared={() => setUseSharedFaceSliders(!useSharedFaceSliders)}
                onChange={handleSliderChange}
                onReset={handleResetSliders}
              />
            ) : (
              <ItemsGrid
                items={filteredItems}
                selectedPath={getSelectionForCategory(activeCategoryId)}
                onSelect={(path) => handleSelectItem(activeCategoryId, path)}
                fallbackIcon={activeCategory?.icon || '📦'}
              />
            )}
          </div>

          {/* 2. Pinned Presets Bar at Bottom (Compact Horizontal Strip, Fixed Height, Never Stretches) */}
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
              {customPresets.map((p) => (
                <div
                  key={p.id}
                  onClick={() => {
                    onBaseBodyChange(p.body);
                    onCostumeChange(p.costume);
                    onFaceChange(p.face);
                    setGenderFilter(p.gender);
                    setCharGender(p.gender);
                  }}
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
                      onClick={(e) => handleDeletePreset(p.id, e)}
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

          {/* 3. Pinned Character HUD Badge (Compact, Clean, Direct Edit Trigger) */}
          <div
            style={{
              padding: '6px 12px',
              background: 'rgba(15, 23, 42, 0.95)',
              borderTop: '1px solid rgba(255, 255, 255, 0.08)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 8,
              flexShrink: 0,
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, overflow: 'hidden' }}>
              <div
                style={{
                  width: 32,
                  height: 32,
                  borderRadius: 6,
                  background: 'rgba(56, 189, 248, 0.15)',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 16,
                  flexShrink: 0,
                }}
              >
                {charGender === 'female' ? '👩' : '🧑'}
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 12, fontWeight: 700, color: '#f8fafc' }}>
                    {charName || 'Chưa đặt tên'}
                  </span>
                  <span style={{ fontSize: 10, color: '#38bdf8', fontWeight: 600 }}>
                    {charAge} tuổi • {charHeightCm}cm
                  </span>
                  <span
                    style={{
                      fontSize: 9,
                      padding: '1px 6px',
                      borderRadius: 10,
                      background: 'rgba(168, 85, 247, 0.2)',
                      color: '#c084fc',
                      fontWeight: 600,
                    }}
                  >
                    {charEducation}
                  </span>
                  {charSkills.length > 0 && (
                    <span
                      style={{
                        fontSize: 9,
                        padding: '1px 6px',
                        borderRadius: 10,
                        background: 'rgba(234, 179, 8, 0.2)',
                        color: '#facc15',
                        fontWeight: 600,
                        display: 'flex',
                        alignItems: 'center',
                        gap: 2,
                      }}
                    >
                      <Zap size={9} /> {charSkills.length} Kỹ Năng
                    </span>
                  )}
                </div>
                <span
                  style={{
                    fontSize: 10,
                    color: '#94a3b8',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  💼 {charOccupation} • 🏛️ {charFaction} • 🧠 {charPersonality}
                </span>
              </div>
            </div>

            {/* Edit Full Profile Button */}
            <button
              onClick={handleOpenEditModal}
              title="Chỉnh sửa hồ sơ thông tin chi tiết, kỹ năng & thuộc tính"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '4px 10px',
                borderRadius: 5,
                background: 'rgba(56, 189, 248, 0.15)',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                color: '#38bdf8',
                fontSize: 11,
                fontWeight: 600,
                cursor: 'pointer',
                flexShrink: 0,
              }}
            >
              <Edit3 size={12} /> Sửa Hồ Sơ & Kỹ Năng
            </button>
          </div>

          {/* 4. Action Bar at Very Bottom */}
          <ActionBar
            scene={scene}
            selectedActorId={selectedActorId}
            onSelectActorId={setSelectedActorId}
            onApply={handleApplyToCurrentActor}
            onAddNew={handleOpenCreateModal}
          />
        </div>
      </div>

      {/* ─── DYNAMIC JSON-DRIVEN PROFILE & SKILL SETUP MODAL ───────────────── */}
      {showProfileModal && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            background: 'rgba(0, 0, 0, 0.8)',
            backdropFilter: 'blur(5px)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 100,
            padding: 16,
          }}
        >
          <div
            style={{
              width: '100%',
              maxWidth: 620,
              background: '#0b1120',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              borderRadius: 10,
              boxShadow: '0 25px 50px rgba(0,0,0,0.9)',
              display: 'flex',
              flexDirection: 'column',
              overflow: 'hidden',
            }}
          >
            {/* Modal Header */}
            <div
              style={{
                padding: '10px 14px',
                borderBottom: '1px solid rgba(255,255,255,0.08)',
                background: 'rgba(15, 23, 42, 0.95)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <User size={16} color="#38bdf8" />
                <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
                  {profileModalMode === 'create'
                    ? '➕ Thêm Nhân Vật Mới & Thiết Lập Toàn Bộ Hồ Sơ'
                    : '✏️ Thiết Lập Hồ Sơ, Kỹ Năng & Thuộc Tính Động'}
                </span>
              </div>
              <button
                onClick={() => setShowProfileModal(false)}
                style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer' }}
              >
                <X size={16} />
              </button>
            </div>

            {/* Modal Navigation Tabs (Basic / Combat & Skills / Lore & Custom) */}
            <div
              style={{
                display: 'flex',
                borderBottom: '1px solid rgba(255,255,255,0.08)',
                background: 'rgba(0,0,0,0.25)',
              }}
            >
              {[
                { id: 'basic', label: '👤 1. Thông Tin Cơ Bản', icon: User },
                { id: 'combat', label: '⚡ 2. Kỹ Năng & Chiến Đấu', icon: Zap },
                { id: 'lore', label: '📜 3. Tiểu Sử & Thuộc Tính Tùy Biến', icon: FileText },
              ].map((t) => (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setModalTab(t.id as any)}
                  style={{
                    flex: 1,
                    padding: '8px 0',
                    fontSize: 11,
                    fontWeight: 700,
                    border: 'none',
                    borderBottom: modalTab === t.id ? '2px solid #38bdf8' : '2px solid transparent',
                    background: modalTab === t.id ? 'rgba(56, 189, 248, 0.12)' : 'transparent',
                    color: modalTab === t.id ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  {t.label}
                </button>
              ))}
            </div>

            {/* Modal Body */}
            <div
              style={{
                padding: 14,
                display: 'flex',
                flexDirection: 'column',
                gap: 12,
                maxHeight: '65vh',
                overflowY: 'auto',
              }}
            >
              {validationError && (
                <div
                  style={{
                    padding: '6px 10px',
                    borderRadius: 6,
                    background: 'rgba(239, 68, 68, 0.15)',
                    border: '1px solid rgba(239, 68, 68, 0.4)',
                    color: '#f87171',
                    fontSize: 11,
                    fontWeight: 600,
                    display: 'flex',
                    alignItems: 'center',
                    gap: 6,
                  }}
                >
                  <AlertCircle size={14} /> {validationError}
                </div>
              )}

              {/* TAB 1: BASIC INFORMATION */}
              {modalTab === 'basic' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 10 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
                        🏷️ Tên Nhân Vật <span style={{ color: '#f87171' }}>*</span>
                      </label>
                      <input
                        type="text"
                        value={charName}
                        onChange={(e) => setCharName(e.target.value)}
                        placeholder="Ví dụ: Lý Tiên Sinh, Tiêu Viêm..."
                        style={inputStyle}
                      />
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
                        🎂 Số Tuổi <span style={{ color: '#f87171' }}>*</span>
                      </label>
                      <input
                        type="number"
                        min="1"
                        max="9999"
                        value={charAge}
                        onChange={(e) => setCharAge(e.target.value === '' ? '' : parseInt(e.target.value))}
                        placeholder="18"
                        style={inputStyle}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        ⚧ Giới Tính
                      </label>
                      <div style={{ display: 'flex', gap: 4 }}>
                        {[
                          { id: 'male', label: '♂ Nam' },
                          { id: 'female', label: '♀ Nữ' },
                          { id: 'unisex', label: 'Chung' },
                        ].map((g) => (
                          <button
                            key={g.id}
                            type="button"
                            onClick={() => {
                              setCharGender(g.id as any);
                              if (g.id !== 'unisex') setGenderFilter(g.id as any);
                            }}
                            style={{
                              flex: 1,
                              padding: '6px 0',
                              borderRadius: 6,
                              fontSize: 11,
                              fontWeight: 700,
                              cursor: 'pointer',
                              border: 'none',
                              background: charGender === g.id ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                              color: charGender === g.id ? '#090d16' : '#cbd5e1',
                            }}
                          >
                            {g.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                          📏 Chiều Cao (cm)
                        </label>
                        <button
                          type="button"
                          onClick={handleAutoMeasureHeight}
                          style={{ background: 'none', border: 'none', color: '#38bdf8', fontSize: 10, fontWeight: 600, cursor: 'pointer', padding: 0 }}
                        >
                          📐 Đo Chuẩn {charGender === 'female' ? '165' : '178'}cm
                        </button>
                      </div>
                      <input
                        type="number"
                        min="50"
                        max="300"
                        value={charHeightCm}
                        onChange={(e) => setCharHeightCm(parseInt(e.target.value) || 170)}
                        placeholder="175"
                        style={inputStyle}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                      🎓 Trình Độ Học Vấn / Cấp Bậc / Tu Vi
                    </label>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <select
                        value={EDUCATION_PRESETS.includes(charEducation) ? charEducation : 'custom'}
                        onChange={(e) => {
                          if (e.target.value !== 'custom') setCharEducation(e.target.value);
                        }}
                        style={{ ...inputStyle, width: 170 }}
                      >
                        {EDUCATION_PRESETS.map((p) => (
                          <option key={p} value={p}>{p}</option>
                        ))}
                        <option value="custom">✍️ Tự nhập tay...</option>
                      </select>
                      <input
                        type="text"
                        value={charEducation}
                        onChange={(e) => setCharEducation(e.target.value)}
                        placeholder="Nhập trình độ hoặc tu vi tùy ý..."
                        style={{ ...inputStyle, flex: 1 }}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        💼 Nghề Nghiệp / Thân Phận
                      </label>
                      <input
                        type="text"
                        value={charOccupation}
                        onChange={(e) => setCharOccupation(e.target.value)}
                        placeholder="Kiếm khách, Đạo sĩ, Học sinh..."
                        style={inputStyle}
                      />
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        🏛️ Môn Phái / Phe Phái
                      </label>
                      <input
                        type="text"
                        value={charFaction}
                        onChange={(e) => setCharFaction(e.target.value)}
                        placeholder="Thục Sơn, Võ Đang, Học Viện..."
                        style={inputStyle}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        🧠 Tính Cách & Khí Chất
                      </label>
                      <input
                        type="text"
                        value={charPersonality}
                        onChange={(e) => setCharPersonality(e.target.value)}
                        placeholder="Lạnh lùng, quả quyết, trọng tình..."
                        style={inputStyle}
                      />
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        🎙️ Phong Cách Thoại / Giọng Điệu
                      </label>
                      <input
                        type="text"
                        value={charVoiceStyle}
                        onChange={(e) => setCharVoiceStyle(e.target.value)}
                        placeholder="Trầm ấm, uy nghiêm, hào sảng..."
                        style={inputStyle}
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* TAB 2: COMBAT & DYNAMIC SKILLS LIST */}
              {modalTab === 'combat' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        ⚡ Chiến Lực / Cấp Độ Sức Mạnh
                      </label>
                      <input
                        type="number"
                        min="1"
                        value={charPowerLevel}
                        onChange={(e) => setCharPowerLevel(parseInt(e.target.value) || 100)}
                        style={inputStyle}
                      />
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                        🔥 Nguyên Tố / Hệ
                      </label>
                      <select
                        value={charElement}
                        onChange={(e) => setCharElement(e.target.value)}
                        style={inputStyle}
                      >
                        {ELEMENT_PRESETS.map((el) => (
                          <option key={el} value={el}>{el}</option>
                        ))}
                      </select>
                    </div>
                  </div>

                  {/* Skills Dynamic List Header */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 6 }}>
                    <label style={{ fontSize: 11, fontWeight: 700, color: '#facc15', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <Zap size={13} /> Danh Sách Kỹ Năng & Tuyệt Kỹ ({charSkills.length})
                    </label>
                    <button
                      type="button"
                      onClick={() => {
                        setCharSkills((prev) => [
                          ...prev,
                          { name: 'Kỹ Năng Mới', level: 1, type: 'Chủ Động', description: '' },
                        ]);
                      }}
                      style={{
                        padding: '3px 8px',
                        borderRadius: 5,
                        background: 'rgba(250, 204, 21, 0.15)',
                        border: '1px solid rgba(250, 204, 21, 0.4)',
                        color: '#facc15',
                        fontSize: 10,
                        fontWeight: 700,
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: 3,
                      }}
                    >
                      <Plus size={11} /> Thêm Kỹ Năng
                    </button>
                  </div>

                  {/* Skills List Items */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    {charSkills.map((sk, idx) => (
                      <div
                        key={idx}
                        style={{
                          padding: 8,
                          borderRadius: 6,
                          background: 'rgba(255,255,255,0.03)',
                          border: '1px solid rgba(255,255,255,0.08)',
                          display: 'flex',
                          flexDirection: 'column',
                          gap: 6,
                        }}
                      >
                        <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 0.8fr auto', gap: 6, alignItems: 'center' }}>
                          <input
                            type="text"
                            value={sk.name}
                            onChange={(e) => {
                              const updated = [...charSkills];
                              updated[idx].name = e.target.value;
                              setCharSkills(updated);
                            }}
                            placeholder="Tên kỹ năng..."
                            style={inputStyle}
                          />

                          <select
                            value={sk.type || 'Chủ Động'}
                            onChange={(e) => {
                              const updated = [...charSkills];
                              updated[idx].type = e.target.value;
                              setCharSkills(updated);
                            }}
                            style={inputStyle}
                          >
                            {SKILL_TYPE_PRESETS.map((st) => (
                              <option key={st} value={st}>{st}</option>
                            ))}
                          </select>

                          <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
                            <span style={{ fontSize: 9, color: '#94a3b8' }}>Lv:</span>
                            <input
                              type="number"
                              min="1"
                              max="100"
                              value={sk.level || 1}
                              onChange={(e) => {
                                const updated = [...charSkills];
                                updated[idx].level = parseInt(e.target.value) || 1;
                                setCharSkills(updated);
                              }}
                              style={{ ...inputStyle, width: 45 }}
                            />
                          </div>

                          <button
                            type="button"
                            onClick={() => {
                              setCharSkills(charSkills.filter((_, i) => i !== idx));
                            }}
                            title="Xóa kỹ năng"
                            style={{ background: 'none', border: 'none', color: '#f87171', cursor: 'pointer', padding: 2 }}
                          >
                            <Trash2 size={13} />
                          </button>
                        </div>

                        <input
                          type="text"
                          value={sk.description || ''}
                          onChange={(e) => {
                            const updated = [...charSkills];
                            updated[idx].description = e.target.value;
                            setCharSkills(updated);
                          }}
                          placeholder="Mô tả hiệu ứng kỹ năng / kiếm chiêu..."
                          style={{ ...inputStyle, fontSize: 10 }}
                        />
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* TAB 3: LORE & DYNAMIC CUSTOM ATTRIBUTES */}
              {modalTab === 'lore' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <label style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8' }}>
                      📜 Tiểu Sử & Bối Cảnh Xuất Thân (Cho AI hiểu nhân vật & tạo cảnh quay)
                    </label>
                    <textarea
                      rows={3}
                      value={charBiography}
                      onChange={(e) => setCharBiography(e.target.value)}
                      placeholder="Mô tả xuất thân, mục tiêu, sở trường võ học để AI sinh kịch bản và đạo diễn diễn xuất chân thực..."
                      style={{ ...inputStyle, resize: 'vertical' }}
                    />
                  </div>

                  {/* Dynamic Custom Attributes List */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 6 }}>
                    <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <Tag size={13} /> Thuộc Tính Tùy Biến Thêm ({customAttributes.length})
                    </label>
                    <button
                      type="button"
                      onClick={() => {
                        setCustomAttributes((prev) => [...prev, { key: '', value: '' }]);
                      }}
                      style={{
                        padding: '3px 8px',
                        borderRadius: 5,
                        background: 'rgba(56, 189, 248, 0.15)',
                        border: '1px solid rgba(56, 189, 248, 0.4)',
                        color: '#38bdf8',
                        fontSize: 10,
                        fontWeight: 700,
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: 3,
                      }}
                    >
                      <Plus size={11} /> Thêm Thuộc Tính
                    </button>
                  </div>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                    {customAttributes.map((attr, idx) => (
                      <div key={idx} style={{ display: 'grid', gridTemplateColumns: '1fr 1.5fr auto', gap: 6, alignItems: 'center' }}>
                        <input
                          type="text"
                          value={attr.key}
                          onChange={(e) => {
                            const updated = [...customAttributes];
                            updated[idx].key = e.target.value;
                            setCustomAttributes(updated);
                          }}
                          placeholder="Tên thuộc tính (Vũ khí, Thú cưng...)"
                          style={inputStyle}
                        />
                        <input
                          type="text"
                          value={attr.value}
                          onChange={(e) => {
                            const updated = [...customAttributes];
                            updated[idx].value = e.target.value;
                            setCustomAttributes(updated);
                          }}
                          placeholder="Giá trị thuộc tính..."
                          style={inputStyle}
                        />
                        <button
                          type="button"
                          onClick={() => {
                            setCustomAttributes(customAttributes.filter((_, i) => i !== idx));
                          }}
                          title="Xóa thuộc tính"
                          style={{ background: 'none', border: 'none', color: '#f87171', cursor: 'pointer', padding: 2 }}
                        >
                          <Trash2 size={13} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>

            {/* Modal Footer */}
            <div
              style={{
                padding: '10px 14px',
                borderTop: '1px solid rgba(255,255,255,0.08)',
                background: 'rgba(15, 23, 42, 0.95)',
                display: 'flex',
                justifyContent: 'flex-end',
                gap: 8,
              }}
            >
              <button
                type="button"
                onClick={() => setShowProfileModal(false)}
                style={{
                  padding: '6px 12px',
                  borderRadius: 6,
                  background: 'rgba(255,255,255,0.06)',
                  border: '1px solid rgba(255,255,255,0.15)',
                  color: '#cbd5e1',
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Hủy
              </button>

              <button
                type="button"
                onClick={handleSaveProfileModal}
                style={{
                  padding: '6px 16px',
                  borderRadius: 6,
                  background: 'linear-gradient(135deg, #0284c7, #0369a1)',
                  border: 'none',
                  color: '#fff',
                  fontSize: 11,
                  fontWeight: 700,
                  cursor: 'pointer',
                  boxShadow: '0 2px 8px rgba(2, 132, 199, 0.3)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                }}
              >
                <CheckCircle size={13} />
                {profileModalMode === 'create' ? 'Xác Nhận Thêm Nhân Vật' : 'Lưu Toàn Bộ Hồ Sơ'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

// ─── Subcomponents ───────────────────────────────────────

const inputStyle: React.CSSProperties = {
  padding: '6px 10px',
  borderRadius: 6,
  background: '#0f172a',
  border: '1px solid rgba(255,255,255,0.15)',
  color: '#fff',
  fontSize: 11,
  fontWeight: 600,
  outline: 'none',
};

/** Grid of item cards for current tab */
function ItemsGrid({ items, selectedPath, onSelect, fallbackIcon }: {
  items: CharacterPartItem[];
  selectedPath: string;
  onSelect: (path: string) => void;
  fallbackIcon: string;
}) {
  if (items.length === 0) {
    return (
      <div style={{ textAlign: 'center', padding: '32px 16px', color: '#64748b', fontSize: 12 }}>
        Chưa có tài nguyên nào trong mục này.
      </div>
    );
  }

  return (
    <div style={{
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill, minmax(105px, 1fr))',
      gap: 8,
    }}>
      {items.map((item) => {
        const isSelected = selectedPath === item.path;
        return (
          <div
            key={item.id || item.path}
            onClick={() => onSelect(item.path)}
            title={item.name}
            style={{
              cursor: 'pointer',
              borderRadius: 6,
              padding: 5,
              display: 'flex',
              flexDirection: 'column',
              gap: 4,
              border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
              background: isSelected ? 'rgba(56, 189, 248, 0.12)' : 'rgba(255,255,255,0.02)',
              transition: 'all 0.15s ease',
              position: 'relative',
            }}
          >
            <Live3DThumbnail
              assetPath={item.path}
              previewUrl={item.preview}
              altText={item.name}
              fallbackIcon={fallbackIcon}
              format={item.format || 'GLB'}
              height={70}
            />

            <span style={{
              fontSize: 10,
              fontWeight: 600,
              color: isSelected ? '#38bdf8' : '#e2e8f0',
              lineHeight: 1.2,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
              textAlign: 'center',
            }}>
              {item.name}
            </span>

            {isSelected && (
              <div style={{
                position: 'absolute',
                top: 4,
                right: 4,
                width: 14,
                height: 14,
                borderRadius: 7,
                background: '#38bdf8',
                color: '#000',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}>
                <Check size={9} strokeWidth={3} />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

/** Facial Sliders Panel */
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
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '6px 8px',
        borderRadius: 6,
        background: 'rgba(255,255,255,0.03)',
        border: '1px solid rgba(255,255,255,0.06)',
      }}>
        <div
          onClick={onToggleShared}
          style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}
          title={useShared ? 'Đang dùng chung cho mọi actor' : 'Đang chỉnh riêng cho actor này'}
        >
          {useShared
            ? <ToggleRight size={16} color="#38bdf8" />
            : <ToggleLeft size={16} color="#94a3b8" />
          }
          <span style={{ fontSize: 11, fontWeight: 600, color: useShared ? '#38bdf8' : '#94a3b8' }}>
            {useShared ? 'Dùng Chung Sliders' : 'Riêng Actor Này'}
          </span>
        </div>

        <button
          onClick={onReset}
          style={{
            padding: '2px 8px',
            borderRadius: 4,
            fontSize: 10,
            fontWeight: 600,
            background: 'rgba(255,255,255,0.06)',
            border: '1px solid rgba(255,255,255,0.1)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          Đặt Lại
        </button>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {sliderDefs.map((def) => {
          const val = sliders[def.key] ?? 1.0;
          return (
            <div key={def.key} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10 }}>
                <span style={{ color: '#cbd5e1', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <span>{def.icon}</span> {def.label}
                </span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>
                  {Math.round(val * 100)}%
                </span>
              </div>
              <input
                type="range"
                min={def.min}
                max={1.0}
                step={0.01}
                value={val}
                onChange={(e) => onChange(def.key, parseFloat(e.target.value))}
                style={{ width: '100%', accentColor: '#38bdf8' }}
              />
            </div>
          );
        })}
      </div>
    </div>
  );
}

/** Action bar: actor select, apply, add new */
function ActionBar({ scene, selectedActorId, onSelectActorId, onApply, onAddNew }: {
  scene: MasterSceneConfig;
  selectedActorId: string;
  onSelectActorId: (id: string) => void;
  onApply: () => void;
  onAddNew: () => void;
}) {
  return (
    <div
      style={{
        flexShrink: 0,
        padding: '6px 12px',
        borderTop: '1px solid rgba(255,255,255,0.06)',
        background: 'rgba(255,255,255,0.02)',
        display: 'flex',
        alignItems: 'center',
        gap: 6,
      }}
    >
      <select
        value={selectedActorId}
        onChange={(e) => onSelectActorId(e.target.value)}
        style={{
          width: 140,
          padding: '6px 10px',
          borderRadius: 6,
          background: '#1e293b',
          border: '1px solid rgba(255,255,255,0.2)',
          color: '#fff',
          outline: 'none',
          fontSize: 11,
        }}
      >
        {scene.actors.map((a) => (
          <option key={a.id} value={a.id}>{a.name || a.id}</option>
        ))}
      </select>

      <button
        onClick={onApply}
        style={{
          flex: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 5,
          padding: '7px 12px',
          borderRadius: 6,
          background: 'linear-gradient(135deg, #0284c7, #0369a1)',
          color: '#fff',
          fontWeight: 700,
          fontSize: 11,
          border: 'none',
          cursor: 'pointer',
          boxShadow: '0 2px 6px rgba(2, 132, 199, 0.3)',
        }}
      >
        <UserCheck size={13} /> Gán Hồ Sơ Vào Cảnh
      </button>

      <button
        onClick={onAddNew}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 5,
          padding: '7px 14px',
          borderRadius: 6,
          background: 'rgba(34, 197, 94, 0.2)',
          border: '1px solid rgba(34, 197, 94, 0.4)',
          color: '#4ade80',
          fontWeight: 700,
          fontSize: 11,
          cursor: 'pointer',
        }}
      >
        <UserPlus size={13} /> ➕ Thêm Mới
      </button>
    </div>
  );
}

// ─── Helpers ─────────────────────────────────────────────

function btnStyle(borderColor: string, textColor: string): React.CSSProperties {
  return {
    display: 'flex',
    alignItems: 'center',
    gap: 4,
    padding: '3px 8px',
    fontSize: 10,
    fontWeight: 600,
    borderRadius: 5,
    cursor: 'pointer',
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
