using UnityEngine;

public class RangedSkeletonEnemy : SkeletonEnemyBase
{
    [Header("Projectile")]
    [SerializeField] private SkeletonProjectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField, Min(0f)] private float projectileSpawnOffset = 0.5f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 6f;

    protected override void Reset()
    {
        base.Reset();

        if (projectileSpawnPoint == null)
            projectileSpawnPoint = transform;
    }

    protected override bool PerformAttack(Vector2 attackDirection, float distanceToTarget)
    {
        if (projectilePrefab == null)
            return false;

        Vector3 spawnOrigin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
        Vector3 spawnPosition = spawnOrigin + (Vector3)(attackDirection * projectileSpawnOffset);

        SkeletonProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        Vector2 halfwayPoint = Target != null ? (Vector2)Target.position : (Vector2)spawnPosition + attackDirection * projectileSpawnOffset;
        projectile.InitializeBoomerang(gameObject, attackDirection, projectileSpeed, EffectiveDamage, halfwayPoint);
        return true;
    }
}
