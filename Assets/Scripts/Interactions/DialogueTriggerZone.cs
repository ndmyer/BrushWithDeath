using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DialogueTriggerZone : MonoBehaviour
{
    [SerializeField, TextArea(2, 6)] private string dialogueText = "Pista has something to say.";
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
        if (hasTriggered || string.IsNullOrWhiteSpace(dialogueText))
            return;

        if (!TryGetPlayer(other, out _))
            return;

        DialogueBoxUI dialogueBox = DialogueBoxUI.Instance;
        if (dialogueBox == null)
        {
            Debug.LogWarning("No DialogueBoxUI exists in the loaded scene.", this);
            return;
        }

        dialogueBox.ShowPista(dialogueText, ResolvePortrait(dialogueBox), displayDuration, useTypewriter);
        hasTriggered = true;
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
