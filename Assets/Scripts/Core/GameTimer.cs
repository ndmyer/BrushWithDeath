using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GameTimer : MonoBehaviour
{
    private static GameTimer instance;

    [Header("Timer")]
    [SerializeField, Min(1f)] private float defaultDurationSeconds = 300f;
    [SerializeField] private bool startRunningOnAwake = true;

    [Header("References")]
    [SerializeField] private GameFlowController gameFlowController;

    public static GameTimer Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<GameTimer>();

            return instance;
        }
    }

    public static GameTimer EnsureInstance()
    {
        if (Instance != null)
            return instance;

        GameObject root = new("GameTimer");
        instance = root.AddComponent<GameTimer>();
        return instance;
    }

    public event Action<float, float> TimeChanged;
    public event Action TimerExpired;

    public float RemainingSeconds { get; private set; }
    public float DurationSeconds => defaultDurationSeconds;
    public bool IsRunning { get; private set; }
    public bool IsExpired { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CacheReferences();
        ResetTimer(startRunningOnAwake);
        GameTimerUI.EnsureInstance()?.Bind(this);
    }

    private void OnValidate()
    {
        defaultDurationSeconds = Mathf.Max(1f, defaultDurationSeconds);
    }

    private void Update()
    {
        if (!IsRunning || IsExpired)
            return;

        float newRemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        if (Mathf.Approximately(newRemainingSeconds, RemainingSeconds))
            return;

        RemainingSeconds = newRemainingSeconds;
        NotifyTimeChanged();

        if (RemainingSeconds <= 0f)
            ExpireTimer();
    }

    public void ResetTimer(bool startRunning = true)
    {
        defaultDurationSeconds = Mathf.Max(1f, defaultDurationSeconds);
        RemainingSeconds = defaultDurationSeconds;
        IsExpired = false;
        IsRunning = startRunning;
        NotifyTimeChanged();
    }

    public void StartTimer()
    {
        if (IsExpired)
            return;

        IsRunning = true;
    }

    public void PauseTimer()
    {
        IsRunning = false;
    }

    private void ExpireTimer()
    {
        if (IsExpired)
            return;

        RemainingSeconds = 0f;
        IsRunning = false;
        IsExpired = true;
        NotifyTimeChanged();
        TimerExpired?.Invoke();

        if (gameFlowController == null)
            gameFlowController = GameFlowController.EnsureInstance();

        gameFlowController?.HandleGameOver();
    }

    private void NotifyTimeChanged()
    {
        TimeChanged?.Invoke(RemainingSeconds, defaultDurationSeconds);
    }

    private void CacheReferences()
    {
        if (gameFlowController == null)
            gameFlowController = GameFlowController.Instance != null ? GameFlowController.Instance : FindAnyObjectByType<GameFlowController>();
    }
}
