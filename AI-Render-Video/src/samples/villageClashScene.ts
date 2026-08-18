import { MasterSceneConfig } from '../types/scene';

export const villageClashScene: MasterSceneConfig = {
  scene_id: 'scene_village_clash_01',
  title: '⚔️ Đại Chiến Làng Hoàng Hôn',
  fps: 30,
  duration: 25.0,
  environment: {
    map: 'farming_village',
    sky_time: 'sunset',
    weather: { fog: 0.015, wind: 0.5 },
  },
  subtitles_config: {
    enable_overlay: true,
    burn_in_export: true,
    font_size: 24,
    show_speaker_name: true,
    position: 'bottom',
    text_color: '#f8fafc',
  },
  dialogues_manifest: [
    {
      line_id: 'dlg_01',
      speaker_id: 'actor_warrior',
      speaker_name: 'Chiến Binh Áo Giáp',
      speaker_color: '#38bdf8',
      text: 'Ngươi đã phá hủy cánh đồng của ngôi làng này!',
      voice_config: {
        voice_id: 'vi-VN-NamMinhNeural',
        speed: 1.05,
        pitch: 1.0,
        emotion: 'angry',
      },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 2.0,
      estimated_duration: 3.5,
    },
    {
      line_id: 'dlg_02',
      speaker_id: 'actor_dark_mage',
      speaker_name: 'Phù Thủy Tối Thượng',
      speaker_color: '#f43f5e',
      text: 'Ngươi nghĩ sức mạnh tầm thường đó có thể ngăn cản ta sao?',
      voice_config: {
        voice_id: 'vi-VN-HoaiMyNeural',
        speed: 0.95,
        pitch: 0.9,
        emotion: 'smirk',
      },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 5.5,
      estimated_duration: 3.0,
    },
  ],
  camera_tracks: [
    {
      start: 0.0,
      end: 5.0,
      shot_type: 'cinematic_dolly',
      from: [-6, 3, 5],
      to: [-2, 2, 3],
      look_at: 'actor_warrior.head',
      fov: 45,
    },
    {
      start: 5.0,
      end: 8.5,
      shot_type: 'face_close_up',
      from: [4, 2, 3],
      to: [3, 1.8, 2.2],
      look_at: 'actor_dark_mage.head',
      fov: 35,
    },
    {
      start: 8.5,
      end: 25.0,
      shot_type: 'cinematic_dolly',
      from: [0, 4, 10],
      to: [0, 2.5, 6],
      look_at: [0, 1.0, 0],
      fov: 50,
    },
  ],
  actors: [
    {
      id: 'actor_warrior',
      name: 'Chiến Binh Áo Giáp',
      model: 'characters/hero_knight.vrm',
      costume: 'default_armor',
      spawn_point: [-3.5, 0, 2],
      rotation_y: 0.3,
      tracks: {
        movement: [
          { start: 0.0, end: 2.0, action: 'walk', destination: [-0.8, 0, 1.5], avoid_obstacles: true },
          { start: 2.0, end: 8.2, action: 'talk_gesture', look_at: 'actor_dark_mage.head' },
        ],
        speech: [
          {
            line_ref: 'dlg_01',
            expressions: [
              { time_offset: 0.0, type: 'angry', weight: 1.0 },
              { time_offset: 2.0, type: 'serious', weight: 0.8 },
            ],
          },
        ],
        combat_actions: [
          {
            start_time: 8.5,
            impact_time: 9.1,
            anim: 'heavy_slash_combo',
            weapon_vfx: { type: 'sword_slash_fire', start: 8.6, end: 9.4 },
            target: {
              actor_id: 'actor_dark_mage',
              reaction_anim: 'fly_back_knockdown',
              knockback_distance: 3.2,
              facial_expression: 'pain',
              impact_vfx: 'impact_hit_sparks',
              screen_shake: { intensity: 0.45, duration: 0.35 },
            },
          },
        ],
      },
    },
    {
      id: 'actor_dark_mage',
      name: 'Phù Thủy Tối Thượng',
      model: 'characters/dark_mage.vrm',
      spawn_point: [2.5, 0, 1.5],
      rotation_y: -0.4,
      tracks: {
        movement: [
          { start: 0.0, end: 5.5, action: 'idle', look_at: 'actor_warrior.head' },
          { start: 5.5, end: 8.5, action: 'talk_gesture', look_at: 'actor_warrior.head' },
        ],
        speech: [
          {
            line_ref: 'dlg_02',
            expressions: [
              { time_offset: 0.0, type: 'smirk', weight: 0.95 },
              { time_offset: 2.0, type: 'surprised', weight: 0.7 },
            ],
          },
        ],
        vfx: [
          { start: 7.0, end: 8.5, type: 'magic_shield_barrier', attach_to: 'root' },
        ],
      },
    },
  ],
  dynamic_world_events: [
    {
      target: 'props.farm_plot_01.crop',
      growth_timeline: [
        { time: 2.0, stage: 'seed', scale: 0.1 },
        { time: 8.0, stage: 'sprout', scale: 0.6 },
        { time: 18.0, stage: 'mature_crop', scale: 1.0 },
      ],
    },
  ],
};

