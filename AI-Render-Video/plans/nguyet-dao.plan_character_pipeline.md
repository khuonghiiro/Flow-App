# Plan Nhân Vật: Băng Nguyệt Tiên Tử — Nguyệt Dao (Lunar Frost Fairy)

> **File:** `plans/nguyet-dao.plan_character_pipeline.md`  
> **Output Folder:** `agent-veo3/output/nguyet-dao/`  
> **Project ID:** `853ab682-e875-48a8-a652-9d12d85cfa2d`  
> **Trực tiếp trên Google Flow:** [https://labs.google/fx/tools/flow/project/853ab682-e875-48a8-a652-9d12d85cfa2d](https://labs.google/fx/tools/flow/project/853ab682-e875-48a8-a652-9d12d85cfa2d)  
> **Trạng thái:** ĐANG TRIỂN KHAI PIPELINE 3 TẦNG  
> **Mô tả:** Nữ Kiếm Tiên thiên tài Băng Nguyệt Kiếm Tông, sở hữu Cửu U Nguyệt Thể. Thiếu nữ 19-20 tuổi, thân hình thanh thoát nhẹ tựa sương khói (167cm, 47kg). Mái tóc bạch ngân ánh trăng suôn mượt cài trâm hoa sen lam ngọc, đạo bào lụa trắng tinh khôi hai lớp xẻ tà thanh nhã thêu vân băng bạc, đai ngọc lam thắt lụa tím lưu ly với mặt ngọc trăng khuyết, hài thêu hoa sen đế phẳng phát sáng hàn băng (zero heels), kiếm pha lê Huyền Băng Linh Kiếm sau lưng.

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Băng Nguyệt Tiên Tử — Nguyệt Dao (Lunar Frost Fairy)"
  gender: "female"
  age: "young adult (looks 19-20 years old)"
  body_type: "slender graceful ethereal feminine physique, light as moonlight mist (167cm, 47kg)"
  personality: "Thanh lãnh, thoát tục, điềm tĩnh dịu dàng, thần thái tựa tiên nữ giáng trần."
  combat_style: "Huyền Băng Kiếm Quyết / Nguyệt Hoa Linh Vũ / Cửu U Băng Trận (Lunar Frost Sword Dance)"
  
  # Ngoại hình
  hair: "Silky long flowing moonlight silvery-white hair tied in an elegant half-up bun with a crystal lotus hairpin and twin floating translucent cyan silk ribbons"
  skin: "Luminous fair porcelain jade skin tone with delicate soft ethereal glow"
  outfit: "Pure snow-white ethereal daoist silk robe embroidered with delicate silver frost patterns, layered translucent cyan-tinted slit overskirt with flowing side slits, soft lavender lilac waist sash with a crescent moon jade pendant, soft white embroidered lotus cloth shoes with faint glowing icy cyan energy soles (strictly flat, zero heels)"
  primary_color: "Moonlight Silver White & Translucent Icy Cyan (#FFFFFF, #80DEEA, #00E5FF)"
  accent_color: "Soft Lavender Lilac & Frosted Silver (#CE93D8, #E0E0E0)"
  
  # Vũ khí & Pháp bảo
  weapon: "Huyền Băng Linh Kiếm (Translucent Icy Crystal Sword) strapped diagonally across the back with a cyan jade tassel"
  spell_element: "Lunar Frost Qi & Icy Moonlight Sparkles (#B2EBF2)"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi sprite, bold clean linework, flat cel-shaded coloring"
  chroma_bg: "#00FF00"
```

---

## Phần B — Registry 5 Góc Xoay Cơ Thể (Tầng 1 & 2)

> **Tiêu chuẩn 10/10**: 
> 1. Đồng nhất 100% chi tiết: Tóc bạch ngân ánh trăng, đạo bào trắng tà xanh ngọc xuyên thấu, trâm hoa sen lam ngọc, kiếm băng pha lê trên lưng, hài đế phẳng phát sáng sương lam (zero heels).
> 2. Các góc xoay chuẩn xác: 0° (chính diện), 45° (chéo 3/4 trước), 90° (mạn sườn trái), 135° (chéo 3/4 sau), 180° (sau lưng đối xứng).

| Góc Xoay | Hướng Quan Sát | Trạng Thái | Media ID (Google Flow) | File Ảnh Cục Bộ |
|:---:|:---|:---:|:---:|:---|
| **0°** | Chính diện (Front View) | `PENDING` | — | `angle_0.png` |
| **45°** | Chéo 3/4 nhìn sang trái (10h) | `PENDING` | — | `angle_45.png` |
| **90°** | Nghiêng cạnh sườn 90° (9h) | `PENDING` | — | `angle_90.png` |
| **135°** | Lưng chéo 3/4 nhìn từ sau | `PENDING` | — | `angle_135.png` |
| **180°** | Sau lưng hoàn toàn (Rear View) | `PENDING` | — | `angle_180.png` |

---

## Phần C — Registry 25 Video 4s Seamless Loop (Tầng 3)

> **Quy chuẩn chuyển động mới**:
> 1. `walk`: Bước chân uyển chuyển thanh thoát nhẹ như lướt trên sương mai; tại góc 45° chân sải thẳng dọc theo trục chéo 45° (không đi ngang).
> 2. `idle`: Đứng yên tĩnh tại, gió núi thổi lay tà áo lụa, dải tóc bạch ngân và ngọc bội trăng khuyết; ngực phập phồng thở êm dịu, tay thả lỏng nhẹ nhàng.
> 3. `hair`: Tóc mọc chân thực từ da đầu, chuyển động quán tính mềm mại tự nhiên, cấm giật cục như tóc giả.
> 4. `output`: Tải video 1080p về các folder con `di-bo/`, `dung-yen/`, `chay/`, `danh-cong/`, `phong-thu/`.
