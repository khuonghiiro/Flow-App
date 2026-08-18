import * as THREE from 'three';

export class CombatVFXTrigger {
  private scene: THREE.Scene;
  private activeVFX: Array<{ object: THREE.Object3D; update: (delta: number) => boolean }> = [];

  constructor(scene: THREE.Scene) {
    this.scene = scene;
  }

  public spawnSlashTrail(position: THREE.Vector3, direction: THREE.Vector3): void {
    const slashGeo = new THREE.TorusGeometry(1.2, 0.08, 8, 24, Math.PI * 0.8);
    const slashMat = new THREE.MeshBasicMaterial({
      color: 0xff5500,
      transparent: true,
      opacity: 0.9,
      side: THREE.DoubleSide,
    });
    const mesh = new THREE.Mesh(slashGeo, slashMat);
    mesh.position.copy(position);
    mesh.rotation.x = Math.PI / 2;
    this.scene.add(mesh);

    let life = 0.4;
    this.activeVFX.push({
      object: mesh,
      update: (delta: number) => {
        life -= delta;
        slashMat.opacity = Math.max(0, life / 0.4);
        mesh.scale.multiplyScalar(1.05);
        if (life <= 0) {
          this.scene.remove(mesh);
          return false;
        }
        return true;
      },
    });
  }

  public spawnHitSparks(position: THREE.Vector3): void {
    const count = 30;
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(count * 3);
    const velocities: THREE.Vector3[] = [];

    for (let i = 0; i < count; i++) {
      positions[i * 3] = position.x;
      positions[i * 3 + 1] = position.y;
      positions[i * 3 + 2] = position.z;

      velocities.push(
        new THREE.Vector3(
          (Math.random() - 0.5) * 6,
          Math.random() * 4 + 1,
          (Math.random() - 0.5) * 6
        )
      );
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const material = new THREE.PointsMaterial({
      color: 0xffdd44,
      size: 0.15,
      transparent: true,
      opacity: 1.0,
      blending: THREE.AdditiveBlending,
    });

    const particles = new THREE.Points(geometry, material);
    this.scene.add(particles);

    let life = 0.5;
    this.activeVFX.push({
      object: particles,
      update: (delta: number) => {
        life -= delta;
        material.opacity = Math.max(0, life / 0.5);
        const posAttr = geometry.getAttribute('position') as THREE.BufferAttribute;
        const arr = posAttr.array as Float32Array;

        for (let i = 0; i < count; i++) {
          arr[i * 3] += velocities[i].x * delta;
          arr[i * 3 + 1] += velocities[i].y * delta;
          arr[i * 3 + 2] += velocities[i].z * delta;
          velocities[i].y -= 9.8 * delta; // Gravity
        }
        posAttr.needsUpdate = true;

        if (life <= 0) {
          this.scene.remove(particles);
          return false;
        }
        return true;
      },
    });
  }

  public spawnMagicShield(targetObject: THREE.Object3D, duration: number = 1.5): void {
    const shieldGeo = new THREE.SphereGeometry(1.2, 16, 16);
    const shieldMat = new THREE.MeshStandardMaterial({
      color: 0x8833ff,
      emissive: 0xaa44ff,
      emissiveIntensity: 0.8,
      transparent: true,
      opacity: 0.45,
      wireframe: true,
    });
    const shield = new THREE.Mesh(shieldGeo, shieldMat);
    shield.position.y = 0.8;
    targetObject.add(shield);

    let life = duration;
    this.activeVFX.push({
      object: shield,
      update: (delta: number) => {
        life -= delta;
        shield.rotation.y += delta * 2;
        if (life <= 0) {
          targetObject.remove(shield);
          return false;
        }
        return true;
      },
    });
  }

  public update(delta: number): void {
    this.activeVFX = this.activeVFX.filter((vfx) => vfx.update(delta));
  }

  public clear(): void {
    for (const vfx of this.activeVFX) {
      if (vfx.object.parent) {
        vfx.object.parent.remove(vfx.object);
      }
    }
    this.activeVFX = [];
  }
}
