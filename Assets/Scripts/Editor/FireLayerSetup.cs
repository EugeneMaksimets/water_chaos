using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Interaction;
using WaterChaos.Player;
using WaterChaos.Water;
using Object = UnityEngine.Object;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Makes fires non-solid but still hit by water: fires get their own "Fire" layer that collides with nothing,
    /// the water stream's hit mask includes it, and hose / throw obstacle masks skip it.
    /// Safe to run again. Scene changes are undo-able; save the scene afterwards.
    /// </summary>
    public static class FireLayerSetup
    {
        private const string UndoName = "Setup Fire Layer";
        private const string FireLayer = "Fire";
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string NozzlePrefabPath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";
        private const int FirstUserLayer = 8;

        [MenuItem("Water Chaos/Setup Fire Layer")]
        private static void Setup()
        {
            int layer = GetOrCreateLayer(FireLayer);
            if (layer < 0)
            {
                Debug.LogError("Fire layer setup: no free layer slot for \"Fire\".");
                return;
            }

            DisableCollisions(layer);

            EditPrefab(FirePrefabPath, root => SetLayer(root, layer, false));
            EditPrefab(NozzlePrefabPath, root => UpdateMasks(root, layer, false));

            foreach (FireExtinguishHandler fire in FindInScene<FireExtinguishHandler>())
                SetLayer(fire.gameObject, layer, true);

            UpdateSceneMasks(layer);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log($"Fire layer setup: done, fires are on layer {layer} \"{FireLayer}\". Save the scene (Cmd+S).");
        }

        private static int GetOrCreateLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
                return existing;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = FirstUserLayer; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue))
                    continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }

            return -1;
        }

        /// <summary>Nothing physically touches a fire; queries (water stream, fire search) still find it.</summary>
        private static void DisableCollisions(int layer)
        {
            for (int other = 0; other < 32; other++)
                Physics.IgnoreLayerCollision(layer, other, true);

            SerializedObject physics = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            SerializedProperty matrix = physics.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null || !matrix.isArray)
                return;

            uint bit = 1u << layer;
            for (int i = 0; i < matrix.arraySize; i++)
            {
                SerializedProperty row = matrix.GetArrayElementAtIndex(i);
                uint value = (uint)row.longValue;
                row.longValue = i == layer ? 0u : value & ~bit;
            }

            physics.ApplyModifiedProperties();
        }

        private static void UpdateSceneMasks(int layer)
        {
            foreach (WaterStream stream in FindInScene<WaterStream>())
                SetMaskBit(stream, "_hitMask", layer, true, true);

            foreach (HoseController hose in FindInScene<HoseController>())
                SetMaskBit(hose, "_obstacleMask", layer, false, true);

            foreach (PlayerItemThrower thrower in FindInScene<PlayerItemThrower>())
                SetMaskBit(thrower, "_collisionMask", layer, false, true);
        }

        private static void UpdateMasks(GameObject root, int layer, bool undo)
        {
            foreach (WaterStream stream in root.GetComponentsInChildren<WaterStream>(true))
                SetMaskBit(stream, "_hitMask", layer, true, undo);

            foreach (HoseController hose in root.GetComponentsInChildren<HoseController>(true))
                SetMaskBit(hose, "_obstacleMask", layer, false, undo);
        }

        private static void SetMaskBit(Object component, string field, int layer, bool include, bool undo)
        {
            SerializedObject data = new SerializedObject(component);
            SerializedProperty mask = data.FindProperty(field);
            if (mask == null)
                return;

            int bit = 1 << layer;
            mask.intValue = include ? mask.intValue | bit : mask.intValue & ~bit;

            if (undo)
                data.ApplyModifiedProperties();
            else
                data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayer(GameObject root, int layer, bool undo)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            {
                if (undo)
                    Undo.RecordObject(part.gameObject, UndoName);
                part.gameObject.layer = layer;
            }
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"Fire layer setup: prefab not found at {path}.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T[] FindInScene<T>() where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
