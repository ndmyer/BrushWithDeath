using UnityEngine;
using UnityEngine.Events;

public class PuzzleEventReceiver : MonoBehaviour
{
    [SerializeField] private PuzzleStateBool targetState;
    [SerializeField] private UnityEvent onActivated;
    [SerializeField] private UnityEvent onDeactivated;
    [SerializeField] private UnityEvent onToggled;

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();
    }

    public void ReceiveActivate()
    {
        targetState?.SetOn();
        onActivated?.Invoke();
    }

    public void ReceiveDeactivate()
    {
        targetState?.SetOff();
        onDeactivated?.Invoke();
    }

    public void ReceiveToggle()
    {
        targetState?.Toggle();
        onToggled?.Invoke();
    }

    public void ReceiveSetState(bool isOn)
    {
        targetState?.SetState(isOn);

        if (isOn)
            onActivated?.Invoke();
        else
            onDeactivated?.Invoke();
    }
}
