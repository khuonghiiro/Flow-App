from abc import ABC, abstractmethod
from typing import Dict, Any, List

class BaseSpeechEngine(ABC):
    def __init__(self, model_size: str = "small", device: str = "auto", compute_type: str = "default", download_root: str = None):
        self.model_size = model_size
        self.device = device
        self.compute_type = compute_type
        self.download_root = download_root
        self.model = None

    @abstractmethod
    def load_model(self, progress_callback=None):
        """Khởi tạo và nạp model vào VRAM / RAM kèm callback báo tiến trình (%)"""
        pass

    @abstractmethod
    def transcribe(
        self,
        audio_path: str,
        target_lang: str = "vi",
        chunk_index: int = 0,
        task: str = "transcribe_and_translate",
        vad_filter: bool = True
    ) -> Dict[str, Any]:
        """
        Chuyển đổi âm thanh thành phụ đề kèm mốc thời gian (start, end, text).
        Trả về định dạng JSON chuẩn FlowMy:
        {
            "chunkIndex": chunk_index,
            "language": "en",
            "segments": [
                { "start": 0.0, "end": 3.5, "text": "...", "translated_text": "..." }
            ]
        }
        """
        pass
