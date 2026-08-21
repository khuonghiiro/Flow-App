import * as THREE from 'three';

/**
 * Photorealistic 3D Lightning & Thunder Storm System
 *
 * Cinematic Features:
 * 1. Raycast Surface Conformation:
 *    - Detects exact hit surface (ground, tree canopy, rock, roof, character).
 *    - Aligns impact shockwave & plasma glow along the surface normal.
 * 2. Ultra-Smooth Radial Falloff Shader:
 *    - Gaussian-like smooth radial falloff (no hard edges).
 *    - Blinding white core fading smoothly to cyan/blue glow.
 * 3. Surface Plasma Tendrils (Lichtenberg Arcs):
 *    - Multi-branch electric arcs crawling across hit surfaces.
 * 4. Multi-Pulse Discharge Envelope:
 *    - Stepped leader -> Return stroke -> After-glow dissipation.
 * 5. Procedural Rolling Thunder Synthesizer (Web Audio API).
 */
export class LightningSystem {
  private scene: THREE.Scene;
  private lightningGroup: THREE.Group;

  // 3D Lightning Bolt
  private boltLines: THREE.LineSegments | null = null;
  private boltGeometry: THREE.BufferGeometry | null = null;
  private boltMaterial: THREE.LineBasicMaterial;

  // Surface Plasma Tendrils (Lichtenberg Arcs)
  private tendrilLines: THREE.LineSegments | null = null;
  private tendrilGeometry: THREE.BufferGeometry | null = null;
  private tendrilMaterial: THREE.LineBasicMaterial;

  // Smooth Radial Shockwave Glow Mesh
  private impactMesh: THREE.Mesh | null = null;
  private impactMaterial: THREE.ShaderMaterial;

  // Dynamic Scene Illumination Point Light
  public impactLight: THREE.PointLight;

  // Flash state & timers
  private activeLightning: boolean = false;
  private flashTimer: number = 0;
  private flashDuration: number = 0;
  private nextStrikeTimer: number = 4.0;
  private currentFlashIntensity: number = 0;
  private strikeOrigin: THREE.Vector3 = new THREE.Vector3();
  private strikeTarget: THREE.Vector3 = new THREE.Vector3();

  // Multi-pulse flash envelope
  private flashPulses: { time: number; intensity: number }[] = [];

  // Raycaster for surface detection
  private raycaster: THREE.Raycaster = new THREE.Raycaster();

  // Web Audio Synthesizer
  private audioCtx: AudioContext | null = null;

