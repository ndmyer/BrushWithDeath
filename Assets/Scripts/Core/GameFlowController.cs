using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameFlowController : MonoBehaviour
{
    private static GameFlowController instance;

    [Header("Game Over")]
    [SerializeField, Min(0f)] private float gameOverFadeDuration = 1f;
    [SerializeField, Min(0f)] private float returnToMenuDelay = 6.5f;
    [SerializeField] private string mainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [SerializeField] private UnityEvent onGameOver;

    private Coroutine gameOverRoutine;

    public static GameFlowController Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<GameFlowController>();

            return instance;
        }
    }

    public bool IsGameOver { get; private set; }

    public static GameFlowController EnsureInstance()
    {
        if (Instance != null)
            return instance;

        GameObject root = new("GameFlowController");
        instance = root.AddComponent<GameFlowController>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void HandleGameOver()
    {
        if (IsGameOver)
            return;

        IsGameOver = true;
        onGameOver?.Invoke();

        if (gameOverRoutine != null)
            StopCoroutine(gameOverRoutine);

        gameOverRoutine = StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        DeathScreenUI deathScreen = DeathScreenUI.Instance;
        if (deathScreen != null)
            yield return deathScreen.FadeToBlack(gameOverFadeDuration);

        if (returnToMenuDelay > 0f)
            yield return new WaitForSecondsRealtime(returnToMenuDelay);

        if (string.IsNullOrWhiteSpace(mainMenuScenePath))
        {
            Debug.LogWarning("GameFlowController is missing a main menu scene path.", this);
            gameOverRoutine = null;
            yield break;
        }

        gameOverRoutine = null;
        SceneManager.LoadScene(mainMenuScenePath);
    }
}
