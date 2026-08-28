import * as THREE from 'three';
import { PartMaterialCustomization } from '../../types/scene';

export interface ColorPresetPalette {
  id: string;
  name: string;
  category: 'natural' | 'cyberpunk' | 'luxury' | 'tactical';
  color: string;
  roughness: number;
  metalness: number;
  emissive?: string;
  emissiveIntensity?: number;
  previewGradient: string;
  description: string;
}

export const PRESET_COLOR_PALETTES: ColorPresetPalette[] = [
  {
    id: 'original',
    name: 'Màu Gốc Ban Đầu',
    category: 'natural',
    color: '#ffffff',
    roughness: 0.6,
    metalness: 0.05,
    emissive: '#000000',
    emissiveIntensity: 0.0,
    previewGradient: 'linear-gradient(135deg, #ffffff, #94a3b8)',
    description: 'Giữ nguyên 100% màu sắc và họa tiết gốc của texture',
  },
  {
    id: 'stealth_black',
    name: 'Đen Nhám Đặc Nhiệm (Tactical Black)',
    category: 'tactical',
    color: '#22252a',
    roughness: 0.75,
    metalness: 0.1,
    emissive: '#000000',
    emissiveIntensity: 0.0,
    previewGradient: 'linear-gradient(135deg, #334155, #0f172a)',
    description: 'Vải dù / da đen nhám chống lóa cao cấp',
  },
  {
    id: 'crimson_silk',
    name: 'Đỏ Ruby Huyết Sắc (Crimson Ruby)',
    category: 'luxury',
    color: '#e11d48',
    roughness: 0.5,
    metalness: 0.15,
    emissive: '#be123c',
    emissiveIntensity: 0.2,
    previewGradient: 'linear-gradient(135deg, #f43f5e, #881337)',
    description: 'Sắc đỏ quyền quý với ánh phản xạ mềm mại',
  },
  {
    id: 'midnight_navy',
    name: 'Xanh Lam Hoàng Gia (Royal Navy)',
    category: 'luxury',
    color: '#2563eb',
    roughness: 0.55,
    metalness: 0.2,
    emissive: '#1d4ed8',
    emissiveIntensity: 0.2,
    previewGradient: 'linear-gradient(135deg, #60a5fa, #1e3a8a)',
    description: 'Xanh dương đậm sâu lắng, sang trọng',
  },
  {
    id: 'emerald_silk',
    name: 'Ngọc Bích Hoàng Gia (Emerald Jade)',
    category: 'luxury',
    color: '#059669',
    roughness: 0.5,
    metalness: 0.2,
    emissive: '#047857',
    emissiveIntensity: 0.2,
    previewGradient: 'linear-gradient(135deg, #34d399, #064e3b)',
    description: 'Tông xanh lục bảo quý phái',
  },
  {
    id: 'royal_gold',
    name: 'Vàng Kim Hoàng Kim (Imperial Gold)',
    category: 'luxury',
    color: '#f59e0b',
    roughness: 0.25,
    metalness: 0.85,
    emissive: '#d97706',
    emissiveIntensity: 0.25,
    previewGradient: 'linear-gradient(135deg, #fde68a, #b45309)',
    description: 'Ánh kim loại vàng bóng bẩy',
  },
  {
    id: 'cyber_cyan_neon',
    name: 'Cyberpunk Cyan Neon',
    category: 'cyberpunk',
    color: '#00f0ff',
    roughness: 0.3,
    metalness: 0.4,
    emissive: '#00f0ff',
    emissiveIntensity: 1.2,
    previewGradient: 'linear-gradient(135deg, #00f0ff, #0369a1)',
    description: 'Đèn neon xanh Cyan phát sáng viễn tưởng',
  },
  {
    id: 'cyber_magenta_neon',
    name: 'Cyberpunk Magenta Glow',
    category: 'cyberpunk',
    color: '#ff007f',
    roughness: 0.3,
    metalness: 0.35,
    emissive: '#ff007f',
    emissiveIntensity: 1.2,
    previewGradient: 'linear-gradient(135deg, #ff007f, #9d174d)',
    description: 'Hiệu ứng phát sáng hồng neon Cyberpunk',
  },
  {
    id: 'sakura_pink',
    name: 'Hồng Phấn Sakura (Sakura Pastel)',
    category: 'natural',
    color: '#fda4af',
    roughness: 0.65,
    metalness: 0.05,
    emissive: '#000000',
    emissiveIntensity: 0.0,
    previewGradient: 'linear-gradient(135deg, #ffe4e6, #fb7185)',
    description: 'Tông màu anime nhẹ nhàng, ngọt ngào',
  },
  {
    id: 'snow_white',
    name: 'Trắng Sứ Tinh Khôi (Pure White)',
    category: 'natural',
    color: '#f8fafc',
    roughness: 0.55,
    metalness: 0.05,
    emissive: '#000000',
    emissiveIntensity: 0.0,
    previewGradient: 'linear-gradient(135deg, #ffffff, #cbd5e1)',
    description: 'Trắng tinh khôi với độ bóng nhẹ',
  },
];

