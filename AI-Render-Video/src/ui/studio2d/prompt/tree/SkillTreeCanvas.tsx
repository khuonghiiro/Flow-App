import React, { useState, useRef, useMemo, useCallback, useEffect } from 'react';
import { SkillTreeNode, SkillBranchCategory } from './types';
import { SKILL_TREE_NODES, SKILL_TREE_LINKS } from './skillTreeData';
import { SkillTreeNodeComponent } from './SkillTreeNodeComponent';
import { SkillTreeControlBar } from './SkillTreeControlBar';
import { LayoutExportModal } from './LayoutExportModal';
import { SkillTreeLegend } from './SkillTreeLegend';
import { SkillTreeLinksLayer } from './SkillTreeLinksLayer';
import { getDescendantNodeIds } from './treeHierarchyUtils';

interface SkillTreeCanvasProps {
  selectedPromptId: string;
  onSelectPrompt: (promptId: string) => void;
}

const STORAGE_KEY = 'studio2d_skill_tree_custom_layout_v13';
const LINE_STYLE_STORAGE_KEY = 'studio2d_skill_tree_line_style';

export const SkillTreeCanvas: React.FC<SkillTreeCanvasProps> = ({
  selectedPromptId,
  onSelectPrompt,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  // ─── Line Style State: 'orthogonal' (default) vs 'curved' ───
  const [lineStyle, setLineStyle] = useState<'orthogonal' | 'curved'>(() => {
    return (localStorage.getItem(LINE_STYLE_STORAGE_KEY) as 'orthogonal' | 'curved') || 'orthogonal';
  });

  const handleToggleLineStyle = () => {
    setLineStyle((prev) => {
      const next = prev === 'orthogonal' ? 'curved' : 'orthogonal';
      localStorage.setItem(LINE_STYLE_STORAGE_KEY, next);
      return next;
    });
  };

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

  // Pan & Zoom state: Allow zoom down to 5% (0.05) with comfortable panoramic default
  const [zoom, setZoom] = useState<number>(0.20);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 800, y: 120 });
  const [isDraggingCanvas, setIsDraggingCanvas] = useState<boolean>(false);
  const [dragStart, setDragStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // Node Dragging State
  const [isEditMode, setIsEditMode] = useState<boolean>(true);
  const [draggingNodeId, setDraggingNodeId] = useState<string | null>(null);
  const [dragMoved, setDragMoved] = useState<boolean>(false);
  const [isAltKeyPressed, setIsAltKeyPressed] = useState<boolean>(false);
  const [isAltDragging, setIsAltDragging] = useState<boolean>(false);
  const [dragBranchNodeIds, setDragBranchNodeIds] = useState<Set<string>>(new Set());

  const dragClientStartRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });
  const dragStartWorldRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });
  const dragInitialPositionsRef = useRef<Map<string, { x: number; y: number }>>(new Map());
  const dragBranchIdsRef = useRef<Set<string>>(new Set());

  // Listen for global Alt key state to provide instant visual feedback
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Alt') {
        setIsAltKeyPressed(true);
      }
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.key === 'Alt') {
        setIsAltKeyPressed(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

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

  // ─── Drag & Drop Handlers for Nodes (Supports Alt + Drag to move full subtree) ───
  const handleNodeMouseDown = (node: SkillTreeNode, e: React.MouseEvent) => {
    if (!isEditMode || e.button !== 0) return;
    e.stopPropagation();

    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;

    dragClientStartRef.current = { x: e.clientX, y: e.clientY };
    const worldX = (e.clientX - rect.left - pan.x) / zoom;
    const worldY = (e.clientY - rect.top - pan.y) / zoom;
    dragStartWorldRef.current = { x: worldX, y: worldY };

    // Record initial snapshot positions of all nodes in graph
    const initialMap = new Map<string, { x: number; y: number }>();
    nodes.forEach((n) => initialMap.set(n.id, { x: n.x, y: n.y }));
    dragInitialPositionsRef.current = initialMap;

    const isAlt = e.altKey || isAltKeyPressed;
    setIsAltDragging(isAlt);

    if (isAlt) {
      // Collect all descendant child nodes recursively
      const descendants = getDescendantNodeIds(node.id, nodes, SKILL_TREE_LINKS);
      descendants.add(node.id);
      dragBranchIdsRef.current = descendants;
      setDragBranchNodeIds(descendants);
    } else {
      const single = new Set([node.id]);
      dragBranchIdsRef.current = single;
      setDragBranchNodeIds(single);
    }

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
    // 1. Moving a Node (or Node + Subtree if Alt is held)
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

      const deltaX = Math.round(worldX - dragStartWorldRef.current.x);
      const deltaY = Math.round(worldY - dragStartWorldRef.current.y);

      // Support dynamically holding Alt during drag
      const isAltNow = e.altKey || isAltKeyPressed;
      if (isAltNow && !isAltDragging) {
        setIsAltDragging(true);
        const descendants = getDescendantNodeIds(draggingNodeId, nodes, SKILL_TREE_LINKS);
        descendants.add(draggingNodeId);
        dragBranchIdsRef.current = descendants;
        setDragBranchNodeIds(descendants);
      }

      const activeIds = dragBranchIdsRef.current;
      const initialMap = dragInitialPositionsRef.current;

      setNodes((prev) =>
        prev.map((n) => {
          if (activeIds.has(n.id)) {
            const init = initialMap.get(n.id);
            if (init) {
              return { ...n, x: init.x + deltaX, y: init.y + deltaY };
            }
          }
          return n;
        })
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
      setIsAltDragging(false);
      dragBranchIdsRef.current = new Set();
      setDragBranchNodeIds(new Set());
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
    const newZoom = Math.max(0.05, Math.min(2.5, zoom * zoomFactor));

    const worldX = (mouseX - pan.x) / zoom;
    const worldY = (mouseY - pan.y) / zoom;

    const newPanX = mouseX - worldX * newZoom;
    const newPanY = mouseY - worldY * newZoom;

    setZoom(newZoom);
    setPan({ x: newPanX, y: newPanY });
  };

  // Zoom helpers: Allow zooming down to 5% (0.05)
  const handleZoomIn = () => setZoom((z) => Math.min(2.5, +(z + 0.05).toFixed(2)));
  const handleZoomOut = () => setZoom((z) => Math.max(0.05, +(z - 0.05).toFixed(2)));
  const handleResetZoom = () => {
    setZoom(0.20);
    setPan({ x: 800, y: 120 });
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
        setPan({ x: 800, y: 120 });
        setZoom(0.20);
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
        lineStyle={lineStyle}
        onToggleLineStyle={handleToggleLineStyle}
        isAltPressed={isAltKeyPressed || isAltDragging}
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
          {/* ─── SVG Connecting Lines Layer (Dynamic Bézier or Orthogonal) ─── */}
          <SkillTreeLinksLayer
            links={SKILL_TREE_LINKS}
            nodeMap={nodeMap}
            activePathSet={activePathSet}
            lineStyle={lineStyle}
            isLinkDimmed={isLinkDimmed}
          />

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
                  isDragging={isNodeDragging || (isAltDragging && dragBranchNodeIds.has(node.id))}
                  isEditMode={isEditMode}
                  onSelect={handleNodeClick}
                  onMouseDownNode={handleNodeMouseDown}
                />
              </div>
            );
          })}
        </div>

        {/* ─── Interactive Helper Legend Overlay ─── */}
        <SkillTreeLegend
          isEditMode={isEditMode}
          isAltPressed={isAltKeyPressed || isAltDragging}
        />
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
