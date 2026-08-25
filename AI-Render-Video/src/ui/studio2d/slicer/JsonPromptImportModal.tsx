import React, { useState } from 'react';
import { FileCode, X, Check, AlertCircle } from 'lucide-react';
import { getAngleDefinitionById, STANDARD_ANGLE_DEFINITIONS } from '../../../core/assets/slicer/SlicerAngleConstants';
import { Character2DAngle, Character2DPartType } from '../../../types/scene2d';

interface JsonPromptImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onApplyJsonMetadata: (metadataList: ParsedJsonMetadataItem[]) => void;
}

export interface ParsedJsonMetadataItem {
  name?: string;
  part_id?: Character2DPartType;
  part_name?: string;
  angle_id?: string;
  angle?: Character2DAngle;
  angle_label?: string;
  z_index?: number;
  save_filename?: string;
  prompt?: string;
}

export const JsonPromptImportModal: React.FC<JsonPromptImportModalProps> = ({
  isOpen,
  onClose,
  onApplyJsonMetadata,
}) => {
  const [jsonText, setJsonText] = useState<string>('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [parsedPreview, setParsedPreview] = useState<ParsedJsonMetadataItem[] | null>(null);

  if (!isOpen) return null;

  const handleParse = (text: string) => {
    setJsonText(text);
    if (!text.trim()) {
      setErrorMsg(null);
      setParsedPreview(null);
      return;
    }

    try {
      const parsed = JSON.parse(text);
      const items: any[] = Array.isArray(parsed)
        ? parsed
        : Array.isArray(parsed.prompts)
        ? parsed.prompts
        : [parsed];

      const results: ParsedJsonMetadataItem[] = [];
      for (const item of items) {
        if (!item || typeof item !== 'object') continue;
        const angleDef = getAngleDefinitionById(item.angle_id || item.angle || item.name || item.save_filename || '');
        results.push({
          name: item.name,
          part_id: item.part_id as Character2DPartType,
          part_name: item.part_name,
          angle_id: item.angle_id || angleDef.id,
          angle: angleDef.angle,
          angle_label: item.angle || angleDef.label,
          z_index: typeof item.z_index === 'number' ? item.z_index : undefined,
          save_filename: item.save_filename,
          prompt: item.prompt,
        });
      }

      if (results.length === 0) {
        setErrorMsg('Không tìm thấy danh sách linh kiện/góc quay hợp lệ trong JSON.');
        setParsedPreview(null);
      } else {
        setErrorMsg(null);
        setParsedPreview(results);
      }
    } catch (e: any) {
      setErrorMsg(`Lỗi định dạng JSON: ${e.message}`);
      setParsedPreview(null);
    }
  };

  const handleConfirm = () => {
    if (parsedPreview && parsedPreview.length > 0) {
      onApplyJsonMetadata(parsedPreview);
      onClose();
    }
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
      onClick={onClose}
    >
      <div
        style={{
          width: '95vw',
          maxWidth: '650px',
          background: '#0b0f19',
          border: '1px solid rgba(168, 85, 247, 0.35)',
          borderRadius: 12,
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.85), 0 0 30px rgba(168, 85, 247, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
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
                background: 'linear-gradient(135deg, #7c3aed, #a855f7)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
              }}
            >
              <FileCode size={16} />
            </div>
            <div>
              <div style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc' }}>
                Nạp Cấu Trúc JSON Metadata Từ Tab 4 (Trợ Lý Prompt AI)
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                Dán file JSON chứa các trường: name, part_id, angle_id, angle, z_index...
              </div>
            </div>
          </div>

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

        {/* Body */}
        <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 12, maxHeight: '75vh', overflowY: 'auto' }}>
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#c084fc', marginBottom: 4 }}>
              Dán nội dung JSON vào đây:
            </div>
            <textarea
              rows={8}
              value={jsonText}
              onChange={(e) => handleParse(e.target.value)}
              placeholder={`{\n  "prompts": [\n    {\n      "name": "05_toc_truoc_000_front",\n      "part_id": "toc_truoc",\n      "part_name": "Mái Tóc Trước",\n      "angle_id": "000_front",\n      "angle": "0° Front (Chính diện)"\n    }\n  ]\n}`}
              style={{
                width: '100%',
                background: '#070b14',
                color: '#38bdf8',
                border: '1px solid rgba(168, 85, 247, 0.3)',
                borderRadius: 6,
                padding: '8px 10px',
                fontSize: 10.5,
                fontFamily: 'monospace',
                outline: 'none',
                boxSizing: 'border-box',
                resize: 'vertical',
              }}
            />
          </div>

          {errorMsg && (
            <div
              style={{
                padding: '8px 10px',
                borderRadius: 6,
                background: 'rgba(239, 68, 68, 0.15)',
                border: '1px solid rgba(239, 68, 68, 0.3)',
                color: '#f87171',
                fontSize: 11,
                display: 'flex',
                alignItems: 'center',
                gap: 6,
              }}
            >
              <AlertCircle size={14} /> {errorMsg}
            </div>
          )}

          {parsedPreview && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#4ade80' }}>
                ✓ Đã nhận diện {parsedPreview.length} tác vụ / góc quay:
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 160, overflowY: 'auto' }}>
                {parsedPreview.map((item, idx) => (
                  <div
                    key={idx}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '4px 8px',
                      borderRadius: 4,
                      background: 'rgba(255, 255, 255, 0.03)',
                      border: '1px solid rgba(255, 255, 255, 0.06)',
                      fontSize: 10,
                    }}
                  >
                    <span style={{ color: '#f8fafc', fontWeight: 600 }}>{item.part_name || item.part_id || item.name}</span>
                    <span style={{ color: '#38bdf8', fontFamily: 'monospace' }}>{item.angle_label || item.angle_id}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '10px 16px',
            background: 'rgba(15, 23, 42, 0.95)',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
            gap: 8,
          }}
        >
          <button
            onClick={onClose}
            style={{
              padding: '6px 12px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              color: '#94a3b8',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Hủy Bỏ
          </button>
          <button
            onClick={handleConfirm}
            disabled={!parsedPreview || parsedPreview.length === 0}
            style={{
              padding: '6px 14px',
              borderRadius: 6,
              background: parsedPreview && parsedPreview.length > 0
                ? 'linear-gradient(135deg, #7c3aed, #a855f7)'
                : 'rgba(255, 255, 255, 0.05)',
              color: parsedPreview && parsedPreview.length > 0 ? '#fff' : '#475569',
              border: 'none',
              fontSize: 11,
              fontWeight: 700,
              cursor: parsedPreview && parsedPreview.length > 0 ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Check size={13} /> Áp Dụng Metadata Vào Lưới
          </button>
        </div>
      </div>
    </div>
  );
};
