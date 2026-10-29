"""Legacy recovery exporter for the original Sister Ysolde Rigify source.

Run with Blender in background mode:
    blender --background SisterYsolde.blend --python export_sister_ysolde_legacy.py -- --output SisterYsolde.fbx

The source .blend is never saved. All hierarchy, weight-name, and triangulation changes
exist only in the temporary Blender process used for the FBX export.

New characters must start from HunterCharacterTemplate.blend and export normally.
"""

import argparse
import json
import sys
from pathlib import Path

import bmesh
import bpy


SOURCE_RIG_NAME = "rig"

# Source deform bone -> Unity-friendly export bone.
BONE_NAMES = {
    "DEF-spine": "Hips",
    "DEF-spine.001": "Spine",
    "DEF-spine.002": "Chest",
    "DEF-spine.003": "UpperChest",
    "DEF-spine.004": "NeckBase",
    "DEF-spine.005": "Neck",
    "DEF-spine.006": "Head",
    "DEF-pelvis.L": "LeftPelvis",
    "DEF-pelvis.R": "RightPelvis",
    "DEF-thigh.L": "LeftUpperLeg",
    "DEF-thigh.L.001": "LeftUpperLegTwist",
    "DEF-shin.L": "LeftLowerLeg",
    "DEF-shin.L.001": "LeftLowerLegTwist",
    "DEF-foot.L": "LeftFoot",
    "DEF-toe.L": "LeftToes",
    "DEF-thigh.R": "RightUpperLeg",
    "DEF-thigh.R.001": "RightUpperLegTwist",
    "DEF-shin.R": "RightLowerLeg",
    "DEF-shin.R.001": "RightLowerLegTwist",
    "DEF-foot.R": "RightFoot",
    "DEF-toe.R": "RightToes",
    "DEF-shoulder.L": "LeftShoulder",
    "DEF-upper_arm.L": "LeftUpperArm",
    "DEF-upper_arm.L.001": "LeftUpperArmTwist",
    "DEF-forearm.L": "LeftLowerArm",
    "DEF-forearm.L.001": "LeftLowerArmTwist",
    "DEF-hand.L": "LeftHand",
    "DEF-shoulder.R": "RightShoulder",
    "DEF-upper_arm.R": "RightUpperArm",
    "DEF-upper_arm.R.001": "RightUpperArmTwist",
    "DEF-forearm.R": "RightLowerArm",
    "DEF-forearm.R.001": "RightLowerArmTwist",
    "DEF-hand.R": "RightHand",
}

# A single humanoid hierarchy. Twist and helper bones remain between mapped joints so
# the original skinning is preserved, but Unity ignores them in the Avatar mapping.
PARENTS = {
    "Hips": None,
    "Spine": "Hips",
    "Chest": "Spine",
    "UpperChest": "Chest",
    "NeckBase": "UpperChest",
    "Neck": "NeckBase",
    "Head": "Neck",
    "LeftPelvis": "Hips",
    "RightPelvis": "Hips",
    "LeftUpperLeg": "Hips",
    "LeftUpperLegTwist": "LeftUpperLeg",
    "LeftLowerLeg": "LeftUpperLegTwist",
    "LeftLowerLegTwist": "LeftLowerLeg",
    "LeftFoot": "LeftLowerLegTwist",
    "LeftToes": "LeftFoot",
    "RightUpperLeg": "Hips",
    "RightUpperLegTwist": "RightUpperLeg",
    "RightLowerLeg": "RightUpperLegTwist",
    "RightLowerLegTwist": "RightLowerLeg",
    "RightFoot": "RightLowerLegTwist",
    "RightToes": "RightFoot",
    "LeftShoulder": "UpperChest",
    "LeftUpperArm": "LeftShoulder",
    "LeftUpperArmTwist": "LeftUpperArm",
    "LeftLowerArm": "LeftUpperArmTwist",
    "LeftLowerArmTwist": "LeftLowerArm",
    "LeftHand": "LeftLowerArmTwist",
    "RightShoulder": "UpperChest",
    "RightUpperArm": "RightShoulder",
    "RightUpperArmTwist": "RightUpperArm",
    "RightLowerArm": "RightUpperArmTwist",
    "RightLowerArmTwist": "RightLowerArm",
    "RightHand": "RightLowerArmTwist",
}


def parse_args():
    raw_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--report")
    return parser.parse_args(raw_args)


def character_meshes(source_rig):
    result = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        if any(mod.type == "ARMATURE" and mod.object == source_rig for mod in obj.modifiers):
            result.append(obj)
    return result


def build_unity_rig(source_rig):
    missing = [name for name in BONE_NAMES if name not in source_rig.data.bones]
    if missing:
        raise RuntimeError(f"Source rig is missing expected bones: {missing}")

    armature = bpy.data.armatures.new("UnityRig")
    unity_rig = bpy.data.objects.new("UnityRig", armature)
    bpy.context.scene.collection.objects.link(unity_rig)
    unity_rig.matrix_world = source_rig.matrix_world.copy()
    unity_rig.show_in_front = True

    bpy.ops.object.select_all(action="DESELECT")
    unity_rig.select_set(True)
    bpy.context.view_layer.objects.active = unity_rig
    bpy.ops.object.mode_set(mode="EDIT")

    created = {}
    for source_name, export_name in BONE_NAMES.items():
        source_bone = source_rig.data.bones[source_name]
        bone = armature.edit_bones.new(export_name)
        bone.matrix = source_bone.matrix_local.copy()
        bone.length = source_bone.length
        bone.use_deform = True
        created[export_name] = bone

    for export_name, parent_name in PARENTS.items():
        bone = created[export_name]
        bone.parent = created[parent_name] if parent_name else None
        if bone.parent and (bone.head - bone.parent.tail).length < 0.0001:
            bone.use_connect = True

    bpy.ops.object.mode_set(mode="OBJECT")
    return unity_rig