/**
 * Material Override Engine: Applies real-time color tints, roughness, metalness, and emissive glow
 * to any Three.js 3D character mesh hierarchy without destroying original textures.
 */
export class MaterialOverrideEngine {
  /**
   * Apply material customization overrides to all matching meshes in a Three.js scene/group
   */
  public static applyMaterialOverrides(
    root: THREE.Object3D | null,
    overrides?: Record<string, PartMaterialCustomization>
  ): void {
    if (!root) return;
    const safeOverrides = overrides || {};

    root.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mesh = child as THREE.Mesh;
        const meshKey = mesh.name;
        const parentKey = mesh.parent?.name || '';
        const customPartKey = (mesh.userData?.partKey || '') as string;

        // Check if there is an explicit override for this specific mesh, parent, or partKey
        const override =
          safeOverrides[meshKey] ||
          safeOverrides[parentKey] ||
          (customPartKey ? safeOverrides[customPartKey] : undefined) ||
          this.findMatchingOverride(mesh, safeOverrides);

        if (!override) {
          // Restore default clean material properties if no override exists
          this.resetMeshMaterialToDefault(mesh);
          return;
        }

        // Apply visibility
        if (override.visible !== undefined) {
          mesh.visible = override.visible;
        }

        if (!mesh.material) return;
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];

        mats.forEach((mat) => {
          const m = mat as any;

          // Apply color tint (multiplies with base texture map)
          if (override.color) {
            m.color = m.color || new THREE.Color();
            m.color.set(override.color);
          }

          // Apply PBR Roughness & Metalness
          if (override.roughness !== undefined && m.roughness !== undefined) {
            m.roughness = Math.max(0.0, Math.min(1.0, override.roughness));
          }
          if (override.metalness !== undefined && m.metalness !== undefined) {
            m.metalness = Math.max(0.0, Math.min(1.0, override.metalness));
          }

          // Apply Emissive Glow
          if (override.emissive !== undefined) {
            m.emissive = m.emissive || new THREE.Color();
            m.emissive.set(override.emissive);
            m.emissiveIntensity =
              override.emissiveIntensity !== undefined ? override.emissiveIntensity : (override.emissive === '#000000' ? 0.0 : 1.0);
          }

          // Apply Wireframe
          if (override.wireframe !== undefined) {
            m.wireframe = override.wireframe;
          }

          // Apply Opacity
          if (override.opacity !== undefined) {
            m.opacity = Math.max(0.0, Math.min(1.0, override.opacity));
            m.transparent = m.opacity < 1.0;
          }

          m.needsUpdate = true;
        });
      }
    });
  }

  /**
   * Reset a specific mesh to default clean material settings
   */
  public static resetMeshMaterialToDefault(mesh: THREE.Mesh): void {
    if (!mesh.material) return;
    const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    mats.forEach((mat) => {
      const m = mat as any;
      if (m.color) {
        m.color.setHex(0xffffff);
      }
      if (m.roughness !== undefined) m.roughness = 0.6;
      if (m.metalness !== undefined) m.metalness = 0.05;
      if (m.emissive) {
        m.emissive.setHex(0x000000);
        m.emissiveIntensity = 0.0;
      }
      if (m.wireframe !== undefined) m.wireframe = false;
      if (m.opacity !== undefined) {
        m.opacity = 1.0;
        m.transparent = false;
      }
      m.needsUpdate = true;
    });
    mesh.visible = true;
  }

  /**
   * Fuzzy find matching override key from mesh name or material name
   */
  private static findMatchingOverride(
    mesh: THREE.Mesh,
    overrides: Record<string, PartMaterialCustomization>
  ): PartMaterialCustomization | undefined {
    const meshLower = mesh.name.toLowerCase();
    for (const key of Object.keys(overrides)) {
      const keyLower = key.toLowerCase();
      if (
        meshLower === keyLower ||
        meshLower.includes(keyLower) ||
        keyLower.includes(meshLower)
      ) {
        return overrides[key];
      }
    }
    return undefined;
  }
}
