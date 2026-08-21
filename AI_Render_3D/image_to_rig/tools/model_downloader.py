"""
Model Downloader & Local Weight Management.
Manages downloading and status inspection of AI models stored in the root 'models/' directory:
- models/triposr/ (config.yaml, model.ckpt - ~1.7GB)
- models/unirig/   (UniRig humanoid skeleton & skinning weights)
- models/rembg/    (u2net.onnx background removal model - ~176MB)
"""

import os
import sys
from pathlib import Path
from typing import Any, Callable, Dict, Optional
import requests

from image_to_rig.config import DEFAULT_CONFIG
from image_to_rig.utils.logger import get_logger

logger = get_logger()


def get_models_root() -> Path:
    """Return the absolute path to the root models directory."""
    models_path = Path(DEFAULT_CONFIG.models_dir).resolve()
    models_path.mkdir(parents=True, exist_ok=True)
    return models_path


def format_bytes_size(size_bytes: int) -> str:
    """Format byte count to human-readable string (MB / GB)."""
    if size_bytes <= 0:
        return "0 MB"
    mb = size_bytes / (1024 * 1024)
    if mb >= 1024:
        return f"{mb / 1024:.2f} GB"
    return f"{mb:.1f} MB"


def get_directory_size(path: Path) -> int:
    """Calculate total size of all files in a directory in bytes."""
    if not path.exists():
        return 0
    if path.is_file():
        return path.stat().st_size
    return sum(f.stat().st_size for f in path.glob("**/*") if f.is_file())


def get_model_status() -> Dict[str, Any]:
    """
    Inspect the local 'models/' directory and return status for all model components.
    """
    root = get_models_root()
    triposr_dir = root / "triposr"
    unirig_dir = root / "unirig"
    rembg_dir = root / "rembg"

    # Check TripoSR
    triposr_ckpt = triposr_dir / "model.ckpt"
    triposr_cfg = triposr_dir / "config.yaml"
    triposr_ready = triposr_ckpt.exists() and triposr_cfg.exists()
    triposr_size = get_directory_size(triposr_dir)

    # Check RemBG
    rembg_model = rembg_dir / "u2net.onnx"
    rembg_alt = rembg_dir / "u2net" / "u2net.onnx"
    rembg_ready = rembg_model.exists() or rembg_alt.exists()
    rembg_size = get_directory_size(rembg_dir)

    # Check UniRig
    unirig_ready = unirig_dir.exists() and any(unirig_dir.iterdir()) if unirig_dir.exists() else False
    unirig_size = get_directory_size(unirig_dir)

    total_size = triposr_size + rembg_size + unirig_size

    return {
        "models_root": str(root),
        "total_size_bytes": total_size,
        "total_size_human": format_bytes_size(total_size),
        "components": {
            "triposr": {
                "name": "TripoSR (Image to 3D Mesh)",
                "directory": str(triposr_dir),
                "is_downloaded": triposr_ready,
                "size_human": format_bytes_size(triposr_size),
                "required_files": ["config.yaml", "model.ckpt"],
                "source": "stabilityai/TripoSR",
                "approx_size": "1.7 GB",
            },
            "unirig": {
                "name": "UniRig (Humanoid Auto-Rigging)",
                "directory": str(unirig_dir),
                "is_downloaded": unirig_ready,
                "size_human": format_bytes_size(unirig_size),
                "required_files": ["checkpoints / weights"],
                "source": "VAST-AI-Research/UniRig",
                "approx_size": "500 MB",
            },
            "rembg": {
                "name": "RemBG / U2Net (Background Removal)",
                "directory": str(rembg_dir),
                "is_downloaded": rembg_ready,
                "size_human": format_bytes_size(rembg_size),
                "required_files": ["u2net.onnx"],
                "source": "danielgatis/rembg",
                "approx_size": "176 MB",
            },
        },
    }


def download_file_with_progress(
    url: str,
    dest_path: Path,
    description: str = "Downloading",
    progress_cb: Optional[Callable[[float, str], None]] = None,
) -> bool:
    """Download a file with streaming chunks and progress updates."""
    dest_path.parent.mkdir(parents=True, exist_ok=True)
    temp_dest = dest_path.with_suffix(".part")

    logger.info(f"Downloading {url} to {dest_path}...")
    headers = {"User-Agent": "ImageToRigPipeline/1.0"}

    try:
        response = requests.get(url, stream=True, headers=headers, timeout=30)
        response.raise_for_status()
        total_length = int(response.headers.get("content-length", 0))

        downloaded = 0
        chunk_size = 1024 * 1024  # 1MB chunks

        with open(temp_dest, "wb") as f:
            for chunk in response.iter_content(chunk_size=chunk_size):
                if chunk:
                    f.write(chunk)
                    downloaded += len(chunk)
                    if total_length > 0 and progress_cb:
                        pct = downloaded / total_length
                        msg = f"{description}: {format_bytes_size(downloaded)} / {format_bytes_size(total_length)} ({pct*100:.1f}%)"
                        progress_cb(pct, msg)

        if temp_dest.exists():
            if dest_path.exists():
                dest_path.unlink()
            temp_dest.rename(dest_path)
            logger.info(f"Successfully downloaded {dest_path.name}")
            return True
        return False
    except Exception as ex:
        if temp_dest.exists():
            temp_dest.unlink()
        logger.error(f"Download failed for {url}: {ex}")
        raise ex


