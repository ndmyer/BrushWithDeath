using UnityEngine;
using UnityEngine.Events;

public class LightableTorch : MonoBehaviour, ILightable
{
    private const int LitLoopStartFrameIndex = 4;
    private const int LitLoopEndFrameIndex = 6;

    public enum TorchType
    {
        Standard,
        Marigold,
        Flamethrower
    }

    [SerializeField] private TorchType torchType = TorchType.Standard;
    [SerializeField] private PuzzleStateBool stateSource;
    [SerializeField] private PuzzleStateVisuals stateVisuals;
    [SerializeField] private PuzzleEventEmitter eventEmitter;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private SpriteRenderer animatedVisualRenderer;
    [SerializeField] private SpriteRenderer possessedVisualRenderer;
    [SerializeField] private SpriteRenderer selectorRenderer;
    [SerializeField] private bool startsLit;
    [SerializeField] private bool toggleOnLight = true;
    [SerializeField] private Color unlitColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color litColor = new Color(1f, 0.6f, 0.15f, 1f);
    [SerializeField] private Sprite[] litAnimationFrames;
    [SerializeField, Min(0f)] private float litAnimationFramesPerSecond = 10f;
    [SerializeField] private Sprite possessedSpriteFallback;
    [SerializeField] private Sprite[] possessedAnimationFrames;
    [SerializeField, Min(0f)] private float possessedAnimationFramesPerSecond = 10f;
    [SerializeField] private Color selectorColor = new Color(1f, 0.42f, 0.08f, 0.85f);
    [SerializeField, Min(0f)] private float selectorPulseSpeed = 3f;
    [SerializeField, Min(0f)] private float selectorMinScaleMultiplier = 0.9f;
    [SerializeField, Min(0f)] private float selectorMaxScaleMultiplier = 1.15f;
    [SerializeField, Range(0f, 1f)] private float selectorMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float selectorMaxAlpha = 0.85f;
    [SerializeField, Min(0f)] private float selectorInnerMaskScaleMultiplier = 0.58f;
    [SerializeField] private PistaController pistaController;
    [SerializeField] private UnityEvent onLit;
    [SerializeField] private UnityEvent onExtinguished;

    public TorchType Type => torchType;
    public bool IsLit => stateSource != null ? stateSource.Value : startsLit;

    private Sprite defaultAnimatedVisualSprite;
    private Sprite defaultPossessedVisualSprite;
    private Vector3 defaultSelectorLocalScale;
    private SpriteMask selectorMask;
    private bool isPossessedByPista;
    private bool isPlayingLitAnimation;
    private float animatedVisualElapsedTime;

    private void Awake()
    {
        if (stateSource == null)
            stateSource = GetComponent<PuzzleStateBool>();

        if (stateVisuals == null)
            stateVisuals = GetComponent<PuzzleStateVisuals>();

        if (eventEmitter == null)
            eventEmitter = GetComponent<PuzzleEventEmitter>();

        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (animatedVisualRenderer == null)
            animatedVisualRenderer = FindAnimatedVisualRenderer();

        if (possessedVisualRenderer == null)
            possessedVisualRenderer = FindPossessedVisualRenderer();

        if (selectorRenderer == null)
            selectorRenderer = FindSelectorRenderer();

        if (animatedVisualRenderer != null)
            defaultAnimatedVisualSprite = animatedVisualRenderer.sprite;

        if (possessedVisualRenderer != null)
            defaultPossessedVisualSprite = possessedVisualRenderer.sprite;

        if (selectorRenderer != null)
        {
            defaultSelectorLocalScale = selectorRenderer.transform.localScale;
            EnsureSelectorMask();
            selectorRenderer.enabled = false;
        }
    }

    private void Start()
    {
        if (pistaController == null)
            pistaController = FindAnyObjectByType<PistaController>();

        if (stateSource != null)
        {
            stateSource.SetState(startsLit);
            ApplyVisuals(stateSource.Value, false);
            return;
        }

        ApplyVisuals(startsLit, false);
    }

    private void Update()
    {
        UpdatePossessionState();
        UpdateAnimatedVisual();
        UpdateSelectorVisual();
    }

