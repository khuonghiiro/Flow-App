import React from 'react';
import { SkillTreeNode, SkillTreeLink } from './types';
import { getOrthogonalPath, getCurvedPath } from './lineUtils';

interface SkillTreeLinksLayerProps {
  links: SkillTreeLink[];
  nodeMap: Map<string, SkillTreeNode>;
  activePathSet: Set<string>;
  lineStyle: 'orthogonal' | 'curved';
  isLinkDimmed: (fromNode?: SkillTreeNode, toNode?: SkillTreeNode) => boolean;
}

export const SkillTreeLinksLayer: React.FC<SkillTreeLinksLayerProps> = ({
  links,
  nodeMap,
  activePathSet,
  lineStyle,
  isLinkDimmed,
}) => {
  return (
    <svg
      style={{
        position: 'absolute',
        left: -8000,
        top: -4000,
        width: 18000,
        height: 12000,
        pointerEvents: 'none',
        overflow: 'visible',
      }}
      viewBox="-8000 -4000 18000 12000"
    >
      <defs>
        <filter id="tree-glow" x="-30%" y="-30%" width="160%" height="160%">
          <feGaussianBlur stdDeviation="4" result="blur" />
          <feComposite in="SourceGraphic" in2="blur" operator="over" />
        </filter>
      </defs>

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
            <path
              d={pathD}
              fill="none"
              stroke={isLinkInActivePath ? '#ffffff' : link.color}
              strokeWidth={isLinkInActivePath ? 6 : link.animated ? 4 : 2.5}
              strokeOpacity={isLinkInActivePath ? 0.7 : dimmed ? 0.08 : 0.25}
              filter="url(#tree-glow)"
            />
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
