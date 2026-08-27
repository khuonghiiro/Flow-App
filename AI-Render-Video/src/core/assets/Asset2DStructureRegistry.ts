/**
 * Asset2DStructureRegistry.ts
 *
 * Dynamic 2D asset registry that reads asset_2d_structure.json from asset_2ds/
 * and auto-discovers new folders, mirroring the pattern of the 3D CharacterAssetRegistry
 * and MapAssetRegistry. Provides fetchAsset2DCategories() for any 2D panel to consume.
 */

// ─── Types ───────────────────────────────────────────────────────────

export interface Asset2DItem {
  id: string;
  name: string;
  /** Relative path from project root, e.g. "asset_2ds/nhan_vat/mat/eye_01.png" */
  path: string;
  /** Preview URL for thumbnails */
  previewUrl?: string;
  format: string;
  categoryId: string;
  subCategoryId?: string;
}

export interface Asset2DSubCategory {
  id: string;
  folder: string;
  label: string;
  icon: string;
  items: Asset2DItem[];
}

export interface Asset2DCategory {
  id: string;
  folder: string;
  label: string;
  icon: string;
  subcategories: Asset2DSubCategory[];
  /** Flat list of all items across all subcategories */
  allItems: Asset2DItem[];
}

/** Shape of a single category entry in asset_2d_structure.json */
interface RawCategoryConfig {
  id: string;
  folder: string;
  label: string;
  icon: string;
  subcategories?: Array<{
    id: string;
    folder: string;
    label: string;
    icon: string;
  }>;
}

/** Shape of the top-level asset_2d_structure.json */
interface RawAsset2DStructure {
  version: string;
  description?: string;
  settings?: {
    auto_discover_new_folders?: boolean;
    supported_image_formats?: string[];
    supported_audio_formats?: string[];
  };
  categories: RawCategoryConfig[];
  dictionary?: Record<string, { label: string; icon: string }>;
}

// ─── Constants ───────────────────────────────────────────────────────

const ASSET_2D_STRUCTURE_URL = '/asset_2ds/asset_2d_structure.json';
const ASSET_2D_MANIFEST_URL = '/asset_2ds/asset_2d_manifest.json';

// ─── Helpers ─────────────────────────────────────────────────────────

/**
 * Convert a snake_case folder name into a human-readable Vietnamese title.
 * Falls back to capitalised words if no dictionary match is found.
 */
