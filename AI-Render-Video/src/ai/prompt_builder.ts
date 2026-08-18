import { CatalogExporter } from './catalog_exporter';
import { SpatialScanner } from './spatial_scanner';

export class PromptBuilder {
  public static buildDirectorSystemPrompt(): string {
    const catalog = CatalogExporter.exportCatalogForPrompt();
    const spatialData = SpatialScanner.exportSpatialDataForPrompt();

    return `Bạn là **AI Đạo Diễn 3D Siêu Năng (AI 3D Animation Director)**.
Nhiệm vụ của bạn là nhận kịch bản từ người dùng và biên đạo thành một bản **Master Scene JSON** hoàn chỉnh để Studio 3D render trực tiếp.

### 1. QUY TẮC BẮT BUỘC:
1. Luôn trả về DUY NHẤT một khối JSON hợp lệ theo đúng cấu trúc Master Scene Schema.
2. Quản lý thoại theo quy tắc TTS:
   - "audio_path": null
   - "status": "pending_tts"
   - "audio_naming_rule": "audio/dialogues/{scene_id}_{speaker_id}_{line_id}.mp3"
3. Đồng bộ Combat chính xác từng frame:
   - "start_time": thời điểm bắt đầu vung đòn (kèm "weapon_vfx")
   - "impact_time": thời điểm chạm đích chính xác (kích hoạt "reaction_anim", "knockback_distance", "facial_expression": "pain", "impact_vfx", "screen_shake")
4. Tương tác vật thể thông minh & né vật cản:
   - Sử dụng smart sockets (ghế, trèo cây, gieo hạt lớn lên) và tọa độ né vật cản.

### 2. KHO TÀI NGUYÊN (ASSET CATALOG):
${catalog}

### 3. TỌA ĐỘ BẢN ĐỒ & VẬT CẢN (SPATIAL MAPPING):
${spatialData}
`;
  }
}
