import * as THREE from 'three';
import { SocketAttacher } from '../assets/SocketAttacher';
import {
  InventoryItem,
  ActorInventory,
  ItemCategory,
  AttachSocket,
} from '../../types/interactions';

// ============================================================
// InventorySystem - Quản lý túi đồ nhân vật
// ============================================================

export class InventorySystem {
  private inventories: Map<string, ActorInventory> = new Map();

  /** Khởi tạo inventory cho actor */
  public initInventory(actorId: string, maxSlots: number = 20): void {
    if (!this.inventories.has(actorId)) {
      this.inventories.set(actorId, {
        actor_id: actorId,
        max_slots: maxSlots,
        items: [],
        equipped: {},
      });
    }
  }

  /** Lấy inventory của actor */
  public getInventory(actorId: string): ActorInventory | undefined {
    return this.inventories.get(actorId);
  }

  /** Thêm item vào inventory */
  public addItem(actorId: string, item: InventoryItem): boolean {
    const inv = this.inventories.get(actorId);
    if (!inv) return false;

    // Check stackable
    if (item.stackable) {
      const existing = inv.items.find((i) => i.item_id === item.item_id);
      if (existing) {
        existing.quantity += item.quantity;
        return true;
      }
    }

    // Check slot limit
    if (inv.items.length >= inv.max_slots) {
      console.warn(`[InventorySystem] Inventory đầy cho ${actorId}`);
      return false;
    }

    inv.items.push({ ...item });
    return true;
  }

  /** Xóa item khỏi inventory */
  public removeItem(actorId: string, itemId: string, quantity: number = 1): boolean {
    const inv = this.inventories.get(actorId);
    if (!inv) return false;

    const idx = inv.items.findIndex((i) => i.item_id === itemId);
    if (idx === -1) return false;

    const item = inv.items[idx];
    item.quantity -= quantity;

    if (item.quantity <= 0) {
      inv.items.splice(idx, 1);
    }

    return true;
  }

  /** Trang bị item (gắn vào socket trên avatar) */
  public equipItem(
    actorId: string,
    itemId: string,
    actorObject: THREE.Object3D
  ): THREE.Object3D | null {
    const inv = this.inventories.get(actorId);
    if (!inv) return null;

    const item = inv.items.find((i) => i.item_id === itemId);
    if (!item || !item.equip_socket) return null;

    // Tạo 3D mesh cho item
    const itemMesh = this.createItemMesh(item);
    if (!itemMesh) return null;

    // Gắn vào socket tương ứng
    const socketName = this.mapSocketToName(item.equip_socket);
    SocketAttacher.attachToSocket(actorObject, itemMesh, socketName);

    // Cập nhật equipped
    this.updateEquipped(inv, item);

    return itemMesh;
  }

  /** Tháo item khỏi socket */
  public unequipItem(
    actorId: string,
    slot: keyof ActorInventory['equipped'],
    actorObject: THREE.Object3D
  ): boolean {
    const inv = this.inventories.get(actorId);
    if (!inv) return false;

    const itemId = inv.equipped[slot];
    if (!itemId) return false;

    // Tìm và xóa mesh 3D
    const item = inv.items.find((i) => i.item_id === itemId);
    if (item?.equip_socket) {
      const socketName = this.mapSocketToName(item.equip_socket);
      const socket = actorObject.getObjectByName(socketName);
      if (socket) {
        // Xóa children item
        const children = [...socket.children];
        children.forEach((child) => {
          if (child.name.startsWith('item_')) {
            socket.remove(child);
          }
        });
      }
    }

    inv.equipped[slot] = undefined;
    return true;
  }

  /** Check item có trong inventory không */
  public hasItem(actorId: string, itemId: string): boolean {
    const inv = this.inventories.get(actorId);
    if (!inv) return false;
    return inv.items.some((i) => i.item_id === itemId && i.quantity > 0);
  }

  /** Lấy số lượng item */
  public getItemCount(actorId: string, itemId: string): number {
    const inv = this.inventories.get(actorId);
    if (!inv) return 0;
    const item = inv.items.find((i) => i.item_id === itemId);
    return item?.quantity || 0;
  }

  /** Chuyển item giữa 2 actor */
  public transferItem(
    fromActorId: string, toActorId: string,
    itemId: string, quantity: number = 1
  ): boolean {
    const fromInv = this.inventories.get(fromActorId);
    const toInv = this.inventories.get(toActorId);
    if (!fromInv || !toInv) return false;

    const fromItem = fromInv.items.find((i) => i.item_id === itemId);
    if (!fromItem || fromItem.quantity < quantity) return false;

    const transferItem: InventoryItem = { ...fromItem, quantity };
    if (!this.addItem(toActorId, transferItem)) return false;
    this.removeItem(fromActorId, itemId, quantity);

    return true;
  }

  /** Reset tất cả inventories */
  public reset(): void {
    this.inventories.clear();
  }

  // ============================================================
  // Private helpers
  // ============================================================