export const chairSittingScene: MasterSceneConfig = {
  scene_id: 'scene_chair_sitting',
  title: '🪑 Đàm Đạo Quán Nước (Ngồi Ghế Gỗ)',
  fps: 30,
  duration: 15.0,
  environment: {
    map: 'farming_village',
    sky_time: 'noon',
    weather: { fog: 0.008, wind: 0.2 },
  },
  subtitles_config: {
    enable_overlay: true,
    burn_in_export: true,
    font_size: 22,
    show_speaker_name: true,
    position: 'bottom',
    text_color: '#ffffff',
  },
  dialogues_manifest: [
    {
      line_id: 'dlg_sit_01',
      speaker_id: 'actor_warrior',
      speaker_name: 'Chiến Binh',
      speaker_color: '#eab308',
      text: 'Trận chiến vừa rồi mệt thật, ngồi nghỉ một chút đã!',
      voice_config: { voice_id: 'vi-VN-NamMinhNeural', speed: 1.0, emotion: 'smile' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 4.5,
      estimated_duration: 3.5,
    },
  ],
  camera_tracks: [
    {
      start: 0.0,
      end: 15.0,
      shot_type: 'cinematic_dolly',
      from: [-6.5, 2.5, 3],
      to: [-3.5, 1.8, 1.2],
      look_at: 'actor_warrior.head',
      fov: 45,
    },
  ],
  actors: [
    {
      id: 'actor_warrior',
      name: 'Chiến Binh',
      model: 'characters/hero_knight.vrm',
      spawn_point: [-2, 0, 1],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 15.0,
            action: 'sit',
            target_object: 'props.wooden_chair_01',
          },
        ],
        speech: [
          {
            line_ref: 'dlg_sit_01',
            expressions: [{ time_offset: 0.0, type: 'smile', weight: 0.9 }],
          },
        ],
      },
    },
  ],
};

