import os
import sys
import shutil
import tempfile
from pathlib import Path
from typing import Optional, List, Dict, Any

# Tắt cảnh báo Symlink & Tokenizer trên Windows
os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"

import warnings
warnings.filterwarnings("ignore", category=UserWarning)
warnings.filterwarnings("ignore", category=FutureWarning)

# Cấu hình UTF-8 cho console Windows
try:
    if sys.stdout and hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if sys.stderr and hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

from fastapi import FastAPI, UploadFile, File, Form, HTTPException, BackgroundTasks, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse, JSONResponse
from pydantic import BaseModel

from .hardware_detector import get_system_hardware_info
from .model_manager import model_manager
from .engines.faster_whisper_engine import FasterWhisperEngine
from .engines.whisperx_engine import WhisperXEngine
from .engines.sensevoice_engine import SenseVoiceEngine
from .engines.seamless_m4t_engine import SeamlessM4TEngine
from .engines.speaker_diarizer import get_global_diarizer

app = FastAPI(
    title="FlowMy AI Speech-to-Subtitle Server",
    description="REST API & Web Studio for Speech Recognition, Subtitle Timestamps & Translation (4 AI Engines)",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Cache active engine instances
import time

active_engines = {}
current_active_model = None

def get_engine_instance(engine_name: str, model_size: str, device: str, compute_type: str):
    global current_active_model
    key = f"{engine_name}_{model_size}_{device}_{compute_type}"
    if key not in active_engines:
        # 1. Hủy và giải phóng toàn bộ model cũ trong VRAM để tránh tràn bộ nhớ GPU
        for old_key, old_engine in list(active_engines.items()):
            if hasattr(old_engine, "unload_model"):
                try:
                    old_engine.unload_model()
                except Exception as e:
                    print(f"[VRAM Manager] Lỗi khi unload model cũ {old_key}: {e}")
        active_engines.clear()

        # 2. Dọn rác bộ nhớ Python và CUDA VRAM
        import gc
        gc.collect()
        import torch
        if torch.cuda.is_available():
            torch.cuda.empty_cache()

        download_root = model_manager.get_models_dir_path()
        if engine_name == "whisperx":
            engine = WhisperXEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        elif engine_name == "sensevoice":
            engine = SenseVoiceEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        elif engine_name == "seamless-m4t":
            engine = SeamlessM4TEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        else: # default faster-whisper
            engine = FasterWhisperEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)

        # Tự động nạp model vào RAM/VRAM ngay lập tức nếu chưa nạp
        try:
            if not getattr(engine, "is_loaded", False):
                print(f"[Auto-Loader] Tu dong nap model '{model_size}' cho engine '{engine_name}' tren {device} ({compute_type})...")
                engine.load_model()
        except Exception as e:
            print(f"[Auto-Loader] Canh bao khi tu dong nap {engine_name} ({model_size}): {e}")

        active_engines[key] = engine
        current_active_model = {
            "engine": engine_name,
            "model_size": model_size,
            "device": device,
            "compute_type": compute_type,
            "loaded_at": time.time()
        }

    return active_engines[key]

# Language Name Map for Auto-Detection
LANGUAGE_NAMES = {
    "vi": "Tiếng Việt",
    "en": "Tiếng Anh",
    "zh": "Tiếng Trung",
    "ja": "Tiếng Nhật",
    "ko": "Tiếng Hàn",
    "fr": "Tiếng Pháp",
    "de": "Tiếng Đức",
    "es": "Tiếng Tây Ban Nha",
    "ru": "Tiếng Nga",
    "th": "Tiếng Thái",
    "id": "Tiếng Indonesia",
    "pt": "Tiếng Bồ Đào Nha",
    "it": "Tiếng Ý",
    "ar": "Tiếng Ả Rập",
    "hi": "Tiếng Hindi",
    "tr": "Tiếng Thổ Nhĩ Kỳ",
    "nl": "Tiếng Hà Lan",
    "pl": "Tiếng Ba Lan",
    "uk": "Tiếng Ukraina",
    "ms": "Tiếng Mã Lai"
}

