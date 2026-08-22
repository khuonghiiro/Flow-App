import { Character2DAngle, Character2DPartType } from '../../types/scene2d';

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
    id: 'hair_multi_angle_grid',
    label: '💇 Bảng Tóc Đa Góc (4 Dãy × 5 Cột)',
    icon: 'Scissors',
    rows: 4,
    cols: 5,
    defaultKeyColor: '#00ff00',
    description: '4 tầng tách lớp của cùng 1 kiểu tóc (Mái trước, Đỉnh đầu, Tóc sau, Tóc mai) qua 5 góc quay.',
    cells: [
      // Row 0: Front Bangs
      { row: 0, col: 0, label: 'Mái Trước 0° (Chính diện)', partSlot: 'toc_truoc', angle: 'front', description: 'Mái trước góc nhìn thẳng' },
      { row: 0, col: 1, label: 'Mái Trước 45° (Nghiêng 3/4)', partSlot: 'toc_truoc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Mái trước góc nghiêng' },
      { row: 0, col: 2, label: 'Mái Trước 90° (Ngang Profile)', partSlot: 'toc_truoc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Mái trước góc nhìn ngang' },
      { row: 0, col: 3, label: 'Mái Trước 135° (Nghiêng Sau)', partSlot: 'toc_truoc', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Mái trước nhìn từ sau chéo' },
      { row: 0, col: 4, label: 'Mái Trước 180° (Góc Khuất)', partSlot: 'toc_truoc', angle: 'back', description: 'Ngọn tóc mái trước ló ra sau sọ đầu' },

      // Row 1: Crown & Bun
      { row: 1, col: 0, label: 'Đỉnh Đầu 0°', partSlot: 'toc_truoc', angle: 'front', description: 'Đỉnh đầu và ngôi tóc chính diện' },
      { row: 1, col: 1, label: 'Đỉnh Đầu 45°', partSlot: 'toc_truoc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Đỉnh đầu góc nghiêng' },
      { row: 1, col: 2, label: 'Đỉnh Đầu 90°', partSlot: 'toc_truoc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Đỉnh đầu nhìn ngang' },
      { row: 1, col: 3, label: 'Búi Tóc 135°', partSlot: 'toc_truoc', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Búi tóc sau chéo' },
      { row: 1, col: 4, label: 'Búi Tóc 180°', partSlot: 'toc_truoc', angle: 'back', description: 'Búi tóc và dây buộc nhìn từ sau' },

      // Row 2: Back Hair
      { row: 2, col: 0, label: 'Tóc Sau 0° (Sau Vai)', partSlot: 'toc_sau', angle: 'front', description: 'Tóc xõa hai bên vai nhìn từ trước' },
      { row: 2, col: 1, label: 'Tóc Sau 45° (Bay Chéo)', partSlot: 'toc_sau', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Tóc sau bay nghiêng' },
      { row: 2, col: 2, label: 'Tóc Sau 90° (Đuôi Tóc Ngang)', partSlot: 'toc_sau', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Đuôi tóc dài nhìn ngang' },
      { row: 2, col: 3, label: 'Tóc Sau 135°', partSlot: 'toc_sau', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Tóc dài góc sau chéo' },
      { row: 2, col: 4, label: 'Tóc Sau 180° (Toàn Cảnh)', partSlot: 'toc_sau', angle: 'back', description: 'Suối tóc hùng vĩ trực diện sau lưng' },

      // Row 3: Sideburns (No ears)
      { row: 3, col: 0, label: 'Tóc Mai 0° (2 Bên Má)', partSlot: 'toc_truoc', angle: 'front', description: '2 lọn tóc mai ôm má nhìn thẳng' },
      { row: 3, col: 1, label: 'Tóc Mai 45°', partSlot: 'toc_truoc', angle: 'three_quarter_left', mirrorAngle: 'three_quarter_right', description: 'Lọn tóc mai bên má nghiêng' },
      { row: 3, col: 2, label: 'Tóc Mai 90° (Vành Tai)', partSlot: 'toc_truoc', angle: 'profile_left', mirrorAngle: 'profile_right', description: 'Lọn tóc mai cong quanh tai' },
      { row: 3, col: 3, label: 'Tóc Mai 135°', partSlot: 'toc_truoc', angle: 'back_three_quarter_left', mirrorAngle: 'back_three_quarter_right', description: 'Lọn tóc sau tai' },
      { row: 3, col: 4, label: 'Tóc Gáy 180°', partSlot: 'toc_truoc', angle: 'back', description: 'Tóc tơ chân gáy sau lưng' },
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
 */
export const generateDemoGridSpriteSheet = (catId: string, bg: 'chroma_green' | 'pure_white' = 'chroma_green'): string => {
  const cat = GRID_CATEGORY_DEFINITIONS.find((c) => c.id === catId) || GRID_CATEGORY_DEFINITIONS[0];
  const bgColor = bg === 'chroma_green' ? '#00ff00' : '#ffffff';
  const strokeCol = bg === 'chroma_green' ? '#000000' : '#1e293b';

  const cellW = 384;
  const cellH = 270;
  const totalW = cat.cols * cellW;
  const totalH = cat.rows * cellH;

  const svgCells = cat.cells
    .map((c) => {
      const cx = c.col * cellW + cellW / 2;
      const cy = c.row * cellH + cellH / 2;
      return `
        <!-- Cell [${c.row}, ${c.col}]: ${c.label} -->
        <g transform="translate(${c.col * cellW}, ${c.row * cellH})">
          <!-- Inner content silhouette -->
          <ellipse cx="${cellW / 2}" cy="${cellH / 2}" rx="${cellW * 0.35}" ry="${cellH * 0.35}" fill="#18181b" stroke="${strokeCol}" stroke-width="3"/>
          <circle cx="${cellW / 2}" cy="${cellH / 2 - 15}" r="${cellW * 0.18}" fill="#09090b"/>
          <path d="M ${cellW / 2 - 40} ${cellH / 2 + 30} Q ${cellW / 2} ${cellH / 2 + 70} ${cellW / 2 + 40} ${cellH / 2 + 30}" stroke="#38bdf8" stroke-width="4" fill="none"/>
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
