using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DialogueTriggerZone : MonoBehaviour
{
    [SerializeField, TextArea(2, 6)] private string dialogueText = "Pista has something to say.";
    [SerializeField] private DialogueLine[] dialogueLines;
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private bool useTypewriter = true;
    [SerializeField, Min(0.5f)] private float displayDuration = 3.5f;

    private bool hasTriggered;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered)
            return;

        DialogueLine[] lines = BuildDialogueLines();
        if (lines.Length == 0)
            return;

        if (!TryGetPlayer(other, out _))
            return;

        DialogueBoxUI dialogueBox = DialogueBoxUI.Instance;
        if (dialogueBox == null)
        {
            Debug.LogWarning("No DialogueBoxUI exists in the loaded scene.", this);
            return;
        }

        dialogueBox.ShowPista(lines, ResolvePortrait(dialogueBox), useTypewriter);
        hasTriggered = true;
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

        return dialogueBox.GetDefaultPistaPortrait();
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private static bool TryGetPlayer(Collider2D other, out PlayerController player)
    {
        player = other.GetComponent<PlayerController>();
        if (player != null)
            return true;

        player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}
