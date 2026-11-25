using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Player;
using WaterChaos.UI;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// After scripts reload, sets up the water transfer prototype in the open game scene once,
    /// if it is not there yet (no pump menu in the scene), and saves the scene.
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

            // Not the game scene, or already set up.
            if (Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include) == null
                || Object.FindAnyObjectByType<PumpRadialMenu>(FindObjectsInactive.Include) != null)
                return;

            if (!WaterTransferSetup.Run())
                return;

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Water transfer: set up automatically and saved \"{scene.name}\".");
        }
    }
}
