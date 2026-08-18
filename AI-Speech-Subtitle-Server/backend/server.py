import os
import sys
import shutil
import tempfile
from pathlib import Path
from typing import Optional, List, Dict, Any

# Tích hợp Windows System Certificate Store để sửa lỗi SSL Certificate Verify Failed trên Windows/VPN
try:
    import truststore
    truststore.inject_into_ssl()
except Exception:
    pass

# Tự động nạp FFmpeg từ imageio_ffmpeg vào PATH để pydub, torchaudio, funasr nhận diện tức thì
try:
    import imageio_ffmpeg
    ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()
    ffmpeg_dir = os.path.dirname(ffmpeg_exe)
    if ffmpeg_dir not in os.environ.get("PATH", ""):
        os.environ["PATH"] = ffmpeg_dir + os.pathsep + os.environ.get("PATH", "")
    from pydub import AudioSegment
    AudioSegment.converter = ffmpeg_exe
    AudioSegment.ffmpeg = ffmpeg_exe
except Exception:
    pass

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
from .pipeline_runner import get_pipeline_runner

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
import threading

active_engines = {}
current_active_model = None
_engine_lock = threading.Lock()

def get_engine_instance(engine_name: str, model_size: str, device: str, compute_type: str):
    global current_active_model
    key = f"{engine_name}_{model_size}_{device}_{compute_type}"
    if key in active_engines:
        return active_engines[key]

    with _engine_lock:
        if key in active_engines:
            return active_engines[key]

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

class TranscribeOnlyRequest(BaseModel):
    audio_path: Optional[str] = None
    engine: Optional[str] = "faster-whisper"
    model_size: Optional[str] = "small"
    device: Optional[str] = "auto"
    compute_type: Optional[str] = "default"
    vad_filter: Optional[bool] = True
    word_timestamps: Optional[bool] = False

class PipelineRequest(BaseModel):
    audio_path: Optional[str] = None
    enable_transcribe: Optional[bool] = True
    enable_diarization: Optional[bool] = True
    enable_translate: Optional[bool] = False
    execution_mode: Optional[str] = "concurrent"  # "concurrent" | "sequential"
    asr_engine: Optional[str] = "faster-whisper"
    asr_model_size: Optional[str] = "small"
    target_lang: Optional[str] = "vi"
    src_lang: Optional[str] = "auto"
    character_samples: Optional[List[Dict[str, Any]]] = None
    num_speakers: Optional[int] = None
    similarity_threshold: Optional[float] = 0.65
    min_duration: Optional[float] = 0.4
    adaptive_learning: Optional[bool] = True
    diarize_engine: Optional[str] = "auto"
    device: Optional[str] = "auto"
    compute_type: Optional[str] = "default"
    vad_filter: Optional[bool] = True
    word_timestamps: Optional[bool] = False
    chunkIndex: Optional[int] = 0
    time_offset: Optional[float] = 0.0

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

@app.post("/api/models/unload")
def unload_models_endpoint():
    """Giải phóng toàn bộ model AI khỏi GPU VRAM / RAM tức thì"""
    for key, eng in list(active_engines.items()):
        if hasattr(eng, "unload_model"):
            try:
                eng.unload_model()
            except Exception:
                pass
    active_engines.clear()
    global current_active_model
    current_active_model = None
    try:
        from .engines.nllb_translator import get_global_translator
        get_global_translator().unload_model()
    except Exception:
        pass
    import gc
    gc.collect()
    try:
        import torch
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
    except Exception:
        pass
    return {"status": "success", "message": "Đã giải phóng toàn bộ model AI khỏi VRAM và RAM!"}

