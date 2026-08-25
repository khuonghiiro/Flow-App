import asyncio
from typing import Dict, Any, Optional, Set
from fastapi import WebSocket
from app.schemas.response_models import TaskStatusResponse


class TaskManager:
    """
    Thread-safe task registry with WebSocket broadcaster for live progress.
    """
    def __init__(self):
        self.tasks: Dict[str, Dict[str, Any]] = {}
        self.listeners: Dict[str, Set[WebSocket]] = {}
        self._lock = asyncio.Lock()

    async def create_task(self, task_id: str, initial_message: str = "Task queued") -> Dict[str, Any]:
        async with self._lock:
            task_data = {
                "task_id": task_id,
                "status": "queued",
                "progress": 0.0,
                "message": initial_message,
                "result_url": None,
                "video_url": None,
                "gif_url": None,
                "duration_seconds": None,
                "file_size_bytes": None,
                "error": None
            }
            self.tasks[task_id] = task_data
            return task_data

    async def update_progress(
        self,
        task_id: str,
        progress: float,
        message: str = "",
        status: str = "processing"
    ):
        async with self._lock:
            if task_id in self.tasks:
                self.tasks[task_id]["progress"] = round(progress, 3)
                self.tasks[task_id]["status"] = status
                if message:
                    self.tasks[task_id]["message"] = message
                    
        await self._notify_subscribers(task_id)

    async def complete_task(
        self,
        task_id: str,
        result_url: str,
        video_url: Optional[str] = None,
        gif_url: Optional[str] = None,
        duration: Optional[float] = None,
        file_size: Optional[int] = None
    ):
        async with self._lock:
            if task_id in self.tasks:
                self.tasks[task_id].update({
                    "status": "completed",
                    "progress": 1.0,
                    "message": "Animation rendered successfully",
                    "result_url": result_url,
                    "video_url": video_url or result_url,
                    "gif_url": gif_url,
                    "duration_seconds": duration,
                    "file_size_bytes": file_size
                })
                
        await self._notify_subscribers(task_id)

    async def fail_task(self, task_id: str, error_message: str):
        async with self._lock:
            if task_id in self.tasks:
                self.tasks[task_id].update({
                    "status": "failed",
                    "progress": 1.0,
                    "message": "Task failed",
                    "error": error_message
                })
                
        await self._notify_subscribers(task_id)

    def get_task(self, task_id: str) -> Optional[TaskStatusResponse]:
        task_data = self.tasks.get(task_id)
        if task_data:
            return TaskStatusResponse(**task_data)
        return None

    async def subscribe(self, task_id: str, websocket: WebSocket):
        if task_id not in self.listeners:
            self.listeners[task_id] = set()
        self.listeners[task_id].add(websocket)

    async def unsubscribe(self, task_id: str, websocket: WebSocket):
        if task_id in self.listeners and websocket in self.listeners[task_id]:
            self.listeners[task_id].remove(websocket)

    async def _notify_subscribers(self, task_id: str):
        if task_id not in self.listeners or task_id not in self.tasks:
            return
            
        data = self.tasks[task_id]
        closed_sockets = set()
        
        for ws in self.listeners[task_id]:
            try:
                await ws.send_json(data)
            except Exception:
                closed_sockets.add(ws)
                
        for ws in closed_sockets:
            self.listeners[task_id].remove(ws)


task_manager = TaskManager()
