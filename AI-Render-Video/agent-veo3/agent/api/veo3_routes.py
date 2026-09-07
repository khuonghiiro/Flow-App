"""Veo3 API Routes - Additional endpoints extending FlowKit core.

Houses endpoints for:
- Skill Tree pipeline automation (/flow/pipeline/*)
- Extension coordination & browser automation
- Request mass cancellation (/requests/cancel-all)
- Enhanced video generation supporting duration, crop, and bg notifications
"""
import asyncio
import logging
from typing import Literal, Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from agent.services.flow_client import get_flow_client
from agent.services.skill_tree_pipeline import (
    create_pipeline,
    get_pipeline,
    list_pipelines,
)

logger = logging.getLogger("veo3_routes")

flow_veo3_router = APIRouter(prefix="/flow", tags=["flow_extensions"])
requests_veo3_router = APIRouter(prefix="/requests", tags=["requests_extensions"])


class EnhancedGenerateVideoRequest(BaseModel):
    start_image_media_id: str
    prompt: str
    project_id: str
    scene_id: str
    aspect_ratio: str = "VIDEO_ASPECT_RATIO_PORTRAIT"
    end_image_media_id: Optional[str] = None
    user_paygate_tier: str = "PAYGATE_TIER_ONE"
    duration: Optional[float] = None
    crop_coordinates: Optional[dict] = None
    model_family: Literal["veo", "omni_flash"] = "veo"
    duration_s: int = 8


class EnhancedGenerateVideoRefsRequest(BaseModel):
    reference_media_ids: list[str]
    prompt: str
    project_id: str
    scene_id: str
    aspect_ratio: str = "VIDEO_ASPECT_RATIO_PORTRAIT"
    user_paygate_tier: str = "PAYGATE_TIER_ONE"
    model_family: Literal["veo", "omni_flash"] = "veo"
    duration_s: int = 8


class StartPipelineRequest(BaseModel):
    project_id: str = ""
    customizer: Optional[dict] = None
    actions: Optional[list[str]] = None


# ─── Enhanced Video Generation Endpoints ──────────────────────────────

@flow_veo3_router.post("/generate-video")
async def generate_video_enhanced(body: EnhancedGenerateVideoRequest):
    """Enhanced direct video generation with duration, crop, and bg poller."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")

    if body.model_family == "omni_flash":
        from agent.services.omni_flash import (
            generate_omni_flash_first_last_video,
            generate_omni_flash_video,
        )
        try:
            if body.end_image_media_id:
                result = await generate_omni_flash_first_last_video(
                    client=client,
                    start_image_media_id=body.start_image_media_id,
                    end_image_media_id=body.end_image_media_id,
                    prompt=body.prompt,
                    project_id=body.project_id,
                    scene_id=body.scene_id,
                    aspect_ratio=body.aspect_ratio,
                    user_paygate_tier=body.user_paygate_tier,
                    duration_s=body.duration_s,
                )
            else:
                result = await generate_omni_flash_video(
                    client=client,
                    start_image_media_id=body.start_image_media_id,
                    prompt=body.prompt,
                    project_id=body.project_id,
                    scene_id=body.scene_id,
                    aspect_ratio=body.aspect_ratio,
                    user_paygate_tier=body.user_paygate_tier,
                    duration_s=body.duration_s,
                )
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from exc
    else:
        dur = body.duration if body.duration is not None else float(body.duration_s)
        result = await client.generate_video(
            start_image_media_id=body.start_image_media_id,
            prompt=body.prompt,
            project_id=body.project_id,
            scene_id=body.scene_id,
            aspect_ratio=body.aspect_ratio,
            end_image_media_id=body.end_image_media_id,
            user_paygate_tier=body.user_paygate_tier,
            duration=dur,
            crop_coordinates=body.crop_coordinates,
        )

    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))

    req_id = result.get("_req_id", "")
    data = result.get("data", result)
    if req_id and body.model_family != "omni_flash":
        asyncio.create_task(bg_poll_and_notify_video(client, data, req_id, body.project_id))
    return data


@flow_veo3_router.post("/generate-video-refs")
async def generate_video_refs_enhanced(body: EnhancedGenerateVideoRefsRequest):
    """Enhanced R2V video generation with background notification."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")

    result = await client.generate_video_from_references(
        reference_media_ids=body.reference_media_ids,
        prompt=body.prompt,
        project_id=body.project_id,
        scene_id=body.scene_id,
        aspect_ratio=body.aspect_ratio,
        user_paygate_tier=body.user_paygate_tier,
    )

    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))

    req_id = result.get("_req_id", "")
    data = result.get("data", result)
    if req_id and body.model_family != "omni_flash":
        asyncio.create_task(bg_poll_and_notify_video(client, data, req_id, body.project_id))
    return data


# ─── Pipeline Endpoints ───────────────────────────────────────────────

