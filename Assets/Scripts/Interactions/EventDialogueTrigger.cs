using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EventDialogueTrigger : MonoBehaviour
{
    public enum DialogueSpeaker
    {
        Pista,
        Sign
    }

    [Header("Dialogue")]
    [SerializeField] private DialogueSpeaker speaker = DialogueSpeaker.Pista;
    [SerializeField, TextArea(2, 6)] private string dialogueText = "Pista has something to say.";
    [SerializeField] private DialogueLine[] dialogueLines;
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private bool useTypewriter = true;
    [SerializeField, Min(0.5f)] private float displayDuration = 3.5f;

    [Header("Conditions")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private PuzzleStateBool requiredTrueState;
    [SerializeField] private PuzzleStateBool requiredFalseState;

    private bool hasTriggered;

    public bool HasTriggered => hasTriggered;

    public void TriggerDialogue()
    {
        TryTriggerDialogue();
    }

    public bool TryTriggerDialogue()
    {
        if (!CanTrigger())
            return false;

        DialogueLine[] lines = BuildDialogueLines();
        if (lines.Length == 0)
            return false;

        DialogueBoxUI dialogueBox = DialogueBoxUI.Instance;
        if (dialogueBox == null)
        {
            Debug.LogWarning("No DialogueBoxUI exists in the loaded scene.", this);
            return false;
        }

        Sprite portrait = ResolvePortrait(dialogueBox);
        if (speaker == DialogueSpeaker.Sign)
            dialogueBox.ShowSign(lines, portrait, useTypewriter);
        else
            dialogueBox.ShowPista(lines, portrait, useTypewriter);

        hasTriggered = true;
        return true;
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    public bool CanTrigger()
    {
        if (!isActiveAndEnabled)
            return false;

        if (triggerOnce && hasTriggered)
            return false;

        if (requiredTrueState != null && !requiredTrueState.Value)
            return false;

        if (requiredFalseState != null && requiredFalseState.Value)
            return false;

        return true;
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

        return speaker == DialogueSpeaker.Sign
            ? dialogueBox.GetDefaultSignPortrait()
            : dialogueBox.GetDefaultPistaPortrait();
    }
}
