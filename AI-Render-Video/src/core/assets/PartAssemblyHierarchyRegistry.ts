/**
 * PartAssemblyHierarchyRegistry.ts
 *
 * Reads part_assembly_hierarchy.json and provides lookup utilities
 * for composite → sub-parts relationships. Integrates with
 * liveScanAsset2DFolder() for real-time asset loading.
 */

import type { Character2DPartType } from '../../types/scene2d';
import { liveScanAsset2DFolder, type Asset2DItem } from './Asset2DStructureRegistry';

// ─── Types ───────────────────────────────────────────────────────

export interface SubPartDef {
  part_type: Character2DPartType;
  label: string;
  icon: string;
  folder: string;
  required: boolean;
  description: string;
}

export interface CompositePartDef {
  id: string;
  label: string;
  icon: string;
  /** Which equip slots trigger this composite's drill-down */
  trigger_slots: Character2DPartType[];
  /** Order to assemble sub-parts (bottom layer first) */
  assembly_order: Character2DPartType[];
  sub_parts: SubPartDef[];
}

interface RawHierarchyData {
  version: string;
  description?: string;
  composites: CompositePartDef[];
}

// ─── Constants ───────────────────────────────────────────────────

const HIERARCHY_URL = '/asset_2ds/part_assembly_hierarchy.json';

// ─── Cache ───────────────────────────────────────────────────────

let _cachedHierarchy: CompositePartDef[] | null = null;
let _cacheTimestamp = 0;
const CACHE_TTL_MS = 30_000; // 30s cache

// ─── Main Fetcher ────────────────────────────────────────────────

/**
 * Fetch the part assembly hierarchy from JSON config.
 * Results are cached for 30s to avoid repeated fetches.
 */
export async function fetchPartHierarchy(): Promise<CompositePartDef[]> {
  const now = Date.now();
  if (_cachedHierarchy && now - _cacheTimestamp < CACHE_TTL_MS) {
    return _cachedHierarchy;
  }

  try {
    const res = await fetch(`${HIERARCHY_URL}?t=${now}`);
    if (!res.ok) {
      console.warn(`[PartHierarchy] Failed to fetch: ${res.status}`);
      return _cachedHierarchy || [];
    }

    const data: RawHierarchyData = await res.json();
    _cachedHierarchy = data.composites || [];
    _cacheTimestamp = now;
    return _cachedHierarchy;
  } catch (err) {
    console.warn('[PartHierarchy] Error loading hierarchy:', err);
    return _cachedHierarchy || [];
  }
}

// ─── Lookup Utilities ────────────────────────────────────────────

/**
 * Find the composite definition that is triggered by a given equip slot.
 * Returns null if the slot has no composite (is a simple slot).
 */
export async function getCompositeForSlot(
  slot: Character2DPartType
): Promise<CompositePartDef | null> {
  const hierarchy = await fetchPartHierarchy();
  return hierarchy.find((c) => c.trigger_slots.includes(slot)) || null;
}

/**
 * Synchronous version — uses cache only. Returns null if cache is empty.
 * Use this in render functions where async is not possible.
 */
export function getCompositeForSlotSync(
  slot: Character2DPartType
): CompositePartDef | null {
  if (!_cachedHierarchy) return null;
  return _cachedHierarchy.find((c) => c.trigger_slots.includes(slot)) || null;
}

/**
 * Check if a slot has a composite (is drill-downable).
 * Synchronous, requires cache to be populated first.
 */
export function slotHasComposite(slot: Character2DPartType): boolean {
  return getCompositeForSlotSync(slot) !== null;
}

/**
 * Get all composite definitions (cached).
 */
export function getAllCompositesSync(): CompositePartDef[] {
  return _cachedHierarchy || [];
}

// ─── Live Asset Loading ──────────────────────────────────────────

/**
 * Load available assets for a specific sub-part from the filesystem.
 * Uses liveScanAsset2DFolder() for real-time results.
 *
 * @param subPart - The sub-part definition with its folder path
 * @returns Array of available asset items for this sub-part
 */
export async function loadSubPartAssets(
  subPart: SubPartDef
): Promise<Asset2DItem[]> {
  if (!subPart.folder) return [];
  return liveScanAsset2DFolder(subPart.folder);
}

/**
 * Load assets for all sub-parts of a composite in parallel.
 * Returns a map of part_type → Asset2DItem[].
 */
export async function loadCompositeAssets(
  composite: CompositePartDef
): Promise<Record<string, Asset2DItem[]>> {
  const results: Record<string, Asset2DItem[]> = {};
  const promises = composite.sub_parts.map(async (subPart) => {
    const items = await loadSubPartAssets(subPart);
    results[subPart.part_type] = items;
  });
  await Promise.all(promises);
  return results;
}

/**
 * Pre-warm the hierarchy cache. Call this on app startup or when
 * the 2D assembler tab opens to avoid loading delay on first click.
 */
export async function preloadPartHierarchy(): Promise<void> {
  await fetchPartHierarchy();
}
