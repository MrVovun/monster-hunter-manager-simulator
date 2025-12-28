using UnityEngine;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text promptText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (root == null)
        {
            root = gameObject;
        }
        SetVisible(false);
    }

    public void ShowPrompt(string text)
    {
        if (promptText != null)
        {
            promptText.text = text;
        }
        SetVisible(true);
    }

    public void HidePrompt(string fallback = null)
    {
        if (!string.IsNullOrEmpty(fallback) && promptText != null)
        {
            promptText.text = fallback;
        }
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
    }
}
