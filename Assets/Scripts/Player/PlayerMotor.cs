using UnityEngine;

// Moves the player's rigidbody
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private RigidbodyInterpolation2D interpolationMode = RigidbodyInterpolation2D.Interpolate;

    private Rigidbody2D rb;
    private Vector2 movementInput;
    private Vector2 facingDirection = Vector2.down;

    public Vector2 MovementInput => movementInput;
    public Vector2 FacingDirection => facingDirection;
    public bool IsMoving => movementInput.sqrMagnitude > 0.01f;

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

    public void StopMovement()
    {
        movementInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = movementInput * moveSpeed;
    }

    private void ApplyPhysicsSettings()
    {
        if (rb == null)
            return;

        rb.interpolation = interpolationMode;
    }
}
