"""Regenerate Angle 135° with strict pose matching to Mannequin 135."""

import asyncio
import base64
import json
import logging
import os
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("fix_135")

PROJECT_ID = "50e2c64e-887c-495e-9da3-ae112aa9f385"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\bach-vo-tran"

MEDIA_ID_0 = "1fe21225-dfdd-4290-aecb-6717ef01734c"
MEDIA_ID_180 = "fa506703-05e0-4c8c-bff1-39b4281d3479"
MANNEQUIN_135 = "f5a68bda-d5b0-47ba-b236-dc2fbc19b915"

PROMPT_FIX_135 = (
    "2D Xianxia anime chibi character sprite — TRUE 135-DEGREE BACK-LEFT THREE-QUARTER ANGLE.\n"
    "CRITICAL POSE COPY FROM REFERENCE 1 (MANNEQUIN POSE GUIDE):\n"
    "The character's entire body stance MUST strictly replicate the exact 135-degree posture in Reference 1:\n"
    "- Torso and spine are rotated at a 135-degree angle (facing diagonally away to the 8 o'clock direction).\n"
    "- Left shoulder, left arm, and left hip are prominent and close in the foreground.\n"
    "- Right shoulder and right arm are further away in depth, partially occluded by the angled torso.\n"
    "- FEET AND LEGS POSE: Strictly copy Reference 1. The left foot is rotated into profile facing left. The right foot is angled diagonally away. Both legs are clearly angled in three-quarter perspective.\n"
    "- STRICTLY FORBIDDEN: Symmetrical 180-degree rear-facing body, symmetrical shoulders, or symmetrical feet. The torso and feet MUST NOT be facing 180 degrees.\n"
    "COSTUME & HAIR (FROM REFERENCE 2):\n"
    "- Character wears the pure snow-white daoist robe with pale icy cyan trim and silver cloud patterns.\n"
    "- Long silky black hair with half-up topknot secured by white jade Guan crown and silver hairpin viewed from behind-left.\n"
    "- Flat continuous fabric waist belt behind back (ZERO BOW, ZERO RIBBON KNOT).\n"
    "- Strictly flat white cloth daoist boots (ZERO HEELS).\n"
    "- Face/Head: Side profile of fair warm ivory cheek and jawline, delicate ear, two thin sideburn strands hugging the cheek, viewed from behind-left.\n"
    "STYLE & BG:\n"
    "- Pure 2D Xianxia anime chibi sprite, clean line art, flat cel-shading, 4.8-5.0 heads ratio.\n"
    "- Solid chroma-key green #00FF00 background. Full body centered from head to feet."
)


async def main():
    # Setup candidates:
    # 1. [MANNEQUIN_135, MEDIA_ID_180] (Pose guide first, then back view costume)
    # 2. [MANNEQUIN_135, MEDIA_ID_0]   (Pose guide first, then front master costume)
    url = "http://127.0.0.1:8100/api/flow/generate-image"
    results = []

    setups = [
        ("mannequin_first_with_180", [MANNEQUIN_135, MEDIA_ID_180]),
        ("mannequin_first_with_0", [MANNEQUIN_135, MEDIA_ID_0]),
    ]

    async with aiohttp.ClientSession() as session:
        cand_idx = 1
        for label, refs in setups:
            logger.info("Generating candidate with setup: %s, refs: %s", label, refs)
            body = {
                "prompt": PROMPT_FIX_135,
                "project_id": PROJECT_ID,
                "aspect_ratio": "IMAGE_ASPECT_RATIO_PORTRAIT",
                "user_paygate_tier": "PAYGATE_TIER_TWO",
                "character_media_ids": refs,
            }

            try:
                async with session.post(url, json=body, timeout=120) as resp:
                    data = await resp.json()
                    media_list = data.get("media", [])
                    if not media_list:
                        logger.warning("Setup %s returned no media: %s", label, data)
                        continue

                    for m in media_list:
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
                        encoded = gen_img.get("encodedImage")

                        img_bytes = None
                        if img_url:
                            async with session.get(img_url) as img_resp:
                                img_bytes = await img_resp.read()
                        elif encoded:
                            img_bytes = base64.b64decode(encoded)

                        if not img_bytes:
                            logger.warning("Could not obtain image bytes for candidate %d", cand_idx)
                            continue

                        out_path = os.path.join(OUTPUT_DIR, f"angle_135_fix_{cand_idx}.png")
                        with open(out_path, "wb") as f:
                            f.write(img_bytes)

                        logger.info("Candidate %d saved to %s (media_id=%s, size=%d bytes)",
                                    cand_idx, out_path, mid, len(img_bytes))
                        results.append({
                            "candidate": cand_idx,
                            "setup": label,
                            "media_id": mid,
                            "file": f"angle_135_fix_{cand_idx}.png",
                            "path": out_path,
                        })
                        cand_idx += 1
            except Exception as e:
                logger.error("Error generating setup %s: %s", label, e)

            await asyncio.sleep(3)

    out_meta = os.path.join(OUTPUT_DIR, "fixed_135_candidates.json")
    with open(out_meta, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    print(f"\n[DONE] Generated {len(results)} candidates! See {out_meta}")


if __name__ == "__main__":
    asyncio.run(main())
