import os
from typing import Dict, Any, List
from .base_engine import BaseSpeechEngine
from .nllb_translator import get_global_translator

class FasterWhisperEngine(BaseSpeechEngine):
    """
    Engine 1: Faster-Whisper (CTranslate2) + NLLB Translation
    Tốc độ cực nhanh, tốn ít VRAM, tự động nhận diện ngôn ngữ và căn chỉnh mốc thời gian.
    """
    def __init__(self, model_size: str = "small", device: str = "auto", compute_type: str = "default", download_root: str = None):
        super().__init__(model_size, device, compute_type, download_root)
        self.model = None

    def load_model(self):
        if self.model is not None:
            return

        from faster_whisper import WhisperModel
        import torch

        actual_device = self.device
        if actual_device == "auto":
            actual_device = "cuda" if torch.cuda.is_available() else "cpu"

        actual_compute = self.compute_type
        if actual_compute == "default":
            actual_compute = "float16" if actual_device == "cuda" else "int8"

        download_dir = self.download_root
        if download_dir:
            download_dir = os.path.join(download_dir, "faster-whisper")

        print(f"[FasterWhisperEngine] Nạp model '{self.model_size}' trên {actual_device.upper()} ({actual_compute})...")
        self.model = WhisperModel(
            model_size_or_path=self.model_size,
            device=actual_device,
            compute_type=actual_compute,
            download_root=download_dir
        )
        print("[FasterWhisperEngine] ✅ Nạp model thành công!")

    def transcribe(
        self,
        audio_path: str,
        target_lang: str = "vi",
        chunk_index: int = 0,
        task: str = "transcribe_and_translate",
        vad_filter: bool = True
    ) -> Dict[str, Any]:
        if not os.path.exists(audio_path):
            raise FileNotFoundError(f"Không tìm thấy file audio: {audio_path}")

        if self.model is None:
            self.load_model()

        # Transcribe & Detect Language
        segments, info = self.model.transcribe(
            audio_path,
            beam_size=5,
            word_timestamps=True,
            vad_filter=vad_filter,
            vad_parameters=dict(min_silence_duration_ms=300)
        )

        detected_lang = info.language or "en"
        raw_segments = []

        for seg in segments:
            raw_segments.append({
                "start": round(seg.start, 2),
                "end": round(seg.end, 2),
                "text": seg.text.strip()
            })

        # Dịch thuật nếu được yêu cầu (task == transcribe_and_translate hoặc target_lang khác ngôn ngữ phát hiện)
        if task in ("transcribe_and_translate", "translate") and target_lang and detected_lang.lower() != target_lang.lower():
            translator = get_global_translator(download_root=self.download_root)
            texts = [s["text"] for s in raw_segments]
            translated_texts = translator.translate_batch(texts, src_lang=detected_lang, tgt_lang=target_lang)

            for i, trans_text in enumerate(translated_texts):
                if i < len(raw_segments):
                    # Gán text hiển thị là bản dịch, giữ nguyên text gốc ở original_text
                    raw_segments[i]["original_text"] = raw_segments[i]["text"]
                    raw_segments[i]["text"] = trans_text

        return {
            "chunkIndex": chunk_index,
            "language": detected_lang,
            "language_probability": round(info.language_probability, 3),
            "duration": round(info.duration, 2),
            "segments": raw_segments
        }
