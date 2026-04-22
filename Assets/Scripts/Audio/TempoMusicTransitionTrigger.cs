using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public class TempoMusicTransitionTrigger : MonoBehaviour
{
    [Header("Music Sets")]
    [SerializeField] private TempoMusicSet set1;
    [SerializeField] private TempoMusicSet set2;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float transitionDuration = 1f;

    private bool playSet1Next = true;

    private void Awake()
    {
        EnsureTriggerCollider();
        PreloadSet(set1);
        PreloadSet(set2);
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayer(other))
            return;

        TempoMusicDirector director = TempoMusicDirector.Instance;
        if (director == null)
            return;

        TempoMusicSet nextSet = playSet1Next ? set1 : set2;
        director.PlaySet(nextSet, transitionDuration);
        playSet1Next = !playSet1Next;
    }

    private void EnsureTriggerCollider()
    {
        PolygonCollider2D collider2D = GetComponent<PolygonCollider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private static void PreloadSet(TempoMusicSet musicSet)
    {
        if (musicSet == null)
            return;

        PreloadClip(musicSet.MainLoop);
        PreloadClip(musicSet.GetTempoLayer(TempoBand.Slow));
        PreloadClip(musicSet.GetTempoLayer(TempoBand.Mid));
        PreloadClip(musicSet.GetTempoLayer(TempoBand.Fast));
        PreloadClip(musicSet.GetTempoLayer(TempoBand.Intense));
    }

    private static void PreloadClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    private static bool TryGetPlayer(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            player = other.GetComponentInParent<PlayerController>();

        return player != null;
    }
}
