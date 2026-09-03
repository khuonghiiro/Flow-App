import React from 'react';
import { SkillTreeNode, SkillTreeLink } from './types';
import { getOrthogonalPath, getCurvedPath } from './lineUtils';

interface SkillTreeLinksLayerProps {
  links: SkillTreeLink[];
  nodeMap: Map<string, SkillTreeNode>;
  activePathSet: Set<string>;
  lineStyle: 'orthogonal' | 'curved';
  isLinkDimmed: (fromNode?: SkillTreeNode, toNode?: SkillTreeNode) => boolean;
  zoom?: number;
  isInteracting?: boolean;
}

const SkillTreeLinksLayerBase: React.FC<SkillTreeLinksLayerProps> = ({
  links,
  nodeMap,
  activePathSet,
  lineStyle,
  isLinkDimmed,
  zoom = 1,
  isInteracting = false,
}) => {
  // Only use expensive SVG Gaussian blur filter when zoomed in close and idle
  const useGlowFilter = !isInteracting && zoom >= 0.35;

  return (
    <svg
      style={{
        position: 'absolute',
        left: 0,
        top: 0,
        width: 1,
        height: 1,
        pointerEvents: 'none',
        overflow: 'visible',
      }}
    >
      {useGlowFilter && (
        <defs>
          <filter id="tree-glow" x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur stdDeviation="3" result="blur" />
            <feComposite in="SourceGraphic" in2="blur" operator="over" />
          </filter>
        </defs>
      )}

      {links.map((link, idx) => {
        const from = nodeMap.get(link.fromId);
        const to = nodeMap.get(link.toId);
        if (!from || !to) return null;

        const isLinkInActivePath =
          activePathSet.has(link.fromId) && activePathSet.has(link.toId);
        const dimmed = !isLinkInActivePath && isLinkDimmed(from, to);

        const pathD =
          lineStyle === 'orthogonal'
            ? getOrthogonalPath(from.x, from.y, to.x, to.y, 14)
            : getCurvedPath(from.x, from.y, to.x, to.y, 0.45);

        return (
          <g key={`${link.fromId}-${link.toId}-${idx}`}>
            {/* Halo underglow: lightweight fake glow when filter is off, or SVG filter when on */}
            <path
              d={pathD}
              fill="none"
              stroke={isLinkInActivePath ? '#ffffff' : link.color}
              strokeWidth={isLinkInActivePath ? 6 : link.animated ? 4 : 3}
              strokeOpacity={isLinkInActivePath ? 0.6 : dimmed ? 0.06 : 0.22}
              filter={useGlowFilter ? 'url(#tree-glow)' : 'none'}
            />
            {/* Sharp core link */}
            <path
              d={pathD}
              fill="none"
              stroke={link.color}
              strokeWidth={isLinkInActivePath ? 3 : link.animated ? 2 : 1.2}
              strokeOpacity={isLinkInActivePath ? 1 : dimmed ? 0.15 : 0.85}
              strokeDasharray={isLinkInActivePath || link.animated ? '8 4' : 'none'}
            />
          </g>
        );
      })}
    </svg>
  );
};

export const SkillTreeLinksLayer = React.memo(SkillTreeLinksLayerBase);
