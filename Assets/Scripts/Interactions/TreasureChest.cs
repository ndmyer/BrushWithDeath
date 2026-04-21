using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class TreasureChest : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer closedRenderer;
    [SerializeField] private SpriteRenderer openBodyRenderer;
    [SerializeField] private SpriteRenderer openLidRenderer;
    [SerializeField] private Sprite[] openingFrames;
    [SerializeField] private Sprite finalOpenFrame;
    [SerializeField, Min(1)] private int openBodyHeightPixels = 15;
    [SerializeField, Min(0.01f)] private float frameDuration = 0.08f;

    [Header("Key Reveal")]
    [SerializeField] private GameObject keyPickupPrefab;
    [SerializeField] private Transform keySpawnPoint;
    [SerializeField] private EventDialogueTrigger keyCollectedDialogueTrigger;

    [Header("Audio")]
    [SerializeField] private AudioClip openSound;

    private bool isOpen;
    private bool isOpening;
    private Sprite generatedOpenBodySprite;
    private Sprite generatedOpenLidSprite;

    public bool IsOpen => isOpen;

    private void Reset()
    {
        CacheReferences();
        EnsureColliderConfiguration();
    }

    private void OnValidate()
    {
        CacheReferences();
        EnsureColliderConfiguration();
    }

    private void Awake()
    {
        CacheReferences();
        EnsureColliderConfiguration();
        ApplyClosedVisualState();
    }

    private void OnDestroy()
    {
        if (generatedOpenBodySprite != null)
            Destroy(generatedOpenBodySprite);

        if (generatedOpenLidSprite != null)
            Destroy(generatedOpenLidSprite);
    }

    public void Open()
    {
        if (isOpen || isOpening)
            return;

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        isOpening = true;
        PlayOpenSound();

        bool playedAnimation = false;
        if (closedRenderer != null && openingFrames != null && openingFrames.Length > 0)
        {
            for (int i = 0; i < openingFrames.Length; i++)
            {
                Sprite frame = openingFrames[i];
                if (frame == null)
                    continue;

                closedRenderer.sprite = frame;
                playedAnimation = true;

                if (i < openingFrames.Length - 1)
                    yield return new WaitForSeconds(frameDuration);
            }
        }

        if (playedAnimation)
            yield return new WaitForSeconds(frameDuration);

        ShowOpenedChest();

        if (openLidRenderer != null && openLidRenderer.enabled)
        {
            yield return null;
            openLidRenderer.enabled = false;
        }

        RevealKeyPickup();

        isOpen = true;
        isOpening = false;
    }

    private void ApplyClosedVisualState()
    {
        if (closedRenderer != null)
        {
            closedRenderer.enabled = true;

            Sprite closedSprite = GetFirstValidOpeningFrame();
            if (closedSprite != null)
                closedRenderer.sprite = closedSprite;
        }

        if (openBodyRenderer != null)
            openBodyRenderer.enabled = false;

        if (openLidRenderer != null)
            openLidRenderer.enabled = false;
    }

    private void ShowOpenedChest()
    {
        Sprite resolvedFinalFrame = ResolveFinalOpenFrame();
        if (resolvedFinalFrame == null)
        {
            if (closedRenderer != null)
                closedRenderer.enabled = true;

            return;
        }

        BuildOpenSprites(resolvedFinalFrame);

        bool isShowingSplitOpenState = false;

        if (openBodyRenderer != null && generatedOpenBodySprite != null)
        {
            openBodyRenderer.sprite = generatedOpenBodySprite;
            openBodyRenderer.enabled = true;
            isShowingSplitOpenState = true;
        }
        else if (openBodyRenderer != null)
        {
            openBodyRenderer.enabled = false;
        }

        if (openLidRenderer != null && generatedOpenLidSprite != null)
        {
            openLidRenderer.sprite = generatedOpenLidSprite;
            openLidRenderer.enabled = true;
        }
        else if (openLidRenderer != null)
        {
            openLidRenderer.enabled = false;
        }

        if (closedRenderer != null)
        {
            closedRenderer.sprite = resolvedFinalFrame;
            closedRenderer.enabled = !isShowingSplitOpenState;
        }
    }

    private void RevealKeyPickup()
    {
        if (keyPickupPrefab == null || keySpawnPoint == null)
            return;

        GameObject keyInstance = Instantiate(keyPickupPrefab, keySpawnPoint.position, Quaternion.identity);

        if (keyCollectedDialogueTrigger == null)
            return;

        KeyPickup keyPickup = keyInstance.GetComponent<KeyPickup>();
        if (keyPickup == null)
            keyPickup = keyInstance.GetComponentInChildren<KeyPickup>();

        keyPickup?.AddOnCollectedListener(keyCollectedDialogueTrigger.TriggerDialogue);
    }

    private void BuildOpenSprites(Sprite resolvedFinalFrame)
    {
        if (resolvedFinalFrame == null)
            return;

        if (generatedOpenBodySprite != null)
            Destroy(generatedOpenBodySprite);

        if (generatedOpenLidSprite != null)
            Destroy(generatedOpenLidSprite);

        Rect sourceRect = resolvedFinalFrame.rect;
        if (sourceRect.width <= 0f || sourceRect.height <= 1f)
            return;

        float bodyHeight = Mathf.Clamp(openBodyHeightPixels, 1, Mathf.RoundToInt(sourceRect.height) - 1);
        float lidHeight = sourceRect.height - bodyHeight;
        Vector2 sourcePivot = resolvedFinalFrame.pivot;

        Rect bodyRect = new(sourceRect.x, sourceRect.y, sourceRect.width, bodyHeight);
        Vector2 bodyPivotPixels = new(sourcePivot.x, Mathf.Clamp(sourcePivot.y, 0.01f, bodyRect.height - 0.01f));
        generatedOpenBodySprite = CreateSpriteSlice(resolvedFinalFrame, bodyRect, bodyPivotPixels);

        Rect lidRect = new(sourceRect.x, sourceRect.y + bodyHeight, sourceRect.width, lidHeight);
        Vector2 lidPivotPixels = new(sourcePivot.x, Mathf.Clamp(sourcePivot.y - bodyHeight, 0.01f, lidRect.height - 0.01f));
        generatedOpenLidSprite = CreateSpriteSlice(resolvedFinalFrame, lidRect, lidPivotPixels);
    }

    private static Sprite CreateSpriteSlice(Sprite sourceSprite, Rect sourceRect, Vector2 pivotPixels)
    {
        if (sourceSprite == null || sourceRect.width <= 0f || sourceRect.height <= 0f)
            return null;

        Vector2 normalizedPivot = new(
            Mathf.Clamp01(pivotPixels.x / sourceRect.width),
            Mathf.Clamp01(pivotPixels.y / sourceRect.height));

        return Sprite.Create(
            sourceSprite.texture,
            sourceRect,
            normalizedPivot,
            sourceSprite.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            Vector4.zero,
            false);
    }

    private Sprite GetFirstValidOpeningFrame()
    {
        if (openingFrames == null)
            return null;

        for (int i = 0; i < openingFrames.Length; i++)
        {
            if (openingFrames[i] != null)
                return openingFrames[i];
        }

        return null;
    }

    private Sprite ResolveFinalOpenFrame()
    {
        if (finalOpenFrame != null)
            return finalOpenFrame;

        if (openingFrames == null)
            return null;

        for (int i = openingFrames.Length - 1; i >= 0; i--)
        {
            if (openingFrames[i] != null)
                return openingFrames[i];
        }

        return null;
    }

    private void CacheReferences()
    {
        if (closedRenderer == null)
            closedRenderer = GetComponent<SpriteRenderer>();

        if (openBodyRenderer == null)
        {
            Transform bodyTransform = transform.Find("Chest_OpenBody");
            if (bodyTransform != null)
                openBodyRenderer = bodyTransform.GetComponent<SpriteRenderer>();
        }

        if (openLidRenderer == null)
        {
            Transform lidTransform = transform.Find("Chest_OpenLid");
            if (lidTransform != null)
                openLidRenderer = lidTransform.GetComponent<SpriteRenderer>();
        }

        if (keySpawnPoint == null)
        {
            Transform spawnTransform = transform.Find("KeySpawnPoint");
            if (spawnTransform != null)
                keySpawnPoint = spawnTransform;
        }
    }

    private void EnsureColliderConfiguration()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = false;
    }

    private void PlayOpenSound()
    {
        if (openSound == null)
            return;

        AudioSource.PlayClipAtPoint(openSound, transform.position);
    }
}
