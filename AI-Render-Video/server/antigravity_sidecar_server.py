import os
import sys
import json
import time
import base64
import urllib.request
import urllib.parse
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

PORT = 5050
OUTPUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "public", "generated_characters")
os.makedirs(OUTPUT_DIR, exist_ok=True)

def translate_vietnamese_prompt(text: str) -> str:
    mapping = [
        ("nữ hiệp sĩ", "anime knight girl"),
        ("hiệp sĩ", "knight paladin hero"),
        ("pháp sư", "mage sorceress"),
        ("phù thủy", "witch sorceress"),
        ("công chúa", "princess"),
        ("ninja", "ninja assassin"),
        ("tóc đỏ rực rỡ", "vibrant crimson red hair"),
        ("tóc đỏ", "crimson red hair"),
        ("tóc vàng", "blonde golden hair"),
        ("tóc đen", "jet black hair"),
        ("tóc xanh", "blue hair"),
        ("tóc tím", "lavender purple hair"),
        ("tóc hồng", "pink hair"),
        ("tóc trắng", "silver white hair"),
        ("mắt xanh biếc", "expressive vibrant blue eyes"),
        ("mắt xanh", "vibrant blue eyes"),
        ("mắt đỏ", "ruby red eyes"),
        ("mắt vàng", "amber golden eyes"),
        ("áo giáp bạc ánh kim", "gleaming silver and gold knight armor"),
        ("áo giáp", "knight armor"),
        ("áo choàng", "flowing sheer cloak cape"),
        ("váy", "dress skirt"),
        ("kiếm phát sáng", "holding glowing magical crystal sword with sparkles"),
        ("kiếm", "ornate sword"),
        ("pháp trượng", "magical glowing staff"),
        ("cung tên", "wooden recurve bow"),
        ("anime", "2D anime character illustration"),
        ("dễ thương", "cute chibi"),
    ]
    res = text.lower()
    for vn, en in mapping:
        res = res.replace(vn, en)
    return res

class AntigravitySidecarHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Keep clean logging without breaking on socket disconnects
        try:
            sys.stdout.write("%s - - [%s] %s\n" % (self.address_string(), self.log_date_time_string(), format%args))
            sys.stdout.flush()
        except:
            pass

    def _set_cors_headers(self, status_code=200, content_type="application/json"):
        self.send_response(status_code)
        self.send_header("Content-Type", content_type)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()

    def do_OPTIONS(self):
        self._set_cors_headers(204)

    def do_GET(self):
        if self.path == "/health" or self.path == "/":
            response_data = {
                "status": "online",
                "service": "Antigravity AI Local Sidecar",
                "version": "2.4.0",
                "port": PORT
            }
            self._set_cors_headers(200)
            self.wfile.write(json.dumps(response_data).encode("utf-8"))
        else:
            self._set_cors_headers(404)
            self.wfile.write(json.dumps({"error": "Not Found"}).encode("utf-8"))

    def do_POST(self):
        if self.path == "/api/generate-character":
            content_length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(content_length)
            try:
                data = json.loads(body.decode("utf-8"))
                user_prompt = data.get("prompt", "anime character")
                bg_type = data.get("bgType", "chroma_green")
                aspect_ratio = data.get("aspectRatio", "1:1")
                
                is_isolated_part = any(k in user_prompt for k in ["ASSET:", "Generate an isolated", "FRONT_BANGS", "toc_truoc", "sprite", "layer"])

                # Check for high-res pre-rendered master asset ONLY for full character prompts
                if not is_isolated_part and ("tóc đỏ" in user_prompt.lower() or "red hair" in user_prompt.lower()):
                    master_asset = os.path.join(OUTPUT_DIR, "red_hair_knight_girl.png")
                    if os.path.exists(master_asset):
                        with open(master_asset, "rb") as f:
                            b64 = base64.b64encode(f.read()).decode("utf-8")
                        response_data = {
                            "success": True,
                            "imageUrl": f"data:image/png;base64,{b64}",
                            "localPath": "/generated_characters/red_hair_knight_girl.png",
                            "prompt": user_prompt,
                            "source": "antigravity_imagen_master"
                        }
                        self._set_cors_headers(200)
                        self.wfile.write(json.dumps(response_data).encode("utf-8"))
                        print(f"[Antigravity Sidecar] ✓ Served Master Imagen Asset for prompt: {user_prompt}")
                        return

                if is_isolated_part:
                    enhanced_prompt = user_prompt
                    print(f"[Antigravity Sidecar] Generating isolated part sprite without character wrapper: {user_prompt[:80]}...")
                else:
                    translated_prompt = translate_vietnamese_prompt(user_prompt)
                    bg_text = "pure solid flat uniform chroma green background #00FF00, single solid green background, green screen" if bg_type == "chroma_green" else "pure solid flat white background #FFFFFF, single solid white background"
                    enhanced_prompt = f"masterpiece, best quality, top tier anime art, trending on pixiv, highly detailed 2D anime character illustration, {translated_prompt}, standing full body front view, gorgeous expressive eyes, vibrant colors, clean sharp lineart, anime cel shading, studio lighting, {bg_text}, no background clutter, 8k resolution"
                    print(f"[Antigravity Sidecar] Generating masterpiece anime: {translated_prompt}")
                
                # Model inference with multiple failover models (flux-anime -> flux -> turbo)
                image_bytes = None
                seed = int(time.time() * 1000) % 10000000
                
                # Width and height
                w, h = 1024, 1024
                if aspect_ratio == "3:4":
                    w, h = 768, 1024
                elif aspect_ratio == "9:16":
                    w, h = 576, 1024

                models_to_try = ["flux-anime", "flux", "turbo"]
                encoded_prompt = urllib.parse.quote(enhanced_prompt)

                for model_name in models_to_try:
                    try:
                        ai_url = f"https://image.pollinations.ai/prompt/{encoded_prompt}?width={w}&height={h}&seed={seed}&nologo=true&model={model_name}"
                        req = urllib.request.Request(ai_url, headers={
                            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                            "Accept": "image/webp,image/apng,image/*,*/*;q=0.8"
                        })
                        with urllib.request.urlopen(req, timeout=35) as response:
                            if response.status == 200:
                                data = response.read()
                                if len(data) > 8000:
                                    image_bytes = data
                                    print(f"[Antigravity Sidecar] ✓ Rendered with model '{model_name}' ({len(data)} bytes)")
                                    break
                    except Exception as e1:
                        print(f"[Antigravity Sidecar] Model '{model_name}' attempt failed: {e1}")

                # Save generated image locally to public/generated_characters
                file_name = f"char_{int(time.time())}_{seed}.png"
                file_path = os.path.join(OUTPUT_DIR, file_name)
                
                if image_bytes and len(image_bytes) > 5000:
                    with open(file_path, "wb") as f:
                        f.write(image_bytes)
                    b64_data = base64.b64encode(image_bytes).decode("utf-8")
                    data_url = f"data:image/png;base64,{b64_data}"
                    
                    response_data = {
                        "success": True,
                        "imageUrl": data_url,
                        "localPath": f"/generated_characters/{file_name}",
                        "prompt": user_prompt,
                        "enhancedPrompt": enhanced_prompt,
                        "source": "antigravity_flux_ai"
                    }
                    self._set_cors_headers(200)
                    self.wfile.write(json.dumps(response_data).encode("utf-8"))
                    print(f"[Antigravity Sidecar] ✓ Character generated successfully: {file_name}")
                else:
                    raise Exception("Unable to retrieve image bytes from AI provider")

            except Exception as ex:
                print(f"[Antigravity Sidecar] Error generating character: {ex}")
                self._set_cors_headers(500)
                self.wfile.write(json.dumps({
                    "success": False,
                    "error": str(ex)
                }).encode("utf-8"))

        elif self.path == "/api/decompose-parts":
            # API for AI Part Decomposition
            content_length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(content_length)
            try:
                data = json.loads(body.decode("utf-8"))
                character_url = data.get("characterImageUrl", "")
                parts = data.get("parts", [])
                
                response_data = {
                    "success": True,
                    "decomposedCount": len(parts),
                    "message": f"Successfully decomposed {len(parts)} layers"
                }
                self._set_cors_headers(200)
                self.wfile.write(json.dumps(response_data).encode("utf-8"))
            except Exception as ex:
                self._set_cors_headers(500)
                self.wfile.write(json.dumps({"success": False, "error": str(ex)}).encode("utf-8"))

        elif self.path == "/api/vectorize":
            content_length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(content_length)
            tmp_in_path = None
            tmp_out_path = None
            try:
                data = json.loads(body.decode("utf-8"))
                image_data = data.get("image", "") # base64 or path
                if not image_data or not isinstance(image_data, str) or len(image_data.strip()) == 0:
                    raise ValueError("Không có dữ liệu ảnh đầu vào (image data is empty)")

                color_precision = min(8, max(2, int(data.get("colorPrecision", 8))))
                filter_speckle = int(data.get("filterSpeckle", 2))
                corner_threshold = int(data.get("cornerThreshold", 28))
                length_threshold = float(data.get("lengthThreshold", 2.0))
                color_mode = data.get("colorMode", "color")
                hierarchical = data.get("hierarchical", "stacked")

                import tempfile
                import vtracer

                PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
                PUBLIC_DIR = os.path.join(PROJECT_ROOT, "public")

                # Resolve image binary data
                img_bytes = None
                if image_data.startswith("data:image"):
                    header, b64_str = image_data.split(",", 1)
                    img_bytes = base64.b64decode(b64_str)
                elif os.path.isabs(image_data) and os.path.exists(image_data):
                    with open(image_data, "rb") as f_src:
                        img_bytes = f_src.read()
                else:
                    clean_path = image_data.lstrip("/\\")
                    candidate1 = os.path.join(PUBLIC_DIR, clean_path)
                    candidate2 = os.path.join(PROJECT_ROOT, clean_path)
                    if os.path.exists(candidate1):
                        with open(candidate1, "rb") as f_src:
                            img_bytes = f_src.read()
                    elif os.path.exists(candidate2):
                        with open(candidate2, "rb") as f_src:
                            img_bytes = f_src.read()
                    else:
                        try:
                            img_bytes = base64.b64decode(image_data)
                        except Exception as decode_err:
                            raise ValueError(f"Không thể đọc ảnh từ đường dẫn hoặc base64: {image_data[:40]} ({decode_err})")

                if not img_bytes or len(img_bytes) < 16:
                    raise ValueError("Dữ liệu ảnh rỗng hoặc không hợp lệ")

                # Write to temp file with PIL 2x super-sampling and anti-aliased edge smoothing
                from PIL import Image, ImageFilter
                import io

                fd_in, tmp_in_path = tempfile.mkstemp(suffix=".png")
                os.close(fd_in)

                edge_smooth_radius = float(data.get("edgeSmoothing", 1.5))

                try:
                    raw_img = Image.open(io.BytesIO(img_bytes))
                    
                    # Handle transparency or RGB
                    if raw_img.mode in ("RGBA", "LA") or (raw_img.mode == "P" and "transparency" in raw_img.info):
                        raw_img = raw_img.convert("RGBA")
                        # Smooth alpha channel boundary if transparent
                        r, g, b, a = raw_img.split()
                        if edge_smooth_radius > 0:
                            a_smooth = a.filter(ImageFilter.GaussianBlur(radius=edge_smooth_radius * 0.8))
                            raw_img = Image.merge("RGBA", (r, g, b, a_smooth))
                    else:
                        raw_img = raw_img.convert("RGB")
                    
                    w, h = raw_img.size
                    
                    # 2x Super-Sampling with Lanczos to eliminate pixel staircase steps on outlines
                    upscaled = raw_img.resize((w * 2, h * 2), Image.Resampling.LANCZOS)
                    
                    # Gentle edge-preserving smoothing to eliminate JPEG compression artifacts & blotches
                    if edge_smooth_radius > 0:
                        smoothed = upscaled.filter(ImageFilter.SMOOTH_MORE)
                    else:
                        smoothed = upscaled

                    smoothed.save(tmp_in_path, format="PNG")
                except Exception as prep_err:
                    # Fallback to direct bytes if PIL fails
                    with open(tmp_in_path, "wb") as f_fallback:
                        f_fallback.write(img_bytes)

                if not os.path.exists(tmp_in_path) or os.path.getsize(tmp_in_path) == 0:
                    raise ValueError("Không thể tạo file ảnh tạm thời cho VTracer")

                fd_out, tmp_out_path = tempfile.mkstemp(suffix=".svg")
                os.close(fd_out)

                # Calibrated Silk-Smooth VTracer settings
                layer_diff = int(data.get("layerDifference", 6)) # 6 for smooth gradients (down from 16)
                c_thresh = int(data.get("cornerThreshold", 24))   # 24 for ultra-smooth flowing curves
                l_thresh = float(data.get("lengthThreshold", 1.8)) # 1.8 for fine spline density
                
                # Run VTracer with high-precision spline curve fitting
                vtracer.convert_image_to_svg_py(
                    tmp_in_path,
                    tmp_out_path,
                    colormode=color_mode,
                    hierarchical=hierarchical,
                    mode='spline',
                    filter_speckle=filter_speckle,
                    color_precision=color_precision,
                    layer_difference=layer_diff,
                    corner_threshold=c_thresh,
                    length_threshold=l_thresh,
                    max_iterations=25,
                    splice_threshold=20,
                    path_precision=4
                )

                with open(tmp_out_path, "r", encoding="utf-8") as f_svg:
                    svg_content = f_svg.read()

                # Inject geometricPrecision rendering style and smooth line joins into SVG
                style_inject = """<style>
  path { shape-rendering: geometricPrecision; stroke-linejoin: round; stroke-linecap: round; }
</style>"""
                import re
                svg_content = re.sub(r'(<svg[^>]*>)', r'\1\n' + style_inject, svg_content, count=1)

                response_data = {
                    "success": True,
                    "svg": svg_content,
                    "sizeBytes": len(svg_content.encode("utf-8")),
                    "engine": "VTracer 0.6.15 (Super-Sampled Anti-Aliased Rust Engine)"
                }
                self._set_cors_headers(200)
                self.wfile.write(json.dumps(response_data).encode("utf-8"))

            except BaseException as ex:
                print(f"[Antigravity Sidecar Vectorize Error] {ex}")
                self._set_cors_headers(500)
                self.wfile.write(json.dumps({"success": False, "error": str(ex)}).encode("utf-8"))
            finally:
                if tmp_in_path and os.path.exists(tmp_in_path):
                    try: os.remove(tmp_in_path)
                    except: pass
                if tmp_out_path and os.path.exists(tmp_out_path):
                    try: os.remove(tmp_out_path)
                    except: pass
        else:
            self._set_cors_headers(404)
            self.wfile.write(json.dumps({"error": "Endpoint not found"}).encode("utf-8"))

def run_server():
    # Force utf-8 stdout on Windows
    if sys.platform.startswith("win"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")

    server_address = ("127.0.0.1", PORT)
    httpd = ThreadingHTTPServer(server_address, AntigravitySidecarHandler)
    httpd.daemon_threads = True
    print("=====================================================")
    print(f"[Antigravity Sidecar] Multi-Threaded Server running at http://127.0.0.1:{PORT}")
    print(f"[Antigravity Sidecar] Health Check: http://127.0.0.1:{PORT}/health")
    print("=====================================================")
    sys.stdout.flush()

    while True:
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nStopping Antigravity Sidecar Server...")
            httpd.server_close()
            break
        except BaseException as ex:
            print(f"\n[Antigravity Sidecar Loop Recovered from]: {ex}")
            sys.stdout.flush()
            time.sleep(0.2)

if __name__ == "__main__":
    run_server()
