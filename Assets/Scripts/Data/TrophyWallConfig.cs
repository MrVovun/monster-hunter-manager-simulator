using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TrophyWallConfig", menuName = "Guild Manager/Trophy Wall Config")]
public class TrophyWallConfig : ScriptableObject
{
    [Header("Layout")]
    [Tooltip("Preferred ordering of families (matches evidence tag 'family'). Unlisted families are appended alphabetically.")]
    public List<string> familyOrder = new List<string>();

    [Header("Frames & Placeholders")]
    [Tooltip("Prefab used when no kills have been recorded for a monster.")]
    public GameObject emptyPlaquePrefab;

    [Tooltip("Frame shown when kills are below the first threshold.")]
    public GameObject baseFramePrefab;

    [Tooltip("Ascending kill thresholds with their associated frame prefabs.")]
    public List<FrameTier> frameTiers = new List<FrameTier>
    {
        new FrameTier{ killThreshold = 25, framePrefab = null },
        new FrameTier{ killThreshold = 50, framePrefab = null },
        new FrameTier{ killThreshold = 100, framePrefab = null },
    };

    [System.Serializable]
    public class FrameTier
    {
        public int killThreshold = 1;
        public GameObject framePrefab;
    }
}
