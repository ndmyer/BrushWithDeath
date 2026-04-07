using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RadioController : MonoBehaviour, IInteractable
{
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
    [SerializeField] private UnityEvent onTurnedOff;
    [SerializeField] private UnityEvent onTurnedOn;
    [SerializeField] private RadioStateEvent onStateChanged;

    public RadioState CurrentState { get; private set; }
    public bool IsActive => CurrentState != RadioState.Off;
    public float BroadcastRadius => broadcastRadius;
    public TempoBand BroadcastTempo => MapStateToTempo(CurrentState);

    private readonly HashSet<TempoReceiver> affectedReceivers = new();
    private readonly List<TempoReceiver> receiversToClear = new();
    private float refreshTimer;

    private void Awake()
    {
        if (auraRenderer == null)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject == gameObject)
                    continue;

                auraRenderer = renderers[i];
                break;
            }
        }

        if (auraTransform == null && auraRenderer != null)
            auraTransform = auraRenderer.transform;

        CurrentState = startingState;
        ApplyStateVisuals(true);
    }

    private void OnValidate()
    {
        if (auraTransform == null && auraRenderer != null)
            auraTransform = auraRenderer.transform;

        if (!Application.isPlaying)
            ApplyStateVisuals(false);
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

    private void OnDrawGizmosSelected()
    {
        RadioState previewState = Application.isPlaying ? CurrentState : startingState;
        Gizmos.color = previewState == RadioState.Off ? new Color(1f, 1f, 1f, 0.2f) : GetAuraColor(previewState);
        Gizmos.DrawWireSphere(transform.position, broadcastRadius);
    }
}
