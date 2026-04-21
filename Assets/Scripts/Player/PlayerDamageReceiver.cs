using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    [Serializable]
    public class DamageEvent : UnityEvent<float> { }

    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.2f;
    [SerializeField] private bool interruptTempoOnDamage = true;
    [SerializeField] private bool logDamage = true;
    [SerializeField] private UnityEvent onDamaged;
    [SerializeField] private DamageEvent onDamageTaken;

    public float LastDamageReceived { get; private set; }
    public Vector2 LastHitDirection { get; private set; }
    public GameObject LastSource { get; private set; }

    private float nextDamageTime;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    public void ReceiveDamage(float damage, Vector2 hitDirection, GameObject source)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerDamageReceiver is missing a PlayerHealth reference.", this);
            return;
        }

        if (Time.time < nextDamageTime || playerHealth.IsDead)
            return;

        LastDamageReceived = damage;
        LastHitDirection = hitDirection;
        LastSource = source;

        if (!playerHealth.ApplyDamage(damage))
            return;

        bool isLethalDamage = playerHealth.IsDead;

        nextDamageTime = Time.time + invulnerabilityDuration;

        if (!isLethalDamage)
            GameSfx.Play(this, GameSfxCue.PlayerHit, pitchVariance: 0.03f, volumeVariance: 0.04f);

        if (interruptTempoOnDamage && playerController != null)
            playerController.InterruptTempoFocus(allowGraceCompletion: false);

        if (logDamage)
        {
            string sourceName = source != null ? source.name : "Unknown";
            Debug.Log($"Player took {damage:0.##} damage from {sourceName}.", this);
        }

        onDamageTaken?.Invoke(damage);
        onDamaged?.Invoke();
    }

    private void CacheReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }
}
