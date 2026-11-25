using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WaterChaos.Player;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Configures the Firefighter model import (clip names, looping), fills the Animator Controller
    /// and builds the Firefighter prefab. Runs once automatically if the prefab is missing,
    /// and can be re-run from the menu after the model is re-exported.
    /// </summary>
    [InitializeOnLoad]
    public static class FirefighterSetup
    {
        private const string ModelPath = "Assets/WaterChaos/Models/Firefighter.fbx";
        private const string ControllerPath = "Assets/WaterChaos/Models/AC_Firefighter.controller";
        private const string PrefabPath = "Assets/WaterChaos/Prefabs/Firefighter.prefab";
        private static readonly string[] ClipNames = { "Idle", "Run", "Extinguish" };

        static FirefighterSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                    Run();
            };
        }

        [MenuItem("Tools/Water Chaos/Setup Firefighter")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                Debug.LogWarning($"[FirefighterSetup] Model not found: {ModelPath}");
                return;
            }

            ConfigureImport();
            AnimatorController controller = BuildController();
            BuildPrefab(controller);
            Debug.Log("[FirefighterSetup] Done: clips, controller and prefab are ready.");
        }

        private static void ConfigureImport()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                // "Firefighter|Idle" -> "Idle"
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            if (controller.layers.Length == 0)
                controller.AddLayer("Base Layer");

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
                stateMachine.RemoveState(child.state);

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();

            for (int i = 0; i < ClipNames.Length; i++)
            {
                AnimationClip clip = clips.FirstOrDefault(c => c.name == ClipNames[i]);
                if (clip == null)
                {
                    Debug.LogWarning($"[FirefighterSetup] Clip '{ClipNames[i]}' not found in {ModelPath}");
                    continue;
                }

                AnimatorState state = stateMachine.AddState(ClipNames[i], new Vector3(300f, 60f * i, 0f));
                state.motion = clip;
                if (i == 0)
                    stateMachine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Firefighter";

            // Unity's fake-null objects break "??", so check explicitly
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            if (instance.GetComponent<FirefighterAnimationSwitcher>() == null)
                instance.AddComponent<FirefighterAnimationSwitcher>();

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }
    }
}
