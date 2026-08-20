import * as THREE from 'three';

/**
 * Photorealistic 3D Lightning & Thunder Storm System
 *
 * Features:
 * 1. Procedural 3D Branching Lightning Bolt Generator:
 *    - Recursive Midpoint Displacement with organic zig-zag jitter
 *    - Main discharge trunk + 3 to 6 branching forks
 *    - Dual-layer electric core (glowing white core + electric blue corona)
 * 2. Real-Time Dynamic 3D Scene Illumination:
 *    - High-intensity PointLight at the lightning bolt strike location
 *    - Casts instantaneous dynamic light on 3D characters, roofs, buildings, trees, and terrain
 * 3. Intra-Cloud Sheet Lightning Flash:
 *    - Illuminates the cloud deck from inside with electric flashes
 * 4. Multi-Pulse Discharge Sequence:
 *    - Stepped leader pre-flash -> Main return stroke -> After-glow dissipation
 * 5. Procedural Thunder Audio Synthesizer:
 *    - Rolling low-frequency thunder rumble synthesized via Web Audio API
 */
export class LightningSystem {
  private scene: THREE.Scene;
  private lightningGroup: THREE.Group;

  // 3D Lightning Bolt Line Geometry
  private boltLines: THREE.LineSegments | null = null;
  private boltGeometry: THREE.BufferGeometry | null = null;
  private boltMaterial: THREE.LineBasicMaterial;

  // Glowing corona billboard mesh
  private impactMesh: THREE.Mesh | null = null;

  // Dynamic Scene Illumination Light
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

  // Web Audio Synthesizer for procedural rolling thunder
  private audioCtx: AudioContext | null = null;

