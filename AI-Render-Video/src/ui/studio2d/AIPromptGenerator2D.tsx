import React, { useState } from 'react';
import {
  Sparkles,
  Copy,
  Check,
  Swords,
  Scissors,
  Grid,
  FileText,
  Globe,
  Smile,
  Eye,
  Shirt,
  Camera,
} from 'lucide-react';
import { AIPartPromptConfig } from '../../types/scene2d';
import { buildAIPromptForPart, AIPromptResult } from '../../core/assets/Asset2DRegistry';

export const AIPromptGenerator2D: React.FC = () => {
  const [activeCategory, setActiveCategory] = useState<'parts' | 'actions'>('parts');
  const [displayLangTab, setDisplayLangTab] = useState<'vietnamese' | 'english' | 'both'>('both');

  // Part Prompt State
  const [partConfig, setPartConfig] = useState<AIPartPromptConfig>({
    sheet_type: 'hair_multi_angle_grid',
    part_type: 'toc_truoc',
    character_style: 'tu_tien_manhua',
    gender: 'nam',
    view_angle: 'all_angles_16_9',
    action_or_expression: 'calm sharp gaze, cultivation focus',
    color_theme: 'cyan and gold trim',
    special_features: 'celestial energy glow, silk ribbons fluttering',
    clean_background: true,
    // Hair
    hair_length: 'long_waist',
    hair_texture: 'straight_silky',
    hair_color: 'jet_black',
    hair_accessories: 'flowing_ribbons',
    // Eyes
    eye_color: 'azure_blue',
    eye_shape: 'sharp_phoenix',
    // Mouth
    mouth_style: 'speaking_cycle',
    // Weapon
    weapon_type: 'flying_sword',
    weapon_element: 'azure_lightning',
  });

  // Action Sequence Generator State
  const [actionType, setActionType] = useState<'combat' | 'dialogue' | 'emotion' | 'eat' | 'transition'>('combat');
  const [actionIntensity, setActionIntensity] = useState<'mild' | 'intense' | 'extreme'>('intense');
  const [copiedPrompt, setCopiedPrompt] = useState<string | null>(null);

  const promptResult: AIPromptResult = buildAIPromptForPart(partConfig);

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedPrompt(id);
    setTimeout(() => setCopiedPrompt(null), 2000);
  };

  /**
   * Generates formatted prompt & camera instructions for AI Action sequences (4K)
   */
  const generateActionSequencePrompt = () => {
    switch (actionType) {
      case 'combat':
        return {
          title: 'Chiến Đấu & Tung Chiêu (Combat Slash & Spell Cast)',
          promptEn: `masterpiece, dynamic 2D anime motion comic keyframe, martial arts cultivation sword slash, glowing azure energy wave cutting through air, dramatic low angle, high contrast motion blur, particles flying, character swinging sword forward, ${actionIntensity === 'extreme' ? 'massive spiritual shockwave, ground cracking' : 'sharp sword aura'}, 4k resolution, flat clean cel shading, --ar 16:9`,
          promptVi: `【 CẢNH CHIẾN ĐẤU & TUNG CHIÊU KIẾM KHÍ (4K - 16:9) 】\n• Mô tả: Vung kiếm chém ra luồng kiếm khí lam ngọc xé toang không gian, góc máy thấp kịch tính, vệt sáng chuyển động và bụi tiên khí bay tung tóe.\n• Mức độ: ${actionIntensity === 'extreme' ? 'Cực đại (Nứt đất nổ trời)' : 'Mạnh mẽ'}.`,
          neg: `blurry, photorealistic 3D, deformed anatomy, extra limbs, watermark, text`,
          cameraGuide: `Góc máy: Jump-cut cận cảnh (1.4x zoom) -> Rung lắc Camera Shake (intensity: 0.8, 0.4s) -> Lia nhanh sang đối thủ bị trúng đòn.`,
        };
      case 'emotion':
        return {
          title: 'Biểu Cảm & Cảm Xúc (Emotions / Close-up Reactions)',
          promptEn: `masterpiece, close-up anime character facial expression, ${partConfig.gender === 'nam' ? 'handsome male cultivator' : 'female heroine'}, extreme emotion: shock and intense realization, widened pupils, sweat drops, wind blowing hair, glowing eyes, cinematic anime close-up shot, 4k resolution, --ar 16:9`,
          promptVi: `【 CẢNH BIỂU CẢM CẬN CẢNH (4K - 16:9) 】\n• Mô tả: Cận cảnh khuôn mặt biểu cảm sốc và bàng hoàng, đồng tử co dãn, giọt mồ hôi lăn nhẹ, tóc bay trước gió, ánh mắt lóe sáng linh lực.`,
          neg: `deformed eyes, asymmetrical pupils, blurry, watermark`,
          cameraGuide: `Góc máy: Close-up trực diện mặt (Zoom 1.6x) -> Đổi layer mắt sang tức giận/kinh ngạc -> Phát âm thanh SFX chấn động tâm can.`,
        };
      case 'eat':
        return {
          title: 'Ăn Uống / Thưởng Trà (Eating / Casual Slice of Life)',
          promptEn: `masterpiece, casual slice of life anime scene, cultivation tea house, character holding steaming tea cup / chopsticks with delicious dumplings, happy warm expression, steam rising, warm cozy ambient lighting, isolated character layer with prop, 4k resolution, --ar 16:9`,
          promptVi: `【 CẢNH ĂN UỐNG & THƯỞNG TRÀ (4K - 16:9) 】\n• Mô tả: Nhân vật ngồi trong quán trà tu tiên, tay cầm chén trà bốc khói hoặc gắp bánh bao, biểu cảm vui vẻ thư thái, khói nóng lượn lờ.`,
          neg: `extra fingers, distorted food, messy background`,
          cameraGuide: `Góc máy: Trung cảnh (Medium Shot) -> Đổi khẩu hình miệng mở/nhai theo chu kỳ nhịp nhàng -> Nhạc nền êm dịu.`,
        };
      case 'transition':
        return {
          title: 'Chuyển Cảnh Bản Đồ (Map Transition / Jump Cut)',
          promptEn: `panoramic 2D layered parallax background, ancient celestial mountains with floating jade temples, sunset golden glow, mist rolling through bamboo forest, clean separated foreground trees and background clouds, 16:9 aspect ratio, 4k wallpaper`,
          promptVi: `【 CHUYỂN CẢNH BẢN ĐỒ TIÊN CẢNH (4K - 16:9) 】\n• Mô tả: Phong cảnh núi tiên hùng vĩ với đền ngọc bồng bềnh, ánh hoàng hôn buông xuống rừng trúc, mây mù cuộn trôi, các lớp tiền cảnh và hậu cảnh phân tách rõ ràng.`,
          neg: `characters, humans, blurry, low resolution`,
          cameraGuide: `Góc máy: Parallax trôi chậm (Hậu cảnh 0.2x, Trung cảnh 1.0x, Tiền cảnh 1.8x) -> Hiệu ứng lóe sáng trắng chuyển cảnh.`,
        };
      default:
        return {
          title: 'Đối Thoại (Dialogue Taunt)',
          promptEn: `anime character half body speaking pose, confident smirk, hand gesture pointing forward, wind blowing daoist robes, isolated puppet ready, 4k resolution, --ar 16:9`,
          promptVi: `【 ĐỐI THOẠI & KHIÊU KHÍCH (4K - 16:9) 】\n• Mô tả: Nhân vật nửa người tư thế nói chuyện tự tin, ngón tay chỉ về phía trước, tà áo đạo bào bay trong gió.`,
          neg: `blurry, deformed hands, noisy`,
          cameraGuide: `Góc máy: Trung cảnh nửa người -> Kích hoạt Mouth Talk Cycle đồng bộ với giọng lồng tiếng Voice TTS.`,
        };
    }
  };

  const currentAction = generateActionSequencePrompt();

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', gap: 16, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: Configuration Panel ───────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, overflowY: 'auto', paddingRight: 4 }}>
        {/* Category Tabs */}
        <div style={{ display: 'flex', gap: 6, background: 'rgba(15, 23, 42, 0.7)', padding: 4, borderRadius: 8 }}>
          <button
            onClick={() => setActiveCategory('parts')}
            style={{
              flex: 1,
              padding: '6px 8px',
              fontSize: 11,
              fontWeight: 600,
              borderRadius: 6,
              border: 'none',
              background: activeCategory === 'parts' ? '#0284c7' : 'transparent',
              color: '#ffffff',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
            }}
          >
            <Grid size={13} /> Bảng Linh Kiện 16:9 (Max 4K)
          </button>
          <button
            onClick={() => setActiveCategory('actions')}
            style={{
              flex: 1,
              padding: '6px 8px',
              fontSize: 11,
              fontWeight: 600,
              borderRadius: 6,
              border: 'none',
              background: activeCategory === 'actions' ? '#a855f7' : 'transparent',
              color: '#ffffff',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
            }}
          >
            <Swords size={13} /> Kịch Bản & Hành Động
          </button>
        </div>

        {activeCategory === 'parts' ? (
          /* Part Prompt Options */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {/* Sheet Type Selector */}
            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4, fontWeight: 700 }}>
                Chọn Loại Bảng Nguyên Liệu Cắt Ghép:
              </label>
              <select
                value={partConfig.sheet_type || 'hair_multi_angle_grid'}
                onChange={(e) => setPartConfig((p) => ({ ...p, sheet_type: e.target.value as any }))}
                style={{ width: '100%', padding: '7px 8px', fontSize: 11, background: '#0f172a', color: '#38bdf8', border: '1px solid #0284c7', borderRadius: 6, fontWeight: 600 }}
              >
                <option value="hair_multi_angle_grid">💇 Bảng Tóc Đa Góc 4 Dãy (Mái, Đỉnh, Sau, Mai tai)</option>
                <option value="eyes_grid">👁️ Bảng Đôi Mắt & Chớp Mắt (Mắt mở, Nhắm, Cảm xúc, Mày)</option>
                <option value="mouth_grid">👄 Bảng Khẩu Hình Miệng & Đối Thoại (Nói A/O/I, Cười, Quát)</option>
                <option value="nose_chin_grid">👃 Bảng Sống Mũi, Cằm Nhọn 90° & Tai (Mũi, Cằm, Tai, Ấn)</option>
                <option value="costume_grid">🥋 Bảng Trang Phục & Đạo Bào 4 Hướng (Cổ, Tà, Tay, Thắt lưng)</option>
                <option value="weapons_grid">⚔️ Bảng Vũ Khí, Pháp Bảo & Kiếm Khí (Kiếm, Chuôi, Aura, Cầm)</option>
                <option value="limbs_hands_grid">🦾 Bảng Tứ Chi & Bàn Tay Bắt Quyết (Tay, Ấn quyết, Chân, Hài)</option>
                <option value="body_turnaround_grid">🌟 Bảng Toàn Thân Nhân Vật 4 Hướng (Turnaround)</option>
                <option value="single_part">📦 Linh Kiện Đơn Lẻ</option>
              </select>
            </div>

            {/* 1. Hair Detailed Options */}
            {partConfig.sheet_type === 'hair_multi_angle_grid' && (
              <div style={{ background: 'rgba(56, 189, 248, 0.05)', padding: 10, borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.2)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>💇 Tùy Chọn Chi Tiết Tóc:</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Độ dài:</label>
                    <select
                      value={partConfig.hair_length || 'long_waist'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, hair_length: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="short">Tóc ngắn cá tính</option>
                      <option value="medium_shoulder">Ngang vai tỉa tầng</option>
                      <option value="long_waist">Dài ngang lưng</option>
                      <option value="very_long_flowing">Dài chấm gót tiên hiệp</option>
                      <option value="top_knot_daoist">Búi tóc cao đạo sĩ</option>
                    </select>
                  </div>

                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Dáng tóc / Xoăn:</label>
                    <select
                      value={partConfig.hair_texture || 'straight_silky'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, hair_texture: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="straight_silky">Thẳng mượt suối lụa</option>
                      <option value="wavy_curls">Xoăn sóng bồng bềnh</option>
                      <option value="wild_spiky">Đánh rối hoang dã</option>
                      <option value="braided_traditional">Tết bím cổ trang</option>
                    </select>
                  </div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Màu sắc:</label>
                    <select
                      value={partConfig.hair_color || 'jet_black'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, hair_color: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="jet_black">Đen tuyền óng ả</option>
                      <option value="silver_white">Bạch kim (Trắng bạc)</option>
                      <option value="crimson_red">Đỏ rực hỏa diệm</option>
                      <option value="azure_blue">Xanh lam ngọc bích</option>
                      <option value="chestnut_brown">Nâu hạt dẻ</option>
                      <option value="golden_blonde">Vàng kim rực rỡ</option>
                      <option value="mystic_purple">Tím huyền bí</option>
                    </select>
                  </div>

                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Phụ kiện:</label>
                    <select
                      value={partConfig.hair_accessories || 'flowing_ribbons'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, hair_accessories: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="none">Không có</option>
                      <option value="flowing_ribbons">Dải lụa bay bồng bềnh</option>
                      <option value="jade_hairpin">Trâm cài ngọc bích</option>
                      <option value="golden_crown">Vương miện vàng kim</option>
                    </select>
                  </div>
                </div>
              </div>
            )}

            {/* 2. Eyes Detailed Options */}
            {partConfig.sheet_type === 'eyes_grid' && (
              <div style={{ background: 'rgba(56, 189, 248, 0.05)', padding: 10, borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.2)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>👁️ Tùy Chọn Đôi Mắt:</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Màu tròng mắt:</label>
                    <select
                      value={partConfig.eye_color || 'azure_blue'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, eye_color: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="azure_blue">Xanh lam ngọc phát sáng</option>
                      <option value="emerald_green">Xanh lục ngọc bích</option>
                      <option value="crimson_red">Đỏ tử thần / Hỏa nhãn</option>
                      <option value="golden_amber">Vàng hổ phách</option>
                      <option value="mystic_purple">Tím tử vi</option>
                      <option value="obsidian_black">Đen tuyền sâu thẳm</option>
                    </select>
                  </div>

                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Dáng mắt:</label>
                    <select
                      value={partConfig.eye_shape || 'sharp_phoenix'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, eye_shape: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="sharp_phoenix">Mắt phượng sắc bén</option>
                      <option value="cold_swordsman">Mắt kiếm khách lạnh lùng</option>
                      <option value="large_clear">Mắt to tròn trong trẻo</option>
                      <option value="fox_alluring">Mắt hồ ly quyến rũ</option>
                    </select>
                  </div>
                </div>
              </div>
            )}

            {/* 3. Weapons Detailed Options */}
            {partConfig.sheet_type === 'weapons_grid' && (
              <div style={{ background: 'rgba(56, 189, 248, 0.05)', padding: 10, borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.2)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>⚔️ Tùy Chọn Vũ Khí & Pháp Bảo:</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Loại vũ khí:</label>
                    <select
                      value={partConfig.weapon_type || 'flying_sword'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, weapon_type: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="flying_sword">Phi Kiếm Tiên Hiệp</option>
                      <option value="broadsword">Đao Lớn Trảm Ma</option>
                      <option value="staff">Trượng Pháp Bảo</option>
                      <option value="feather_fan">Quạt Lông Vũ Tiên Gia</option>
                    </select>
                  </div>

                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8' }}>Nguyên tố Kiếm khí:</label>
                    <select
                      value={partConfig.weapon_element || 'azure_lightning'}
                      onChange={(e) => setPartConfig((p) => ({ ...p, weapon_element: e.target.value as any }))}
                      style={{ width: '100%', padding: '4px', fontSize: 10, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4 }}
                    >
                      <option value="azure_lightning">Lôi điện xanh lam</option>
                      <option value="crimson_flame">Hỏa diệm rực đỏ</option>
                      <option value="frost_ice">Băng tuyết hàn băng</option>
                      <option value="golden_radiance">Vàng kim thái dương</option>
                    </select>
                  </div>
                </div>
              </div>
            )}

            {/* Art Style */}
            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4 }}>
                Phong cách đồ họa:
              </label>
              <select
                value={partConfig.character_style}
                onChange={(e) => setPartConfig((p) => ({ ...p, character_style: e.target.value as any }))}
                style={{ width: '100%', padding: '6px 8px', fontSize: 11, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
              >
                <option value="tu_tien_manhua">Tu Tiên / Manhua Trung Quốc (Dynamic Comic)</option>
                <option value="anime_action">Anime Nhật Bản (Action Crisp Lines)</option>
                <option value="kiem_hiep">Kiếm Hiệp / Wuxia Cổ Trang</option>
                <option value="cyberpunk_anime">Cyberpunk Anime</option>
                <option value="chibi">Chibi Dễ Thương</option>
              </select>
            </div>

            {/* Gender */}
            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4 }}>
                Giới tính:
              </label>
              <div style={{ display: 'flex', gap: 6 }}>
                <button
                  onClick={() => setPartConfig((p) => ({ ...p, gender: 'nam' }))}
                  style={{
                    flex: 1,
                    padding: '5px',
                    fontSize: 11,
                    borderRadius: 5,
                    border: '1px solid rgba(255,255,255,0.1)',
                    background: partConfig.gender === 'nam' ? '#0284c7' : 'rgba(255,255,255,0.03)',
                    color: '#fff',
                    cursor: 'pointer',
                  }}
                >
                  Nam (Male)
                </button>
                <button
                  onClick={() => setPartConfig((p) => ({ ...p, gender: 'nu' }))}
                  style={{
                    flex: 1,
                    padding: '5px',
                    fontSize: 11,
                    borderRadius: 5,
                    border: '1px solid rgba(255,255,255,0.1)',
                    background: partConfig.gender === 'nu' ? '#ec4899' : 'rgba(255,255,255,0.03)',
                    color: '#fff',
                    cursor: 'pointer',
                  }}
                >
                  Nữ (Female)
                </button>
              </div>
            </div>

            {/* Background Color Mode */}
            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4, fontWeight: 700 }}>
                Màu Nền Tách Ảnh (Dành cho AI / Banana Pro):
              </label>
              <div style={{ display: 'flex', gap: 6 }}>
                <button
                  onClick={() => setPartConfig((p) => ({ ...p, bg_type: 'chroma_green' }))}
                  style={{
                    flex: 1,
                    padding: '6px 8px',
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: partConfig.bg_type === 'chroma_green' || !partConfig.bg_type ? '1px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
                    background: partConfig.bg_type === 'chroma_green' || !partConfig.bg_type ? 'rgba(34, 197, 94, 0.15)' : 'rgba(255,255,255,0.03)',
                    color: partConfig.bg_type === 'chroma_green' || !partConfig.bg_type ? '#4ade80' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🟢 Nền Xanh Chroma (#00FF00) (Khuyên dùng)
                </button>
                <button
                  onClick={() => setPartConfig((p) => ({ ...p, bg_type: 'pure_white' }))}
                  style={{
                    flex: 1,
                    padding: '6px 8px',
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: partConfig.bg_type === 'pure_white' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: partConfig.bg_type === 'pure_white' ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.03)',
                    color: partConfig.bg_type === 'pure_white' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  ⚪ Nền Trắng Tinh (#FFFFFF)
                </button>
              </div>
            </div>

            {/* Color Theme */}
            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4 }}>
                Tông màu chủ đạo:
              </label>
              <input
                type="text"
                value={partConfig.color_theme}
                onChange={(e) => setPartConfig((p) => ({ ...p, color_theme: e.target.value }))}
                placeholder="Ví dụ: Xanh lam viền vàng kim"
                style={{ width: '100%', padding: '6px 8px', fontSize: 11, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
              />
            </div>
          </div>
        ) : (
          /* Action Sequences Generator */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4 }}>
                Loại hành động kịch bản:
              </label>
              <select
                value={actionType}
                onChange={(e) => setActionType(e.target.value as any)}
                style={{ width: '100%', padding: '6px 8px', fontSize: 11, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
              >
                <option value="combat">⚔️ Chiến đấu & Tung chiêu kiếm khí</option>
                <option value="emotion">😱 Biểu cảm giật mình / Tức giận / Sốc</option>
                <option value="dialogue">💬 Đối thoại khẩu khí / Nói chuyện</option>
                <option value="eat">🍵 Ăn uống / Thưởng trà / Thư giãn</option>
                <option value="transition">🗺️ Chuyển cảnh bản đồ (Map Cut)</option>
              </select>
            </div>

            <div>
              <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4 }}>
                Mức độ uy lực / Cường độ:
              </label>
              <div style={{ display: 'flex', gap: 6 }}>
                {(['mild', 'intense', 'extreme'] as const).map((lvl) => (
                  <button
                    key={lvl}
                    onClick={() => setActionIntensity(lvl)}
                    style={{
                      flex: 1,
                      padding: '5px',
                      fontSize: 10,
                      borderRadius: 5,
                      border: '1px solid rgba(255,255,255,0.1)',
                      background: actionIntensity === lvl ? '#a855f7' : 'rgba(255,255,255,0.03)',
                      color: '#fff',
                      cursor: 'pointer',
                    }}
                  >
                    {lvl === 'mild' ? 'Nhẹ' : lvl === 'intense' ? 'Mạnh' : 'Cực Đại'}
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ─── RIGHT: Generated Dual Language Prompt & Copy ─────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 16, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        {/* Top Header & Copy Action */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 13, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sparkles size={15} /> PROMPT TÁCH LỚP 4K (16:9) CHO TỪNG LOẠI LINH KIỆN
          </div>

          <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6 }}>
            <button
              onClick={() => setDisplayLangTab('both')}
              style={{ padding: '3px 8px', fontSize: 10, borderRadius: 4, border: 'none', background: displayLangTab === 'both' ? '#0284c7' : 'transparent', color: displayLangTab === 'both' ? '#fff' : '#94a3b8', cursor: 'pointer' }}
            >
              Song Ngữ
            </button>
            <button
              onClick={() => setDisplayLangTab('vietnamese')}
              style={{ padding: '3px 8px', fontSize: 10, borderRadius: 4, border: 'none', background: displayLangTab === 'vietnamese' ? '#0284c7' : 'transparent', color: displayLangTab === 'vietnamese' ? '#fff' : '#94a3b8', cursor: 'pointer' }}
            >
              Tiếng Việt
            </button>
            <button
              onClick={() => setDisplayLangTab('english')}
              style={{ padding: '3px 8px', fontSize: 10, borderRadius: 4, border: 'none', background: displayLangTab === 'english' ? '#0284c7' : 'transparent', color: displayLangTab === 'english' ? '#fff' : '#94a3b8', cursor: 'pointer' }}
            >
              Tiếng Anh
            </button>
          </div>
        </div>

        {/* Banana Pro & Transparent Alpha Info Banner */}
        <div style={{ background: 'rgba(34, 197, 94, 0.08)', padding: '8px 12px', borderRadius: 6, border: '1px solid rgba(34, 197, 94, 0.25)', fontSize: 11, color: '#86efac', display: 'flex', alignItems: 'center', gap: 8 }}>
          <span>💡 <b>Quy trình Tách Nền Trong Suốt cho Banana Pro / AI:</b> Các AI tạo ảnh luôn xuất định dạng RGB (không có kênh Alpha trong suốt trực tiếp). Bạn chỉ cần tạo ảnh trên <b>Nền Xanh Chroma</b> hoặc <b>Nền Trắng</b> $\rightarrow$ nạp ảnh vào tab <b>✂️ Tách Nền & Cắt Khung</b> trong app, hệ thống sẽ tự động bóc tách thành PNG trong suốt 100% không tì vết!</span>
        </div>

        {/* Quick Copy Main English Prompt with Negative & 4K 16:9 */}
        <div style={{ display: 'flex', gap: 8 }}>
          <button
            onClick={() => handleCopy(activeCategory === 'parts' ? promptResult.fullCopyText : `${currentAction.promptEn}\n\nNegative prompt:\n${currentAction.neg}`, 'full_en')}
            style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              padding: '8px 12px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: copiedPrompt === 'full_en' ? '#22c55e' : 'linear-gradient(135deg, #0284c7, #0369a1)',
              color: '#fff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(2, 132, 199, 0.3)',
            }}
          >
            {copiedPrompt === 'full_en' ? <Check size={13} /> : <Copy size={13} />}
            {copiedPrompt === 'full_en' ? 'Đã Sao Chép Tiếng Anh (Kèm Khử Lỗi)!' : '📋 Sao Chép Prompt Tiếng Anh (Kèm Khử Lỗi & 4K)'}
          </button>

          <button
            onClick={() => handleCopy(activeCategory === 'parts' ? promptResult.promptVietnamese : currentAction.promptVi, 'vi')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '8px 12px',
              fontSize: 11,
              fontWeight: 600,
              borderRadius: 6,
              background: copiedPrompt === 'vi' ? '#22c55e' : 'rgba(255,255,255,0.06)',
              color: '#e2e8f0',
              border: '1px solid rgba(255,255,255,0.1)',
              cursor: 'pointer',
            }}
          >
            {copiedPrompt === 'vi' ? <Check size={12} /> : <FileText size={12} />}
            {copiedPrompt === 'vi' ? 'Đã Chép!' : 'Chép Tiếng Việt'}
          </button>
        </div>

        {/* Vietnamese Translation / Structure Breakdown */}
        {(displayLangTab === 'vietnamese' || displayLangTab === 'both') && (
          <div style={{ background: '#0b1329', padding: 12, borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.3)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
              <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                <Globe size={12} /> Bản Dịch Tiếng Việt & Cấu Trúc 4 Dãy (User Đọc Hiểu):
              </span>
            </div>
            <pre style={{ fontSize: 11, color: '#e2e8f0', lineHeight: 1.6, margin: 0, whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>
              {activeCategory === 'parts' ? promptResult.promptVietnamese : currentAction.promptVi}
            </pre>
          </div>
        )}

        {/* English Positive Prompt Box */}
        {(displayLangTab === 'english' || displayLangTab === 'both') && (
          <div style={{ background: '#090d16', padding: 12, borderRadius: 6, border: '1px solid rgba(255,255,255,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
              <span style={{ fontSize: 11, fontWeight: 600, color: '#4ade80' }}>Prompt Tiếng Anh (Cho Midjourney / FLUX / SD - Max 4K):</span>
              <button
                onClick={() => handleCopy(activeCategory === 'parts' ? promptResult.promptEnglish : currentAction.promptEn, 'pos_only')}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', cursor: 'pointer' }}
              >
                {copiedPrompt === 'pos_only' ? 'Đã Chép!' : 'Chép Đoạn Này'}
              </button>
            </div>
            <p style={{ fontSize: 11, color: '#e2e8f0', lineHeight: 1.5, margin: 0, userSelect: 'all', wordBreak: 'break-word' }}>
              {activeCategory === 'parts' ? promptResult.promptEnglish : currentAction.promptEn}
            </p>
          </div>
        )}

        {/* Negative Prompt (Khử lỗi) Box */}
        {(displayLangTab === 'english' || displayLangTab === 'both') && (
          <div style={{ background: '#090d16', padding: 12, borderRadius: 6, border: '1px solid rgba(239, 68, 68, 0.3)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
              <span style={{ fontSize: 11, fontWeight: 600, color: '#f87171' }}>Khử Lỗi (Negative Prompt):</span>
              <button
                onClick={() => handleCopy(activeCategory === 'parts' ? promptResult.negativePrompt : currentAction.neg, 'neg_only')}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', cursor: 'pointer' }}
              >
                {copiedPrompt === 'neg_only' ? 'Đã Chép!' : 'Chép Negative'}
              </button>
            </div>
            <p style={{ fontSize: 10, color: '#94a3b8', lineHeight: 1.4, margin: 0, userSelect: 'all' }}>
              {activeCategory === 'parts' ? promptResult.negativePrompt : currentAction.neg}
            </p>
          </div>
        )}

        {/* Grid Slicing Specifications */}
        {activeCategory === 'parts' && (
          <div style={{ background: 'rgba(168, 85, 247, 0.08)', padding: 10, borderRadius: 6, border: '1px dashed rgba(168, 85, 247, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontSize: 10, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 6 }}>
              <Scissors size={13} /> {promptResult.gridStructureGuide}
            </div>
          </div>
        )}

        {/* Camera Guidelines in Action mode */}
        {activeCategory === 'actions' && (
          <div style={{ background: 'rgba(168, 85, 247, 0.1)', padding: 12, borderRadius: 6, border: '1px solid rgba(168, 85, 247, 0.3)' }}>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#c084fc', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
              <Camera size={13} /> HƯỚNG DẪN GÓC MÁY & DIỄN HOẠT TRONG STUDIO
            </div>
            <p style={{ fontSize: 11, color: '#e2e8f0', margin: 0, lineHeight: 1.5 }}>
              {currentAction.cameraGuide}
            </p>
          </div>
        )}
      </div>
    </div>
  );
};
