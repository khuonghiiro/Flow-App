"""
FlowKit Loader - cleanly bridges and imports upstream flowkit modules
so that agent-veo3 inherits from flowkit without merge conflicts or breaking changes.
"""
import importlib.util
import logging
import os
import pathlib
import sys
from typing import Any

logger = logging.getLogger(__name__)

# Locate flowkit directory (sibling directory by default)
_CURRENT_DIR = pathlib.Path(__file__).resolve().parent
_VEO3_ROOT = _CURRENT_DIR.parent
_DEFAULT_FLOWKIT = _VEO3_ROOT.parent / "flowkit"

FLOWKIT_DIR = pathlib.Path(os.environ.get("FLOWKIT_DIR", _DEFAULT_FLOWKIT)).resolve()

if FLOWKIT_DIR.exists() and FLOWKIT_DIR.is_dir():
    flowkit_str = str(FLOWKIT_DIR)
    if flowkit_str not in sys.path:
        # Append so current workspace takes precedence, flowkit is fallback
        sys.path.append(flowkit_str)
        logger.debug("Added flowkit to sys.path: %s", flowkit_str)
else:
    logger.warning("Upstream flowkit directory not found at: %s", FLOWKIT_DIR)


def load_flowkit_module(rel_path: str, module_name: str) -> Any:
    """Dynamically load an upstream module from flowkit without colliding with agent-veo3.

    Args:
        rel_path: relative path within flowkit, e.g. 'agent/services/flow_client.py'
        module_name: unique module name in sys.modules, e.g. 'flowkit_base.services.flow_client'
    """
    if module_name in sys.modules:
        return sys.modules[module_name]

    full_path = FLOWKIT_DIR / rel_path
    if not full_path.exists():
        raise FileNotFoundError(f"Upstream flowkit file not found: {full_path}")

    spec = importlib.util.spec_from_file_location(module_name, str(full_path))
    if spec is None or spec.loader is None:
        raise ImportError(f"Could not create module spec for: {full_path}")

    mod = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = mod
    spec.loader.exec_module(mod)
    return mod
