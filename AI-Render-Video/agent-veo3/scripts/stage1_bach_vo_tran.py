"""Stage 1: Generate Batch 3 Candidates for Angle 0° (Bach Vo Tran)."""

import asyncio
import json
import logging
import os
import sys
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("stage1_bach_vo_tran")

PROJECT_ID = "50e2c64e-887c-495e-9da3-ae112aa9f385"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\bach-vo-tran"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Read mannequin reference for 0°
mannequin_refs_path = os.path.join(OUTPUT_DIR, "mannequin_refs.json")
mannequin_0_id = None
if os.path.exists(mannequin_refs_path):
    with open(mannequin_refs_path) as f:
        mrefs = json.load(f)
        mannequin_0_id = mrefs.get("0")

PROMPT_0 = (
    "2D Xianxia anime chibi character sprite — TRUE DIRECT 0-DEGREE FRONT VIEW.\n"
    "CRITICAL ANATOMICAL PROPORTION & SCALE LOCK: Mature stylized semi-chibi anime sprite proportion (~4.8 to 5.0 heads tall, full-body vertical height occupies 85-88% canvas height). "
    "Head size is proportionate and balanced with broader male shoulders; strictly ZERO oversized giant chibi head, ZERO shrunken tiny torso, ZERO bobblehead deformity. "
    "Athletic straight torso, slender waist, and long graceful legs grounded on floor plane.\n"
    "CAMERA & POSE: Character MUST face 100% DIRECTLY forward at camera (strict 0.0° front view). "
    "Symmetrical standing pose, torso upright, both shoulders horizontal and level, both arms relaxed at sides, empty hands. "
    "Both feet planted parallel and pointing directly forward towards viewer (12 o'clock).\n"
    "CRITICAL — BLANK FACELESS HEAD: Completely BLANK, SMOOTH, FEATURELESS face surface. "
    "NO eyes, NO eyebrows, NO nose, NO mouth. Facial skin tone MUST seamlessly match neck and hands (fair warm jade skin). "
    "Clean flat cel-shaded anime skin, strictly zero facial features.\n"
    "CHARACTER: Tiên Môn Kiếm Tử — Bạch Vô Trần (Bai Wuchen), male immortal sword cultivator, 20-22 years old appearance, tall poised celestial presence.\n"
    "HAIR: Silky long black hair tied into an elegant half-up celestial bun with a white jade Guan crown and silver hairpin, soft face-framing sidelocks, hair cascading down back.\n"
    "COSTUME: Layered pure snow-white daoist robe with pale icy cyan and silver cloud embroidery trim, cross-collar 2 layers, straight downward fabric drape under calm gravity (ZERO fake wind). "
    "Triple-layer silk waist sash with a round white jade cloud pendant. Strictly continuous flat belt band. "
    "SHOES: Flat white and pale cyan cloth daoist boots (CRITICAL: STRICTLY FLAT, ZERO HEELS, NO HIGH HEELS).\n"
    "WEAPON: None (empty hands, pure martial aura). "
    "STYLE: Pure flat 2D anime chibi sprite illustration, bold clean linework, flat cel-shaded coloring. "
    "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible from head to feet."
)


async def generate_single_candidate(session, cand_idx):
    cand_name = f"angle_0_c{cand_idx}"
    logger.info("Starting candidate %s...", cand_name)
    url = "http://127.0.0.1:8100/api/flow/generate-image"

    body = {
        "prompt": PROMPT_0,
        "project_id": PROJECT_ID,
        "aspect_ratio": "IMAGE_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
    }
    # Pass 0° mannequin reference to lock proportion if available
    if mannequin_0_id:
        body["character_media_ids"] = [mannequin_0_id]

    for attempt in range(4):
        try:
            logger.info("[%s attempt %d/4] Sending request to Flow API...", cand_name, attempt + 1)
            async with session.post(url, json=body, timeout=120) as resp:
                data = await resp.json()
                if "detail" in data and "Extension not connected" in str(data["detail"]):
                    logger.warning("%s: Extension reconnecting, waiting 5s...", cand_name)
                    await asyncio.sleep(5)
                    continue

                media_list = data.get("media", [])
                if not media_list:
                    logger.warning("%s failed or empty media: %s", cand_name, data)
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

                target_file = os.path.join(OUTPUT_DIR, f"{cand_name}.png")
                if img_url:
                    async with session.get(img_url) as img_resp:
                        content = await img_resp.read()
                        with open(target_file, "wb") as f:
                            f.write(content)
                    logger.info("Saved %s -> %s (media_id=%s, size=%d)", cand_name, target_file, mid, len(content))
                    return {"name": cand_name, "media_id": mid, "url": img_url, "file": target_file}
                elif "image" in m and "encodedImage" in m["image"]:
                    import base64
                    content = base64.b64decode(m["image"]["encodedImage"])
                    with open(target_file, "wb") as f:
                        f.write(content)
                    logger.info("Saved %s from base64 (media_id=%s)", cand_name, mid)
                    return {"name": cand_name, "media_id": mid, "url": None, "file": target_file}
        except Exception as e:
            logger.error("Error generating %s: %s", cand_name, e)
            await asyncio.sleep(4)

    return {"name": cand_name, "media_id": None, "url": None, "file": None}


async def main():
    logger.info("Generating batch 3 candidates for angle 0° (Bach Vo Tran)...")
    async with aiohttp.ClientSession() as session:
        # Run 3 candidates sequentially or small delay to ensure high success rate
        results = []
        for i in [1, 2, 3]:
            res = await generate_single_candidate(session, i)
            results.append(res)
            await asyncio.sleep(2)

    meta_file = os.path.join(OUTPUT_DIR, "candidates_0.json")
    with open(meta_file, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    logger.info("Stage 1 completed. Results saved to %s", meta_file)


if __name__ == "__main__":
    asyncio.run(main())
