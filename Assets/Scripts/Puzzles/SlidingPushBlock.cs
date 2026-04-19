using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class SlidingPushBlock : MonoBehaviour, IKnockbackable
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D spawnAreaCollider;
    [SerializeField] private Transform spawnAnchor;
    [SerializeField] private Transform pathStartPoint;
    [SerializeField] private Transform pathEndPoint;
    [SerializeField] private TempoService tempoService;

    [Header("Path Movement")]
    [SerializeField] private bool moveOnPath;
    [SerializeField] private bool startMovingTowardEnd = true;
    [SerializeField, Min(0f)] private float slowPathMoveSpeed = 0.75f;
    [SerializeField, FormerlySerializedAs("pathMoveSpeed"), Min(0f)] private float midPathMoveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float fastPathMoveSpeed = 2.125f;
    [SerializeField, Min(0f)] private float intensePathMoveSpeed = 2.625f;

    [Header("Reset")]
    [SerializeField, Min(0f)] private float resetDelay = 1.5f;
    [SerializeField] private Vector2 spawnAreaMargin = new(0.25f, 0.25f);

    [Header("Slow Tempo")]
    [SerializeField, Min(0f)] private float slowSlideSpeed = 1.5f;
    [SerializeField] private float slowDirectionOffsetDegrees;

    [Header("Mid Tempo")]
    [SerializeField, Min(0f)] private float midSlideSpeed = 3f;
    [SerializeField] private float midDirectionOffsetDegrees = 12f;

    [Header("Fast Tempo")]
    [SerializeField, Min(0f)] private float fastSlideSpeed = 4.25f;
    [SerializeField] private float fastDirectionOffsetDegrees = 18f;

    [Header("Intense Tempo")]
    [SerializeField, Min(0f)] private float intenseSlideSpeed = 5.25f;
    [SerializeField] private float intenseDirectionOffsetDegrees = 24f;

    public bool IsLockedInSocket { get; private set; }
    public bool IsSliding => slideSpeed > 0.001f;
    public PushBlockSocket CurrentSocket => currentSocket;
    public Vector2 SpawnPosition => spawnPosition;

    private PushBlockSocket currentSocket;
    private Vector2 slideDirection;
    private float slideSpeed;
    private float resetTimer;
    private bool resetCheckActive;
    private Vector2 spawnPosition;
    private float spawnRotation;
    private bool movingTowardPathEnd;
    private bool pathMovementSuspended;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        movingTowardPathEnd = startMovingTowardEnd;
        pathMovementSuspended = false;
        CacheSpawnTransform();
        StopSliding();
        SyncTransformToBody();
    }

    private void FixedUpdate()
    {
        if (IsLockedInSocket)
            return;

        if (IsSliding)
        {
            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
            Vector2 targetPosition = currentPosition + (slideDirection * slideSpeed * Time.fixedDeltaTime);

            if (body != null)
                body.MovePosition(targetPosition);
            else
                transform.position = targetPosition;

            return;
        }

        float currentPathMoveSpeed = GetPathMoveSpeed();
        if (pathMovementSuspended || !TryGetPathEndpoints(out Vector2 pathStart, out Vector2 pathEnd) || currentPathMoveSpeed <= 0.001f)
            return;

        MoveAlongPath(pathStart, pathEnd, currentPathMoveSpeed);
    }

    private void Update()
    {
        if (!resetCheckActive || IsLockedInSocket)
            return;

        resetTimer += Time.deltaTime;
        if (resetTimer < resetDelay)
            return;

        if (ShouldResetAfterDelay())
        {
            ResetToSpawn();
            return;
        }

        if (!IsSliding)
            resetCheckActive = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!enabled || IsLockedInSocket || collision == null)
            return;

        StopSliding();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!enabled || IsLockedInSocket || collision == null)
            return;

        StopSliding();
    }

    public void ApplyKnockback(Vector2 direction, float strengthMultiplier = 1f)
    {
        if (IsLockedInSocket)
            return;

        Vector2 cardinalDirection = DirectionUtility.ToCardinal(direction);
        TempoBand currentTempo = tempoService != null ? tempoService.CurrentTempo : TempoBand.Mid;
        float directionOffset = GetDirectionOffsetDegrees(currentTempo);
        float speed = GetSlideSpeed(currentTempo) * Mathf.Max(0f, strengthMultiplier);

        pathMovementSuspended = true;
        slideDirection = Rotate(cardinalDirection, directionOffset).normalized;
        slideSpeed = speed;
        resetTimer = 0f;
        resetCheckActive = true;
    }

    public void ApplyKnockbackFrom(Vector2 sourcePosition, float strengthMultiplier = 1f)
    {
        ApplyKnockback((Vector2)transform.position - sourcePosition, strengthMultiplier);
    }

    public void LockInSocket(PushBlockSocket socket, Vector2 snappedPosition)
    {
        if (socket == null)
            return;

        currentSocket = socket;
        IsLockedInSocket = true;
        pathMovementSuspended = true;
        resetCheckActive = false;
        resetTimer = 0f;
        StopSliding();
        SetWorldPosition(snappedPosition);
    }

    public void UnlockFromSocket(PushBlockSocket socket)
    {
        if (socket == null || currentSocket != socket)
            return;

        currentSocket = null;
        IsLockedInSocket = false;
    }

    public void ResetToSpawn()
    {
        if (currentSocket != null)
            currentSocket.ClearSocket(false);

        IsLockedInSocket = false;
        currentSocket = null;
        resetCheckActive = false;
        resetTimer = 0f;
        movingTowardPathEnd = startMovingTowardEnd;
        pathMovementSuspended = false;
        StopSliding();

        CacheSpawnTransform();

        SetWorldPosition(spawnPosition);
        SetWorldRotation(spawnRotation);
    }

    public void RefreshSpawnPoint()
    {
        CacheSpawnTransform();
    }

    private void CacheSpawnTransform()
    {
        if (pathStartPoint != null)
        {
            spawnPosition = pathStartPoint.position;
            spawnRotation = pathStartPoint.eulerAngles.z;
            return;
        }

        if (spawnAnchor != null)
        {
            spawnPosition = spawnAnchor.position;
            spawnRotation = spawnAnchor.eulerAngles.z;
            return;
        }

        if (body != null)
        {
            spawnPosition = body.position;
            spawnRotation = body.rotation;
            return;
        }

        spawnPosition = transform.position;
        spawnRotation = transform.eulerAngles.z;
    }

    private void MoveAlongPath(Vector2 pathStart, Vector2 pathEnd, float currentPathMoveSpeed)
    {
        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
        Vector2 targetPosition = movingTowardPathEnd ? pathEnd : pathStart;
        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, currentPathMoveSpeed * Time.fixedDeltaTime);

        if (body != null)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        if ((targetPosition - nextPosition).sqrMagnitude > 0.0001f)
            return;

        SetWorldPosition(targetPosition);
        movingTowardPathEnd = !movingTowardPathEnd;
    }

    private bool TryGetPathEndpoints(out Vector2 pathStart, out Vector2 pathEnd)
    {
        pathStart = pathStartPoint != null
            ? (Vector2)pathStartPoint.position
            : spawnAnchor != null
                ? (Vector2)spawnAnchor.position
                : Application.isPlaying
                    ? spawnPosition
                    : body != null
                        ? body.position
                        : (Vector2)transform.position;

        if (!moveOnPath || pathEndPoint == null)
        {
            pathEnd = pathStart;
            return false;
        }

        pathEnd = pathEndPoint.position;
        return (pathEnd - pathStart).sqrMagnitude > 0.0001f;
    }

    private bool ShouldResetAfterDelay()
    {
        if (pathMovementSuspended && TryGetPathEndpoints(out _, out _))
            return true;

        return !IsWithinSpawnAreaWithMargin();
    }

    private float GetPathMoveSpeed()
    {
        TempoBand currentTempo = tempoService != null ? tempoService.CurrentTempo : TempoBand.Mid;
        return currentTempo switch
        {
            TempoBand.Slow => slowPathMoveSpeed,
            TempoBand.Fast => fastPathMoveSpeed,
            TempoBand.Intense => intensePathMoveSpeed,
            _ => midPathMoveSpeed
        };
    }

    private bool IsWithinSpawnAreaWithMargin()
    {
        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;

        if (spawnAreaCollider == null || !spawnAreaCollider.enabled)
        {
            Vector2 fallbackMargin = new(
                Mathf.Max(0.01f, spawnAreaMargin.x),
                Mathf.Max(0.01f, spawnAreaMargin.y));

            Vector2 offset = currentPosition - spawnPosition;
            return Mathf.Abs(offset.x) <= fallbackMargin.x && Mathf.Abs(offset.y) <= fallbackMargin.y;
        }

        Bounds bounds = spawnAreaCollider.bounds;
        bounds.Expand(new Vector3(spawnAreaMargin.x * 2f, spawnAreaMargin.y * 2f, 0f));
        return bounds.Contains(currentPosition);
    }

    private float GetSlideSpeed(TempoBand tempo)
    {
        return tempo switch
        {
            TempoBand.Slow => slowSlideSpeed,
            TempoBand.Fast => fastSlideSpeed,
            TempoBand.Intense => intenseSlideSpeed,
            _ => midSlideSpeed
        };
    }

    private float GetDirectionOffsetDegrees(TempoBand tempo)
    {
        return tempo switch
        {
            TempoBand.Slow => slowDirectionOffsetDegrees,
            TempoBand.Fast => fastDirectionOffsetDegrees,
            TempoBand.Intense => intenseDirectionOffsetDegrees,
            _ => midDirectionOffsetDegrees
        };
    }

    private void StopSliding()
    {
        slideDirection = Vector2.zero;
        slideSpeed = 0f;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void SetWorldPosition(Vector2 worldPosition)
    {
        if (body != null)
        {
            body.position = worldPosition;
            body.linearVelocity = Vector2.zero;
        }

        transform.position = worldPosition;
    }

    private void SetWorldRotation(float zRotation)
    {
        if (body != null)
        {
            body.rotation = zRotation;
            body.angularVelocity = 0f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
    }

    private void SyncTransformToBody()
    {
        if (body == null)
            return;

        transform.position = body.position;
        transform.rotation = Quaternion.Euler(0f, 0f, body.rotation);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        if (Mathf.Abs(degrees) < 0.001f)
            return direction;

        return Quaternion.Euler(0f, 0f, degrees) * direction;
    }

    private void OnDrawGizmosSelected()
    {
        if (TryGetPathEndpoints(out Vector2 pathStart, out Vector2 pathEnd))
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawLine(pathStart, pathEnd);
            Gizmos.DrawSphere(pathStart, 0.12f);
            Gizmos.DrawSphere(pathEnd, 0.12f);
        }

        Vector3 drawPosition = pathStartPoint != null
            ? pathStartPoint.position
            : spawnAnchor != null
                ? spawnAnchor.position
                : transform.position;

        if (spawnAreaCollider != null)
        {
            Bounds bounds = spawnAreaCollider.bounds;
            bounds.Expand(new Vector3(spawnAreaMargin.x * 2f, spawnAreaMargin.y * 2f, 0f));
            Gizmos.color = new Color(0.15f, 0.9f, 0.8f, 0.35f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            return;
        }

        Gizmos.color = new Color(0.15f, 0.9f, 0.8f, 0.35f);
        Gizmos.DrawWireCube(drawPosition, new Vector3(Mathf.Max(0.1f, spawnAreaMargin.x * 2f), Mathf.Max(0.1f, spawnAreaMargin.y * 2f), 0f));
    }
}