export const treeClimbingScene: MasterSceneConfig = {
  scene_id: 'scene_tree_climbing',
  title: '🌳 Trèo Cây Trinh Sát Hoàng Hôn',
  fps: 30,
  duration: 18.0,
  environment: {
    map: 'farming_village',
    sky_time: 'sunset',
    weather: { fog: 0.01, wind: 0.4 },
  },
  subtitles_config: {
    enable_overlay: true,
    burn_in_export: true,
    font_size: 22,
    show_speaker_name: true,
    position: 'bottom',
    text_color: '#ffffff',
  },
  dialogues_manifest: [
    {
      line_id: 'dlg_tree_01',
      speaker_id: 'actor_dark_mage',
      speaker_name: 'Phù Thủy',
      speaker_color: '#a855f7',
      text: 'Từ ngọn cây này có thể bao quát toàn bộ ngôi làng, không ai thấy được ta!',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, emotion: 'serious' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 4.5,
      estimated_duration: 3.5,
    },
    {
      line_id: 'dlg_tree_02',
      speaker_id: 'actor_warrior',
      speaker_name: 'Chiến Binh',
      speaker_color: '#eab308',
      text: 'Ủa? Tên Phù Thủy vừa chạy đằng này mà trốn đâu mất rồi? Tán cây rậm quá chẳng thấy đâu!',
      voice_config: { voice_id: 'vi-VN-NamMinhNeural', speed: 1.0, emotion: 'fear' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 8.5,
      estimated_duration: 4.5,
    },
    {
      line_id: 'dlg_tree_03',
      speaker_id: 'actor_dark_mage',
      speaker_name: 'Phù Thủy',
      speaker_color: '#a855f7',
      text: 'Hehe, ngươi làm sao thấy được ta? Ta đang nhìn xuyên qua kẽ lá quan sát từng bước đi của ngươi đây!',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, emotion: 'smile' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 13.5,
      estimated_duration: 4.0,
    },
  ],
  camera_tracks: [
    // Shot 1 (0.0s - 4.5s): Theo dõi Phù Thủy trèo lên thân cây
    {
      start: 0.0,
      end: 4.5,
      shot_type: 'cinematic_dolly',
      from: [7.8, 3.2, 3.8],
      to: [5.2, 2.8, 1.2],
      look_at: 'actor_dark_mage.head',
      fov: 48,
    },
    // Shot 2 (4.5s - 8.2s): Soi cận cảnh Phù Thủy nói trên cây (Tán lá mờ X-Ray)
    {
      start: 4.5,
      end: 8.2,
      shot_type: 'face_close_up',
      follow_target: 'actor_dark_mage',
      fov: 34,
    },
    // Shot 3 (8.2s - 13.0s): Góc nhìn từ Chiến Binh dưới đất ngước nhìn lên tán cây đặc rậm rạp
    {
      start: 8.2,
      end: 13.0,
      shot_type: 'cinematic_dolly',
      from: [1.2, 1.6, -0.6],
      to: [1.6, 1.8, -0.9],
      look_at: [4.6, 3.4, -3.0],
      fov: 46,
    },
    // Shot 4 (13.0s - 18.0s): Góc nhìn từ Phù Thủy trên cây nhìn xuyên qua kẽ lá xuống Chiến Binh
    {
      start: 13.0,
      end: 18.0,
      shot_type: 'cinematic_dolly',
      from: [4.9, 2.7, -2.6],
      to: [4.7, 2.5, -2.4],
      look_at: [1.2, 1.2, -1.0],
      fov: 42,
    },
  ],
  actors: [
    {
      id: 'actor_dark_mage',
      name: 'Phù Thủy',
      model: 'characters/dark_mage.vrm',
      spawn_point: [2, 0, 1],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 18.0,
            action: 'climb',
            target_object: 'props.village_tree_01',
          },
        ],
        speech: [
          {
            line_ref: 'dlg_tree_01',
            expressions: [{ time_offset: 0.0, type: 'smirk', weight: 0.8 }],
          },
          {
            line_ref: 'dlg_tree_03',
            expressions: [{ time_offset: 0.0, type: 'smile', weight: 0.9 }],
          },
        ],
      },
    },
    {
      id: 'actor_warrior',
      name: 'Chiến Binh',
      model: 'characters/hero_knight.vrm',
      spawn_point: [-1.0, 0, 1.5],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 6.0,
            action: 'walk',
            destination: [1.2, 0, -1.0],
          },
          {
            start: 6.0,
            end: 18.0,
            action: 'talk_gesture',
            look_at: 'actor_dark_mage.head',
          },
        ],
        speech: [
          {
            line_ref: 'dlg_tree_02',
            expressions: [{ time_offset: 0.0, type: 'surprised', weight: 0.8 }],
          },
        ],
      },
    },
  ],
};

