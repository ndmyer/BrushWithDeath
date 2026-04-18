using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerDamageReceiver))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerProgression))]
[RequireComponent(typeof(TempoGroundIndicator))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Normal,
        Attacking,
        PlayingTempo,
        Damaged,
        Dialogue,
        PistaFocus,
        Dead
    }

    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PistaController pistaController;
    [SerializeField] private TempoService tempoService;

    private PlayerInputReader inputReader;
    private PlayerMotor motor;
    private PlayerInteractor interactor;

    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    private void Awake()
    {
        EnsureRequiredComponent<PlayerDamageReceiver>();
        EnsureRequiredComponent<PlayerHealth>();
        EnsureRequiredComponent<PlayerProgression>();
        EnsureRequiredComponent<TempoGroundIndicator>();

        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
        interactor = GetComponent<PlayerInteractor>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (pistaController == null)
            pistaController = FindAnyObjectByType<PistaController>();

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();
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

            case PlayerState.PlayingTempo:
                HandleTempoFocusState();
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
        if (inputReader.TempoHeld)
        {
            EnterTempoFocus();
            HandleTempoFocusState();
            return;
        }

        motor.SetMovementInput(inputReader.MoveInput);

        if (inputReader.InteractPressed)
            HandleInteract();

        if (inputReader.LanternPressed)
            HandleLantern();

        if (inputReader.GuitarPressed)
            HandleGuitar();

        if (inputReader.PistaPressed)
            HandlePista();
    }

    private void HandleTempoFocusState()
    {
        motor.StopMovement();

        if (!inputReader.TempoHeld)
        {
            ExitTempoFocus(allowGraceCompletion: true);
            return;
        }

        if (inputReader.TryGetTempoSelectionHeld(out TempoBand selectedTempo))
            tempoService?.BeginChannel(selectedTempo);
    }

    private void HandlePistaFocusState()
    {
        motor.StopMovement();

        if (inputReader.PistaRecallPressed)
        {
            pistaController?.RecallToPlayer();
            SetState(PlayerState.Normal);
            return;
        }

        if (inputReader.GuitarPressed)
            pistaController?.TriggerPulseAttack();

        pistaController?.ProcessAimInput(inputReader.MoveInput);

        if (!inputReader.PistaHeld)
        {
            pistaController?.EndAiming();
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

        if (spriteRenderer != null)
            spriteRenderer.flipX = face.x < -0.01f;
    }

    public void SetState(PlayerState newState)
    {
        CurrentState = newState;
    }

    public void EnterDeathState(bool snapPistaToPlayer = false)
    {
        tempoService?.CancelChannel(allowGraceCompletion: false);

        if (snapPistaToPlayer)
            pistaController?.SnapToPlayer();
        else
            pistaController?.EndAiming();

        motor.StopMovement();
        SetState(PlayerState.Dead);
    }

    public void ExitDeathState()
    {
        motor.StopMovement();
        SetState(PlayerState.Normal);
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
        interactor.TryGuitarHit(motor.FacingDirection);
    }

    private void HandlePista()
    {
        if (inputReader.PistaRecallPressed)
        {
            pistaController?.RecallToPlayer();
            Debug.Log("Pista recall requested.");
            return;
        }

        Debug.Log("Entered Pista focus.");
        pistaController?.BeginAiming();
        SetState(PlayerState.PistaFocus);
        motor.StopMovement();
    }

    public bool InterruptTempoFocus(bool allowGraceCompletion)
    {
        if (CurrentState != PlayerState.PlayingTempo)
            return false;

        ExitTempoFocus(allowGraceCompletion);
        return true;
    }

    private void EnsureRequiredComponent<T>()
        where T : Component
    {
        if (!TryGetComponent<T>(out _))
            gameObject.AddComponent<T>();
    }

    private void EnterTempoFocus()
    {
        if (CurrentState == PlayerState.PlayingTempo)
            return;

        SetState(PlayerState.PlayingTempo);
        motor.StopMovement();
    }

    private void ExitTempoFocus(bool allowGraceCompletion)
    {
        tempoService?.CancelChannel(allowGraceCompletion);
        SetState(PlayerState.Normal);
    }
}
