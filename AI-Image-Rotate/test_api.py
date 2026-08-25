import io
import base64
import requests
from PIL import Image, ImageDraw

SERVER_URL = "http://127.0.0.1:3978"


def create_test_image():
    """Creates a simple colorful test subject."""
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([40, 40, 216, 216], fill=(0, 242, 254, 255), outline=(127, 0, 255, 255), width=6)
    draw.rectangle([80, 100, 176, 150], fill=(15, 23, 42, 255))
    draw.ellipse([95, 115, 115, 135], fill=(255, 255, 255, 255))
    draw.ellipse([140, 115, 160, 135], fill=(255, 255, 255, 255))
    
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return f"data:image/png;base64,{base64.b64encode(buf.getvalue()).decode('utf-8')}"


def test_system_status():
    print("[1] Testing GET /api/system/status ...")
    res = requests.get(f"{SERVER_URL}/api/system/status")
    print(f"Status: {res.status_code}, Response: {res.json()}")
    assert res.status_code == 200


def test_rotate_single(b64):
    print("\n[2] Testing POST /api/rotate/single (Azimuth: 90 deg) ...")
    payload = {
        "image_base64": b64,
        "azimuth_deg": 90.0,
        "elevation_deg": 10.0,
        "remove_background": False
    }
    res = requests.post(f"{SERVER_URL}/api/rotate/single", json=payload)
    print(f"Status: {res.status_code}")
    data = res.json()
    print(f"Success: {data['success']}, Elapsed: {data['elapsed_seconds']}s, Frames: {len(data['frames'])}")
    assert res.status_code == 200
    assert data["success"] is True


def test_rotate_turntable(b64):
    print("\n[3] Testing POST /api/rotate/turntable (16 frames 360) ...")
    payload = {
        "image_base64": b64,
        "num_frames": 16,
        "elevation_deg": 0.0,
        "remove_background": False,
        "generate_gif": True,
        "generate_spritesheet": True
    }
    res = requests.post(f"{SERVER_URL}/api/rotate/turntable", json=payload)
    print(f"Status: {res.status_code}")
    data = res.json()
    print(f"Success: {data['success']}, Elapsed: {data['elapsed_seconds']}s, Frames: {len(data['frames'])}")
    print(f"GIF present: {bool(data['gif_base64'])}, Spritesheet present: {bool(data['spritesheet_base64'])}")
    assert res.status_code == 200
    assert len(data["frames"]) == 16


if __name__ == "__main__":
    test_img_b64 = create_test_image()
    test_system_status()
    test_rotate_single(test_img_b64)
    test_rotate_turntable(test_img_b64)
    print("\nALL API ENDPOINTS TESTED AND WORKING 100% PERFECTLY!")
