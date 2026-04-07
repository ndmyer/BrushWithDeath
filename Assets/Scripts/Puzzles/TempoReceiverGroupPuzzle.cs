using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TempoReceiverGroupPuzzle : MonoBehaviour
{
    [Header("Receivers")]
    [SerializeField] private TempoReceiver[] requiredReceivers;
    [SerializeField] private bool latchMatchedReceivers = true;
    [SerializeField] private bool resetProgressOnAwake = true;

    [Header("Completion")]
    [SerializeField] private PuzzleStateBool completionState;
    [SerializeField] private PuzzleEventEmitter completionEmitter;
    [SerializeField] private UnityEvent onSolved;
    [SerializeField] private UnityEvent onReset;

    private readonly List<TempoReceiver> trackedReceivers = new();
    private bool[] receiverCompletedStates;
    private bool isSolved;

    private void Awake()
    {
        if (completionState == null)
            completionState = GetComponent<PuzzleStateBool>();

        if (completionEmitter == null)
            completionEmitter = GetComponent<PuzzleEventEmitter>();

        RebuildReceiverCache();

        if (resetProgressOnAwake)
            ResetPuzzle(false);
    }

    private void OnEnable()
    {
        RebuildReceiverCache();
        SubscribeToReceivers();
        SyncFromReceivers();
    }

    private void OnDisable()
    {
        UnsubscribeFromReceivers();
    }

    public void ResetPuzzle()
    {
        ResetPuzzle(true);
    }

    private void ResetPuzzle(bool invokeResetEvent)
    {
        EnsureCompletionArray();

        for (int i = 0; i < receiverCompletedStates.Length; i++)
            receiverCompletedStates[i] = false;

        isSolved = false;
        completionState?.SetState(false);
        completionEmitter?.EmitSetState(false);

        if (invokeResetEvent)
            onReset?.Invoke();
    }

    private void HandleReceiverMatchChanged(TempoReceiver receiver, bool isMatch)
    {
        int receiverIndex = trackedReceivers.IndexOf(receiver);
        if (receiverIndex < 0)
            return;

        EnsureCompletionArray();

        if (latchMatchedReceivers)
        {
            if (isMatch)
                receiverCompletedStates[receiverIndex] = true;
        }
        else
        {
            receiverCompletedStates[receiverIndex] = isMatch;
        }

        EvaluateCompletion();
    }

    private void SyncFromReceivers()
    {
        EnsureCompletionArray();

        for (int i = 0; i < trackedReceivers.Count; i++)
        {
            TempoReceiver receiver = trackedReceivers[i];
            if (receiver == null)
                continue;

            if (latchMatchedReceivers)
            {
                if (receiver.CurrentMatch)
                    receiverCompletedStates[i] = true;
            }
            else
            {
                receiverCompletedStates[i] = receiver.CurrentMatch;
            }
        }

        EvaluateCompletion();
    }

    private void EvaluateCompletion()
    {
        bool allReceiversCompleted = trackedReceivers.Count > 0;

        for (int i = 0; i < trackedReceivers.Count; i++)
        {
            if (receiverCompletedStates == null || i >= receiverCompletedStates.Length || !receiverCompletedStates[i])
            {
                allReceiversCompleted = false;
                break;
            }
        }

        bool changed = SetSolvedState(allReceiversCompleted);
        if (!changed)
            return;

        if (allReceiversCompleted)
            onSolved?.Invoke();
        else
            onReset?.Invoke();
    }

    private bool SetSolvedState(bool solved)
    {
        if (isSolved == solved)
            return false;

        isSolved = solved;
        completionState?.SetState(solved);
        completionEmitter?.EmitSetState(solved);
        return true;
    }

    private void RebuildReceiverCache()
    {
        trackedReceivers.Clear();

        if (requiredReceivers == null)
        {
            receiverCompletedStates = System.Array.Empty<bool>();
            return;
        }

        HashSet<int> receiverIds = new();

        foreach (TempoReceiver receiver in requiredReceivers)
        {
            if (receiver == null)
                continue;

            if (!receiverIds.Add(receiver.GetInstanceID()))
                continue;

            trackedReceivers.Add(receiver);
        }

        if (receiverCompletedStates == null || receiverCompletedStates.Length != trackedReceivers.Count)
            receiverCompletedStates = new bool[trackedReceivers.Count];
    }

    private void EnsureCompletionArray()
    {
        if (receiverCompletedStates == null || receiverCompletedStates.Length != trackedReceivers.Count)
            receiverCompletedStates = new bool[trackedReceivers.Count];
    }

    private void SubscribeToReceivers()
    {
        foreach (TempoReceiver receiver in trackedReceivers)
        {
            if (receiver == null)
                continue;

            receiver.MatchChanged -= HandleReceiverMatchChanged;
            receiver.MatchChanged += HandleReceiverMatchChanged;
        }
    }

    private void UnsubscribeFromReceivers()
    {
        foreach (TempoReceiver receiver in trackedReceivers)
        {
            if (receiver == null)
                continue;

            receiver.MatchChanged -= HandleReceiverMatchChanged;
        }
    }
}
