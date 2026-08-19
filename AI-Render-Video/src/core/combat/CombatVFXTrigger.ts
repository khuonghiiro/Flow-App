import * as THREE from 'three';

export class CombatVFXTrigger {
  private scene: THREE.Scene;
  private activeVFX: Array<{ object: THREE.Object3D; update: (delta: number) => boolean; dispose: () => void }> = [];

  constructor(scene: THREE.Scene) {
    this.scene = scene;
  }

  public spawnSlashTrail(position: THREE.Vector3, direction: THREE.Vector3): void {
    const slashGeo = new THREE.RingGeometry(0.7, 1.3, 32, 1, 0, Math.PI * 0.8);
    const slashMat = new THREE.MeshBasicMaterial({
      color: 0xff3b00,
      transparent: true,
      opacity: 0.95,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const mesh = new THREE.Mesh(slashGeo, slashMat);
    mesh.position.copy(position);
    mesh.rotation.x = Math.PI / 2;

    this.scene.add(mesh);

    let life = 0.28;
    const maxLife = 0.28;

    const dispose = () => {
      slashGeo.dispose();
      slashMat.dispose();
    };

    this.activeVFX.push({
      object: mesh,
      dispose,
      update: (delta: number) => {
        life -= delta;
        slashMat.opacity = Math.max(0, (life / maxLife) * 0.95);
        mesh.scale.addScalar(delta * 1.6);
        if (life <= 0) {
          if (mesh.parent) mesh.parent.remove(mesh);
          dispose();
          return false;
        }
        return true;
      },
    });
  }

  public spawnHitSparks(position: THREE.Vector3): void {
    const count = 32;
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
          Math.random() * 4 + 1.2,
          (Math.random() - 0.5) * 6
        )
      );
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const material = new THREE.PointsMaterial({
      color: 0xffe066,
      size: 0.16,
      transparent: true,
      opacity: 1.0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    const particles = new THREE.Points(geometry, material);
    this.scene.add(particles);

    let life = 0.45;
    const maxLife = 0.45;

    const dispose = () => {
      geometry.dispose();
      material.dispose();
    };

    this.activeVFX.push({
      object: particles,
      dispose,
      update: (delta: number) => {
        life -= delta;
        material.opacity = Math.max(0, (life / maxLife) * 1.0);
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
          if (particles.parent) particles.parent.remove(particles);
          dispose();
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
      depthWrite: false,
    });
    const shield = new THREE.Mesh(shieldGeo, shieldMat);
    shield.position.y = 0.8;
    targetObject.add(shield);

    let life = duration;
    const dispose = () => {
      shieldGeo.dispose();
      shieldMat.dispose();
    };

    this.activeVFX.push({
      object: shield,
      dispose,
      update: (delta: number) => {
        life -= delta;
        shield.rotation.y += delta * 2;
        if (life <= 0) {
          targetObject.remove(shield);
          dispose();
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
      vfx.dispose();
    }
    this.activeVFX = [];
  }
}
