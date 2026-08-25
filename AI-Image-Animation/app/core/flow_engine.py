from typing import Tuple, Optional
import numpy as np
try:
    import cv2
    HAS_CV2 = True
except ImportError:
    cv2 = None
    HAS_CV2 = False

from .vector_field import VectorFieldGenerator
from .turbulence import TurbulenceGenerator
from app.schemas.request_models import FlowAnimateRequest, SingleFramePreviewRequest


class FlowWarpEngine:
    """
    High-performance mesh & optical flow warping engine.
    Produces physics-accurate directional deformation for hair, clothing, and fluids.
    """
    @staticmethod
    def warp_frame(
        source_image: np.ndarray,
        flow_x: np.ndarray,
        flow_y: np.ndarray,
        mask: Optional[np.ndarray],
        phase: float,
        wind_strength: float = 1.0,
        wave_frequency: float = 1.5,
        turbulence: float = 0.5,
        flutter_scale: float = 1.0,
        max_displacement_px: float = 40.0
    ) -> np.ndarray:
        """
        Warps a single image frame according to the vector field and phase oscillation.
        """
        h, w = source_image.shape[:2]
        
        # Compute harmonic oscillation for this phase
        oscillation = TurbulenceGenerator.compute_wave_displacement(
            h, w, flow_x, flow_y, phase, wave_frequency, turbulence, flutter_scale
        )
        
        # Total displacement in pixels
        effective_force = oscillation * (wind_strength * max_displacement_px)
        
        # Compute pixel offset maps
        dx = flow_x * effective_force
        dy = flow_y * effective_force
        
        # Apply mask attenuation (0 = static, 1 = full motion)
        if mask is not None:
            if mask.shape[:2] != (h, w):
                if HAS_CV2:
                    mask = cv2.resize(mask, (w, h), interpolation=cv2.INTER_LINEAR)
                else:
                    from PIL import Image
                    pil_m = Image.fromarray((mask * 255).astype(np.uint8))
                    mask = np.array(pil_m.resize((w, h), Image.BILINEAR)) / 255.0
            dx *= mask
            dy *= mask
            
        # Create base coordinate meshgrid for backward mapping
        grid_x, grid_y = np.meshgrid(
            np.arange(w, dtype=np.float32),
            np.arange(h, dtype=np.float32)
        )
        
        map_x = np.clip(grid_x - dx, 0, w - 1).astype(np.float32)
        map_y = np.clip(grid_y - dy, 0, h - 1).astype(np.float32)
        
        if HAS_CV2:
            warped = cv2.remap(
                source_image,
                map_x,
                map_y,
                interpolation=cv2.INTER_LINEAR,
                borderMode=cv2.BORDER_REFLECT_101
            )
        else:
            try:
                from scipy.ndimage import map_coordinates
                channels = []
                for c in range(source_image.shape[2]):
                    ch = map_coordinates(
                        source_image[:, :, c], [map_y, map_x], order=1, mode='reflect'
                    )
                    channels.append(ch)
                warped = np.stack(channels, axis=-1).astype(np.uint8)
            except Exception:
                warped = source_image
        
        return warped

    @staticmethod
    def generate_frame_sequence(
        source_image: np.ndarray,
        mask: Optional[np.ndarray],
        req: FlowAnimateRequest
    ) -> list:
        """
        Generates a full sequence of animated video frames.
        """
        h, w = source_image.shape[:2]
        total_frames = max(2, int(req.duration_seconds * req.fps))
        
        # Pre-compute static dense vector field
        flow_x, flow_y = VectorFieldGenerator.generate_flow_field(
            h, w, req.vectors, req.pins
        )
        
        frames = []
        for frame_idx in range(total_frames):
            phase = float(frame_idx) / float(total_frames)
            
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
