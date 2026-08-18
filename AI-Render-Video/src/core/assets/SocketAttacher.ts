import * as THREE from 'three';

export class SocketAttacher {
  public static createWeapon(type: 'fire_sword' | 'magic_staff'): THREE.Group {
    const weapon = new THREE.Group();

    if (type === 'fire_sword') {
      weapon.name = 'weapon_fire_sword';
      // Hilt & Guard
      const hiltGeo = new THREE.CylinderGeometry(0.025, 0.025, 0.25, 6);
      const hiltMat = new THREE.MeshStandardMaterial({ color: 0x332211, roughness: 0.6 });
      const hilt = new THREE.Mesh(hiltGeo, hiltMat);
      hilt.position.y = -0.1;
      weapon.add(hilt);

      const guardGeo = new THREE.BoxGeometry(0.2, 0.03, 0.05);
      const goldMat = new THREE.MeshStandardMaterial({ color: 0xe6a117, metalness: 0.8, roughness: 0.3 });
      const guard = new THREE.Mesh(guardGeo, goldMat);
      guard.position.y = 0.02;
      weapon.add(guard);

      // Blade
      const bladeGeo = new THREE.BoxGeometry(0.06, 0.8, 0.015);
      const bladeMat = new THREE.MeshStandardMaterial({
        color: 0xcccccc,
        metalness: 0.9,
        roughness: 0.2,
        emissive: 0xff4400,
        emissiveIntensity: 0.2,
      });
      const blade = new THREE.Mesh(bladeGeo, bladeMat);
      blade.position.y = 0.42;
      blade.castShadow = true;
      weapon.add(blade);

      // Tip marker for VFX
      const tipMarker = new THREE.Object3D();
      tipMarker.name = 'weapon_tip';
      tipMarker.position.set(0, 0.82, 0);
      weapon.add(tipMarker);
    } else {
      weapon.name = 'weapon_magic_staff';
      // Staff Shaft
      const shaftGeo = new THREE.CylinderGeometry(0.03, 0.03, 1.4, 8);
      const woodMat = new THREE.MeshStandardMaterial({ color: 0x221133, roughness: 0.7 });
      const shaft = new THREE.Mesh(shaftGeo, woodMat);
      shaft.position.y = 0.4;
      shaft.castShadow = true;
      weapon.add(shaft);

      // Magical Orb on Top
      const orbGeo = new THREE.SphereGeometry(0.12, 16, 16);
      const orbMat = new THREE.MeshStandardMaterial({
        color: 0x9933ff,
        emissive: 0xaa22ff,
        emissiveIntensity: 0.8,
        roughness: 0.1,
      });
      const orb = new THREE.Mesh(orbGeo, orbMat);
      orb.position.y = 1.15;
      weapon.add(orb);

      // Tip marker for VFX
      const tipMarker = new THREE.Object3D();
      tipMarker.name = 'weapon_tip';
      tipMarker.position.set(0, 1.25, 0);
      weapon.add(tipMarker);
    }

    return weapon;
  }

  public static attachToSocket(
    actorObject: THREE.Object3D,
    item: THREE.Object3D,
    socketName: string
  ): boolean {
    const socket = actorObject.getObjectByName(socketName);
    if (socket) {
      socket.add(item);
      return true;
    }
    // Fallback: attach directly to actor root if socket not found
    actorObject.add(item);
    return false;
  }
}
