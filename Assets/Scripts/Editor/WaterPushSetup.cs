using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;
using WaterChaos.Interaction;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Lets the water move the village props that stood still (cars, shop shelves and counters, kitchen counters,
    /// the air pump, the propane cage) and refreshes the push profiles (lighter bodies, a profile for cars),
    /// without rebuilding the village, so the scene keeps its hand edits.
    /// Fences, hedges, walls, pool parts, fridges and air conditioners stay fixed.
    /// </summary>
    public static class WaterPushSetup
    {
        private const string PrefabRoot = "Assets/WaterChaos/Environment/Prefabs/";

        private static readonly (string prefab, string profile)[] Props =
        {
            ("Street/Car_Red", VillageAssets.Car),
            ("Street/Car_Blue", VillageAssets.Car),
            ("Street/Car_Yellow", VillageAssets.Car),
            ("Interior/KitchenCounter", VillageAssets.Heavy),
            ("GasStation/ShopShelf", VillageAssets.Heavy),
            ("GasStation/ShopCounter", VillageAssets.Heavy),
            ("GasStation/AirPump", VillageAssets.Heavy),
            ("GasStation/PropaneCage", VillageAssets.Heavy),
        };

        [MenuItem("Water Chaos/Setup Water Push (Props)")]
        public static void Run()
        {
            VillageAssets.Prepare();

            foreach ((string prefab, string profile) in Props)
                MakePushable(PrefabRoot + prefab + ".prefab", profile);

            AssetDatabase.SaveAssets();
            Debug.Log("Water push: props set up, profiles refreshed.");
        }

        /// <summary>Batch entry: also opens and saves the Mini Village scene so its instances pick up the change.</summary>
        public static void RunAndSave()
        {
            Run();
            var scene = EditorSceneManager.OpenScene("Assets/WaterChaos/Scenes/WaterChaos_MiniVillage.unity");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void MakePushable(string path, string profile)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<WaterPushHandler>() != null)
                    return;

                if (profile == VillageAssets.Car)
                    GroundBodyCollider(root);

                VillageAssets.MakePushable(root, profile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The car body box started above the wheel bottoms; stretch it down so the car rests on its wheels.
        private static void GroundBodyCollider(GameObject car)
        {
            foreach (BoxCollider box in car.GetComponents<BoxCollider>())
            {
                if (Mathf.Abs(box.center.y - 0.6f) > 0.01f)
                    continue;

                box.center = new Vector3(box.center.x, 0.525f, box.center.z);
                box.size = new Vector3(box.size.x, 1.05f, box.size.z);
            }
        }
    }
}
