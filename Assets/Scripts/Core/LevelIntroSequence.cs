using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelIntroSequence : MonoBehaviour
{
    private const string DefaultIntroEndPointName = "IntroEndPoint";

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private Transform introEndPoint;
    [SerializeField] private EventDialogueTrigger dialogueIntro;
    [SerializeField] private GameObject enemySpawnZoneRoot;
    [SerializeField] private GameTimer gameTimer;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;

    private bool introCompleted;
    private bool shouldRunIntro;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        shouldRunIntro = CanRunIntro();

        if (shouldRunIntro)
            gameTimer?.PauseTimer();

        if (shouldRunIntro && enemySpawnZoneRoot != null && enemySpawnZoneRoot.activeSelf)
            enemySpawnZoneRoot.SetActive(false);
    }

    private void Start()
    {
        if (!shouldRunIntro || introCompleted)
            return;

        StartCoroutine(PlayIntroRoutine());
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || introCompleted)
            return;

        playerMotor?.ClearForcedMovement();
        playerMotor?.StopMovement();

        if (playerController != null && playerController.CurrentState == PlayerController.PlayerState.Dialogue)
            playerController.SetState(PlayerController.PlayerState.Normal);
    }

    private IEnumerator PlayIntroRoutine()
    {
        if (!CanRunIntro())
        {
            Debug.LogWarning("Level intro sequence is missing required references and will be skipped.", this);
            yield break;
        }

        playerController.SetState(PlayerController.PlayerState.Dialogue);

        bool dialogueStarted = dialogueIntro != null && dialogueIntro.TryTriggerDialogue();
        Transform playerTransform = playerController.transform;
        float arrivalDistanceSqr = arrivalDistance * arrivalDistance;

        while (true)
        {
            Vector2 toTarget = (Vector2)(introEndPoint.position - playerTransform.position);
            if (toTarget.sqrMagnitude <= arrivalDistanceSqr)
                break;

            playerMotor.SetForcedMovement(toTarget, walkSpeed);
            yield return null;
        }

        playerMotor.ClearForcedMovement();

        Vector3 playerPosition = playerTransform.position;
        Vector3 targetPosition = introEndPoint.position;
        playerTransform.position = new Vector3(targetPosition.x, targetPosition.y, playerPosition.z);

        if (dialogueStarted && DialogueBoxUI.Instance != null)
            yield return new WaitUntil(() => DialogueBoxUI.Instance == null || !DialogueBoxUI.Instance.IsDisplaying);

        FinishIntro();
    }

    private void FinishIntro()
    {
        if (introCompleted)
            return;

        introCompleted = true;

        playerMotor?.ClearForcedMovement();
        playerMotor?.StopMovement();

        if (playerController != null && playerController.CurrentState == PlayerController.PlayerState.Dialogue)
            playerController.SetState(PlayerController.PlayerState.Normal);

        if (enemySpawnZoneRoot != null && !enemySpawnZoneRoot.activeSelf)
            enemySpawnZoneRoot.SetActive(true);

        gameTimer?.StartTimer();
    }

    private bool CanRunIntro()
    {
        if (!Application.isPlaying)
            return false;

        AutoAssignReferences();
        return playerController != null && playerMotor != null && introEndPoint != null;
    }

    private void AutoAssignReferences()
    {
        if (dialogueIntro == null)
            dialogueIntro = GetComponent<EventDialogueTrigger>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (playerMotor == null && playerController != null)
            playerMotor = playerController.GetComponent<PlayerMotor>();

        if (enemySpawnZoneRoot == null)
        {
            EnemySpawnZone enemySpawnZone = FindSceneEnemySpawnZone();
            if (enemySpawnZone != null)
                enemySpawnZoneRoot = enemySpawnZone.gameObject;
        }

        if (gameTimer == null)
            gameTimer = GameTimer.Instance != null ? GameTimer.Instance : FindAnyObjectByType<GameTimer>();

        if (introEndPoint == null)
        {
            Transform resolvedEndPoint = FindSceneTransformByName(DefaultIntroEndPointName);
            if (resolvedEndPoint != null)
                introEndPoint = resolvedEndPoint;
        }
    }

    private EnemySpawnZone FindSceneEnemySpawnZone()
    {
        EnemySpawnZone[] spawnZones = Resources.FindObjectsOfTypeAll<EnemySpawnZone>();
        for (int i = 0; i < spawnZones.Length; i++)
        {
            EnemySpawnZone spawnZone = spawnZones[i];
            if (spawnZone == null || spawnZone.gameObject.scene != gameObject.scene)
                continue;

            return spawnZone;
        }

        return null;
    }

    private Transform FindSceneTransformByName(string objectName)
    {
        Transform[] sceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform sceneTransform = sceneTransforms[i];
            if (sceneTransform == null || sceneTransform.gameObject.scene != gameObject.scene)
                continue;

            if (sceneTransform.name == objectName)
                return sceneTransform;
        }

        return null;
    }
}
