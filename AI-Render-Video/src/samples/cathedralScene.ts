import { MasterSceneConfig } from '../types/scene';

export const cathedralScene: MasterSceneConfig = {
  scene_id: 'scene_cathedral_mystery',
  title: '⛪ Thánh Đường Cổ Kính (Map cathedral.glb)',
  fps: 60,
  duration: 20.0,
  environment: {
    map: 'assets/maps/cathedral.glb',
    sky_time: 'night',
    weather: {
      fog: 0.000,
      wind: 0.05,
    },
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
      line_id: 'dlg_cath_01',
      speaker_id: 'actor_warrior',
      speaker_name: 'Chiến Binh Ánh Sáng',
      speaker_color: '#38bdf8',
      text: 'Đây chính là Đại Thánh Đường Cổ Kính ngàn năm lưu truyền...',
      voice_config: { voice_id: 'vi-VN-NamMinhNeural', speed: 1.0, emotion: 'serious' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 1.0,
      estimated_duration: 4.5,
    },
    {
      line_id: 'dlg_cath_02',
      speaker_id: 'actor_anime_girl',
      speaker_name: 'Tiểu Vũ (Anime)',
      speaker_color: '#f472b6',
      text: 'Kiến trúc mái vòm Gothic ở đây thật tráng lệ và bí ẩn!',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, emotion: 'smile' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 6.5,
      estimated_duration: 4.8,
    },
    {
      line_id: 'dlg_cath_03',
      speaker_id: 'actor_dark_mage',
      speaker_name: 'Hắc Pháp Sư',
      speaker_color: '#a855f7',
      text: 'Kẻ phàm trần... sao dám bước chân vào nơi linh thiêng này!',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 0.95, emotion: 'smirk' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 12.0,
      estimated_duration: 5.0,
    },
    {
      line_id: 'dlg_cath_04',
      speaker_id: 'actor_warrior',
      speaker_name: 'Chiến Binh Ánh Sáng',
      speaker_color: '#38bdf8',
      text: 'Hãy chuẩn bị tiếp chiêu đi, Hắc Pháp Sư!',
      voice_config: { voice_id: 'vi-VN-NamMinhNeural', speed: 1.05, emotion: 'angry' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 17.2,
      estimated_duration: 2.8,
    },
  ],
  camera_tracks: [
    {
      start: 0.0,
      end: 6.0,
      shot_type: 'cinematic_dolly',
      from: [0, 4.0, 12.0],
      to: [0, 2.5, 5.0],
      look_at: 'actor_warrior.head',
      fov: 55,
    },
    {
      start: 6.0,
      end: 11.5,
      shot_type: 'face_close_up',
      follow_target: 'actor_anime_girl',
      fov: 34,
    },
    {
      start: 11.5,
      end: 16.5,
      shot_type: 'cinematic_dolly',
      from: [4.0, 3.2, 0.0],
      to: [1.2, 1.8, -3.5],
      look_at: 'actor_dark_mage.head',
      fov: 48,
    },
    {
      start: 16.5,
      end: 20.0,
      shot_type: 'combat_action_cam',
      follow_target: 'actor_warrior',
      distance: 4.0,
      height: 1.8,
      fov: 52,
    },
  ],
  actors: [
    {
      id: 'actor_warrior',
      name: 'Chiến Binh Ánh Sáng',
      model: 'characters/hero_knight.vrm',
      spawn_point: [-1.2, 0, 3.5],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 5.0,
            action: 'walk',
            destination: [-0.6, 0, 0.5],
          },
          {
            start: 5.0,
            end: 16.5,
            action: 'talk_gesture',
            look_at: 'actor_dark_mage.head',
          },
          {
            start: 16.5,
            end: 20.0,
            action: 'idle',
            look_at: 'actor_dark_mage.head',
          },
        ],
        combat_actions: [
          {
            start_time: 16.8,
            impact_time: 17.5,
            anim: 'heavy_slash_combo',
            weapon_vfx: { type: 'sword_slash_fire', start: 16.8, end: 17.8 },
            target: {
              actor_id: 'actor_dark_mage',
              reaction_anim: 'stagger_back',
              knockback_distance: 1.5,
              facial_expression: 'pain',
              impact_vfx: 'impact_hit_sparks',
              screen_shake: { intensity: 0.4, duration: 0.3 },
            },
          },
        ],
        speech: [
          {
            line_ref: 'dlg_cath_01',
            expressions: [{ time_offset: 0.0, type: 'serious', weight: 0.8 }],
          },
          {
            line_ref: 'dlg_cath_04',
            expressions: [{ time_offset: 0.0, type: 'angry', weight: 0.9 }],
          },
        ],
      },
    },
    {
      id: 'actor_anime_girl',
      name: 'Tiểu Vũ (Anime)',
      model: 'assets/characters/sample_avatar.vrm',
      spawn_point: [1.2, 0, 4.0],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 6.0,
            action: 'walk',
            destination: [0.8, 0, 1.2],
          },
          {
            start: 6.0,
            end: 20.0,
            action: 'talk_gesture',
            look_at: 'actor_warrior.head',
          },
        ],
        speech: [
          {
            line_ref: 'dlg_cath_02',
            expressions: [{ time_offset: 0.0, type: 'smile', weight: 0.9 }],
          },
        ],
      },
    },
    {
      id: 'actor_dark_mage',
      name: 'Hắc Pháp Sư',
      model: 'characters/dark_mage.vrm',
      spawn_point: [0, 0, -4.5],
      tracks: {
        movement: [
          {
            start: 0.0,
            end: 16.5,
            action: 'idle',
            look_at: 'actor_warrior.head',
          },
          {
            start: 16.5,
            end: 20.0,
            action: 'talk_gesture',
            look_at: 'actor_warrior.head',
          },
        ],
        vfx: [
          { start: 16.0, end: 18.5, type: 'magic_shield_barrier', attach_to: 'root' },
        ],
        speech: [
          {
            line_ref: 'dlg_cath_03',
            expressions: [{ time_offset: 0.0, type: 'smirk', weight: 0.9 }],
          },
        ],
      },
    },
  ],
};
