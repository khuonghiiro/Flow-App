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
import sys
import uuid
import aiohttp

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from agent.services.prompt_templates import format_template, get_action_templates

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("action_generator")

BASE_OUTPUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "output"))
PLANS_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "plans"))

ACTION_FOLDER_MAP = {
    "idle": ("dung-yen", "Đứng Yên"),
    "walk": ("di-bo", "Đi Bộ"),
    "run": ("chay", "Chạy"),
    "attack": ("danh-cong", "Đánh Công"),
    "defend": ("phong-thu", "Phòng Thủ"),
}


def update_plan_file(character_key: str, action: str, angle: str, media_id: str, file_rel: str, size_bytes: int):
    """Update markdown plan file table with newly generated video."""
    plan_path = os.path.join(PLANS_DIR, f"{character_key}.plan_character_pipeline.md")
    if not os.path.exists(plan_path):
        return

    action_folder, action_label = ACTION_FOLDER_MAP.get(action.lower(), (action.lower(), action))
    size_kb = f"{size_bytes // 1024} KB" if size_bytes else "N/A"
    new_line = f"| **{action_label} (Gốc 4.0s)** | {angle}° | `{media_id}` | [{action}_{angle}.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/{character_key}/{file_rel}) | 720x1280, **4.00s**, {size_kb} | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |\n"

    try:
        with open(plan_path, "r", encoding="utf-8") as f:
            content = f.read()

        lines = content.splitlines(True)
        replaced = False
        for idx, line in enumerate(lines):
            if f"**{action_label}" in line and f"| {angle}° |" in line:
                lines[idx] = new_line
                replaced = True
                break

        if replaced:
            with open(plan_path, "w", encoding="utf-8") as f:
                f.write("".join(lines))
            logger.info("Updated plan file (replaced row): %s", plan_path)
            return

        if "## 5. Registry Hoạt Ảnh Động Tác" in content:
            # Append to table
            parts = content.split("## 5. Registry Hoạt Ảnh Động Tác")
            table_part = parts[1]
            t_lines = table_part.splitlines(True)
            last_row_idx = -1
            for idx, line in enumerate(t_lines):
                if line.strip().startswith("|") and not line.strip().startswith("|---"):
                    last_row_idx = idx
            if last_row_idx >= 0:
                t_lines.insert(last_row_idx + 1, new_line)
                new_table_part = "".join(t_lines)
                content = parts[0] + "## 5. Registry Hoạt Ảnh Động Tác" + new_table_part
                with open(plan_path, "w", encoding="utf-8") as f:
                    f.write(content)
                logger.info("Updated plan file: %s", plan_path)
    except Exception as e:
        logger.warning("Failed to update plan file: %s", e)


