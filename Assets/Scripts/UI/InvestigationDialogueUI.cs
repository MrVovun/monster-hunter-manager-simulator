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

    private readonly List<QuestionEntry> questionEntries = new List<QuestionEntry>();
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
                var capturedQuestion = question;
                button.onClick.AddListener(() => HandleQuestionClicked(capturedQuestion));
                button.interactable = !waitingForResponse;
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

        float waitTime = Mathf.Max(0f, currentManager.GetQuestionDuration(question));
        if (waitTime > 0f)
        {
            currentManager?.PlayClientThinkingAnimation();
            yield return new WaitForSecondsRealtime(waitTime);
        }

        string response = currentManager.ResolveQuestion(question);
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
        SetActive(false);
        ReleaseCursor();
        if (responseRoutine != null)
        {
            StopCoroutine(responseRoutine);
            responseRoutine = null;
        }
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
}
