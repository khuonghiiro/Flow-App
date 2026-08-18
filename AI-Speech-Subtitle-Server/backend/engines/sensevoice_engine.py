import os
import sys
import subprocess
from typing import Dict, Any, List, Optional
from .base_engine import BaseSpeechEngine
from .nllb_translator import get_global_translator

class SenseVoiceEngine(BaseSpeechEngine):
    """
    Engine 3: SenseVoice-Small (FunASR / Alibaba)
    Siêu nhẹ, siêu tốc độ (nhanh hơn Whisper 10x), nhận diện đa ngôn ngữ tức thì, chạy mượt trên CPU & GPU.
    Tự động cài đặt gói funasr/modelscope nếu chưa có và tự động tải model về lưu trữ offline.
    """
    def __init__(self, model_size: str = "base", device: str = "auto", compute_type: str = "default", download_root: str = None):
        super().__init__(model_size, device, compute_type, download_root)
        self.model = None
        self.is_fallback = False

    def _try_install_funasr(self, progress_callback=None) -> bool:
        """Tu dong cai dat thu vien funasr va modelscope neu moi truong chua co"""
        print("[SenseVoiceEngine] Thu vien 'funasr' chua co. Dang tu dong cai dat qua pip...")
        if progress_callback:
            progress_callback(15, "downloading", "Dang tu dong cai dat goi funasr & modelscope qua pip...", "Cai dat funasr")

        try:
            cmd = [sys.executable, "-m", "pip", "install", "funasr", "modelscope", "--no-warn-script-location"]
            proc = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
            if proc.returncode == 0:
                print("[SenseVoiceEngine] [OK] Da cai dat thanh cong thu vien 'funasr'!")
                if progress_callback:
                    progress_callback(35, "downloading", "Da cai dat FunASR thanh cong! Dang nap model...", "Cai dat hoan tat")
                import importlib
                importlib.invalidate_caches()
                return True
            else:
                print(f"[SenseVoiceEngine] Khong the cai dat funasr: {proc.stderr[:200]}")
                return False
        except Exception as e:
            print(f"[SenseVoiceEngine] Loi khi tu dong cai dat funasr: {e}")
            return False

    def _find_local_model_dir(self, download_dir: Optional[str]) -> Optional[str]:
        """Tim thu muc chua model SenseVoice cuc bo tren o dia de nap offline hoan toan"""
        candidates = []
        if download_dir and os.path.exists(download_dir):
            candidates.append(download_dir)

        from pathlib import Path
        project_root = Path(__file__).resolve().parent.parent.parent
        candidates.append(str(project_root / "models" / "sensevoice"))
        candidates.append(str(project_root / "models" / "SenseVoiceSmall"))

        # Kiem tra thu muc cache ModelScope
        ms_cache = Path.home() / ".cache" / "modelscope" / "models" / "iic--SenseVoiceSmall" / "snapshots" / "master"
        if ms_cache.exists():
            candidates.append(str(ms_cache))

        # Kiem tra thu muc cache HuggingFace
        hf_cache_dir = Path.home() / ".cache" / "huggingface" / "hub" / "models--FunAudioLLM--SenseVoiceSmall" / "snapshots"
        if hf_cache_dir.exists():
            for snap in hf_cache_dir.iterdir():
                if snap.is_dir():
                    candidates.append(str(snap))

        for c in candidates:
            if os.path.exists(c) and os.path.isfile(os.path.join(c, "config.yaml")):
                return c
        return None

    def load_model(self, progress_callback=None):
        if self.model is not None:
            if progress_callback:
                progress_callback(100, "ready", "Model da co san trong bo nho RAM/VRAM", "San sang")
            return

        import torch
        if progress_callback:
            progress_callback(10, "init", "Khoi tao SenseVoice-Small...", "Khoi tao")

        actual_device = self.device
        if actual_device == "auto":
            actual_device = "cuda" if torch.cuda.is_available() else "cpu"

        download_dir = self.download_root
        if download_dir:
            download_dir = os.path.join(download_dir, "sensevoice")
            os.makedirs(download_dir, exist_ok=True)

        # 1. Kiem tra va nap funasr
        try:
            from funasr import AutoModel
        except ImportError:
            # Thu tu dong cai dat neu chua co
            if self._try_install_funasr(progress_callback):
                try:
                    from funasr import AutoModel
                except ImportError:
                    AutoModel = None
            else:
                AutoModel = None

        if AutoModel is not None:
            try:
                if progress_callback:
                    progress_callback(45, "loading", f"Dang nap SenseVoice-Small tren {actual_device}...", "Nap model")

                # Uu tien 1: Nap truc tiep tu thu muc model cuc bo (Offline Mode khong can internet)
                local_dir = self._find_local_model_dir(download_dir)
                if local_dir and os.path.exists(local_dir):
                    print(f"[SenseVoiceEngine] Phat hien model cuc bo tai: {local_dir}. Dang nap offline...")
                    try:
                        self.model = AutoModel(
                            model=local_dir,
                            device=actual_device,
                            disable_update=True
                        )
                        self.is_fallback = False
                        print("[SenseVoiceEngine] [OK] Nap SenseVoice-Small tu thu muc cuc bo thanh cong (Offline Mode)!")
                        if progress_callback:
                            progress_callback(100, "ready", "Nap thanh cong SenseVoice-Small tu thu muc cuc bo!", "Hoan tat (100%)")
                        return
                    except Exception as local_err:
                        print(f"[SenseVoiceEngine] Canh bao nap offline: {local_err}. Thu nap qua hub...")

                # Uu tien 2: Nap qua ModelScope / HuggingFace Hub voi disable_update=True
                print(f"[SenseVoiceEngine] Dang nap SenseVoice-Small tren thiet bi {actual_device}...")
                try:
                    self.model = AutoModel(
                        model="iic/SenseVoiceSmall",
                        device=actual_device,
                        hub="ms",
                        disable_update=True
                    )
                except Exception as hf_err:
                    print(f"[SenseVoiceEngine] ModelScope load failed ({hf_err}), trying HuggingFace hub...")
                    self.model = AutoModel(
                        model="iic/SenseVoiceSmall",
                        device=actual_device,
                        hub="hf",
                        disable_update=True
                    )
                self.is_fallback = False
                print("[SenseVoiceEngine] [OK] Nap SenseVoice-Small thanh cong!")
                if progress_callback:
                    progress_callback(100, "ready", "Nap thanh cong SenseVoice-Small!", "Hoan tat (100%)")
                return
            except Exception as e:
                print(f"[SenseVoiceEngine] Canh bao khi nap SenseVoice qua AutoModel: {e}")

        # 2. Fallback sang Faster-Whisper Base neu funasr gap su co
        print("[SenseVoiceEngine] Chuyen sang che do du phong Faster-Whisper Base mode.")
        if progress_callback:
            progress_callback(60, "loading", "Chuyen sang Faster-Whisper Base fallback...", "Nap fallback")
        from faster_whisper import WhisperModel
        self.model = WhisperModel(
            model_size_or_path="base",
            device=actual_device,
            compute_type="int8",
            download_root=download_dir
        )
        self.is_fallback = True
        if progress_callback:
            progress_callback(100, "ready", "Nap thanh cong Faster-Whisper Base fallback!", "Hoan tat (100%)")

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
            if not self.is_fallback and hasattr(self.model, "generate"):
                # SenseVoice Native ASR
                res = self.model.generate(
                    input=audio_path,
                    language="auto",
                    use_itn=True,
                    batch_size_s=60
                )
                if res and len(res) > 0:
                    text = res[0].get("text", "").strip()
                    # Bỏ các nhãn cảm xúc/ngôn ngữ như <|HAPPY|>, <|vi|>, <|en|>
                    import re
                    clean_text = re.sub(r"<\|.*?\|>", "", text).strip()

                    # Cố gắng phân tách câu theo dấu chấm, chấm phẩy
                    sentences = [s.strip() for s in re.split(r'([.?!;\n]+)', clean_text) if s.strip()]
                    merged_sentences = []
                    curr = ""
                    for s in sentences:
                        if re.match(r'^[.?!;\n]+$', s):
                            curr += s
                            merged_sentences.append(curr.strip())
                            curr = ""
                        else:
                            if curr:
                                merged_sentences.append(curr.strip())
                            curr = s
                    if curr:
                        merged_sentences.append(curr.strip())

                    if not merged_sentences:
                        merged_sentences = [clean_text] if clean_text else [""]

                    # Ước lượng mốc thời gian nếu không có timestamp chi tiết
                    for idx, sentence in enumerate(merged_sentences):
                        raw_segments.append({
                            "id": idx + 1,
                            "start": 0.0,
                            "end": 0.0,
                            "text": sentence
                        })
            else:
                # Faster-Whisper Fallback
                segments, info = self.model.transcribe(audio_path, beam_size=3, vad_filter=vad_filter)
                detected_lang = info.language or "en"
                for idx, seg in enumerate(segments):
                    raw_segments.append({
                        "id": idx + 1,
                        "start": round(seg.start, 2),
                        "end": round(seg.end, 2),
                        "text": seg.text.strip()
                    })
        except Exception as e:
            print(f"[SenseVoiceEngine] Loi trong qua trinh transcribe: {e}")

        # Dịch thuật nếu được yêu cầu
        if task in ("transcribe_and_translate", "translate") and target_lang:
            translator = get_global_translator(download_root=self.download_root)
            texts = [s["text"] for s in raw_segments]
            translated_texts = translator.translate_batch(
                texts,
                src_lang=detected_lang if detected_lang != "auto" else "en",
                tgt_lang=target_lang
            )
            for i, trans_text in enumerate(translated_texts):
                if i < len(raw_segments):
                    raw_segments[i]["original_text"] = raw_segments[i]["text"]
                    raw_segments[i]["text"] = trans_text

        return {
            "chunkIndex": chunk_index,
            "language": detected_lang,
            "segments": raw_segments
        }
