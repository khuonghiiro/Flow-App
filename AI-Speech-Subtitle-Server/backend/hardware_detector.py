import os
import platform
import multiprocessing

def get_system_hardware_info():
    """
    Tự động phát hiện thông số phần cứng: GPU, VRAM, CPU, RAM
    và đưa ra khuyến nghị lựa chọn Model + số luồng chạy song song tối ưu.
    """
    cpu_cores = multiprocessing.cpu_count()
    ram_gb = 16.0

    try:
        import psutil
        cpu_cores = psutil.cpu_count(logical=False) or cpu_cores
        cpu_threads = psutil.cpu_count(logical=True) or cpu_cores
        ram_gb = round(psutil.virtual_memory().total / (1024 ** 3), 1)
    except Exception:
        cpu_threads = cpu_cores
        try:
            import ctypes
            class MEMORYSTATUSEX(ctypes.Structure):
                _fields_ = [
                    ("dwLength", ctypes.c_ulong),
                    ("dwMemoryLoad", ctypes.c_ulong),
                    ("ullTotalPhys", ctypes.c_ulonglong),
                    ("ullAvailPhys", ctypes.c_ulonglong),
                    ("ullTotalPageFile", ctypes.c_ulonglong),
                    ("ullAvailPageFile", ctypes.c_ulonglong),
                    ("ullTotalVirtual", ctypes.c_ulonglong),
                    ("ullAvailVirtual", ctypes.c_ulonglong),
                    ("sullAvailExtendedVirtual", ctypes.c_ulonglong),
                ]
            stat = MEMORYSTATUSEX()
            stat.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
            ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(stat))
            ram_gb = round(stat.ullTotalPhys / (1024 ** 3), 1)
        except Exception:
            pass

    info = {
        "os": f"{platform.system()} {platform.release()}",
        "cpu_name": platform.processor() or "Unknown CPU",
        "cpu_cores": cpu_cores,
        "cpu_threads": cpu_threads,
        "ram_gb": ram_gb,
        "has_gpu": False,
        "gpu_name": "Không tìm thấy GPU rời (Dùng CPU Mode)",
        "vram_gb": 0.0,
        "cuda_available": False,
        "recommended_engine": "faster-whisper",
        "recommended_model_size": "small",
        "recommended_compute_type": "int8",
        "recommended_device": "cpu",
        "recommended_concurrency": 2,
        "advice_message": ""
    }

    # Thử kiểm tra CUDA qua PyTorch nếu đã cài
    try:
        import torch
        if torch.cuda.is_available():
            info["has_gpu"] = True
            info["cuda_available"] = True
            info["gpu_name"] = torch.cuda.get_device_name(0)
            total_vram_bytes = torch.cuda.get_device_properties(0).total_memory
            info["vram_gb"] = round(total_vram_bytes / (1024 ** 3), 1)
            info["recommended_device"] = "cuda"
    except Exception:
        pass

    # Nếu PyTorch chưa nạp nhưng có nvidia-smi trên Windows
    if not info["has_gpu"]:
        try:
            import subprocess
            out = subprocess.check_output(
                ["nvidia-smi", "--query-gpu=name,memory.total", "--format=csv,noheader,nounits"],
                encoding="utf-8"
            )
            lines = out.strip().split("\n")
            if lines and len(lines[0].split(",")) >= 2:
                name, mem = lines[0].split(",")
                info["has_gpu"] = True
                info["cuda_available"] = True
                info["gpu_name"] = name.strip()
                info["vram_gb"] = round(float(mem.strip()) / 1024.0, 1)
                info["recommended_device"] = "cuda"
        except Exception:
            pass

    # Phân tích và đưa ra khuyến nghị thông minh
    vram = info["vram_gb"]
    ram = info["ram_gb"]
    cores = info["cpu_threads"]

    if info["has_gpu"] and vram >= 15.0:
        info["recommended_engine"] = "seamless-m4t"
        info["recommended_model_size"] = "large-v3"
        info["recommended_compute_type"] = "float16"
        info["recommended_concurrency"] = 6
        info["advice_message"] = (
            f"🚀 Cấu hình GPU rất mạnh ({info['gpu_name']} - {vram}GB VRAM): "
            "Bạn có thể chạy mượt mà bất kỳ Model nào (kể cả Whisper Large-v3 hoặc SeamlessM4T v2). "
            "Khuyên dùng 6 - 8 luồng audio song song trong Loop Node để đạt tốc độ cao nhất!"
        )
    elif info["has_gpu"] and vram >= 10.0:
        info["recommended_engine"] = "whisperx"
        info["recommended_model_size"] = "large-v3-turbo"
        info["recommended_compute_type"] = "float16"
        info["recommended_concurrency"] = 4
        info["advice_message"] = (
            f"✨ Cấu hình GPU cao cấp ({info['gpu_name']} - {vram}GB VRAM): "
            "Khuyên dùng Dự án 2 (WhisperX Alignment) hoặc Faster-Whisper Large-v3-turbo. "
            "Chạy song song tối ưu: 4 - 6 luồng audio cùng lúc."
        )
    elif info["has_gpu"] and vram >= 5.5:
        info["recommended_engine"] = "faster-whisper"
        info["recommended_model_size"] = "medium"
        info["recommended_compute_type"] = "float16"
        info["recommended_concurrency"] = 3
        info["advice_message"] = (
            f"⚡ Cấu hình GPU tầm trung ({info['gpu_name']} - {vram}GB VRAM): "
            "Khuyên dùng Dự án 1 (Faster-Whisper model 'medium' hoặc 'small') kết hợp NLLB-200. "
            "Chạy song song tối ưu: 3 - 4 luồng audio cùng lúc để tránh tràn VRAM."
        )
    elif info["has_gpu"] and vram >= 3.5:
        info["recommended_engine"] = "faster-whisper"
        info["recommended_model_size"] = "small"
        info["recommended_compute_type"] = "int8_float16"
        info["recommended_concurrency"] = 2
        info["advice_message"] = (
            f"🎯 Cấu hình GPU phổ thông ({info['gpu_name']} - {vram}GB VRAM): "
            "Khuyên dùng Faster-Whisper model 'small' với Compute Type 'int8_float16'. "
            "Chạy song song tối ưu: 2 luồng audio cùng lúc."
        )
    else:
        # Máy CPU / Không có GPU rời hoặc VRAM < 4GB
        info["recommended_engine"] = "sensevoice"
        info["recommended_model_size"] = "base"
        info["recommended_compute_type"] = "int8"
        info["recommended_device"] = "cpu"
        info["recommended_concurrency"] = max(1, min(cores // 2, 2))
        info["advice_message"] = (
            f"💡 Chế độ CPU Mode ({info['cpu_threads']} Threads, {ram}GB RAM): "
            "Khuyên dùng Dự án 3 (SenseVoice Ultra-Fast) hoặc Faster-Whisper model 'base' / 'small' (int8). "
            "Chạy song song tối ưu: 1 - 2 luồng audio để CPU không bị quá tải."
        )

    return info

if __name__ == "__main__":
    import json
    import sys
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass
    print(json.dumps(get_system_hardware_info(), indent=2, ensure_ascii=False))
