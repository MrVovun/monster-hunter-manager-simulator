using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardGameUI : MonoBehaviour
{
    [Serializable]
    public class CardSprite
    {
        public int value = 1;
        public Sprite sprite;
    }

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button drawButton;
    [SerializeField] private Button standButton;
    [SerializeField] private Button nextRoundButton;
    [SerializeField] private Button closeButton;

    [Header("Cards")]
    [SerializeField] private CardGameCardView cardPrefab;
    [SerializeField] private Transform playerCardsParent;
    [SerializeField] private Transform opponentCardsParent;
    [SerializeField] private Transform playerSideCardsParent;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private List<CardSprite> cardFrontSprites = new List<CardSprite>();

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text opponentScoreText;
    [SerializeField] private TMP_Text playerRoundsText;
    [SerializeField] private TMP_Text opponentRoundsText;
    [SerializeField] private TMP_Text statusText;

    [Header("Rules")]
    [SerializeField] private int targetScore = 20;
    [SerializeField] private int roundsToWin = 2;
    [SerializeField] private Vector2Int mainDeckValueRange = new Vector2Int(1, 10);
    [SerializeField] private int sideCardsPerMatch = 4;
    [SerializeField] private Vector2Int sideCardValueRange = new Vector2Int(1, 6);
    [SerializeField] private int opponentStandAtScore = 17;

    private readonly List<CardGameCardView> spawnedCards = new List<CardGameCardView>();
    private readonly List<int> playerCards = new List<int>();
    private readonly List<int> opponentCards = new List<int>();
    private readonly List<int> playerSideCards = new List<int>();
    private readonly HashSet<int> usedSideCardIndices = new HashSet<int>();

    private Action onClosed;
    private Hunter opponentHunter;
    private bool cursorCaptured;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool playerStanding;
    private bool opponentStanding;
    private bool roundOver;
    private bool matchOver;
    private int playerRoundsWon;
    private int opponentRoundsWon;
    private int selectedSideCardIndex = -1;

    private void Awake()
    {
        if (drawButton != null) drawButton.onClick.AddListener(HandleDrawClicked);
        if (standButton != null) standButton.onClick.AddListener(HandleStandClicked);
        if (nextRoundButton != null) nextRoundButton.onClick.AddListener(StartRound);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        SetRootActive(false);
    }

    public void Show(Hunter hunter, Action closedCallback)
    {
        opponentHunter = hunter;
        onClosed = closedCallback;
        playerRoundsWon = 0;
        opponentRoundsWon = 0;
        matchOver = false;
        BuildSideHand();
        SetRootActive(true);
        CaptureCursor();
        StartRound();
    }

    public void Hide()
    {
        ClearSpawnedCards();
        SetRootActive(false);
        ReleaseCursor();
        Action callback = onClosed;
        onClosed = null;
        opponentHunter = null;
        callback?.Invoke();
    }

    private void StartRound()
    {
        playerCards.Clear();
        opponentCards.Clear();
        playerStanding = false;
        opponentStanding = false;
        roundOver = false;
        selectedSideCardIndex = -1;

        DrawTo(playerCards);
        DrawTo(opponentCards);
        SetStatus("Draw, play a side card, or stand.");
        Refresh();
    }

    private void BuildSideHand()
    {
        playerSideCards.Clear();
        usedSideCardIndices.Clear();
        int min = Mathf.Min(sideCardValueRange.x, sideCardValueRange.y);
        int max = Mathf.Max(sideCardValueRange.x, sideCardValueRange.y);
        int count = Mathf.Max(0, sideCardsPerMatch);
        for (int i = 0; i < count; i++)
        {
            playerSideCards.Add(UnityEngine.Random.Range(min, max + 1));
        }
    }

    private void HandleDrawClicked()
    {
        if (roundOver || matchOver || playerStanding) return;

        InteractionFeedbackManager.PlayUIClick();
        DrawTo(playerCards);
        ResolveAfterPlayerAction();
    }

    private void HandleStandClicked()
    {
        if (roundOver || matchOver || playerStanding) return;

        InteractionFeedbackManager.PlayUIClick();
        playerStanding = true;
        SetStatus("You stand.");
        RunOpponentUntilRoundEnds();
        Refresh();
    }

    private void HandleSideCardClicked(int sideCardIndex)
    {
        if (roundOver || matchOver || playerStanding) return;
        if (sideCardIndex < 0 || sideCardIndex >= playerSideCards.Count) return;
        if (usedSideCardIndices.Contains(sideCardIndex)) return;

        InteractionFeedbackManager.PlayUIClick();
        selectedSideCardIndex = sideCardIndex;
        playerCards.Add(playerSideCards[sideCardIndex]);
        usedSideCardIndices.Add(sideCardIndex);
        ResolveAfterPlayerAction();
    }

    private void ResolveAfterPlayerAction()
    {
        if (GetScore(playerCards) > targetScore)
        {
            FinishRound(opponentWon: true, "You busted.");
            return;
        }

        TakeOpponentTurn();
        if (!roundOver && playerStanding && opponentStanding)
        {
            FinishRoundByScores();
        }

        Refresh();
    }

    private void RunOpponentUntilRoundEnds()
    {
        while (!roundOver && !opponentStanding)
        {
            TakeOpponentTurn();
        }

        if (!roundOver)
        {
            FinishRoundByScores();
        }
    }

    private void TakeOpponentTurn()
    {
        if (opponentStanding || roundOver) return;

        int opponentScore = GetScore(opponentCards);
        int playerScore = GetScore(playerCards);
        bool shouldStand = opponentScore >= opponentStandAtScore || (playerStanding && opponentScore >= playerScore);
        if (shouldStand)
        {
            opponentStanding = true;
            SetStatus($"{GetOpponentName()} stands.");
            return;
        }

        DrawTo(opponentCards);
        if (GetScore(opponentCards) > targetScore)
        {
            FinishRound(opponentWon: false, $"{GetOpponentName()} busted.");
        }
    }

    private void FinishRoundByScores()
    {
        int playerScore = GetScore(playerCards);
        int opponentScore = GetScore(opponentCards);
        if (playerScore == opponentScore)
        {
            FinishRoundTie("The round is a tie.");
            return;
        }

        bool opponentWon = opponentScore > playerScore;
        FinishRound(opponentWon, opponentWon ? $"{GetOpponentName()} wins the round." : "You win the round.");
    }

    private void FinishRound(bool opponentWon, string message)
    {
        roundOver = true;
        if (opponentWon) opponentRoundsWon++;
        else playerRoundsWon++;

        if (playerRoundsWon >= roundsToWin || opponentRoundsWon >= roundsToWin)
        {
            matchOver = true;
            string winner = playerRoundsWon > opponentRoundsWon ? "You win the match." : $"{GetOpponentName()} wins the match.";
            SetStatus($"{message} {winner}");
        }
        else
        {
            SetStatus(message);
        }

        Refresh();
    }

    private void FinishRoundTie(string message)
    {
        roundOver = true;
        SetStatus(message);
        Refresh();
    }

    private void DrawTo(List<int> target)
    {
        int min = Mathf.Min(mainDeckValueRange.x, mainDeckValueRange.y);
        int max = Mathf.Max(mainDeckValueRange.x, mainDeckValueRange.y);
        target.Add(UnityEngine.Random.Range(min, max + 1));
    }

    private void Refresh()
    {
        ClearSpawnedCards();
        RefreshHeader();
        RefreshCards();
        RefreshButtons();
    }

    private void RefreshHeader()
    {
        string opponentName = GetOpponentName();
        if (titleText != null) titleText.text = $"Cards with {opponentName}";
        if (playerScoreText != null) playerScoreText.text = $"You: {GetScore(playerCards)}";
        if (opponentScoreText != null) opponentScoreText.text = $"{opponentName}: {GetScore(opponentCards)}";
        if (playerRoundsText != null) playerRoundsText.text = playerRoundsWon.ToString();
        if (opponentRoundsText != null) opponentRoundsText.text = opponentRoundsWon.ToString();
    }

    private void RefreshCards()
    {
        SpawnCards(playerCardsParent, playerCards, true, null);
        SpawnCards(opponentCardsParent, opponentCards, true, null);

        if (playerSideCardsParent != null)
        {
            for (int i = 0; i < playerSideCards.Count; i++)
            {
                int index = i;
                CardGameCardView card = SpawnCard(playerSideCardsParent, playerSideCards[i], true, () => HandleSideCardClicked(index));
                if (card != null)
                {
                    bool used = usedSideCardIndices.Contains(index);
                    card.gameObject.SetActive(!used);
                    card.SetSelected(index == selectedSideCardIndex);
                }
            }
        }
    }

    private void SpawnCards(Transform parent, List<int> values, bool faceUp, Action<int> clickCallback)
    {
        if (parent == null || values == null) return;
        foreach (int value in values)
        {
            SpawnCard(parent, value, faceUp, clickCallback != null ? () => clickCallback(value) : null);
        }
    }

    private CardGameCardView SpawnCard(Transform parent, int value, bool faceUp, Action clickCallback)
    {
        if (cardPrefab == null || parent == null) return null;

        CardGameCardView card = Instantiate(cardPrefab, parent);
        Sprite sprite = faceUp ? GetCardFrontSprite(value) : cardBackSprite;
        card.Initialize(value, sprite, faceUp, clickCallback != null ? _ => clickCallback() : null);
        spawnedCards.Add(card);
        return card;
    }

    private void RefreshButtons()
    {
        bool canAct = !roundOver && !matchOver && !playerStanding;
        if (drawButton != null) drawButton.interactable = canAct;
        if (standButton != null) standButton.interactable = canAct;
        if (nextRoundButton != null)
        {
            nextRoundButton.gameObject.SetActive(roundOver && !matchOver);
            nextRoundButton.interactable = roundOver && !matchOver;
        }
        if (closeButton != null) closeButton.interactable = true;
    }

    private Sprite GetCardFrontSprite(int value)
    {
        if (cardFrontSprites != null)
        {
            foreach (var entry in cardFrontSprites)
            {
                if (entry != null && entry.value == value && entry.sprite != null)
                {
                    return entry.sprite;
                }
            }
        }

        return null;
    }

    private int GetScore(List<int> cards)
    {
        int score = 0;
        if (cards == null) return score;
        foreach (int card in cards)
        {
            score += card;
        }
        return score;
    }

    private string GetOpponentName()
    {
        if (opponentHunter != null && opponentHunter.Data != null && !string.IsNullOrWhiteSpace(opponentHunter.Data.hunterName))
        {
            return opponentHunter.Data.hunterName;
        }

        return opponentHunter != null ? opponentHunter.name : "Hunter";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }
    }

    private void ClearSpawnedCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        spawnedCards.Clear();
    }

    private void SetRootActive(bool active)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(active);
        }
        else
        {
            gameObject.SetActive(active);
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
