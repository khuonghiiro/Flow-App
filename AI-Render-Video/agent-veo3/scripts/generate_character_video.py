"""Stage 3: Generate 4s Seamless Loop Action Video for Bach Vo Tran."""

import asyncio
import json
import logging
import os
import sys
import uuid
import aiohttp

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("generate_video")

PROJECT_ID = "50e2c64e-887c-495e-9da3-ae112aa9f385"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\bach-vo-tran"
MEDIA_ID_0 = "1fe21225-dfdd-4290-aecb-6717ef01734c"

PROMPT_IDLE_0 = (
    "2D Xianxia anime chibi sprite character standing still idle breathing animation, seamless 4-second loop.\n"
    "TRANQUIL SOFT BREEZE: Faint whisper-soft gentle breeze caresses the character. Silky black hair tips, sidelocks, and white robe lower hems waft with tender delicate micro-motion.\n"
    "NATURAL RHYTHMIC BREATHING: Subtle, calm, rhythmic rise and fall of chest breathing. Body, head, and feet remain completely stationary and grounded on floor plane.\n"
    "CRITICAL ARM IMMOBILITY LOCK: Arms hang completely relaxed straight down at sides and DO NOT MOVE. Empty hands remain still.\n"
    "PRESERVATION LOCK: Completely blank, smooth mannequin head remains 100% intact with uniform skin tone. STRICTLY ZERO facial features, ZERO mouth opening. STRICTLY ZERO weapons or props. Flat cel-shaded anime colors.\n"
    "Solid chroma-key green #00FF00 background. Seamless 4s loop where start frame equals end frame."
)


async def main():
    logger.info("Submitting 4s Idle Loop video for Bach Vo Tran (0°)...")
    url_gen = "http://127.0.0.1:8100/api/flow/generate-video"
    url_check = "http://127.0.0.1:8100/api/flow/check-status"

    body = {
        "start_image_media_id": MEDIA_ID_0,
        "end_image_media_id": MEDIA_ID_0,
        "prompt": PROMPT_IDLE_0,
        "project_id": PROJECT_ID,
        "scene_id": f"scene_idle_{uuid.uuid4().hex[:6]}",
        "aspect_ratio": "VIDEO_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
        "duration": 4.0,
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(url_gen, json=body, timeout=60) as resp:
            data = await resp.json()
            logger.info("Video submission response: %s", json.dumps(data, indent=2))

        operations = []
        for op in data.get("operations", []):
            name = op.get("name")
            if name:
                operations.append({"name": name, "projectId": PROJECT_ID})

        for m in data.get("media", []):
            name = m.get("name")
            if name:
                operations.append({"name": name, "projectId": PROJECT_ID})

        if not operations:
            logger.error("No operations returned from video generation submission!")
            sys.exit(1)

        logger.info("Polling %d operation(s)...", len(operations))
        video_url = None
        vid_media_id = None

        for poll in range(50):
            await asyncio.sleep(8)
            try:
                async with session.post(url_check, json={"operations": operations}, timeout=30) as c_resp:
                    sdata = await c_resp.json()

                for m in sdata.get("media", []):
                    mst = m.get("mediaMetadata", {}).get("mediaStatus", {}).get("mediaGenerationStatus", "")
                    blob_size = m.get("mediaMetadata", {}).get("mediaBlobSize", "")
                    logger.info("Poll %d/50 -> Status: %s (Blob: %s bytes)", poll + 1, mst, blob_size)

                    if mst in ("MEDIA_GENERATION_STATUS_SUCCESSFUL", "SUCCESSFUL"):
                        vid_media_id = m.get("name")
                        raw_v = m.get("video", {})
                        video_url = (
                            raw_v.get("fifeUrl")
                            or raw_v.get("servingUri")
                            or raw_v.get("videoUri")
                            or f"https://flow-content.google/video/{vid_media_id}"
                        )
                        logger.info("Video generation SUCCESSFUL! Media ID: %s, URL: %s", vid_media_id, video_url)
                        break

                    if mst in ("MEDIA_GENERATION_STATUS_FAILED", "FAILED"):
                        logger.error("Video generation FAILED: %s", m)
                        sys.exit(1)

                if video_url:
                    break
            except Exception as e:
                logger.warning("Poll error: %s", e)

        if not video_url:
            logger.error("Video generation polling timed out!")
            sys.exit(1)

        # Download .mp4 video
        dest_video = os.path.join(OUTPUT_DIR, "dung-yen", "idle_0.mp4")
        logger.info("Downloading .mp4 to %s...", dest_video)
        async with session.get(video_url) as v_resp:
            v_data = await v_resp.read()
            with open(dest_video, "wb") as vf:
                vf.write(v_data)

        logger.info("Successfully downloaded 4s video! Size: %d bytes", len(v_data))
        print(f"\n[DONE] Video saved to: {dest_video} ({len(v_data)} bytes)")


if __name__ == "__main__":
    asyncio.run(main())
