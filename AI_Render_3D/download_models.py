"""
Root utility script to download all required AI models into the local 'models/' folder.
Run: python download_models.py
"""

import os
import sys

# Add project root to sys.path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from image_to_rig.tools.model_downloader import (
    get_models_root,
    get_model_status,
    download_all_models,
)

if __name__ == "__main__":
    print("======================================================================")
    print("  🚀 TẢI VÀ KIỂM TRA MODEL AI CHO PIPELINE IMAGE-TO-RIG")
    print(f"  📁 Thư mục lưu trữ: {get_models_root()}")
    print("======================================================================")

    status = get_model_status()
    print(f"\n📊 Dung lượng hiện tại: {status['total_size_human']}")
    for k, comp in status["components"].items():
        st = "✅ ĐÃ TẢI" if comp["is_downloaded"] else "❌ CHƯA CÓ"
        print(f"  • {comp['name']}: {st} (Dung lượng: {comp['size_human']} / ~{comp['approx_size']})")
        print(f"    Thư mục: {comp['directory']}")

    print("\n⏳ Đang kiểm tra và tải các model còn thiếu...")
    
    def on_progress(p: float, msg: str):
        bar_len = 30
        filled = int(bar_len * p)
        bar = "█" * filled + "░" * (bar_len - filled)
        print(f"\r[{bar}] {p*100:5.1f}% | {msg}", end="", flush=True)

    try:
        download_all_models(on_progress)
        print("\n\n======================================================================")
        print("🎉 [THÀNH CÔNG] Tất cả model đã sẵn sàng trong thư mục 'models/'!")
        print("======================================================================")
    except Exception as ex:
        print(f"\n\n❌ [LỖI KHI TẢI MODEL]: {ex}")
        sys.exit(1)
