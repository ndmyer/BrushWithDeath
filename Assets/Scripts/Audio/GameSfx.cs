using UnityEngine;

public static class GameSfx
{
    private const string ResourcePath = "Audio/GameSfxLibrary";

    private static GameSfxLibrary library;
    private static bool hasWarnedMissingLibrary;

    public static void Play(Component host, GameSfxCue cue, float volumeScale = 1f, float pitchVariance = 0f, float volumeVariance = 0f)
    {
        if (host == null)
            return;

        Play(host.gameObject, cue, volumeScale, pitchVariance, volumeVariance);
    }

    public static void Play(GameObject host, GameSfxCue cue, float volumeScale = 1f, float pitchVariance = 0f, float volumeVariance = 0f)
    {
        if (host == null || !TryResolveClip(cue, out AudioClip clip))
            return;

        OneShotSfxPlayer player = OneShotSfxPlayer.GetOrAdd(host);
        player?.Play(clip, volumeScale, pitchVariance, volumeVariance);
    }

    public static void PlayDetached(GameSfxCue cue, Vector3 position, float volumeScale = 1f, float pitchVariance = 0f, float volumeVariance = 0f)
    {
        if (!TryResolveClip(cue, out AudioClip clip))
            return;

        OneShotSfxPlayer.PlayDetached(clip, position, volumeScale, pitchVariance, volumeVariance);
    }

    private static bool TryResolveClip(GameSfxCue cue, out AudioClip clip)
    {
        clip = null;

        if (library == null)
            library = Resources.Load<GameSfxLibrary>(ResourcePath);

        if (library == null)
        {
            if (!hasWarnedMissingLibrary)
            {
                Debug.LogWarning($"GameSfxLibrary could not be loaded from Resources/{ResourcePath}.");
                hasWarnedMissingLibrary = true;
            }

            return false;
        }

        clip = library.GetRandomClip(cue);
        return clip != null;
    }
}