@app.post("/api/identify-speakers")
async def identify_speakers_endpoint(
    request: Request,
    file: Optional[UploadFile] = File(None),
    audio_path: Optional[str] = Form(None),
    segments: Optional[str] = Form(None),
    character_samples: Optional[str] = Form(None),
    num_speakers: Optional[int] = Form(None),
    similarity_threshold: Optional[float] = Form(0.75),
    min_duration: Optional[float] = Form(0.4),
    adaptive_learning: Optional[bool] = Form(True),
    embedding_engine: Optional[str] = Form("auto")
):
    """
    Endpoint phân tách và nhận diện nhân vật cho danh sách timeline hiện có.
    Hỗ trợ cả JSON body và Multipart Form (khi gọi từ Web UI / FlowMy Node).
    """
    temp_file_to_clean = None
    target_audio_path = None
    segs_list = []
    char_samples_list = None
    n_speakers = None
    sim_thresh = 0.70
    min_dur = 0.4
    adapt_learn = True
    emb_engine = "auto"

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
        sim_thresh = float(body.get("similarity_threshold", 0.70))
        min_dur = float(body.get("min_duration", 0.4))
        adapt_learn = bool(body.get("adaptive_learning", True))
        emb_engine = str(body.get("embedding_engine", "auto"))
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
        sim_thresh = similarity_threshold if similarity_threshold is not None else 0.70
        min_dur = min_duration if min_duration is not None else 0.4
        adapt_learn = adaptive_learning if adaptive_learning is not None else True
        emb_engine = embedding_engine or "auto"
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
        sim_thresh = similarity_threshold if similarity_threshold is not None else 0.70
        min_dur = min_duration if min_duration is not None else 0.4
        adapt_learn = adaptive_learning if adaptive_learning is not None else True
        emb_engine = embedding_engine or "auto"

    if not target_audio_path or not os.path.exists(target_audio_path):
        raise HTTPException(status_code=400, detail="Thiếu file audio hoặc đường dẫn audio_path hợp lệ để phân tách nhân vật.")

    try:
        diarizer = get_global_diarizer()
        updated_segs = diarizer.diarize_and_identify(
            audio_path=target_audio_path,
            segments=segs_list,
            character_samples=char_samples_list,
            num_speakers=n_speakers,
            similarity_threshold=sim_thresh,
            min_duration=min_dur,
            adaptive_learning=adapt_learn,
            embedding_engine=emb_engine
        )
        return {
            "status": "success",
            "total_segments": len(updated_segs),
            "similarity_threshold": sim_thresh,
            "min_duration": min_dur,
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
    character_samples: Optional[str] = Form(None),
    similarity_threshold: Optional[float] = Form(0.75),
    min_duration: Optional[float] = Form(0.4),
    adaptive_learning: Optional[bool] = Form(True),
    embedding_engine: Optional[str] = Form("auto")
):
    """
    Endpoint chính nhận diện giọng nói, tự động phát hiện ngôn ngữ, dịch phụ đề & phân tách nhân vật.
    Hỗ trợ cả JSON body (FlowMy HttpRequestNode) và Multipart Form (Web UI).
    """
    temp_file_to_clean = None
    target_audio_path = None
    char_samples_list = None
    is_diarize = False
    n_speakers = None
    sim_thresh = 0.75
    min_dur = 0.4
    adapt_learn = True
    emb_engine = "auto"

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
        sim_thresh = float(body.get("similarity_threshold", 0.75))
        min_dur = float(body.get("min_duration", 0.4))
        adapt_learn = bool(body.get("adaptive_learning", True))
        emb_engine = str(body.get("embedding_engine", "auto"))
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
        sim_thresh = similarity_threshold if similarity_threshold is not None else 0.75
        min_dur = min_duration if min_duration is not None else 0.4
        adapt_learn = adaptive_learning if adaptive_learning is not None else True
        emb_engine = embedding_engine or "auto"
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
        sim_thresh = similarity_threshold if similarity_threshold is not None else 0.75
        min_dur = min_duration if min_duration is not None else 0.4
        adapt_learn = adaptive_learning if adaptive_learning is not None else True
        emb_engine = embedding_engine or "auto"
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
                    num_speakers=n_speakers,
                    similarity_threshold=sim_thresh,
                    min_duration=min_dur,
                    adaptive_learning=adapt_learn,
                    embedding_engine=emb_engine
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

