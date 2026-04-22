using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class LevelExitSequenceTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private Transform endPosition;
    [SerializeField] private EventDialogueTrigger dialogueTrigger;
    [SerializeField] private Tilemap revealedPathTilemap;
    [SerializeField] private AudioClip exitMusicClip;
    [SerializeField] private GameTimer gameTimer;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;

    [Header("Exit Timing")]
    [FormerlySerializedAs("fadeDelay")]
    [SerializeField, Min(0f)] private float fadeStartDelay = 1.5f;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [FormerlySerializedAs("returnToMenuDelay")]
    [SerializeField, Min(0f)] private float mainMenuDelayAfterFadeStart = 2f;
    [SerializeField] private string mainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float exitMusicVolume = 1f;
    [SerializeField, Min(0f)] private float backgroundMusicFadeDuration = 1f;

    private Collider2D triggerCollider;
    private bool hasTriggered;
    private AudioSource exitMusicSource;
    private TempoMusicDirector tempoMusicDirector;

    private void Reset()
    {
        CacheReferences();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || !isActiveAndEnabled)
            return;

        if (!other.TryGetComponent(out PlayerController enteringPlayer))
            return;

        if (playerController == null)
            playerController = enteringPlayer;

        if (enteringPlayer != playerController)
            return;

        if (playerMotor == null)
            playerMotor = enteringPlayer.GetComponent<PlayerMotor>();

        hasTriggered = true;
        StartCoroutine(PlayExitSequence());
    }

    private IEnumerator PlayExitSequence()
    {
        if (playerController == null || playerMotor == null || endPosition == null)
        {
            Debug.LogWarning("Level exit sequence trigger is missing required references and will be skipped.", this);
            yield break;
        }

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        playerController.SetState(PlayerController.PlayerState.Dialogue);
        gameTimer?.PauseTimer();
        dialogueTrigger?.TryTriggerDialogue();
        SetExitPathVisible(true);
        FadeOutBackgroundMusic();
        PlayExitMusic();
        StartCoroutine(FadeAndReturnRoutine());

        Transform playerTransform = playerController.transform;
        float arrivalDistanceSqr = arrivalDistance * arrivalDistance;

        while (true)
        {
            Vector2 toTarget = (Vector2)(endPosition.position - playerTransform.position);
            if (toTarget.sqrMagnitude <= arrivalDistanceSqr)
                break;

            playerMotor.SetForcedMovement(toTarget, walkSpeed);
            yield return null;
        }

        playerMotor.ClearForcedMovement();

        Vector3 playerPosition = playerTransform.position;
        Vector3 targetPosition = endPosition.position;
        playerTransform.position = new Vector3(targetPosition.x, targetPosition.y, playerPosition.z);
    }

    private IEnumerator FadeAndReturnRoutine()
    {
        if (fadeStartDelay > 0f)
            yield return new WaitForSecondsRealtime(fadeStartDelay);

        DeathScreenUI deathScreen = DeathScreenUI.Instance;
        if (deathScreen != null)
        {
            Debug.Log($"LevelExitSequenceTrigger starting fade for {fadeDuration:0.##} seconds.", this);
            StartCoroutine(deathScreen.FadeToBlack(fadeDuration));
        }
        else
        {
            Debug.LogWarning("LevelExitSequenceTrigger could not find DeathScreenUI. Loading main menu without fade.", this);
        }

        if (mainMenuDelayAfterFadeStart > 0f)
            yield return new WaitForSecondsRealtime(mainMenuDelayAfterFadeStart);

        LoadMainMenu();
    }

    private void CacheReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (playerMotor == null && playerController != null)
            playerMotor = playerController.GetComponent<PlayerMotor>();

        if (dialogueTrigger == null)
            dialogueTrigger = GetComponent<EventDialogueTrigger>();

        if (exitMusicSource == null)
            exitMusicSource = GetComponent<AudioSource>();

        if (tempoMusicDirector == null)
            tempoMusicDirector = TempoMusicDirector.Instance != null ? TempoMusicDirector.Instance : FindAnyObjectByType<TempoMusicDirector>();

        if (gameTimer == null)
            gameTimer = GameTimer.Instance != null ? GameTimer.Instance : FindAnyObjectByType<GameTimer>();
    }

    private void SetExitPathVisible(bool isVisible)
    {
        if (revealedPathTilemap == null)
            return;

        Color tilemapColor = revealedPathTilemap.color;
        tilemapColor.a = isVisible ? 1f : 0f;
        revealedPathTilemap.color = tilemapColor;

        TilemapRenderer tilemapRenderer = revealedPathTilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null)
            tilemapRenderer.enabled = isVisible;
    }

    private void PlayExitMusic()
    {
        if (exitMusicClip == null)
            return;

        if (exitMusicSource == null)
        {
            exitMusicSource = GetComponent<AudioSource>();
            if (exitMusicSource == null)
                exitMusicSource = gameObject.AddComponent<AudioSource>();
        }

        exitMusicSource.playOnAwake = false;
        exitMusicSource.loop = false;
        exitMusicSource.ignoreListenerPause = true;
        exitMusicSource.spatialBlend = 0f;
        exitMusicSource.volume = exitMusicVolume;
        exitMusicSource.clip = exitMusicClip;
        exitMusicSource.Play();
    }

    private void FadeOutBackgroundMusic()
    {
        if (tempoMusicDirector == null)
            tempoMusicDirector = TempoMusicDirector.Instance != null ? TempoMusicDirector.Instance : FindAnyObjectByType<TempoMusicDirector>();

        tempoMusicDirector?.FadeOut(backgroundMusicFadeDuration);
    }

    private void LoadMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuScenePath))
        {
            Debug.LogWarning("Level exit sequence trigger is missing a main menu scene path.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuScenePath);
    }
}
