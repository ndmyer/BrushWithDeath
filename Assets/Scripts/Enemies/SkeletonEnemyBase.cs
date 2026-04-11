using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class SkeletonEnemyBase : MonoBehaviour, IKnockbackable
{
    [Serializable]
    private class TempoStatModifier
    {
        [Min(0f)] public float moveSpeedMultiplier = 1f;
        [Min(0f)] public float knockbackMultiplier = 1f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Min(0f)] public float attackSpeedMultiplier = 1f;
        [Min(0f)] public float attackRangeMultiplier = 1f;
    }

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private TempoService tempoService;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D hitbox;

    [Header("Stats")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [SerializeField, FormerlySerializedAs("knockbackAmount"), Min(0f)] private float knockbackDistance = 1f;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0.01f)] private float attackSpeed = 1f;
    [SerializeField, Min(0f)] private float attackRange = 1.25f;

    [Header("Behavior")]
    [SerializeField, Min(0f)] private float detectionRange = 6f;
    [SerializeField, Min(0f)] private float containmentInsetDistance = 0.1f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.12f;
    [SerializeField, Min(0f)] private float deathCleanupDelay = 1.25f;
    [SerializeField] private bool destroyAfterDeath = true;

    [Header("Idle Movement")]
    [SerializeField, Min(0f)] private float idleWanderRadius = 2f;
    [SerializeField] private Vector2 idlePauseDurationRange = new Vector2(1.25f, 2.5f);
    [SerializeField, Min(0f)] private float idleArrivalDistance = 0.1f;
    [SerializeField, Min(1)] private int idlePointSampleAttempts = 8;

    [Header("Tempo")]
    [SerializeField] private TempoStatModifier slowTempo = new();
    [SerializeField] private TempoStatModifier midTempo = new();
    [SerializeField] private TempoStatModifier fastTempo = new();
    [SerializeField] private TempoStatModifier intenseTempo = new();

    [Header("Animation")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string deathTriggerName = "Die";
    [SerializeField] private string deadBoolName = "IsDead";

    [Header("Events")]
    [SerializeField] private UnityEvent onAttack;
    [SerializeField] private UnityEvent onDeath;

    private Vector2 desiredVelocity;
    private Vector2 knockbackVelocity;
    private Collider2D containmentArea;
    private Bounds containmentBounds;
    private Vector2 idleAnchorPosition;
    private Vector2 idleDestination;
    private float attackCooldownTimer;
    private float idleWaitTimer;
    private float knockbackTimer;
    private bool hasIdleDestination;
    private bool hasContainmentBounds;

    protected Transform Target => target;
    protected TempoBand CurrentTempo { get; private set; } = TempoBand.Mid;
    protected Vector2 FacingDirection { get; private set; } = Vector2.down;
    protected bool IsDead { get; private set; }
    protected float EffectiveDamage => damage * GetTempoModifier(CurrentTempo).damageMultiplier;
    protected float EffectiveAttackRange => attackRange * GetTempoModifier(CurrentTempo).attackRangeMultiplier;

    public event Action<SkeletonEnemyBase> Died;

    protected virtual void Reset()
    {
        CacheReferences();
    }

    protected virtual void OnValidate()
    {
        CacheReferences();
        idlePauseDurationRange.x = Mathf.Max(0f, idlePauseDurationRange.x);
        idlePauseDurationRange.y = Mathf.Max(idlePauseDurationRange.x, idlePauseDurationRange.y);
    }

    protected virtual void Awake()
    {
        CacheReferences();
        idleAnchorPosition = transform.position;
        idleWaitTimer = GetIdlePauseDuration();
        ResolveTarget();
    }

    protected virtual void OnEnable()
    {
        SubscribeToTempo();
    }

    protected virtual void OnDisable()
    {
        if (tempoService != null)
            tempoService.TempoUpdated -= HandleTempoUpdated;
    }

    protected virtual void Update()
    {
        if (IsDead)
        {
            desiredVelocity = Vector2.zero;
            UpdateAnimator(Vector2.zero);
            return;
        }

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
                knockbackVelocity = Vector2.zero;

            UpdateAnimator(knockbackVelocity);
            return;
        }

        if (!ResolveTarget())
        {
            TickIdleMovement((Vector2)transform.position);
            UpdateAnimator(desiredVelocity);
            return;
        }

        Vector2 targetPosition = target.position;
        Vector2 currentPosition = transform.position;
        Vector2 toActualTarget = targetPosition - currentPosition;
        float distanceToActualTarget = toActualTarget.magnitude;

        if (HasContainment() && !IsInsideContainment(currentPosition))
        {
            Vector2 returnTarget = GetContainedPosition(currentPosition, idleAnchorPosition);
            Vector2 toReturnTarget = returnTarget - currentPosition;

            desiredVelocity = toReturnTarget.sqrMagnitude > Mathf.Epsilon
                ? toReturnTarget.normalized * GetMoveSpeed()
                : Vector2.zero;

            UpdateAnimator(desiredVelocity);
            return;
        }

        bool targetInDetectionRange = detectionRange <= 0f || distanceToActualTarget <= detectionRange;
        if (!targetInDetectionRange)
        {
            TickIdleMovement(currentPosition);
            UpdateAnimator(desiredVelocity);
            return;
        }

        bool targetOutsideContainment = HasContainment() && !IsInsideContainment(targetPosition);
        Vector2 pursuitTarget = HasContainment()
            ? GetContainedPosition(targetPosition, currentPosition)
            : targetPosition;

        if (targetOutsideContainment && HasReachedPoint(currentPosition, pursuitTarget))
        {
            TriggerIdleWaitIfNeeded();
            TickIdleMovement(currentPosition);
            UpdateAnimator(desiredVelocity);
            return;
        }

        if (distanceToActualTarget > Mathf.Epsilon)
            FacingDirection = DirectionUtility.ToCardinal(toActualTarget);

        hasIdleDestination = false;
        idleWaitTimer = 0f;
        Vector2 toPursuitTarget = pursuitTarget - currentPosition;

        TickBehavior(toPursuitTarget, toActualTarget, distanceToActualTarget);
        UpdateAnimator(desiredVelocity);
    }

    protected virtual void FixedUpdate()
    {
        if (body == null)
            return;

        if (IsDead)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        body.linearVelocity = knockbackTimer > 0f ? knockbackVelocity : desiredVelocity;
    }

    public void ApplyKnockback(Vector2 direction, float strengthMultiplier = 1f)
    {
        if (IsDead)
            return;

        Vector2 knockbackDirection = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized
            : -FacingDirection;

        float totalDistance = knockbackDistance * GetTempoModifier(CurrentTempo).knockbackMultiplier * Mathf.Max(0f, strengthMultiplier);
        float duration = Mathf.Max(0.01f, knockbackDuration);

        desiredVelocity = Vector2.zero;
        knockbackVelocity = knockbackDirection * (totalDistance / duration);
        knockbackTimer = duration;
    }

    public void ApplyKnockbackFrom(Vector2 sourcePosition, float strengthMultiplier = 1f)
    {
        ApplyKnockback((Vector2)transform.position - sourcePosition, strengthMultiplier);
    }

    public void Kill()
    {
        if (IsDead)
            return;

        IsDead = true;
        desiredVelocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        attackCooldownTimer = 0f;
        knockbackTimer = 0f;

        if (tempoService != null)
            tempoService.TempoUpdated -= HandleTempoUpdated;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        if (hitbox != null)
            hitbox.enabled = false;

        if (animator != null)
        {
            if (!string.IsNullOrWhiteSpace(deadBoolName))
                animator.SetBool(deadBoolName, true);

            if (!string.IsNullOrWhiteSpace(deathTriggerName))
                animator.SetTrigger(deathTriggerName);
        }

        onDeath?.Invoke();
        Died?.Invoke(this);

        if (destroyAfterDeath)
            Destroy(gameObject, deathCleanupDelay);
    }

    public void SetContainmentArea(Collider2D area)
    {
        containmentArea = area;
        hasContainmentBounds = false;
        idleAnchorPosition = GetContainedPosition(idleAnchorPosition, (Vector2)transform.position);
    }

    public void SetContainmentBounds(Bounds bounds)
    {
        containmentArea = null;
        containmentBounds = bounds;
        hasContainmentBounds = true;
        idleAnchorPosition = GetContainedPosition(idleAnchorPosition, (Vector2)transform.position);
    }

    public void ClearContainmentBounds()
    {
        containmentArea = null;
        hasContainmentBounds = false;
    }

    protected bool TryGetDamageableTarget(out IDamageable damageable)
    {
        return TryGetInterface(target, out damageable);
    }

    protected virtual void TickBehavior(Vector2 toPursuitTarget, Vector2 toActualTarget, float actualDistanceToTarget)
    {
        if (actualDistanceToTarget > EffectiveAttackRange)
        {
            desiredVelocity = toPursuitTarget.sqrMagnitude > Mathf.Epsilon
                ? toPursuitTarget.normalized * GetMoveSpeed()
                : Vector2.zero;
            return;
        }

        desiredVelocity = Vector2.zero;

        if (attackCooldownTimer > 0f)
            return;

        Vector2 attackDirection = toActualTarget.sqrMagnitude > Mathf.Epsilon ? toActualTarget.normalized : FacingDirection;
        if (!PerformAttack(attackDirection, actualDistanceToTarget))
            return;

        attackCooldownTimer = GetAttackCooldown();

        if (animator != null && !string.IsNullOrWhiteSpace(attackTriggerName))
            animator.SetTrigger(attackTriggerName);

        onAttack?.Invoke();
    }

    protected abstract bool PerformAttack(Vector2 attackDirection, float distanceToTarget);

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleMarigoldContact(other);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleMarigoldContact(collision.collider);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    private void CacheReferences()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (hitbox == null)
            hitbox = GetComponent<Collider2D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void SubscribeToTempo()
    {
        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (tempoService == null)
            return;

        tempoService.TempoUpdated -= HandleTempoUpdated;
        tempoService.TempoUpdated += HandleTempoUpdated;
        HandleTempoUpdated(tempoService.GetCurrentSnapshot());
    }

    private void HandleTempoUpdated(TempoStateSnapshot snapshot)
    {
        CurrentTempo = snapshot.CurrentTempo;
    }

    private bool ResolveTarget()
    {
        if (target != null)
            return true;

        PlayerDamageReceiver damageReceiver = FindAnyObjectByType<PlayerDamageReceiver>();
        if (damageReceiver != null)
        {
            target = damageReceiver.transform;
            return true;
        }

        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            target = playerController.transform;
            return true;
        }

        return false;
    }

    private void TryHandleMarigoldContact(Collider2D other)
    {
        if (IsDead || other == null)
            return;

        MarigoldHazard marigoldHazard = other.GetComponentInParent<MarigoldHazard>();
        if (marigoldHazard != null && marigoldHazard.IsActive)
        {
            Kill();
            return;
        }

        LightableTorch torch = other.GetComponentInParent<LightableTorch>();
        if (torch != null &&
            torch.Type == LightableTorch.TorchType.Marigold &&
            torch.IsLit)
        {
            Kill();
        }
    }

    private float GetMoveSpeed()
    {
        return moveSpeed * GetTempoModifier(CurrentTempo).moveSpeedMultiplier;
    }

    private float GetIdlePauseDuration()
    {
        return Random.Range(idlePauseDurationRange.x, idlePauseDurationRange.y);
    }

    private void TriggerIdleWaitIfNeeded()
    {
        if (hasIdleDestination || idleWaitTimer > 0f)
            return;

        desiredVelocity = Vector2.zero;
        idleWaitTimer = GetIdlePauseDuration();
    }

    private bool HasContainment()
    {
        return containmentArea != null || hasContainmentBounds;
    }

    private void TickIdleMovement(Vector2 currentPosition)
    {
        if (hasIdleDestination)
        {
            Vector2 toDestination = idleDestination - currentPosition;
            float arrivalDistance = Mathf.Max(0.01f, idleArrivalDistance);

            if (toDestination.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                hasIdleDestination = false;
                idleWaitTimer = GetIdlePauseDuration();
                desiredVelocity = Vector2.zero;
                return;
            }

            desiredVelocity = toDestination.normalized * GetMoveSpeed();
            if (desiredVelocity.sqrMagnitude > Mathf.Epsilon)
                FacingDirection = DirectionUtility.ToCardinal(desiredVelocity);

            return;
        }

        if (idleWaitTimer > 0f)
        {
            idleWaitTimer -= Time.deltaTime;
            desiredVelocity = Vector2.zero;
            return;
        }

        if (!TryGetIdleDestination(currentPosition, out Vector2 destination))
        {
            idleWaitTimer = GetIdlePauseDuration();
            desiredVelocity = Vector2.zero;
            return;
        }

        idleDestination = destination;
        hasIdleDestination = true;

        Vector2 toNewDestination = idleDestination - currentPosition;
        desiredVelocity = toNewDestination.sqrMagnitude > Mathf.Epsilon
            ? toNewDestination.normalized * GetMoveSpeed()
            : Vector2.zero;

        if (desiredVelocity.sqrMagnitude > Mathf.Epsilon)
            FacingDirection = DirectionUtility.ToCardinal(desiredVelocity);
    }

    private bool TryGetIdleDestination(Vector2 currentPosition, out Vector2 destination)
    {
        destination = currentPosition;

        if (idleWanderRadius <= Mathf.Epsilon)
            return false;

        Vector2 anchor = HasContainment()
            ? GetContainedPosition(idleAnchorPosition, currentPosition)
            : idleAnchorPosition;
        float minimumTravelDistance = Mathf.Max(0.01f, idleArrivalDistance * 2f);

        for (int i = 0; i < idlePointSampleAttempts; i++)
        {
            Vector2 candidate = anchor + Random.insideUnitCircle * idleWanderRadius;

            if (HasContainment() && !IsInsideContainment(candidate))
                continue;

            if ((candidate - currentPosition).sqrMagnitude < minimumTravelDistance * minimumTravelDistance)
                continue;

            destination = candidate;
            return true;
        }

        return false;
    }

    private bool HasReachedPoint(Vector2 currentPosition, Vector2 destination)
    {
        float arrivalDistance = Mathf.Max(0.01f, idleArrivalDistance);
        return (destination - currentPosition).sqrMagnitude <= arrivalDistance * arrivalDistance;
    }

    private Vector2 GetContainedPosition(Vector2 worldPosition, Vector2 interiorReference)
    {
        if (containmentArea != null)
            return GetContainedPositionInArea(worldPosition, interiorReference);

        return GetContainedPositionInBounds(worldPosition);
    }

    private bool IsInsideContainment(Vector2 worldPosition)
    {
        if (containmentArea != null)
            return containmentArea.OverlapPoint(worldPosition);

        Vector3 point = new Vector3(worldPosition.x, worldPosition.y, containmentBounds.center.z);
        return containmentBounds.Contains(point);
    }

    private Vector2 GetContainedPositionInArea(Vector2 worldPosition, Vector2 interiorReference)
    {
        if (containmentArea == null || containmentArea.OverlapPoint(worldPosition))
            return worldPosition;

        Vector2 boundaryPoint = containmentArea.ClosestPoint(worldPosition);
        Vector2 referencePoint = GetValidContainmentReference(interiorReference);
        if (!containmentArea.OverlapPoint(referencePoint))
            return boundaryPoint;

        Vector2 toReference = referencePoint - boundaryPoint;
        if (toReference.sqrMagnitude <= Mathf.Epsilon)
            return referencePoint;

        float insetDistance = Mathf.Max(0.01f, containmentInsetDistance);
        Vector2 insetPoint = boundaryPoint + toReference.normalized * insetDistance;
        if (containmentArea.OverlapPoint(insetPoint))
            return insetPoint;

        Vector2 nearestInsidePoint = referencePoint;
        Vector2 nearestOutsidePoint = boundaryPoint;

        for (int i = 0; i < 8; i++)
        {
            Vector2 midpoint = Vector2.Lerp(nearestOutsidePoint, nearestInsidePoint, 0.5f);
            if (containmentArea.OverlapPoint(midpoint))
                nearestInsidePoint = midpoint;
            else
                nearestOutsidePoint = midpoint;
        }

        return nearestInsidePoint;
    }

    private Vector2 GetContainedPositionInBounds(Vector2 worldPosition)
    {
        float insetX = Mathf.Min(Mathf.Max(0f, containmentInsetDistance), containmentBounds.extents.x);
        float insetY = Mathf.Min(Mathf.Max(0f, containmentInsetDistance), containmentBounds.extents.y);

        return new Vector2(
            Mathf.Clamp(worldPosition.x, containmentBounds.min.x + insetX, containmentBounds.max.x - insetX),
            Mathf.Clamp(worldPosition.y, containmentBounds.min.y + insetY, containmentBounds.max.y - insetY));
    }

    private Vector2 GetValidContainmentReference(Vector2 preferredReference)
    {
        if (containmentArea == null)
            return preferredReference;

        if (containmentArea.OverlapPoint(preferredReference))
            return preferredReference;

        if (containmentArea.OverlapPoint(idleAnchorPosition))
            return idleAnchorPosition;

        Vector2 currentPosition = transform.position;
        if (containmentArea.OverlapPoint(currentPosition))
            return currentPosition;

        return preferredReference;
    }

    private float GetAttackCooldown()
    {
        float effectiveAttackSpeed = attackSpeed * GetTempoModifier(CurrentTempo).attackSpeedMultiplier;
        return 1f / Mathf.Max(0.01f, effectiveAttackSpeed);
    }

    private TempoStatModifier GetTempoModifier(TempoBand tempo)
    {
        TempoStatModifier modifier = tempo switch
        {
            TempoBand.Slow => slowTempo,
            TempoBand.Fast => fastTempo,
            TempoBand.Intense => intenseTempo,
            _ => midTempo
        };

        return modifier ?? new TempoStatModifier();
    }

    private void UpdateAnimator(Vector2 velocity)
    {
        if (animator == null)
            return;

        Vector2 moveDirection = velocity.sqrMagnitude > Mathf.Epsilon
            ? velocity.normalized
            : Vector2.zero;

        animator.SetFloat("MoveX", moveDirection.x);
        animator.SetFloat("MoveY", moveDirection.y);
        animator.SetFloat("FaceX", FacingDirection.x);
        animator.SetFloat("FaceY", FacingDirection.y);
        animator.SetBool("IsMoving", velocity.sqrMagnitude > 0.001f);
    }

    private static bool TryGetInterface<T>(Component source, out T value)
        where T : class
    {
        value = null;

        if (source == null)
            return false;

        value = source.GetComponent(typeof(T)) as T;
        if (value != null)
            return true;

        value = source.GetComponentInParent(typeof(T)) as T;
        if (value != null)
            return true;

        value = source.GetComponentInChildren(typeof(T)) as T;
        return value != null;
    }
}
