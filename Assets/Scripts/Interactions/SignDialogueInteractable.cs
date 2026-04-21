using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SignDialogueInteractable : MonoBehaviour, IInteractable
{
    [SerializeField, TextArea(2, 6)] private string dialogueText = "A weathered sign creaks in the wind.";
    [SerializeField] private DialogueLine[] dialogueLines;
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private bool useSpriteRendererPortrait = true;
    [SerializeField] private bool useTypewriter;
    [SerializeField, Min(0.5f)] private float displayDuration = 3.5f;

    public void Interact(PlayerController player)
    {
        DialogueLine[] lines = BuildDialogueLines();
        if (lines.Length == 0)
            return;

        GameSfx.Play(this, GameSfxCue.SignInteract, pitchVariance: 0.02f);

        DialogueBoxUI dialogueBox = DialogueBoxUI.Instance;
        if (dialogueBox == null)
        {
            Debug.LogWarning("No DialogueBoxUI exists in the loaded scene.", this);
            return;
        }

        dialogueBox.ShowSign(lines, ResolvePortrait(dialogueBox), useTypewriter);
    }

    private DialogueLine[] BuildDialogueLines()
    {
        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            List<DialogueLine> resolvedLines = new();
            foreach (DialogueLine line in dialogueLines)
            {
                if (!line.HasText())
                    continue;

                resolvedLines.Add(new DialogueLine
                {
                    text = line.text,
                    duration = line.ResolveDuration(displayDuration),
                });
            }

            if (resolvedLines.Count > 0)
                return resolvedLines.ToArray();
        }

        if (string.IsNullOrWhiteSpace(dialogueText))
            return System.Array.Empty<DialogueLine>();

        return new[]
        {
            new DialogueLine
            {
                text = dialogueText,
                duration = displayDuration,
            }
        };
    }

    private Sprite ResolvePortrait(DialogueBoxUI dialogueBox)
    {
        if (portraitOverride != null)
            return portraitOverride;

        if (useSpriteRendererPortrait)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer != null && spriteRenderer.sprite != null)
                return spriteRenderer.sprite;
        }

        return dialogueBox.GetDefaultSignPortrait();
    }
}
