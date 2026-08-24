/**
 * Metadata info extracted when parsing an asset image filename
 */
export interface ParsedPartFilenameInfo {
  part_id: string;
  part_name: string;
  group_id: string;
  group_name: string;
  angle_id: string;
  angle_name: string;
  angle_deg: number;
  z_index: number;
  is_master_character: boolean;
  save_filename: string;
}

/**
 * Parses an asset image filename to auto-detect its character part, angle, z_index, and group
 * @param filename - e.g. "05_toc_truoc_000_front.png", "toc_sau_180_back.jpg", "master_045_three_quarter.png"
 */
export function parsePartFilename(filename: string): ParsedPartFilenameInfo | null {
  if (!filename) return null;
  const cleanName = filename.toLowerCase().replace(/\.[a-zA-Z0-9]+$/, '');

  // Check master character turnaround
  if (cleanName.includes('master') || cleanName.includes('character_turnaround')) {
    let angle_deg = 0;
    let angle_id = '000_front';
    let angle_name = '0° Front (Chính diện)';

    if (cleanName.includes('045') || cleanName.includes('three_quarter') || cleanName.includes('45')) {
      angle_deg = 45;
      angle_id = '045_three_quarter';
      angle_name = '45° Three-Quarter (Nghiêng 3/4)';
    } else if (cleanName.includes('090') || cleanName.includes('side') || cleanName.includes('90')) {
      angle_deg = 90;
      angle_id = '090_side';
      angle_name = '90° Side Profile (Nhìn ngang)';
    } else if (cleanName.includes('135') || cleanName.includes('rear_three_quarter')) {
      angle_deg = 135;
      angle_id = '135_rear';
      angle_name = '135° Rear Three-Quarter (Nghiêng sau)';
    } else if (cleanName.includes('180') || cleanName.includes('back')) {
      angle_deg = 180;
      angle_id = '180_back';
      angle_name = '180° Back (Sau lưng)';
    } else if (cleanName.includes('top') || cleanName.includes('dinh_dau')) {
      angle_deg = 90;
      angle_id = 'top_down';
      angle_name = 'Top-Down (Đỉnh đầu)';
    }

    return {
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle_id,
      angle_name,
      angle_deg,
      z_index: 0,
      is_master_character: true,
      save_filename: `master_${angle_id}.png`,
    };
  }

  const partMap: Record<
    string,
    { part_id: string; part_name: string; group_id: string; group_name: string; z_index: number; filePrefix: string }
  > = {
    toc_truoc: { part_id: 'toc_truoc', part_name: 'Mái Tóc Trước', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 50, filePrefix: '05_toc_truoc' },
    toc_sau: { part_id: 'toc_sau', part_name: 'Suối Tóc Sau Lưng', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 10, filePrefix: '01_toc_sau' },
    khuon_mat: { part_id: 'khuon_mat_no_face', part_name: 'Khuôn Mặt Trần', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 30, filePrefix: '03_khuon_mat' },
    trong_den: { part_id: 'trong_den_iris', part_name: 'Mống Mắt (Iris)', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 42, filePrefix: '04a_trong_den_iris' },
    trong_trang: { part_id: 'trong_trang', part_name: 'Tròng Trắng (Sclera)', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 41, filePrefix: '04b_trong_trang' },
    diem_sang: { part_id: 'diem_sang_mat', part_name: 'Điểm Sáng Mắt', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 43, filePrefix: '04c_diem_sang_mat' },
    mi_mat: { part_id: 'mi_mat', part_name: 'Mi Mắt & Chớp Mắt', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 44, filePrefix: '04d_mi_mat' },
    long_may: { part_id: 'long_may', part_name: 'Cặp Lông Mày', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 45, filePrefix: '04e_long_may' },
    mui: { part_id: 'mui', part_name: 'Sống Mũi', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 35, filePrefix: '04f_mui' },
    doi_tai: { part_id: 'doi_tai', part_name: 'Đôi Tai', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 26, filePrefix: '04g_doi_tai' },
    mieng: { part_id: 'mieng', part_name: 'Khẩu Hình Miệng', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 36, filePrefix: '04h_mieng' },
    ngu_quan: { part_id: 'mat', part_name: 'Đôi Mắt & Ngũ Quan', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 40, filePrefix: '04_ngu_quan_mat' },
    than_co_ban: { part_id: 'than_co_ban', part_name: 'Thân Đạo Bào Hanfu', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 20, filePrefix: '02_than_co_ban' },
    canh_tay_trai: { part_id: 'canh_tay_trai', part_name: 'Cánh Tay Trái', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 21, filePrefix: '02a_canh_tay_trai' },
    cang_tay_trai: { part_id: 'cang_tay_trai', part_name: 'Cẳng Tay Trái', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 22, filePrefix: '02b_cang_tay_trai' },
    ban_tay_trai: { part_id: 'ban_tay_trai', part_name: 'Bàn Tay Trái', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 23, filePrefix: '02c_ban_tay_trai' },
    canh_tay_phai: { part_id: 'canh_tay_phai', part_name: 'Cánh Tay Phải', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 19, filePrefix: '02d_canh_tay_phai' },
    cang_tay_phai: { part_id: 'cang_tay_phai', part_name: 'Cẳng Tay Phải', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 18, filePrefix: '02e_cang_tay_phai' },
    ban_tay_phai: { part_id: 'ban_tay_phai', part_name: 'Bàn Tay Phải', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 17, filePrefix: '02f_ban_tay_phai' },
    dui_trai: { part_id: 'dui_trai', part_name: 'Đùi Trái', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 15, filePrefix: '03a_dui_trai' },
    cang_chan_trai: { part_id: 'cang_chan_trai', part_name: 'Cẳng Chân & Ủng Trái', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 16, filePrefix: '03b_cang_chan_trai' },
    dui_phai: { part_id: 'dui_phai', part_name: 'Đùi Phải', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 13, filePrefix: '03c_dui_phai' },
    cang_chan_phai: { part_id: 'cang_chan_phai', part_name: 'Cẳng Chân & Ủng Phải', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 14, filePrefix: '03d_cang_chan_phai' },
    ao_choang: { part_id: 'ao_choang', part_name: 'Áo Choàng / Tà Áo Bay', group_id: '04_props_costumes', group_name: 'Trang Phục Bay & Vũ Khí', z_index: 8, filePrefix: '06a_ao_choang' },
    vu_khi: { part_id: 'vu_khi', part_name: 'Phi Kiếm / Vũ Khí', group_id: '04_props_costumes', group_name: 'Trang Phục Bay & Vũ Khí', z_index: 60, filePrefix: '06_vu_khi' },
  };

  for (const [key, meta] of Object.entries(partMap)) {
    if (cleanName.includes(key)) {
      let angle_deg = 0;
      let angle_id = '000_front';
      let angle_name = '0° Front (Chính diện)';

      if (cleanName.includes('045') || cleanName.includes('three_quarter') || cleanName.includes('45')) {
        angle_deg = 45;
        angle_id = '045_three_quarter';
        angle_name = '45° Three-Quarter (Nghiêng 3/4)';
      } else if (cleanName.includes('090') || cleanName.includes('side') || cleanName.includes('90')) {
        angle_deg = 90;
        angle_id = '090_side';
        angle_name = '90° Side Profile (Nhìn ngang)';
      } else if (cleanName.includes('135') || cleanName.includes('rear_three_quarter')) {
        angle_deg = 135;
        angle_id = '135_rear';
        angle_name = '135° Rear Three-Quarter (Nghiêng sau)';
      } else if (cleanName.includes('180') || cleanName.includes('back')) {
        angle_deg = 180;
        angle_id = '180_back';
        angle_name = '180° Back (Sau lưng)';
      } else if (cleanName.includes('high_angle') || cleanName.includes('top')) {
        angle_deg = 90;
        angle_id = 'high_angle_top';
        angle_name = 'High Angle (Trên cao nhìn xuống)';
      } else if (cleanName.includes('low_angle') || cleanName.includes('bottom')) {
        angle_deg = 90;
        angle_id = 'low_angle_bottom';
        angle_name = 'Low Angle (Dưới hất lên)';
      }

      return {
        part_id: meta.part_id,
        part_name: meta.part_name,
        group_id: meta.group_id,
        group_name: meta.group_name,
        z_index: meta.z_index,
        angle_id,
        angle_name,
        angle_deg,
        is_master_character: false,
        save_filename: `${meta.filePrefix}_${angle_id}.png`,
      };
    }
  }

  return null;
}
