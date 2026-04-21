using UnityEngine;

// Moves the player's rigidbody
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private RigidbodyInterpolation2D interpolationMode = RigidbodyInterpolation2D.Interpolate;

    private Rigidbody2D rb;
    private Vector2 movementInput;
    private Vector2 forcedMovementInput;
    private Vector2 facingDirection = Vector2.down;
    private float forcedMoveSpeed;
    private bool hasForcedMovement;

    public Vector2 MovementInput => hasForcedMovement ? forcedMovementInput : movementInput;
    public Vector2 FacingDirection => facingDirection;
    public bool IsMoving => MovementInput.sqrMagnitude > 0.01f;
    public bool HasForcedMovement => hasForcedMovement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ApplyPhysicsSettings();
    }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        ApplyPhysicsSettings();
    }

    private void OnValidate()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        ApplyPhysicsSettings();
    }

    public void SetMovementInput(Vector2 input)
    {
        movementInput = input;

        if (input.sqrMagnitude > 0.01f)
            facingDirection = DirectionUtility.ToCardinal(input);
    }

    public void SetForcedMovement(Vector2 direction, float speed)
    {
        if (speed <= 0f || direction.sqrMagnitude <= 0.0001f)
        {
            ClearForcedMovement();
            return;
        }

        forcedMovementInput = direction.normalized;
        forcedMoveSpeed = speed;
        hasForcedMovement = true;
        facingDirection = DirectionUtility.ToCardinal(forcedMovementInput);
    }

    public void ClearForcedMovement()
    {
        hasForcedMovement = false;
        forcedMovementInput = Vector2.zero;
        forcedMoveSpeed = 0f;
        rb.linearVelocity = Vector2.zero;
    }

    public void StopMovement()
    {
        movementInput = Vector2.zero;

        if (!hasForcedMovement)
            rb.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = hasForcedMovement
            ? forcedMovementInput * forcedMoveSpeed
            : movementInput * moveSpeed;
    }

    private void ApplyPhysicsSettings()
    {
        if (rb == null)
            return;

        rb.interpolation = interpolationMode;
    }
}
