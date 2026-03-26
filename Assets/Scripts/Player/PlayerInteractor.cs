using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Transform interactionOrigin;
    [SerializeField] private float interactionDistance = 0.75f;
    [SerializeField] private float interactionRadius = 0.2f;
    [SerializeField] private LayerMask interactionMask = Physics2D.DefaultRaycastLayers;

    public bool TryInteract(Vector2 facingDirection, PlayerController player)
    {
        Vector2 direction = DirectionUtility.ToCardinal(facingDirection);
        Vector2 origin = interactionOrigin != null ? (Vector2)interactionOrigin.position : (Vector2)transform.position;

        RaycastHit2D hit = Physics2D.CircleCast(origin, interactionRadius, direction, interactionDistance, interactionMask);
        if (!hit.collider)
            return false;

        if (!hit.collider.TryGetComponent<IInteractable>(out IInteractable interactable))
            return false;

        interactable.Interact(player);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = interactionOrigin != null ? (Vector2)interactionOrigin.position : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, interactionRadius);
        Gizmos.DrawLine(origin, origin + Vector2.down * interactionDistance);
        Gizmos.DrawWireSphere(origin + Vector2.down * interactionDistance, interactionRadius);
    }
}
