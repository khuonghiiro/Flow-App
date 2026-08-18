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

    def is_model_installed(self, engine: str, model_size: str) -> bool:
        """
        Kiểm tra xem model cụ thể đã được tải về ổ cứng chưa
        """
        if not self.models_dir.exists():
            return False

        size_str = model_size.lower().replace("_", "-")

        # Quét các thư mục có thể chứa model
        search_dirs = [
            self.models_dir,
            self.models_dir / "faster-whisper",
            self.models_dir / "whisperx",
            self.models_dir / "sensevoice",
            self.models_dir / "campplus",
            self.models_dir / "hub"
        ]

        for s_dir in search_dirs:
            if not s_dir.exists():
                continue
            for item in s_dir.iterdir():
                if item.is_dir() or item.is_file():
                    name_lower = item.name.lower()
                    if engine in ("faster-whisper", "whisperx"):
                        # Khớp các dạng: models--Systran--faster-whisper-small, small, faster-whisper-small
                        if size_str in name_lower and ("whisper" in name_lower or item.name == model_size):
                            # Kiểm tra xem thư mục có tệp tin thực sự bên trong không (không phải thư mục rỗng)
                            if item.is_dir() and (any(item.glob("**/*.bin")) or any(item.glob("**/*.safetensors")) or any(item.glob("**/snapshots/**/*"))):
                                return True
                    elif engine == "sensevoice":
                        if "sensevoice" in name_lower:
                            return True
                    elif engine == "campplus":
                        if "campplus" in name_lower:
                            return True
                    elif engine == "seamless-m4t":
                        if "seamless-m4t" in name_lower and ("large" in size_str if "large" in name_lower else "medium" in size_str):
                            return True
                    elif engine == "translator":
                        if "nllb" in name_lower:
                            return True

        return False

    def get_model_status_map(self) -> dict:
        """
        Trả về dictionary thể hiện trạng thái (đã tải / chưa tải) cho tất cả model
        """
        whisper_sizes = ["tiny", "base", "small", "medium", "large-v3", "large-v3-turbo"]
        status = {
            "faster-whisper": {},
            "whisperx": {},
            "sensevoice": {
                "base": self.is_model_installed("sensevoice", "base")
            },
            "campplus": {
                "base": self.is_model_installed("campplus", "base")
            },
            "seamless-m4t": {
                "medium": self.is_model_installed("seamless-m4t", "medium"),
                "large-v3": self.is_model_installed("seamless-m4t", "large-v3")
            },
            "translator": {
                "nllb-200": self.is_model_installed("translator", "nllb-200")
            }
        }

        for sz in whisper_sizes:
            status["faster-whisper"][sz] = self.is_model_installed("faster-whisper", sz)
            status["whisperx"][sz] = self.is_model_installed("whisperx", sz)

        return status

    def list_installed_models(self) -> list:
        """
        Quét danh sách tất cả các model đã tải về trong thư mục models_dir
        """
        import datetime
        results = []
        if not self.models_dir.exists():
            return results

        # Thư mục cha cần bỏ qua nếu quét ở root
        parent_containers = {"faster-whisper", "whisperx", "sensevoice", "hub", "models"}
        sub_dirs = ["faster-whisper", "whisperx", "sensevoice", "hub", "."]
        seen_paths = set()

        for sub in sub_dirs:
            target_dir = self.models_dir if sub == "." else self.models_dir / sub
            if not target_dir.exists():
                continue

            for item in target_dir.iterdir():
                if not item.is_dir() or item.name.startswith("."):
                    continue

                # Bỏ qua thư mục cha nếu quét ở root
                if sub == "." and item.name in parent_containers:
                    continue

                path_str = str(item.resolve())
                if path_str in seen_paths:
                    continue

                # Chỉ tính nếu thư mục có tệp tin thực sự bên trong
                size = self.get_directory_size_mb(item)
                if size <= 0.01:
                    continue

                seen_paths.add(path_str)
                raw_name = item.name

                # Lấy ngày sửa đổi cuối cùng
                try:
                    mtime = item.stat().st_mtime
                    modified_str = datetime.datetime.fromtimestamp(mtime).strftime("%d/%m/%Y %H:%M")
                except Exception:
                    modified_str = "N/A"

                # Nhận diện engine & tên thân thiện
                engine = "AI Engine"
                clean_name = raw_name

                if "faster-whisper" in path_str.lower() or "systran" in raw_name.lower():
                    engine = "Faster-Whisper"
                    clean_name = raw_name.replace("models--Systran--faster-whisper-", "").replace("models--", "")
                elif "whisperx" in path_str.lower():
                    engine = "WhisperX"
                    clean_name = raw_name.replace("models--Systran--faster-whisper-", "").replace("models--", "")
                elif "sensevoice" in path_str.lower() or "sensevoice" in raw_name.lower():
                    engine = "SenseVoice"
                    clean_name = "SenseVoice-Small"
                elif "seamless" in raw_name.lower():
                    engine = "SeamlessM4T"
                    clean_name = raw_name.replace("models--facebook--", "")
                elif "nllb" in raw_name.lower():
                    engine = "NLLB-Translator"
                    clean_name = "Meta NLLB-200 (600M)"
                elif raw_name.startswith("models--"):
                    clean_name = raw_name.replace("models--", "").replace("--", "/")

                # Làm sạch tên cho các model phổ biến
                if "mobiuslabs" in clean_name.lower() or "large-v3-turbo" in clean_name.lower():
                    clean_name = "large-v3-turbo (MobiusLabs)"

                results.append({
                    "name": f"{engine}: {clean_name}",
                    "model_name": clean_name,
                    "engine": engine,
                    "size_mb": size,
                    "modified": modified_str,
                    "path": path_str
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
