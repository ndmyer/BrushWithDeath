using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerInteractor))]
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
    private PlayerInteractor interactor;

    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
        interactor = GetComponent<PlayerInteractor>();
    }

    private void Update()
    {
        HandleStateLogic();
        UpdateAnimator();
        inputReader.ClearFrameButtons();
    }

    private void HandleStateLogic()
    {
        switch (CurrentState)
        {
            case PlayerState.Normal:
                HandleNormalState();
                break;

            case PlayerState.PistaFocus:
                HandlePistaFocusState();
                break;

            default:
                motor.StopMovement();
                break;
        }
    }

    private void HandleNormalState()
    {
        // if (inputReader.PistaHeld)
        // {
        //     SetState(PlayerState.PistaFocus);
        //     motor.StopMovement();
        //     return;
        // }

        motor.SetMovementInput(inputReader.MoveInput);

        if (inputReader.InteractPressed)
            HandleInteract();

        if (inputReader.LanternPressed)
            HandleLantern();

        if (inputReader.GuitarPressed)
            HandleGuitar();

        if (inputReader.TempoPressed)
            HandleTempo();

        if (inputReader.PistaPressed)
            HandlePista();
    }

    private void HandlePistaFocusState()
    {
        motor.StopMovement();

        if (!inputReader.PistaHeld)
        {
            SetState(PlayerState.Normal);
            return;
        }
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

    private void HandleInteract()
    {
        Debug.Log("Player Interacted.");
        interactor.TryInteract(motor.FacingDirection, this);
    }

    private void HandleLantern()
    {
        Debug.Log("Player used lantern.");
        interactor.TryLight(motor.FacingDirection, this);
    }

    private void HandleGuitar()
    {
        Debug.Log("Guitar action not implemented yet.");
    }

    private void HandleTempo()
    {
        Debug.Log("Tempo action not implemented yet.");
    }

    private void HandlePista()
    {
        if (inputReader.PistaRecallPressed)
            Debug.Log("Pista recall requested.");

        Debug.Log("Entered Pista focus.");
        SetState(PlayerState.PistaFocus);
        motor.StopMovement();
    }
}
