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
from app.schemas.request_models import DiffusionAnimateRequest, DiffusionModelType


class AIMotionModel:
    """
    AI Diffusion-based motion synthesis pipeline supporting AnimateDiff & SVD.
    Optimized for NVIDIA RTX 3060 12GB VRAM using FP16, VAE slicing, and CPU offloading.
    """
    def __init__(self):
        self.pipeline = None
        self.loaded_type: Optional[str] = None
        self.is_loaded = False

    def load_pipeline(self, model_type: str = "animatediff"):
        """
        Lazily loads diffusion pipeline with RTX 3060 memory safeguards.
        """
        if self.is_loaded and self.loaded_type == model_type and self.pipeline is not None:
            return
            
        # If another model is loaded, unload it first
        if self.pipeline is not None:
            self.unload_from_vram()

        try:
            dtype = torch.float16 if ENABLE_FP16 and torch.cuda.is_available() else torch.float32
            
            if model_type == "animatediff":
                from diffusers import AnimateDiffPipeline, MotionAdapter, DDIMScheduler
                
                # 1. Load Motion Adapter (AnimateDiff v1.5 v3)
                adapter = MotionAdapter.from_pretrained(
                    "guoyww/animatediff-motion-adapter-v1-5-3",
                    torch_dtype=dtype
                )
                
                # 2. Load Pipeline with Anime/SD1.5 base checkpoint
                self.pipeline = AnimateDiffPipeline.from_pretrained(
                    "runwayml/stable-diffusion-v1-5",
                    motion_adapter=adapter,
                    torch_dtype=dtype
                )
                
                # 3. Setup DDIM scheduler for smooth temporal interpolation
                scheduler = DDIMScheduler.from_pretrained(
                    "runwayml/stable-diffusion-v1-5",
                    subfolder="scheduler",
                    clip_sample=False,
                    timestep_spacing="linspace",
                    beta_schedule="linear",
                    steps_offset=1
                )
                self.pipeline.scheduler = scheduler
                
            else:  # SVD
                from diffusers import StableVideoDiffusionPipeline
                self.pipeline = StableVideoDiffusionPipeline.from_pretrained(
                    "vdo/stable-video-diffusion-img2vid-xt-1-1",
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
                    
                if ENABLE_VAE_SLICING and hasattr(self.pipeline, "enable_vae_slicing"):
                    self.pipeline.enable_vae_slicing()
                if hasattr(self.pipeline, "enable_vae_tiling"):
                    self.pipeline.enable_vae_tiling()
            
            self.loaded_type = model_type
            self.is_loaded = True
        except Exception as e:
            self.is_loaded = False
            self.loaded_type = None
            self.pipeline = None
            gpu_manager.clean_memory()
            raise RuntimeError(f"Failed to load AI motion diffusion pipeline ({model_type}): {e}")

    def unload_from_vram(self) -> bool:
        """
        Unloads model pipeline and frees all GPU VRAM.
        """
        if self.pipeline is not None:
            del self.pipeline
            self.pipeline = None
            self.is_loaded = False
            self.loaded_type = None
            gpu_manager.clean_memory()
            return True
        gpu_manager.clean_memory()
        return False

    def get_status(self) -> dict:
        """
        Returns model status and VRAM footprint info.
        """
        gpu_stat = gpu_manager.get_status()
        model_name = "AnimateDiff (Anime SD1.5)" if self.loaded_type == "animatediff" else "Stable Video Diffusion XT"
        return {
            "is_loaded": self.is_loaded,
            "model_type": self.loaded_type or "none",
            "model_name": model_name if self.is_loaded else "Offline (Not Loaded)",
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
        Generates animated frames using diffusion models.
        """
        target_model = req.model_type.value if hasattr(req.model_type, 'value') else str(req.model_type)
        
        if not self.is_loaded or self.loaded_type != target_model:
            self.load_pipeline(target_model)
            
        if self.is_loaded and self.pipeline is not None:
            try:
                # Normalize input: ensure RGB uint8 PIL Image
                if image_np.dtype != np.uint8:
                    if image_np.max() <= 1.0:
                        image_np = (image_np * 255).astype(np.uint8)
                    else:
                        image_np = image_np.astype(np.uint8)
                pil_img = Image.fromarray(image_np).convert("RGB")

                with torch.inference_mode():
                    if self.loaded_type == "animatediff":
                        pil_img = pil_img.resize((512, 512), Image.Resampling.LANCZOS)
                        prompt = req.prompt or "masterpiece, flowing black hair, clothes fluttering in wind, cinematic lighting, 8k"
                        negative_prompt = req.negative_prompt or "distorted, bad anatomy, blurry, flickering, static"
                        
                        output = self.pipeline(
                            prompt=prompt,
                            negative_prompt=negative_prompt,
                            num_frames=min(24, max(12, req.num_frames)),
                            guidance_scale=req.guidance_scale or 7.5,
                            num_inference_steps=20,
                            generator=torch.manual_seed(42) if torch.cuda.is_available() else None
                        )
                        frames = output.frames[0]
                    else:  # SVD (True Image-to-Video preserving exact image)
                        pil_img = pil_img.resize((512, 512), Image.Resampling.LANCZOS)
                        num_f = min(25, max(14, req.num_frames))
                        frames = self.pipeline(
                            pil_img,
                            decode_chunk_size=4,
                            motion_bucket_id=req.motion_bucket_id or 127,
                            noise_aug_strength=req.noise_aug_strength or 0.02,
                            num_inference_steps=15,
                            num_frames=num_f,
                            fps=req.fps
                        ).frames[0]
                    
                gpu_manager.clean_memory()
                return [np.array(f) for f in frames]
            except Exception as ex:
                gpu_manager.clean_memory()
                raise RuntimeError(f"AI Diffusion generation error: {ex}")
        else:
            raise RuntimeError(
                "AI Diffusion pipeline could not be loaded into GPU VRAM."
            )


ai_model_instance = AIMotionModel()