export const lanternVillageScene: MasterSceneConfig = {
  scene_id: 'scene_lantern_twilight',
  title: '🏮 Cổ Trấn Đèn Lồng (Model Tải Về)',
  fps: 60,
  duration: 18.0,
  environment: {
    map: 'farming_village',
    sky_time: 'sunset',
    weather: { fog: 0.01, wind: 0.35 },
  },
  subtitles_config: {
    enable_overlay: true,
    burn_in_export: true,
    font_size: 22,
    show_speaker_name: true,
    position: 'bottom',
    text_color: '#ffffff',
  },
  dialogues_manifest: [
    {
      line_id: 'dlg_lantern_01',
      speaker_id: 'actor_anime_girl',
      speaker_name: 'Tiểu Vũ (VRM)',
      speaker_color: '#38bdf8',
      text: 'Hoàng hôn buông xuống ngôi làng thật yên ả, chiếc lồng đèn này sáng lung linh quá!',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, emotion: 'smile' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 2.5,
      estimated_duration: 4.8,
    },
    {
      line_id: 'dlg_lantern_02',
      speaker_id: 'actor_anime_girl',
      speaker_name: 'Tiểu Vũ (VRM)',
      speaker_color: '#38bdf8',
      text: 'Để mình ngồi nghỉ chân tại quán nước bên đường ngắm nhìn cảnh sắc...',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, emotion: 'serious' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 8.5,
      estimated_duration: 4.2,
    },
    {
      line_id: 'dlg_lantern_03',
      speaker_id: 'actor_anime_girl',
      speaker_name: 'Tiểu Vũ (VRM)',
      speaker_color: '#38bdf8',
      text: 'Gió chiều thổi qua mát rượi, một ngày thật tuyệt vời!',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, emotion: 'smile' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 13.8,
      estimated_duration: 3.8,
    },
  ],
  camera_tracks: [
    {
      start: 0.0,
      end: 6.5,
      shot_type: 'cinematic_dolly',
      from: [2.5, 2.5, 4.0],
      to: [-1.0, 1.8, 2.2],
      look_at: 'actor_anime_girl.head',
      fov: 46,
    },
    {
      start: 6.5,
      end: 11.5,
      shot_type: 'face_close_up',
      follow_target: 'actor_anime_girl',
      fov: 34,
    },
    {
      start: 11.5,
      end: 18.0,
      shot_type: 'cinematic_dolly',
      from: [-2.0, 1.6, -0.4],
      to: [-4.2, 1.4, -0.6],
      look_at: 'actor_anime_girl.head',
      fov: 40,
    },
  ],
  actors: [
    {
      id: 'actor_anime_girl',
      name: 'Tiểu Vũ (VRM)',
      model: 'assets/characters/sample_avatar.vrm',
      spawn_point: [0.5, 0, 1.8],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 8.0,
            action: 'walk',
            destination: [-4.0, 0, -1.65],
          },
          {
            start: 8.0,
            end: 18.0,
            action: 'sit',
            target_object: 'props.wooden_chair_01',
          },
        ],
        speech: [
          {
            line_ref: 'dlg_lantern_01',
            expressions: [{ time_offset: 0.0, type: 'smile', weight: 0.8 }],
          },
          {
            line_ref: 'dlg_lantern_02',
            expressions: [{ time_offset: 0.0, type: 'serious', weight: 0.6 }],
          },
          {
            line_ref: 'dlg_lantern_03',
            expressions: [{ time_offset: 0.0, type: 'smile', weight: 0.9 }],
          },
        ],
      },
    },
  ],
};

export const sampleScenes = [
  villageClashScene,
  chairSittingScene,
  treeClimbingScene,
  lanternVillageScene,
];
