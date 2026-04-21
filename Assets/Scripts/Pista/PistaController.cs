using System.Collections.Generic;
using UnityEngine;

public class PistaController : MonoBehaviour
{
    public enum PistaState
    {
        FollowingPlayer,
        Aiming,
        Traveling,
        LatchedToLantern
    }

    private enum TravelDestination
    {
        None,
        Player,
        Lantern
    }

    [Header("References")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private SpriteRenderer travelSpriteRenderer;
    [SerializeField] private SpriteRenderer explosionSpriteRenderer;
    [SerializeField] private TrailRenderer[] travelTrails;
    [SerializeField] private ParticleSystem[] travelParticleEffects;

    [Header("Travel Visuals")]
    [SerializeField] private Sprite[] travelAnimationFrames;
    [SerializeField, Min(0f)] private float travelAnimationFramesPerSecond = 16f;
    [SerializeField] private float travelRotationOffset = -90f;

    [Header("Pulse Attack Visuals")]
    [SerializeField] private Sprite[] pulseAttackAnimationFrames;
    [SerializeField, Min(0f)] private float pulseAttackAnimationFramesPerSecond = 16f;

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
    public bool IsTraveling => CurrentState == PistaState.Traveling;

    private Transform playerTarget;
    private bool aimStickEngaged;
    private Sprite defaultTravelSprite;
    private Quaternion defaultTravelLocalRotation = Quaternion.identity;
    private Sprite defaultExplosionSprite;
    private bool isPlayingPulseAttackAnimation;
    private float travelAnimationElapsedTime;
    private float pulseAttackAnimationElapsedTime;
    private bool travelVisualsActive;
    private TravelDestination currentTravelDestination;
    private readonly Collider2D[] switchOverlapResults = new Collider2D[8];
    private readonly RaycastHit2D[] switchCastResults = new RaycastHit2D[8];
    private readonly Collider2D[] pulseAttackResults = new Collider2D[16];
    private readonly HashSet<int> activatedSwitchesThisTravel = new();

    private void Awake()
    {
        ResolvePlayerTarget();

        if (travelSpriteRenderer == null)
            travelSpriteRenderer = GetComponent<SpriteRenderer>();

        if (travelSpriteRenderer != null)
        {
            defaultTravelSprite = travelSpriteRenderer.sprite;
            defaultTravelLocalRotation = travelSpriteRenderer.transform.localRotation;
        }

        if (explosionSpriteRenderer == null)
            explosionSpriteRenderer = FindExplosionSpriteRenderer();

        if (explosionSpriteRenderer != null)
            defaultExplosionSprite = explosionSpriteRenderer.sprite;

        if (travelTrails == null || travelTrails.Length == 0)
            travelTrails = GetComponentsInChildren<TrailRenderer>(true);

        if (travelParticleEffects == null || travelParticleEffects.Length == 0)
            travelParticleEffects = GetComponentsInChildren<ParticleSystem>(true);

        if (playerTarget != null && CurrentState == PistaState.FollowingPlayer)
            transform.position = GetFollowAnchorPosition();

        ApplyTravelVisualState(forceRefresh: true);
    }

    private void Update()
    {
        ResolvePlayerTarget();

        switch (CurrentState)
        {
            case PistaState.FollowingPlayer:
                SnapToFollowAnchor();
                break;

            case PistaState.Aiming:
                if (CurrentLanternTarget != null)
                    SnapToLanternTarget();
                else
                    SnapToFollowAnchor();
                break;

            case PistaState.Traveling:
                UpdateTravel();
                break;

            case PistaState.LatchedToLantern:
                SnapToLanternTarget();
                break;
        }

        UpdatePulseAttackAnimation();
        UpdateTravelAnimation();
        ApplyTravelVisualState();
    }

    public void SetPlayerTarget(Transform targetTransform)
    {
        playerTarget = targetTransform;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        playerTarget = null;
        CurrentLanternTarget = null;
        CurrentPreviewTarget = null;
        currentTravelDestination = TravelDestination.None;
    }

    public void BeginAiming()
    {
        SetState(PistaState.Aiming);
    }

    public void EndAiming()
    {
        aimStickEngaged = false;
        CurrentPreviewTarget = null;

        if (CurrentState == PistaState.Traveling)
            return;

        if (CurrentLanternTarget != null)
        {
            SetState(PistaState.LatchedToLantern);
            return;
        }

        SetState(PistaState.FollowingPlayer);
    }

    public void MoveToLantern(Transform lanternTarget)
    {
        if (lanternTarget == null)
            return;

        CurrentPreviewTarget = null;
        CurrentLanternTarget = lanternTarget;
        activatedSwitchesThisTravel.Clear();
        currentTravelDestination = TravelDestination.Lantern;
        SetState(PistaState.Traveling);
    }

    public void RecallToPlayer()
    {
        CurrentLanternTarget = null;
        CurrentPreviewTarget = null;
        aimStickEngaged = false;
        activatedSwitchesThisTravel.Clear();

        if (playerTarget == null)
        {
            currentTravelDestination = TravelDestination.None;
            SetState(PistaState.FollowingPlayer);
            return;
        }

        currentTravelDestination = TravelDestination.Player;
        SetState(PistaState.Traveling);
    }

    public void SnapToPlayer()
    {
        CurrentLanternTarget = null;
        CurrentPreviewTarget = null;
        aimStickEngaged = false;
        activatedSwitchesThisTravel.Clear();
        currentTravelDestination = TravelDestination.None;
        SetState(PistaState.FollowingPlayer);
        transform.position = GetFollowAnchorPosition();
    }

    public int TriggerPulseAttack()
    {
        if (CurrentState != PistaState.LatchedToLantern || CurrentLanternTarget == null)
            return 0;

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

        BeginPulseAttackAnimation();
        return affectedTargetCount;
    }

    public void ProcessAimInput(Vector2 aimInput)
    {
        if (CurrentState == PistaState.Traveling)
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
        ResolvePlayerTarget();

        if (playerTarget == null)
            return transform.position;

        return playerTarget.position + (Vector3)followOffset;
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null)
            return;

        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            playerTarget = playerController.transform;
    }

