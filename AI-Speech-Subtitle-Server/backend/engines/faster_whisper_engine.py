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

    def load_model(self, progress_callback=None):
        if self.model is not None:
            if progress_callback:
                progress_callback(100, "ready", "Model đã có sẵn trong bộ nhớ RAM/VRAM", "Sẵn sàng")
            return

        from faster_whisper import WhisperModel
        import torch

        if progress_callback:
            progress_callback(5, "init", f"Đang khởi tạo cấu hình cho model '{self.model_size}'...", "Khởi tạo")

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
            size_key = self.model_size.lower().replace("_", "-")
            search_dirs = [
                root_p / "faster-whisper",
                root_p / "whisperx",
                root_p,
                Path.home() / ".cache" / "huggingface" / "hub"
            ]
            for s_dir in search_dirs:
                if not s_dir.exists():
                    continue
                for item in s_dir.iterdir():
                    if not item.is_dir():
                        continue
                    name_lower = item.name.lower()
                    if "whisper" not in name_lower and item.name != self.model_size:
                        continue
                    if size_key == "large-v3":
                        if "large-v3-turbo" in name_lower:
                            continue
                        if "large-v3" not in name_lower and "large" not in name_lower:
                            continue
                    elif size_key not in name_lower:
                        continue

                    snaps_dir = item / "snapshots"
                    if snaps_dir.exists():
                        for snap in snaps_dir.iterdir():
                            if snap.is_dir() and ((snap / "model.bin").exists() or (snap / "config.json").exists()):
                                model_to_load = str(snap.resolve())
                                download_dir = None
                                is_local = True
                                break
                    if is_local:
                        break

                    if (item / "model.bin").exists() or (item / "config.json").exists():
                        model_to_load = str(item.resolve())
                        download_dir = None
                        is_local = True
                        break
                if is_local:
                    break

            if not is_local and download_dir:
                download_dir = os.path.join(self.download_root, "faster-whisper")

        if is_local:
            if progress_callback:
                progress_callback(25, "init", "Đã tìm thấy model trên máy. Đang mở tệp trọng số...", "Đọc tệp cục bộ")
                progress_callback(50, "loading", f"Đang nạp cấu trúc mạng nơ-ron vào {actual_device.upper()} ({actual_compute})...", "Nạp VRAM")
        else:
            if progress_callback:
                progress_callback(10, "downloading", f"Model '{self.model_size}' chưa có trên máy. Đang kết nối HuggingFace Hub...", "Bắt đầu tải")
            
            # Tải qua huggingface_hub snapshot_download với progress tracker
            try:
                import huggingface_hub
                from faster_whisper.utils import _MODELS
                repo_id = _MODELS.get(self.model_size, f"Systran/faster-whisper-{self.model_size}")
                
                class TqdmTracker:
                    def __init__(self, cb):
                        self.cb = cb

                    def make_tqdm(self):
                        tracker = self
                        from tqdm.auto import tqdm
                        class CustomTqdm(tqdm):
                            def __init__(self, *args, **kwargs):
                                super().__init__(*args, **kwargs)

                            def update(self, n=1):
                                super().update(n)
                                if self.total and self.total > 0 and tracker.cb:
                                    pct = 10 + int((self.n / self.total) * 70)
                                    cur_mb = round(self.n / (1024 * 1024), 1)
                                    tot_mb = round(self.total / (1024 * 1024), 1)
                                    tracker.cb(
                                        min(pct, 80),
                                        "downloading",
                                        f"Đang tải {self.desc or 'weights'}: {cur_mb} MB / {tot_mb} MB ({pct}%)",
                                        f"Tiến độ: {pct}% ({cur_mb}/{tot_mb} MB)"
                                    )
                        return CustomTqdm

                tracker = TqdmTracker(progress_callback)
                os.makedirs(download_dir, exist_ok=True)
                downloaded_path = huggingface_hub.snapshot_download(
                    repo_id,
                    cache_dir=download_dir,
                    tqdm_class=tracker.make_tqdm() if progress_callback else None
                )
                model_to_load = downloaded_path
                download_dir = None
            except Exception as e:
                print(f"[FasterWhisperEngine] Download snapshot error: {e}, using default fallback")

        if progress_callback:
            progress_callback(85, "loading", f"Đang nạp cấu trúc mạng nơ-ron vào {actual_device.upper()} ({actual_compute})...", "Nạp VRAM")

        print(f"[FasterWhisperEngine] Nap model '{self.model_size}' tren {actual_device.upper()} ({actual_compute})...")
        self.model = WhisperModel(
            model_size_or_path=model_to_load,
            device=actual_device,
            compute_type=actual_compute,
            download_root=download_dir
        )
        print("[FasterWhisperEngine] [OK] Nap model thanh cong!")

        if progress_callback:
            progress_callback(100, "ready", f"Nạp thành công model '{self.model_size}' vào {actual_device.upper()}!", "Hoàn tất (100%)")

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
            print(f"[FasterWhisperEngine] [OK] Da giai phong model '{self.model_size}' khoi VRAM/RAM")

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

        # Transcribe & Detect Language với VAD chuẩn xác
        segments, info = self.model.transcribe(
            audio_path,
            beam_size=5,
            word_timestamps=True,
            vad_filter=vad_filter,
            vad_parameters=dict(min_silence_duration_ms=400, speech_pad_ms=150)
        )

        detected_lang = info.language or "en"
        raw_segments = []

        for seg in segments:
            seg_start = round(seg.start, 2)
            seg_end = round(seg.end, 2)

            # Khóa chính xác mốc end theo từ cuối cùng để không bị kéo dài qua khoảng lặng
            if hasattr(seg, "words") and seg.words:
                last_word = seg.words[-1]
                if last_word and getattr(last_word, "end", None):
                    precise_end = round(last_word.end + 0.15, 2)
                    if precise_end < seg_end:
                        seg_end = precise_end

            if seg_end <= seg_start:
                seg_end = round(seg_start + 0.5, 2)

            duration = round(seg_end - seg_start, 2)
            text_str = seg.text.strip()
            if not text_str:
                continue

            raw_segments.append({
                "id": len(raw_segments) + 1,
                "start": seg_start,
                "end": seg_end,
                "duration": duration,
                "text": text_str,
                "original_text": text_str,
                "translations": {
                    detected_lang: text_str
                }
            })

        # Dịch thuật nếu được yêu cầu
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
            "language_probability": round(info.language_probability, 3),
            "duration": round(info.duration, 2),
            "segments": raw_segments
        }
