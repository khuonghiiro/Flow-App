import React, { useState } from 'react';
import {
  Sparkles,
  Copy,
  Check,
  Swords,
  Scissors,
  User,
  Globe,
  Eye,
  Shirt,
  Shield,
  Sparkle,
  Maximize2,
} from 'lucide-react';
import { AIPartPromptConfig } from '../../types/scene2d';
import { buildAIPromptForPart, AIPromptResult } from '../../core/assets/Asset2DRegistry';

export const AIPromptGenerator2D: React.FC = () => {
  const [workflowTab, setWorkflowTab] = useState<'step1_master' | 'step2_decomposed_parts' | 'step3_actions'>('step1_master');
  const [decomposedPartType, setDecomposedPartType] = useState<'hair_multi_angle_grid' | 'eyes_grid' | 'mouth_grid' | 'nose_chin_grid' | 'costume_grid' | 'weapons_grid' | 'limbs_hands_grid'>('hair_multi_angle_grid');
  const [displayLangTab, setDisplayLangTab] = useState<'vietnamese' | 'gemini' | 'english' | 'json' | 'both'>('vietnamese');
  const [referenceImageUrl, setReferenceImageUrl] = useState<string>('');

  // Master Character & Part Config State - Direct text values for easy editing
  const [config, setConfig] = useState<AIPartPromptConfig>({
    workflow_step: 'step1_master_character',
    sheet_type: 'hair_multi_angle_grid',
    part_type: 'toc_truoc',
    character_style: 'Anime Nhật Bản mắt to sắc nét, phong cách Kyoto Animation / Ufotable',
    custom_character_style: '',
    gender: 'nam',
    view_angle: 'all_angles_16_9',
    action_or_expression: 'Ánh mắt sắc bén, thần thái kiên định tự tin',
    color_theme: 'Xanh lam phối trắng viền kim tuyến',
    special_features: 'Linh lực phát sáng nhẹ, tà áo bay phất phơ',
    clean_background: true,
    aspect_ratio: '16:9',
    bg_type: 'chroma_green',
    // Five Senses & Facial (Step 1)
    eye_shape: 'Mắt anime to tròn long lanh tinh anh',
    custom_eye_shape: '',
    eye_color: 'Xanh lam ngọc phát sáng linh lực',
    custom_eye_color: '',
    nose_shape: 'Sống mũi thẳng cao thanh tú',
    custom_nose_shape: '',
    mouth_style: 'Cười nhếch môi tự tin',
    custom_mouth_style: '',
    ear_style: 'human_natural',
    // Costume & Robes (Step 1)
    costume_style: 'Đạo bào tu tiên cách tân thướt tha, tay áo rộng',
    custom_costume_style: '',
    costume_color: 'Xanh lam phối trắng viền chỉ vàng kim',
    // Weapon & Prop Item (Step 1)
    prop_item: 'Phi kiếm phát sáng linh lực lam ngọc',
    custom_prop_item: '',
    // Hair (Step 1 & Step 2)
    hair_length: 'Dài ngang lưng suôn mượt',
    custom_hair_length: '',
    hair_texture: 'Thẳng mượt như suối lụa',
    custom_hair_texture: '',
    hair_color: 'Đen tuyền óng ả',
    custom_hair_color: '',
    hair_accessories: 'Trâm cài ngọc bích đính dải lụa',
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

  // Helper to render Aspect Ratio Selector with smart recommendation
  const renderAspectRatioSelector = () => {
    const currentAr = config.aspect_ratio || (workflowTab === 'step1_master' ? '16:9' : '1:1');

    return (
      <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 12, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Maximize2 size={14} /> Tỉ Lệ Khung Hình AI Xuất Ảnh (--ar):
          </div>
          <span style={{ fontSize: 10.5, color: '#38bdf8', background: 'rgba(56, 189, 248, 0.15)', padding: '2px 8px', borderRadius: 4, fontWeight: 800 }}>
            {currentAr}
          </span>
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
    <div style={{ display: 'grid', gridTemplateColumns: '42% minmax(0, 1fr)', gap: 16, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: 3-Step Workflow & Textbox-First Controls Panel (42%) ─── */}
      <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
        {/* CỐ ĐỊNH: 3-Step Production Workflow Switcher (Sticky Top) */}
        <div style={{ flexShrink: 0, display: 'flex', flexDirection: 'column', gap: 6, background: 'rgba(15, 23, 42, 0.95)', padding: 8, borderRadius: 10, border: '1px solid rgba(56, 189, 248, 0.3)', boxShadow: '0 4px 16px rgba(0,0,0,0.4)', marginBottom: 8 }}>
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

        {/* PHẦN NỘI DUNG CUỘN (SCROLLABLE CONTROLS) */}
        <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12, paddingRight: 6 }}>
          {/* ─── STEP 1: MASTER CHARACTER TURNAROUND CONTROLS ──────── */}
          {workflowTab === 'step1_master' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {/* Step Guide Banner */}
              <div style={{ background: 'rgba(2, 132, 199, 0.12)', padding: '10px 14px', borderRadius: 8, border: '1px solid rgba(2, 132, 199, 0.35)', fontSize: 11.5, color: '#e0f2fe', lineHeight: 1.5 }}>
                🌟 <b>Tạo bảng vẽ tổng thể nhân vật hoàn chỉnh trên Nền Trắng Studio</b> (Mặt, Mắt, Mũi, Miệng, Đạo bào, Pháp bảo & Mái tóc đồng nhất 5 góc quay). Gõ trực tiếp vào các ô bên dưới để sửa chi tiết nhân vật!
              </div>

              {/* Gender & Art Style */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.4fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Giới tính:</label>
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
                      Nam
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
                      Nữ
                    </button>
                  </div>
                </div>

                <div>
                  <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>🎨 Phong cách nghệ thuật:</label>
                  <input
                    type="text"
                    value={config.character_style || ''}
                    onChange={(e) => setConfig((p) => ({ ...p, character_style: e.target.value }))}
                    placeholder="VD: Anime Nhật Bản mắt to sắc nét, Kyoto Animation..."
                    style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid rgba(56, 189, 248, 0.4)', borderRadius: 6 }}
                  />
                </div>
              </div>

              {/* Five Senses & Facial Features */}
              <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ fontSize: 12, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Eye size={14} /> Khuôn Mặt & Ngũ Quan (Mắt, Mũi, Miệng):
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Dáng Mắt & Thần thái:</label>
                    <input
                      type="text"
                      value={config.eye_shape || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, eye_shape: e.target.value }))}
                      placeholder="VD: Mắt anime to tròn long lanh tinh anh..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>

                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Màu Tròng Mắt:</label>
                    <input
                      type="text"
                      value={config.eye_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, eye_color: e.target.value }))}
                      placeholder="VD: Xanh lam ngọc phát sáng linh lực..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Sống Mũi:</label>
                    <input
                      type="text"
                      value={config.nose_shape || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, nose_shape: e.target.value }))}
                      placeholder="VD: Sống mũi thẳng cao thanh tú..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>

                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Khẩu Hình / Nụ Cười:</label>
                    <input
                      type="text"
                      value={config.mouth_style || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, mouth_style: e.target.value }))}
                      placeholder="VD: Cười nhếch môi tự tin..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>
                </div>
              </div>

              {/* Costume & Robes */}
              <div style={{ background: 'rgba(168, 85, 247, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ fontSize: 12, fontWeight: 800, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Shirt size={14} /> Trang Phục & Đạo Bào:
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Kiểu Trang Phục / Đạo Bào:</label>
                    <input
                      type="text"
                      value={config.costume_style || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, costume_style: e.target.value }))}
                      placeholder="VD: Đạo bào tu tiên thướt tha, tay áo rộng..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>

                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Màu sắc & Họa tiết:</label>
                    <input
                      type="text"
                      value={config.costume_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, costume_color: e.target.value }))}
                      placeholder="VD: Xanh lam phối trắng viền chỉ vàng kim..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#38bdf8', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 6 }}
                    />
                  </div>
                </div>
              </div>

              {/* Weapon & Prop Item */}
              <div style={{ background: 'rgba(234, 179, 8, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(234, 179, 8, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                <div style={{ fontSize: 12, fontWeight: 800, color: '#facc15', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Shield size={14} /> Pháp Bảo & Đồ Vật Nhân Vật Cầm:
                </div>
                <input
                  type="text"
                  value={config.prop_item || ''}
                  onChange={(e) => setConfig((p) => ({ ...p, prop_item: e.target.value }))}
                  placeholder="VD: Phi kiếm phát sáng linh lực lam ngọc, Quạt lông vũ..."
                  style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11, background: '#090d16', color: '#facc15', border: '1px solid rgba(234, 179, 8, 0.3)', borderRadius: 6 }}
                />
              </div>

              {/* Hair Style for Step 1 */}
              <div style={{ background: 'rgba(16, 185, 129, 0.06)', padding: 12, borderRadius: 8, border: '1px solid rgba(16, 185, 129, 0.25)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ fontSize: 12, fontWeight: 800, color: '#34d399' }}>💇 Kiểu Tóc & Trâm Cài Nhân Vật:</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Độ dài tóc:</label>
                    <input
                      type="text"
                      value={config.hair_length || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, hair_length: e.target.value }))}
                      placeholder="VD: Dài ngang lưng suôn mượt, Đuôi ngựa cao..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>

                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Màu sắc tóc:</label>
                    <input
                      type="text"
                      value={config.hair_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, hair_color: e.target.value }))}
                      placeholder="VD: Đen tuyền óng ả, Bạch kim, Xanh lam..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Chất tóc / Xoăn:</label>
                    <input
                      type="text"
                      value={config.hair_texture || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, hair_texture: e.target.value }))}
                      placeholder="VD: Thẳng mượt như suối lụa, Xoăn sóng bồng bềnh..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
                  </div>

                  <div>
                    <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 3 }}>Trâm cài / Phụ kiện tóc:</label>
                    <input
                      type="text"
                      value={config.hair_accessories || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, hair_accessories: e.target.value }))}
                      placeholder="VD: Trâm cài ngọc bích đính dải lụa, Vương miện vàng..."
                      style={{ width: '100%', height: 34, padding: '5px 8px', fontSize: 11, background: '#090d16', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
                    />
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
                <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Chọn linh kiện cần bóc tách:</label>
                <select
                  value={decomposedPartType}
                  onChange={(e) => setDecomposedPartType(e.target.value as any)}
                  style={{ width: '100%', height: 36, padding: '6px 10px', fontSize: 11.5, background: '#090d16', color: '#fff', border: '1px solid rgba(52, 211, 153, 0.5)', borderRadius: 6 }}
                >
                  <option value="body_turnaround_grid">🥋 Bảng Bóc Tách Toàn Bộ Cơ Thể & Đạo Bào (Full-Body Puppet 20 Linh Kiện - Chuẩn Hoạt Ảnh Trung Quốc)</option>
                  <option value="hair_multi_angle_grid">💇 Mái Tóc Đa Tầng (3 Dãy x 5 Góc Quay) - Khuyên dùng</option>
                  <option value="eyes_grid">👀 Đôi Mắt & Chớp Mắt (4 Dãy x 5 Cảm Xúc)</option>
                  <option value="mouth_grid">👄 Khẩu Hình Miệng & Lip-Sync Nói Chuyện (4 Dãy x 5 Cột)</option>
                  <option value="nose_chin_grid">👃 Sống Mũi, Cằm Nhọn 90° & Đôi Tai</option>
                  <option value="costume_grid">🥋 Trang Phục & Đạo Bào Rỗng Ruột</option>
                  <option value="weapons_grid">🗡️ Pháp Bảo & Vũ Khí Đa Góc</option>
                  <option value="limbs_hands_grid">🖐️ Tứ Chi & Bàn Tay Bắt Quyết Kiếm Ấn</option>
                </select>
              </div>

              {/* Background Type for Extraction */}
              <div>
                <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Phông nền xuất ảnh (Chroma Key):</label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, bg_type: 'chroma_green' }))}
                    style={{
                      padding: '8px 10px',
                      fontSize: 11,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.bg_type === 'chroma_green' ? '1px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
                      background: config.bg_type === 'chroma_green' ? 'rgba(34, 197, 94, 0.2)' : 'rgba(255,255,255,0.03)',
                      color: config.bg_type === 'chroma_green' ? '#4ade80' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    🟢 Xanh Lá (Chroma Green)
                  </button>
                  <button
                    onClick={() => setConfig((p) => ({ ...p, bg_type: 'pure_white' }))}
                    style={{
                      padding: '8px 10px',
                      fontSize: 11,
                      fontWeight: 600,
                      borderRadius: 6,
                      border: config.bg_type === 'pure_white' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                      background: config.bg_type === 'pure_white' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
                      color: config.bg_type === 'pure_white' ? '#7dd3fc' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    ⚪ Trắng Tinh (#FFFFFF)
                  </button>
                </div>
              </div>

              {/* Reference Image URL for Consistent Decomposition */}
              <div>
                <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
                  🔗 Link ảnh nhân vật mẫu Bước 1 (Tùy chọn cho Midjourney --sref):
                </label>
                <input
                  type="text"
                  value={referenceImageUrl}
                  onChange={(e) => setReferenceImageUrl(e.target.value)}
                  placeholder="Dán link ảnh Discord / Web của ảnh Bước 1 vào đây..."
                  style={{ width: '100%', height: 34, padding: '5px 10px', fontSize: 11, background: '#090d16', color: '#34d399', border: '1px solid rgba(52, 211, 153, 0.4)', borderRadius: 6 }}
                />
              </div>

              {/* Aspect Ratio Selector for Step 2 */}
              {renderAspectRatioSelector()}
            </div>
          )}

          {/* ─── STEP 3: ACTION KEYFRAMES & STORYTELLING CONTROLS ───── */}
          {workflowTab === 'step3_actions' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {/* Step Guide Banner */}
              <div style={{ background: 'rgba(168, 85, 247, 0.12)', padding: '10px 14px', borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.35)', fontSize: 11.5, color: '#f3e8ff', lineHeight: 1.5 }}>
                ⚔️ <b>Sinh kịch bản chiêu thức, biểu cảm cận cảnh & chuyển cảnh kịch tính 4K</b>. Dán prompt này vào AI để tạo Keyframe cao trào trong video motion comic!
              </div>

              {/* Action Type Selection */}
              <div>
                <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Thể loại phân cảnh:</label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  {[
                    { id: 'combat', label: '⚔️ Chiến Đấu & Tung Chiêu' },
                    { id: 'emotion', label: '😱 Cận Cảnh Biểu Cảm' },
                    { id: 'eat', label: '🍵 Ăn Uống & Thưởng Trà' },
                    { id: 'transition', label: '🌄 Chuyển Cảnh Tiên Cảnh' },
                  ].map((act) => (
                    <button
                      key={act.id}
                      onClick={() => setActionType(act.id as any)}
                      style={{
                        padding: '8px 10px',
                        fontSize: 11,
                        fontWeight: 600,
                        borderRadius: 6,
                        border: actionType === act.id ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.1)',
                        background: actionType === act.id ? 'linear-gradient(135deg, #a855f7, #7e22ce)' : 'rgba(255,255,255,0.03)',
                        color: '#fff',
                        cursor: 'pointer',
                        textAlign: 'left',
                      }}
                    >
                      {act.label}
                    </button>
                  ))}
                </div>
              </div>

              {/* Intensity level */}
              <div>
                <label style={{ fontSize: 11, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Cường độ hiệu ứng (VFX Intensity):</label>
                <div style={{ display: 'flex', gap: 6 }}>
                  {(['mild', 'intense', 'extreme'] as const).map((lvl) => (
                    <button
                      key={lvl}
                      onClick={() => setActionIntensity(lvl)}
                      style={{
                        flex: 1,
                        padding: '7px 10px',
                        fontSize: 11,
                        fontWeight: 600,
                        borderRadius: 6,
                        border: actionIntensity === lvl ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.1)',
                        background: actionIntensity === lvl ? '#9333ea' : 'rgba(255,255,255,0.03)',
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
      </div>

      {/* ─── RIGHT: Generated Dual Language Prompt & Copy (58%) ─────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 16, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto', minWidth: 0, width: '100%', boxSizing: 'border-box' }}>
        {/* Top Header & Copy Action */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sparkles size={16} />
            {workflowTab === 'step1_master'
              ? '🌟 PROMPT BƯỚC 1: BẢNG THIẾT KẾ NHÂN VẬT GỐC (MASTER 4K)'
              : workflowTab === 'step2_decomposed_parts'
              ? '✂️ PROMPT BƯỚC 2: BÓC TÁCH LINH KIỆN NHÂN VẬT ĐA GÓC QUAY'
              : '⚔️ PROMPT BƯỚC 3: KỊCH BẢN HÀNH ĐỘNG & CHIÊU THỨC (4K)'}
          </div>

          <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6, flexWrap: 'wrap' }}>
            <button
              onClick={() => setDisplayLangTab('both')}
              style={{ padding: '4px 8px', fontSize: 10.5, borderRadius: 4, border: 'none', background: displayLangTab === 'both' ? '#0284c7' : 'transparent', color: displayLangTab === 'both' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 600 }}
            >
              🌟 Tất cả
            </button>
            <button
              onClick={() => setDisplayLangTab('vietnamese')}
              style={{ padding: '4px 8px', fontSize: 10.5, borderRadius: 4, border: 'none', background: displayLangTab === 'vietnamese' ? '#0284c7' : 'transparent', color: displayLangTab === 'vietnamese' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 700 }}
            >
              🇻🇳 Tiếng Việt
            </button>
            <button
              onClick={() => setDisplayLangTab('gemini')}
              style={{ padding: '4px 8px', fontSize: 10.5, borderRadius: 4, border: 'none', background: displayLangTab === 'gemini' ? '#f59e0b' : 'transparent', color: displayLangTab === 'gemini' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 700 }}
            >
              🍌 Gemini/LLM
            </button>
            <button
              onClick={() => setDisplayLangTab('english')}
              style={{ padding: '4px 8px', fontSize: 10.5, borderRadius: 4, border: 'none', background: displayLangTab === 'english' ? '#0284c7' : 'transparent', color: displayLangTab === 'english' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 600 }}
            >
              🌐 Midjourney
            </button>
            <button
              onClick={() => setDisplayLangTab('json')}
              style={{ padding: '4px 8px', fontSize: 10.5, borderRadius: 4, border: 'none', background: displayLangTab === 'json' ? '#8b5cf6' : 'transparent', color: displayLangTab === 'json' ? '#fff' : '#94a3b8', cursor: 'pointer', fontWeight: 700 }}
            >
              📄 JSON
            </button>
          </div>
        </div>

        {/* Quick Copy Buttons */}
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button
            onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptGemini : currentAction.promptVi, 'gemini_copy')}
            style={{
              flex: 1,
              minWidth: 200,
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
            {copiedPrompt === 'gemini_copy' ? 'Đã Chép Tiếng Việt!' : '🇻🇳 Chép Prompt Tiếng Việt (AI Tự Hiểu)'}
          </button>

          <button
            onClick={() => handleCopy(workflowTab !== 'step3_actions' ? finalFullCopyText : `${currentAction.promptEn}\n\nNegative prompt:\n${currentAction.neg}`, 'full_en')}
            style={{
              flex: 1,
              minWidth: 200,
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
            {copiedPrompt === 'full_en' ? 'Đã Sao Chép Text!' : '🌐 Chép Cho Midjourney / Flux'}
          </button>
        </div>

        {/* Vietnamese Translation & Guide */}
        {(displayLangTab === 'vietnamese' || displayLangTab === 'both') && (
          <div style={{ background: '#0b1329', padding: 14, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.3)', width: '100%', boxSizing: 'border-box' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Globe size={13} /> Lệnh Tiếng Việt Tự Nhiên (AI Tự Đọc Hiểu & Thực Thi):
              </span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptVietnamese : currentAction.promptVi, 'vi_box')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8', border: '1px solid #38bdf8', cursor: 'pointer' }}
              >
                {copiedPrompt === 'vi_box' ? 'Đã Chép!' : 'Chép Đoạn Này'}
              </button>
            </div>
            <pre style={{ fontSize: 11.5, color: '#e2e8f0', lineHeight: 1.65, margin: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-word', fontFamily: 'inherit', width: '100%' }}>
              {workflowTab !== 'step3_actions' ? promptResult.promptVietnamese : currentAction.promptVi}
            </pre>
          </div>
        )}

        {/* Gemini Conversational Prompt Box */}
        {(displayLangTab === 'gemini' || displayLangTab === 'both') && (
          <div style={{ background: '#1c1917', padding: 14, borderRadius: 8, border: '1px solid #f59e0b', width: '100%', boxSizing: 'border-box' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#fbbf24', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Sparkles size={13} /> Lệnh Đối Thoại Cho Gemini / ChatGPT / DALL-E 3:
              </span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptGemini : currentAction.promptVi, 'gemini_box')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(245, 158, 11, 0.2)', color: '#fbbf24', border: '1px solid #f59e0b', cursor: 'pointer' }}
              >
                {copiedPrompt === 'gemini_box' ? 'Đã Chép!' : 'Chép Đoạn Này'}
              </button>
            </div>
            <pre style={{ fontSize: 11.5, color: '#fde68a', lineHeight: 1.65, margin: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-word', fontFamily: 'inherit', width: '100%' }}>
              {workflowTab !== 'step3_actions' ? promptResult.promptGemini : currentAction.promptVi}
            </pre>
          </div>
        )}

        {/* JSON Structured Prompt Box (Fixed Height 420px, Responsive Width) */}
        {(displayLangTab === 'json' || displayLangTab === 'both') && (
          <div style={{ background: '#070b14', padding: 14, borderRadius: 8, border: '1px solid #8b5cf6', width: '100%', boxSizing: 'border-box' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Sparkles size={13} /> Cấu Trúc JSON Chuẩn Cho AI (Co dãn theo màn hình, Chiều cao 450px):
              </span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? promptResult.promptJSON : JSON.stringify(currentAction, null, 2), 'json_box')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(139, 92, 246, 0.2)', color: '#c084fc', border: '1px solid #8b5cf6', cursor: 'pointer' }}
              >
                {copiedPrompt === 'json_box' ? 'Đã Chép JSON!' : 'Chép JSON'}
              </button>
            </div>
            <pre style={{
              fontSize: 11,
              color: '#a5f3fc',
              lineHeight: 1.55,
              margin: 0,
              width: '100%',
              height: '420px',
              maxHeight: '450px',
              overflow: 'auto',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
              boxSizing: 'border-box',
              background: 'rgba(0,0,0,0.5)',
              padding: 12,
              borderRadius: 6,
              fontFamily: 'monospace'
            }}>
              {workflowTab !== 'step3_actions' ? promptResult.promptJSON : JSON.stringify(currentAction, null, 2)}
            </pre>
          </div>
        )}

        {/* English Positive Prompt Box */}
        {(displayLangTab === 'english' || displayLangTab === 'both') && (
          <div style={{ background: '#090d16', padding: 14, borderRadius: 8, border: '1px solid rgba(255,255,255,0.1)', width: '100%', boxSizing: 'border-box' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#4ade80' }}>Prompt Tiếng Anh (Cho Midjourney / FLUX / SD - Max 4K):</span>
              <button
                onClick={() => handleCopy(workflowTab !== 'step3_actions' ? finalPromptEnglish : currentAction.promptEn, 'pos_only')}
                style={{ padding: '4px 8px', fontSize: 11, borderRadius: 4, background: 'rgba(74, 222, 128, 0.15)', color: '#4ade80', border: '1px solid #4ade80', cursor: 'pointer' }}
              >
                {copiedPrompt === 'pos_only' ? 'Đã Chép!' : 'Chép Đoạn Này'}
              </button>
            </div>
            <pre style={{ fontSize: 11, color: '#bbf7d0', lineHeight: 1.6, margin: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-word', fontFamily: 'inherit', width: '100%' }}>
              {workflowTab !== 'step3_actions' ? finalPromptEnglish : currentAction.promptEn}
            </pre>
          </div>
        )}
      </div>
    </div>
  );
};
