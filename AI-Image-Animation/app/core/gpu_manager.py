import gc
try:
    import torch
    HAS_TORCH = True
except ImportError:
    torch = None
    HAS_TORCH = False

from app.config import TARGET_GPU_VRAM_GB, ENABLE_FP16
from app.schemas.response_models import GPUStatusResponse


class GPUManager:
    """
    Manages GPU resources and optimizations specifically for NVIDIA RTX 3060 12GB VRAM.
    """
    def __init__(self):
        self.is_cuda = HAS_TORCH and torch.cuda.is_available()
        self.device = torch.device("cuda" if self.is_cuda else "cpu") if HAS_TORCH else "cpu"
        self.device_name = torch.cuda.get_device_name(0) if self.is_cuda else "CPU Execution"
        self._init_optimizations()

    def _init_optimizations(self):
        """Sets PyTorch CUDA optimization flags."""
        if self.is_cuda:
            torch.backends.cudnn.benchmark = True
            try:
                # Enable TF32 for Ampere architecture (RTX 30 series)
                torch.backends.cuda.matmul.allow_tf32 = True
                torch.backends.cudnn.allow_tf32 = True
            except Exception:
                pass

    def get_status(self) -> GPUStatusResponse:
        """Returns current VRAM usage and hardware state."""
        if not self.is_cuda:
            return GPUStatusResponse(
                device_name="CPU Execution",
                is_cuda_available=False,
                total_vram_gb=0.0,
                used_vram_gb=0.0,
                free_vram_gb=0.0,
                is_rtx_3060_optimized=False,
                recommended_engine="Neural Vector Flow (Instant CPU/WebGL)"
            )
        
        total_bytes = torch.cuda.get_device_properties(0).total_memory
        allocated_bytes = torch.cuda.memory_allocated(0)
        reserved_bytes = torch.cuda.memory_reserved(0)
        
        total_gb = round(total_bytes / (1024 ** 3), 2)
        used_gb = round(reserved_bytes / (1024 ** 3), 2)
        free_gb = round((total_bytes - reserved_bytes) / (1024 ** 3), 2)
        
        is_rtx_3060 = "3060" in self.device_name or (11.0 <= total_gb <= 13.0)
        
        return GPUStatusResponse(
            device_name=self.device_name,
            is_cuda_available=True,
            total_vram_gb=total_gb,
            used_vram_gb=used_gb,
            free_vram_gb=free_gb,
            is_rtx_3060_optimized=is_rtx_3060,
            recommended_engine="Dual Engine (Neural Flow + RTX 3060 FP16 Diffusion)"
        )

    def clean_memory(self):
        """Forces Python garbage collection and releases cached CUDA memory."""
        gc.collect()
        if self.is_cuda:
            torch.cuda.empty_cache()
            torch.cuda.ipc_collect()

    @property
    def dtype(self):
        """Returns standard inference dtype."""
        return torch.float16 if (self.is_cuda and ENABLE_FP16) else torch.float32


gpu_manager = GPUManager()
