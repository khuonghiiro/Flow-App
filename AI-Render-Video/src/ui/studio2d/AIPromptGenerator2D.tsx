import React, { useState } from 'react';
import {
  Sparkles,
  Scissors,
  User,
  Swords,
} from 'lucide-react';
import { AIPartPromptConfig, Character2DPartType } from '../../types/scene2d';
import { buildAIPromptForPart, buildFilenameVariants, AIPromptResult } from '../../core/assets/Asset2DRegistry';
import { Step1MasterForm } from './prompt/Step1MasterForm';
import { Step2DecomposedForm, CHARACTER_PART_GROUPS } from './prompt/Step2DecomposedForm';
import { PromptOutputPanel } from './prompt/PromptOutputPanel';
import { JsonSchemaGuideModal } from './prompt/JsonSchemaGuideModal';

export const AIPromptGenerator2D: React.FC = () => {
  const [workflowTab, setWorkflowTab] = useState<'step1_master' | 'step2_decomposed_parts' | 'step3_actions'>('step1_master');
  const [promptFormatTab, setPromptFormatTab] = useState<'en' | 'vi' | 'json'>('en');

  // Category & Decomposed selection state (Step 2)
  const [targetCategory, setTargetCategory] = useState<
    'character' | 'animal' | 'tree' | 'rock' | 'water' | 'mountain' | 'building'
  >('character');
  const [selectedTag, setSelectedTag] = useState<Character2DPartType>('toc_truoc');
  const [step2Layout, setStep2Layout] = useState<'single_isolated_1x1' | 'seamless_turnaround_1x4' | 'cinematic_single_part_2x3'>('single_isolated_1x1');
  const [step2Angle, setStep2Angle] = useState<'front' | 'three_quarter' | 'profile_side' | 'back' | 'high_angle' | 'low_angle'>('front');
  const [copiedPrompt, setCopiedPrompt] = useState<string | null>(null);

  // Sync Count, Base Prompt & Scope options (Step 2)
  const [batchCount, setBatchCount] = useState<number>(1);
  const [includeBasePrompt, setIncludeBasePrompt] = useState<boolean>(true);
  const [jsonExportScope, setJsonExportScope] = useState<'component_all_angles' | 'single_angle' | 'group_all_parts'>('component_all_angles');
  const [isSchemaModalOpen, setIsSchemaModalOpen] = useState<boolean>(false);

  // Master Character & Part Config State
  const [config, setConfig] = useState<AIPartPromptConfig>({
    workflow_step: 'step1_master_character',
    sheet_type: 'single_isolated_1x1',
    part_type: 'toc_truoc',
    character_style: 'Chinese Guoman / 国漫 Xianxia Chibi Anime',
    custom_character_style: '',
    gender: 'nu',
    body_proportion: 'chibi_2_5',
    custom_body_proportion: '',
    view_angle: 'front',
    action_or_expression: 'Mỉm cười thanh tao nhẹ nhàng, thần thái tiên tử bí ẩn',
    color_theme: 'Trắng bạch kim phối tím nhạt viền ngọc bích',
    special_features: 'Linh lực phát sáng nhẹ, tà áo bay phất phơ',
    clean_background: true,
    aspect_ratio: 'auto',
    bg_type: 'chroma_green',
    // Five Senses & Facial (Step 1)
    eye_shape: 'Mắt anime to tròn long lanh tinh anh',
    custom_eye_shape: '',
    eye_color: 'Lam ngọc sáng lấp lánh (Sparkling Platinum Blue)',
    custom_eye_color: '',
    nose_shape: 'Sống mũi nhỏ thanh tú',
    custom_nose_shape: '',
    mouth_style: 'Khẩu hình cười mỉm nhỏ nhắn môi mảnh',
    custom_mouth_style: '',
    ear_style: 'human_natural',
    // Costume & Robes (Step 1)
    costume_style: 'Đạo bào Hanfu tu tiên trắng lụa, viền tím nhạt, tà áo thướt tha dải lụa bay',
    custom_costume_style: '',
    costume_color: 'Trắng bạch kim phối tím nhạt viền ngọc bích',
    // Weapon & Prop Item (Step 1)
    prop_item: 'Kiếm tiên phát sáng linh lực lam ngọc',
    custom_prop_item: '',
    // Hair (Step 1 & Step 2)
    hair_length: 'Dài quá eo buông xõa mượt mà',
    custom_hair_length: '',
    hair_texture: 'Tóc thẳng suôn mượt rẽ ngôi giữa kèm 2 lọn ôm má',
    custom_hair_texture: '',
    hair_color: 'Bạch kim ánh bạc (Platinum White)',
    custom_hair_color: '',
    hair_accessories: 'Trâm cài ngọc bích đính dải lụa xanh ngọc',
    custom_hair_accessories: '',
    batch_count: 1,
    base_count: 1,
  });

  const [baseCount, setBaseCount] = useState<number>(1);

  // Action Sequence Generator State (Step 3)
  const [actionType, setActionType] = useState<'combat' | 'dialogue' | 'emotion' | 'eat' | 'transition'>('combat');

  // Derive Effective Prompt Config based on active workflow step
  const effectiveConfig: AIPartPromptConfig = {
    ...config,
    workflow_step:
      workflowTab === 'step1_master'
        ? 'step1_master_character'
        : workflowTab === 'step2_decomposed_parts'
        ? 'step2_decomposed_parts'
        : undefined,
    sheet_type:
      workflowTab === 'step1_master'
        ? 'body_turnaround_grid'
        : step2Layout,
    part_type: selectedTag,
    view_angle: workflowTab === 'step2_decomposed_parts' ? step2Angle : config.view_angle,
    aspect_ratio: workflowTab === 'step1_master' ? '16:9' : (config.aspect_ratio || 'auto'),
    include_base_prompt: includeBasePrompt,
    batch_count: batchCount,
    base_count: baseCount,
    json_scope: jsonExportScope === 'single_angle' ? 'single_angle' : 'all_angles',
  };

  const promptResult: AIPromptResult = buildAIPromptForPart(effectiveConfig);

  const activeGroup = CHARACTER_PART_GROUPS.find((g) => g.items.some((it) => it.id === selectedTag)) || CHARACTER_PART_GROUPS[0];

  const getGroupBananaProJSON = () => {
    const groupPrompts: any[] = [];
    activeGroup.items.forEach((item) => {
      const itemConfig: AIPartPromptConfig = {
        ...effectiveConfig,
        part_type: item.id,
        json_scope: 'all_angles',
      };
      const res = buildAIPromptForPart(itemConfig);
      try {
        const parsed = JSON.parse(res.promptJSON);
        if (Array.isArray(parsed.prompts)) {
          groupPrompts.push(...parsed.prompts);
        }
      } catch (e) {
        // ignore
      }
    });

    const baseRef = buildAIPromptForPart({ ...effectiveConfig, part_type: selectedTag, include_base_prompt: true }).promptJSON;
    let baseText = '';
    let baseRule = '';
    try {
      const parsed = JSON.parse(baseRef);
      baseText = parsed.base_prompt || '';
      baseRule = parsed.rule || '';
    } catch (e) {}

    const jsonPayload: any = {};
    if (includeBasePrompt && baseText) {
      const baseFnMeta = buildFilenameVariants('master_character_turnaround.png', baseCount);
      jsonPayload.base_prompt = baseText;
      jsonPayload.base_aspect_ratio = '16:9';
      jsonPayload.base_count = baseCount;
      jsonPayload.base_save_filename = baseFnMeta.save_filename;
      jsonPayload.base_save_filenames = baseFnMeta.save_filenames;
      jsonPayload.base_candidate_selection = baseFnMeta.candidate_selection;
    }
    if (baseRule) {
      jsonPayload.rule = baseRule;
    }
    jsonPayload.prompts = groupPrompts;

    return JSON.stringify(jsonPayload, null, 2);
  };

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedPrompt(id);
    setTimeout(() => setCopiedPrompt(null), 2000);
  };

  const generateActionSequencePrompt = () => {
    switch (actionType) {
      case 'combat':
        return {
          promptVi: `【 CẢNH CHIẾN ĐẤU & TUNG CHIÊU KIẾM KHÍ (4K - 16:9) 】\n• Mô tả: Vung kiếm chém ra luồng kiếm khí lam ngọc xé toang không gian, góc máy thấp kịch tính, vệt sáng chuyển động và bụi tiên khí bay tung tóe.\n• Phong cách: Anime 2D chuẩn Ufotable / Kyoto Animation, nét vẽ phẳng sắc nét.\n• Tỷ lệ: 16:9.`,
          cameraGuide: `Góc máy: Jump-cut cận cảnh (1.4x zoom) -> Rung lắc Camera Shake (intensity: 0.8, 0.4s) -> Lia nhanh sang đối thủ bị trúng đòn.`,
        };
      case 'emotion':
        return {
          promptVi: `【 CẢNH BIỂU CẢM CẬN CẢNH (4K - 16:9) 】\n• Mô tả: Cận cảnh khuôn mặt biểu cảm sốc và bàng hoàng, đồng tử co dãn, giọt mồ hôi lăn nhẹ, tóc bay trước gió, ánh mắt lóe sáng linh lực.\n• Tỷ lệ: 16:9.`,
          cameraGuide: `Góc máy: Close-up trực diện mặt (Zoom 1.6x) -> Đổi layer mắt sang tức giận/kinh ngạc -> Phát âm thanh SFX chấn động tâm can.`,
        };
      case 'eat':
        return {
          promptVi: `【 CẢNH ĂN UỐNG & THƯỞNG TRÀ (4K - 16:9) 】\n• Mô tả: Nhân vật ngồi trong quán trà tu tiên, tay cầm chén trà bốc khói hoặc gắp bánh bao, biểu cảm vui vẻ thư thái, khói nóng lượn lờ.\n• Tỷ lệ: 16:9.`,
          cameraGuide: `Góc máy: Trung cảnh (Medium Shot) -> Đổi khẩu hình miệng mở/nhai theo chu kỳ nhịp nhàng.`,
        };
      case 'transition':
        return {
          promptVi: `【 CHUYỂN CẢNH BẢN ĐỒ TIÊN CẢNH (4K - 16:9) 】\n• Mô tả: Phong cảnh núi tiên hùng vĩ với đền ngọc bồng bềnh, ánh hoàng hôn buông xuống rừng trúc, mây mù cuộn trôi, các lớp tiền cảnh và hậu cảnh phân tách rõ ràng.\n• Tỷ lệ: 16:9.`,
          cameraGuide: `Góc máy: Parallax trôi chậm (Hậu cảnh 0.2x, Trung cảnh 1.0x, Tiền cảnh 1.8x) -> Hiệu ứng lóe sáng trắng chuyển cảnh.`,
        };
      default:
        return {
          promptVi: `【 ĐỐI THOẠI & KHIÊU KHÍCH (4K - 16:9) 】\n• Mô tả: Nhân vật nửa người tư thế nói chuyện tự tin, ngón tay chỉ về phía trước, tà áo đạo bào bay trong gió.\n• Tỷ lệ: 16:9.`,
          cameraGuide: `Góc máy: Trung cảnh nửa người -> Kích hoạt Mouth Talk Cycle đồng bộ với giọng lồng tiếng Voice TTS.`,
        };
    }
  };

  const currentAction = generateActionSequencePrompt();

  const getActivePromptText = () => {
    if (workflowTab === 'step3_actions') return currentAction.promptVi;
    if (workflowTab === 'step1_master') {
      if (promptFormatTab === 'vi') return promptResult.promptVietnamese;
      return promptResult.promptEnglish;
    }
    // step2_decomposed_parts
    if (promptFormatTab === 'en') return promptResult.promptEnglish;
    if (promptFormatTab === 'vi') return promptResult.promptVietnamese;
    if (jsonExportScope === 'group_all_parts') {
      return getGroupBananaProJSON();
    }
    return promptResult.promptJSON;
  };

  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: 'minmax(460px, 520px) 1fr',
        gap: 16,
        height: '100%',
        padding: '12px 16px',
        background: '#040711',
        overflow: 'hidden',
        color: '#f8fafc',
      }}
    >
      {/* ─── CỘT TRÁI: CẤU HÌNH BƯỚC 1 - 2 - 3 ─── */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          height: '100%',
          background: '#090d16',
          borderRadius: 10,
          border: '1px solid rgba(255, 255, 255, 0.08)',
          padding: 14,
          overflow: 'hidden',
        }}
      >
        {/* Header Tabs */}
        <div style={{ marginBottom: 12 }}>
          <div style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sparkles size={16} color="#38bdf8" /> Trợ Lý Tạo Prompt Hoạt Ảnh 2D
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr 1fr', gap: 6 }}>
            <button
              onClick={() => {
                setWorkflowTab('step1_master');
                setPromptFormatTab('en');
              }}
              style={{
                padding: '8px 6px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 8,
                border: workflowTab === 'step1_master' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                background: workflowTab === 'step1_master' ? 'linear-gradient(135deg, #0284c7, #2563eb)' : 'rgba(255,255,255,0.03)',
                color: workflowTab === 'step1_master' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 2,
              }}
            >
              <User size={14} />
              <span>BƯỚC 1</span>
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Bảng Xoay 16:9</span>
            </button>

            <button
              onClick={() => {
                setWorkflowTab('step2_decomposed_parts');
                setPromptFormatTab('json');
              }}
              style={{
                padding: '8px 6px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 8,
                border: workflowTab === 'step2_decomposed_parts' ? '1px solid #34d399' : '1px solid rgba(255,255,255,0.08)',
                background: workflowTab === 'step2_decomposed_parts' ? 'linear-gradient(135deg, #10b981, #059669)' : 'rgba(255,255,255,0.03)',
                color: workflowTab === 'step2_decomposed_parts' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 2,
              }}
            >
              <Scissors size={14} />
              <span>BƯỚC 2</span>
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Bóc Tách Khớp Xương</span>
            </button>

            <button
              onClick={() => setWorkflowTab('step3_actions')}
              style={{
                padding: '8px 6px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 8,
                border: workflowTab === 'step3_actions' ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.08)',
                background: workflowTab === 'step3_actions' ? 'linear-gradient(135deg, #a855f7, #7e22ce)' : 'rgba(255,255,255,0.03)',
                color: workflowTab === 'step3_actions' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 2,
              }}
            >
              <Swords size={14} />
              <span>BƯỚC 3</span>
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Kịch Bản 4K</span>
            </button>
          </div>
        </div>

        {/* Scrollable Form Body */}
        <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12, paddingRight: 4 }}>
          {workflowTab === 'step1_master' && (
            <Step1MasterForm config={config} setConfig={setConfig} />
          )}

          {workflowTab === 'step2_decomposed_parts' && (
            <Step2DecomposedForm
              config={config}
              setConfig={setConfig}
              step2Layout={step2Layout}
              setStep2Layout={setStep2Layout}
              step2Angle={step2Angle}
              setStep2Angle={setStep2Angle}
              targetCategory={targetCategory}
              setTargetCategory={setTargetCategory}
              selectedTag={selectedTag}
              setSelectedTag={setSelectedTag}
            />
          )}

          {workflowTab === 'step3_actions' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ background: 'rgba(168, 85, 247, 0.12)', padding: '8px 12px', borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.35)', fontSize: 11, color: '#f3e8ff', lineHeight: 1.4 }}>
                ⚔️ <b>Sinh kịch bản chuyển cảnh, tung chiêu và biểu cảm cho Motion Comic 4K</b>.
              </div>

              <div>
                <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
                  Loại cảnh quay:
                </label>
                <select
                  value={actionType}
                  onChange={(e) => setActionType(e.target.value as any)}
                  style={{ width: '100%', height: 32, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#c084fc', border: '1px solid rgba(168, 85, 247, 0.4)', borderRadius: 5 }}
                >
                  <option value="combat">⚔️ Chiến Đấu & Tung Chiêu Kiếm Khí</option>
                  <option value="dialogue">💬 Đối Thoại & Khiêu Khích</option>
                  <option value="emotion">😱 Biểu Cảm Sốc / Kinh Ngạc (Close-up)</option>
                  <option value="eat">🍵 Ăn Uống & Thưởng Trà</option>
                  <option value="transition">🌄 Chuyển Cảnh Bản Đồ Tiên Cảnh</option>
                </select>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ─── CỘT PHẢI: HIỂN THỊ KẾT QUẢ PROMPT / JSON & BỘ ĐIỀU KHIỂN ─── */}
      <PromptOutputPanel
        workflowTab={workflowTab}
        promptFormatTab={promptFormatTab}
        setPromptFormatTab={setPromptFormatTab}
        activePromptText={getActivePromptText()}
        copiedPrompt={copiedPrompt}
        handleCopy={handleCopy}
        batchCount={batchCount}
        setBatchCount={(cnt) => {
          setBatchCount(cnt);
          setConfig((p) => ({ ...p, batch_count: cnt }));
        }}
        baseCount={baseCount}
        setBaseCount={(cnt) => {
          setBaseCount(cnt);
          setConfig((p) => ({ ...p, base_count: cnt }));
        }}
        includeBasePrompt={includeBasePrompt}
        setIncludeBasePrompt={setIncludeBasePrompt}
        jsonExportScope={jsonExportScope}
        setJsonExportScope={setJsonExportScope}
        step2Layout={step2Layout}
        activeGroupItemCount={activeGroup.items.length}
        onOpenSchemaGuide={() => setIsSchemaModalOpen(true)}
        cameraGuide={workflowTab === 'step3_actions' ? currentAction.cameraGuide : undefined}
      />

      {/* ─── MODAL TRA CỨU JSON SCHEMA GUIDE ─── */}
      <JsonSchemaGuideModal
        isOpen={isSchemaModalOpen}
        onClose={() => setIsSchemaModalOpen(false)}
      />
    </div>
  );
};