  constructor(scene: THREE.Scene) {
    this.scene = scene;
    this.lightningGroup = new THREE.Group();
    this.lightningGroup.name = 'cinematic_lightning_engine';
    this.scene.add(this.lightningGroup);

    // 1. Core electric bolt material
    this.boltMaterial = new THREE.LineBasicMaterial({
      color: 0xffffff,
      linewidth: 3,
      transparent: true,
      opacity: 0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    // 2. Surface plasma tendrils material
    this.tendrilMaterial = new THREE.LineBasicMaterial({
      color: 0xa5f3fc,
      linewidth: 2,
      transparent: true,
      opacity: 0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    // 3. Dynamic 3D Point Light
    this.impactLight = new THREE.PointLight(0xb0e0ff, 0, 160, 1.2);
    this.impactLight.position.set(0, 5, 0);
    this.lightningGroup.add(this.impactLight);

    // 4. Ultra-smooth radial shockwave shader (No hard edges, smooth quadratic/gaussian falloff)
    const impactGeom = new THREE.PlaneGeometry(10.0, 10.0, 1, 1);
    this.impactMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uColor: { value: new THREE.Color(0x38bdf8) },
        uOpacity: { value: 0.0 },
      },
      vertexShader: `
        varying vec2 vUv;
        void main() {
          vUv = uv;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 uColor;
        uniform float uOpacity;
        varying vec2 vUv;
        void main() {
          vec2 center = vUv - vec2(0.5);
          float dist = length(center) * 2.0;
          if (dist >= 1.0) discard;

          // Smooth radial falloff: bright hot center, fading seamlessly to transparent
          float radialFalloff = pow(1.0 - dist, 2.4);
          float core = pow(clamp(1.0 - dist * 2.0, 0.0, 1.0), 3.5);
          vec3 finalColor = mix(uColor, vec3(1.0, 1.0, 1.0), core * 0.9);

          float alpha = radialFalloff * uOpacity;
          gl_FragColor = vec4(finalColor * alpha, alpha);
        }
      `,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      side: THREE.DoubleSide,
    });

    this.impactMesh = new THREE.Mesh(impactGeom, this.impactMaterial);
    this.impactMesh.visible = false;
    this.lightningGroup.add(this.impactMesh);
  }

  /**
   * Generates a procedural branching 3D lightning bolt between start and end vectors
   */
  private generateBoltGeometry(start: THREE.Vector3, end: THREE.Vector3): THREE.BufferGeometry {
    const points: THREE.Vector3[] = [];

    const createBranch = (p1: THREE.Vector3, p2: THREE.Vector3, depth: number, maxSpread: number) => {
      if (depth <= 0) {
        points.push(p1.clone(), p2.clone());
        return;
      }

      const mid = new THREE.Vector3().lerpVectors(p1, p2, 0.45 + Math.random() * 0.1);
      const segmentDir = new THREE.Vector3().subVectors(p2, p1);
      const length = segmentDir.length();

      const perp1 = new THREE.Vector3(-segmentDir.z, 0, segmentDir.x).normalize();
      const perp2 = new THREE.Vector3(0, 1, 0);

      const jitterAmount = (length * 0.22 + maxSpread) * (Math.random() - 0.5);
      const jitterAmountY = (length * 0.15 + maxSpread * 0.5) * (Math.random() - 0.5);

      mid.addScaledVector(perp1, jitterAmount);
      mid.addScaledVector(perp2, jitterAmountY);

      createBranch(p1, mid, depth - 1, maxSpread * 0.6);
      createBranch(mid, p2, depth - 1, maxSpread * 0.6);

      if (depth >= 2 && Math.random() < 0.65) {
        const forkEnd = mid.clone().add(new THREE.Vector3(
          (Math.random() - 0.5) * length * 0.7,
          -length * (0.3 + Math.random() * 0.4),
          (Math.random() - 0.5) * length * 0.7
        ));
        createBranch(mid, forkEnd, depth - 2, maxSpread * 0.45);
      }
    };

    createBranch(start, end, 5, 8.0);

    const positions = new Float32Array(points.length * 3);
    for (let i = 0; i < points.length; i++) {
      positions[i * 3] = points[i].x;
      positions[i * 3 + 1] = points[i].y;
      positions[i * 3 + 2] = points[i].z;
    }

    const geom = new THREE.BufferGeometry();
    geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    return geom;
  }

  /**
   * Generates surface plasma arcs (Lichtenberg tendrils) crawling across hit geometry
   */
  private generateSurfaceTendrils(center: THREE.Vector3, normal: THREE.Vector3): THREE.BufferGeometry {
    const points: THREE.Vector3[] = [];
    const tangent = new THREE.Vector3();
    if (Math.abs(normal.y) < 0.9) {
      tangent.crossVectors(normal, new THREE.Vector3(0, 1, 0)).normalize();
    } else {
      tangent.crossVectors(normal, new THREE.Vector3(1, 0, 0)).normalize();
    }
    const bitangent = new THREE.Vector3().crossVectors(normal, tangent).normalize();

    const numTendrils = 6;
    for (let t = 0; t < numTendrils; t++) {
      const baseAngle = (t / numTendrils) * Math.PI * 2 + (Math.random() - 0.5) * 0.5;
      const maxRadius = 1.8 + Math.random() * 2.4;
      let curr = center.clone().addScaledVector(normal, 0.04);
      const steps = 4;
      for (let s = 1; s <= steps; s++) {
        const r = (s / steps) * maxRadius;
        const angle = baseAngle + (Math.random() - 0.5) * 0.8;
        const nextPos = center.clone()
          .addScaledVector(tangent, Math.cos(angle) * r)
          .addScaledVector(bitangent, Math.sin(angle) * r)
          .addScaledVector(normal, 0.04 + (Math.random() - 0.5) * 0.04);
        points.push(curr.clone(), nextPos.clone());
        curr = nextPos;
      }
    }

    const geom = new THREE.BufferGeometry();
    const posArr = new Float32Array(points.length * 3);
    for (let i = 0; i < points.length; i++) {
      posArr[i * 3] = points[i].x;
      posArr[i * 3 + 1] = points[i].y;
      posArr[i * 3 + 2] = points[i].z;
    }
    geom.setAttribute('position', new THREE.BufferAttribute(posArr, 3));
    return geom;
  }

  /**
   * Triggers a photorealistic lightning strike event
   */
  public triggerStrike(
    cameraPos: THREE.Vector3,
    cloudAltitude: number = 90,
    strikeIntensity: number = 1.0
  ): void {
    const angle = Math.random() * Math.PI * 2;
    const distance = 25 + Math.random() * 65;
    const groundX = cameraPos.x + Math.cos(angle) * distance;
    const groundZ = cameraPos.z + Math.sin(angle) * distance;

    let hitPoint = new THREE.Vector3(groundX, 0.02, groundZ);
    let hitNormal = new THREE.Vector3(0, 1, 0);

    try {
      const targetMeshes: THREE.Mesh[] = [];
      this.scene.traverse((obj) => {
        if ((obj as THREE.Mesh).isMesh && obj.visible && obj.parent !== this.lightningGroup && obj !== this.impactMesh) {
          targetMeshes.push(obj as THREE.Mesh);
        }
      });

      if (targetMeshes.length > 0) {
        this.raycaster.set(new THREE.Vector3(groundX, 200, groundZ), new THREE.Vector3(0, -1, 0));
        const hits = this.raycaster.intersectObjects(targetMeshes, false);
        if (hits && hits.length > 0 && hits[0].point) {
          hitPoint = hits[0].point;
          if (hits[0].face && hits[0].object) {
            hitNormal = hits[0].face.normal.clone().transformDirection(hits[0].object.matrixWorld).normalize();
          }
        }
      }
    } catch (err) {
      // Fail-safe fallback to ground plane
    }

    const cloudX = groundX + (Math.random() - 0.5) * 50;
    const cloudZ = groundZ + (Math.random() - 0.5) * 50;
    const cloudY = cloudAltitude + (Math.random() - 0.5) * 15;

    this.strikeOrigin.set(cloudX, cloudY, cloudZ);
    this.strikeTarget.copy(hitPoint);

    // Rebuild procedural branching bolt geometry
    if (this.boltLines) {
      this.lightningGroup.remove(this.boltLines);
      this.boltGeometry?.dispose();
      this.boltLines = null;
    }
    if (this.tendrilLines) {
      this.lightningGroup.remove(this.tendrilLines);
      this.tendrilGeometry?.dispose();
      this.tendrilLines = null;
    }

    if (strikeIntensity > 0.05) {
      this.boltGeometry = this.generateBoltGeometry(this.strikeOrigin, this.strikeTarget);
      this.boltLines = new THREE.LineSegments(this.boltGeometry, this.boltMaterial);
      this.lightningGroup.add(this.boltLines);

      // Generate surface plasma tendrils crawling on hit surface
      this.tendrilGeometry = this.generateSurfaceTendrils(hitPoint, hitNormal);
      this.tendrilLines = new THREE.LineSegments(this.tendrilGeometry, this.tendrilMaterial);
      this.lightningGroup.add(this.tendrilLines);

      // Align impact light & smooth radial shockwave to hit surface normal
      this.impactLight.position.copy(hitPoint).addScaledVector(hitNormal, 1.2);
      if (this.impactMesh) {
        this.impactMesh.position.copy(hitPoint).addScaledVector(hitNormal, 0.04);
        const up = new THREE.Vector3(0, 0, 1);
        this.impactMesh.quaternion.setFromUnitVectors(up, hitNormal);
        this.impactMesh.visible = true;
      }
    }

    // Realistic stepped multi-flash envelope
    this.flashPulses = [
      { time: 0.00, intensity: 0.55 },
      { time: 0.04, intensity: 0.15 },
      { time: 0.07, intensity: 1.00 },
      { time: 0.15, intensity: 0.80 },
      { time: 0.23, intensity: 0.40 },
      { time: 0.35, intensity: 0.00 },
    ];

    this.activeLightning = true;
    this.flashTimer = 0;
    this.flashDuration = 0.36;

    if (strikeIntensity > 0.05) {
      this.playThunderSound(distance);
    }
  }

  /**
   * Synthesizes procedural rolling thunder rumble via Web Audio API
   */
  private playThunderSound(distanceMeters: number): void {
    try {
      const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
      if (!AudioContextClass) return;
      if (!this.audioCtx) this.audioCtx = new AudioContextClass();
      if (this.audioCtx.state === 'suspended') this.audioCtx.resume();

      const ctx = this.audioCtx;
      const now = ctx.currentTime;
      const delay = Math.min(2.5, distanceMeters / 340.0);

      const bufferSize = ctx.sampleRate * 3.5;
      const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
      const data = buffer.getChannelData(0);
      let lastVal = 0;
      for (let i = 0; i < bufferSize; i++) {
        const white = Math.random() * 2 - 1;
        data[i] = (lastVal + (0.02 * white)) / 1.02;
        lastVal = data[i];
        data[i] *= 3.5;
      }

      const noiseNode = ctx.createBufferSource();
      noiseNode.buffer = buffer;

      const filter = ctx.createBiquadFilter();
      filter.type = 'lowpass';
      filter.frequency.setValueAtTime(180, now + delay);
      filter.frequency.exponentialRampToValueAtTime(45, now + delay + 2.8);

      const gain = ctx.createGain();
      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.setValueAtTime(0.0001, now + delay);
      gain.gain.linearRampToValueAtTime(0.65, now + delay + 0.08);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + delay + 3.2);

      noiseNode.connect(filter);
      filter.connect(gain);
      gain.connect(ctx.destination);

      noiseNode.start(now + delay);
      noiseNode.stop(now + delay + 3.4);
    } catch {}
  }

