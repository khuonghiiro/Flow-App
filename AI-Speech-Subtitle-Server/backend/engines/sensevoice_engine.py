import os
from typing import Dict, Any, List
from .base_engine import BaseSpeechEngine
from .nllb_translator import get_global_translator

class SenseVoiceEngine(BaseSpeechEngine):
    """
    Engine 3: SenseVoice-Small (FunASR / Alibaba)
    Siêu nhẹ, siêu tốc độ (nhanh hơn Whisper 10x), nhận diện đa ngôn ngữ tức thì, chạy mượt trên CPU.
    """
    def __init__(self, model_size: str = "base", device: str = "auto", compute_type: str = "default", download_root: str = None):
        super().__init__(model_size, device, compute_type, download_root)
        self.model = None

    def load_model(self, progress_callback=None):
        if self.model is not None:
            if progress_callback:
                progress_callback(100, "ready", "Model đã có sẵn trong bộ nhớ RAM/VRAM", "Sẵn sàng")
            return

        import torch
        if progress_callback:
            progress_callback(10, "init", "Khởi tạo SenseVoice-Small...", "Khởi tạo")

        actual_device = self.device
        if actual_device == "auto":
            actual_device = "cuda" if torch.cuda.is_available() else "cpu"

        download_dir = self.download_root
        if download_dir:
            download_dir = os.path.join(download_dir, "sensevoice")

        try:
            from funasr import AutoModel
            if progress_callback:
                progress_callback(45, "loading", f"Đang nạp SenseVoice-Small trên {actual_device}...", "Nạp bộ nhớ")
            print(f"[SenseVoiceEngine] Nap SenseVoice-Small tren {actual_device}...")
            self.model = AutoModel(
                model="iic/SenseVoiceSmall",
                device=actual_device,
                hub="hf"
            )
            print("[SenseVoiceEngine] [OK] Nap SenseVoice thanh cong!")
            if progress_callback:
                progress_callback(100, "ready", "Nạp thành công SenseVoice-Small!", "Hoàn tất (100%)")
        except Exception as e:
            print(f"[SenseVoiceEngine] FunASR chua san sang ({e}). Chuyen sang Faster-Whisper Base mode.")
            if progress_callback:
                progress_callback(60, "loading", "Chuyển sang Faster-Whisper Base fallback...", "Nạp fallback")
            from faster_whisper import WhisperModel
            self.model = WhisperModel(
                model_size_or_path="base",
                device=actual_device,
                compute_type="int8",
                download_root=download_dir
            )
            if progress_callback:
                progress_callback(100, "ready", "Nạp thành công Faster-Whisper Base fallback!", "Hoàn tất (100%)")

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

        detected_lang = "auto"
        raw_segments = []

        try:
            # SenseVoice ASR
            res = self.model.generate(input=audio_path, language="auto", use_itn=True)
            if res and len(res) > 0:
                text = res[0].get("text", "").strip()
                # Clean emotion tags like <|HAPPY|>
                import re
                clean_text = re.sub(r"<\|.*?\|>", "", text).strip()
                raw_segments.append({
                    "start": 0.0,
                    "end": 0.0, # Will be set by audio duration
                    "text": clean_text
                })
        except Exception:
            # Fallback faster-whisper
            segments, info = self.model.transcribe(audio_path, beam_size=3, vad_filter=vad_filter)
            detected_lang = info.language or "en"
            for seg in segments:
                raw_segments.append({
                    "start": round(seg.start, 2),
                    "end": round(seg.end, 2),
                    "text": seg.text.strip()
                })

        # Dịch thuật
        if task in ("transcribe_and_translate", "translate") and target_lang:
            translator = get_global_translator(download_root=self.download_root)
            texts = [s["text"] for s in raw_segments]
            translated_texts = translator.translate_batch(texts, src_lang=detected_lang if detected_lang != "auto" else "en", tgt_lang=target_lang)
            for i, trans_text in enumerate(translated_texts):
                if i < len(raw_segments):
                    raw_segments[i]["original_text"] = raw_segments[i]["text"]
                    raw_segments[i]["text"] = trans_text

        return {
            "chunkIndex": chunk_index,
            "language": detected_lang,
            "segments": raw_segments
        }
