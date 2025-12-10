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

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private InvestigationManager currentManager;
    private InvestigationCase currentCase;
    private System.Action onClose;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool cursorCaptured;

    private void Awake()
    {
        SetActive(false);
    }

    public void Show(InvestigationCase caseData, InvestigationManager manager, System.Action closeCallback)
    {
        currentCase = caseData;
        currentManager = manager;
        onClose = closeCallback;

        RefreshQuestions();
        HookButtons();
        PlayResponse("...");
        SetActive(true);
        CaptureCursor();
    }

    private void HookButtons()
    {
        var viewButton = viewOrderButton != null ? viewOrderButton : acceptButton;
        if (viewButton != null)
        {
            viewButton.onClick.RemoveAllListeners();
            viewButton.onClick.AddListener(HandleViewOrder);
        }

        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() =>
            {
                currentManager?.CompleteInvestigation();
                Close();
            });
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
        foreach (var go in spawnedItems)
        {
            if (go != null) Destroy(go);
        }
        spawnedItems.Clear();

        if (questionItemPrefab == null || questionsList == null || currentManager == null) return;

        var available = currentManager.GetAvailableQuestions();
        foreach (var question in available)
        {
            var entry = Instantiate(questionItemPrefab, questionsList);
            spawnedItems.Add(entry);
            var button = entry.GetComponentInChildren<Button>();
            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = question.promptText;
            }

            if (button != null)
            {
                button.onClick.AddListener(() => HandleQuestionClicked(question));
            }
        }
    }

    private void HandleQuestionClicked(InvestigationQuestion question)
    {
        string response = currentManager != null ? currentManager.ResolveQuestion(question) : string.Empty;
        RefreshQuestions();
        if (string.IsNullOrWhiteSpace(response))
        {
            response = "...";
        }
        PlayResponse(response);
    }

    private void PlayResponse(string text)
    {
        bool played = false;

        if (responseTypewriter != null)
        {
            try
            {
                responseTypewriter.ShowText(text);
                responseTypewriter.StartShowingText();
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
    }

    public void Reopen()
    {
        RefreshQuestions();
        SetActive(true);
        CaptureCursor();
    }

    public void Close()
    {
        SetActive(false);
        ReleaseCursor();
        onClose?.Invoke();
        onClose = null;
        currentManager = null;
        currentCase = null;
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
}
