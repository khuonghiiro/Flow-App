import io
import base64
import math
import zipfile
import logging
from pathlib import Path
from typing import List, Tuple, Optional
from PIL import Image

logger = logging.getLogger(__name__)


class ImagePostprocessor:
    """Creates spritesheets, animated GIFs, and ZIP bundles from multi-angle outputs."""

    @staticmethod
    def create_gif(frames: List[Image.Image], duration_ms: int = 80) -> str:
        """Assembles a list of PIL images into a looping Base64 GIF data URL."""
        if not frames:
            return ""

        # Convert to RGB with neutral background for clean GIF rendering
        gif_frames = []
        for img in frames:
            img_rgba = img.convert("RGBA")
            bg_frame = Image.new("RGBA", img.size, (20, 24, 33, 255))
            bg_frame.paste(img_rgba, (0, 0), mask=img_rgba.split()[3])
            gif_frames.append(bg_frame.convert("RGB"))

        buffer = io.BytesIO()
        gif_frames[0].save(
            buffer,
            format="GIF",
            save_all=True,
            append_images=gif_frames[1:],
            duration=duration_ms,
            loop=0,
            optimize=True
        )
        encoded = base64.b64encode(buffer.getvalue()).decode("utf-8")
        return f"data:image/gif;base64,{encoded}"

    @staticmethod
    def create_spritesheet(frames: List[Image.Image], max_cols: int = 6) -> str:
        """Assembles frames into a unified 360-degree spritesheet image."""
        if not frames:
            return ""

        num_frames = len(frames)
        cols = min(num_frames, max_cols)
        rows = math.ceil(num_frames / cols)

        frame_w, frame_h = frames[0].size
        sheet_w = cols * frame_w
        sheet_h = rows * frame_h

        spritesheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))

        for idx, frame in enumerate(frames):
            c = idx % cols
            r = idx // cols
            frame_rgba = frame.convert("RGBA")
            spritesheet.paste(frame_rgba, (c * frame_w, r * frame_h), mask=frame_rgba.split()[3])

        buffer = io.BytesIO()
        spritesheet.save(buffer, format="PNG", optimize=True)
        encoded = base64.b64encode(buffer.getvalue()).decode("utf-8")
        return f"data:image/png;base64,{encoded}"

    @staticmethod
    def create_zip_archive(frames: List[Tuple[int, float, Image.Image]], output_zip_path: Path) -> Path:
        """Saves individual angle images and packs them into a downloadable ZIP."""
        output_zip_path.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(output_zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
            for idx, azimuth, frame_img in frames:
                filename = f"angle_{idx:02d}_{int(azimuth):03d}deg.png"
                img_buffer = io.BytesIO()
                frame_img.save(img_buffer, format="PNG")
                zf.writestr(filename, img_buffer.getvalue())
        return output_zip_path


postprocessor = ImagePostprocessor()
