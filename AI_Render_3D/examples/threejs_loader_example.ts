/**
 * TypeScript integration example for Studio Three.js pipeline.
 * Demonstrates loading rigged .glb assets produced by image-to-rig-pipeline,
 * verifying bone hierarchy, and applying procedural animations.
 */

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

export interface RiggedCharacterAsset {
  scene: THREE.Group;
  skinnedMesh: THREE.SkinnedMesh | null;
  skeleton: THREE.Skeleton | null;
  bones: Map<string, THREE.Bone>;
  mixer: THREE.AnimationMixer;
}

export interface AssetMetadata {
  model_id: string;
  bone_count: number;
  bone_names: string[];
  has_skinning: boolean;
  file_size_bytes: number;
  processing_time_seconds: number;
}

/**
 * Load a rigged character .glb asset exported from the Python pipeline.
 */
export async function loadRiggedCharacter(
  glbUrl: string,
  onProgress?: (progress: number) => void
): Promise<RiggedCharacterAsset> {
  const loader = new GLTFLoader();

  return new Promise((resolve, reject) => {
    loader.load(
      glbUrl,
      (gltf) => {
        let skinnedMesh: THREE.SkinnedMesh | null = null;
        let skeleton: THREE.Skeleton | null = null;
        const bones = new Map<string, THREE.Bone>();

        gltf.scene.traverse((child) => {
          if ((child as THREE.SkinnedMesh).isSkinnedMesh) {
            skinnedMesh = child as THREE.SkinnedMesh;
            skeleton = skinnedMesh.skeleton;
          }
          if ((child as THREE.Bone).isBone) {
            bones.set(child.name, child as THREE.Bone);
          }
        });

        const mixer = new THREE.AnimationMixer(gltf.scene);

        console.log(`[AssetLoader] Loaded character '${glbUrl}' successfully!`);
        console.log(`[AssetLoader] Bones identified: ${bones.size}`, Array.from(bones.keys()));

        resolve({
          scene: gltf.scene,
          skinnedMesh,
          skeleton,
          bones,
          mixer,
        });
      },
      (xhr) => {
        if (onProgress && xhr.total > 0) {
          onProgress(xhr.loaded / xhr.total);
        }
      },
      (error) => {
        console.error('[AssetLoader] Failed to load character:', error);
        reject(error);
      }
    );
  });
}

/**
 * Procedural humanoid waving animation test.
 */
export function playWaveAnimation(character: RiggedCharacterAsset, time: number): void {
  const rightUpperArm = character.bones.get('RightUpperArm');
  const rightLowerArm = character.bones.get('RightLowerArm');

  if (rightUpperArm && rightLowerArm) {
    rightUpperArm.rotation.z = Math.PI / 3 + Math.sin(time * 3) * 0.2;
    rightLowerArm.rotation.y = Math.sin(time * 6) * 0.5;
  }
}