@app.post("/api/transcribe-only")
async def transcribe_only_endpoint(
    request: Request,
    file: Optional[UploadFile] = File(None),
    audio_path: Optional[str] = Form(None),
    engine: Optional[str] = Form(None),
    model_size: Optional[str] = Form(None),
    device: Optional[str] = Form(None),
    compute_type: Optional[str] = Form(None),
    vad_filter: bool = Form(True),
    word_timestamps: bool = Form(False)
):
    """
    Endpoint chỉ có nhiệm vụ bóc tách phụ đề thuần túy (ASR Only).
    Không dịch, không phân tách nhân vật -> Tốc độ xử lý nhanh nhất!
    """
    temp_file_to_clean = None
    target_audio_path = None
    engine_name = "faster-whisper"
    size = "small"
    dev = "auto"
    comp = "default"
    vad = True
    word_ts = False

    content_type = request.headers.get("content-type", "")
    if "application/json" in content_type:
        try:
            body = await request.json()
        except Exception:
            body = {}
        target_audio_path = body.get("audio_path")
        engine_name = body.get("engine", "faster-whisper")
        size = body.get("model_size", "small")
        dev = body.get("device", "auto")
        comp = body.get("compute_type", "default")
        vad = body.get("vad_filter", True)
        word_ts = body.get("word_timestamps", False)
    elif file is not None:
        suffix = Path(file.filename).suffix if file.filename else ".wav"
        fd, temp_path = tempfile.mkstemp(prefix="flowmy_trans_only_", suffix=suffix)
        os.close(fd)
        with open(temp_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
        target_audio_path = temp_path
        temp_file_to_clean = temp_path
        engine_name = engine or "faster-whisper"
        size = model_size or "small"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
        word_ts = word_timestamps
    elif audio_path is not None:
        target_audio_path = audio_path
        engine_name = engine or "faster-whisper"
        size = model_size or "small"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
        word_ts = word_timestamps
    else:
        raise HTTPException(status_code=400, detail="Thiếu file audio hoặc đường dẫn audio_path.")

    if not target_audio_path or not os.path.exists(target_audio_path):
        raise HTTPException(status_code=404, detail=f"Không tìm thấy file audio: {target_audio_path}")

    try:
        engine_inst = get_engine_instance(engine_name, size, dev, comp)
        raw_result = engine_inst.transcribe(
            audio_path=target_audio_path,
            task="transcribe_only",
            vad_filter=vad,
            word_timestamps=word_ts
        )
        detected_lang = raw_result.get("language", "auto")
        lang_code = detected_lang.lower()[:2] if detected_lang else "auto"
        lang_name = LANGUAGE_NAMES.get(lang_code, detected_lang.upper())

        return {
            "status": "success",
            "task": "transcribe_only",
            "engine": engine_name,
            "model_size": size,
            "language": detected_lang,
            "language_name": lang_name,
            "language_probability": raw_result.get("language_probability", 1.0),
            "total_segments": len(raw_result.get("segments", [])),
            "segments": raw_result.get("segments", [])
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Lỗi bóc tách phụ đề ({engine_name}): {str(e)}")
    finally:
        if temp_file_to_clean and os.path.exists(temp_file_to_clean):
            try:
                os.remove(temp_file_to_clean)
            except Exception:
                pass

@app.post("/api/pipeline")
async def pipeline_endpoint(
    request: Request,
    file: Optional[UploadFile] = File(None),
    audio_path: Optional[str] = Form(None),
    enable_transcribe: Optional[bool] = Form(True),
    enable_diarization: Optional[bool] = Form(True),
    enable_translate: Optional[bool] = Form(False),
    execution_mode: Optional[str] = Form("concurrent"),
    asr_engine: Optional[str] = Form("faster-whisper"),
    asr_model_size: Optional[str] = Form("small"),
    target_lang: Optional[str] = Form("vi"),
    src_lang: Optional[str] = Form("auto"),
    character_samples: Optional[str] = Form(None),
    num_speakers: Optional[int] = Form(None),
    similarity_threshold: Optional[float] = Form(0.65),
    min_duration: Optional[float] = Form(0.4),
    adaptive_learning: Optional[bool] = Form(True),
    diarize_engine: Optional[str] = Form("auto"),
    device: Optional[str] = Form("auto"),
    compute_type: Optional[str] = Form("default"),
    vad_filter: bool = Form(True),
    word_timestamps: bool = Form(False),
    chunkIndex: Optional[int] = Form(0),
    time_offset: Optional[float] = Form(0.0)
):
    """
    Endpoint Tổng Hợp All-in-One: Bóc tách phụ đề + Nhận diện nhân vật + Dịch thuật.
    Hỗ trợ chế độ Concurrent (song song khi đủ VRAM) hoặc Sequential (tuần tự tiết kiệm VRAM).
    Hỗ trợ xử lý song song các chunk audio cắt nhỏ với tham số chunkIndex & time_offset.
    """
    temp_file_to_clean = None
    target_audio_path = None
    e_trans = True
    e_diar = True
    e_tl = False
    exec_mode = "concurrent"
    a_eng = "faster-whisper"
    a_size = "small"
    tgt_l = "vi"
    src_l = "auto"
    char_samples_list = None
    n_spk = None
    sim_th = 0.65
    min_d = 0.4
    ad_learn = True
    d_eng = "auto"
    dev = "auto"
    comp = "default"
    vad = True
    word_ts = False

    content_type = request.headers.get("content-type", "")
    if "application/json" in content_type:
        try:
            body = await request.json()
        except Exception:
            body = {}
        target_audio_path = body.get("audio_path")
        e_trans = bool(body.get("enable_transcribe", True))
        e_diar = bool(body.get("enable_diarization", True))
        e_tl = bool(body.get("enable_translate", False))
        exec_mode = str(body.get("execution_mode", "concurrent"))
        a_eng = str(body.get("asr_engine", "faster-whisper"))
        a_size = str(body.get("asr_model_size", "small"))
        tgt_l = str(body.get("target_lang", "vi"))
        src_l = str(body.get("src_lang", "auto"))
        char_samples_list = body.get("character_samples") or body.get("characters")
        n_spk = body.get("num_speakers")
        sim_th = float(body.get("similarity_threshold", 0.65))
        min_d = float(body.get("min_duration", 0.4))
        ad_learn = bool(body.get("adaptive_learning", True))
        d_eng = str(body.get("diarize_engine", "auto"))
        dev = str(body.get("device", "auto"))
        comp = str(body.get("compute_type", "default"))
        vad = bool(body.get("vad_filter", True))
        word_ts = bool(body.get("word_timestamps", False))
        chunk_idx = int(body.get("chunkIndex", 0) or 0)
        t_offset = float(body.get("time_offset", 0.0) or 0.0)
    elif file is not None:
        suffix = Path(file.filename).suffix if file.filename else ".wav"
        fd, temp_path = tempfile.mkstemp(prefix="flowmy_pipeline_", suffix=suffix)
        os.close(fd)
        with open(temp_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
        target_audio_path = temp_path
        temp_file_to_clean = temp_path
        e_trans = enable_transcribe if enable_transcribe is not None else True
        e_diar = enable_diarization if enable_diarization is not None else True
        e_tl = enable_translate if enable_translate is not None else False
        exec_mode = execution_mode or "concurrent"
        a_eng = asr_engine or "faster-whisper"
        a_size = asr_model_size or "small"
        tgt_l = target_lang or "vi"
        src_l = src_lang or "auto"
        n_spk = num_speakers
        sim_th = similarity_threshold if similarity_threshold is not None else 0.65
        min_d = min_duration if min_duration is not None else 0.4
        ad_learn = adaptive_learning if adaptive_learning is not None else True
        d_eng = diarize_engine or "auto"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
        word_ts = word_timestamps
        chunk_idx = int(chunkIndex or 0)
        t_offset = float(time_offset or 0.0)
        if character_samples:
            try:
                char_samples_list = json.loads(character_samples)
            except Exception:
                char_samples_list = None
    elif audio_path is not None:
        target_audio_path = audio_path
        e_trans = enable_transcribe if enable_transcribe is not None else True
        e_diar = enable_diarization if enable_diarization is not None else True
        e_tl = enable_translate if enable_translate is not None else False
        exec_mode = execution_mode or "concurrent"
        a_eng = asr_engine or "faster-whisper"
        a_size = asr_model_size or "small"
        tgt_l = target_lang or "vi"
        src_l = src_lang or "auto"
        n_spk = num_speakers
        sim_th = similarity_threshold if similarity_threshold is not None else 0.65
        min_d = min_duration if min_duration is not None else 0.4
        ad_learn = adaptive_learning if adaptive_learning is not None else True
        d_eng = diarize_engine or "auto"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
        word_ts = word_timestamps
        chunk_idx = int(chunkIndex or 0)
        t_offset = float(time_offset or 0.0)
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
        runner = get_pipeline_runner()
        result = runner.run_pipeline(
            audio_path=target_audio_path,
            enable_transcribe=e_trans,
            enable_diarization=e_diar,
            enable_translate=e_tl,
            execution_mode=exec_mode,
            asr_engine=a_eng,
            asr_model_size=a_size,
            target_lang=tgt_l,
            src_lang=src_l,
            character_samples=char_samples_list,
            num_speakers=n_spk,
            similarity_threshold=sim_th,
            min_duration=min_d,
            adaptive_learning=ad_learn,
            diarize_engine=d_eng,
            device=dev,
            compute_type=comp,
            vad_filter=vad,
            word_timestamps=word_ts,
            chunk_index=chunk_idx,
            time_offset=t_offset,
            active_engines_dict=active_engines,
            get_engine_func=get_engine_instance
        )
        return result
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Lỗi thực thi Pipeline All-in-One: {str(e)}")
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
