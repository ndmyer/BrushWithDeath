using System;
using UnityEngine;
using UnityEngine.Events;

public class TempoReceiver : MonoBehaviour
{

    [Serializable]
    public class TempoEvent : UnityEvent<TempoBand> { }

    [SerializeField] private TempoBand requiredTempo = TempoBand.Mid;
    [SerializeField] private bool listenToGlobalTempo = true;
    [SerializeField] private TempoService tempoService;
    [SerializeField] private PuzzleStateBool targetState;
    [SerializeField] private PuzzleEventEmitter eventEmitter;
    [SerializeField] private bool invertMatchResult;
    [SerializeField] private UnityEvent onTempoMatched;
    [SerializeField] private UnityEvent onTempoMismatched;
    [SerializeField] private TempoEvent onTempoReceived;

    private void Awake()
    {
        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();
    }

    private void OnEnable()
    {
        if (!listenToGlobalTempo)
            return;

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (tempoService == null)
            return;

        tempoService.TempoUpdated += HandleTempoUpdated;
        HandleTempoUpdated(tempoService.GetCurrentSnapshot());
    }

    private void OnDisable()
    {
        if (tempoService != null)
            tempoService.TempoUpdated -= HandleTempoUpdated;
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

    private void HandleTempoUpdated(TempoStateSnapshot snapshot)
    {
        if (snapshot.UpdateType != TempoUpdateType.Initialized &&
            snapshot.UpdateType != TempoUpdateType.ChannelCompleted)
        {
            return;
        }

        ReceiveTempo(snapshot.CurrentTempo);
    }
}
