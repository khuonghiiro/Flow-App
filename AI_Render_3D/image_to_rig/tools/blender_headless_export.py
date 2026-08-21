"""
Headless Blender glTF Export Automation Script.
Usage:
    blender --background --python blender_headless_export.py -- --mesh input.obj --output output.glb
"""

import sys
from pathlib import Path


def parse_args():
    args = sys.argv
    if "--" not in args:
        return {}
    params = {}
    cli_args = args[args.index("--") + 1 :]
    for i in range(0, len(cli_args), 2):
        if i + 1 < len(cli_args) and cli_args[i].startswith("--"):
            key = cli_args[i].lstrip("-")
            params[key] = cli_args[i + 1]
    return params


def main():
    params = parse_args()
    mesh_file = params.get("mesh")
    output_file = params.get("output", "character_export.glb")

    if not mesh_file or not Path(mesh_file).exists():
        print(f"[ERROR] Invalid or missing mesh file: {mesh_file}")
        sys.exit(1)

    try:
        import bpy

        # Clear existing scene
        bpy.ops.wm.read_factory_settings(use_empty=True)

        # Import OBJ
        bpy.ops.wm.obj_import(filepath=str(Path(mesh_file).resolve()))
        mesh_obj = bpy.context.selected_objects[0]
        bpy.context.view_layer.objects.active = mesh_obj

        # Export standard glTF binary
        out_path = Path(output_file).resolve()
        out_path.parent.mkdir(parents=True, exist_ok=True)

        bpy.ops.export_scene.gltf(
            filepath=str(out_path),
            export_format="GLB",
            export_apply=True,
            export_skins=True,
            export_morph=True,
        )
        print(f"[SUCCESS] Exported glTF to {out_path}")

    except ImportError:
        print("[ERROR] Script must be run inside Blender's python runtime.")
        sys.exit(1)


if __name__ == "__main__":
    main()
