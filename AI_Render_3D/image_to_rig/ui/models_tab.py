"""
UI Components and Event Handlers for Model Management & Downloading.
Provides interactive status cards, single-click downloads, and storage inspection.
"""

from pathlib import Path
from typing import Dict, Tuple
import gradio as gr

from image_to_rig.tools.model_downloader import (
    get_models_root,
    get_model_status,
    download_hunyuan3d,
    download_trellis,
    download_triposr,
    download_rembg,
    download_unirig,
    download_all_models,
)



def render_model_status_markdown() -> str:
    """Generate formatted markdown representation of the models directory status."""
    status = get_model_status()
    root_dir = status["models_root"]
    total_size = status["total_size_human"]

    lines = [
        f"### 📁 Thư mục Model: `{root_dir}`",
        f"**Tổng dung lượng đã sử dụng trên ổ đĩa**: `{total_size}`\n",
        "| Thành phần Model | Tình trạng | Dung lượng hiện tại | Kích thước dự kiến | Thư mục lưu trữ |",
        "| :--- | :--- | :--- | :--- | :--- |",
    ]

    for key, comp in status["components"].items():
        st_icon = "✅ ĐÃ SẴN SÀNG" if comp["is_downloaded"] else "❌ CHƯA TẢI"
        lines.append(
            f"| **{comp['name']}** | `{st_icon}` | {comp['size_human']} | ~{comp['approx_size']} | `{comp['directory']}` |"
        )

    return "\n".join(lines)


def build_models_tab() -> Dict:
    """Construct Gradio components for the Models Management tab."""
    gr.Markdown(
        """
        # 📦 Quản Lý & Tải Model AI Vào Thư Mục `models/`
        Tất cả các file weights/checkpoints của pipeline được lưu trữ tập trung tại thư mục `models/` ở root dự án.
        Bạn có thể tải sẵn về máy để chạy offline hoặc kiểm tra dung lượng ổ đĩa tại đây.
        """
    )

    with gr.Row():
        with gr.Column(scale=2):
            status_markdown = gr.Markdown(value=render_model_status_markdown)
            
            with gr.Row():
                btn_refresh = gr.Button("🔄 Làm mới trạng thái", variant="secondary")
                btn_download_all = gr.Button("📥 Tải tất cả Model (~6.5 GB)", variant="primary")

            with gr.Row():
                btn_dl_hunyuan = gr.Button("📥 Tải Hunyuan3D-2GP SOTA (2.9 GB)", variant="primary")
                btn_dl_trellis = gr.Button("📥 Tải TRELLIS SOTA (2.8 GB)", variant="secondary")
                btn_dl_triposr = gr.Button("📥 Tải TripoSR (1.7 GB)", variant="secondary")
                btn_dl_rembg = gr.Button("📥 Tải RemBG (176 MB)", variant="secondary")

        with gr.Column(scale=1):
            log_box = gr.Textbox(
                label="Nhật ký tải & kiểm tra Model (Download Logs)",
                value="Sẵn sàng kiểm tra và tải model...",
                lines=8,
                interactive=False,
            )

    # Event Callbacks
    def on_refresh() -> Tuple[str, str]:
        md = render_model_status_markdown()
        return md, "✅ Đã làm mới trạng thái thư mục models/."

    def on_download_hunyuan(progress=gr.Progress()) -> Tuple[str, str]:
        def cb(p, msg):
            progress(p, desc=msg)
        progress(0.1, desc="Đang tải Hunyuan3D-2GP SOTA...")
        res = download_hunyuan3d(progress_cb=cb)
        md = render_model_status_markdown()
        return md, f"Hunyuan3D-2GP: {res.get('message', 'Hoàn tất!')}"

    def on_download_trellis(progress=gr.Progress()) -> Tuple[str, str]:
        def cb(p, msg):
            progress(p, desc=msg)
        progress(0.1, desc="Đang tải TRELLIS SOTA...")
        res = download_trellis(progress_cb=cb)
        md = render_model_status_markdown()
        return md, f"TRELLIS: {res.get('message', 'Hoàn tất!')}"

    def on_download_triposr(progress=gr.Progress()) -> Tuple[str, str]:
        def cb(p, msg):
            progress(p, desc=msg)
        progress(0.1, desc="Đang tải TripoSR...")
        res = download_triposr(progress_cb=cb)
        md = render_model_status_markdown()
        return md, f"TripoSR: {res.get('message', 'Hoàn tất!')}"

    def on_download_rembg(progress=gr.Progress()) -> Tuple[str, str]:
        def cb(p, msg):
            progress(p, desc=msg)
        progress(0.1, desc="Đang tải RemBG...")
        res = download_rembg(progress_cb=cb)
        md = render_model_status_markdown()
        return md, f"RemBG: {res.get('message', 'Hoàn tất!')}"

    def on_download_unirig(progress=gr.Progress()) -> Tuple[str, str]:
        def cb(p, msg):
            progress(p, desc=msg)
        progress(0.5, desc="Đang thiết lập UniRig...")
        res = download_unirig(progress_cb=cb)
        md = render_model_status_markdown()
        return md, f"UniRig: {res.get('message', 'Hoàn tất!')}"

    def on_download_all(progress=gr.Progress()) -> Tuple[str, str]:
        def cb(p, msg):
            progress(p, desc=msg)
        progress(0.05, desc="Bắt đầu tải toàn bộ models...")
        download_all_models(progress_cb=cb)
        md = render_model_status_markdown()
        return md, "🎉 Đã tải hoàn tất tất cả model vào thư mục models/!"

    btn_refresh.click(fn=on_refresh, outputs=[status_markdown, log_box])
    btn_dl_hunyuan.click(fn=on_download_hunyuan, outputs=[status_markdown, log_box])
    btn_dl_trellis.click(fn=on_download_trellis, outputs=[status_markdown, log_box])
    btn_dl_triposr.click(fn=on_download_triposr, outputs=[status_markdown, log_box])
    btn_dl_rembg.click(fn=on_download_rembg, outputs=[status_markdown, log_box])
    btn_download_all.click(fn=on_download_all, outputs=[status_markdown, log_box])


    return {
        "status_markdown": status_markdown,
        "btn_refresh": btn_refresh,
        "btn_download_all": btn_download_all,
    }
