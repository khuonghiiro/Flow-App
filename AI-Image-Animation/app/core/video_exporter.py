import os
from pathlib import Path
from typing import List
import numpy as np
from PIL import Image

try:
    import imageio
    HAS_IMAGEIO = True
except ImportError:
    imageio = None
    HAS_IMAGEIO = False

from app.schemas.request_models import ExportFormat


class VideoExporter:
    """
    Encodes animated frame sequences into MP4, WebM, GIF, or APNG.
    """
    @staticmethod
    def export(
        frames: List[np.ndarray],
        output_path: Path,
        format_type: ExportFormat,
        fps: int = 30
    ) -> Path:
        """
        Main entry point for encoding frames to target media format.
        """
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        if format_type == ExportFormat.MP4:
            return VideoExporter._export_mp4(frames, output_path, fps)
        elif format_type == ExportFormat.WEBM:
            return VideoExporter._export_webm(frames, output_path, fps)
        elif format_type == ExportFormat.GIF:
            return VideoExporter._export_gif(frames, output_path, fps)
        elif format_type == ExportFormat.APNG:
            return VideoExporter._export_apng(frames, output_path, fps)
        else:
            return VideoExporter._export_mp4(frames, output_path, fps)

    @staticmethod
    def _export_mp4(frames: List[np.ndarray], output_path: Path, fps: int) -> Path:
        """
        Encodes MP4 using H.264 with YUV420P pixel format for universal web browser playback.
        """
        # Ensure RGB format
        rgb_frames = [
            f[:, :, :3] if f.ndim == 3 and f.shape[2] == 4 else f
            for f in frames
        ]
        
        if not HAS_IMAGEIO:
            # Fallback to GIF or APNG if FFMPEG is not yet installed
            return VideoExporter._export_gif(frames, output_path.with_suffix(".gif"), fps)

        writer = imageio.get_writer(
            str(output_path),
            format="FFMPEG",
            mode="I",
            fps=fps,
            codec="libx264",
            pixelformat="yuv420p",
            quality=8,
            output_params=["-preset", "fast", "-crf", "20"]
        )
        for frame in rgb_frames:
            writer.append_data(frame.astype(np.uint8))
        writer.close()
        return output_path

    @staticmethod
    def _export_webm(frames: List[np.ndarray], output_path: Path, fps: int) -> Path:
        """
        Encodes WebM container using VP9 codec.
        """
        rgb_frames = [
            f[:, :, :3] if f.ndim == 3 and f.shape[2] == 4 else f
            for f in frames
        ]
        
        if not HAS_IMAGEIO:
            return VideoExporter._export_gif(frames, output_path.with_suffix(".gif"), fps)

        writer = imageio.get_writer(
            str(output_path),
            format="FFMPEG",
            mode="I",
            fps=fps,
            codec="libvpx-vp9",
            output_params=["-crf", "28", "-b:v", "0"]
        )
        for frame in rgb_frames:
            writer.append_data(frame.astype(np.uint8))
        writer.close()
        return output_path

    @staticmethod
    def _export_gif(frames: List[np.ndarray], output_path: Path, fps: int) -> Path:
        """
        Encodes high-quality looping GIF using PIL palette quantization.
        """
        pil_frames = []
        duration_ms = int(1000.0 / float(fps))
        
        for frame in frames:
            pil_img = Image.fromarray(frame.astype(np.uint8))
            # Convert to RGB if RGBA
            if pil_img.mode == "RGBA":
                bg = Image.new("RGB", pil_img.size, (255, 255, 255))
                bg.paste(pil_img, mask=pil_img.split()[3])
                pil_img = bg
            # Quantize with adaptive palette
            quantized = pil_img.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
            pil_frames.append(quantized)
            
        if pil_frames:
            pil_frames[0].save(
                str(output_path),
                save_all=True,
                append_images=pil_frames[1:],
                duration=duration_ms,
                loop=0,
                optimize=True
            )
        return output_path

    @staticmethod
    def _export_apng(frames: List[np.ndarray], output_path: Path, fps: int) -> Path:
        """
        Encodes animated PNG format.
        """
        pil_frames = [Image.fromarray(f.astype(np.uint8)) for f in frames]
        duration_ms = int(1000.0 / float(fps))
        
        if pil_frames:
            pil_frames[0].save(
                str(output_path),
                save_all=True,
                append_images=pil_frames[1:],
                duration=duration_ms,
                loop=0
            )
        return output_path
