using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Player;
using WaterChaos.UI;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of the reservoir info panel: a WaterReservoirDetector (4 m, player layer excluded)
    /// and a WaterReservoirInfoUI on the player, and a WaterReservoirTarget on every tank / lake root
    /// (objects with a ReservoirLevelView or ReservoirSurfaceView), so their walls and floor count too.
    /// Safe to run again: existing components are reused. Save the scene afterwards.
    /// </summary>
    public static class ReservoirInfoSetup
    {
        private const string UndoName = "Setup Reservoir Info";
        private const float DetectionDistance = 4f;

        [MenuItem("Water Chaos/Setup Reservoir Info")]
        private static void Setup()
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            Camera camera = inventory != null ? inventory.GetComponentInChildren<Camera>(true) : null;
            if (camera == null)
            {
                Debug.LogError("Reservoir info setup: the scene needs a PlayerInventory with a child Camera.");
                return;
            }

            GameObject player = inventory.gameObject;

            if (!player.TryGetComponent(out WaterReservoirDetector detector))
                detector = Undo.AddComponent<WaterReservoirDetector>(player);

            SerializedObject detectorData = new SerializedObject(detector);
            detectorData.FindProperty("_camera").objectReferenceValue = camera.transform;
            detectorData.FindProperty("_maxDistance").floatValue = DetectionDistance;
            detectorData.FindProperty("_mask").intValue = ~(1 << player.layer);
            detectorData.ApplyModifiedProperties();

            if (!player.TryGetComponent(out WaterReservoirInfoUI ui))
                ui = Undo.AddComponent<WaterReservoirInfoUI>(player);

            SerializedObject uiData = new SerializedObject(ui);
            uiData.FindProperty("_detector").objectReferenceValue = detector;
            uiData.ApplyModifiedProperties();

            int targets = AddTargets();

            EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log($"Reservoir info setup done: detector and UI on \"{player.name}\", {targets} reservoir target(s) added. Save the scene.");
        }

        private static int AddTargets()
        {
            int added = 0;
            WaterReservoirComponent[] reservoirs = Object.FindObjectsByType<WaterReservoirComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (WaterReservoirComponent reservoir in reservoirs)
            {
                Transform root = reservoir.transform.parent;
                if (root == null || root.GetComponent<WaterReservoirTarget>() != null)
                    continue;

                // Only known reservoir roots: a generic parent (e.g. "Environment") would make everything under it a reservoir.
                if (root.GetComponent<ReservoirLevelView>() == null && root.GetComponent<ReservoirSurfaceView>() == null)
                    continue;

                WaterReservoirTarget target = Undo.AddComponent<WaterReservoirTarget>(root.gameObject);
                SerializedObject data = new SerializedObject(target);
                data.FindProperty("_reservoir").objectReferenceValue = reservoir;
                data.ApplyModifiedProperties();
                added++;
            }

            return added;
        }
    }
}
