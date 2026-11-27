using UnityEditor;
using UnityEngine;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Adds the dripping end (<see cref="TransferHoseDripView"/>) to every transfer hose prefab, with the water splash
    /// material; scene hoses made from them get it too. Safe to run again.
    /// </summary>
    public static class TransferHoseDripSetup
    {
        private const string PrefabFolder = "Assets/WaterChaos/Prefabs/WaterTransfer";
        private const string DropMaterialPath = "Assets/WaterChaos/Materials/Water/Materials/M_WaterSplash.mat";

        [MenuItem("Water Chaos/Add Transfer Hose Drips")]
        public static void Run()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(DropMaterialPath);
            int added = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    TransferHose hose = root.GetComponent<TransferHose>();
                    if (hose == null)
                        continue;

                    TransferHoseDripView drips = root.GetComponent<TransferHoseDripView>();
                    if (drips == null)
                        drips = root.AddComponent<TransferHoseDripView>();

                    SerializedObject data = new SerializedObject(drips);
                    data.FindProperty("_hose").objectReferenceValue = hose;
                    data.FindProperty("_material").objectReferenceValue = material;
                    data.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"Transfer hose drips: {added} prefab(s) updated" + (material == null ? $" (no material at {DropMaterialPath}: set one by hand)." : "."));
        }
    }
}
