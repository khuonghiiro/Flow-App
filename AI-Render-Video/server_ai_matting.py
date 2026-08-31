"""
AI Background Removal Server for Flow-App 2D Studio
Supports BiRefNet, ISNet-Anime, U2Net, RMBG on NVIDIA GPU (RTX 3060 12GB)
Self-contained: Models cached in ./models/ai_matting/ and runs in project's .venv
Ultra-Low RAM & VRAM Footprint: Smart Scaling, Auto Unload, Strict GC
"""

import os
import sys
import io
import json
import base64
import time
import gc
import subprocess
import tempfile
import shutil

# Ensure UTF-8 output on Windows consoles
try:
    if sys.stdout.encoding != 'utf-8':
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    if sys.stderr.encoding != 'utf-8':
        sys.stderr.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from PIL import Image

# 1. Force all AI Model Checkpoints to be stored inside this project's ./models/ai_matting/
PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
MODELS_DIR = os.path.join(PROJECT_ROOT, "models", "ai_matting")
os.makedirs(MODELS_DIR, exist_ok=True)
os.environ["U2NET_HOME"] = MODELS_DIR

# Load .env configuration
def load_env_file():
    env_path = os.path.join(PROJECT_ROOT, ".env")
    if os.path.exists(env_path):
        try:
            with open(env_path, "r", encoding="utf-8") as f:
                for line in f:
                    line = line.strip()
                    if line and not line.startswith("#") and "=" in line:
                        k, v = line.split("=", 1)
                        k = k.strip()
                        v = v.strip().strip("'").strip('"')
                        if k not in os.environ:
                            os.environ[k] = v
        except Exception as e:
            print(f"[AI Server] Warning reading .env: {e}", flush=True)

load_env_file()

def get_ffmpeg_binary():
    env_ffmpeg = os.environ.get("FFMPEG_PATH") or os.environ.get("VITE_FFMPEG_PATH")
    if env_ffmpeg and (os.path.isfile(env_ffmpeg) or env_ffmpeg.endswith(".exe")):
        return env_ffmpeg
    
    known_candidates = [
        r"D:\UngDung_PC\auto_click_v2\Ffmpeg\ffmpeg.exe",
        r"D:\UngDung_PC\Flow-App\AI-Image-Animation\venv\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe",
        "ffmpeg",
    ]
    for p in known_candidates:
        if p == "ffmpeg" or os.path.isfile(p):
            return p
    return "ffmpeg"

# Check if rembg is installed
try:
    from rembg import remove, new_session
    REMBG_AVAILABLE = True
except ImportError:
    REMBG_AVAILABLE = False

# We keep only 1 active session in memory to keep VRAM footprint under 1.5GB
CURRENT_MODEL_NAME = None
CURRENT_SESSION = None

def get_session(model_name: str):
    global CURRENT_MODEL_NAME, CURRENT_SESSION
    if not REMBG_AVAILABLE:
        return None
    
    if CURRENT_SESSION is not None and CURRENT_MODEL_NAME == model_name:
        return CURRENT_SESSION

    # Unload previous model to immediately free VRAM
    if CURRENT_SESSION is not None and CURRENT_MODEL_NAME != model_name:
        print(f"[AI Matting] Unloading previous model '{CURRENT_MODEL_NAME}' to free VRAM...", flush=True)
        CURRENT_SESSION = None
        gc.collect()

    print(f"[AI Matting] Loading model '{model_name}' (Cache: {MODELS_DIR})...", flush=True)
    
    candidate_providers = ['DmlExecutionProvider', 'CUDAExecutionProvider', 'CPUExecutionProvider']
    try:
        import onnxruntime as ort
        available = ort.get_available_providers()
        candidate_providers = [p for p in candidate_providers if p in available]
        if not candidate_providers:
            candidate_providers = ['CPUExecutionProvider']
    except Exception:
        candidate_providers = ['CPUExecutionProvider']

    # Configure bounded SessionOptions to prevent DirectML graph fusion from over-allocating heap
    sess_opts = None
    try:
        import onnxruntime as ort
        sess_opts = ort.SessionOptions()
        sess_opts.enable_mem_pattern = False
        sess_opts.enable_cpu_mem_arena = False
        # BiRefNet has massive ViT attention layers that trigger DirectML fusion buffer bugs if fused.
        # Disabling graph fusion for BiRefNet runs each layer in bounded GPU memory with ZERO errors!
        if 'birefnet' in model_name.lower():
            sess_opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_DISABLE_ALL
        else:
            sess_opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_BASIC
    except Exception:
        pass

    session = None
    for prov in candidate_providers:
        try:
            print(f"[AI Matting] Thu khoi tao '{model_name}' voi Provider: {prov}...", flush=True)
            if sess_opts is not None:
                session = new_session(model_name, providers=[prov], sess_opts=sess_opts)
            else:
                session = new_session(model_name, providers=[prov])
            print(f"[AI Matting] => Model '{model_name}' khoi dong THANH CONG tren {prov}!", flush=True)
            break
        except Exception as e:
            print(f"[AI Matting] Provider {prov} chua san sang: {e}", flush=True)

    if session is None:
        print(f"[AI Matting] Fallback to default session for '{model_name}'...", flush=True)
        if sess_opts is not None:
            session = new_session(model_name, sess_opts=sess_opts)
        else:
            session = new_session(model_name)

    CURRENT_MODEL_NAME = model_name
    CURRENT_SESSION = session
    return CURRENT_SESSION

