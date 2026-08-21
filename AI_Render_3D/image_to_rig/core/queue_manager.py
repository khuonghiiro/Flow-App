"""
Thread-safe single-worker Job Queue and GPU Mutex Manager.
Guarantees strictly serial GPU processing to protect 12GB VRAM from out-of-memory errors.
"""

import threading
import time
import uuid
from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, Optional


class JobStatus(str, Enum):
    """Enumeration of pipeline job states."""
    PENDING = "PENDING"
    RUNNING = "RUNNING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    CANCELLED = "CANCELLED"


@dataclass
class JobInfo:
    """Metadata and execution status for a pipeline job."""
    job_id: str
    created_at: float = field(default_factory=time.time)
    started_at: Optional[float] = None
    completed_at: Optional[float] = None
    status: JobStatus = JobStatus.PENDING
    progress: float = 0.0
    current_stage: str = "Initialized"
    result: Optional[Any] = None
    error_message: Optional[str] = None


class GPUJobQueue:
    """Thread-safe single-worker job queue with GPU execution lock."""

    def __init__(self, max_concurrent: int = 1):
        self._lock = threading.Lock()
        self._gpu_semaphore = threading.Semaphore(max_concurrent)
        self._jobs: Dict[str, JobInfo] = {}

    def create_job(self) -> JobInfo:
        """Create and register a new pending job with a unique UUID."""
        with self._lock:
            job_id = str(uuid.uuid4())[:8]
            job = JobInfo(job_id=job_id)
            self._jobs[job_id] = job
            return job

    def get_job(self, job_id: str) -> Optional[JobInfo]:
        """Fetch job info by identifier."""
        with self._lock:
            return self._jobs.get(job_id)

    def update_job_progress(
        self,
        job_id: str,
        progress: float,
        stage: str,
    ) -> None:
        """Update the progress ratio (0.0 to 1.0) and stage description of a job."""
        with self._lock:
            job = self._jobs.get(job_id)
            if job:
                job.progress = max(0.0, min(1.0, progress))
                job.current_stage = stage

    def run_synchronous_job(
        self,
        job_id: str,
        task_fn: Callable[..., Any],
        *args: Any,
        **kwargs: Any,
    ) -> Any:
        """
        Execute a heavy GPU task within the GPU mutex lock.
        Blocks until GPU lock is acquired and job finishes.
        """
        job = self.get_job(job_id)
        if not job:
            raise ValueError(f"Job ID {job_id} not found in queue.")

        acquired = self._gpu_semaphore.acquire(blocking=True)
        try:
            with self._lock:
                job.status = JobStatus.RUNNING
                job.started_at = time.time()
                job.current_stage = "GPU Lock Acquired, processing..."

            result = task_fn(*args, **kwargs)

            with self._lock:
                job.status = JobStatus.COMPLETED
                job.completed_at = time.time()
                job.progress = 1.0
                job.current_stage = "Finished"
                job.result = result
            return result

        except Exception as ex:
            with self._lock:
                job.status = JobStatus.FAILED
                job.completed_at = time.time()
                job.error_message = str(ex)
                job.current_stage = f"Error: {ex}"
            raise ex
        finally:
            if acquired:
                self._gpu_semaphore.release()


_GLOBAL_QUEUE: Optional[GPUJobQueue] = None


def get_gpu_queue() -> GPUJobQueue:
    """Retrieve singleton instance of GPU job queue."""
    global _GLOBAL_QUEUE
    if _GLOBAL_QUEUE is None:
        _GLOBAL_QUEUE = GPUJobQueue(max_concurrent=1)
    return _GLOBAL_QUEUE
