import os
from typing import Dict, Any, List
from .base_engine import BaseSpeechEngine
from .nllb_translator import get_global_translator, NLLB_LANG_MAP

class SeamlessM4TEngine(BaseSpeechEngine):
    """
    Engine 4: Meta SeamlessM4T v2 (Speech-to-Text Translation All-in-One)
    Dịch trực tiếp từ giọng nói sang văn bản đa ngôn ngữ trong một model duy nhất.
    """
    def __init__(self, model_size: str = "medium", device: str = "auto", compute_type: str = "default", download_root: str = None):
        super().__init__(model_size, device, compute_type, download_root)
        self.processor = None
        self.model = None

    def load_model(self):
        if self.model is not None:
            return

        import torch
        from transformers import AutoProcessor, SeamlessM4Tv2ForSpeechToText

        actual_device = self.device
        if actual_device == "auto":
            actual_device = "cuda" if torch.cuda.is_available() else "cpu"

        model_name = "facebook/seamless-m4t-v2-large" if "large" in self.model_size.lower() else "facebook/seamless-m4t-medium"
        print(f"[SeamlessM4TEngine] Nạp SeamlessM4T '{model_name}' trên {actual_device}...")

        kwargs = {}
        if self.download_root:
            kwargs["cache_dir"] = self.download_root

        try:
            self.processor = AutoProcessor.from_pretrained(model_name, **kwargs)
            self.model = SeamlessM4Tv2ForSpeechToText.from_pretrained(model_name, **kwargs).to(actual_device)
            print("[SeamlessM4TEngine] ✅ Nạp SeamlessM4T thành công!")
        except Exception as e:
            print(f"[SeamlessM4TEngine] ⚠ Lỗi nạp SeamlessM4T ({e}). Chuyển sang Faster-Whisper.")
            from faster_whisper import WhisperModel
            self.model = WhisperModel(model_size_or_path="medium", device=actual_device)

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

        # Seamless S2TT or Faster-Whisper fallback
        try:
            import torchaudio
            tgt_code = NLLB_LANG_MAP.get(target_lang.lower()[:2], "vie_Latn")[:3]
            waveform, sr = torchaudio.load(audio_path)
            if sr != 16000:
                import torchaudio.transforms as T
                waveform = T.Resample(sr, 16000)(waveform)

            inputs = self.processor(audios=waveform[0], return_tensors="pt", sampling_rate=16000).to(self.model.device)
            tokens = self.model.generate(**inputs, tgt_lang=tgt_code)
            text = self.processor.decode(tokens[0].tolist(), skip_special_tokens=True).strip()

            return {
                "chunkIndex": chunk_index,
                "language": target_lang,
                "segments": [
                    {
                        "start": 0.0,
                        "end": round(len(waveform[0]) / 16000.0, 2),
                        "text": text
                    }
                ]
            }
        except Exception:
            # Fallback
            from faster_whisper import WhisperModel
            if not isinstance(self.model, WhisperModel):
                self.model = WhisperModel(model_size_or_path="medium", device="auto")
            segments, info = self.model.transcribe(audio_path, beam_size=5)
            raw = []
            for seg in segments:
                raw.append({
                    "start": round(seg.start, 2),
                    "end": round(seg.end, 2),
                    "text": seg.text.strip()
                })
            return {
                "chunkIndex": chunk_index,
                "language": info.language,
                "segments": raw
            }
