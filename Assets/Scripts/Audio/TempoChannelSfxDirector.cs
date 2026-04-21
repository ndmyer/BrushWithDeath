using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class TempoChannelSfxDirector : MonoBehaviour
{
    [SerializeField] private TempoService tempoService;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField, Range(0f, 1f)] private float loopVolume = 1f;
    [SerializeField] private bool ignoreListenerPause = true;

    private TempoService subscribedTempoService;
    private TempoBand activeTempo = TempoBand.Mid;

    private void Awake()
    {
        CacheReferences();
        ConfigureAudioSource();
        BindTempoService();
        SyncLoopPlayback(GetSnapshot());
    }

    private void OnEnable()
    {
        CacheReferences();
        ConfigureAudioSource();
        BindTempoService();
        SyncLoopPlayback(GetSnapshot());
    }

    private void OnDisable()
    {
        StopLoop();
        UnbindTempoService();
    }

    private void OnDestroy()
    {
        UnbindTempoService();
    }

    private void HandleTempoUpdated(TempoStateSnapshot snapshot)
    {
        SyncLoopPlayback(snapshot);
    }

    private void SyncLoopPlayback(TempoStateSnapshot snapshot)
    {
        if (!snapshot.IsChanneling || snapshot.TargetTempo == snapshot.CurrentTempo)
        {
            StopLoop();
            return;
        }

        AudioClip targetLoop = GetLoopClip(snapshot.TargetTempo);
        if (targetLoop == null)
        {
            StopLoop();
            return;
        }

        if (audioSource.isPlaying && audioSource.clip == targetLoop && activeTempo == snapshot.TargetTempo)
            return;

        activeTempo = snapshot.TargetTempo;
        audioSource.clip = targetLoop;
        audioSource.volume = Mathf.Clamp01(loopVolume);
        audioSource.Play();
    }

    private void StopLoop()
    {
        if (audioSource == null)
            return;

        if (audioSource.isPlaying)
            audioSource.Stop();

        audioSource.clip = null;
    }

    private AudioClip GetLoopClip(TempoBand tempoBand)
    {
        GameSfxLibrary library = GameSfxLibrary.Instance;
        if (library == null)
            return null;

        return tempoBand switch
        {
            TempoBand.Slow => library.GuitarSlowLoopClip,
            TempoBand.Fast => library.GuitarFastLoopClip,
            TempoBand.Intense => library.GuitarIntenseLoopClip,
            _ => library.GuitarMidLoopClip,
        };
    }

    private void CacheReferences()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        audioSource.ignoreListenerPause = ignoreListenerPause;
        audioSource.outputAudioMixerGroup = outputMixerGroup;
    }

    private void BindTempoService()
    {
        if (tempoService == subscribedTempoService)
            return;

        UnbindTempoService();
        subscribedTempoService = tempoService;

        if (subscribedTempoService == null)
            return;

        subscribedTempoService.TempoUpdated -= HandleTempoUpdated;
        subscribedTempoService.TempoUpdated += HandleTempoUpdated;
    }

    private void UnbindTempoService()
    {
        if (subscribedTempoService == null)
            return;

        subscribedTempoService.TempoUpdated -= HandleTempoUpdated;
        subscribedTempoService = null;
    }

    private TempoStateSnapshot GetSnapshot()
    {
        if (tempoService != null)
            return tempoService.GetCurrentSnapshot();

        return new TempoStateSnapshot(
            TempoBand.Mid,
            TempoBand.Mid,
            false,
            1f,
            0f,
            0f,
            TempoUpdateType.Initialized);
    }
}
