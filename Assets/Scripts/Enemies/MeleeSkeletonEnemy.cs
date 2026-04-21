using UnityEngine;

public class MeleeSkeletonEnemy : SkeletonEnemyBase
{
    private enum AttackState
    {
        None,
        Lunging,
        Returning
    }

    [Header("Lunge")]
    [SerializeField, Min(0.01f)] private float lungeSpeed = 8f;
    [SerializeField, Min(0.01f)] private float returnSpeed = 6f;
    [SerializeField, Min(0.01f)] private float lungeArrivalDistance = 0.05f;
    [SerializeField, Min(0f)] private float damageContactRadius = 0.4f;
    [SerializeField, Min(0f)] private float minimumLungeDistance = 0.4f;

    private AttackState attackState;
    private Vector2 lungeStartPosition;
    private Vector2 lungeTargetPosition;
    private Vector2 lungeDirection = Vector2.right;
    private bool hasAppliedLungeDamage;

    protected override void TickBehavior(Vector2 toPursuitTarget, Vector2 toActualTarget, float actualDistanceToTarget)
    {
        Vector2 currentPosition = transform.position;

        if (attackState != AttackState.None)
        {
            TickLungeAttack(currentPosition);
            return;
        }

        if (actualDistanceToTarget > EffectiveAttackRange)
        {
            SetDesiredVelocity(toPursuitTarget.sqrMagnitude > Mathf.Epsilon
                ? toPursuitTarget.normalized * MoveSpeed
                : Vector2.zero);
            return;
        }

        SetDesiredVelocity(Vector2.zero);

        if (IsAttackOnCooldown)
            return;

        Vector2 attackDirection = toActualTarget.sqrMagnitude > Mathf.Epsilon ? toActualTarget.normalized : FacingDirection;
        BeginLungeAttack(currentPosition, attackDirection, actualDistanceToTarget);
    }

    protected override bool ShouldApplySeparation()
    {
        return attackState == AttackState.None;
    }

    protected override bool PerformAttack(Vector2 attackDirection, float distanceToTarget)
    {
        return false;
    }

    private void BeginLungeAttack(Vector2 currentPosition, Vector2 attackDirection, float distanceToTarget)
    {
        attackState = AttackState.Lunging;
        lungeStartPosition = currentPosition;
        lungeDirection = attackDirection.sqrMagnitude > Mathf.Epsilon ? attackDirection.normalized : FacingDirection;
        lungeTargetPosition = currentPosition + lungeDirection * Mathf.Max(distanceToTarget, minimumLungeDistance);
        hasAppliedLungeDamage = false;

        SetFacingDirection(lungeDirection);
        GameSfx.Play(this, GameSfxCue.MeleeAttack, pitchVariance: 0.03f, volumeVariance: 0.04f);
        TriggerAttack(lungeDirection);
    }

    private void TickLungeAttack(Vector2 currentPosition)
    {
        switch (attackState)
        {
            case AttackState.Lunging:
                TickLungeOut(currentPosition);
                break;
            case AttackState.Returning:
                TickLungeReturn(currentPosition);
                break;
        }
    }

    private void TickLungeOut(Vector2 currentPosition)
    {
        TryApplyLungeDamage();

        Vector2 toLungeTarget = lungeTargetPosition - currentPosition;
        float arrivalDistance = GetEffectiveArrivalDistance(lungeSpeed);
        if (toLungeTarget.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            attackState = AttackState.Returning;
            SetDesiredVelocity(Vector2.zero);
            return;
        }

        SetFacingDirection(toLungeTarget);
        SetDesiredVelocity(toLungeTarget.normalized * lungeSpeed);
    }

    private void TickLungeReturn(Vector2 currentPosition)
    {
        Vector2 toStart = lungeStartPosition - currentPosition;
        float arrivalDistance = GetEffectiveArrivalDistance(returnSpeed);
        if (toStart.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            attackState = AttackState.None;
            SetDesiredVelocity(Vector2.zero);
            return;
        }

        SetFacingDirection(toStart);
        SetDesiredVelocity(toStart.normalized * returnSpeed);
    }

    private void TryApplyLungeDamage()
    {
        if (hasAppliedLungeDamage || Target == null)
            return;

        Vector2 toTarget = (Vector2)Target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude > damageContactRadius * damageContactRadius)
            return;

        if (!TryGetDamageableTarget(out IDamageable damageable))
            return;

        damageable.ReceiveDamage(EffectiveDamage, lungeDirection, gameObject);
        hasAppliedLungeDamage = true;
    }

    private float GetEffectiveArrivalDistance(float travelSpeed)
    {
        float physicsStepDistance = Mathf.Max(0.01f, travelSpeed) * Time.fixedDeltaTime;
        return Mathf.Max(0.01f, lungeArrivalDistance, physicsStepDistance);
    }
}
