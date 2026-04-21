using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameTimerUI : MonoBehaviour
{
    private static GameTimerUI instance;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new(220f, 72f);
    [SerializeField] private Vector2 panelOffset = new(-18f, -18f);
    [SerializeField] private int canvasSortingOrder = 45;

    [Header("Visuals")]
    [SerializeField] private Color panelColor = new(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField, Min(1f)] private float fontSize = 44f;

    private GameTimer timer;
    [System.NonSerialized] private RectTransform panelRoot;
    [System.NonSerialized] private Image backgroundImage;
    [System.NonSerialized] private TextMeshProUGUI timerLabel;

    public event Action<GameTimer> TimerBound;
    public GameTimer BoundTimer => timer;

    public static GameTimerUI Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<GameTimerUI>();

            return instance;
        }
    }

    public static GameTimerUI EnsureInstance()
    {
        if (Instance != null)
            return instance;

        GameObject root = new("GameTimerUI", typeof(RectTransform));
        instance = root.AddComponent<GameTimerUI>();
        instance.EnsureRuntimeSetup();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureRuntimeSetup();
    }

    private void Start()
    {
        if (timer == null)
            Bind(GameTimer.Instance != null ? GameTimer.Instance : FindAnyObjectByType<GameTimer>());
    }

    private void OnDestroy()
    {
        if (timer != null)
            timer.TimeChanged -= HandleTimeChanged;

        if (instance == this)
            instance = null;
    }

    public void Bind(GameTimer targetTimer)
    {
        if (timer == targetTimer)
        {
            Refresh(timer != null ? timer.RemainingSeconds : 0f);
            TimerBound?.Invoke(timer);
            return;
        }

        if (timer != null)
            timer.TimeChanged -= HandleTimeChanged;

        timer = targetTimer;

        if (timer != null)
            timer.TimeChanged += HandleTimeChanged;

        TimerBound?.Invoke(timer);

        EnsureRuntimeSetup();
        Refresh(timer != null ? timer.RemainingSeconds : 0f);
    }

    private void EnsureRuntimeSetup()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null)
            canvasScaler = gameObject.AddComponent<CanvasScaler>();

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (panelRoot == null)
            panelRoot = CreatePanelRoot();

        if (backgroundImage == null)
            backgroundImage = panelRoot.GetComponent<Image>();

        if (timerLabel == null)
            timerLabel = CreateTimerLabel(panelRoot);

        backgroundImage.color = panelColor;
        timerLabel.color = textColor;
        timerLabel.fontSize = fontSize;
    }

    private RectTransform CreatePanelRoot()
    {
        GameObject panelObject = new("TimerPanel", typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.SetParent(transform, false);
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = panelOffset;
        rectTransform.sizeDelta = panelSize;
        return rectTransform;
    }

    private TextMeshProUGUI CreateTimerLabel(RectTransform parent)
    {
        GameObject labelObject = new("Label", typeof(RectTransform));
        RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(12f, 8f);
        rectTransform.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;

        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        label.text = FormatTime(300f);
        return label;
    }

    private void HandleTimeChanged(float remainingSeconds, float _)
    {
        Refresh(remainingSeconds);
    }

    private void Refresh(float remainingSeconds)
    {
        EnsureRuntimeSetup();

        if (timerLabel != null)
            timerLabel.text = FormatTime(remainingSeconds);
    }

    private static string FormatTime(float remainingSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:0}:{seconds:00}";
    }
}