# --- Pydantic Request Models ---
class LoadModelRequest(BaseModel):
    engine: Optional[str] = "faster-whisper"
    model_size: Optional[str] = "small"
    device: Optional[str] = "auto"
    compute_type: Optional[str] = "default"

class SetModelDirRequest(BaseModel):
    directory: str

class DeleteModelRequest(BaseModel):
    model_path: Optional[str] = None
    path: Optional[str] = None

class TranslateSegmentsRequest(BaseModel):
    segments: List[Dict[str, Any]]
    src_lang: Optional[str] = "auto"
    target_lang: Optional[str] = "vi"

# --- API Endpoints ---
@app.get("/api/health")
def health_check():
    return {"status": "ok", "service": "FlowMy AI Speech Server", "version": "1.0.0"}

@app.get("/api/hardware")
def get_hardware():
    """Lấy thông tin phần cứng GPU/VRAM/CPU/RAM & Khuyến nghị số luồng song song"""
    return get_system_hardware_info()

@app.get("/api/models/active")
def get_active_model_info():
    """Lấy thông tin model hiện đang nạp sẵn trong RAM/VRAM"""
    return {
        "has_model": len(active_engines) > 0,
        "active_model": current_active_model
    }

@app.post("/api/translate-segments")
def translate_segments_endpoint(req: TranslateSegmentsRequest):
    """
    Dịch danh sách timeline phụ đề sang ngôn ngữ đích mà không cần chạy lại nhận diện âm thanh
    """
    if not req.segments:
        return {"status": "success", "segments": [], "target_lang": req.target_lang}

    try:
        from .engines.nllb_translator import get_global_translator
    except (ImportError, ValueError):
        from backend.engines.nllb_translator import get_global_translator
    download_root = model_manager.get_models_dir_path()
    translator = get_global_translator(download_root=download_root)

    src_l = req.src_lang or "auto"
    tgt_l = req.target_lang or "vi"

    texts = [seg.get("original_text") or seg.get("text", "") for seg in req.segments]
    translated_texts = translator.translate_batch(texts, src_lang=src_l, tgt_lang=tgt_l)

    updated_segments = []
    for i, seg in enumerate(req.segments):
        seg_copy = dict(seg)
        trans_txt = translated_texts[i] if i < len(translated_texts) else ""
        if "translations" not in seg_copy or not isinstance(seg_copy["translations"], dict):
            seg_copy["translations"] = {}
        if seg_copy.get("original_text"):
            seg_copy["translations"][src_l] = seg_copy["original_text"]
        elif seg_copy.get("text"):
            seg_copy["translations"][src_l] = seg_copy["text"]

        seg_copy["translations"][tgt_l] = trans_txt
        seg_copy["translated_text"] = trans_txt
        seg_copy["text"] = trans_txt
        updated_segments.append(seg_copy)

    tgt_name = LANGUAGE_NAMES.get(tgt_l.lower()[:2], tgt_l.upper())
    return {
        "status": "success",
        "source_lang": src_l,
        "target_lang": tgt_l,
        "target_lang_name": tgt_name,
        "segments": updated_segments
    }

@app.get("/api/models")
def get_models_info():
    """Lấy danh sách các model đã tải và trạng thái có sẵn"""
    models = model_manager.list_installed_models()
    total_size = sum(m["size_mb"] for m in models)
    status_map = model_manager.get_model_status_map()
    return {
        "models_dir": model_manager.get_models_dir_path(),
        "total_size_mb": round(total_size, 2),
        "installed_models": models,
        "status_map": status_map,
        "active_model": current_active_model
    }

from fastapi.responses import FileResponse, JSONResponse, StreamingResponse

