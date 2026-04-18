using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class GeneratePlayerAnimationAssets
{
    private const string PlayerAnimationFolder = "Assets/Animations/Player";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string ControllerPath = PlayerAnimationFolder + "/Player.controller";

    private const string IdleDownSheetPath = "Assets/Art/Animations/Jorge_Idle_Down.png";
    private const string IdleSideSheetPath = "Assets/Art/Animations/Jorge_Idle_Side.png";
    private const string IdleUpSheetPath = "Assets/Art/Animations/Jorge_Idle_Up.png";
    private const string WalkDownSheetPath = "Assets/Art/Animations/Jorge_WalkCycle_Down.png";
    private const string WalkSideSheetPath = "Assets/Art/Animations/Jorge_WalkCycle_Side.png";
    private const string WalkUpSheetPath = "Assets/Art/Animations/Jorge_WalkCycle_Up.png";

    private static readonly string[] IdleDownFrameNames =
    {
        "Jorge_Idle_Down_0",
        "Jorge_Idle_Down_1",
        "Jorge_Idle_Down_2",
        "Jorge_Idle_Down_3",
        "Jorge_Idle_Down_4",
        "Jorge_Idle_Down_5",
        "Jorge_Idle_Down_6",
        "Jorge_Idle_Down_7"
    };

    private static readonly string[] IdleSideFrameNames =
    {
        "Jorge_Idle_Side_0",
        "Jorge_Idle_Side_1",
        "Jorge_Idle_Side_2",
        "Jorge_Idle_Side_3",
        "Jorge_Idle_Side_4",
        "Jorge_Idle_Side_5",
        "Jorge_Idle_Side_6",
        "Jorge_Idle_Side_7"
    };

    private static readonly string[] IdleUpFrameNames =
    {
        "Jorge_Idle_Up_0",
        "Jorge_Idle_Up_1",
        "Jorge_Idle_Up_2",
        "Jorge_Idle_Up_3",
        "Jorge_Idle_Up_4",
        "Jorge_Idle_Up_5",
        "Jorge_Idle_Up_6",
        "Jorge_Idle_Up_7"
    };

    private static readonly string[] WalkDownFrameNames =
    {
        "Jorge_WalkCycle_Down_0",
        "Jorge_WalkCycle_Down_1",
        "Jorge_WalkCycle_Down_2",
        "Jorge_WalkCycle_Down_3"
    };

    private static readonly string[] WalkSideFrameNames =
    {
        "Jorge_WalkCycle_Side_0",
        "Jorge_WalkCycle_Side_1",
        "Jorge_WalkCycle_Side_2",
        "Jorge_WalkCycle_Side_3"
    };

    private static readonly string[] WalkUpFrameNames =
    {
        "Jorge_WalkCycle_Up_0",
        "Jorge_WalkCycle_Up_1",
        "Jorge_WalkCycle_Up_2",
        "Jorge_WalkCycle_Up_3"
    };

    [MenuItem("Tools/Brush With Death/Generate Player Animation Assets")]
    public static void Generate()
    {
        EnsureFolder(PlayerAnimationFolder);

        AnimationClip idleDown = CreateSpriteAnimationClip(PlayerAnimationFolder + "/Player_Idle_Down.anim", IdleDownSheetPath, IdleDownFrameNames, 8f);
        AnimationClip idleSide = CreateSpriteAnimationClip(PlayerAnimationFolder + "/Player_Idle_Side.anim", IdleSideSheetPath, IdleSideFrameNames, 8f);
        AnimationClip idleUp = CreateSpriteAnimationClip(PlayerAnimationFolder + "/Player_Idle_Up.anim", IdleUpSheetPath, IdleUpFrameNames, 8f);
        AnimationClip walkDown = CreateSpriteAnimationClip(PlayerAnimationFolder + "/Player_Walk_Down.anim", WalkDownSheetPath, WalkDownFrameNames, 10f);
        AnimationClip walkSide = CreateSpriteAnimationClip(PlayerAnimationFolder + "/Player_Walk_Side.anim", WalkSideSheetPath, WalkSideFrameNames, 10f);
        AnimationClip walkUp = CreateSpriteAnimationClip(PlayerAnimationFolder + "/Player_Walk_Up.anim", WalkUpSheetPath, WalkUpFrameNames, 10f);

        AnimatorController controller = CreateAnimatorController(idleDown, idleSide, idleUp, walkDown, walkSide, walkUp);
        AssignAnimatorToPlayerPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Generated player animation clips, controller, and prefab assignment.");
    }

    public static void GenerateBatch()
    {
        Generate();
    }

    private static AnimationClip CreateSpriteAnimationClip(string clipPath, string spriteSheetPath, IReadOnlyList<string> frameNames, float frameRate)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        Dictionary<string, Sprite> spritesByName = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath)
            .OfType<Sprite>()
            .ToDictionary(sprite => sprite.name, sprite => sprite);

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frameNames.Count];
        for (int i = 0; i < frameNames.Count; i++)
        {
            if (!spritesByName.TryGetValue(frameNames[i], out Sprite sprite))
                throw new InvalidOperationException($"Missing sprite '{frameNames[i]}' in '{spriteSheetPath}'.");

            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprite
            };
        }

        clip.frameRate = frameRate;

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        SetClipLooping(clip, true);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateAnimatorController(AnimationClip idleDown, AnimationClip idleSide, AnimationClip idleUp, AnimationClip walkDown, AnimationClip walkSide, AnimationClip walkUp)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("FaceX", AnimatorControllerParameterType.Float);
        controller.AddParameter("FaceY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle");
        AnimatorState walkState = stateMachine.AddState("Walk");

        idleState.motion = CreateDirectionalBlendTree(controller, "IdleDirectional", "FaceX", "FaceY", idleDown, idleSide, idleUp);
        walkState.motion = CreateDirectionalBlendTree(controller, "WalkDirectional", "MoveX", "MoveY", walkDown, walkSide, walkUp);

        stateMachine.defaultState = idleState;

        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.hasFixedDuration = true;
        idleToWalk.duration = 0.05f;
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.hasFixedDuration = true;
        walkToIdle.duration = 0.05f;
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static BlendTree CreateDirectionalBlendTree(AnimatorController controller, string name, string parameterX, string parameterY, Motion down, Motion side, Motion up)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = parameterX,
            blendParameterY = parameterY,
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(up, new Vector2(0f, 1f));
        tree.AddChild(down, new Vector2(0f, -1f));
        tree.AddChild(side, new Vector2(1f, 0f));
        tree.AddChild(side, new Vector2(-1f, 0f));

        return tree;
    }

    private static void AssignAnimatorToPlayerPrefab(AnimatorController controller)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Animator animator = prefabRoot.GetComponent<Animator>();
            if (animator == null)
                animator = prefabRoot.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;

            PlayerController playerController = prefabRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject serializedPlayerController = new SerializedObject(playerController);
                serializedPlayerController.FindProperty("animator").objectReferenceValue = animator;
                serializedPlayerController.FindProperty("spriteRenderer").objectReferenceValue = prefabRoot.GetComponent<SpriteRenderer>();
                serializedPlayerController.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }

    private static void SetClipLooping(AnimationClip clip, bool shouldLoop)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
        if (clipSettings == null)
            throw new InvalidOperationException($"Unable to access clip settings for '{clip.name}'.");

        clipSettings.FindPropertyRelative("m_LoopTime").boolValue = shouldLoop;
        serializedClip.ApplyModifiedPropertiesWithoutUndo();
    }
}
