import React, { useState } from 'react';
import { BookOpen, X, Copy, Check, Info } from 'lucide-react';
import { JSON_SCHEMA_FIELD_GUIDE } from '../../../core/assets/prompt_builders/Step1MasterPromptBuilder';

interface JsonSchemaGuideModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const FIELD_DOCUMENTATION_ROWS = [
  {
    field: 'base_prompt',
    type: 'string',
    desc: 'Mô tả gốc nhân vật',
    purpose: 'Làm mỏ neo trực quan xác lập ngoại hình, màu sắc, phong cách nghệ thuật đồng bộ cho tất cả các góc quay và linh kiện.',
  },
  {
    field: 'prompts',
    type: 'Array<PromptItem>',
    desc: 'Danh sách các prompt',
    purpose: 'Mảng chứa các tác vụ sinh ảnh chi tiết cho từng góc quay hoặc linh kiện bóc tách.',
  },
  {
    field: 'prompts[].name',
    type: 'string',
    desc: 'Mã định danh tác vụ',
    purpose: 'Mã duy nhất dùng để quản lý pipeline và đặt tên file ảnh khi tự động tải về.',
  },
  {
    field: 'prompts[].part_id',
    type: 'string',
    desc: 'Mã slot linh kiện 2D',
    purpose: 'Xác định slot gắn trên khung xương 2D (ví dụ: `toc_truoc`, `khuon_mat`, `canh_tay_trai`, `dui_phai`).',
  },
  {
    field: 'prompts[].part_name',
    type: 'string',
    desc: 'Tên tiếng Việt của bộ phận',
    purpose: 'Tên hiển thị thân thiện trên giao diện để người dùng dễ nhận biết linh kiện.',
  },
  {
    field: 'prompts[].group_id',
    type: 'string',
    desc: 'Mã nhóm giải phẫu',
    purpose: 'Phân loại nhóm (01_head_face, 02_torso_arms, 03_legs_feet, 04_props_costumes) để gom xuất theo nhóm.',
  },
  {
    field: 'prompts[].angle',
    type: 'string',
    desc: 'Tên góc quay hiển thị',
    purpose: 'Tên góc quay tự nhiên kèm bản dịch tiếng Việt (ví dụ: `0° Front (Chính diện)`, `45° Three-Quarter`).',
  },
  {
    field: 'prompts[].angle_id',
    type: 'string',
    desc: 'Mã góc máy chuẩn',
    purpose: 'Chuẩn hóa định danh góc (000_front, 045_three_quarter, 090_side, 180_back, top_down) cho hệ thống tự động nhận dạng.',
  },
  {
    field: 'prompts[].angle_deg',
    type: 'number (0..360)',
    desc: 'Độ góc quay số học',
    purpose: 'Phục vụ xoay trục không gian 3D, nội suy góc nhìn và chuyển đổi góc máy động trong xưởng 2D.',
  },
  {
    field: 'prompts[].z_index',
    type: 'number (0..100)',
    desc: 'Thứ tự độ sâu lớp vẽ',
    purpose: 'Xác định lớp nào vẽ đè lên lớp nào khi lắp ráp (số lớn hơn vẽ đè lên số nhỏ hơn: Tóc mái Z=50 > Mắt Z=40 > Mặt Z=30 > Tóc sau Z=10).',
  },
  {
    field: 'prompts[].save_filename',
    type: 'string',
    desc: 'Tên file ảnh xuất',
    purpose: 'Tên file PNG chuẩn để công cụ cắt lưới (Grid Slicer) và Assembler tự động nạp vào bộ nhớ.',
  },
  {
    field: 'prompts[].view_desc',
    type: 'string',
    desc: 'Mô tả góc nhìn camera',
    purpose: 'Giải thích hướng quan sát và mục đích của góc quay cho người dùng và AI hiểu bố cục hình ảnh.',
  },
  {
    field: 'prompts[].prompt',
    type: 'string (<4000 ký tự)',
    desc: 'Câu lệnh sinh ảnh AI',
    purpose: 'Câu lệnh chi tiết hoàn chỉnh mô tả bóc tách, góc máy, chi tiết cần vẽ & cấm vẽ, phông nền đơn sắc và tỉ lệ khung hình.',
  },
  {
    field: 'prompts[].count',
    type: 'number (1..50)',
    desc: 'Số lượng ảnh cần sinh',
    purpose: 'Số lượng biến thể ảnh AI cần tạo cho prompt này (đồng bộ theo ô Textbox Count trên giao diện).',
  },
  {
    field: 'prompts[].aspect_ratio',
    type: 'string',
    desc: 'Tỉ lệ khung hình',
    purpose: 'Tỉ lệ khung hình của ảnh kết xuất (1:1, 3:4, 4:3, 16:9, 9:16) để AI sinh ảnh đúng khuôn dạng.',
  },
];

