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
  Eye,
  EyeOff,
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
  assembly: CharacterAssembly;
  onAssemblyChange: (updatedAssembly: CharacterAssembly) => void;
  sliders: FaceSliderConfig;
  onSlidersChange: (sliders: FaceSliderConfig) => void;
  onCaptureSnapshot?: () => string;
}

export const ModularOutfitVerticalTabs: React.FC<ModularOutfitVerticalTabsProps> = ({
  scene,
  onUpdateScene,
  assembly,
  onAssemblyChange,
  sliders,
  onSlidersChange,
  onCaptureSnapshot,
}) => {
  const [categories, setCategories] = useState<CharacterCategory[]>(CHARACTER_CATEGORIES);
  const [activeCategoryId, setActiveCategoryId] = useState('than_co_ban');
  const [genderFilter, setGenderFilter] = useState<'male' | 'female'>('male');
  const [hideEmptyCategories, setHideEmptyCategories] = useState<boolean>(true);
  const [useSharedFaceSliders, setUseSharedFaceSliders] = useState(true);
  const [selectedActorId, setSelectedActorId] = useState(scene.actors[0]?.id || '');

  // Visible categories filtered by empty count (always show _lap_rap and key tabs)
  const visibleCategories = hideEmptyCategories
    ? categories.filter((cat) => cat.id === '_lap_rap' || cat.id === 'nhan_vat_lap_rap' || cat.id === 'than_co_ban' || cat.id === 'trang_phuc' || filterByGender(cat.items, genderFilter).length > 0)
    : categories;

  useEffect(() => {
    if (activeCategoryId !== '_sliders') {
      const isCurrentVisible = visibleCategories.some((c) => c.id === activeCategoryId);
      if (!isCurrentVisible && visibleCategories.length > 0) {
        setActiveCategoryId(visibleCategories[0].id);
      }
    }
  }, [genderFilter, hideEmptyCategories, categories, visibleCategories, activeCategoryId]);

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

  // Actor synchronization: Automatically load full assembly, sliders, and profile when switching selectedActorId
  useEffect(() => {
    const actor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
    if (!actor) return;
    setCharName(actor.name || 'Nhân Vật');
    if (actor.profile) {
      if (actor.profile.age !== undefined) setCharAge(actor.profile.age);
      if (actor.profile.gender) {
        setCharGender(actor.profile.gender);
        if (actor.profile.gender !== 'unisex') setGenderFilter(actor.profile.gender);
      }
      if (actor.profile.height_cm !== undefined) setCharHeightCm(actor.profile.height_cm);
      if (actor.profile.education_level) setCharEducation(actor.profile.education_level);
      if (actor.profile.occupation) setCharOccupation(actor.profile.occupation);
      if (actor.profile.faction) setCharFaction(actor.profile.faction);
      if (actor.profile.personality) setCharPersonality(actor.profile.personality);
      if (actor.profile.voice_style) setCharVoiceStyle(actor.profile.voice_style);
      if (actor.profile.power_level !== undefined) setCharPowerLevel(actor.profile.power_level);
      if (actor.profile.element) setCharElement(actor.profile.element);
      if (actor.profile.biography) setCharBiography(actor.profile.biography);
      if (actor.profile.skills) setCharSkills(actor.profile.skills);
      if (actor.profile.custom_attributes) {
        setCustomAttributes(
          Object.entries(actor.profile.custom_attributes).map(([key, value]) => ({ key, value: String(value) }))
        );
      }
    }
    const ass = actor.assembly || actor.profile?.assembly;
    if (ass) {
      onAssemblyChange(ass);
      const sl = ass.sliders || actor.profile?.sliders;
      if (sl) onSlidersChange(sl);
    } else if (actor.model) {
      onAssemblyChange({ than_co_ban: actor.model, base_body: actor.model });
    }
  }, [selectedActorId]);

  // Modal State
  const [showProfileModal, setShowProfileModal] = useState<boolean>(false);
  const [profileModalMode, setProfileModalMode] = useState<'create' | 'edit'>('create');
  const [validationError, setValidationError] = useState<string | null>(null);

  const [isAppliedSuccess, setIsAppliedSuccess] = useState(false);
  const [presetSavedToast, setPresetSavedToast] = useState('');
  const [jsonImportToast, setJsonImportToast] = useState('');
  const jsonImportRef = useRef<HTMLInputElement>(null);

  const [customPresets, setCustomPresets] = useState<CustomPreset[]>(() => {
    try {
      const saved = localStorage.getItem('custom_character_presets');
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });

  useEffect(() => {
    const loadCategories = () => {
      fetchLiveCharacterCategories().then((cats) => {
        setCategories(cats);
      });
    };
    loadCategories();

    const handleAssetsUpdate = () => {
      try {
        const saved = localStorage.getItem('custom_character_presets');
        if (saved) setCustomPresets(JSON.parse(saved));
      } catch {}
      loadCategories();
    };

    window.addEventListener('flow_assets_updated', handleAssetsUpdate);
    return () => window.removeEventListener('flow_assets_updated', handleAssetsUpdate);
  }, []);

  const [perActorSliders, setPerActorSliders] = useState<Record<string, FaceSliderConfig>>({});
  const currentSliders = useSharedFaceSliders ? sliders : (perActorSliders[selectedActorId] || sliders);

  const currentBody = assembly?.than_co_ban || assembly?.base_body || categories.find((c) => c.id === 'than_co_ban')?.items[0]?.path || scene.actors[0]?.model || '';
  const currentCostume = assembly?.trang_phuc || assembly?.costume || '';
  const currentFace = assembly?.khuon_mat || assembly?.face || '';

  const handleSliderChange = (key: keyof FaceSliderConfig, value: number) => {
    let updated: FaceSliderConfig;
    if (useSharedFaceSliders) {
      updated = { ...sliders, [key]: value };
      onSlidersChange(updated);
    } else {
      updated = { ...(perActorSliders[selectedActorId] || sliders), [key]: value };
      setPerActorSliders((prev) => ({ ...prev, [selectedActorId]: updated }));
      onSlidersChange(updated);
    }
    try {
      localStorage.setItem('flow_character_face_sliders', JSON.stringify(updated));
    } catch {}
  };

  const handleResetSliders = () => {
    const updated: FaceSliderConfig = { ...DEFAULT_FACE_SLIDERS };
    onSlidersChange(updated);
    if (!useSharedFaceSliders) {
      setPerActorSliders((prev) => ({ ...prev, [selectedActorId]: updated }));
    }
    try {
      localStorage.setItem('flow_character_face_sliders', JSON.stringify(updated));
    } catch {}
    setPresetSavedToast('✅ Đã đặt lại thanh trượt về mặc định (Face cũ 0%, Mũi 0%, Miệng 0%)!');
    setTimeout(() => setPresetSavedToast(''), 3000);
  };

  const handleSaveSliderCache = () => {
    try {
      localStorage.setItem('flow_character_face_sliders', JSON.stringify(currentSliders));
      setPresetSavedToast('💾 Đã lưu cấu hình thanh trượt vào Cache!');
      setTimeout(() => setPresetSavedToast(''), 3000);
    } catch {}
  };

  const applyPreset = (p: CustomPreset) => {
    const presetAss: CharacterAssembly = p.assembly || {
      than_co_ban: p.body,
      base_body: p.body,
      trang_phuc: p.costume,
      costume: p.costume,
      khuon_mat: p.face,
      face: p.face,
      kieu_toc: p.hairstyle,
      hairstyle: p.hairstyle,
      ...p,
    };
    onAssemblyChange(presetAss);
    if (p.sliders) onSlidersChange(p.sliders);
    setGenderFilter(p.gender);
    setCharGender(p.gender);
    if (p.name) setCharName(p.name.replace(/^[^\w\s]*\s*/, ''));
    if (p.profile) {
      if (p.profile.age !== undefined) setCharAge(p.profile.age);
      if (p.profile.height_cm !== undefined) setCharHeightCm(p.profile.height_cm);
      if (p.profile.education_level) setCharEducation(p.profile.education_level);
      if (p.profile.occupation) setCharOccupation(p.profile.occupation);
      if (p.profile.faction) setCharFaction(p.profile.faction);
      if (p.profile.personality) setCharPersonality(p.profile.personality);
      if (p.profile.voice_style) setCharVoiceStyle(p.profile.voice_style);
      if (p.profile.power_level !== undefined) setCharPowerLevel(p.profile.power_level);
      if (p.profile.element) setCharElement(p.profile.element);
      if (p.profile.biography) setCharBiography(p.profile.biography);
      if (p.profile.skills) setCharSkills(p.profile.skills);
      if (p.profile.custom_attributes) {
        setCustomAttributes(
          Object.entries(p.profile.custom_attributes).map(([key, value]) => ({ key, value: String(value) }))
        );
      }
    }
  };

  const handleSaveCustomPreset = () => {
    let name = '';
    try { name = prompt('Nhập tên cho mẫu phối đồ này:', charName) || ''; } catch {}
    if (!name) name = charName || 'Nhân Vật Mới';

    // 1. Capture snapshot directly from 3D Character Viewport
    let snapshotDataUrl = '';
    if (onCaptureSnapshot) {
      snapshotDataUrl = onCaptureSnapshot();
    }

    const safeName = (name || 'nhan_vat_lap_rap')
      .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
      .toLowerCase();

    const customAttrMap = customAttributes.reduce((acc, curr) => {
      if (curr.key.trim()) acc[curr.key.trim()] = curr.value;
      return acc;
    }, {} as Record<string, any>);

    const profile: CharacterProfileJSON = {
      ...buildCharacterProfile(name, { ...assembly, sliders: { ...currentSliders } }, charBiography, {
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
      preview_image: `assets/nhan_vat/_lap_rap/${safeName}.png`,
    };

    const newPreset: CustomPreset = {
      id: `preset_${Date.now()}`,
      name: `${currentBody.includes('manekina') ? '👩' : '🧑'} ${name}`,
      body: currentBody,
      costume: currentCostume,
      face: currentFace,
      gender: currentBody.includes('manekina') ? 'female' : 'male',
      assembly: { ...assembly, sliders: { ...currentSliders } },
      sliders: { ...currentSliders },
      preview: snapshotDataUrl || `assets/nhan_vat/_lap_rap/${safeName}.png`,
      profile: profile as any,
    };

    const updated = [newPreset, ...customPresets.filter((p) => p.name !== newPreset.name)];
    setCustomPresets(updated);
    try {
      localStorage.setItem('custom_character_presets', JSON.stringify(updated));
      window.dispatchEvent(new Event('flow_assets_updated'));
    } catch {}

    // 2. Save directly to project disk: assets/nhan_vat/_lap_rap/
    fetch('/api/save-character', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filename: safeName,
        profileData: profile,
        previewImageBase64: snapshotDataUrl,
      }),
    }).catch((err) => console.warn('Lỗi lưu nhân vật qua API:', err));

    // 3. Trigger client download so user has file on PC Downloads
    const jsonBlob = new Blob([JSON.stringify(profile, null, 2)], { type: 'application/json' });
    const jsonUrl = URL.createObjectURL(jsonBlob);
    const aJson = document.createElement('a');
    aJson.href = jsonUrl;
    aJson.download = `${safeName}.json`;
    aJson.click();
    URL.revokeObjectURL(jsonUrl);

    if (snapshotDataUrl && snapshotDataUrl.includes('base64,')) {
      const aImg = document.createElement('a');
      aImg.href = snapshotDataUrl;
      aImg.download = `${safeName}.png`;
      aImg.click();
    }

    setPresetSavedToast(`💾 Đã lưu vào assets/nhan_vat/_lap_rap/ & tải về "${safeName}.json" + "${safeName}.png"!`);
    setTimeout(() => setPresetSavedToast(''), 5000);
  };

  const handleDeletePreset = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const updated = customPresets.filter((p) => p.id !== id);
    setCustomPresets(updated);
    try {
      localStorage.setItem('custom_character_presets', JSON.stringify(updated));
      window.dispatchEvent(new Event('flow_assets_updated'));
    } catch {}
  };

  const handleExportJSON = () => {
    let snapshotDataUrl = '';
    if (onCaptureSnapshot) {
      snapshotDataUrl = onCaptureSnapshot();
    }
    const safeName = (charName || 'nhan_vat_lap_rap')
      .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
      .toLowerCase();
    const customAttrMap = customAttributes.reduce((acc, curr) => {
      if (curr.key.trim()) acc[curr.key.trim()] = curr.value;
      return acc;
    }, {} as Record<string, any>);

    const profile: CharacterProfileJSON = {
      ...buildCharacterProfile(charName, { ...assembly, sliders: { ...currentSliders } }, charBiography, {
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
      preview_image: `assets/nhan_vat/_lap_rap/${safeName}.png`,
    };

    // Save to server disk
    fetch('/api/save-character', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filename: safeName,
        profileData: profile,
        previewImageBase64: snapshotDataUrl,
      }),
    }).catch((err) => console.warn('Lỗi lưu nhân vật qua API:', err));

    const jsonBlob = new Blob([JSON.stringify(profile, null, 2)], { type: 'application/json' });
    const jsonUrl = URL.createObjectURL(jsonBlob);
    const aJson = document.createElement('a');
    aJson.href = jsonUrl;
    aJson.download = `${safeName}.json`;
    aJson.click();
    URL.revokeObjectURL(jsonUrl);

    if (snapshotDataUrl && snapshotDataUrl.includes('base64,')) {
      const aImg = document.createElement('a');
      aImg.href = snapshotDataUrl;
      aImg.download = `${safeName}.png`;
      aImg.click();
    }

    const exportedPreset: CustomPreset = {
      id: `preset_${Date.now()}`,
      name: `${currentBody.includes('manekina') ? '👩' : '🧑'} ${charName || 'Nhân Vật Đã Ráp'}`,
      body: currentBody,
      costume: currentCostume,
      face: currentFace,
      gender: charGender === 'female' ? 'female' : 'male',
      assembly: { ...assembly, sliders: { ...currentSliders } },
      sliders: { ...currentSliders },
      preview: snapshotDataUrl || undefined,
      profile: profile as any,
    };
    const updatedOnExport = [exportedPreset, ...customPresets.filter((p) => p.name !== exportedPreset.name)];
    setCustomPresets(updatedOnExport);
    try {
      localStorage.setItem('custom_character_presets', JSON.stringify(updatedOnExport));
      window.dispatchEvent(new Event('flow_assets_updated'));
    } catch {}

    setJsonImportToast(`💾 Đã lưu vào assets/nhan_vat/_lap_rap/ & tải về "${safeName}.json" + "${safeName}.png"!`);
    setTimeout(() => setJsonImportToast(''), 5000);
  };

  const handleImportJSON = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (evt) => {
      try {
        const profile: CharacterProfileJSON = JSON.parse(evt.target?.result as string);
        const ass: CharacterAssembly = profile.assembly || {
          than_co_ban: profile.base_body,
          base_body: profile.base_body,
          trang_phuc: profile.costume,
          costume: profile.costume,
          khuon_mat: profile.face,
          face: profile.face,
          kieu_toc: profile.hairstyle,
          hairstyle: profile.hairstyle,
          ...profile,
        };
        onAssemblyChange(ass);
        if (profile.face_sliders || ass.sliders) onSlidersChange(profile.face_sliders || ass.sliders!);
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

        const importedPreset: CustomPreset = {
          id: `preset_${Date.now()}`,
          name: `${((ass.than_co_ban || ass.base_body || '') as string).includes('manekina') ? '👩' : '🧑'} ${profile.name || 'Nhân Vật Nhập'}`,
          body: ass.than_co_ban || ass.base_body || categories.find((c) => c.id === 'than_co_ban')?.items[0]?.path || '',
          gender: profile.gender === 'female' ? 'female' : 'male',
          assembly: ass,
          sliders: profile.face_sliders || ass.sliders,
        };
        const updatedOnImport = [importedPreset, ...customPresets.filter((p) => p.name !== importedPreset.name)];
        setCustomPresets(updatedOnImport);
        try {
          localStorage.setItem('custom_character_presets', JSON.stringify(updatedOnImport));
          window.dispatchEvent(new Event('flow_assets_updated'));
        } catch {}

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

    let snapshotDataUrl = '';
    if (onCaptureSnapshot) {
      snapshotDataUrl = onCaptureSnapshot();
    }

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
      assembly: { ...assembly, sliders: { ...currentSliders } },
      sliders: { ...currentSliders },
    };

    const body = assembly?.than_co_ban || assembly?.base_body || targetActor.model || categories.find((c) => c.id === 'than_co_ban')?.items[0]?.path || '';
    targetActor.name = charName;
    targetActor.model = body;
    targetActor.assembly = { ...assembly, sliders: { ...currentSliders } };
    targetActor.profile = profileData;

    const appliedPreset: CustomPreset = {
      id: `preset_${targetActor.id}`,
      name: `${body.includes('manekina') ? '👩' : '🧑'} ${charName}`,
      body,
      gender: charGender === 'female' ? 'female' : 'male',
      assembly: { ...assembly, sliders: { ...currentSliders } },
      sliders: { ...currentSliders },
      preview: snapshotDataUrl || undefined,
      profile: profileData as any,
    };
    const existingIndex = customPresets.findIndex((p) => p.id === appliedPreset.id || p.name === appliedPreset.name);
    const updatedPresets = existingIndex >= 0
      ? customPresets.map((p, idx) => (idx === existingIndex ? appliedPreset : p))
      : [appliedPreset, ...customPresets];
    setCustomPresets(updatedPresets);
    try {
      localStorage.setItem('custom_character_presets', JSON.stringify(updatedPresets));
      window.dispatchEvent(new Event('flow_assets_updated'));
    } catch {}

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

    let snapshotDataUrl = '';
    if (onCaptureSnapshot) {
      snapshotDataUrl = onCaptureSnapshot();
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
      assembly: { ...assembly, sliders: { ...currentSliders } },
      sliders: { ...currentSliders },
    };

    const body = assembly?.than_co_ban || assembly?.base_body || categories.find((c) => c.id === 'than_co_ban')?.items[0]?.path || '';
    const safeName = charName.trim().replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_').toLowerCase();

    // Save to disk
    const fullProfileJSON: CharacterProfileJSON = {
      ...buildCharacterProfile(charName.trim(), { ...assembly, sliders: { ...currentSliders } }, charBiography, profileData),
      preview_image: `assets/nhan_vat/_lap_rap/${safeName}.png`,
    };
    fetch('/api/save-character', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filename: safeName,
        profileData: fullProfileJSON,
        previewImageBase64: snapshotDataUrl,
      }),
    }).catch((err) => console.warn('Lỗi lưu nhân vật qua API:', err));

    if (profileModalMode === 'create') {
      const newId = `actor_${Math.random().toString(36).substring(2, 7)}`;
      const newActor: ActorConfig = {
        id: newId,
        name: charName.trim(),
        model: body,
        assembly: { ...assembly, sliders: { ...currentSliders } },
        profile: profileData,
        spawn_point: [0.0, 0, 1.5],
        rotation_y: 0,
        tracks: { movement: [{ start: 0, end: 10, action: 'idle' }], speech: [] },
      };
      onUpdateScene({ ...scene, actors: [...scene.actors, newActor] });
      setSelectedActorId(newId);

      const newPreset: CustomPreset = {
        id: `preset_${newId}`,
        name: `${body.includes('manekina') ? '👩' : '🧑'} ${charName.trim()}`,
        body,
        gender: charGender === 'female' ? 'female' : 'male',
        assembly: { ...assembly, sliders: { ...currentSliders } },
        sliders: { ...currentSliders },
        preview: snapshotDataUrl || undefined,
        profile: profileData as any,
      };
      const updatedPresets = [newPreset, ...customPresets.filter((p) => p.name !== newPreset.name && p.id !== newPreset.id)];
      setCustomPresets(updatedPresets);
      try {
        localStorage.setItem('custom_character_presets', JSON.stringify(updatedPresets));
        window.dispatchEvent(new Event('flow_assets_updated'));
      } catch {}
    } else {
      const targetActor = scene.actors.find((a) => a.id === selectedActorId) || scene.actors[0];
      if (targetActor) {
        targetActor.name = charName.trim();
        targetActor.model = body;
        targetActor.profile = profileData;
        targetActor.assembly = { ...assembly, sliders: { ...currentSliders } };
        onUpdateScene({ ...scene, actors: scene.actors.map((a) => (a.id === targetActor.id ? { ...targetActor } : a)) });

        const editPreset: CustomPreset = {
          id: `preset_${targetActor.id}`,
          name: `${body.includes('manekina') ? '👩' : '🧑'} ${charName.trim()}`,
          body,
          gender: charGender === 'female' ? 'female' : 'male',
          assembly: { ...assembly, sliders: { ...currentSliders } },
          sliders: { ...currentSliders },
          preview: snapshotDataUrl || undefined,
          profile: profileData as any,
        };
        const updatedPresets = [editPreset, ...customPresets.filter((p) => p.name !== editPreset.name && p.id !== editPreset.id)];
        setCustomPresets(updatedPresets);
        try {
          localStorage.setItem('custom_character_presets', JSON.stringify(updatedPresets));
          window.dispatchEvent(new Event('flow_assets_updated'));
        } catch {}
      }
    }
    setShowProfileModal(false);
    setIsAppliedSuccess(true);
    setTimeout(() => setIsAppliedSuccess(false), 3000);
  };

  const getSelectionForCategory = (catId: string): string => {
    const val = assembly?.[catId] || (catId === 'than_co_ban' ? assembly?.base_body : catId === 'trang_phuc' ? assembly?.costume : catId === 'khuon_mat' ? assembly?.face : catId === 'kieu_toc' ? assembly?.hairstyle : '');
    return typeof val === 'string' ? val : '';
  };

  const handleSelectItem = (catId: string, path: string) => {
    const current = getSelectionForCategory(catId);
    const nextVal = current === path ? '' : path;
    const nextAss = { ...assembly };
    if (nextVal) {
      nextAss[catId] = nextVal;
      if (catId === 'than_co_ban') nextAss.base_body = nextVal;
      if (catId === 'trang_phuc') nextAss.costume = nextVal;
      if (catId === 'khuon_mat') nextAss.face = nextVal;
      if (catId === 'kieu_toc') nextAss.hairstyle = nextVal;
    } else {
      delete nextAss[catId];
      if (catId === 'than_co_ban') delete nextAss.base_body;
      if (catId === 'trang_phuc') delete nextAss.costume;
      if (catId === 'khuon_mat') delete nextAss.face;
      if (catId === 'kieu_toc') delete nextAss.hairstyle;
    }
    onAssemblyChange(nextAss);
  };

  const activeCategory = categories.find((c) => c.id === activeCategoryId);
  const filteredItems = activeCategory ? filterByGender(activeCategory.items, genderFilter) : [];

  const SLIDER_DEFS: {
    key: keyof FaceSliderConfig;
    label: string;
    icon: string;
    min: number;
    description: string;
    color: string;
  }[] = [
    { key: 'baseFaceOpacity', label: 'Độ Hiện Face Cũ', icon: '🎭', min: 0, description: 'Ẩn/hiện mặt và đầu gốc (0% = ẩn hoàn toàn)', color: '#f59e0b' },
    { key: 'noseOpacity', label: 'Độ Nổi Mũi', icon: '👃', min: 0, description: 'Độ nổi chi tiết sống mũi & chóp mũi (0% = làm phẳng)', color: '#8b5cf6' },
    { key: 'mouthOpacity', label: 'Độ Rõ Miệng & Môi', icon: '👄', min: 0, description: 'Độ rõ nét khuôn miệng và viền môi (0% = ẩn viền)', color: '#ec4899' },
    { key: 'eyebrowOpacity', label: 'Độ Đậm Lông Mày', icon: '🤨', min: 0, description: 'Độ đậm nét của lông mày', color: '#06b6d4' },
    { key: 'pupilOpacity', label: 'Độ Sáng Tròng Mắt', icon: '✨', min: 0, description: 'Độ sáng tròng mắt và phản chiếu', color: '#38bdf8' },
    { key: 'skinSmoothness', label: 'Độ Mịn Da', icon: '🌸', min: 0.1, description: 'Độ mịn và phản quang làn da', color: '#10b981' },
    { key: 'costumeOpacity', label: 'Độ Đậm Trang Phục', icon: '🥋', min: 0, description: 'Độ rõ nét của quần áo & giáp', color: '#a855f7' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', position: 'relative' }}>
      {/* ─── TOP BAR ────────────────────────────────────────────────────────── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 12px', gap: 8, flexShrink: 0, borderBottom: '1px solid rgba(255,255,255,0.06)', background: 'rgba(255,255,255,0.02)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: '#38bdf8' }}>Xưởng Lắp Ráp & Hồ Sơ Nhân Vật</span>

          {/* Gender Filter Toggle */}
          <div
            onClick={() => { const nextG = genderFilter === 'male' ? 'female' : 'male'; setGenderFilter(nextG); setCharGender(nextG); }}
            style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '3px 10px', borderRadius: 20, cursor: 'pointer', background: genderFilter === 'male' ? 'rgba(56, 189, 248, 0.15)' : 'rgba(236, 72, 153, 0.15)', border: `1px solid ${genderFilter === 'male' ? 'rgba(56, 189, 248, 0.4)' : 'rgba(236, 72, 153, 0.4)'}`, userSelect: 'none' }}
          >
            {genderFilter === 'male' ? <ToggleLeft size={14} color="#38bdf8" /> : <ToggleRight size={14} color="#ec4899" />}
            <span style={{ fontSize: 11, fontWeight: 700, color: genderFilter === 'male' ? '#38bdf8' : '#ec4899' }}>{genderFilter === 'male' ? '♂ Nam' : '♀ Nữ'}</span>
          </div>

          {/* Hide Empty Items (Count = 0) Toggle */}
          <div
            onClick={() => setHideEmptyCategories(!hideEmptyCategories)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '3px 9px',
              borderRadius: 20,
              cursor: 'pointer',
              background: hideEmptyCategories ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255, 255, 255, 0.05)',
              border: `1px solid ${hideEmptyCategories ? 'rgba(56, 189, 248, 0.4)' : 'rgba(255, 255, 255, 0.15)'}`,
              color: hideEmptyCategories ? '#38bdf8' : '#94a3b8',
              fontSize: 11,
              fontWeight: 600,
              userSelect: 'none',
              transition: 'all 0.15s ease',
            }}
            title="Bật/Tắt ẩn danh mục không có tài nguyên (số lượng = 0)"
          >
            {hideEmptyCategories ? <EyeOff size={13} color="#38bdf8" /> : <Eye size={13} color="#94a3b8" />}
            <span>{hideEmptyCategories ? 'Đang Ẩn mục (0)' : 'Hiện tất cả'}</span>
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
        {/* Single-Column Clean Vertical Sidebar Tabs */}
        <div
          style={{
            width: 140,
            height: '100%',
            flexShrink: 0,
            display: 'flex',
            flexDirection: 'column',
            borderRight: '1px solid rgba(255, 255, 255, 0.08)',
            background: 'rgba(15, 23, 42, 0.95)',
            gap: 4,
            padding: '8px 6px',
            overflowY: 'auto',
            overflowX: 'hidden',
          }}
        >
          {visibleCategories.map((cat) => {
            const isActive = activeCategoryId === cat.id;
            const isAssembled = cat.id === '_lap_rap' || cat.id === 'nhan_vat_lap_rap';
            const count = isAssembled ? customPresets.length : filterByGender(cat.items, genderFilter).length;
            return (
              <button
                key={cat.id}
                onClick={() => setActiveCategoryId(cat.id)}
                title={cat.label}
                style={{
                  width: '100%',
                  minHeight: 40,
                  display: 'flex',
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: 6,
                  padding: '6px 8px',
                  border: 'none',
                  cursor: 'pointer',
                  borderRadius: 6,
                  borderLeft: isActive ? '3px solid #38bdf8' : '3px solid transparent',
                  background: isActive ? 'rgba(56, 189, 248, 0.18)' : 'rgba(255, 255, 255, 0.03)',
                  color: isActive ? '#38bdf8' : count > 0 ? '#cbd5e1' : '#64748b',
                  opacity: count === 0 ? 0.6 : 1.0,
                  boxSizing: 'border-box',
                  transition: 'all 0.15s ease',
                }}
              >
                <span style={{ fontSize: 16 }}>{cat.icon}</span>
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
                  <span style={{ fontSize: 11, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', width: '100%', textAlign: 'left' }}>{cat.label}</span>
                  <span style={{ fontSize: 8, color: isActive ? '#38bdf8' : '#64748b', fontWeight: 600 }}>{count} {isAssembled ? 'mẫu' : 'món'}</span>
                </div>
                <span style={{ fontSize: 9, fontWeight: 700, padding: '1px 5px', borderRadius: 8, background: count > 0 ? (isActive ? '#38bdf8' : 'rgba(148, 163, 184, 0.25)') : 'rgba(255,255,255,0.06)', color: count > 0 ? (isActive ? '#090d16' : '#cbd5e1') : '#475569' }}>{count}</span>
              </button>
            );
          })}
          <button
            onClick={() => setActiveCategoryId('_sliders')}
            title="Cấu Hình Slider Khuôn Mặt"
            style={{
              width: '100%',
              minHeight: 40,
              display: 'flex',
              flexDirection: 'row',
              alignItems: 'center',
              gap: 6,
              padding: '6px 8px',
              border: 'none',
              cursor: 'pointer',
              borderRadius: 6,
              borderLeft: activeCategoryId === '_sliders' ? '3px solid #fbbf24' : '3px solid transparent',
              background: activeCategoryId === '_sliders' ? 'rgba(251, 191, 36, 0.18)' : 'rgba(255, 255, 255, 0.03)',
              color: activeCategoryId === '_sliders' ? '#fbbf24' : '#94a3b8',
              boxSizing: 'border-box',
              transition: 'all 0.15s ease',
            }}
          >
            <Sliders size={16} />
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
              <span style={{ fontSize: 11, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Thanh Trượt</span>
              <span style={{ fontSize: 8, color: activeCategoryId === '_sliders' ? '#fbbf24' : '#64748b', fontWeight: 600 }}>Chi tiết mặt</span>
            </div>
          </button>
        </div>

        {/* Content Area */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
          {/* 1. Items Grid / Sliders / Assembled Characters Area */}
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}>
            {activeCategoryId === '_sliders' ? (
              <SlidersPanel
                sliders={currentSliders}
                sliderDefs={SLIDER_DEFS}
                useShared={useSharedFaceSliders}
                onToggleShared={() => setUseSharedFaceSliders(!useSharedFaceSliders)}
                onChange={handleSliderChange}
                onReset={handleResetSliders}
                onSaveCache={handleSaveSliderCache}
              />
            ) : activeCategoryId === '_lap_rap' || activeCategoryId === 'nhan_vat_lap_rap' ? (
              <AssembledCharactersPanel
                presets={customPresets}
                currentBody={currentBody}
                currentCostume={currentCostume}
                currentFace={currentFace}
                onApply={applyPreset}
                onDelete={handleDeletePreset}
                onImportClick={() => jsonImportRef.current?.click()}
              />
            ) : (
              <ItemsGrid items={filteredItems} selectedPath={getSelectionForCategory(activeCategoryId)} onSelect={(path) => handleSelectItem(activeCategoryId, path)} fallbackIcon={activeCategory?.icon || '📦'} />
            )}
          </div>

          {/* 2. Pinned Presets Bar at Bottom (34px fixed height) */}
          <PresetsBar presets={customPresets} onApply={applyPreset} onDelete={handleDeletePreset} />

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
          <ActionBar
            scene={scene}
            selectedActorId={selectedActorId}
            onSelectActorId={setSelectedActorId}
            onApply={handleApplyToCurrentActor}
            onAddNew={() => {
              setProfileModalMode('create');
              setCharName(`Nhân Vật ${scene.actors.length + 1}`);
              setCharAge(20);
              setValidationError(null);
              setShowProfileModal(true);
            }}
          />
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
          categories={categories}
          assembly={assembly}
          onAssemblyChange={onAssemblyChange}
          sliders={currentSliders}
          onSlidersChange={onSlidersChange}
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

function AssembledCharactersPanel({
  presets,
  currentBody,
  currentCostume,
  currentFace,
  onApply,
  onDelete,
  onImportClick,
}: {
  presets: CustomPreset[];
  currentBody: string;
  currentCostume: string;
  currentFace: string;
  onApply: (preset: CustomPreset) => void;
  onDelete: (id: string, e: React.MouseEvent) => void;
  onImportClick: () => void;
}) {
  if (presets.length === 0) {
    return (
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '36px 16px',
          gap: 12,
          color: '#94a3b8',
          background: 'rgba(255, 255, 255, 0.02)',
          borderRadius: 10,
          border: '1px dashed rgba(255, 255, 255, 0.1)',
          margin: '10px 0',
        }}
      >
        <div style={{ fontSize: 32 }}>✨</div>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#f1f5f9' }}>
          Chưa có mẫu nhân vật lắp ráp nào
        </span>
        <span style={{ fontSize: 11, color: '#64748b', maxWidth: 360, textAlign: 'center' }}>
          Bạn có thể chọn Thân + Trang Phục + Mặt rồi bấm nút "Lưu Mẫu" ở thanh dưới cùng, hoặc bấm nút nạp file .JSON bên dưới!
        </span>
        <button
          onClick={onImportClick}
          style={{
            marginTop: 4,
            background: 'linear-gradient(135deg, #0284c7, #0369a1)',
            border: '1px solid #38bdf8',
            color: '#fff',
            borderRadius: 6,
            padding: '6px 14px',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
          }}
        >
          <Upload size={13} /> Nạp File .JSON Nhân Vật
        </button>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {/* Top action strip for _lap_rap */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '6px 8px', borderRadius: 6, background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.06)' }}>
        <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>✨ Danh Sách Nhân Vật Đã Ráp ({presets.length})</span>
        <button
          onClick={onImportClick}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 10px',
            borderRadius: 6,
            fontSize: 10,
            fontWeight: 700,
            background: 'rgba(56, 189, 248, 0.15)',
            border: '1px solid rgba(56, 189, 248, 0.4)',
            color: '#38bdf8',
            cursor: 'pointer',
          }}
        >
          <Upload size={11} /> Nhập File JSON
        </button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))', gap: 10 }}>
        {presets.map((p) => {
          const isCurrentActive = currentBody === p.body && currentCostume === p.costume && currentFace === p.face;
          const preview = p.preview || (p.profile?.preview_image) || (p.costume ? (p.costume.endsWith('.glb') ? p.costume.replace('.glb', '.png') : p.costume) : (p.body?.endsWith('.glb') ? p.body.replace('.glb', '.png') : ''));

          return (
            <div
              key={p.id}
              onClick={() => onApply(p)}
              style={{
                cursor: 'pointer',
                borderRadius: 8,
                padding: 6,
                display: 'flex',
                flexDirection: 'column',
                gap: 6,
                border: isCurrentActive ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                background: isCurrentActive ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
                position: 'relative',
                transition: 'all 0.15s ease',
              }}
            >
              {/* Delete button */}
              <button
                onClick={(e) => onDelete(p.id, e)}
                title="Xóa mẫu phối đồ này"
                style={{
                  position: 'absolute',
                  top: 8,
                  right: 8,
                  zIndex: 5,
                  background: 'rgba(0, 0, 0, 0.75)',
                  border: '1px solid rgba(239, 68, 68, 0.5)',
                  color: '#ef4444',
                  borderRadius: 4,
                  padding: '2px 5px',
                  fontSize: 10,
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
              >
                ✕
              </button>

              <div style={{ width: '100%', height: 85, overflow: 'hidden', borderRadius: 6 }}>
                <Live3DThumbnail assetPath={p.body} previewUrl={preview} altText={p.name} fallbackIcon="✨" format="JSON" height={85} />
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <span style={{ fontSize: 11, fontWeight: 700, color: isCurrentActive ? '#38bdf8' : '#f1f5f9', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {p.name}
                </span>
                <span style={{ fontSize: 9, color: '#64748b' }}>
                  {p.gender === 'female' ? 'Nữ' : 'Nam'} • Đã phối đồ
                </span>
              </div>

              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onApply(p);
                }}
                style={{
                  width: '100%',
                  padding: '4px 0',
                  borderRadius: 4,
                  fontSize: 10,
                  fontWeight: 700,
                  cursor: 'pointer',
                  border: 'none',
                  background: isCurrentActive ? '#38bdf8' : 'rgba(255, 255, 255, 0.08)',
                  color: isCurrentActive ? '#090d16' : '#cbd5e1',
                }}
              >
                {isCurrentActive ? '✓ Đang Dùng' : 'Áp Dụng'}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}

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
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(115px, 1fr))', gap: 10, alignItems: 'stretch' }}>
      {items.map((item) => {
        const isSelected = selectedPath === item.path;
        return (
          <div
            key={item.id || item.path}
            onClick={() => onSelect(item.path)}
            title={item.name}
            style={{
              cursor: 'pointer',
              borderRadius: 8,
              padding: 6,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'stretch',
              justifyContent: 'space-between',
              gap: 6,
              border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
              background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
              position: 'relative',
              boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.25)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <div style={{ width: '100%', height: 75, overflow: 'hidden', borderRadius: 6 }}>
              <Live3DThumbnail assetPath={item.path} previewUrl={item.preview} altText={item.name} fallbackIcon={fallbackIcon} format={item.format || 'GLB'} height={75} />
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, textAlign: 'left' }}>
              <span style={{ fontSize: 11, fontWeight: 600, color: isSelected ? '#38bdf8' : '#e2e8f0', lineHeight: 1.2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {item.name}
              </span>
              <span style={{ fontSize: 9, color: '#64748b' }}>
                {item.format || 'GLB'} • {item.sizeMB || '0.5'} MB
              </span>
            </div>
            {isSelected && (
              <div style={{ position: 'absolute', top: 6, right: 6, width: 16, height: 16, borderRadius: 8, background: '#38bdf8', color: '#000', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 2px 4px rgba(0,0,0,0.5)' }}>
                <Check size={10} strokeWidth={3} />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

function SlidersPanel({
  sliders,
  sliderDefs,
  useShared,
  onToggleShared,
  onChange,
  onReset,
  onSaveCache,
}: {
  sliders: FaceSliderConfig;
  sliderDefs: {
    key: keyof FaceSliderConfig;
    label: string;
    icon: string;
    min: number;
    description: string;
    color: string;
  }[];
  useShared: boolean;
  onToggleShared: () => void;
  onChange: (key: keyof FaceSliderConfig, value: number) => void;
  onReset: () => void;
  onSaveCache: () => void;
}) {
  const quickPresets = [0, 0.25, 0.5, 0.75, 1.0];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {/* Control Header Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '8px 12px',
          borderRadius: 8,
          background: 'rgba(255, 255, 255, 0.04)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          boxShadow: '0 2px 6px rgba(0,0,0,0.2)',
        }}
      >
        <div
          onClick={onToggleShared}
          style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', userSelect: 'none' }}
          title="Chọn áp dụng đồng bộ tất cả nhân vật hoặc riêng cho nhân vật đang chọn"
        >
          {useShared ? <ToggleRight size={18} color="#38bdf8" /> : <ToggleLeft size={18} color="#94a3b8" />}
          <span style={{ fontSize: 11, fontWeight: 700, color: useShared ? '#38bdf8' : '#cbd5e1' }}>
            {useShared ? 'Dùng Chung Cho Mọi Actor' : 'Riêng Cho Actor Này'}
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            onClick={onSaveCache}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 10px',
              borderRadius: 6,
              fontSize: 10,
              fontWeight: 700,
              background: 'rgba(16, 185, 129, 0.2)',
              border: '1px solid rgba(16, 185, 129, 0.5)',
              color: '#34d399',
              cursor: 'pointer',
            }}
            title="Lưu cấu hình thanh trượt vào bộ nhớ trình duyệt"
          >
            <Save size={11} /> Lưu Cache
          </button>
          <button
            onClick={onReset}
            style={{
              padding: '4px 10px',
              borderRadius: 6,
              fontSize: 10,
              fontWeight: 600,
              background: 'rgba(255, 255, 255, 0.06)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              color: '#94a3b8',
              cursor: 'pointer',
            }}
            title="Đặt lại về mặc định (Face cũ 0%, Mũi 0%, Miệng 0%)"
          >
            Đặt Lại Mặc Định
          </button>
        </div>
      </div>

      {/* Sliders List */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {sliderDefs.map((def) => {
          const val = sliders[def.key] ?? (def.key === 'baseFaceOpacity' || def.key === 'noseOpacity' || def.key === 'mouthOpacity' ? 0.0 : 1.0);
          const percent = Math.round(val * 100);

          return (
            <div
              key={def.key}
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: 6,
                padding: '8px 12px',
                borderRadius: 8,
                background: 'rgba(255, 255, 255, 0.025)',
                border: '1px solid rgba(255, 255, 255, 0.06)',
                transition: 'all 0.15s ease',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 14 }}>{def.icon}</span>
                  <div style={{ display: 'flex', flexDirection: 'column' }}>
                    <span style={{ fontSize: 11, fontWeight: 700, color: '#f1f5f9' }}>{def.label}</span>
                    <span style={{ fontSize: 9, color: '#64748b' }}>{def.description}</span>
                  </div>
                </div>

                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    minWidth: 44,
                    padding: '2px 6px',
                    borderRadius: 6,
                    background: percent === 0 ? 'rgba(239, 68, 68, 0.15)' : percent === 100 ? 'rgba(16, 185, 129, 0.15)' : 'rgba(56, 189, 248, 0.15)',
                    border: `1px solid ${percent === 0 ? 'rgba(239, 68, 68, 0.3)' : percent === 100 ? 'rgba(16, 185, 129, 0.3)' : 'rgba(56, 189, 248, 0.3)'}`,
                    color: percent === 0 ? '#f87171' : percent === 100 ? '#34d399' : '#38bdf8',
                    fontSize: 11,
                    fontWeight: 800,
                  }}
                >
                  {percent}%
                </div>
              </div>

              {/* Smooth Range Slider */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 2 }}>
                <input
                  type="range"
                  min={def.min}
                  max={1.0}
                  step={0.01}
                  value={val}
                  onChange={(e) => onChange(def.key, parseFloat(e.target.value))}
                  style={{
                    flex: 1,
                    accentColor: def.color,
                    height: 5,
                    cursor: 'pointer',
                  }}
                />
              </div>

              {/* Quick Preset Buttons */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginTop: 2 }}>
                {quickPresets.map((preset) => {
                  const isPresetActive = Math.abs(val - preset) < 0.03;
                  return (
                    <button
                      key={preset}
                      type="button"
                      onClick={() => onChange(def.key, preset)}
                      style={{
                        flex: 1,
                        padding: '3px 0',
                        fontSize: 9,
                        fontWeight: 700,
                        borderRadius: 4,
                        cursor: 'pointer',
                        border: isPresetActive ? `1px solid ${def.color}` : '1px solid rgba(255, 255, 255, 0.08)',
                        background: isPresetActive ? `rgba(${hexToRgb(def.color)}, 0.25)` : 'rgba(255, 255, 255, 0.02)',
                        color: isPresetActive ? '#ffffff' : '#94a3b8',
                        transition: 'all 0.1s ease',
                      }}
                    >
                      {preset === 0 ? '0% (Ẩn)' : preset === 1 ? '100%' : `${Math.round(preset * 100)}%`}
                    </button>
                  );
                })}
              </div>
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
