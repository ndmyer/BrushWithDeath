using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerDamageReceiver))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerProgression))]
[RequireComponent(typeof(TempoGroundIndicator))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    private const string LanternSwingTriggerName = "LanternSwing";
    private const string LanternStateName = "Lantern";
    private const string LanternClipNamePrefix = "Player_Lantern_";
    private const string IsDeadBoolName = "IsDead";
    private const string DeathStateName = "Death";
    private const string DeathClipName = "Player_Death";
    private const int BaseAnimatorLayer = 0;
    private const float DefaultLanternAttackDuration = 0.4f;
    private const float LanternAnimationTimeoutPadding = 0.1f;
    private const float DeathAnimationTimeoutPadding = 0.15f;

    private static readonly int LanternStateHash = Animator.StringToHash(LanternStateName);
    private static readonly int DeathStateHash = Animator.StringToHash(DeathStateName);

    public enum PlayerState
    {
        Normal,
        Attacking,
        PlayingTempo,
        Damaged,
        Dialogue,
        PistaFocus,
        Dead
    }

    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PistaController pistaController;
    [SerializeField] private TempoService tempoService;

    [Header("Actions")]
    [SerializeField, Min(0f)] private float lanternUseCooldown = 0.25f;

    [Header("Tempo Visuals")]
    [SerializeField] private Sprite[] tempoLoopFrames;
    [SerializeField, Min(0f)] private float tempoLoopFramesPerSecond = 12f;

    private PlayerInputReader inputReader;
    private PlayerMotor motor;
    private PlayerInteractor interactor;
    private PlayerLanternSwingVFX lanternSwingVfx;
    private bool hasLanternSwingTrigger;
    private bool hasIsDeadBool;
    private float nextLanternUseTime = float.NegativeInfinity;
    private float lanternAttackUnlockTime = float.NegativeInfinity;
    private bool isTempoLoopActive;
    private bool hasEnteredLanternState;
    private float tempoLoopElapsedTime;

    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    private void Awake()
    {
        EnsureRequiredComponent<PlayerDamageReceiver>();
        EnsureRequiredComponent<PlayerHealth>();
        EnsureRequiredComponent<PlayerProgression>();
        EnsureRequiredComponent<TempoGroundIndicator>();
        GameTimer.EnsureInstance();

        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
        interactor = GetComponent<PlayerInteractor>();
        lanternSwingVfx = GetComponent<PlayerLanternSwingVFX>();

        if (animator == null)
            animator = GetComponent<Animator>();

        CacheAnimationParameters();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (pistaController == null)
            pistaController = FindAnyObjectByType<PistaController>();

        if (tempoService == null)
            tempoService = TempoService.Instance != null ? TempoService.Instance : FindAnyObjectByType<TempoService>();
    }

    private void Update()
    {
        HandleStateLogic();
        UpdateAnimator();
        inputReader.ClearFrameButtons();
    }

    private void LateUpdate()
    {
        UpdateTempoLoopVisual();
    }

    private void HandleStateLogic()
    {
        switch (CurrentState)
        {
            case PlayerState.Normal:
                HandleNormalState();
                break;

            case PlayerState.Dialogue:
                HandleDialogueState();
                break;

            case PlayerState.PlayingTempo:
                HandleTempoFocusState();
                break;

            case PlayerState.Attacking:
                HandleLanternAttackState();
                break;

            case PlayerState.PistaFocus:
                HandlePistaFocusState();
                break;

            default:
                motor.StopMovement();
                break;
        }
    }

    private void HandleDialogueState()
    {
        if (!motor.HasForcedMovement)
            motor.StopMovement();
    }

    private void HandleNormalState()
    {
        if (inputReader.TempoHeld)
        {
            EnterTempoFocus();
            HandleTempoFocusState();
            return;
        }

        motor.SetMovementInput(inputReader.MoveInput);

        if (inputReader.LanternPressed)
            HandleLantern();

        if (inputReader.PistaPressed)
            HandlePista();
    }

    private void HandleTempoFocusState()
    {
        motor.StopMovement();

        if (!inputReader.TempoHeld)
        {
            ExitTempoFocus(allowGraceCompletion: true);
            return;
        }

        if (inputReader.TryGetTempoSelectionHeld(out TempoBand selectedTempo))
            tempoService?.BeginChannel(selectedTempo);
    }

    private void HandlePistaFocusState()
    {
        motor.StopMovement();

        if (inputReader.PistaRecallPressed)
        {
            pistaController?.RecallToPlayer();
            SetState(PlayerState.Normal);
            return;
        }

        if (inputReader.LanternPressed)
            pistaController?.TriggerPulseAttack();

        pistaController?.ProcessAimInput(inputReader.MoveInput);

        if (!inputReader.PistaHeld)
        {
            pistaController?.EndAiming();
            SetState(PlayerState.Normal);
            return;
        }
    }

    private void HandleLanternAttackState()
    {
        motor.StopMovement();

        if (IsLanternAttackStillActive())
            return;

        SetState(PlayerState.Normal);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        Vector2 move = motor.MovementInput;
        Vector2 face = motor.FacingDirection;

        animator.SetFloat("MoveX", move.x);
        animator.SetFloat("MoveY", move.y);
        animator.SetFloat("FaceX", face.x);
        animator.SetFloat("FaceY", face.y);
        animator.SetBool("IsMoving", motor.IsMoving);

        UpdateDeathAnimatorState();
    }

    public void SetState(PlayerState newState)
    {
        CurrentState = newState;
    }

    public void EnterDeathState(bool snapPistaToPlayer = false)
    {
        tempoService?.CancelChannel(allowGraceCompletion: false);

        if (snapPistaToPlayer)
            pistaController?.SnapToPlayer();
        else
            pistaController?.EndAiming();

        motor.ClearForcedMovement();
        motor.StopMovement();
        SetState(PlayerState.Dead);
        UpdateDeathAnimatorState();
    }

    public void ExitDeathState()
    {
        motor.StopMovement();
        SetState(PlayerState.Normal);
        UpdateDeathAnimatorState();
    }

    public IEnumerator WaitForDeathAnimationToFinish()
    {
        if (animator == null || !hasIsDeadBool)
            yield break;

        float deathClipDuration = GetDeathClipDuration();
        float timeout = Mathf.Max(0.1f, deathClipDuration + DeathAnimationTimeoutPadding);
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseAnimatorLayer);
            if (stateInfo.shortNameHash == DeathStateHash)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            if (deathClipDuration > 0f)
                yield return new WaitForSeconds(deathClipDuration);

            yield break;
        }

        while (elapsed < timeout)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseAnimatorLayer);
            if (stateInfo.shortNameHash == DeathStateHash && !animator.IsInTransition(BaseAnimatorLayer) && stateInfo.normalizedTime >= 1f)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void HandleLantern()
    {
        if (Time.time < nextLanternUseTime)
            return;

        nextLanternUseTime = Time.time + lanternUseCooldown;

        if (animator != null && hasLanternSwingTrigger)
            animator.SetTrigger(LanternSwingTriggerName);

        lanternSwingVfx?.Play(motor.FacingDirection);

        interactor.TryLanternSwing(motor.FacingDirection, this);

        motor.StopMovement();
        hasEnteredLanternState = false;
        lanternAttackUnlockTime = Time.time + GetLanternAttackDuration() + LanternAnimationTimeoutPadding;
        SetState(PlayerState.Attacking);
    }

    private void HandlePista()
    {
        if (inputReader.PistaRecallPressed)
        {
            pistaController?.RecallToPlayer();
            Debug.Log("Pista recall requested.");
            return;
        }

        Debug.Log("Entered Pista focus.");
        pistaController?.BeginAiming();
        SetState(PlayerState.PistaFocus);
        motor.StopMovement();
    }

    public bool InterruptTempoFocus(bool allowGraceCompletion)
    {
        if (CurrentState != PlayerState.PlayingTempo)
            return false;

        ExitTempoFocus(allowGraceCompletion);
        return true;
    }

    private void EnsureRequiredComponent<T>()
        where T : Component
    {
        if (!TryGetComponent<T>(out _))
            gameObject.AddComponent<T>();
    }

    private void EnterTempoFocus()
    {
        if (CurrentState == PlayerState.PlayingTempo)
            return;

        SetState(PlayerState.PlayingTempo);
        motor.StopMovement();
    }

    private void ExitTempoFocus(bool allowGraceCompletion)
    {
        tempoService?.CancelChannel(allowGraceCompletion);
        SetState(PlayerState.Normal);
    }

    private void CacheAnimationParameters()
    {
        hasLanternSwingTrigger = HasAnimatorParameter(LanternSwingTriggerName, AnimatorControllerParameterType.Trigger);
        hasIsDeadBool = HasAnimatorParameter(IsDeadBoolName, AnimatorControllerParameterType.Bool);
    }

    private void UpdateDeathAnimatorState()
    {
        if (animator != null && hasIsDeadBool)
            animator.SetBool(IsDeadBoolName, CurrentState == PlayerState.Dead);
    }

    private void UpdateTempoLoopVisual()
    {
        if (spriteRenderer == null || !HasTempoLoopFrames())
            return;

        if (CurrentState != PlayerState.PlayingTempo)
        {
            isTempoLoopActive = false;
            tempoLoopElapsedTime = 0f;
            return;
        }

        if (!isTempoLoopActive)
        {
            isTempoLoopActive = true;
            tempoLoopElapsedTime = 0f;
        }
        else
        {
            tempoLoopElapsedTime += Time.deltaTime;
        }

        Sprite nextFrame = GetTempoLoopFrame();
        if (nextFrame != null)
            spriteRenderer.sprite = nextFrame;
    }

    private Sprite GetTempoLoopFrame()
    {
        if (!HasTempoLoopFrames())
            return null;

        int frameIndex = 0;
        if (tempoLoopFrames.Length > 1 && tempoLoopFramesPerSecond > Mathf.Epsilon)
            frameIndex = Mathf.FloorToInt(tempoLoopElapsedTime * tempoLoopFramesPerSecond) % tempoLoopFrames.Length;

        return tempoLoopFrames[frameIndex] != null ? tempoLoopFrames[frameIndex] : GetFirstTempoLoopFrame();
    }

    private bool HasTempoLoopFrames()
    {
        if (tempoLoopFrames == null || tempoLoopFrames.Length == 0)
            return false;

        for (int i = 0; i < tempoLoopFrames.Length; i++)
        {
            if (tempoLoopFrames[i] != null)
                return true;
        }

        return false;
    }

    private Sprite GetFirstTempoLoopFrame()
    {
        if (tempoLoopFrames == null)
            return null;

        for (int i = 0; i < tempoLoopFrames.Length; i++)
        {
            if (tempoLoopFrames[i] != null)
                return tempoLoopFrames[i];
        }

        return null;
    }

    private float GetDeathClipDuration()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == DeathClipName)
                return clip.length / Mathf.Max(0.0001f, animator.speed);
        }

        return 0f;
    }

    private bool IsLanternAttackStillActive()
    {
        if (animator == null || !hasLanternSwingTrigger)
            return Time.time < lanternAttackUnlockTime;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseAnimatorLayer);
        bool isTransitioning = animator.IsInTransition(BaseAnimatorLayer);
        bool isInLanternState = currentState.shortNameHash == LanternStateHash;
        bool isTransitioningIntoLantern = false;

        if (isTransitioning)
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseAnimatorLayer);
            isTransitioningIntoLantern = nextState.shortNameHash == LanternStateHash;
        }

        if (isInLanternState || isTransitioningIntoLantern)
        {
            hasEnteredLanternState = true;
            return true;
        }

        if (hasEnteredLanternState)
            return false;

        return Time.time < lanternAttackUnlockTime;
    }

    private float GetLanternAttackDuration()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return DefaultLanternAttackDuration;

        float longestClipDuration = 0f;
        float animatorSpeed = Mathf.Max(0.0001f, animator.speed);
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || !clip.name.StartsWith(LanternClipNamePrefix))
                continue;

            longestClipDuration = Mathf.Max(longestClipDuration, clip.length / animatorSpeed);
        }

        return longestClipDuration > 0f ? longestClipDuration : DefaultLanternAttackDuration;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
