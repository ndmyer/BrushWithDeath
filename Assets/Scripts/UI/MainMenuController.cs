using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string levelScenePath = "Assets/Scenes/Levels/Level-1.unity";
    [SerializeField] private AudioClip menuMusic;

    [Header("Controller Navigation")]
    [SerializeField, Min(0.1f)] private float navigationThreshold = 0.5f;

    private AudioSource musicSource;
    private MainMenuActionButton[] actionButtons;
    private int selectedIndex;
    private bool navigationHeld;

    private void Awake()
    {
        CacheButtons();
        SelectButton(selectedIndex);
        PlayMenuMusic();
    }

    private void Update()
    {
        HandleControllerNavigation();
    }

    public void Play()
    {
        if (string.IsNullOrWhiteSpace(levelScenePath))
        {
            Debug.LogWarning("MainMenuController is missing a level scene path.", this);
            return;
        }

        SceneManager.LoadScene(levelScenePath);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayMenuMusic()
    {
        if (menuMusic == null)
            return;

        if (!TryGetComponent(out musicSource))
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.ignoreListenerPause = true;
        musicSource.clip = menuMusic;

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    public void NotifyButtonSelected(MainMenuActionButton button)
    {
        if (button == null)
            return;

        CacheButtons();

        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] != button)
                continue;

            SelectButton(i);
            return;
        }
    }

    private void CacheButtons()
    {
        actionButtons = GetComponentsInChildren<MainMenuActionButton>(true);

        if (actionButtons == null || actionButtons.Length == 0)
            selectedIndex = 0;
        else
            selectedIndex = Mathf.Clamp(selectedIndex, 0, actionButtons.Length - 1);
    }

    private void HandleControllerNavigation()
    {
        if (actionButtons == null || actionButtons.Length == 0)
            return;

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return;

        Vector2 moveInput = gamepad.leftStick.ReadValue() + gamepad.dpad.ReadValue();
        float verticalInput = moveInput.y;

        if (Mathf.Abs(verticalInput) >= navigationThreshold)
        {
            if (!navigationHeld)
            {
                int nextIndex = verticalInput < 0f ? selectedIndex + 1 : selectedIndex - 1;
                SelectButton(nextIndex);
                navigationHeld = true;
            }
        }
        else
        {
            navigationHeld = false;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
            actionButtons[selectedIndex].Trigger();
    }

    private void SelectButton(int index)
    {
        if (actionButtons == null || actionButtons.Length == 0)
            return;

        selectedIndex = Mathf.Clamp(index, 0, actionButtons.Length - 1);

        for (int i = 0; i < actionButtons.Length; i++)
            actionButtons[i].SetSelected(i == selectedIndex);
    }
}
