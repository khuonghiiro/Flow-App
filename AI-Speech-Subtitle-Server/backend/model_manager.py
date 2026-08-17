import os
import shutil
from pathlib import Path

# Thư mục gốc mặc định để lưu các model AI
DEFAULT_MODELS_DIR = Path(__file__).resolve().parent.parent / "models"
DEFAULT_MODELS_DIR.mkdir(parents=True, exist_ok=True)

class ModelManager:
    def __init__(self, models_dir: str = None):
        self.models_dir = Path(models_dir) if models_dir else DEFAULT_MODELS_DIR
        self.models_dir.mkdir(parents=True, exist_ok=True)

    def set_models_dir(self, new_dir: str):
        self.models_dir = Path(new_dir)
        self.models_dir.mkdir(parents=True, exist_ok=True)

    def get_models_dir_path(self) -> str:
        return str(self.models_dir.resolve())

    def get_directory_size_mb(self, path: Path) -> float:
        if not path.exists():
            return 0.0
        total = 0
        try:
            for p in path.glob("**/*"):
                if p.is_file():
                    total += p.stat().st_size
        except Exception:
            pass
        return round(total / (1024 * 1024), 2)

    def list_installed_models(self) -> list:
        """
        Quét danh sách các model đã tải về trong thư mục models_dir
        """
        results = []
        if not self.models_dir.exists():
            return results

        # 1. Faster-Whisper models
        fw_dir = self.models_dir / "faster-whisper"
        if fw_dir.exists():
            for item in fw_dir.iterdir():
                if item.is_dir():
                    size = self.get_directory_size_mb(item)
                    results.append({
                        "engine": "faster-whisper",
                        "model_name": item.name,
                        "size_mb": size,
                        "path": str(item.resolve())
                    })

        # 2. Huggingface / NLLB / WhisperX / SenseVoice / Seamless models
        hf_dir = self.models_dir / "hub"
        if hf_dir.exists():
            for item in hf_dir.iterdir():
                if item.is_dir() and item.name.startswith("models--"):
                    clean_name = item.name.replace("models--", "").replace("--", "/")
                    size = self.get_directory_size_mb(item)
                    results.append({
                        "engine": "huggingface",
                        "model_name": clean_name,
                        "size_mb": size,
                        "path": str(item.resolve())
                    })

        return results

    def delete_model(self, model_path: str) -> bool:
        """
        Xóa thư mục model để giải phóng dung lượng ổ cứng
        """
        p = Path(model_path)
        if p.exists() and p.is_dir():
            try:
                shutil.rmtree(p)
                return True
            except Exception as e:
                print(f"[ModelManager] Lỗi xóa model {model_path}: {e}")
                return False
        return False

    def clear_all_models(self) -> bool:
        """
        Xóa toàn bộ model trong thư mục cache
        """
        try:
            for item in self.models_dir.iterdir():
                if item.is_dir():
                    shutil.rmtree(item)
                elif item.is_file():
                    item.unlink()
            return True
        except Exception as e:
            print(f"[ModelManager] Lỗi dọn dẹp cache: {e}")
            return False

# Global instance
model_manager = ModelManager()
