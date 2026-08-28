import * as THREE from 'three';

export interface SecondaryBoneChainNode {
  bone: THREE.Bone;
  name: string;
  type: 'hair' | 'cloth' | 'ribbon' | 'generic';
  depth: number;
  initialRotation: THREE.Euler;
  currentVelocity: THREE.Euler;
  currentOffset: THREE.Euler;
  swayFrequency: number;
  swayAmplitude: number;
  damping: number;
  stiffness: number;
}

export interface SecondaryPhysicsOptions {
  enabled: boolean;
  intensity: number; // 0.0 - 2.0 (mặc định 1.0)
  windForce?: number;
  characterSpeed?: number;
}

/**
 * SecondaryPhysicsEngine
 * Hệ thống mô phỏng vật lý lò xo thứ cấp (Spring-Damper Dynamics & Inertial Lag)
 * tự động kích hoạt cho Tóc (Hair), Áo choàng (Overcoat/Cape), Váy (Skirt/Dress),
 * Ruy băng (Ribbon), Ống tay áo (Sleeves) và các khớp trang phục phụ (như 726 khớp của Columbina).
 */
export class SecondaryPhysicsEngine {
  private static secondaryNodesCache = new WeakMap<THREE.Object3D, SecondaryBoneChainNode[]>();

  /**
   * Kiểm tra xem một tên khớp có phải là xương vật lý thứ cấp (tóc, váy, áo choàng, nơ, ruy băng, chuỗi lò xo)
   */
  public static isSecondaryBone(name: string): { isSecondary: boolean; type: 'hair' | 'cloth' | 'ribbon' | 'generic' } {
    const lower = name.toLowerCase();

    // 1. Tóc (Hair / Bangs / Ahoge / Twintail / Ponytail / Toc)
    if (
      lower.includes('hair') ||
      lower.includes('bang') ||
      lower.includes('toc') ||
      lower.includes('ahoge') ||
      lower.includes('ponytail') ||
      lower.includes('twintail') ||
      lower.includes('braid')
    ) {
      return { isSecondary: true, type: 'hair' };
    }

    // 2. Trang phục / Áo choàng / Váy / Áo / Dải lụa
    if (
      lower.includes('overcoat') ||
      lower.includes('coat') ||
      lower.includes('skirt') ||
      lower.includes('dress') ||
      lower.includes('cape') ||
      lower.includes('sleeve') ||
      lower.includes('cloth') ||
      lower.includes('robe') ||
      lower.includes('ao') ||
      lower.includes('vay') ||
      lower.includes('flap') ||
      lower.includes('hem')
    ) {
      return { isSecondary: true, type: 'cloth' };
    }

    // 3. Ruy băng / Nơ / Dây đai / Dây chuông
    if (
      lower.includes('ribbon') ||
      lower.includes('tie') ||
      lower.includes('belt') ||
      lower.includes('bell') ||
      lower.includes('tassel') ||
      lower.includes('no_') ||
      lower.includes('ruyban')
    ) {
      return { isSecondary: true, type: 'ribbon' };
    }

    // 4. Các mã xương vật lý game MMD / Genshin / Unity SpringBone (Q_*, qd_*, pf_*, pj_*, x_*, F_*, ts_*)
    if (
      /^q_\d+/i.test(lower) ||
      /^qd_\d+/i.test(lower) ||
      /^pf_\d+/i.test(lower) ||
      /^pj_\d+/i.test(lower) ||
      /^x_\d+/i.test(lower) ||
      /^f_\d+/i.test(lower) ||
      /^ts_\d+/i.test(lower) ||
      lower.includes('qd_') ||
      lower.includes('pj_') ||
      lower.includes('pf_') ||
      lower.includes('wing') ||
      lower.includes('ear') ||
      lower.includes('tail')
    ) {
      return { isSecondary: true, type: 'cloth' };
    }

    return { isSecondary: false, type: 'generic' };
  }

  /**
   * Phân tích và tạo danh sách các node xương thứ cấp cho một Model Group
   */
  public static analyzeSecondaryBones(rootGroup: THREE.Object3D): SecondaryBoneChainNode[] {
    const cached = this.secondaryNodesCache.get(rootGroup);
    if (cached) return cached;

    const nodes: SecondaryBoneChainNode[] = [];

    rootGroup.traverse((child) => {
      if ((child as THREE.Bone).isBone) {
        const bone = child as THREE.Bone;
        const check = this.isSecondaryBone(bone.name);

        if (check.isSecondary) {
          // Lưu lại góc xoay nghỉ ban đầu (Đảm bảo luôn là đối tượng THREE.Euler hợp lệ)
          let initRot: THREE.Euler;
          const rawRot = bone.userData.initialRotation;
          if (rawRot instanceof THREE.Euler) {
            initRot = rawRot.clone();
          } else if (rawRot && typeof rawRot.x === 'number') {
            initRot = new THREE.Euler(rawRot.x, rawRot.y, rawRot.z, rawRot.order || 'XYZ');
          } else if (rawRot && typeof (rawRot as any)._x === 'number') {
            initRot = new THREE.Euler(
              (rawRot as any)._x,
              (rawRot as any)._y,
              (rawRot as any)._z,
              (rawRot as any)._order || 'XYZ'
            );
          } else {
            initRot = bone.rotation.clone();
          }
          bone.userData.initialRotation = initRot;

          let initPos: THREE.Vector3;
          const rawPos = bone.userData.initialPosition;
          if (rawPos instanceof THREE.Vector3) {
            initPos = rawPos.clone();
          } else if (rawPos && typeof rawPos.x === 'number') {
            initPos = new THREE.Vector3(rawPos.x, rawPos.y, rawPos.z);
          } else {
            initPos = bone.position.clone();
          }
          bone.userData.initialPosition = initPos;

          // Tính độ sâu phân cấp (depth) trong chuỗi xương
          let depth = 0;
          let p = bone.parent;
          while (p && (p as THREE.Bone).isBone) {
            depth++;
            p = p.parent;
          }

          // Tùy chỉnh thông số vật lý theo loại
          let stiffness = 0.85;
          let damping = 0.82;
          let swayAmp = 0.08;
          let swayFreq = 1.6;

          if (check.type === 'hair') {
            stiffness = 0.90;
            damping = 0.85;
            swayAmp = 0.06 + Math.min(depth * 0.02, 0.08);
            swayFreq = 2.2;
          } else if (check.type === 'cloth') {
            stiffness = 0.78;
            damping = 0.80;
            swayAmp = 0.10 + Math.min(depth * 0.03, 0.12);
            swayFreq = 1.4;
          } else if (check.type === 'ribbon') {
            stiffness = 0.82;
            damping = 0.84;
            swayAmp = 0.12 + Math.min(depth * 0.04, 0.15);
            swayFreq = 2.0;
          }

          nodes.push({
            bone,
            name: bone.name,
            type: check.type,
            depth,
            initialRotation: initRot.clone(),
            currentVelocity: new THREE.Euler(0, 0, 0),
            currentOffset: new THREE.Euler(0, 0, 0),
            swayFrequency: swayFreq,
            swayAmplitude: swayAmp,
            damping,
            stiffness,
          });
        }
      }
    });

    this.secondaryNodesCache.set(rootGroup, nodes);
    return nodes;
  }

