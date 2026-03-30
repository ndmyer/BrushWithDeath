using System;
using UnityEngine;
using UnityEngine.Events;

public class TempoReceiver : MonoBehaviour
{
    public enum TempoBand
    {
        Slow,
        Mid,
        Fast,
        Intense
    }

    [Serializable]
    public class TempoEvent : UnityEvent<TempoBand> { }

    [SerializeField] private TempoBand requiredTempo = TempoBand.Mid;
    [SerializeField] private PuzzleStateBool targetState;
    [SerializeField] private PuzzleEventEmitter eventEmitter;
    [SerializeField] private bool invertMatchResult;
    [SerializeField] private UnityEvent onTempoMatched;
    [SerializeField] private UnityEvent onTempoMismatched;
    [SerializeField] private TempoEvent onTempoReceived;

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();
    }

    public void ReceiveTempo(TempoBand tempo)
    {
        bool isMatch = tempo == requiredTempo;
        if (invertMatchResult)
            isMatch = !isMatch;

        targetState?.SetState(isMatch);
        eventEmitter?.EmitSetState(isMatch);
        onTempoReceived?.Invoke(tempo);

        if (isMatch)
            onTempoMatched?.Invoke();
        else
            onTempoMismatched?.Invoke();
    }
}
