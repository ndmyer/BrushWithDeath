using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RadioController : MonoBehaviour, IInteractable
{
    private static readonly Color MidTempoColor = new Color(0.93f, 0.77f, 0.34f, 1f);

    public enum RadioState
    {
        Off,
        Slow,
        Fast,
        Intense
    }

    [System.Serializable]
    public class RadioStateEvent : UnityEvent<RadioState> { }

    [SerializeField] private RadioState startingState = RadioState.Off;
    [SerializeField] private float broadcastRadius = 3f;
    [SerializeField] private float refreshInterval = 0.1f;
    [SerializeField] private SpriteRenderer auraRenderer;
    [SerializeField] private Transform auraTransform;
    [SerializeField] private float auraDiameterMultiplier = 2f;
    [SerializeField] private Color slowAuraColor = new Color(0.2f, 0.6f, 1f, 0.2f);
    [SerializeField] private Color fastAuraColor = new Color(1f, 0.65f, 0.2f, 0.2f);
    [SerializeField] private Color intenseAuraColor = new Color(1f, 0.2f, 0.2f, 0.25f);
    [SerializeField] private SpriteRenderer offStateRenderer;
    [SerializeField] private SpriteRenderer activeStateRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite[] slowAnimationFrames;
    [SerializeField, Min(0f)] private float slowAnimationFramesPerSecond = 10f;
    [SerializeField] private Sprite[] fastAnimationFrames;
    [SerializeField, Min(0f)] private float fastAnimationFramesPerSecond = 10f;
    [SerializeField] private Sprite[] intenseAnimationFrames;
    [SerializeField, Min(0f)] private float intenseAnimationFramesPerSecond = 10f;
    [SerializeField] private ParticleSystem onParticleSystem;
    [SerializeField] private UnityEvent onTurnedOff;
    [SerializeField] private UnityEvent onTurnedOn;
    [SerializeField] private RadioStateEvent onStateChanged;

    public RadioState CurrentState { get; private set; }
    public bool IsActive => CurrentState != RadioState.Off;
    public float BroadcastRadius => broadcastRadius;
    public TempoBand BroadcastTempo => MapStateToTempo(CurrentState);

    public Color GetTempoColor(TempoBand tempoBand)
    {
        if (tempoBand == TempoBand.Mid)
            return MidTempoColor;

        return GetAuraColor(MapTempoToState(tempoBand));
    }

    private readonly HashSet<TempoReceiver> affectedReceivers = new();
    private readonly List<TempoReceiver> receiversToClear = new();
    private float refreshTimer;
    private Sprite defaultActiveSprite;
    private RadioState lastAnimatedState = RadioState.Off;
    private float activeAnimationElapsedTime;

    private void Awake()
    {
        AutoAssignAuraRenderer();
        AutoAssignVisualRenderers();
        AutoAssignParticleSystem();

        if (auraTransform == null && auraRenderer != null)
            auraTransform = auraRenderer.transform;

        if (offStateRenderer != null && offSprite == null)
            offSprite = offStateRenderer.sprite;

        if (activeStateRenderer != null)
            defaultActiveSprite = activeStateRenderer.sprite;

        CurrentState = startingState;
        ApplyStateVisuals(true);
    }

    private void OnValidate()
    {
        AutoAssignAuraRenderer();
        AutoAssignVisualRenderers();
        AutoAssignParticleSystem();

        if (auraTransform == null && auraRenderer != null)
            auraTransform = auraRenderer.transform;

        if (offStateRenderer != null && offSprite == null)
            offSprite = offStateRenderer.sprite;

        if (activeStateRenderer != null)
            defaultActiveSprite = activeStateRenderer.sprite;

        if (!Application.isPlaying)
        {
            CurrentState = startingState;
            ApplyStateVisuals(false);
        }
    }

    private void OnEnable()
    {
        refreshTimer = 0f;
        ApplyStateVisuals(true);

        if (IsActive)
            RefreshReceivers();
    }

    private void OnDisable()
    {
        StopOnParticles();
        ClearAffectedReceivers();
    }

    private void Update()
    {
        if (!IsActive)
            return;

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = Mathf.Max(0.01f, refreshInterval);
        RefreshReceivers();
    }

    public void Interact(PlayerController player)
    {
        AdvanceState();
    }

    public void AdvanceState()
    {
        CurrentState = CurrentState switch
        {
            RadioState.Off => RadioState.Slow,
            RadioState.Slow => RadioState.Fast,
            RadioState.Fast => RadioState.Intense,
            _ => RadioState.Off
        };

        ApplyStateVisuals(true);
        refreshTimer = 0f;

        if (IsActive)
            RefreshReceivers();
        else
            ClearAffectedReceivers();
    }

    private void RefreshReceivers()
    {
        float radiusSquared = broadcastRadius * broadcastRadius;
        Vector3 origin = transform.position;
        TempoBand tempo = BroadcastTempo;

        receiversToClear.Clear();
        foreach (TempoReceiver receiver in affectedReceivers)
            receiversToClear.Add(receiver);

        foreach (TempoReceiver receiver in TempoReceiver.ActiveReceivers)
        {
            if (receiver == null)
                continue;

            Vector3 receiverPoint = receiver.GetClosestBroadcastPoint(origin);
            Vector3 offset = receiverPoint - origin;
            if (offset.sqrMagnitude > radiusSquared)
                continue;

            receiver.ReceiveTempo(tempo);
            affectedReceivers.Add(receiver);
            receiversToClear.Remove(receiver);
        }

        for (int i = 0; i < receiversToClear.Count; i++)
        {
            TempoReceiver receiver = receiversToClear[i];
            if (receiver == null)
                continue;

            receiver.ReceiveTempo(TempoBand.Mid);
            affectedReceivers.Remove(receiver);
        }
    }

    private void ClearAffectedReceivers()
    {
        foreach (TempoReceiver receiver in affectedReceivers)
        {
            if (receiver == null)
                continue;

            receiver.ReceiveTempo(TempoBand.Mid);
        }

        affectedReceivers.Clear();
        receiversToClear.Clear();
    }

    private void ApplyStateVisuals(bool invokeEvents)
    {
        if (auraTransform != null)
        {
            float diameter = Mathf.Max(0f, broadcastRadius) * Mathf.Max(0f, auraDiameterMultiplier);
            auraTransform.localScale = new Vector3(diameter, diameter, 1f);
        }

        if (auraRenderer != null)
        {
            auraRenderer.enabled = IsActive;
            if (IsActive)
                auraRenderer.color = GetAuraColor(CurrentState);
        }

        ApplyRadioVisualState();
        ApplyParticleState();

        if (!invokeEvents)
            return;

        onStateChanged?.Invoke(CurrentState);

        if (IsActive)
            onTurnedOn?.Invoke();
        else
            onTurnedOff?.Invoke();
    }

    private Color GetAuraColor(RadioState state)
    {
        return state switch
        {
            RadioState.Slow => slowAuraColor,
            RadioState.Fast => fastAuraColor,
            RadioState.Intense => intenseAuraColor,
            _ => Color.clear
        };
    }

    private static TempoBand MapStateToTempo(RadioState state)
    {
        return state switch
        {
            RadioState.Slow => TempoBand.Slow,
            RadioState.Fast => TempoBand.Fast,
            RadioState.Intense => TempoBand.Intense,
            _ => TempoBand.Mid
        };
    }

    private static RadioState MapTempoToState(TempoBand tempoBand)
    {
        return tempoBand switch
        {
            TempoBand.Slow => RadioState.Slow,
            TempoBand.Fast => RadioState.Fast,
            TempoBand.Intense => RadioState.Intense,
            _ => RadioState.Off
        };
    }

    private void AutoAssignAuraRenderer()
    {
        if (auraRenderer != null)
            return;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.gameObject == gameObject)
                continue;

            if (renderer.name.IndexOf("aura", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                auraRenderer = renderer;
                return;
            }
        }
    }

    private void AutoAssignVisualRenderers()
    {
        if (offStateRenderer != null && activeStateRenderer != null)
            return;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer == auraRenderer)
                continue;

            bool isOffRenderer = renderer.name.IndexOf("off", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isActiveRenderer = renderer.name.IndexOf("on", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    renderer.name.IndexOf("active", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    renderer.name.IndexOf("visual", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (offStateRenderer == null && isOffRenderer)
            {
                offStateRenderer = renderer;
                continue;
            }

            if (activeStateRenderer == null && isActiveRenderer)
            {
                activeStateRenderer = renderer;
                continue;
            }

            if (offStateRenderer == null)
            {
                offStateRenderer = renderer;
                continue;
            }

            if (activeStateRenderer == null && renderer != offStateRenderer)
            {
                activeStateRenderer = renderer;
                return;
            }
        }
    }

    private void AutoAssignParticleSystem()
    {
        if (onParticleSystem != null)
            return;

        onParticleSystem = GetComponentInChildren<ParticleSystem>(true);
    }

    private void ApplyRadioVisualState()
    {
        if (offStateRenderer != null)
        {
            offStateRenderer.gameObject.SetActive(!IsActive);
            if (!IsActive && offSprite != null)
                offStateRenderer.sprite = offSprite;
        }

        if (activeStateRenderer == null)
            return;

        activeStateRenderer.gameObject.SetActive(IsActive);

        if (!IsActive)
        {
            activeAnimationElapsedTime = 0f;
            lastAnimatedState = RadioState.Off;
            if (defaultActiveSprite != null)
                activeStateRenderer.sprite = defaultActiveSprite;
            return;
        }

        if (lastAnimatedState != CurrentState)
        {
            activeAnimationElapsedTime = 0f;
            lastAnimatedState = CurrentState;
        }

        ApplyActiveAnimationFrame(0f);
    }

    private void ApplyParticleState()
    {
        if (onParticleSystem == null)
            return;

        ApplyParticleColor();

        if (!Application.isPlaying)
            return;

        if (IsActive)
        {
            if (!onParticleSystem.isPlaying)
                onParticleSystem.Play(true);

            return;
        }

        StopOnParticles();
    }

    private void ApplyParticleColor()
    {
        if (onParticleSystem == null)
            return;

        if (!IsActive)
            return;

        ParticleSystem.MainModule main = onParticleSystem.main;
        Color particleColor = GetAuraColor(CurrentState);
        particleColor.a = Mathf.Max(0.6f, particleColor.a);
        main.startColor = particleColor;
    }

    private void StopOnParticles()
    {
        if (onParticleSystem == null)
            return;

        onParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LateUpdate()
    {
        if (!IsActive)
            return;

        ApplyActiveAnimationFrame(Time.deltaTime);
    }

    private void ApplyActiveAnimationFrame(float deltaTime)
    {
        if (activeStateRenderer == null)
            return;

        Sprite[] animationFrames = GetAnimationFrames(CurrentState);
        Sprite fallbackSprite = defaultActiveSprite;

        if (!HasSprites(animationFrames))
        {
            if (fallbackSprite != null)
                activeStateRenderer.sprite = fallbackSprite;
            return;
        }

        float framesPerSecond = GetAnimationFramesPerSecond(CurrentState);
        if (framesPerSecond <= 0f)
        {
            activeStateRenderer.sprite = GetFirstAvailableSprite(animationFrames) ?? fallbackSprite;
            return;
        }

        activeAnimationElapsedTime += deltaTime;
        int frameIndex = Mathf.FloorToInt(activeAnimationElapsedTime * framesPerSecond) % animationFrames.Length;
        Sprite nextFrame = animationFrames[frameIndex];
        activeStateRenderer.sprite = nextFrame != null ? nextFrame : GetFirstAvailableSprite(animationFrames) ?? fallbackSprite;
    }

    private Sprite[] GetAnimationFrames(RadioState state)
    {
        return state switch
        {
            RadioState.Slow => slowAnimationFrames,
            RadioState.Fast => fastAnimationFrames,
            RadioState.Intense => intenseAnimationFrames,
            _ => null
        };
    }

    private float GetAnimationFramesPerSecond(RadioState state)
    {
        return state switch
        {
            RadioState.Slow => slowAnimationFramesPerSecond,
            RadioState.Fast => fastAnimationFramesPerSecond,
            RadioState.Intense => intenseAnimationFramesPerSecond,
            _ => 0f
        };
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
        RadioState previewState = Application.isPlaying ? CurrentState : startingState;
        Gizmos.color = previewState == RadioState.Off ? new Color(1f, 1f, 1f, 0.2f) : GetAuraColor(previewState);
        Gizmos.DrawWireSphere(transform.position, broadcastRadius);
    }
}
