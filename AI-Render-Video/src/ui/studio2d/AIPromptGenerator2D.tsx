import React, { useState } from 'react';
import {
  Sparkles,
  Copy,
  Check,
  Swords,
  Scissors,
  User,
  Eye,
  Shirt,
  Shield,
  Maximize2,
  Layers,
  Sparkle,
  TreePine,
  Dog,
  Mountain,
  Waves,
  Boxes,
} from 'lucide-react';
import { AIPartPromptConfig, Character2DPartType } from '../../types/scene2d';
import { buildAIPromptForPart, AIPromptResult } from '../../core/assets/Asset2DRegistry';

interface CharacterTagItem {
  id: Character2DPartType;
  label: string;
  icon: string;
  desc: string;
}

interface CharacterPartGroup {
  groupTitle: string;
  items: CharacterTagItem[];
}

const CHARACTER_PART_GROUPS: CharacterPartGroup[] = [
  {
    groupTitle: '👤 1. Khuôn Mặt & Ngũ Quan (Tách Rời Từng Lớp)',
    items: [
      { id: 'toc_truoc', label: 'Mái Tóc Trước', icon: '💇', desc: 'Mái trước 6 góc (Ô 180° để rỗng)' },
      { id: 'toc_sau', label: 'Suối Tóc Sau', icon: '🌊', desc: 'Tóc sau phủ kín lưng 180°' },
      { id: 'khuon_mat_no_face', label: 'Mặt Trần (No Face)', icon: '👤', desc: 'Đầu trần V-line không ngũ quan' },
      { id: 'trong_den_iris', label: 'Mống Mắt (Đổi Màu)', icon: '🔮', desc: 'Tròng đen & mống mắt màu tách rời để dễ thay màu' },
      { id: 'trong_trang', label: 'Tròng Trắng (Sclera)', icon: '⚪', desc: 'Hốc tròng trắng làm nền lót' },
      { id: 'diem_sang_mat', label: 'Điểm Sáng (Highlight)', icon: '✨', desc: 'Chấm sáng lấp lánh phản chiếu mắt' },
      { id: 'mi_mat', label: 'Mi Mắt & Chớp Mắt', icon: '👁️', desc: 'Mí mắt trên/dưới & nháy mắt 3 trạng thái' },
      { id: 'long_may', label: 'Cặp Lông Mày', icon: '✏️', desc: 'Lông mày biểu cảm độc lập' },
      { id: 'mui', label: 'Sống Mũi', icon: '👃', desc: 'Sống mũi nhỏ thanh tú' },
      { id: 'doi_tai', label: 'Đôi Tai', icon: '👂', desc: 'Vành tai trái / phải' },
      { id: 'mieng', label: 'Khẩu Hình Miệng', icon: '👄', desc: 'Khẩu hình nói chuyện & cười' },
      { id: 'mat', label: 'Đôi Mắt Tổng Hợp', icon: '👀', desc: 'Mắt đầy đủ cả tròng và nền' },
    ],
  },
  {
    groupTitle: '🦾 2. Khớp Xương Cánh Tay & Bàn Tay (Trái / Phải)',
    items: [
      { id: 'than_co_ban', label: 'Thân Ngực & Eo', icon: '🥋', desc: 'Thân áo giáp không tay chân' },
      { id: 'canh_tay_trai', label: 'Cánh Tay Trái (Vai→Khuỷu)', icon: '🦾', desc: 'Bắp tay trái' },
      { id: 'cang_tay_trai', label: 'Cẳng Tay Trái (Khuỷu→Cổ)', icon: '🦾', desc: 'Cẳng tay trái' },
      { id: 'ban_tay_trai', label: 'Bàn Tay Trái', icon: '🖐️', desc: 'Bàn tay xòe/kiếm ấn trái' },
      { id: 'canh_tay_phai', label: 'Cánh Tay Phải (Vai→Khuỷu)', icon: '🦾', desc: 'Bắp tay phải' },
      { id: 'cang_tay_phai', label: 'Cẳng Tay Phải (Khuỷu→Cổ)', icon: '🦾', desc: 'Cẳng tay phải' },
      { id: 'ban_tay_phai', label: 'Bàn Tay Phải', icon: '🖐️', desc: 'Bàn tay cầm kiếm/quyết phải' },
    ],
  },
  {
    groupTitle: '🦵 3. Khớp Xương Đùi, Cẳng Chân & Giày (Trái / Phải)',
    items: [
      { id: 'dui_trai', label: 'Đùi Trái (Hông→Gối)', icon: '🦵', desc: 'Khớp đùi trái' },
      { id: 'cang_chan_trai', label: 'Cẳng Chân & Ủng Trái', icon: '🥾', desc: 'Cẳng chân và ủng trái' },
      { id: 'dui_phai', label: 'Đùi Phải (Hông→Gối)', icon: '🦵', desc: 'Khớp đùi phải' },
      { id: 'cang_chan_phai', label: 'Cẳng Chân & Ủng Phải', icon: '🥾', desc: 'Cẳng chân và ủng phải' },
    ],
  },
  {
    groupTitle: '👘 4. Trang Phục Bay & Vũ Khí Pháp Bảo',
    items: [
      { id: 'ao_choang', label: 'Áo Choàng / Tà Áo Bay', icon: '👘', desc: 'Áo choàng lưng buông bay' },
      { id: 'vu_khi', label: 'Vũ Khí & Pháp Bảo', icon: '🗡️', desc: 'Phi kiếm, quạt, bảo bối' },
    ],
  },
];

