using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 0.75f;
    [SerializeField] private float interactionRadius = 0.2f;

    private Collider2D[] selfColliders;

    private void Awake()
    {
        selfColliders = GetComponents<Collider2D>();
    }

    public bool TryInteract(Vector2 facingDirection, PlayerController player)
    {
        if (!TryCast(facingDirection, out RaycastHit2D hit))
            return false;

        if (!hit.collider.TryGetComponent<IInteractable>(out IInteractable interactable))
            return false;

        interactable.Interact(player);
        return true;
    }

    public bool TryLight(Vector2 facingDirection, PlayerController player)
    {
        if (!TryCast(facingDirection, out RaycastHit2D hit))
            return false;

        if (!hit.collider.TryGetComponent<ILightable>(out ILightable lightable))
            return false;

        lightable.Light(player);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = transform.position;

        Vector2 direction = Vector2.down;
        PlayerMotor motor = GetComponent<PlayerMotor>();
        if (motor != null && motor.FacingDirection.sqrMagnitude > 0.001f)
            direction = motor.FacingDirection;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, interactionRadius);
        Gizmos.DrawLine(origin, origin + direction * interactionDistance);
        Gizmos.DrawWireSphere(origin + direction * interactionDistance, interactionRadius);
    }

    private bool TryCast(Vector2 facingDirection, out RaycastHit2D validHit)
    {
        Vector2 direction = DirectionUtility.ToCardinal(facingDirection);
        Vector2 origin = transform.position;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, interactionRadius, direction, interactionDistance);

        foreach (RaycastHit2D hit in hits)
        {
            if (!hit.collider || IsSelfCollider(hit.collider))
                continue;

            validHit = hit;
            return true;
        }

        validHit = default;
        return false;
    }

    private bool IsSelfCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        foreach (Collider2D selfCollider in selfColliders)
        {
            if (collider == selfCollider)
                return true;
        }

        return false;
    }
}
