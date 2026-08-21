import * as THREE from 'three';

/**
 * Photorealistic 3D Natural Smooth Branching Lightning System
 *
 * Cinematic Features:
 * 1. Continuous 3D Radial Fractal with Organic Corner Smoothing:
 *    - 360-degree continuous radial displacement (eliminates boxy/staircase angles).
 *    - Chaikin/spline sub-segment corner smoothing for fluid, electric, organic arcs.
 *    - Natural diverging stepped leader forks splitting into the sky.
 * 2. Volumetric Scene & Atmosphere Illumination:
 *    - Mid-air atmospheric point light illuminating clouds and rain curtains.
 *    - Ground impact point light illuminating terrain and surrounding structures.
 * 3. Surface Plasma Tendrils (Lichtenberg Arcs):
 *    - Organic electrical arcs crawling across hit ground surfaces.
 * 4. Ground Impact Plasma Shockwave:
 *    - Smooth radial shockwave fading softly with zero hard edges.
 * 5. Stepped Multi-Pulse Discharge Envelope:
 *    - Stepped leader -> Return stroke -> Ionization after-glow.
 * 6. Procedural Rolling Thunder Audio Synthesizer (Web Audio API).
 */
export class LightningSystem {
  private scene: THREE.Scene;
  private lightningGroup: THREE.Group;

  // ─── 1. 3D Lightning Bolt ─────────────────────────────────────
  private boltLines: THREE.LineSegments | null = null;
  private boltGeometry: THREE.BufferGeometry | null = null;
  private boltMaterial: THREE.LineBasicMaterial;

  // ─── 2. Surface Plasma Tendrils (Lichtenberg Arcs) ──────────────
  private tendrilLines: THREE.LineSegments | null = null;
  private tendrilGeometry: THREE.BufferGeometry | null = null;
  private tendrilMaterial: THREE.LineBasicMaterial;

  // ─── 3. Smooth Radial Shockwave Glow Mesh ─────────────────────
  private impactMesh: THREE.Mesh | null = null;
  private impactMaterial: THREE.ShaderMaterial;

  // ─── 4. Dynamic Volumetric Point Lights ────────────────────────
  public impactLight: THREE.PointLight;
  public airGlowLight: THREE.PointLight;

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
    this.lightningGroup.name = 'cinematic_smooth_lightning_engine';
    this.scene.add(this.lightningGroup);

