"""
GPU resource manager and VRAM tracking utility.
Provides safe memory cleanup and hardware diagnostic capabilities for RTX 3060 12GB.
"""

import gc
from typing import Dict, Tuple


class GPUManager:
    """Manages PyTorch CUDA hardware resources and memory tracking."""

    @staticmethod
    def get_torch():
        """Lazily load torch to keep import times fast and robust."""
        try:
            import torch
            return torch
        except ImportError:
            return None

    @classmethod
    def is_cuda_available(cls) -> bool:
        """Check if CUDA-capable GPU is available."""
        torch = cls.get_torch()
        return bool(torch and torch.cuda.is_available())

    @classmethod
    def get_device_name(cls) -> str:
        """Return the device model name or fallback description."""
        torch = cls.get_torch()
        if torch and torch.cuda.is_available():
            return torch.cuda.get_device_name(0)
        return "CPU (No CUDA Detected)"

    @classmethod
    def get_vram_status_mb(cls) -> Dict[str, float]:
        """Return current VRAM usage statistics in megabytes (MB)."""
        torch = cls.get_torch()
        if not torch or not torch.cuda.is_available():
            return {"allocated_mb": 0.0, "reserved_mb": 0.0, "total_mb": 0.0, "free_mb": 0.0}

        allocated = torch.cuda.memory_allocated(0) / (1024 * 1024)
        reserved = torch.cuda.memory_reserved(0) / (1024 * 1024)
        total = torch.cuda.get_device_properties(0).total_memory / (1024 * 1024)
        free = total - reserved

        return {
            "allocated_mb": round(allocated, 2),
            "reserved_mb": round(reserved, 2),
            "total_mb": round(total, 2),
            "free_mb": round(free, 2),
        }

    @classmethod
    def cleanup_memory(cls) -> Tuple[float, float]:
        """
        Trigger garbage collection and empty CUDA cache.
        Returns:
            Tuple of (vram_freed_mb, current_allocated_mb)
        """
        torch = cls.get_torch()
        gc.collect()
        if torch and torch.cuda.is_available():
            before_alloc = torch.cuda.memory_allocated(0) / (1024 * 1024)
            torch.cuda.empty_cache()
            torch.cuda.ipc_collect()
            after_alloc = torch.cuda.memory_allocated(0) / (1024 * 1024)
            freed = max(0.0, before_alloc - after_alloc)
            return round(freed, 2), round(after_alloc, 2)
        return 0.0, 0.0

    @classmethod
    def get_optimal_device_str(cls) -> str:
        """Determine device string ('cuda:0' or 'cpu')."""
        return "cuda:0" if cls.is_cuda_available() else "cpu"
