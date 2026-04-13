using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Serializable]
    public class HealthChangedEvent : UnityEvent<float, float> { }

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Transform respawnPoint;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 3f;

    [Header("Respawn")]
    [FormerlySerializedAs("respawnDelay")]
    [SerializeField, Min(0f)] private float blackHoldDuration = 2f;

    [Header("Events")]
    [SerializeField] private UnityEvent onDeath;
    [SerializeField] private UnityEvent onRespawn;
    [SerializeField] private HealthChangedEvent onHealthChanged;

    private Coroutine respawnRoutine;
    private Vector3 fallbackRespawnPosition;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        maxHealth = Mathf.Max(1f, maxHealth);
        blackHoldDuration = Mathf.Max(0f, blackHoldDuration);
    }

    private void Awake()
    {
        CacheReferences();
        fallbackRespawnPosition = transform.position;
        RestoreFullHealth();
    }

    public bool ApplyDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return false;

        SetCurrentHealth(CurrentHealth - damage);

        if (CurrentHealth <= 0f)
            HandleDeath();

        return true;
    }

    public bool Heal(float amount)
    {
        if (IsDead || amount <= 0f || CurrentHealth >= maxHealth)
            return false;

        SetCurrentHealth(CurrentHealth + amount);
        return true;
    }

    public void RestoreFullHealth()
    {
        SetCurrentHealth(maxHealth);
    }

    public void SetRespawnPoint(Transform newRespawnPoint)
    {
        respawnPoint = newRespawnPoint;
    }

    private void HandleDeath()
    {
        if (IsDead)
            return;

        IsDead = true;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        playerController?.EnterDeathState();
        onDeath?.Invoke();

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);

        respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        DeathScreenUI deathScreen = DeathScreenUI.Instance;
        if (deathScreen != null)
            yield return deathScreen.FadeToBlack();

        ResetDuringBlackout();

        float holdDuration = Mathf.Max(0f, blackHoldDuration);
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        if (deathScreen != null)
            yield return deathScreen.FadeFromBlack();

        IsDead = false;
        playerController?.ExitDeathState();
        onRespawn?.Invoke();
        respawnRoutine = null;
    }

    private void ResetDuringBlackout()
    {
        MoveToRespawnPoint();
        RestoreFullHealth();
        playerController?.EnterDeathState(snapPistaToPlayer: true);
    }

    private void MoveToRespawnPoint()
    {
        Vector3 targetPosition = respawnPoint != null ? respawnPoint.position : fallbackRespawnPosition;

        if (body != null)
        {
            body.position = targetPosition;
            body.linearVelocity = Vector2.zero;
            return;
        }

        transform.position = targetPosition;
    }

    private void SetCurrentHealth(float value)
    {
        CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
        onHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void CacheReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (body == null)
            body = GetComponent<Rigidbody2D>();
    }
}
