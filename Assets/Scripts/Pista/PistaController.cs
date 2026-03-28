using UnityEngine;

public class PistaController : MonoBehaviour
{
    public enum PistaState
    {
        FollowingPlayer,
        Aiming,
        MovingToLantern,
        LatchedToLantern
    }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Follow")]
    [SerializeField] private Vector2 followOffset = new Vector2(0.75f, 0f);
    [SerializeField] private float followMoveSpeed = 8f;

    [Header("Targeting")]
    [SerializeField] private float lanternQueryRange = 3f;
    [SerializeField] private float halfConeAngle = 40f;
    [SerializeField] private float stickEngageThreshold = 0.5f;
    [SerializeField] private float stickResetThreshold = 0.2f;

    [Header("Lantern Travel")]
    [SerializeField] private float lanternMoveSpeed = 10f;
    [SerializeField] private float arrivalDistance = 0.05f;

    public PistaState CurrentState { get; private set; } = PistaState.FollowingPlayer;
    public Transform CurrentLanternTarget { get; private set; }
    public Transform CurrentPreviewTarget { get; private set; }

    private bool aimStickEngaged;

    private void Awake()
    {
        if (playerTarget != null && CurrentState == PistaState.FollowingPlayer)
            transform.position = GetFollowAnchorPosition();
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case PistaState.FollowingPlayer:
                MoveToward(GetFollowAnchorPosition(), followMoveSpeed);
                break;

            case PistaState.Aiming:
                if (CurrentLanternTarget != null)
                    SnapToLanternTarget();
                else
                    MoveToward(GetFollowAnchorPosition(), followMoveSpeed);
                break;

            case PistaState.MovingToLantern:
                UpdateMoveToLantern();
                break;

            case PistaState.LatchedToLantern:
                SnapToLanternTarget();
                break;
        }
    }

    public void SetPlayerTarget(Transform targetTransform)
    {
        playerTarget = targetTransform;
    }

    public void BeginAiming()
    {
        CurrentState = PistaState.Aiming;
    }

    public void EndAiming()
    {
        aimStickEngaged = false;
        CurrentPreviewTarget = null;

        if (CurrentState == PistaState.MovingToLantern)
            return;

        if (CurrentLanternTarget != null)
        {
            CurrentState = PistaState.LatchedToLantern;
            return;
        }

        CurrentState = PistaState.FollowingPlayer;
    }

    public void MoveToLantern(Transform lanternTarget)
    {
        if (lanternTarget == null)
            return;

        CurrentLanternTarget = lanternTarget;
        CurrentState = PistaState.MovingToLantern;
    }

    public void RecallToPlayer()
    {
        CurrentLanternTarget = null;
        CurrentPreviewTarget = null;
        aimStickEngaged = false;
        CurrentState = PistaState.FollowingPlayer;
    }

    public void ProcessAimInput(Vector2 aimInput)
    {
        if (CurrentState == PistaState.MovingToLantern)
            return;

        float inputMagnitude = aimInput.magnitude;
        bool isLatched = CurrentState == PistaState.LatchedToLantern;

        if (inputMagnitude >= stickEngageThreshold)
        {
            BeginAiming();
            aimStickEngaged = true;
            CurrentPreviewTarget = FindBestLanternTarget(aimInput / inputMagnitude);
            return;
        }

        if (!aimStickEngaged)
        {
            CurrentPreviewTarget = null;

            if (!isLatched)
                BeginAiming();

            return;
        }

        if (inputMagnitude <= stickResetThreshold)
        {
            aimStickEngaged = false;

            if (CurrentPreviewTarget != null)
            {
                MoveToLantern(CurrentPreviewTarget);
                CurrentPreviewTarget = null;
                return;
            }
        }

        if (!isLatched)
            BeginAiming();
    }

    public Vector3 GetFollowAnchorPosition()
    {
        if (playerTarget == null)
            return transform.position;

        return playerTarget.position + (Vector3)followOffset;
    }

    private void UpdateMoveToLantern()
    {
        if (CurrentLanternTarget == null)
        {
            CurrentState = PistaState.FollowingPlayer;
            return;
        }

        MoveToward(CurrentLanternTarget.position, lanternMoveSpeed);

        if ((CurrentLanternTarget.position - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            transform.position = CurrentLanternTarget.position;
            CurrentState = PistaState.LatchedToLantern;
        }
    }

    private void SnapToLanternTarget()
    {
        if (CurrentLanternTarget == null)
        {
            CurrentState = PistaState.FollowingPlayer;
            return;
        }

        transform.position = CurrentLanternTarget.position;
    }

    private void MoveToward(Vector3 targetPosition, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private Transform FindBestLanternTarget(Vector2 aimDirection)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, lanternQueryRange);
        Transform bestTarget = null;
        float bestScore = float.NegativeInfinity;
        float minDot = Mathf.Cos(halfConeAngle * Mathf.Deg2Rad);

        foreach (Collider2D hit in hits)
        {
            if (!TryGetLitTarget(hit, out Transform targetTransform))
                continue;

            Vector2 toTarget = (Vector2)(targetTransform.position - transform.position);
            float distance = toTarget.magnitude;

            if (distance <= Mathf.Epsilon || distance > lanternQueryRange)
                continue;

            Vector2 targetDirection = toTarget / distance;
            float directionDot = Vector2.Dot(aimDirection, targetDirection);

            if (directionDot < minDot)
                continue;

            if (IsBlocked(targetTransform.position))
                continue;

            float distanceScore = 1f - (distance / lanternQueryRange);
            float directionScore = Mathf.InverseLerp(minDot, 1f, directionDot);
            float combinedScore = distanceScore * 0.6f + directionScore * 0.4f;

            if (combinedScore <= bestScore)
                continue;

            bestScore = combinedScore;
            bestTarget = targetTransform;
        }

        return bestTarget;
    }

    private bool TryGetLitTarget(Collider2D hit, out Transform targetTransform)
    {
        targetTransform = null;

        ILightable lightable = hit.GetComponent<ILightable>();

        if (lightable == null || !lightable.IsLit)
            return false;

        targetTransform = hit.transform;
        return true;
    }

    private bool IsBlocked(Vector3 targetPosition)
    {
        if (obstacleLayers.value == 0)
            return false;

        RaycastHit2D hit = Physics2D.Linecast(transform.position, targetPosition, obstacleLayers);
        return hit.collider != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetFollowAnchorPosition(), 0.1f);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, lanternQueryRange);

        if (CurrentPreviewTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, CurrentPreviewTarget.position);
            Gizmos.DrawWireSphere(CurrentPreviewTarget.position, 0.18f);
        }

        if (CurrentLanternTarget == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, CurrentLanternTarget.position);
        Gizmos.DrawWireSphere(CurrentLanternTarget.position, 0.12f);
    }
}
