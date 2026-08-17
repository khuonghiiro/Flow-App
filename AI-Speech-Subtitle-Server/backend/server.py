import os
import tempfile
import shutil
from pathlib import Path
from typing import Optional, List
from fastapi import FastAPI, UploadFile, File, Form, HTTPException, BackgroundTasks
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
active_engines = {}

def get_engine_instance(engine_name: str, model_size: str, device: str, compute_type: str):
    key = f"{engine_name}_{model_size}_{device}_{compute_type}"
    if key not in active_engines:
        download_root = model_manager.get_models_dir_path()
        if engine_name == "whisperx":
            engine = WhisperXEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        elif engine_name == "sensevoice":
            engine = SenseVoiceEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        elif engine_name == "seamless-m4t":
            engine = SeamlessM4TEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        else: # default faster-whisper
            engine = FasterWhisperEngine(model_size=model_size, device=device, compute_type=compute_type, download_root=download_root)
        
        active_engines[key] = engine

    return active_engines[key]

# --- Pydantic Request Models ---
class TranscribeRequest(BaseModel):
    audio_path: Optional[str] = None
    chunkIndex: Optional[int] = 0
    engine: Optional[str] = "faster-whisper"
    model_size: Optional[str] = "small"
    target_lang: Optional[str] = "vi"
    task: Optional[str] = "transcribe_and_translate"
    device: Optional[str] = "auto"
    compute_type: Optional[str] = "default"
    vad_filter: Optional[bool] = True

class SetModelDirRequest(BaseModel):
    directory: str

class DeleteModelRequest(BaseModel):
    model_path: str

# --- API Endpoints ---
@app.get("/api/health")
def health_check():
    return {"status": "ok", "service": "FlowMy AI Speech Server", "version": "1.0.0"}

@app.get("/api/hardware")
def get_hardware():
    """Lấy thông tin phần cứng GPU/VRAM/CPU/RAM & Khuyến nghị số luồng song song"""
    return get_system_hardware_info()

@app.get("/api/models")
def get_models_info():
    """Lấy danh sách các model đã tải và đường dẫn lưu trữ"""
    models = model_manager.list_installed_models()
    total_size = sum(m["size_mb"] for m in models)
    return {
        "models_dir": model_manager.get_models_dir_path(),
        "total_size_mb": round(total_size, 2),
        "installed_models": models
    }

@app.post("/api/models/directory")
def update_models_directory(req: SetModelDirRequest):
    model_manager.set_models_dir(req.directory)
    return {"status": "success", "models_dir": model_manager.get_models_dir_path()}

@app.post("/api/models/delete")
def delete_model_endpoint(req: DeleteModelRequest):
    success = model_manager.delete_model(req.model_path)
    return {"status": "success" if success else "failed"}

@app.post("/api/models/clear")
def clear_all_models_endpoint():
    success = model_manager.clear_all_models()
    return {"status": "success" if success else "failed"}

@app.post("/api/transcribe")
async def transcribe_endpoint(
    req: Optional[TranscribeRequest] = None,
    file: Optional[UploadFile] = File(None),
    audio_path: Optional[str] = Form(None),
    chunkIndex: Optional[int] = Form(0),
    engine: Optional[str] = Form("faster-whisper"),
    model_size: Optional[str] = Form("small"),
    target_lang: Optional[str] = Form("vi"),
    task: Optional[str] = Form("transcribe_and_translate"),
    device: Optional[str] = Form("auto"),
    compute_type: Optional[str] = Form("default"),
    vad_filter: Optional[bool] = Form(True)
):
    """
    Endpoint chính nhận diện giọng nói & dịch phụ đề:
    Hỗ trợ cả JSON body (cho FlowMy HttpRequestNode) và Multipart Form (cho Web UI kéo thả)
    """
    temp_file_to_clean = None
    target_audio_path = None

    # 1. Parse parameters từ Request Body hoặc Form
    if req is not None and req.audio_path:
        target_audio_path = req.audio_path
        chunk_idx = req.chunkIndex or 0
        engine_name = req.engine or "faster-whisper"
        size = req.model_size or "small"
        tgt_lang = req.target_lang or "vi"
        tsk = req.task or "transcribe_and_translate"
        dev = req.device or "auto"
        comp = req.compute_type or "default"
        vad = req.vad_filter if req.vad_filter is not None else True
    elif file is not None:
        # Nhận file upload từ Web UI
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
        tgt_lang = target_lang or "vi"
        tsk = task or "transcribe_and_translate"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
    elif audio_path is not None:
        target_audio_path = audio_path
        chunk_idx = chunkIndex or 0
        engine_name = engine or "faster-whisper"
        size = model_size or "small"
        tgt_lang = target_lang or "vi"
        tsk = task or "transcribe_and_translate"
        dev = device or "auto"
        comp = compute_type or "default"
        vad = vad_filter
    else:
        raise HTTPException(status_code=400, detail="Thiếu file audio hoặc đường dẫn audio_path.")

    if not os.path.exists(target_audio_path):
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
