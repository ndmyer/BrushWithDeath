using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Normal,
        Attacking,
        PlayingTempo,
        Damaged,
        Dialogue,
        PistaFocus
    }

    [SerializeField] private Animator animator;

    private PlayerInputReader inputReader;
    private PlayerMotor motor;

    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        HandleStateLogic();
        UpdateAnimator();
    }

    private void HandleStateLogic()
    {
        switch (CurrentState)
        {
            case PlayerState.Normal:
                HandleNormalState();
                break;

            default:
                motor.StopMovement();
                break;
        }
    }

    private void HandleNormalState()
    {
        motor.SetMovementInput(inputReader.MoveInput);

        if (inputReader.InteractPressed)
            Debug.Log("Interact pressed");

        if (inputReader.LanternPressed)
            Debug.Log("Lantern pressed");

        if (inputReader.GuitarPressed)
            Debug.Log("Guitar pressed");

        if (inputReader.TempoPressed)
            Debug.Log("Tempo pressed");

        if (inputReader.PistaPressed)
            Debug.Log("Pista pressed");
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        Vector2 move = motor.MovementInput;
        Vector2 face = motor.FacingDirection;

        animator.SetFloat("MoveX", move.x);
        animator.SetFloat("MoveY", move.y);
        animator.SetFloat("FaceX", face.x);
        animator.SetFloat("FaceY", face.y);
        animator.SetBool("IsMoving", motor.IsMoving);
    }

    public void SetState(PlayerState newState)
    {
        CurrentState = newState;
    }
}
