using UnityEngine;

[DisallowMultipleComponent]
public class GameTimerDialogueListener : MonoBehaviour
{
    [SerializeField] private GameTimer gameTimer;
    [SerializeField] private EventDialogueTrigger dialogueTrigger;
    [SerializeField] private bool triggerImmediatelyIfAlreadyExpired = true;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (gameTimer == null)
            return;

        gameTimer.TimerExpired -= HandleTimerExpired;
        gameTimer.TimerExpired += HandleTimerExpired;

        if (triggerImmediatelyIfAlreadyExpired && gameTimer.IsExpired)
            dialogueTrigger?.TryTriggerDialogue();
    }

    private void OnDisable()
    {
        if (gameTimer != null)
            gameTimer.TimerExpired -= HandleTimerExpired;
    }

    private void HandleTimerExpired()
    {
        dialogueTrigger?.TryTriggerDialogue();
    }

    private void CacheReferences()
    {
        if (dialogueTrigger == null)
            dialogueTrigger = GetComponent<EventDialogueTrigger>();

        if (gameTimer == null)
            gameTimer = GameTimer.Instance != null ? GameTimer.Instance : FindAnyObjectByType<GameTimer>();
    }
}
