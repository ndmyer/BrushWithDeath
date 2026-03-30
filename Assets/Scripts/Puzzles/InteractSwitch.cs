using UnityEngine;

public class InteractSwitch : MonoBehaviour, IInteractable
{
    public enum SwitchMode
    {
        Toggle,
        SetOn,
        SetOff
    }

    [SerializeField] private SwitchMode switchMode = SwitchMode.Toggle;
    [SerializeField] private PuzzleStateBool targetState;
    [SerializeField] private PuzzleEventEmitter eventEmitter;

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();
    }

    public void Interact(PlayerController player)
    {
        bool resultingState = ApplyStateChange();

        if (eventEmitter == null)
            return;

        eventEmitter.EmitSetState(resultingState);
    }

    private bool ApplyStateChange()
    {
        if (targetState == null)
            return switchMode != SwitchMode.SetOff;

        switch (switchMode)
        {
            case SwitchMode.SetOn:
                targetState.SetOn();
                break;
            case SwitchMode.SetOff:
                targetState.SetOff();
                break;
            default:
                targetState.Toggle();
                break;
        }

        return targetState.Value;
    }
}
