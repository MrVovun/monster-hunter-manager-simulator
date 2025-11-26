using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Guild Manager/Monster")]
public class MonsterData : ScriptableObject
{
    [Header("Info")]
    public string monsterId;
    public string displayName;
    public Sprite portrait;

    [Header("Traits")]
    public List<MonsterTrait> traits = new List<MonsterTrait>();

    [Header("Selection Weight")]
    [Tooltip("Higher weight = more likely to be chosen.")]
    public int weight = 1;

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
        if (string.IsNullOrWhiteSpace(monsterId))
        {
            monsterId = Guid.NewGuid().ToString("N");
        }
    }
}
