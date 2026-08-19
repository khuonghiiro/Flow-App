import * as THREE from 'three';

// ============================================================
// Costume Style Definitions
// ============================================================

export type CostumeStyle =
  | 'knight_armor'
  | 'dark_mage_robe'
  | 'anime_school'
  | 'xianxia_cultivator'
  | 'super_armor'
  | 'casual'
  | 'farming_clothes';

export interface CostumeColors {
  torso: number;
  limbs: number;
  boots: number;
  accent: number;
  metalness: number;
  roughness: number;
}

/** Color palette for each costume style */
const COSTUME_PALETTES: Record<CostumeStyle, CostumeColors> = {
  knight_armor: {
    torso: 0x3a4f66, limbs: 0x2c3b4d, boots: 0x1e2733,
    accent: 0xdfa012, metalness: 0.7, roughness: 0.3,
  },
  dark_mage_robe: {
    torso: 0x221333, limbs: 0x2a1740, boots: 0x180d24,
    accent: 0x9933ff, metalness: 0.2, roughness: 0.8,
  },
  anime_school: {
    torso: 0x38bdf8, limbs: 0x2a1740, boots: 0x180d24,
    accent: 0xf472b6, metalness: 0.1, roughness: 0.5,
  },
  xianxia_cultivator: {
    torso: 0xeee8d5, limbs: 0x5c4a3a, boots: 0x3b2f23,
    accent: 0xc9a961, metalness: 0.3, roughness: 0.6,
  },
  super_armor: {
    torso: 0xcc3333, limbs: 0x1a1a2e, boots: 0x111122,
    accent: 0xffdd00, metalness: 0.85, roughness: 0.15,
  },
  casual: {
    torso: 0x4a6fa5, limbs: 0x3d4f5f, boots: 0x2c3340,
    accent: 0xffffff, metalness: 0.1, roughness: 0.7,
  },
  farming_clothes: {
    torso: 0x6b8e23, limbs: 0x556b2f, boots: 0x3b3b1a,
    accent: 0xdaa520, metalness: 0.1, roughness: 0.8,
  },
};

// ============================================================
// AvatarMeshFactory - Factory tạo mesh parts cho VRMAvatar
// ============================================================

export class AvatarMeshFactory {

  /** Lấy color palette cho costume */
  public static getPalette(style: CostumeStyle): CostumeColors {
    return COSTUME_PALETTES[style] || COSTUME_PALETTES.casual;
  }

  /** Tạo torso mesh theo costume style */
  public static createTorso(style: CostumeStyle): THREE.Group {
    const group = new THREE.Group();
    group.name = 'torso_group';
    const palette = this.getPalette(style);

    const torsoMat = new THREE.MeshStandardMaterial({
      color: palette.torso,
      metalness: palette.metalness,
      roughness: palette.roughness,
    });
    const torsoGeo = new THREE.BoxGeometry(0.46, 0.62, 0.28);
    const torso = new THREE.Mesh(torsoGeo, torsoMat);
    torso.position.y = 0.32;
    torso.castShadow = true;
    torso.name = 'torso_mesh';
    group.add(torso);

    // Style-specific decorations
    this.addTorsoDecorations(group, style, palette);

    return group;
  }

  /** Thêm trang trí cho torso theo style */
  private static addTorsoDecorations(
    group: THREE.Group, style: CostumeStyle, palette: CostumeColors
  ): void {
    if (style === 'knight_armor') {
      this.addKnightDecorations(group, palette);
    } else if (style === 'anime_school') {
      this.addAnimeDecorations(group, palette);
    } else if (style === 'dark_mage_robe') {
      this.addMageDecorations(group);
    } else if (style === 'xianxia_cultivator') {
      this.addXianxiaDecorations(group, palette);
    } else if (style === 'super_armor') {
      this.addSuperArmorDecorations(group, palette);
    }
  }

  private static addKnightDecorations(group: THREE.Group, p: CostumeColors): void {
    const emblemGeo = new THREE.CylinderGeometry(0.12, 0.12, 0.05, 6);
    const goldMat = new THREE.MeshStandardMaterial({
      color: p.accent, metalness: 0.8, roughness: 0.3,
    });
    const emblem = new THREE.Mesh(emblemGeo, goldMat);
    emblem.rotation.x = Math.PI / 2;
    emblem.position.set(0, 0.45, 0.16);
    group.add(emblem);

    const capeGeo = new THREE.PlaneGeometry(0.5, 0.9);
    const capeMat = new THREE.MeshStandardMaterial({
      color: 0x8b1c1c, side: THREE.DoubleSide,
    });
    const cape = new THREE.Mesh(capeGeo, capeMat);
    cape.position.set(0, 0.25, -0.16);
    cape.rotation.x = 0.1;
    group.add(cape);
  }

