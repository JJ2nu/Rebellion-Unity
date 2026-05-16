import bpy, os, sys

input_dir = r"D:\Projects\Rebellion\Assets\05_Models"
output_dir = r"D:\Projects\Rebellion\Assets\05_Models_FBX"

os.makedirs(output_dir, exist_ok=True)

for root, dirs, files in os.walk(input_dir):
    for file in files:
        if file.endswith(".glb"):
            glb_path = os.path.join(root, file)
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.gltf(filepath=glb_path)
            
            out_path = os.path.join(output_dir, file.replace(".glb", ".fbx"))
            bpy.ops.export_scene.fbx(filepath=out_path, use_selection=False)
            print(f"변환 완료: {file}")