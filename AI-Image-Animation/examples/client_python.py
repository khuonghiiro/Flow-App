"""
AI Image Animation - Python Client Example (Port 3979)
"""

import base64
import time
import requests

SERVER_BASE = "http://localhost:3979"


def image_file_to_base64(file_path: str) -> str:
    with open(file_path, "rb") as f:
        encoded = base64.b64encode(f.read()).decode("utf-8")
        return f"data:image/png;base64,{encoded}"


def animate_image(image_path: str, output_video_path: str):
    # 1. Check GPU status
    health = requests.get(f"{SERVER_BASE}/api/health").json()
    print(f"Server GPU: {health['device_name']} (Free VRAM: {health['free_vram_gb']} GB)")

    # 2. Prepare payload
    image_b64 = image_file_to_base64(image_path)
    payload = {
        "image": image_b64,
        "vectors": [
            {"start_x": 0.35, "start_y": 0.3, "end_x": 0.65, "end_y": 0.28, "strength": 1.2}
        ],
        "pins": [
            {"x": 0.5, "y": 0.45, "radius": 0.12, "weight": 1.0}
        ],
        "wind_strength": 1.2,
        "wave_frequency": 1.8,
        "turbulence": 0.6,
        "duration_seconds": 3.0,
        "fps": 30,
        "format": "mp4",
        "loop_mode": "seamless_phase"
    }

    # 3. Submit task
    print("Submitting animation task...")
    res = requests.post(f"{SERVER_BASE}/api/animate/flow", json=payload)
    res.raise_for_status()
    task_id = res.json()["task_id"]
    print(f"Task ID: {task_id}")

    # 4. Poll until completed
    while True:
        task = requests.get(f"{SERVER_BASE}/api/tasks/{task_id}").json()
        print(f"Progress: {int(task['progress'] * 100)}% - {task['message']}")

        if task["status"] == "completed":
            video_url = f"{SERVER_BASE}{task['result_url']}"
            print(f"Download URL: {video_url}")
            
            # Download file
            video_data = requests.get(video_url).content
            with open(output_video_path, "wb") as out_f:
                out_f.write(video_data)
            print(f"Saved animation to {output_video_path}")
            break
        elif task["status"] == "failed":
            print(f"Task failed: {task.get('error')}")
            break
        time.sleep(0.5)


if __name__ == "__main__":
    print("AI Image Animation Python Client Ready.")
