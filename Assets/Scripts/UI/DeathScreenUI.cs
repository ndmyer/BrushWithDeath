using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DeathScreenUI : MonoBehaviour
{
    private static DeathScreenUI instance;

    [SerializeField] private RectTransform deathScreenRoot;
    [SerializeField] private CanvasGroup deathScreenCanvasGroup;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;

    public static DeathScreenUI Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<DeathScreenUI>();

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CacheReferences();
        SetAlpha(0f);
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public IEnumerator FadeToBlack()
    {
        CacheReferences();
        yield return FadeToAlpha(1f, fadeOutDuration);
    }

    public IEnumerator FadeToBlack(float duration)
    {
        CacheReferences();
        yield return FadeToAlpha(1f, duration);
    }

    public IEnumerator FadeFromBlack()
    {
        CacheReferences();
        yield return FadeToAlpha(0f, fadeInDuration);
    }

    private void CacheReferences()
    {
        if (deathScreenRoot == null)
            deathScreenRoot = transform as RectTransform;

        if (deathScreenRoot != null && deathScreenCanvasGroup == null)
        {
            deathScreenCanvasGroup = deathScreenRoot.GetComponent<CanvasGroup>();
            if (deathScreenCanvasGroup == null)
                deathScreenCanvasGroup = deathScreenRoot.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private IEnumerator FadeToAlpha(float targetAlpha, float duration)
    {
        if (deathScreenCanvasGroup == null)
            yield break;

        float startAlpha = deathScreenCanvasGroup.alpha;
        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= Mathf.Epsilon)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        deathScreenCanvasGroup.interactable = true;
        deathScreenCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (deathScreenCanvasGroup == null)
            return;

        deathScreenCanvasGroup.alpha = Mathf.Clamp01(alpha);
        bool isVisible = deathScreenCanvasGroup.alpha > 0.001f;
        deathScreenCanvasGroup.interactable = isVisible;
        deathScreenCanvasGroup.blocksRaycasts = isVisible;
    }
}
