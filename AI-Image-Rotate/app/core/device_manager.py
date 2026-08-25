import gc
import logging
from typing import Dict, Any, Optional

logger = logging.getLogger(__name__)

# Try to import torch safely
try:
    import torch
    TORCH_AVAILABLE = True
except ImportError:
    TORCH_AVAILABLE = False
    torch = None


class DeviceManager:
    """Manages PyTorch CUDA device and VRAM memory optimization for RTX 3060 12GB."""

    def __init__(self):
        self._device_str = "cuda" if (TORCH_AVAILABLE and torch.cuda.is_available()) else "cpu"
        self._half_precision = True if self._device_str == "cuda" else False
        logger.info(f"[DeviceManager] Active device: {self._device_str} (FP16: {self._half_precision})")

    @property
    def device_str(self) -> str:
        return self._device_str

    @property
    def torch_device(self):
        if not TORCH_AVAILABLE:
            return "cpu"
        return torch.device(self._device_str)

    @property
    def torch_dtype(self):
        if not TORCH_AVAILABLE:
            return None
        return torch.float16 if (self._device_str == "cuda" and self._half_precision) else torch.float32

    def get_gpu_info(self) -> Dict[str, Any]:
        """Returns VRAM usage and device details."""
        if not TORCH_AVAILABLE or not torch.cuda.is_available():
            return {
                "device": "cpu",
                "gpu_name": None,
                "vram_total_gb": 0.0,
                "vram_used_gb": 0.0,
                "vram_free_gb": 0.0,
                "cuda_available": False,
            }

        total = torch.cuda.get_device_properties(0).total_memory / (1024 ** 3)
        reserved = torch.cuda.memory_reserved(0) / (1024 ** 3)
        allocated = torch.cuda.memory_allocated(0) / (1024 ** 3)
        free = total - reserved

        return {
            "device": "cuda",
            "gpu_name": torch.cuda.get_device_name(0),
            "vram_total_gb": round(total, 2),
            "vram_used_gb": round(reserved, 2),
            "vram_allocated_gb": round(allocated, 2),
            "vram_free_gb": round(free, 2),
            "cuda_available": True,
        }

    def clean_vram(self):
        """Cleans cached VRAM tensors and runs garbage collection."""
        gc.collect()
        if TORCH_AVAILABLE and torch.cuda.is_available():
            torch.cuda.empty_cache()
            torch.cuda.ipc_collect()


device_manager = DeviceManager()
