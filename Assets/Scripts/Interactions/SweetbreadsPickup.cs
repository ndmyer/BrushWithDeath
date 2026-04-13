using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SweetbreadsPickup : MonoBehaviour
{
    [SerializeField, Min(0f)] private float healAmount = 1f;

    private bool hasBeenConsumed;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenConsumed || !TryGetPlayerHealth(other, out PlayerHealth playerHealth))
            return;

        hasBeenConsumed = true;
        playerHealth.Heal(healAmount);
        Destroy(gameObject);
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private static bool TryGetPlayerHealth(Collider2D other, out PlayerHealth playerHealth)
    {
        playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            return true;

        playerHealth = other.GetComponentInParent<PlayerHealth>();
        return playerHealth != null;
    }
}
