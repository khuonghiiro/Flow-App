import React, { useState } from 'react';
import {
  User,
  X,
  AlertCircle,
  Zap,
  FileText,
  Plus,
  Trash2,
  Tag,
  CheckCircle,
} from 'lucide-react';
import { CharacterCategory, CHARACTER_CATEGORIES, CharacterSkillItem, DEFAULT_FACE_SLIDERS } from '../CharacterAssetRegistry';
import { CharacterAssembly, FaceSliderConfig } from '../../types/scene';

export const EDUCATION_PRESETS = [
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

export const ELEMENT_PRESETS = [
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

export const SKILL_TYPE_PRESETS = [
  'Chủ Động',
  'Bị Động',
  'Kiếm Pháp',
  'Thần Thông',
  'Phép Thuật',
  'Nội Công',
  'Thân Pháp',
  'Trận Pháp',
];

export interface CustomAttributeItem {
  key: string;
  value: string;
}

export interface CharacterProfileModalProps {
  mode: 'create' | 'edit';
  charName: string;
  setCharName: (name: string) => void;
  charAge: number | '';
  setCharAge: (age: number | '') => void;
  charGender: 'male' | 'female' | 'unisex';
  setCharGender: (gender: 'male' | 'female' | 'unisex') => void;
  charHeightCm: number;
  setCharHeightCm: (h: number) => void;
  charEducation: string;
  setCharEducation: (edu: string) => void;
  charOccupation: string;
  setCharOccupation: (occ: string) => void;
  charFaction: string;
  setCharFaction: (faction: string) => void;
  charPersonality: string;
  setCharPersonality: (p: string) => void;
  charVoiceStyle: string;
  setCharVoiceStyle: (v: string) => void;
  charPowerLevel: number;
  setCharPowerLevel: (p: number) => void;
  charElement: string;
  setCharElement: (el: string) => void;
  charBiography: string;
  setCharBiography: (bio: string) => void;
  charSkills: CharacterSkillItem[];
  setCharSkills: React.Dispatch<React.SetStateAction<CharacterSkillItem[]>>;
  customAttributes: CustomAttributeItem[];
  setCustomAttributes: React.Dispatch<React.SetStateAction<CustomAttributeItem[]>>;
  categories?: CharacterCategory[];
  assembly?: CharacterAssembly;
  onAssemblyChange?: (assembly: CharacterAssembly) => void;
  sliders?: FaceSliderConfig;
  onSlidersChange?: (sliders: FaceSliderConfig) => void;
  validationError: string | null;
  onClose: () => void;
  onSave: () => void;
  onAutoMeasureHeight: () => void;
  onGenderFilterChange: (gender: 'male' | 'female') => void;
}

export const inputStyle: React.CSSProperties = {
  padding: '6px 10px',
  borderRadius: 6,
  background: '#0f172a',
  border: '1px solid rgba(255,255,255,0.15)',
  color: '#fff',
  fontSize: 11,
  fontWeight: 600,
  outline: 'none',
};

export const CharacterProfileModal: React.FC<CharacterProfileModalProps> = ({
  mode,
  charName,
  setCharName,
  charAge,
  setCharAge,
  charGender,
  setCharGender,
  charHeightCm,
  setCharHeightCm,
  charEducation,
  setCharEducation,
  charOccupation,
  setCharOccupation,
  charFaction,
  setCharFaction,
  charPersonality,
  setCharPersonality,
  charVoiceStyle,
  setCharVoiceStyle,
  charPowerLevel,
  setCharPowerLevel,
  charElement,
  setCharElement,
  charBiography,
  setCharBiography,
  charSkills,
  setCharSkills,
  customAttributes,
  setCustomAttributes,
  categories,
  assembly,
  onAssemblyChange,
  sliders,
  onSlidersChange,
  validationError,
  onClose,
  onSave,
  onAutoMeasureHeight,
  onGenderFilterChange,
}) => {
  const [modalTab, setModalTab] = useState<'basic' | 'combat' | 'lore' | 'appearance'>('basic');

  return (
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
              {mode === 'create'
                ? '➕ Thêm Nhân Vật Mới & Thiết Lập Toàn Bộ Hồ Sơ'
                : '✏️ Thiết Lập Hồ Sơ, Kỹ Năng & Thuộc Tính Động'}
            </span>
          </div>
          <button
            onClick={onClose}
            style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer' }}
          >
            <X size={16} />
          </button>
        </div>

        {/* Modal Navigation Tabs */}
        <div
          style={{
            display: 'flex',
            borderBottom: '1px solid rgba(255,255,255,0.08)',
            background: 'rgba(0,0,0,0.25)',
          }}
        >
          {[
            { id: 'basic', label: '👤 1. Cơ Bản' },
            { id: 'combat', label: '⚡ 2. Kỹ Năng' },
            { id: 'lore', label: '📜 3. Tiểu Sử' },
            { id: 'appearance', label: '👗 4. Lắp Ráp & Slider 3D' },
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
                          if (g.id !== 'unisex') onGenderFilterChange(g.id as any);
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
                      onClick={onAutoMeasureHeight}
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

          {/* TAB 4: 3D MODULAR ASSEMBLY & FACIAL SLIDERS */}
          {modalTab === 'appearance' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              {/* Section 1: Modular Equipped Parts */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
                    👘 Các Bộ Phận Lắp Ráp 3D Đang Trang Bị
                  </span>
                  <span style={{ fontSize: 10, color: '#94a3b8' }}>
                    (Tự động đồng bộ với Xưởng 3D)
                  </span>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  {(categories || CHARACTER_CATEGORIES)
                    .filter((c) => !c.id.startsWith('_'))
                    .map((cat) => {
                      const val = assembly?.[cat.id] || (cat.id === 'than_co_ban' ? assembly?.base_body : cat.id === 'trang_phuc' ? assembly?.costume : cat.id === 'khuon_mat' ? assembly?.face : cat.id === 'kieu_toc' ? assembly?.hairstyle : cat.id === 'kieu_rau' ? assembly?.beard : cat.id === 'phu_kien' && Array.isArray(assembly?.accessories) ? assembly.accessories.join(', ') : undefined);
                      const isEquipped = Boolean(val);
                      const isRequired = cat.id === 'than_co_ban' || cat.id === 'base_body';
                      const strVal = typeof val === 'string' ? val : Array.isArray(val) ? val.join(', ') : '';
                      const cleanName = strVal ? strVal.split('/').pop()?.replace(/\.[^/.]+$/, '').replace(/_/g, ' ') : 'Chưa chọn';

                      return (
                        <div
                          key={cat.id}
                          style={{
                            padding: '6px 8px',
                            borderRadius: 6,
                            background: isEquipped ? 'rgba(56, 189, 248, 0.08)' : 'rgba(255,255,255,0.02)',
                            border: `1px solid ${isEquipped ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.06)'}`,
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            gap: 6,
                          }}
                        >
                          <div style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
                            <span style={{ fontSize: 10, fontWeight: 700, color: isEquipped ? '#38bdf8' : '#64748b' }}>
                              {cat.icon || '📦'} {cat.label}
                            </span>
                            <span
                              style={{
                                fontSize: 10,
                                color: isEquipped ? '#f1f5f9' : '#475569',
                                whiteSpace: 'nowrap',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                                maxWidth: 180,
                              }}
                              title={strVal || 'Chưa chọn'}
                            >
                              {cleanName}
                            </span>
                          </div>

                          {isEquipped && !isRequired && onAssemblyChange && (
                            <button
                              type="button"
                              onClick={() => {
                                if (!assembly) return;
                                const updated = { ...assembly };
                                delete updated[cat.id];
                                if (cat.id === 'than_co_ban') delete updated.base_body;
                                if (cat.id === 'trang_phuc') delete updated.costume;
                                if (cat.id === 'khuon_mat') delete updated.face;
                                if (cat.id === 'kieu_toc') delete updated.hairstyle;
                                if (cat.id === 'kieu_rau') delete updated.beard;
                                if (cat.id === 'long_may') delete updated.eyebrow;
                                if (cat.id === 'mat') delete updated.eye;
                                if (cat.id === 'mui') delete updated.nose;
                                if (cat.id === 'mieng') delete updated.mouth;
                                if (cat.id === 'mu_non') delete updated.hat;
                                if (cat.id === 'giay_dep') delete updated.shoes;
                                if (cat.id === 'phu_kien') delete updated.accessories;
                                onAssemblyChange(updated);
                              }}
                              style={{
                                background: 'rgba(239, 68, 68, 0.15)',
                                border: '1px solid rgba(239, 68, 68, 0.3)',
                                color: '#f87171',
                                borderRadius: 4,
                                padding: '2px 5px',
                                fontSize: 9,
                                fontWeight: 700,
                                cursor: 'pointer',
                              }}
                              title="Gỡ bỏ món này"
                            >
                              Gỡ
                            </button>
                          )}
                        </div>
                      );
                    })}
                </div>
              </div>

              {/* Section 2: Facial Sliders */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 4 }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ fontSize: 11, fontWeight: 700, color: '#fbbf24' }}>
                    🎛️ Cấu Hình Thanh Trượt (Sliders) Khuôn Mặt & Da
                  </span>
                  {onSlidersChange && (
                    <button
                      type="button"
                      onClick={() => onSlidersChange({ ...DEFAULT_FACE_SLIDERS })}
                      style={{
                        background: 'rgba(255, 255, 255, 0.05)',
                        border: '1px solid rgba(255, 255, 255, 0.15)',
                        color: '#cbd5e1',
                        borderRadius: 4,
                        padding: '2px 7px',
                        fontSize: 9,
                        fontWeight: 600,
                        cursor: 'pointer',
                      }}
                    >
                      Đặt Lại Mặc Định
                    </button>
                  )}
                </div>

                {sliders && onSlidersChange ? (
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                    {[
                      { key: 'baseFaceOpacity', label: '🎭 Độ Hiện Face Gốc', val: sliders.baseFaceOpacity ?? 0, color: '#f59e0b' },
                      { key: 'noseOpacity', label: '👃 Độ Nổi Mũi', val: sliders.noseOpacity ?? 0, color: '#8b5cf6' },
                      { key: 'mouthOpacity', label: '👄 Độ Rõ Miệng & Môi', val: sliders.mouthOpacity ?? 0, color: '#ec4899' },
                      { key: 'eyebrowOpacity', label: '🤨 Độ Đậm Lông Mày', val: sliders.eyebrowOpacity ?? 1, color: '#06b6d4' },
                      { key: 'pupilOpacity', label: '✨ Độ Sáng Tròng Mắt', val: sliders.pupilOpacity ?? 1, color: '#38bdf8' },
                      { key: 'skinSmoothness', label: '🌸 Độ Mịn Da', val: sliders.skinSmoothness ?? 0.75, color: '#10b981' },
                      { key: 'costumeOpacity', label: '🥋 Độ Đậm Trang Phục', val: sliders.costumeOpacity ?? 1, color: '#a855f7' },
                    ].map((s) => (
                      <div
                        key={s.key}
                        style={{
                          padding: '6px 8px',
                          borderRadius: 6,
                          background: 'rgba(255,255,255,0.025)',
                          border: '1px solid rgba(255,255,255,0.06)',
                          display: 'flex',
                          flexDirection: 'column',
                          gap: 4,
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <span style={{ fontSize: 10, fontWeight: 700, color: '#f1f5f9' }}>{s.label}</span>
                          <span style={{ fontSize: 10, fontWeight: 800, color: s.color }}>{Math.round(s.val * 100)}%</span>
                        </div>
                        <input
                          type="range"
                          min="0"
                          max="1"
                          step="0.01"
                          value={s.val}
                          onChange={(e) => {
                            const num = parseFloat(e.target.value);
                            onSlidersChange({ ...sliders, [s.key]: num });
                          }}
                          style={{ accentColor: s.color, height: 4, cursor: 'pointer' }}
                        />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div style={{ fontSize: 11, color: '#64748b', textAlign: 'center', padding: 8 }}>
                    Chưa có cấu hình thanh trượt riêng. Sẽ áp dụng mặc định của xưởng.
                  </div>
                )}
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
            onClick={onClose}
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
            onClick={onSave}
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
            {mode === 'create' ? 'Xác Nhận Thêm Nhân Vật' : 'Lưu Toàn Bộ Hồ Sơ'}
          </button>
        </div>
      </div>
    </div>
  );
};
