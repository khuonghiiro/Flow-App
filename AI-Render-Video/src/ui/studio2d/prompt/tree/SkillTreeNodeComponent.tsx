import React, { useState } from 'react';
import { SkillTreeNode } from './types';

interface SkillTreeNodeProps {
  node: SkillTreeNode;
  isSelected: boolean;
  isActivePath?: boolean;
  isDragging?: boolean;
  isEditMode?: boolean;
  onSelect: (node: SkillTreeNode) => void;
  onMouseDownNode?: (node: SkillTreeNode, e: React.MouseEvent) => void;
}

export const SkillTreeNodeComponent: React.FC<SkillTreeNodeProps> = ({
  node,
  isSelected,
  isActivePath = false,
  isDragging = false,
  isEditMode = true,
  onSelect,
  onMouseDownNode,
}) => {
  const [isHovered, setIsHovered] = useState(false);

  const isRoot = node.tier === 0;
  const isPillar = node.tier === 1;
  const isHub = node.tier === 2 && node.isHub;

  // Sizing: Icon is the primary hero
  const orbSize = isRoot ? 84 : isPillar ? 68 : isHub ? 56 : 48;
  const iconSize = isRoot ? 54 : isPillar ? 44 : isHub ? 36 : 30;

  const active = isSelected || isActivePath;
  const activeColor = node.color || '#38bdf8';
  const [imgError, setImgError] = useState<boolean>(false);

  const renderIcon = () => {
    if (node.iconPath && !imgError) {
      return (
        <img
          src={node.iconPath}
          alt={node.shortLabel}
          onError={() => setImgError(true)}
          draggable={false}
          style={{
            width: iconSize,
            height: iconSize,
            objectFit: 'contain',
            pointerEvents: 'none',
            filter: isSelected
              ? 'drop-shadow(0 0 8px rgba(255,255,255,0.9))'
              : isHovered
              ? 'drop-shadow(0 0 5px rgba(255,255,255,0.6))'
              : 'none',
            transition: 'filter 0.2s ease, transform 0.2s ease',
            transform: isHovered || isSelected ? 'scale(1.08)' : 'scale(1)',
          }}
        />
      );
    }
    return <span style={{ fontSize: iconSize * 0.75, pointerEvents: 'none' }}>{node.icon}</span>;
  };

  return (
    <div
      onClick={() => onSelect(node)}
      onMouseDown={(e) => onMouseDownNode?.(node, e)}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      style={{
        position: 'absolute',
        left: node.x,
        top: node.y,
        transform: `translate(-50%, -50%) ${
          isDragging ? 'scale(1.22)' : isSelected ? 'scale(1.15)' : isHovered ? 'scale(1.08)' : 'scale(1)'
        }`,
        cursor: isEditMode ? (isDragging ? 'grabbing' : 'grab') : 'pointer',
        zIndex: isDragging ? 50 : isSelected ? 25 : isHovered ? 20 : isRoot ? 15 : isPillar ? 12 : 8,
        userSelect: 'none',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 5,
        transition: isDragging ? 'none' : 'transform 0.22s cubic-bezier(0.34, 1.56, 0.64, 1)',
      }}
    >
      {/* ─── Dragging Live Coordinates Badge ─── */}
      {isDragging && (
        <div
          style={{
            position: 'absolute',
            top: -28,
            padding: '2px 8px',
            borderRadius: 6,
            background: '#0f172a',
            border: `1.5px solid ${activeColor}`,
            color: '#38bdf8',
            fontSize: 10,
            fontWeight: 800,
            whiteSpace: 'nowrap',
            boxShadow: `0 0 16px ${activeColor}`,
            pointerEvents: 'none',
          }}
        >
          X: {Math.round(node.x)} | Y: {Math.round(node.y)}
        </div>
      )}

      {/* ─── Hover Tooltip ─── */}
      {isHovered && !isDragging && (
        <div
          style={{
            position: 'absolute',
            bottom: '100%',
            marginBottom: 8,
            padding: '6px 10px',
            borderRadius: 8,
            background: 'rgba(3, 7, 18, 0.95)',
            border: `1.5px solid ${activeColor}`,
            boxShadow: `0 0 20px ${activeColor}66, 0 8px 24px rgba(0,0,0,0.8)`,
            backdropFilter: 'blur(10px)',
            whiteSpace: 'nowrap',
            pointerEvents: 'none',
            zIndex: 40,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            gap: 2,
            animation: 'fadeIn 0.15s ease',
          }}
        >
          <div style={{ fontSize: 11.5, fontWeight: 800, color: '#ffffff' }}>
            {node.title}
          </div>
          {node.subtitle && (
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>{node.subtitle}</div>
          )}
          {node.badge && (
            <span
              style={{
                fontSize: 8.5,
                fontWeight: 800,
                padding: '1px 6px',
                borderRadius: 4,
                background: `${activeColor}25`,
                color: activeColor,
                marginTop: 2,
              }}
            >
              {node.badge}
            </span>
          )}
          {isEditMode && (
            <div style={{ fontSize: 8.5, color: '#38bdf8', marginTop: 2, fontStyle: 'italic' }}>
              ✦ Nhấp giữ để kéo di chuyển vị trí
            </div>
          )}
        </div>
      )}

      {/* ─── Hero Skill Orb ─── */}
      <div
        style={{
          width: orbSize,
          height: orbSize,
          borderRadius: isRoot || isHub ? '50%' : isPillar ? 16 : 12,
          background: isSelected || isDragging
            ? `radial-gradient(circle at center, ${activeColor}55 0%, #080e1e 100%)`
            : active
            ? `radial-gradient(circle at center, ${activeColor}25 0%, #060b18 100%)`
            : 'radial-gradient(circle at center, #0e172a 0%, #030712 100%)',
          border: isDragging
            ? `2.5px solid #ffffff`
            : isSelected
            ? `2.5px solid ${activeColor}`
            : active
            ? `2px solid ${activeColor}aa`
            : isHovered
            ? `2px solid ${activeColor}88`
            : `1.5px solid rgba(255, 255, 255, 0.14)`,
          boxShadow: isDragging
            ? `0 0 35px #ffffff, 0 0 60px ${activeColor}`
            : isSelected
            ? `0 0 30px ${activeColor}, 0 0 50px ${activeColor}66`
            : isHovered
            ? `0 0 20px ${activeColor}77`
            : active
            ? `0 0 16px ${activeColor}44`
            : '0 4px 12px rgba(0, 0, 0, 0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          transition: isDragging ? 'none' : 'all 0.2s ease',
        }}
      >
        {/* Active outer pulse ring for Root and selected */}
        {(isSelected || isDragging) && (
          <div
            style={{
              position: 'absolute',
              inset: -6,
              borderRadius: isRoot || isHub ? '50%' : isPillar ? 22 : 18,
              border: `1.5px dashed ${isDragging ? '#ffffff' : activeColor}`,
              opacity: 0.8,
              pointerEvents: 'none',
            }}
          />
        )}

        {/* The Icon */}
        {renderIcon()}
      </div>

      {/* ─── Secondary Sub-text Badge (Làm phụ) ─── */}
      <div
        style={{
          fontSize: isRoot ? 11 : isPillar ? 10.5 : 9.5,
          fontWeight: isSelected ? 800 : 700,
          color: isSelected ? '#ffffff' : active ? activeColor : '#cbd5e1',
          background: isSelected
            ? activeColor
            : active
            ? `${activeColor}33`
            : 'rgba(15, 23, 42, 0.85)',
          padding: isRoot ? '2px 8px' : '2px 6px',
          borderRadius: 6,
          border: isSelected
            ? `1px solid ${activeColor}`
            : active
            ? `1px solid ${activeColor}66`
            : '1px solid rgba(255, 255, 255, 0.1)',
          boxShadow: isSelected ? `0 0 12px ${activeColor}88` : 'none',
          whiteSpace: 'nowrap',
          maxWidth: 110,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          textAlign: 'center',
          backdropFilter: 'blur(4px)',
          pointerEvents: 'none',
          transition: 'all 0.2s ease',
        }}
      >
        {node.shortLabel}
      </div>
    </div>
  );
};
