using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class TempoMusicDirector : MonoBehaviour
{
    private const string BankRootPrefix = "_TempoMusicBank";
    private const int BankCount = 2;
    private const double ScheduledStartLeadTime = 0.05d;

    private enum SourceSlot
    {
        Main,
        Slow,
        Mid,
        Fast,
        Intense
    }

    private sealed class MusicBank
    {
        public TempoMusicSet MusicSet { get; set; }
        public float Weight { get; set; }
        public AudioSource MainSource { get; set; }
        public AudioSource SlowSource { get; set; }
        public AudioSource MidSource { get; set; }
        public AudioSource FastSource { get; set; }
        public AudioSource IntenseSource { get; set; }
    }

    private static TempoMusicDirector instance;

    [Header("References")]
    [SerializeField] private TempoService tempoService;
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    [Header("Default Music")]
    [SerializeField] private TempoMusicSet defaultMusicSet;
    [SerializeField] private bool playDefaultOnEnable = true;

    [Header("Transition Settings")]
    [SerializeField, Min(0f)] private float defaultSetTransitionDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Min(0f)] private float radioCrossfadeSpeed = 3f;

    [Header("Source Settings")]
    [SerializeField] private bool loopSources = true;
    [SerializeField] private bool ignoreListenerPause = true;

    private readonly List<TempoMusicZone> activeZones = new();
    private readonly MusicBank[] banks = new MusicBank[BankCount];

    private TempoService subscribedTempoService;
    private TempoStateSnapshot currentSnapshot;
    private int activeBankIndex = -1;
    private int transitionBankIndex = -1;
    private bool isSetTransitioning;
    private float setTransitionDuration;
    private float setTransitionElapsed;
    private Transform playerTransform;
    private float radioCrossfadeBlend;

    public static TempoMusicDirector Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<TempoMusicDirector>();

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
            Debug.LogWarning("Multiple TempoMusicDirector instances.", this);

        instance = this;
        EnsureBanks();
        TryBindTempoService();
        currentSnapshot = GetTempoSnapshot();
    }

    private void OnEnable()
    {
        TryBindTempoService();
        currentSnapshot = GetTempoSnapshot();

        if (playDefaultOnEnable)
            EvaluateZoneRequest(immediate: true);
    }

    private void OnDisable()
    {
        UnbindTempoService();
        StopAllBanks();
        activeZones.Clear();
    }

    private void OnDestroy()
    {
        UnbindTempoService();

        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        TryBindTempoService();
        currentSnapshot = GetTempoSnapshot();
        UpdateRadioCrossfadeBlend();
        UpdateSetTransition();
        ApplyBankVolumes();
    }

    public void EnterZone(TempoMusicZone zone)
    {
        if (zone == null || activeZones.Contains(zone))
            return;

        activeZones.Add(zone);
        EvaluateZoneRequest();
    }

    public void ExitZone(TempoMusicZone zone)
    {
        if (zone == null)
            return;

        if (!activeZones.Remove(zone))
            return;

        EvaluateZoneRequest();
    }

    public void PlaySet(TempoMusicSet musicSet, float transitionDuration = -1f)
    {
        TransitionToSet(musicSet, ResolveTransitionDuration(transitionDuration));
    }

    public void FadeOut(float transitionDuration = -1f)
    {
        TransitionToSet(null, ResolveTransitionDuration(transitionDuration));
    }

    public void PlayDefaultSet(bool immediate = false)
    {
        if (immediate)
        {
            PlaySetImmediate(defaultMusicSet);
            return;
        }

        TransitionToSet(defaultMusicSet, defaultSetTransitionDuration);
    }

    private void EvaluateZoneRequest(bool immediate = false)
    {
        TempoMusicSet targetSet = defaultMusicSet;
        float transitionDuration = defaultSetTransitionDuration;
        int bestPriority = int.MinValue;

        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            TempoMusicZone zone = activeZones[i];
            if (zone == null)
            {
                activeZones.RemoveAt(i);
                continue;
            }

            if (zone.MusicSet == null)
                continue;

            if (zone.Priority <= bestPriority)
                continue;

            targetSet = zone.MusicSet;
            transitionDuration = zone.TransitionDuration;
            bestPriority = zone.Priority;
        }

        if (immediate)
        {
            PlaySetImmediate(targetSet);
            return;
        }

        TransitionToSet(targetSet, transitionDuration);
    }

    private void PlaySetImmediate(TempoMusicSet musicSet)
    {
        StopAllBanks();

        if (musicSet == null)
            return;

        int bankIndex = 0;
        PrepareBank(bankIndex, musicSet, startImmediately: true);
        activeBankIndex = bankIndex;
        banks[bankIndex].Weight = 1f;
        currentSnapshot = GetTempoSnapshot();
        ApplyBankVolumes();
    }

    private void TransitionToSet(TempoMusicSet musicSet, float transitionDuration)
    {
        if (isSetTransitioning)
            CommitTransitionToDominantBank();

        TempoMusicSet currentSet = GetActiveSet();
        if (currentSet == musicSet)
            return;

        if (activeBankIndex < 0)
        {
            PlaySetImmediate(musicSet);
            return;
        }

        setTransitionDuration = Mathf.Max(0f, transitionDuration);
        setTransitionElapsed = 0f;
        isSetTransitioning = true;

        if (musicSet == null)
        {
            transitionBankIndex = -1;

            if (setTransitionDuration <= Mathf.Epsilon)
                CompleteSetTransition();

            return;
        }

        int nextBankIndex = GetInactiveBankIndex();
        bool startImmediately = setTransitionDuration <= (float)ScheduledStartLeadTime;
        PrepareBank(nextBankIndex, musicSet, startImmediately);
        banks[nextBankIndex].Weight = 0f;
        transitionBankIndex = nextBankIndex;

        if (setTransitionDuration <= Mathf.Epsilon)
            CompleteSetTransition();
    }

    private void UpdateSetTransition()
    {
        if (!isSetTransitioning)
            return;

        setTransitionElapsed += Time.deltaTime;
        float progress = setTransitionDuration <= Mathf.Epsilon
            ? 1f
            : Mathf.Clamp01(setTransitionElapsed / setTransitionDuration);

        if (activeBankIndex >= 0)
            banks[activeBankIndex].Weight = 1f - progress;

        if (transitionBankIndex >= 0)
            banks[transitionBankIndex].Weight = progress;

        if (progress >= 1f)
            CompleteSetTransition();
    }

    private void CompleteSetTransition()
    {
        int outgoingBankIndex = activeBankIndex;
        int incomingBankIndex = transitionBankIndex;

        if (outgoingBankIndex >= 0)
        {
            banks[outgoingBankIndex].Weight = 0f;

            if (outgoingBankIndex != incomingBankIndex)
                StopBank(outgoingBankIndex);
        }

        activeBankIndex = incomingBankIndex;
        transitionBankIndex = -1;
        isSetTransitioning = false;
        setTransitionElapsed = 0f;

        if (activeBankIndex >= 0)
            banks[activeBankIndex].Weight = 1f;
    }

    private void CommitTransitionToDominantBank()
    {
        if (!isSetTransitioning)
            return;

        int dominantBankIndex = activeBankIndex;
        if (transitionBankIndex >= 0 &&
            banks[transitionBankIndex].Weight > (activeBankIndex >= 0 ? banks[activeBankIndex].Weight : 0f))
        {
            dominantBankIndex = transitionBankIndex;
        }

        for (int i = 0; i < banks.Length; i++)
        {
            if (banks[i] == null)
                continue;

            if (i == dominantBankIndex)
            {
                banks[i].Weight = dominantBankIndex >= 0 ? 1f : 0f;
                continue;
            }

            banks[i].Weight = 0f;
            StopBank(i);
        }

        activeBankIndex = dominantBankIndex;
        transitionBankIndex = -1;
        isSetTransitioning = false;
        setTransitionElapsed = 0f;
    }

    private void ApplyBankVolumes()
    {
        ResolveTempoLayerWeights(currentSnapshot, out float slowWeight, out float midWeight, out float fastWeight, out float intenseWeight);

        for (int i = 0; i < banks.Length; i++)
        {
            MusicBank bank = banks[i];
            if (bank == null)
                continue;

            TempoMusicSet musicSet = bank.MusicSet;
            float bankWeight = Mathf.Clamp01(bank.Weight) * masterVolume * (1f - radioCrossfadeBlend);

            if (musicSet == null || bankWeight <= Mathf.Epsilon)
            {
                SetSourceVolume(bank.MainSource, 0f);
                SetSourceVolume(bank.SlowSource, 0f);
                SetSourceVolume(bank.MidSource, 0f);
                SetSourceVolume(bank.FastSource, 0f);
                SetSourceVolume(bank.IntenseSource, 0f);
                continue;
            }

            SetSourceVolume(bank.MainSource, musicSet.MainVolume * bankWeight);
            SetSourceVolume(bank.SlowSource, musicSet.GetTempoVolume(TempoBand.Slow) * slowWeight * bankWeight);
            SetSourceVolume(bank.MidSource, musicSet.GetTempoVolume(TempoBand.Mid) * midWeight * bankWeight);
            SetSourceVolume(bank.FastSource, musicSet.GetTempoVolume(TempoBand.Fast) * fastWeight * bankWeight);
            SetSourceVolume(bank.IntenseSource, musicSet.GetTempoVolume(TempoBand.Intense) * intenseWeight * bankWeight);
        }
    }

    private void ResolveTempoLayerWeights(
        TempoStateSnapshot snapshot,
        out float slowWeight,
        out float midWeight,
        out float fastWeight,
        out float intenseWeight)
    {
        slowWeight = 0f;
        midWeight = 0f;
        fastWeight = 0f;
        intenseWeight = 0f;

        if (!snapshot.IsChanneling || snapshot.TargetTempo == snapshot.CurrentTempo)
        {
            SetTempoWeight(snapshot.CurrentTempo, 1f, ref slowWeight, ref midWeight, ref fastWeight, ref intenseWeight);
            return;
        }

        float progress = Mathf.Clamp01(snapshot.ChannelProgress);
        SetTempoWeight(snapshot.CurrentTempo, 1f - progress, ref slowWeight, ref midWeight, ref fastWeight, ref intenseWeight);
        SetTempoWeight(snapshot.TargetTempo, progress, ref slowWeight, ref midWeight, ref fastWeight, ref intenseWeight);
    }

    private static void SetTempoWeight(
        TempoBand tempoBand,
        float weight,
        ref float slowWeight,
        ref float midWeight,
        ref float fastWeight,
        ref float intenseWeight)
    {
        switch (tempoBand)
        {
            case TempoBand.Slow:
                slowWeight = weight;
                break;

            case TempoBand.Fast:
                fastWeight = weight;
                break;

            case TempoBand.Intense:
                intenseWeight = weight;
                break;

            default:
                midWeight = weight;
                break;
        }
    }

    private void PrepareBank(int bankIndex, TempoMusicSet musicSet, bool startImmediately)
    {
        EnsureBanks();
        MusicBank bank = banks[bankIndex];
        StopBank(bankIndex);
        bank.MusicSet = musicSet;
        bank.Weight = 0f;

        ConfigureSource(bank.MainSource, musicSet != null ? musicSet.MainLoop : null);
        ConfigureSource(bank.SlowSource, musicSet != null ? musicSet.GetTempoLayer(TempoBand.Slow) : null);
        ConfigureSource(bank.MidSource, musicSet != null ? musicSet.GetTempoLayer(TempoBand.Mid) : null);
        ConfigureSource(bank.FastSource, musicSet != null ? musicSet.GetTempoLayer(TempoBand.Fast) : null);
        ConfigureSource(bank.IntenseSource, musicSet != null ? musicSet.GetTempoLayer(TempoBand.Intense) : null);

        if (musicSet == null)
            return;

        double startTime = AudioSettings.dspTime + ScheduledStartLeadTime;

        StartSource(bank.MainSource, startImmediately, startTime);
        StartSource(bank.SlowSource, startImmediately, startTime);
        StartSource(bank.MidSource, startImmediately, startTime);
        StartSource(bank.FastSource, startImmediately, startTime);
        StartSource(bank.IntenseSource, startImmediately, startTime);
    }

    private void StartSource(AudioSource source, bool startImmediately, double scheduledStartTime)
    {
        if (source == null || source.clip == null)
            return;

        if (startImmediately)
            source.Play();
        else
            source.PlayScheduled(scheduledStartTime);
    }

    private void ConfigureSource(AudioSource source, AudioClip clip)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = clip;
        source.loop = loopSources;
        source.playOnAwake = false;
        source.ignoreListenerPause = ignoreListenerPause;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = musicMixerGroup;
    }

    private void StopAllBanks()
    {
        for (int i = 0; i < banks.Length; i++)
            StopBank(i);

        activeBankIndex = -1;
        transitionBankIndex = -1;
        isSetTransitioning = false;
        setTransitionElapsed = 0f;
    }

    private void StopBank(int bankIndex)
    {
        if (bankIndex < 0 || bankIndex >= banks.Length || banks[bankIndex] == null)
            return;

        MusicBank bank = banks[bankIndex];
        StopSource(bank.MainSource);
        StopSource(bank.SlowSource);
        StopSource(bank.MidSource);
        StopSource(bank.FastSource);
        StopSource(bank.IntenseSource);
        bank.MusicSet = null;
        bank.Weight = 0f;
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    private void EnsureBanks()
    {
        for (int i = 0; i < banks.Length; i++)
        {
            if (banks[i] != null)
                continue;

            string bankName = i == 0 ? $"{BankRootPrefix}_A" : $"{BankRootPrefix}_B";
            Transform bankRoot = transform.Find(bankName);
            if (bankRoot == null)
            {
                GameObject bankObject = new GameObject(bankName);
                bankRoot = bankObject.transform;
                bankRoot.SetParent(transform, false);
            }

            MusicBank bank = new MusicBank
            {
                MainSource = GetOrCreateSource(bankRoot, SourceSlot.Main),
                SlowSource = GetOrCreateSource(bankRoot, SourceSlot.Slow),
                MidSource = GetOrCreateSource(bankRoot, SourceSlot.Mid),
                FastSource = GetOrCreateSource(bankRoot, SourceSlot.Fast),
                IntenseSource = GetOrCreateSource(bankRoot, SourceSlot.Intense)
            };

            banks[i] = bank;
        }
    }

    private AudioSource GetOrCreateSource(Transform bankRoot, SourceSlot slot)
    {
        string childName = slot.ToString();
        Transform child = bankRoot.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(bankRoot, false);
        }

        if (!child.TryGetComponent(out AudioSource source))
            source = child.gameObject.AddComponent<AudioSource>();

        ConfigureSource(source, source.clip);
        return source;
    }

    private void TryBindTempoService()
    {
        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (tempoService == subscribedTempoService)
            return;

        UnbindTempoService();
        subscribedTempoService = tempoService;

        if (subscribedTempoService == null)
            return;

        subscribedTempoService.TempoUpdated += HandleTempoUpdated;
    }

    private void UnbindTempoService()
    {
        if (subscribedTempoService == null)
            return;

        subscribedTempoService.TempoUpdated -= HandleTempoUpdated;
        subscribedTempoService = null;
    }

    private void HandleTempoUpdated(TempoStateSnapshot snapshot)
    {
        currentSnapshot = snapshot;
    }

    private TempoStateSnapshot GetTempoSnapshot()
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

    private TempoMusicSet GetActiveSet()
    {
        return activeBankIndex >= 0 && activeBankIndex < banks.Length && banks[activeBankIndex] != null
            ? banks[activeBankIndex].MusicSet
            : null;
    }

    private int GetInactiveBankIndex()
    {
        if (activeBankIndex <= 0)
            return 1;

        return 0;
    }

    private float ResolveTransitionDuration(float transitionDuration)
    {
        return transitionDuration >= 0f ? transitionDuration : defaultSetTransitionDuration;
    }

    private void UpdateRadioCrossfadeBlend()
    {
        if (playerTransform == null)
        {
            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
                playerTransform = playerController.transform;
        }

        float targetBlend = playerTransform != null ? RadioController.GetStrongestBlendAt(playerTransform.position) : 0f;
        radioCrossfadeBlend = Mathf.MoveTowards(radioCrossfadeBlend, targetBlend, Mathf.Max(0f, radioCrossfadeSpeed) * Time.deltaTime);
    }

    private static void SetSourceVolume(AudioSource source, float volume)
    {
        if (source == null)
            return;

        source.volume = Mathf.Clamp01(volume);
    }
}
