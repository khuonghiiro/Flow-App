from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from app.api.deps import task_manager

router = APIRouter(tags=["WebSocket Stream"])


@router.websocket("/ws/progress/{task_id}")
async def websocket_task_progress(websocket: WebSocket, task_id: str):
    """
    WebSocket endpoint broadcasting real-time progress updates for a specific task.
    """
    await websocket.accept()
    await task_manager.subscribe(task_id, websocket)
    
    # Send initial state immediately if exists
    task = task_manager.get_task(task_id)
    if task:
        await websocket.send_json(task.model_dump())
        
    try:
        while True:
            # Keep socket open and receive any ping / cancel messages
            data = await websocket.receive_text()
            if data == "ping":
                await websocket.send_text("pong")
    except WebSocketDisconnect:
        await task_manager.unsubscribe(task_id, websocket)
    except Exception:
        await task_manager.unsubscribe(task_id, websocket)