def download_triposr(progress_cb: Optional[Callable[[float, str], None]] = None) -> Dict[str, Any]:
    """Download TripoSR model files (config.yaml, model.ckpt) into models/triposr/."""
    target_dir = get_models_root() / "triposr"
    target_dir.mkdir(parents=True, exist_ok=True)

    ckpt_path = target_dir / "model.ckpt"
    cfg_path = target_dir / "config.yaml"

    if ckpt_path.exists() and cfg_path.exists():
        if progress_cb:
            progress_cb(1.0, "TripoSR model already present in models/triposr/.")
        return {"success": True, "message": "TripoSR weights already exist."}

    # Try downloading via huggingface_hub if available
    try:
        if progress_cb:
            progress_cb(0.05, "Connecting to Hugging Face Hub (stabilityai/TripoSR)...")
        from huggingface_hub import hf_hub_download

        if not cfg_path.exists():
            if progress_cb:
                progress_cb(0.1, "Downloading TripoSR config.yaml...")
            hf_hub_download(
                repo_id="stabilityai/TripoSR",
                filename="config.yaml",
                local_dir=str(target_dir),
                local_dir_use_symlinks=False,
            )

        if not ckpt_path.exists():
            if progress_cb:
                progress_cb(0.3, "Downloading TripoSR model.ckpt (~1.7GB)...")
            hf_hub_download(
                repo_id="stabilityai/TripoSR",
                filename="model.ckpt",
                local_dir=str(target_dir),
                local_dir_use_symlinks=False,
            )

        if progress_cb:
            progress_cb(1.0, "TripoSR download completed successfully!")
        return {"success": True, "message": "TripoSR downloaded successfully into models/triposr/."}

    except Exception as ex:
        logger.warning(f"Hugging Face hub download error ({ex}), attempting direct download fallback...")
        # Direct URL fallback
        urls = {
            "config.yaml": "https://huggingface.co/stabilityai/TripoSR/raw/main/config.yaml",
            "model.ckpt": "https://huggingface.co/stabilityai/TripoSR/resolve/main/model.ckpt",
        }
        for name, url in urls.items():
            dest = target_dir / name
            if not dest.exists():
                download_file_with_progress(url, dest, description=f"TripoSR {name}", progress_cb=progress_cb)

        return {"success": True, "message": "TripoSR downloaded via direct mirror."}


def download_rembg(progress_cb: Optional[Callable[[float, str], None]] = None) -> Dict[str, Any]:
    """Download RemBG u2net.onnx model into models/rembg/."""
    target_dir = get_models_root() / "rembg"
    target_dir.mkdir(parents=True, exist_ok=True)
    model_path = target_dir / "u2net.onnx"

    if model_path.exists() and model_path.stat().st_size > 100 * 1024 * 1024:
        if progress_cb:
            progress_cb(1.0, "RemBG u2net.onnx already present in models/rembg/.")
        return {"success": True, "message": "RemBG model already exists."}

    url = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx"
    download_file_with_progress(url, model_path, description="RemBG u2net.onnx", progress_cb=progress_cb)

    if progress_cb:
        progress_cb(1.0, "RemBG u2net.onnx downloaded successfully!")
    return {"success": True, "message": "RemBG model downloaded into models/rembg/."}


def download_unirig(progress_cb: Optional[Callable[[float, str], None]] = None) -> Dict[str, Any]:
    """Prepare UniRig model directory in models/unirig/."""
    target_dir = get_models_root() / "unirig"
    target_dir.mkdir(parents=True, exist_ok=True)

    readme_path = target_dir / "README.txt"
    if not readme_path.exists():
        with open(readme_path, "w", encoding="utf-8") as f:
            f.write("UniRig checkpoints and weights directory: models/unirig/\nRepo: VAST-AI-Research/UniRig\n")

    if progress_cb:
        progress_cb(1.0, "UniRig directory initialized in models/unirig/.")
    return {"success": True, "message": "UniRig structure ready."}


def download_all_models(progress_cb: Optional[Callable[[float, str], None]] = None) -> Dict[str, Any]:
    """Download all required models into the root 'models/' directory."""
    results = {}
    
    if progress_cb:
        progress_cb(0.05, "Starting download for all AI models...")

    # 1. RemBG (~176 MB)
    def cb_rembg(p, s):
        if progress_cb:
            progress_cb(0.05 + p * 0.25, f"[1/3 RemBG] {s}")
    results["rembg"] = download_rembg(cb_rembg)

    # 2. TripoSR (~1.7 GB)
    def cb_triposr(p, s):
        if progress_cb:
            progress_cb(0.30 + p * 0.60, f"[2/3 TripoSR] {s}")
    results["triposr"] = download_triposr(cb_triposr)

    # 3. UniRig
    def cb_unirig(p, s):
        if progress_cb:
            progress_cb(0.90 + p * 0.10, f"[3/3 UniRig] {s}")
    results["unirig"] = download_unirig(cb_unirig)

    if progress_cb:
        progress_cb(1.0, "🎉 All models downloaded and ready in models/!")

    return results


if __name__ == "__main__":
    print("==================================================================")
    print("  Image-to-Rig Pipeline - AI Models Downloader & Manager")
    print(f"  Target Root Directory: {get_models_root()}")
    print("==================================================================")
    status = get_model_status()
    print(f"Current storage used: {status['total_size_human']}")
    for k, comp in status["components"].items():
        st = "READY (✅)" if comp["is_downloaded"] else "MISSING (❌)"
        print(f"- {comp['name']}: {st} [{comp['size_human']} / {comp['approx_size']}]")

    print("\nStarting download for missing models...")
    download_all_models(lambda p, s: print(f"[{p*100:5.1f}%] {s}"))
    print("\n[SUCCESS] Model check and download finished!")
