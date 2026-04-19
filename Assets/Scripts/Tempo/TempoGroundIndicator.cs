using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class TempoGroundIndicator : MonoBehaviour
{
    private const string VisualRootName = "_TempoIndicator";
    private const string BaseRendererName = "CurrentTempo";
    private const string ChannelRendererName = "ChannelTarget";
    private const string IndicatorShaderName = "BrushWithDeath/Tempo/EtherealIndicator";
    private const int GeneratedSpriteResolution = 64;

    private static readonly int TintPropertyId = Shader.PropertyToID("_Tint");
    private static readonly int OpacityPropertyId = Shader.PropertyToID("_Opacity");
    private static readonly int AspectPropertyId = Shader.PropertyToID("_Aspect");
    private static readonly int InnerFadeStartPropertyId = Shader.PropertyToID("_InnerFadeStart");
    private static readonly int InnerFadeEndPropertyId = Shader.PropertyToID("_InnerFadeEnd");
    private static readonly int InnerFadePowerPropertyId = Shader.PropertyToID("_InnerFadePower");
    private static readonly int EdgeSoftnessPropertyId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int WaveCountPropertyId = Shader.PropertyToID("_WaveCount");
    private static readonly int WaveAmplitudePropertyId = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int SecondaryWaveCountPropertyId = Shader.PropertyToID("_SecondaryWaveCount");
    private static readonly int SecondaryWaveAmplitudePropertyId = Shader.PropertyToID("_SecondaryWaveAmplitude");
    private static readonly int WaveSpeedPropertyId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int SecondaryWaveSpeedPropertyId = Shader.PropertyToID("_SecondaryWaveSpeed");
    private static readonly int PulseAmountPropertyId = Shader.PropertyToID("_PulseAmount");
    private static readonly int PulseSpeedPropertyId = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PhaseOffsetPropertyId = Shader.PropertyToID("_PhaseOffset");
    private static readonly int RimBrightnessPropertyId = Shader.PropertyToID("_RimBrightness");
    private static readonly int BoundsScalePropertyId = Shader.PropertyToID("_BoundsScale");

    [Header("References")]
    [SerializeField] private TempoService tempoService;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.3f, 0f);
    [SerializeField] private Vector2 worldSize = new Vector2(0.9f, 0.9f);

    [Header("Render Order")]
    [SerializeField] private int baseSortingOrder = -2;
    [SerializeField] private int channelSortingOrder = -1;

    [Header("Opacity")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.24f;
    [SerializeField, Range(0f, 1f)] private float channelStartAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float channelFullAlpha = 0.72f;

    [Header("Look")]
    [SerializeField, Range(0f, 0.5f)] private float innerFadeStart = 0.12f;
    [SerializeField, Range(0.1f, 1f)] private float innerFadeEnd = 0.82f;
    [SerializeField, Min(0.1f)] private float innerFadePower = 2.2f;
    [SerializeField, Range(0.005f, 0.2f)] private float edgeSoftness = 0.05f;
    [SerializeField, Min(1f)] private float waveCount = 10f;
    [SerializeField, Range(0f, 0.2f)] private float waveAmplitude = 0.055f;
    [SerializeField, Min(1f)] private float secondaryWaveCount = 18f;
    [SerializeField, Range(0f, 0.15f)] private float secondaryWaveAmplitude = 0.022f;
    [SerializeField, Min(0f)] private float waveSpeed = 1.85f;
    [SerializeField, Min(0f)] private float secondaryWaveSpeed = 1.2f;
    [SerializeField, Min(1f)] private float rimBrightness = 1.2f;
    [SerializeField, Range(0f, 0.15f)] private float idlePulseAmount = 0.016f;
    [SerializeField, Range(0f, 0.2f)] private float channelPulseAmount = 0.04f;
    [SerializeField, Min(0f)] private float pulseSpeed = 1.35f;
    [SerializeField, Range(0f, 0.2f)] private float boundsPadding = 0.05f;

    [Header("Tempo Colors")]
    [SerializeField] private Color slowColor = new Color(0.27f, 0.78f, 0.92f, 1f);
    [SerializeField] private Color midColor = new Color(0.93f, 0.77f, 0.34f, 1f);
    [SerializeField] private Color fastColor = new Color(0.96f, 0.49f, 0.25f, 1f);
    [SerializeField] private Color intenseColor = new Color(0.88f, 0.2f, 0.28f, 1f);

    private Transform visualRoot;
    private SpriteRenderer baseRenderer;
    private SpriteRenderer channelRenderer;
    private SpriteRenderer playerSpriteRenderer;
    private SortingGroup sortingGroup;
    private MaterialPropertyBlock basePropertyBlock;
    private MaterialPropertyBlock channelPropertyBlock;

    private static Sprite sharedCircleSprite;
    private static Sprite sharedQuadSprite;
    private static Material sharedIndicatorMaterial;
    private static bool sharedIndicatorMaterialResolved;

    private void Awake()
    {
        CacheReferences();
        EnsureVisuals();
        ApplySnapshot(GetSnapshot());
    }

    private void LateUpdate()
    {
        CacheReferences();
        EnsureVisuals();
        ApplySnapshot(GetSnapshot());
    }

    private void OnDisable()
    {
        if (baseRenderer != null)
            baseRenderer.enabled = false;

        if (channelRenderer != null)
            channelRenderer.enabled = false;
    }

    private void CacheReferences()
    {
        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (playerSpriteRenderer == null)
            TryGetComponent(out playerSpriteRenderer);

        if (sortingGroup == null)
            TryGetComponent(out sortingGroup);

        if (sortingGroup == null)
        {
            sortingGroup = gameObject.AddComponent<SortingGroup>();

            if (playerSpriteRenderer != null)
            {
                sortingGroup.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                sortingGroup.sortingOrder = playerSpriteRenderer.sortingOrder;
            }
        }
        else if (playerSpriteRenderer != null)
        {
            sortingGroup.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            sortingGroup.sortingOrder = playerSpriteRenderer.sortingOrder;
        }
    }

    private void EnsureVisuals()
    {
        if (visualRoot == null)
        {
            Transform existingRoot = transform.Find(VisualRootName);

            if (existingRoot != null)
            {
                visualRoot = existingRoot;
            }
            else
            {
                GameObject rootObject = new GameObject(VisualRootName);
                visualRoot = rootObject.transform;
                visualRoot.SetParent(transform, false);
            }
        }

        visualRoot.localPosition = localOffset;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        baseRenderer = GetOrCreateRenderer(BaseRendererName, ref baseRenderer);
        channelRenderer = GetOrCreateRenderer(ChannelRendererName, ref channelRenderer);

        ConfigureRenderer(baseRenderer, baseSortingOrder);
        ConfigureRenderer(channelRenderer, channelSortingOrder);
    }

    private SpriteRenderer GetOrCreateRenderer(string childName, ref SpriteRenderer cachedRenderer)
    {
        if (cachedRenderer != null)
            return cachedRenderer;

        Transform child = visualRoot.Find(childName);

        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(visualRoot, false);
        }

        if (!child.TryGetComponent(out cachedRenderer))
            cachedRenderer = child.gameObject.AddComponent<SpriteRenderer>();

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return cachedRenderer;
    }

    private TempoStateSnapshot GetSnapshot()
    {
        if (tempoService != null)
            return tempoService.GetCurrentSnapshot();

        return new TempoStateSnapshot(
            TempoBand.Mid,
            TempoBand.Mid,
            false,
            1f,
            0f,
            0f,
            TempoUpdateType.Initialized);
    }

    private void ApplySnapshot(TempoStateSnapshot snapshot)
    {
        if (baseRenderer == null || channelRenderer == null)
            return;

        float baseBoundsScale = GetBoundsScale(idlePulseAmount);
        baseRenderer.enabled = true;
        baseRenderer.transform.localScale = new Vector3(worldSize.x * baseBoundsScale, worldSize.y * baseBoundsScale, 1f);
        ApplyRendererAppearance(
            baseRenderer,
            ref basePropertyBlock,
            GetTempoColor(snapshot.CurrentTempo),
            idleAlpha,
            idlePulseAmount,
            0f,
            baseBoundsScale);

        bool showChannel = snapshot.IsChanneling && snapshot.TargetTempo != snapshot.CurrentTempo;

        if (!showChannel)
        {
            channelRenderer.enabled = false;
            channelRenderer.transform.localScale = Vector3.zero;
            return;
        }

        float progress = Mathf.Clamp01(snapshot.ChannelProgress);
        float channelBoundsScale = GetBoundsScale(channelPulseAmount);
        channelRenderer.enabled = progress > Mathf.Epsilon;
        channelRenderer.transform.localScale = new Vector3(
            worldSize.x * progress * channelBoundsScale,
            worldSize.y * progress * channelBoundsScale,
            1f);
        ApplyRendererAppearance(
            channelRenderer,
            ref channelPropertyBlock,
            GetTempoColor(snapshot.TargetTempo),
            Mathf.Lerp(channelStartAlpha, channelFullAlpha, progress),
            channelPulseAmount,
            0.87f,
            channelBoundsScale);
    }

    private void ApplyRendererAppearance(
        SpriteRenderer renderer,
        ref MaterialPropertyBlock propertyBlock,
        Color color,
        float opacity,
        float pulseAmount,
        float phaseOffset,
        float boundsScale)
    {
        Material indicatorMaterial = GetSharedIndicatorMaterial();
        if (indicatorMaterial == null || renderer.sharedMaterial != indicatorMaterial)
        {
            renderer.SetPropertyBlock(null);
            renderer.color = WithAlpha(color, opacity);
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        renderer.color = Color.white;
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(TintPropertyId, color);
        propertyBlock.SetFloat(OpacityPropertyId, Mathf.Clamp01(opacity));
        propertyBlock.SetFloat(AspectPropertyId, GetAspectRatio());
        propertyBlock.SetFloat(InnerFadeStartPropertyId, Mathf.Clamp(innerFadeStart, 0f, innerFadeEnd - 0.01f));
        propertyBlock.SetFloat(InnerFadeEndPropertyId, Mathf.Clamp(innerFadeEnd, innerFadeStart + 0.01f, 1f));
        propertyBlock.SetFloat(InnerFadePowerPropertyId, Mathf.Max(0.1f, innerFadePower));
        propertyBlock.SetFloat(EdgeSoftnessPropertyId, Mathf.Max(0.005f, edgeSoftness));
        propertyBlock.SetFloat(WaveCountPropertyId, Mathf.Max(1f, waveCount));
        propertyBlock.SetFloat(WaveAmplitudePropertyId, Mathf.Max(0f, waveAmplitude));
        propertyBlock.SetFloat(SecondaryWaveCountPropertyId, Mathf.Max(1f, secondaryWaveCount));
        propertyBlock.SetFloat(SecondaryWaveAmplitudePropertyId, Mathf.Max(0f, secondaryWaveAmplitude));
        propertyBlock.SetFloat(WaveSpeedPropertyId, Mathf.Max(0f, waveSpeed));
        propertyBlock.SetFloat(SecondaryWaveSpeedPropertyId, Mathf.Max(0f, secondaryWaveSpeed));
        propertyBlock.SetFloat(PulseAmountPropertyId, Mathf.Max(0f, pulseAmount));
        propertyBlock.SetFloat(PulseSpeedPropertyId, Mathf.Max(0f, pulseSpeed));
        propertyBlock.SetFloat(PhaseOffsetPropertyId, phaseOffset);
        propertyBlock.SetFloat(RimBrightnessPropertyId, Mathf.Max(1f, rimBrightness));
        propertyBlock.SetFloat(BoundsScalePropertyId, Mathf.Max(1f, boundsScale));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private Color GetTempoColor(TempoBand tempoBand)
    {
        return tempoBand switch
        {
            TempoBand.Slow => slowColor,
            TempoBand.Fast => fastColor,
            TempoBand.Intense => intenseColor,
            _ => midColor
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private float GetAspectRatio()
    {
        return worldSize.y <= Mathf.Epsilon ? 1f : worldSize.x / worldSize.y;
    }

    private float GetBoundsScale(float pulseAmount)
    {
        float waveDisplacement = Mathf.Max(0f, waveAmplitude) + (Mathf.Max(0f, secondaryWaveAmplitude) * 1.6f);
        return 1f
            + waveDisplacement
            + Mathf.Max(0f, pulseAmount)
            + Mathf.Max(0.005f, edgeSoftness)
            + Mathf.Max(0f, boundsPadding);
    }

    private void ConfigureRenderer(SpriteRenderer renderer, int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.sortingOrder = sortingOrder;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.color = Color.white;

        if (playerSpriteRenderer != null)
            renderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;

        Material indicatorMaterial = GetSharedIndicatorMaterial();
        if (indicatorMaterial == null)
        {
            renderer.sprite = GetSharedCircleSprite();
            renderer.sharedMaterial = null;
            return;
        }

        renderer.sprite = GetSharedQuadSprite();
        renderer.sharedMaterial = indicatorMaterial;
    }

    private static Material GetSharedIndicatorMaterial()
    {
        if (sharedIndicatorMaterial != null)
            return sharedIndicatorMaterial;

        if (sharedIndicatorMaterialResolved)
            return null;

        sharedIndicatorMaterialResolved = true;

        Shader indicatorShader = Shader.Find(IndicatorShaderName);
        if (indicatorShader == null)
            return null;

        sharedIndicatorMaterial = new Material(indicatorShader)
        {
            name = "TempoIndicatorSharedMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        return sharedIndicatorMaterial;
    }

    private static Sprite GetSharedQuadSprite()
    {
        if (sharedQuadSprite != null)
            return sharedQuadSprite;

        Texture2D texture = new Texture2D(GeneratedSpriteResolution, GeneratedSpriteResolution, TextureFormat.RGBA32, false);
        texture.name = "TempoIndicatorQuad";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[GeneratedSpriteResolution * GeneratedSpriteResolution];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        sharedQuadSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, GeneratedSpriteResolution, GeneratedSpriteResolution),
            new Vector2(0.5f, 0.5f),
            GeneratedSpriteResolution);
        sharedQuadSprite.name = "TempoIndicatorQuad";
        sharedQuadSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedQuadSprite;
    }

    private static Sprite GetSharedCircleSprite()
    {
        if (sharedCircleSprite != null)
            return sharedCircleSprite;

        Texture2D texture = new Texture2D(GeneratedSpriteResolution, GeneratedSpriteResolution, TextureFormat.RGBA32, false);
        texture.name = "TempoIndicatorCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[GeneratedSpriteResolution * GeneratedSpriteResolution];
        Vector2 center = new Vector2((GeneratedSpriteResolution - 1) * 0.5f, (GeneratedSpriteResolution - 1) * 0.5f);
        float radius = (GeneratedSpriteResolution * 0.5f) - 2f;
        float edgeSoftness = 2f;

        for (int y = 0; y < GeneratedSpriteResolution; y++)
        {
            for (int x = 0; x < GeneratedSpriteResolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - edgeSoftness, radius + edgeSoftness, distance);
                pixels[(y * GeneratedSpriteResolution) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        sharedCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, GeneratedSpriteResolution, GeneratedSpriteResolution),
            new Vector2(0.5f, 0.5f),
            GeneratedSpriteResolution);
        sharedCircleSprite.name = "TempoIndicatorCircle";
        sharedCircleSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedCircleSprite;
    }
}
