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
  id: '01_mannequin_limbs' | '02_face_features' | '03_costume_clothes';
  groupTitle: string;
  folderName: string;
  items: CharacterTagItem[];
}

export const CHARACTER_PART_GROUPS: CharacterPartGroup[] = [
  {
    id: '01_mannequin_limbs',
    groupTitle: '🧍 1. Khung Xương Mannequin Cơ Thể (Z-Index: 11 - 14)',
    folderName: '01_mannequin_limbs',
    items: [
      { id: 'than_mannequin', label: 'Thân Mannequin [Z:11]', icon: '🥋', desc: 'Thân ngực eo hông có chỏm lồi vòng cung ở cổ, vai, háng' },
      { id: 'bap_tay', label: 'Bắp Tay [Z:12]', icon: '🦾', desc: 'Bắp tay có chỏm lồi vòng cung 2 đầu (vai ➔ khuỷu)' },
      { id: 'cang_tay', label: 'Cẳng Tay [Z:13]', icon: '🦾', desc: 'Cẳng tay có chỏm lồi vòng cung 2 đầu (khuỷu ➔ cổ tay)' },
      { id: 'ban_tay', label: 'Bàn Tay [Z:14]', icon: '🖐️', desc: 'Bàn tay có chỏm lồi cổ tay + 5 ngón xòe mở' },
      { id: 'dui', label: 'Đùi Mannequin [Z:12]', icon: '🦵', desc: 'Khớp đùi có chỏm lồi vòng cung 2 đầu (hông ➔ gối)' },
      { id: 'cang_chan', label: 'Cẳng Chân [Z:13]', icon: '🦵', desc: 'Cẳng chân có chỏm lồi vòng cung 2 đầu (gối ➔ mắt cá)' },
      { id: 'ban_chan', label: 'Bàn Chân [Z:14]', icon: '🦶', desc: 'Bàn chân có chỏm lồi mắt cá + ngón flat' },
    ],
  },
  {
    id: '02_face_features',
    groupTitle: '👤 2. Khuôn Mặt & Ngũ Quan Bóc Tách (Z-Index: 15 - 50)',
    folderName: '02_face_features',
    items: [
      { id: 'khuon_mat_no_face', label: 'Mặt Trần & Cổ [Z:11]', icon: '👤', desc: 'Đầu trần mannequin + cổ xuống xương quai xanh' },
      { id: 'doi_tai', label: 'Vành Tai [Z:15]', icon: '👂', desc: '1 tai đối xứng (0°/90°) & góc nghiêng (45°)' },
      { id: 'trong_trang', label: 'Lòng Trắng Mắt [Z:21]', icon: '⚪', desc: 'Lòng trắng có dải bóng đổ hốc mắt trên' },
      { id: 'trong_den_iris', label: 'Mống Mắt (Iris) [Z:22]', icon: '🔮', desc: 'Đĩa con ngươi màu độc lập dễ thay đổi texture' },
      { id: 'diem_sang_mat', label: 'Điểm Sáng [Z:23]', icon: '✨', desc: 'Chấm sáng lấp lánh phản chiếu mắt' },
      { id: 'mi_mat', label: 'Mi Mắt (Pure Lashes) [Z:24]', icon: '👁️', desc: 'Viền mi mắt đen sắc sảo, lòng rỗng xuyên thấu' },
      { id: 'long_may', label: 'Lông Mày [Z:25]', icon: '✏️', desc: '1 lông mày (0°/90°) & cặp phối cảnh (45°)' },
      { id: 'mui', label: 'Sống Mũi [Z:25]', icon: '👃', desc: 'Sống mũi thanh tú & góc nghiêng' },
      { id: 'mieng', label: 'Khẩu Hình Miệng [Z:25]', icon: '👄', desc: 'Khẩu hình nói chuyện & cười' },
      { id: 'mat', label: 'Mắt Tổng Hợp [Z:20]', icon: '👀', desc: '1 mắt đối xứng (0°/90°) & cặp phối cảnh (45°)' },
      { id: 'toc_truoc', label: 'Mái Tóc Trước [Z:50]', icon: '💇', desc: 'Mái trước độc lập bám theo vòm trán (180° để rỗng)' },
      { id: 'toc_sau', label: 'Suối Tóc Sau [Z:1]', icon: '🌊', desc: 'Tóc nền sau lưng trơn 180°' },
    ],
  },
  {
    id: '03_costume_clothes',
    groupTitle: '👘 3. Trang Phục & Y Phục Bóc Tách (Z-Index: 51 - 60)',
    folderName: '03_costume_clothes',
    items: [
      { id: 'than_co_ban', label: 'Thân Áo & Quần [Z:51]', icon: '🥋', desc: 'Áo giáp / y phục thân khoét cổ & nách khớp mannequin' },
      { id: 'dai_lung', label: 'Đai Lưng Thắt Eo [Z:52]', icon: '🎗️', desc: 'Đai thắt eo ngọc bội nằm giữa ranh giới áo và quần' },
      { id: 'ong_ao_bap_tay', label: 'Ống Áo Bắp Tay [Z:53]', icon: '👘', desc: 'Ống bọc bắp tay khoét rỗng 2 đầu' },
      { id: 'ong_tay_xoe', label: 'Ống Tay Xòe & Hộ Uyển [Z:54]', icon: '👘', desc: 'Ống tay áo tiên hiệp xòe rộng & bao cổ tay' },
      { id: 'ung_giay', label: 'Ủng Giày Tiên Hiệp [Z:54]', icon: '🥾', desc: 'Đôi ủng cao cổ khoét miệng khớp cẳng chân' },
      { id: 'vat_ao_duoi', label: 'Vạt Áo / Tà Áo Dưới Eo [Z:55]', icon: '👗', desc: 'Tà áo buông rủ từ thắt lưng xuống gối' },
      { id: 'ao_choang', label: 'Áo Choàng / Tà Áo Bay [Z:2]', icon: '🥻', desc: 'Áo choàng sau lưng buông bay' },
      { id: 'vu_khi', label: 'Vũ Khí & Pháp Bảo [Z:60]', icon: '🗡️', desc: 'Phi kiếm, quạt, pháp bảo, linh phù' },
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
  activeStepFilter?: '01_mannequin_limbs' | '02_face_features' | '03_costume_clothes' | 'all';
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
  activeStepFilter = 'all',
}) => {
  const getBannerInfo = () => {
    if (activeStepFilter === '01_mannequin_limbs') {
      return {
        bg: 'rgba(16, 185, 129, 0.12)',
        border: '1px solid rgba(16, 185, 129, 0.35)',
        color: '#d1fae5',
        text: '🦾 BƯỚC 2: Bóc tách khung xương Mannequin (01_mannequin_limbs) với chỏm lồi hình vòng cung (convex dome overlap cap) để bù phần khuyết khi xoay khớp!',
      };
    }
    if (activeStepFilter === '02_face_features') {
      return {
        bg: 'rgba(56, 189, 248, 0.12)',
        border: '1px solid rgba(56, 189, 248, 0.35)',
        color: '#e0f2fe',
        text: '👤 BƯỚC 3: Bóc tách ngũ quan & khuôn mặt (02_face_features) tách riêng từng layer (mắt, mày, mũi, miệng, tai, tóc) để tạo animation biểu cảm động!',
      };
    }
    if (activeStepFilter === '03_costume_clothes') {
      return {
        bg: 'rgba(236, 72, 153, 0.12)',
        border: '1px solid rgba(236, 72, 153, 0.35)',
        color: '#fce7f3',
        text: '👘 BƯỚC 4: Bóc tách trang phục & y phục (03_costume_clothes) gồm áo vest, đai lưng, ống tay, ủng giày, áo choàng, vũ khí để mặc lên khung mannequin!',
      };
    }
    return {
      bg: 'rgba(16, 185, 129, 0.12)',
      border: '1px solid rgba(16, 185, 129, 0.35)',
      color: '#d1fae5',
      text: '✂️ Giải phẫu bóc tách từng linh kiện & khớp xương phục vụ gắn xương IK/FK và chuyển động hoạt ảnh 2D mượt mà!',
    };
  };

  const banner = getBannerInfo();
  const displayedGroups = activeStepFilter === 'all'
    ? CHARACTER_PART_GROUPS
    : CHARACTER_PART_GROUPS.filter((g) => g.id === activeStepFilter);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div
        style={{
          background: banner.bg,
          padding: '8px 12px',
          borderRadius: 8,
          border: banner.border,
          fontSize: 11,
          color: banner.color,
          lineHeight: 1.4,
        }}
      >
        {banner.text}
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
              { id: 'front', label: '0° Chính diện (Đối xứng)' },
              { id: 'three_quarter', label: '45° Nghiêng 3/4 Trái' },
              { id: 'profile_side', label: '90° Nhìn ngang Trái' },
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
          {displayedGroups.map((group, gIdx) => (
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

      {/* Cấu Hình Chi Tiết Riêng Cho Mái Tóc Trước */}
      {selectedTag === 'toc_truoc' && (
        <div style={{ background: 'rgba(236, 72, 153, 0.08)', padding: 10, borderRadius: 8, border: '1px solid rgba(236, 72, 153, 0.3)', display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label style={{ fontSize: 11, fontWeight: 800, color: '#f472b6', display: 'flex', alignItems: 'center', gap: 5 }}>
            💇 Kiểu Dáng Mái Tóc Trước (Front Bangs Style):
          </label>
          <select
            value={config.bangs_style || 'see_through_airy'}
            onChange={(e) => setConfig((p) => ({ ...p, bangs_style: e.target.value }))}
            style={{ width: '100%', height: 32, padding: '4px 6px', fontSize: 11, background: '#090d16', color: '#f472b6', border: '1px solid rgba(244, 114, 182, 0.5)', borderRadius: 6 }}
          >
            <option value="see_through_airy">Mái thưa Hàn Quốc / Anime tỉa lớp (See-Through Airy Bangs)</option>
            <option value="blunt_straight">Mái bằng ngang trán (Blunt Straight Bangs)</option>
            <option value="side_swept_7_3">Mái xéo rẽ ngôi 7/3 (Side-Swept Bangs 7/3)</option>
            <option value="curtain_parted_5_5">Mái rẽ đôi ngôi giữa 5/5 (Curtain Center-Parted)</option>
            <option value="hime_cut_tendrils">Mái Hime lọn dài ôm má (Hime Cut & Side Tendrils)</option>
            <option value="spiky_action">Mái nhọn so le lộn xộn năng động (Spiky Action Bangs)</option>
          </select>
          <div style={{ fontSize: 9.5, color: '#fbcfe8', opacity: 0.9 }}>
            💡 Gợi ý: Chọn kiểu mái khớp với ảnh tham chiếu của bạn để AI trích xuất hình dáng chính xác 100%!
          </div>
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
