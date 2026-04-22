using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RangedSkeletonEnemy : SkeletonEnemyBase
{
    [Serializable]
    private class TempoAimProfile
    {
        [Min(0f)] public float maxAimOffsetDegrees = 8f;
        [Min(0f)] public float projectileSpeedMultiplier = 1f;
    }

    [Header("Projectile")]
    [SerializeField] private SkeletonProjectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField, Min(0f)] private float projectileSpawnOffset = 0.5f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 6f;

    [Header("Aim")]
    [SerializeField] private TempoAimProfile slowAim = new TempoAimProfile();
    [SerializeField] private TempoAimProfile midAim = new TempoAimProfile();
    [SerializeField] private TempoAimProfile fastAim = new TempoAimProfile();
    [SerializeField] private TempoAimProfile intenseAim = new TempoAimProfile();

    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureAimProfiles();
    }

    protected override void Reset()
    {
        base.Reset();
        EnsureAimProfiles();

        if (projectileSpawnPoint == null)
            projectileSpawnPoint = transform;
    }

    protected override bool PerformAttack(Vector2 attackDirection, float distanceToTarget)
    {
        if (projectilePrefab == null)
            return false;

        Vector2 adjustedDirection = GetAdjustedAttackDirection(attackDirection);

        Vector3 spawnOrigin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
        Vector3 spawnPosition = spawnOrigin + (Vector3)(adjustedDirection * projectileSpawnOffset);

        SkeletonProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        float aimDistance = Mathf.Max(distanceToTarget, EffectiveAttackRange, projectileSpawnOffset * 2f);
        Vector2 halfwayPoint = (Vector2)spawnPosition + adjustedDirection * aimDistance;
        float adjustedProjectileSpeed = projectileSpeed * GetAimProfile(CurrentTempo).projectileSpeedMultiplier;
        projectile.InitializeBoomerang(gameObject, adjustedDirection, adjustedProjectileSpeed, EffectiveDamage, halfwayPoint);
        GameSfx.Play(this, GameSfxCue.RangedAttack, pitchVariance: 0.03f, volumeVariance: 0.04f);
        return true;
    }

    private void EnsureAimProfiles()
    {
        if (slowAim == null)
            slowAim = new TempoAimProfile();

        if (midAim == null)
            midAim = new TempoAimProfile();

        if (fastAim == null)
            fastAim = new TempoAimProfile();

        if (intenseAim == null)
            intenseAim = new TempoAimProfile();
    }

    private Vector2 GetAdjustedAttackDirection(Vector2 attackDirection)
    {
        Vector2 baseDirection = attackDirection.sqrMagnitude > Mathf.Epsilon ? attackDirection.normalized : FacingDirection;
        float maxAimOffsetDegrees = GetAimProfile(CurrentTempo).maxAimOffsetDegrees;

        if (maxAimOffsetDegrees <= Mathf.Epsilon)
            return baseDirection;

        float angleOffsetDegrees = Random.Range(-maxAimOffsetDegrees, maxAimOffsetDegrees);
        return Rotate(baseDirection, angleOffsetDegrees * Mathf.Deg2Rad);
    }

    private TempoAimProfile GetAimProfile(TempoBand tempoBand)
    {
        switch (tempoBand)
        {
            case TempoBand.Slow:
                return slowAim;
            case TempoBand.Fast:
                return fastAim;
            case TempoBand.Intense:
                return intenseAim;
            default:
                return midAim;
        }
    }

    private static Vector2 Rotate(Vector2 value, float angleRadians)
    {
        float sin = Mathf.Sin(angleRadians);
        float cos = Mathf.Cos(angleRadians);
        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos);
    }
}
