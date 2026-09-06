"""Stage 2: Generate Pha 2A Candidates for 45°, 90°, 180° (Bach Vo Tran)."""

import asyncio
import json
import logging
import os
import sys
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("stage2_bach_vo_tran")

PROJECT_ID = "50e2c64e-887c-495e-9da3-ae112aa9f385"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\bach-vo-tran"
MEDIA_ID_0 = "1fe21225-dfdd-4290-aecb-6717ef01734c"

# Load mannequin references
with open(os.path.join(OUTPUT_DIR, "mannequin_refs.json")) as f:
    MANNEQUINS = json.load(f)

PROMPTS = {
    "45": (
        "2D Xianxia anime chibi character sprite — TRUE DEEP 45-DEGREE THREE-QUARTER ISOMETRIC VIEW.\n"
        "FIRST AND MOST IMPORTANT INSTRUCTION: ROTATE the character's ENTIRE BODY 45 degrees to face 10 o'clock (upper-left diagonal). STRICTLY FORBIDDEN to face camera directly. "
        "ASYMMETRICAL 3/4 DEPTH POSE: Left shoulder and left foot forward in foreground, right shoulder and right foot receding backward in depth. Chest plane visibly rotated 45 degrees diagonally away from camera. "
        "BLANK FACELESS HEAD: Completely blank smooth porcelain mannequin head turned 45 degrees left (NO eyes, NO nose, NO mouth). "
        "IDENTITY LOCK: Match 0° reference: Bạch Vô Trần, long silky black hair half-up celestial bun with white jade Guan crown and silver hairpin, pure snow-white daoist robe with pale icy cyan and silver cloud embroidery trim, triple-layer sash with round white jade cloud medallion, flat white and pale cyan cloth boots (STRICTLY FLAT, ZERO HEELS), empty hands. "
        "STYLE: Pure flat 2D anime chibi sprite illustration, clean linework, flat cel-shaded coloring. BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    ),
    "90": (
        "2D Xianxia anime chibi character sprite — TRUE 90-DEGREE PURE SIDE PROFILE VIEW.\n"
        "CAMERA & POSE: Character stands in strict 90-degree left side silhouette (facing exactly 9 o'clock direction). "
        "PURE LATERAL FLANK VIEW: Only left side of character is visible: left side of head, left shoulder, left sleeve, left side of white robe, left hip, left leg, left flat cloth boot pointing directly to 9 o'clock. "
        "Right arm, right shoulder, and right leg are 100% occluded directly behind the body and invisible. STRICTLY ZERO FRONT VIEW, ZERO FRONT CHEST. Pure flank side silhouette. "
        "IDENTITY LOCK: Match 0° reference: Bạch Vô Trần, long black hair cascading backward, white jade crown with silver hairpin, pure white daoist robe with cyan embroidery, flat boots. "
        "BLANK FACELESS HEAD: Pure smooth blank mannequin head in left side profile outline (NO eyes, NO nose, NO mouth). "
        "STYLE: Pure flat 2D anime chibi sprite illustration, bold clean linework, flat cel-shaded coloring. BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    ),
    "180": (
        "2D Xianxia anime chibi character sprite — TRUE 180-DEGREE FULL REAR VIEW.\n"
        "CAMERA & POSE: Character stands with spine vertical facing 100% DIRECTLY AWAY from camera (180° back view). "
        "CRITICAL: Symmetrical back view. Full back of silky black hair cascading down past waist, white jade Guan crown and silver hairpin viewed from behind, symmetrical back of pure snow-white daoist robe with silver cloud embroidery. "
        "CRITICAL BELT BACK RULE: Plain flat continuous silk belt band behind back, STRICTLY ZERO BOW, ZERO RIBBON KNOT. "
        "Both heels facing camera, feet pointing away symmetrically. Flat cloth daoist boots (STRICTLY FLAT, ZERO HEELS). "
        "IDENTITY LOCK: Match 0° reference character costume colors, white robe, pale cyan accents. Empty hands at sides. "
        "STYLE: Pure flat 2D anime chibi sprite illustration, clean linework, flat cel-shaded coloring. BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    )
}


async def generate_candidate(session, angle, cand_idx):
    cand_name = f"angle_{angle}_c{cand_idx}"
    prompt = PROMPTS[angle]
    mannequin_id = MANNEQUINS.get(angle)
    refs = [MEDIA_ID_0]
    if mannequin_id:
        refs.append(mannequin_id)

    logger.info("Starting %s with Dual-Ref: %s...", cand_name, refs)
    url = "http://127.0.0.1:8100/api/flow/generate-image"
    body = {
        "prompt": prompt,
        "project_id": PROJECT_ID,
        "aspect_ratio": "IMAGE_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
        "character_media_ids": refs,
    }

    for attempt in range(4):
        try:
            async with session.post(url, json=body, timeout=120) as resp:
                data = await resp.json()
                media_list = data.get("media", [])
                if not media_list:
                    logger.warning("%s attempt %d empty response: %s", cand_name, attempt + 1, data)
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
                    logger.info("Saved %s -> %s (media_id=%s)", cand_name, target_file, mid)
                    return {"name": cand_name, "angle": angle, "media_id": mid, "url": img_url, "file": target_file}
                elif "image" in m and "encodedImage" in m["image"]:
                    import base64
                    content = base64.b64decode(m["image"]["encodedImage"])
                    with open(target_file, "wb") as f:
                        f.write(content)
                    logger.info("Saved %s from base64 (media_id=%s)", cand_name, mid)
                    return {"name": cand_name, "angle": angle, "media_id": mid, "url": None, "file": target_file}
        except Exception as e:
            logger.error("Error in %s: %s", cand_name, e)
            await asyncio.sleep(4)

    return {"name": cand_name, "angle": angle, "media_id": None, "url": None, "file": None}


async def main():
    logger.info("Starting Stage 2 Pha 2A: 45°, 90°, 180° (Bach Vo Tran)...")
    results = {}
    async with aiohttp.ClientSession() as session:
        # Generate candidates for each angle
        for ang in ["45", "90", "180"]:
            for c in [1, 2]:
                res = await generate_candidate(session, ang, c)
                results[res["name"]] = res
                await asyncio.sleep(2)

    with open(os.path.join(OUTPUT_DIR, "candidates_stage2a.json"), "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    logger.info("Pha 2A complete!")


if __name__ == "__main__":
    asyncio.run(main())
