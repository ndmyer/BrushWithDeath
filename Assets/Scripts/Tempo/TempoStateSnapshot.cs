using UnityEngine;

public readonly struct TempoStateSnapshot
{
    public TempoStateSnapshot(
        TempoBand currentTempo,
        TempoBand targetTempo,
        bool isChanneling,
        float channelDuration,
        float channelElapsed,
        float cancelGracePeriod,
        TempoUpdateType updateType)
    {
        CurrentTempo = currentTempo;
        TargetTempo = targetTempo;
        IsChanneling = isChanneling;
        ChannelDuration = Mathf.Max(0f, channelDuration);
        ChannelElapsed = Mathf.Clamp(channelElapsed, 0f, ChannelDuration);
        CancelGracePeriod = Mathf.Max(0f, cancelGracePeriod);
        UpdateType = updateType;
    }

    public TempoBand CurrentTempo { get; }
    public TempoBand TargetTempo { get; }
    public bool IsChanneling { get; }
    public float ChannelDuration { get; }
    public float ChannelElapsed { get; }
    public float CancelGracePeriod { get; }
    public TempoUpdateType UpdateType { get; }
    public float ChannelRemaining => Mathf.Max(0f, ChannelDuration - ChannelElapsed);
    public float ChannelProgress => ChannelDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(ChannelElapsed / ChannelDuration);
    public bool CompletedThisUpdate => UpdateType == TempoUpdateType.ChannelCompleted;
}
