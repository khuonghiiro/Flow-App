# 👤 THÂN HÌNH CƠ BẢN (Base Bodies)

## Mục đích
Chứa model nhân vật đầy đủ bộ xương Humanoid, dùng làm nền tảng để gắn các bộ phận modular (mặt, tóc, trang phục).

## Định dạng
- `.vrm` (khuyến nghị — có BlendShapes cho lip-sync và biểu cảm)
- `.glb` (cho nhân vật không cần lip-sync)

## Quy tắc đặt tên
```
{giới_tính}_{vai_trò}.vrm
```
Ví dụ: `male_warrior.vrm`, `female_mage.vrm`, `child_body.vrm`

## Yêu cầu kỹ thuật cho VRM
- Bộ xương: Humanoid chuẩn (đầu, cổ, cột sống, vai, tay, chân)
- BlendShapes lip-sync: `aa`, `ih`, `ou`, `ee`, `oh`
- BlendShapes biểu cảm: `blink`, `smile`, `angry`, `sorrow`, `surprised`
