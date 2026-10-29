# Character model pipeline

Custom hunter models use the existing data-driven visual path. `HunterData.visualPrefab` is the only assignment required: `Hunter` instantiates that prefab, finds its Animator, and connects it to `SharedCharacterAnimator`. Character names must never appear in importer, setup, or runtime code.

The legacy `P09HumanoidPreset` field remains available while old modular hunters are being replaced. A custom model uses `visualPrefab` and leaves `p09VisualPreset` empty.

## Current reference assets

- `Assets/Content/3d/Characters/Template/HunterCharacterTemplate.blend` is the starting point for every new custom model.
- `Assets/Content/3d/Characters/SisterYsolde/Source/SisterYsolde.blend` is Sister Ysolde's original editable Rigify source. It is not the reusable template.
- `Assets/Content/3d/Characters/SisterYsolde/Model/SisterYsolde.fbx` is her generated production model for Unity. Do not model in the FBX.
- `Assets/Content/3d/Characters/SisterYsolde/Prefabs/SisterYsolde_Visual.prefab` is her Unity integration prefab and is what `HunterData.visualPrefab` references.
- `Tools/Blender/export_sister_ysolde_legacy.py` exists only to recover or regenerate Sister Ysolde from her old Rigify source. Do not use it for new characters.

## Asset layout

Give every custom character an isolated folder. Use the stable `hunterId`, not the display name, for the folder and asset prefix.

```text
Assets/Content/3d/Characters/<HunterId>/
  Source/
    <HunterId>.blend
  Model/
    <HunterId>.fbx
  Textures/
  Materials/
  Prefabs/
    <HunterId>_Visual.prefab
```

Do not share writable materials between characters unless they are intentionally identical. A material edited for one hunter must not change another hunter.

## One shared Blender template

Create future characters from one project-owned Blender template containing the approved Unity export skeleton. The template is the skeleton contract for every custom humanoid.

- Keep the Rigify/metarig controls for authoring if useful, but maintain a separate game/export armature in the file.
- Export only the game armature and render meshes. Never export Rigify controls, mechanisms, widgets, constraints, cameras, or lights.
- Keep export bone names and hierarchy identical between characters. Adjust bone positions to the character, but do not rename or insert arbitrary bones between required Humanoid joints.
- Model in meters with applied object transforms. Character and armature objects must export at position zero, identity rotation, and scale one.
- Skin to deform bones only, with no more than four influences per vertex.
- Keep the character in the same approved reference pose used by the base avatar.
- Put the final texture atlas in the first UV channel (`UV0`). Blender's active render UV is not enough; Unity shaders read `UV0`.
- Apply triangulation before the model reaches cloth authoring. Once MagicaCloth painting exists, changing vertex count or order invalidates its data.

Use stable mesh roles so prefabs are easy to inspect:

```text
Body
Hair
Coat
Face_EyeL
Face_EyeR
Face_Mouth
```

Additional meshes may use descriptive names such as `Boots`, `Gloves`, or `Accessory_Belt`. Hair and cloth that need simulation should be separate meshes. Their topology should have evenly spaced vertices, fixed root rows, and enough subdivisions to bend without requiring reduction.

## Face preparation

Eyes and mouth remain separate meshes with separate transparent materials. Each face mesh uses its animation atlas in `UV0`.

- Use the same atlas dimensions and cell order for every character.
- Keep the eye meshes close to the face surface without intersecting it.
- Keep the mouth mesh separate from the head and eyes.
- Do not bake a character-specific blink or speech implementation into a prefab. A shared face-animation component will drive atlas offsets for every character.

## FBX export

Export selected objects to the character's `Model` folder with these rules:

- FBX axes: Forward `-Z`, Up `Y`.
- Apply unit scale.
- Export Mesh and Armature only.
- Add Leaf Bones off.
- Only Deform Bones on.
- Bake Animation off for the character model. Animation clips are separate assets.
- Embed Textures off.
- Export the already triangulated result.

The first character needed a conversion script because its Rigify output was not a valid reusable game skeleton. That converter is recovery tooling for that source file, not the production workflow for future characters.

## Unity model import

Apply one shared `Hunter Character Model` importer preset to every custom-character FBX:

- Rig: Humanoid.
- Avatar Definition: Create From This Model.
- Confirm the Avatar is valid and the character is in the reference pose.
- Read/Write Enabled: on, required by MagicaCloth Mesh Cloth.
- Optimize Game Objects: off, required for cloth setup and reliable bone access.
- Skin Weights: four bones.
- Import Animation: off for the model FBX.
- Import Cameras and Lights: off.
- Material import: external materials; map every FBX material slot explicitly.

Do not solve import errors by adding character-specific code. Correct the Blender template, FBX, importer preset, or prefab data.

## Materials

Create Unity material assets in the character's `Materials` folder and assign them through the model importer's material remaps.

- Body and clothing: the shared project character shader or URP Lit until that shader is ready.
- Eyes and mouth: the shared transparent, unlit face shader/material setup.
- Face textures: clamp wrap mode, no mip maps unless testing proves they are needed.
- Body atlas: mip maps enabled; use the wrap mode required by the UVs.

Material remaps are serialized in the FBX `.meta` file. They do not require an editor script.

## Visual prefab

Create a prefab variant from the imported FBX and save it as `<HunterId>_Visual.prefab`.

The prefab is the character-specific integration layer. Configure all data here:

- Assign the shared character Animator Controller.
- Disable root motion.
- Keep the prefab root at local position zero, identity rotation, and scale one.
- Configure eye and mouth renderers.
- Add and paint MagicaCloth components.
- Add Magica colliders and pre-build cloth data after tuning.
- Add character-only VFX or accessories when needed.

The prefab must contain exactly one usable Humanoid Animator. Gameplay attachment points use `Animator.GetBoneTransform`, so ordinary equipment does not need per-character socket code.

## Assigning a hunter

Open the hunter's `HunterData` asset and assign the finished visual prefab to **Visual Prefab**. Clear **P09 Visual Preset** for a custom model.

That Inspector assignment is the complete model-to-hunter mapping. Do not add an `AssignModelToHunterName` method, a GUID table, a Resources naming lookup, or a character-specific editor initializer.

## Verification gate

Before assigning a model to production content, verify it in the character test scene or the live guild:

1. Humanoid Avatar is valid and all shared animations retarget correctly.
2. Root position, facing direction, and scale match existing hunters.
3. Every renderer has the intended material and `UV0` mapping.
4. Eyes and mouth display the correct atlas cell.
5. Hand and head bone attachments align correctly.
6. Coat and hair cloth remain stable during idle, walking, sitting, turning, and arm-crossing animations.
7. No missing scripts, materials, textures, or prefab references appear in the Console.
8. Reimporting the FBX preserves the prefab, material remaps, and Avatar.

## Project implementation plan

1. Build and approve the reusable Blender base file with the final export skeleton, mesh names, face planes, UV conventions, and triangulation.
2. Create the Unity `Hunter Character Model` importer preset and apply it to the character model folder.
3. Implement one shared atlas face-animation component for blinking, eye movement, and talking.
4. Add one generic selected-prefab validator for the verification gate. It may report problems but must not contain hunter names or assign hunter assets.
5. Finish Sister Ysolde's MagicaCloth setup and use her as the reference prefab.
6. Produce the next model from the template, complete the checklist, and assign its prefab through `HunterData.visualPrefab` without changing C#.
7. After all hunters use custom prefabs, remove the legacy P09 visual path in a separate migration.
