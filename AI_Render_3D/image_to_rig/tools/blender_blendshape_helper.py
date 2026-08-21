"""
Blender Add-on / Script Helper for Rapid Facial Blendshape Sculpting.
Sets up ARKit 52 / Minimal Viseme Shape Keys on any head mesh with 1 click.
"""

import sys

STANDARD_ARKit_KEYS = [
    "eyeBlinkLeft",
    "eyeBlinkRight",
    "jawOpen",
    "mouthSmileLeft",
    "mouthSmileRight",
    "mouthPucker",
    "mouthFunnel",
    "browInnerUp",
    "browDownLeft",
    "browDownRight",
]


def setup_facial_shape_keys(target_object_name: str = None):
    """Create basis and ARKit shape keys on active mesh object in Blender."""
    try:
        import bpy
    except ImportError:
        print("[ERROR] Must be executed inside Blender Python environment.")
        return

    obj = bpy.context.active_object if target_object_name is None else bpy.data.objects.get(target_object_name)

    if not obj or obj.type != "MESH":
        print("[ERROR] Please select an active Mesh object.")
        return

    # Ensure Basis key exists
    if not obj.data.shape_keys:
        obj.shape_key_add(name="Basis")

    created = []
    for key_name in STANDARD_ARKit_KEYS:
        if key_name not in obj.data.shape_keys.key_blocks:
            sk = obj.shape_key_add(name=key_name, from_mix=False)
            sk.value = 0.0
            created.append(key_name)

    print(f"[SUCCESS] Created {len(created)} shape keys on '{obj.name}'.")
    print("Artists can now select a shape key, set value to 1.0, and enter Sculpt Mode.")


if __name__ == "__main__":
    setup_facial_shape_keys()
