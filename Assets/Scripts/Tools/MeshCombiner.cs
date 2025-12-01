using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MeshCombiner : MonoBehaviour
{
    [Tooltip("Combine only direct children. Leave unchecked to combine all descendants.")]
    public bool combineOnlyDirectChildren = true;

    [Tooltip("Disable source renderers after combining.")]
    public bool disableSourceRenderers = true;

    [ContextMenu("Combine Meshes")]
    public void CombineMeshes()
    {
        MeshFilter[] meshFilters = combineOnlyDirectChildren
            ? GetComponentsInChildren<MeshFilter>(includeInactive: false)
            : GetComponentsInChildren<MeshFilter>(includeInactive: true);

        List<CombineInstance> combines = new List<CombineInstance>();
        Material sharedMaterial = null;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null || mf.transform == transform) continue;

            var renderer = mf.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) continue;

            if (sharedMaterial == null)
            {
                sharedMaterial = renderer.sharedMaterial;
            }
            else if (renderer.sharedMaterial != sharedMaterial)
            {
                Debug.LogWarning("MeshCombiner: Multiple materials detected. Skipping " + mf.name);
                continue;
            }

            CombineInstance ci = new CombineInstance
            {
                mesh = mf.sharedMesh,
                transform = mf.transform.localToWorldMatrix
            };
            combines.Add(ci);

            if (disableSourceRenderers)
            {
                renderer.enabled = false;
            }
        }

        if (combines.Count == 0)
        {
            Debug.LogWarning("MeshCombiner: No meshes found to combine.");
            return;
        }

        MeshFilter targetFilter = GetComponent<MeshFilter>();
        if (targetFilter == null) targetFilter = gameObject.AddComponent<MeshFilter>();

        MeshRenderer targetRenderer = GetComponent<MeshRenderer>();
        if (targetRenderer == null) targetRenderer = gameObject.AddComponent<MeshRenderer>();

        Mesh combinedMesh = new Mesh();
        combinedMesh.name = name + "_CombinedMesh";
        combinedMesh.CombineMeshes(combines.ToArray());
        targetFilter.sharedMesh = combinedMesh;
        targetRenderer.sharedMaterial = sharedMaterial;

        Debug.Log($"MeshCombiner: Combined {combines.Count} meshes into {combinedMesh.name}");
    }
}