async def generate_action(character_key: str, action: str, angle: str, session: aiohttp.ClientSession = None, force: bool = False):
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

    action_folder, action_label = ACTION_FOLDER_MAP.get(action.lower(), (action.lower(), action))
    dest_dir = os.path.join(char_dir, action_folder)
    os.makedirs(dest_dir, exist_ok=True)

    # Check if already completed
    if not force:
        existing = meta.get("actions", {}).get(action_folder, {}).get(angle_key, {})
        if existing.get("status") == "COMPLETED" and os.path.exists(os.path.join(char_dir, existing.get("file", ""))):
            logger.info("Action '%s' %s° already completed for %s, skipping (use --force to overwrite).", action, angle, character_key)
            return True

    customizer = {
        "characterName": meta.get("name", "Diệp Thanh Lam"),
        "gender": meta.get("gender", "female"),
        "age": "young adult (18-20)",
        "personality": "Thanh nhã thoát tục, ôn nhu nội liễm, băng cơ ngọc cốt, tiên khí thuần khiết",
        "chromaBgHex": "#00FF00",
        "skinTone": "Fair warm ivory natural jade skin tone",
        "waistRearMotionLock": "Rear waist delicate silk ribbon sash draping calmly downward without flapping under natural gravity.",
    }
    action_dict = get_action_templates(action.lower(), customizer)
    if not action_dict or angle not in action_dict:
        logger.error("No template found for action '%s' at angle '%s'", action, angle)
        return False

    raw_template = action_dict[angle]
    prompt = format_template(raw_template, customizer)

    logger.info("=" * 70)
    logger.info("[FRAME LOCK CONFIRMED] Action: '%s', Angle: %s° for %s", action, angle, character_key)
    logger.info(" -> START FRAME (mediaId): %s", media_id)
    logger.info(" -> END FRAME   (mediaId): %s", media_id)
    logger.info(" -> PROMPT: %s...", prompt[:200])
    logger.info("=" * 70)

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

    own_session = False
    if session is None:
        session = aiohttp.ClientSession()
        own_session = True

    try:
        data = None
        for submit_retry in range(5):
            async with session.post(url_gen, json=body, timeout=60) as resp:
                data = await resp.json()
                if resp.status == 200 and not data.get("error"):
                    break
                if "Extension not connected" in str(data):
                    logger.warning("Extension reconnecting, waiting 3s (retry %d/5)...", submit_retry + 1)
                    await asyncio.sleep(3)
                    continue
                logger.error("Submission failed: %s", data)
                return False
        else:
            logger.error("Submission failed after retries: %s", data)
            return False

        operations = []
        for op in data.get("operations", []):
            if op.get("name"): operations.append({"name": op["name"], "projectId": project_id})
        for m in data.get("media", []):
            if m.get("name"): operations.append({"name": m["name"], "projectId": project_id})

        if not operations:
            logger.error("No operations returned!")
            return False

        logger.info("Polling operation for %s %s°...", action, angle)
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
            logger.error("Polling timed out for %s %s°!", action, angle)
            return False

        logger.info("Generation SUCCESSFUL! Media ID: %s. Fetching signed CDN URL...", vid_media_id)
        url_redirect = f"http://127.0.0.1:8100/api/flow/media-redirect-url/{vid_media_id}"
        signed_url = None
        for _ in range(10):
            try:
                async with session.get(url_redirect, timeout=15) as r:
                    res_data = await r.json()
                    if res_data.get("status") == 200:
                        u = res_data.get("data", {}).get("url", "")
                        if u and "flow-content.google" in u:
                            signed_url = u
                            break
            except Exception as e:
                logger.debug("Redirect poll error: %s", e)
            await asyncio.sleep(2)

        # Fallback to captured-video-urls
        if not signed_url:
            async with session.get("http://127.0.0.1:8100/api/flow/captured-video-urls") as r:
                urls = await r.json()
            for item in reversed(urls):
                u = item.get("url", "")
                if vid_media_id in u and ("/video/" in u or "flow-content.google" in u):
                    signed_url = u
                    break

        if not signed_url:
            logger.error("Signed video URL could not be obtained for %s!", vid_media_id)
            return False

        logger.info("Downloading MP4 for %s %s° from CDN...", action, angle)
        async with session.get(signed_url, timeout=90) as r:
            raw_bytes = await r.read()

        if len(raw_bytes) < 1000:
            logger.error("Downloaded file too small (%d bytes)!", len(raw_bytes))
            return False

        dest_vid = os.path.join(dest_dir, f"{action}_{angle}.mp4")
        with open(dest_vid, "wb") as f:
            f.write(raw_bytes)

        # Update metadata
        actions_dict = meta.setdefault("actions", {}).setdefault(action_folder, {})
        actions_dict[angle_key] = {
            "media_id": vid_media_id,
            "file": f"{action_folder}/{action}_{angle}.mp4",
            "duration": 4.0,
            "resolution": "720x1280",
            "size_bytes": os.path.getsize(dest_vid),
            "status": "COMPLETED",
        }
        with open(meta_path, "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=2, ensure_ascii=False)

        # Update markdown plan
        update_plan_file(character_key, action, angle, vid_media_id, f"{action_folder}/{action}_{angle}.mp4", os.path.getsize(dest_vid))

        logger.info("[SUCCESS] Generated %s %s° loop: %s (Size: %d bytes)", action, angle, dest_vid, os.path.getsize(dest_vid))
        return True
    finally:
        if own_session:
            await session.close()



async def run_batch(character_key: str, tasks: list[tuple[str, str]], concurrency: int = 6, force: bool = False):
    sem = asyncio.Semaphore(concurrency)
    async with aiohttp.ClientSession() as session:
        async def _worker(act, ang, idx: int):
            # Stagger startup slightly so parallel requests do not clash on websocket
            await asyncio.sleep(min(idx * 0.8, 4.0))
            async with sem:
                logger.info("=== Starting [%s %s°] ===", act, ang)
                success = await generate_action(character_key, act, ang, session=session, force=force)
                logger.info("=== Finished [%s %s°]: %s ===", act, ang, "OK" if success else "FAILED")
                return success

        results = await asyncio.gather(*[_worker(act, ang, i) for i, (act, ang) in enumerate(tasks)])
        logger.info("Batch summary: %d/%d successful", sum(results), len(results))


def main():
    parser = argparse.ArgumentParser(description="Generate seamless action loops for characters.")
    parser.add_argument("--character", required=True, help="Character key (e.g. diep-thanh-lam)")
    parser.add_argument("--action", default=None, help="Action name: idle, walk, run, attack, defend")
    parser.add_argument("--angle", default=None, help="Angle: 0, 45, 90, 135, 180 (or comma-separated e.g. 0,45,90)")
    parser.add_argument("--all", action="store_true", help="Generate all actions for all angles")
    parser.add_argument("--force", action="store_true", help="Force re-generation and overwrite existing videos")
    parser.add_argument("--concurrency", type=int, default=6, help="Number of concurrent generations (default: 6)")
    args = parser.parse_args()

    all_actions = ["idle", "walk", "run", "attack", "defend"]
    all_angles = ["0", "45", "90", "135", "180"]

    if args.all:
        tasks = []
        for act in all_actions:
            for ang in all_angles:
                tasks.append((act, ang))
        logger.info("Queued ALL %d action-angle combinations for %s (concurrency: %d, force: %s)", len(tasks), args.character, args.concurrency, args.force)
        asyncio.run(run_batch(args.character, tasks, concurrency=args.concurrency, force=args.force))
    elif args.action:
        acts = [a.strip() for a in args.action.split(",")]
        angs = [a.strip() for a in args.angle.split(",")] if args.angle else all_angles
        tasks = [(act, ang) for act in acts for ang in angs]
        if len(tasks) == 1:
            act, ang = tasks[0]
            asyncio.run(generate_action(args.character, act, ang, force=args.force))
        else:
            logger.info("Queued %d tasks (actions=%s, angles=%s, concurrency: %d, force: %s)", len(tasks), acts, angs, args.concurrency, args.force)
            asyncio.run(run_batch(args.character, tasks, concurrency=args.concurrency, force=args.force))
    else:
        parser.print_help()


if __name__ == "__main__":
    main()

