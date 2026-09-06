"""Stage 1: Generate 3 Candidates for 0° Direct Front View (Diep Thanh Lam)."""

import asyncio
import base64
import json
import logging
import os
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("stage1_diep_thanh_lam")

PROJECT_ID = "7c7b4d45-2416-4902-b77a-1601a0aecff9"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\diep-thanh-lam"
os.makedirs(OUTPUT_DIR, exist_ok=True)
os.makedirs(os.path.join(OUTPUT_DIR, "dung-yen"), exist_ok=True)

FEMALE_MANNEQUINS_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\public\mannequins\female"

PROMPT_0 = (
    "MASTER CHARACTER DESIGN — 2D XIANXIA/MANHWA CHIBI FEMALE SPRITE — 0° DIRECT FRONT VIEW\n"
    "CRITICAL ANATOMICAL PROPORTION & SCALE LOCK: Mature stylized semi-chibi manhwa anime sprite proportion (~4.8 to 5.0 heads tall, full-body vertical height occupies 85-88% canvas height). "
    "Head size is proportionate and balanced with delicate female shoulders; strictly ZERO oversized giant chibi head, ZERO shrunken tiny torso, ZERO bobblehead deformity. "
    "Slender graceful feminine waist, long elegant legs grounded on floor plane.\n"
    "CAMERA & POSE: Character MUST face 100% DIRECTLY forward at camera (strict 0.0° front view). "
    "Head facing 100% straight forward towards viewer. NO head turn, NO head tilt, NO 3/4 angle. "
    "Symmetrical standing pose, torso upright, shoulders level, both arms relaxed straight down along sides, empty delicate hands. "
    "Both feet planted parallel and pointing directly forward towards viewer (12 o'clock). "
    "NATURAL FABRIC DRAPE UNDER GRAVITY: Robes, sleeves, and dress hems hang straight down naturally under calm gravity. Strictly NO wind blowing, NO billowing fabric, NO flapping hems. "
    "CRITICAL — BLANK FACELESS HEAD & UNIFORM JADE SKIN: Completely BLANK, SMOOTH, FEATURELESS face surface. "
    "NO eyes, NO eyebrows, NO nose, NO mouth. Facial skin color MUST seamlessly and uniformly match the neck and hands (fair warm ivory jade skin tone) with 100% consistency. "
    "CRITICAL — ZERO WEAPONS OR PROPS: Strictly NO weapons, NO swords, NO props. Hands are empty and resting naturally. "
    "HAIR: Long silky ink-black hair styled with celestial twin butterfly loop buns, delicate pale icy cyan crystal hairpin and silver hair ornaments, two soft face-framing sidelocks hugging cheeks, back hair cascading smoothly down past waist. "
    "OUTFIT: Two-layered flowing Xianxia daoist robes: pure snow-white silk inner robe with cross-collar, translucent pale icy azure and mist cyan outer silk robe with delicate silver cloud embroidery trim, wide layered flowing sleeves hanging down, multi-layered flowing dress hem draping naturally to floor, delicate waist sash with pale jade floral medallion. "
    "SHOES: Strictly flat white cloth lotus shoes with zero heels (NO high heels). "
    "COLORS: Pure Snow White & Pale Icy Azure, Soft Celestial Silver & Mist Cyan accents. "
    "STYLE: Pure flat 2D Xianxia manhwa anime chibi illustration, bold clean linework, flat cel-shaded coloring. "
    "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible from head crown to feet."
)


async def upload_mannequins(session: aiohttp.ClientSession) -> dict:
    url = "http://127.0.0.1:8100/api/flow/upload-image"
    angles = ["0", "45", "90", "135", "180"]
    uploaded = {}
    for a in angles:
        fpath = os.path.join(FEMALE_MANNEQUINS_DIR, f"angle_{a}.png")
        fname = f"mannequin_female_{a}.png"
        payload = {
            "file_path": fpath,
            "project_id": PROJECT_ID,
            "file_name": fname,
        }
        logger.info("Uploading female mannequin %s°...", a)
        async with session.post(url, json=payload, timeout=30) as resp:
            data = await resp.json()
            mid = data.get("media_id")
            uploaded[a] = mid
            logger.info("Uploaded mannequin %s° -> media_id: %s", a, mid)
    return uploaded


async def main():
    async with aiohttp.ClientSession() as session:
        # Step 1: Upload mannequins
        mannequin_map_file = os.path.join(OUTPUT_DIR, "mannequin_refs.json")
        if not os.path.exists(mannequin_map_file):
            mannequin_map = await upload_mannequins(session)
            with open(mannequin_map_file, "w") as f:
                json.dump(mannequin_map, f, indent=2)
            logger.info("Saved mannequin map: %s", mannequin_map)
        else:
            with open(mannequin_map_file) as f:
                mannequin_map = json.load(f)
            logger.info("Loaded existing mannequin map: %s", mannequin_map)

        # Step 2: Generate 3 candidates for 0°
        gen_url = "http://127.0.0.1:8100/api/flow/generate-image"
        mannequin_0 = mannequin_map.get("0")
        refs = [mannequin_0] if mannequin_0 else []

        candidates = []
        for i in range(1, 4):
            logger.info("Generating Candidate %d for 0°...", i)
            payload = {
                "prompt": PROMPT_0,
                "project_id": PROJECT_ID,
                "aspect_ratio": "IMAGE_ASPECT_RATIO_PORTRAIT",
                "user_paygate_tier": "PAYGATE_TIER_TWO",
                "character_media_ids": refs,
            }
            try:
                async with session.post(gen_url, json=payload, timeout=90) as resp:
                    data = await resp.json()
                    media_list = data.get("media", [])
                    if not media_list:
                        logger.warning("Candidate %d returned no media: %s", i, data)
                        continue
                    m = media_list[0]
                    mid = m.get("name")
                    gen_img = m.get("image", {}).get("generatedImage", {})
                    img_url = (
                        m.get("fifeUrl")
                        or m.get("servingUri")
                        or gen_img.get("fifeUrl")
                        or gen_img.get("servingUri")
                    )
                    encoded = gen_img.get("encodedImage")

                    img_bytes = None
                    if img_url:
                        async with session.get(img_url) as img_resp:
                            img_bytes = await img_resp.read()
                    elif encoded:
                        img_bytes = base64.b64decode(encoded)

                    if not img_bytes:
                        logger.warning("No image bytes for candidate %d", i)
                        continue

                    out_path = os.path.join(OUTPUT_DIR, f"candidate_0_{i}.png")
                    with open(out_path, "wb") as f:
                        f.write(img_bytes)

                    logger.info("Candidate %d saved: %s (media_id=%s, size=%d)", i, out_path, mid, len(img_bytes))
                    candidates.append({
                        "candidate": i,
                        "media_id": mid,
                        "file": f"candidate_0_{i}.png",
                        "path": out_path,
                        "size_bytes": len(img_bytes),
                    })
            except Exception as e:
                logger.error("Error generating candidate %d: %s", i, e)

            await asyncio.sleep(2)

        meta_file = os.path.join(OUTPUT_DIR, "candidates_0.json")
        with open(meta_file, "w", encoding="utf-8") as f:
            json.dump(candidates, f, indent=2, ensure_ascii=False)

        print(f"\n[DONE] Generated {len(candidates)} candidates for 0°! See {meta_file}")


if __name__ == "__main__":
    asyncio.run(main())
