/**
 * SkyboxManager — Hierarchical 360° Panorama Asset Registry & Dynamic Selector
 *
 * Categorized by:
 * - Time of day: binh_minh, buoi_sang, buoi_trua, buoi_chieu, buoi_toi, giong_bao
 * - Cloud coverage / weather: khong_may (< 25%), it_may (25% - 65%), nhieu_may (> 65%)
 */

export interface SkyboxItem {
  id: string;
  name: string;
  url: string;
  timeCategory: 'binh_minh' | 'buoi_sang' | 'buoi_trua' | 'buoi_chieu' | 'buoi_toi' | 'giong_bao';
  cloudLevel: 'khong_may' | 'it_may' | 'nhieu_may';
}

export class SkyboxManager {
  public static readonly CATEGORIES = [
    { key: 'binh_minh', label: '🌅 Bình Minh (Dawn / Sunrise)' },
    { key: 'buoi_sang', label: '🌤️ Buổi Sáng (Morning)' },
    { key: 'buoi_trua', label: '☀️ Buổi Trưa (Noon / Midday)' },
    { key: 'buoi_chieu', label: '🌇 Buổi Chiều (Sunset / Dusk)' },
    { key: 'buoi_toi', label: '🌙 Buổi Tối (Night / Stars)' },
    { key: 'giong_bao', label: '⛈️ Giông Bão (Storm / Overcast)' },
  ] as const;

  public static readonly CLOUD_LEVELS = [
    { key: 'khong_may', label: '☀️ Không Mây (0% - 25%)', minCov: 0.0, maxCov: 0.25 },
    { key: 'it_may', label: '🌤️ Ít Mây (25% - 65%)', minCov: 0.25, maxCov: 0.65 },
    { key: 'nhieu_may', label: '☁️ Nhiều Mây (65% - 100%)', minCov: 0.65, maxCov: 1.0 },
  ] as const;

  /** Pre-registered skybox catalog mapping to assets/SkyBoxs/ */
  public static readonly CATALOG: SkyboxItem[] = [
    // Bình Minh (Sunrise)
    { id: 'bm_km_1', name: 'Bình Minh - Không Mây 1', url: '/assets/SkyBoxs/binh_minh/khong_may/binh_minh_khong_may_1.png', timeCategory: 'binh_minh', cloudLevel: 'khong_may' },
    { id: 'bm_im_1', name: 'Bình Minh - Ít Mây 1', url: '/assets/SkyBoxs/binh_minh/it_may/binh_minh_it_may_1.png', timeCategory: 'binh_minh', cloudLevel: 'it_may' },
    { id: 'bm_nm_1', name: 'Bình Minh - Nhiều Mây 1', url: '/assets/SkyBoxs/binh_minh/nhieu_may/binh_minh_nhieu_may_1.png', timeCategory: 'binh_minh', cloudLevel: 'nhieu_may' },

    // Buổi Sáng (Morning)
    { id: 'bs_km_1', name: 'Buổi Sáng - Không Mây 1', url: '/assets/SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png', timeCategory: 'buoi_sang', cloudLevel: 'khong_may' },
    { id: 'bs_im_1', name: 'Buổi Sáng - Ít Mây 1', url: '/assets/SkyBoxs/buoi_sang/it_may/buoi_sang_it_may_1.png', timeCategory: 'buoi_sang', cloudLevel: 'it_may' },
    { id: 'bs_nm_1', name: 'Buổi Sáng - Nhiều Mây 1', url: '/assets/SkyBoxs/buoi_sang/nhieu_may/buoi_sang_nhieu_may_1.png', timeCategory: 'buoi_sang', cloudLevel: 'nhieu_may' },

    // Buổi Trưa (Noon)
    { id: 'bt_km_1', name: 'Buổi Trưa - Không Mây 1', url: '/assets/SkyBoxs/buoi_trua/khong_may/buoi_trua_khong_may_1.png', timeCategory: 'buoi_trua', cloudLevel: 'khong_may' },
    { id: 'bt_im_1', name: 'Buổi Trưa - Ít Mây 1', url: '/assets/SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png', timeCategory: 'buoi_trua', cloudLevel: 'it_may' },
    { id: 'bt_nm_1', name: 'Buổi Trưa - Nhiều Mây 1', url: '/assets/SkyBoxs/buoi_trua/nhieu_may/buoi_trua_nhieu_may_1.png', timeCategory: 'buoi_trua', cloudLevel: 'nhieu_may' },

    // Buổi Chiều (Sunset / Afternoon)
    { id: 'bc_km_1', name: 'Buổi Chiều - Không Mây 1', url: '/assets/SkyBoxs/buoi_chieu/khong_may/buoi_chieu_khong_may_1.png', timeCategory: 'buoi_chieu', cloudLevel: 'khong_may' },
    { id: 'bc_im_1', name: 'Buổi Chiều - Ít Mây 1', url: '/assets/SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png', timeCategory: 'buoi_chieu', cloudLevel: 'it_may' },
    { id: 'bc_nm_1', name: 'Buổi Chiều - Nhiều Mây 1', url: '/assets/SkyBoxs/buoi_chieu/nhieu_may/buoi_chieu_nhieu_may_1.png', timeCategory: 'buoi_chieu', cloudLevel: 'nhieu_may' },

    // Buổi Tối (Night)
    { id: 'bt_km_night_1', name: 'Buổi Tối - Không Mây 1', url: '/assets/SkyBoxs/buoi_toi/khong_may/buoi_toi_khong_may_1.png', timeCategory: 'buoi_toi', cloudLevel: 'khong_may' },
    { id: 'bt_im_night_1', name: 'Buổi Tối - Đêm Sao 1', url: '/assets/SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png', timeCategory: 'buoi_toi', cloudLevel: 'it_may' },
    { id: 'bt_nm_night_1', name: 'Buổi Tối - Nhiều Mây 1', url: '/assets/SkyBoxs/buoi_toi/nhieu_may/buoi_toi_nhieu_may_1.png', timeCategory: 'buoi_toi', cloudLevel: 'nhieu_may' },

    // Giông Bão (Storm)
    { id: 'gb_im_1', name: 'Giông Bão - Ít Mây 1', url: '/assets/SkyBoxs/giong_bao/it_may/giong_bao_it_may_1.png', timeCategory: 'giong_bao', cloudLevel: 'it_may' },
    { id: 'gb_nm_1', name: 'Giông Bão - Nhiều Mây 1', url: '/assets/SkyBoxs/giong_bao/nhieu_may/giong_bao_nhieu_may_1.png', timeCategory: 'giong_bao', cloudLevel: 'nhieu_may' },
  ];

