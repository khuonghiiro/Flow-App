import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';

export interface HitCalculation {
  hitPoint: THREE.Vector3;
  knockbackDirection: THREE.Vector3;
  distance: number;
}

export class HitBoxDetector {
  public static calculateHit(attacker: VRMAvatar, defender: VRMAvatar): HitCalculation {
    const attackerPos = new THREE.Vector3();
    attacker.rootObject.getWorldPosition(attackerPos);

    const defenderPos = new THREE.Vector3();
    defender.rootObject.getWorldPosition(defenderPos);

    const weaponTip = attacker.rootObject.getObjectByName('weapon_tip');
    const hitPoint = new THREE.Vector3();

    if (weaponTip) {
      weaponTip.getWorldPosition(hitPoint);
    } else {
      hitPoint.copy(attackerPos).lerp(defenderPos, 0.7);
      hitPoint.y += 1.2;
    }

    const knockbackDirection = defenderPos.clone().sub(attackerPos).normalize();
    if (knockbackDirection.lengthSq() < 0.001) {
      knockbackDirection.set(1, 0, 0); // Default direction if coincident
    }
    knockbackDirection.y = 0; // Horizontal knockback

    const distance = attackerPos.distanceTo(defenderPos);

    return {
      hitPoint,
      knockbackDirection,
      distance,
    };
  }
}
