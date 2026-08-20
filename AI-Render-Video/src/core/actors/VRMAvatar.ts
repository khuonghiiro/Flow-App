import * as THREE from 'three';
import { ActorConfig, CharacterAssembly } from '../../types/scene';
import { SocketAttacher } from '../assets/SocketAttacher';
import { AssetLoaderRegistry } from '../assets/AssetLoaderRegistry';

export class VRMAvatar {
  public config: ActorConfig;
  public rootObject: THREE.Group;
  public proceduralGroup: THREE.Group;
  public modularGroup: THREE.Group;
  public headBone: THREE.Object3D;
  public neckBone: THREE.Object3D;
  public spineBone: THREE.Object3D;
  public leftArm: THREE.Object3D;
  public leftElbow: THREE.Object3D;
  public rightArm: THREE.Object3D;
  public rightElbow: THREE.Object3D;
  public leftLeg: THREE.Object3D;
  public leftKnee: THREE.Object3D;
  public rightLeg: THREE.Object3D;
  public rightKnee: THREE.Object3D;
  public leftFoot: THREE.Object3D;
  public rightFoot: THREE.Object3D;
  public mouthMesh: THREE.Mesh;
  public eyeLMesh: THREE.Mesh;
  public eyeRMesh: THREE.Mesh;
  public eyebrowL: THREE.Mesh;
  public eyebrowR: THREE.Mesh;
  public weaponSocketR: THREE.Object3D;
  public weaponSocketL: THREE.Object3D;

