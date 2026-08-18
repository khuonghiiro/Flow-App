import React, { useState } from 'react';
import { Bot, Send, Copy, Check, Code, Sparkles, RefreshCw } from 'lucide-react';
import { MasterSceneConfig } from '../types/scene';
import { PromptBuilder } from '../ai/prompt_builder';

interface AIChatDirectorProps {
  scene: MasterSceneConfig;
  onApplyScene: (newScene: MasterSceneConfig) => void;
}

export const AIChatDirector: React.FC<AIChatDirectorProps> = ({ scene, onApplyScene }) => {
  const [messages, setMessages] = useState<Array<{ role: 'user' | 'ai'; text: string; json?: MasterSceneConfig }>>([
    {
      role: 'ai',
      text: 'Xin chào Đạo Diễn! Tôi là AI Giám Sát Biên Đạo 3D. Tôi đã nạp toàn bộ kho tài nguyên (nhân vật, vũ khí, hiệu ứng) và tọa độ bản đồ làng quê. Hãy cho tôi biết kịch bản bạn muốn dàn dựng!',
    },
  ]);
  const [inputText, setInputText] = useState('');
  const [copiedPrompt, setCopiedPrompt] = useState(false);
  const [jsonInput, setJsonInput] = useState(JSON.stringify(scene, null, 2));

  const handleCopyPrompt = () => {
    const prompt = PromptBuilder.buildDirectorSystemPrompt();
    navigator.clipboard.writeText(prompt);
    setCopiedPrompt(true);
    setTimeout(() => setCopiedPrompt(false), 2000);
  };

  const handleSendMessage = () => {
    if (!inputText.trim()) return;

    const newMsg = { role: 'user' as const, text: inputText };
    setMessages((prev) => [...prev, newMsg]);
    setInputText('');

    // Simulate AI Director Assistant response
    setTimeout(() => {
      setMessages((prev) => [
        ...prev,
        {
          role: 'ai',
          text: `Tôi đã ghi nhận kịch bản: "${inputText}". Bạn có thể chỉnh sửa trực tiếp JSON bên dưới hoặc copy System Prompt để đưa vào các model LLM lớn nhất!`,
        },
      ]);
    }, 600);
  };

  const handleApplyJson = () => {
    try {
      const parsed = JSON.parse(jsonInput);
      onApplyScene(parsed);
      alert('Đã nạp và cập nhật Master Scene JSON vào Studio thành công!');
    } catch (e) {
      alert(`Lỗi cú pháp JSON: ${e}`);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Bot size={16} color="#818cf8" /> AI Đạo Diễn (Director Chat)
        </span>
        <button className="btn-secondary" style={{ padding: '3px 8px', fontSize: 11 }} onClick={handleCopyPrompt}>
          {copiedPrompt ? <Check size={12} color="#10b981" /> : <Copy size={12} />}
          {copiedPrompt ? 'Đã chép Prompt' : 'Copy System Prompt'}
        </button>
      </div>

      {/* Messages Scroll Area */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 10,
          maxHeight: 180,
          overflowY: 'auto',
          paddingRight: 4,
        }}
      >
        {messages.map((m, idx) => (
          <div
            key={idx}
            style={{
              alignSelf: m.role === 'user' ? 'flex-end' : 'flex-start',
              maxWidth: '90%',
              padding: '8px 12px',
              borderRadius: 8,
              fontSize: 12,
              lineHeight: 1.45,
              background: m.role === 'user' ? 'rgba(99, 102, 241, 0.25)' : 'rgba(255, 255, 255, 0.05)',
              border: '1px solid var(--border-subtle)',
              color: m.role === 'user' ? '#ffffff' : '#cbd5e1',
            }}
          >
            {m.text}
          </div>
        ))}
      </div>

      {/* Input row */}
      <div style={{ display: 'flex', gap: 6 }}>
        <input
          className="form-input"
          style={{ padding: '6px 10px', fontSize: 12 }}
          placeholder="Nhập yêu cầu kịch bản cho AI Đạo Diễn..."
          value={inputText}
          onChange={(e) => setInputText(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSendMessage()}
        />
        <button className="btn-icon" onClick={handleSendMessage}>
          <Send size={14} color="#818cf8" />
        </button>
      </div>

      {/* Master Scene JSON Editor */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 12, fontWeight: 600, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Code size={13} /> Master Scene JSON Editor
          </span>
          <button className="btn-secondary" style={{ padding: '2px 6px', fontSize: 10 }} onClick={handleApplyJson}>
            <RefreshCw size={10} /> Cập Nhật Kịch Bản
          </button>
        </div>
        <textarea
          className="form-textarea"
          style={{ height: 130, fontSize: 11 }}
          value={jsonInput}
          onChange={(e) => setJsonInput(e.target.value)}
        />
      </div>
    </div>
  );
};
