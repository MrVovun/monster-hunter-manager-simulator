using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KitchenRecipeUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button chooseButton;

    [Header("List")]
    [SerializeField] private Transform listParent;
    [SerializeField] private KitchenRecipeOptionUI optionPrefab;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    private readonly List<KitchenRecipeOptionUI> spawnedOptions = new List<KitchenRecipeOptionUI>();
    private KitchenManager manager;
    private KitchenRecipe selectedRecipe;
    private Action onClosed;
    private bool cursorCaptured;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
        if (chooseButton != null)
        {
            chooseButton.onClick.AddListener(HandleChoosePressed);
        }
        SetRootActive(false);
    }

    public void Show(KitchenManager targetManager, Action closedCallback)
    {
        if (targetManager == null || listParent == null || optionPrefab == null)
        {
            Debug.LogWarning("KitchenRecipeUI: Missing manager, list parent, or option prefab.", this);
            return;
        }

        manager = targetManager;
        onClosed = closedCallback;
        manager.OnStateChanged += HandleManagerChanged;
        SetRootActive(true);
        CaptureCursor();
        Refresh();
    }

    public void Hide()
    {
        if (manager != null)
        {
            manager.OnStateChanged -= HandleManagerChanged;
        }
        ClearList();
        SetRootActive(false);
        ReleaseCursor();
        var callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void HandleManagerChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        ClearList();
        var recipes = manager != null ? manager.GetRecipes() : null;

        if (recipes == null || recipes.Count == 0)
        {
            selectedRecipe = null;
            UpdateStatus();
            return;
        }

        if (selectedRecipe == null || !recipes.Contains(selectedRecipe))
        {
            selectedRecipe = recipes[0];
        }

        foreach (var recipe in recipes)
        {
            if (recipe == null) continue;
            var option = Instantiate(optionPrefab, listParent);
            option.Initialize(recipe, HandleRecipeSelected, HandleRecipeChosen, manager != null ? manager.GetRolledCounterTrait() : null);
            option.SetSelected(recipe == selectedRecipe);
            option.SetInteractable(true);
            option.SetCanChoose(manager != null && manager.CanChooseRecipe());
            option.SetStatus(GetRecipeStatus(recipe));
            spawnedOptions.Add(option);
        }

        UpdateStatus();
    }

    private void HandleRecipeSelected(KitchenRecipe recipe)
    {
        if (recipe == null) return;
        selectedRecipe = recipe;
        foreach (var option in spawnedOptions)
        {
            option.SetSelected(option.GetRecipe() == selectedRecipe);
        }
        UpdateStatus();
    }

    private void HandleChoosePressed()
    {
        HandleRecipeChosen(selectedRecipe);
    }

    private void HandleRecipeChosen(KitchenRecipe recipe)
    {
        if (manager == null || recipe == null) return;
        if (manager.TryChooseRecipe(recipe))
        {
            Hide();
        }
        else
        {
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        KitchenRecipe activeRecipe = manager != null ? manager.GetCurrentRecipe() : null;
        if (statusText != null)
        {
            statusText.text = BuildStatusText(activeRecipe);
        }
        if (chooseButton != null)
        {
            chooseButton.interactable = manager != null && manager.CanChooseRecipe() && selectedRecipe != null;
        }
    }

    private string GetRecipeStatus(KitchenRecipe recipe)
    {
        if (manager == null || recipe == null) return string.Empty;
        KitchenRecipe activeRecipe = manager.GetCurrentRecipe();
        if (activeRecipe == recipe) return "Cooking today";
        if (activeRecipe != null) return "Unavailable today";
        return manager.CanChooseRecipe() ? "Available" : "Unavailable";
    }

    private string BuildStatusText(KitchenRecipe activeRecipe)
    {
        if (manager == null) return string.Empty;
        if (activeRecipe != null) return $"Cooking: {activeRecipe.GetDisplayName()}";
        if (manager.CanChooseRecipe()) return "Choose today's meal.";

        var tm = GameManager.Instance != null ? GameManager.Instance.GetTimeManager() : null;
        if (tm != null && tm.GetDayState() != TimeManager.DayState.Active)
        {
            return "Cooking is available during the workday.";
        }

        return "Kitchen unavailable.";
    }

    private void ClearList()
    {
        foreach (var option in spawnedOptions)
        {
            if (option != null)
            {
                Destroy(option.gameObject);
            }
        }
        spawnedOptions.Clear();
    }

    private void SetRootActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
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
