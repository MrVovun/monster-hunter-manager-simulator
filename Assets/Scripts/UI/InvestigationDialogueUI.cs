using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private ScrollRect questionsScrollRect;
    [SerializeField] private bool resetQuestionScrollToTopOnRefresh = true;
    [SerializeField] private bool numberQuestionReplies = true;
    [SerializeField] private bool enableNumberKeyQuestionSelection = true;
    [SerializeField] private Color previouslyAskedQuestionColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Header("Revealed Evidence")]
    [SerializeField] private GameObject evidencePanel;
    [SerializeField] private TMP_Text knownTagsText;
    [SerializeField] private Transform knownTraitsParent;
    [SerializeField] private TMP_Text knownTraitsFallbackText;
    [SerializeField] private GameObject traitItemPrefab;
    [SerializeField] private TraitTooltipPanel traitTooltipPanel;
    [SerializeField] private Image traitIconPrototype;

    private readonly List<QuestionEntry> questionEntries = new List<QuestionEntry>();
    private readonly List<GameObject> spawnedKnownTraitItems = new List<GameObject>();
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
    private Coroutine resetQuestionsScrollRoutine;

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

            if (questionsScrollRect == null)
            {
                questionsScrollRect = questionsList.GetComponentInParent<ScrollRect>(true);
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
        UnsubscribeFromCaseUpdates();
        currentCase = caseData;
        currentManager = manager;
        onClose = closeCallback;
        askedQuestionIds.Clear();
        waitingForResponse = false;
        awaitingTypewriterCompletion = false;

        SubscribeToCaseUpdates();
        RefreshQuestions();
        RefreshEvidencePanel();
        HookButtons();
        PlayResponse(GetClientOpeningLine(), false);
        SetQuestionsInteractable(true);
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
            viewButton.interactable = !hunterDialogue && CanOpenOrderPanelForTutorial();
            RefreshButtonVisual(viewButton);
        }

        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() =>
            {
                if (!TutorialManager.IsActionAllowed(TutorialIds.DeclineOrder)) return;
                currentManager?.CompleteInvestigation();
                Close();
            });
            declineButton.gameObject.SetActive(!hunterDialogue);
            declineButton.interactable = !hunterDialogue && TutorialManager.IsActionAllowed(TutorialIds.DeclineOrder);
            RefreshButtonVisual(declineButton);
        }

        if (acceptButton != null)
        {
            acceptButton.gameObject.SetActive(!hunterDialogue);
        }
    }

    private void OnDestroy()
    {
        if (resetQuestionsScrollRoutine != null)
        {
            StopCoroutine(resetQuestionsScrollRoutine);
            resetQuestionsScrollRoutine = null;
        }

        UnsubscribeFromCaseUpdates();
        if (responseTypewriter != null)
        {
            responseTypewriter.onTextShowed.RemoveListener(HandleTypewriterCompleted);
        }
    }

    private void HandleViewOrder()
    {
        if (!CanOpenOrderPanelForTutorial()) return;
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

    private bool CanOpenOrderPanelForTutorial()
    {
        return TutorialManager.IsActionAllowed(TutorialIds.SelectMonster)
            || TutorialManager.IsActionAllowed(TutorialIds.AcceptOrder)
            || TutorialManager.IsActionAllowed(TutorialIds.ReferOrder);
    }

    private string FormatQuestionReplyText(int index, string promptText)
    {
        string prompt = string.IsNullOrWhiteSpace(promptText) ? "..." : promptText.Trim();
        return numberQuestionReplies ? $"{index + 1}. {prompt}" : prompt;
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
        for (int i = 0; i < available.Count; i++)
        {
            var question = available[i];
            var entryObj = Instantiate(questionItemPrefab, questionsList);
            var button = entryObj.GetComponentInChildren<Button>();
            var text = entryObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = FormatQuestionReplyText(i, question.promptText);
            }

            if (button != null)
            {
                EnsureButtonVisualFeedback(button);
                var capturedQuestion = question;
                button.onClick.AddListener(() => HandleQuestionClicked(capturedQuestion));
                button.interactable = false;
                ApplyQuestionVisualState(button, question);
            }

            questionEntries.Add(new QuestionEntry
            {
                root = entryObj,
                button = button,
                text = text,
                question = question
            });
        }

        SetQuestionsInteractable(!waitingForResponse);
        ResetQuestionsScrollToTop();
    }

    private void ResetQuestionsScrollToTop()
    {
        if (!resetQuestionScrollToTopOnRefresh || questionsList == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(questionsList);
        Canvas.ForceUpdateCanvases();
        ApplyQuestionsScrollTop();

        if (isActiveAndEnabled)
        {
            if (resetQuestionsScrollRoutine != null)
            {
                StopCoroutine(resetQuestionsScrollRoutine);
            }

            resetQuestionsScrollRoutine = StartCoroutine(ResetQuestionsScrollToTopNextFrame());
        }
    }

    private void Update()
    {
        HandleQuestionNumberShortcuts();
    }

    private IEnumerator ResetQuestionsScrollToTopNextFrame()
    {
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(questionsList);
        Canvas.ForceUpdateCanvases();
        ApplyQuestionsScrollTop();
        resetQuestionsScrollRoutine = null;
    }

    private void ApplyQuestionsScrollTop()
    {
        if (questionsScrollRect != null)
        {
            if (questionsScrollRect.content == null)
            {
                questionsScrollRect.content = questionsList;
            }

            questionsScrollRect.StopMovement();
            questionsScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        questionsList.anchoredPosition = new Vector2(questionsList.anchoredPosition.x, 0f);
    }

    private void HandleQuestionClicked(InvestigationQuestion question)
    {
        if (waitingForResponse || question == null || currentManager == null)
        {
            return;
        }
        if (!TutorialManager.IsActionAllowed(TutorialIds.DialogueQuestions))
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

    private void HandleQuestionNumberShortcuts()
    {
        if (!enableNumberKeyQuestionSelection) return;
        if (!IsDialogueVisible()) return;
        if (waitingForResponse || currentManager == null) return;
        if (!TutorialManager.IsActionAllowed(TutorialIds.DialogueQuestions)) return;
        if (questionsCanvasGroup != null && (!questionsCanvasGroup.interactable || !questionsCanvasGroup.blocksRaycasts)) return;

        int index = GetPressedQuestionNumberIndex();
        if (index < 0 || index >= questionEntries.Count) return;

        QuestionEntry entry = questionEntries[index];
        if (entry == null || entry.button == null || !entry.button.interactable) return;

        HandleQuestionClicked(entry.question);
    }

    private bool IsDialogueVisible()
    {
        GameObject root = rootPanel != null ? rootPanel : gameObject;
        return root != null && root.activeInHierarchy;
    }

    private int GetPressedQuestionNumberIndex()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return -1;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 3;
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 4;
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) return 5;
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) return 6;
        if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) return 7;
        if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame) return 8;

        return -1;
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

        RefreshEvidencePanel();
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
        RefreshEvidencePanel();
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
        if (resetQuestionsScrollRoutine != null)
        {
            StopCoroutine(resetQuestionsScrollRoutine);
            resetQuestionsScrollRoutine = null;
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
        UnsubscribeFromCaseUpdates();
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
        ClearKnownTraitItems();
        if (evidencePanel != null)
        {
            evidencePanel.SetActive(false);
        }
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
        RefreshEvidencePanel();
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

    private void SubscribeToCaseUpdates()
    {
        if (currentManager != null)
        {
            currentManager.OnCaseUpdated -= RefreshEvidencePanel;
            currentManager.OnCaseUpdated += RefreshEvidencePanel;
        }
    }

    private void UnsubscribeFromCaseUpdates()
    {
        if (currentManager != null)
        {
            currentManager.OnCaseUpdated -= RefreshEvidencePanel;
        }
    }

    private void RefreshEvidencePanel()
    {
        bool hunterDialogue = currentManager != null && currentManager.IsHunterDialogueActive;
        bool hasAnyField = knownTagsText != null || knownTraitsParent != null || knownTraitsFallbackText != null;
        bool shouldShow = !hunterDialogue && currentCase != null && hasAnyField;

        if (evidencePanel != null)
        {
            evidencePanel.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            if (knownTagsText != null) knownTagsText.text = string.Empty;
            ClearKnownTraitItems();
            return;
        }

        PopulateKnownTags();
        PopulateKnownTraits();
    }

    private void PopulateKnownTags()
    {
        if (knownTagsText == null) return;

        if (currentCase?.knownTags != null && currentCase.knownTags.Count > 0)
        {
            var lines = currentCase.knownTags
                .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.categoryName))
                .Select(tag => $"{tag.categoryName}: {(!string.IsNullOrWhiteSpace(tag.valueName) ? tag.valueName : "???")}");
            string text = string.Join("\n", lines);
            knownTagsText.text = string.IsNullOrWhiteSpace(text) ? "Tags: ???" : text;
        }
        else
        {
            knownTagsText.text = "Tags: ???";
        }
    }

    private void PopulateKnownTraits()
    {
        ClearKnownTraitItems();

        if (currentCase == null || currentCase.confirmedTraitIds == null || currentCase.confirmedTraitIds.Count == 0)
        {
            if (knownTraitsFallbackText != null)
            {
                knownTraitsFallbackText.text = "Traits: ???";
            }
            return;
        }

        List<MonsterTrait> traits = new List<MonsterTrait>();
        foreach (var traitId in currentCase.confirmedTraitIds)
        {
            if (string.IsNullOrEmpty(traitId)) continue;
            var trait = currentCase.truthTraits?.FirstOrDefault(t => t != null && string.Equals(t.traitId, traitId, System.StringComparison.OrdinalIgnoreCase));
            if (trait != null)
            {
                traits.Add(trait);
            }
        }

        if (traits.Count == 0)
        {
            if (knownTraitsFallbackText != null)
            {
                knownTraitsFallbackText.text = "Traits: ???";
            }
            return;
        }

        if (knownTraitsFallbackText != null)
        {
            knownTraitsFallbackText.text = string.Empty;
        }

        if (knownTraitsParent != null)
        {
            knownTraitsParent.gameObject.SetActive(true);
        }

        foreach (var trait in traits)
        {
            GameObject item = CreateTraitItem(trait);
            if (item == null) continue;
            item.transform.SetParent(knownTraitsParent, false);
            spawnedKnownTraitItems.Add(item);
        }
    }

    private void ClearKnownTraitItems()
    {
        foreach (var item in spawnedKnownTraitItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedKnownTraitItems.Clear();

        if (knownTraitsParent != null)
        {
            foreach (Transform child in knownTraitsParent)
            {
                Destroy(child.gameObject);
            }
            knownTraitsParent.gameObject.SetActive(false);
        }

        if (knownTraitsFallbackText != null)
        {
            knownTraitsFallbackText.text = string.Empty;
        }
    }

    private GameObject CreateTraitItem(MonsterTrait trait)
    {
        if (knownTraitsParent == null)
        {
            return null;
        }

        GameObject item = traitItemPrefab != null ? Instantiate(traitItemPrefab) : new GameObject("KnownTrait");
        RectTransform rect = item.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = item.AddComponent<RectTransform>();
        }

        TMP_Text text = item.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }

        Image icon = FindOrCreateTraitIcon(item);
        if (icon != null)
        {
            icon.sprite = trait != null ? trait.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (traitTooltipPanel != null)
        {
            var tooltip = item.GetComponent<TraitTooltipTrigger>();
            if (tooltip == null)
            {
                tooltip = item.AddComponent<TraitTooltipTrigger>();
            }
            tooltip.Initialize(traitTooltipPanel, rect, trait != null ? trait.displayName : "Trait", trait != null ? trait.description : string.Empty);
        }

        return item;
    }

    private Image FindOrCreateTraitIcon(GameObject item)
    {
        if (item == null) return null;

        Image rootImage = item.GetComponent<Image>();
        if (rootImage != null && item.GetComponent<Button>() == null)
        {
            rootImage.preserveAspect = true;
            return rootImage;
        }

        Image icon = null;
        var images = item.GetComponentsInChildren<Image>(true);
        foreach (var candidate in images)
        {
            if (candidate == null || candidate == rootImage) continue;
            string imageName = candidate.gameObject.name;
            if (!string.IsNullOrEmpty(imageName) && imageName.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                icon = candidate;
                break;
            }
        }

        if (icon == null)
        {
            foreach (var candidate in images)
            {
                if (candidate == null || candidate == rootImage) continue;
                if (!candidate.gameObject.activeInHierarchy && candidate.GetComponentInParent<CanvasGroup>() == null) continue;
                string imageName = candidate.gameObject.name;
                if (!string.IsNullOrEmpty(imageName) && imageName.IndexOf("line", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!string.IsNullOrEmpty(imageName) && imageName.IndexOf("slash", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                icon = candidate;
                break;
            }
        }

        if (icon == null)
        {
            foreach (var candidate in images)
            {
                if (candidate == null || candidate == rootImage) continue;
                string imageName = candidate.gameObject.name;
                if (!string.IsNullOrEmpty(imageName) && imageName.IndexOf("line", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!string.IsNullOrEmpty(imageName) && imageName.IndexOf("slash", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                icon = candidate;
                break;
            }
        }

        if (icon == null && rootImage != null)
        {
            icon = rootImage;
        }

        if (icon == null && traitIconPrototype != null)
        {
            icon = Instantiate(traitIconPrototype, item.transform);
        }

        if (icon == null)
        {
            icon = rootImage != null ? rootImage : item.AddComponent<Image>();
        }

        icon.raycastTarget = traitTooltipPanel != null;
        icon.preserveAspect = true;
        return icon;
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
                entry.button.interactable = value && TutorialManager.IsActionAllowed(TutorialIds.DialogueQuestions);
                ApplyQuestionVisualState(entry.button, entry.question);
            }
        }

        if (questionsCanvasGroup != null)
        {
            bool allowed = value && TutorialManager.IsActionAllowed(TutorialIds.DialogueQuestions);
            questionsCanvasGroup.interactable = allowed;
            questionsCanvasGroup.blocksRaycasts = allowed;
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
            TutorialManager.ReportEvent(TutorialIds.EventClientQuestionAnswered);
            if (!currentManager.IsHunterDialogueActive && AreAllQuestionsAsked())
            {
                TutorialManager.ReportEvent(TutorialIds.EventAllClientQuestionsAsked);
                HookButtons();
            }
        }
    }

    private bool AreAllQuestionsAsked()
    {
        foreach (var entry in questionEntries)
        {
            string id = entry?.question != null ? entry.question.questionId : null;
            if (string.IsNullOrEmpty(id)) continue;
            if (!askedQuestionIds.Contains(id)) return false;
        }

        return questionEntries.Count > 0;
    }

    private class QuestionEntry
    {
        public GameObject root;
        public Button button;
        public TMP_Text text;
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
        TMP_Text label = null;
        foreach (var entry in questionEntries)
        {
            if (entry?.button == button)
            {
                label = entry.text;
                break;
            }
        }

        if (label == null)
        {
            label = button.GetComponentInChildren<TMP_Text>(true);
        }

        if (target == null && label != null)
        {
            target = label;
        }

        if (target == null) return;

        Color color = previouslyAskedQuestionColor;
        color.a = target.color.a;
        target.color = color;

        if (label != null && label != target)
        {
            Color textColor = previouslyAskedQuestionColor;
            textColor.a = label.color.a;
            label.color = textColor;
        }

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

    private void RefreshButtonVisual(Button button)
    {
        if (button == null) return;
        var visualFeedback = button.GetComponent<UIButtonVisualFeedback>();
        if (visualFeedback != null)
        {
            visualFeedback.RefreshVisualState(true);
        }
    }
}
