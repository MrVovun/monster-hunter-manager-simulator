using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Guild Manager/Tutorial Sequence", fileName = "TutorialSequence")]
public class TutorialSequence : ScriptableObject
{
    public string sequenceId = "first_session";
    public List<TutorialStep> steps = new List<TutorialStep>();
}

[System.Serializable]
public class TutorialStep
{
    [Header("Dialogue")]
    public string stepId;
    public string speakerName = "Guild Inspector";
    public TutorialSpeakerMood speakerMood = TutorialSpeakerMood.Neutral;
    [TextArea(2, 5)] public string text;
    public Sprite portraitOverride;
    public AudioClip voiceClip;

    [Header("Progress")]
    [Tooltip("Dropdown for common tutorial events that complete this step.")]
    public TutorialEventKey completionEvent = TutorialEventKey.None;
    [Tooltip("Optional custom event ID. Used only when Completion Event is None.")]
    public string customCompletionEvent;
    [Min(1)] public int requiredEventCount = 1;
    public bool allowManualContinue;

    [Header("Restrictions")]
    [Tooltip("Dropdown list for common tutorial actions allowed during this step.")]
    public List<TutorialActionKey> allowedActions = new List<TutorialActionKey>();
    [Tooltip("Optional custom action IDs. Use only for special scene-specific tutorial gates.")]
    public List<string> allowedActionIds = new List<string>();

    [Header("Special Rules")]
    [Tooltip("For client-bell steps: use this exact order instead of random generation.")]
    public TutorialOrderDefinition forcedOrder;
    [Tooltip("For pass-time steps: force the wait UI to exactly the first active mission's remaining time.")]
    public bool forcePassTimeToFirstActiveMissionRemaining;
    [Tooltip("For monster-selection steps: only this monster can be selected.")]
    public MonsterData forcedMonsterSelection;
    [Tooltip("For hiring tutorial steps: override the posted ad duration.")]
    public bool forceHiringAdDuration;
    public float forcedHiringAdDurationSeconds = 60f;
    [Tooltip("For hiring tutorial steps: do not charge posting fee or campaign burn.")]
    public bool forceHiringAdFree;

    public string GetCompletionEventId()
    {
        string id = TutorialKeyUtility.ToId(completionEvent);
        return string.IsNullOrWhiteSpace(id) ? customCompletionEvent : id;
    }

    public bool HasAllowedActions()
    {
        if (allowedActions != null)
        {
            foreach (var action in allowedActions)
            {
                if (action != TutorialActionKey.None)
                {
                    return true;
                }
            }
        }

        if (allowedActionIds != null)
        {
            foreach (string id in allowedActionIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool AllowsAction(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return false;

        if (allowedActions != null)
        {
            foreach (var action in allowedActions)
            {
                string id = TutorialKeyUtility.ToId(action);
                if (!string.IsNullOrWhiteSpace(id) && string.Equals(id, actionId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (allowedActionIds != null)
        {
            foreach (string id in allowedActionIds)
            {
                if (!string.IsNullOrWhiteSpace(id) && string.Equals(id, actionId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public enum TutorialSpeakerMood
{
    Neutral,
    Happy,
    Worried,
    Stern,
    Surprised,
    Thinking
}
