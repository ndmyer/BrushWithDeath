using System;
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

    public PuzzleStateBool TargetState => targetState;
    public bool CurrentValue => targetState != null && targetState.Value;
    public event Action<InteractSwitch, bool> Activated;

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();
    }

    public void Interact(PlayerController player)
    {
        Activate();
    }

    public void Activate()
    {
        bool resultingState = ApplyStateChange();

        EmitResult(resultingState);
        Activated?.Invoke(this, resultingState);
    }

    public void SetState(bool isOn)
    {
        bool resultingState = ApplyExplicitStateChange(isOn);

        EmitResult(resultingState);
    }

    public void SetOn()
    {
        SetState(true);
    }

    public void SetOff()
    {
        SetState(false);
    }

    private void EmitResult(bool resultingState)
    {

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

    private bool ApplyExplicitStateChange(bool isOn)
    {
        if (targetState == null)
            return isOn;

        targetState.SetState(isOn);
        return targetState.Value;
    }
}
