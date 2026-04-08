using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SlidingBlockPuzzleController : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField] private PushBlockSocket[] requiredSockets;
    [SerializeField] private SlidingPushBlock[] controlledBlocks;
    [SerializeField] private bool resetAssignedBlocksOnAwake = true;
    [SerializeField] private bool lockAfterSolved = true;

    [Header("Completion")]
    [SerializeField] private PuzzleStateBool completionState;
    [SerializeField] private PuzzleEventEmitter completionEmitter;
    [SerializeField] private UnityEvent onSolved;
    [SerializeField] private UnityEvent onReset;

    private bool isSolved;

    private void Awake()
    {
        if (completionState == null)
            completionState = GetComponent<PuzzleStateBool>();

        if (completionEmitter == null)
            completionEmitter = GetComponent<PuzzleEventEmitter>();

        if (resetAssignedBlocksOnAwake)
            ResetPuzzle(false);
    }

    private void OnEnable()
    {
        SubscribeToSockets();
        EvaluateCompletion();
    }

    private void OnDisable()
    {
        UnsubscribeFromSockets();
    }

    public void ResetPuzzle()
    {
        ResetPuzzle(true);
    }

    private void HandleSocketOccupancyChanged(PushBlockSocket changedSocket, bool isOccupied)
    {
        if (changedSocket == null)
            return;

        if (isSolved && lockAfterSolved)
            return;

        EvaluateCompletion();
    }

    private void EvaluateCompletion()
    {
        bool allSocketsOccupied = false;

        if (requiredSockets != null && requiredSockets.Length > 0)
        {
            allSocketsOccupied = true;

            for (int i = 0; i < requiredSockets.Length; i++)
            {
                if (requiredSockets[i] == null || !requiredSockets[i].IsOccupied)
                {
                    allSocketsOccupied = false;
                    break;
                }
            }
        }

        if (allSocketsOccupied)
            SolvePuzzle();
        else if (!isSolved)
            SetCompletionState(false);
    }

    private void SolvePuzzle()
    {
        if (isSolved)
            return;

        isSolved = true;
        SetCompletionState(true);
        onSolved?.Invoke();
    }

    private void ResetPuzzle(bool invokeResetEvent)
    {
        isSolved = false;
        SetCompletionState(false);
        ResetAssignedSockets();
        ResetAssignedBlocks();

        if (invokeResetEvent)
            onReset?.Invoke();
    }

    private void SetCompletionState(bool isComplete)
    {
        completionState?.SetState(isComplete);
        completionEmitter?.EmitSetState(isComplete);
    }

    private void ResetAssignedSockets()
    {
        if (requiredSockets == null)
            return;

        HashSet<int> clearedSockets = new();

        foreach (PushBlockSocket socket in requiredSockets)
        {
            if (socket == null)
                continue;

            if (!clearedSockets.Add(socket.GetInstanceID()))
                continue;

            socket.ClearSocket(false);
        }
    }

    private void ResetAssignedBlocks()
    {
        if (controlledBlocks == null)
            return;

        HashSet<int> resetBlocks = new();

        foreach (SlidingPushBlock block in controlledBlocks)
        {
            if (block == null)
                continue;

            if (!resetBlocks.Add(block.GetInstanceID()))
                continue;

            block.ResetToSpawn();
        }
    }

    private void SubscribeToSockets()
    {
        if (requiredSockets == null)
            return;

        HashSet<int> subscribedSockets = new();

        foreach (PushBlockSocket socket in requiredSockets)
        {
            if (socket == null)
                continue;

            if (!subscribedSockets.Add(socket.GetInstanceID()))
                continue;

            socket.OccupancyChanged -= HandleSocketOccupancyChanged;
            socket.OccupancyChanged += HandleSocketOccupancyChanged;
        }
    }

    private void UnsubscribeFromSockets()
    {
        if (requiredSockets == null)
            return;

        HashSet<int> unsubscribedSockets = new();

        foreach (PushBlockSocket socket in requiredSockets)
        {
            if (socket == null)
                continue;

            if (!unsubscribedSockets.Add(socket.GetInstanceID()))
                continue;

            socket.OccupancyChanged -= HandleSocketOccupancyChanged;
        }
    }
}
