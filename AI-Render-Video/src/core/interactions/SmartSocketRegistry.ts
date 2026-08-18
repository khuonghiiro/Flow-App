import { Vec3Tuple } from '../../types/scene';

export interface SmartSocket {
  id: string;
  type: 'chair' | 'tree' | 'farm_plot';
  entryPosition: Vec3Tuple;
  targetPosition: Vec3Tuple;
  targetRotationY?: number;
}

export class SmartSocketRegistry {
  private static sockets: Map<string, SmartSocket> = new Map([
    [
      'props.wooden_chair_01',
      {
        id: 'props.wooden_chair_01',
        type: 'chair',
        entryPosition: [-4, 0, -1.0],
        targetPosition: [-4, 0, -2.0],
        targetRotationY: 0, // Faces forward towards camera (+Z)
      },
    ],
    [
      'props.village_tree_01',
      {
        id: 'props.village_tree_01',
        type: 'tree',
        entryPosition: [4, 0, -1.8],
        targetPosition: [4.6, 2.3, -3.0],
        targetRotationY: -Math.PI / 4,
      },
    ],
    [
      'props.farm_plot_01',
      {
        id: 'props.farm_plot_01',
        type: 'farm_plot',
        entryPosition: [0, 0, -3.5],
        targetPosition: [0, 0, -5.0],
        targetRotationY: 0,
      },
    ],
  ]);

  public static getSocket(id: string): SmartSocket | undefined {
    return this.sockets.get(id);
  }

  public static registerSocket(socket: SmartSocket): void {
    this.sockets.set(socket.id, socket);
  }

  public static getAll(): SmartSocket[] {
    return Array.from(this.sockets.values());
  }
}
