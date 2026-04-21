using UnityEngine;

[DisallowMultipleComponent]
public class GameTimerDialogueListener : MonoBehaviour
{
    [SerializeField] private GameTimerUI timerUI;
    [SerializeField] private EventDialogueTrigger dialogueTrigger;
    [SerializeField] private bool triggerImmediatelyIfAlreadyExpired = true;

    private GameTimer gameTimer;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (timerUI != null)
        {
            timerUI.TimerBound -= HandleTimerBound;
            timerUI.TimerBound += HandleTimerBound;
        }

        BindToGameTimer(timerUI != null ? timerUI.BoundTimer : null);
    }

    private void OnDisable()
    {
        if (timerUI != null)
            timerUI.TimerBound -= HandleTimerBound;

        BindToGameTimer(null);
    }

    private void HandleTimerExpired()
    {
        dialogueTrigger?.TryTriggerDialogue();
    }

    private void HandleTimerBound(GameTimer boundTimer)
    {
        BindToGameTimer(boundTimer);
    }

    private void BindToGameTimer(GameTimer nextTimer)
    {
        if (gameTimer == nextTimer)
        {
            if (triggerImmediatelyIfAlreadyExpired && gameTimer != null && gameTimer.IsExpired)
                dialogueTrigger?.TryTriggerDialogue();

            return;
        }

        if (gameTimer != null)
            gameTimer.TimerExpired -= HandleTimerExpired;

        gameTimer = nextTimer;

        if (gameTimer == null)
            return;

        gameTimer.TimerExpired -= HandleTimerExpired;
        gameTimer.TimerExpired += HandleTimerExpired;

        if (triggerImmediatelyIfAlreadyExpired && gameTimer.IsExpired)
            dialogueTrigger?.TryTriggerDialogue();
    }

    private void CacheReferences()
    {
        if (dialogueTrigger == null)
            dialogueTrigger = GetComponent<EventDialogueTrigger>();

        if (timerUI == null)
            timerUI = GameTimerUI.Instance != null ? GameTimerUI.Instance : FindAnyObjectByType<GameTimerUI>();
    }
}