    public void Light(PlayerController player)
    {
        bool nextState = toggleOnLight ? !IsLit : true;
        ApplyLitState(nextState, true);
        Debug.Log($"{name} toggled {(IsLit ? "lit" : "unlit")} by lantern from {player.name}.", this);
    }

    public void SetLit(bool isLit)
    {
        ApplyLitState(isLit, false);
    }

    private void ApplyLitState(bool isLit, bool emitEvents)
    {
        bool previousState = IsLit;

        if (stateSource != null)
            stateSource.SetState(isLit);
        else
            startsLit = isLit;

        ApplyVisuals(isLit, isLit && !previousState);

        if (eventEmitter != null)
            eventEmitter.EmitSetState(isLit);

        if (isLit && !previousState)
            GameSfx.Play(this, GameSfxCue.TorchLit, pitchVariance: 0.02f);

        if (!emitEvents || previousState == isLit)
            return;

        if (isLit)
            onLit?.Invoke();
        else
            onExtinguished?.Invoke();
    }

    private void ApplyVisuals(bool isLit, bool playLitAnimation)
    {
        if (targetRenderer != null)
            targetRenderer.color = isLit ? litColor : unlitColor;

        stateVisuals?.Apply(isLit);

        if (isLit)
            BeginLitAnimationPlayback(playLitAnimation);
        else
            StopLitAnimationPlayback();

        UpdateVisualRendererVisibility();
        ApplyAnimatedVisualImmediate();
    }

    private void UpdatePossessionState()
    {
        bool nextPossessionState = IsPistaPossessingThisTorch();

        if (nextPossessionState == isPossessedByPista)
            return;

        isPossessedByPista = nextPossessionState;

        if (isPossessedByPista)
            StopLitAnimationPlayback();
        else if (IsLit)
            BeginLitAnimationPlayback(false);

        UpdateVisualRendererVisibility();
        ApplyAnimatedVisualImmediate();
    }

    private bool IsPistaPossessingThisTorch()
    {
        if (pistaController == null)
            return false;

        if (pistaController.CurrentState == PistaController.PistaState.FollowingPlayer ||
            pistaController.CurrentState == PistaController.PistaState.Traveling)
            return false;

        Transform currentLanternTarget = pistaController.CurrentLanternTarget;
        return MatchesTorchTarget(currentLanternTarget);
    }

    private void UpdateAnimatedVisual()
    {
        if (!IsLit)
            return;

        if (ShouldRenderPossessedVisuals())
        {
            UpdatePossessedVisual();
            return;
        }

        if (animatedVisualRenderer == null)
            return;

        if (!isPlayingLitAnimation)
            BeginLitAnimationPlayback(false);

        if (!HasSprites(litAnimationFrames))
        {
            StopLitAnimationPlayback();
            SetAnimatedVisualToDefaultLitSprite();
            return;
        }

        if (litAnimationFramesPerSecond <= 0f)
        {
            StopLitAnimationPlayback();
            SetAnimatedVisualToDefaultLitSprite();
            return;
        }

        animatedVisualElapsedTime += Time.deltaTime;
        int litFrameIndex = GetLitFrameIndexForElapsedTime(animatedVisualElapsedTime);

        Sprite litFrame = litAnimationFrames[litFrameIndex];
        if (litFrame != null)
        {
            animatedVisualRenderer.sprite = litFrame;
            return;
        }

        SetAnimatedVisualToDefaultLitSprite();
    }

    private void UpdateSelectorVisual()
    {
        if (selectorRenderer == null)
            return;

        bool shouldShowSelector = IsLit && IsSelectedByPista();
        selectorRenderer.enabled = shouldShowSelector;

        if (!shouldShowSelector)
        {
            selectorRenderer.transform.localScale = defaultSelectorLocalScale;
            selectorRenderer.color = ApplySelectorAlpha(selectorColor, selectorMinAlpha);
            return;
        }

        float pulse = selectorPulseSpeed <= 0f
            ? 1f
            : 0.5f + 0.5f * Mathf.Sin(Time.time * selectorPulseSpeed * Mathf.PI * 2f);

        float scaleMultiplier = Mathf.Lerp(selectorMinScaleMultiplier, selectorMaxScaleMultiplier, pulse);
        float alpha = Mathf.Lerp(selectorMinAlpha, selectorMaxAlpha, pulse);

        selectorRenderer.transform.localScale = defaultSelectorLocalScale * scaleMultiplier;
        selectorRenderer.color = ApplySelectorAlpha(selectorColor, alpha);
    }

