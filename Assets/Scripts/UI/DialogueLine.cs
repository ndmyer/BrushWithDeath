using UnityEngine;

[System.Serializable]
public struct DialogueLine
{
    [TextArea(2, 6)] public string text;
    [Min(0.1f)] public float duration;

    public bool HasText()
    {
        return !string.IsNullOrWhiteSpace(text);
    }

    public float ResolveDuration(float fallbackDuration)
    {
        return duration > 0f ? duration : fallbackDuration;
    }
}
