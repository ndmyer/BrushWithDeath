using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class OneShotSfxPlayer : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;
    [SerializeField] private bool ignoreListenerPause = true;
    [SerializeField] private float spatialBlend;

    private readonly List<AudioSource> pooledSources = new();

    public void Play(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null)
            return;

        AudioSource source = GetAvailableSource();
        if (source == null)
            return;

        source.clip = clip;
        source.volume = Mathf.Clamp01(baseVolume * Mathf.Max(0f, volumeScale));
        source.pitch = Mathf.Max(0.01f, pitch);
        source.Play();
    }

    public void Play(AudioClip clip, float volumeScale, float pitchVariance, float volumeVariance)
    {
        float resolvedPitch = ResolvePitch(1f, pitchVariance);
        float resolvedVolume = ResolveVolume(volumeScale, volumeVariance);
        Play(clip, resolvedVolume, resolvedPitch);
    }

    public void PlayRandom(AudioClip[] clips, float volumeScale = 1f, Vector2 pitchRange = default)
    {
        AudioClip clip = PickRandomClip(clips);
        if (clip == null)
            return;

        float pitch = ResolvePitch(pitchRange);
        Play(clip, volumeScale, pitch);
    }

    public static void Play(Component owner, AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (owner == null || clip == null)
            return;

        GetOrCreate(owner).Play(clip, volumeScale, pitch);
    }

    public static void PlayRandom(Component owner, AudioClip[] clips, float volumeScale = 1f, Vector2 pitchRange = default)
    {
        if (owner == null)
            return;

        GetOrCreate(owner).PlayRandom(clips, volumeScale, pitchRange);
    }

    public static void PlayDetached(AudioClip clip, Vector3 position, float volumeScale = 1f, float pitch = 1f, AudioMixerGroup mixerGroup = null)
    {
        if (clip == null)
            return;

        GameObject tempObject = new($"OneShotSfx_{clip.name}");
        tempObject.transform.position = position;

        AudioSource source = tempObject.AddComponent<AudioSource>();
        ConfigureSource(source, mixerGroup, ignoreListenerPause: true, spatialBlend: 0f);
        source.clip = clip;
        source.volume = Mathf.Clamp01(Mathf.Max(0f, volumeScale));
        source.pitch = Mathf.Max(0.01f, pitch);
        source.Play();

        float cleanupDelay = clip.length / source.pitch + 0.1f;
        Destroy(tempObject, cleanupDelay);
    }

    public static void PlayDetached(AudioClip clip, Vector3 position, float volumeScale, float pitchVariance, float volumeVariance, AudioMixerGroup mixerGroup = null)
    {
        PlayDetached(clip, position, ResolveVolume(volumeScale, volumeVariance), ResolvePitch(1f, pitchVariance), mixerGroup);
    }

    public static void PlayRandomDetached(AudioClip[] clips, Vector3 position, float volumeScale = 1f, Vector2 pitchRange = default, AudioMixerGroup mixerGroup = null)
    {
        AudioClip clip = PickRandomClip(clips);
        if (clip == null)
            return;

        PlayDetached(clip, position, volumeScale, ResolvePitch(pitchRange), mixerGroup);
    }

    private AudioSource GetAvailableSource()
    {
        for (int i = 0; i < pooledSources.Count; i++)
        {
            AudioSource source = pooledSources[i];
            if (source == null)
                continue;

            if (!source.isPlaying)
                return source;
        }

        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(newSource, outputMixerGroup, ignoreListenerPause, spatialBlend);
        pooledSources.Add(newSource);
        return newSource;
    }

    private static void ConfigureSource(AudioSource source, AudioMixerGroup mixerGroup, bool ignoreListenerPause, float spatialBlend)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.outputAudioMixerGroup = mixerGroup;
        source.ignoreListenerPause = ignoreListenerPause;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.dopplerLevel = 0f;
    }

    public static OneShotSfxPlayer GetOrAdd(GameObject host)
    {
        if (host == null)
            return null;

        OneShotSfxPlayer player = host.GetComponent<OneShotSfxPlayer>();
        if (player == null)
            player = host.AddComponent<OneShotSfxPlayer>();

        return player;
    }

    private static OneShotSfxPlayer GetOrCreate(Component owner)
    {
        return GetOrAdd(owner != null ? owner.gameObject : null);
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validClipCount = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validClipCount++;
        }

        if (validClipCount == 0)
            return null;

        int selectedIndex = Random.Range(0, validClipCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (selectedIndex == 0)
                return clips[i];

            selectedIndex--;
        }

        return null;
    }

    private static float ResolvePitch(Vector2 pitchRange)
    {
        if (pitchRange == default)
            return 1f;

        float minPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        return Random.Range(minPitch, maxPitch);
    }

    private static float ResolvePitch(float basePitch, float pitchVariance)
    {
        float variance = Mathf.Max(0f, pitchVariance);
        return Mathf.Max(0.01f, Random.Range(basePitch - variance, basePitch + variance));
    }

    private static float ResolveVolume(float baseVolumeScale, float volumeVariance)
    {
        float variance = Mathf.Max(0f, volumeVariance);
        return Mathf.Max(0f, Random.Range(baseVolumeScale - variance, baseVolumeScale + variance));
    }
}