def retarget_mesh_object(mesh_obj, source_rig, unity_rig):
    # Preserve bind-space placement while changing the object parent.
    world_matrix = mesh_obj.matrix_world.copy()
    mesh_obj.parent = unity_rig
    mesh_obj.matrix_world = world_matrix

    for modifier in mesh_obj.modifiers:
        if modifier.type == "ARMATURE" and modifier.object == source_rig:
            modifier.object = unity_rig

    for group in mesh_obj.vertex_groups:
        if group.name in BONE_NAMES:
            group.name = BONE_NAMES[group.name]

    # Unity shaders sample the first UV channel (TEXCOORD0). Blender materials,
    # however, use whichever UV layer is marked active for rendering. These meshes
    # keep their original unwrap first and the packed character/face atlas second,
    # so an untouched FBX looks correct in Blender but scrambled in Unity. Copy the
    # render UVs into channel zero for the temporary export copy.
    uv_layers = mesh_obj.data.uv_layers
    render_uv = next((layer for layer in uv_layers if layer.active_render), None)
    if render_uv is not None and len(uv_layers) > 0 and render_uv != uv_layers[0]:
        uv0 = uv_layers[0]
        for source_loop, target_loop in zip(render_uv.data, uv0.data):
            target_loop.uv = source_loop.uv
        uv0.name = "UV0_Atlas"
        uv0.active_render = True
        uv_layers.active_index = 0

    # Export triangles explicitly. This avoids Unity discarding the self-intersecting
    # wrist n-gon while leaving the editable source mesh unchanged on disk.
    bm = bmesh.new()
    bm.from_mesh(mesh_obj.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(mesh_obj.data)
    bm.free()
    mesh_obj.data.update()


def fix_external_image_paths(meshes):
    # The archived source lives in SisterYsolde/Source and its textures in
    # SisterYsolde/Textures. Keep the parent fallback for older checkouts.
    character_dir = Path(bpy.data.filepath).resolve().parent.parent
    texture_dir = character_dir / "Textures"
    if not texture_dir.is_dir():
        texture_dir = character_dir
    texture_files = {
        "M_CharacterAtlas": "M_CharacterAtlas_Base_3072.psd",
        "M_Face_Eyes_Atlas": "Face_Eyes_Atlas_2x2.png",
        "M_Face_Mouth_Atlas": "Face_Mouth_Atlas_2x2.png",
    }
    for material_name, filename in texture_files.items():
        material = bpy.data.materials.get(material_name)
        filepath = texture_dir / filename
        if not material or not material.node_tree or not filepath.is_file():
            continue
        image = bpy.data.images.load(str(filepath), check_existing=True)
        image.filepath = str(filepath)
        image_node = next((node for node in material.node_tree.nodes if node.type == "TEX_IMAGE"), None)
        if image_node:
            image_node.image = image

    used_images = set()
    for mesh in meshes:
        for slot in mesh.material_slots:
            material = slot.material
            if not material or not material.use_nodes:
                continue
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    used_images.add(node.image)

    # Broken, unused packed images can make Blender's FBX exporter log misleading
    # missing-file errors, even though no exported material references them.
    for image in list(bpy.data.images):
        if image.type == "IMAGE" and image not in used_images:
            bpy.data.images.remove(image)

    fixed = {}
    for image in used_images:
        if not image.filepath:
            continue
        candidate = texture_dir / Path(image.filepath).name
        if candidate.is_file():
            image.filepath = str(candidate)
            fixed[image.name] = str(candidate)
    return fixed


def export_fbx(output, unity_rig, meshes):
    bpy.ops.object.select_all(action="DESELECT")
    unity_rig.select_set(True)
    for mesh in meshes:
        mesh.hide_set(False)
        mesh.hide_viewport = False
        mesh.hide_render = False
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = unity_rig

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        use_tspace=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        armature_nodetype="NULL",
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )


def main():
    args = parse_args()
    output = Path(args.output).resolve()
    source_rig = bpy.data.objects.get(SOURCE_RIG_NAME)
    if not source_rig or source_rig.type != "ARMATURE":
        raise RuntimeError(f"Armature object '{SOURCE_RIG_NAME}' was not found")

    meshes = character_meshes(source_rig)
    if not meshes:
        raise RuntimeError("No character meshes use the Rigify armature")

    unity_rig = build_unity_rig(source_rig)
    for mesh in meshes:
        retarget_mesh_object(mesh, source_rig, unity_rig)
    images = fix_external_image_paths(meshes)
    export_fbx(output, unity_rig, meshes)

    report = {
        "source": bpy.data.filepath,
        "output": str(output),
        "rig": unity_rig.name,
        "bones": list(PARENTS.keys()),
        "meshes": [mesh.name for mesh in meshes],
        "uv0": {
            mesh.name: mesh.data.uv_layers[0].name if mesh.data.uv_layers else None
            for mesh in meshes
        },
        "images": images,
    }
    report_path = Path(args.report).resolve() if args.report else output.with_suffix(".export.json")
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
