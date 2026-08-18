import * as THREE from 'three';
import { ActorConfig } from '../../types/scene';
import { SocketAttacher } from '../assets/SocketAttacher';

export class VRMAvatar {
  public config: ActorConfig;
  public rootObject: THREE.Group;
  public headBone: THREE.Object3D;
  public neckBone: THREE.Object3D;
  public spineBone: THREE.Object3D;
  public leftArm: THREE.Object3D;
  public rightArm: THREE.Object3D;
  public leftLeg: THREE.Object3D;
  public rightLeg: THREE.Object3D;
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

    const isKnight = config.id.includes('warrior') || config.model.includes('knight');

    // Hierarchy: Root -> Spine -> Neck -> Head
    this.spineBone = new THREE.Group();
    this.spineBone.name = 'spine';
    this.spineBone.position.y = 0.9;
    this.rootObject.add(this.spineBone);

    // Torso / Armor Body
    const torsoMat = new THREE.MeshStandardMaterial({
      color: isKnight ? 0x3a4f66 : 0x221333,
      metalness: isKnight ? 0.7 : 0.2,
      roughness: isKnight ? 0.3 : 0.8,
    });
    const torsoGeo = new THREE.BoxGeometry(0.5, 0.65, 0.3);
    const torso = new THREE.Mesh(torsoGeo, torsoMat);
    torso.position.y = 0.32;
    torso.castShadow = true;
    this.spineBone.add(torso);

    if (isKnight) {
      // Golden Chest Emblem & Pauldrons
      const emblemGeo = new THREE.CylinderGeometry(0.12, 0.12, 0.05, 6);
      const goldMat = new THREE.MeshStandardMaterial({ color: 0xdfa012, metalness: 0.8, roughness: 0.3 });
      const emblem = new THREE.Mesh(emblemGeo, goldMat);
      emblem.rotation.x = Math.PI / 2;
      emblem.position.set(0, 0.45, 0.16);
      this.spineBone.add(emblem);

      // Cape
      const capeGeo = new THREE.PlaneGeometry(0.5, 0.9);
      const capeMat = new THREE.MeshStandardMaterial({ color: 0x8b1c1c, side: THREE.DoubleSide });
      const cape = new THREE.Mesh(capeGeo, capeMat);
      cape.position.set(0, 0.25, -0.16);
      cape.rotation.x = 0.1;
      this.spineBone.add(cape);
    } else {
      // Dark Mage Cape / Robe Trim
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
    const skinMat = new THREE.MeshStandardMaterial({ color: 0xffd1b3, roughness: 0.6 });
    const headGeo = new THREE.BoxGeometry(0.32, 0.36, 0.32);
    const head = new THREE.Mesh(headGeo, skinMat);
    head.castShadow = true;
    this.headBone.add(head);

    // Helmet or Wizard Hat
    if (isKnight) {
      const helmGeo = new THREE.BoxGeometry(0.36, 0.2, 0.36);
      const helmMat = new THREE.MeshStandardMaterial({ color: 0x2c3b4d, metalness: 0.8, roughness: 0.2 });
      const helm = new THREE.Mesh(helmGeo, helmMat);
      helm.position.y = 0.12;
      this.headBone.add(helm);
    } else {
      const hatGeo = new THREE.ConeGeometry(0.35, 0.7, 8);
      const hatMat = new THREE.MeshStandardMaterial({ color: 0x371357, roughness: 0.7 });
      const hat = new THREE.Mesh(hatGeo, hatMat);
      hat.position.set(0, 0.45, -0.05);
      hat.rotation.x = -0.15;
      this.headBone.add(hat);
    }

    // Eyes & Eyebrows
    const eyeGeo = new THREE.SphereGeometry(0.035, 8, 8);
    const eyeMat = new THREE.MeshBasicMaterial({ color: isKnight ? 0x224488 : 0x9922cc });
    this.eyeLMesh = new THREE.Mesh(eyeGeo, eyeMat);
    this.eyeLMesh.position.set(-0.08, 0.04, 0.165);
    this.headBone.add(this.eyeLMesh);

    this.eyeRMesh = new THREE.Mesh(eyeGeo, eyeMat);
    this.eyeRMesh.position.set(0.08, 0.04, 0.165);
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

    // Arms
    const limbMat = new THREE.MeshStandardMaterial({
      color: isKnight ? 0x2c3b4d : 0x2a1740,
      metalness: isKnight ? 0.6 : 0.2,
      roughness: 0.4,
    });
    const armGeo = new THREE.CylinderGeometry(0.07, 0.06, 0.55, 6);

    this.leftArm = new THREE.Group();
    this.leftArm.name = 'arm_l';
    this.leftArm.position.set(-0.35, 0.55, 0);
    const armMeshL = new THREE.Mesh(armGeo, limbMat);
    armMeshL.position.y = -0.25;
    armMeshL.castShadow = true;
    this.leftArm.add(armMeshL);
    this.spineBone.add(this.leftArm);

    this.weaponSocketL = new THREE.Group();
    this.weaponSocketL.name = 'weapon_l';
    this.weaponSocketL.position.set(0, -0.5, 0);
    this.leftArm.add(this.weaponSocketL);

    this.rightArm = new THREE.Group();
    this.rightArm.name = 'arm_r';
    this.rightArm.position.set(0.35, 0.55, 0);
    const armMeshR = new THREE.Mesh(armGeo, limbMat);
    armMeshR.position.y = -0.25;
    armMeshR.castShadow = true;
    this.rightArm.add(armMeshR);
    this.spineBone.add(this.rightArm);

    this.weaponSocketR = new THREE.Group();
    this.weaponSocketR.name = 'weapon_r';
    this.weaponSocketR.position.set(0, -0.5, 0);
    this.rightArm.add(this.weaponSocketR);

    // Legs
    const legGeo = new THREE.CylinderGeometry(0.08, 0.07, 0.8, 6);
    this.leftLeg = new THREE.Group();
    this.leftLeg.name = 'leg_l';
    this.leftLeg.position.set(-0.16, 0.85, 0);
    const legMeshL = new THREE.Mesh(legGeo, limbMat);
    legMeshL.position.y = -0.4;
    legMeshL.castShadow = true;
    this.leftLeg.add(legMeshL);
    this.rootObject.add(this.leftLeg);

    this.rightLeg = new THREE.Group();
    this.rightLeg.name = 'leg_r';
    this.rightLeg.position.set(0.16, 0.85, 0);
    const legMeshR = new THREE.Mesh(legGeo, limbMat);
    legMeshR.position.y = -0.4;
    legMeshR.castShadow = true;
    this.rightLeg.add(legMeshR);
    this.rootObject.add(this.rightLeg);

    // Attach Default Weapon
    if (isKnight) {
      const sword = SocketAttacher.createWeapon('fire_sword');
      this.weaponSocketR.add(sword);
    } else {
      const staff = SocketAttacher.createWeapon('magic_staff');
      this.weaponSocketR.add(staff);
    }
  }

  public getHeadPosition(out: THREE.Vector3 = new THREE.Vector3()): THREE.Vector3 {
    return this.headBone.getWorldPosition(out);
  }
}
