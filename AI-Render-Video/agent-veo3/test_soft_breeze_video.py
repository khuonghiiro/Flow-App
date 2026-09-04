import asyncio
import aiohttp
import json
import os
import sys
import uuid

sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

PROJECT_ID = "7de76f25-e719-452f-8a03-fb6eece897e4"
OUTPUT_DIR = r"e:\UngDung_PC\Flow-App\AI-Render-Video\agent-veo3\output\da-vo-nhai\videos\dung-yen"
os.makedirs(OUTPUT_DIR, exist_ok=True)

mid_0 = "62a88991-c3d8-4fb5-8cf0-23da064aba43"

from agent.services.prompt_templates import IDLE_PROMPT_TEMPLATES

PROMPT_IDLE_0_SOFT = IDLE_PROMPT_TEMPLATES["0"].format(chromaBgHex="#00FF00")

async def main():
    print("Prompt being tested:\n", PROMPT_IDLE_0_SOFT, "\n", flush=True)
    print("Submitting 4s loop video with ULTRA-SOFT ANIME BREEZE...", flush=True)
    url_gen = "http://127.0.0.1:8100/api/flow/generate-video"
    url_check = "http://127.0.0.1:8100/api/flow/check-status"

    body = {
        "start_image_media_id": mid_0,
        "end_image_media_id": mid_0,
        "prompt": PROMPT_IDLE_0_SOFT,
        "project_id": PROJECT_ID,
        "scene_id": f"scene_{uuid.uuid4().hex[:8]}",
        "aspect_ratio": "VIDEO_ASPECT_RATIO_PORTRAIT",
        "user_paygate_tier": "PAYGATE_TIER_TWO",
        "duration": 4.0
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(url_gen, json=body, timeout=60) as resp:
            data = await resp.json()
            print("Submission response:", json.dumps(data, indent=2, ensure_ascii=False), flush=True)

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
            print("No operations or media returned!", flush=True)
            return

        print(f"Tracking operations: {operations}", flush=True)

        max_polls = 40
        for i in range(max_polls):
            await asyncio.sleep(8)
            try:
                async with session.post(url_check, json={"operations": operations}, timeout=30) as check_resp:
                    sdata = await check_resp.json()

                for m in sdata.get("media", []):
                    mst = m.get("mediaMetadata", {}).get("mediaStatus", {}).get("mediaGenerationStatus", "")
                    blob_size = m.get("mediaMetadata", {}).get("mediaBlobSize", "")
                    print(f"Poll {i+1}/{max_polls} -> Media Status: {mst} (Blob: {blob_size} bytes)", flush=True)
                    if mst in ("MEDIA_GENERATION_STATUS_SUCCESSFUL", "SUCCESSFUL"):
                        print(f"\n SUCCESS! 4s Soft Breeze Loop Video generated successfully!")
                        print(f"Media Name: {m.get('name')}")
                        print(f"Project ID: {PROJECT_ID}")
                        print(f"Flow Web URL: https://labs.google/fx/tools/flow/project/{PROJECT_ID}")
                        return

                    if mst in ("MEDIA_GENERATION_STATUS_FAILED", "FAILED"):
                        print(f"FAILED: {m}", flush=True)
                        return

            except Exception as e:
                print(f"Poll error: {e}", flush=True)

if __name__ == "__main__":
    asyncio.run(main())
