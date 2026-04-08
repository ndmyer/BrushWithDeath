using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class PushBlockSocket : MonoBehaviour
{
    [SerializeField] private SlidingPushBlock requiredBlock;
    [SerializeField] private Transform snapPoint;
    [SerializeField] private PuzzleStateBool targetState;
    [SerializeField] private PuzzleEventEmitter eventEmitter;
    [SerializeField] private UnityEvent onOccupied;
    [SerializeField] private UnityEvent onCleared;

    public bool IsOccupied => occupyingBlock != null;
    public SlidingPushBlock OccupyingBlock => occupyingBlock;
    public event Action<PushBlockSocket, bool> OccupancyChanged;

    private SlidingPushBlock occupyingBlock;

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponent<PuzzleStateBool>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryOccupyFromCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryOccupyFromCollider(other);
    }

    public bool TryOccupy(SlidingPushBlock block)
    {
        if (block == null)
            return false;

        if (occupyingBlock == block)
            return true;

        if (IsOccupied || !CanAccept(block))
            return false;

        occupyingBlock = block;
        block.LockInSocket(this, GetSnapPosition());
        ApplyOccupiedState(true, true);
        return true;
    }

    public void ClearSocket(bool resetOccupyingBlock)
    {
        SlidingPushBlock previousBlock = occupyingBlock;
        if (previousBlock == null && targetState == null && eventEmitter == null)
            return;

        occupyingBlock = null;

        if (previousBlock != null)
            previousBlock.UnlockFromSocket(this);

        bool shouldInvokeChangeEvents = previousBlock != null || (targetState != null && targetState.Value);
        ApplyOccupiedState(false, shouldInvokeChangeEvents);

        if (resetOccupyingBlock && previousBlock != null)
            previousBlock.ResetToSpawn();
    }

    private bool CanAccept(SlidingPushBlock block)
    {
        return requiredBlock == null || requiredBlock == block;
    }

    private void TryOccupyFromCollider(Collider2D other)
    {
        if (other == null)
            return;

        SlidingPushBlock block = other.GetComponent<SlidingPushBlock>();
        if (block == null)
            block = other.GetComponentInParent<SlidingPushBlock>();
        if (block == null)
            block = other.GetComponentInChildren<SlidingPushBlock>();
        if (block == null)
            return;

        TryOccupy(block);
    }

    private void ApplyOccupiedState(bool isOccupied, bool invokeChangeEvents)
    {
        targetState?.SetState(isOccupied);
        eventEmitter?.EmitSetState(isOccupied);

        if (!invokeChangeEvents)
            return;

        OccupancyChanged?.Invoke(this, isOccupied);

        if (isOccupied)
            onOccupied?.Invoke();
        else
            onCleared?.Invoke();
    }

    private Vector2 GetSnapPosition()
    {
        return snapPoint != null ? snapPoint.position : transform.position;
    }
}
