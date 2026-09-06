"""Universal Action Loop Generator for Characters.
Usage:
    python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --action idle --angle 0
"""

import argparse
import asyncio
import base64
import json
import logging
import os
import subprocess
import sys
import uuid
import aiohttp
import imageio_ffmpeg

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from agent.services.prompt_templates import ACTION_TEMPLATES, format_template

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("action_generator")

BASE_OUTPUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "output"))


async def generate_action(character_key: str, action: str, angle: str):
    char_dir = os.path.join(BASE_OUTPUT_DIR, character_key)
    meta_path = os.path.join(char_dir, "character_meta.json")

    if not os.path.exists(meta_path):
        logger.error("character_meta.json not found in %s", char_dir)
        return False

    with open(meta_path, "r", encoding="utf-8") as f:
        meta = json.load(f)

    project_id = meta.get("project_id")
    angle_key = f"angle_{angle}"
    angle_data = meta.get(angle_key, {})
    media_id = angle_data.get("media_id")

    if not project_id or not media_id:
        logger.error("Missing project_id (%s) or media_id (%s) for %s", project_id, media_id, angle_key)
        return False

    # Get prompt from standard templates
    action_dict = ACTION_TEMPLATES.get(action.lower())
    if not action_dict or angle not in action_dict:
        logger.error("No template found for action '%s' at angle '%s'", action, angle)
        return False

    raw_template = action_dict[angle]
    customizer = {
        "gender": meta.get("gender", "female"),
        "chromaBgHex": "#00FF00",
    }
    prompt = format_template(raw_template, customizer)

    action_folder_map = {
        "idle": "dung-yen",
        "walk": "di-bo",
        "run": "chay",
        "attack": "danh-cong",
        "defend": "phong-thu",
    }
    action_folder = action_folder_map.get(action.lower(), action.lower())
    dest_dir = os.path.join(char_dir, action_folder)
    os.makedirs(dest_dir, exist_ok=True)

    logger.info("Generating '%s' angle %s° for %s (Project: %s, Media: %s)", action, angle, character_key, project_id, media_id)
    url_gen = "http://127.0.0.1:8100/api/flow/generate-video"
    url_check = "http://127.0.0.1:8100/api/flow/check-status"

    body = {
        "start_image_media_id": media_id,
        "end_image_media_id": media_id,
        "prompt": prompt,
        "project_id": project_id,
        "scene_id": f"scene_{action}_{angle}_{uuid.uuid4().hex[:6]}",
        "aspect_ratio": "VIDEO_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
        "duration": 4.0,
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(url_gen, json=body, timeout=60) as resp:
            data = await resp.json()
            if resp.status >= 400 or data.get("error"):
                logger.error("Submission failed: %s", data)
                return False

        operations = []
        for op in data.get("operations", []):
            if op.get("name"): operations.append({"name": op["name"], "projectId": project_id})
        for m in data.get("media", []):
            if m.get("name"): operations.append({"name": m["name"], "projectId": project_id})

        if not operations:
            logger.error("No operations returned!")
            return False

        logger.info("Polling operation for %s...", character_key)
        vid_media_id = None
        for poll in range(60):
            await asyncio.sleep(8)
            async with session.post(url_check, json={"operations": operations}, timeout=30) as c_resp:
                sdata = await c_resp.json()
            for m in sdata.get("media", []):
                mst = m.get("mediaMetadata", {}).get("mediaStatus", {}).get("mediaGenerationStatus", "")
                if mst in ("MEDIA_GENERATION_STATUS_SUCCESSFUL", "SUCCESSFUL"):
                    vid_media_id = m.get("name")
                    break
                if mst in ("MEDIA_GENERATION_STATUS_FAILED", "FAILED"):
                    logger.error("Generation failed: %s", m)
                    return False
            if vid_media_id:
                break

        if not vid_media_id:
            logger.error("Polling timed out!")
            return False

        # Get signed video URL
        async with session.get("http://127.0.0.1:8100/api/flow/captured-video-urls") as r:
            urls = await r.json()

        signed_url = None
        for item in reversed(urls):
            u = item.get("url", "")
            if vid_media_id in u and "/video/" in u:
                signed_url = u
                break

        if not signed_url:
            for item in reversed(urls):
                u = item.get("url", "")
                if "/video/" in u:
                    signed_url = u
                    break

        if not signed_url:
            logger.error("Signed video URL could not be captured!")
            return False

        async with session.post("http://127.0.0.1:8100/api/flow/fetch-blob", json={"url": signed_url}) as r:
            blob_data = await r.json()

        raw_bytes = base64.b64decode(blob_data.get("data", ""))
        dest_4s = os.path.join(dest_dir, f"{action}_{angle}_4s.mp4")
        with open(dest_4s, "wb") as f:
            f.write(raw_bytes)

        ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()

        # 2.0s loop
        dest_2s = os.path.join(dest_dir, f"{action}_{angle}.mp4")
        subprocess.run([
            ffmpeg_exe, "-y", "-i", dest_4s,
            "-filter:v", "setpts=0.5*PTS", "-an",
            "-c:v", "libx264", "-crf", "18", "-preset", "slow", "-pix_fmt", "yuv420p",
            dest_2s
        ], check=True)

        # 1.6s loop
        dest_1_6s = os.path.join(dest_dir, f"{action}_{angle}_1.6s.mp4")
        subprocess.run([
            ffmpeg_exe, "-y", "-i", dest_4s,
            "-filter:v", "setpts=0.4*PTS", "-an",
            "-c:v", "libx264", "-crf", "18", "-preset", "slow", "-pix_fmt", "yuv420p",
            dest_1_6s
        ], check=True)

        # Update metadata
        actions_dict = meta.setdefault("actions", {}).setdefault(action_folder, {})
        actions_dict[angle_key] = {
            "media_id": vid_media_id,
            "file": f"{action_folder}/{action}_{angle}.mp4",
            "file_1_6s": f"{action_folder}/{action}_{angle}_1.6s.mp4",
            "file_4s_raw": f"{action_folder}/{action}_{angle}_4s.mp4",
            "duration": 2.0,
            "duration_short": 1.6,
            "resolution": "720x1280",
            "size_bytes": os.path.getsize(dest_2s),
            "status": "COMPLETED",
        }
        with open(meta_path, "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=2, ensure_ascii=False)

        logger.info("[SUCCESS] Generated %s %s° loop: %s", action, angle, dest_2s)
        return True


def main():
    parser = argparse.ArgumentParser(description="Generate seamless action loops for characters.")
    parser.add_argument("--character", required=True, help="Character key (e.g. diep-thanh-lam)")
    parser.add_argument("--action", default="idle", help="Action name: idle, walk, run, attack, defend")
    parser.add_argument("--angle", default="0", choices=["0", "45", "90", "135", "180"], help="Angle: 0, 45, 90, 135, 180")
    args = parser.parse_args()
    asyncio.run(generate_action(args.character, args.action, args.angle))


if __name__ == "__main__":
    main()
