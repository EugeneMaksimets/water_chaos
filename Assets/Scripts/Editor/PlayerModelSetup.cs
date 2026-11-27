using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Player;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Gives the player a firefighter body: the Firefighter prefab under the player, feet on the ground,
    /// a little behind the camera, without colliders (the player's own capsule stays the body), with
    /// <see cref="PlayerBodyModel"/> (idle/run, head hidden). The old capsule look is switched off.
    /// Safe to run again: an existing model is kept. Save the scene afterwards.
    /// </summary>
    public static class PlayerModelSetup
    {
        private const string PrefabPath = "Assets/WaterChaos/Prefabs/Firefighter.prefab";
        private const string ModelName = "Player Firefighter Model";

        // Behind the eyes, so the camera never looks out through the chest, in meters (m).
        private const float BackOffset = 0.3f;

        [MenuItem("Water Chaos/Add Player Firefighter Model")]
        private static void Run()
        {
            PlayerInputHandler input = Object.FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (input == null || prefab == null)
            {
                Debug.LogError($"Player model: the scene needs a player (PlayerInputHandler) and {PrefabPath} must exist.");
                return;
            }

            Transform player = input.transform;
            if (player.Find(ModelName) != null)
            {
                Debug.Log("Player model: the player already has a firefighter model.", player.Find(ModelName));
                return;
            }

            Undo.SetCurrentGroupName("Add Player Firefighter Model");
            CharacterController body = player.GetComponent<CharacterController>();

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player);
            Undo.RegisterCreatedObjectUndo(model, "Add Player Firefighter Model");
            model.name = ModelName;
            float feet = body != null ? body.center.y - body.height * 0.5f : -1f;
            model.transform.localPosition = new Vector3(0f, feet, -BackOffset);
            model.transform.localRotation = Quaternion.identity;

            // The player's capsule is the body for physics; the model only shows it.
            foreach (Collider part in model.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(part);

            FirefighterAnimationSwitcher animation = model.GetComponentInChildren<FirefighterAnimationSwitcher>(true);
            Animator animator = animation != null ? animation.GetComponent<Animator>() : model.GetComponentInChildren<Animator>(true);

            PlayerBodyModel component = Undo.AddComponent<PlayerBodyModel>(model);
            SerializedObject data = new SerializedObject(component);
            data.FindProperty("_body").objectReferenceValue = body;
            data.FindProperty("_animation").objectReferenceValue = animation;
            data.FindProperty("_head").objectReferenceValue = FindHead(animator);
            data.ApplyModifiedProperties();

            // The old capsule look of the player.
            MeshRenderer capsule = player.GetComponent<MeshRenderer>();
            if (capsule != null)
            {
                Undo.RecordObject(capsule, "Add Player Firefighter Model");
                capsule.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Selection.activeGameObject = model;
            Debug.Log($"Player model: \"{ModelName}\" added under {player.name}"
                + (data.FindProperty("_head").objectReferenceValue == null ? " (NO HEAD BONE found: the head stays visible)." : ".")
                + " Check its height and offset in Play Mode. Save the scene (Cmd+S).", model);
        }

        private static Transform FindHead(Animator animator)
        {
            if (animator == null)
                return null;

            if (animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null)
                    return bone;
            }

            return animator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.ToLowerInvariant() == "head" || t.name.ToLowerInvariant().EndsWith(":head"));
        }
    }
}