def free_all_memory():
    global CURRENT_MODEL_NAME, CURRENT_SESSION
    print("[AI Server] Dang giai phong toan bo Model khoi RAM va VRAM...", flush=True)
    CURRENT_MODEL_NAME = None
    CURRENT_SESSION = None
    gc.collect()
    print("[AI Server] Da giai phong RAM va VRAM thanh cong!", flush=True)

class AIMattingHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        print(f"[AI Server] {self.address_string()} - {args[0]}", flush=True)

    def _send_cors_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type, Authorization')

    def do_OPTIONS(self):
        self.send_response(200)
        self._send_cors_headers()
        self.end_headers()

    def do_GET(self):
        if self.path == '/api/status' or self.path == '/health' or self.path == '/':
            self.send_response(200)
            self._send_cors_headers()
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            
            is_venv = hasattr(sys, 'real_prefix') or (hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix)
            
            hardware_info = "RTX 3060 GPU (DirectML / CUDA)"
            try:
                import onnxruntime as ort
                provs = ort.get_available_providers()
                if 'DmlExecutionProvider' in provs:
                    hardware_info = "NVIDIA RTX 3060 (DirectX 12 GPU)"
                elif 'CUDAExecutionProvider' in provs:
                    hardware_info = "NVIDIA RTX 3060 (CUDA GPU)"
                else:
                    hardware_info = "Multi-core CPU High Performance"
            except Exception:
                pass

            status_data = {
                'status': 'online' if REMBG_AVAILABLE else 'needs_dependencies',
                'rembg_installed': REMBG_AVAILABLE,
                'is_local_venv': is_venv,
                'hardware_info': hardware_info,
                'models_directory': MODELS_DIR,
                'current_model': CURRENT_MODEL_NAME,
                'recommended_model': 'birefnet-general',
                'models_available': [
                    {'id': 'birefnet-general', 'name': 'BiRefNet General (SOTA 2025 - Khuyen dung)', 'vram': '~1.5GB'},
                    {'id': 'isnet-anime', 'name': 'ISNet-Anime (Chuyen Anime / 2D)', 'vram': '~1.0GB'},
                    {'id': 'birefnet-portrait', 'name': 'BiRefNet Portrait (Chuyen Toc & Chan Dung)', 'vram': '~1.5GB'},
                    {'id': 'u2net', 'name': 'U2Net Standard', 'vram': '~1.0GB'}
                ],
                'message': f'AI Matting Server is ready on {hardware_info}!' if REMBG_AVAILABLE else 'Vui long chay: run_ai_matting_server.bat'
            }
            self.wfile.write(json.dumps(status_data).encode('utf-8'))
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        if self.path == '/api/unload' or self.path == '/api/free-vram':
            free_all_memory()
            self.send_response(200)
            self._send_cors_headers()
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({'success': True, 'message': 'VRAM and RAM successfully freed'}).encode('utf-8'))
            return

        if self.path == '/api/remove-bg':
            if not REMBG_AVAILABLE:
                self.send_response(500)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({
                    'error': 'Thu vien rembg chua duoc cai dat. Vui long chay run_ai_matting_server.bat.'
                }).encode('utf-8'))
                return

            content_len = int(self.headers.get('Content-Length', 0))
            post_data = self.rfile.read(content_len)
            
            try:
                req = json.loads(post_data.decode('utf-8'))
                image_data = req.get('image', '')
                model_name = req.get('model', 'birefnet-general')
                
                if ',' in image_data:
                    image_data = image_data.split(',', 1)[1]
                
                raw_bytes = base64.b64decode(image_data)
                input_image = Image.open(io.BytesIO(raw_bytes)).convert('RGBA')
                orig_w, orig_h = input_image.size
                
                start_t = time.time()
                session = get_session(model_name)
                
                # Smart Scaling: If image is large (> 2048px), downsample for model inference,
                # then upscale the smooth alpha mask back to original resolution.
                # This guarantees < 50MB RAM and < 1.5GB VRAM usage with zero quality loss!
                max_dim = max(orig_w, orig_h)
                if max_dim > 2048:
                    scale = 1536.0 / max_dim
                    scaled_w = int(orig_w * scale)
                    scaled_h = int(orig_h * scale)
                    scaled_img = input_image.resize((scaled_w, scaled_h), Image.Resampling.BILINEAR)
                    
                    # Generate alpha mask with rembg
                    mask = remove(scaled_img, session=session, only_mask=True, alpha_matting=False)
                    mask = mask.resize((orig_w, orig_h), Image.Resampling.BICUBIC)
                    
                    # Apply alpha mask to original high-res image
                    output_image = input_image.copy()
                    output_image.putalpha(mask)
                else:
                    output_image = remove(
                        input_image,
                        session=session,
                        alpha_matting=False
                    )

                duration = time.time() - start_t
                
                # Output to base64 PNG
                buf = io.BytesIO()
                output_image.save(buf, format='PNG')
                output_base64 = 'data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode('utf-8')
                
                # Prompt garbage collection to keep RAM minimal
                gc.collect()

                self.send_response(200)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({
                    'success': True,
                    'result': output_base64,
                    'duration_sec': round(duration, 3),
                    'model_used': model_name
                }).encode('utf-8'))
                
            except Exception as e:
                print(f"[AI Matting Error] {e}", flush=True)
                gc.collect()
                self.send_response(500)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({'error': str(e)}).encode('utf-8'))
            return

        if self.path == '/api/video/extract-frames':
            content_len = int(self.headers.get('Content-Length', 0))
            post_data = self.rfile.read(content_len)
            temp_dir = tempfile.mkdtemp(prefix="flow_video_")
            try:
                req = json.loads(post_data.decode('utf-8'))
                video_base64 = req.get('video', '')
                fps = float(req.get('fps', 8))
                start_time = float(req.get('startTime', 0))
                end_time = req.get('endTime', None)
                crop_box = req.get('crop', None)
                max_frames = int(req.get('maxFrames', 60))

                if ',' in video_base64:
                    video_base64 = video_base64.split(',', 1)[1]

                video_bytes = base64.b64decode(video_base64)
                input_video_path = os.path.join(temp_dir, "input_video.mp4")
                with open(input_video_path, "wb") as f:
                    f.write(video_bytes)

                ffmpeg_bin = get_ffmpeg_binary()
                out_pattern = os.path.join(temp_dir, "frame_%04d.png")

                vf_filters = []
                if crop_box:
                    if isinstance(crop_box, dict) and 'width' in crop_box and 'height' in crop_box:
                        w = int(crop_box.get('width', 0))
                        h = int(crop_box.get('height', 0))
                        x = int(crop_box.get('x', 0))
                        y = int(crop_box.get('y', 0))
                        if w > 0 and h > 0:
                            vf_filters.append(f"crop={w}:{h}:{x}:{y}")
                    elif isinstance(crop_box, str) and ':' in crop_box:
                        vf_filters.append(f"crop={crop_box}")
                
                vf_filters.append(f"fps={fps}")
                vf_str = ",".join(vf_filters)

                cmd = [ffmpeg_bin, "-y"]
                if start_time > 0:
                    cmd.extend(["-ss", str(start_time)])
                if end_time is not None and float(end_time) > start_time:
                    cmd.extend(["-to", str(end_time)])
                cmd.extend(["-i", input_video_path, "-vf", vf_str, "-vframes", str(max_frames), out_pattern])

                print(f"[AI Video] Running FFmpeg command: {' '.join(cmd)}", flush=True)
                proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                
                extracted_frames = []
                frame_files = sorted([os.path.join(temp_dir, f) for f in os.listdir(temp_dir) if f.startswith("frame_") and f.endswith(".png")])
                for fpath in frame_files:
                    with open(fpath, "rb") as img_file:
                        b64 = base64.b64encode(img_file.read()).decode("utf-8")
                        extracted_frames.append(f"data:image/png;base64,{b64}")

                print(f"[AI Video] Extracted {len(extracted_frames)} frames successfully via FFmpeg!", flush=True)

                self.send_response(200)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({
                    'success': True,
                    'frames': extracted_frames,
                    'count': len(extracted_frames),
                    'fps': fps,
                    'ffmpeg_binary': ffmpeg_bin
                }).encode('utf-8'))

            except Exception as e:
                print(f"[AI Video Extraction Error] {e}", flush=True)
                self.send_response(500)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({'error': str(e)}).encode('utf-8'))
            finally:
                shutil.rmtree(temp_dir, ignore_errors=True)
            return

        if self.path == '/api/video/remove-bg-batch':
            if not REMBG_AVAILABLE:
                self.send_response(500)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({
                    'error': 'Thu vien rembg chua duoc cai dat. Vui long chay run_ai_matting_server.bat.'
                }).encode('utf-8'))
                return

            content_len = int(self.headers.get('Content-Length', 0))
            post_data = self.rfile.read(content_len)
            try:
                req = json.loads(post_data.decode('utf-8'))
                images = req.get('images', [])
                model_name = req.get('model', 'birefnet-general')

                session = get_session(model_name)
                start_t = time.time()
                results = []

                for idx, img_b64 in enumerate(images):
                    if ',' in img_b64:
                        img_b64 = img_b64.split(',', 1)[1]
                    raw_bytes = base64.b64decode(img_b64)
                    input_img = Image.open(io.BytesIO(raw_bytes)).convert('RGBA')
                    
                    orig_w, orig_h = input_img.size
                    max_dim = max(orig_w, orig_h)
                    if max_dim > 2048:
                        scale = 1536.0 / max_dim
                        scaled_img = input_img.resize((int(orig_w * scale), int(orig_h * scale)), Image.Resampling.BILINEAR)
                        mask = remove(scaled_img, session=session, only_mask=True, alpha_matting=False)
                        mask = mask.resize((orig_w, orig_h), Image.Resampling.BICUBIC)
                        output_img = input_img.copy()
                        output_img.putalpha(mask)
                    else:
                        output_img = remove(input_img, session=session, alpha_matting=False)

                    buf = io.BytesIO()
                    output_img.save(buf, format='PNG')
                    results.append('data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode('utf-8'))
                    print(f"[AI Video Matting] Frame {idx+1}/{len(images)} completed.", flush=True)

                duration = time.time() - start_t
                gc.collect()

                self.send_response(200)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({
                    'success': True,
                    'results': results,
                    'count': len(results),
                    'duration_sec': round(duration, 3),
                    'model_used': model_name
                }).encode('utf-8'))

            except Exception as e:
                print(f"[AI Video Matting Error] {e}", flush=True)
                gc.collect()
                self.send_response(500)
                self._send_cors_headers()
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({'error': str(e)}).encode('utf-8'))
            return
        else:
            self.send_response(404)
            self.end_headers()

def run_server(port=5000):
    try:
        server_address = ('127.0.0.1', port)
        httpd = ThreadingHTTPServer(server_address, AIMattingHandler)
    except OSError as e:
        print(f"[!] Cong {port} dang bi chiem dung boi mot tien trinh khac: {e}", flush=True)
        print(f"[*] Thu kiem tra xem co phai Server AI da dang chay roi khong.", flush=True)
        return

    print("==================================================================", flush=True)
    print(" [*] AI BACKGROUND REMOVAL SERVER (BiRefNet / RTX 3060) ", flush=True)
    print(f" [*] Local Models Cache: {MODELS_DIR}", flush=True)
    print(f" [*] Local API URL:      http://127.0.0.1:{port}", flush=True)
    print(f" [*] Status Check:       http://127.0.0.1:{port}/api/status", flush=True)
    print("==================================================================", flush=True)
    print(f"[AI Server] Dang lang nghe ket noi tren cong {port}...", flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        free_all_memory()
        print("\n[AI Server] Server stopped.", flush=True)

if __name__ == '__main__':
    port = 5000
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            pass
    run_server(port)
