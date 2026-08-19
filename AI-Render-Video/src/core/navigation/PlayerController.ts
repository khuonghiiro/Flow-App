import * as THREE from 'three';
import { VRMAvatar } from '../actors/VRMAvatar';
import { ActorAnimator } from '../actors/ActorAnimator';

export class PlayerController {
  private keys: { [key: string]: boolean } = {};
  public controlledActorId: string | null = null;
  public velocityY: number = 0;
  public isGrounded: boolean = true;
  
  private moveSpeed = 4.0;
  private jumpForce = 7.5;
  private gravity = 20.0;

  constructor() {
    this.handleKeyDown = this.handleKeyDown.bind(this);
    this.handleKeyUp = this.handleKeyUp.bind(this);
    window.addEventListener('keydown', this.handleKeyDown);
    window.addEventListener('keyup', this.handleKeyUp);
  }

  public dispose() {
    window.removeEventListener('keydown', this.handleKeyDown);
    window.removeEventListener('keyup', this.handleKeyUp);
  }

  private handleKeyDown(e: KeyboardEvent) {
    // Prevent default scrolling for movement keys
    if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', ' '].includes(e.key)) {
      if (document.activeElement?.tagName !== 'INPUT' && document.activeElement?.tagName !== 'TEXTAREA') {
        e.preventDefault();
      }
    }
    this.keys[e.code] = true;
  }

  private handleKeyUp(e: KeyboardEvent) {
    this.keys[e.code] = false;
  }

  public update(delta: number, avatar: VRMAvatar, animator: ActorAnimator, camera: THREE.Camera, colliders: THREE.Object3D[] = []) {
    if (!avatar.rootObject) return;

    let moveX = 0;
    let moveZ = 0;

    if (this.keys['KeyW'] || this.keys['ArrowUp']) moveZ -= 1;
    if (this.keys['KeyS'] || this.keys['ArrowDown']) moveZ += 1;
    if (this.keys['KeyA'] || this.keys['ArrowLeft']) moveX -= 1;
    if (this.keys['KeyD'] || this.keys['ArrowRight']) moveX += 1;

    // Movement logic
    if (moveX !== 0 || moveZ !== 0) {
      // Normalize vector for diagonal movement
      const length = Math.sqrt(moveX * moveX + moveZ * moveZ);
      moveX /= length;
      moveZ /= length;

      // Calculate movement relative to camera yaw
      const camEuler = new THREE.Euler().setFromQuaternion(camera.quaternion, 'YXZ');
      const camYaw = camEuler.y;

      const rotMatrix = new THREE.Matrix4().makeRotationY(camYaw);
      const moveDir = new THREE.Vector3(moveX, 0, moveZ).applyMatrix4(rotMatrix).normalize();

      let finalMove = moveDir.clone();

      // Horizontal Wall Collision Detection & Sliding (exclude ground plane)
      const wallColliders = colliders.filter((c) => c.name !== 'fast_physics_ground_plane');
      if (wallColliders.length > 0 && finalMove.lengthSq() > 0) {
        const moveNorm = finalMove.clone().normalize();
        const right = new THREE.Vector3(moveNorm.z, 0, -moveNorm.x).multiplyScalar(0.25);
        const left = right.clone().negate();

        // Check 3 heights: 0.4 (Feet), 0.9 (Waist), 1.4 (Shoulders)
        const heights = [0.4, 0.9, 1.4];
        let slideNormal = new THREE.Vector3();
        let hitFound = false;

        for (const height of heights) {
          const rayOrigin = new THREE.Vector3(
            avatar.rootObject.position.x,
            avatar.rootObject.position.y + height,
            avatar.rootObject.position.z
          );
          
          const origins = [
            rayOrigin,
            rayOrigin.clone().add(right),
            rayOrigin.clone().add(left)
          ];
          
          for (const origin of origins) {
            const raycaster = new THREE.Raycaster(origin, moveNorm, 0, 0.45);
            const hits = raycaster.intersectObjects(wallColliders, false);
            
            // Only block movement if the hit surface is a steep wall (worldNormal.y <= 0.6)
            const validWallHit = hits.find(h => {
              if (!h.face) return false;
              const normalMatrix = new THREE.Matrix3().getNormalMatrix(h.object.matrixWorld);
              const worldNormal = h.face.normal.clone().applyMatrix3(normalMatrix).normalize();
              return worldNormal.y <= 0.6;
            });
            
            if (validWallHit && validWallHit.face) {
               const normalMatrix = new THREE.Matrix3().getNormalMatrix(validWallHit.object.matrixWorld);
               slideNormal = validWallHit.face.normal.clone().applyMatrix3(normalMatrix).normalize();
               hitFound = true;
               break;
            }
          }
          if (hitFound) break;
        }

        if (hitFound) {
          const horizontalNormal = slideNormal.clone();
          horizontalNormal.y = 0;
          if (horizontalNormal.lengthSq() > 0.001) {
            horizontalNormal.normalize();
            const dot = finalMove.dot(horizontalNormal);
            if (dot < 0) {
              finalMove.sub(horizontalNormal.multiplyScalar(dot));
            }
          } else {
            finalMove.set(0, 0, 0);
          }
        }
      }

      const isRunning = this.keys['ShiftLeft'];
      const speed = isRunning ? this.moveSpeed * 1.5 : this.moveSpeed;

      avatar.rootObject.position.x += finalMove.x * speed * delta;
      avatar.rootObject.position.z += finalMove.z * speed * delta;

      // Update rotation
      const targetRotation = Math.atan2(moveDir.x, moveDir.z);
      let currentRotation = avatar.rootObject.rotation.y;
      
      let diff = targetRotation - currentRotation;
      while (diff < -Math.PI) diff += Math.PI * 2;
      while (diff > Math.PI) diff -= Math.PI * 2;

      avatar.rootObject.rotation.y += diff * 10 * delta;

      animator.setAction(isRunning ? 'run' : 'walk');
    } else {
      // Action triggers (Emotes)
      if (this.keys['Digit1']) {
        animator.setAction('talk_gesture');
      } else if (this.keys['Digit2']) {
        animator.setAction('block_defend');
      } else if (this.keys['Digit3']) {
        animator.setAction('sit');
      } else if (this.keys['Digit4']) {
        animator.setAction('heavy_slash_combo');
      } else {
        if (this.isGrounded) {
          animator.setAction('idle');
        }
      }
    }

    // Jump logic
    if (this.keys['Space'] && this.isGrounded) {
      this.velocityY = this.jumpForce;
      this.isGrounded = false;
      animator.setAction('dodge');
    }

    // Apply gravity
    if (!this.isGrounded) {
      this.velocityY -= this.gravity * delta;
      
      // Upward Ceiling Collision Check when jumping up
      const ceilingColliders = colliders.filter((c) => c.name !== 'fast_physics_ground_plane');
      if (this.velocityY > 0 && ceilingColliders.length > 0) {
        const headOrigin = new THREE.Vector3(
          avatar.rootObject.position.x,
          avatar.rootObject.position.y + 1.0,
          avatar.rootObject.position.z
        );
        const upRay = new THREE.Raycaster(headOrigin, new THREE.Vector3(0, 1, 0), 0, 0.6);
        const hits = upRay.intersectObjects(ceilingColliders, false);
        const ceilingHit = hits.find(h => {
            if (!h.face) return false;
            const normalMatrix = new THREE.Matrix3().getNormalMatrix(h.object.matrixWorld);
            const worldNormal = h.face.normal.clone().applyMatrix3(normalMatrix).normalize();
            return worldNormal.y < -0.5;
        });
        
        if (ceilingHit) {
           this.velocityY = 0;
           avatar.rootObject.position.y = ceilingHit.point.y - 1.6;
        }
      }

      avatar.rootObject.position.y += this.velocityY * delta;

      // Phương án 3: Rơi tự do khỏi mép bản đồ và tự động hồi sinh (Respawn)
      if (avatar.rootObject.position.y < -12.0) {
        const spawn = avatar.config.spawn_point || [0, 0, 0];
        avatar.rootObject.position.set(...spawn);
        this.velocityY = 0;
        this.isGrounded = true;
        animator.setAction('idle');
      }
    }
  }
}
