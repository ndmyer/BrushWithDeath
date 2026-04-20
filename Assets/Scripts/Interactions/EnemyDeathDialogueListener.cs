using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDeathDialogueListener : MonoBehaviour
{
    public enum ListenerScope
    {
        Global,
        SpecificEnemy
    }

    public enum EnemyDeathCauseFilter
    {
        Any,
        Unknown,
        Marigold
    }

    [SerializeField] private EventDialogueTrigger dialogueTrigger;
    [SerializeField] private ListenerScope listenerScope = ListenerScope.Global;
    [SerializeField] private SkeletonEnemyBase specificEnemy;
    [SerializeField] private EnemyDeathCauseFilter causeFilter = EnemyDeathCauseFilter.Any;

    private void Awake()
    {
        if (dialogueTrigger == null)
            dialogueTrigger = GetComponent<EventDialogueTrigger>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (listenerScope == ListenerScope.SpecificEnemy)
        {
            if (specificEnemy != null)
            {
                specificEnemy.DiedWithCause -= HandleEnemyDied;
                specificEnemy.DiedWithCause += HandleEnemyDied;
            }

            return;
        }

        SkeletonEnemyBase.AnyEnemyDied -= HandleEnemyDied;
        SkeletonEnemyBase.AnyEnemyDied += HandleEnemyDied;
    }

    private void Unsubscribe()
    {
        if (listenerScope == ListenerScope.SpecificEnemy)
        {
            if (specificEnemy != null)
                specificEnemy.DiedWithCause -= HandleEnemyDied;

            return;
        }

        SkeletonEnemyBase.AnyEnemyDied -= HandleEnemyDied;
    }

    private void HandleEnemyDied(SkeletonEnemyBase enemy, SkeletonEnemyBase.DeathCause cause)
    {
        if (listenerScope == ListenerScope.SpecificEnemy && enemy != specificEnemy)
            return;

        if (!MatchesCause(cause))
            return;

        dialogueTrigger?.TryTriggerDialogue();
    }

    private bool MatchesCause(SkeletonEnemyBase.DeathCause cause)
    {
        switch (causeFilter)
        {
            case EnemyDeathCauseFilter.Unknown:
                return cause == SkeletonEnemyBase.DeathCause.Unknown;
            case EnemyDeathCauseFilter.Marigold:
                return cause == SkeletonEnemyBase.DeathCause.Marigold;
            default:
                return true;
        }
    }
}
