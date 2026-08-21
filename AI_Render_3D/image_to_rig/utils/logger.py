"""
Structured logging module for tracking pipeline stages and performance.
Provides stage tracking, elapsed timing, and optional callback hooks.
"""

import logging
import sys
import time
from typing import Callable, List, Optional


class PipelineLogger:
    """Stage-aware structured logger for image-to-rig workflow."""

    def __init__(self, name: str = "ImageToRigPipeline"):
        self.logger = logging.getLogger(name)
        if not self.logger.handlers:
            self.logger.setLevel(logging.INFO)
            # Ensure UTF-8 stream handling on Windows
            if hasattr(sys.stdout, "reconfigure"):
                try:
                    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
                except Exception:
                    pass
            handler = logging.StreamHandler(sys.stdout)
            formatter = logging.Formatter(
                "[%(asctime)s] [%(levelname)s] [%(name)s] %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
            handler.setFormatter(formatter)
            self.logger.addHandler(handler)


        self._callbacks: List[Callable[[str, str], None]] = []
        self._stage_start_times: dict[str, float] = {}

    def register_callback(self, callback: Callable[[str, str], None]) -> None:
        """Register a callback function to receive log stream (e.g. for UI updates)."""
        self._callbacks.append(callback)

    def _broadcast(self, level: str, message: str) -> None:
        """Send message to all registered UI/streaming callbacks."""
        for cb in self._callbacks:
            try:
                cb(level, message)
            except Exception:
                pass

    def info(self, message: str) -> None:
        """Log info level message."""
        self.logger.info(message)
        self._broadcast("INFO", message)

    def warning(self, message: str) -> None:
        """Log warning level message."""
        self.logger.warning(message)
        self._broadcast("WARNING", message)

    def error(self, message: str) -> None:
        """Log error level message."""
        self.logger.error(message)
        self._broadcast("ERROR", message)

    def start_stage(self, stage_name: str) -> None:
        """Mark the beginning of a processing stage."""
        self._stage_start_times[stage_name] = time.time()
        msg = f"=== [STAGE START] {stage_name} ==="
        self.info(msg)

    def end_stage(self, stage_name: str) -> float:
        """Mark completion of a stage and report duration in seconds."""
        start_time = self._stage_start_times.pop(stage_name, time.time())
        duration = time.time() - start_time
        msg = f"=== [STAGE COMPLETED] {stage_name} (Duration: {duration:.2f}s) ==="
        self.info(msg)
        return duration


_GLOBAL_LOGGER: Optional[PipelineLogger] = None


def get_logger() -> PipelineLogger:
    """Retrieve or initialize the global pipeline logger singleton."""
    global _GLOBAL_LOGGER
    if _GLOBAL_LOGGER is None:
        _GLOBAL_LOGGER = PipelineLogger()
    return _GLOBAL_LOGGER