export function formatAsset2DDisplayName(folderName: string): string {
  if (!folderName) return 'Tài Nguyên 2D';
  return folderName
    .replace(/^_+/, '')
    .replace(/_/g, ' ')
    .replace(/-/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

// ─── Main Fetcher ────────────────────────────────────────────────────

/**
 * Fetch and build dynamic 2D asset categories from asset_2d_structure.json.
 *
 * When `auto_discover_new_folders` is true (default), any folder that exists
 * in asset_2ds/ but is NOT listed in the JSON will be added automatically,
 * using the `dictionary` for nice labels or falling back to formatAsset2DDisplayName.
 *
 * Items are NOT populated here (no recursive file scanning from the browser).
 * Each component that consumes these categories should populate items lazily
 * through its own loading mechanism (e.g. API endpoint or manifest).
 */
export async function fetchAsset2DCategories(): Promise<Asset2DCategory[]> {
  try {
    const res = await fetch(`${ASSET_2D_STRUCTURE_URL}?t=${Date.now()}`);
    if (!res.ok) {
      console.warn(`[Asset2DStructureRegistry] Failed to fetch structure: ${res.status}`);
      return [];
    }

    const data: RawAsset2DStructure = await res.json();
    const dictionary = data.dictionary || {};

    // Try loading manifest for pre-scanned items
    let manifestCategories: Record<string, any> = {};
    try {
      const mRes = await fetch(`${ASSET_2D_MANIFEST_URL}?t=${Date.now()}`);
      if (mRes.ok) {
        const mData = await mRes.json();
        manifestCategories = mData.categories || {};
      }
    } catch { /* manifest not available — items will be empty */ }

    // Parse manifest items into typed Asset2DItem[]
    const parseItems = (rawList: any[]): Asset2DItem[] => {
      if (!Array.isArray(rawList)) return [];
      return rawList.map((item: any) => ({
        id: item.id || item.filename || '',
        name: item.name || formatAsset2DDisplayName(item.filename || ''),
        path: item.path || `asset_2ds/${item.relPath || ''}`,
        previewUrl: item.previewUrl,
        format: item.format || 'PNG',
        categoryId: item.categoryId || '',
        subCategoryId: item.subCategoryId,
      }));
    };

    // Build categories from the JSON definition
    const categories: Asset2DCategory[] = (data.categories || []).map((catConfig) => {
      const manifestCat = manifestCategories[catConfig.id];

      const subcategories: Asset2DSubCategory[] = (catConfig.subcategories || []).map((sub) => {
        // Load items from manifest if available
        const rawItems = manifestCat && !Array.isArray(manifestCat)
          ? manifestCat[sub.id] || []
          : [];

        return {
          id: sub.id,
          folder: sub.folder,
          label: sub.label,
          icon: sub.icon || '📦',
          items: parseItems(rawItems),
        };
      });

      // Flat items: either from _all key or the array itself
      let allItems: Asset2DItem[] = [];
      if (manifestCat) {
        if (Array.isArray(manifestCat)) {
          allItems = parseItems(manifestCat);
        } else if (manifestCat._all) {
          allItems = parseItems(manifestCat._all);
        } else {
          allItems = subcategories.flatMap((s) => s.items);
        }
      }

      return {
        id: catConfig.id,
        folder: catConfig.folder,
        label: catConfig.label,
        icon: catConfig.icon || '📦',
        subcategories,
        allItems,
      };
    });

    // Auto-discover: try fetching a directory listing API if available
    if (data.settings?.auto_discover_new_folders !== false) {
      try {
        const dirRes = await fetch(`/api/list-2d-folders?t=${Date.now()}`);
        if (dirRes.ok) {
          const folders: string[] = await dirRes.json();
          const knownIds = new Set(categories.map((c) => c.id));

          for (const folderName of folders) {
            if (knownIds.has(folderName)) continue;
            if (folderName.startsWith('.')) continue;

            const dictEntry = dictionary[folderName];
            const manifestItems = manifestCategories[folderName];
            categories.push({
              id: folderName,
              folder: folderName,
              label: dictEntry?.label || formatAsset2DDisplayName(folderName),
              icon: dictEntry?.icon || '📦',
              subcategories: [],
              allItems: Array.isArray(manifestItems) ? parseItems(manifestItems) : [],
            });
          }
        }
      } catch {
        // API not available — auto-discover silently skipped
      }
    }

    return categories;
  } catch (err) {
    console.warn('[Asset2DStructureRegistry] Error loading 2D asset structure:', err);
    return [];
  }
}

/**
 * Look up a category or folder name in the structure dictionary.
 * Returns { label, icon } if found, or generates from the folder name.
 */
export async function lookupAsset2DLabel(
  folderId: string
): Promise<{ label: string; icon: string }> {
  try {
    const res = await fetch(`${ASSET_2D_STRUCTURE_URL}?t=${Date.now()}`);
    if (res.ok) {
      const data: RawAsset2DStructure = await res.json();

      // Check categories first
      const cat = data.categories.find((c) => c.id === folderId || c.folder === folderId);
      if (cat) return { label: cat.label, icon: cat.icon };

      // Check subcategories
      for (const c of data.categories) {
        const sub = (c.subcategories || []).find(
          (s) => s.id === folderId || s.folder === folderId
        );
        if (sub) return { label: sub.label, icon: sub.icon };
      }

      // Check dictionary
      if (data.dictionary?.[folderId]) {
        return data.dictionary[folderId];
      }
    }
  } catch { /* ignore */ }

  return { label: formatAsset2DDisplayName(folderId), icon: '📦' };
}

/**
 * Live-scan a specific folder in asset_2ds/ via the dev server API.
 * Returns items from the filesystem in **real-time** — no need to run _scan_asset_2ds.bat.
 *
 * @param folderPath - Relative path inside asset_2ds/, e.g. "nhan_vat/mat" or "ban_do/hau_canh"
 * @returns Fresh list of Asset2DItem[] from the filesystem
 *
 * @example
 * // After saving a cropped eye image to asset_2ds/nhan_vat/mat/
 * const freshItems = await liveScanAsset2DFolder('nhan_vat/mat');
 * // freshItems includes the newly saved file immediately
 */
export async function liveScanAsset2DFolder(folderPath: string): Promise<Asset2DItem[]> {
  try {
    const res = await fetch(`/api/scan-2d-assets?folder=${encodeURIComponent(folderPath)}&t=${Date.now()}`);
    if (!res.ok) return [];

    const rawItems: any[] = await res.json();
    if (!Array.isArray(rawItems)) return [];

    return rawItems.map((item: any) => ({
      id: item.id || '',
      name: item.name || formatAsset2DDisplayName(item.filename || ''),
      path: item.path || `asset_2ds/${item.relPath || ''}`,
      previewUrl: item.previewUrl,
      format: item.format || 'PNG',
      categoryId: item.categoryId || folderPath.split('/')[0] || '',
      subCategoryId: item.subCategoryId,
    }));
  } catch (err) {
    console.warn(`[Asset2DStructureRegistry] Live scan error for "${folderPath}":`, err);
    return [];
  }
}
