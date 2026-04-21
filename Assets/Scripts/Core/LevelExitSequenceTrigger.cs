using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class LevelExitSequenceTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private Transform endPosition;
    [SerializeField] private EventDialogueTrigger dialogueTrigger;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;

    [Header("Exit Timing")]
    [SerializeField, Min(0f)] private float fadeDelay = 1.5f;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [SerializeField, Min(0f)] private float returnToMenuDelay = 2f;
    [SerializeField] private string mainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    private Collider2D triggerCollider;
    private bool hasTriggered;

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
        dialogueTrigger?.TryTriggerDialogue();

        Transform playerTransform = playerController.transform;
        float arrivalDistanceSqr = arrivalDistance * arrivalDistance;
        float fadeStartTime = Time.unscaledTime + fadeDelay;

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

        float waitForFade = fadeStartTime - Time.unscaledTime;
        if (waitForFade > 0f)
            yield return new WaitForSecondsRealtime(waitForFade);

        DeathScreenUI deathScreen = DeathScreenUI.Instance;
        if (deathScreen != null)
            yield return deathScreen.FadeToBlack(fadeDuration);

        if (returnToMenuDelay > 0f)
            yield return new WaitForSecondsRealtime(returnToMenuDelay);

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