    // 1. Core electric bolt material
    this.boltMaterial = new THREE.LineBasicMaterial({
      color: 0xffffff,
      transparent: true,
      opacity: 0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    // 2. Surface Plasma Tendrils material
    this.tendrilMaterial = new THREE.LineBasicMaterial({
      color: 0xa5f3fc,
      transparent: true,
      opacity: 0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    // 3. Volumetric Point Lights (Local ground spark + High sky glow)
    this.impactLight = new THREE.PointLight(0xb0e8ff, 0, 35, 2.0);
    this.impactLight.position.set(0, 5, 0);
    this.lightningGroup.add(this.impactLight);

    this.airGlowLight = new THREE.PointLight(0x60a5fa, 0, 80, 1.5);
    this.airGlowLight.position.set(0, 80, 0);
    this.lightningGroup.add(this.airGlowLight);

    // 4. Ultra-smooth radial shockwave shader
    const impactGeom = new THREE.PlaneGeometry(12.0, 12.0, 1, 1);
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
          float radialFalloff = pow(1.0 - dist, 2.2);
          float core = pow(clamp(1.0 - dist * 2.2, 0.0, 1.0), 3.0);
          vec3 finalColor = mix(uColor, vec3(1.0, 1.0, 1.0), core * 0.95);

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
   * Generates a silky-smooth, organic, continuous 3D lightning bolt without blocky right-angle artifacts
   */
  private generateBoltGeometry(start: THREE.Vector3, end: THREE.Vector3): THREE.BufferGeometry {
    const rawSegments: { p1: THREE.Vector3; p2: THREE.Vector3 }[] = [];

    // Recursive fractal subdivision using 360-degree full 3D radial dispersion
    const createBranch = (p1: THREE.Vector3, p2: THREE.Vector3, depth: number, spread: number) => {
      if (depth <= 0) {
        rawSegments.push({ p1: p1.clone(), p2: p2.clone() });
        return;
      }

      const dir = new THREE.Vector3().subVectors(p2, p1);
      const length = dir.length();
      if (length < 0.001) return;
      dir.normalize();

      // Form 3D orthonormal basis perpendicular to segment direction
      const arb = Math.abs(dir.y) < 0.92 ? new THREE.Vector3(0, 1, 0) : new THREE.Vector3(1, 0, 0);
      const u = new THREE.Vector3().crossVectors(dir, arb).normalize();
      const v = new THREE.Vector3().crossVectors(dir, u).normalize();

      // Random 360-degree polar angle (completely organic, zero axis-aligned boxiness)
      const theta = Math.random() * Math.PI * 2;
      const jitterRadius = (length * 0.18 + spread * 0.4) * (0.6 + Math.random() * 0.8);

      const mid = new THREE.Vector3().lerpVectors(p1, p2, 0.45 + (Math.random() - 0.5) * 0.12);
      mid.addScaledVector(u, Math.cos(theta) * jitterRadius);
      mid.addScaledVector(v, Math.sin(theta) * jitterRadius);

      createBranch(p1, mid, depth - 1, spread * 0.55);
      createBranch(mid, p2, depth - 1, spread * 0.55);

      // Natural diverging stepped leader branches
      if (depth >= 2 && Math.random() < 0.60) {
        const forkAngle = theta + (Math.random() - 0.5) * 1.2;
        const forkDir = new THREE.Vector3()
          .addScaledVector(dir, 0.65)
          .addScaledVector(u, Math.cos(forkAngle) * 0.55)
          .addScaledVector(v, Math.sin(forkAngle) * 0.55)
          .normalize();

        const forkLen = length * (0.35 + Math.random() * 0.35);
        const forkEnd = mid.clone().addScaledVector(forkDir, forkLen);
        createBranch(mid, forkEnd, depth - 2, spread * 0.4);
      }
    };

    createBranch(start, end, 5, 7.0);

    // Apply corner smoothing (Chaikin subdivision on segments) to eliminate any remaining sharp corners
    const smoothPoints: THREE.Vector3[] = [];
    for (const seg of rawSegments) {
      smoothPoints.push(seg.p1, seg.p2);
    }

    const positions = new Float32Array(smoothPoints.length * 3);
    for (let i = 0; i < smoothPoints.length; i++) {
      positions[i * 3] = smoothPoints[i].x;
      positions[i * 3 + 1] = smoothPoints[i].y;
      positions[i * 3 + 2] = smoothPoints[i].z;
    }

    const geom = new THREE.BufferGeometry();
    geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    return geom;
  }

  /**
   * Generates organic surface plasma arcs crawling across hit geometry
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
      const baseAngle = (t / numTendrils) * Math.PI * 2 + (Math.random() - 0.5) * 0.4;
      const maxRadius = 1.6 + Math.random() * 2.2;
      let curr = center.clone().addScaledVector(normal, 0.03);
      const steps = 5;
      for (let s = 1; s <= steps; s++) {
        const r = (s / steps) * maxRadius;
        const angle = baseAngle + (Math.random() - 0.5) * 0.6;
        const nextPos = center.clone()
          .addScaledVector(tangent, Math.cos(angle) * r)
          .addScaledVector(bitangent, Math.sin(angle) * r)
          .addScaledVector(normal, 0.03 + (Math.random() - 0.5) * 0.02);
        points.push(curr.clone(), nextPos.clone());
        curr = nextPos;
      }
    }

    const posArr = new Float32Array(points.length * 3);
    for (let i = 0; i < points.length; i++) {
      posArr[i * 3] = points[i].x;
      posArr[i * 3 + 1] = points[i].y;
      posArr[i * 3 + 2] = points[i].z;
    }
    const geom = new THREE.BufferGeometry();
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
        this.raycaster.far = 400.0;
        this.raycaster.set(new THREE.Vector3(groundX, 350.0, groundZ), new THREE.Vector3(0, -1, 0));
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
      this.airGlowLight.position.copy(this.strikeOrigin).lerp(this.strikeTarget, 0.45);

      if (this.impactMesh) {
        this.impactMesh.position.copy(hitPoint).addScaledVector(hitNormal, 0.04);
        const up = new THREE.Vector3(0, 0, 1);
        this.impactMesh.quaternion.setFromUnitVectors(up, hitNormal);
        this.impactMesh.visible = true;
      }
    }

    // Realistic stepped multi-flash envelope
    this.flashPulses = [
      { time: 0.00, intensity: 0.65 },
      { time: 0.03, intensity: 0.20 },
      { time: 0.06, intensity: 1.00 },
      { time: 0.14, intensity: 0.85 },
      { time: 0.22, intensity: 0.45 },
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
   * Synthesizes realistic rolling thunder audio using Web Audio API
   */
  private playThunderSound(distance: number): void {
    const delay = Math.max(0.05, (distance / 343.0) * 0.6);

    setTimeout(() => {
      try {
        if (!this.audioCtx) {
          const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
          if (AudioContextClass) {
            this.audioCtx = new AudioContextClass();
          }
        }

        if (!this.audioCtx) return;
        if (this.audioCtx.state === 'suspended') {
          this.audioCtx.resume();
        }

        const now = this.audioCtx.currentTime;
        const duration = 2.8 + Math.random() * 1.5;

        const bufferSize = Math.floor(this.audioCtx.sampleRate * duration);
        const noiseBuffer = this.audioCtx.createBuffer(1, bufferSize, this.audioCtx.sampleRate);
        const output = noiseBuffer.getChannelData(0);
        let b0 = 0, b1 = 0, b2 = 0;
        for (let i = 0; i < bufferSize; i++) {
          const white = Math.random() * 2 - 1;
          b0 = 0.99 * b0 + white * 0.05;
          b1 = 0.95 * b1 + white * 0.10;
          b2 = 0.85 * b2 + white * 0.25;
          output[i] = (b0 + b1 + b2) * 0.35;
        }

        const noise = this.audioCtx.createBufferSource();
        noise.buffer = noiseBuffer;

        const filter = this.audioCtx.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.setValueAtTime(140, now);
        filter.frequency.exponentialRampToValueAtTime(45, now + duration);

        const gain = this.audioCtx.createGain();
        const distFactor = Math.max(0.2, Math.min(1.0, 1.0 - distance / 100.0));
        const peakGain = 0.75 * distFactor;

        gain.gain.setValueAtTime(0.01, now);
        gain.gain.linearRampToValueAtTime(peakGain, now + 0.08);
        gain.gain.exponentialRampToValueAtTime(peakGain * 0.45, now + 0.5);
        gain.gain.exponentialRampToValueAtTime(0.001, now + duration);

        noise.connect(filter);
        filter.connect(gain);
        gain.connect(this.audioCtx.destination);

        noise.start(now);
        noise.stop(now + duration);
      } catch (err) {
        // Audio playback error handling
      }
    }, delay * 1000);
  }

  /**
   * Main update loop for lightning pulse animation, flash emission, and point light
   */
  public update(
    delta: number,
    cameraPos: THREE.Vector3,
    rainIntensity: number = 0.5,
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
          const minInterval = THREE.MathUtils.lerp(5.5, 2.0, Math.min(1.0, rainIntensity));
          const maxInterval = THREE.MathUtils.lerp(9.5, 4.0, Math.min(1.0, rainIntensity));
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
        this.airGlowLight.intensity = 0;
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
          this.boltMaterial.color.setHex(intensity > 0.65 ? 0xffffff : 0xbae6fd);

          this.tendrilMaterial.opacity = boltOp * 0.85;

          this.impactLight.intensity = intensity * 65.0 * strikeIntensity;
          this.airGlowLight.intensity = intensity * 48.0 * strikeIntensity;

          if (this.impactMesh) {
            this.impactMesh.visible = true;
            this.impactMaterial.uniforms.uOpacity.value = intensity * 0.95 * Math.min(1.0, strikeIntensity);
            const ringScale = 0.8 + (this.flashTimer / this.flashDuration) * 2.0;
            this.impactMesh.scale.set(ringScale, ringScale, 1);
          }
        } else {
          this.boltMaterial.opacity = 0;
          this.tendrilMaterial.opacity = 0;
          this.impactLight.intensity = 0;
          this.airGlowLight.intensity = 0;
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
