import os
from typing import Dict, Any, List
from .base_engine import BaseSpeechEngine
from .nllb_translator import get_global_translator

class WhisperXEngine(BaseSpeechEngine):
    """
    Engine 2: WhisperX with Phoneme Forced Alignment
    Mốc thời gian chuẩn xác tuyệt đối đến từng từ, tích hợp VAD và alignment.
    Tự động cài đặt gói whisperx nếu chưa có và chỉ fallback sang Faster-Whisper nếu xảy ra lỗi.
    """
    def __init__(self, model_size: str = "small", device: str = "auto", compute_type: str = "default", download_root: str = None):
        super().__init__(model_size, device, compute_type, download_root)
        self.model = None
        self.is_fallback = False

    def _try_install_whisperx(self, progress_callback=None) -> bool:
        """Tự động cài đặt thư viện whisperx qua pip nếu máy chưa có"""
        import subprocess
        import sys

        print("[WhisperXEngine] Thư viện 'whisperx' chưa có trong môi trường. Đang tự động cài đặt qua pip...")
        if progress_callback:
            progress_callback(15, "downloading", "Đang tự động cài đặt thư viện WhisperX qua pip...", "Cài đặt gói whisperx")

        try:
            cmd = [sys.executable, "-m", "pip", "install", "whisperx", "--no-warn-script-location"]
            proc = subprocess.run(cmd, capture_output=True, text=True, timeout=240)
            if proc.returncode == 0:
                print("[WhisperXEngine] [OK] Đã cài đặt thành công thư viện 'whisperx'!")
                if progress_callback:
                    progress_callback(35, "downloading", "Đã cài đặt WhisperX thành công! Đang nạp model...", "Cài đặt hoàn tất")
                import importlib
                importlib.invalidate_caches()
                return True
            else:
                print(f"[WhisperXEngine] Không thể cài đặt whisperx: {proc.stderr[:200]}")
                return False
        except Exception as e:
            print(f"[WhisperXEngine] Lỗi trong quá trình cài đặt whisperx: {e}")
            return False

    def load_model(self, progress_callback=None):
        if self.model is not None:
            if progress_callback:
                progress_callback(100, "ready", "Model đã có sẵn trong bộ nhớ RAM/VRAM", "Sẵn sàng")
            return

        import torch
        if progress_callback:
            progress_callback(10, "init", f"Khởi tạo WhisperX ({self.model_size})...", "Khởi tạo")

        actual_device = self.device
        if actual_device == "auto":
            actual_device = "cuda" if torch.cuda.is_available() else "cpu"

        actual_compute = self.compute_type
        if actual_compute == "default":
            actual_compute = "float16" if actual_device == "cuda" else "int8"

        download_dir = self.download_root
        model_to_load = self.model_size
        is_local = False

        if self.download_root:
            from pathlib import Path
            root_p = Path(self.download_root)
            candidates = [
                root_p / "whisperx" / f"models--Systran--faster-whisper-{self.model_size}",
                root_p / "faster-whisper" / f"models--Systran--faster-whisper-{self.model_size}",
                root_p / f"models--Systran--faster-whisper-{self.model_size}",
                root_p / "whisperx" / f"models--mobiuslabsgmbh--faster-whisper-{self.model_size}",
                root_p / "faster-whisper" / f"models--mobiuslabsgmbh--faster-whisper-{self.model_size}",
                root_p / "whisperx" / self.model_size,
                root_p / "faster-whisper" / self.model_size,
                root_p / self.model_size
            ]
            for cand in candidates:
                if cand.exists() and cand.is_dir():
                    snapshots_dir = cand / "snapshots"
                    if snapshots_dir.exists():
                        snaps = [s for s in snapshots_dir.iterdir() if s.is_dir()]
                        if snaps:
                            model_to_load = str(snaps[0].resolve())
                            download_dir = None
                            is_local = True
                            break
                    model_to_load = str(cand.resolve())
                    download_dir = None
                    is_local = True
                    break

            if not is_local and download_dir:
                download_dir = os.path.join(self.download_root, "whisperx")

        # 1. Kiểm tra thư viện whisperx, nếu chưa có thì tự động cài đặt
        whisperx_available = False
        try:
            import whisperx
            whisperx_available = True
        except (ImportError, ModuleNotFoundError):
            whisperx_available = self._try_install_whisperx(progress_callback)

        # 2. Nếu whisperx có sẵn, nạp bằng whisperx.load_model
        if whisperx_available:
            try:
                import whisperx
                if progress_callback:
                    progress_callback(40, "loading", f"Đang nạp WhisperX model '{self.model_size}'...", "Nạp VRAM")
                print(f"[WhisperXEngine] Nap WhisperX model '{self.model_size}' tren {actual_device}...")
                self.model = whisperx.load_model(
                    self.model_size,
                    device=actual_device,
                    compute_type=actual_compute,
                    download_root=download_dir
                )
                self.is_fallback = False
                print("[WhisperXEngine] [OK] Nap WhisperX thanh cong!")
                if progress_callback:
                    progress_callback(100, "ready", f"Nạp thành công WhisperX ({self.model_size})!", "Hoàn tất (100%)")
                return
            except Exception as e:
                print(f"[WhisperXEngine] Khoi tao WhisperX gap loi ({e}). Chuyen sang Faster-Whisper fallback.")

        # 3. Fallback sang Faster-Whisper nếu cài đặt hoặc khởi tạo WhisperX bị lỗi
        if progress_callback:
            progress_callback(60, "loading", "Tự động chuyển sang Faster-Whisper fallback...", "Nạp weights")
        from faster_whisper import WhisperModel
        self.model = WhisperModel(
            model_size_or_path=model_to_load,
            device=actual_device,
            compute_type=actual_compute,
            download_root=download_dir
        )
        self.is_fallback = True
        print(f"[WhisperXEngine] [OK] Nap Faster-Whisper fallback '{self.model_size}' tren {actual_device.upper()} ({actual_compute}) thanh cong!")
        if progress_callback:
            progress_callback(100, "ready", f"Nạp thành công model '{self.model_size}' trên {actual_device.upper()} ({actual_compute})!", f"Sẵn sàng trên {actual_device.upper()}")

    def unload_model(self):
        """Giải phóng bộ nhớ RAM/VRAM của model để tránh tràn VRAM khi đổi model"""
        if self.model is not None:
            del self.model
            self.model = None
            import gc
            gc.collect()
            import torch
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
            print(f"[WhisperXEngine] [OK] Đã giải phóng model '{self.model_size}' khỏi VRAM/RAM")

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
                s_start = round(seg.get("start", 0.0), 2)
                s_end = round(seg.get("end", 0.0), 2)
                if s_end <= s_start:
                    s_end = round(s_start + 0.5, 2)
                txt = seg.get("text", "").strip()
                if not txt:
                    continue
                raw_segments.append({
                    "id": len(raw_segments) + 1,
                    "start": s_start,
                    "end": s_end,
                    "duration": round(s_end - s_start, 2),
                    "text": txt,
                    "original_text": txt,
                    "translations": {
                        detected_lang: txt
                    }
                })
        except Exception:
            # Fallback faster-whisper với VAD và word timestamps chính xác
            segments, info = self.model.transcribe(
                audio_path,
                beam_size=5,
                word_timestamps=True,
                vad_filter=vad_filter,
                vad_parameters=dict(min_silence_duration_ms=400, speech_pad_ms=150)
            )
            detected_lang = info.language or "en"
            for seg in segments:
                s_start = round(seg.start, 2)
                s_end = round(seg.end, 2)

                if hasattr(seg, "words") and seg.words:
                    last_w = seg.words[-1]
                    if last_w and getattr(last_w, "end", None):
                        p_end = round(last_w.end + 0.15, 2)
                        if p_end < s_end:
                            s_end = p_end

                if s_end <= s_start:
                    s_end = round(s_start + 0.5, 2)

                txt = seg.text.strip()
                if not txt:
                    continue

                raw_segments.append({
                    "id": len(raw_segments) + 1,
                    "start": s_start,
                    "end": s_end,
                    "duration": round(s_end - s_start, 2),
                    "text": txt,
                    "original_text": txt,
                    "translations": {
                        detected_lang: txt
                    }
                })

        # Dịch thuật
        if task in ("transcribe_and_translate", "translate") and target_lang and detected_lang.lower() != target_lang.lower():
            translator = get_global_translator(download_root=self.download_root)
            texts = [s["text"] for s in raw_segments]
            translated_texts = translator.translate_batch(texts, src_lang=detected_lang, tgt_lang=target_lang)
            for i, trans_text in enumerate(translated_texts):
                if i < len(raw_segments):
                    raw_segments[i]["original_text"] = raw_segments[i]["text"]
                    raw_segments[i]["translated_text"] = trans_text
                    raw_segments[i]["translations"][target_lang] = trans_text
                    raw_segments[i]["text"] = trans_text

        return {
            "chunkIndex": chunk_index,
            "language": detected_lang,
            "segments": raw_segments
        }
