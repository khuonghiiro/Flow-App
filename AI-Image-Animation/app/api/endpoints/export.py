import os
from fastapi import APIRouter, HTTPException
from fastapi.responses import FileResponse
from app.config import OUTPUTS_DIR

router = APIRouter(prefix="/download", tags=["File Export"])


@router.get("/{filename}", summary="Download rendered animation file")
async def download_file(filename: str):
    """
    Downloads rendered MP4, WebM, GIF, or APNG files by filename.
    """
    # Prevent directory traversal
    clean_filename = os.path.basename(filename)
    file_path = OUTPUTS_DIR / clean_filename
    
    if not file_path.exists() or not file_path.is_file():
        raise HTTPException(status_code=404, detail="Requested file does not exist or has expired")
        
    ext = os.path.splitext(clean_filename)[1].lower()
    media_types = {
        ".mp4": "video/mp4",
        ".webm": "video/webm",
        ".gif": "image/gif",
        ".png": "image/png",
        ".apng": "image/apng",
    }
    media_type = media_types.get(ext, "application/octet-stream")
    
    return FileResponse(
        path=file_path,
        media_type=media_type,
        filename=clean_filename
    )
