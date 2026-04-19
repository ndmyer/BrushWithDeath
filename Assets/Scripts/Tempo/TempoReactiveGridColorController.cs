using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
public class TempoReactiveGridColorController : MonoBehaviour
{
    private const string TargetSceneName = "Level-1";
    private const string TargetTilemapName = "TempoReactive";

    [SerializeField] private TempoService tempoService;
    [SerializeField] private RadioController radioColorSource;
    [SerializeField] private Tilemap targetTilemap;

    private Color baseTilemapColor = Color.white;
    private bool hasCapturedBaseColor;

    private void Awake()
    {
        CacheReferences();
        CaptureBaseColor();
        ApplySnapshot(GetSnapshot());
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureBaseColor();
        ApplySnapshot(GetSnapshot());
    }

    private void Update()
    {
        CacheReferences();
        ApplySnapshot(GetSnapshot());
    }

    private void CacheReferences()
    {
        if (targetTilemap == null)
            TryGetComponent(out targetTilemap);

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();

        if (radioColorSource == null)
            radioColorSource = FindAnyObjectByType<RadioController>();
    }

    private void CaptureBaseColor()
    {
        if (hasCapturedBaseColor || targetTilemap == null)
            return;

        baseTilemapColor = targetTilemap.color;
        hasCapturedBaseColor = true;
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
        if (targetTilemap == null)
            return;

        Color currentColor = GetTempoTint(snapshot.CurrentTempo);

        if (!snapshot.IsChanneling || snapshot.TargetTempo == snapshot.CurrentTempo)
        {
            targetTilemap.color = currentColor;
            return;
        }

        Color targetColor = GetTempoTint(snapshot.TargetTempo);
        targetTilemap.color = Color.Lerp(currentColor, targetColor, Mathf.Clamp01(snapshot.ChannelProgress));
    }

    private Color GetTempoTint(TempoBand tempoBand)
    {
        Color tintColor = radioColorSource != null
            ? radioColorSource.GetTempoColor(tempoBand)
            : GetFallbackTempoColor(tempoBand);

        tintColor.a = baseTilemapColor.a;
        return tintColor;
    }

    private static Color GetFallbackTempoColor(TempoBand tempoBand)
    {
        return tempoBand switch
        {
            TempoBand.Slow => new Color(0.2f, 0.6f, 1f, 1f),
            TempoBand.Fast => new Color(1f, 0.2f, 0.2f, 1f),
            TempoBand.Intense => new Color(0.9607843f, 0.49019608f, 0.2509804f, 1f),
            _ => new Color(0.93f, 0.77f, 0.34f, 1f)
        };
    }

    private static bool IsTargetTilemap(Tilemap tilemap)
    {
        return tilemap != null
            && tilemap.gameObject.scene.IsValid()
            && tilemap.gameObject.scene.name == TargetSceneName
            && tilemap.name == TargetTilemapName;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInLoadedScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        TryInstall(activeScene);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (!scene.IsValid() || scene.name != TargetSceneName)
            return;

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Tilemap[] tilemaps = rootObjects[i].GetComponentsInChildren<Tilemap>(true);
            for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
            {
                Tilemap tilemap = tilemaps[tilemapIndex];
                if (!IsTargetTilemap(tilemap) || tilemap.TryGetComponent<TempoReactiveGridColorController>(out _))
                    continue;

                tilemap.gameObject.AddComponent<TempoReactiveGridColorController>();
            }
        }
    }
}