  private static addAnimeDecorations(group: THREE.Group, p: CostumeColors): void {
    const skirtGeo = new THREE.ConeGeometry(0.38, 0.45, 8, 1, true);
    const skirtMat = new THREE.MeshStandardMaterial({
      color: p.accent, roughness: 0.6, side: THREE.DoubleSide,
    });
    const skirt = new THREE.Mesh(skirtGeo, skirtMat);
    skirt.position.y = 0.05;
    group.add(skirt);

    const bowGeo = new THREE.BoxGeometry(0.16, 0.08, 0.06);
    const bowMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.4 });
    const bow = new THREE.Mesh(bowGeo, bowMat);
    bow.position.set(0, 0.45, 0.15);
    group.add(bow);
  }

  private static addMageDecorations(group: THREE.Group): void {
    const robeGeo = new THREE.CylinderGeometry(0.28, 0.45, 0.8, 8);
    const robeMat = new THREE.MeshStandardMaterial({ color: 0x190f29, roughness: 0.85 });
    const robe = new THREE.Mesh(robeGeo, robeMat);
    robe.position.y = -0.1;
    group.add(robe);
  }

  private static addXianxiaDecorations(group: THREE.Group, p: CostumeColors): void {
    // Long flowing robe bottom
    const robeGeo = new THREE.CylinderGeometry(0.3, 0.5, 0.9, 8, 1, true);
    const robeMat = new THREE.MeshStandardMaterial({
      color: p.torso, roughness: 0.5, side: THREE.DoubleSide,
    });
    const robe = new THREE.Mesh(robeGeo, robeMat);
    robe.position.y = -0.1;
    group.add(robe);

    // Golden sash belt
    const sashGeo = new THREE.TorusGeometry(0.28, 0.02, 6, 12);
    const sashMat = new THREE.MeshStandardMaterial({
      color: p.accent, metalness: 0.6, roughness: 0.3,
    });
    const sash = new THREE.Mesh(sashGeo, sashMat);
    sash.rotation.x = Math.PI / 2;
    sash.position.y = 0.08;
    group.add(sash);
  }

  private static addSuperArmorDecorations(group: THREE.Group, p: CostumeColors): void {
    // Shoulder pads
    const padGeo = new THREE.SphereGeometry(0.12, 8, 8, 0, Math.PI);
    const padMat = new THREE.MeshStandardMaterial({
      color: p.accent, metalness: 0.9, roughness: 0.1,
      emissive: p.accent, emissiveIntensity: 0.3,
    });
    const padL = new THREE.Mesh(padGeo, padMat);
    padL.position.set(-0.3, 0.6, 0);
    group.add(padL);
    const padR = new THREE.Mesh(padGeo, padMat);
    padR.position.set(0.3, 0.6, 0);
    group.add(padR);
  }

  /** Tạo head mesh theo costume style */
  public static createHead(style: CostumeStyle, isAnime: boolean = false): THREE.Group {
    const group = new THREE.Group();
    group.name = 'head_group';

    const skinColor = isAnime ? 0xffdfd0 : 0xffd1b3;
    const skinMat = new THREE.MeshStandardMaterial({ color: skinColor, roughness: 0.5 });
    const headGeo = new THREE.BoxGeometry(0.32, 0.34, 0.3);
    const head = new THREE.Mesh(headGeo, skinMat);
    head.castShadow = true;
    group.add(head);

    this.addHairOrHelmet(group, style, isAnime);
    return group;
  }

  /** Thêm tóc hoặc mũ theo style */
  private static addHairOrHelmet(
    group: THREE.Group, style: CostumeStyle, isAnime: boolean
  ): void {
    if (style === 'knight_armor') {
      const helmGeo = new THREE.BoxGeometry(0.36, 0.2, 0.36);
      const helmMat = new THREE.MeshStandardMaterial({
        color: 0x2c3b4d, metalness: 0.8, roughness: 0.2,
      });
      const helm = new THREE.Mesh(helmGeo, helmMat);
      helm.position.y = 0.12;
      group.add(helm);
    } else if (isAnime || style === 'anime_school') {
      this.addAnimeTwinTails(group);
    } else if (style === 'dark_mage_robe') {
      const hatGeo = new THREE.ConeGeometry(0.35, 0.7, 8);
      const hatMat = new THREE.MeshStandardMaterial({ color: 0x371357, roughness: 0.7 });
      const hat = new THREE.Mesh(hatGeo, hatMat);
      hat.position.set(0, 0.45, -0.05);
      hat.rotation.x = -0.15;
      group.add(hat);
    } else if (style === 'xianxia_cultivator') {
      this.addXianxiaHair(group);
    } else if (style === 'super_armor') {
      // Crown / aura ring
      const crownGeo = new THREE.TorusGeometry(0.22, 0.02, 6, 16);
      const crownMat = new THREE.MeshStandardMaterial({
        color: 0xffdd00, emissive: 0xffaa00, emissiveIntensity: 0.5,
        metalness: 0.9, roughness: 0.1,
      });
      const crown = new THREE.Mesh(crownGeo, crownMat);
      crown.position.y = 0.22;
      crown.rotation.x = Math.PI / 2;
      group.add(crown);
    }
  }

  /** Tóc twin-tails anime */
  private static addAnimeTwinTails(group: THREE.Group): void {
    const hairMat = new THREE.MeshStandardMaterial({ color: 0x38bdf8, roughness: 0.4 });
    const bangGeo = new THREE.BoxGeometry(0.34, 0.12, 0.32);
    const bangs = new THREE.Mesh(bangGeo, hairMat);
    bangs.position.y = 0.16;
    group.add(bangs);

    const tailGeo = new THREE.ConeGeometry(0.08, 0.65, 6);
    const tailL = new THREE.Mesh(tailGeo, hairMat);
    tailL.position.set(-0.22, -0.15, -0.05);
    tailL.rotation.z = 0.2;
    group.add(tailL);

    const tailR = new THREE.Mesh(tailGeo, hairMat);
    tailR.position.set(0.22, -0.15, -0.05);
    tailR.rotation.z = -0.2;
    group.add(tailR);

    // Hair clips
    const clipGeo = new THREE.SphereGeometry(0.04, 6, 6);
    const clipMat = new THREE.MeshStandardMaterial({
      color: 0xfbbf24, metalness: 0.8, roughness: 0.2,
    });
    const clipL = new THREE.Mesh(clipGeo, clipMat);
    clipL.position.set(-0.2, 0.15, 0.05);
    group.add(clipL);
    const clipR = new THREE.Mesh(clipGeo, clipMat);
    clipR.position.set(0.2, 0.15, 0.05);
    group.add(clipR);
  }

  /** Tóc tiên hiệp (dài, buộc cao) */
  private static addXianxiaHair(group: THREE.Group): void {
    const hairMat = new THREE.MeshStandardMaterial({ color: 0x1a1a2e, roughness: 0.5 });

    // Top bun
    const bunGeo = new THREE.SphereGeometry(0.1, 8, 8);
    const bun = new THREE.Mesh(bunGeo, hairMat);
    bun.position.set(0, 0.28, -0.05);
    group.add(bun);

    // Hair pin
    const pinGeo = new THREE.CylinderGeometry(0.01, 0.01, 0.2, 4);
    const pinMat = new THREE.MeshStandardMaterial({
      color: 0xc9a961, metalness: 0.8, roughness: 0.2,
    });
    const pin = new THREE.Mesh(pinGeo, pinMat);
    pin.position.set(0.06, 0.3, -0.05);
    pin.rotation.z = Math.PI / 4;
    group.add(pin);

    // Long flowing back hair
    const backHairGeo = new THREE.BoxGeometry(0.2, 0.7, 0.06);
    const backHair = new THREE.Mesh(backHairGeo, hairMat);
    backHair.position.set(0, -0.2, -0.16);
    group.add(backHair);
  }

  /** Tạo limb material theo costume */
  public static createLimbMaterial(style: CostumeStyle): THREE.MeshStandardMaterial {
    const palette = this.getPalette(style);
    return new THREE.MeshStandardMaterial({
      color: palette.limbs,
      metalness: style === 'knight_armor' ? 0.6 : palette.metalness,
      roughness: 0.4,
    });
  }

  /** Tạo boot material theo costume */
  public static createBootMaterial(style: CostumeStyle): THREE.MeshStandardMaterial {
    const palette = this.getPalette(style);
    return new THREE.MeshStandardMaterial({
      color: palette.boots,
      roughness: 0.6,
    });
  }
}
