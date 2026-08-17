import os
from typing import Optional

# Mã ngôn ngữ NLLB-200 tương ứng với các mã ngôn ngữ thông dụng
NLLB_LANG_MAP = {
    "vi": "vie_Latn",
    "en": "eng_Latn",
    "zh": "zho_Hans",
    "ja": "jpn_Jpan",
    "ko": "kor_Hang",
    "fr": "fra_Latn",
    "de": "deu_Latn",
    "es": "spa_Latn",
    "ru": "rus_Cyrl",
    "th": "tha_Thai",
    "id": "ind_Latn",
    "pt": "por_Latn",
    "it": "ita_Latn",
    "ar": "arb_Arab"
}

class NllbTranslator:
    """
    Translator offline sử dụng model Meta NLLB-200-distilled-600M siêu nhẹ,
    dịch câu chữ tự nhiên, giữ nguyên ngữ cảnh.
    """
    def __init__(self, model_name: str = "facebook/nllb-200-distilled-600M", device: str = "auto", download_root: Optional[str] = None):
        self.model_name = model_name
        self.device = device
        self.download_root = download_root
        self.tokenizer = None
        self.model = None
        self._is_loaded = False

    def load_model(self):
        if self._is_loaded and self.model is not None:
            return

        try:
            import torch
            from transformers import AutoModelForSeq2SeqLM, AutoTokenizer

            actual_device = "cuda" if (self.device == "cuda" or (self.device == "auto" and torch.cuda.is_available())) else "cpu"
            print(f"[NllbTranslator] Đang nạp model dịch {self.model_name} trên {actual_device}...")

            kwargs = {}
            if self.download_root:
                kwargs["cache_dir"] = self.download_root

            self.tokenizer = AutoTokenizer.from_pretrained(self.model_name, **kwargs)
            self.model = AutoModelForSeq2SeqLM.from_pretrained(self.model_name, **kwargs).to(actual_device)
            self._is_loaded = True
            print(f"[NllbTranslator] ✅ Nạp model dịch thành công!")
        except Exception as e:
            print(f"[NllbTranslator] ⚠ Không thể nạp NLLB model ({e}). Sẽ sử dụng bản dịch gốc nếu có lỗi.")

    def translate_batch(self, texts: list, src_lang: str = "en", tgt_lang: str = "vi") -> list:
        if not texts:
            return []

        # Nếu ngôn ngữ nguồn trùng ngôn ngữ đích -> không cần dịch
        if src_lang.lower().startswith(tgt_lang.lower()) or tgt_lang.lower().startswith(src_lang.lower()):
            return texts

        if not self._is_loaded or self.model is None:
            self.load_model()

        if self.model is None or self.tokenizer is None:
            return texts

        try:
            import torch
            src_code = NLLB_LANG_MAP.get(src_lang.lower()[:2], "eng_Latn")
            tgt_code = NLLB_LANG_MAP.get(tgt_lang.lower()[:2], "vie_Latn")

            self.tokenizer.src_lang = src_code
            encoded = self.tokenizer(texts, return_tensors="pt", padding=True, truncation=True, max_length=256)
            encoded = {k: v.to(self.model.device) for k, v in encoded.items()}

            generated_tokens = self.model.generate(
                **encoded,
                forced_bos_token_id=self.tokenizer.lang_code_to_id[tgt_code],
                max_length=256
            )

            decoded = self.tokenizer.batch_decode(generated_tokens, skip_special_tokens=True)
            return decoded
        except Exception as e:
            print(f"[NllbTranslator] Lỗi dịch batch: {e}")
            return texts

# Global instance
_global_translator = None

def get_global_translator(download_root: Optional[str] = None) -> NllbTranslator:
    global _global_translator
    if _global_translator is None:
        _global_translator = NllbTranslator(download_root=download_root)
    return _global_translator
