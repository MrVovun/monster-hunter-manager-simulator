using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private List<MoodPortrait> moodPortraits = new List<MoodPortrait>();

    private readonly Dictionary<TutorialSpeakerMood, Sprite> portraitLookup = new Dictionary<TutorialSpeakerMood, Sprite>();

    private void Awake()
    {
        BuildLookup();
        Hide();
    }

    public void Show(TutorialStep step, string manualContinueBindingText)
    {
        if (step == null)
        {
            Hide();
            return;
        }

        BuildLookup();
        SetActive(true);

        if (speakerText != null) speakerText.text = string.IsNullOrWhiteSpace(step.speakerName) ? "Guild Inspector" : step.speakerName;
        if (bodyText != null) bodyText.text = step.text ?? string.Empty;
        if (hintText != null)
        {
            hintText.text = step.allowManualContinue
                ? $"Press {manualContinueBindingText} to continue"
                : string.Empty;
            hintText.gameObject.SetActive(step.allowManualContinue);
        }

        Sprite portrait = step.portraitOverride;
        if (portrait == null)
        {
            portraitLookup.TryGetValue(step.speakerMood, out portrait);
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.clip = step.voiceClip;
            if (step.voiceClip != null)
            {
                voiceSource.Play();
            }
        }
    }

    public void Hide()
    {
        if (voiceSource != null)
        {
            voiceSource.Stop();
        }

        SetActive(false);
    }

    private void SetActive(bool value)
    {
        if (root != null)
        {
            root.SetActive(value);
        }
        else
        {
            gameObject.SetActive(value);
        }
    }

    private void BuildLookup()
    {
        portraitLookup.Clear();
        foreach (var entry in moodPortraits)
        {
            if (entry == null || entry.portrait == null) continue;
            portraitLookup[entry.mood] = entry.portrait;
        }
    }

    [System.Serializable]
    public class MoodPortrait
    {
        public TutorialSpeakerMood mood;
        public Sprite portrait;
    }
}
