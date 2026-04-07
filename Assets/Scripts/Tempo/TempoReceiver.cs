using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TempoReceiver : MonoBehaviour
{
    private static readonly HashSet<TempoReceiver> activeReceivers = new();

    [Serializable]
    public class TempoEvent : UnityEvent<TempoBand> { }

    [SerializeField] private TempoBand requiredTempo = TempoBand.Mid;
    [SerializeField] private bool listenToGlobalTempo = true;
    [SerializeField] private TempoService tempoService;
    [SerializeField] private Collider2D proximityCollider;
    [SerializeField] private PuzzleStateBool targetState;
    [SerializeField] private PuzzleEventEmitter eventEmitter;
    [SerializeField] private bool invertMatchResult;
    [SerializeField] private UnityEvent onTempoMatched;
    [SerializeField] private UnityEvent onTempoMismatched;
    [SerializeField] private TempoEvent onTempoReceived;

    public static IReadOnlyCollection<TempoReceiver> ActiveReceivers => activeReceivers;
    public TempoBand RequiredTempo => requiredTempo;
    public bool HasReceivedTempo => hasReceivedTempo;
    public bool CurrentMatch => currentMatch;
    public event Action<TempoReceiver, bool> MatchChanged;

    private bool hasReceivedTempo;
    private bool currentMatch;
    private TempoBand lastReceivedTempo = TempoBand.Mid;

    private void Awake()
    {
        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (proximityCollider == null)
            proximityCollider = GetComponent<Collider2D>();

        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();
    }

    private void OnEnable()
    {
        activeReceivers.Add(this);
        hasReceivedTempo = false;
        currentMatch = false;

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
        activeReceivers.Remove(this);

        if (tempoService != null)
            tempoService.TempoUpdated -= HandleTempoUpdated;
    }

    public void ReceiveTempo(TempoBand tempo)
    {
        if (hasReceivedTempo && lastReceivedTempo == tempo)
            return;

        hasReceivedTempo = true;
        lastReceivedTempo = tempo;

        bool isMatch = tempo == requiredTempo;
        if (invertMatchResult)
            isMatch = !isMatch;

        bool matchChanged = currentMatch != isMatch;
        currentMatch = isMatch;

        targetState?.SetState(isMatch);
        eventEmitter?.EmitSetState(isMatch);
        onTempoReceived?.Invoke(tempo);

        if (matchChanged)
            MatchChanged?.Invoke(this, currentMatch);

        if (isMatch)
            onTempoMatched?.Invoke();
        else
            onTempoMismatched?.Invoke();
    }

    public Vector3 GetClosestBroadcastPoint(Vector3 origin)
    {
        if (proximityCollider != null && proximityCollider.enabled)
            return proximityCollider.ClosestPoint(origin);

        return transform.position;
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