@flow_veo3_router.post("/pipeline/start")
async def start_pipeline(body: StartPipelineRequest):
    """Start an end-to-end 3-stage Skill Tree pipeline."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")

    project_id = body.project_id
    if not project_id:
        from agent.db import crud
        projects = await crud.list_projects()
        project_id = projects[0]["id"] if projects else ""

    pipeline = create_pipeline(
        project_id=project_id,
        customizer_values=body.customizer,
        action_keys=body.actions,
    )
    asyncio.create_task(pipeline.run())
    return {
        "pipeline_id": pipeline.id,
        "status": pipeline.status,
        "stage": pipeline.stage,
        "project_id": project_id,
    }


@flow_veo3_router.get("/pipeline/status/{pipeline_id}")
async def get_pipeline_status_endpoint(pipeline_id: str):
    """Get live status, angle states, and 5-slot activity of a pipeline."""
    pipeline = get_pipeline(pipeline_id)
    if not pipeline:
        raise HTTPException(404, f"Pipeline '{pipeline_id}' not found")
    return pipeline.to_dict()


@flow_veo3_router.post("/pipeline/cancel/{pipeline_id}")
async def cancel_pipeline_endpoint(pipeline_id: str):
    """Cancel a running pipeline."""
    pipeline = get_pipeline(pipeline_id)
    if not pipeline:
        raise HTTPException(404, f"Pipeline '{pipeline_id}' not found")
    pipeline.cancel()
    return {"pipeline_id": pipeline.id, "status": "CANCELLED"}


@flow_veo3_router.get("/pipeline/list")
async def list_pipelines_endpoint():
    """List all registered pipelines in memory."""
    return list_pipelines()


# ─── Extension & Browser Bridge Endpoints ─────────────────────────────

@flow_veo3_router.get("/extension-details")
async def extension_details():
    """Get detailed extension status including open tabs."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client._send("get_status", {}, timeout=10)


@flow_veo3_router.post("/navigate-tab")
async def navigate_tab(body: dict):
    """Navigate a tab via extension."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client._send("navigate_tab", body, timeout=15)


@flow_veo3_router.get("/media-redirect-url/{media_id}")
async def get_media_redirect_url(media_id: str):
    """Get signed Cloud CDN download URL for any media via Flow redirect."""
    from agent.services.omni_flash import _fetch_media_url
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await _fetch_media_url(client, media_id)


@flow_veo3_router.post("/reload-extension")
async def reload_extension():
    """Send reload command to connected Chrome extension."""
    client = get_flow_client()
    if hasattr(client, "reload_extension"):
        return await client.reload_extension()
    return await client._send("reload_extension", {}, timeout=10)


@flow_veo3_router.get("/test-captcha")
async def test_captcha_endpoint(action: str = "IMAGE_GENERATION"):
    """Test reCAPTCHA token generation via extension."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client._send("solve_captcha", {"captchaAction": action}, timeout=35)


@flow_veo3_router.get("/media-urls")
async def get_all_cached_media_urls():
    """Return all cached media URLs intercepted from Google Flow."""
    client = get_flow_client()
    return getattr(client, "_recent_media_urls", {})


@flow_veo3_router.post("/fetch-blob")
async def fetch_blob_endpoint(body: dict):
    """Fetch binary blob via extension in browser context."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    url = body.get("url")
    if not url:
        raise HTTPException(400, "Missing url")
    return await client._send("fetch_blob", {"url": url}, timeout=60)


@flow_veo3_router.post("/exec-tab")
async def exec_tab_endpoint(body: dict):
    """Execute JS in a Google Flow tab."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    code = body.get("code")
    if not code:
        raise HTTPException(400, "Missing code")
    return await client._send("exec_tab", {"code": code, "tabId": body.get("tab_id")}, timeout=30)


@flow_veo3_router.get("/captured-video-urls")
async def get_captured_video_urls():
    """Get video URLs captured by extension webRequest."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    res = await client._send("get_captured_video_urls", {}, timeout=10)
    return res.get("result", []) if isinstance(res, dict) else []


# ─── Queue / Request Operations ──────────────────────────────────────

@requests_veo3_router.post("/cancel-all")
async def cancel_all_requests():
    """Cancel all active (PENDING or PROCESSING) requests."""
    from agent.db import crud
    active = await crud.list_requests()
    cancelled = 0
    for r in active:
        if r.get("status") in ("PENDING", "PROCESSING"):
            await crud.update_request(r["id"], status="FAILED", error_message="Cancelled by user")
            cancelled += 1

    from agent.worker.processor import get_worker_controller
    controller = get_worker_controller()
    controller._active_ids.clear()
    controller._deferred.clear()
    controller._retry_after.clear()

    return {"status": "ok", "cancelled": cancelled}


# ─── Background Video Poller Helper ──────────────────────────────────

async def bg_poll_and_notify_video(client, data: dict, req_id: str, project_id: str):
    """Poll video status in background and notify extension UI of completion."""
    from agent.config import VIDEO_POLL_INTERVAL

    poll_items = []
    for op in data.get("operations", []):
        poll_items.append(op)
    for m in data.get("media", []):
        name = m.get("name", "")
        if name:
            poll_items.append({"name": name, "projectId": project_id})

    if not poll_items:
        return

    for _ in range(60):  # max ~10 min
        await asyncio.sleep(VIDEO_POLL_INTERVAL)
        try:
            status_res = await client.check_video_status(poll_items)
            if not status_res or status_res.get("error"):
                continue
            sdata = status_res.get("data", status_res)

            for op in sdata.get("operations", []):
                st = op.get("status", "")
                if st in ("MEDIA_GENERATION_STATUS_SUCCESSFUL", "SUCCESSFUL"):
                    vid = op.get("operation", {}).get("metadata", {}).get("video", {})
                    mid = vid.get("mediaId", "")
                    url = vid.get("fifeUrl", "")
                    if hasattr(client, "notify_request_status"):
                        await client.notify_request_status(req_id=req_id, media_id=mid, status="COMPLETED", output_url=url)
                    return
                elif st in ("MEDIA_GENERATION_STATUS_FAILED", "FAILED"):
                    mid = op.get("operation", {}).get("metadata", {}).get("video", {}).get("mediaId", "")
                    if hasattr(client, "notify_request_status"):
                        await client.notify_request_status(req_id=req_id, media_id=mid, status="FAILED")
                    return
        except Exception as exc:
            logger.debug("Background poll exception: %s", exc)