@app.get("/api/models/load-stream")
async def load_model_stream(
    engine: Optional[str] = "faster-whisper",
    model_size: Optional[str] = "small",
    device: Optional[str] = "auto",
    compute_type: Optional[str] = "default"
):
    """
    Server-Sent Events (SSE) endpoint để stream tiến trình nạp/tải model AI thời gian thực (%)
    """
    async def event_generator():
        import queue
        import threading
        import asyncio
        import json

        msg_queue = queue.Queue()
        done_flag = threading.Event()

        def progress_cb(percent, stage, message, detail=""):
            msg_queue.put({
                "percent": percent,
                "stage": stage,
                "message": message,
                "detail": detail,
                "status": "completed" if percent >= 100 else ("error" if stage == "error" else "in_progress")
            })

        def worker():
            try:
                engine_inst = get_engine_instance(engine or "faster-whisper", model_size or "small", device or "auto", compute_type or "default")
                engine_inst.load_model(progress_callback=progress_cb)
            except Exception as e:
                msg_queue.put({
                    "percent": 0,
                    "stage": "error",
                    "message": f"Lỗi nạp model: {str(e)}",
                    "detail": str(e),
                    "status": "error"
                })
            finally:
                done_flag.set()

        thread = threading.Thread(target=worker, daemon=True)
        thread.start()

        while not done_flag.is_set() or not msg_queue.empty():
            while not msg_queue.empty():
                item = msg_queue.get()
                yield f"data: {json.dumps(item)}\n\n"
            await asyncio.sleep(0.08)

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no"
        }
    )

