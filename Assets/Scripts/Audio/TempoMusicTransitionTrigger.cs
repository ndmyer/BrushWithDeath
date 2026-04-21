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

    private static bool TryGetPlayer(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            player = other.GetComponentInParent<PlayerController>();

        return player != null;
    }
}