    private void ApplyAnimatedVisualImmediate()
    {
        if (!IsLit)
        {
            if (animatedVisualRenderer != null && defaultAnimatedVisualSprite != null)
                animatedVisualRenderer.sprite = defaultAnimatedVisualSprite;

            if (possessedVisualRenderer != null)
                possessedVisualRenderer.sprite = defaultPossessedVisualSprite;

            return;
        }

        if (ShouldRenderPossessedVisuals())
        {
            ApplyPossessedVisualImmediate();
            return;
        }

        if (animatedVisualRenderer == null)
            return;

        if (isPlayingLitAnimation)
        {
            Sprite litFrame = GetCurrentLitAnimationSprite();
            if (litFrame != null)
                animatedVisualRenderer.sprite = litFrame;
            else
                SetAnimatedVisualToDefaultLitSprite();
            return;
        }

        SetAnimatedVisualToDefaultLitSprite();
    }

    private void UpdateVisualRendererVisibility()
    {
        bool showPossessedVisuals = IsLit && ShouldRenderPossessedVisuals();
        bool showLitVisuals = IsLit && !showPossessedVisuals;

        if (animatedVisualRenderer != null && animatedVisualRenderer != targetRenderer)
            animatedVisualRenderer.gameObject.SetActive(showLitVisuals);

        if (possessedVisualRenderer != null)
            possessedVisualRenderer.gameObject.SetActive(showPossessedVisuals);
    }

    private void BeginLitAnimationPlayback(bool playFromStart)
    {
        float framesPerSecond = litAnimationFramesPerSecond;
        int startFrameIndex = playFromStart ? 0 : GetLitLoopStartIndex();
        animatedVisualElapsedTime = framesPerSecond > 0f
            ? startFrameIndex / framesPerSecond
            : 0f;
        isPlayingLitAnimation = true;
    }