  /**
   * Determine matching time category from sky_time or sun_position
   */
  public static resolveTimeCategory(
    skyTime: string = 'noon',
    sunPos?: number
  ): 'binh_minh' | 'buoi_sang' | 'buoi_trua' | 'buoi_chieu' | 'buoi_toi' {
    if (sunPos !== undefined) {
      if (sunPos < 0.22) return 'binh_minh';
      if (sunPos < 0.45) return 'buoi_sang';
      if (sunPos < 0.70) return 'buoi_trua';
      if (sunPos < 0.88) return 'buoi_chieu';
      return 'buoi_toi';
    }

    switch (skyTime) {
      case 'sunrise':
      case 'dawn':
        return 'binh_minh';
      case 'morning':
        return 'buoi_sang';
      case 'sunset':
      case 'dusk':
      case 'afternoon':
        return 'buoi_chieu';
      case 'night':
      case 'midnight':
        return 'buoi_toi';
      default:
        return 'buoi_trua';
    }
  }

  /**
   * Determine cloud level bucket from coverage percentage
   */
  public static resolveCloudLevel(coverage: number = 0.5): 'khong_may' | 'it_may' | 'nhieu_may' {
    if (coverage <= 0.25) return 'khong_may';
    if (coverage <= 0.65) return 'it_may';
    return 'nhieu_may';
  }

  /**
   * Get best matching skybox URL based on weather sliders & parameters
   */
  public static getMatchingSkybox(params: {
    skyTime?: string;
    sunPosition?: number;
    cloudCoverage?: number;
    rainIntensity?: number;
    randomSeed?: number;
  }): SkyboxItem | null {
    const rain = params.rainIntensity ?? 0;
    const coverage = params.cloudCoverage ?? 0.5;

    let timeCat: 'binh_minh' | 'buoi_sang' | 'buoi_trua' | 'buoi_chieu' | 'buoi_toi' | 'giong_bao';
    if (rain > 0.65) {
      timeCat = 'giong_bao';
    } else {
      timeCat = this.resolveTimeCategory(params.skyTime, params.sunPosition);
    }

    const cloudLvl = this.resolveCloudLevel(coverage);

    // Filter matching items
    let matches = this.CATALOG.filter(
      (item) => item.timeCategory === timeCat && item.cloudLevel === cloudLvl
    );

    // Fallback to time category if no exact cloud level match
    if (matches.length === 0) {
      matches = this.CATALOG.filter((item) => item.timeCategory === timeCat);
    }

    // Fallback to any noon item if still empty
    if (matches.length === 0) {
      matches = this.CATALOG.filter((item) => item.timeCategory === 'buoi_trua');
    }

    if (matches.length === 0) return null;

    // Pick (random or seed-based)
    if (params.randomSeed !== undefined) {
      const idx = Math.abs(Math.floor(params.randomSeed)) % matches.length;
      return matches[idx];
    }

    return matches[0];
  }
}
