using System.Collections.Generic;
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

    [Header("Switch Interaction")]
    [SerializeField] private LayerMask switchInteractionLayers = ~0;
    [SerializeField] private float switchInteractionRadius = 0.2f;

    [Header("Pulse Attack")]
    [SerializeField] private LayerMask pulseAttackLayers = ~0;
    [SerializeField] private float pulseAttackRadius = 0.9f;
    [SerializeField, Min(0f)] private float pulseAttackKnockbackMultiplier = 1f;

    public PistaState CurrentState { get; private set; } = PistaState.FollowingPlayer;
    public Transform CurrentLanternTarget { get; private set; }
    public Transform CurrentPreviewTarget { get; private set; }

    private bool aimStickEngaged;
    private readonly Collider2D[] switchOverlapResults = new Collider2D[8];
    private readonly RaycastHit2D[] switchCastResults = new RaycastHit2D[8];
    private readonly Collider2D[] pulseAttackResults = new Collider2D[16];
    private readonly HashSet<int> activatedSwitchesThisTravel = new();

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
        activatedSwitchesThisTravel.Clear();
        CurrentState = PistaState.MovingToLantern;
    }

    public void RecallToPlayer()
    {
        CurrentLanternTarget = null;
        CurrentPreviewTarget = null;
        aimStickEngaged = false;
        activatedSwitchesThisTravel.Clear();
        CurrentState = PistaState.FollowingPlayer;
    }

    public int TriggerPulseAttack()
    {
        ContactFilter2D contactFilter = CreatePulseAttackContactFilter();
        int overlapHitCount = Physics2D.OverlapCircle(transform.position, GetPulseAttackRadius(), contactFilter, pulseAttackResults);

        HashSet<int> activatedSwitches = new();
        HashSet<int> knockedBackTargets = new();
        int affectedTargetCount = 0;

        for (int i = 0; i < overlapHitCount; i++)
        {
            Collider2D hit = pulseAttackResults[i];

            if (TryActivateSwitchFromCollider(hit, activatedSwitches))
                affectedTargetCount++;

            if (TryApplyPulseKnockback(hit, knockedBackTargets))
                affectedTargetCount++;
        }

        return affectedTargetCount;
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

        Vector3 previousPosition = transform.position;
        MoveToward(CurrentLanternTarget.position, lanternMoveSpeed);
        ActivateSwitchesAlongTravel(previousPosition, transform.position);

        if ((CurrentLanternTarget.position - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            transform.position = CurrentLanternTarget.position;
            ActivateNearbySwitches(transform.position, CreateSwitchContactFilter(), GetSwitchInteractionRadius());
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

    private void ActivateSwitchesAlongTravel(Vector2 startPosition, Vector2 endPosition)
    {
        ContactFilter2D contactFilter = CreateSwitchContactFilter();
        float interactionRadius = GetSwitchInteractionRadius();
        Vector2 travelDelta = endPosition - startPosition;
        float travelDistance = travelDelta.magnitude;

        ActivateNearbySwitches(startPosition, contactFilter, interactionRadius);

        if (travelDistance > Mathf.Epsilon)
        {
            Vector2 travelDirection = travelDelta / travelDistance;
            int castHitCount = Physics2D.CircleCast(startPosition, interactionRadius, travelDirection, contactFilter, switchCastResults, travelDistance);

            for (int i = 0; i < castHitCount; i++)
                TryActivateSwitchFromCollider(switchCastResults[i].collider, activatedSwitchesThisTravel);
        }

        ActivateNearbySwitches(endPosition, contactFilter, interactionRadius);
    }

    private void ActivateNearbySwitches(Vector2 position, ContactFilter2D contactFilter, float interactionRadius)
    {
        int overlapHitCount = Physics2D.OverlapCircle(position, interactionRadius, contactFilter, switchOverlapResults);

        for (int i = 0; i < overlapHitCount; i++)
            TryActivateSwitchFromCollider(switchOverlapResults[i], activatedSwitchesThisTravel);
    }

    private bool TryActivateSwitchFromCollider(Collider2D hit, HashSet<int> processedSwitches)
    {
        if (hit == null)
            return false;

        if (!TryGetInteractSwitch(hit, out InteractSwitch interactSwitch, out Component switchComponent))
            return false;

        if (switchComponent == null || processedSwitches == null || !processedSwitches.Add(switchComponent.GetInstanceID()))
            return false;

        interactSwitch.Activate();
        return true;
    }

    private ContactFilter2D CreateSwitchContactFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = switchInteractionLayers,
            useTriggers = true
        };
    }

    private float GetSwitchInteractionRadius()
    {
        return Mathf.Max(0.01f, switchInteractionRadius);
    }

    private bool TryApplyPulseKnockback(Collider2D hit, HashSet<int> processedTargets)
    {
        if (!TryGetInterface(hit, out IKnockbackable knockbackable, out Component targetComponent))
            return false;

        if (targetComponent == null || processedTargets == null || !processedTargets.Add(targetComponent.GetInstanceID()))
            return false;

        knockbackable.ApplyKnockbackFrom(transform.position, pulseAttackKnockbackMultiplier);
        return true;
    }

    private ContactFilter2D CreatePulseAttackContactFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = pulseAttackLayers,
            useTriggers = true
        };
    }

    private float GetPulseAttackRadius()
    {
        return Mathf.Max(0.01f, pulseAttackRadius);
    }

    private static bool TryGetInteractSwitch(Collider2D source, out InteractSwitch interactSwitch, out Component component)
    {
        interactSwitch = null;
        component = null;

        if (source == null)
            return false;

        component = source.GetComponent<InteractSwitch>();
        if (component == null)
            component = source.GetComponentInParent<InteractSwitch>();
        if (component == null)
            component = source.GetComponentInChildren<InteractSwitch>();
        if (component == null)
            return false;

        interactSwitch = component as InteractSwitch;
        return interactSwitch != null;
    }

    private static bool TryGetInterface<T>(Collider2D source, out T value, out Component component)
        where T : class
    {
        value = null;
        component = null;

        if (source == null)
            return false;

        component = source.GetComponent(typeof(T));
        if (component == null)
            component = source.GetComponentInParent(typeof(T));
        if (component == null)
            component = source.GetComponentInChildren(typeof(T));
        if (component == null)
            return false;

        value = component as T;
        return value != null;
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

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, GetSwitchInteractionRadius());

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, GetPulseAttackRadius());

        if (CurrentLanternTarget == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, CurrentLanternTarget.position);
        Gizmos.DrawWireSphere(CurrentLanternTarget.position, 0.12f);
    }
}
