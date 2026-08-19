import React, { useState, useRef, useEffect } from 'react';
import { Bot, Send, Copy, Check, Code, Sparkles, RefreshCw, MessageSquare, Flame, TreePine, Armchair, Wheat, BookOpen } from 'lucide-react';
import { MasterSceneConfig } from '../types/scene';
import { PromptBuilder } from '../ai/prompt_builder';
import { sampleScenes, findSceneById, findSceneByKeyword } from '../core/scenes/SceneRegistry';

interface AIChatDirectorProps {
  scene: MasterSceneConfig;
  onApplyScene: (newScene: MasterSceneConfig) => void;
}

export const AIChatDirector: React.FC<AIChatDirectorProps> = ({ scene, onApplyScene }) => {
  const [activeSubTab, setActiveSubTab] = useState<'chat' | 'json'>('chat');
  const [messages, setMessages] = useState<Array<{ role: 'user' | 'ai'; text: string; scenePreset?: MasterSceneConfig }>>([
    {
      role: 'ai',
      text: 'Xin chào Đạo Diễn! Tôi là **AI Studio Director**. Tôi đã nạp toàn bộ Asset Catalog và các kịch bản mẫu từ thư mục JSON. Hãy cho tôi biết kịch bản bạn muốn chỉ đạo biên kịch!',
    },
  ]);
  const [inputText, setInputText] = useState('');
  const [copiedPrompt, setCopiedPrompt] = useState(false);
  const [copiedJson, setCopiedJson] = useState(false);
  const [jsonInput, setJsonInput] = useState(JSON.stringify(scene, null, 2));
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setJsonInput(JSON.stringify(scene, null, 2));
  }, [scene]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleCopyPrompt = () => {
    const prompt = PromptBuilder.buildDirectorSystemPrompt();
    navigator.clipboard.writeText(prompt);
    setCopiedPrompt(true);
    setTimeout(() => setCopiedPrompt(false), 2000);
  };

  const handleCopyJson = () => {
    navigator.clipboard.writeText(jsonInput);
    setCopiedJson(true);
    setTimeout(() => setCopiedJson(false), 2000);
  };

  const handleSendMessage = (textToSend?: string) => {
    const query = textToSend || inputText;
    if (!query.trim()) return;

    const userMsg = { role: 'user' as const, text: query };
    setMessages((prev) => [...prev, userMsg]);
    if (!textToSend) setInputText('');

    // AI Director reasoning and scene dispatch
    setTimeout(() => {
      const lower = query.toLowerCase();
      let matchedScene: MasterSceneConfig | undefined;
      let replyText = `Tôi đã biên đạo kịch bản theo yêu cầu của bạn: "${query}".`;

      if (lower.includes('ngồi ghế') || lower.includes('quán nước') || lower.includes('ghế')) {
        matchedScene = findSceneById('scene_chair_sitting');
        replyText = '🪑 **Đã biên đạo Kịch bản Ngồi Ghế Đàm Đạo:** Nhân vật tiếp cận ghế gỗ, tự động xoay 180° hướng ra ngoài, khớp tư thế ngồi và bắt đầu câu thoại!';
      } else if (lower.includes('trèo cây') || lower.includes('cây') || lower.includes('ngắm')) {
        matchedScene = findSceneById('scene_tree_climbing');
        replyText = '🌳 **Đã biên đạo Kịch bản Trèo Cây Trinh Sát:** Nhân vật leo dọc thân cây làng quê theo waypoints lên nhánh chạc ba, ngồi ngắm cảnh hoàng hôn!';
      } else if (lower.includes('sách') || lower.includes('thần thoại') || lower.includes('fantasy') || lower.includes('book')) {
        matchedScene = findSceneById('scene_medieval_fantasy_book');
        replyText = '📖 **Đã biên đạo Kịch bản Sách Thần Thoại:** Khởi tạo map Cuốn Sách Huyền Ảo (medieval_fantasy_book.glb) cùng lâu đài và dòng suối ma thuật!';
      } else if (lower.includes('thánh đường') || lower.includes('cathedral')) {
        matchedScene = findSceneById('scene_cathedral_mystery');
        replyText = '⛪ **Đã biên đạo Kịch bản Thánh Đường:** Khởi tạo không gian kiến trúc Gothic trang nghiêm và màn đấu trí phép thuật!';
      } else if (lower.includes('hải tặc') || lower.includes('cướp biển') || lower.includes('pirate')) {
        matchedScene = findSceneById('scene_pirate_adventure');
        replyText = '🏴‍☠️ **Đã biên đạo Kịch bản Đảo Hải Tặc:** Khởi tạo chiến hạm cướp biển và hòn đảo nhiệt đới!';
      } else if (lower.includes('combat') || lower.includes('đánh') || lower.includes('chém') || lower.includes('chiến')) {
        matchedScene = findSceneById('scene_village_clash_01');
        replyText = '⚔️ **Đã biên đạo Kịch bản Đại Chiến Võ Thuật:** Khớp frame vung kiếm t=8.5s kèm vệt lửa, va chạm t=9.1s đối thủ văng lùi 3.2m, mặt nhăn đau đớn, nổ tia lửa và rung camera!';
      } else {
        matchedScene = findSceneByKeyword(query);
        if (matchedScene) {
          replyText = `🎬 **Đã chuyển sang kịch bản:** ${matchedScene.title || matchedScene.scene_id}`;
        } else {
          replyText += '\nBạn có thể chuyển sang tab **Master JSON** để tinh chỉnh từng mili-giây, hoặc tải các kịch bản mẫu từ menu trên cùng!';
        }
      }

      if (matchedScene) {
        onApplyScene(matchedScene);
      }

      setMessages((prev) => [
        ...prev,
        {
          role: 'ai',
          text: replyText,
          scenePreset: matchedScene,
        },
      ]);
    }, 500);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  const handleApplyJson = () => {
    try {
      const parsed = JSON.parse(jsonInput);
      onApplyScene(parsed);
      alert('✓ Đã nạp và cập nhật Master Scene JSON vào Studio thành công!');
    } catch (e) {
      alert(`Lỗi cú pháp JSON: ${e}`);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', gap: 10 }}>
      {/* Sub-Tabs: Chat vs Full JSON */}
      <div style={{ display: 'flex', background: 'rgba(10, 12, 22, 0.6)', borderRadius: 8, padding: 3, border: '1px solid var(--border-subtle)' }}>
        <button
          className={`sidebar-tab-btn ${activeSubTab === 'chat' ? 'active' : ''}`}
          style={{ padding: '6px 10px', fontSize: 11, borderRadius: 6 }}
          onClick={() => setActiveSubTab('chat')}
        >
          <MessageSquare size={13} /> Trò Chuyện AI
        </button>
        <button
          className={`sidebar-tab-btn ${activeSubTab === 'json' ? 'active' : ''}`}
          style={{ padding: '6px 10px', fontSize: 11, borderRadius: 6 }}
          onClick={() => setActiveSubTab('json')}
        >
          <Code size={13} /> Master JSON
        </button>
      </div>

      {activeSubTab === 'chat' ? (
        <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, gap: 10 }}>
          {/* Quick Prompts Chips */}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            <button
              className="btn-secondary"
              style={{ fontSize: 10, padding: '3px 8px', borderRadius: 12 }}
              onClick={() => handleSendMessage('Dàn dựng màn Combat vung kiếm lửa và va đập')}
            >
              <Flame size={11} color="#f43f5e" /> Combat Chí Mạng
            </button>
            <button
              className="btn-secondary"
              style={{ fontSize: 10, padding: '3px 8px', borderRadius: 12 }}
              onClick={() => handleSendMessage('Diễn cảnh nhân vật chạy tới ngồi ghế gỗ nghỉ ngơi')}
            >
              <Armchair size={11} color="#eab308" /> Ngồi Ghế Gỗ
            </button>
            <button
              className="btn-secondary"
              style={{ fontSize: 10, padding: '3px 8px', borderRadius: 12 }}
              onClick={() => handleSendMessage('Cho nhân vật trèo lên cành cây làng ngắm cảnh')}
            >
              <TreePine size={11} color="#10b981" /> Trèo Cây Trinh Sát
            </button>
          </div>

          {/* ChatGPT / Gemini Message List */}
          <div
            style={{
              flex: 1,
              overflowY: 'auto',
              display: 'flex',
              flexDirection: 'column',
              gap: 12,
              paddingRight: 4,
              minHeight: 140,
            }}
          >
            {messages.map((m, idx) => (
              <div
                key={idx}
                style={{
                  display: 'flex',
                  gap: 8,
                  alignSelf: m.role === 'user' ? 'flex-end' : 'flex-start',
                  maxWidth: '92%',
                }}
              >
                {m.role === 'ai' && (
                  <div
                    style={{
                      width: 26,
                      height: 26,
                      borderRadius: '50%',
                      background: 'linear-gradient(135deg, #6366f1, #06b6d4)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      flexShrink: 0,
                      boxShadow: '0 0 10px rgba(99, 102, 241, 0.4)',
                    }}
                  >
                    <Sparkles size={13} color="#ffffff" />
                  </div>
                )}

                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: m.role === 'user' ? '14px 14px 2px 14px' : '14px 14px 14px 2px',
                    fontSize: 12,
                    lineHeight: 1.5,
                    background:
                      m.role === 'user'
                        ? 'linear-gradient(135deg, rgba(99, 102, 241, 0.35), rgba(79, 70, 229, 0.45))'
                        : 'rgba(20, 24, 40, 0.85)',
                    border: '1px solid ' + (m.role === 'user' ? 'rgba(99, 102, 241, 0.4)' : 'rgba(255, 255, 255, 0.08)'),
                    color: m.role === 'user' ? '#ffffff' : '#e2e8f0',
                    boxShadow: '0 4px 14px rgba(0, 0, 0, 0.3)',
                    whiteSpace: 'pre-wrap',
                  }}
                >
                  {m.text}
                </div>
              </div>
            ))}
            <div ref={messagesEndRef} />
          </div>

          {/* ChatGPT / Gemini Style Embedded Input Box (120px height) */}
          <div
            style={{
              position: 'relative',
              background: 'rgba(12, 15, 26, 0.95)',
              border: '1px solid var(--border-glow)',
              borderRadius: 12,
              padding: '8px 10px 38px 12px',
              boxShadow: '0 4px 20px rgba(0, 0, 0, 0.5), 0 0 15px rgba(99, 102, 241, 0.15)',
            }}
          >
            <textarea
              className="chat-embedded-textarea"
              style={{
                width: '100%',
                height: '75px',
                background: 'transparent',
                border: 'none',
                outline: 'none',
                color: '#ffffff',
                fontFamily: 'inherit',
                fontSize: 12,
                lineHeight: 1.45,
                resize: 'none',
              }}
              placeholder="Nhập yêu cầu kịch bản cho AI Đạo Diễn... (Shift+Enter để xuống dòng, Enter để gửi)"
              value={inputText}
              onChange={(e) => setInputText(e.target.value)}
              onKeyDown={handleKeyDown}
            />

            {/* Bottom-right inside toolbar */}
            <div
              style={{
                position: 'absolute',
                bottom: 8,
                left: 10,
                right: 8,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <button
                className="btn-secondary"
                style={{ padding: '2px 8px', fontSize: 10, borderRadius: 12 }}
                onClick={handleCopyPrompt}
              >
                {copiedPrompt ? <Check size={11} color="#10b981" /> : <Copy size={11} />}
                {copiedPrompt ? 'Đã chép Prompt' : 'System Prompt'}
              </button>

              <button
                className="btn-primary"
                style={{
                  width: 30,
                  height: 30,
                  padding: 0,
                  borderRadius: '50%',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
                onClick={() => handleSendMessage()}
                title="Gửi cho AI Đạo Diễn"
              >
                <Send size={13} />
              </button>
            </div>
          </div>
        </div>
      ) : (
        /* Full Height Master JSON Editor */
        <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, gap: 8 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 11, color: '#94a3b8' }}>
              Master Scene Schema (JSON)
            </span>
            <div style={{ display: 'flex', gap: 6 }}>
              <button className="btn-secondary" style={{ padding: '2px 6px', fontSize: 10 }} onClick={handleCopyJson}>
                {copiedJson ? <Check size={11} color="#10b981" /> : <Copy size={11} />}
                {copiedJson ? 'Đã chép' : 'Copy'}
              </button>
              <button className="btn-primary" style={{ padding: '4px 10px', fontSize: 11 }} onClick={handleApplyJson}>
                <RefreshCw size={11} /> Cập Nhật
              </button>
            </div>
          </div>

          <textarea
            className="form-textarea"
            style={{
              flex: 1,
              width: '100%',
              minHeight: 0,
              height: '100%',
              fontSize: 11,
              lineHeight: 1.4,
              fontFamily: 'var(--font-mono)',
              background: 'rgba(8, 10, 18, 0.95)',
            }}
            value={jsonInput}
            onChange={(e) => setJsonInput(e.target.value)}
          />
        </div>
      )}
    </div>
  );
};
