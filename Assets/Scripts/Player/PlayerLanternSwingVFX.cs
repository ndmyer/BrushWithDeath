using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerLanternSwingVFX : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] downFrames;
    [SerializeField] private Sprite[] upFrames;
    [SerializeField] private Sprite[] sideFrames;
    [SerializeField] private Vector2 downOffset;
    [SerializeField] private Vector2 upOffset = new(0f, 0.6f);
    [SerializeField] private Vector2 leftOffset = new(-0.6f, 0f);
    [SerializeField] private Vector2 rightOffset = new(0.6f, 0f);
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;

    private Coroutine playbackRoutine;
    private Transform targetTransform;
    private Vector3 baseLocalPosition;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (targetRenderer != null)
        {
            targetTransform = targetRenderer.transform;
            baseLocalPosition = targetTransform.localPosition;
        }

        ClearVisual();
    }

    public void Play(Vector2 facingDirection)
    {
        if (targetRenderer == null)
            return;

        if (playbackRoutine != null)
            StopCoroutine(playbackRoutine);

        playbackRoutine = StartCoroutine(PlayRoutine(DirectionUtility.ToCardinal(facingDirection)));
    }

    private IEnumerator PlayRoutine(Vector2 facingDirection)
    {
        Sprite[] frames = GetFrames(facingDirection, out bool flipX, out Vector2 localOffset);
        if (frames == null || frames.Length == 0)
        {
            ClearVisual();
            playbackRoutine = null;
            yield break;
        }

        targetRenderer.enabled = true;
        targetRenderer.flipX = flipX;

        if (targetTransform != null)
            targetTransform.localPosition = baseLocalPosition + (Vector3)localOffset;

        float secondsPerFrame = 1f / Mathf.Max(1f, framesPerSecond);

        for (int i = 0; i < frames.Length; i++)
        {
            targetRenderer.sprite = frames[i];
            yield return new WaitForSeconds(secondsPerFrame);
        }

        ClearVisual();
        playbackRoutine = null;
    }

    private Sprite[] GetFrames(Vector2 facingDirection, out bool flipX, out Vector2 localOffset)
    {
        flipX = false;
        localOffset = downOffset;

        if (facingDirection.x > 0.5f)
        {
            flipX = true;
            localOffset = rightOffset;
            return sideFrames;
        }

        if (facingDirection.x < -0.5f)
        {
            localOffset = leftOffset;
            return sideFrames;
        }

        if (facingDirection.y > 0.5f)
        {
            localOffset = upOffset;
            return upFrames;
        }

        return downFrames;
    }

    private void ClearVisual()
    {
        if (targetRenderer == null)
            return;

        targetRenderer.sprite = null;
        targetRenderer.flipX = false;
        targetRenderer.enabled = false;

        if (targetTransform != null)
            targetTransform.localPosition = baseLocalPosition;
    }
}
