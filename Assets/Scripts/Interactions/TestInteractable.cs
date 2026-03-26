using UnityEngine;
using UnityEngine.Events;

public class TestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string debugMessage = "Test interactable triggered.";
    [SerializeField] private UnityEvent onInteracted;

    public void Interact(PlayerController player)
    {
        Debug.Log($"{debugMessage} Player: {player.name}", this);
        onInteracted?.Invoke();
    }
}
