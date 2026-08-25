import base64
import io
import re
from typing import Tuple, Optional
import numpy as np
from PIL import Image

try:
    import cv2
    HAS_CV2 = True
except ImportError:
    cv2 = None
    HAS_CV2 = False


def decode_base64_image(data_str: str) -> np.ndarray:
    """
    Decodes a Base64 string or Data URI into an RGB/RGBA numpy array.
    """
    if "," in data_str:
        # Strip header like data:image/png;base64,
        data_str = re.sub(r"^data:image\/[a-zA-Z+]+;base64,", "", data_str)
    
    image_bytes = base64.b64decode(data_str)
    pil_image = Image.open(io.BytesIO(image_bytes))
    
    # Handle EXIF orientation if present
    try:
        from PIL import ImageOps
        pil_image = ImageOps.exif_transpose(pil_image)
    except Exception:
        pass
    
    if pil_image.mode not in ["RGB", "RGBA"]:
        pil_image = pil_image.convert("RGBA" if "A" in pil_image.getbands() else "RGB")
    
    return np.array(pil_image)


def encode_image_base64(image_np: np.ndarray, format: str = "PNG") -> str:
    """
    Encodes an RGB/RGBA numpy array into a Base64 Data URI string.
    """
    pil_img = Image.fromarray(image_np.astype(np.uint8))
    buffer = io.BytesIO()
    pil_img.save(buffer, format=format)
    encoded = base64.b64encode(buffer.getvalue()).decode("utf-8")
    mime = "image/png" if format.upper() == "PNG" else "image/jpeg"
    return f"data:{mime};base64,{encoded}"


def decode_base64_mask(mask_str: Optional[str], target_shape: Tuple[int, int]) -> np.ndarray:
    """
    Decodes a Base64 mask into a normalized float32 2D array [0.0, 1.0].
    If mask_str is None or empty, returns an all-ones array (animate entire image).
    """
    target_h, target_w = target_shape[:2]
    
    if not mask_str:
        return np.ones((target_h, target_w), dtype=np.float32)
    
    try:
        mask_np = decode_base64_image(mask_str)
        if mask_np.ndim == 3:
            if mask_np.shape[2] == 4:
                # Use alpha channel if informative, else luminance
                alpha = mask_np[:, :, 3]
                if np.max(alpha) > np.min(alpha):
                    mask_gray = alpha
                else:
                    mask_gray = cv2.cvtColor(mask_np[:, :, :3], cv2.COLOR_RGB2GRAY)
            else:
                if HAS_CV2:
                    mask_gray = cv2.cvtColor(mask_np, cv2.COLOR_RGB2GRAY)
                else:
                    mask_gray = np.mean(mask_np[:, :, :3], axis=2)
        else:
            mask_gray = mask_np
        
        # Resize to match target shape
        if mask_gray.shape[:2] != (target_h, target_w):
            if HAS_CV2:
                mask_gray = cv2.resize(mask_gray, (target_w, target_h), interpolation=cv2.INTER_LINEAR)
            else:
                pil_m = Image.fromarray((mask_gray * 255).astype(np.uint8))
                mask_gray = np.array(pil_m.resize((target_w, target_h), Image.BILINEAR))
        
        # Normalize to [0.0, 1.0]
        mask_norm = mask_gray.astype(np.float32) / 255.0
        return np.clip(mask_norm, 0.0, 1.0)
    except Exception:
        # Fallback to full image
        return np.ones((target_h, target_w), dtype=np.float32)


def process_feather_mask(
    mask_np: np.ndarray,
    blur_radius: int = 15,
    threshold: float = 0.05
) -> np.ndarray:
    """
    Applies Gaussian feathering to prevent hard seams around hair & cloth boundaries.
    """
    if mask_np is None:
        return None
    
    cleaned = np.where(mask_np > threshold, mask_np, 0.0).astype(np.float32)
    
    if HAS_CV2:
        ksize = max(3, blur_radius * 2 + 1)
        feathered = cv2.GaussianBlur(cleaned, (ksize, ksize), 0)
    else:
        try:
            from scipy.ndimage import gaussian_filter
            feathered = gaussian_filter(cleaned, sigma=blur_radius / 2.0)
        except Exception:
            feathered = cleaned
    
    return np.clip(feathered, 0.0, 1.0)


def resize_maintaining_aspect(
    image_np: np.ndarray,
    max_dim: int = 1024
) -> Tuple[np.ndarray, float]:
    """
    Resizes image so that max(width, height) <= max_dim while preserving aspect ratio.
    Returns (resized_image, scale_factor).
    """
    h, w = image_np.shape[:2]
    if max(h, w) <= max_dim:
        return image_np, 1.0
    
    scale = max_dim / float(max(h, w))
    new_w = max(16, int(round(w * scale / 2.0) * 2))  # Ensure even dimensions for video codecs
    new_h = max(16, int(round(h * scale / 2.0) * 2))
    
    if HAS_CV2:
        resized = cv2.resize(image_np, (new_w, new_h), interpolation=cv2.INTER_AREA)
    else:
        pil_img = Image.fromarray(image_np)
        resized = np.array(pil_img.resize((new_w, new_h), Image.LANCZOS))
        
    return resized, scale
