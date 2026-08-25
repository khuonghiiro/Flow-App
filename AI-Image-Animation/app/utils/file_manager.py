import time
import uuid
from pathlib import Path
from typing import Tuple
from app.config import OUTPUTS_DIR, TEMP_DIR


def generate_unique_id(prefix: str = "anim") -> str:
    """
    Generates a timestamped unique task identifier.
    """
    timestamp = int(time.time())
    short_uuid = uuid.uuid4().hex[:8]
    return f"{prefix}_{timestamp}_{short_uuid}"


def get_output_path(task_id: str, extension: str) -> Tuple[Path, str]:
    """
    Returns (absolute_file_path, relative_url_path) for an output asset.
    """
    ext = extension.lstrip(".")
    filename = f"{task_id}.{ext}"
    file_path = OUTPUTS_DIR / filename
    relative_url = f"/api/download/{filename}"
    return file_path, relative_url


def cleanup_old_files(max_age_hours: int = 24) -> int:
    """
    Removes temporary and output files older than max_age_hours.
    Returns number of deleted files.
    """
    now = time.time()
    max_age_sec = max_age_hours * 3600
    deleted_count = 0
    
    for folder in [OUTPUTS_DIR, TEMP_DIR]:
        if not folder.exists():
            continue
        for item in folder.iterdir():
            if item.is_file():
                try:
                    if now - item.stat().st_mtime > max_age_sec:
                        item.unlink()
                        deleted_count += 1
                except Exception:
                    pass
                    
    return deleted_count
