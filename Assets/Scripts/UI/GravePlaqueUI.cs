using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GravePlaqueUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text hunterNameText;
    [SerializeField] private TMP_Text completedMissionsText;
    [SerializeField] private Button closeButton;

    private Action onClosed;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool cursorCaptured;

    private void Awake()
    {
        WireCloseButton();
        SetActive(false);
    }

    public void Show(GraveRecord record, Action closedCallback)
    {
        onClosed = closedCallback;
        WireCloseButton();

        if (hunterNameText != null)
        {
            hunterNameText.text = record != null ? record.hunterName : "Unknown Hunter";
        }
        if (completedMissionsText != null)
        {
            int count = record != null ? record.completedMissions : 0;
            completedMissionsText.text = $"Missions completed: {count}";
        }

        SetActive(true);
        CaptureCursor();
    }

    public void Close()
    {
        SetActive(false);
        ReleaseCursor();
        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void WireCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
    }

    private void SetActive(bool value)
    {
        GameObject root = panelRoot != null ? panelRoot : gameObject;
        root.SetActive(value);
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
