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
    [Tooltip("Large image shown in the construction detail panel. Falls back to Plan Overlay if left empty.")]
    public Sprite previewImage;
    [Tooltip("Optional detail preview shown after this construction is built. Falls back to Preview Image.")]
    public Sprite builtPreviewImage;
    public Sprite planOverlay;

    [Header("Requirements")]
    public int goldCost;
    public int requiredReputation;
    [Tooltip("If assigned, this construction only becomes visible after the prerequisite is built.")]
    public GuildConstructionDefinition prerequisite;
    [Tooltip("If true, this construction starts already built in a new save.")]
    public bool startsBuilt;

    [Header("Passive Effects")]
    [Tooltip("Additional maximum hunters provided while this construction is built. 0 = no hunter capacity effect.")]
    public int hunterCapacityIncrease;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(constructionId))
        {
            constructionId = name;
        }
    }

    public Sprite GetPreviewSprite(bool built)
    {
        if (built && builtPreviewImage != null) return builtPreviewImage;
        if (previewImage != null) return previewImage;
        return planOverlay;
    }
}