export const JsonSchemaGuideModal: React.FC<JsonSchemaGuideModalProps> = ({ isOpen, onClose }) => {
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const handleCopyGuideJson = () => {
    navigator.clipboard.writeText(JSON.stringify(JSON_SCHEMA_FIELD_GUIDE, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(3, 7, 18, 0.85)',
        backdropFilter: 'blur(12px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
      }}
    >
      <div
        style={{
          width: '95vw',
          maxWidth: '900px',
          maxHeight: '85vh',
          background: '#0b0f19',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 12,
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.8), 0 0 30px rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* Modal Header */}
        <div
          style={{
            padding: '12px 16px',
            background: 'rgba(15, 23, 42, 0.95)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
              }}
            >
              <BookOpen size={16} />
            </div>
            <div>
              <div style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc' }}>
                Tài Liệu Chú Giải Cấu Trúc JSON (JSON Schema Guide)
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                Ý nghĩa và tác dụng của từng trường dữ liệu cho AI sinh ảnh & Lắp ráp 2D Puppet
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <button
              onClick={handleCopyGuideJson}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '5px 10px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 700,
                background: copied ? '#22c55e' : '#0284c7',
                color: '#fff',
                border: 'none',
                cursor: 'pointer',
              }}
            >
              {copied ? <Check size={13} /> : <Copy size={13} />}
              <span>{copied ? 'Đã sao chép!' : 'Sao chép JSON Guide'}</span>
            </button>
            <button
              onClick={onClose}
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                background: 'rgba(255, 255, 255, 0.05)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#94a3b8',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                cursor: 'pointer',
              }}
            >
              <X size={14} />
            </button>
          </div>
        </div>

        {/* Modal Body */}
        <div style={{ flex: 1, padding: 14, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div
            style={{
              padding: '10px 12px',
              background: 'rgba(56, 189, 248, 0.08)',
              borderRadius: 8,
              border: '1px solid rgba(56, 189, 248, 0.25)',
              fontSize: 11,
              color: '#e0f2fe',
              lineHeight: 1.5,
              display: 'flex',
              alignItems: 'flex-start',
              gap: 8,
            }}
          >
            <Info size={16} color="#38bdf8" style={{ flexShrink: 0, marginTop: 2 }} />
            <div>
              <b>Mục đích sử dụng:</b> Khi bạn đưa file JSON này cho các mô hình AI (Gemini, Claude, GPT, ComfyUI, Banana Pro) thực thi, khối <code>_schema_guide</code> và tài liệu dưới đây giúp AI hiểu rõ vai trò của từng trường để sinh ra hình ảnh chính xác tuyệt đối, đúng góc máy và đúng thứ tự phân lớp 2D rigging.
            </div>
          </div>

          {/* Table */}
          <div style={{ overflowX: 'auto', borderRadius: 8, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11, textAlign: 'left' }}>
              <thead>
                <tr style={{ background: 'rgba(15, 23, 42, 0.9)', color: '#38bdf8', borderBottom: '1px solid rgba(255, 255, 255, 0.1)' }}>
                  <th style={{ padding: '8px 10px', width: '22%' }}>Tên trường (Field)</th>
                  <th style={{ padding: '8px 10px', width: '15%' }}>Kiểu dữ liệu</th>
                  <th style={{ padding: '8px 10px', width: '25%' }}>Mô tả ngắn</th>
                  <th style={{ padding: '8px 10px', width: '38%' }}>Tác dụng & Ý nghĩa trong 2D Animation</th>
                </tr>
              </thead>
              <tbody>
                {FIELD_DOCUMENTATION_ROWS.map((row, idx) => (
                  <tr
                    key={row.field}
                    style={{
                      background: idx % 2 === 0 ? 'rgba(255, 255, 255, 0.02)' : 'rgba(255, 255, 255, 0.04)',
                      borderBottom: '1px solid rgba(255, 255, 255, 0.05)',
                    }}
                  >
                    <td style={{ padding: '8px 10px', fontFamily: 'monospace', color: '#facc15', fontWeight: 700 }}>
                      {row.field}
                    </td>
                    <td style={{ padding: '8px 10px', color: '#a855f7', fontFamily: 'monospace', fontSize: 10 }}>
                      {row.type}
                    </td>
                    <td style={{ padding: '8px 10px', color: '#f8fafc', fontWeight: 600 }}>
                      {row.desc}
                    </td>
                    <td style={{ padding: '8px 10px', color: '#cbd5e1', lineHeight: 1.4 }}>
                      {row.purpose}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
};
