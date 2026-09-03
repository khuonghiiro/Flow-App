import { SkillTreeNode, SkillTreeLink } from './types';

/**
 * Traverses the skill tree graph downwards from rootId to collect all descendant node IDs.
 * Uses both explicit SkillTreeLink connections (fromId -> toId) and node.parentId relationships.
 */
export function getDescendantNodeIds(
  rootId: string,
  allNodes: SkillTreeNode[],
  allLinks: SkillTreeLink[]
): Set<string> {
  const descendants = new Set<string>();
  const queue: string[] = [rootId];

  // Adjacency list: parentId -> array of childIds
  const childrenMap = new Map<string, string[]>();

  // 1. From links
  for (const link of allLinks) {
    const list = childrenMap.get(link.fromId) || [];
    if (!list.includes(link.toId)) {
      list.push(link.toId);
    }
    childrenMap.set(link.fromId, list);
  }

  // 2. From node.parentId
  for (const node of allNodes) {
    if (node.parentId) {
      const list = childrenMap.get(node.parentId) || [];
      if (!list.includes(node.id)) {
        list.push(node.id);
      }
      childrenMap.set(node.parentId, list);
    }
  }

  while (queue.length > 0) {
    const current = queue.shift()!;
    const children = childrenMap.get(current) || [];
    for (const childId of children) {
      if (!descendants.has(childId) && childId !== rootId) {
        descendants.add(childId);
        queue.push(childId);
      }
    }
  }

  return descendants;
}