  constructor(config: ActorConfig) {
    this.config = config;
    this.rootObject = new THREE.Group();
    this.rootObject.name = `Actor_${config.id}`;
    this.rootObject.position.set(...config.spawn_point);
    if (config.rotation_y !== undefined) {
      this.rootObject.rotation.y = config.rotation_y;
    }

    this.proceduralGroup = new THREE.Group();
    this.proceduralGroup.name = 'procedural_body_group';
    this.rootObject.add(this.proceduralGroup);

    this.modularGroup = new THREE.Group();
    this.modularGroup.name = 'modular_character_group';
    this.rootObject.add(this.modularGroup);

    const isKnight = config.id.includes('warrior') || config.model.includes('knight');
    const isAnime = config.id.includes('anime') || config.name.includes('Tiểu Vũ') || config.model.includes('sample_avatar') || config.id.includes('girl');

    // Hierarchy: Root -> Spine -> Neck -> Head
    this.spineBone = new THREE.Group();
    this.spineBone.name = 'spine';
    this.spineBone.position.y = 0.9;
    this.proceduralGroup.add(this.spineBone);

    // Torso / Dress Body
    let torsoColor = 0x221333;
    let metalness = 0.2;
    let roughness = 0.8;
    if (isKnight) {
      torsoColor = 0x3a4f66;
      metalness = 0.7;
      roughness = 0.3;
    } else if (isAnime) {
      torsoColor = 0x38bdf8;
      metalness = 0.1;
      roughness = 0.5;
    }

    const torsoMat = new THREE.MeshStandardMaterial({
      color: torsoColor,
      metalness,
      roughness,
    });
    const torsoGeo = new THREE.BoxGeometry(0.46, 0.62, 0.28);
    const torso = new THREE.Mesh(torsoGeo, torsoMat);
    torso.position.y = 0.32;
    torso.castShadow = true;
    this.spineBone.add(torso);

    if (isKnight) {
      // Golden Chest Emblem & Cape
      const emblemGeo = new THREE.CylinderGeometry(0.12, 0.12, 0.05, 6);
      const goldMat = new THREE.MeshStandardMaterial({ color: 0xdfa012, metalness: 0.8, roughness: 0.3 });
      const emblem = new THREE.Mesh(emblemGeo, goldMat);
      emblem.rotation.x = Math.PI / 2;
      emblem.position.set(0, 0.45, 0.16);
      this.spineBone.add(emblem);

      const capeGeo = new THREE.PlaneGeometry(0.5, 0.9);
      const capeMat = new THREE.MeshStandardMaterial({ color: 0x8b1c1c, side: THREE.DoubleSide });
      const cape = new THREE.Mesh(capeGeo, capeMat);
      cape.position.set(0, 0.25, -0.16);
      cape.rotation.x = 0.1;
      this.spineBone.add(cape);
    } else if (isAnime) {
      // Cute Anime Skirt & Bow
      const skirtGeo = new THREE.ConeGeometry(0.38, 0.45, 8, 1, true);
      const skirtMat = new THREE.MeshStandardMaterial({ color: 0xf472b6, roughness: 0.6, side: THREE.DoubleSide });
      const skirt = new THREE.Mesh(skirtGeo, skirtMat);
      skirt.position.y = 0.05;
      this.spineBone.add(skirt);

      const bowGeo = new THREE.BoxGeometry(0.16, 0.08, 0.06);
      const bowMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.4 });
      const bow = new THREE.Mesh(bowGeo, bowMat);
      bow.position.set(0, 0.45, 0.15);
      this.spineBone.add(bow);
    } else {
      // Dark Mage Robe Trim
      const robeGeo = new THREE.CylinderGeometry(0.28, 0.45, 0.8, 8);
      const robeMat = new THREE.MeshStandardMaterial({ color: 0x190f29, roughness: 0.85 });
      const robe = new THREE.Mesh(robeGeo, robeMat);
      robe.position.y = -0.1;
      this.spineBone.add(robe);
    }

    // Neck & Head
    this.neckBone = new THREE.Group();
    this.neckBone.name = 'neck';
    this.neckBone.position.y = 0.65;
    this.spineBone.add(this.neckBone);

    this.headBone = new THREE.Group();
    this.headBone.name = 'head';
    this.headBone.position.y = 0.22;
    this.neckBone.add(this.headBone);

    // Face / Head Mesh
    const skinMat = new THREE.MeshStandardMaterial({ color: isAnime ? 0xffdfd0 : 0xffd1b3, roughness: 0.5 });
    const headGeo = new THREE.BoxGeometry(0.32, 0.34, 0.3);
    const head = new THREE.Mesh(headGeo, skinMat);
    head.castShadow = true;
    this.headBone.add(head);

    // Hair / Helmet / Wizard Hat
    if (isKnight) {
      const helmGeo = new THREE.BoxGeometry(0.36, 0.2, 0.36);
      const helmMat = new THREE.MeshStandardMaterial({ color: 0x2c3b4d, metalness: 0.8, roughness: 0.2 });
      const helm = new THREE.Mesh(helmGeo, helmMat);
      helm.position.y = 0.12;
      this.headBone.add(helm);
    } else if (isAnime) {
      // Anime Twin-Tails Hair with Golden Clips
      const hairMat = new THREE.MeshStandardMaterial({ color: 0x38bdf8, roughness: 0.4 });
      const bangGeo = new THREE.BoxGeometry(0.34, 0.12, 0.32);
      const bangs = new THREE.Mesh(bangGeo, hairMat);
      bangs.position.y = 0.16;
      this.headBone.add(bangs);

      // Left Tail
      const tailGeo = new THREE.ConeGeometry(0.08, 0.65, 6);
      const tailL = new THREE.Mesh(tailGeo, hairMat);
      tailL.position.set(-0.22, -0.15, -0.05);
      tailL.rotation.z = 0.2;
      this.headBone.add(tailL);

      // Right Tail
      const tailR = new THREE.Mesh(tailGeo, hairMat);
      tailR.position.set(0.22, -0.15, -0.05);
      tailR.rotation.z = -0.2;
      this.headBone.add(tailR);

      // Hair Ribbon Clips
      const clipGeo = new THREE.SphereGeometry(0.04, 6, 6);
      const clipMat = new THREE.MeshStandardMaterial({ color: 0xfbbf24, metalness: 0.8, roughness: 0.2 });
      const clipL = new THREE.Mesh(clipGeo, clipMat);
      clipL.position.set(-0.2, 0.15, 0.05);
      this.headBone.add(clipL);
      const clipR = new THREE.Mesh(clipGeo, clipMat);
      clipR.position.set(0.2, 0.15, 0.05);
      this.headBone.add(clipR);
    } else {
      const hatGeo = new THREE.ConeGeometry(0.35, 0.7, 8);
      const hatMat = new THREE.MeshStandardMaterial({ color: 0x371357, roughness: 0.7 });
      const hat = new THREE.Mesh(hatGeo, hatMat);
      hat.position.set(0, 0.45, -0.05);
      hat.rotation.x = -0.15;
      this.headBone.add(hat);
    }

    // Eyes & Eyebrows
    const eyeGeo = new THREE.SphereGeometry(isAnime ? 0.045 : 0.035, 8, 8);
    const eyeMat = new THREE.MeshBasicMaterial({ color: isKnight ? 0x224488 : isAnime ? 0x0284c7 : 0x9922cc });
    this.eyeLMesh = new THREE.Mesh(eyeGeo, eyeMat);
    this.eyeLMesh.position.set(-0.08, 0.04, 0.155);
    this.headBone.add(this.eyeLMesh);

    this.eyeRMesh = new THREE.Mesh(eyeGeo, eyeMat);
    this.eyeRMesh.position.set(0.08, 0.04, 0.155);
    this.headBone.add(this.eyeRMesh);

    const browGeo = new THREE.BoxGeometry(0.08, 0.02, 0.02);
    const browMat = new THREE.MeshBasicMaterial({ color: 0x222222 });
    this.eyebrowL = new THREE.Mesh(browGeo, browMat);
    this.eyebrowL.position.set(-0.08, 0.1, 0.17);
    this.headBone.add(this.eyebrowL);

    this.eyebrowR = new THREE.Mesh(browGeo, browMat);
    this.eyebrowR.position.set(0.08, 0.1, 0.17);
    this.headBone.add(this.eyebrowR);

    // Mouth with Blendshape Morph Capability
    const mouthGeo = new THREE.BoxGeometry(0.1, 0.03, 0.02);
    const mouthMat = new THREE.MeshBasicMaterial({ color: 0x661111 });
    this.mouthMesh = new THREE.Mesh(mouthGeo, mouthMat);
    this.mouthMesh.name = 'mouth';
    this.mouthMesh.position.set(0, -0.08, 0.165);
    this.headBone.add(this.mouthMesh);

    // Limbs Material
    const limbMat = new THREE.MeshStandardMaterial({
      color: isKnight ? 0x2c3b4d : 0x2a1740,
      metalness: isKnight ? 0.6 : 0.2,
      roughness: 0.4,
    });

    // Articulated Arms (Shoulder -> Upper Arm -> Elbow -> Forearm -> Hand Socket)
    const upperArmGeo = new THREE.CylinderGeometry(0.065, 0.06, 0.28, 6);
    const forearmGeo = new THREE.CylinderGeometry(0.06, 0.052, 0.26, 6);

    // Left Arm
    this.leftArm = new THREE.Group();
    this.leftArm.name = 'shoulder_l';
    this.leftArm.position.set(-0.34, 0.55, 0);
    const upperArmMeshL = new THREE.Mesh(upperArmGeo, limbMat);
    upperArmMeshL.position.y = -0.14;
    upperArmMeshL.castShadow = true;
    this.leftArm.add(upperArmMeshL);

    this.leftElbow = new THREE.Group();
    this.leftElbow.name = 'elbow_l';
    this.leftElbow.position.set(0, -0.28, 0);
    const forearmMeshL = new THREE.Mesh(forearmGeo, limbMat);
    forearmMeshL.position.y = -0.13;
    forearmMeshL.castShadow = true;
    this.leftElbow.add(forearmMeshL);
    this.leftArm.add(this.leftElbow);

    this.weaponSocketL = new THREE.Group();
    this.weaponSocketL.name = 'weapon_l';
    this.weaponSocketL.position.set(0, -0.26, 0);
    this.leftElbow.add(this.weaponSocketL);
    this.spineBone.add(this.leftArm);

    // Right Arm
    this.rightArm = new THREE.Group();
    this.rightArm.name = 'shoulder_r';
    this.rightArm.position.set(0.34, 0.55, 0);
    const upperArmMeshR = new THREE.Mesh(upperArmGeo, limbMat);
    upperArmMeshR.position.y = -0.14;
    upperArmMeshR.castShadow = true;
    this.rightArm.add(upperArmMeshR);

    this.rightElbow = new THREE.Group();
    this.rightElbow.name = 'elbow_r';
    this.rightElbow.position.set(0, -0.28, 0);
    const forearmMeshR = new THREE.Mesh(forearmGeo, limbMat);
    forearmMeshR.position.y = -0.13;
    forearmMeshR.castShadow = true;
    this.rightElbow.add(forearmMeshR);
    this.rightArm.add(this.rightElbow);

    this.weaponSocketR = new THREE.Group();
    this.weaponSocketR.name = 'weapon_r';
    this.weaponSocketR.position.set(0, -0.26, 0);
    this.rightElbow.add(this.weaponSocketR);
    this.spineBone.add(this.rightArm);

    // Articulated Legs (Hip -> Thigh -> Knee -> Calf/Shin -> Foot)
    const thighGeo = new THREE.CylinderGeometry(0.08, 0.072, 0.42, 6);
    const calfGeo = new THREE.CylinderGeometry(0.072, 0.062, 0.42, 6);
    const footGeo = new THREE.BoxGeometry(0.11, 0.06, 0.17);
    const bootMat = new THREE.MeshStandardMaterial({
      color: isKnight ? 0x1e2733 : 0x180d24,
      roughness: 0.6,
    });

    // Left Leg
    this.leftLeg = new THREE.Group();
    this.leftLeg.name = 'hip_l';
    this.leftLeg.position.set(-0.16, 0.85, 0);
    const thighMeshL = new THREE.Mesh(thighGeo, limbMat);
    thighMeshL.position.y = -0.21;
    thighMeshL.castShadow = true;
    this.leftLeg.add(thighMeshL);

    this.leftKnee = new THREE.Group();
    this.leftKnee.name = 'knee_l';
    this.leftKnee.position.set(0, -0.42, 0);
    const calfMeshL = new THREE.Mesh(calfGeo, limbMat);
    calfMeshL.position.y = -0.21;
    calfMeshL.castShadow = true;
    this.leftKnee.add(calfMeshL);

    this.leftFoot = new THREE.Mesh(footGeo, bootMat);
    this.leftFoot.position.set(0, -0.42, 0.04);
    this.leftFoot.castShadow = true;
    this.leftKnee.add(this.leftFoot);
    this.leftLeg.add(this.leftKnee);
    this.proceduralGroup.add(this.leftLeg);

    // Right Leg
    this.rightLeg = new THREE.Group();
    this.rightLeg.name = 'hip_r';
    this.rightLeg.position.set(0.16, 0.85, 0);
    const thighMeshR = new THREE.Mesh(thighGeo, limbMat);
    thighMeshR.position.y = -0.21;
    thighMeshR.castShadow = true;
    this.rightLeg.add(thighMeshR);

    this.rightKnee = new THREE.Group();
    this.rightKnee.name = 'knee_r';
    this.rightKnee.position.set(0, -0.42, 0);
    const calfMeshR = new THREE.Mesh(calfGeo, limbMat);
    calfMeshR.position.y = -0.21;
    calfMeshR.castShadow = true;
    this.rightKnee.add(calfMeshR);

    this.rightFoot = new THREE.Mesh(footGeo, bootMat);
    this.rightFoot.position.set(0, -0.42, 0.04);
    this.rightFoot.castShadow = true;
    this.rightKnee.add(this.rightFoot);
    this.rightLeg.add(this.rightKnee);
    this.proceduralGroup.add(this.rightLeg);

    // Attach Default Weapon
    if (isKnight) {
      const sword = SocketAttacher.createWeapon('fire_sword');
      this.weaponSocketR.add(sword);
    } else if (isAnime) {
      const lantern = SocketAttacher.createWeapon('lantern');
      this.weaponSocketR.add(lantern);
    } else {
      const staff = SocketAttacher.createWeapon('magic_staff');
      this.weaponSocketR.add(staff);
    }

    // Automatically trigger modular 3D character loading
    this.loadModularAssembly(config.assembly, config.model);
  }

  /**
   * Nạp động mô hình 3D thực tế và ghép các bộ phận Modular (Thân + Quần Áo + Mặt + Tóc + Phụ Kiện)
   */
  public async loadModularAssembly(assembly?: CharacterAssembly, modelPath?: string): Promise<void> {
    const resolvePath = (p?: string): string => {
      if (!p) return '';
      if (p.startsWith('http://') || p.startsWith('https://')) return p;
      return p.startsWith('/') ? p : `/${p}`;
    };

    // Clean previous modular parts
    while (this.modularGroup.children.length > 0) {
      this.modularGroup.remove(this.modularGroup.children[0]);
    }

    if (assembly && (assembly.base_body || assembly.costume || assembly.face)) {
      const partsToLoad: { key: string; path: string }[] = [];
      if (assembly.base_body) partsToLoad.push({ key: 'base_body', path: resolvePath(assembly.base_body) });
      if (assembly.costume) partsToLoad.push({ key: 'costume', path: resolvePath(assembly.costume) });
      if (assembly.face) partsToLoad.push({ key: 'face', path: resolvePath(assembly.face) });
      if (assembly.hairstyle) partsToLoad.push({ key: 'hairstyle', path: resolvePath(assembly.hairstyle) });
      if (assembly.beard) partsToLoad.push({ key: 'beard', path: resolvePath(assembly.beard) });
      if (assembly.accessories) {
        assembly.accessories.forEach((acc, idx) => {
          partsToLoad.push({ key: `acc_${idx}`, path: resolvePath(acc) });
        });
      }

      let loadedCount = 0;
      for (const item of partsToLoad) {
        try {
          const modelGroup = await AssetLoaderRegistry.loadCharacterPart(item.path);
          modelGroup.name = `part_${item.key}`;
          this.modularGroup.add(modelGroup);
          loadedCount++;
        } catch (err) {
          console.warn(`[VRMAvatar] Không thể nạp modular part ${item.key} (${item.path}):`, err);
        }
      }

      if (loadedCount > 0) {
        // Successfully loaded real 3D modular meshes, hide procedural placeholder
        this.proceduralGroup.visible = false;
      }
    } else if (modelPath && (modelPath.endsWith('.glb') || modelPath.endsWith('.gltf') || modelPath.endsWith('.vrm'))) {
      try {
        const fullModel = await AssetLoaderRegistry.loadCharacterPart(resolvePath(modelPath));
        fullModel.name = 'full_character_model';
        this.modularGroup.add(fullModel);
        this.proceduralGroup.visible = false;
      } catch (err) {
        console.warn(`[VRMAvatar] Không thể nạp full model ${modelPath}:`, err);
      }
    }
  }

  public getHeadPosition(out: THREE.Vector3 = new THREE.Vector3()): THREE.Vector3 {
    return this.headBone.getWorldPosition(out);
  }
}
