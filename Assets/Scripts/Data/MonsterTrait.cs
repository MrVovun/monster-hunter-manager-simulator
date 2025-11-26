using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterTrait", menuName = "Guild Manager/Monster Trait")]
public class MonsterTrait : ScriptableObject
{
    [Header("Identifiers")]
    public string traitId;
    public string displayName;

    [Header("Description")]
    [TextArea(2, 4)] public string description;

    private void OnEnable()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            traitId = Guid.NewGuid().ToString("N");
        }
    }
}
