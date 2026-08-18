import { MasterSceneConfig } from '../types/scene';

export const villageClashScene: MasterSceneConfig = {
  scene_id: 'scene_village_clash',
  title: 'Đại Chiến Làng Hoàng Hôn: Chiến Binh vs Phù Thủy',
  fps: 30,
  duration: 25.0,
  environment: {
    map: 'farming_village',
    sky_time: 'sunset',
    weather: { fog: 0.012, wind: 0.3 },
  },
  subtitles_config: {
    enable_overlay: true,
    burn_in_export: true,
    font_size: 24,
    show_speaker_name: true,
    position: 'bottom',
    text_color: '#ffffff',
    background_opacity: 0.75,
  },
  dialogues_manifest: [
    {
      line_id: 'dlg_01',
      speaker_id: 'actor_warrior',
      speaker_name: 'Chiến Binh',
      speaker_color: '#eab308',
      text: 'Ngươi đã chuẩn bị tinh thần để đền tội chưa?',
      voice_config: { voice_id: 'vi-VN-NamMinhNeural', speed: 1.05, emotion: 'angry' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 2.0,
      estimated_duration: 3.2,
    },
    {
      line_id: 'dlg_02',
      speaker_id: 'actor_dark_mage',
      speaker_name: 'Phù Thủy Tối Thượng',
      speaker_color: '#a855f7',
      text: 'Ha ha! Một kẻ như ngươi mà cũng đòi cản đường ta sao?',
      voice_config: { voice_id: 'vi-VN-HoaiMyNeural', speed: 1.0, pitch: 0.1, emotion: 'arrogant' },
      audio_path: null,
      audio_naming_rule: 'audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3',
      status: 'pending_tts',
      start_time: 5.5,
      estimated_duration: 3.5,
    },
  ],
  camera_tracks: [
    {
      start: 0.0,
      end: 5.0,
      shot_type: 'cinematic_dolly',
      from: [-6, 3, 10],
      to: [-1.5, 1.8, 4.5],
      look_at: 'actor_warrior.head',
      fov: 45,
    },
    {
      start: 5.0,
      end: 8.0,
      shot_type: 'cinematic_dolly',
      from: [6, 2.5, 9],
      to: [1.5, 1.8, 4],
      look_at: 'actor_dark_mage.head',
      fov: 45,
    },
    {
      start: 8.0,
      end: 15.0,
      shot_type: 'combat_action_cam',
      follow_target: 'actor_warrior',
      distance: 4.2,
      height: 1.8,
      fov: 55,
    },
    {
      start: 15.0,
      end: 25.0,
      shot_type: 'cinematic_dolly',
      from: [0, 5, 12],
      to: [0, 3, 7],
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
              knockback_distance: 2.5,
              facial_expression: 'pain',
              impact_vfx: 'impact_hit_sparks',
              screen_shake: { intensity: 0.38, duration: 0.3 },
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
