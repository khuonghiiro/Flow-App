import React, { useState } from 'react';
import {
  Sparkles,
  Copy,
  Check,
  Swords,
  Scissors,
  User,
  FileText,
  Globe,
  Eye,
  Shirt,
  Camera,
  Shield,
  Sparkle,
  Link,
  Layers,
  Edit3,
  Maximize2,
} from 'lucide-react';
import { AIPartPromptConfig } from '../../types/scene2d';
import { buildAIPromptForPart, AIPromptResult } from '../../core/assets/Asset2DRegistry';

export const AIPromptGenerator2D: React.FC = () => {
  const [workflowTab, setWorkflowTab] = useState<'step1_master' | 'step2_decomposed_parts' | 'step3_actions'>('step1_master');
  const [decomposedPartType, setDecomposedPartType] = useState<'hair_multi_angle_grid' | 'eyes_grid' | 'mouth_grid' | 'nose_chin_grid' | 'costume_grid' | 'weapons_grid' | 'limbs_hands_grid'>('hair_multi_angle_grid');
  const [displayLangTab, setDisplayLangTab] = useState<'vietnamese' | 'english' | 'json' | 'gemini' | 'both'>('both');
  const [referenceImageUrl, setReferenceImageUrl] = useState<string>('');

  // Master Character & Part Config State
  const [config, setConfig] = useState<AIPartPromptConfig>({
    workflow_step: 'step1_master_character',
    sheet_type: 'hair_multi_angle_grid',
    part_type: 'toc_truoc',
    character_style: 'tu_tien_manhua',
    custom_character_style: '',
    gender: 'nam',
    view_angle: 'all_angles_16_9',
    action_or_expression: 'calm sharp gaze, cultivation focus',
    color_theme: 'cyan and gold trim',
    special_features: 'celestial energy glow, silk ribbons fluttering',
    clean_background: true,
    aspect_ratio: '16:9',
    bg_type: 'chroma_green',
    // Five Senses & Facial (Step 1)
    eye_color: 'azure_blue',
    custom_eye_color: '',
    eye_shape: 'sharp_phoenix',
    custom_eye_shape: '',
    nose_shape: 'straight_high_bridge',
    custom_nose_shape: '',
    mouth_style: 'confident_smirk',
    custom_mouth_style: '',
    ear_style: 'human_natural',
    // Costume & Robes (Step 1)
    costume_style: 'dao_bao_tien_hiep',
    custom_costume_style: '',
    costume_color: 'cyan and white with gold accents',
    // Weapon & Prop Item (Step 1)
    prop_item: 'flying_sword',
    custom_prop_item: '',
    // Hair (Step 1 - Master Character Hair)
    hair_length: 'long_waist',
    custom_hair_length: '',
    hair_texture: 'straight_silky',
    custom_hair_texture: '',
    hair_color: 'jet_black',
    custom_hair_color: '',
    hair_accessories: 'jade_hairpin',
    custom_hair_accessories: '',
  });

  // Action Sequence Generator State (Step 3)
  const [actionType, setActionType] = useState<'combat' | 'dialogue' | 'emotion' | 'eat' | 'transition'>('combat');
  const [actionIntensity, setActionIntensity] = useState<'mild' | 'intense' | 'extreme'>('intense');
  const [copiedPrompt, setCopiedPrompt] = useState<string | null>(null);

  // Derive Effective Prompt Config based on active workflow step
  const effectiveConfig: AIPartPromptConfig = {
    ...config,
    workflow_step:
      workflowTab === 'step1_master'
        ? 'step1_master_character'
        : workflowTab === 'step2_decomposed_parts'
        ? 'step2_decomposed_parts'
        : undefined,
    sheet_type: workflowTab === 'step1_master' ? 'body_turnaround_grid' : decomposedPartType,
    aspect_ratio: config.aspect_ratio || (workflowTab === 'step1_master' ? '16:9' : decomposedPartType !== 'hair_multi_angle_grid' ? '16:9' : '1:1'),
  };

  const promptResult: AIPromptResult = buildAIPromptForPart(effectiveConfig);

  // If user provided a reference image URL in Step 2, append --sref to Midjourney prompt
  const finalPromptEnglish =
    workflowTab === 'step2_decomposed_parts' && referenceImageUrl.trim()
      ? promptResult.promptEnglish.includes('--ar')
        ? promptResult.promptEnglish.replace('--ar', `--sref ${referenceImageUrl.trim()} --ar`)
        : `${promptResult.promptEnglish} --sref ${referenceImageUrl.trim()}`
      : promptResult.promptEnglish;

  const finalFullCopyText =
    workflowTab === 'step2_decomposed_parts' && referenceImageUrl.trim()
      ? `${finalPromptEnglish}\n\nNegative prompt:\n${promptResult.negativePrompt}`
      : promptResult.fullCopyText;

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
          promptEn: `masterpiece, close-up anime character facial expression, ${config.gender === 'nam' ? 'handsome male cultivator' : 'female heroine'}, extreme emotion: shock and intense realization, widened pupils, sweat drops, wind blowing hair, glowing eyes, cinematic anime close-up shot, 4k resolution, --ar 16:9`,
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

  // Friendly human text for inherited hair properties
  const getInheritedHairText = () => {
    const color = config.hair_color === 'custom' ? config.custom_hair_color : config.hair_color === 'silver_white' ? 'Bạch kim (Trắng bạc)' : config.hair_color === 'crimson_red' ? 'Đỏ rực hỏa diệm' : config.hair_color === 'azure_blue' ? 'Xanh lam ngọc' : 'Đen tuyền óng ả';
    const length = config.hair_length === 'custom' ? config.custom_hair_length : config.hair_length === 'very_long_flowing' ? 'Dài chấm gót tiên hiệp' : config.hair_length === 'medium_shoulder' ? 'Ngang vai tỉa tầng' : config.hair_length === 'short' ? 'Tóc ngắn cá tính' : 'Dài ngang lưng';
    const texture = config.hair_texture === 'custom' ? config.custom_hair_texture : config.hair_texture === 'wavy_curls' ? 'Xoăn sóng bồng bềnh' : config.hair_texture === 'wild_spiky' ? 'Đánh rối hoang dã' : 'Thẳng mượt suối lụa';
    const acc = config.hair_accessories === 'custom' ? config.custom_hair_accessories : config.hair_accessories === 'golden_crown' ? 'Vương miện vàng' : config.hair_accessories === 'flowing_ribbons' ? 'Dải lụa bay' : config.hair_accessories === 'none' ? 'Không có' : 'Trâm cài ngọc / Bạc';
    return { color, length, texture, acc };
  };

  const inheritedHair = getInheritedHairText();

  // Helper to render Aspect Ratio Selector with smart recommendation
  const renderAspectRatioSelector = () => {
    const currentAr = config.aspect_ratio || (workflowTab === 'step1_master' ? '16:9' : '1:1');
    const isLongHair = config.hair_length === 'very_long_flowing' || config.hair_length === 'long_waist';

    return (
      <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 12.5, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Maximize2 size={14} /> Tỉ Lệ Khung Hình AI Xuất Ảnh (--ar):
          </div>
          <span style={{ fontSize: 10.5, color: '#38bdf8', background: 'rgba(56, 189, 248, 0.15)', padding: '2px 8px', borderRadius: 4, fontWeight: 800 }}>
            {currentAr}
          </span>
        </div>

        {/* Smart Advice based on hair length & costume */}
        <div style={{ fontSize: 11, color: '#bae6fd', background: 'rgba(2, 132, 199, 0.12)', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(2, 132, 199, 0.25)', lineHeight: 1.45 }}>
          {isLongHair
            ? '💡 Nhân vật có suối tóc dài / đạo bào thướt tha: Khuyên dùng 16:9 (Trải đều 5 góc) hoặc 3:4 (Khổ đứng cao) để không bị AI cắt xén đuôi tóc!'
            : '💡 Tóc ngắn / trung bình: Khuyên dùng 16:9 (Model Sheet 5 góc) hoặc 1:1 (Lưới vuông đều).'}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 6 }}>
          {[
            { ar: '16:9', label: '16:9', desc: 'Rộng Ngang' },
            { ar: '3:4', label: '3:4', desc: 'Khổ Đứng' },
            { ar: '1:1', label: '1:1', desc: 'Vuông Đều' },
            { ar: '9:16', label: '9:16', desc: 'Dọc Dài' },
          ].map((item) => {
            const active = currentAr === item.ar;
            return (
              <button
                key={item.ar}
                onClick={() => setConfig((p) => ({ ...p, aspect_ratio: item.ar as any }))}
                style={{
                  padding: '7px 4px',
                  fontSize: 11.5,
                  fontWeight: 700,
                  borderRadius: 6,
                  border: active ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                  background: active ? 'linear-gradient(135deg, #0284c7, #0369a1)' : 'rgba(255,255,255,0.03)',
                  color: active ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  gap: 2,
                  boxShadow: active ? '0 2px 8px rgba(2, 132, 199, 0.3)' : 'none',
                }}
              >
                <span>{item.label}</span>
                <span style={{ fontSize: 9, opacity: 0.85 }}>{item.desc}</span>
              </button>
            );
          })}
        </div>
      </div>
    );
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '40% minmax(0, 1fr)', gap: 16, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: 3-Step Workflow & Configuration Panel (40%) ───────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, overflowY: 'auto', paddingRight: 6 }}>
        {/* 3-Step Production Workflow Switcher */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, background: 'rgba(15, 23, 42, 0.95)', padding: 8, borderRadius: 10, border: '1px solid rgba(56, 189, 248, 0.3)', boxShadow: '0 4px 16px rgba(0,0,0,0.4)' }}>
          <div style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', textTransform: 'uppercase', letterSpacing: '0.6px', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sparkle size={13} /> QUY TRÌNH SẢN XUẤT 3 BƯỚC CHUẨN STUDIO:
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 6 }}>
            <button
              onClick={() => setWorkflowTab('step1_master')}
              style={{
                padding: '8px 6px',
                fontSize: 11.5,
                fontWeight: 700,
                borderRadius: 8,
                border: workflowTab === 'step1_master' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                background: workflowTab === 'step1_master' ? 'linear-gradient(135deg, #0284c7, #0369a1)' : 'rgba(255,255,255,0.03)',
                color: workflowTab === 'step1_master' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 3,
                boxShadow: workflowTab === 'step1_master' ? '0 4px 12px rgba(2, 132, 199, 0.4)' : 'none',
                transition: 'all 0.2s ease',
              }}
            >
              <User size={15} />
              <span>BƯỚC 1</span>
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Nhân Vật Gốc</span>
            </button>

            <button
              onClick={() => setWorkflowTab('step2_decomposed_parts')}
              style={{
                padding: '8px 6px',
                fontSize: 11.5,
                fontWeight: 700,
                borderRadius: 8,
                border: workflowTab === 'step2_decomposed_parts' ? '1px solid #34d399' : '1px solid rgba(255,255,255,0.08)',
                background: workflowTab === 'step2_decomposed_parts' ? 'linear-gradient(135deg, #10b981, #059669)' : 'rgba(255,255,255,0.03)',
                color: workflowTab === 'step2_decomposed_parts' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 3,
                boxShadow: workflowTab === 'step2_decomposed_parts' ? '0 4px 12px rgba(16, 185, 129, 0.4)' : 'none',
                transition: 'all 0.2s ease',
              }}
            >
              <Scissors size={15} />
              <span>BƯỚC 2</span>
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Bóc Tách Linh Kiện</span>
            </button>

            <button
              onClick={() => setWorkflowTab('step3_actions')}
              style={{
                padding: '8px 6px',
                fontSize: 11.5,
                fontWeight: 700,
                borderRadius: 8,
                border: workflowTab === 'step3_actions' ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.08)',
                background: workflowTab === 'step3_actions' ? 'linear-gradient(135deg, #a855f7, #7e22ce)' : 'rgba(255,255,255,0.03)',
                color: workflowTab === 'step3_actions' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 3,
                boxShadow: workflowTab === 'step3_actions' ? '0 4px 12px rgba(168, 85, 247, 0.4)' : 'none',
                transition: 'all 0.2s ease',
              }}
            >
              <Swords size={15} />
              <span>BƯỚC 3</span>
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Kịch Bản 4K</span>
            </button>
          </div>
        </div>

        {/* ─── STEP 1: MASTER CHARACTER TURNAROUND CONTROLS ──────── */}
        {workflowTab === 'step1_master' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {/* Step Guide Banner */}
            <div style={{ background: 'rgba(2, 132, 199, 0.12)', padding: '10px 14px', borderRadius: 8, border: '1px solid rgba(2, 132, 199, 0.35)', fontSize: 11.5, color: '#e0f2fe', lineHeight: 1.5 }}>
              🌟 <b>Tạo bảng vẽ tổng thể nhân vật hoàn chỉnh trên Nền Trắng Studio</b> (Mặt, Mắt, Mũi, Miệng, Đạo bào, Pháp bảo & Mái tóc đồng nhất 5 góc quay). Ảnh này làm mẫu tham chiếu gốc để sang Bước 2 bóc tách tóc!
            </div>

            {/* Gender & Art Style */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', gap: 8 }}>
              <div>
                <label style={{ fontSize: 11.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Giới tính:</label>
                <div style={{ display: 'flex', gap: 6 }}>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, gender: 'nam' }))}
                    style={{
                      flex: 1,
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.gender === 'nam' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.12)',
                      background: config.gender === 'nam' ? '#0284c7' : 'rgba(255,255,255,0.04)',
                      color: '#fff',
                      cursor: 'pointer',
                    }}
                  >
                    Nam Tu Sĩ
                  </button>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, gender: 'nu' }))}
                    style={{
                      flex: 1,
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.gender === 'nu' ? '1px solid #f472b6' : '1px solid rgba(255,255,255,0.12)',
                      background: config.gender === 'nu' ? '#db2777' : 'rgba(255,255,255,0.04)',
                      color: '#fff',
                      cursor: 'pointer',
                    }}
                  >
                    Nữ Tiên Tử
                  </button>
                </div>
              </div>

              <div>
                <label style={{ fontSize: 11.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Phong cách nghệ thuật:</label>
                <select
                  value={config.character_style}
                  onChange={(e) => setConfig((p) => ({ ...p, character_style: e.target.value as any }))}
                  style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11.5, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
                >
                  <option value="chibi">🌟 Hoạt Hình Chibi Đáng Yêu (Cute Anime Chibi 2.5D)</option>
                  <option value="hoat_hinh_3d_trung_quoc">🐉 Hoạt Hình 3D Trung Quốc (3D Donghua)</option>
                  <option value="tu_tien_manhua">Tu Tiên / Manhua Trung Quốc</option>
                  <option value="kiem_hiep">Kiếm Hiệp / Cổ Trang Wuxia</option>
                  <option value="anime_action">Anime Action Nhật Bản</option>
                  <option value="cyberpunk_anime">Cyberpunk Anime</option>
                  <option value="custom">✍️ Tự Nhập Phong Cách Khác (Custom)...</option>
                </select>
                {config.character_style === 'custom' && (
                  <input
                    type="text"
                    value={config.custom_character_style || ''}
                    onChange={(e) => setConfig((p) => ({ ...p, custom_character_style: e.target.value }))}
                    placeholder="VD: Dark Fantasy Manhwa, Genshin 2.5D..."
                    style={{ width: '100%', marginTop: 6, height: 34, padding: '5px 10px', fontSize: 11.5, background: '#090d16', color: '#38bdf8', border: '1px solid #38bdf8', borderRadius: 6 }}
                  />
                )}
              </div>
            </div>

            {/* Five Senses & Facial Features */}
            <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 12.5, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Eye size={14} /> Khuôn Mặt & Ngũ Quan {config.character_style === 'chibi' ? '(Phong Cách Chibi Kawaii)' : '(Mắt, Mũi, Miệng)'}:
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Dáng Mắt & Thần thái:</label>
                  <select
                    value={config.eye_shape || (config.character_style === 'chibi' ? 'chibi_sparkling_starry' : 'sharp_phoenix')}
                    onChange={(e) => setConfig((p) => ({ ...p, eye_shape: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    {config.character_style === 'chibi' ? (
                      <>
                        <option value="chibi_sparkling_starry">🌟 Mắt tròn to long lanh ánh sao</option>
                        <option value="chibi_happy_crescent">💖 Mắt cười híp cong lưỡi liềm</option>
                        <option value="chibi_pouty_teary">🥺 Mắt ươn ướt cún con dễ thương</option>
                        <option value="large_clear">👀 Mắt to trong veo sáng ngời</option>
                        <option value="custom">✍️ Tự Nhập Dáng Mắt Khác...</option>
                      </>
                    ) : (
                      <>
                        <option value="sharp_phoenix">Mắt phượng sắc lạnh</option>
                        <option value="cold_swordsman">Mắt kiếm khách kiên định</option>
                        <option value="large_clear">Mắt trong sáng tinh anh</option>
                        <option value="fox_alluring">Mắt hồ ly quyến rũ</option>
                        <option value="custom">✍️ Tự Nhập Dáng Mắt Khác...</option>
                      </>
                    )}
                  </select>
                  {config.eye_shape === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_eye_shape || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_eye_shape: e.target.value }))}
                      placeholder="VD: Mắt rồng uy nghiêm, Mắt trái tim..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid #38bdf8', borderRadius: 5 }}
                    />
                  )}
                </div>

                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Màu Tròng Mắt:</label>
                  <select
                    value={config.eye_color || 'azure_blue'}
                    onChange={(e) => setConfig((p) => ({ ...p, eye_color: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    {config.character_style === 'chibi' ? (
                      <>
                        <option value="azure_blue">💎 Xanh lam ngọc phát sáng</option>
                        <option value="chibi_sweet_pink">🍓 Hồng dâu tây kẹo ngọt</option>
                        <option value="golden_amber">🍯 Vàng mật ong hổ phách</option>
                        <option value="emerald_green">🍃 Xanh ngọc lục bảo</option>
                        <option value="obsidian_black">🖤 Đen tuyền búp bê</option>
                        <option value="custom">✍️ Tự Nhập Màu Mắt Khác...</option>
                      </>
                    ) : (
                      <>
                        <option value="azure_blue">Xanh lam ngọc phát sáng</option>
                        <option value="golden_amber">Vàng hổ phách tiên linh</option>
                        <option value="crimson_red">Đỏ rực hỏa nhãn</option>
                        <option value="emerald_green">Xanh lục bích</option>
                        <option value="obsidian_black">Đen tuyền sâu thẳm</option>
                        <option value="custom">✍️ Tự Nhập Màu Mắt Khác...</option>
                      </>
                    )}
                  </select>
                  {config.eye_color === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_eye_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_eye_color: e.target.value }))}
                      placeholder="VD: Tím tử lôi dạ quang..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid #38bdf8', borderRadius: 5 }}
                    />
                  )}
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Sống Mũi:</label>
                  <select
                    value={config.nose_shape || (config.character_style === 'chibi' ? 'chibi_tiny_dot' : 'straight_high_bridge')}
                    onChange={(e) => setConfig((p) => ({ ...p, nose_shape: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    {config.character_style === 'chibi' ? (
                      <>
                        <option value="chibi_tiny_dot">👃 Mũi chấm nhỏ xíu đáng yêu</option>
                        <option value="chibi_no_nose">🚫 Ẩn mũi (Chibi cổ điển không vẽ mũi)</option>
                        <option value="small_delicate">👃 Mũi nhỏ nhắn nhẹ nhàng</option>
                        <option value="custom">✍️ Tự Nhập Dáng Mũi Khác...</option>
                      </>
                    ) : (
                      <>
                        <option value="straight_high_bridge">Sống mũi thẳng cao thanh tú</option>
                        <option value="sharp_defined">Mũi sắc nét góc cạnh</option>
                        <option value="small_delicate">Mũi nhỏ nhắn nhẹ nhàng</option>
                        <option value="custom">✍️ Tự Nhập Dáng Mũi Khác...</option>
                      </>
                    )}
                  </select>
                  {config.nose_shape === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_nose_shape || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_nose_shape: e.target.value }))}
                      placeholder="VD: Mũi cao lai Tây..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid #38bdf8', borderRadius: 5 }}
                    />
                  )}
                </div>

                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Khẩu Hình / Nụ Cười:</label>
                  <select
                    value={config.mouth_style || (config.character_style === 'chibi' ? 'chibi_cat_mouth' : 'confident_smirk')}
                    onChange={(e) => setConfig((p) => ({ ...p, mouth_style: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    {config.character_style === 'chibi' ? (
                      <>
                        <option value="chibi_cat_mouth">😺 Miệng mèo tinh nghịch :3</option>
                        <option value="chibi_surprised_o">👄 Miệng chữ O ngơ ngác đáng yêu</option>
                        <option value="chibi_puffed_cheek">🍡 Phồng má ngậm bánh bao</option>
                        <option value="chibi_big_smile">😄 Cười tít mắt hớn hở</option>
                        <option value="custom">✍️ Tự Nhập Khẩu Hình Khác...</option>
                      </>
                    ) : (
                      <>
                        <option value="confident_smirk">Cười nhếch tự tin</option>
                        <option value="gentle_smile">Nụ cười dịu dàng</option>
                        <option value="battle_roar">Nghiêm nghị tập trung</option>
                        <option value="custom">✍️ Tự Nhập Khẩu Hình Khác...</option>
                      </>
                    )}
                  </select>
                  {config.mouth_style === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_mouth_style || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_mouth_style: e.target.value }))}
                      placeholder="VD: Cắn môi đăm chiêu..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid #38bdf8', borderRadius: 5 }}
                    />
                  )}
                </div>
              </div>
            </div>

            {/* Costume & Robes */}
            <div style={{ background: 'rgba(168, 85, 247, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 12.5, fontWeight: 800, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Shirt size={14} /> Trang Phục & Đạo Bào:
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Kiểu Đạo Bào:</label>
                  <select
                    value={config.costume_style || 'dao_bao_tien_hiep'}
                    onChange={(e) => setConfig((p) => ({ ...p, costume_style: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    {config.character_style === 'chibi' ? (
                      <>
                        <option value="dao_bao_tien_hiep">Đạo bào mini tay thụng bồng bềnh</option>
                        <option value="bach_y_tien_tu">Bạch y tiểu tiên nữ đáng yêu</option>
                        <option value="kiem_khach_ao_vai">Tiểu kiếm khách áo vải</option>
                        <option value="hac_y_ma_dao">Tiểu ma đầu hắc y tinh quái</option>
                        <option value="hoang_toc_kim_bao">Tiểu hoàng tử / công chúa kim bào</option>
                        <option value="custom">✍️ Tự Nhập Kiểu Trang Phục Khác...</option>
                      </>
                    ) : (
                      <>
                        <option value="dao_bao_tien_hiep">Đạo bào tu tiên thướt tha</option>
                        <option value="kiem_khach_ao_vai">Kiếm khách áo vải phong trần</option>
                        <option value="hac_y_ma_dao">Hắc y ma đạo huyền bí</option>
                        <option value="bach_y_tien_tu">Bạch y tiên tử thanh khiết</option>
                        <option value="hoang_toc_kim_bao">Hoàng tộc kim bào quý phái</option>
                        <option value="custom">✍️ Tự Nhập Kiểu Trang Phục Khác...</option>
                      </>
                    )}
                  </select>
                  {config.costume_style === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_costume_style || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_costume_style: e.target.value }))}
                      placeholder="VD: Chiến giáp long lân..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#c084fc', border: '1px solid #c084fc', borderRadius: 5 }}
                    />
                  )}
                </div>

                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Màu sắc & Họa tiết:</label>
                  <input
                    type="text"
                    value={config.costume_color || 'cyan and white with gold accents'}
                    onChange={(e) => setConfig((p) => ({ ...p, costume_color: e.target.value }))}
                    placeholder="VD: Lam ngọc viền kim tuyến..."
                    style={{ width: '100%', height: 34, padding: '5px 10px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
                  />
                </div>
              </div>
            </div>

            {/* Weapon & Prop Item */}
            <div style={{ background: 'rgba(234, 179, 8, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(234, 179, 8, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ fontSize: 12.5, fontWeight: 800, color: '#facc15', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Shield size={14} /> Pháp Bảo & Đồ Vật Nhân Vật Cầm:
              </div>
              <select
                value={config.prop_item || 'flying_sword'}
                onChange={(e) => setConfig((p) => ({ ...p, prop_item: e.target.value as any }))}
                style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11.5, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
              >
                <option value="flying_sword">🗡️ Phi Kiếm ngọc bích phát sáng kiếm ý</option>
                <option value="feather_fan">🪶 Quạt Lông Vũ tiên gia thái cực</option>
                <option value="talisman_scrolls">📜 Cuộn Bùa Chú phù lục linh quang</option>
                <option value="gourd_wine">🍶 Hồ Lô Tiên Tửu chứa linh tuyền</option>
                <option value="jade_hairpin">✨ Trâm Cài Ngọc Bích đính dải lụa</option>
                <option value="custom">✍️ Tự Nhập Pháp Bảo / Vũ Khí Khác...</option>
              </select>
              {config.prop_item === 'custom' && (
                <input
                  type="text"
                  value={config.custom_prop_item || ''}
                  onChange={(e) => setConfig((p) => ({ ...p, custom_prop_item: e.target.value }))}
                  placeholder="VD: Trường thương rồng bạc, Đàn tranh cổ linh..."
                  style={{ width: '100%', height: 34, padding: '5px 10px', fontSize: 11.5, background: '#090d16', color: '#facc15', border: '1px solid #facc15', borderRadius: 6 }}
                />
              )}
            </div>

            {/* Hair Style for Step 1 */}
            <div style={{ background: 'rgba(16, 185, 129, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(16, 185, 129, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 12.5, fontWeight: 800, color: '#34d399' }}>💇 Kiểu Tóc & Trâm Cài Nhân Vật:</div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Độ dài tóc:</label>
                  <select
                    value={config.hair_length || 'long_waist'}
                    onChange={(e) => setConfig((p) => ({ ...p, hair_length: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    <option value="long_waist">Dài ngang lưng</option>
                    <option value="very_long_flowing">Dài chấm gót tiên hiệp</option>
                    <option value="medium_shoulder">Ngang vai tỉa tầng</option>
                    <option value="short">Tóc ngắn cá tính</option>
                    <option value="custom">✍️ Tự Nhập Độ Dài...</option>
                  </select>
                  {config.hair_length === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_hair_length || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_hair_length: e.target.value }))}
                      placeholder="VD: Cột đuôi ngựa cao..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#34d399', border: '1px solid #34d399', borderRadius: 5 }}
                    />
                  )}
                </div>

                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Màu sắc:</label>
                  <select
                    value={config.hair_color || 'jet_black'}
                    onChange={(e) => setConfig((p) => ({ ...p, hair_color: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    <option value="jet_black">Đen tuyền óng ả</option>
                    <option value="silver_white">Bạch kim (Trắng bạc)</option>
                    <option value="crimson_red">Đỏ rực hỏa diệm</option>
                    <option value="azure_blue">Xanh lam ngọc</option>
                    <option value="golden_blonde">Vàng kim ánh dương</option>
                    <option value="custom">✍️ Tự Nhập Màu Tóc...</option>
                  </select>
                  {config.hair_color === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_hair_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_hair_color: e.target.value }))}
                      placeholder="VD: Xanh ngọc ombre tím..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#34d399', border: '1px solid #34d399', borderRadius: 5 }}
                    />
                  )}
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Chất tóc / Xoăn:</label>
                  <select
                    value={config.hair_texture || 'straight_silky'}
                    onChange={(e) => setConfig((p) => ({ ...p, hair_texture: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    <option value="straight_silky">Thẳng mượt suối lụa</option>
                    <option value="wavy_curls">Xoăn sóng bồng bềnh</option>
                    <option value="wild_spiky">Đánh rối hoang dã</option>
                    <option value="custom">✍️ Tự Nhập Chất Tóc...</option>
                  </select>
                  {config.hair_texture === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_hair_texture || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_hair_texture: e.target.value }))}
                      placeholder="VD: Tóc bím dài tiên hiệp..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#34d399', border: '1px solid #34d399', borderRadius: 5 }}
                    />
                  )}
                </div>

                <div>
                  <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Trâm cài / Phụ kiện:</label>
                  <select
                    value={config.hair_accessories || 'jade_hairpin'}
                    onChange={(e) => setConfig((p) => ({ ...p, hair_accessories: e.target.value as any }))}
                    style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                  >
                    <option value="jade_hairpin">Trâm cài ngọc / Bạc</option>
                    <option value="flowing_ribbons">Dải lụa bay bồng bềnh</option>
                    <option value="golden_crown">Vương miện vàng kim</option>
                    <option value="none">Không có</option>
                    <option value="custom">✍️ Tự Nhập Phụ Kiện...</option>
                  </select>
                  {config.hair_accessories === 'custom' && (
                    <input
                      type="text"
                      value={config.custom_hair_accessories || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, custom_hair_accessories: e.target.value }))}
                      placeholder="VD: Mũ miện đính ngọc..."
                      style={{ width: '100%', marginTop: 5, height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#34d399', border: '1px solid #34d399', borderRadius: 5 }}
                    />
                  )}
                </div>
              </div>
            </div>

            {/* Aspect Ratio Selector for Step 1 */}
            {renderAspectRatioSelector()}
          </div>
        )}

        {/* ─── STEP 2: DECOMPOSED PARTS (HAIR, EYES, MOUTH, CLOTHES, ETC.) ─── */}
        {workflowTab === 'step2_decomposed_parts' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {/* Step Guide Banner */}
            <div style={{ background: 'rgba(16, 185, 129, 0.12)', padding: '10px 14px', borderRadius: 8, border: '1px solid rgba(16, 185, 129, 0.35)', fontSize: 11.5, color: '#d1fae5', lineHeight: 1.5 }}>
              ✂️ <b>Bóc tách linh kiện (Tóc, Mắt, Miệng, Đạo bào...) từ nhân vật Bước 1 thành Sprite Sheet đa góc quay</b>. Hệ thống tự động kế thừa đặc tính từ Bước 1 để đảm bảo khi ghép vào Studio sẽ chuẩn khít 100%!
            </div>
            
            {/* Part Selection */}
            <div>
              <label style={{ fontSize: 11.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Chọn linh kiện cần bóc tách:</label>
              <select
                value={decomposedPartType}
                onChange={(e) => setDecomposedPartType(e.target.value as any)}
                style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11.5, background: '#090d16', color: '#fff', border: '1px solid rgba(52, 211, 153, 0.5)', borderRadius: 6 }}
              >
                <option value="hair_multi_angle_grid">💇 Mái Tóc (4 Tầng x 5 Góc Quay)</option>
                <option value="eyes_grid">👁️ Đôi Mắt & Chớp Mắt (Cảm xúc & Khớp Khung Mắt)</option>
                <option value="mouth_grid">👄 Khẩu Hình Miệng (Nói Chuyện & Biểu Cảm)</option>
                <option value="nose_chin_grid">👃 Sống Mũi, Khung Cằm & Vành Tai</option>
                <option value="costume_grid">👘 Trang Phục / Đạo Bào Rỗng Ruột</option>
                <option value="weapons_grid">🗡️ Vũ Khí & Pháp Bảo</option>
                <option value="limbs_hands_grid">💪 Tứ Chi & Bàn Tay Bắt Quyết</option>
              </select>
            </div>

            {/* Inherited Character Hair Badge Card (Show only for hair) */}
            {decomposedPartType === 'hair_multi_angle_grid' && (
            <div style={{ background: 'rgba(16, 185, 129, 0.08)', padding: 12, borderRadius: 8, border: '1px solid rgba(16, 185, 129, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ fontSize: 12, fontWeight: 800, color: '#34d399', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Layers size={14} /> Đặc Tính Tóc Tự Động Kế Thừa Từ Bước 1:
                </span>
                <button
                  onClick={() => setWorkflowTab('step1_master')}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 4,
                    padding: '3px 8px',
                    fontSize: 10.5,
                    borderRadius: 4,
                    border: '1px solid rgba(52, 211, 153, 0.4)',
                    background: 'rgba(52, 211, 153, 0.15)',
                    color: '#6ee7b7',
                    cursor: 'pointer',
                  }}
                >
                  <Edit3 size={11} /> Sửa ở Bước 1
                </button>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                <div style={{ background: 'rgba(0,0,0,0.35)', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Màu sắc tóc:</div>
                  <div style={{ fontSize: 11.5, fontWeight: 700, color: '#a7f3d0' }}>{inheritedHair.color}</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.35)', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Độ dài tóc:</div>
                  <div style={{ fontSize: 11.5, fontWeight: 700, color: '#a7f3d0' }}>{inheritedHair.length}</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.35)', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Chất tóc / Dáng:</div>
                  <div style={{ fontSize: 11.5, fontWeight: 700, color: '#a7f3d0' }}>{inheritedHair.texture}</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.35)', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Trâm cài / Phụ kiện:</div>
                  <div style={{ fontSize: 11.5, fontWeight: 700, color: '#a7f3d0' }}>{inheritedHair.acc}</div>
                </div>
              </div>
            </div>
            )}

            {/* Step 1 Image Reference Link Input */}
            <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <label style={{ fontSize: 11.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                <Link size={13} /> Link / File Ảnh Nhân Vật Bước 1 (Tùy chọn cho Midjourney --sref):
              </label>
              <input
                type="text"
                value={referenceImageUrl}
                onChange={(e) => setReferenceImageUrl(e.target.value)}
                placeholder="Dán link ảnh nhân vật tạo được từ Bước 1 vào đây..."
                style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11.5, background: '#090d16', color: '#38bdf8', border: '1px solid rgba(56, 189, 248, 0.3)', borderRadius: 6 }}
              />
              <span style={{ fontSize: 10, color: '#94a3b8' }}>
                💡 Nếu nhập link ảnh, hệ thống sẽ tự động thêm cờ <code>--sref [link]</code> vào cuối prompt Midjourney!
              </span>
            </div>

            {/* Aspect Ratio Selector for Step 2 */}
            {renderAspectRatioSelector()}

            {/* Background Settings */}
            <div style={{ background: 'rgba(255,255,255,0.03)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div>
                <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
                  Màu phông nền bóc tách:
                </label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, bg_type: 'chroma_green' }))}
                    style={{
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.bg_type === 'chroma_green' ? '1px solid #22c55e' : '1px solid rgba(255,255,255,0.12)',
                      background: config.bg_type === 'chroma_green' ? 'rgba(34, 197, 94, 0.25)' : 'rgba(0,0,0,0.3)',
                      color: config.bg_type === 'chroma_green' ? '#4ade80' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    🟢 Xanh Lá (#00FF00)
                  </button>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, bg_type: 'pure_white' }))}
                    style={{
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.bg_type === 'pure_white' ? '1px solid #f8fafc' : '1px solid rgba(255,255,255,0.12)',
                      background: config.bg_type === 'pure_white' ? 'rgba(248, 250, 252, 0.15)' : 'rgba(0,0,0,0.3)',
                      color: config.bg_type === 'pure_white' ? '#f8fafc' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    ⚪ Trắng Tinh (#FFFFFF)
                  </button>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, bg_type: 'chroma_gray' }))}
                    style={{
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.bg_type === 'chroma_gray' ? '1px solid #94a3b8' : '1px solid rgba(255,255,255,0.12)',
                      background: config.bg_type === 'chroma_gray' ? 'rgba(148, 163, 184, 0.25)' : 'rgba(0,0,0,0.3)',
                      color: config.bg_type === 'chroma_gray' ? '#f8fafc' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    🔘 Xám Đậm (#333333)
                  </button>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, bg_type: 'pure_black' }))}
                    style={{
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.bg_type === 'pure_black' ? '1px solid #475569' : '1px solid rgba(255,255,255,0.12)',
                      background: config.bg_type === 'pure_black' ? 'rgba(0, 0, 0, 0.8)' : 'rgba(0,0,0,0.3)',
                      color: config.bg_type === 'pure_black' ? '#f8fafc' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    ⚫ Đen Tuyền (#000000)
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ─── STEP 3: ACTION SEQUENCES CONTROLS ─────────────────── */}
        {workflowTab === 'step3_actions' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div>
              <label style={{ fontSize: 12, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
                Loại hành động kịch bản:
              </label>
              <select
                value={actionType}
                onChange={(e) => setActionType(e.target.value as any)}
                style={{ width: '100%', height: 38, padding: '6px 10px', fontSize: 12, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
              >
                <option value="combat">⚔️ Chiến đấu & Tung chiêu kiếm khí</option>
                <option value="emotion">😱 Biểu cảm giật mình / Tức giận / Sốc</option>
                <option value="dialogue">💬 Đối thoại khẩu khí / Nói chuyện</option>
                <option value="eat">🍵 Ăn uống / Thưởng trà / Thư giãn</option>
                <option value="transition">🗺️ Chuyển cảnh bản đồ (Map Cut)</option>
              </select>
            </div>

            <div>
              <label style={{ fontSize: 12, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
                Mức độ uy lực / Cường độ:
              </label>
              <div style={{ display: 'flex', gap: 8 }}>
                {(['mild', 'intense', 'extreme'] as const).map((lvl) => (
                  <button
                    key={lvl}
                    onClick={() => setActionIntensity(lvl)}
                    style={{
                      flex: 1,
                      height: 36,
                      fontSize: 11.5,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: '1px solid rgba(255,255,255,0.12)',
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

      {/* ─── RIGHT: Generated Dual Language Prompt & Copy (60%) ─────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 16, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        {/* Top Header & Copy Action */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 13.5, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sparkles size={16} />
            {workflowTab === 'step1_master'
              ? '🌟 PROMPT BƯỚC 1: BẢNG THIẾT KẾ NHÂN VẬT GỐC (MASTER 4K)'
              : workflowTab === 'step2_decomposed_parts'
              ? '✂️ PROMPT BƯỚC 2: BÓC TÁCH LINH KIỆN NHÂN VẬT ĐA GÓC QUAY'
              : '⚔️ PROMPT BƯỚC 3: KỊCH BẢN HÀNH ĐỘNG & CHIÊU THỨC (4K)'}
          </div>

          <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6 }}>
            <button
              onClick={() => setDisplayLangTab('both')}
              style={{ padding: '4px 10px', fontSize: 11, borderRadius: 4, border: 'none', background: displayLangTab === 'both' ? '#0284c7' : 'transparent', color: displayLangTab === 'both' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 600 }}
            >
              Tất cả
            </button>
            <button
              onClick={() => setDisplayLangTab('gemini')}
              style={{ padding: '4px 10px', fontSize: 11, borderRadius: 4, border: 'none', background: displayLangTab === 'gemini' ? '#f59e0b' : 'transparent', color: displayLangTab === 'gemini' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 700 }}
            >
              🍌 Gemini/LLM
            </button>
            <button
              onClick={() => setDisplayLangTab('json')}
              style={{ padding: '4px 10px', fontSize: 11, borderRadius: 4, border: 'none', background: displayLangTab === 'json' ? '#8b5cf6' : 'transparent', color: displayLangTab === 'json' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 700 }}
            >
              📄 JSON
            </button>
            <button
              onClick={() => setDisplayLangTab('english')}
              style={{ padding: '4px 10px', fontSize: 11, borderRadius: 4, border: 'none', background: displayLangTab === 'english' ? '#0284c7' : 'transparent', color: displayLangTab === 'english' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 600 }}
            >
              Tiếng Anh
            </button>
            <button
              onClick={() => setDisplayLangTab('vietnamese')}
              style={{ padding: '4px 10px', fontSize: 11, borderRadius: 4, border: 'none', background: displayLangTab === 'vietnamese' ? '#0284c7' : 'transparent', color: displayLangTab === 'vietnamese' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 600 }}
            >
              Tiếng Việt
            </button>
          </div>
        </div>

        {/* Workflow Instruction Banner */}
        {workflowTab === 'step1_master' && (
          <div style={{ background: 'rgba(2, 132, 199, 0.08)', padding: '10px 14px', borderRadius: 6, border: '1px solid rgba(2, 132, 199, 0.25)', fontSize: 11.5, color: '#7dd3fc', lineHeight: 1.5 }}>
            💡 <b>Hướng dẫn Bước 1:</b> Copy prompt tiếng Anh dán vào <b>Midjourney / Flux</b> HOẶC copy prompt Gemini dán vào <b>Gemini / DALL-E</b> $\to$ Tải ảnh nhân vật về $\to$ Sang Bước 2 để bóc tách!
          </div>
        )}

        {workflowTab === 'step2_decomposed_parts' && (
          <div style={{ background: 'rgba(34, 197, 94, 0.08)', padding: '10px 14px', borderRadius: 6, border: '1px solid rgba(34, 197, 94, 0.25)', fontSize: 11.5, color: '#86efac', lineHeight: 1.5 }}>
            💡 <b>Hướng dẫn Bước 2:</b> Trong Midjourney, thêm cờ <code>--sref [link_ảnh_bước_1]</code> hoặc dán prompt vào Gemini/DALL-E kèm theo ảnh Bước 1 để làm mẫu tham chiếu gốc (Reference Image).
          </div>
        )}

        {/* Quick Copy Buttons */}
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button
            onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptGemini : currentAction.promptVi, 'gemini_copy')}
            style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              padding: '9px 14px',
              fontSize: 11.5,
              fontWeight: 700,
              borderRadius: 6,
              background: copiedPrompt === 'gemini_copy' ? '#22c55e' : 'linear-gradient(135deg, #d97706, #b45309)',
              color: '#fff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(217, 119, 6, 0.3)',
            }}
          >
            {copiedPrompt === 'gemini_copy' ? <Check size={14} /> : <Copy size={14} />}
            {copiedPrompt === 'gemini_copy' ? 'Đã Chép Cho LLM!' : '🍌 Sao Chép Cho Gemini / LLM'}
          </button>

          <button
            onClick={() => handleCopy(workflowTab !== 'step3_actions' ? finalFullCopyText : `${currentAction.promptEn}\n\nNegative prompt:\n${currentAction.neg}`, 'full_en')}
            style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              padding: '9px 14px',
              fontSize: 11.5,
              fontWeight: 700,
              borderRadius: 6,
              background: copiedPrompt === 'full_en' ? '#22c55e' : 'linear-gradient(135deg, #0284c7, #0369a1)',
              color: '#fff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(2, 132, 199, 0.3)',
            }}
          >
            {copiedPrompt === 'full_en' ? <Check size={14} /> : <Copy size={14} />}
            {copiedPrompt === 'full_en' ? 'Đã Sao Chép Text!' : '📋 Sao Chép Cho Midjourney'}
          </button>
        </div>

        {/* Gemini Conversational Prompt Box */}
        {(displayLangTab === 'gemini' || displayLangTab === 'both') && (
          <div style={{ background: '#1c1917', padding: 14, borderRadius: 8, border: '1px solid #f59e0b' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#fbbf24', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Sparkles size={13} /> Lệnh tự nhiên (Dành cho Gemini / ChatGPT / DALL-E 3):
              </span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptGemini : currentAction.promptVi, 'gemini_box')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(245, 158, 11, 0.2)', color: '#fbbf24', border: '1px solid #f59e0b', cursor: 'pointer' }}
              >
                {copiedPrompt === 'gemini_box' ? 'Đã Chép!' : 'Chép Đoạn Này'}
              </button>
            </div>
            <pre style={{ fontSize: 11.5, color: '#fde68a', lineHeight: 1.65, margin: 0, whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>
              {workflowTab !== 'step3_actions' ? promptResult.promptGemini : currentAction.promptVi}
            </pre>
          </div>
        )}

        {/* JSON Structured Prompt Box */}
        {(displayLangTab === 'json' || displayLangTab === 'both') && (
          <div style={{ background: '#070b14', padding: 14, borderRadius: 8, border: '1px solid #8b5cf6' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Sparkles size={13} /> Cấu Trúc JSON Chuẩn Cho AI (LLM / Midjourney / FLUX Prompting):
              </span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptJSON : JSON.stringify(currentAction, null, 2), 'json_box')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(139, 92, 246, 0.2)', color: '#c084fc', border: '1px solid #8b5cf6', cursor: 'pointer' }}
              >
                {copiedPrompt === 'json_box' ? 'Đã Chép JSON!' : 'Chép JSON'}
              </button>
            </div>
            <pre style={{ fontSize: 11, color: '#a5f3fc', lineHeight: 1.55, margin: 0, overflowX: 'auto', background: 'rgba(0,0,0,0.5)', padding: 10, borderRadius: 6, fontFamily: 'monospace' }}>
              {workflowTab !== 'step3_actions' ? promptResult.promptJSON : JSON.stringify(currentAction, null, 2)}
            </pre>
          </div>
        )}

        {/* Vietnamese Translation & Guide */}
        {(displayLangTab === 'vietnamese' || displayLangTab === 'both') && (
          <div style={{ background: '#0b1329', padding: 14, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.3)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Globe size={13} /> Hướng Dẫn Chi Tiết Tiếng Việt:
              </span>
            </div>
            <pre style={{ fontSize: 11.5, color: '#e2e8f0', lineHeight: 1.65, margin: 0, whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>
              {workflowTab !== 'step3_actions' ? promptResult.promptVietnamese : currentAction.promptVi}
            </pre>
          </div>
        )}

        {/* English Positive Prompt Box */}
        {(displayLangTab === 'english' || displayLangTab === 'both') && (
          <div style={{ background: '#090d16', padding: 14, borderRadius: 8, border: '1px solid rgba(255,255,255,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#4ade80' }}>Prompt Tiếng Anh (Cho Midjourney / FLUX / SD - Max 4K):</span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? finalPromptEnglish : currentAction.promptEn, 'pos_only')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', cursor: 'pointer' }}
              >
                {copiedPrompt === 'pos_only' ? 'Đã Chép!' : 'Chép Đoạn Này'}
              </button>
            </div>
            <p style={{ fontSize: 11.5, color: '#e2e8f0', lineHeight: 1.55, margin: 0, userSelect: 'all', wordBreak: 'break-word' }}>
              {workflowTab !== 'step3_actions' ? finalPromptEnglish : currentAction.promptEn}
            </p>
          </div>
        )}

        {/* Negative Prompt Box */}
        {(displayLangTab === 'english' || displayLangTab === 'both') && (
          <div style={{ background: '#090d16', padding: 14, borderRadius: 8, border: '1px solid rgba(239, 68, 68, 0.3)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#f87171' }}>Khử Lỗi (Negative Prompt):</span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.negativePrompt : currentAction.neg, 'neg_only')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', cursor: 'pointer' }}
              >
                {copiedPrompt === 'neg_only' ? 'Đã Chép!' : 'Chép Negative'}
              </button>
            </div>
            <p style={{ fontSize: 11, color: '#94a3b8', lineHeight: 1.45, margin: 0, userSelect: 'all' }}>
              {workflowTab !== 'step3_actions' ? promptResult.negativePrompt : currentAction.neg}
            </p>
          </div>
        )}

        {/* Grid Slicing Specifications */}
        {workflowTab !== 'step3_actions' && (
          <div style={{ background: 'rgba(168, 85, 247, 0.08)', padding: 12, borderRadius: 8, border: '1px dashed rgba(168, 85, 247, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontSize: 11, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 6 }}>
              <Scissors size={14} /> {promptResult.gridStructureGuide}
            </div>
          </div>
        )}

        {/* Camera Guidelines in Action mode */}
        {workflowTab === 'step3_actions' && (
          <div style={{ background: 'rgba(168, 85, 247, 0.1)', padding: 14, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.3)' }}>
            <div style={{ fontSize: 12, fontWeight: 700, color: '#c084fc', marginBottom: 6, display: 'flex', alignItems: 'center', gap: 6 }}>
              <Camera size={14} /> HƯỚNG DẪN GÓC MÁY & DIỄN HOẠT TRONG STUDIO
            </div>
            <p style={{ fontSize: 11.5, color: '#e2e8f0', margin: 0, lineHeight: 1.55 }}>
              {currentAction.cameraGuide}
            </p>
          </div>
        )}
      </div>
    </div>
  );
};
