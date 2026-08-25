from typing import List
import numpy as np
from app.schemas.request_models import LoopMode


class SeamlessLooper:
    """
    Ensures video sequences loop seamlessly without visual jumps or stutter.
    """
    @staticmethod
    def apply_loop_mode(frames: List[np.ndarray], mode: LoopMode) -> List[np.ndarray]:
        """
        Processes a sequence of frames to match the requested seamless loop mode.
        """
        if not frames or len(frames) < 2:
            return frames
            
        if mode == LoopMode.PING_PONG:
            return SeamlessLooper._create_ping_pong_loop(frames)
        elif mode == LoopMode.CYCLIC_CROSSFADE:
            return SeamlessLooper._create_cyclic_crossfade_loop(frames)
        else:
            # SEAMLESS_PHASE is inherently harmonic
            return frames

    @staticmethod
    def _create_ping_pong_loop(frames: List[np.ndarray]) -> List[np.ndarray]:
        """
        Creates smooth ping-pong sequence: forward -> smooth reversal.
        """
        forward = list(frames)
        # Reverse excluding first and last frames to avoid duplicate pauses
        backward = list(reversed(frames[1:-1]))
        return forward + backward

    @staticmethod
    def _create_cyclic_crossfade_loop(
        frames: List[np.ndarray],
        blend_ratio: float = 0.2
    ) -> List[np.ndarray]:
        """
        Blends tail frames into head frames using a smooth cosine transition.
        """
        n = len(frames)
        blend_len = max(2, int(n * blend_ratio))
        
        if blend_len * 2 >= n:
            return frames
            
        result = []
        # Non-overlapping core
        for i in range(blend_len, n - blend_len):
            result.append(frames[i])
            
        # Overlapping seam blend
        for i in range(blend_len):
            alpha = 0.5 * (1.0 - np.cos(np.pi * float(i) / float(blend_len)))
            f_tail = frames[n - blend_len + i].astype(np.float32)
            f_head = frames[i].astype(np.float32)
            
            blended = (1.0 - alpha) * f_tail + alpha * f_head
            result.append(np.clip(blended, 0, 255).astype(np.uint8))
            
        return result
