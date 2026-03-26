using UnityEngine;

public static class DirectionUtility
{
    public static Vector2 ToCardinal(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f)
            return Vector2.down;

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            return input.x > 0 ? Vector2.right : Vector2.left;

        return input.y > 0 ? Vector2.up : Vector2.down;
    }
}
