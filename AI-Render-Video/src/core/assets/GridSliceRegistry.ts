import { Character2DAngle, Character2DPartType } from '../../types/scene2d';
import demoHairMultiAngleSheet from '../../assets/demo_hair_multi_angle_sheet.jpg';

export interface GridCellDefinition {
  row: number;
  col: number;
  label: string;
  partSlot: Character2DPartType;
  angle?: Character2DAngle;
  mirrorAngle?: Character2DAngle; // For right-side symmetry (e.g. profile_left -> profile_right)
  description: string;
}

export interface GridCategoryDefinition {
  id: string;
  label: string;
  icon: string;
  rows: number;
  cols: number;
  defaultKeyColor: string;
  description: string;
  cells: GridCellDefinition[];
}

export const GRID_CATEGORY_DEFINITIONS: GridCategoryDefinition[] = [
  {
    id: 'single_full_image',
    label: '🖼️ Ảnh Đơn Hoàn Chỉnh (TẮT KHUNG LƯỚI - 1 Ảnh Duy Nhất)',
    icon: 'Maximize2',
    rows: 1,
    cols: 1,
    defaultKeyColor: '#ffffff',
    description: 'Tắt hoàn toàn logic chia khung lưới. Coi toàn bộ bức ảnh tải lên là 1 vật thể/linh kiện đơn nhất hoàn chỉnh 100%, không bị cắt nhỏ.',
    cells: [
      {
        row: 0,
        col: 0,
        label: 'Toàn Bộ Ảnh Hoàn Chỉnh (Full Single Image)',
        partSlot: 'than_co_ban',
        angle: 'front',
        description: 'Toàn bộ bức ảnh hoàn chỉnh (100% width x 100% height)',
      },
    ],
  },
  {
    id: 'hair_multi_angle_grid',
    label: '💇 Bảng Tóc Đa Góc (4 Dãy × 5 Cột)',
    icon: 'Scissors',
    rows: 4,
    cols: 5,
    defaultKeyColor: '#00ff00',
    description: '4 tầng bóc tách chuyên sâu của cùng 1 kiểu tóc (Dòng 1: Mái trước trán, Dòng 2: Đỉnh đầu nhìn từ trên xuống, Dòng 3: Tóc sau đầu gồm cả đỉnh đầu nối suối tóc lưng, Dòng 4: Lọn tóc mai 2 bên má & tóc tơ gáy) qua 5 góc camera.',
    cells: [
      // Row 0 (Dòng 1): Mái Trước Trán (Front Bangs Fringe - Thuần túy mái trước, không có tóc sau/gáy)
      { row: 0, col: 0, label: 'Mái Trước 0° (Chính Diện)', partSlot: 'toc_truoc', angle: 'front', description: 'Mái trước trán nhìn thẳng chính diện' },
      { row: 0, col: 1, label: 'Mái Trước 45° (Nghiêng 3/4)', partSlot: 'toc_truoc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Mái trước trán xoay nghiêng 45°' },
      { row: 0, col: 2, label: 'Mái Trước 90° (Ngang Profile)', partSlot: 'toc_truoc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Lát cắt mỏng của mái trước nhìn ngang 90°' },
      { row: 0, col: 3, label: 'Mái Trước 135° (Nghiêng Sau)', partSlot: 'toc_truoc', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Mép mái trước nhìn từ sau chéo 135°' },
      { row: 0, col: 4, label: 'Mái Trước 180° (Sau Lưng - Ô Ẩn)', partSlot: 'toc_truoc', angle: 'back', description: 'Mái trước khuất hoàn toàn sau đầu ở góc 180°' },

      // Row 1 (Dòng 2): Đỉnh Chỏm Đầu / Búi Tóc & Trâm Cài Soi Từ Trên Cao Xuống (Bird's Eye Top-Down)
      { row: 1, col: 0, label: 'Đỉnh Chỏm Đầu 0° (Trâm Ngang)', partSlot: 'dau', angle: 'top_down', description: 'Đỉnh chỏm đầu & búi tóc trâm cài nhìn từ trên cao xuống ở hướng 0°' },
      { row: 1, col: 1, label: 'Đỉnh Chỏm Đầu 45° (Trâm Chéo)', partSlot: 'dau', angle: 'top_down_three_quarter_left', mirrorAngle: 'top_down_three_quarter_right', description: 'Đỉnh chỏm đầu xoay góc nghiêng 45°' },
      { row: 1, col: 2, label: 'Đỉnh Chỏm Đầu 90° (Trâm Dọc)', partSlot: 'dau', angle: 'top_down_profile_left', mirrorAngle: 'top_down_profile_right', description: 'Đỉnh chỏm đầu nhìn ngang 90°' },
      { row: 1, col: 3, label: 'Đỉnh Chỏm Đầu 135° (Trâm Sau Chéo)', partSlot: 'dau', angle: 'top_down_back_three_quarter_left', mirrorAngle: 'top_down_back_three_quarter_right', description: 'Đỉnh chỏm đầu xoay sau chéo 135°' },
      { row: 1, col: 4, label: 'Đỉnh Chỏm Đầu 180° (Trâm Sau Ngang)', partSlot: 'dau', angle: 'top_down_back', description: 'Đỉnh chỏm đầu nhìn thẳng từ phía sau lưng 180°' },

      // Row 2 (Dòng 3): Toàn Bộ Tóc Sau Đầu (Thuần Túy Tóc Sau - Không Dính Tóc Mái Trước)
      { row: 2, col: 0, label: 'Tóc Sau Đầu 0° (Phủ Qua Vai Nhìn Từ Trước)', partSlot: 'toc_sau', angle: 'front', description: 'Toàn bộ tóc sau đầu và suối tóc buông 2 vai (Khuôn mặt mở rỗng)' },
      { row: 2, col: 1, label: 'Tóc Sau Đầu 45° (Nghiêng 3/4)', partSlot: 'toc_sau', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Tóc sau đầu xoay nghiêng 45° buông lệch vai' },
      { row: 2, col: 2, label: 'Tóc Sau Đầu 90° (Ngang Profile)', partSlot: 'toc_sau', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Nửa sau sọ đầu và suối tóc nhìn ngang 90° uốn lượn chữ S' },
      { row: 2, col: 3, label: 'Tóc Sau Đầu 135° (Nghiêng Sau)', partSlot: 'toc_sau', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Mảng tóc sau đầu nhìn từ phía sau 135°' },
      { row: 2, col: 4, label: 'Tóc Sau Đầu 180° (Toàn Cảnh Sau Lưng)', partSlot: 'toc_sau', angle: 'back', description: 'Toàn bộ tóc sau đầu phủ kín lưng trực diện 180°' },

      // Row 3 (Dòng 4): Lọn Tóc Mai 2 Bên Má & Tóc Tơ Chân Gáy (Độc lập, diễn hoạt phất phơ)
      { row: 3, col: 0, label: 'Tóc Mai 0° (2 Bên Má Nhìn Thẳng)', partSlot: 'khuon_mat', angle: 'front', description: '2 lọn tóc mai độc lập ôm 2 bên má nhìn thẳng' },
      { row: 3, col: 1, label: 'Tóc Mai 45° (Má Nghiêng 3/4)', partSlot: 'khuon_mat', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Lọn tóc mai bên má nghiêng 45°' },
      { row: 3, col: 2, label: 'Tóc Mai 90° (Vành Tai Nhìn Ngang)', partSlot: 'khuon_mat', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Lọn tóc mai cong ôm vành tai nhìn từ phía tai 90°' },
      { row: 3, col: 3, label: 'Tóc Mai 135° (Sau Tai Chéo)', partSlot: 'khuon_mat', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Lọn tóc sau tai 135°' },
      { row: 3, col: 4, label: 'Tóc Gáy 180° (Chân Gáy Sau Lưng)', partSlot: 'khuon_mat', angle: 'back', description: 'Lọn tóc tơ chân gáy sau lưng 180°' },
    ],
  },
  {
    id: 'eyes_grid',
    label: '👁️ Bảng Đôi Mắt & Chớp Mắt (4 Dãy × 5 Cột)',
    icon: 'Eye',
    rows: 4,
    cols: 5,
    defaultKeyColor: '#00ff00',
    description: 'Trạng thái mắt mở, nhắm chớp mắt, kiếm ý phát sáng và lông mày.',
    cells: [
      { row: 0, col: 0, label: 'Mắt Mở 0° (Chính diện)', partSlot: 'mat', angle: 'front', description: 'Đôi mắt mở to nhìn thẳng' },
      { row: 0, col: 1, label: 'Mắt Mở 45° (Nghiêng 3/4)', partSlot: 'mat', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Mắt mở góc nghiêng' },
      { row: 0, col: 2, label: 'Mắt Mở 90° (Ngang Profile)', partSlot: 'mat', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Một mắt nhìn ngang' },
      { row: 0, col: 3, label: 'Mắt Liếc 135°', partSlot: 'mat', angle: 'back_three_quarter_left', description: 'Ánh mắt liếc sau' },
      { row: 0, col: 4, label: 'Mắt Ngước Nhìn', partSlot: 'mat', angle: 'front', description: 'Đồng tử ngước lên' },

      { row: 1, col: 0, label: 'Mắt Nhắm 0° (Chớp mắt)', partSlot: 'mat', angle: 'front', description: 'Mí mắt nhắm bình thường' },
      { row: 1, col: 1, label: 'Mắt Nhắm 45°', partSlot: 'mat', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Mắt nhắm góc nghiêng' },
      { row: 1, col: 2, label: 'Mắt Nhắm 90°', partSlot: 'mat', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Mí mắt nhắm ngang' },
      { row: 1, col: 3, label: 'Mắt Cười Híp Mí', partSlot: 'mat', angle: 'front', description: 'Đường cong mắt cười vui' },
      { row: 1, col: 4, label: 'Mắt Nghiến Nhắm', partSlot: 'mat', angle: 'front', description: 'Nhắm chặt chịu đau chiến đấu' },

      { row: 2, col: 0, label: 'Mắt Kiếm Ý Phát Sáng', partSlot: 'mat', angle: 'front', description: 'Tròng mắt tỏa hào quang' },
      { row: 2, col: 1, label: 'Mắt Kinh Ngạc Sốc', partSlot: 'mat', angle: 'front', description: 'Đồng tử co giật' },
      { row: 2, col: 2, label: 'Mắt Lạnh Lùng', partSlot: 'mat', angle: 'front', description: 'Ánh mắt sát khí' },
      { row: 2, col: 3, label: 'Mắt Ma Nhãn', partSlot: 'mat', angle: 'front', description: 'Ma đạo hắc ám' },
      { row: 2, col: 4, label: 'Mắt Tiên Nhãn', partSlot: 'mat', angle: 'front', description: 'Thần quang rực rỡ' },

      { row: 3, col: 0, label: 'Lông Mày Thẳng 0°', partSlot: 'mat', angle: 'front', description: 'Lông mày kiếm khách' },
      { row: 3, col: 1, label: 'Lông Mày Chau Giận', partSlot: 'mat', angle: 'front', description: 'Chau mày tức giận' },
      { row: 3, col: 2, label: 'Lông Mày Nhướng Cao', partSlot: 'mat', angle: 'front', description: 'Nhướng mày thắc mắc' },
      { row: 3, col: 3, label: 'Lông Mày 90° Ngang', partSlot: 'mat', angle: 'profile_left', description: 'Lông mày nhìn ngang' },
      { row: 3, col: 4, label: 'Lông Mày Chiến Đấu', partSlot: 'mat', angle: 'front', description: 'Nghiêm nghị tung chiêu' },
    ],
  },
  {
    id: 'mouth_grid',
    label: '👄 Bảng Khẩu Hình Miệng & Đối Thoại (4 Dãy × 5 Cột)',
    icon: 'Smile',
    rows: 4,
    cols: 5,
    defaultKeyColor: '#00ff00',
    description: 'Chu kỳ Lip-Sync nói chuyện A/O/I, cười mỉm, quát tháo và vết máu.',
    cells: [
      { row: 0, col: 0, label: 'Miệng Ngậm 0° (Âm M)', partSlot: 'mieng', angle: 'front', description: 'Miệng khép bình thường' },
      { row: 0, col: 1, label: 'Miệng Mở 0° (Âm A)', partSlot: 'mieng', angle: 'front', description: 'Khẩu hình âm A mở to' },
      { row: 0, col: 2, label: 'Miệng Tròn 0° (Âm O/U)', partSlot: 'mieng', angle: 'front', description: 'Khẩu hình âm O/U' },
      { row: 0, col: 3, label: 'Miệng Rộng 0° (Âm I/E)', partSlot: 'mieng', angle: 'front', description: 'Khẩu hình âm I/E' },
      { row: 0, col: 4, label: 'Cười Mỉm 0°', partSlot: 'mieng', angle: 'front', description: 'Nụ cười tự tin' },

      { row: 1, col: 0, label: 'Miệng Nói 45°', partSlot: 'mieng', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Khẩu hình nói góc nghiêng' },
      { row: 1, col: 1, label: 'Nhếch Mép 45°', partSlot: 'mieng', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Cười khẩy góc nghiêng' },
      { row: 1, col: 2, label: 'Miệng Ngậm 90° Profile', partSlot: 'mieng', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Mép môi nhìn ngang' },
      { row: 1, col: 3, label: 'Miệng Nói 90° Profile', partSlot: 'mieng', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Mép môi mở khi nói' },
      { row: 1, col: 4, label: 'Quát Tháo 90°', partSlot: 'mieng', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Thét gầm nhìn ngang' },

      { row: 2, col: 0, label: 'Thét Gầm Tung Chiêu', partSlot: 'mieng', angle: 'front', description: 'Quát to chiến đấu lộ răng' },
      { row: 2, col: 1, label: 'Nghiến Răng Chịu Đau', partSlot: 'mieng', angle: 'front', description: 'Khít răng chịu đòn' },
      { row: 2, col: 2, label: 'Cười Lớn Sảng Khoái', partSlot: 'mieng', angle: 'front', description: 'Ha ha cười lớn' },
      { row: 2, col: 3, label: 'Cười Khẩy Lạnh Lùng', partSlot: 'mieng', angle: 'front', description: 'Nhếch môi khinh bỉ' },
      { row: 2, col: 4, label: 'Há Miệng Kinh Ngạc', partSlot: 'mieng', angle: 'front', description: 'Sốc mở miệng' },

      { row: 3, col: 0, label: 'Vệt Máu Khóe Môi', partSlot: 'mieng', angle: 'front', description: 'Bị thương khi chiến đấu' },
      { row: 3, col: 1, label: 'Ngậm Phù Chú', partSlot: 'mieng', angle: 'front', description: 'Ngậm đạo bùa ở miệng' },
      { row: 3, col: 2, label: 'Thở Dốc Bốc Khói', partSlot: 'mieng', angle: 'front', description: 'Thở dốc kiệt sức' },
      { row: 3, col: 3, label: 'Cắn Môi Dưới', partSlot: 'mieng', angle: 'front', description: 'Tập trung quyết tâm' },
      { row: 3, col: 4, label: 'Khép Môi Lạnh', partSlot: 'mieng', angle: 'front', description: 'Lặng lẽ ngậm miệng' },
    ],
  },
  {
    id: 'nose_chin_grid',
    label: '👃 Bảng Sống Mũi, Cằm Nhọn 90° & Tai (4 Dãy × 5 Cột)',
    icon: 'Sparkles',
    rows: 4,
    cols: 5,
    defaultKeyColor: '#00ff00',
    description: 'Sống mũi kiếm hiệp, khung cằm quai hàm 90°, vành tai và thần ấn.',
    cells: [
      { row: 0, col: 0, label: 'Sống Mũi 0°', partSlot: 'mui', angle: 'front', description: 'Bóng mũi nhìn thẳng' },
      { row: 0, col: 1, label: 'Sống Mũi 45°', partSlot: 'mui', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Sống mũi nghiêng' },
      { row: 0, col: 2, label: 'Sống Mũi 90° Cao Thẳng', partSlot: 'mui', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Sống mũi kiếm hiệp 90°' },
      { row: 0, col: 3, label: 'Bóng Mũi 135°', partSlot: 'mui', angle: 'back_three_quarter_left', description: 'Bóng mũi sau' },
      { row: 0, col: 4, label: 'Mũi Góc Ngước', partSlot: 'mui', angle: 'front', description: 'Mũi nhìn từ dưới' },

      { row: 1, col: 0, label: 'Khung Cằm 0°', partSlot: 'dau', angle: 'front', description: 'Cằm trái xoan và cổ' },
      { row: 1, col: 1, label: 'Khung Cằm 45°', partSlot: 'dau', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Đường viền hàm nghiêng' },
      { row: 1, col: 2, label: 'Cằm Nhọn & Quai Hàm 90°', partSlot: 'dau', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Cằm nhọn Profile 90°' },
      { row: 1, col: 3, label: 'Khung Hàm 135°', partSlot: 'dau', angle: 'back_three_quarter_left', description: 'Khung hàm sau' },
      { row: 1, col: 4, label: 'Sau Gáy 180°', partSlot: 'dau', angle: 'back', description: 'Chân cổ và gáy sau đầu' },

      { row: 2, col: 0, label: 'Đôi Tai 0°', partSlot: 'dau', angle: 'front', description: 'Tai nhìn thẳng 2 bên' },
      { row: 2, col: 1, label: 'Vành Tai 45°', partSlot: 'dau', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Vành tai góc nghiêng' },
      { row: 2, col: 2, label: 'Toàn Bộ Vành Tai 90°', partSlot: 'dau', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Tai nhìn ngang rõ sụn tai' },
      { row: 2, col: 3, label: 'Tai Sau 135°', partSlot: 'dau', angle: 'back_three_quarter_left', description: 'Mặt sau tai nghiêng' },
      { row: 2, col: 4, label: 'Mặt Sau Tai 180°', partSlot: 'dau', angle: 'back', description: 'Sau dái tai kèm khuyên ngọc' },

      { row: 3, col: 0, label: 'Thần Ấn Trán Tiên Gia', partSlot: 'dau', angle: 'front', description: 'Dấu ấn phát sáng' },
      { row: 3, col: 1, label: 'Ma Vân Hắc Ám', partSlot: 'dau', angle: 'front', description: 'Vân ma đỏ rực' },
      { row: 3, col: 2, label: 'Vết Sẹo Kiếm Khách', partSlot: 'dau', angle: 'front', description: 'Vết sẹo ngang má' },
      { row: 3, col: 3, label: 'Giọt Mồ Hôi Cảm Xúc', partSlot: 'dau', angle: 'front', description: 'Giọt mồ hôi' },
      { row: 3, col: 4, label: 'Thiên Nhãn Thức Tỉnh', partSlot: 'dau', angle: 'front', description: 'Mắt thứ 3 giữa trán' },
    ],
  },
  {
    id: 'costume_grid',
    label: '🥋 Bảng Trang Phục & Đạo Bào 4 Hướng (4 Dãy × 4 Cột)',
    icon: 'Shirt',
    rows: 4,
    cols: 4,
    defaultKeyColor: '#00ff00',
    description: 'Trang phục rỗng ruột phân tách 4 góc quay: Cổ áo, Tà váy, Ống tay, Thắt lưng.',
    cells: [
      { row: 0, col: 0, label: 'Cổ Áo & Ngực 0°', partSlot: 'trang_phuc', angle: 'front', description: 'Vạt áo ngực trước' },
      { row: 0, col: 1, label: 'Cổ Áo & Ngực 45°', partSlot: 'trang_phuc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Cổ áo nghiêng' },
      { row: 0, col: 2, label: 'Cổ Áo & Ngực 90°', partSlot: 'trang_phuc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Thân áo nhìn ngang' },
      { row: 0, col: 3, label: 'Lưng Áo Sau 180°', partSlot: 'trang_phuc', angle: 'back', description: 'Lưng áo có đường may ngọc bích' },

      { row: 1, col: 0, label: 'Tà Váy Dưới 0°', partSlot: 'trang_phuc', angle: 'front', description: 'Tà áo trước' },
      { row: 1, col: 1, label: 'Tà Váy Dưới 45°', partSlot: 'trang_phuc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Tà áo bay nghiêng' },
      { row: 1, col: 2, label: 'Tà Váy Dưới 90°', partSlot: 'trang_phuc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Nếp gấp áo nhìn ngang' },
      { row: 1, col: 3, label: 'Vạt Áo Sau 180°', partSlot: 'trang_phuc', angle: 'back', description: 'Đuôi áo rủ sau lưng' },

      { row: 2, col: 0, label: 'Ống Tay Áo 0°', partSlot: 'trang_phuc', angle: 'front', description: 'Tay áo rủ tự nhiên' },
      { row: 2, col: 1, label: 'Ống Tay Áo 45°', partSlot: 'trang_phuc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Tay áo bay theo gió' },
      { row: 2, col: 2, label: 'Ống Tay Áo 90°', partSlot: 'trang_phuc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Ống tay nhìn ngang' },
      { row: 2, col: 3, label: 'Khăn Choàng Sau 180°', partSlot: 'trang_phuc', angle: 'back', description: 'Vai áo sau lưng' },

      { row: 3, col: 0, label: 'Thắt Lưng 0°', partSlot: 'trang_phuc', angle: 'front', description: 'Đai lưng vàng kim kèm ngọc bội' },
      { row: 3, col: 1, label: 'Thắt Lưng 45°', partSlot: 'trang_phuc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Nút thắt lụa nghiêng' },
      { row: 3, col: 2, label: 'Thắt Lưng 90°', partSlot: 'trang_phuc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Viền hông nhìn ngang' },
      { row: 3, col: 3, label: 'Nơ Buộc Lưng 180°', partSlot: 'trang_phuc', angle: 'back', description: 'Nơ lụa thắt sau lưng' },
    ],
  },
  {
    id: 'weapons_grid',
    label: '⚔️ Bảng Vũ Khí & Pháp Bảo (4 Dãy × 5 Cột)',
    icon: 'Swords',
    rows: 4,
    cols: 5,
    defaultKeyColor: '#00ff00',
    description: 'Thân kiếm đa góc, chuôi bao kiếm, kiếm khí phát sáng và tư thế tay cầm.',
    cells: [
      { row: 0, col: 0, label: 'Thân Kiếm Thẳng 0°', partSlot: 'vu_khi', angle: 'front', description: 'Lưỡi kiếm thẳng đứng' },
      { row: 0, col: 1, label: 'Thân Kiếm Nghiêng 45°', partSlot: 'vu_khi', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Góc phối cảnh chéo' },
      { row: 0, col: 2, label: 'Lưỡi Kiếm 90° (Cạnh Mỏng)', partSlot: 'vu_khi', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Cạnh sắc nhìn ngang' },
      { row: 0, col: 3, label: 'Bao Kiếm Sau Lưng 180°', partSlot: 'vu_khi', angle: 'back', description: 'Bao kiếm đeo sau lưng' },
      { row: 0, col: 4, label: 'Phi Kiếm Ngang', partSlot: 'vu_khi', angle: 'front', description: 'Ngự kiếm bay ngang' },

      { row: 1, col: 0, label: 'Chuôi Kiếm Rồng Chạm Ngọc', partSlot: 'vu_khi', angle: 'front', description: 'Chuôi cầm chạm rồng' },
      { row: 1, col: 1, label: 'Chắn Tay Vàng Kim', partSlot: 'vu_khi', angle: 'front', description: 'Chắn kiếm hoa văn' },
      { row: 1, col: 2, label: 'Bao Kiếm Dải Lụa Đỏ', partSlot: 'vu_khi', angle: 'front', description: 'Bao kiếm tinh xảo' },
      { row: 1, col: 3, label: 'Vòng Khuyên Đuôi Kiếm', partSlot: 'vu_khi', angle: 'front', description: 'Dây tua rua đuôi' },
      { row: 1, col: 4, label: 'Họng Tra Kiếm', partSlot: 'vu_khi', angle: 'front', description: 'Miệng bao kiếm' },

      { row: 2, col: 0, label: 'Kiếm Khí Lam Quang', partSlot: 'vu_khi', angle: 'front', description: 'Linh lực bao quanh kiếm' },
      { row: 2, col: 1, label: 'Vệt Chém Trăng Khuyết', partSlot: 'vu_khi', angle: 'front', description: 'VFX đường kiếm chém' },
      { row: 2, col: 2, label: 'Mũi Kiếm Đâm Xuyên', partSlot: 'vu_khi', angle: 'front', description: 'Vệt đâm thẳng' },
      { row: 2, col: 3, label: 'Kiếm Trận Xoay Vòng', partSlot: 'vu_khi', angle: 'front', description: 'Vòng kiếm bay' },
      { row: 2, col: 4, label: 'Tia Sét Lôi Điện', partSlot: 'vu_khi', angle: 'front', description: 'Tia sét phát sáng' },

      { row: 3, col: 0, label: 'Tay Cầm Kiếm Thuận', partSlot: 'vu_khi', angle: 'front', description: 'Bàn tay nắm chuôi' },
      { row: 3, col: 1, label: 'Tay Cầm Kiếm Ngược', partSlot: 'vu_khi', angle: 'front', description: 'Cầm ngược lưỡi kiếm' },
      { row: 3, col: 2, label: 'Bắt Quyết Kiếm Ấn', partSlot: 'vu_khi', angle: 'front', description: 'Hai ngón tay chỉ đạo' },
      { row: 3, col: 3, label: 'Phi Kiếm Lơ Lửng', partSlot: 'vu_khi', angle: 'front', description: 'Kiếm bay trước người' },
      { row: 3, col: 4, label: 'Song Kiếm Hợp Bích', partSlot: 'vu_khi', angle: 'front', description: 'Cặp kiếm song thủ' },
    ],
  },
];

/**
 * Generates a clean Chroma-Green or White sample SVG grid sheet for instant user testing
 * Specially rendered with intuitive multi-angle anime hair sprites and clear angle badges
 */
export const generateDemoGridSpriteSheet = (catId: string, bg: 'chroma_green' | 'pure_white' = 'chroma_green'): string => {
  if (catId === 'hair_multi_angle_grid' && bg === 'chroma_green') {
    return demoHairMultiAngleSheet;
  }

  const cat = GRID_CATEGORY_DEFINITIONS.find((c) => c.id === catId) || GRID_CATEGORY_DEFINITIONS[0];
  const bgColor = bg === 'chroma_green' ? '#00ff00' : '#ffffff';

  const cellW = 384;
  const cellH = 270;
  const totalW = cat.cols * cellW;
  const totalH = cat.rows * cellH;

  const svgCells = cat.cells
    .map((c) => {
      const cx = cellW / 2;
      const cy = cellH / 2;
      const col = c.col;
      const row = c.row;

      // Render distinct stylized hair shapes depending on row & angle
      let hairShape = '';
      if (row === 0) {
        // Front Bangs at various angles (0°, 45°, 90°, 135°, 180°)
        if (col === 0) {
          // Front 0°: Symmetric bangs
          hairShape = `
            <path d="M ${cx - 70} ${cy - 40} Q ${cx} ${cy - 65} ${cx + 70} ${cy - 40} Q ${cx + 60} ${cy + 15} ${cx + 40} ${cy + 45} Q ${cx + 10} ${cy + 10} ${cx} ${cy + 35} Q ${cx - 10} ${cy + 10} ${cx - 40} ${cy + 45} Q ${cx - 60} ${cy + 15} ${cx - 70} ${cy - 40} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="3"/>
            <path d="M ${cx - 30} ${cy - 25} Q ${cx} ${cy - 45} ${cx + 30} ${cy - 25} Q ${cx + 10} ${cy + 5} ${cx} ${cy + 25} Z" fill="#4338ca" opacity="0.6"/>
            <path d="M ${cx - 50} ${cy - 30} Q ${cx} ${cy - 50} ${cx + 50} ${cy - 30}" stroke="#a5b4fc" stroke-width="4" stroke-linecap="round" fill="none"/>
          `;
        } else if (col === 1) {
          // 45° 3/4 View: Shifted bangs
          hairShape = `
            <path d="M ${cx - 60} ${cy - 40} Q ${cx - 10} ${cy - 65} ${cx + 65} ${cy - 35} Q ${cx + 70} ${cy + 20} ${cx + 50} ${cy + 50} Q ${cx + 20} ${cy + 15} ${cx + 5} ${cy + 38} Q ${cx - 15} ${cy + 15} ${cx - 45} ${cy + 30} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="3"/>
            <path d="M ${cx - 30} ${cy - 25} Q ${cx + 5} ${cy - 45} ${cx + 45} ${cy - 20}" stroke="#a5b4fc" stroke-width="4" stroke-linecap="round" fill="none"/>
          `;
        } else if (col === 2) {
          // 90° Profile: Side view of bangs
          hairShape = `
            <path d="M ${cx - 30} ${cy - 45} Q ${cx + 20} ${cy - 60} ${cx + 45} ${cy - 35} Q ${cx + 65} ${cy + 10} ${cx + 55} ${cy + 50} Q ${cx + 30} ${cy + 25} ${cx + 20} ${cy + 40} Q ${cx + 5} ${cy + 15} ${cx - 20} ${cy + 5} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="3"/>
            <path d="M ${cx - 10} ${cy - 30} Q ${cx + 25} ${cy - 45} ${cx + 40} ${cy - 25}" stroke="#a5b4fc" stroke-width="4" stroke-linecap="round" fill="none"/>
          `;
        } else if (col === 3) {
          // 135° Back 3/4
          hairShape = `
            <path d="M ${cx - 50} ${cy - 45} Q ${cx} ${cy - 65} ${cx + 55} ${cy - 40} Q ${cx + 45} ${cy + 10} ${cx + 25} ${cy + 35} Q ${cx - 10} ${cy + 5} ${cx - 45} ${cy + 15} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="3"/>
          `;
        } else {
          // 180° Full Back
          hairShape = `
            <path d="M ${cx - 55} ${cy - 45} Q ${cx} ${cy - 65} ${cx + 55} ${cy - 45} Q ${cx + 45} ${cy + 5} ${cx + 20} ${cy + 25} Q ${cx} ${cy + 10} ${cx - 20} ${cy + 25} Q ${cx - 45} ${cy + 5} ${cx - 55} ${cy - 45} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="3"/>
          `;
        }
      } else if (row === 1) {
        // Crown / Top Bun
        hairShape = `
          <ellipse cx="${cx}" cy="${cy - 20}" rx="38" ry="30" fill="#1e1b4b" stroke="#312e81" stroke-width="3"/>
          <path d="M ${cx - 25} ${cy - 20} Q ${cx} ${cy - 35} ${cx + 25} ${cy - 20}" stroke="#a5b4fc" stroke-width="3" fill="none"/>
          <rect x="${cx - 12}" y="${cy + 5}" width="24" height="12" rx="4" fill="#d97706"/>
          <path d="M ${cx - 40} ${cy + 10} L ${cx + 40} ${cy + 10}" stroke="#fbbf24" stroke-width="4" stroke-linecap="round"/>
        `;
      } else if (row === 2) {
        // Back Long Flowing Hair
        hairShape = `
          <path d="M ${cx - 65} ${cy - 50} Q ${cx} ${cy - 40} ${cx + 65} ${cy - 50} Q ${cx + 75} ${cy + 20} ${cx + 50} ${cy + 85} Q ${cx} ${cy + 95} ${cx - 50} ${cy + 85} Q ${cx - 75} ${cy + 20} ${cx - 65} ${cy - 50} Z" fill="#0f172a" stroke="#1e293b" stroke-width="3"/>
          <path d="M ${cx - 30} ${cy - 20} Q ${cx} ${cy + 30} ${cx - 15} ${cy + 80}" stroke="#64748b" stroke-width="3" stroke-linecap="round" fill="none"/>
          <path d="M ${cx + 30} ${cy - 20} Q ${cx} ${cy + 30} ${cx + 15} ${cy + 80}" stroke="#64748b" stroke-width="3" stroke-linecap="round" fill="none"/>
        `;
      } else {
        // Sideburns
        hairShape = `
          <path d="M ${cx - 40} ${cy - 40} Q ${cx - 50} ${cy} ${cx - 35} ${cy + 45} Q ${cx - 30} ${cy + 10} ${cx - 32} ${cy - 30} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="2"/>
          <path d="M ${cx + 40} ${cy - 40} Q ${cx + 50} ${cy} ${cx + 35} ${cy + 45} Q ${cx + 30} ${cy + 10} ${cx + 32} ${cy - 30} Z" fill="#1e1b4b" stroke="#312e81" stroke-width="2"/>
        `;
      }

      return `
        <!-- Cell [${row}, ${col}]: ${c.label} -->
        <g transform="translate(${col * cellW}, ${row * cellH})">
          <!-- Cell Border Grid (Faint) -->
          <rect x="2" y="2" width="${cellW - 4}" height="${cellH - 4}" fill="none" stroke="rgba(0,0,0,0.15)" stroke-width="1.5" stroke-dasharray="6,4"/>
          
          <!-- Rendered Hair Part Sprite -->
          ${hairShape}

          <!-- Crisp Angle Badge -->
          <rect x="8" y="8" width="110" height="22" rx="4" fill="rgba(15, 23, 42, 0.85)"/>
          <text x="14" y="23" font-family="sans-serif" font-size="10.5" font-weight="bold" fill="#38bdf8">${c.label.split('(')[0]}</text>
        </g>
      `;
    })
    .join('\n');

  return 'data:image/svg+xml;utf8,' + encodeURIComponent(`
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${totalW} ${totalH}" width="${totalW}" height="${totalH}">
      <rect width="${totalW}" height="${totalH}" fill="${bgColor}"/>
      ${svgCells}
    </svg>
  `);
};
