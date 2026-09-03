from typing import Tuple, Optional, List
import numpy as np
try:
    import cv2
    HAS_CV2 = True
except ImportError:
    cv2 = None
    HAS_CV2 = False

from .vector_field import VectorFieldGenerator
from .turbulence import TurbulenceGenerator
from app.schemas.request_models import FlowAnimateRequest, LoopMode


class FlowWarpEngine:
    """
    High-performance optical flow & mesh deformation engine.
    Produces silky-smooth, photorealistic directional flow for hair, cloth, and fluid dynamics.
    """
    @staticmethod
    def _remap_image(source: np.ndarray, map_x: np.ndarray, map_y: np.ndarray) -> np.ndarray:
        if HAS_CV2:
            return cv2.remap(
                source,
                map_x,
                map_y,
                interpolation=cv2.INTER_LINEAR,
                borderMode=cv2.BORDER_REFLECT_101
            )
        else:
            try:
                from scipy.ndimage import map_coordinates
                channels = []
                for c in range(source.shape[2]):
                    ch = map_coordinates(
                        source[:, :, c], [map_y, map_x], order=1, mode='reflect'
                    )
                    channels.append(ch)
                return np.stack(channels, axis=-1).astype(np.uint8)
            except Exception:
                return source

    @staticmethod
    def warp_frame(
        source_image: np.ndarray,
        flow_x: np.ndarray,
        flow_y: np.ndarray,
        mask: Optional[np.ndarray],
        phase: float,
        wind_strength: float = 1.0,
        wave_frequency: float = 1.0,
        turbulence: float = 0.5,
        flutter_scale: float = 1.0,
        max_displacement_px: float = 20.0
    ) -> np.ndarray:
        """
        Warps a single image frame according to the vector field and harmonic oscillation.
        """
        h, w = source_image.shape[:2]
        
        # Smooth harmonic oscillation for this phase
        oscillation = TurbulenceGenerator.compute_wave_displacement(
            h, w, flow_x, flow_y, phase, wave_frequency, turbulence, flutter_scale
        )
        
        # Pixel displacement scaled by wind strength
        displacement_scale = wind_strength * max_displacement_px
        effective_force = oscillation * displacement_scale
        
        dx = flow_x * effective_force
        dy = flow_y * effective_force
        
        # Smooth cubic mask attenuation (0 = static, 1 = full motion)
        if mask is not None:
            if mask.shape[:2] != (h, w):
                if HAS_CV2:
                    mask_clean = cv2.resize(mask, (w, h), interpolation=cv2.INTER_LINEAR)
                else:
                    from PIL import Image
                    pil_m = Image.fromarray((mask * 255).astype(np.uint8))
                    mask_clean = np.array(pil_m.resize((w, h), Image.BILINEAR)) / 255.0
            else:
                mask_clean = mask
                
            soft_mask = np.clip(mask_clean, 0.0, 1.0) ** 1.2
            dx *= soft_mask
            dy *= soft_mask
            
        grid_x, grid_y = np.meshgrid(
            np.arange(w, dtype=np.float32),
            np.arange(h, dtype=np.float32)
        )
        
        map_x = np.clip(grid_x - dx, 0, w - 1).astype(np.float32)
        map_y = np.clip(grid_y - dy, 0, h - 1).astype(np.float32)
        
        return FlowWarpEngine._remap_image(source_image, map_x, map_y)

    @staticmethod
    def warp_dual_phase_advection(
        source_image: np.ndarray,
        flow_x: np.ndarray,
        flow_y: np.ndarray,
        mask: Optional[np.ndarray],
        phase: float,
        speed_px: float = 24.0
    ) -> np.ndarray:
        """
        Dual-phase cyclic forward advection (Plotagraph / Motionleap standard).
        Generates seamless, endless flowing water, clouds, and streaming hair.
        """
        h, w = source_image.shape[:2]
        
        grid_x, grid_y = np.meshgrid(
            np.arange(w, dtype=np.float32),
            np.arange(h, dtype=np.float32)
        )
        
        # Two forward advection cycles offset by 180 degrees (0.5)
        p1 = phase % 1.0
        p2 = (phase + 0.5) % 1.0
        
        # Smooth sine-based transparency crossfade window
        w1 = np.sin(np.pi * p1)
        w2 = np.sin(np.pi * p2)
        weight_sum = max(1e-6, w1 + w2)
        w1_norm = w1 / weight_sum
        w2_norm = w2 / weight_sum
        
        # Distance advected in current window
        d1 = p1 * speed_px
        d2 = p2 * speed_px
        
        fx = flow_x
        fy = flow_y
        if mask is not None:
            if mask.shape[:2] != (h, w):
                mask = cv2.resize(mask, (w, h), interpolation=cv2.INTER_LINEAR) if HAS_CV2 else mask
            soft_mask = np.clip(mask, 0.0, 1.0)
            fx = fx * soft_mask
            fy = fy * soft_mask
            
        map_x1 = np.clip(grid_x - fx * d1, 0, w - 1).astype(np.float32)
        map_y1 = np.clip(grid_y - fy * d1, 0, h - 1).astype(np.float32)
        warped1 = FlowWarpEngine._remap_image(source_image, map_x1, map_y1).astype(np.float32)
        
        map_x2 = np.clip(grid_x - fx * d2, 0, w - 1).astype(np.float32)
        map_y2 = np.clip(grid_y - fy * d2, 0, h - 1).astype(np.float32)
        warped2 = FlowWarpEngine._remap_image(source_image, map_x2, map_y2).astype(np.float32)
        
        blended = (warped1 * w1_norm + warped2 * w2_norm)
        
        if mask is not None:
            mask_3d = soft_mask[:, :, None] if soft_mask.ndim == 2 else soft_mask
            out = blended * mask_3d + source_image.astype(np.float32) * (1.0 - mask_3d)
            return np.clip(out, 0, 255).astype(np.uint8)
            
        return np.clip(blended, 0, 255).astype(np.uint8)

    @staticmethod
    def generate_frame_sequence(
        source_image: np.ndarray,
        mask: Optional[np.ndarray],
        req: FlowAnimateRequest
    ) -> List[np.ndarray]:
        """
        Generates a full sequence of animated video frames.
        """
        h, w = source_image.shape[:2]
        total_frames = max(2, int(req.duration_seconds * req.fps))
        
        # Pre-compute static dense vector field
        flow_x, flow_y = VectorFieldGenerator.generate_flow_field(
            h, w, req.vectors, req.pins
        )
        
        is_cyclic = req.loop_mode == LoopMode.CYCLIC_CROSSFADE
        frames = []
        
        for frame_idx in range(total_frames):
            phase = float(frame_idx) / float(total_frames)
            
            if is_cyclic:
                frame = FlowWarpEngine.warp_dual_phase_advection(
                    source_image=source_image,
                    flow_x=flow_x,
                    flow_y=flow_y,
                    mask=mask,
                    phase=phase,
                    speed_px=req.wind_strength * 22.0
                )
            else:
                frame = FlowWarpEngine.warp_frame(
                    source_image=source_image,
                    flow_x=flow_x,
                    flow_y=flow_y,
                    mask=mask,
                    phase=phase,
                    wind_strength=req.wind_strength,
                    wave_frequency=req.wave_frequency,
                    turbulence=req.turbulence,
                    flutter_scale=req.flutter_scale
                )
            frames.append(frame)
            
        return frames

