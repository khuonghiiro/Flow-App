"""Gradio Web UI package for Image-to-Rig pipeline."""

def create_gradio_app(*args, **kwargs):
    from image_to_rig.ui.gradio_app import create_gradio_app as _create_app
    return _create_app(*args, **kwargs)

__all__ = ["create_gradio_app"]