    private void UpdateTravel()
    {
        Vector3 destinationPosition;
        float travelSpeed;

        switch (currentTravelDestination)
        {
            case TravelDestination.Player:
                destinationPosition = GetFollowAnchorPosition();
                travelSpeed = followMoveSpeed;
                break;

            case TravelDestination.Lantern:
                if (CurrentLanternTarget == null)
                {
                    currentTravelDestination = TravelDestination.None;
                    SetState(PistaState.FollowingPlayer);
                    return;
                }

                destinationPosition = CurrentLanternTarget.position;
                travelSpeed = lanternMoveSpeed;
                break;

            default:
                SetState(PistaState.FollowingPlayer);
                return;
        }

        Vector3 previousPosition = transform.position;
        MoveToward(destinationPosition, travelSpeed);
        UpdateTravelRotation((Vector2)(transform.position - previousPosition));
        ActivateSwitchesAlongTravel(previousPosition, transform.position);

        if ((destinationPosition - transform.position).sqrMagnitude > arrivalDistance * arrivalDistance)
            return;

        transform.position = destinationPosition;
        ActivateNearbySwitches(transform.position, CreateSwitchContactFilter(), GetSwitchInteractionRadius());

        if (currentTravelDestination == TravelDestination.Lantern)
        {
            currentTravelDestination = TravelDestination.None;
            SetState(PistaState.LatchedToLantern);
            return;
        }

        currentTravelDestination = TravelDestination.None;
        SetState(PistaState.FollowingPlayer);
    }

    private void SnapToLanternTarget()
    {
        if (CurrentLanternTarget == null)
        {
            SetState(PistaState.FollowingPlayer);
            return;
        }

        transform.position = CurrentLanternTarget.position;
    }

    private void SnapToFollowAnchor()
    {
        transform.position = GetFollowAnchorPosition();
    }