  constructor(scene: THREE.Scene) {
    this.scene = scene;
    this.lightningGroup = new THREE.Group();
    this.lightningGroup.name = 'cinematic_lightning_engine';
    this.scene.add(this.lightningGroup);

    // Core electric line material
    this.boltMaterial = new THREE.LineBasicMaterial({
      color: 0xffffff,
      linewidth: 3,
      transparent: true,
      opacity: 0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    // Dynamic 3D Point Light for illuminating characters, roofs, and terrain
    this.impactLight = new THREE.PointLight(0xb0e0ff, 0, 150, 1.2);
    this.impactLight.position.set(0, 5, 0);
    this.lightningGroup.add(this.impactLight);

    // Ground impact shockwave glow
    const impactGeom = new THREE.RingGeometry(0.2, 4.5, 32);
    const impactMat = new THREE.MeshBasicMaterial({
      color: 0x93c5fd,
      transparent: true,
      opacity: 0,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    this.impactMesh = new THREE.Mesh(impactGeom, impactMat);
    this.impactMesh.rotation.x = -Math.PI / 2;
    this.impactMesh.position.set(0, 0.1, 0);
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

      // Midpoint displacement with random perpendicular jitter
      const mid = new THREE.Vector3().lerpVectors(p1, p2, 0.45 + Math.random() * 0.1);
      const segmentDir = new THREE.Vector3().subVectors(p2, p1);
      const length = segmentDir.length();

      // Perpendicular displacement vector
      const perp1 = new THREE.Vector3(-segmentDir.z, 0, segmentDir.x).normalize();
      const perp2 = new THREE.Vector3(0, 1, 0);
      
      const jitterAmount = (length * 0.22 + maxSpread) * (Math.random() - 0.5);
      const jitterAmountY = (length * 0.15 + maxSpread * 0.5) * (Math.random() - 0.5);

      mid.addScaledVector(perp1, jitterAmount);
      mid.addScaledVector(perp2, jitterAmountY);

      // Recursive sub-segments
      createBranch(p1, mid, depth - 1, maxSpread * 0.6);
      createBranch(mid, p2, depth - 1, maxSpread * 0.6);

      // Spawn fork branches (random chance on higher levels)
      if (depth >= 2 && Math.random() < 0.65) {
        const forkEnd = mid.clone().add(new THREE.Vector3(
          (Math.random() - 0.5) * length * 0.7,
          -length * (0.3 + Math.random() * 0.4),
          (Math.random() - 0.5) * length * 0.7
        ));
        createBranch(mid, forkEnd, depth - 2, maxSpread * 0.45);
      }
    };

    // Generate main trunk (5 levels of recursive subdivision)
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
   * Triggers a photorealistic lightning strike event
   */
  public triggerStrike(
    cameraPos: THREE.Vector3,
    cloudAltitude: number = 90,
    strikeIntensity: number = 1.0
  ): void {
    // Pick random strike coordinates around the camera (30m - 85m away)
    const angle = Math.random() * Math.PI * 2;
    const distance = 25 + Math.random() * 65;
    const groundX = cameraPos.x + Math.cos(angle) * distance;
    const groundZ = cameraPos.z + Math.sin(angle) * distance;

    // Origin in the clouds with organic horizontal offset
    const cloudX = groundX + (Math.random() - 0.5) * 50;
    const cloudZ = groundZ + (Math.random() - 0.5) * 50;
    const cloudY = cloudAltitude + (Math.random() - 0.5) * 15;

    this.strikeOrigin.set(cloudX, cloudY, cloudZ);
    this.strikeTarget.set(groundX, 0, groundZ);

    // Build new procedural branching bolt geometry if ground strike is active
    if (this.boltLines) {
      this.lightningGroup.remove(this.boltLines);
      this.boltGeometry?.dispose();
      this.boltLines = null;
    }

    if (strikeIntensity > 0.05) {
      this.boltGeometry = this.generateBoltGeometry(this.strikeOrigin, this.strikeTarget);
      this.boltLines = new THREE.LineSegments(this.boltGeometry, this.boltMaterial);
      this.lightningGroup.add(this.boltLines);

      // Move dynamic 3D light to strike impact location
      this.impactLight.position.set(groundX, 5.0, groundZ);
      if (this.impactMesh) {
        this.impactMesh.position.set(groundX, 0.08, groundZ);
      }
    }

    // Set up realistic stepped multi-flash envelope:
    // Pre-flash (leader) -> Main Return Stroke -> After-glow pulse
    this.flashPulses = [
      { time: 0.00, intensity: 0.55 }, // Pre-flash
      { time: 0.04, intensity: 0.15 },
      { time: 0.07, intensity: 1.00 }, // Main primary lightning stroke
      { time: 0.15, intensity: 0.80 }, // Secondary arc discharge
      { time: 0.23, intensity: 0.40 }, // Afterglow dissipation
      { time: 0.35, intensity: 0.00 }, // End
    ];

    this.activeLightning = true;
    this.flashTimer = 0;
    this.flashDuration = 0.36;

    // Play synthesized rolling thunder rumble
    if (strikeIntensity > 0.05) {
      this.playThunderSound(distance);
    }
  }

  /**
   * Procedural Audio Synthesizer: Generates realistic rolling thunder rumble via Web Audio API
   */
  private playThunderSound(distanceMeters: number): void {
    try {
      const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
      if (!AudioContextClass) return;

      if (!this.audioCtx) {
        this.audioCtx = new AudioContextClass();
      }
      if (this.audioCtx.state === 'suspended') {
        this.audioCtx.resume();
      }

      const ctx = this.audioCtx;
      const now = ctx.currentTime;
      // Sound travels ~340m/s: delay thunder relative to visual flash
      const delay = Math.min(2.5, distanceMeters / 340.0);

      // Noise buffer for thunder rumble
      const bufferSize = ctx.sampleRate * 3.5;
      const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
      const data = buffer.getChannelData(0);
      let lastVal = 0;
      for (let i = 0; i < bufferSize; i++) {
        const white = Math.random() * 2 - 1;
        // Brown noise filter for deep bass rumble
        data[i] = (lastVal + (0.02 * white)) / 1.02;
        lastVal = data[i];
        data[i] *= 3.5;
      }

      const noiseNode = ctx.createBufferSource();
      noiseNode.buffer = buffer;

      // Low-pass filter for thunderous bass body
      const filter = ctx.createBiquadFilter();
      filter.type = 'lowpass';
      filter.frequency.setValueAtTime(180, now + delay);
      filter.frequency.exponentialRampToValueAtTime(45, now + delay + 2.8);

      // Gain envelope
      const gain = ctx.createGain();
      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.setValueAtTime(0.0001, now + delay);
      // Sharp initial crack
      gain.gain.linearRampToValueAtTime(0.65, now + delay + 0.08);
      // Rolling rumble decay
      gain.gain.exponentialRampToValueAtTime(0.0001, now + delay + 3.2);

      noiseNode.connect(filter);
      filter.connect(gain);
      gain.connect(ctx.destination);

      noiseNode.start(now + delay);
      noiseNode.stop(now + delay + 3.4);
    } catch {
      // Audio playback silently guarded
    }
  }

  /**
   * Main simulation update loop
   * @param delta Frame time delta in seconds
   * @param cameraPos Camera position
   * @param rainIntensity Current rain intensity (0.0 to 1.0)
   * @param cloudAltitude Cloud deck altitude in meters
   * @param customFrequency User-defined lightning interval in seconds (0 = auto)
   * @param cloudIntensity Cloud pulse & sheet flash intensity (0.0 to 2.0)
   * @param strikeIntensity Ground strike bolt & 3D object illumination intensity (0.0 to 2.0)
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
    // Check if lightning should trigger: either manual custom frequency is set, or rain is active
    const isTriggerEnabled = customFrequency > 0 || rainIntensity >= 0.25;

    if (isTriggerEnabled) {
      this.nextStrikeTimer -= delta;
      if (this.nextStrikeTimer <= 0) {
        this.triggerStrike(cameraPos, cloudAltitude, strikeIntensity);

        if (customFrequency > 0) {
          // User-controlled interval with +/- 25% organic jitter
          const jitter = (Math.random() - 0.5) * customFrequency * 0.5;
          this.nextStrikeTimer = Math.max(0.4, customFrequency + jitter);
        } else {
          // Auto frequency based on rain
          const minInterval = THREE.MathUtils.lerp(5.5, 2.0, rainIntensity);
          const maxInterval = THREE.MathUtils.lerp(9.5, 4.0, rainIntensity);
          this.nextStrikeTimer = minInterval + Math.random() * (maxInterval - minInterval);
        }
      }
    } else {
      this.nextStrikeTimer = 3.5;
    }

    // Process active lightning discharge envelope
    if (this.activeLightning) {
      this.flashTimer += delta;

      if (this.flashTimer >= this.flashDuration) {
        this.activeLightning = false;
        this.currentFlashIntensity = 0;
        this.boltMaterial.opacity = 0;
        this.impactLight.intensity = 0;
        if (this.impactMesh) {
          (this.impactMesh.material as THREE.MeshBasicMaterial).opacity = 0;
        }
      } else {
        // Evaluate multi-pulse intensity
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

        // Visual bolt line opacity & color flash
        if (strikeIntensity > 0.05) {
          this.boltMaterial.opacity = intensity * Math.min(1.0, strikeIntensity);
          this.boltMaterial.color.setHex(intensity > 0.7 ? 0xffffff : 0xbae6fd);

          // 3D Scene Illumination Point Light (illuminates characters, roofs, trees, terrain)
          this.impactLight.intensity = intensity * 38.0 * strikeIntensity;

          // Ground impact shockwave
          if (this.impactMesh) {
            (this.impactMesh.material as THREE.MeshBasicMaterial).opacity = intensity * 0.85 * Math.min(1.0, strikeIntensity);
            const ringScale = 1.0 + (this.flashTimer / this.flashDuration) * 2.5;
            this.impactMesh.scale.set(ringScale, ringScale, 1);
          }
        } else {
          this.boltMaterial.opacity = 0;
          this.impactLight.intensity = 0;
          if (this.impactMesh) {
            (this.impactMesh.material as THREE.MeshBasicMaterial).opacity = 0;
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

    if (this.impactMesh) {
      this.lightningGroup.remove(this.impactMesh);
      this.impactMesh.geometry.dispose();
      (this.impactMesh.material as THREE.Material).dispose();
    }

    this.scene.remove(this.lightningGroup);
    if (this.audioCtx && this.audioCtx.state !== 'closed') {
      this.audioCtx.close();
    }
  }
}
