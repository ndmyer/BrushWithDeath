using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    [SerializeField] private UnityEvent onCollected;

    private bool hasBeenCollected;

    public void AddOnCollectedListener(UnityAction listener)
    {
        if (listener != null)
            onCollected.AddListener(listener);
    }

    public void RemoveOnCollectedListener(UnityAction listener)
    {
        if (listener != null)
            onCollected.RemoveListener(listener);
    }

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
        if (hasBeenCollected || !TryGetPlayerProgression(other, out PlayerProgression progression))
            return;

        hasBeenCollected = true;
        progression.CollectKey();
        GameSfx.PlayDetached(GameSfxCue.CollectKey, transform.position);
        onCollected?.Invoke();
        Destroy(gameObject);
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private static bool TryGetPlayerProgression(Collider2D other, out PlayerProgression progression)
    {
        progression = other.GetComponent<PlayerProgression>();
        if (progression != null)
            return true;

        progression = other.GetComponentInParent<PlayerProgression>();
        return progression != null;
    }
}
