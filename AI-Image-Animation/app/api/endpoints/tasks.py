from fastapi import APIRouter, HTTPException
from app.api.deps import task_manager
from app.schemas.response_models import TaskStatusResponse

router = APIRouter(prefix="/tasks", tags=["Task Management"])


@router.get("/{task_id}", response_model=TaskStatusResponse, summary="Query task progress and status")
async def get_task_status(task_id: str):
    """
    Returns current rendering progress, status ('queued', 'processing', 'completed', 'failed'), and download URLs.
    """
    task = task_manager.get_task(task_id)
    if not task:
        raise HTTPException(status_code=404, detail=f"Task '{task_id}' not found")
    return task
