import * as THREE from 'three';

export interface SelectedPartInfo {
  mesh: THREE.Mesh;
  meshKey: string;
  displayName: string;
  categoryLabel: string;
  categoryIcon: string;
  initialColor: string;
  initialRoughness: number;
  initialMetalness: number;
  initialEmissive: string;
  initialEmissiveIntensity: number;
  initialWireframe: boolean;
  initialVisible: boolean;
}

export class Interactive3DPartSelector {
  private raycaster = new THREE.Raycaster();
  private mouse = new THREE.Vector2();
  private highlightHelper: THREE.BoxHelper | null = null;

  /**
   * Resolve a human-friendly Vietnamese display name and category for any mesh in the scene
   */
  public static getPartFriendlyInfo(mesh: THREE.Mesh): { displayName: string; categoryLabel: string; categoryIcon: string } {
    const rawName = (mesh.name || mesh.parent?.name || 'Mesh_Part').trim();
    const lower = rawName.toLowerCase();

    // 1. Tops / Costumes
    if (lower.includes('top') || lower.includes('shirt') || lower.includes('jacket') || lower.includes('coat') || lower.includes('ao_') || lower.includes('ao')) {
      return { displayName: `Áo Trang Phục (${rawName})`, categoryLabel: 'Trang Phục', categoryIcon: '👘' };
    }
    // 2. Bottoms / Skirts / Pants
    if (lower.includes('bottom') || lower.includes('pant') || lower.includes('skirt') || lower.includes('short') || lower.includes('quan_') || lower.includes('quan') || lower.includes('vay')) {
      return { displayName: `Quần / Váy (${rawName})`, categoryLabel: 'Trang Phục', categoryIcon: '🩳' };
    }
    // 3. Shoes / Boots
    if (lower.includes('shoe') || lower.includes('boot') || lower.includes('sneaker') || lower.includes('foot') || lower.includes('giay')) {
      return { displayName: `Giày Dép (${rawName})`, categoryLabel: 'Giày Dép', categoryIcon: '👟' };
    }
    // 4. Leg Accessories / Socks
    if (lower.includes('legacc') || lower.includes('sock') || lower.includes('stocking') || lower.includes('tat') || lower.includes('vo')) {
      return { displayName: `Tất / Vớ Chân (${rawName})`, categoryLabel: 'Phụ Kiện Chân', categoryIcon: '🧦' };
    }
    // 5. Hair
    if (lower.includes('hair') || lower.includes('toc')) {
      return { displayName: `Kiểu Tóc (${rawName})`, categoryLabel: 'Kiểu Tóc', categoryIcon: '💇' };
    }
    // 6. Face / Head
    if (lower.includes('face') || lower.includes('head') || lower.includes('mat') || lower.includes('pupil') || lower.includes('eye') || lower.includes('brow') || lower.includes('mouth')) {
      return { displayName: `Khuôn Mặt / Mắt (${rawName})`, categoryLabel: 'Khuôn Mặt', categoryIcon: '🎭' };
    }
    // 7. Base Body
    if (lower.includes('body') || lower.includes('skin') || lower.includes('than') || lower.includes('base')) {
      return { displayName: `Thân Cơ Bản (${rawName})`, categoryLabel: 'Thân Cơ Bản', categoryIcon: '🧍' };
    }
    // 8. Wings
    if (lower.includes('wing') || lower.includes('canh')) {
      return { displayName: `Đôi Cánh (${rawName})`, categoryLabel: 'Đôi Cánh', categoryIcon: '🪶' };
    }
    // 9. Tail
    if (lower.includes('tail') || lower.includes('duoi')) {
      return { displayName: `Đuôi Thú (${rawName})`, categoryLabel: 'Đuôi Thú', categoryIcon: '🦊' };
    }
    // 10. Hat
    if (lower.includes('hat') || lower.includes('cap') || lower.includes('helmet') || lower.includes('mu') || lower.includes('non')) {
      return { displayName: `Mũ & Nón (${rawName})`, categoryLabel: 'Mũ & Nón', categoryIcon: '🎩' };
    }

    return { displayName: `Chi Tiết: ${rawName}`, categoryLabel: 'Bộ Phận 3D', categoryIcon: '✨' };
  }

  /**
   * Raycast from mouse pointer on the canvas to find clicked mesh
   */
  public hitTest(
    event: MouseEvent | React.MouseEvent,
    container: HTMLElement,
    camera: THREE.Camera,
    scene: THREE.Scene
  ): SelectedPartInfo | null {
    const rect = container.getBoundingClientRect();
    this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    this.raycaster.setFromCamera(this.mouse, camera);

    const hits = this.raycaster
      .intersectObjects(scene.children, true)
      .filter((h) => {
        const obj = h.object as any;
        return (
          obj.isMesh &&
          obj.visible &&
          !obj.isGridHelper &&
          !obj.isLine &&
          !obj.isBoxHelper &&
          obj !== this.highlightHelper
        );
      });

    if (hits.length === 0) return null;

    const hitMesh = hits[0].object as THREE.Mesh;
    const meshKey = hitMesh.name || hitMesh.parent?.name || `Part_${hitMesh.id}`;
    const friendly = Interactive3DPartSelector.getPartFriendlyInfo(hitMesh);

    // Read current material values
    let currentColor = '#ffffff';
    let currentRoughness = 0.55;
    let currentMetalness = 0.05;
    let currentEmissive = '#000000';
    let currentEmissiveIntensity = 0.0;
    let currentWireframe = false;

    if (hitMesh.material) {
      const mat = (Array.isArray(hitMesh.material) ? hitMesh.material[0] : hitMesh.material) as any;
      if (mat.color) {
        currentColor = `#${mat.color.getHexString()}`;
      }
      if (mat.roughness !== undefined) currentRoughness = mat.roughness;
      if (mat.metalness !== undefined) currentMetalness = mat.metalness;
      if (mat.emissive) {
        currentEmissive = `#${mat.emissive.getHexString()}`;
        currentEmissiveIntensity = mat.emissiveIntensity || 0;
      }
      if (mat.wireframe !== undefined) currentWireframe = mat.wireframe;
    }

    return {
      mesh: hitMesh,
      meshKey,
      displayName: friendly.displayName,
      categoryLabel: friendly.categoryLabel,
      categoryIcon: friendly.categoryIcon,
      initialColor: currentColor,
      initialRoughness: currentRoughness,
      initialMetalness: currentMetalness,
      initialEmissive: currentEmissive,
      initialEmissiveIntensity: currentEmissiveIntensity,
      initialWireframe: currentWireframe,
      initialVisible: hitMesh.visible,
    };
  }

  /**
   * Attach visual selection bounding box highlight to selected mesh
   */
  public attachHighlight(mesh: THREE.Mesh | null, scene: THREE.Scene): void {
    this.removeHighlight(scene);
    if (!mesh) return;

    this.highlightHelper = new THREE.BoxHelper(mesh, 0x00f0ff);
    (this.highlightHelper as any).isBoxHelper = true;
    this.highlightHelper.renderOrder = 999;
    scene.add(this.highlightHelper);
  }

  /**
   * Remove visual highlight box from scene
   */
  public removeHighlight(scene: THREE.Scene): void {
    if (this.highlightHelper) {
      scene.remove(this.highlightHelper);
      this.highlightHelper.geometry.dispose();
      (this.highlightHelper.material as THREE.Material).dispose();
      this.highlightHelper = null;
    }
  }

  /**
   * Update highlight helper if mesh transforms/changes
   */
  public updateHighlight(): void {
    if (this.highlightHelper) {
      this.highlightHelper.update();
    }
  }
}
