import React, { useState } from 'react';
import { SkillTreeNode } from './types';

export interface SkillTreeNodeProps {
  node: SkillTreeNode;
  isSelected: boolean;
  isActivePath?: boolean;
  isDragging?: boolean;
  isEditMode?: boolean;
  onSelect: (node: SkillTreeNode) => void;
  onMouseDownNode?: (node: SkillTreeNode, e: React.MouseEvent) => void;
  canvasTheme?: 'dark' | 'light';
  isDimmed?: boolean;
}

const SkillTreeNodeBase: React.FC<SkillTreeNodeProps> = ({
  node,
  isSelected,
  isActivePath = false,
  isDragging = false,
  isEditMode = true,
  onSelect,
  onMouseDownNode,
  canvasTheme = 'dark',
  isDimmed = false,
}) => {
  const [isHovered, setIsHovered] = useState(false);

  const isRoot = node.tier === 0;
  const isPillar = node.tier === 1;
  const isHub = Boolean(node.isHub);
  const isLeafNode = !isRoot && !isPillar && !isHub;
  const isLeafPromptNode = isLeafNode && Boolean(node.promptId);

  // Sizing: For leaf nodes, no container box or border - pure, large floating icons!
  const orbSize = isRoot ? 138 : isPillar ? 112 : isHub ? 88 : 78;
  const iconSize = isRoot ? 96 : isPillar ? 78 : isHub ? 62 : 72;

  const nodeColor = node.color || '#38bdf8';
  const active = isSelected || isActivePath;
  const [imgError, setImgError] = useState<boolean>(false);
  const isLight = canvasTheme === 'light';

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
              ? `drop-shadow(0 0 18px ${nodeColor}) drop-shadow(0 0 30px rgba(255,255,255,0.9)) brightness(1.3)`
              : isHovered
              ? `drop-shadow(0 0 16px ${nodeColor}) brightness(1.25)`
              : isLight
              ? `drop-shadow(0 2px 6px rgba(0,0,0,0.3)) drop-shadow(0 0 6px ${nodeColor}cc)`
              : `drop-shadow(0 0 12px ${nodeColor}cc) drop-shadow(0 0 4px rgba(255,255,255,0.15)) brightness(1.2)`,
            transition: 'filter 0.2s ease, transform 0.2s ease',
            transform: isHovered || isSelected ? 'scale(1.12)' : 'scale(1)',
          }}
        />
      );
    }
    return (
      <span
        style={{
          fontSize: iconSize * 0.8,
          lineHeight: 1,
          pointerEvents: 'none',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          filter: isSelected
            ? `drop-shadow(0 0 14px ${nodeColor}) drop-shadow(0 0 20px rgba(255,255,255,0.95))`
            : isHovered
            ? `drop-shadow(0 0 10px ${nodeColor})`
            : `drop-shadow(0 0 6px ${nodeColor}aa)`,
          transition: 'transform 0.2s ease, filter 0.2s ease',
          transform: isHovered || isSelected ? 'scale(1.12)' : 'scale(1)',
        }}
      >
        {node.icon}
      </span>
    );
  };

  return (
    <div
      onClick={(e) => {
        e.stopPropagation();
        onSelect(node);
      }}
      onMouseDown={(e) => onMouseDownNode?.(node, e)}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      style={{
        position: 'absolute',
        left: node.x,
        top: node.y,
        opacity: isDimmed ? 0.65 : 1,
        filter: isDimmed ? 'saturate(0.9)' : 'none',
        transform: `translate3d(-50%, -50%, 0) ${
          isDragging ? 'scale(1.22)' : isSelected ? 'scale(1.15)' : isHovered ? 'scale(1.08)' : 'scale(1)'
        }`,
        cursor: isEditMode ? (isDragging ? 'grabbing' : 'grab') : 'pointer',
        zIndex: isDragging ? 50 : isSelected ? 25 : isHovered ? 20 : isRoot ? 15 : isPillar ? 12 : 8,
        userSelect: 'none',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 5,
        willChange: isDragging ? 'transform' : 'auto',
        transition: isDragging ? 'none' : 'transform 0.18s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.25s ease, filter 0.25s ease',
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
          borderRadius: isLeafNode ? 0 : isRoot || isHub ? '50%' : 18,
          background: isLeafNode
            ? 'transparent'
            : isSelected || isDragging
            ? `radial-gradient(circle at center, ${nodeColor}40 0%, #172132 70%, #0f1520 100%)`
            : isRoot
            ? `radial-gradient(circle at center, ${nodeColor}30 0%, #141c2a 70%, #0e141f 100%)`
            : isPillar
            ? `radial-gradient(circle at center, ${nodeColor}25 0%, #131a28 70%, #0d121c 100%)`
            : `radial-gradient(circle at center, ${nodeColor}22 0%, #111724 100%)`,
          border: isLeafNode
            ? 'none'
            : isDragging
            ? `2.5px solid #ffffff`
            : isSelected
            ? `2.5px solid ${nodeColor}`
            : isRoot
            ? `3px solid ${nodeColor}`
            : isPillar
            ? `2.5px solid ${nodeColor}`
            : `2px solid ${nodeColor}`,
          boxShadow: isLeafNode
            ? 'none'
            : isDragging
            ? `0 0 32px #ffffff, 0 0 50px ${nodeColor}`
            : isSelected
            ? `0 0 28px ${nodeColor}dd, 0 0 50px ${nodeColor}55, inset 0 0 14px ${nodeColor}40`
            : isRoot
            ? `0 0 32px ${nodeColor}90, 0 0 60px ${nodeColor}30, inset 0 0 18px ${nodeColor}35`
            : isPillar
            ? `0 0 24px ${nodeColor}80, 0 0 45px ${nodeColor}25, inset 0 0 14px ${nodeColor}28`
            : `0 0 18px ${nodeColor}70, 0 0 35px ${nodeColor}20`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          transition: isDragging ? 'none' : 'all 0.2s ease',
        }}
      >
        {/* Active outer dashed ring when selected or dragged */}
        {(isSelected || isDragging) && (
          <div
            style={{
              position: 'absolute',
              inset: isLeafNode ? -8 : isRoot ? -10 : -6,
              borderRadius: isRoot || isHub || isLeafNode ? '50%' : 18,
              border: `1.8px dashed ${isDragging ? '#ffffff' : nodeColor}`,
              opacity: 0.85,
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
          fontSize: isRoot ? 13.5 : isPillar ? 12.5 : isHub ? 11.5 : 11,
          fontWeight: isRoot || isPillar ? 800 : isSelected ? 800 : 700,
          color: isSelected ? '#ffffff' : isHovered ? nodeColor : '#f1f5f9',
          background: isSelected
            ? nodeColor
            : active
            ? `${nodeColor}35`
            : isLeafNode
            ? isLight
              ? 'rgba(15, 23, 42, 0.92)'
              : 'rgba(8, 14, 28, 0.94)'
            : 'rgba(10, 16, 30, 0.95)',
          padding: isRoot ? '4px 14px' : isPillar ? '3px 11px' : '2.5px 9px',
          borderRadius: 6,
          border: isSelected
            ? `1px solid ${nodeColor}`
            : active
            ? `1px solid ${nodeColor}90`
            : isLeafNode
            ? `1px solid ${nodeColor}45`
            : '1px solid rgba(148, 163, 184, 0.20)',
          boxShadow: isSelected
            ? `0 0 12px ${nodeColor}88`
            : isHovered
            ? `0 0 8px ${nodeColor}40`
            : '0 2px 8px rgba(0, 0, 0, 0.35)',
          whiteSpace: 'nowrap',
          maxWidth: isRoot ? 180 : isPillar ? 160 : 135,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          textAlign: 'center',
          backdropFilter: 'blur(4px)',
          pointerEvents: 'none',
          transition: 'all 0.15s ease',
        }}
      >
        {node.shortLabel}
      </div>
    </div>
  );
};

export const SkillTreeNodeComponent = React.memo(SkillTreeNodeBase, (prev, next) => {
  return (
    prev.node.id === next.node.id &&
    prev.node.x === next.node.x &&
    prev.node.y === next.node.y &&
    prev.isSelected === next.isSelected &&
    prev.isActivePath === next.isActivePath &&
    prev.isDragging === next.isDragging &&
    prev.isEditMode === next.isEditMode &&
    prev.canvasTheme === next.canvasTheme &&
    prev.isDimmed === next.isDimmed &&
    prev.node.shortLabel === next.node.shortLabel &&
    prev.node.color === next.node.color
  );
});
