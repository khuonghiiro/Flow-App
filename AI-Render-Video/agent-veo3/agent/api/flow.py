"""Direct Flow API endpoints — for manual operations outside the queue."""
import asyncio
import logging
from typing import Literal, Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from agent.services.flow_client import get_flow_client
from agent.services.omni_flash import (
    check_omni_flash_status,
    generate_omni_flash_first_frame_video,
    generate_omni_flash_first_last_video,
    generate_omni_flash_video,
)
from agent.services.skill_tree_pipeline import (
    create_pipeline,
    get_pipeline,
    list_pipelines,
)

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/flow", tags=["flow"])


class GenerateImageRequest(BaseModel):
    prompt: str
    project_id: str
    aspect_ratio: str = "IMAGE_ASPECT_RATIO_PORTRAIT"
    user_paygate_tier: str = "PAYGATE_TIER_ONE"
    character_media_ids: Optional[list[str]] = None


class GenerateVideoRequest(BaseModel):
    start_image_media_id: str
    prompt: str
    project_id: str
    scene_id: str
    aspect_ratio: str = "VIDEO_ASPECT_RATIO_PORTRAIT"
    end_image_media_id: Optional[str] = None
    user_paygate_tier: str = "PAYGATE_TIER_ONE"
    duration: Optional[float] = None
    crop_coordinates: Optional[dict] = None
    # Backward compatible: legacy requests remain Veo unless explicitly set.
    model_family: Literal["veo", "omni_flash"] = "veo"
    duration_s: int = 8


class GenerateVideoRefsRequest(BaseModel):
    reference_media_ids: list[str]
    prompt: str
    project_id: str
    scene_id: str
    aspect_ratio: str = "VIDEO_ASPECT_RATIO_PORTRAIT"
    user_paygate_tier: str = "PAYGATE_TIER_ONE"
    # Backward compatible: existing callers keep the Veo R2V path unless they
    # explicitly opt into Omni Flash.
    model_family: Literal["veo", "omni_flash"] = "veo"
    duration_s: int = 8


class GenerateOmniFlashVideoRequest(BaseModel):
    reference_media_ids: list[str]
    prompt: str
    project_id: str
    scene_id: str = ""
    duration_s: int = 8
    aspect_ratio: str = "VIDEO_ASPECT_RATIO_PORTRAIT"
    user_paygate_tier: str = "PAYGATE_TIER_ONE"


class UpscaleVideoRequest(BaseModel):
    media_id: str
    scene_id: str
    aspect_ratio: str = "VIDEO_ASPECT_RATIO_PORTRAIT"
    resolution: str = "VIDEO_RESOLUTION_4K"


class UploadImageRequest(BaseModel):
    file_path: str  # absolute path to local image file
    project_id: str = ""
    file_name: str = "image.png"


class CheckStatusRequest(BaseModel):
    operations: list[dict] = []
    # Omni/workflow-mode callers should pass workflow descriptors instead of
    # operation handles. If workflows is set, /check-status automatically uses
    # authenticated Flow project polling.
    workflows: Optional[list[dict]] = None
    project_id: str = ""
    include_encoded_video: bool = False


class CheckOmniStatusRequest(BaseModel):
    workflows: list[dict]
    project_id: str = ""
    include_encoded_video: bool = False


class EditImageRequest(BaseModel):
    prompt: str
    source_media_id: str
    project_id: str
    aspect_ratio: str = "IMAGE_ASPECT_RATIO_PORTRAIT"
    user_paygate_tier: str = "PAYGATE_TIER_ONE"


class StartPipelineRequest(BaseModel):
    project_id: str = ""
    customizer: Optional[dict] = None
    actions: Optional[list[str]] = None


@router.get("/status")
async def extension_status():
    """Check if extension is connected."""
    client = get_flow_client()
    return {
        "connected": client.connected,
        "flow_key_present": client._flow_key is not None,
    }


@router.get("/extension-details")
async def extension_details():
    """Get detailed extension status including open tabs."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client._send("get_status", {}, timeout=10)


@router.post("/navigate-tab")
async def navigate_tab(body: dict):
    """Navigate a tab via extension."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client._send("navigate_tab", body, timeout=15)


