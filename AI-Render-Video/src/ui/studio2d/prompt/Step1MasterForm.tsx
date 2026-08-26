import React from 'react';
import { Eye, Shirt, Sparkles, User } from 'lucide-react';
import { AIPartPromptConfig } from '../../../types/scene2d';

interface Step1MasterFormProps {
  config: AIPartPromptConfig;
  setConfig: React.Dispatch<React.SetStateAction<AIPartPromptConfig>>;
}

const ASPECT_RATIO_OPTIONS = [
  { id: '16:9', label: '16:9 Rộng Ngang (Chuẩn Sprite Sheet)' },
  { id: '1:1', label: '1:1 Vuông (Square Sprite / Avatar)' },
  { id: '3:4', label: '3:4 Chân Dung (Portrait Stand)' },
  { id: '4:3', label: '4:3 Ngang Chuẩn (Standard Comic)' },
  { id: '9:16', label: '9:16 Dọc Toàn Thân (Shorts / Reels)' },
];

export const Step1MasterForm: React.FC<Step1MasterFormProps> = ({ config, setConfig }) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div
        style={{
          background: 'rgba(2, 132, 199, 0.12)',
          padding: '8px 12px',
          borderRadius: 8,
          border: '1px solid rgba(2, 132, 199, 0.35)',
          fontSize: 11,
          color: '#e0f2fe',
          lineHeight: 1.4,
        }}
      >
        🌟 <b>Bảng Xoay Nhân Vật Gốc (Turnaround Sheet {config.aspect_ratio || '16:9'})</b> gồm chuỗi 5 góc chuẩn <code>Front → 45° → Side → 135° → Back</code> + 1 góc soi Đỉnh Đầu.
      </div>

      {/* 1. Giới tính & Phong cách nghệ thuật */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.6fr', gap: 8 }}>
        <div>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 3 }}>
            Giới tính (Gender):
          </label>
          <div style={{ display: 'flex', gap: 4 }}>
            <button
              onClick={() => setConfig((p) => ({ ...p, gender: 'nam' }))}
              style={{
                flex: 1,
                height: 32,
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 5,
                border: config.gender === 'nam' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.12)',
                background: config.gender === 'nam' ? '#0284c7' : 'rgba(255,255,255,0.04)',
                color: '#fff',
                cursor: 'pointer',
              }}
            >
              Nam (Male)
            </button>
            <button
              onClick={() => setConfig((p) => ({ ...p, gender: 'nu' }))}
              style={{
                flex: 1,
                height: 32,
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 5,
                border: config.gender === 'nu' ? '1px solid #f472b6' : '1px solid rgba(255,255,255,0.12)',
                background: config.gender === 'nu' ? '#db2777' : 'rgba(255,255,255,0.04)',
                color: '#fff',
                cursor: 'pointer',
              }}
            >
              Nữ (Female)
            </button>
          </div>
        </div>

        <div>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 3 }}>
            🎨 Phong cách (Art Style):
          </label>
          <input
            type="text"
            value={config.character_style || ''}
            onChange={(e) => setConfig((p) => ({ ...p, character_style: e.target.value }))}
            placeholder="Chinese Guoman / 国漫 Xianxia Chibi..."
            style={{
              width: '100%',
              height: 32,
              padding: '4px 8px',
              fontSize: 10.5,
              background: '#090d16',
              color: '#38bdf8',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              borderRadius: 5,
              boxSizing: 'border-box',
            }}
          />
        </div>
      </div>

      {/* 2. Tông Màu Da & Kiểu Khớp Mannequin */}
      <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: 8 }}>
        <div style={{ background: 'rgba(244, 114, 182, 0.05)', padding: 8, borderRadius: 8, border: '1px solid rgba(244, 114, 182, 0.25)' }}>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#f472b6', display: 'block', marginBottom: 3 }}>
            🌸 Tông Màu Da (Skin Tone):
          </label>
          <select
            value={config.skin_tone || 'fair_porcelain_pink'}
            onChange={(e) => setConfig((p) => ({ ...p, skin_tone: e.target.value }))}
            style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fbcfe8', border: '1px solid rgba(244, 114, 182, 0.4)', borderRadius: 5 }}
          >
            <option value="fair_porcelain_pink">🌸 Trắng Hồng Sứ Anime (Fair Porcelain Rosy - Chuẩn Live2D)</option>
            <option value="porcelain_white">⚪ Trắng Sứ BJD (Pure Porcelain White)</option>
            <option value="warm_peach">🍑 Da Đào Tự Nhiên (Warm Peach / Beige)</option>
            <option value="tan_sunkissed">🍫 Da Ngăm Khỏe Khoắn (Tan / Sun-kissed)</option>
          </select>
        </div>

        <div style={{ background: 'rgba(52, 211, 153, 0.05)', padding: 8, borderRadius: 8, border: '1px solid rgba(52, 211, 153, 0.25)' }}>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#34d399', display: 'block', marginBottom: 3 }}>
            ⭕ Khớp Nối & Vạch Cắt (Joints):
          </label>
          <select
            value={config.mannequin_joint_style || 'convex_dome_caps'}
            onChange={(e) => setConfig((p) => ({ ...p, mannequin_joint_style: e.target.value }))}
            style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#6ee7b7', border: '1px solid rgba(52, 211, 153, 0.4)', borderRadius: 5 }}
          >
            <option value="convex_dome_caps">⭕ Chỏm Lồi Vòng Cung (Convex Dome Cap - Chuẩn Rig 2D)</option>
            <option value="arc_lines">〰️ Vạch Cung Chỉ Dẫn (Arc Guide Lines)</option>
            <option value="clean_flat_cut">✂️ Cắt Phẳng Tự Nhiên (Clean Flat Cut)</option>
          </select>
        </div>
      </div>

      {/* 3. Tỷ lệ cơ thể & Tỉ lệ khung hình Aspect Ratio */}
      <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: 8 }}>
        <div style={{ background: 'rgba(255, 255, 255, 0.02)', padding: 8, borderRadius: 8, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', display: 'block', marginBottom: 3 }}>
            📐 Tỷ lệ cơ thể (Proportion):
          </label>
          <select
            value={config.body_proportion || 'chibi_2_5'}
            onChange={(e) => setConfig((p) => ({ ...p, body_proportion: e.target.value }))}
            style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5 }}
          >
            <option value="chibi_2_5">Chibi 2.5 đầu (Đầu to, thân gọn cute - Khuyên dùng)</option>
            <option value="chibi_2_heads">Super Chibi 2 đầu (Siêu dễ thương)</option>
            <option value="anime_standard">Anime tiêu chuẩn 6-7 đầu (Thon thả)</option>
            <option value="heroic_martial">Hiệp khách 7.5 đầu (Oai phong)</option>
          </select>
        </div>

        <div style={{ background: 'rgba(255, 255, 255, 0.02)', padding: 8, borderRadius: 8, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#facc15', display: 'block', marginBottom: 3 }}>
            📺 Tỉ lệ khung hình (Aspect Ratio):
          </label>
          <select
            value={config.aspect_ratio || '16:9'}
            onChange={(e) => setConfig((p) => ({ ...p, aspect_ratio: e.target.value as any }))}
            style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#facc15', border: '1px solid rgba(250, 204, 21, 0.4)', borderRadius: 5 }}
          >
            {ASPECT_RATIO_OPTIONS.map((opt) => (
              <option key={opt.id} value={opt.id}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* 3. Ngũ quan & Khuôn mặt */}
      <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 10, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ fontSize: 11.5, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Eye size={13} /> Khuôn Mặt & Ngũ Quan (Face):
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Dáng Mắt (Eyes):</label>
            <input
              type="text"
              value={config.eye_shape || ''}
              onChange={(e) => setConfig((p) => ({ ...p, eye_shape: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Màu Mắt (Eye Color):</label>
            <input
              type="text"
              value={config.eye_color || ''}
              onChange={(e) => setConfig((p) => ({ ...p, eye_color: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Sống Mũi (Nose):</label>
            <input
              type="text"
              value={config.nose_shape || ''}
              onChange={(e) => setConfig((p) => ({ ...p, nose_shape: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Miệng / Thần Thái (Mouth):</label>
            <input
              type="text"
              value={config.mouth_style || ''}
              onChange={(e) => setConfig((p) => ({ ...p, mouth_style: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
        </div>
      </div>

      {/* 4. Mái tóc */}
      <div style={{ background: 'rgba(236, 72, 153, 0.06)', padding: 10, borderRadius: 8, border: '1px solid rgba(236, 72, 153, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ fontSize: 11.5, fontWeight: 800, color: '#f472b6', display: 'flex', alignItems: 'center', gap: 5 }}>
          💇 Mái Tóc (Hair):
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Màu Tóc (Color):</label>
            <input
              type="text"
              value={config.hair_color || ''}
              onChange={(e) => setConfig((p) => ({ ...p, hair_color: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Kiểu Tóc (Style):</label>
            <input
              type="text"
              value={config.hair_texture || ''}
              onChange={(e) => setConfig((p) => ({ ...p, hair_texture: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Độ Dài Tóc (Length):</label>
            <input
              type="text"
              value={config.hair_length || ''}
              onChange={(e) => setConfig((p) => ({ ...p, hair_length: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Trâm Cài / Phụ Kiện:</label>
            <input
              type="text"
              value={config.hair_accessories || ''}
              onChange={(e) => setConfig((p) => ({ ...p, hair_accessories: e.target.value }))}
              style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
            />
          </div>
        </div>
        <div>
          <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Kiểu Mái Trước (Front Bangs Style):</label>
          <select
            value={config.bangs_style || 'see_through_airy'}
            onChange={(e) => setConfig((p) => ({ ...p, bangs_style: e.target.value }))}
            style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#f472b6', border: '1px solid rgba(244, 114, 182, 0.4)', borderRadius: 5 }}
          >
            <option value="see_through_airy">Mái thưa Hàn Quốc / Anime (See-Through Bangs)</option>
            <option value="blunt_straight">Mái bằng ngang trán (Blunt Straight Bangs)</option>
            <option value="side_swept_7_3">Mái xéo rẽ ngôi 7/3 (Side-Swept Bangs 7/3)</option>
            <option value="curtain_parted_5_5">Mái rẽ đôi ngôi giữa 5/5 (Curtain Center-Parted)</option>
            <option value="hime_cut_tendrils">Mái Hime lọn dài ôm má (Hime Cut & Tendrils)</option>
            <option value="spiky_action">Mái nhọn so le lộn xộn (Spiky Action Bangs)</option>
          </select>
        </div>
      </div>

      {/* 5. Trang phục & Vũ khí */}
      <div style={{ background: 'rgba(168, 85, 247, 0.06)', padding: 10, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ fontSize: 11.5, fontWeight: 800, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Shirt size={13} /> Trang Phục & Vũ Khí (Clothing & Weapon):
        </div>
        <input
          type="text"
          value={config.costume_style || ''}
          onChange={(e) => setConfig((p) => ({ ...p, costume_style: e.target.value }))}
          placeholder="Kiểu trang phục: Hanfu tu tiên trắng bạc, tà áo dài..."
          style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
        />
        <input
          type="text"
          value={config.prop_item || ''}
          onChange={(e) => setConfig((p) => ({ ...p, prop_item: e.target.value }))}
          placeholder="Vũ khí / Pháp bảo: Kiếm tiên phát sáng linh lực..."
          style={{ width: '100%', height: 28, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#facc15', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
        />
      </div>

      {/* 6. Màu nền xuất ảnh */}
      <div>
        <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
          Màu nền kết xuất (Background Chroma Key):
        </label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <button
            onClick={() => setConfig((p) => ({ ...p, bg_type: 'chroma_green' }))}
            style={{
              padding: '6px 8px',
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 5,
              border: config.bg_type === 'chroma_green' ? '1px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
              background: config.bg_type === 'chroma_green' ? 'rgba(34, 197, 94, 0.25)' : 'rgba(255,255,255,0.03)',
              color: config.bg_type === 'chroma_green' ? '#4ade80' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            🟢 Phông Xanh (#00FF00)
          </button>
          <button
            onClick={() => setConfig((p) => ({ ...p, bg_type: 'pure_white' }))}
            style={{
              padding: '6px 8px',
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 5,
              border: config.bg_type === 'pure_white' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
              background: config.bg_type === 'pure_white' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.03)',
              color: config.bg_type === 'pure_white' ? '#7dd3fc' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            ⚪ Nền Trắng (#FFFFFF)
          </button>
        </div>
      </div>

      {/* 7. Quy tắc ràng buộc cuối mỗi prompt (Prompt Rules) */}
      <div style={{ background: 'rgba(255, 255, 255, 0.02)', border: '1px solid rgba(255, 255, 255, 0.08)', borderRadius: 8, padding: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#facc15', display: 'flex', alignItems: 'center', gap: 4 }}>
            📜 Quy tắc ràng buộc (Rules thêm vào cuối mỗi Prompt):
          </label>
          <button
            type="button"
            onClick={() => setConfig((p) => ({ ...p, custom_rules: '' }))}
            style={{ fontSize: 9.5, color: '#94a3b8', background: 'none', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
            title="Dùng quy tắc tự động chuẩn theo màu nền đã cấu hình"
          >
            Mặc định
          </button>
        </div>
        <textarea
          value={config.custom_rules ?? ''}
          onChange={(e) => setConfig((p) => ({ ...p, custom_rules: e.target.value }))}
          placeholder={
            config.bg_type === 'pure_white'
              ? 'Bắt buộc nền trắng tinh khiết (#FFFFFF) phẳng 1 màu, không bóng đổ, nét 2D chuẩn tách nền, tuyệt đối không chữ/watermark...'
              : 'Bắt buộc nền xanh (#00FF00) thuần sắc độ chuẩn, không gradient, không bóng đổ, nét vẽ khép kín, tuyệt đối không chữ/watermark...'
          }
          rows={2}
          style={{
            width: '100%',
            padding: '6px 8px',
            fontSize: 10.5,
            background: '#040711',
            color: '#38bdf8',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            borderRadius: 6,
            boxSizing: 'border-box',
            resize: 'vertical',
            fontFamily: 'inherit',
          }}
        />
        {/* Preset tags */}
        <div style={{ display: 'flex', gap: 4, marginTop: 4, flexWrap: 'wrap' }}>
          <button
            type="button"
            onClick={() =>
              setConfig((p) => ({
                ...p,
                custom_rules:
                  'MANDATORY RULES: Strictly flat solid uniform pure ' +
                  (config.bg_type === 'pure_white' ? 'White (#FFFFFF)' : 'Chroma Green (#00FF00)') +
                  ' background with zero gradients or cast shadows. Crisp 2D lineart, no background objects, no watermark, no text.',
              }))
            }
            style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8', border: '1px solid rgba(56, 189, 248, 0.3)', cursor: 'pointer' }}
          >
            + Nền chuẩn {config.bg_type === 'pure_white' ? '#FFFFFF' : '#00FF00'}
          </button>
          <button
            type="button"
            onClick={() =>
              setConfig((p) => ({
                ...p,
                custom_rules:
                  (p.custom_rules ? p.custom_rules + ' ' : '') +
                  'Strictly NO text, NO labels, NO numbers, NO watermark, NO comic panels.',
              }))
            }
            style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: 'rgba(239, 68, 68, 0.15)', color: '#f87171', border: '1px solid rgba(239, 68, 68, 0.3)', cursor: 'pointer' }}
          >
            + Cấm chữ & watermark
          </button>
        </div>
      </div>
    </div>
  );
};
