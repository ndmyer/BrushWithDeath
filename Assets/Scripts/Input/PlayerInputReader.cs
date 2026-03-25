using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputReader : MonoBehaviour
{
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction interactAction;
    private InputAction lanternAction;
    private InputAction guitarAction;
    private InputAction tempoAction;
    private InputAction pistaAction;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LastMoveInput { get; private set; } = Vector2.down;

    public bool InteractPressed { get; private set; }
    public bool LanternPressed { get; private set; }
    public bool GuitarPressed { get; private set; }
    public bool TempoPressed { get; private set; }
    public bool PistaPressed { get; private set; }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        interactAction = playerInput.actions["Interact"];
        lanternAction = playerInput.actions["Lantern"];
        guitarAction = playerInput.actions["Guitar"];
        tempoAction = playerInput.actions["Tempo"];
        pistaAction = playerInput.actions["Pista"];
    }

    private void Update()
    {
        ReadMovement();
        ReadButtons();
    }

    private void LateUpdate()
    {
        InteractPressed = false;
        LanternPressed = false;
        GuitarPressed = false;
        TempoPressed = false;
        PistaPressed = false;
    }

    private void ReadMovement()
    {
        MoveInput = moveAction.ReadValue<Vector2>();

        if (MoveInput.sqrMagnitude > 1f)
            MoveInput = MoveInput.normalized;

        if (MoveInput.sqrMagnitude > 0.01f)
            LastMoveInput = MoveInput.normalized;
    }

    private void ReadButtons()
    {
        InteractPressed = interactAction.WasPressedThisFrame();
        LanternPressed = lanternAction.WasPressedThisFrame();
        GuitarPressed = guitarAction.WasPressedThisFrame();
        TempoPressed = tempoAction.WasPressedThisFrame();
        PistaPressed = pistaAction.WasPressedThisFrame();
    }
}
