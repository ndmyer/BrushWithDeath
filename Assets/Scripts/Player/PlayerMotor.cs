using UnityEngine;

// Moves the player's rigidbody
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4.5f;

    private Rigidbody2D rb;
    private Vector2 movementInput;
    private Vector2 facingDirection = Vector2.down;

    public Vector2 MovementInput => movementInput;
    public Vector2 FacingDirection => facingDirection;
    public bool IsMoving => movementInput.sqrMagnitude > 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
}
