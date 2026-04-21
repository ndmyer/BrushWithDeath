using UnityEngine;

[CreateAssetMenu(fileName = "GameSfxLibrary", menuName = "Brush With Death/Audio/Game Sfx Library")]
public class GameSfxLibrary : ScriptableObject
{
    private const string ResourcePath = "Audio/GameSfxLibrary";

    private static GameSfxLibrary instance;
    private static bool hasTriedLoad;
    private static bool hasLoggedMissingWarning;

    [Header("Menu")]
    [SerializeField] private AudioClip menuConfirmClip;

    [Header("Player")]
    [SerializeField] private AudioClip[] playerHitClips;
    [SerializeField] private AudioClip playerDeathClip;
    [SerializeField] private AudioClip sweetbreadHealClip;
    [SerializeField] private AudioClip lanternSwingClip;
    [SerializeField] private AudioClip guitarSlowLoopClip;
    [SerializeField] private AudioClip guitarMidLoopClip;
    [SerializeField] private AudioClip guitarFastLoopClip;
    [SerializeField] private AudioClip guitarIntenseLoopClip;
    [SerializeField] private AudioClip pistaSentOutClip;
    [SerializeField] private AudioClip pistaReturnClip;

    [Header("Pista")]
    [SerializeField] private AudioClip[] pistaDialogueClips;
    [SerializeField] private AudioClip[] pistaTravelYapClips;

    [Header("Enemies")]
    [SerializeField] private AudioClip enemySpawnClip;
    [SerializeField] private AudioClip[] meleeAttackClips;
    [SerializeField] private AudioClip[] rangedAttackClips;
    [SerializeField] private AudioClip meleeDamagedClip;
    [SerializeField] private AudioClip rangedDamagedClip;
    [SerializeField] private AudioClip meleeMarigoldDeathClip;
    [SerializeField] private AudioClip rangedMarigoldDeathClip;

    [Header("Puzzles")]
    [SerializeField] private AudioClip switchToggleClip;
    [SerializeField] private AudioClip badSwitchClip;
    [SerializeField] private AudioClip puzzleFailedClip;
    [SerializeField] private AudioClip puzzleSolvedClip;
    [SerializeField] private AudioClip blockMovingClip;
    [SerializeField] private AudioClip blockImpactClip;
    [SerializeField] private AudioClip radioSwitchClip;

    [Header("Overworld")]
    [SerializeField] private AudioClip keyCollectClip;
    [SerializeField] private AudioClip[] crateBreakClips;
    [SerializeField] private AudioClip lockedDoorOpenClip;
    [SerializeField] private AudioClip signInteractClip;
    [SerializeField] private AudioClip timesUpClip;
    [SerializeField] private AudioClip torchLitClip;

    public static GameSfxLibrary Instance
    {
        get
        {
            if (!hasTriedLoad)
            {
                hasTriedLoad = true;
                instance = Resources.Load<GameSfxLibrary>(ResourcePath);
            }

            if (instance == null && !hasLoggedMissingWarning)
            {
                hasLoggedMissingWarning = true;
                Debug.LogWarning($"GameSfxLibrary could not be loaded from Resources path '{ResourcePath}'.");
            }

            return instance;
        }
    }