  /** Tạo 3D mesh cho item (placeholder geometries) */
  private createItemMesh(item: InventoryItem): THREE.Group | null {
    const group = new THREE.Group();
    group.name = `item_${item.item_id}`;

    switch (item.category) {
      case 'weapon':
        return SocketAttacher.createWeapon(
          item.model_id as 'fire_sword' | 'magic_staff' | 'lantern' || 'fire_sword'
        );
      case 'tool':
        return this.createToolMesh(item.item_id);
      case 'consumable':
        return this.createConsumableMesh(item.item_id);
      default:
        // Generic box placeholder
        const geo = new THREE.BoxGeometry(0.1, 0.1, 0.1);
        const mat = new THREE.MeshStandardMaterial({ color: 0x888888 });
        group.add(new THREE.Mesh(geo, mat));
        return group;
    }
  }

  /** Tạo mesh cho tool (cuốc, bình tưới, xẻng...) */
  private createToolMesh(toolId: string): THREE.Group {
    const group = new THREE.Group();
    group.name = `item_${toolId}`;

    if (toolId.includes('hoe') || toolId.includes('cuoc')) {
      // Cuốc
      const handleGeo = new THREE.CylinderGeometry(0.02, 0.02, 0.8, 6);
      const handleMat = new THREE.MeshStandardMaterial({ color: 0x5c3a1e, roughness: 0.7 });
      const handle = new THREE.Mesh(handleGeo, handleMat);
      handle.position.y = 0.3;
      group.add(handle);

      const headGeo = new THREE.BoxGeometry(0.15, 0.03, 0.06);
      const headMat = new THREE.MeshStandardMaterial({ color: 0x888888, metalness: 0.7 });
      const head = new THREE.Mesh(headGeo, headMat);
      head.position.set(0, 0.72, 0);
      group.add(head);
    } else if (toolId.includes('water') || toolId.includes('binh_tuoi')) {
      // Bình tưới
      const bodyGeo = new THREE.CylinderGeometry(0.08, 0.1, 0.2, 8);
      const bodyMat = new THREE.MeshStandardMaterial({ color: 0x2d7d46, roughness: 0.5 });
      const body = new THREE.Mesh(bodyGeo, bodyMat);
      group.add(body);

      const spoutGeo = new THREE.CylinderGeometry(0.02, 0.03, 0.15, 6);
      const spout = new THREE.Mesh(spoutGeo, bodyMat);
      spout.position.set(0.08, 0.05, 0);
      spout.rotation.z = -Math.PI / 4;
      group.add(spout);
    } else {
      // Generic tool
      const geo = new THREE.CylinderGeometry(0.02, 0.02, 0.6, 6);
      const mat = new THREE.MeshStandardMaterial({ color: 0x5c3a1e });
      group.add(new THREE.Mesh(geo, mat));
    }

    return group;
  }

  /** Tạo mesh cho consumable (ly nước, thức ăn...) */
  private createConsumableMesh(itemId: string): THREE.Group {
    const group = new THREE.Group();
    group.name = `item_${itemId}`;

    if (itemId.includes('cup') || itemId.includes('ly')) {
      // Ly nước
      const cupGeo = new THREE.CylinderGeometry(0.04, 0.035, 0.1, 8);
      const cupMat = new THREE.MeshStandardMaterial({
        color: 0xeeeeee, roughness: 0.3, metalness: 0.1,
      });
      group.add(new THREE.Mesh(cupGeo, cupMat));
    } else if (itemId.includes('bottle') || itemId.includes('binh')) {
      // Bình rượu
      const bottleGeo = new THREE.CylinderGeometry(0.03, 0.05, 0.18, 8);
      const bottleMat = new THREE.MeshStandardMaterial({
        color: 0x8b4513, roughness: 0.6,
      });
      group.add(new THREE.Mesh(bottleGeo, bottleMat));
    } else {
      // Generic food
      const geo = new THREE.SphereGeometry(0.05, 8, 8);
      const mat = new THREE.MeshStandardMaterial({ color: 0xdd8833 });
      group.add(new THREE.Mesh(geo, mat));
    }

    return group;
  }

  /** Map AttachSocket → bone name */
  private mapSocketToName(socket: AttachSocket): string {
    switch (socket) {
      case 'weapon_r': return 'weapon_r';
      case 'weapon_l': return 'weapon_l';
      case 'both_hands': return 'weapon_r'; // Primary hand
      case 'back': return 'spine';
      case 'hip_r': return 'hip_r';
      case 'hip_l': return 'hip_l';
      case 'head': return 'head';
      default: return 'weapon_r';
    }
  }

  /** Update equipped record */
  private updateEquipped(inv: ActorInventory, item: InventoryItem): void {
    if (!item.equip_socket) return;
    switch (item.equip_socket) {
      case 'weapon_r':
        inv.equipped.weapon_r = item.item_id;
        break;
      case 'weapon_l':
        inv.equipped.weapon_l = item.item_id;
        break;
    }
  }
}
