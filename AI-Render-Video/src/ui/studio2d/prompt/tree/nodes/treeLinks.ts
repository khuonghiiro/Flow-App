import { SkillTreeLink } from '../types';

export const ALL_TREE_LINKS: SkillTreeLink[] = [
  // Links from Root Master to 5 Pillars
  { fromId: 'root_master', toId: 'pillar_character', color: '#38bdf8', animated: true },
  { fromId: 'root_master', toId: 'pillar_locomotion', color: '#34d399', animated: true },
  { fromId: 'root_master', toId: 'pillar_actions', color: '#f59e0b', animated: true },
  { fromId: 'root_master', toId: 'pillar_face', color: '#ec4899', animated: true },
  { fromId: 'root_master', toId: 'pillar_weapons', color: '#c084fc', animated: true },

  // Pillar 1: Character (5 góc)
  { fromId: 'pillar_character', toId: 'node_char_0', color: '#38bdf8' },
  { fromId: 'pillar_character', toId: 'node_char_45', color: '#38bdf8' },
  { fromId: 'pillar_character', toId: 'node_char_90', color: '#38bdf8' },
  { fromId: 'pillar_character', toId: 'node_char_135', color: '#38bdf8' },
  { fromId: 'pillar_character', toId: 'node_char_180', color: '#38bdf8' },

  // Pillar 2: Locomotion (Đi Bộ & Chạy độc lập)
  { fromId: 'pillar_locomotion', toId: 'hub_walk', color: '#34d399', animated: true },
  { fromId: 'hub_walk', toId: 'node_walk_0', color: '#34d399' },
  { fromId: 'hub_walk', toId: 'node_walk_45', color: '#34d399' },
  { fromId: 'hub_walk', toId: 'node_walk_90', color: '#34d399' },
  { fromId: 'hub_walk', toId: 'node_walk_135', color: '#34d399' },
  { fromId: 'hub_walk', toId: 'node_walk_180', color: '#34d399' },

  { fromId: 'pillar_locomotion', toId: 'hub_run', color: '#34d399', animated: true },
  { fromId: 'hub_run', toId: 'node_run_0', color: '#34d399' },
  { fromId: 'hub_run', toId: 'node_run_45', color: '#34d399' },
  { fromId: 'hub_run', toId: 'node_run_90', color: '#34d399' },
  { fromId: 'hub_run', toId: 'node_run_135', color: '#34d399' },
  { fromId: 'hub_run', toId: 'node_run_180', color: '#34d399' },

  // Pillar 3: Actions
  // 1. Đứng yên
  { fromId: 'pillar_actions', toId: 'hub_idle', color: '#f59e0b', animated: true },
  { fromId: 'hub_idle', toId: 'node_idle_0', color: '#f59e0b' },
  { fromId: 'hub_idle', toId: 'node_idle_45', color: '#f59e0b' },
  { fromId: 'hub_idle', toId: 'node_idle_90', color: '#f59e0b' },
  { fromId: 'hub_idle', toId: 'node_idle_135', color: '#f59e0b' },
  { fromId: 'hub_idle', toId: 'node_idle_180', color: '#f59e0b' },

  // 2. Phòng thủ
  { fromId: 'pillar_actions', toId: 'hub_defend', color: '#f59e0b', animated: true },
  { fromId: 'hub_defend', toId: 'node_defend_0', color: '#f59e0b' },
  { fromId: 'hub_defend', toId: 'node_defend_45', color: '#f59e0b' },
  { fromId: 'hub_defend', toId: 'node_defend_90', color: '#f59e0b' },
  { fromId: 'hub_defend', toId: 'node_defend_135', color: '#f59e0b' },
  { fromId: 'hub_defend', toId: 'node_defend_180', color: '#f59e0b' },

  // 3. Ngồi
  { fromId: 'pillar_actions', toId: 'hub_sit', color: '#f59e0b', animated: true },
  { fromId: 'hub_sit', toId: 'node_sit_0', color: '#f59e0b' },
  { fromId: 'hub_sit', toId: 'node_sit_45', color: '#f59e0b' },
  { fromId: 'hub_sit', toId: 'node_sit_90', color: '#f59e0b' },
  { fromId: 'hub_sit', toId: 'node_sit_135', color: '#f59e0b' },
  { fromId: 'hub_sit', toId: 'node_sit_180', color: '#f59e0b' },

  // 4. Nằm
  { fromId: 'pillar_actions', toId: 'hub_lie', color: '#f59e0b', animated: true },
  { fromId: 'hub_lie', toId: 'node_lie_0', color: '#f59e0b' },
  { fromId: 'hub_lie', toId: 'node_lie_45', color: '#f59e0b' },
  { fromId: 'hub_lie', toId: 'node_lie_90', color: '#f59e0b' },
  { fromId: 'hub_lie', toId: 'node_lie_135', color: '#f59e0b' },
  { fromId: 'hub_lie', toId: 'node_lie_180', color: '#f59e0b' },

  // 5. Bật Nhảy
  { fromId: 'pillar_actions', toId: 'hub_jump', color: '#f59e0b', animated: true },
  { fromId: 'hub_jump', toId: 'node_jump_0', color: '#f59e0b' },
  { fromId: 'hub_jump', toId: 'node_jump_45', color: '#f59e0b' },
  { fromId: 'hub_jump', toId: 'node_jump_90', color: '#f59e0b' },
  { fromId: 'hub_jump', toId: 'node_jump_135', color: '#f59e0b' },
  { fromId: 'hub_jump', toId: 'node_jump_180', color: '#f59e0b' },

  // 6. Đánh công
  { fromId: 'pillar_actions', toId: 'hub_attack', color: '#f59e0b', animated: true },
  { fromId: 'hub_attack', toId: 'node_atk_0', color: '#f59e0b' },
  { fromId: 'hub_attack', toId: 'node_atk_45', color: '#f59e0b' },
  { fromId: 'hub_attack', toId: 'node_atk_90', color: '#f59e0b' },
  { fromId: 'hub_attack', toId: 'node_atk_135', color: '#f59e0b' },
  { fromId: 'hub_attack', toId: 'node_atk_180', color: '#f59e0b' },

  // 7. Ngã / Đổ sụp
  { fromId: 'pillar_actions', toId: 'hub_fall', color: '#f59e0b', animated: true },
  { fromId: 'hub_fall', toId: 'node_fall_0', color: '#f59e0b' },
  { fromId: 'hub_fall', toId: 'node_fall_45', color: '#f59e0b' },
  { fromId: 'hub_fall', toId: 'node_fall_90', color: '#f59e0b' },
  { fromId: 'hub_fall', toId: 'node_fall_135', color: '#f59e0b' },
  { fromId: 'hub_fall', toId: 'node_fall_180', color: '#f59e0b' },

  // 8. Bị trúng đòn
  { fromId: 'pillar_actions', toId: 'hub_hit', color: '#f59e0b', animated: true },
  { fromId: 'hub_hit', toId: 'node_hit_0', color: '#f59e0b' },
  { fromId: 'hub_hit', toId: 'node_hit_45', color: '#f59e0b' },
  { fromId: 'hub_hit', toId: 'node_hit_90', color: '#f59e0b' },
  { fromId: 'hub_hit', toId: 'node_hit_135', color: '#f59e0b' },
  { fromId: 'hub_hit', toId: 'node_hit_180', color: '#f59e0b' },

  // 9. Cử động đầu
  { fromId: 'pillar_actions', toId: 'hub_head', color: '#f59e0b', animated: true },
  { fromId: 'hub_head', toId: 'node_head_shake', color: '#f59e0b' },
  { fromId: 'hub_head', toId: 'node_head_nod', color: '#f59e0b' },
  { fromId: 'hub_head', toId: 'node_head_look', color: '#f59e0b' },

  // Pillar 4: Ngũ Quan & Biểu Cảm
  { fromId: 'pillar_face', toId: 'hub_face_8s', color: '#ec4899', animated: true },
  { fromId: 'hub_face_8s', toId: 'node_face_8s_0', color: '#ec4899' },
  { fromId: 'hub_face_8s', toId: 'node_face_8s_45', color: '#ec4899' },
  { fromId: 'hub_face_8s', toId: 'node_face_8s_90', color: '#ec4899' },
  { fromId: 'hub_face_8s', toId: 'node_face_8s_135', color: '#ec4899' },

  { fromId: 'pillar_face', toId: 'hub_face_single', color: '#ec4899', animated: true },
  // 0° expressions
  { fromId: 'hub_face_single', toId: 'node_f0_blink', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_smile', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_talk', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_surp', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_sad', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_angry', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_wink', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_cry', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_fear', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_smirk', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_shy', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f0_focus', color: '#ec4899' },

  // 45° expressions
  { fromId: 'hub_face_single', toId: 'node_f45_talk', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_sad', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_surp', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_angry', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_smile', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_blink', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_wink', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_cry', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_smirk', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_fear', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f45_shy', color: '#ec4899' },

  // 90° expressions
  { fromId: 'hub_face_single', toId: 'node_f90_prof', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f90_talk', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f90_smile', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f90_angry', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f90_surp', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f90_sad', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f90_cry', color: '#ec4899' },

  // 135° expressions
  { fromId: 'hub_face_single', toId: 'node_f135_glance', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f135_smile', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f135_talk', color: '#ec4899' },
  { fromId: 'hub_face_single', toId: 'node_f135_alert', color: '#ec4899' },

  // Pillar 5: Vũ khí
  { fromId: 'pillar_weapons', toId: 'node_w_sword', color: '#c084fc' },
  { fromId: 'pillar_weapons', toId: 'node_w_staff', color: '#c084fc' },
  { fromId: 'pillar_weapons', toId: 'node_w_bow', color: '#c084fc' },
  { fromId: 'pillar_weapons', toId: 'node_w_spell', color: '#c084fc' },
];
