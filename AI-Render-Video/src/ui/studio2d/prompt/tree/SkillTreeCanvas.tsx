import React, { useState, useRef, useMemo, useCallback } from 'react';
import { SkillTreeNode, SkillBranchCategory } from './types';
import { SKILL_TREE_NODES, SKILL_TREE_LINKS } from './skillTreeData';
import { SkillTreeNodeComponent } from './SkillTreeNodeComponent';
import { SkillTreeControlBar } from './SkillTreeControlBar';
import { LayoutExportModal } from './LayoutExportModal';

interface SkillTreeCanvasProps {
  selectedPromptId: string;
  onSelectPrompt: (promptId: string) => void;
}

const STORAGE_KEY = 'studio2d_skill_tree_custom_layout_v6';

export const SkillTreeCanvas: React.FC<SkillTreeCanvasProps> = ({
  selectedPromptId,
  onSelectPrompt,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  // ─── Custom Layout State (Loaded from localStorage or defaults) ───
  const [nodes, setNodes] = useState<SkillTreeNode[]>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed: Array<{ id: string; x: number; y: number }> = JSON.parse(saved);
        const posMap = new Map(parsed.map((p) => [p.id, p]));
        return SKILL_TREE_NODES.map((n) => {
          const custom = posMap.get(n.id);
          return custom ? { ...n, x: custom.x, y: custom.y } : n;
        });
      }
    } catch (e) {
      console.warn('Failed to load custom skill tree layout', e);
    }
    return SKILL_TREE_NODES;
  });

  const [isCustomized, setIsCustomized] = useState<boolean>(() => {
    return Boolean(localStorage.getItem(STORAGE_KEY));
  });

  // Pan & Zoom state (Optimized for user-crafted layout)
  const [zoom, setZoom] = useState<number>(0.38);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 440, y: 40 });
  const [isDraggingCanvas, setIsDraggingCanvas] = useState<boolean>(false);
  const [dragStart, setDragStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // Node Dragging State
  const [isEditMode, setIsEditMode] = useState<boolean>(true);
  const [draggingNodeId, setDraggingNodeId] = useState<string | null>(null);
  const [dragOffset, setDragOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [dragMoved, setDragMoved] = useState<boolean>(false);
  const dragClientStartRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  // UI state
  const [activeBranch, setActiveBranch] = useState<SkillBranchCategory | 'all'>('all');
  const [selectedNodeId, setSelectedNodeId] = useState<string>('root_master');
  const [isExportModalOpen, setIsExportModalOpen] = useState<boolean>(false);

  // Node lookup map for dynamic SVG link resolution
  const nodeMap = useMemo(() => {
    const map = new Map<string, SkillTreeNode>();
    nodes.forEach((n) => map.set(n.id, n));
    return map;
  }, [nodes]);

  // Determine currently active node based on selectedPromptId or selectedNodeId
  const activeNode = useMemo(() => {
    const byPrompt = nodes.find((n) => n.promptId === selectedPromptId);
    if (byPrompt && selectedNodeId !== byPrompt.id) {
      return byPrompt;
    }
    return nodeMap.get(selectedNodeId) || byPrompt || nodes[0];
  }, [selectedPromptId, selectedNodeId, nodeMap, nodes]);

  // Compute Active Path Lineage from Root to the Active Node
  const activePathSet = useMemo(() => {
    const set = new Set<string>();
    let curr: SkillTreeNode | undefined = activeNode;
    while (curr) {
      set.add(curr.id);
      curr = curr.parentId ? nodeMap.get(curr.parentId) : undefined;
    }
    return set;
  }, [activeNode, nodeMap]);

  // ─── Drag & Drop Handlers for Nodes ───
  const handleNodeMouseDown = (node: SkillTreeNode, e: React.MouseEvent) => {
    if (!isEditMode || e.button !== 0) return;
    e.stopPropagation();

    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;

    dragClientStartRef.current = { x: e.clientX, y: e.clientY };
    const worldX = (e.clientX - rect.left - pan.x) / zoom;
    const worldY = (e.clientY - rect.top - pan.y) / zoom;

    setDragOffset({ x: worldX - node.x, y: worldY - node.y });
    setDraggingNodeId(node.id);
    setDragMoved(false);
  };

  // Canvas Pan Handlers
  const handleMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return;
    setIsDraggingCanvas(true);
    setDragStart({ x: e.clientX - pan.x, y: e.clientY - pan.y });
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    // 1. Moving a Node
    if (draggingNodeId && containerRef.current) {
      const dist = Math.hypot(
        e.clientX - dragClientStartRef.current.x,
        e.clientY - dragClientStartRef.current.y
      );
      if (dist < 4) return; // threshold to prevent click jitter
      setDragMoved(true);

      const rect = containerRef.current?.getBoundingClientRect();
      if (!rect) return;
      const worldX = (e.clientX - rect.left - pan.x) / zoom;
      const worldY = (e.clientY - rect.top - pan.y) / zoom;

      const newX = Math.round(worldX - dragOffset.x);
      const newY = Math.round(worldY - dragOffset.y);

      setNodes((prev) =>
        prev.map((n) => (n.id === draggingNodeId ? { ...n, x: newX, y: newY } : n))
      );
      return;
    }

    // 2. Panning the Canvas
    if (isDraggingCanvas) {
      setPan({
        x: e.clientX - dragStart.x,
        y: e.clientY - dragStart.y,
      });
    }
  };

  const handleMouseUp = () => {
    if (draggingNodeId) {
      if (dragMoved) {
        // Save current layout to localStorage
        const layoutData = nodes.map((n) => ({
          id: n.id,
          x: Math.round(n.x),
          y: Math.round(n.y),
          label: n.shortLabel,
        }));
        localStorage.setItem(STORAGE_KEY, JSON.stringify(layoutData));
        setIsCustomized(true);
      } else {
        // Direct click without dragging
        const target = nodes.find((n) => n.id === draggingNodeId);
        if (target) {
          handleNodeClick(target);
        }
      }
      setDraggingNodeId(null);
    }
    setIsDraggingCanvas(false);
  };

  // Wheel Zoom Towards Mouse Cursor Coordinates
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    if (!containerRef.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;

    const zoomFactor = e.deltaY < 0 ? 1.14 : 0.88;
    const newZoom = Math.max(0.35, Math.min(1.8, zoom * zoomFactor));

    const worldX = (mouseX - pan.x) / zoom;
    const worldY = (mouseY - pan.y) / zoom;

    const newPanX = mouseX - worldX * newZoom;
    const newPanY = mouseY - worldY * newZoom;

    setZoom(newZoom);
    setPan({ x: newPanX, y: newPanY });
  };

  // Zoom helpers
  const handleZoomIn = () => setZoom((z) => Math.min(1.8, z + 0.15));
  const handleZoomOut = () => setZoom((z) => Math.max(0.35, z - 0.15));
  const handleResetZoom = () => {
    setZoom(0.38);
    setPan({ x: 440, y: 40 });
    setActiveBranch('all');
  };

  // Reset to default coordinates
  const handleResetDefaultLayout = () => {
    localStorage.removeItem(STORAGE_KEY);
    setNodes(SKILL_TREE_NODES);
    setIsCustomized(false);
  };

  // Branch focus: smooth centering on pillar
  const handleSelectBranch = useCallback(
    (branch: SkillBranchCategory | 'all') => {
      setActiveBranch(branch);
      if (branch === 'all') {
        setPan({ x: 440, y: 40 });
        setZoom(0.38);
        return;
      }
      const pillar = nodes.find((n) => n.id === `pillar_${branch}`);
      if (pillar) {
        setSelectedNodeId(pillar.id);
        if (pillar.promptId) {
          onSelectPrompt(pillar.promptId);
        }
        if (containerRef.current) {
          const rect = containerRef.current.getBoundingClientRect();
          const targetZoom = 0.75;
          setZoom(targetZoom);
          setPan({
            x: rect.width * 0.35 - pillar.x * targetZoom,
            y: rect.height / 2 - pillar.y * targetZoom,
          });
        }
      }
    },
    [nodes, onSelectPrompt]
  );

  // Click on node
  const handleNodeClick = (node: SkillTreeNode) => {
    setSelectedNodeId(node.id);
    setActiveBranch(node.category);
    if (node.promptId) {
      onSelectPrompt(node.promptId);
    }
  };

  // Determine branch dimming
  const isNodeDimmed = (node: SkillTreeNode) => {
    if (activeBranch === 'all') return false;
    return node.category !== activeBranch && node.tier !== 0;
  };

  const isLinkDimmed = (fromNode?: SkillTreeNode, toNode?: SkillTreeNode) => {
    if (activeBranch === 'all') return false;
    if (!fromNode || !toNode) return false;
    return fromNode.category !== activeBranch && toNode.category !== activeBranch;
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: 8,
        overflow: 'hidden',
        position: 'relative',
      }}
    >
      {/* ─── Control Bar ─── */}
      <SkillTreeControlBar
        activeBranch={activeBranch}
        onSelectBranch={handleSelectBranch}
        zoom={zoom}
        onZoomIn={handleZoomIn}
        onZoomOut={handleZoomOut}
        onResetZoom={handleResetZoom}
        totalNodes={nodes.length}
        isEditMode={isEditMode}
        onToggleEditMode={() => setIsEditMode((prev) => !prev)}
        onOpenExportModal={() => setIsExportModalOpen(true)}
        isCustomized={isCustomized}
      />

      {/* ─── Canvas Viewport ─── */}
      <div
        ref={containerRef}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseUp}
        onWheel={handleWheel}
        style={{
          flex: 1,
          position: 'relative',
          overflow: 'hidden',
          borderRadius: 10,
          background: 'radial-gradient(ellipse at 30% 30%, #0c152c 0%, #030712 100%)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          cursor: draggingNodeId ? 'grabbing' : isDraggingCanvas ? 'grabbing' : 'default',
          userSelect: 'none',
        }}
      >
        {/* Subtle Cyber / RPG Grid Background */}
        <div
          style={{
            position: 'absolute',
            inset: 0,
            backgroundImage: `
              linear-gradient(to right, rgba(56, 189, 248, 0.05) 1px, transparent 1px),
              linear-gradient(to bottom, rgba(56, 189, 248, 0.05) 1px, transparent 1px)
            `,
            backgroundSize: `${36 * zoom}px ${36 * zoom}px`,
            backgroundPosition: `${pan.x}px ${pan.y}px`,
            pointerEvents: 'none',
          }}
        />

        {/* ─── Transformable World Canvas ─── */}
        <div
          style={{
            position: 'absolute',
            left: 0,
            top: 0,
            transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
            transformOrigin: '0 0',
            width: 4400,
            height: 4800,
          }}
        >
          {/* ─── SVG Connecting Lines Layer (Dynamic Bézier) ─── */}
          <svg
            style={{
              position: 'absolute',
              left: 0,
              top: 0,
              width: 4400,
              height: 4800,
              pointerEvents: 'none',
              overflow: 'visible',
            }}
          >
            <defs>
              <filter id="tree-glow" x="-30%" y="-30%" width="160%" height="160%">
                <feGaussianBlur stdDeviation="4" result="blur" />
                <feComposite in="SourceGraphic" in2="blur" operator="over" />
              </filter>
            </defs>

            {SKILL_TREE_LINKS.map((link, idx) => {
              const from = nodeMap.get(link.fromId);
              const to = nodeMap.get(link.toId);
              if (!from || !to) return null;

              const isLinkInActivePath = activePathSet.has(link.fromId) && activePathSet.has(link.toId);
              const dimmed = !isLinkInActivePath && isLinkDimmed(from, to);

              const dx = to.x - from.x;
              const ctrl1X = from.x + dx * 0.45;
              const ctrl1Y = from.y;
              const ctrl2X = to.x - dx * 0.45;
              const ctrl2Y = to.y;
              const pathD = `M ${from.x} ${from.y} C ${ctrl1X} ${ctrl1Y}, ${ctrl2X} ${ctrl2Y}, ${to.x} ${to.y}`;

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

          {/* ─── Skill Nodes Layer ─── */}
          {nodes.map((node) => {
            const isSelected =
              node.id === selectedNodeId ||
              (node.promptId && node.promptId === selectedPromptId) ||
              (node.id === 'root_master' && selectedPromptId === 'character_base');
            const isActivePath = activePathSet.has(node.id);
            const dimmed = !isActivePath && isNodeDimmed(node);
            const isNodeDragging = draggingNodeId === node.id;

            return (
              <div
                key={node.id}
                style={{
                  opacity: dimmed ? 0.25 : 1,
                  filter: dimmed ? 'grayscale(0.6)' : 'none',
                  transition: isNodeDragging ? 'none' : 'opacity 0.25s ease, filter 0.25s ease',
                }}
              >
                <SkillTreeNodeComponent
                  node={node}
                  isSelected={Boolean(isSelected)}
                  isActivePath={Boolean(isActivePath)}
                  isDragging={isNodeDragging}
                  isEditMode={isEditMode}
                  onSelect={handleNodeClick}
                  onMouseDownNode={handleNodeMouseDown}
                />
              </div>
            );
          })}
        </div>

        {/* ─── Interactive Helper Legend Overlay ─── */}
        <div
          style={{
            position: 'absolute',
            bottom: 12,
            left: 12,
            padding: '6px 14px',
            borderRadius: 8,
            background: 'rgba(3, 7, 18, 0.9)',
            border: '1px solid rgba(56, 189, 248, 0.2)',
            fontSize: 11,
            color: '#cbd5e1',
            backdropFilter: 'blur(8px)',
            display: 'flex',
            alignItems: 'center',
            gap: 14,
            boxShadow: '0 4px 16px rgba(0,0,0,0.5)',
            pointerEvents: 'none',
          }}
        >
          <span style={{ color: '#38bdf8', fontWeight: 700 }}>
            🖐️ Giữ & Kéo thả bất kỳ node nào để chỉnh toạ độ
          </span>
          <span>🖱️ Kéo nền để trượt bản đồ</span>
          <span>🔍 Cuộn chuột để Zoom</span>
          <span style={{ color: '#34d399', fontWeight: 700 }}>
            📋 Bấm "Xuất Toạ Độ" để copy gửi AI
          </span>
        </div>
      </div>

      {/* ─── Layout Export Modal ─── */}
      <LayoutExportModal
        isOpen={isExportModalOpen}
        onClose={() => setIsExportModalOpen(false)}
        nodes={nodes}
        onResetDefault={handleResetDefaultLayout}
      />
    </div>
  );
};
