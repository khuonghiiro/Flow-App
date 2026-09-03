import React, { useState } from 'react';
import { X, Copy, Check, FileCode, RotateCcw } from 'lucide-react';
import { SkillTreeNode } from './types';

interface LayoutExportModalProps {
  isOpen: boolean;
  onClose: () => void;
  nodes: SkillTreeNode[];
  onResetDefault: () => void;
}

export const LayoutExportModal: React.FC<LayoutExportModalProps> = ({
  isOpen,
  onClose,
  nodes,
  onResetDefault,
}) => {
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  // Prepare simplified coordinates array for clean copying
  const coordinatesData = nodes.map((n) => ({
    id: n.id,
    x: Math.round(n.x),
    y: Math.round(n.y),
    label: n.shortLabel,
  }));

  const jsonString = JSON.stringify(coordinatesData, null, 2);

  const handleCopy = () => {
    navigator.clipboard.writeText(jsonString);
    setCopied(true);
    setTimeout(() => setCopied(false), 2500);
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0, 0, 0, 0.75)',
        backdropFilter: 'blur(8px)',
        zIndex: 9999,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 20,
      }}
      onClick={onClose}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: '100%',
          maxWidth: 680,
          background: '#0b1329',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 12,
          boxShadow: '0 20px 50px rgba(0,0,0,0.8), 0 0 30px rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          maxHeight: '85vh',
          overflow: 'hidden',
          animation: 'fadeIn 0.2s ease',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: '14px 18px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(15, 23, 42, 0.6)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <FileCode size={18} color="#38bdf8" />
            <h3 style={{ margin: 0, fontSize: 14, fontWeight: 800, color: '#ffffff' }}>
              Xuất Toạ Độ Bố Cục Cây Kỹ Năng ({nodes.length} Nodes)
            </h3>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'transparent',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              padding: 4,
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <X size={18} />
          </button>
        </div>

        {/* Description Banner */}
        <div
          style={{
            padding: '10px 18px',
            background: 'rgba(56, 189, 248, 0.08)',
            borderBottom: '1px solid rgba(56, 189, 248, 0.15)',
            fontSize: 11.5,
            color: '#bae6fd',
            lineHeight: 1.5,
          }}
        >
          💡 Bạn đã chỉnh sửa vị trí các node theo ý muốn! Hãy bấm nút{' '}
          <strong>"Sao Chép Toạ Độ"</strong> bên dưới rồi paste nội dung này vào ô chat, tôi sẽ nạp toạ độ của bạn vào mã nguồn gốc vĩnh viễn.
        </div>

        {/* Code Content */}
        <div style={{ padding: 16, overflowY: 'auto', flex: 1 }}>
          <pre
            style={{
              margin: 0,
              padding: 12,
              borderRadius: 8,
              background: '#030712',
              border: '1px solid rgba(255, 255, 255, 0.06)',
              color: '#34d399',
              fontSize: 11,
              fontFamily: 'monospace',
              maxHeight: 380,
              overflowY: 'auto',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
            }}
          >
            {jsonString}
          </pre>
        </div>

        {/* Footer Actions */}
        <div
          style={{
            padding: '12px 18px',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(15, 23, 42, 0.6)',
          }}
        >
          <button
            onClick={() => {
              if (window.confirm('Bạn có chắc muốn đặt lại toàn bộ toạ độ về mặc định ban đầu không?')) {
                onResetDefault();
                onClose();
              }
            }}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 12px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: 'rgba(239, 68, 68, 0.15)',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              color: '#f87171',
              cursor: 'pointer',
            }}
          >
            <RotateCcw size={13} /> Khôi Phục Mặc Định
          </button>

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button
              onClick={onClose}
              style={{
                padding: '6px 14px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 6,
                background: 'rgba(255, 255, 255, 0.05)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#cbd5e1',
                cursor: 'pointer',
              }}
            >
              Đóng
            </button>

            <button
              onClick={handleCopy}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '7px 18px',
                fontSize: 11.5,
                fontWeight: 800,
                borderRadius: 6,
                background: copied
                  ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)'
                  : 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
                border: 'none',
                color: '#ffffff',
                cursor: 'pointer',
                boxShadow: copied
                  ? '0 0 15px rgba(16, 185, 129, 0.5)'
                  : '0 0 15px rgba(2, 132, 199, 0.5)',
                transition: 'all 0.2s ease',
              }}
            >
              {copied ? <Check size={14} /> : <Copy size={14} />}
              {copied ? 'Đã Sao Chép Toạ Độ!' : 'Sao Chép Toạ Độ Để Gửi AI'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
