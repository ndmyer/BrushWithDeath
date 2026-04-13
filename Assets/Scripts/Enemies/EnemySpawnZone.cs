using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class EnemySpawnZone : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private SkeletonEnemyBase[] enemyPrefabs;
    [SerializeField] private Transform spawnedEnemyParent;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool keepSpawnedEnemiesContained = true;
    [SerializeField] private Collider2D areaShape;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 areaSize = new Vector2(8f, 8f);
    [SerializeField] private Vector2 areaOffset = Vector2.zero;
    [SerializeField, Min(1)] private int spawnAttemptsPerEnemy = 8;
    [SerializeField, Min(0f)] private float spawnCheckRadius = 0.25f;
    [SerializeField] private LayerMask spawnBlockers;

    [Header("Spawn Counts")]
    [SerializeField, Min(0)] private int minConcurrentEnemies = 1;
    [SerializeField, Min(1)] private int maxConcurrentEnemies = 3;
    [SerializeField, Min(0)] private int initialSpawnQuota = 5;

    [Header("Respawn")]
    [SerializeField, Min(0f)] private float respawnDelay = 2f;

    [Header("Tempo")]
    [SerializeField] private TempoService tempoService;
    [SerializeField, Min(0f)] private float tempoChangeInterval = 10f;

    private readonly List<SkeletonEnemyBase> activeEnemies = new List<SkeletonEnemyBase>();
    private Coroutine refillRoutine;
    private bool hasStarted;
    private Transform playerTransform;
    private float tempoChangeTimer;
    private int totalSpawnedEnemies;

    private void Reset()
    {
        if (areaShape == null)
            areaShape = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        tempoChangeTimer = tempoChangeInterval;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            if (activeEnemies[i] == null)
                continue;

            activeEnemies[i].Died -= HandleEnemyDeath;
            activeEnemies[i].Died += HandleEnemyDeath;
        }

        if (hasStarted && spawnOnStart)
            SpawnToCurrentLimit();
    }

    private void OnValidate()
    {
        areaSize.x = Mathf.Max(0.01f, areaSize.x);
        areaSize.y = Mathf.Max(0.01f, areaSize.y);
        maxConcurrentEnemies = Mathf.Max(1, maxConcurrentEnemies);
        minConcurrentEnemies = Mathf.Clamp(minConcurrentEnemies, 0, maxConcurrentEnemies);
        initialSpawnQuota = Mathf.Max(0, initialSpawnQuota);

        if (areaShape == null)
            areaShape = GetComponent<Collider2D>();

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();
    }

    private void OnDisable()
    {
        StopRefillRoutine();

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            if (activeEnemies[i] != null)
                activeEnemies[i].Died -= HandleEnemyDeath;
        }
    }

    private void Start()
    {
        hasStarted = true;
        ResolveTempoService();
        ResolvePlayerTransform();

        if (!spawnOnStart)
            return;

        SpawnToCurrentLimit();
    }

    private void Update()
    {
        TickTempoCycling();
    }

    public void SpawnToCurrentLimit()
    {
        CleanupDestroyedEnemies();

        while (CanSpawnMore())
        {
            if (!TrySpawnEnemy())
                break;
        }
    }

    public void ResetSpawnProgress(bool clearExistingEnemies)
    {
        StopRefillRoutine();

        if (clearExistingEnemies)
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                SkeletonEnemyBase enemy = activeEnemies[i];
                if (enemy == null)
                    continue;

                enemy.Died -= HandleEnemyDeath;
                Destroy(enemy.gameObject);
            }

            activeEnemies.Clear();
        }

        totalSpawnedEnemies = 0;

        if (spawnOnStart)
            SpawnToCurrentLimit();
    }

    private bool TrySpawnEnemy()
    {
        SkeletonEnemyBase prefab = GetRandomPrefab();
        if (prefab == null)
            return false;

        Vector2 spawnPosition = GetSpawnPosition();
        SkeletonEnemyBase enemyInstance = Instantiate(prefab, spawnPosition, Quaternion.identity, spawnedEnemyParent);
        enemyInstance.Died += HandleEnemyDeath;

        if (keepSpawnedEnemiesContained)
        {
            if (HasAreaShape())
                enemyInstance.SetContainmentArea(areaShape);
            else
                enemyInstance.SetContainmentBounds(GetWorldBounds());
        }

        activeEnemies.Add(enemyInstance);
        totalSpawnedEnemies++;
        return true;
    }

    private void HandleEnemyDeath(SkeletonEnemyBase enemy)
    {
        if (enemy != null)
            enemy.Died -= HandleEnemyDeath;

        activeEnemies.Remove(enemy);

        if (!isActiveAndEnabled)
            return;

        CleanupDestroyedEnemies();

        if (CanSpawnMore() && refillRoutine == null)
            refillRoutine = StartCoroutine(RefillAfterDelay());
    }

    private IEnumerator RefillAfterDelay()
    {
        while (CanSpawnMore())
        {
            if (respawnDelay > 0f)
                yield return new WaitForSeconds(respawnDelay);

            CleanupDestroyedEnemies();

            if (!CanSpawnMore())
                break;

            if (!TrySpawnEnemy())
                break;
        }

        refillRoutine = null;
    }

    private bool CanSpawnMore()
    {
        return activeEnemies.Count < GetCurrentConcurrentLimit();
    }

    private int GetCurrentConcurrentLimit()
    {
        int highPressureLimit = Mathf.Max(minConcurrentEnemies, maxConcurrentEnemies);
        int sustainedLimit = Mathf.Clamp(minConcurrentEnemies, 0, highPressureLimit);

        return totalSpawnedEnemies < initialSpawnQuota
            ? highPressureLimit
            : sustainedLimit;
    }

    private SkeletonEnemyBase GetRandomPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        int validPrefabCount = 0;

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] != null)
                validPrefabCount++;
        }

        if (validPrefabCount == 0)
            return null;

        int prefabIndex = Random.Range(0, validPrefabCount);

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] == null)
                continue;

            if (prefabIndex == 0)
                return enemyPrefabs[i];

            prefabIndex--;
        }

        return null;
    }

    private Vector2 GetSpawnPosition()
    {
        Bounds searchBounds = GetSpawnSearchBounds();

        for (int i = 0; i < spawnAttemptsPerEnemy; i++)
        {
            Vector2 candidate = GetRandomPoint(searchBounds);
            if (IsSpawnPointValid(candidate))
                return candidate;
        }

        return GetFallbackSpawnPoint(searchBounds);
    }

    private Bounds GetWorldBounds()
    {
        Vector3 center = transform.position + (Vector3)areaOffset;
        Vector3 size = new Vector3(Mathf.Max(0.01f, areaSize.x), Mathf.Max(0.01f, areaSize.y), 0.1f);
        return new Bounds(center, size);
    }

    private Bounds GetSpawnSearchBounds()
    {
        return HasAreaShape() ? areaShape.bounds : GetWorldBounds();
    }

    private Vector2 GetRandomPoint(Bounds worldBounds)
    {
        return new Vector2(
            Random.Range(worldBounds.min.x, worldBounds.max.x),
            Random.Range(worldBounds.min.y, worldBounds.max.y));
    }

    private bool HasAreaShape()
    {
        return areaShape != null;
    }

    private void TickTempoCycling()
    {
        if (!ResolveTempoService() || !ResolvePlayerTransform())
            return;

        if (!IsPlayerInsideZone())
        {
            tempoChangeTimer = tempoChangeInterval;
            return;
        }

        if (tempoService.IsChanneling)
            return;

        if (tempoChangeTimer > 0f)
            tempoChangeTimer -= Time.deltaTime;

        if (tempoChangeTimer > 0f)
            return;

        if (tempoService.BeginChannel(GetRandomNextTempo(tempoService.CurrentTempo)))
            tempoChangeTimer = tempoChangeInterval;
    }

    private bool ResolveTempoService()
    {
        if (tempoService != null)
            return true;

        tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();
        return tempoService != null;
    }

    private bool ResolvePlayerTransform()
    {
        if (playerTransform != null)
            return true;

        PlayerDamageReceiver damageReceiver = FindAnyObjectByType<PlayerDamageReceiver>();
        if (damageReceiver != null)
        {
            playerTransform = damageReceiver.transform;
            return true;
        }

        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
            return true;
        }

        return false;
    }

    private bool IsPlayerInsideZone()
    {
        return playerTransform != null && IsPointInsideSpawnArea(playerTransform.position);
    }

    private TempoBand GetRandomNextTempo(TempoBand currentTempo)
    {
        TempoBand[] candidates = new TempoBand[3];
        int candidateIndex = 0;

        AddTempoCandidate(TempoBand.Slow, currentTempo, candidates, ref candidateIndex);
        AddTempoCandidate(TempoBand.Mid, currentTempo, candidates, ref candidateIndex);
        AddTempoCandidate(TempoBand.Fast, currentTempo, candidates, ref candidateIndex);
        AddTempoCandidate(TempoBand.Intense, currentTempo, candidates, ref candidateIndex);

        if (candidateIndex == 0)
            return currentTempo;

        return candidates[Random.Range(0, candidateIndex)];
    }

    private static void AddTempoCandidate(TempoBand candidate, TempoBand currentTempo, TempoBand[] candidates, ref int candidateIndex)
    {
        if (candidate == currentTempo)
            return;

        candidates[candidateIndex] = candidate;
        candidateIndex++;
    }

    private bool IsSpawnPointValid(Vector2 point)
    {
        if (!IsPointInsideSpawnArea(point))
            return false;

        if (spawnCheckRadius > 0f && Physics2D.OverlapCircle(point, spawnCheckRadius, spawnBlockers))
            return false;

        return true;
    }

    private bool IsPointInsideSpawnArea(Vector2 point)
    {
        if (HasAreaShape())
            return areaShape.OverlapPoint(point);

        Bounds bounds = GetWorldBounds();
        Vector3 testPoint = new Vector3(point.x, point.y, bounds.center.z);
        return bounds.Contains(testPoint);
    }

    private Vector2 GetFallbackSpawnPoint(Bounds searchBounds)
    {
        Vector2 centerPoint = searchBounds.center;
        if (IsSpawnPointValid(centerPoint))
            return centerPoint;

        if (HasAreaShape())
            return areaShape.ClosestPoint(centerPoint);

        return GetRandomPoint(searchBounds);
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    private void StopRefillRoutine()
    {
        if (refillRoutine == null)
            return;

        StopCoroutine(refillRoutine);
        refillRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (HasAreaShape())
            return;

        Bounds worldBounds = GetWorldBounds();

        Gizmos.color = new Color(0.95f, 0.35f, 0.2f, 0.2f);
        Gizmos.DrawCube(worldBounds.center, worldBounds.size);

        Gizmos.color = new Color(0.95f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
    }
}
