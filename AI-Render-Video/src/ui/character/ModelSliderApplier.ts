import * as THREE from 'three';
import { FaceSliderConfig } from '../CharacterAssetRegistry';

/**
 * Apply facial sliders, skin smoothness, and wireframe settings to any Three.js 3D model hierarchy
 */
export function applySlidersToModelGroup(
  group: THREE.Object3D,
  sliders: FaceSliderConfig,
  showWireframe: boolean = false
): void {
  const { baseFaceOpacity, eyebrowOpacity, pupilOpacity, noseOpacity, mouthOpacity, skinSmoothness, costumeOpacity } = sliders;

  group.traverse((child) => {
    if ((child as THREE.Mesh).isMesh) {
      const mesh = child as THREE.Mesh;
      const name = mesh.name.toLowerCase();
      const parentName = (mesh.parent?.name || '').toLowerCase();
      const matName = Array.isArray(mesh.material)
        ? mesh.material.map((m) => m.name.toLowerCase()).join(' ')
        : (mesh.material?.name || '').toLowerCase();

      const isBaseFace =
        (name.includes('face') || parentName.includes('face') || matName.includes('face')) &&
        !name.includes('p0054') && !name.includes('p0052') && !matName.includes('p0054') && !matName.includes('p0052');

      const isEyebrow = name.includes('eyebrow') || parentName.includes('eyebrow') || matName.includes('eyebrow');
      const isPupil = name.includes('pupil') || parentName.includes('pupil') || matName.includes('pupil');
      const isNose = name.includes('nose') || parentName.includes('nose');
      const isMouth = name.includes('mouth') || parentName.includes('mouth') || name.includes('lip') || parentName.includes('lip');

      if (mesh.material) {
        const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
        mats.forEach((m: any) => {
          m.wireframe = showWireframe;
          m.side = THREE.FrontSide; // Backface culling prevents inner mouth/tongue bleeding through

          // Base Face / Mặt Cũ
          if (isBaseFace) {
            mesh.visible = baseFaceOpacity > 0.02;
            m.transparent = baseFaceOpacity < 0.98;
            m.opacity = baseFaceOpacity;
          }

          // Eyebrow Opacity
          if (isEyebrow) {
            mesh.visible = eyebrowOpacity > 0.02;
            m.transparent = eyebrowOpacity < 0.98;
            m.opacity = eyebrowOpacity;
          }

          // Pupil Opacity
          if (isPupil) {
            mesh.visible = pupilOpacity > 0.02;
            m.transparent = pupilOpacity < 0.98;
            m.opacity = pupilOpacity;
          }

          // Nose Opacity
          if (isNose) {
            mesh.visible = noseOpacity > 0.02;
            m.transparent = noseOpacity < 0.98;
            m.opacity = noseOpacity;
          }

          // Mouth & Lip Opacity
          if (isMouth) {
            mesh.visible = mouthOpacity > 0.02;
            m.transparent = mouthOpacity < 0.98;
            m.opacity = mouthOpacity;
          }

          // Skin Smoothness (Roughness)
          if (name.includes('body') || name.includes('face') || parentName.includes('face') || matName.includes('face')) {
            if (m.roughness !== undefined) {
              m.roughness = Math.max(0.1, 1.0 - skinSmoothness * 0.45);
            }
          }

          // Costume Opacity
          if (!name.includes('body') && !name.includes('face') && !isPupil && !isEyebrow && !isBaseFace && !isNose && !isMouth) {
            mesh.visible = costumeOpacity > 0.02;
            m.transparent = costumeOpacity < 0.98;
            m.opacity = costumeOpacity;
          }
        });
      }
    }
  });
}