export const AIPromptGenerator2D: React.FC = () => {
  const [workflowTab, setWorkflowTab] = useState<'step1_master' | 'step2_decomposed_parts' | 'step3_actions'>('step1_master');
  
  // Category Selector for Step 2
  const [targetCategory, setTargetCategory] = useState<
    'character' | 'animal' | 'tree' | 'rock' | 'water' | 'mountain' | 'building'
  >('character');
  
  // Selected Tag for Character decomposition
  const [selectedTag, setSelectedTag] = useState<Character2DPartType>('toc_truoc');
  const [copiedPrompt, setCopiedPrompt] = useState<string | null>(null);

  // Master Character & Part Config State
  const [config, setConfig] = useState<AIPartPromptConfig>({
    workflow_step: 'step1_master_character',
    sheet_type: 'cinematic_single_part_2x3',
    part_type: 'toc_truoc',
    character_style: 'Anime 2D Nhật Bản mắt to long lanh, phong cách Kyoto Animation / Ufotable',
    custom_character_style: '',
    gender: 'nu',
    view_angle: 'all_angles_16_9',
    action_or_expression: 'Ánh mắt sắc bén, thần thái kiên định tự tin',
    color_theme: 'Đỏ rực ánh kim phối bạc sáng',
    special_features: 'Linh lực phát sáng nhẹ, tà áo bay phất phơ',
    clean_background: true,
    aspect_ratio: '16:9',
    bg_type: 'chroma_green',
    // Five Senses & Facial (Step 1)
    eye_shape: 'Mắt anime to tròn long lanh tinh anh',
    custom_eye_shape: '',
    eye_color: 'Xanh biếc ngọc bích phát sáng',
    custom_eye_color: '',
    nose_shape: 'Sống mũi thẳng cao thanh tú',
    custom_nose_shape: '',
    mouth_style: 'Cười nhếch môi tự tin',
    custom_mouth_style: '',
    ear_style: 'human_natural',
    // Costume & Robes (Step 1)
    costume_style: 'Áo giáp bạc ánh kim phối đạo bào thướt tha',
    custom_costume_style: '',
    costume_color: 'Bạc ánh kim phối viền chỉ đỏ',
    // Weapon & Prop Item (Step 1)
    prop_item: 'Thanh kiếm phát sáng linh lực lam ngọc',
    custom_prop_item: '',
    // Hair (Step 1 & Step 2)
    hair_length: 'Dài ngang lưng suôn mượt',
    custom_hair_length: '',
    hair_texture: 'Tóc đỏ rực rỡ bồng bềnh',
    custom_hair_texture: '',
    hair_color: 'Đỏ rực rỡ (#E62A2A)',
    custom_hair_color: '',
    hair_accessories: 'Trâm cài bạc đính dải lụa',
    custom_hair_accessories: '',
  });

  // Action Sequence Generator State (Step 3)
  const [actionType, setActionType] = useState<'combat' | 'dialogue' | 'emotion' | 'eat' | 'transition'>('combat');
  const [actionIntensity, setActionIntensity] = useState<'mild' | 'intense' | 'extreme'>('intense');

  // Derive Effective Prompt Config based on active workflow step
  const effectiveConfig: AIPartPromptConfig = {
    ...config,
    workflow_step:
      workflowTab === 'step1_master'
        ? 'step1_master_character'
        : workflowTab === 'step2_decomposed_parts'
        ? 'step2_decomposed_parts'
        : undefined,
    sheet_type: workflowTab === 'step1_master' ? 'body_turnaround_grid' : 'cinematic_single_part_2x3',
    part_type: selectedTag,
    aspect_ratio: '16:9',
  };

  const promptResult: AIPromptResult = buildAIPromptForPart(effectiveConfig);

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
          promptVi: `【 CẢNH CHIẾN ĐẤU & TUNG CHIÊU KIẾM KHÍ (4K - 16:9) 】\n• Mô tả: Vung kiếm chém ra luồng kiếm khí lam ngọc xé toang không gian, góc máy thấp kịch tính, vệt sáng chuyển động và bụi tiên khí bay tung tóe.\n• Phong cách: Anime 2D chuẩn Ufotable / Kyoto Animation, nét vẽ phẳng sắc nét.\n• Mức độ: ${actionIntensity === 'extreme' ? 'Cực đại (Nứt đất nổ trời)' : 'Mạnh mẽ'}.\n• Tỷ lệ: 16:9 Widescreen.`,
          cameraGuide: `Góc máy: Jump-cut cận cảnh (1.4x zoom) -> Rung lắc Camera Shake (intensity: 0.8, 0.4s) -> Lia nhanh sang đối thủ bị trúng đòn.`,
        };
      case 'emotion':
        return {
          title: 'Biểu Cảm & Cảm Xúc (Emotions / Close-up Reactions)',
          promptVi: `【 CẢNH BIỂU CẢM CẬN CẢNH (4K - 16:9) 】\n• Mô tả: Cận cảnh khuôn mặt biểu cảm sốc và bàng hoàng, đồng tử co dãn, giọt mồ hôi lăn nhẹ, tóc bay trước gió, ánh mắt lóe sáng linh lực.\n• Phong cách: Anime 2D mắt to tròn long lanh có đốm sáng phản chiếu.\n• Tỷ lệ: 16:9 Widescreen.`,
          cameraGuide: `Góc máy: Close-up trực diện mặt (Zoom 1.6x) -> Đổi layer mắt sang tức giận/kinh ngạc -> Phát âm thanh SFX chấn động tâm can.`,
        };
      case 'eat':
        return {
          title: 'Ăn Uống / Thưởng Trà (Eating / Casual Slice of Life)',
          promptVi: `【 CẢNH ĂN UỐNG & THƯỞNG TRÀ (4K - 16:9) 】\n• Mô tả: Nhân vật ngồi trong quán trà tu tiên, tay cầm chén trà bốc khói hoặc gắp bánh bao, biểu cảm vui vẻ thư thái, khói nóng lượn lờ.\n• Tỷ lệ: 16:9 Widescreen.`,
          cameraGuide: `Góc máy: Trung cảnh (Medium Shot) -> Đổi khẩu hình miệng mở/nhai theo chu kỳ nhịp nhàng -> Nhạc nền êm dịu.`,
        };
      case 'transition':
        return {
          title: 'Chuyển Cảnh Bản Đồ (Map Transition / Jump Cut)',
          promptVi: `【 CHUYỂN CẢNH BẢN ĐỒ TIÊN CẢNH (4K - 16:9) 】\n• Mô tả: Phong cảnh núi tiên hùng vĩ với đền ngọc bồng bềnh, ánh hoàng hôn buông xuống rừng trúc, mây mù cuộn trôi, các lớp tiền cảnh và hậu cảnh phân tách rõ ràng.\n• Tỷ lệ: 16:9 Widescreen.`,
          cameraGuide: `Góc máy: Parallax trôi chậm (Hậu cảnh 0.2x, Trung cảnh 1.0x, Tiền cảnh 1.8x) -> Hiệu ứng lóe sáng trắng chuyển cảnh.`,
        };
      default:
        return {
          title: 'Đối Thoại (Dialogue Taunt)',
          promptVi: `【 ĐỐI THOẠI & KHIÊU KHÍCH (4K - 16:9) 】\n• Mô tả: Nhân vật nửa người tư thế nói chuyện tự tin, ngón tay chỉ về phía trước, tà áo đạo bào bay trong gió.\n• Tỷ lệ: 16:9 Widescreen.`,
          cameraGuide: `Góc máy: Trung cảnh nửa người -> Kích hoạt Mouth Talk Cycle đồng bộ với giọng lồng tiếng Voice TTS.`,
        };
    }
  };

  const currentAction = generateActionSequencePrompt();

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
      {/* ════════════════════════════════════════════════════════════════════════════════
          CỘT TRÁI: CÁC BƯỚC ĐIỀU KHIỂN & CẤU HÌNH NHÂN VẬT / LINH KIỆN
      ════════════════════════════════════════════════════════════════════════════════ */}
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
        {/* Header Tabs 1-2-3 */}
        <div style={{ marginBottom: 12 }}>
          <div style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sparkles size={16} color="#38bdf8" /> Trợ Lý Tạo Prompt Hoạt Ảnh 2D
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr 1fr', gap: 6 }}>
            <button
              onClick={() => setWorkflowTab('step1_master')}
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
              <span style={{ fontSize: 9.5, opacity: 0.9 }}>Nhân Vật Gốc</span>
            </button>

            <button
              onClick={() => setWorkflowTab('step2_decomposed_parts')}
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
          {/* ─── BƯỚC 1: TẠO NHÂN VẬT GỐC ĐA GÓC QUAY ─── */}
          {workflowTab === 'step1_master' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ background: 'rgba(2, 132, 199, 0.12)', padding: '8px 12px', borderRadius: 8, border: '1px solid rgba(2, 132, 199, 0.35)', fontSize: 11, color: '#e0f2fe', lineHeight: 1.4 }}>
                🌟 <b>Tạo bảng vẽ nhân vật Anime 2D hoàn chỉnh</b> (Mắt to tròn long lanh, ngũ quan sắc nét, 5 góc quay đồng nhất trên Nền Trắng).
              </div>

              {/* Giới tính & Phong cách Anime */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.5fr', gap: 8 }}>
                <div>
                  <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 3 }}>Giới tính:</label>
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
                      Nam
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
                      Nữ
                    </button>
                  </div>
                </div>

                <div>
                  <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 3 }}>🎨 Phong cách Anime:</label>
                  <input
                    type="text"
                    value={config.character_style || ''}
                    onChange={(e) => setConfig((p) => ({ ...p, character_style: e.target.value }))}
                    placeholder="Anime 2D mắt to nét đẹp..."
                    style={{ width: '100%', height: 32, padding: '4px 8px', fontSize: 10.5, background: '#090d16', color: '#38bdf8', border: '1px solid rgba(56, 189, 248, 0.4)', borderRadius: 5, boxSizing: 'border-box' }}
                  />
                </div>
              </div>

              {/* Ngũ quan */}
              <div style={{ background: 'rgba(56, 189, 248, 0.06)', padding: 10, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                <div style={{ fontSize: 11.5, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                  <Eye size={13} /> Mắt, Mũi & Miệng Nhân Vật:
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Dáng Mắt:</label>
                    <input
                      type="text"
                      value={config.eye_shape || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, eye_shape: e.target.value }))}
                      style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Màu Mắt:</label>
                    <input
                      type="text"
                      value={config.eye_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, eye_color: e.target.value }))}
                      style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
                    />
                  </div>
                </div>
              </div>

              {/* Mái tóc */}
              <div style={{ background: 'rgba(236, 72, 153, 0.06)', padding: 10, borderRadius: 8, border: '1px solid rgba(236, 72, 153, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                <div style={{ fontSize: 11.5, fontWeight: 800, color: '#f472b6', display: 'flex', alignItems: 'center', gap: 5 }}>
                  💇 Mái Tóc:
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Màu Tóc:</label>
                    <input
                      type="text"
                      value={config.hair_color || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, hair_color: e.target.value }))}
                      style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Kiểu & Độ Dài:</label>
                    <input
                      type="text"
                      value={config.hair_texture || ''}
                      onChange={(e) => setConfig((p) => ({ ...p, hair_texture: e.target.value }))}
                      style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
                    />
                  </div>
                </div>
              </div>

              {/* Trang phục & Vũ khí */}
              <div style={{ background: 'rgba(168, 85, 247, 0.06)', padding: 10, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                <div style={{ fontSize: 11.5, fontWeight: 800, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 5 }}>
                  <Shirt size={13} /> Trang Phục & Vũ Khí:
                </div>
                <input
                  type="text"
                  value={config.costume_style || ''}
                  onChange={(e) => setConfig((p) => ({ ...p, costume_style: e.target.value }))}
                  placeholder="Áo giáp bạc, tà áo thướt tha..."
                  style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
                />
                <input
                  type="text"
                  value={config.prop_item || ''}
                  onChange={(e) => setConfig((p) => ({ ...p, prop_item: e.target.value }))}
                  placeholder="Kiếm phát sáng, pháp bảo..."
                  style={{ width: '100%', height: 30, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#facc15', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 5, boxSizing: 'border-box' }}
                />
              </div>
            </div>
          )}

          {/* ─── BƯỚC 2: BÓC TÁCH KHỚP XƯƠNG GIẢI PHẪU (NHÂN VẬT & CÁC THỂ LOẠI) ─── */}
          {workflowTab === 'step2_decomposed_parts' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ background: 'rgba(16, 185, 129, 0.12)', padding: '8px 12px', borderRadius: 8, border: '1px solid rgba(16, 185, 129, 0.35)', fontSize: 11, color: '#d1fae5', lineHeight: 1.4 }}>
                ✂️ <b>Giải phẫu bóc tách từng khớp xương & ngũ quan thành Bảng 6 Góc Điện Ảnh 16:9</b> để phục vụ gắn xương IK/FK và chuyển động hoạt ảnh mượt mà!
              </div>

              {/* Thể Loại Đối Tượng Bóc Tách */}
              <div>
                <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
                  📂 Thể Loại Đối Tượng Bóc Tách:
                </label>
                <select
                  value={targetCategory}
                  onChange={(e) => setTargetCategory(e.target.value as any)}
                  style={{ width: '100%', height: 34, padding: '4px 8px', fontSize: 11, background: '#090d16', color: '#34d399', border: '1px solid rgba(52, 211, 153, 0.5)', borderRadius: 6 }}
                >
                  <option value="character">🧑 Nhân Vật (Giải Phẫu Khớp Xương 2D) — [ĐẦY ĐỦ LOGIC]</option>
                  <option value="animal">🐾 Động Vật & Linh Thú (Sắp có)</option>
                  <option value="tree">🌳 Cây Cối & Thảo Mộc (Sắp có)</option>
                  <option value="rock">🪨 Đá Sỏi & Khoáng Thạch (Sắp có)</option>
                  <option value="water">🌊 Sông Nước & Thác Nước (Sắp có)</option>
                  <option value="mountain">🏔️ Đồi Núi & Địa Hình (Sắp có)</option>
                  <option value="building">🏠 Kiến Trúc & Nhà Cửa (Sắp có)</option>
                </select>
              </div>

              {/* Phân Nhóm Giải Phẫu Khớp Xương (Khi chọn Nhân Vật) */}
              {targetCategory === 'character' ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  {CHARACTER_PART_GROUPS.map((group, gIdx) => (
                    <div
                      key={gIdx}
                      style={{
                        background: 'rgba(255, 255, 255, 0.02)',
                        border: '1px solid rgba(255, 255, 255, 0.08)',
                        borderRadius: 8,
                        padding: '8px 10px',
                      }}
                    >
                      <div style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', marginBottom: 6 }}>
                        {group.groupTitle}
                      </div>

                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                        {group.items.map((tag) => {
                          const isSelected = selectedTag === tag.id;
                          return (
                            <button
                              key={tag.id}
                              onClick={() => {
                                setSelectedTag(tag.id);
                                setConfig((p) => ({ ...p, part_type: tag.id, sheet_type: 'cinematic_single_part_2x3' }));
                              }}
                              style={{
                                padding: '6px 8px',
                                fontSize: 10.5,
                                fontWeight: 700,
                                borderRadius: 6,
                                border: isSelected ? '1px solid #34d399' : '1px solid rgba(255,255,255,0.08)',
                                background: isSelected ? 'rgba(16, 185, 129, 0.25)' : 'rgba(255,255,255,0.03)',
                                color: isSelected ? '#4ade80' : '#cbd5e1',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: 6,
                                textAlign: 'left',
                                transition: 'all 0.15s ease',
                              }}
                            >
                              <span style={{ fontSize: 14 }}>{tag.icon}</span>
                              <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                <div>{tag.label}</div>
                              </div>
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div style={{ padding: 14, background: 'rgba(255,255,255,0.03)', borderRadius: 8, border: '1px dashed rgba(255,255,255,0.15)', textAlign: 'center', color: '#94a3b8', fontSize: 11 }}>
                  🚧 Thể loại này đang được phát triển phân tích bóc tách các lớp chuyên biệt. Hiện tại vui lòng chọn <b>🧑 Nhân Vật</b> để trải nghiệm đầy đủ các khớp giải phẫu!
                </div>
              )}

              {/* Màu nền tách phông */}
              <div>
                <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Màu nền tách phông (Chroma Key):</label>
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
                    🟢 Xanh Lá (#00FF00)
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
                    ⚪ Trắng Tinh (#FFFFFF)
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* ─── BƯỚC 3: KỊCH BẢN HÀNH ĐỘNG & CHIÊU THỨC ─── */}
          {workflowTab === 'step3_actions' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ background: 'rgba(168, 85, 247, 0.12)', padding: '8px 12px', borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.35)', fontSize: 11, color: '#f3e8ff', lineHeight: 1.4 }}>
                ⚔️ <b>Sinh kịch bản chuyển cảnh, tung chiêu và biểu cảm cho Motion Comic 4K</b>.
              </div>

              <div>
                <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>Loại cảnh quay:</label>
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

      {/* ════════════════════════════════════════════════════════════════════════════════
          CỘT PHẢI: HIỂN THỊ PROMPT TIẾNG VIỆT CHUẨN ĐIỆN ẢNH & NÚT SAO CHÉP LỚN
      ════════════════════════════════════════════════════════════════════════════════ */}
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
        {/* Header Bar */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10, paddingBottom: 8, borderBottom: '1px solid rgba(255,255,255,0.08)' }}>
          <div style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
            {workflowTab === 'step1_master'
              ? '📋 PROMPT TIẾNG VIỆT BƯỚC 1: TẠO NHÂN VẬT ANIME GỐC (16:9)'
              : workflowTab === 'step2_decomposed_parts'
              ? '✂️ PROMPT TIẾNG VIỆT BƯỚC 2: BÓC TÁCH 6 GÓC ĐIỆN ẢNH (2 HÀNG × 3 CỘT — 16:9)'
              : '⚔️ PROMPT TIẾNG VIỆT BƯỚC 3: KỊCH BẢN HÀNH ĐỘNG 4K'}
          </div>

          <div style={{ fontSize: 10.5, color: '#38bdf8', background: 'rgba(56, 189, 248, 0.15)', padding: '3px 8px', borderRadius: 4, fontWeight: 700 }}>
            Tỷ Lệ 16:9 Chuẩn
          </div>
        </div>

        {/* Big Copy Button */}
        <button
          onClick={() =>
            handleCopy(
              workflowTab === 'step1_master'
                ? promptResult.promptVietnamese
                : workflowTab === 'step2_decomposed_parts'
                ? promptResult.promptVietnamese
                : currentAction.promptVi,
              'main_copy'
            )
          }
          style={{
            width: '100%',
            height: 44,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            fontSize: 13,
            fontWeight: 800,
            borderRadius: 8,
            background: copiedPrompt === 'main_copy' ? '#22c55e' : 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)',
            color: '#fff',
            border: 'none',
            cursor: 'pointer',
            boxShadow: '0 4px 14px rgba(2, 132, 199, 0.4)',
            marginBottom: 12,
            transition: 'all 0.2s',
          }}
        >
          {copiedPrompt === 'main_copy' ? <Check size={18} /> : <Copy size={18} />}
          {copiedPrompt === 'main_copy'
            ? '✓ ĐÃ SAO CHÉP PROMPT TIẾNG VIỆT!'
            : '📋 SAO CHÉP PROMPT TIẾNG VIỆT (DÁN VÀO BANANA PRO / AI)'}
        </button>

        {/* Prompt Content Box (Pure Vietnamese) */}
        <div
          style={{
            flex: 1,
            background: '#040711',
            borderRadius: 8,
            border: '1px solid rgba(56, 189, 248, 0.25)',
            padding: 14,
            overflowY: 'auto',
          }}
        >
          <pre
            style={{
              fontSize: 12,
              color: '#e0f2fe',
              lineHeight: 1.7,
              margin: 0,
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
              fontFamily: 'inherit',
            }}
          >
            {workflowTab !== 'step3_actions' ? promptResult.promptVietnamese : currentAction.promptVi}
          </pre>

          {/* Camera Guide for Action Sequence */}
          {workflowTab === 'step3_actions' && currentAction.cameraGuide && (
            <div style={{ marginTop: 14, padding: 10, background: 'rgba(168, 85, 247, 0.15)', borderRadius: 6, border: '1px solid rgba(168, 85, 247, 0.3)', fontSize: 11, color: '#e9d5ff' }}>
              🎥 <b>Hướng dẫn đạo diễn chuyển cảnh:</b> {currentAction.cameraGuide}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
