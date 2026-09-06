"""Stage 2 Pha 2B: Generate 135° Candidate for Bach Vo Tran."""

import asyncio
import json
import logging
import os
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("stage2b_bach_vo_tran")

PROJECT_ID = "50e2c64e-887c-495e-9da3-ae112aa9f385"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\bach-vo-tran"
MEDIA_ID_180 = "fa506703-05e0-4c8c-bff1-39b4281d3479"

with open(os.path.join(OUTPUT_DIR, "mannequin_refs.json")) as f:
    MANNEQUINS = json.load(f)

MANNEQUIN_135 = MANNEQUINS.get("135")

PROMPT_135 = (
    "2D Xianxia anime chibi character sprite — TRUE 135-DEGREE BACK-LEFT THREE-QUARTER PERSPECTIVE.\n"
    "CAMERA & POSE: Character viewed from behind-left at 135-degree angle (facing diagonal 8 o'clock away from camera). "
    "CRITICAL: Full view of back-left of head, silky black hair with white jade Guan crown and silver hairpin viewed from behind-left angle. "
    "Back of pure snow-white daoist robe with silver cloud embroidery. "
    "Left shoulder, left side of torso, and left heel visible in foreground; right shoulder angled away in depth. "
    "CRITICAL BELT: Flat plain continuous silk belt band behind back, STRICTLY ZERO BOW, ZERO RIBBON KNOT. "
    "SHOES: Flat white cloth daoist boots (CRITICAL: STRICTLY FLAT, ZERO HEELS, NO HIGH HEELS). "
    "IDENTITY LOCK: Match reference character costume colors, white robe, pale cyan accents. "
    "STYLE: Pure flat 2D anime chibi sprite illustration, clean linework, flat cel-shaded coloring. "
    "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
)


async def main():
    refs = [MEDIA_ID_180]
    if MANNEQUIN_135:
        refs.append(MANNEQUIN_135)

    logger.info("Generating 135° candidate with Dual-Ref: %s...", refs)
    url = "http://127.0.0.1:8100/api/flow/generate-image"
    body = {
        "prompt": PROMPT_135,
        "project_id": PROJECT_ID,
        "aspect_ratio": "IMAGE_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
        "character_media_ids": refs,
    }

    async with aiohttp.ClientSession() as session:
        for attempt in range(4):
            try:
                async with session.post(url, json=body, timeout=120) as resp:
                    data = await resp.json()
                    media_list = data.get("media", [])
                    if not media_list:
                        logger.warning("Attempt %d empty media: %s", attempt + 1, data)
                        await asyncio.sleep(4)
                        continue

                    m = media_list[0]
                    mid = m.get("name")
                    gen_img = m.get("image", {}).get("generatedImage", {})
                    img_url = (
                        m.get("fifeUrl")
                        or m.get("servingUri")
                        or gen_img.get("fifeUrl")
                        or gen_img.get("servingUri")
                        or m.get("image", {}).get("fifeUrl")
                        or m.get("image", {}).get("servingUri")
                    )

                    target_file = os.path.join(OUTPUT_DIR, "angle_135.png")
                    if img_url:
                        async with session.get(img_url) as img_resp:
                            content = await img_resp.read()
                            with open(target_file, "wb") as f:
                                f.write(content)
                        logger.info("Saved 135° -> %s (media_id=%s)", target_file, mid)

                        # Update metadata
                        meta_path = os.path.join(OUTPUT_DIR, "character_meta.json")
                        with open(meta_path) as mf:
                            meta = json.load(mf)
                        meta["angle_135"] = {"media_id": mid, "file": "angle_135.png", "status": "COMPLETED"}
                        with open(meta_path, "w", encoding="utf-8") as mf:
                            json.dump(meta, mf, indent=2, ensure_ascii=False)

                        return
            except Exception as e:
                logger.error("Error in 135° attempt %d: %s", attempt + 1, e)
                await asyncio.sleep(4)


if __name__ == "__main__":
    asyncio.run(main())
