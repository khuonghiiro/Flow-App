import React from 'react';
import { Maximize2, Scissors } from 'lucide-react';
import { AIPartPromptConfig, Character2DPartType } from '../../../types/scene2d';

export interface CharacterTagItem {
  id: Character2DPartType;
  label: string;
  icon: string;
  desc: string;
}

export interface CharacterPartGroup {
  groupTitle: string;
  items: CharacterTagItem[];
}

export const CHARACTER_PART_GROUPS: CharacterPartGroup[] = [
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

const ASPECT_RATIO_OPTIONS = [
  { id: 'auto', label: '✨ Tự động theo linh kiện (Mắt/Mũi/Miệng/Tay 1:1, Mặt/Mái/Thân 3:4, Tóc dài/Chân/Kiếm 9:16)' },
  { id: '1:1', label: '1:1 Vuông (Mắt, Mũi, Miệng, Lông Mày, Bàn Tay)' },
  { id: '3:4', label: '3:4 Dọc Vừa (Mặt Trần, Mái Tóc Trước, Thân, Bắp Tay, Cẳng Tay)' },
  { id: '9:16', label: '9:16 Dọc Cao (Suối Tóc Sau Lưng, Khớp Đùi, Cẳng Chân & Ủng, Áo Choàng, Kiếm)' },
  { id: '16:9', label: '16:9 Rộng Ngang (Bảng Turnaround 5 Góc, Chuỗi Xoay 1x4, Sprite 2x3, Đôi Tai 2 Cột)' },
  { id: '4:3', label: '4:3 Ngang Vừa (Khung Đôi Tiêu Chuẩn)' },
];

interface Step2DecomposedFormProps {
  config: AIPartPromptConfig;
  setConfig: React.Dispatch<React.SetStateAction<AIPartPromptConfig>>;
  step2Layout: 'single_isolated_1x1' | 'seamless_turnaround_1x4' | 'cinematic_single_part_2x3';
  setStep2Layout: (layout: 'single_isolated_1x1' | 'seamless_turnaround_1x4' | 'cinematic_single_part_2x3') => void;
  step2Angle: string;
  setStep2Angle: (angle: any) => void;
  targetCategory: string;
  setTargetCategory: (cat: any) => void;
  selectedTag: Character2DPartType;
  setSelectedTag: (tag: Character2DPartType) => void;
}

export const Step2DecomposedForm: React.FC<Step2DecomposedFormProps> = ({
  config,
  setConfig,
  step2Layout,
  setStep2Layout,
  step2Angle,
  setStep2Angle,
  targetCategory,
  setTargetCategory,
  selectedTag,
  setSelectedTag,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div
        style={{
          background: 'rgba(16, 185, 129, 0.12)',
          padding: '8px 12px',
          borderRadius: 8,
          border: '1px solid rgba(16, 185, 129, 0.35)',
          fontSize: 11,
          color: '#d1fae5',
          lineHeight: 1.4,
        }}
      >
        ✂️ <b>Giải phẫu bóc tách từng linh kiện & khớp xương</b> phục vụ gắn xương IK/FK và chuyển động hoạt ảnh 2D mượt mà!
      </div>

      {/* Bố Cục Bóc Tách (Layout Mode) */}
      <div style={{ background: 'rgba(56, 189, 248, 0.05)', padding: 10, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <label style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Maximize2 size={13} /> 📐 Bố Cục Xuất Ảnh (Layout Mode):
        </label>
        <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr 1fr', gap: 6 }}>
          <button
            onClick={() => setStep2Layout('single_isolated_1x1')}
            style={{
              padding: '7px 6px',
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 6,
              border: step2Layout === 'single_isolated_1x1' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
              background: step2Layout === 'single_isolated_1x1' ? 'linear-gradient(135deg, rgba(2, 132, 199, 0.4), rgba(56, 189, 248, 0.2))' : 'rgba(255,255,255,0.03)',
              color: step2Layout === 'single_isolated_1x1' ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 2,
              textAlign: 'center',
            }}
          >
            <span>🖼️ Ảnh Đơn Biệt Lập</span>
            <span style={{ fontSize: 9, opacity: 0.85, color: '#4ade80' }}>★ Nét Căng 4K - Không Lưới</span>
          </button>

          <button
            onClick={() => setStep2Layout('seamless_turnaround_1x4')}
            style={{
              padding: '7px 6px',
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 6,
              border: step2Layout === 'seamless_turnaround_1x4' ? '1px solid #34d399' : '1px solid rgba(255,255,255,0.1)',
              background: step2Layout === 'seamless_turnaround_1x4' ? 'linear-gradient(135deg, rgba(16, 185, 129, 0.4), rgba(52, 211, 153, 0.2))' : 'rgba(255,255,255,0.03)',
              color: step2Layout === 'seamless_turnaround_1x4' ? '#34d399' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 2,
              textAlign: 'center',
            }}
          >
            <span>🎞️ Chuỗi 4 Góc Liền</span>
            <span style={{ fontSize: 9, opacity: 0.85 }}>1 Hàng Liền Mạch</span>
          </button>

          <button
            onClick={() => setStep2Layout('cinematic_single_part_2x3')}
            style={{
              padding: '7px 6px',
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 6,
              border: step2Layout === 'cinematic_single_part_2x3' ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.1)',
              background: step2Layout === 'cinematic_single_part_2x3' ? 'linear-gradient(135deg, rgba(168, 85, 247, 0.4), rgba(192, 132, 252, 0.2))' : 'rgba(255,255,255,0.03)',
              color: step2Layout === 'cinematic_single_part_2x3' ? '#c084fc' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 2,
              textAlign: 'center',
            }}
          >
            <span>🔲 Bảng 6 Góc Lưới</span>
            <span style={{ fontSize: 9, opacity: 0.85 }}>2×3 Điện Ảnh</span>
          </button>
        </div>

        {/* Thanh Chọn Góc Quay Cần Tạo (Single Angle) */}
        <div style={{ marginTop: 4, paddingTop: 6, borderTop: '1px dashed rgba(255,255,255,0.1)' }}>
          <div style={{ fontSize: 10, color: '#cbd5e1', marginBottom: 4, fontWeight: 700 }}>
            🎯 Chọn Góc Quay Cần Tạo (Single Angle):
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 4 }}>
            {[
              { id: 'front', label: '0° Chính diện' },
              { id: 'three_quarter', label: '45° Nghiêng 3/4' },
              { id: 'profile_side', label: '90° Nhìn ngang' },
              { id: 'back', label: '180° Sau lưng' },
              { id: 'high_angle', label: 'Trên nhìn xuống' },
              { id: 'low_angle', label: 'Dưới hất lên' },
            ].map((ang) => {
              const isAngSel = step2Angle === ang.id;
              return (
                <button
                  key={ang.id}
                  onClick={() => setStep2Angle(ang.id)}
                  style={{
                    padding: '5px 4px',
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 4,
                    border: isAngSel ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                    background: isAngSel ? '#0284c7' : 'rgba(255,255,255,0.04)',
                    color: isAngSel ? '#fff' : '#cbd5e1',
                    cursor: 'pointer',
                  }}
                >
                  {ang.label}
                </button>
              );
            })}
          </div>
        </div>
      </div>

      {/* Tỉ Lệ Khung Hình & Thể Loại Đối Tượng */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <div>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#facc15', display: 'block', marginBottom: 3 }}>
            📺 Tỉ lệ khung hình (Aspect Ratio):
          </label>
          <select
            value={config.aspect_ratio || 'auto'}
            onChange={(e) => setConfig((p) => ({ ...p, aspect_ratio: e.target.value as any }))}
            style={{ width: '100%', height: 32, padding: '4px 6px', fontSize: 10.5, background: '#040711', color: '#facc15', border: '1px solid rgba(250, 204, 21, 0.4)', borderRadius: 6 }}
          >
            {ASPECT_RATIO_OPTIONS.map((opt) => (
              <option key={opt.id} value={opt.id}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 3 }}>
            📂 Thể Loại Đối Tượng:
          </label>
          <select
            value={targetCategory}
            onChange={(e) => setTargetCategory(e.target.value)}
            style={{ width: '100%', height: 32, padding: '4px 6px', fontSize: 10.5, background: '#090d16', color: '#34d399', border: '1px solid rgba(52, 211, 153, 0.5)', borderRadius: 6 }}
          >
            <option value="character">🧑 Nhân Vật (Khớp Xương 2D) [ĐẦY ĐỦ LOGIC]</option>
            <option value="animal">🐾 Động Vật & Linh Thú (Sắp có)</option>
            <option value="tree">🌳 Cây Cối & Thảo Mộc (Sắp có)</option>
            <option value="rock">🪨 Đá Sỏi & Khoáng Thạch (Sắp có)</option>
            <option value="water">🌊 Sông Nước & Thác Nước (Sắp có)</option>
            <option value="mountain">🏔️ Đồi Núi & Địa Hình (Sắp có)</option>
            <option value="building">🏠 Kiến Trúc & Nhà Cửa (Sắp có)</option>
          </select>
        </div>
      </div>

      {/* Phân Nhóm Giải Phẫu Khớp Xương */}
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
                        setConfig((p) => ({ ...p, part_type: tag.id }));
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
        <label style={{ fontSize: 10.5, fontWeight: 700, color: '#cbd5e1', display: 'block', marginBottom: 4 }}>
          Màu nền tách phông (Chroma Key):
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

      {/* Quy tắc ràng buộc cuối mỗi prompt (Prompt Rules) */}
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
                  'Strictly isolated layer with closed contour lineart, no connected body parts.',
              }))
            }
            style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: 'rgba(34, 197, 94, 0.15)', color: '#4ade80', border: '1px solid rgba(34, 197, 94, 0.3)', cursor: 'pointer' }}
          >
            + Tách lớp biệt lập
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
