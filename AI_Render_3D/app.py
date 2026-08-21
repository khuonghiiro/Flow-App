"""
Main entrypoint to launch Gradio Web UI.
Run: python app.py
"""

import os
import sys

# Add project root to Python module search path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from image_to_rig.ui.gradio_app import create_gradio_app
from image_to_rig.utils.logger import get_logger

if __name__ == "__main__":
    logger = get_logger()
    logger.info("Starting Gradio Web Application on http://localhost:7860 ...")
    
    app = create_gradio_app()
    app.launch(
        server_name="0.0.0.0",
        server_port=7860,
        share=False,
        show_error=True,
    )
