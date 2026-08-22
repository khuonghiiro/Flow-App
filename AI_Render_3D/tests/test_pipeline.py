"""
Integration tests for ImageToRigPipeline and GPUJobQueue.
"""

from pathlib import Path
from PIL import Image
import pytest

from image_to_rig.config import PipelineConfig
from image_to_rig.core.pipeline import ImageToRigPipeline
from image_to_rig.core.queue_manager import GPUJobQueue, JobStatus


@pytest.fixture
def temp_pipeline(tmp_path):
    config = PipelineConfig(
        temp_dir=str(tmp_path / "temp"),
    )
    config.export.output_dir = str(tmp_path / "out")
    return ImageToRigPipeline(config)


def test_full_pipeline_execution_trellis(temp_pipeline, tmp_path):
    # Create test input image
    img_path = tmp_path / "hero_trellis.png"
    img = Image.new("RGBA", (512, 512), color=(200, 200, 200, 255))
    img.save(img_path)

    progress_events = []

    def on_progress(p: float, s: str):
        progress_events.append((p, s))

    result = temp_pipeline.run_pipeline(
        image_path=str(img_path),
        progress_cb=on_progress,
        extract_face_scaffold=True,
        engine="trellis",
    )

    assert result.success is True
    assert result.glb_path is not None
    assert Path(result.glb_path).exists()
    assert result.metadata is not None
    assert result.metadata["bone_count"] > 10
    assert result.stage1_result.engine_used == "trellis"
    assert result.face_scaffold is not None
    assert len(progress_events) > 0


def test_full_pipeline_execution_hunyuan3d(temp_pipeline, tmp_path):
    # Create test input image
    img_path = tmp_path / "hero_hunyuan.png"
    img = Image.new("RGBA", (512, 512), color=(210, 210, 210, 255))
    img.save(img_path)

    result = temp_pipeline.run_pipeline(
        image_path=str(img_path),
        extract_face_scaffold=True,
        engine="hunyuan3d",
    )

    assert result.success is True
    assert result.glb_path is not None
    assert Path(result.glb_path).exists()
    assert result.stage1_result.engine_used == "hunyuan3d"
    assert result.face_scaffold is not None


def test_stage1_image_to_mesh_standalone(temp_pipeline, tmp_path):
    img_path = tmp_path / "stage1_standalone.png"
    img = Image.new("RGBA", (512, 512), color=(180, 180, 180, 255))
    img.save(img_path)

    res = temp_pipeline.run_image_to_mesh(str(img_path), engine="trellis", ss_steps=20, slat_steps=20)
    assert res.mesh_path.endswith(".glb")
    assert Path(res.mesh_path).exists()
    assert res.vertex_count > 0
    assert res.triangle_count > 0



def test_gpu_queue_serial_execution():
    queue = GPUJobQueue(max_concurrent=1)
    job1 = queue.create_job()
    job2 = queue.create_job()

    executed_order = []

    def worker_task(val):
        executed_order.append(val)
        return val * 2

    res1 = queue.run_synchronous_job(job1.job_id, worker_task, 1)
    res2 = queue.run_synchronous_job(job2.job_id, worker_task, 2)

    assert res1 == 2
    assert res2 == 4
    assert executed_order == [1, 2]
    assert job1.status == JobStatus.COMPLETED
    assert job2.status == JobStatus.COMPLETED
