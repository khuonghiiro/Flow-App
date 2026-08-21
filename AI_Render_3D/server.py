"""
Main entrypoint to launch FastAPI REST Server.
Run: python server.py
"""

import os
import sys
import uvicorn

# Add project root to Python module search path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from image_to_rig.api.server import create_api_app
from image_to_rig.utils.logger import get_logger

app = create_api_app()

if __name__ == "__main__":
    logger = get_logger()
    logger.info("Starting FastAPI REST API server on http://localhost:8000 ...")
    logger.info("Swagger Documentation available at http://localhost:8000/docs")

    uvicorn.run(
        "server:app",
        host="0.0.0.0",
        port=8000,
        reload=False,
        workers=1,  # Strict single-worker to prevent GPU VRAM concurrency issues
    )
