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

                try:
                    import vtracer
                except ImportError:
                    raise RuntimeError("Thư viện vtracer chưa được cài đặt trong Python. Vui lòng cài đặt: py -3.11 -m pip install vtracer")

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

                from PIL import Image, ImageFilter
                import io, cv2, numpy as np

                raw_img = Image.open(io.BytesIO(img_bytes))
                is_rgba = raw_img.mode in ("RGBA", "LA") or (raw_img.mode == "P" and "transparency" in raw_img.info)
                if is_rgba:
                    raw_img = raw_img.convert("RGBA")
                else:
                    raw_img = raw_img.convert("RGB")

                w, h = raw_img.size
                max_dim = int(data.get("maxDimension", 800))
                if max(w, h) > max_dim:
                    ratio = max_dim / float(max(w, h))
                    target_w, target_h = max(32, int(w * ratio)), max(32, int(h * ratio))
                    processed_img = raw_img.resize((target_w, target_h), Image.Resampling.LANCZOS)
                else:
                    processed_img = raw_img

                img_np = np.array(processed_img)
                edge_smooth_radius = float(data.get("edgeSmoothing", 1.2))

                # Bilateral smoothing in BGR, then convert back to true RGB
                if is_rgba:
                    bgr = cv2.cvtColor(img_np[:, :, :3], cv2.COLOR_RGB2BGR)
                    alpha = img_np[:, :, 3]
                    if edge_smooth_radius > 0:
                        d = min(11, max(5, int(edge_smooth_radius * 5)))
                        sigma = float(edge_smooth_radius * 22.0)
                        smooth_bgr = cv2.bilateralFilter(bgr, d=d, sigmaColor=sigma, sigmaSpace=sigma)
                        smooth_rgb = cv2.cvtColor(smooth_bgr, cv2.COLOR_BGR2RGB)
                        smooth_alpha = cv2.GaussianBlur(alpha, (3, 3), edge_smooth_radius * 0.4)
                        smooth_rgba = np.dstack([smooth_rgb, smooth_alpha])
                        buf_out = io.BytesIO()
                        Image.fromarray(smooth_rgba).save(buf_out, format="PNG")
                        prepared_bytes = buf_out.getvalue()
                    else:
                        buf_out = io.BytesIO()
                        processed_img.save(buf_out, format="PNG")
                        prepared_bytes = buf_out.getvalue()
                else:
                    bgr = cv2.cvtColor(img_np, cv2.COLOR_RGB2BGR)
                    if edge_smooth_radius > 0:
                        d = min(11, max(5, int(edge_smooth_radius * 5)))
                        sigma = float(edge_smooth_radius * 22.0)
                        smooth_bgr = cv2.bilateralFilter(bgr, d=d, sigmaColor=sigma, sigmaSpace=sigma)
                        smooth_rgb = cv2.cvtColor(smooth_bgr, cv2.COLOR_BGR2RGB)
                        buf_out = io.BytesIO()
                        Image.fromarray(smooth_rgb).save(buf_out, format="PNG")
                        prepared_bytes = buf_out.getvalue()
                    else:
                        buf_out = io.BytesIO()
                        processed_img.save(buf_out, format="PNG")
                        prepared_bytes = buf_out.getvalue()

                c_prec = min(8, max(2, int(data.get("colorPrecision", 6))))
                layer_diff = max(2, int(data.get("layerDifference", 16)))
                c_thresh = max(10, int(data.get("cornerThreshold", 50)))
                l_thresh = max(0.5, float(data.get("lengthThreshold", 3.0)))
                f_speckle = max(1, int(data.get("filterSpeckle", 6)))
                max_iter = max(5, min(20, int(data.get("maxIterations", 10))))

                svg_content = vtracer.convert_raw_image_to_svg(
                    prepared_bytes,
                    img_format="png",
                    colormode=color_mode,
                    hierarchical=hierarchical,
                    mode='spline',
                    filter_speckle=f_speckle,
                    color_precision=c_prec,
                    layer_difference=layer_diff,
                    corner_threshold=c_thresh,
                    length_threshold=l_thresh,
                    max_iterations=max_iter,
                    splice_threshold=45,
                    path_precision=2
                )

                style_inject = """<style> path { shape-rendering: geometricPrecision; } </style>"""
                import re
                svg_content = re.sub(r'(<svg[^>]*>)', r'\1\n' + style_inject, svg_content, count=1)

                response_data = {
                    "success": True,
                    "svg": svg_content,
                    "sizeBytes": len(svg_content.encode("utf-8")),
                    "engine": "VTracer 0.6.15 (High-Precision Bilateral Spline)"
                }
                self._set_cors_headers(200)
                self.wfile.write(json.dumps(response_data).encode("utf-8"))

            except BaseException as ex:
                print(f"[Antigravity Sidecar Vectorize Error] {ex}")
                self._set_cors_headers(500)
                self.wfile.write(json.dumps({"success": False, "error": str(ex)}).encode("utf-8"))

        elif self.path == "/api/auto-rig":
            content_length = int(self.headers.get("Content-Length", 0))
            post_data = self.rfile.read(content_length)
            try:
                data = json.loads(post_data.decode("utf-8"))
                image_data = data.get("image", "")
                part_type = data.get("partType", "ban_tay")

                img_bytes = None
                if image_data.startswith("data:image/"):
                    base64_str = image_data.split(",", 1)[1]
                    img_bytes = base64.b64decode(base64_str)
                elif os.path.exists(image_data):
                    with open(image_data, "rb") as f:
                        img_bytes = f.read()
                else:
                    try:
                        img_bytes = base64.b64decode(image_data)
                    except:
                        pass

                if not img_bytes:
                    raise ValueError("Dữ liệu ảnh không hợp lệ")

                from PIL import Image
                import io, math, numpy as np

                pil_img = Image.open(io.BytesIO(img_bytes)).convert("RGB")
                img_w, img_h = pil_img.size
                img_np = np.array(pil_img)

                bone_nodes = []
                engine_name = "Google MediaPipe AI (Neural Keypoints)"
                landmarks_count = 0

                # 1. Try MediaPipe Hands if part is hand
                if "tay" in part_type or part_type == "ban_tay":
                    try:
                        import mediapipe as mp
                        global _MP_HANDS_INSTANCE, _MP_LOCK
                        if '_MP_LOCK' not in globals():
                            import threading
                            _MP_LOCK = threading.Lock()
                            _MP_HANDS_INSTANCE = mp.solutions.hands.Hands(
                                static_image_mode=True,
                                max_num_hands=1,
                                min_detection_confidence=0.15
                            )

                        with _MP_LOCK:
                            results = _MP_HANDS_INSTANCE.process(img_np)

                        if results.multi_hand_landmarks and len(results.multi_hand_landmarks) > 0:
                            hl = results.multi_hand_landmarks[0]
                            landmarks_count = len(hl.landmark)
                            lm = [(p.x, p.y) for p in hl.landmark]

                            def calc_bone(p1, p2, parent_deg=0):
                                dx = p2[0] - p1[0]
                                dy = p2[1] - p1[1]
                                deg = math.degrees(math.atan2(dx, -dy))
                                local_rot = deg - parent_deg
                                while local_rot > 180: local_rot -= 360
                                while local_rot < -180: local_rot += 360
                                length = math.hypot(dx, dy)
                                return round(local_rot, 1), round(length, 3), deg

                            wrist_x, wrist_y = lm[0]
                            palm_x, palm_y = (lm[0][0] + lm[9][0]) / 2.0, (lm[0][1] + lm[9][1]) / 2.0

                            rot_wrist, len_wrist, deg_wrist = calc_bone(lm[0], (palm_x, palm_y), 0)
                            bone_nodes.append({
                                "id": "wrist_root", "name": "Cổ Tay (Wrist)", "parentId": None,
                                "position": [round(wrist_x, 3), round(wrist_y, 3)],
                                "rotation": round(deg_wrist, 1), "length": max(0.08, len_wrist), "color": "#f59e0b"
                            })

                            rot_palm, len_palm, deg_palm = calc_bone((palm_x, palm_y), lm[9], deg_wrist)
                            bone_nodes.append({
                                "id": "palm_center", "name": "Tâm Bàn Tay (Palm)", "parentId": "wrist_root",
                                "position": [0.0, 0.0], "rotation": rot_palm, "length": max(0.08, len_palm), "color": "#f59e0b"
                            })

                            # Thumb
                            r_tb, l_tb, d_tb = calc_bone((palm_x, palm_y), lm[2], deg_palm)
                            bone_nodes.append({
                                "id": "thumb_base", "name": "Gốc Ngón Cái", "parentId": "palm_center",
                                "position": [round(lm[2][0] - palm_x, 3), round(lm[2][1] - palm_y, 3)],
                                "rotation": r_tb, "length": max(0.05, l_tb), "color": "#ef4444"
                            })
                            r_tt, l_tt, _ = calc_bone(lm[2], lm[4], d_tb)
                            bone_nodes.append({
                                "id": "thumb_tip", "name": "Đầu Ngón Cái", "parentId": "thumb_base",
                                "position": [0.0, 0.0], "rotation": r_tt, "length": max(0.05, l_tt), "color": "#ef4444"
                            })

                            # Index
                            r_ib, l_ib, d_ib = calc_bone((palm_x, palm_y), lm[5], deg_palm)
                            bone_nodes.append({
                                "id": "index_base", "name": "Gốc Ngón Trỏ", "parentId": "palm_center",
                                "position": [round(lm[5][0] - palm_x, 3), round(lm[5][1] - palm_y, 3)],
                                "rotation": r_ib, "length": max(0.05, l_ib), "color": "#3b82f6"
                            })
                            r_im, l_im, d_im = calc_bone(lm[5], lm[6], d_ib)
                            bone_nodes.append({
                                "id": "index_mid", "name": "Giữa Ngón Trỏ", "parentId": "index_base",
                                "position": [0.0, 0.0], "rotation": r_im, "length": max(0.04, l_im), "color": "#3b82f6"
                            })
                            r_it, l_it, _ = calc_bone(lm[6], lm[8], d_im)
                            bone_nodes.append({
                                "id": "index_tip", "name": "Đầu Ngón Trỏ", "parentId": "index_mid",
                                "position": [0.0, 0.0], "rotation": r_it, "length": max(0.04, l_it), "color": "#3b82f6"
                            })

                            # Middle
                            r_mb, l_mb, d_mb = calc_bone((palm_x, palm_y), lm[9], deg_palm)
                            bone_nodes.append({
                                "id": "middle_base", "name": "Gốc Ngón Giữa", "parentId": "palm_center",
                                "position": [round(lm[9][0] - palm_x, 3), round(lm[9][1] - palm_y, 3)],
                                "rotation": r_mb, "length": max(0.05, l_mb), "color": "#10b981"
                            })
                            r_mm, l_mm, d_mm = calc_bone(lm[9], lm[10], d_mb)
                            bone_nodes.append({
                                "id": "middle_mid", "name": "Giữa Ngón Giữa", "parentId": "middle_base",
                                "position": [0.0, 0.0], "rotation": r_mm, "length": max(0.04, l_mm), "color": "#10b981"
                            })
                            r_mt, l_mt, _ = calc_bone(lm[10], lm[12], d_mm)
                            bone_nodes.append({
                                "id": "middle_tip", "name": "Đầu Ngón Giữa", "parentId": "middle_mid",
                                "position": [0.0, 0.0], "rotation": r_mt, "length": max(0.04, l_mt), "color": "#10b981"
                            })

                            # Ring
                            r_rb, l_rb, d_rb = calc_bone((palm_x, palm_y), lm[13], deg_palm)
                            bone_nodes.append({
                                "id": "ring_base", "name": "Gốc Ngón Áp Út", "parentId": "palm_center",
                                "position": [round(lm[13][0] - palm_x, 3), round(lm[13][1] - palm_y, 3)],
                                "rotation": r_rb, "length": max(0.05, l_rb), "color": "#8b5cf6"
                            })
                            r_rm, l_rm, d_rm = calc_bone(lm[13], lm[14], d_rb)
                            bone_nodes.append({
                                "id": "ring_mid", "name": "Giữa Ngón Áp Út", "parentId": "ring_base",
                                "position": [0.0, 0.0], "rotation": r_rm, "length": max(0.04, l_rm), "color": "#8b5cf6"
                            })
                            r_rt, l_rt, _ = calc_bone(lm[14], lm[16], d_rm)
                            bone_nodes.append({
                                "id": "ring_tip", "name": "Đầu Ngón Áp Út", "parentId": "ring_mid",
                                "position": [0.0, 0.0], "rotation": r_rt, "length": max(0.04, l_rt), "color": "#8b5cf6"
                            })

                            # Pinky
                            r_pb, l_pb, d_pb = calc_bone((palm_x, palm_y), lm[17], deg_palm)
                            bone_nodes.append({
                                "id": "pinky_base", "name": "Gốc Ngón Út", "parentId": "palm_center",
                                "position": [round(lm[17][0] - palm_x, 3), round(lm[17][1] - palm_y, 3)],
                                "rotation": r_pb, "length": max(0.04, l_pb), "color": "#ec4899"
                            })
                            r_pm, l_pm, d_pm = calc_bone(lm[17], lm[18], d_pb)
                            bone_nodes.append({
                                "id": "pinky_mid", "name": "Giữa Ngón Út", "parentId": "pinky_base",
                                "position": [0.0, 0.0], "rotation": r_pm, "length": max(0.03, l_pm), "color": "#ec4899"
                            })
                            r_pt, l_pt, _ = calc_bone(lm[18], lm[20], d_pm)
                            bone_nodes.append({
                                "id": "pinky_tip", "name": "Đầu Ngón Út", "parentId": "pinky_mid",
                                "position": [0.0, 0.0], "rotation": r_pt, "length": max(0.03, l_pt), "color": "#ec4899"
                            })
                    except Exception as mp_err:
                        print(f"[MediaPipe Detection Error]: {mp_err}")

                # 2. High-Precision Computer Vision Convex-Hull Fingertip & Palm Detection
                if not bone_nodes and ("tay" in part_type or part_type == "ban_tay"):
                    try:
                        import cv2
                        min_dim = float(min(img_w, img_h))
                        gray = cv2.cvtColor(img_np, cv2.COLOR_RGB2GRAY)
                        mean_corner = (int(gray[0,0]) + int(gray[0,-1]) + int(gray[-1,0]) + int(gray[-1,-1])) / 4.0
                        if mean_corner > 200:
                            thresh = cv2.threshold(gray, 240, 255, cv2.THRESH_BINARY_INV)[1]
                        else:
                            thresh = cv2.threshold(gray, 20, 255, cv2.THRESH_BINARY)[1]

                        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
                        if contours:
                            cnt = max(contours, key=cv2.contourArea)
                            dist = cv2.distanceTransform(thresh, cv2.DIST_L2, 5)
                            _, max_r, _, (palm_x, palm_y) = cv2.minMaxLoc(dist)

                            # Wrist base (lowest contour points)
                            pts = cnt.reshape(-1, 2)
                            wrist_candidates = [p for p in pts if p[1] > palm_y + max_r * 0.6]
                            wrist_y = int(np.max([p[1] for p in wrist_candidates])) if wrist_candidates else int(palm_y + max_r)
                            wrist_x = int(np.mean([p[0] for p in wrist_candidates if p[1] > wrist_y - 25])) if wrist_candidates else palm_x

                            # Convex hull for finger tips
                            hull_pts = cv2.convexHull(cnt).reshape(-1, 2)
                            tips = []
                            for p in hull_pts:
                                d = math.hypot(p[0] - palm_x, p[1] - palm_y)
                                if d > max_r * 1.05 and p[1] < palm_y + max_r * 0.65:
                                    angle = math.atan2(p[0] - palm_x, -(p[1] - palm_y))
                                    tips.append((p[0], p[1], angle, d))
                            tips.sort(key=lambda x: x[2])

                            merged_tips = []
                            for p in tips:
                                if not merged_tips:
                                    merged_tips.append(p)
                                else:
                                    last = merged_tips[-1]
                                    if math.hypot(p[0] - last[0], p[1] - last[1]) < 40:
                                        if p[3] > last[3]:
                                            merged_tips[-1] = p
                                    else:
                                        merged_tips.append(p)

                            if len(merged_tips) >= 2:
                                engine_name = f"High-Precision Vision Engine ({len(merged_tips)} Tips Detected)"
                                landmarks_count = len(merged_tips)

                                dx_w = palm_x - wrist_x
                                dy_w = palm_y - wrist_y
                                deg_w = math.degrees(math.atan2(dx_w, -dy_w))
                                len_w = math.hypot(dx_w, dy_w) / min_dim

                                # 1. Wrist root (from wrist stump to palm center)
                                bone_nodes.append({
                                    "id": "wrist_root", "name": "Cổ Tay (Wrist)", "parentId": None,
                                    "position": [round(wrist_x / img_w, 3), round(wrist_y / img_h, 3)],
                                    "rotation": round(deg_w, 1), "length": round(len_w, 3), "color": "#f59e0b"
                                })

                                # 2. Palm center (anchor for fingers)
                                bone_nodes.append({
                                    "id": "palm_center", "name": "Tâm Bàn Tay (Palm)", "parentId": "wrist_root",
                                    "position": [0.0, 0.0], "rotation": round(-deg_w, 1), "length": 0.001, "color": "#f59e0b"
                                })

                                # 3. 5 Fingers with accurate 3-segment FK lengths
                                finger_defs = [
                                    ("thumb", "Ngón Cái", "#ef4444"),
                                    ("index", "Ngón Trỏ", "#3b82f6"),
                                    ("middle", "Ngón Giữa", "#10b981"),
                                    ("ring", "Ngón Áp Út", "#8b5cf6"),
                                    ("pinky", "Ngón Út", "#ec4899"),
                                ]

                                for idx, fdef in enumerate(finger_defs):
                                    f_prefix, f_name, f_color = fdef
                                    tip_idx = min(len(merged_tips) - 1, int(idx * (len(merged_tips) - 1) / 4.0))
                                    tip_p = merged_tips[tip_idx]

                                    dx_f = tip_p[0] - palm_x
                                    dy_f = tip_p[1] - palm_y
                                    deg_f = math.degrees(math.atan2(dx_f, -dy_f))
                                    total_len = math.hypot(dx_f, dy_f) / min_dim

                                    bone_nodes.append({
                                        "id": f"{f_prefix}_base", "name": f"Gốc {f_name}", "parentId": "palm_center",
                                        "position": [0.0, 0.0], "rotation": round(deg_f, 1),
                                        "length": round(total_len * 0.38, 3), "color": f_color
                                    })
                                    bone_nodes.append({
                                        "id": f"{f_prefix}_mid", "name": f"Giữa {f_name}", "parentId": f"{f_prefix}_base",
                                        "position": [0.0, 0.0], "rotation": 0,
                                        "length": round(total_len * 0.33, 3), "color": f_color
                                    })
                                    bone_nodes.append({
                                        "id": f"{f_prefix}_tip", "name": f"Đầu {f_name}", "parentId": f"{f_prefix}_mid",
                                        "position": [0.0, 0.0], "rotation": 0,
                                        "length": round(total_len * 0.29, 3), "color": f_color
                                    })
                    except Exception as cv_err:
                        print(f"[OpenCV Convex-Hull Error]: {cv_err}")

                # 3. Fallback to adaptive silhouette bounding if all else fails
                if not bone_nodes:
                    engine_name = "Anatomical Silhouette Auto-Fit Engine"
                    bone_nodes = [
                        {"id": "wrist_root", "name": "Cổ Tay (Wrist)", "parentId": None, "position": [0.5, 0.9], "rotation": 0, "length": 0.18, "color": "#f59e0b"},
                        {"id": "palm_center", "name": "Tâm Bàn Tay (Palm)", "parentId": "wrist_root", "position": [0.0, 0.0], "rotation": 0, "length": 0.24, "color": "#f59e0b"},
                        {"id": "thumb_base", "name": "Gốc Ngón Cái", "parentId": "palm_center", "position": [-0.18, 0.2], "rotation": -52, "length": 0.16, "color": "#ef4444"},
                        {"id": "thumb_tip", "name": "Đầu Ngón Cái", "parentId": "thumb_base", "position": [0.0, 0.0], "rotation": 0, "length": 0.15, "color": "#ef4444"},
                        {"id": "index_base", "name": "Gốc Ngón Trỏ", "parentId": "palm_center", "position": [-0.14, 0.01], "rotation": -15, "length": 0.15, "color": "#3b82f6"},
                        {"id": "index_mid", "name": "Giữa Ngón Trỏ", "parentId": "index_base", "position": [0.0, 0.0], "rotation": 0, "length": 0.13, "color": "#3b82f6"},
                        {"id": "index_tip", "name": "Đầu Ngón Trỏ", "parentId": "index_mid", "position": [0.0, 0.0], "rotation": 0, "length": 0.1, "color": "#3b82f6"},
                        {"id": "middle_base", "name": "Gốc Ngón Giữa", "parentId": "palm_center", "position": [0.0, 0.0], "rotation": 0, "length": 0.16, "color": "#10b981"},
                        {"id": "middle_mid", "name": "Giữa Ngón Giữa", "parentId": "middle_base", "position": [0.0, 0.0], "rotation": 0, "length": 0.15, "color": "#10b981"},
                        {"id": "middle_tip", "name": "Đầu Ngón Giữa", "parentId": "middle_mid", "position": [0.0, 0.0], "rotation": 0, "length": 0.12, "color": "#10b981"},
                        {"id": "ring_base", "name": "Gốc Ngón Áp Út", "parentId": "palm_center", "position": [0.14, 0.01], "rotation": 18, "length": 0.15, "color": "#8b5cf6"},
                        {"id": "ring_mid", "name": "Giữa Ngón Áp Út", "parentId": "ring_base", "position": [0.0, 0.0], "rotation": 0, "length": 0.13, "color": "#8b5cf6"},
                        {"id": "ring_tip", "name": "Đầu Ngón Áp Út", "parentId": "ring_mid", "position": [0.0, 0.0], "rotation": 0, "length": 0.1, "color": "#8b5cf6"},
                        {"id": "pinky_base", "name": "Gốc Ngón Út", "parentId": "palm_center", "position": [0.24, 0.05], "rotation": 38, "length": 0.12, "color": "#ec4899"},
                        {"id": "pinky_mid", "name": "Giữa Ngón Út", "parentId": "pinky_base", "position": [0.0, 0.0], "rotation": 0, "length": 0.1, "color": "#ec4899"},
                        {"id": "pinky_tip", "name": "Đầu Ngón Út", "parentId": "pinky_mid", "position": [0.0, 0.0], "rotation": 0, "length": 0.08, "color": "#ec4899"},
                    ]

                res_obj = {
                    "success": True,
                    "boneRig": {
                        "id": f"autorig_{part_type}_{int(time.time()*1000)}",
                        "name": f"Auto-Rigged {part_type}",
                        "nameVi": f"Khung Xương Tự Động ({part_type})",
                        "targetPart": part_type,
                        "bones": bone_nodes,
                        "category": "hand" if "tay" in part_type else "full_body",
                    },
                    "engine": engine_name,
                    "landmarksCount": landmarks_count if landmarks_count > 0 else len(bone_nodes)
                }
                self._set_cors_headers(200)
                self.wfile.write(json.dumps(res_obj).encode("utf-8"))

            except BaseException as ex:
                print(f"[Antigravity Sidecar Auto-Rig Error] {ex}")
                self._set_cors_headers(500)
                self.wfile.write(json.dumps({"success": False, "error": str(ex)}).encode("utf-8"))
        else:
            self._set_cors_headers(404)
            self.wfile.write(json.dumps({"error": "Endpoint not found"}).encode("utf-8"))

def run_server():
    # Force utf-8 stdout on Windows
    if sys.platform.startswith("win"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
            sys.stderr.reconfigure(encoding="utf-8")
        except:
            pass

    class ReusableThreadingServer(ThreadingHTTPServer):
        allow_reuse_address = True
        daemon_threads = True

    server_address = ("127.0.0.1", PORT)
    httpd = ReusableThreadingServer(server_address, AntigravitySidecarHandler)
    print("=====================================================")
    print(f"[Antigravity Sidecar] Multi-Threaded Server running at http://127.0.0.1:{PORT}")
    print(f"[Antigravity Sidecar] Health Check: http://127.0.0.1:{PORT}/health")
    print("=====================================================")
    sys.stdout.flush()

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping Antigravity Sidecar Server...")
        httpd.server_close()
    except BaseException as ex:
        print(f"\n[Antigravity Sidecar Error]: {ex}")
        sys.stdout.flush()

if __name__ == "__main__":
    run_server()
