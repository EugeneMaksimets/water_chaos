using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Player;
using WaterChaos.UI;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// After scripts reload, sets up the water transfer prototype in the open game scene if it is not there yet
    /// (no pump menu in the scene), applies scene upgrades, and saves the scene when anything changed.
    /// The same setup is available by hand: Water Chaos/Setup Water Transfer.
    /// </summary>
    [InitializeOnLoad]
    internal static class WaterTransferAutoSetup
    {
        static WaterTransferAutoSetup()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Not the game scene.
            if (Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include) == null)
                return;

            bool changed = false;
            if (Object.FindAnyObjectByType<PumpRadialMenu>(FindObjectsInactive.Include) == null)
            {
                if (!WaterTransferSetup.Run())
                    return;
                changed = true;
            }

            changed |= WaterTransferSetup.UpgradeScene();
            if (!changed)
                return;

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Water transfer: set up automatically and saved \"{scene.name}\".");
        }
    }
}