    private void MoveToward(Vector3 targetPosition, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private void SetState(PistaState newState)
    {
        if (CurrentState == newState)
            return;

        bool wasTraveling = CurrentState == PistaState.Traveling;
        CurrentState = newState;

        if (!wasTraveling && newState == PistaState.Traveling)
            BeginTravelAnimation();
        else if (wasTraveling && newState != PistaState.Traveling)
            StopTravelAnimation();

        ApplyTravelVisualState(forceRefresh: true);
    }

    private void BeginTravelAnimation()
    {
        travelAnimationElapsedTime = 0f;
        ApplyTravelAnimationFrame(0);
        ResetTravelRotation();
    }

    private void StopTravelAnimation()
    {
        travelAnimationElapsedTime = 0f;

        if (travelSpriteRenderer != null && defaultTravelSprite != null)
            travelSpriteRenderer.sprite = defaultTravelSprite;

        ResetTravelRotation();
    }

    private void BeginPulseAttackAnimation()
    {
        if (explosionSpriteRenderer == null || !HasSprites(pulseAttackAnimationFrames))
            return;

        isPlayingPulseAttackAnimation = true;
        pulseAttackAnimationElapsedTime = 0f;
        explosionSpriteRenderer.enabled = true;
        explosionSpriteRenderer.sprite = GetFirstAvailableSprite(pulseAttackAnimationFrames);
        ApplyTravelVisualState(forceRefresh: true);
    }

    private void UpdatePulseAttackAnimation()
    {
        if (!isPlayingPulseAttackAnimation)
            return;

        if (explosionSpriteRenderer == null || !HasSprites(pulseAttackAnimationFrames) || pulseAttackAnimationFramesPerSecond <= 0f)
        {
            StopPulseAttackAnimation();
            return;
        }

        pulseAttackAnimationElapsedTime += Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(pulseAttackAnimationElapsedTime * pulseAttackAnimationFramesPerSecond);

        if (frameIndex >= pulseAttackAnimationFrames.Length)
        {
            StopPulseAttackAnimation();
            return;
        }

        Sprite animationFrame = pulseAttackAnimationFrames[frameIndex];
        if (animationFrame != null)
            explosionSpriteRenderer.sprite = animationFrame;
    }

    private void StopPulseAttackAnimation()
    {
        isPlayingPulseAttackAnimation = false;
        pulseAttackAnimationElapsedTime = 0f;

        if (explosionSpriteRenderer != null)
        {
            explosionSpriteRenderer.sprite = defaultExplosionSprite;
            explosionSpriteRenderer.enabled = false;
        }

        ApplyTravelVisualState(forceRefresh: true);
    }

    private void ApplyTravelVisualState(bool forceRefresh = false)
    {
        bool shouldShowTravelVisuals = CurrentState == PistaState.Traveling;
        if (!forceRefresh && travelVisualsActive == shouldShowTravelVisuals)
            return;

        travelVisualsActive = shouldShowTravelVisuals;

        if (travelSpriteRenderer != null)
        {
            if (shouldShowTravelVisuals)
            {
                if (forceRefresh)
                    ApplyTravelAnimationFrame(0);
            }
            else
            {
                if (defaultTravelSprite != null)
                    travelSpriteRenderer.sprite = defaultTravelSprite;

                ResetTravelRotation();
            }

            travelSpriteRenderer.enabled = shouldShowTravelVisuals;
        }

        if (explosionSpriteRenderer != null)
        {
            if (!isPlayingPulseAttackAnimation)
                explosionSpriteRenderer.sprite = defaultExplosionSprite;

            explosionSpriteRenderer.enabled = isPlayingPulseAttackAnimation;
        }

        if (travelTrails != null)
        {
            for (int i = 0; i < travelTrails.Length; i++)
            {
                TrailRenderer trail = travelTrails[i];
                if (trail == null)
                    continue;

                trail.emitting = shouldShowTravelVisuals;

                if (!shouldShowTravelVisuals)
                    trail.Clear();
            }
        }

        if (travelParticleEffects == null)
            return;

        for (int i = 0; i < travelParticleEffects.Length; i++)
        {
            ParticleSystem particleEffect = travelParticleEffects[i];
            if (particleEffect == null)
                continue;

            if (shouldShowTravelVisuals)
            {
                if (!particleEffect.isPlaying)
                    particleEffect.Play(true);
            }
            else
            {
                particleEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void UpdateTravelAnimation()
    {
        if (CurrentState != PistaState.Traveling)
            return;

        if (!HasSprites(travelAnimationFrames))
        {
            if (travelSpriteRenderer != null && defaultTravelSprite != null)
                travelSpriteRenderer.sprite = defaultTravelSprite;

            return;
        }

        if (travelAnimationFramesPerSecond <= 0f)
        {
            ApplyTravelAnimationFrame(0);
            return;
        }

        travelAnimationElapsedTime += Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(travelAnimationElapsedTime * travelAnimationFramesPerSecond) % travelAnimationFrames.Length;
        ApplyTravelAnimationFrame(frameIndex);
    }

    private void ApplyTravelAnimationFrame(int frameIndex)
    {
        if (travelSpriteRenderer == null)
            return;

        Sprite sprite = defaultTravelSprite;

        if (travelAnimationFrames != null && frameIndex >= 0 && frameIndex < travelAnimationFrames.Length)
            sprite = travelAnimationFrames[frameIndex] != null ? travelAnimationFrames[frameIndex] : GetFirstAvailableSprite(travelAnimationFrames);

        if (sprite != null)
            travelSpriteRenderer.sprite = sprite;
    }

    private void UpdateTravelRotation(Vector2 movementDelta)
    {
        if (travelSpriteRenderer == null || movementDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        float angle = Mathf.Atan2(movementDelta.y, movementDelta.x) * Mathf.Rad2Deg + travelRotationOffset;
        travelSpriteRenderer.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward) * defaultTravelLocalRotation;
    }

    private void ResetTravelRotation()
    {
        if (travelSpriteRenderer != null)
            travelSpriteRenderer.transform.localRotation = defaultTravelLocalRotation;
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

        if (!TryGetInterface(hit, out ILightable lightable, out Component lightableComponent) || !lightable.IsLit)
            return false;

        targetTransform = lightableComponent != null ? lightableComponent.transform : hit.transform;
        return true;
    }

    private bool IsBlocked(Vector3 targetPosition)
    {
        if (obstacleLayers.value == 0)
            return false;

        RaycastHit2D hit = Physics2D.Linecast(transform.position, targetPosition, obstacleLayers);
        return hit.collider != null;
    }

    private SpriteRenderer FindExplosionSpriteRenderer()
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer != null && spriteRenderer != travelSpriteRenderer)
                return spriteRenderer;
        }

        return null;
    }

    private static bool HasSprites(Sprite[] sprites)
    {
        if (sprites == null)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return true;
        }

        return false;
    }

    private static Sprite GetFirstAvailableSprite(Sprite[] sprites)
    {
        if (sprites == null)
            return null;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return sprites[i];
        }

        return null;
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
