"""
Main entrypoint to launch Gradio Web UI.
Run: python app.py
"""

import os
import sys
import time

# Add project root to Python module search path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from image_to_rig.ui.gradio_app import create_gradio_app
from image_to_rig.utils.logger import get_logger

if __name__ == "__main__":
    logger = get_logger()
    logger.info("Starting Gradio Web Application on http://localhost:7860 ...")
    
    app = create_gradio_app()
    app.queue(default_concurrency_limit=1)
    try:
        app.launch(
            server_name="127.0.0.1",
            server_port=7860,
            share=False,
            show_error=True,
            prevent_thread_lock=True,
        )
    except OSError:
        logger.warning("Port 7860 is busy, attempting alternative port 7861...")
        app.launch(
            server_name="127.0.0.1",
            server_port=7861,
            share=False,
            show_error=True,
            prevent_thread_lock=True,
        )

    # Keep server alive indefinitely
    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        logger.info("Stopping Gradio Server...")