    private SpriteRenderer FindAnimatedVisualRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null && renderer != targetRenderer)
                return renderer;
        }

        return null;
    }

    private SpriteRenderer FindPossessedVisualRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer == targetRenderer || renderer == animatedVisualRenderer)
                continue;

            if (renderer.name == "TorchPossessedVisual")
                return renderer;
        }

        return null;
    }

    private SpriteRenderer FindSelectorRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null && renderer != targetRenderer && renderer != animatedVisualRenderer && renderer != possessedVisualRenderer)
                return renderer;
        }

        return null;
    }

    private void EnsureSelectorMask()
    {
        if (selectorRenderer == null || selectorRenderer.sprite == null)
            return;

        selectorRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

        if (selectorMask == null)
            selectorMask = selectorRenderer.GetComponentInChildren<SpriteMask>(true);

        if (selectorMask == null)
        {
            GameObject maskObject = new GameObject("SelectorCutoutMask");
            maskObject.transform.SetParent(selectorRenderer.transform, false);
            selectorMask = maskObject.AddComponent<SpriteMask>();
        }

        selectorMask.sprite = selectorRenderer.sprite;
        selectorMask.alphaCutoff = 0.1f;
        selectorMask.transform.localPosition = Vector3.zero;
        selectorMask.transform.localRotation = Quaternion.identity;
        selectorMask.transform.localScale = Vector3.one * selectorInnerMaskScaleMultiplier;
    }

    private void StopLitAnimationPlayback()
    {
        animatedVisualElapsedTime = 0f;
        isPlayingLitAnimation = false;
    }

    private int GetLitFrameIndexForElapsedTime(float elapsedTime)
    {
        if (litAnimationFrames == null || litAnimationFrames.Length == 0)
            return -1;

        int rawFrameIndex = Mathf.Max(0, Mathf.FloorToInt(elapsedTime * litAnimationFramesPerSecond));
        int loopStartIndex = GetLitLoopStartIndex();
        int loopEndIndex = GetLitLoopEndIndex(loopStartIndex);

        if (rawFrameIndex <= loopEndIndex)
            return rawFrameIndex;

        int loopLength = loopEndIndex - loopStartIndex + 1;
        if (loopLength <= 0)
            return loopEndIndex;

        return loopStartIndex + ((rawFrameIndex - loopStartIndex) % loopLength);
    }

    private int GetLitLoopStartIndex()
    {
        return litAnimationFrames == null || litAnimationFrames.Length == 0
            ? 0
            : Mathf.Clamp(LitLoopStartFrameIndex, 0, litAnimationFrames.Length - 1);
    }

    private int GetLitLoopEndIndex(int loopStartIndex)
    {
        return litAnimationFrames == null || litAnimationFrames.Length == 0
            ? loopStartIndex
            : Mathf.Clamp(LitLoopEndFrameIndex, loopStartIndex, litAnimationFrames.Length - 1);
    }

    private Sprite GetCurrentLitAnimationSprite()
    {
        int litFrameIndex = GetLitFrameIndexForElapsedTime(animatedVisualElapsedTime);
        if (litFrameIndex < 0 || litAnimationFrames == null || litFrameIndex >= litAnimationFrames.Length)
            return null;

        return litAnimationFrames[litFrameIndex];
    }

    private void SetAnimatedVisualToDefaultLitSprite()
    {
        if (animatedVisualRenderer != null && defaultAnimatedVisualSprite != null)
            animatedVisualRenderer.sprite = defaultAnimatedVisualSprite;
    }

    private Sprite GetPossessedFallbackSprite()
    {
        if (ShouldUsePossessedVisuals() && possessedSpriteFallback != null)
            return possessedSpriteFallback;

        if (ShouldUsePossessedVisuals() && defaultPossessedVisualSprite != null)
            return defaultPossessedVisualSprite;

        return null;
    }

    private bool ShouldUsePossessedVisuals()
    {
        return isPossessedByPista && (HasSprites(possessedAnimationFrames) || possessedSpriteFallback != null);
    }

    private bool ShouldRenderPossessedVisuals()
    {
        return possessedVisualRenderer != null && ShouldUsePossessedVisuals();
    }

    private void UpdatePossessedVisual()
    {
        if (possessedVisualRenderer == null)
            return;

        Sprite[] possessedFrames = possessedAnimationFrames;
        Sprite fallbackSprite = GetPossessedFallbackSprite();

        if (!HasSprites(possessedFrames))
        {
            if (fallbackSprite != null)
                possessedVisualRenderer.sprite = fallbackSprite;
            return;
        }

        float framesPerSecond = possessedAnimationFramesPerSecond;
        if (framesPerSecond <= 0f)
        {
            possessedVisualRenderer.sprite = GetFirstAvailableSprite(possessedFrames) ?? fallbackSprite;
            return;
        }

        animatedVisualElapsedTime += Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(animatedVisualElapsedTime * framesPerSecond) % possessedFrames.Length;
        possessedVisualRenderer.sprite = possessedFrames[frameIndex] ?? fallbackSprite;
    }

    private void ApplyPossessedVisualImmediate()
    {
        if (possessedVisualRenderer == null)
            return;

        Sprite[] possessedFrames = possessedAnimationFrames;
        Sprite fallbackSprite = GetPossessedFallbackSprite();

        if (HasSprites(possessedFrames))
        {
            possessedVisualRenderer.sprite = GetFirstAvailableSprite(possessedFrames) ?? fallbackSprite;
            return;
        }

        if (fallbackSprite != null)
            possessedVisualRenderer.sprite = fallbackSprite;
    }

    private bool IsSelectedByPista()
    {
        if (pistaController == null)
            return false;

        return MatchesTorchTarget(pistaController.CurrentPreviewTarget);
    }

    private bool MatchesTorchTarget(Transform target)
    {
        if (target == null)
            return false;

        if (target == transform)
            return true;

        LightableTorch targetedTorch = target.GetComponent<LightableTorch>();
        if (targetedTorch == null)
            targetedTorch = target.GetComponentInParent<LightableTorch>();
        if (targetedTorch == null)
            targetedTorch = target.GetComponentInChildren<LightableTorch>();

        return targetedTorch == this;
    }

    private static Color ApplySelectorAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static bool HasSprites(Sprite[] sprites)
    {
        if (sprites == null)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return true;
        }

        return false;
    }

    private static Sprite GetFirstAvailableSprite(Sprite[] sprites)
    {
        if (sprites == null)
            return null;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return sprites[i];
        }

        return null;
    }
}
