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
    private bool activatingForShow;

    private void Awake()
    {
        BuildLookup();
        if (!activatingForShow)
        {
            Hide();
        }
    }

    public void Show(TutorialStep step, string manualContinueBindingText)
    {
        if (step == null)
        {
            Hide();
            return;
        }

        BuildLookup();
        activatingForShow = true;
        SetActive(true);
        activatingForShow = false;

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

        PlayVoice(step.voiceClip);
    }

    public void Hide()
    {
        if (voiceSource != null)
        {
            voiceSource.Stop();
        }

        SetActive(false);
    }

    public void PauseVoice()
    {
        if (voiceSource != null && voiceSource.isPlaying)
        {
            voiceSource.Pause();
        }
    }

    public void ResumeVoice()
    {
        bool visible = root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;
        if (voiceSource != null && voiceSource.clip != null && visible && voiceSource.gameObject.activeInHierarchy)
        {
            if (!voiceSource.enabled)
            {
                voiceSource.enabled = true;
            }
            voiceSource.UnPause();
        }
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

    private void PlayVoice(AudioClip clip)
    {
        if (voiceSource == null) return;

        if (voiceSource.gameObject.activeInHierarchy)
        {
            voiceSource.Stop();
        }

        voiceSource.clip = clip;
        if (clip == null) return;

        if (!voiceSource.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("TutorialPopupUI: Cannot play tutorial voice because the assigned AudioSource is inactive in the hierarchy. Keep the AudioSource active or place it under the popup root.", this);
            return;
        }

        if (!voiceSource.enabled)
        {
            voiceSource.enabled = true;
        }

        voiceSource.Play();
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
