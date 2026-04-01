using System;
using UnityEngine;

public class TempoService : MonoBehaviour
{
    [SerializeField] private TempoBand startingTempo = TempoBand.Mid;
    [SerializeField] private float channelDuration = 1.5f;
    [SerializeField] private float cancelGracePeriod = 0.15f;

    public static TempoService Instance { get; private set; }

    public event Action<TempoStateSnapshot> TempoUpdated;

    public TempoBand CurrentTempo { get; private set; } = TempoBand.Mid;
    public TempoBand TargetTempo { get; private set; } = TempoBand.Mid;
    public bool IsChanneling { get; private set; }
    public float ChannelDuration => channelDuration;
    public float CancelGracePeriod => cancelGracePeriod;
    public float ChannelElapsed { get; private set; }
    public float ChannelRemaining => Mathf.Max(0f, channelDuration - ChannelElapsed);
    public float ChannelProgress => channelDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(ChannelElapsed / channelDuration);

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple TempoService instances.", this);

        Instance = this;
        CurrentTempo = startingTempo;
        TargetTempo = startingTempo;
        ChannelElapsed = 0f;
        IsChanneling = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsChanneling)
            return;

        ChannelElapsed += Time.deltaTime;

        if (ChannelElapsed < channelDuration)
            return;

        CompleteChannel();
    }

    public bool BeginChannel(TempoBand targetTempo)
    {
        if (targetTempo == CurrentTempo)
            return false;

        if (IsChanneling && targetTempo == TargetTempo)
            return false;

        bool wasChanneling = IsChanneling;
        TargetTempo = targetTempo;
        ChannelElapsed = 0f;
        IsChanneling = true;

        Debug.Log($"Tempo channel started: {CurrentTempo} -> {TargetTempo}", this);

        Broadcast(wasChanneling ? TempoUpdateType.ChannelTargetChanged : TempoUpdateType.ChannelStarted);
        return true;
    }

    public bool CancelChannel(bool allowGraceCompletion)
    {
        if (!IsChanneling)
            return false;

        if (allowGraceCompletion && ChannelRemaining <= cancelGracePeriod)
        {
            CompleteChannel();
            return true;
        }

        IsChanneling = false;
        TargetTempo = CurrentTempo;
        ChannelElapsed = 0f;
        Broadcast(TempoUpdateType.ChannelCanceled);
        return true;
    }

    public void SetTempoImmediate(TempoBand tempo)
    {
        CurrentTempo = tempo;
        TargetTempo = tempo;
        IsChanneling = false;
        ChannelElapsed = 0f;
        Broadcast(TempoUpdateType.ChannelCompleted);
    }

    public TempoStateSnapshot GetCurrentSnapshot(TempoUpdateType updateType = TempoUpdateType.Initialized)
    {
        return new TempoStateSnapshot(
            CurrentTempo,
            TargetTempo,
            IsChanneling,
            channelDuration,
            ChannelElapsed,
            cancelGracePeriod,
            updateType);
    }

    private void CompleteChannel()
    {
        CurrentTempo = TargetTempo;
        IsChanneling = false;
        ChannelElapsed = channelDuration;
        Broadcast(TempoUpdateType.ChannelCompleted);
        ChannelElapsed = 0f;
    }

    private void Broadcast(TempoUpdateType updateType)
    {
        TempoUpdated?.Invoke(GetCurrentSnapshot(updateType));
    }
}
