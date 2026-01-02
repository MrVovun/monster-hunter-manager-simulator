using System;
using UnityEngine;

[Serializable]
public class HunterDialogueQuestion
{
    [Tooltip("Unique id for this question.")]
    public string questionId;
    [TextArea(1, 3)] public string questionText;
    [TextArea(2, 4)] public string answerText;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(questionId))
        {
            questionId = Guid.NewGuid().ToString("N");
        }
    }
}
