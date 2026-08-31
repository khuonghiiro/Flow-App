import React, { useState, useMemo } from 'react';
import { Search, X, User, Footprints, Flame, Sparkles } from 'lucide-react';
import { PromptItem, PromptStepCategory } from './types';

interface PromptSidebarProps {
  items: PromptItem[];
  selectedId: string;
  onSelect: (id: string) => void;
}

export const PromptSidebar: React.FC<PromptSidebarProps> = ({
  items,
  selectedId,
  onSelect,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [activeCategoryFilter, setActiveCategoryFilter] = useState<PromptStepCategory | 'all'>('all');
  const [actionSubFilter, setActionSubFilter] = useState<string>('all');

  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      const matchesCategory =
        activeCategoryFilter === 'all' || item.stepCategory === activeCategoryFilter;

      // Check action sub-filter if on Step 3
      let matchesSubFilter = true;
      if (activeCategoryFilter === 'step3_actions' && actionSubFilter !== 'all') {
        if (actionSubFilter === 'run') matchesSubFilter = item.id.startsWith('run_');
        else if (actionSubFilter === 'sit') matchesSubFilter = item.id.startsWith('sit_');
        else if (actionSubFilter === 'lie') matchesSubFilter = item.id.startsWith('lie_');
        else if (actionSubFilter === 'jump') matchesSubFilter = item.id.startsWith('jump_');
        else if (actionSubFilter === 'idle') matchesSubFilter = item.id.startsWith('idle_');
        else if (actionSubFilter === 'attack') matchesSubFilter = item.id.startsWith('attack_');
        else if (actionSubFilter === 'head') matchesSubFilter = ['head_shake', 'head_nod', 'look_aside'].includes(item.id);
      }

      const matchesSearch =
        searchTerm.trim() === '' ||
        item.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.subtitle.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.tags.some((t) => t.toLowerCase().includes(searchTerm.toLowerCase()));

      return matchesCategory && matchesSubFilter && matchesSearch;
    });
  }, [items, activeCategoryFilter, actionSubFilter, searchTerm]);

  // Group items by stepCategory for structured sidebar view
  const categoryGroups: { id: PromptStepCategory; label: string; icon: React.ReactNode; color: string }[] = [
    { id: 'step1_character', label: 'BƯỚC 1: NHÂN VẬT & GÓC NHÌN', icon: <User size={13} />, color: '#38bdf8' },
    { id: 'step2_walk', label: 'BƯỚC 2: ĐI BỘ THEO CÁC GÓC', icon: <Footprints size={13} />, color: '#34d399' },
    { id: 'step3_actions', label: 'BƯỚC 3: ĐỘNG TÁC THEO CÁC GÓC', icon: <Flame size={13} />, color: '#f59e0b' },
    { id: 'step4_weapons', label: 'BƯỚC 4: VŨ KHÍ & PHÉP THUẬT', icon: <Sparkles size={13} />, color: '#c084fc' },
  ];

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#0d1322',
        borderRadius: 10,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        padding: '12px 10px',
        overflow: 'hidden',
      }}
    >
      {/* ─── Search Bar ─── */}
      <div style={{ position: 'relative', marginBottom: 10 }}>
        <Search
          size={14}
          style={{
            position: 'absolute',
            left: 10,
            top: '50%',
            transform: 'translateY(-50%)',
            color: '#64748b',
          }}
        />
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          placeholder="Tìm prompt theo tên, góc (0°, 45°, 90°, 180°), động tác..."
          style={{
            width: '100%',
            height: 34,
            padding: '4px 28px 4px 30px',
            fontSize: 11.5,
            background: 'rgba(15, 23, 42, 0.8)',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            borderRadius: 6,
            color: '#f8fafc',
            outline: 'none',
          }}
        />
        {searchTerm && (
          <button
            onClick={() => setSearchTerm('')}
            style={{
              position: 'absolute',
              right: 8,
              top: '50%',
              transform: 'translateY(-50%)',
              background: 'transparent',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <X size={12} />
          </button>
        )}
      </div>

      {/* ─── Step Filter Badges ─── */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(5, 1fr)',
          gap: 4,
          marginBottom: activeCategoryFilter === 'step3_actions' ? 6 : 10,
        }}
      >
        <button
          onClick={() => {
            setActiveCategoryFilter('all');
            setActionSubFilter('all');
          }}
          style={{
            padding: '5px 2px',
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            border: activeCategoryFilter === 'all' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
            background: activeCategoryFilter === 'all' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
            color: activeCategoryFilter === 'all' ? '#38bdf8' : '#94a3b8',
            cursor: 'pointer',
            textAlign: 'center',
          }}
        >
          Tất Cả ({items.length})
        </button>
        <button
          onClick={() => {
            setActiveCategoryFilter('step1_character');
            setActionSubFilter('all');
          }}
          style={{
            padding: '5px 2px',
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            border: activeCategoryFilter === 'step1_character' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
            background: activeCategoryFilter === 'step1_character' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
            color: activeCategoryFilter === 'step1_character' ? '#38bdf8' : '#94a3b8',
            cursor: 'pointer',
            textAlign: 'center',
          }}
        >
          👤 Bước 1
        </button>
        <button
          onClick={() => {
            setActiveCategoryFilter('step2_walk');
            setActionSubFilter('all');
          }}
          style={{
            padding: '5px 2px',
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            border: activeCategoryFilter === 'step2_walk' ? '1px solid #34d399' : '1px solid rgba(255,255,255,0.08)',
            background: activeCategoryFilter === 'step2_walk' ? 'rgba(52, 211, 153, 0.2)' : 'rgba(255,255,255,0.03)',
            color: activeCategoryFilter === 'step2_walk' ? '#34d399' : '#94a3b8',
            cursor: 'pointer',
            textAlign: 'center',
          }}
        >
          🚶 Bước 2
        </button>
        <button
          onClick={() => {
            setActiveCategoryFilter('step3_actions');
            setActionSubFilter('all');
          }}
          style={{
            padding: '5px 2px',
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            border: activeCategoryFilter === 'step3_actions' ? '1px solid #f59e0b' : '1px solid rgba(255,255,255,0.08)',
            background: activeCategoryFilter === 'step3_actions' ? 'rgba(245, 158, 11, 0.2)' : 'rgba(255,255,255,0.03)',
            color: activeCategoryFilter === 'step3_actions' ? '#f59e0b' : '#94a3b8',
            cursor: 'pointer',
            textAlign: 'center',
          }}
        >
          ⚡ Bước 3
        </button>
        <button
          onClick={() => {
            setActiveCategoryFilter('step4_weapons');
            setActionSubFilter('all');
          }}
          style={{
            padding: '5px 2px',
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            border: activeCategoryFilter === 'step4_weapons' ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.08)',
            background: activeCategoryFilter === 'step4_weapons' ? 'rgba(192, 132, 252, 0.2)' : 'rgba(255,255,255,0.03)',
            color: activeCategoryFilter === 'step4_weapons' ? '#c084fc' : '#94a3b8',
            cursor: 'pointer',
            textAlign: 'center',
          }}
        >
          ⚔️ Bước 4
        </button>
      </div>

      {/* ─── Step 3 Action Subcategory Chips ─── */}
      {activeCategoryFilter === 'step3_actions' && (
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 3,
            marginBottom: 10,
            background: 'rgba(0,0,0,0.3)',
            padding: '4px 6px',
            borderRadius: 6,
          }}
        >
          {[
            { id: 'all', label: 'Tất Cả' },
            { id: 'run', label: '🏃 Chạy' },
            { id: 'sit', label: '🪑 Ngồi' },
            { id: 'lie', label: '🛌 Nằm' },
            { id: 'jump', label: '⬆️ Nhảy' },
            { id: 'idle', label: '🧍 Đứng' },
            { id: 'attack', label: '⚔️ Đánh' },
            { id: 'head', label: '🤨 Đầu' },
          ].map((chip) => (
            <button
              key={chip.id}
              onClick={() => setActionSubFilter(chip.id)}
              style={{
                padding: '3px 6px',
                fontSize: 9,
                fontWeight: 700,
                borderRadius: 4,
                border: actionSubFilter === chip.id ? '1px solid #f59e0b' : '1px solid transparent',
                background: actionSubFilter === chip.id ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255,255,255,0.04)',
                color: actionSubFilter === chip.id ? '#fde047' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              {chip.label}
            </button>
          ))}
        </div>
      )}

      {/* ─── Scrollable Menu Sections ─── */}
      <div
        style={{
          flex: 1,
          overflowY: 'auto',
          display: 'flex',
          flexDirection: 'column',
          gap: 12,
          paddingRight: 4,
        }}
      >
        {filteredItems.length === 0 ? (
          <div style={{ textAlign: 'center', color: '#64748b', fontSize: 12, padding: '30px 10px' }}>
            Không tìm thấy prompt phù hợp với từ khóa "{searchTerm}"
          </div>
        ) : (
          categoryGroups.map((group) => {
            const groupItems = filteredItems.filter((it) => it.stepCategory === group.id);
            if (groupItems.length === 0) return null;

            return (
              <div key={group.id} style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <div
                  style={{
                    fontSize: 10,
                    fontWeight: 800,
                    color: group.color,
                    padding: '4px 6px',
                    letterSpacing: '0.4px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 6,
                    borderBottom: `1px solid ${group.color}33`,
                    marginBottom: 2,
                  }}
                >
                  {group.icon}
                  {group.label}
                  <span
                    style={{
                      marginLeft: 'auto',
                      fontSize: 9,
                      padding: '1px 5px',
                      borderRadius: 10,
                      background: `${group.color}22`,
                      color: group.color,
                    }}
                  >
                    {groupItems.length}
                  </span>
                </div>

                {groupItems.map((item) => {
                  const isSelected = item.id === selectedId;
                  return (
                    <button
                      key={item.id}
                      onClick={() => onSelect(item.id)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: 8,
                        padding: '8px 10px',
                        borderRadius: 7,
                        border: isSelected
                          ? '1px solid #6366f1'
                          : '1px solid rgba(255, 255, 255, 0.05)',
                        background: isSelected
                          ? 'linear-gradient(135deg, rgba(99, 102, 241, 0.35), rgba(168, 85, 247, 0.25))'
                          : 'rgba(255, 255, 255, 0.02)',
                        color: isSelected ? '#ffffff' : '#cbd5e1',
                        cursor: 'pointer',
                        textAlign: 'left',
                        transition: 'all 0.15s ease',
                        boxShadow: isSelected ? '0 0 12px rgba(99, 102, 241, 0.25)' : 'none',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                        <span style={{ fontSize: 14 }}>{item.icon}</span>
                        <div style={{ minWidth: 0 }}>
                          <div
                            style={{
                              fontSize: 11.5,
                              fontWeight: isSelected ? 700 : 600,
                              color: isSelected ? '#ffffff' : '#e2e8f0',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                          >
                            {item.title}
                          </div>
                        </div>
                      </div>

                      <span
                        style={{
                          fontSize: 8.5,
                          fontWeight: 700,
                          padding: '2px 5px',
                          borderRadius: 4,
                          flexShrink: 0,
                          background:
                            item.promptType === 'video'
                              ? 'rgba(52, 211, 153, 0.15)'
                              : item.promptType === 'attachment'
                              ? 'rgba(192, 132, 252, 0.15)'
                              : 'rgba(56, 189, 248, 0.15)',
                          color:
                            item.promptType === 'video'
                              ? '#34d399'
                              : item.promptType === 'attachment'
                              ? '#c084fc'
                              : '#38bdf8',
                          border:
                            item.promptType === 'video'
                              ? '1px solid rgba(52, 211, 153, 0.3)'
                              : item.promptType === 'attachment'
                              ? '1px solid rgba(192, 132, 252, 0.3)'
                              : '1px solid rgba(56, 189, 248, 0.3)',
                        }}
                      >
                        {item.promptType === 'video' ? '🎬 Video' : item.promptType === 'attachment' ? '⚡ Prop' : '🖼️ Ảnh'}
                      </span>
                    </button>
                  );
                })}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};
