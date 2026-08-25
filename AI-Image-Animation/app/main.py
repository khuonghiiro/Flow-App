from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from app.config import (
    SERVER_TITLE,
    SERVER_DESCRIPTION,
    SERVER_VERSION,
    STATIC_DIR,
    CORS_ORIGINS,
    CORS_ALLOW_CREDENTIALS,
    CORS_ALLOW_METHODS,
    CORS_ALLOW_HEADERS,
)
from app.api.endpoints import (
    health_router,
    presets_router,
    preview_router,
    animate_flow_router,
    animate_ai_router,
    tasks_router,
    export_router,
    models_router,
)
from app.api.websocket import router as ws_router
from app.utils.file_manager import cleanup_old_files


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup: clean old temp files
    try:
        cleanup_old_files(max_age_hours=24)
    except Exception:
        pass
    yield
    # Shutdown


app = FastAPI(
    title=SERVER_TITLE,
    description=SERVER_DESCRIPTION,
    version=SERVER_VERSION,
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc"
)

# Open CORS Middleware to allow external websites and custom UIs to call the API
app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS,
    allow_credentials=CORS_ALLOW_CREDENTIALS,
    allow_methods=CORS_ALLOW_METHODS,
    allow_headers=CORS_ALLOW_HEADERS,
)

# Register API Routers
app.include_router(health_router, prefix="/api")
app.include_router(models_router, prefix="/api")
app.include_router(presets_router, prefix="/api")
app.include_router(preview_router, prefix="/api")
app.include_router(animate_flow_router, prefix="/api")
app.include_router(animate_ai_router, prefix="/api")
app.include_router(tasks_router, prefix="/api")
app.include_router(export_router, prefix="/api")
app.include_router(ws_router)

# Mount Static Files for Built-in Studio UI
if STATIC_DIR.exists():
    app.mount("/", StaticFiles(directory=str(STATIC_DIR), html=True), name="static")
