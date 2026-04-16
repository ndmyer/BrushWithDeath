using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeSelectionGuard
{
    static PlayModeSelectionGuard()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange stateChange)
    {
        if (stateChange != PlayModeStateChange.ExitingPlayMode)
            return;

        UnityEngine.Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
            return;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (!IsSceneObject(selectedObjects[i]))
                continue;

            Selection.objects = Array.Empty<UnityEngine.Object>();
            break;
        }
    }

    private static bool IsSceneObject(UnityEngine.Object target)
    {
        if (target == null || EditorUtility.IsPersistent(target))
            return false;

        if (target is GameObject gameObject)
            return gameObject.scene.IsValid();

        if (target is Component component)
            return component.gameObject.scene.IsValid();

        return false;
    }
}
