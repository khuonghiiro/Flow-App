"""Stage 2: Generate Remaining Angles (45°, 90°, 180°, 135°) for Diep Thanh Lam."""

import asyncio
import base64
import json
import logging
import os
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("stage2_diep_thanh_lam")

PROJECT_ID = "7c7b4d45-2416-4902-b77a-1601a0aecff9"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\diep-thanh-lam"
MEDIA_ID_0 = "973a46d8-4d96-4657-aea8-ec7861080166"

with open(os.path.join(OUTPUT_DIR, "mannequin_refs.json")) as f:
    MANNEQUINS = json.load(f)

PROMPTS = {
    "45": (
        "ROTATE THE FEMALE CHARACTER 45 DEGREES — THREE-QUARTER VIEW — 2D Xianxia manhwa anime chibi sprite.\n"
        "FIRST AND MOST IMPORTANT INSTRUCTION: ROTATE the character's ENTIRE BODY 45 degrees to face 10 o'clock (upper-left diagonal). "
        "VISIBLE ROTATION PROOF: Left shoulder pushed forward toward viewer, right shoulder recedes in depth. "
        "Waist floral jade medallion shifted to the left hip area, NOT in center. "
        "Left foot stepped forward, right foot stepped back in depth. Both feet point towards 10 o'clock. "
        "CRITICAL SCALE & PROPORTIONS: Maintain EXACT same mature semi-chibi height and proportion (~4.8-5.0 heads tall, 85-88% canvas height) as Reference 0°. "
        "IDENTITY LOCK: Inherit hairstyle with twin butterfly loop buns, pale cyan crystal hairpins, two-layered daoist robes (white inner, pale icy azure outer with silver cloud embroidery), delicate waist sash with jade flower medallion. "
        "Blank smooth faceless head matching neck skin tone. Strictly flat cloth shoes with zero heels. "
        "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    ),
    "90": (
        "PURE 90-DEGREE SIDE PROFILE VIEW (FACING LEFT 9 O'CLOCK) — 2D Xianxia manhwa anime chibi sprite.\n"
        "MANDATORY 90-DEGREE SIDE PROFILE ROTATION: The female character stands in a STRICT 90-DEGREE SIDE PROFILE facing directly left towards 9 o'clock. "
        "ONLY THE LEFT PROFILE is visible. Left arm and shoulder face the viewer; right arm and shoulder are 100% COMPLETELY HIDDEN AND OCCLUDED behind torso. "
        "Both feet point directly towards the left edge of the screen (9 o'clock). "
        "Head is in pure 90-degree side profile showing one ear, jawline, and delicate hair loops and pins in side view. "
        "CRITICAL SCALE: Maintain EXACT same tall, slender height (~4.8-5.0 heads tall, 85-88% canvas height) as Reference 0°. "
        "IDENTITY LOCK: Inherit exact costume colors, pale icy azure robe, white inner layer, silver cloud trim, flat cloth shoes. "
        "Smooth blank featureless face in pure side silhouette. Strictly zero weapons or props. "
        "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    ),
    "180": (
        "ROTATE CHARACTER 180° — PERFECT DIRECT REAR VIEW — 2D XIANXIA MANHWA CHIBI FEMALE SPRITE\n"
        "CRITICAL ROTATION: Character faces 100% DIRECTLY AWAY from camera (strict 180.0° rear view). "
        "Full back view of hairstyle: twin butterfly loop buns viewed from behind, silver pins, and long silky black hair cascading down the spine past the waist. "
        "Full back of robes facing camera with bilateral symmetry. "
        "WAIST BELT REAR: Delicate butterfly ribbon knot with two soft cascading silk ribbons draping straight downward under calm gravity without fluttering. "
        "Both legs straight and symmetrical, heels facing camera in flat cloth shoes. "
        "IDENTITY LOCK: Match Reference 0° robe colors (white and pale icy azure with silver cloud patterns). "
        "CRITICAL SCALE: Maintain exact same height (~4.8-5.0 heads tall, 85-88% canvas height). "
        "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    ),
    "135": (
        "2D XIANXIA MANHWA ANIME CHIBI FEMALE SPRITE — TRUE 135-DEGREE BACK-LEFT THREE-QUARTER ANGLE.\n"
        "CRITICAL POSE COPY FROM REFERENCE 1 (FEMALE MANNEQUIN 135° POSE GUIDE):\n"
        "The character's entire body stance MUST strictly replicate the exact 135-degree posture in Reference 1:\n"
        "- Torso and spine are rotated at a 135-degree angle (facing diagonally away to the 8 o'clock direction).\n"
        "- Left shoulder, left arm, and left hip are prominent and close in the foreground.\n"
        "- Right shoulder and right arm are further away in depth, partially occluded by the angled torso.\n"
        "- FEET AND LEGS POSE: Strictly copy Reference 1. The left foot is rotated into profile facing left (9 o'clock). The right foot is angled diagonally away into depth. Both legs are clearly angled in three-quarter perspective.\n"
        "- STRICTLY FORBIDDEN: Symmetrical 180-degree rear-facing body, symmetrical shoulders, or symmetrical feet. The torso and feet MUST NOT be facing 180 degrees.\n"
        "COSTUME & HAIR IDENTITY (FROM REFERENCE 2):\n"
        "- Character wears the outfit from Reference 2: pure snow-white and pale icy azure layered robes with silver cloud embroidery.\n"
        "- Hairstyle: Twin butterfly loop buns with crystal pins and cascading long black hair viewed from 135° rear-left.\n"
        "- Waist rear sash: Delicate butterfly bow with cascading ribbons viewed at 45-degree angle offset.\n"
        "- Footwear: Flat cloth shoes (strictly flat, zero heels).\n"
        "- Face/Head: Side profile of fair warm ivory cheek, jawline, delicate ear, and sideburns viewed from behind-left.\n"
        "SCALE & FABRIC PHYSICS:\n"
        "- Maintain mature semi-chibi proportion (~4.8-5.0 heads tall, occupies 85-88% canvas height).\n"
        "- Natural fabric drape under calm gravity, strictly zero fake wind blowing.\n"
        "BACKGROUND: Solid chroma-key green #00FF00. Centered, full body visible."
    ),
}


async def generate_angle(session: aiohttp.ClientSession, angle_key: str, refs: list[str]) -> tuple[str, str]:
    url = "http://127.0.0.1:8100/api/flow/generate-image"
    prompt = PROMPTS[angle_key]
    payload = {
        "prompt": prompt,
        "project_id": PROJECT_ID,
        "aspect_ratio": "IMAGE_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
        "character_media_ids": refs,
    }

    logger.info("Generating angle %s° with refs: %s", angle_key, refs)
    for attempt in range(3):
        try:
            async with session.post(url, json=payload, timeout=120) as resp:
                data = await resp.json()
                media_list = data.get("media", [])
                if not media_list:
                    logger.warning("Attempt %d empty for %s°: %s", attempt + 1, angle_key, data)
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
                )
                encoded = gen_img.get("encodedImage")

                img_bytes = None
                if img_url:
                    async with session.get(img_url) as img_resp:
                        img_bytes = await img_resp.read()
                elif encoded:
                    img_bytes = base64.b64decode(encoded)

                if img_bytes:
                    out_path = os.path.join(OUTPUT_DIR, f"angle_{angle_key}.png")
                    with open(out_path, "wb") as f:
                        f.write(img_bytes)
                    logger.info("Successfully saved angle %s° -> %s (mid=%s, size=%d)", angle_key, out_path, mid, len(img_bytes))
                    return mid, out_path
        except Exception as e:
            logger.error("Error generating %s° (attempt %d): %s", angle_key, attempt + 1, e)
            await asyncio.sleep(4)

    raise RuntimeError(f"Failed to generate angle {angle_key}° after 3 attempts")


async def main():
    async with aiohttp.ClientSession() as session:
        meta_path = os.path.join(OUTPUT_DIR, "character_meta.json")
        with open(meta_path) as mf:
            meta = json.load(mf)

        # Phase 2A: 45°, 90°, 180°
        angles_2a = [
            ("45", [MEDIA_ID_0, MANNEQUINS["45"]]),
            ("90", [MEDIA_ID_0, MANNEQUINS["90"]]),
            ("180", [MEDIA_ID_0, MANNEQUINS["180"]]),
        ]

        for ang, refs in angles_2a:
            mid, path = await generate_angle(session, ang, refs)
            meta[f"angle_{ang}"] = {"media_id": mid, "file": f"angle_{ang}.png", "status": "COMPLETED"}
            with open(meta_path, "w", encoding="utf-8") as mf:
                json.dump(meta, mf, indent=2, ensure_ascii=False)
            await asyncio.sleep(3)

        # Phase 2B: 135° (Pose guide first!)
        media_180 = meta["angle_180"]["media_id"]
        mid_135, path_135 = await generate_angle(session, "135", [MANNEQUINS["135"], media_180])
        meta["angle_135"] = {"media_id": mid_135, "file": "angle_135.png", "status": "COMPLETED"}
        with open(meta_path, "w", encoding="utf-8") as mf:
            json.dump(meta, mf, indent=2, ensure_ascii=False)

        print("\n[DONE] Stage 2 Completed! All 5 angles generated successfully.")


if __name__ == "__main__":
    asyncio.run(main())