  /**
   * Cập nhật chuyển động vật lý thứ cấp cho toàn bộ xương tóc và trang phục theo thời gian thực (60 FPS)
   */
  public static update(
    rootGroup: THREE.Object3D,
    timeInSeconds: number,
    delta: number,
    options: SecondaryPhysicsOptions = { enabled: true, intensity: 1.0 }
  ): void {
    if (!options.enabled || options.intensity <= 0.001) return;

    const nodes = this.analyzeSecondaryBones(rootGroup);
    if (nodes.length === 0) return;

    const intensity = Math.min(Math.max(options.intensity, 0.1), 2.5);
    const clampedDelta = Math.min(delta, 0.05);

    // Tính toán sóng vật lý dao động đa hài (Multi-Harmonic Procedural Waves)
    const t = timeInSeconds;

    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      const { bone, initialRotation, type, depth } = node;

      const phase = (i % 7) * 0.42 + depth * 0.35;
      const freq = node.swayFrequency;

      // 1. Dao động gió & nhịp thở lan truyền (Propagating Wave)
      let targetX = 0;
      let targetY = 0;
      let targetZ = 0;

      if (type === 'hair') {
        // Tóc đung đưa nhẹ theo luồng gió và nhịp thở, có độ bồng bềnh
        const hairWave = Math.sin(t * freq + phase) * node.swayAmplitude * intensity;
        const hairMicro = Math.sin(t * 3.5 + phase * 2) * 0.02 * intensity;
        targetX = hairWave * 0.7 + hairMicro;
        targetZ = Math.cos(t * freq * 0.8 + phase) * node.swayAmplitude * 0.5 * intensity;
        targetY = Math.sin(t * 1.2 + phase) * 0.02 * intensity;
      } else if (type === 'cloth') {
        // Áo choàng / Váy / Tà áo dài đung đưa mềm mại rủ xuống dưới tác dụng trọng lực và quán tính
        const clothWaveX = Math.sin(t * freq + phase) * node.swayAmplitude * intensity;
        const clothWaveZ = Math.cos(t * freq * 0.9 + phase) * node.swayAmplitude * 0.8 * intensity;
        const gravitySag = 0.03 * intensity; // Độ rủ tự nhiên
        targetX = clothWaveX + gravitySag;
        targetZ = clothWaveZ;
        targetY = Math.sin(t * 1.8 + phase) * 0.03 * intensity;
      } else {
        // Ruy băng, nơ, dây trang sức
        const ribbonWave = Math.sin(t * freq * 1.3 + phase) * node.swayAmplitude * intensity * 1.2;
        targetX = ribbonWave;
        targetZ = Math.cos(t * freq + phase) * node.swayAmplitude * intensity;
      }

      // 2. Damping lò xo (Spring-Damper Interpolation)
      const lerpFactor = Math.min(1.0, clampedDelta * 14 * node.stiffness);
      node.currentOffset.x += (targetX - node.currentOffset.x) * lerpFactor;
      node.currentOffset.y += (targetY - node.currentOffset.y) * lerpFactor;
      node.currentOffset.z += (targetZ - node.currentOffset.z) * lerpFactor;

      // 3. Áp dụng góc xoay mới trực tiếp lên Bone
      bone.rotation.set(
        initialRotation.x + node.currentOffset.x,
        initialRotation.y + node.currentOffset.y,
        initialRotation.z + node.currentOffset.z
      );
    }
  }

  /**
   * Đếm tổng số khớp xương thứ cấp đã nhận diện được
   */
  public static getSecondaryBonesCount(rootGroup: THREE.Object3D): { totalSecondary: number; hairCount: number; clothCount: number } {
    const nodes = this.analyzeSecondaryBones(rootGroup);
    let hairCount = 0;
    let clothCount = 0;
    nodes.forEach((n) => {
      if (n.type === 'hair') hairCount++;
      else clothCount++;
    });
    return { totalSecondary: nodes.length, hairCount, clothCount };
  }
}
