import io
import base64
import logging
from typing import Tuple, Optional
from PIL import Image, ImageOps
import numpy as np

logger = logging.getLogger(__name__)

# Rembg optional import
try:
    import rembg
    REMBG_AVAILABLE = True
except ImportError:
    REMBG_AVAILABLE = False
    logger.warning("[Preprocessor] rembg not installed. Falling back to native alpha processing.")


class ImagePreprocessor:
    """Handles image loading, background removal, centering, and canvas formatting."""

    @staticmethod
    def base64_to_image(b64_string: str) -> Image.Image:
        """Converts a Base64 string to a PIL Image (RGBA)."""
        if "," in b64_string:
            b64_string = b64_string.split(",", 1)[1]
        image_data = base64.b64decode(b64_string)
        image = Image.open(io.BytesIO(image_data))
        return ImageOps.exif_transpose(image).convert("RGBA")

    @staticmethod
    def image_to_base64(image: Image.Image, format: str = "PNG") -> str:
        """Encodes a PIL Image to a Base64 data URL."""
        buffer = io.BytesIO()
        image.save(buffer, format=format, optimize=True)
        encoded = base64.b64encode(buffer.getvalue()).decode("utf-8")
        return f"data:image/{format.lower()};base64,{encoded}"

    def remove_background(self, image: Image.Image) -> Image.Image:
        """Removes background from image using rembg or returns RGBA with alpha mask."""
        if REMBG_AVAILABLE:
            try:
                session = rembg.new_session("u2net")
                return rembg.remove(image, session=session)
            except Exception as e:
                logger.error(f"[Preprocessor] rembg execution failed: {e}")
        
        # Fallback: create alpha mask if image has plain white/black background
        return self._simple_alpha_fallback(image)

    def _simple_alpha_fallback(self, image: Image.Image) -> Image.Image:
        """Simple color-key fallback when rembg is not ready."""
        rgba = image.convert("RGBA")
        data = np.array(rgba)
        # If corner pixels are near pure white, make white background transparent
        corners = [data[0, 0], data[0, -1], data[-1, 0], data[-1, -1]]
        avg_corner = np.mean(corners, axis=0)
        if np.all(avg_corner[:3] > 240):
            mask = np.all(data[:, :, :3] > 235, axis=2)
            data[:, :, 3] = np.where(mask, 0, 255)
            return Image.fromarray(data, mode="RGBA")
        return rgba

    def center_and_pad(self, image: Image.Image, target_size: int = 512, padding_ratio: float = 0.85) -> Image.Image:
        """Centers the bounding box subject and places onto a square transparent canvas."""
        image = image.convert("RGBA")
        bbox = image.getbbox()
        if not bbox:
            return image.resize((target_size, target_size), Image.Resampling.LANCZOS)

        cropped = image.crop(bbox)
        w, h = cropped.size
        max_side = max(w, h)
        scale = (target_size * padding_ratio) / max_side

        new_w = max(1, int(w * scale))
        new_h = max(1, int(h * scale))
        resized = cropped.resize((new_w, new_h), Image.Resampling.LANCZOS)

        # Create target canvas
        canvas = Image.new("RGBA", (target_size, target_size), (0, 0, 0, 0))
        offset_x = (target_size - new_w) // 2
        offset_y = (target_size - new_h) // 2
        canvas.paste(resized, (offset_x, offset_y), resized)
        return canvas

    def preprocess(self, image_input: Image.Image, remove_bg: bool = True, target_size: int = 512) -> Image.Image:
        """Full pipeline: clean, remove background, center and pad."""
        img = image_input
        if remove_bg:
            img = self.remove_background(img)
        return self.center_and_pad(img, target_size=target_size)


preprocessor = ImagePreprocessor()
