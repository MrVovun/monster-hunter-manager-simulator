using UnityEngine;

[CreateAssetMenu(fileName = "GuildConstruction", menuName = "Guild Manager/Guild Construction")]
public class GuildConstructionDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string constructionId;
    public string ConstructionId => constructionId;

    [Header("Presentation")]
    public string displayName;
    [TextArea] public string description;
    public Sprite planOverlay;

    [Header("Requirements")]
    public int goldCost;
    public int requiredReputation;
    [Tooltip("If assigned, this construction only becomes visible after the prerequisite is built.")]
    public GuildConstructionDefinition prerequisite;
    [Tooltip("If true, this construction starts already built in a new save.")]
    public bool startsBuilt;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(constructionId))
        {
            constructionId = name;
        }
    }
}
