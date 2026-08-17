import os
from typing import Dict, Any, List
from .base_engine import BaseSpeechEngine
from .nllb_translator import get_global_translator

class WhisperXEngine(BaseSpeechEngine):
    """
    Engine 2: WhisperX with Phoneme Forced Alignment
    Mốc thời gian chuẩn xác tuyệt đối đến từng từ, tích hợp VAD và alignment.
    """
    def __init__(self, model_size: str = "small", device: str = "auto", compute_type: str = "default", download_root: str = None):
        super().__init__(model_size, device, compute_type, download_root)
        self.model = None

    def load_model(self):
        if self.model is not None:
            return

        import torch
        actual_device = self.device
        if actual_device == "auto":
            actual_device = "cuda" if torch.cuda.is_available() else "cpu"

        actual_compute = self.compute_type
        if actual_compute == "default":
            actual_compute = "float16" if actual_device == "cuda" else "int8"

        download_dir = self.download_root
        if download_dir:
            download_dir = os.path.join(download_dir, "whisperx")

        try:
            import whisperx
            print(f"[WhisperXEngine] Nạp WhisperX model '{self.model_size}' trên {actual_device}...")
            self.model = whisperx.load_model(
                self.model_size,
                device=actual_device,
                compute_type=actual_compute,
                download_root=download_dir
            )
            print("[WhisperXEngine] ✅ Nạp WhisperX thành công!")
        except Exception as e:
            print(f"[WhisperXEngine] ⚠ WhisperX chưa cài hoặc gặp lỗi ({e}). Chuyển sang Faster-Whisper fallback.")
            from faster_whisper import WhisperModel
            self.model = WhisperModel(
                model_size_or_path=self.model_size,
                device=actual_device,
                compute_type=actual_compute,
                download_root=download_dir
            )

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

        detected_lang = "en"
        raw_segments = []

        try:
            import whisperx
            # Đọc audio
            audio = whisperx.load_audio(audio_path)
            result = self.model.transcribe(audio, batch_size=16)
            detected_lang = result.get("language", "en")

            # Align whisper output
            try:
                device = self.device if self.device != "auto" else "cuda"
                model_a, metadata = whisperx.load_align_model(language_code=detected_lang, device=device)
                aligned_result = whisperx.align(result["segments"], model_a, metadata, audio, device, return_char_alignments=False)
                segments_source = aligned_result.get("segments", result["segments"])
            except Exception:
                segments_source = result.get("segments", [])

            for seg in segments_source:
                raw_segments.append({
                    "start": round(seg.get("start", 0.0), 2),
                    "end": round(seg.get("end", 0.0), 2),
                    "text": seg.get("text", "").strip()
                })
        except Exception:
            # Fallback faster-whisper
            segments, info = self.model.transcribe(audio_path, beam_size=5, word_timestamps=True, vad_filter=vad_filter)
            detected_lang = info.language or "en"
            for seg in segments:
                raw_segments.append({
                    "start": round(seg.start, 2),
                    "end": round(seg.end, 2),
                    "text": seg.text.strip()
                })

        # Dịch thuật
        if task in ("transcribe_and_translate", "translate") and target_lang and detected_lang.lower() != target_lang.lower():
            translator = get_global_translator(download_root=self.download_root)
            texts = [s["text"] for s in raw_segments]
            translated_texts = translator.translate_batch(texts, src_lang=detected_lang, tgt_lang=target_lang)
            for i, trans_text in enumerate(translated_texts):
                if i < len(raw_segments):
                    raw_segments[i]["original_text"] = raw_segments[i]["text"]
                    raw_segments[i]["text"] = trans_text

        return {
            "chunkIndex": chunk_index,
            "language": detected_lang,
            "segments": raw_segments
        }
