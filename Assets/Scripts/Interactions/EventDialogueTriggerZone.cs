using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EventDialogueTriggerZone : MonoBehaviour
{
    [Header("Zone Events")]
    [SerializeField] private EventDialogueTrigger onPlayerEntered;
    [SerializeField] private EventDialogueTrigger onPlayerExited;

    private readonly HashSet<int> playersInside = new();

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        playersInside.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayer(other, out PlayerController player))
            return;

        if (!playersInside.Add(player.GetInstanceID()))
            return;

        onPlayerEntered?.TryTriggerDialogue();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetPlayer(other, out PlayerController player))
            return;

        if (!playersInside.Remove(player.GetInstanceID()))
            return;

        onPlayerExited?.TryTriggerDialogue();
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private static bool TryGetPlayer(Collider2D other, out PlayerController player)
    {
        player = other.GetComponent<PlayerController>();
        if (player != null)
            return true;

        player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}