    public AudioClip MenuConfirmClip => menuConfirmClip;
    public AudioClip[] PlayerHitClips => playerHitClips;
    public AudioClip PlayerDeathClip => playerDeathClip;
    public AudioClip SweetbreadHealClip => sweetbreadHealClip;
    public AudioClip LanternSwingClip => lanternSwingClip;
    public AudioClip GuitarSlowLoopClip => guitarSlowLoopClip;
    public AudioClip GuitarMidLoopClip => guitarMidLoopClip;
    public AudioClip GuitarFastLoopClip => guitarFastLoopClip;
    public AudioClip GuitarIntenseLoopClip => guitarIntenseLoopClip;
    public AudioClip PistaSentOutClip => pistaSentOutClip;
    public AudioClip PistaReturnClip => pistaReturnClip;
    public AudioClip[] PistaDialogueClips => pistaDialogueClips;
    public AudioClip[] PistaTravelYapClips => pistaTravelYapClips;
    public AudioClip EnemySpawnClip => enemySpawnClip;
    public AudioClip[] MeleeAttackClips => meleeAttackClips;
    public AudioClip[] RangedAttackClips => rangedAttackClips;
    public AudioClip MeleeDamagedClip => meleeDamagedClip;
    public AudioClip RangedDamagedClip => rangedDamagedClip;
    public AudioClip MeleeMarigoldDeathClip => meleeMarigoldDeathClip;
    public AudioClip RangedMarigoldDeathClip => rangedMarigoldDeathClip;
    public AudioClip SwitchToggleClip => switchToggleClip;
    public AudioClip BadSwitchClip => badSwitchClip;
    public AudioClip PuzzleFailedClip => puzzleFailedClip;
    public AudioClip PuzzleSolvedClip => puzzleSolvedClip;
    public AudioClip BlockMovingClip => blockMovingClip;
    public AudioClip BlockImpactClip => blockImpactClip;
    public AudioClip RadioSwitchClip => radioSwitchClip;
    public AudioClip KeyCollectClip => keyCollectClip;
    public AudioClip[] CrateBreakClips => crateBreakClips;
    public AudioClip LockedDoorOpenClip => lockedDoorOpenClip;
    public AudioClip SignInteractClip => signInteractClip;
    public AudioClip TimesUpClip => timesUpClip;
    public AudioClip TorchLitClip => torchLitClip;

    public AudioClip GetRandomClip(GameSfxCue cue)
    {
        return cue switch
        {
            GameSfxCue.MenuConfirm => menuConfirmClip,
            GameSfxCue.PlayerHit => GetRandomFromBank(playerHitClips),
            GameSfxCue.PlayerDeath => playerDeathClip,
            GameSfxCue.BreadHeal => sweetbreadHealClip,
            GameSfxCue.LanternSwing => lanternSwingClip,
            GameSfxCue.GuitarSlowLoop => guitarSlowLoopClip,
            GameSfxCue.GuitarMidLoop => guitarMidLoopClip,
            GameSfxCue.GuitarFastLoop => guitarFastLoopClip,
            GameSfxCue.GuitarIntenseLoop => guitarIntenseLoopClip,
            GameSfxCue.PistaSend => pistaSentOutClip,
            GameSfxCue.PistaReturn => pistaReturnClip,
            GameSfxCue.PistaDialogue => GetRandomFromBank(pistaDialogueClips),
            GameSfxCue.PistaYap => GetRandomFromBank(pistaTravelYapClips),
            GameSfxCue.EnemySpawn => enemySpawnClip,
            GameSfxCue.MeleeAttack => GetRandomFromBank(meleeAttackClips),
            GameSfxCue.RangedAttack => GetRandomFromBank(rangedAttackClips),
            GameSfxCue.MeleeDamaged => meleeDamagedClip,
            GameSfxCue.RangedDamaged => rangedDamagedClip,
            GameSfxCue.MeleePurified => meleeMarigoldDeathClip,
            GameSfxCue.RangedPurified => rangedMarigoldDeathClip,
            GameSfxCue.SwitchToggle => switchToggleClip,
            GameSfxCue.BadSwitch => badSwitchClip,
            GameSfxCue.PuzzleFailed => puzzleFailedClip,
            GameSfxCue.PuzzleSolved => puzzleSolvedClip,
            GameSfxCue.BlockMoving => blockMovingClip,
            GameSfxCue.BlockImpact => blockImpactClip,
            GameSfxCue.RadioSwitch => radioSwitchClip,
            GameSfxCue.CollectKey => keyCollectClip,
            GameSfxCue.CrateBreak => GetRandomFromBank(crateBreakClips),
            GameSfxCue.LockedDoorOpened => lockedDoorOpenClip,
            GameSfxCue.SignInteract => signInteractClip,
            GameSfxCue.TimeUp => timesUpClip,
            GameSfxCue.TorchLit => torchLitClip,
            _ => null,
        };
    }

    private static AudioClip GetRandomFromBank(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validClipCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validClipCount++;
        }

        if (validClipCount == 0)
            return null;

        int selectedIndex = Random.Range(0, validClipCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (selectedIndex == 0)
                return clips[i];

            selectedIndex--;
        }

        return null;
    }
}
