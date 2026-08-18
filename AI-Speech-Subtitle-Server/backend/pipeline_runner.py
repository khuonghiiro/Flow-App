import os
import time
from concurrent.futures import ThreadPoolExecutor
from typing import Dict, Any, List, Optional

class PipelineRunner:
    """
    All-in-One Audio AI Pipeline Engine:
    - Bóc tách phụ đề (ASR: Faster-Whisper, SenseVoice, WhisperX, SeamlessM4T)
    - Phân tách & Nhận diện nhân vật (CAM++ Neural Voice Diarization)
    - Dịch thuật phụ đề đa ngôn ngữ (NLLB-200 / SeamlessM4T)
    - Hỗ trợ chế độ Concurrent (Song song siêu tốc khi đủ VRAM) hoặc Sequential (Tuần tự dọn VRAM chống OOM).
    """
    def __init__(self):
        self.thread_pool = ThreadPoolExecutor(max_workers=4)

    def _unload_memory(self, active_engines: dict):
        """Dọn dẹp GPU VRAM và RAM để tránh tràn bộ nhớ khi chạy tuần tự"""
        try:
            import torch
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except Exception:
            pass
        import gc
        gc.collect()

    def run_pipeline(
        self,
        audio_path: str,
        enable_transcribe: bool = True,
        enable_diarization: bool = True,
        enable_translate: bool = False,
        execution_mode: str = "concurrent",
        asr_engine: str = "faster-whisper",
        asr_model_size: str = "small",
        target_lang: str = "vi",
        src_lang: str = "auto",
        character_samples: Optional[List[Dict[str, Any]]] = None,
        num_speakers: Optional[int] = None,
        similarity_threshold: float = 0.65,
        min_duration: float = 0.4,
        adaptive_learning: bool = True,
        diarize_engine: str = "auto",
        device: str = "auto",
        compute_type: str = "default",
        vad_filter: bool = True,
        word_timestamps: bool = False,
        chunk_index: int = 0,
        time_offset: float = 0.0,
        active_engines_dict: Optional[dict] = None,
        get_engine_func: Optional[Any] = None
    ) -> Dict[str, Any]:
        """
        Thực thi toàn bộ quy trình Pipeline All-in-One đồng bộ / đa luồng
        """
        start_time = time.time()
        if not audio_path or not os.path.exists(audio_path):
            raise ValueError(f"Không tìm thấy file audio: {audio_path}")

        segments = []
        source_lang = src_lang
        lang_prob = 1.0

        # ====================================================================
        # BƯỚC 1: BÓC TÁCH PHỤ ĐỀ (ASR TRANSCRIPTION)
        # ====================================================================
        if enable_transcribe and get_engine_func is not None:
            engine_inst = get_engine_func(asr_engine, asr_model_size, device, compute_type)
            trans_task = "transcribe_and_translate" if (enable_translate and asr_engine == "seamless-m4t") else "transcribe_only"
            
            raw_result = engine_inst.transcribe(
                audio_path=audio_path,
                target_lang=target_lang,
                task=trans_task,
                vad_filter=vad_filter,
                word_timestamps=word_timestamps
            )
            segments = raw_result.get("segments", [])
            source_lang = raw_result.get("language", src_lang)
            lang_prob = raw_result.get("language_probability", 1.0)

            # Nếu chạy chế độ Sequential và GPU VRAM thấp, dọn dẹp ASR engine
            if execution_mode == "sequential" and active_engines_dict is not None and device in ("cuda", "auto"):
                self._unload_memory(active_engines_dict)

        # ====================================================================
        # BƯỚC 2 & 3: PHÂN TÁCH NHÂN VẬT & DỊCH THUẬT
        # ====================================================================
        need_diarize = enable_diarization
        need_translate = enable_translate and (asr_engine != "seamless-m4t")

        if execution_mode == "concurrent" and (need_diarize or need_translate):
            # CHẾ ĐỘ SONG SONG: Chạy Diarization và Translation đồng thời trên ThreadPool
            future_diarize = None
            future_translate = None

            if need_diarize:
                from .engines.speaker_diarizer import get_global_diarizer
                diarizer = get_global_diarizer()
                future_diarize = self.thread_pool.submit(
                    diarizer.diarize_and_identify,
                    audio_path=audio_path,
                    segments=segments,
                    character_samples=character_samples,
                    num_speakers=num_speakers,
                    similarity_threshold=similarity_threshold,
                    min_duration=min_duration,
                    adaptive_learning=adaptive_learning,
                    embedding_engine=diarize_engine
                )

            if need_translate and segments:
                from .engines.nllb_translator import get_global_translator
                translator = get_global_translator()
                texts = [s.get("text", "") for s in segments]
                future_translate = self.thread_pool.submit(
                    translator.translate_batch,
                    texts=texts,
                    src_lang=source_lang,
                    tgt_lang=target_lang
                )

            if future_diarize is not None:
                try:
                    segments = future_diarize.result()
                except Exception as e:
                    print(f"[PipelineRunner] Canh bao loi diarize concurrent: {e}")

            if future_translate is not None:
                try:
                    translated_texts = future_translate.result()
                    for idx, t_text in enumerate(translated_texts):
                        if idx < len(segments):
                            segments[idx]["translation"] = t_text
                except Exception as e:
                    print(f"[PipelineRunner] Canh bao loi translate concurrent: {e}")

        else:
            # CHẾ ĐỘ TUẦN TỰ (Sequential): Xử lý từng model để tiết kiệm VRAM tối đa
            if need_diarize:
                from .engines.speaker_diarizer import get_global_diarizer
                diarizer = get_global_diarizer()
                segments = diarizer.diarize_and_identify(
                    audio_path=audio_path,
                    segments=segments,
                    character_samples=character_samples,
                    num_speakers=num_speakers,
                    similarity_threshold=similarity_threshold,
                    min_duration=min_duration,
                    adaptive_learning=adaptive_learning,
                    embedding_engine=diarize_engine
                )
                if execution_mode == "sequential" and active_engines_dict is not None:
                    self._unload_memory(active_engines_dict)

            if need_translate and segments:
                from .engines.nllb_translator import get_global_translator
                translator = get_global_translator()
                texts = [s.get("text", "") for s in segments]
                translated_texts = translator.translate_batch(
                    texts=texts,
                    src_lang=source_lang,
                    tgt_lang=target_lang
                )
                for idx, t_text in enumerate(translated_texts):
                    if idx < len(segments):
                        segments[idx]["translation"] = t_text
                if execution_mode == "sequential" and active_engines_dict is not None:
                    translator.unload_model()
                    self._unload_memory(active_engines_dict)

        # Tự động cộng dồn mốc thời gian offset khi xử lý song song các chunk âm thanh
        if time_offset and float(time_offset) > 0.0:
            off_val = float(time_offset)
            for s in segments:
                if "start" in s and s["start"] is not None:
                    s["start"] = round(s["start"] + off_val, 2)
                if "end" in s and s["end"] is not None:
                    s["end"] = round(s["end"] + off_val, 2)

        elapsed_ms = round((time.time() - start_time) * 1000, 1)

        # Trích xuất danh sách nhân vật duy nhất được phát hiện
        detected_speakers = []
        seen_speakers = set()
        for s in segments:
            spk = s.get("speaker")
            if spk and spk not in seen_speakers:
                seen_speakers.add(spk)
                detected_speakers.append({
                    "name": spk,
                    "color": s.get("speaker_color", "#3b82f6")
                })

        return {
            "status": "success",
            "chunkIndex": chunk_index,
            "time_offset": time_offset,
            "execution_mode": execution_mode,
            "elapsed_ms": elapsed_ms,
            "total_segments": len(segments),
            "source_language": source_lang,
            "source_language_probability": round(lang_prob, 2),
            "target_language": target_lang,
            "detected_speakers_count": len(detected_speakers),
            "detected_speakers": detected_speakers,
            "pipeline_summary": {
                "transcribe": enable_transcribe,
                "diarization": enable_diarization,
                "translation": enable_translate
            },
            "segments": segments
        }

# Global singleton
_global_pipeline_runner = None

def get_pipeline_runner() -> PipelineRunner:
    global _global_pipeline_runner
    if _global_pipeline_runner is None:
        _global_pipeline_runner = PipelineRunner()
    return _global_pipeline_runner
