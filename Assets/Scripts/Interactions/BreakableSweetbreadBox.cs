using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BreakableSweetbreadBox : MonoBehaviour, IKnockbackable
{
    private const string UrpSpriteUnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private const string UrpParticlesUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";
    private const string LegacySpriteUnlitShaderName = "Sprites/Default";

    private static Material sharedBreakParticleMaterial;
    private static bool sharedBreakParticleMaterialResolved;
    private static Texture2D sharedBreakParticleTexture;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D hitCollider;

    [Header("Drop")]
    [SerializeField] private GameObject sweetbreadPrefab;
    [SerializeField] private Transform dropSpawnPoint;

    [Header("Break Particles")]
    [SerializeField] private Color breakParticleColor = new(0.8235294f, 0.6431373f, 0.35686275f, 1f);

    private bool isBroken;

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
    }

    public void ApplyKnockback(Vector2 direction, float strengthMultiplier = 1f)
    {
        Break();
    }

    public void ApplyKnockbackFrom(Vector2 sourcePosition, float strengthMultiplier = 1f)
    {
        Break();
    }

    private void Break()
    {
        if (isBroken)
            return;

        isBroken = true;

        Vector3 spawnPosition = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
        SpawnSweetbread(spawnPosition);
        PlayBreakParticles(spawnPosition);

        if (hitCollider != null)
            hitCollider.enabled = false;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        Destroy(gameObject);
    }

    private void SpawnSweetbread(Vector3 spawnPosition)
    {
        if (sweetbreadPrefab == null)
            return;

        Instantiate(sweetbreadPrefab, spawnPosition, Quaternion.identity);
    }

    private void PlayBreakParticles(Vector3 spawnPosition)
    {
        GameObject effectObject = new($"{name}_BreakParticles");
        effectObject.transform.position = spawnPosition;

        ParticleSystem particleSystem = effectObject.AddComponent<ParticleSystem>();
        ConfigureBreakParticles(particleSystem);

        ParticleSystemRenderer particleRenderer = effectObject.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.sharedMaterial = GetSharedBreakParticleMaterial();

            if (spriteRenderer != null)
            {
                particleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                particleRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }

        particleSystem.Play(true);
    }

    private void ConfigureBreakParticles(ParticleSystem particleSystem)
    {
        var main = particleSystem.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 24;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = breakParticleColor;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 12, 18)
        });

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.08f;
        shape.radiusThickness = 1f;

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaFade = new();
        alphaFade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9372549f, 0.7882353f), 0f),
                new GradientColorKey(new Color(0.62352943f, 0.40392157f, 0.18039216f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaFade);

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
    }

    private void CacheReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();

        if (dropSpawnPoint == null)
        {
            Transform candidate = transform.Find("DropSpawnPoint");
            if (candidate != null)
                dropSpawnPoint = candidate;
        }
    }

    private void EnsureColliderConfiguration()
    {
        if (hitCollider != null)
            hitCollider.isTrigger = false;
    }

    private static Material GetSharedBreakParticleMaterial()
    {
        if (sharedBreakParticleMaterial != null)
            return sharedBreakParticleMaterial;

        if (sharedBreakParticleMaterialResolved)
            return null;

        sharedBreakParticleMaterialResolved = true;

        Shader shader = Shader.Find(UrpSpriteUnlitShaderName);
        if (shader == null)
            shader = Shader.Find(UrpParticlesUnlitShaderName);

        if (shader == null)
            shader = Shader.Find(LegacySpriteUnlitShaderName);

        if (shader == null)
            return null;

        sharedBreakParticleMaterial = new Material(shader)
        {
            name = "BreakableSweetbreadBoxParticles",
            hideFlags = HideFlags.HideAndDontSave
        };
        sharedBreakParticleMaterial.mainTexture = GetSharedBreakParticleTexture();
        return sharedBreakParticleMaterial;
    }

    private static Texture2D GetSharedBreakParticleTexture()
    {
        if (sharedBreakParticleTexture != null)
            return sharedBreakParticleTexture;

        sharedBreakParticleTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "BreakableSweetbreadBoxParticleTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        sharedBreakParticleTexture.SetPixel(0, 0, Color.white);
        sharedBreakParticleTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return sharedBreakParticleTexture;
    }
}
