import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';

export interface ExportModelOptions {
  filename?: string;
  binary?: boolean;
  includeAnimations?: boolean;
  embedImages?: boolean;
}

/**
 * CharacterModelExporter
 * Xuất mô hình 3D nhân vật đã gắn xương sang định dạng chuẩn .glb / .gltf
 * Tương thích 100% khi nhập vào Blender, Unreal Engine 5, Unity, Maya, Mixamo.
 */
export class CharacterModelExporter {
  public static async exportToGLB(
    object3D: THREE.Object3D,
    options: ExportModelOptions = {}
  ): Promise<{ blob: Blob; filename: string }> {
    const {
      filename = 'character_rigged',
      binary = true,
      includeAnimations = true,
      embedImages = true,
    } = options;

    return new Promise((resolve, reject) => {
      const exporter = new GLTFExporter();

      // Cập nhật ma trận thế giới trước khi đóng gói
      object3D.updateMatrixWorld(true);

      const exportOptions: any = {
        binary,
        embedImages,
        animations: includeAnimations ? (object3D as any).animations || [] : [],
        forceIndices: true,
        truncateDrawRange: false,
      };

      exporter.parse(
        object3D,
        (gltf) => {
          let blob: Blob;
          const cleanName = filename.replace(/\.[^/.]+$/, '');
          const finalFilename = binary ? `${cleanName}.glb` : `${cleanName}.gltf`;

          if (gltf instanceof ArrayBuffer) {
            blob = new Blob([gltf], { type: 'model/gltf-binary' });
          } else {
            const output = JSON.stringify(gltf, null, 2);
            blob = new Blob([output], { type: 'application/json' });
          }

          resolve({ blob, filename: finalFilename });
        },
        (error) => {
          console.error('[CharacterModelExporter] Lỗi khi xuất mô hình 3D:', error);
          reject(error);
        },
        exportOptions
      );
    });
  }

  /**
   * Tự động kích hoạt tải file về máy tính người dùng
   */
  public static downloadBlob(blob: Blob, filename: string): void {
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 4000);
  }
}
