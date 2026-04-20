using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DialogueBoxUI : MonoBehaviour
{
    private static DialogueBoxUI instance;

    [Header("References")]
    [SerializeField] private RectTransform dialogueRoot;
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Portrait Defaults")]
    [SerializeField] private Sprite defaultSignPortrait;
    [SerializeField] private Sprite defaultPistaPortrait;

    [Header("Timing")]
    [SerializeField, Min(0.5f)] private float defaultDisplayDuration = 3.5f;

    [Header("Typewriter")]
    [SerializeField, Min(1f)] private float typewriterCharactersPerSecond = 40f;

    private Coroutine displayRoutine;

    public static DialogueBoxUI Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<DialogueBoxUI>();

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CacheReferences();
        HideImmediate();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void ShowSign(DialogueLine[] lines, Sprite portraitOverride = null, bool useTypewriter = false)
    {
        Show(lines, portraitOverride != null ? portraitOverride : GetDefaultSignPortrait(), useTypewriter);
    }

    public void ShowSign(string message, Sprite portraitOverride = null, float duration = -1f, bool useTypewriter = false)
    {
        ShowSign(CreateSingleLineSequence(message, duration), portraitOverride, useTypewriter);
    }

    public void ShowPista(DialogueLine[] lines, Sprite portraitOverride = null, bool useTypewriter = true)
    {
        Show(lines, portraitOverride != null ? portraitOverride : GetDefaultPistaPortrait(), useTypewriter);
    }

    public void ShowPista(string message, Sprite portraitOverride = null, float duration = -1f, bool useTypewriter = true)
    {
        ShowPista(CreateSingleLineSequence(message, duration), portraitOverride, useTypewriter);
    }

    public Sprite GetDefaultSignPortrait()
    {
        return defaultSignPortrait;
    }

    public Sprite GetDefaultPistaPortrait()
    {
        if (defaultPistaPortrait != null)
            return defaultPistaPortrait;

        PistaController pista = FindAnyObjectByType<PistaController>();
        if (pista == null)
            return null;

        SpriteRenderer spriteRenderer = pista.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = pista.GetComponentInChildren<SpriteRenderer>();

        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    public void HideImmediate()
    {
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = int.MaxValue;

        SetVisible(false);
    }

    private void Show(DialogueLine[] lines, Sprite portrait, bool useTypewriter)
    {
        CacheReferences();

        if (dialogueRoot == null)
        {
            Debug.LogWarning("DialogueBoxUI is missing a dialogue root reference.", this);
            return;
        }

        if (!CanDisplayLines(lines))
        {
            Debug.LogWarning("DialogueBoxUI is missing a TextMeshProUGUI reference.", this);
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
            portraitImage.preserveAspect = true;
        }

        SetVisible(true);

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplayRoutine(lines, useTypewriter));
    }

    private IEnumerator DisplayRoutine(DialogueLine[] lines, bool useTypewriter)
    {
        foreach (DialogueLine line in lines)
        {
            if (!line.HasText())
                continue;

            dialogueText.text = line.text;
            dialogueText.maxVisibleCharacters = int.MaxValue;

            if (useTypewriter)
                yield return PlayTypewriter(line.text);

            yield return new WaitForSecondsRealtime(line.ResolveDuration(defaultDisplayDuration));
        }

        displayRoutine = null;
        SetVisible(false);
    }

    private void CacheReferences()
    {
        if (dialogueRoot == null)
            dialogueRoot = transform as RectTransform;

        if (dialogueRoot != null && dialogueCanvasGroup == null)
        {
            dialogueCanvasGroup = dialogueRoot.GetComponent<CanvasGroup>();
            if (dialogueCanvasGroup == null)
                dialogueCanvasGroup = dialogueRoot.gameObject.AddComponent<CanvasGroup>();
        }

        if (dialogueText == null)
            dialogueText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (portraitImage == null)
            portraitImage = GetComponentInChildren<Image>(true);
    }

    private bool CanDisplayLines(DialogueLine[] lines)
    {
        if (dialogueText == null || lines == null || lines.Length == 0)
            return false;

        foreach (DialogueLine line in lines)
        {
            if (line.HasText())
                return true;
        }

        return false;
    }

    private IEnumerator PlayTypewriter(string message)
    {
        dialogueText.text = message;
        dialogueText.ForceMeshUpdate();

        int totalCharacters = dialogueText.textInfo.characterCount;
        if (totalCharacters <= 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            yield break;
        }

        dialogueText.maxVisibleCharacters = 0;

        float revealedCharacters = 0f;
        while (dialogueText.maxVisibleCharacters < totalCharacters)
        {
            revealedCharacters += typewriterCharactersPerSecond * Time.unscaledDeltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(revealedCharacters), 0, totalCharacters);
            yield return null;
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private DialogueLine[] CreateSingleLineSequence(string message, float duration)
    {
        return new[]
        {
            new DialogueLine
            {
                text = message,
                duration = duration > 0f ? duration : defaultDisplayDuration,
            }
        };
    }

    private void SetVisible(bool isVisible)
    {
        if (dialogueCanvasGroup == null)
            return;

        dialogueCanvasGroup.alpha = isVisible ? 1f : 0f;
        dialogueCanvasGroup.interactable = isVisible;
        dialogueCanvasGroup.blocksRaycasts = isVisible;
    }
}
