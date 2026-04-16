using UnityEngine;

[DisallowMultipleComponent]
public class SignDialogueInteractable : MonoBehaviour, IInteractable
{
    [SerializeField, TextArea(2, 6)] private string dialogueText = "A weathered sign creaks in the wind.";
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private bool useSpriteRendererPortrait = true;
    [SerializeField] private bool useTypewriter;
    [SerializeField, Min(0.5f)] private float displayDuration = 3.5f;

    public void Interact(PlayerController player)
    {
        if (string.IsNullOrWhiteSpace(dialogueText))
            return;

        DialogueBoxUI dialogueBox = DialogueBoxUI.Instance;
        if (dialogueBox == null)
        {
            Debug.LogWarning("No DialogueBoxUI exists in the loaded scene.", this);
            return;
        }

        dialogueBox.ShowSign(dialogueText, ResolvePortrait(dialogueBox), displayDuration, useTypewriter);
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
