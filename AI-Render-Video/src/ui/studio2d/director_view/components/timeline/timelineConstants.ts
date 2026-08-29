import { ActionPoseType, PropGrowthStage, Director2DProject, MultiAngleDirectorShot } from '../../../../types/studio2d_director';

export const ACTION_OPTIONS: {
  id: ActionPoseType;
  label: string;
  icon: string;
  color: string;
  desc: string;
}[] = [
  { id: 'talk_dialogue', label: 'Nói chuyện / Thoại', icon: '💬', color: '#38bdf8', desc: 'Khẩu hình nhấp nhô & nhịp thoại tự nhiên' },
  { id: 'combat_slash', label: 'Tung chiêu / Chém kiếm', icon: '⚔️', color: '#ef4444', desc: 'Vung vũ khí chém khí bộc phát' },
  { id: 'combat_cast', label: 'Bộc phát / Niệm phép', icon: '⚡', color: '#eab308', desc: 'Tỏa hào quang ma thuật & chưởng lực' },
  { id: 'idle_breathe', label: 'Đứng thở tĩnh tại', icon: '🧘', color: '#10b981', desc: 'Nhịp thở tĩnh tâm nhẹ nhàng' },
  { id: 'shocked_back', label: 'Trúng đòn / Giật lùi', icon: '😱', color: '#f97316', desc: 'Giật lùi về sau do chấn động đòn đánh' },
  { id: 'fly_dash', label: 'Lướt nhanh / Phi thân', icon: '💨', color: '#06b6d4', desc: 'Lướt bay tốc độ cao qua màn hình' },
  { id: 'walk_cycle', label: 'Đi bộ / Di chuyển', icon: '🚶', color: '#a855f7', desc: 'Bước chân di chuyển đều đặn' },
];

export const PROP_GROWTH_OPTIONS: {
  id: PropGrowthStage;
  label: string;
  icon: string;
  color: string;
  desc: string;
}[] = [
  { id: 'seed_sprout', label: 'Mầm non / Mới nảy', icon: '🌱', color: '#84cc16', desc: 'Thu nhỏ 0.35x, nhú mầm từ dưới đất lên' },
  { id: 'grow_big', label: 'Lớn dần / Vươn cao', icon: '🌿', color: '#22c55e', desc: 'Cây mở rộng vươn cành 1.25x kích thước' },
  { id: 'bloom_flowers', label: 'Nở hoa rực rỡ', icon: '🌸', color: '#f472b6', desc: 'Tỏa cánh hoa hồng & hạt phấn bay lượn' },
  { id: 'bear_fruit', label: 'Kết trái / Ra quả', icon: '🍎', color: '#ef4444', desc: 'Chùm quả chín đỏ xum xuê trên cành' },
  { id: 'sway_wind', label: 'Gió thổi đung đưa', icon: '🍃', color: '#06b6d4', desc: 'Cành lá đung đưa uốn lượn theo gió' },
  { id: 'glow_magic', label: 'Tỏa sáng tiên khí', icon: '✨', color: '#38bdf8', desc: 'Phát quang hào quang linh mộc ma thuật' },
  { id: 'normal', label: 'Trạng thái bình thường', icon: '🌲', color: '#64748b', desc: 'Cây cối đứng yên tự nhiên' },
];

/** Format digital timecode (MM:SS.ms) */
export const formatTimecode = (sec: number): string => {
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  const ms = Math.floor((sec % 1) * 100);
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}.${ms.toString().padStart(2, '0')}`;
};

/** Format time with milliseconds (e.g. 3s:450ms) */
export const formatTimeWithMs = (sec: number): string => {
  const s = Math.floor(sec);
  const ms = Math.floor((sec % 1) * 1000);
  return `${s}s:${ms.toString().padStart(3, '0')}ms`;
};

/** Helper to find which shot corresponds to a timestamp */
export const findShotAtTime = (project: Director2DProject, time: number, totalDuration: number) => {
  let acc = 0;
  for (const s of project.shots) {
    if (time >= acc && time <= acc + s.durationSeconds) {
      return { shot: s, shotStartTime: acc };
    }
    acc += s.durationSeconds;
  }
  const lastShot = project.shots[project.shots.length - 1];
  return {
    shot: lastShot,
    shotStartTime: Math.max(0, totalDuration - (lastShot?.durationSeconds || 0)),
  };
};
