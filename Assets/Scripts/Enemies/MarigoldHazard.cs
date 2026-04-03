using UnityEngine;

public class MarigoldHazard : MonoBehaviour
{
    [SerializeField] private bool isActive = true;

    public bool IsActive => isActive;

    public void SetActive(bool active)
    {
        isActive = active;
    }
}
