using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Febucci.TextAnimatorForUnity;

public class InvestigationDialogueUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform questionsList;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button viewOrderButton;
    [SerializeField] private TypewriterComponent responseTypewriter;
    [SerializeField] private TMP_Text responseFallbackText;
    [SerializeField] private GameObject questionItemPrefab;
    [SerializeField] private Color previouslyAskedQuestionColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private readonly List<QuestionEntry> questionEntries = new List<QuestionEntry>();
    private readonly HashSet<string> askedQuestionIds = new HashSet<string>();
    private InvestigationManager currentManager;
    private InvestigationCase currentCase;
    private System.Action onClose;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool cursorCaptured;
    private Coroutine responseRoutine;
    private bool waitingForResponse;
    private bool awaitingTypewriterCompletion;

    private CanvasGroup questionsCanvasGroup;

    private InvestigationQuestion lastQuestion;
    private string lastResponse;

    private void Awake()
    {
        SetActive(false);
        if (responseTypewriter != null)
        {
            responseTypewriter.onTextShowed.AddListener(HandleTypewriterCompleted);
        }

        if (questionsList != null)
        {
            questionsCanvasGroup = questionsList.GetComponent<CanvasGroup>();
            if (questionsCanvasGroup == null)
            {
                questionsCanvasGroup = questionsList.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private string GetClientOpeningLine()
    {
        if (currentManager != null && currentManager.IsHunterDialogueActive)
        {
            return currentManager.GetHunterGreeting();
        }
        var order = currentManager != null ? currentManager.CurrentOrder : null;
        if (order == null)
        {
            return "...";
        }

        string line = order.GetDescriptionFor(Order.DescriptionAudience.Client);
        return string.IsNullOrWhiteSpace(line) ? "..." : line;
    }

    public void Show(InvestigationCase caseData, InvestigationManager manager, System.Action closeCallback)
    {
        currentCase = caseData;
        currentManager = manager;
        onClose = closeCallback;
        askedQuestionIds.Clear();

        RefreshQuestions();
        HookButtons();
        PlayResponse(GetClientOpeningLine(), false);
        SetActive(true);
        CaptureCursor();
    }

    private void HookButtons()
    {
        var viewButton = viewOrderButton != null ? viewOrderButton : acceptButton;
        bool hunterDialogue = currentManager != null && currentManager.IsHunterDialogueActive;
        if (viewButton != null)
        {
            viewButton.onClick.RemoveAllListeners();
            viewButton.onClick.AddListener(HandleViewOrder);
            viewButton.gameObject.SetActive(!hunterDialogue);
        }

        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() =>
            {
                currentManager?.CompleteInvestigation();
                Close();
            });
            declineButton.gameObject.SetActive(!hunterDialogue);
        }

        if (acceptButton != null)
        {
            acceptButton.gameObject.SetActive(!hunterDialogue);
        }
    }

    private void OnDestroy()
    {
        if (responseTypewriter != null)
        {
            responseTypewriter.onTextShowed.RemoveListener(HandleTypewriterCompleted);
        }
    }

    private void HandleViewOrder()
    {
        if (currentManager == null) return;
        ReleaseCursor();
        SetActive(false);
        currentManager.ShowOrderDetails(() =>
        {
            if (currentManager != null && currentManager.CurrentCase != null && currentCase == currentManager.CurrentCase)
            {
                Reopen();
            }
        });
    }

    private void RefreshQuestions()
    {
        foreach (var entry in questionEntries)
        {
            if (entry?.root != null)
            {
                Destroy(entry.root);
            }
        }
        questionEntries.Clear();

        if (questionItemPrefab == null || questionsList == null || currentManager == null) return;

        var available = currentManager.GetAvailableQuestions();
        foreach (var question in available)
        {
            var entryObj = Instantiate(questionItemPrefab, questionsList);
            var button = entryObj.GetComponentInChildren<Button>();
            var text = entryObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = question.promptText;
            }

            if (button != null)
            {
                EnsureButtonVisualFeedback(button);
                var capturedQuestion = question;
                button.onClick.AddListener(() => HandleQuestionClicked(capturedQuestion));
                button.interactable = !waitingForResponse;
                ApplyQuestionVisualState(button, question);
            }

            questionEntries.Add(new QuestionEntry
            {
                root = entryObj,
                button = button,
                question = question
            });
        }
    }

    private void HandleQuestionClicked(InvestigationQuestion question)
    {
        if (waitingForResponse || question == null || currentManager == null)
        {
            return;
        }

        InteractionFeedbackManager.PlayUIClick();
        MarkQuestionAsked(question);

        // Advance action-based time for asking a question
        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (tm != null)
        {
            float duration = currentManager.GetQuestionActionDuration(question);
            tm.AdvanceTime(duration);
        }

        if (responseRoutine != null)
        {
            StopCoroutine(responseRoutine);
        }

        responseRoutine = StartCoroutine(PlayResponseRoutine(question));
    }

    private IEnumerator PlayResponseRoutine(InvestigationQuestion question)
    {
        waitingForResponse = true;
        SetQuestionsInteractable(false);
        var managerAtStart = currentManager;

        float waitTime = Mathf.Max(0f, managerAtStart.GetResponseDelaySeconds());
        if (waitTime > 0f)
        {
            managerAtStart.PlayClientThinkingAnimation();
            yield return new WaitForSecondsRealtime(waitTime);
        }

        string response = managerAtStart.ResolveQuestion(question);
        if (currentManager != managerAtStart)
        {
            responseRoutine = null;
            yield break;
        }

        lastQuestion = question;
        lastResponse = response;
        if (string.IsNullOrWhiteSpace(response))
        {
            response = "...";
        }

        RefreshQuestions();
        SetQuestionsInteractable(false);
        currentManager?.PlayClientSpeakingAnimation();
        PlayResponse(response, true);
        responseRoutine = null;
    }

    private void PlayResponse(string text, bool lockDuringPlayback)
    {
        bool played = false;
        bool shouldLock = lockDuringPlayback && responseTypewriter != null;

        if (responseTypewriter != null)
        {
            try
            {
                responseTypewriter.ShowText(text);
                responseTypewriter.StartShowingText();
                awaitingTypewriterCompletion = shouldLock;
                played = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"InvestigationDialogueUI: Unable to play typewriter response. {ex.Message}", this);
            }
        }

        if (!played && responseFallbackText != null)
        {
            responseFallbackText.text = text;
        }

        if (!shouldLock)
        {
            CompleteResponsePlayback();
        }
    }

    public void Reopen()
    {
        RefreshQuestions();
        SetActive(true);
        CaptureCursor();
    }

    public void Close(bool invokeCallback = true)
    {
        if (responseRoutine != null)
        {
            StopCoroutine(responseRoutine);
            responseRoutine = null;
        }
        SetActive(false);
        ReleaseCursor();
        waitingForResponse = false;
        awaitingTypewriterCompletion = false;
        if (responseTypewriter != null)
        {
            responseTypewriter.StopShowingText();
        }
        SetQuestionsInteractable(true);
        currentManager?.StopClientTalkingAnimation();
        if (invokeCallback)
        {
            onClose?.Invoke();
        }
        onClose = null;
        currentManager = null;
        currentCase = null;
        lastQuestion = null;
        lastResponse = null;
        askedQuestionIds.Clear();
    }

    public void ReopenWithResponse(string text)
    {
        Reopen();
        if (responseTypewriter != null)
        {
            responseTypewriter.StopShowingText();
        }
        if (responseFallbackText != null)
        {
            responseFallbackText.text = string.IsNullOrWhiteSpace(text) ? "..." : text;
        }
    }

    public void HidePanel()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void ShowPanelWithResponse(string text, bool refreshQuestions = true)
    {
        if (refreshQuestions)
        {
            RefreshQuestions();
        }
        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        CaptureCursor();
        waitingForResponse = false;
        awaitingTypewriterCompletion = false;
        if (responseTypewriter != null)
        {
            responseTypewriter.StopShowingText();
        }
        if (responseFallbackText != null)
        {
            responseFallbackText.text = string.IsNullOrWhiteSpace(text) ? "..." : text;
        }
        SetQuestionsInteractable(true);
    }

    private void SetActive(bool value)
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(value);
        }
        else
        {
            gameObject.SetActive(value);
        }
    }

    private void CaptureCursor()
    {
        if (cursorCaptured) return;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorCaptured = true;
    }

    private void ReleaseCursor()
    {
        if (!cursorCaptured) return;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        cursorCaptured = false;
    }

    private void SetQuestionsInteractable(bool value)
    {
        foreach (var entry in questionEntries)
        {
            if (entry?.button != null)
            {
                entry.button.interactable = value;
                ApplyQuestionVisualState(entry.button, entry.question);
            }
        }

        if (questionsCanvasGroup != null)
        {
            questionsCanvasGroup.interactable = value;
            questionsCanvasGroup.blocksRaycasts = value;
        }
    }

    private void HandleTypewriterCompleted()
    {
        if (!awaitingTypewriterCompletion) return;
        awaitingTypewriterCompletion = false;
        CompleteResponsePlayback();
    }

    private void CompleteResponsePlayback()
    {
        if (!waitingForResponse)
        {
            return;
        }

        waitingForResponse = false;
        currentManager?.StopClientTalkingAnimation();
        SetQuestionsInteractable(true);
        if (currentManager != null && lastQuestion != null)
        {
            currentManager.HandleResponsePlaybackFinished(lastQuestion, lastResponse);
        }
    }

    private class QuestionEntry
    {
        public GameObject root;
        public Button button;
        public InvestigationQuestion question;
    }

    private void MarkQuestionAsked(InvestigationQuestion question)
    {
        if (question == null || string.IsNullOrEmpty(question.questionId)) return;
        askedQuestionIds.Add(question.questionId);
    }

    private void ApplyQuestionVisualState(Button button, InvestigationQuestion question)
    {
        if (button == null || question == null || string.IsNullOrEmpty(question.questionId)) return;
        if (!askedQuestionIds.Contains(question.questionId)) return;

        Graphic target = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
        if (target == null) return;

        Color color = previouslyAskedQuestionColor;
        color.a = target.color.a;
        target.color = color;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        button.colors = colors;

        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.SetNormalColor(color, true);
        }
    }

    private void EnsureButtonVisualFeedback(Button button)
    {
        if (button == null) return;
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback == null)
        {
            visualFeedback = button.gameObject.AddComponent<UIButtonVisualFeedback>();
        }

        visualFeedback.Configure(
            colorEnabled: true,
            scaleEnabled: true,
            hover: new Color(0.82f, 0.82f, 0.82f, 1f),
            pressedState: new Color(0.65f, 0.65f, 0.65f, 1f),
            disabled: new Color(0.45f, 0.45f, 0.45f, 0.7f),
            hoverScaleValue: 1.01f,
            pressedScaleValue: 0.99f,
            duration: 0.08f);
    }
}
