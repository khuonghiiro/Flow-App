import logging
from contextlib import asynccontextmanager
from pathlib import Path
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse

from app.config import settings
from app.api.endpoints.rotation import router as rotation_router
from app.api.endpoints.background import router as background_router
from app.api.endpoints.system import router as system_router

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s"
)
logger = logging.getLogger("AIImageRotate")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Lifespan context manager for startup and shutdown events."""
    logger.info(f"=======================================================")
    logger.info(f"  Starting {settings.APP_NAME} v{settings.APP_VERSION}")
    logger.info(f"  Server listening on http://0.0.0.0:{settings.PORT}")
    logger.info(f"  Swagger Docs available at http://localhost:{settings.PORT}/docs")
    logger.info(f"=======================================================")
    yield
    logger.info("Shutting down AI Image Rotate server...")


app = FastAPI(
    title=settings.APP_NAME,
    version=settings.APP_VERSION,
    description="High-performance AI Image 360 Rotation & Novel View Synthesis API optimized for RTX 3060 12GB VRAM.",
    lifespan=lifespan
)

# Enable CORS for external web applications
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Register API Routers
app.include_router(rotation_router)
app.include_router(background_router)
app.include_router(system_router)

# Mount outputs directory for downloads
app.mount("/outputs", StaticFiles(directory=str(settings.OUTPUT_DIR)), name="outputs")

# Mount static frontend
static_dir = Path(__file__).resolve().parent / "static"
if static_dir.exists():
    app.mount("/static", StaticFiles(directory=str(static_dir)), name="static")


@app.get("/", include_in_schema=False)
async def serve_index():
    """Serves the main Web UI application."""
    index_file = static_dir / "index.html"
    if index_file.exists():
        return FileResponse(str(index_file))
    return {"message": f"{settings.APP_NAME} is running on port {settings.PORT}"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app.main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
