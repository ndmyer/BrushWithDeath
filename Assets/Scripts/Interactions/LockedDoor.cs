using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class LockedDoor : MonoBehaviour
{
    [SerializeField] private bool startsOpen;
    [SerializeField] private GameSfxCue openSfxCue = GameSfxCue.LockedDoorOpened;

    [Header("References")]
    [SerializeField] private Collider2D[] blockingColliders;
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;

    public bool IsOpen { get; private set; }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        SetOpenState(startsOpen);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsOpen || collision == null)
            return;

        if (!TryGetPlayerProgression(collision, out PlayerProgression progression))
            return;

        if (!progression.ConsumeKey())
            return;

        Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        SetOpenState(true);
        GameSfx.Play(this, openSfxCue);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        SetOpenState(false);
    }

    private void SetOpenState(bool isOpen)
    {
        IsOpen = isOpen;

        if (blockingColliders != null)
        {
            foreach (Collider2D collider2D in blockingColliders)
            {
                if (collider2D != null)
                    collider2D.enabled = !isOpen;
            }
        }

        if (closedVisual != null)
            closedVisual.SetActive(!isOpen);

        if (openVisual != null)
            openVisual.SetActive(isOpen);
    }

    private void CacheReferences()
    {
        if (blockingColliders == null || blockingColliders.Length == 0)
            blockingColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);

        if (closedVisual == null)
        {
            Transform closedTransform = transform.Find("Door_Closed_Sprite");
            if (closedTransform != null)
                closedVisual = closedTransform.gameObject;
        }

        if (openVisual == null)
        {
            Transform openTransform = transform.Find("Door_Open_Sprite");
            if (openTransform != null)
                openVisual = openTransform.gameObject;
        }
    }

    private static bool TryGetPlayerProgression(Collision2D collision, out PlayerProgression progression)
    {
        progression = collision.collider.GetComponent<PlayerProgression>();
        if (progression != null)
            return true;

        progression = collision.collider.GetComponentInParent<PlayerProgression>();
        return progression != null;
    }
}