@router.get("/credits")
async def get_credits():
    """Get user credits from Google Flow."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    result = await client.get_credits()
    if result.get("error"):
        raise HTTPException(502, result["error"])
    return result.get("data", result)


@router.post("/generate-image")
async def generate_image(body: GenerateImageRequest):
    """Generate image directly (bypasses queue)."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    result = await client.generate_images(**body.model_dump())
    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))
    return result.get("data", result)


@router.post("/generate-video")
async def generate_video(body: GenerateVideoRequest):
    """Submit frame-conditioned video generation using Veo or Omni Flash.

    Existing callers default to Veo. For Omni set ``model_family=omni_flash``
    and ``duration_s`` to 4/6/8/10. With only ``start_image_media_id`` the
    request uses Omni First frame. When ``end_image_media_id`` is also present,
    it uses Omni First+Last frames.

    Omni responses include ``flowkitPolling.workflows`` and must use workflow
    media polling rather than legacy operation polling.
    """
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")

    if body.model_family == "omni_flash":
        try:
            common = dict(
                start_image_media_id=body.start_image_media_id,
                prompt=body.prompt,
                project_id=body.project_id,
                scene_id=body.scene_id,
                duration_s=body.duration_s,
                aspect_ratio=body.aspect_ratio,
                user_paygate_tier=body.user_paygate_tier,
            )
            if body.end_image_media_id:
                result = await generate_omni_flash_first_last_video(
                    end_image_media_id=body.end_image_media_id,
                    **common,
                )
            else:
                result = await generate_omni_flash_first_frame_video(**common)
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
        asyncio.create_task(_bg_poll_and_notify_video(client, data, req_id, body.project_id))
    return data


@router.post("/generate-video-refs")
async def generate_video_refs(body: GenerateVideoRefsRequest):
    """Submit reference-to-video generation using Veo or Gemini Omni Flash.

    Existing requests default to ``model_family=veo``. Set
    ``model_family=omni_flash`` and ``duration_s`` to 4/6/8/10 to use Omni.
    Omni responses include ``flowkitPolling.workflows``; poll those workflows,
    not the operation-looking handles in the raw Flow response.
    """
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")

    if body.model_family == "omni_flash":
        try:
            result = await generate_omni_flash_video(
                reference_media_ids=body.reference_media_ids,
                prompt=body.prompt,
                project_id=body.project_id,
                scene_id=body.scene_id,
                duration_s=body.duration_s,
                aspect_ratio=body.aspect_ratio,
                user_paygate_tier=body.user_paygate_tier,
            )
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from exc
    else:
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
        asyncio.create_task(_bg_poll_and_notify_video(client, data, req_id, body.project_id))
    return data


@router.post("/generate-video-omni")
async def generate_video_omni(body: GenerateOmniFlashVideoRequest):
    """Submit Gemini Omni Flash reference-to-video generation.

    The response includes ``flowkitPolling.workflows`` for the correct
    workflow/media polling path.
    """
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    try:
        result = await generate_omni_flash_video(**body.model_dump())
    except ValueError as exc:
        raise HTTPException(400, str(exc)) from exc
    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))
    return result.get("data", result)


