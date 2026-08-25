import gc
from typing import List, Optional
import numpy as np
from PIL import Image

try:
    import torch
    HAS_TORCH = True
except ImportError:
    torch = None
    HAS_TORCH = False

from app.config import ENABLE_FP16, ENABLE_VAE_SLICING, ENABLE_CPU_OFFLOAD
from app.core.gpu_manager import gpu_manager
from app.schemas.request_models import DiffusionAnimateRequest


class AIMotionModel:
    """
    AI Diffusion-based motion synthesis pipeline.
    Optimized for NVIDIA RTX 3060 12GB VRAM using FP16, VAE slicing, and CPU offloading.
    """
    def __init__(self):
        self.pipeline = None
        self.is_loaded = False

    def load_pipeline(self):
        """
        Lazily loads diffusion pipeline with RTX 3060 memory safeguards.
        """
        if self.is_loaded:
            return
            
        try:
            from diffusers import StableVideoDiffusionPipeline
            
            dtype = torch.float16 if ENABLE_FP16 and torch.cuda.is_available() else torch.float32
            
            # Load SVD or lightweight AnimateDiff
            self.pipeline = StableVideoDiffusionPipeline.from_pretrained(
                "stabilityai/stable-video-diffusion-img2vid-xt-1-1",
                torch_dtype=dtype,
                variant="fp16" if ENABLE_FP16 else None,
                low_cpu_mem_usage=True
            )
            
            # RTX 3060 12GB VRAM optimizations
            if torch.cuda.is_available():
                if ENABLE_CPU_OFFLOAD:
                    self.pipeline.enable_model_cpu_offload()
                else:
                    self.pipeline.to("cuda")
                    
                if ENABLE_VAE_SLICING:
                    self.pipeline.enable_vae_slicing()
                    
    def unload_from_vram(self) -> bool:
        """
        Unloads model pipeline and frees all GPU VRAM.
        """
        if self.pipeline is not None:
            del self.pipeline
            self.pipeline = None
            self.is_loaded = False
            gpu_manager.clean_memory()
            return True
        gpu_manager.clean_memory()
        return False

    def get_status(self) -> dict:
        """
        Returns model status and VRAM footprint info.
        """
        gpu_stat = gpu_manager.get_status()
        return {
            "is_loaded": self.is_loaded,
            "model_name": "Stable Video Diffusion XT / Flow Motion",
            "device": str(gpu_manager.device),
            "vram_used_gb": gpu_stat.used_vram_gb,
            "vram_free_gb": gpu_stat.free_vram_gb,
            "is_rtx_3060": gpu_stat.is_rtx_3060_optimized
        }

    def generate(
        self,
        image_np: np.ndarray,
        req: DiffusionAnimateRequest
    ) -> List[np.ndarray]:
        """
        Generates animated frames using diffusion or fallback motion synthesis.
        """
        pil_img = Image.fromarray(image_np).resize((576, 576))
        
        if not self.is_loaded:
            self.load_pipeline()
            
        if self.is_loaded and self.pipeline is not None:
            try:
                with torch.inference_mode():
                    frames = self.pipeline(
                        pil_img,
                        decode_chunk_size=4,
                        motion_bucket_id=req.motion_bucket_id,
                        noise_aug_strength=req.noise_aug_strength,
                        num_frames=req.num_frames,
                        fps=req.fps
                    ).frames[0]
                    
                gpu_manager.clean_memory()
                return [np.array(f) for f in frames]
            except Exception as ex:
                gpu_manager.clean_memory()
                raise RuntimeError(f"AI Diffusion error: {ex}")
        else:
            # Fallback if offline / weights not downloaded
            raise RuntimeError(
                "AI Diffusion weights are not loaded. Use the Neural Vector Flow Engine for instant 60fps rendering without requiring heavy checkpoint downloads."
            )
