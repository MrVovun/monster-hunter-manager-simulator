using System;
using UnityEngine;
using UnityEngine.UI;

public class CandidateProfilePanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private HunterDetailsPanel detailsPanel;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button closeButton;

    private HunterRecruitmentManager recruitmentManager;
    private HunterRecruitmentManager.RecruitmentCandidate activeCandidate;
    private bool cursorModified;
    private CursorLockMode cachedLockMode;
    private bool cachedCursorVisible;
    private System.Action closeCallback;

    private void Awake()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public void Initialize(HunterRecruitmentManager manager)
    {
        recruitmentManager = manager;
    }

    public void ShowCandidate(HunterRecruitmentManager.RecruitmentCandidate candidate, System.Action onClosed)
    {
        activeCandidate = candidate;
        closeCallback = onClosed;
        if (root != null)
        {
            root.SetActive(true);
        }

        detailsPanel?.ShowHunter(candidate?.spawnedHunter);
        WireButtons();
        ApplyCursorState(true);
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        detailsPanel?.Clear();
        activeCandidate = null;
        ApplyCursorState(false);
        closeCallback?.Invoke();
        closeCallback = null;
    }

    public void HandleCandidateResolved(HunterRecruitmentManager.RecruitmentCandidate candidate)
    {
        if (candidate == activeCandidate)
        {
            Hide();
        }
    }

    private void WireButtons()
    {
        bool canAct = activeCandidate != null && activeCandidate.status == HunterRecruitmentManager.CandidateStatus.Pending;

        if (hireButton != null)
        {
            hireButton.onClick.RemoveAllListeners();
            hireButton.onClick.AddListener(OnHirePressed);
            hireButton.interactable = canAct;
            RefreshButtonVisual(hireButton);
        }

        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(OnDeclinePressed);
            declineButton.interactable = canAct;
            RefreshButtonVisual(declineButton);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnHirePressed()
    {
        if (activeCandidate == null) return;
        recruitmentManager?.HireCandidate(activeCandidate);
    }

    private void OnDeclinePressed()
    {
        if (activeCandidate == null) return;
        recruitmentManager?.DeclineCandidate(activeCandidate);
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

    private void ApplyCursorState(bool panelActive)
    {
        if (panelActive)
        {
            if (!cursorModified)
            {
                cachedLockMode = Cursor.lockState;
                cachedCursorVisible = Cursor.visible;
                cursorModified = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (cursorModified)
        {
            Cursor.lockState = cachedLockMode;
            Cursor.visible = cachedCursorVisible;
            cursorModified = false;
        }
    }
}
