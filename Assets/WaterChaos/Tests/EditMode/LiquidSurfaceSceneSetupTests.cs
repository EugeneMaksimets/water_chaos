using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WaterChaos.Tests
{
    /// <summary>
    /// The scenes' liquid surface controller must point at its settings and prefabs: with empty references puddles
    /// and snowdrifts still exist (toxic exposure works) but are invisible. Reads the scene files as text, so it never
    /// touches the scene open in the editor.
    /// </summary>
    public class LiquidSurfaceSceneSetupTests
    {
        private const string ControllerClass = "WaterChaos.Surfaces.LiquidSurfaceController";

        [TestCase("Assets/WaterChaos/Scenes/WaterChaos_MiniVillage.unity")]
        [TestCase("Assets/Scenes/SampleScene.unity")]
        public void Controller_HasSettingsAndPrefabs(string scenePath)
        {
            string scene = File.ReadAllText(scenePath);
            int start = scene.IndexOf(ControllerClass, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"{scenePath} has no LiquidSurfaceController");

            int end = scene.IndexOf("--- !u!", start, System.StringComparison.Ordinal);
            string block = end > start ? scene.Substring(start, end - start) : scene.Substring(start);

            foreach (string field in new[] { "_settings", "_puddlePrefab", "_snowdriftPrefab" })
            {
                Match match = Regex.Match(block, field + @": \{fileID: (-?\d+)");
                Assert.IsTrue(match.Success, $"{field} not found");
                Assert.AreNotEqual("0", match.Groups[1].Value, $"{scenePath}: {field} is empty");
            }
        }
    }
}
