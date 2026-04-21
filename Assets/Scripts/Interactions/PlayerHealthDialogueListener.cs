using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealthDialogueListener : MonoBehaviour
{
    public enum HealthTriggerMode
    {
        CurrentHealthAtOrBelow,
        CurrentHealthExactly,
        MissingHealthAtOrAbove,
        HealthFractionAtOrBelow
    }

    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private EventDialogueTrigger dialogueTrigger;
    [SerializeField] private HealthTriggerMode triggerMode = HealthTriggerMode.CurrentHealthAtOrBelow;
    [SerializeField, Min(0f)] private float threshold = 1f;
    [SerializeField] private bool onlyOnHealthLoss = true;
    [SerializeField] private bool ignoreWhileDead = true;

    private float previousHealth = float.NaN;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (playerHealth == null)
            return;

        previousHealth = playerHealth.CurrentHealth;
        playerHealth.HealthChanged -= HandleHealthChanged;
        playerHealth.HealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        bool healthDropped = float.IsNaN(previousHealth) || currentHealth < previousHealth;
        previousHealth = currentHealth;

        if (onlyOnHealthLoss && !healthDropped)
            return;

        if (ignoreWhileDead && playerHealth != null && playerHealth.IsDead)
            return;

        if (!MeetsThreshold(currentHealth, maxHealth))
            return;

        dialogueTrigger?.TryTriggerDialogue();
    }

    private bool MeetsThreshold(float currentHealth, float maxHealth)
    {
        switch (triggerMode)
        {
            case HealthTriggerMode.CurrentHealthExactly:
                return Mathf.Approximately(currentHealth, threshold);
            case HealthTriggerMode.MissingHealthAtOrAbove:
                return (maxHealth - currentHealth) >= threshold;
            case HealthTriggerMode.HealthFractionAtOrBelow:
                return maxHealth > Mathf.Epsilon && (currentHealth / maxHealth) <= threshold;
            default:
                return currentHealth <= threshold;
        }
    }

    private void CacheReferences()
    {
        if (dialogueTrigger == null)
            dialogueTrigger = GetComponent<EventDialogueTrigger>();

        if (playerHealth == null)
            playerHealth = FindAnyObjectByType<PlayerHealth>();
    }
}