@app.post("/api/models/load")
def load_model_endpoint(req: LoadModelRequest):
    """Nạp trước model vào RAM/VRAM để sẵn sàng chuyển đổi tức thì"""
    import time
    start_t = time.time()
    try:
        engine_inst = get_engine_instance(req.engine or "faster-whisper", req.model_size or "small", req.device or "auto", req.compute_type or "default")
        engine_inst.load_model()
        elapsed = round(time.time() - start_t, 2)
        return {
            "status": "success",
            "message": f"Nạp thành công model '{req.model_size}' ({req.engine}) trong {elapsed}s",
            "engine": req.engine,
            "model_size": req.model_size,
            "elapsed_seconds": elapsed
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Lỗi nạp model ({req.engine} - {req.model_size}): {str(e)}")

@app.post("/api/models/directory")
def update_models_directory(req: SetModelDirRequest):
    model_manager.set_models_dir(req.directory)
    return {"status": "success", "models_dir": model_manager.get_models_dir_path()}

@app.post("/api/models/delete")
def delete_model_endpoint(req: DeleteModelRequest):
    target_path = req.model_path or req.path
    if not target_path:
        raise HTTPException(status_code=400, detail="Thiếu đường dẫn model cần xóa.")
    success = model_manager.delete_model(target_path)
    return {"status": "success" if success else "failed"}

@app.post("/api/models/clear")
def clear_all_models_endpoint():
    success = model_manager.clear_all_models()
    return {"status": "success" if success else "failed"}

@app.post("/api/identify-speakers")
async def identify_speakers_endpoint(
    request: Request,
    file: Optional[UploadFile] = File(None),
    audio_path: Optional[str] = Form(None),
    segments: Optional[str] = Form(None),
    character_samples: Optional[str] = Form(None),
    num_speakers: Optional[int] = Form(None),
    similarity_threshold: Optional[float] = Form(0.92)
):
    """
    Endpoint phân tách và nhận diện nhân vật cho danh sách timeline hiện có
    Hỗ trợ cả JSON body và Multipart Form (khi gọi từ Web UI)
    """
    temp_file_to_clean = None
    target_audio_path = None
    segs_list = []
    char_samples_list = None
    n_speakers = None
    sim_thresh = 0.92

    content_type = request.headers.get("content-type", "")

    if "application/json" in content_type:
        try:
            body = await request.json()
        except Exception:
            body = {}
        target_audio_path = body.get("audio_path")
        segs_list = body.get("segments", [])
        char_samples_list = body.get("character_samples") or body.get("characters")
        n_speakers = body.get("num_speakers")
        sim_thresh = body.get("similarity_threshold", 0.65)
    elif file is not None:
        suffix = Path(file.filename).suffix if file.filename else ".wav"
        fd, temp_path = tempfile.mkstemp(prefix="flowmy_diarize_", suffix=suffix)
        os.close(fd)
        with open(temp_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
        target_audio_path = temp_path
        temp_file_to_clean = temp_path
        if segments:
            try:
                segs_list = json.loads(segments)
            except Exception:
                segs_list = []
        if character_samples:
            try:
                char_samples_list = json.loads(character_samples)
            except Exception:
                char_samples_list = None
        n_speakers = num_speakers
        sim_thresh = similarity_threshold or 0.65
    elif audio_path is not None:
        target_audio_path = audio_path
        if segments:
            try:
                segs_list = json.loads(segments)
            except Exception:
                segs_list = []
        if character_samples:
            try:
                char_samples_list = json.loads(character_samples)
            except Exception:
                char_samples_list = None
        n_speakers = num_speakers
        sim_thresh = similarity_threshold or 0.65

    if not target_audio_path or not os.path.exists(target_audio_path):
        raise HTTPException(status_code=400, detail="Thiếu file audio hoặc đường dẫn audio_path hợp lệ để phân tách nhân vật.")

    try:
        diarizer = get_global_diarizer()
        updated_segs = diarizer.diarize_and_identify(
            audio_path=target_audio_path,
            segments=segs_list,
            character_samples=char_samples_list,
            num_speakers=n_speakers,
            similarity_threshold=sim_thresh
        )
        return {
            "status": "success",
            "total_segments": len(updated_segs),
            "segments": updated_segs
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Lỗi phân tách nhân vật: {str(e)}")
    finally:
        if temp_file_to_clean and os.path.exists(temp_file_to_clean):
            try:
                os.remove(temp_file_to_clean)
            except Exception:
                pass

@app.post("/api/transcribe")
async def transcribe_endpoint(
    request: Request,
    file: Optional[UploadFile] = File(None),
    audio_path: Optional[str] = Form(None),
    chunkIndex: Optional[int] = Form(None),
    engine: Optional[str] = Form(None),
    model_size: Optional[str] = Form(None),
    enable_translate: Optional[bool] = Form(None),
    task: Optional[str] = Form(None),
    target_lang: Optional[str] = Form(None),
    device: Optional[str] = Form(None),
    compute_type: Optional[str] = Form(None),
    vad_filter: bool = Form(True),
    enable_diarization: Optional[bool] = Form(None),
    num_speakers: Optional[int] = Form(None),
    character_samples: Optional[str] = Form(None) # JSON string list if passed in form
):
    """
    Endpoint chính nhận diện giọng nói, tự động phát hiện ngôn ngữ, dịch phụ đề & phân tách nhân vật
    Hỗ trợ cả JSON body (FlowMy HttpRequestNode) và Multipart Form (Web UI)
    """
    temp_file_to_clean = None
    target_audio_path = None
    char_samples_list = None
    is_diarize = False
    n_speakers = None

    content_type = request.headers.get("content-type", "")

    # 1. Parse JSON Body nếu request là application/json
    if "application/json" in content_type:
        try:
            body = await request.json()
        except Exception:
            body = {}
        target_audio_path = body.get("audio_path")
        chunk_idx = body.get("chunkIndex", 0)
        engine_name = body.get("engine", "faster-whisper")
        size = body.get("model_size", "small")
        is_translate = body.get("enable_translate")
        if is_translate is None:
            is_translate = (body.get("task") == "transcribe_and_translate")
        tgt_lang = body.get("target_lang", "vi")
        tsk = "transcribe_and_translate" if is_translate else "transcribe"
        dev = body.get("device", "auto")
        comp = body.get("compute_type", "default")
        vad = body.get("vad_filter", True)
        is_diarize = body.get("enable_diarization", False)
        n_speakers = body.get("num_speakers")
        char_samples_list = body.get("character_samples") or body.get("characters")
    elif file is not None:
        # Multipart upload file từ Web UI
        suffix = Path(file.filename).suffix if file.filename else ".wav"
        fd, temp_path = tempfile.mkstemp(prefix="flowmy_upload_", suffix=suffix)
        os.close(fd)
        with open(temp_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
        target_audio_path = temp_path
        temp_file_to_clean = temp_path
        chunk_idx = chunkIndex or 0
        engine_name = engine or "faster-whisper"
        size = model_size or "small"
        is_translate = enable_translate if enable_translate is not None else (task == "transcribe_and_translate")
        tgt_lang = target_lang or "vi"
        tsk = "transcribe_and_translate" if is_translate else "transcribe"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
        is_diarize = enable_diarization if enable_diarization is not None else False
        n_speakers = num_speakers
        if character_samples:
            try:
                char_samples_list = json.loads(character_samples)
            except Exception:
                char_samples_list = None
    elif audio_path is not None:
        target_audio_path = audio_path
        chunk_idx = chunkIndex or 0
        engine_name = engine or "faster-whisper"
        size = model_size or "small"
        is_translate = enable_translate if enable_translate is not None else (task == "transcribe_and_translate")
        tgt_lang = target_lang or "vi"
        tsk = "transcribe_and_translate" if is_translate else "transcribe"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
        is_diarize = enable_diarization if enable_diarization is not None else False
        n_speakers = num_speakers
        if character_samples:
            try:
                char_samples_list = json.loads(character_samples)
            except Exception:
                char_samples_list = None
    else:
        raise HTTPException(status_code=400, detail="Thiếu file audio hoặc đường dẫn audio_path.")

    if not target_audio_path or not os.path.exists(target_audio_path):
        raise HTTPException(status_code=404, detail=f"Không tìm thấy file audio: {target_audio_path}")

    try:
        engine_inst = get_engine_instance(engine_name, size, dev, comp)
        result = engine_inst.transcribe(
            audio_path=target_audio_path,
            target_lang=tgt_lang,
            chunk_index=chunk_idx,
            task=tsk,
            vad_filter=vad
        )

        detected_lang = result.get("language", "auto")
        lang_code = detected_lang.lower()[:2] if detected_lang else "auto"
        lang_name = LANGUAGE_NAMES.get(lang_code, detected_lang.upper())

        result["language_name"] = lang_name
        result["translated"] = bool(is_translate and tgt_lang and lang_code != tgt_lang.lower()[:2])
        result["target_lang"] = tgt_lang if result["translated"] else None

        # 3. Phân tách nhân vật & nhận diện giọng nói nếu được bật
        if is_diarize or char_samples_list:
            try:
                diarizer = get_global_diarizer()
                result["segments"] = diarizer.diarize_and_identify(
                    audio_path=target_audio_path,
                    segments=result.get("segments", []),
                    character_samples=char_samples_list,
                    num_speakers=n_speakers
                )
                result["has_speakers"] = True
            except Exception as d_err:
                print(f"[SpeakerDiarizer] Cảnh báo lỗi phân tách nhân vật: {d_err}")

        return result
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Lỗi nhận diện âm thanh ({engine_name}): {str(e)}")
    finally:
        if temp_file_to_clean and os.path.exists(temp_file_to_clean):
            try:
                os.remove(temp_file_to_clean)
            except Exception:
                pass

# --- Static Web UI Serving ---
frontend_dir = Path(__file__).resolve().parent.parent / "frontend"
if frontend_dir.exists():
    app.mount("/src", StaticFiles(directory=str(frontend_dir / "src")), name="src")

    @app.get("/")
    async def serve_index():
        index_file = frontend_dir / "index.html"
        if index_file.exists():
            return FileResponse(str(index_file))
        return JSONResponse({"message": "Web UI frontend is running. Please access /api/hardware for hardware info."})
