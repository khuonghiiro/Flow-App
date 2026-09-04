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
  canvasTheme?: 'dark' | 'light';
}

/** Category → gradient color pair for richer line aesthetics */
const CATEGORY_GRADIENT: Record<string, [string, string]> = {
  character: ['#38bdf8', '#60a5fa'],
  walk: ['#34d399', '#6ee7b7'],
  actions: ['#f59e0b', '#fbbf24'],
  face: ['#ec4899', '#f472b6'],
  weapons: ['#c084fc', '#a78bfa'],
};

const SkillTreeLinksLayerBase: React.FC<SkillTreeLinksLayerProps> = ({
  links,
  nodeMap,
  activePathSet,
  lineStyle,
  isLinkDimmed,
  zoom = 1,
  isInteracting = false,
  canvasTheme = 'dark',
}) => {
  // Only use expensive SVG effects when zoomed in close and idle
  const useEffects = !isInteracting && zoom >= 0.25;
  const isLight = canvasTheme === 'light';

  // Collect unique gradient IDs needed
  const gradientSet = new Set<string>();
  links.forEach((link) => {
    const from = nodeMap.get(link.fromId);
    if (from) gradientSet.add(from.category);
  });

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
      <defs>
        {/* Glow filter for active links */}
        {useEffects && (
          <filter id="tree-glow" x="-25%" y="-25%" width="150%" height="150%">
            <feGaussianBlur stdDeviation="4" result="blur" />
            <feComposite in="SourceGraphic" in2="blur" operator="over" />
          </filter>
        )}

        {/* Category-based linear gradients */}
        {useEffects &&
          Array.from(gradientSet).map((cat) => {
            const [c1, c2] = CATEGORY_GRADIENT[cat] || ['#94a3b8', '#cbd5e1'];
            return (
              <linearGradient key={`grad-${cat}`} id={`link-grad-${cat}`} x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stopColor={c1} stopOpacity={0.9} />
                <stop offset="100%" stopColor={c2} stopOpacity={0.9} />
              </linearGradient>
            );
          })}

        {/* Active path glow gradient */}
        {useEffects && (
          <linearGradient id="active-link-grad" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#60a5fa" stopOpacity={1} />
            <stop offset="50%" stopColor="#38bdf8" stopOpacity={1} />
            <stop offset="100%" stopColor="#818cf8" stopOpacity={1} />
          </linearGradient>
        )}

        {/* Animated flow marker for active links */}
        {useEffects && (
          <marker id="flow-dot" viewBox="0 0 6 6" refX="3" refY="3" markerWidth="4" markerHeight="4">
            <circle cx="3" cy="3" r="2.5" fill="#38bdf8" opacity="0.9" />
          </marker>
        )}
      </defs>

      {links.map((link, idx) => {
        const from = nodeMap.get(link.fromId);
        const to = nodeMap.get(link.toId);
        if (!from || !to) return null;

        const isLinkInActivePath =
          activePathSet.has(link.fromId) && activePathSet.has(link.toId);
        const dimmed = !isLinkInActivePath && isLinkDimmed(from, to);
        const category = from.category;

        const pathD =
          lineStyle === 'orthogonal'
            ? getOrthogonalPath(from.x, from.y, to.x, to.y, 18)
            : getCurvedPath(from.x, from.y, to.x, to.y, 0.45);

        const gradId = useEffects ? `url(#link-grad-${category})` : link.color;
        const activeGrad = useEffects ? 'url(#active-link-grad)' : (isLight ? '#1e40af' : '#60a5fa');

        return (
          <g key={`${link.fromId}-${link.toId}-${idx}`}>
            {/* Layer 1: Outer glow halo */}
            <path
              d={pathD}
              fill="none"
              stroke={isLinkInActivePath ? activeGrad : gradId}
              strokeWidth={isLinkInActivePath ? 8 : link.animated ? 5 : 3.5}
              strokeOpacity={isLinkInActivePath ? 0.35 : dimmed ? 0.08 : 0.18}
              strokeLinecap="round"
              strokeLinejoin="round"
              filter={useEffects && isLinkInActivePath ? 'url(#tree-glow)' : 'none'}
            />

            {/* Layer 2: Core line with gradient */}
            <path
              d={pathD}
              fill="none"
              stroke={isLinkInActivePath ? activeGrad : gradId}
              strokeWidth={isLinkInActivePath ? 2.8 : link.animated ? 1.8 : 1.2}
              strokeOpacity={isLinkInActivePath ? 1 : dimmed ? 0.4 : 0.75}
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeDasharray={
                isLinkInActivePath
                  ? '10 5'
                  : link.animated
                  ? '6 4'
                  : 'none'
              }
            >
              {/* Animated dash movement for active paths */}
              {isLinkInActivePath && useEffects && (
                <animate
                  attributeName="stroke-dashoffset"
                  from="0"
                  to="-30"
                  dur="1.5s"
                  repeatCount="indefinite"
                />
              )}
            </path>

            {/* Layer 3: Tiny endpoint dots for non-dimmed links */}
            {!dimmed && useEffects && !isInteracting && (
              <>
                <circle
                  cx={from.x}
                  cy={from.y}
                  r={isLinkInActivePath ? 3 : 2}
                  fill={isLinkInActivePath ? '#60a5fa' : link.color}
                  opacity={isLinkInActivePath ? 0.8 : 0.5}
                />
                <circle
                  cx={to.x}
                  cy={to.y}
                  r={isLinkInActivePath ? 3 : 2}
                  fill={isLinkInActivePath ? '#818cf8' : link.color}
                  opacity={isLinkInActivePath ? 0.8 : 0.5}
                />
              </>
            )}
          </g>
        );
      })}
    </svg>
  );
};

export const SkillTreeLinksLayer = React.memo(SkillTreeLinksLayerBase);
