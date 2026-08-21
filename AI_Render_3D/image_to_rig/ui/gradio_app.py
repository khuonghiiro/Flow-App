"""
Interactive Gradio Web UI for Image-to-Rig Pipeline.
Provides single-step and end-to-end execution, 3D model previews, AI prompt guides, and model management.
"""

from pathlib import Path
import json
import time
from typing import Tuple, Optional
import gradio as gr
from PIL import Image

from image_to_rig.config import DEFAULT_CONFIG
from image_to_rig.core.pipeline import ImageToRigPipeline
from image_to_rig.core.stage2_rig import Stage2RigResult
from image_to_rig.ui.prompt_guide import (
    FULL_GUIDE_MARKDOWN,
    QUICK_GUIDE_MARKDOWN,
    PROMPT_TEMPLATES,
    get_template_choices,
)
from image_to_rig.ui.models_tab import build_models_tab
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


def create_gradio_app() -> gr.Blocks:
    """Construct and configure the Gradio web interface."""
    pipeline = ImageToRigPipeline(DEFAULT_CONFIG)
    logger = get_logger()

    # Session state storage for intermediate results
    session_state = {
        "current_image_path": None,
        "current_mesh_path": None,
        "stage2_rig_result": None,
    }

    # Callbacks for UI actions
    def on_image_uploaded(image_pil: Optional[Image.Image]) -> Tuple[str, str]:
        if image_pil is None:
            return "Chưa có ảnh nào được tải lên.", "Vui lòng chọn ảnh nhân vật 2D."

        temp_path = Path(DEFAULT_CONFIG.temp_dir) / "uploaded_input.png"
        temp_path.parent.mkdir(parents=True, exist_ok=True)
        image_pil.save(str(temp_path))
        session_state["current_image_path"] = str(temp_path)

        val = pipeline.validate_input(str(temp_path))
        status_lines = []
        if val.is_valid:
            status_lines.append("✅ Ảnh hợp lệ.")
        else:
            status_lines.append(f"❌ Lỗi ảnh: {'; '.join(val.errors)}")

        if val.warnings:
            status_lines.append(f"⚠️ Lưu ý: {'; '.join(val.warnings)}")

        return "\n".join(status_lines), f"Đã lưu ảnh tạm: {temp_path.name}"

    def on_generate_mesh(image_pil: Optional[Image.Image], progress=gr.Progress()) -> Tuple[Optional[str], str]:
        if not session_state["current_image_path"]:
            if image_pil is None:
                return None, "❌ Vui lòng tải ảnh lên trước khi tạo mesh."
            on_image_uploaded(image_pil)

        progress(0.1, desc="Đang tiền xử lý ảnh & chạy TripoSR...")
        try:
            res = pipeline.run_image_to_mesh(session_state["current_image_path"])
            session_state["current_mesh_path"] = res.mesh_path
            progress(1.0, desc="Hoàn tất tạo mesh!")
            info = f"✅ Tạo mesh thành công ({res.duration_seconds:.2f}s)!\nVertices: {res.vertex_count:,} | Faces: {res.triangle_count:,}"
            return res.mesh_path, info
        except Exception as ex:
            return None, f"❌ Lỗi Stage 1: {str(ex)}"

    def on_auto_rig(progress=gr.Progress()) -> Tuple[Optional[str], str]:
        if not session_state["current_mesh_path"]:
            return None, "❌ Vui lòng tạo mesh ở Giai đoạn 1 trước khi Auto Rig."

        progress(0.3, desc="Đang phân tích tính đối xứng & chạy UniRig...")
        try:
            rig_res = pipeline.run_auto_rig(session_state["current_mesh_path"])
            session_state["stage2_rig_result"] = rig_res

            progress(0.8, desc="Đang xuất file glTF .glb...")
            stem = Path(session_state["current_image_path"]).stem
            out_glb = str(Path(DEFAULT_CONFIG.export.output_dir) / f"{stem}_rigged.glb")
            export_res = pipeline.run_export_glb(rig_res, out_glb)

            progress(1.0, desc="Hoàn tất Auto-Rig!")
            info = f"✅ Auto Rig thành công ({rig_res.duration_seconds + export_res.duration_seconds:.2f}s)!\nBones: {len(rig_res.joint_names)} | File: {Path(out_glb).name}"
            return export_res.glb_path, info
        except Exception as ex:
            return None, f"❌ Lỗi Stage 2: {str(ex)}"

    def on_run_full_pipeline(image_pil: Optional[Image.Image], progress=gr.Progress()) -> Tuple[Optional[str], Optional[str], str, str]:
        if image_pil is None:
            return None, None, "❌ Vui lòng tải ảnh lên.", "{}"

        on_image_uploaded(image_pil)
        progress(0.05, desc="Bắt đầu toàn trình Image-to-Rig...")

        def update_cb(p: float, s: str):
            progress(p, desc=s)

        res = pipeline.run_pipeline(
            image_path=session_state["current_image_path"],
            progress_cb=update_cb,
        )

        if not res.success:
            return None, None, f"❌ Pipeline thất bại: {res.error_message}", "{}"

        meta_str = json.dumps(res.metadata, indent=2) if res.metadata else "{}"
        log_msg = (
            f"🎉 Toàn trình hoàn tất thành công trong {res.total_time_seconds:.2f} giây!\n"
            f"- Model GLB: {Path(res.glb_path).name}\n"
            f"- Số xương: {res.metadata.get('bone_count', 0)}\n"
            f"- Dung lượng: {res.metadata.get('file_size_bytes', 0) / (1024*1024):.2f} MB\n"
        )
        if res.face_scaffold:
            log_msg += f"\n💡 {res.face_scaffold.instruction_note}"

        return res.glb_path, res.glb_path, log_msg, meta_str

    def get_hardware_status() -> str:
        vram = GPUManager.get_vram_status_mb()
        return (
            f"🖥️ Thiết bị: {GPUManager.get_device_name()}\n"
            f"⚡ CUDA Sẵn sàng: {GPUManager.is_cuda_available()}\n"
            f"📊 VRAM Đã cấp phát: {vram['allocated_mb']} MB / {vram['total_mb']} MB\n"
            f"💾 VRAM Trống: {vram['free_mb']} MB"
        )

    def on_select_template(template_key: str) -> Tuple[str, str, str]:
        item = PROMPT_TEMPLATES.get(template_key, {})
        desc = item.get("description", "")
        prompt = item.get("prompt", "")
        neg = item.get("negative_prompt", "")
        return desc, prompt, neg

    # Gradio Blocks UI Layout
    with gr.Blocks(title="Studio Image-to-Rig Pipeline", theme=gr.themes.Soft()) as demo:
        gr.Markdown(
            """
            # 🎨 Studio Image-to-Rig Pipeline (TripoSR + UniRig)
            **Tự động hóa chuyển đổi ảnh nhân vật 2D thành model 3D có sẵn Skeleton & Skinning Weights (.glb) cho Three.js.**
            *Tối ưu cho GPU NVIDIA RTX 3060 12GB VRAM & Quản lý Model trong thư mục `models/`.*
            """
        )

        with gr.Tabs():
            # TAB 1: Image to Rig Main
            with gr.TabItem("🚀 1. Tạo Model 3D & Auto-Rig"):
                with gr.Row():
                    with gr.Column(scale=1):
                        input_image = gr.Image(type="pil", label="1. Tải ảnh nhân vật 2D (PNG / JPG / WEBP)")
                        
                        with gr.Accordion("💡 Hướng dẫn nhanh: Chuẩn ảnh tạo 3D & Rigging", open=False):
                            gr.Markdown(QUICK_GUIDE_MARKDOWN)

                        validation_box = gr.Textbox(label="Kiểm tra ảnh đầu vào", interactive=False, lines=2)
                        
                        with gr.Row():
                            btn_stage1 = gr.Button("🔨 Giai đoạn 1: Tạo Mesh", variant="secondary")
                            btn_stage2 = gr.Button("🦴 Giai đoạn 2: Auto Rig", variant="secondary")

                        btn_full = gr.Button("⚡ Chạy Toàn Trình (Generate & Rig)", variant="primary", scale=2)

                    with gr.Column(scale=1):
                        model_3d_preview = gr.Model3D(label="Xem trước 3D Mesh / Rigged GLB", clear_color=[0.1, 0.1, 0.1, 1.0])
                        download_glb = gr.File(label="Tải file .glb hoàn chỉnh")
                        log_output = gr.Textbox(label="Nhật ký tiến trình (Stage Logs)", interactive=False, lines=5)

                with gr.Accordion("📋 Metadata JSON Asset (Dành cho Asset Catalog Scanner)", open=False):
                    metadata_display = gr.Code(label="metadata.json", language="json")

            # TAB 2: AI Prompt Guide
            with gr.TabItem("📖 2. Hướng Dẫn Prompt & Chuẩn Ảnh AI"):
                gr.Markdown("### 🎯 Kho Mẫu Prompt AI Chuẩn (Sẵn Sàng Sao Chép)")
                with gr.Row():
                    tpl_dropdown = gr.Dropdown(
                        label="Chọn phong cách / mẫu nhân vật",
                        choices=get_template_choices(),
                        value="nam_casual",
                    )
                
                tpl_desc = gr.Markdown(value=PROMPT_TEMPLATES["nam_casual"]["description"])
                with gr.Row():
                    tpl_prompt = gr.Textbox(
                        label="Prompt Tiếng Anh (Dành cho Midjourney / SD / Flux)",
                        value=PROMPT_TEMPLATES["nam_casual"]["prompt"],
                        lines=4,
                    )
                    tpl_negative = gr.Textbox(
                        label="Negative Prompt",
                        value=PROMPT_TEMPLATES["nam_casual"]["negative_prompt"],
                        lines=4,
                    )

                tpl_dropdown.change(
                    fn=on_select_template,
                    inputs=[tpl_dropdown],
                    outputs=[tpl_desc, tpl_prompt, tpl_negative],
                )

                gr.Markdown(FULL_GUIDE_MARKDOWN)

            # TAB 3: VRM Branch A
            with gr.TabItem("👤 3. Chuyển Đổi VRoid / VRM"):
                gr.Markdown(
                    """
                    ### Nhánh A: Nhân vật từ VRoid Studio (.vrm)
                    File `.vrm` đã chứa sẵn hơn 52 chuẩn ARKit Blendshapes (nháy mắt, lipsync khẩu hình `a/i/u/e/o`, cảm xúc).
                    Module sẽ giữ nguyên toàn bộ morph targets này khi chuyển đổi sang `.glb`.
                    """
                )
                vrm_input = gr.File(label="Tải file nhân vật .vrm")
                vrm_btn = gr.Button("Trích xuất & Bảo toàn Blendshapes", variant="primary")
                vrm_output = gr.Textbox(label="Kết quả VRM", interactive=False)

            # TAB 4: Face Blendshape Helper Branch B
            with gr.TabItem("🎭 4. Sculpt Biểu Cảm Khuôn Mặt (Blender)"):
                gr.Markdown(
                    """
                    ### ⚠️ Lưu ý kỹ thuật quan trọng về TripoSR & UniRig:
                    TripoSR + UniRig tạo ra mesh và xương mềm toàn thân, **KHÔNG** tự động tạo morph targets cho biểu cảm khuôn mặt.
                    
                    **Quy trình bán tự động (Semi-automatic):**
                    1. Pipeline đã tự động tách vùng đầu/khuôn mặt thành mesh scaffold.
                    2. Mở Blender và chạy script hỗ trợ `setup_shapekeys.py` đính kèm.
                    3. Nghệ sĩ 3D chỉ cần dùng Sculpt Mode chỉnh nhanh 3 shape keys cơ bản (`mouth_open`, `eye_blink`, `smile`) trong 2-3 phút.
                    """
                )
                gr.File(label="Tải file Blender Sculpt Helper Script")

            # TAB 5: Models Downloader & Manager
            with gr.TabItem("📦 5. Quản Lý & Tải Model (models/)"):
                build_models_tab()

            # TAB 6: GPU Hardware Monitor
            with gr.TabItem("⚙️ 6. Giám Sát Phần Cứng GPU"):
                hw_status = gr.Textbox(value=get_hardware_status, label="Trạng thái GPU & VRAM", interactive=False, lines=5)
                btn_refresh_hw = gr.Button("🔄 Làm mới thông số VRAM")
                btn_refresh_hw.click(fn=get_hardware_status, outputs=[hw_status])

        # Event Bindings
        input_image.change(fn=on_image_uploaded, inputs=[input_image], outputs=[validation_box, log_output])
        btn_stage1.click(fn=on_generate_mesh, inputs=[input_image], outputs=[model_3d_preview, log_output])
        btn_stage2.click(fn=on_auto_rig, outputs=[model_3d_preview, log_output])
        btn_full.click(
            fn=on_run_full_pipeline,
            inputs=[input_image],
            outputs=[model_3d_preview, download_glb, log_output, metadata_display],
        )

    return demo
