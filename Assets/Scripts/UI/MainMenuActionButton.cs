using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MainMenuButtonAction
{
    Play,
    Quit,
}

[DisallowMultipleComponent]
public class MainMenuActionButton : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private MainMenuController menuController;
    [SerializeField] private MainMenuButtonAction action;
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new(1f, 0.55f, 0f, 1f);

    private void Awake()
    {
        CacheGraphic();
        SetSelected(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
        Trigger();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Select();
        Trigger();
    }

    public void Trigger()
    {
        if (menuController == null)
            menuController = GetComponentInParent<MainMenuController>();

        if (menuController == null)
        {
            Debug.LogWarning("MainMenuActionButton could not find a MainMenuController.", this);
            return;
        }

        switch (action)
        {
            case MainMenuButtonAction.Play:
                menuController.Play();
                break;
            case MainMenuButtonAction.Quit:
                menuController.Quit();
                break;
        }
    }

    public void SetSelected(bool isSelected)
    {
        CacheGraphic();

        if (targetGraphic != null)
            targetGraphic.color = isSelected ? selectedColor : normalColor;
    }

    private void Select()
    {
        if (menuController == null)
            menuController = GetComponentInParent<MainMenuController>();

        menuController?.NotifyButtonSelected(this);
    }

    private void CacheGraphic()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponentInChildren<Graphic>();
    }
}