  /**
   * Main simulation update loop
   */
  public update(
    delta: number,
    cameraPos: THREE.Vector3,
    rainIntensity: number,
    cloudAltitude: number = 90,
    customFrequency: number = 0,
    cloudIntensity: number = 1.0,
    strikeIntensity: number = 1.0
  ): {
    flashIntensity: number;
    cloudFlashIntensity: number;
    sceneFlashIntensity: number;
    strikeOrigin: THREE.Vector3;
    isActive: boolean;
  } {
    const isTriggerEnabled = customFrequency > 0 || rainIntensity >= 0.25;

    if (isTriggerEnabled) {
      this.nextStrikeTimer -= delta;
      if (this.nextStrikeTimer <= 0) {
        this.triggerStrike(cameraPos, cloudAltitude, strikeIntensity);
        if (customFrequency > 0) {
          const jitter = (Math.random() - 0.5) * customFrequency * 0.5;
          this.nextStrikeTimer = Math.max(0.4, customFrequency + jitter);
        } else {
          const minInterval = THREE.MathUtils.lerp(5.5, 2.0, rainIntensity);
          const maxInterval = THREE.MathUtils.lerp(9.5, 4.0, rainIntensity);
          this.nextStrikeTimer = minInterval + Math.random() * (maxInterval - minInterval);
        }
      }
    } else {
      this.nextStrikeTimer = 3.5;
    }

    if (this.activeLightning) {
      this.flashTimer += delta;

      if (this.flashTimer >= this.flashDuration) {
        this.activeLightning = false;
        this.currentFlashIntensity = 0;
        this.boltMaterial.opacity = 0;
        this.tendrilMaterial.opacity = 0;
        this.impactLight.intensity = 0;
        if (this.impactMesh) {
          this.impactMesh.visible = false;
          this.impactMaterial.uniforms.uOpacity.value = 0;
        }
      } else {
        let intensity = 0;
        for (let i = 0; i < this.flashPulses.length - 1; i++) {
          const p1 = this.flashPulses[i];
          const p2 = this.flashPulses[i + 1];
          if (this.flashTimer >= p1.time && this.flashTimer <= p2.time) {
            const factor = (this.flashTimer - p1.time) / (p2.time - p1.time);
            intensity = THREE.MathUtils.lerp(p1.intensity, p2.intensity, factor);
            break;
          }
        }

        this.currentFlashIntensity = intensity;

        if (strikeIntensity > 0.05) {
          const boltOp = intensity * Math.min(1.0, strikeIntensity);
          this.boltMaterial.opacity = boltOp;
          this.boltMaterial.color.setHex(intensity > 0.7 ? 0xffffff : 0xbae6fd);

          this.tendrilMaterial.opacity = boltOp * 0.85;

          this.impactLight.intensity = intensity * 42.0 * strikeIntensity;

          if (this.impactMesh) {
            this.impactMesh.visible = true;
            this.impactMaterial.uniforms.uOpacity.value = intensity * 0.9 * Math.min(1.0, strikeIntensity);
            const ringScale = 0.8 + (this.flashTimer / this.flashDuration) * 1.8;
            this.impactMesh.scale.set(ringScale, ringScale, 1);
          }
        } else {
          this.boltMaterial.opacity = 0;
          this.tendrilMaterial.opacity = 0;
          this.impactLight.intensity = 0;
          if (this.impactMesh) {
            this.impactMesh.visible = false;
            this.impactMaterial.uniforms.uOpacity.value = 0;
          }
        }
      }
    }

    return {
      flashIntensity: this.currentFlashIntensity,
      cloudFlashIntensity: this.currentFlashIntensity * cloudIntensity,
      sceneFlashIntensity: this.currentFlashIntensity * strikeIntensity,
      strikeOrigin: this.strikeOrigin,
      isActive: this.activeLightning,
    };
  }

  public dispose(): void {
    if (this.boltLines) {
      this.lightningGroup.remove(this.boltLines);
      this.boltGeometry?.dispose();
    }
    this.boltMaterial.dispose();

    if (this.tendrilLines) {
      this.lightningGroup.remove(this.tendrilLines);
      this.tendrilGeometry?.dispose();
    }
    this.tendrilMaterial.dispose();

    if (this.impactMesh) {
      this.lightningGroup.remove(this.impactMesh);
      this.impactMesh.geometry.dispose();
      this.impactMaterial.dispose();
    }

    this.scene.remove(this.lightningGroup);
    if (this.audioCtx && this.audioCtx.state !== 'closed') {
      this.audioCtx.close();
    }
  }
}
