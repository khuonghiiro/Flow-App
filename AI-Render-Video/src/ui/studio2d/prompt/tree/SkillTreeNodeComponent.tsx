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
  const isHub = Boolean(node.isHub);
  const isLeafPromptNode = !isRoot && !isPillar && !isHub && Boolean(node.promptId);

  // Sizing: Icon is the primary hero - enlarged root and main pillars for maximum clarity
  const orbSize = isRoot ? 120 : isPillar ? 92 : isHub ? 62 : 48;
  const iconSize = isRoot ? 84 : isPillar ? 64 : isHub ? 40 : 30;

  const nodeColor = node.color || '#38bdf8';
  const active = isSelected || isActivePath;
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
              ? `drop-shadow(0 0 5px ${nodeColor}88)`
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
            border: `1.5px solid ${nodeColor}`,
            color: '#38bdf8',
            fontSize: 10,
            fontWeight: 800,
            whiteSpace: 'nowrap',
            boxShadow: `0 0 16px ${nodeColor}`,
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
            border: `1.5px solid ${nodeColor}`,
            boxShadow: `0 0 16px ${nodeColor}55, 0 8px 24px rgba(0,0,0,0.8)`,
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
                background: `${nodeColor}22`,
                border: `1px solid ${nodeColor}66`,
                color: '#f8fafc',
                marginTop: 2,
              }}
            >
              {node.badge}
            </span>
          )}
          {isLeafPromptNode && (
            <div style={{ fontSize: 8.5, color: nodeColor, marginTop: 2, fontWeight: 700 }}>
              ✦ Nhấp để nạp Prompt này vào studio
            </div>
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
          borderRadius: isRoot ? '50%' : isPillar ? 22 : isHub ? '50%' : 12,
          background: isSelected || isDragging
            ? `radial-gradient(circle at center, ${nodeColor}45 0%, #070e1c 100%)`
            : isRoot
            ? `radial-gradient(circle at center, ${nodeColor}35 0%, #081329 70%, #030712 100%)`
            : isPillar
            ? `radial-gradient(circle at center, ${nodeColor}28 0%, #070f20 70%, #030712 100%)`
            : active
            ? `radial-gradient(circle at center, ${nodeColor}25 0%, #060b18 100%)`
            : isLeafPromptNode
            ? `radial-gradient(circle at 45% 40%, ${nodeColor}18 0%, #091122 70%, #030712 100%)`
            : 'radial-gradient(circle at center, #0e172a 0%, #030712 100%)',
          border: isDragging
            ? `3px solid #ffffff`
            : isSelected
            ? `3px solid ${nodeColor}`
            : isRoot
            ? `3px solid ${nodeColor}`
            : isPillar
            ? `2.5px solid ${nodeColor}`
            : active
            ? `2px solid ${nodeColor}aa`
            : isHovered
            ? `2px solid ${nodeColor}`
            : isLeafPromptNode
            ? `1.5px solid ${nodeColor}80`
            : `1.5px solid rgba(255, 255, 255, 0.14)`,
          boxShadow: isDragging
            ? `0 0 36px #ffffff, 0 0 60px ${nodeColor}`
            : isSelected
            ? `0 0 30px ${nodeColor}dd, 0 0 60px ${nodeColor}66, inset 0 0 16px ${nodeColor}55`
            : isRoot
            ? `0 0 35px ${nodeColor}88, 0 0 70px ${nodeColor}44, inset 0 0 20px ${nodeColor}33`
            : isPillar
            ? `0 0 24px ${nodeColor}77, 0 0 45px ${nodeColor}33, inset 0 0 14px ${nodeColor}25`
            : isHovered
            ? `0 0 18px ${nodeColor}88, 0 0 32px ${nodeColor}44, inset 0 0 8px ${nodeColor}30`
            : active
            ? `0 0 14px ${nodeColor}66`
            : isLeafPromptNode
            ? `0 0 12px ${nodeColor}35, 0 4px 12px rgba(0, 0, 0, 0.6), inset 0 0 8px ${nodeColor}18`
            : '0 4px 12px rgba(0, 0, 0, 0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          transition: isDragging ? 'none' : 'all 0.2s ease',
        }}
      >
        {/* Subtle top inner light reflection for leaf prompt nodes */}
        {isLeafPromptNode && (
          <div
            style={{
              position: 'absolute',
              top: 2,
              left: '18%',
              right: '18%',
              height: 1,
              background: `linear-gradient(to right, transparent, ${nodeColor}90, transparent)`,
              pointerEvents: 'none',
            }}
          />
        )}

        {/* Active outer dashed ring when selected or dragged */}
        {(isSelected || isDragging) && (
          <div
            style={{
              position: 'absolute',
              inset: isRoot ? -10 : -6,
              borderRadius: isRoot || isHub ? '50%' : isPillar ? 26 : 18,
              border: `1.5px dashed ${isDragging ? '#ffffff' : nodeColor}`,
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
          fontSize: isRoot ? 13 : isPillar ? 11.5 : isHub ? 10.5 : 9.5,
          fontWeight: isRoot || isPillar ? 900 : isSelected ? 800 : 600,
          color: isSelected ? '#ffffff' : isHovered ? nodeColor : isLeafPromptNode ? '#e2e8f0' : isRoot || isPillar ? '#f8fafc' : '#94a3b8',
          background: isSelected
            ? nodeColor
            : active
            ? `${nodeColor}25`
            : isRoot || isPillar
            ? 'rgba(15, 23, 42, 0.95)'
            : 'rgba(15, 23, 42, 0.85)',
          padding: isRoot ? '4px 14px' : isPillar ? '3px 10px' : '2px 6px',
          borderRadius: isRoot || isPillar ? 8 : 6,
          border: isSelected
            ? `1.5px solid ${nodeColor}`
            : active
            ? `1.5px solid ${nodeColor}88`
            : isRoot || isPillar
            ? `1.5px solid ${nodeColor}70`
            : isLeafPromptNode
            ? `1px solid ${nodeColor}40`
            : '1px solid rgba(255, 255, 255, 0.1)',
          boxShadow: isSelected
            ? `0 0 14px ${nodeColor}88`
            : isRoot || isPillar
            ? `0 0 12px ${nodeColor}40`
            : isHovered
            ? `0 0 8px ${nodeColor}44`
            : 'none',
          whiteSpace: 'nowrap',
          maxWidth: isRoot ? 160 : isPillar ? 135 : 110,
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