@router.post("/upscale-video")
async def upscale_video(body: UpscaleVideoRequest):
    """Submit video upscale (returns operations for polling)."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    result = await client.upscale_video(**body.model_dump())
    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))
    return result.get("data", result)


@router.post("/check-status")
async def check_status(body: CheckStatusRequest):
    """Check Veo operation status or Omni workflow/media status.

    Veo: pass ``operations``.
    Omni Flash: pass ``workflows`` from submit ``flowkitPolling.workflows``.
    """
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")

    if body.workflows:
        try:
            return await check_omni_flash_status(
                body.workflows,
                include_encoded_video=body.include_encoded_video,
                project_id=body.project_id,
            )
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from exc
        except RuntimeError as exc:
            raise HTTPException(502, str(exc)) from exc

    if not body.operations:
        raise HTTPException(400, "Provide operations for Veo or workflows for Omni Flash")

    result = await client.check_video_status(body.operations)
    if result.get("error"):
        raise HTTPException(502, result["error"])
    if isinstance(result.get("status"), int) and result["status"] >= 400:
        raise HTTPException(result["status"], result.get("data", "Flow polling failed"))
    return result.get("data", result)


@router.post("/check-omni-status")
async def check_omni_status(body: CheckOmniStatusRequest):
    """Poll Gemini Omni Flash jobs via workflow primary media IDs."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    try:
        return await check_omni_flash_status(
            body.workflows,
            include_encoded_video=body.include_encoded_video,
            project_id=body.project_id,
        )
    except ValueError as exc:
        raise HTTPException(400, str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(502, str(exc)) from exc


@router.post("/refresh-urls/{project_id}")
async def refresh_project_urls(project_id: str):
    """Bulk refresh all media URLs for a project via per-media get_media calls."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    result = await client.refresh_project_urls(project_id)
    if result.get("error"):
        raise HTTPException(502, result["error"])
    return result


@router.get("/media/{media_id}")
async def get_media(media_id: str, project_id: str = "", tool: str = "PINHOLE"):
    """Get media metadata + fresh signed URL from Google Flow.

    Returns the raw response which contains fresh signed URLs and metadata.
    """
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    result = await client.get_media(media_id, project_id=project_id, tool=tool)
    if result.get("error"):
        raise HTTPException(502, result["error"])
    status = result.get("status", 200)
    if isinstance(status, int) and status >= 400:
        raise HTTPException(status, result.get("data", "Media not found"))
    return result.get("data", result)


@router.get("/media-redirect-url/{media_id}")
async def get_media_redirect_url(media_id: str):
    """Get signed Cloud CDN download URL for any media (image or video) via Flow's authenticated redirect."""
    from agent.services.omni_flash import _fetch_media_url
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    res = await _fetch_media_url(client, media_id)
    return res


@router.post("/edit-image")
async def edit_image(body: EditImageRequest):
    """Edit an existing image using IMAGE_INPUT_TYPE_BASE_IMAGE (bypasses queue)."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    result = await client.edit_image(
        body.prompt, body.source_media_id, body.project_id,
        aspect_ratio=body.aspect_ratio,
        user_paygate_tier=body.user_paygate_tier,
    )
    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))
    return result.get("data", result)


@router.post("/upload-image")
async def upload_image(body: UploadImageRequest):
    """Upload a local image file to Google Flow and get a media_id."""
    import base64, mimetypes
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    try:
        with open(body.file_path, "rb") as f:
            image_bytes = f.read()
    except FileNotFoundError:
        raise HTTPException(404, f"File not found: {body.file_path}")
    b64 = base64.b64encode(image_bytes).decode()
    mime = mimetypes.guess_type(body.file_path)[0] or "image/png"
    result = await client.upload_image(b64, mime_type=mime, project_id=body.project_id, file_name=body.file_name)
    if result.get("error") or (isinstance(result.get("status"), int) and result["status"] >= 400):
        raise HTTPException(result.get("status", 502), result.get("error", result.get("data")))
    media_id = result.get("_mediaId")
    return {"media_id": media_id, "raw": result.get("data", result)}


# ─── Skill Tree Pipeline Endpoints ───────────────────────────

@router.post("/pipeline/start")
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


@router.get("/pipeline/status/{pipeline_id}")
async def get_pipeline_status_endpoint(pipeline_id: str):
    """Get live status, angle states, and 5-slot activity of a pipeline."""
    pipeline = get_pipeline(pipeline_id)
    if not pipeline:
        raise HTTPException(404, f"Pipeline '{pipeline_id}' not found")
    return pipeline.to_dict()


@router.post("/pipeline/cancel/{pipeline_id}")
async def cancel_pipeline_endpoint(pipeline_id: str):
    """Cancel a running pipeline."""
    pipeline = get_pipeline(pipeline_id)
    if not pipeline:
        raise HTTPException(404, f"Pipeline '{pipeline_id}' not found")
    pipeline.cancel()
    return {"pipeline_id": pipeline.id, "status": "CANCELLED"}


@router.get("/pipeline/list")
async def list_pipelines_endpoint():
    """List all registered pipelines in memory."""
    return list_pipelines()


# ─── Background Video Status Notifier ────────────────────────

async def _bg_poll_and_notify_video(client, data: dict, req_id: str, project_id: str):
    """Background: poll video status and notify extension on completion.

    When video gen is called via direct API, the extension logs the request
    as 'queued'. This poller watches until SUCCESSFUL/FAILED and sends
    an update_request_log message so the extension UI shows 'done'/'failed'.
    """
    from agent.config import VIDEO_POLL_INTERVAL

    poll_items = []
    media_ids = []
    for op in data.get("operations", []):
        poll_items.append(op)
        mid = op.get("operation", {}).get("metadata", {}).get("video", {}).get("mediaId", "")
        if mid:
            media_ids.append(mid)

    for wf in data.get("workflows", []):
        primary = wf.get("metadata", {}).get("primaryMediaId", "")
        if primary:
            media_ids.append(primary)

    for m in data.get("media", []):
        name = m.get("name", "")
        if name:
            poll_items.append({"name": name, "projectId": project_id})
            if name not in media_ids:
                media_ids.append(name)

    if not poll_items:
        return

    max_polls = 60  # ~10 min
    for _ in range(max_polls):
        await asyncio.sleep(VIDEO_POLL_INTERVAL)
        try:
            status_res = await client.check_video_status(poll_items)
            if status_res.get("error"):
                continue
            sdata = status_res.get("data", status_res)

            for op in sdata.get("operations", []):
                st = op.get("status", "")
                if st in ("MEDIA_GENERATION_STATUS_SUCCESSFUL", "SUCCESSFUL"):
                    vid = op.get("operation", {}).get("metadata", {}).get("video", {})
                    mid = vid.get("mediaId", "")
                    url = vid.get("fifeUrl", "")
                    await client.notify_request_status(
                        req_id=req_id, media_id=mid,
                        status="COMPLETED", output_url=url,
                    )
                    logger.info("BG notify: video %s COMPLETED (req=%s)", mid[:8], req_id[:8])
                    return
                elif st in ("MEDIA_GENERATION_STATUS_FAILED", "FAILED"):
                    mid = op.get("operation", {}).get("metadata", {}).get("video", {}).get("mediaId", "")
                    await client.notify_request_status(
                        req_id=req_id, media_id=mid, status="FAILED",
                    )
                    logger.info("BG notify: video FAILED (req=%s)", req_id[:8])
                    return

            for m in sdata.get("media", []):
                gen_status = (
                    m.get("mediaMetadata", {})
                     .get("mediaStatus", {})
                     .get("mediaGenerationStatus", "")
                )
                if gen_status in ("MEDIA_GENERATION_STATUS_SUCCESSFUL", "SUCCESSFUL"):
                    mid = m.get("name", "")
                    url = m.get("video", {}).get("fifeUrl", "")
                    await client.notify_request_status(
                        req_id=req_id, media_id=mid,
                        status="COMPLETED", output_url=url,
                    )
                    logger.info("BG notify: video %s COMPLETED (req=%s)", mid[:8], req_id[:8])
                    return
                elif gen_status in ("MEDIA_GENERATION_STATUS_FAILED", "FAILED"):
                    mid = m.get("name", "")
                    await client.notify_request_status(
                        req_id=req_id, media_id=mid, status="FAILED",
                    )
                    logger.info("BG notify: video FAILED (req=%s)", req_id[:8])
                    return
        except Exception as e:
            logger.debug("BG poll error: %s", e)

    logger.warning("BG video poll timed out for req=%s", req_id[:8])


@router.post("/reload-extension")
async def reload_extension():
    """Send reload command to the connected Chrome extension."""
    client = get_flow_client()
    return await client.reload_extension()


@router.get("/test-captcha")
async def test_captcha_endpoint(action: str = "IMAGE_GENERATION"):
    """Test reCAPTCHA token generation via extension."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client._send("solve_captcha", {"captchaAction": action}, timeout=35)


@router.get("/media-urls")
async def get_all_cached_media_urls():
    client = get_flow_client()
    return getattr(client, "_recent_media_urls", {})


@router.post("/fetch-blob")
async def fetch_blob_endpoint(body: dict):
    """Fetch binary blob via extension in browser context."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    url = body.get("url")
    if not url:
        raise HTTPException(400, "Missing url")
    return await client.fetch_blob(url)


@router.post("/exec-tab")
async def exec_tab_endpoint(body: dict):
    """Execute JS in a Google Flow tab."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    code = body.get("code")
    if not code:
        raise HTTPException(400, "Missing code")
    return await client.exec_tab(code, body.get("tab_id"))


@router.get("/captured-video-urls")
async def get_captured_video_urls():
    """Get video URLs captured by extension webRequest."""
    client = get_flow_client()
    if not client.connected:
        raise HTTPException(503, "Extension not connected")
    return await client.get_captured_video_urls()
