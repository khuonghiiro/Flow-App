"""
Unit tests for model downloader and prompt guide features.
Compatible with both pytest and python -m unittest.
"""

import unittest
from pathlib import Path
from image_to_rig.config import DEFAULT_CONFIG, PipelineConfig
from image_to_rig.tools.model_downloader import (
    get_models_root,
    get_model_status,
    format_bytes_size,
    download_unirig,
)
from image_to_rig.ui.prompt_guide import (
    PROMPT_TEMPLATES,
    FULL_GUIDE_MARKDOWN,
    QUICK_GUIDE_MARKDOWN,
    get_template_choices,
)


class TestModelsAndGuide(unittest.TestCase):

    def test_models_directory_config(self):
        config = PipelineConfig()
        self.assertEqual(config.models_dir, "models")
        self.assertEqual(config.triposr.local_model_dir, "models/triposr")
        self.assertEqual(config.unirig.checkpoint_dir, "models/unirig")
        self.assertEqual(config.rembg_dir, "models/rembg")

    def test_model_status_inspection(self):
        status = get_model_status()
        self.assertIn("models_root", status)
        self.assertIn("total_size_human", status)
        self.assertIn("triposr", status["components"])
        self.assertIn("unirig", status["components"])
        self.assertIn("rembg", status["components"])

    def test_format_bytes_size(self):
        self.assertEqual(format_bytes_size(0), "0 MB")
        self.assertIn("1.0 MB", format_bytes_size(1024 * 1024))
        self.assertIn("1.00 GB", format_bytes_size(1024 * 1024 * 1024))

    def test_prompt_guide_templates(self):
        self.assertGreaterEqual(len(PROMPT_TEMPLATES), 4)
        for key, tpl in PROMPT_TEMPLATES.items():
            self.assertIn("title", tpl)
            self.assertIn("prompt", tpl)
            self.assertTrue("a-pose" in tpl["prompt"].lower() or "turnaround" in tpl["prompt"].lower())
            self.assertIn("negative_prompt", tpl)

        choices = get_template_choices()
        self.assertEqual(len(choices), len(PROMPT_TEMPLATES))
        self.assertGreater(len(FULL_GUIDE_MARKDOWN), 500)
        self.assertGreater(len(QUICK_GUIDE_MARKDOWN), 50)

    def test_download_unirig_structure(self):
        res = download_unirig()
        self.assertTrue(res["success"])
        root = get_models_root()
        self.assertTrue((root / "unirig").exists())


if __name__ == "__main__":
    unittest.main()
