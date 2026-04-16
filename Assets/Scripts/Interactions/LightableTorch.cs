using UnityEngine;
using UnityEngine.Events;

public class LightableTorch : MonoBehaviour, ILightable
{
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
    [SerializeField] private bool startsLit;
    [SerializeField] private bool toggleOnLight = true;
    [SerializeField] private Color unlitColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color litColor = new Color(1f, 0.6f, 0.15f, 1f);
    [SerializeField] private Sprite[] litAnimationFrames;
    [SerializeField, Min(0f)] private float litAnimationFramesPerSecond = 10f;
    [SerializeField] private Sprite possessedSpriteFallback;
    [SerializeField] private Sprite[] possessedAnimationFrames;
    [SerializeField, Min(0f)] private float possessedAnimationFramesPerSecond = 10f;
    [SerializeField] private PistaController pistaController;
    [SerializeField] private UnityEvent onLit;
    [SerializeField] private UnityEvent onExtinguished;

    public TorchType Type => torchType;
    public bool IsLit => stateSource != null ? stateSource.Value : startsLit;

    private Sprite defaultAnimatedVisualSprite;
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

        if (animatedVisualRenderer != null)
            defaultAnimatedVisualSprite = animatedVisualRenderer.sprite;
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
        SetAnimatedVisualVisible(isLit);

        if (playLitAnimation)
            BeginLitAnimationPlayback();
        else
            StopLitAnimationPlayback();

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
        if (currentLanternTarget == null)
            return false;

        if (currentLanternTarget == transform)
            return true;

        LightableTorch targetedTorch = currentLanternTarget.GetComponent<LightableTorch>();
        if (targetedTorch == null)
            targetedTorch = currentLanternTarget.GetComponentInParent<LightableTorch>();
        if (targetedTorch == null)
            targetedTorch = currentLanternTarget.GetComponentInChildren<LightableTorch>();

        return targetedTorch == this;
    }

    private void UpdateAnimatedVisual()
    {
        if (animatedVisualRenderer == null || !IsLit)
            return;

        if (ShouldUsePossessedVisuals())
        {
            Sprite[] possessedFrames = possessedAnimationFrames;
            Sprite fallbackSprite = GetPossessedFallbackSprite();

            if (!HasSprites(possessedFrames))
            {
                if (fallbackSprite != null)
                    animatedVisualRenderer.sprite = fallbackSprite;
                return;
            }

            float framesPerSecond = possessedAnimationFramesPerSecond;
            if (framesPerSecond <= 0f)
            {
                animatedVisualRenderer.sprite = GetFirstAvailableSprite(possessedFrames) ?? fallbackSprite;
                return;
            }

            animatedVisualElapsedTime += Time.deltaTime;
            int frameIndex = Mathf.FloorToInt(animatedVisualElapsedTime * framesPerSecond) % possessedFrames.Length;
            animatedVisualRenderer.sprite = possessedFrames[frameIndex] ?? fallbackSprite;
            return;
        }

        if (!isPlayingLitAnimation)
        {
            SetAnimatedVisualToDefaultLitSprite();
            return;
        }

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
        int litFrameIndex = Mathf.FloorToInt(animatedVisualElapsedTime * litAnimationFramesPerSecond);

        if (litFrameIndex >= litAnimationFrames.Length)
        {
            StopLitAnimationPlayback();
            SetAnimatedVisualToDefaultLitSprite();
            return;
        }

        Sprite litFrame = litAnimationFrames[litFrameIndex];
        if (litFrame != null)
        {
            animatedVisualRenderer.sprite = litFrame;
            return;
        }

        SetAnimatedVisualToDefaultLitSprite();
    }

    private void ApplyAnimatedVisualImmediate()
    {
        if (animatedVisualRenderer == null)
            return;

        if (!IsLit)
        {
            if (defaultAnimatedVisualSprite != null)
                animatedVisualRenderer.sprite = defaultAnimatedVisualSprite;
            return;
        }

        if (ShouldUsePossessedVisuals())
        {
            Sprite[] possessedFrames = possessedAnimationFrames;
            Sprite fallbackSprite = GetPossessedFallbackSprite();

            if (HasSprites(possessedFrames))
            {
                animatedVisualRenderer.sprite = GetFirstAvailableSprite(possessedFrames) ?? fallbackSprite;
                return;
            }

            if (fallbackSprite != null)
                animatedVisualRenderer.sprite = fallbackSprite;

            return;
        }

        if (isPlayingLitAnimation)
        {
            Sprite litFrame = GetFirstAvailableSprite(litAnimationFrames);
            if (litFrame != null)
                animatedVisualRenderer.sprite = litFrame;
            else
                SetAnimatedVisualToDefaultLitSprite();
            return;
        }

        SetAnimatedVisualToDefaultLitSprite();
    }

    private void SetAnimatedVisualVisible(bool isVisible)
    {
        if (animatedVisualRenderer == null || animatedVisualRenderer == targetRenderer)
            return;

        animatedVisualRenderer.gameObject.SetActive(isVisible);
    }

    private void BeginLitAnimationPlayback()
    {
        animatedVisualElapsedTime = 0f;
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

    private void StopLitAnimationPlayback()
    {
        animatedVisualElapsedTime = 0f;
        isPlayingLitAnimation = false;
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

        return null;
    }

    private bool ShouldUsePossessedVisuals()
    {
        return isPossessedByPista && (HasSprites(possessedAnimationFrames) || possessedSpriteFallback != null);
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
