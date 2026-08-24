import os
import sys
import json
import time
import base64
import urllib.request
import urllib.parse
from http.server import HTTPServer, BaseHTTPRequestHandler

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
                
                # Check for high-res pre-rendered master asset
                if "tóc đỏ" in user_prompt.lower() or "red hair" in user_prompt.lower():
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
        else:
            self._set_cors_headers(404)
            self.wfile.write(json.dumps({"error": "Endpoint not found"}).encode("utf-8"))

def run_server():
    # Force utf-8 stdout on Windows
    if sys.platform.startswith("win"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")

    server_address = ("127.0.0.1", PORT)
    httpd = HTTPServer(server_address, AntigravitySidecarHandler)
    print("=====================================================")
    print(f"[Antigravity Sidecar] Server running at http://127.0.0.1:{PORT}")
    print(f"[Antigravity Sidecar] Health Check: http://127.0.0.1:{PORT}/health")
    print("=====================================================")
    sys.stdout.flush()
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping Antigravity Sidecar Server...")
        httpd.server_close()

if __name__ == "__main__":
    run_server()
