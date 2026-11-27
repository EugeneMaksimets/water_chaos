using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Player;
using WaterChaos.Sounds;
using WaterChaos.Teammate;
using WaterChaos.Water;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Sets up the game sounds: the settings asset (Resources/SoundSettings) with every sound, its clips from
    /// Audio/Generated and a starting volume and range; the sound components on the fire, pump and nozzle prefabs,
    /// the teammates' nozzles, the player's footsteps and the level ambience. Existing settings are kept
    /// (only missing sounds are added). Save the scene afterwards.
    /// </summary>
    public static class SoundSetup
    {
        private const string SettingsPath = "Assets/Resources/SoundSettings.asset";
        private const string ClipFolder = "Assets/WaterChaos/Audio/Generated";
        private const string ScenePath = "Assets/WaterChaos/Scenes/WaterChaos_MiniVillage.unity";
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string PumpPrefabPath = "Assets/WaterChaos/Prefabs/Pump.prefab";
        private const string NozzlePrefabPath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";
        private const string AmbienceName = "Ambience";

        // Id, clip names, volume, spatial, min and max distance (m), pitch range.
        private static readonly (SoundId id, string[] clips, float volume, bool spatial, float min, float max, float pitchLow, float pitchHigh)[] Defaults =
        {
            (SoundId.FireBurning, new[] { "Fire_Burning" }, 0.8f, true, 1.5f, 18f, 0.9f, 1.1f),
            (SoundId.FireOut, new[] { "Fire_Out" }, 0.7f, true, 1f, 15f, 0.9f, 1.1f),
            (SoundId.ExtinguishHiss, new[] { "Extinguish_Hiss" }, 0.6f, true, 1f, 15f, 0.9f, 1.1f),
            (SoundId.WaterSpray, new[] { "Water_Spray" }, 0.35f, true, 3f, 25f, 0.95f, 1.05f),
            (SoundId.WaterSplash, new[] { "Water_Splash" }, 0.45f, true, 1f, 18f, 0.9f, 1.1f),
            (SoundId.PumpMotor, new[] { "Pump_Motor" }, 0.6f, true, 1.5f, 20f, 0.95f, 1.05f),
            (SoundId.PumpStart, new[] { "Pump_Start" }, 0.7f, true, 1.5f, 20f, 0.95f, 1.05f),
            (SoundId.PumpStop, new[] { "Pump_Stop" }, 0.7f, true, 1.5f, 20f, 0.95f, 1.05f),
            (SoundId.AxeSwing, new[] { "Axe_Swing" }, 0.6f, false, 1f, 10f, 0.9f, 1.15f),
            (SoundId.AxeHit, new[] { "Axe_Hit" }, 0.9f, true, 1f, 20f, 0.9f, 1.1f),
            (SoundId.Footstep, new[] { "Footstep_1", "Footstep_2", "Footstep_3", "Footstep_4" }, 0.45f, false, 1f, 10f, 0.85f, 1.15f),
            (SoundId.TruckSiren, new[] { "Truck_Siren" }, 0.6f, true, 10f, 80f, 1f, 1f),
            (SoundId.TruckBrakes, new[] { "Truck_Brakes" }, 0.8f, true, 5f, 60f, 0.95f, 1.05f),
            (SoundId.TruckDoor, new[] { "Truck_Door" }, 0.9f, true, 2f, 30f, 0.95f, 1.05f),
            (SoundId.Flamethrower, new[] { "Flamethrower" }, 0.8f, true, 2f, 30f, 0.95f, 1.05f),
            (SoundId.Ambience, new[] { "Ambience_Nature" }, 0.35f, false, 1f, 10f, 1f, 1f),
            (SoundId.ObjectiveComplete, new[] { "UI_ObjectiveComplete" }, 0.6f, false, 1f, 10f, 1f, 1f),
            (SoundId.StoryOpen, new[] { "UI_StoryOpen" }, 0.5f, false, 1f, 10f, 0.95f, 1.05f),
            (SoundId.PickUp, new[] { "Item_PickUp" }, 0.6f, false, 1f, 10f, 0.95f, 1.1f),
            (SoundId.HoseConnect, new[] { "Hose_Connect" }, 0.6f, false, 1f, 10f, 0.95f, 1.1f),
            (SoundId.DoorBreak, new[] { "Door_Break" }, 1f, true, 2f, 30f, 0.95f, 1.05f),
            (SoundId.Knockdown, new[] { "Knockdown_Thud" }, 0.8f, true, 2f, 20f, 0.9f, 1.1f),
        };

        private static readonly List<string> s_report = new List<string>();

        [MenuItem("Water Chaos/Sound Settings")]
        private static void SelectSettings()
        {
            SoundSettings settings = AssetDatabase.LoadAssetAtPath<SoundSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("Sound settings: not created yet, run Water Chaos/Setup Sounds (Mini Village).");
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Water Chaos/Setup Sounds (Mini Village)")]
        private static void RunFromMenu()
        {
            Run();
            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>Batch mode: opens the village, sets everything up and saves.</summary>
        public static void RunAndSave()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Run();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static void Run()
        {
            s_report.Clear();
            CreateSettings();

            AddToPrefab<FireSound>(FirePrefabPath, root => root);
            AddToPrefab<PumpSound>(PumpPrefabPath, root => root);
            AddToPrefab<StreamSound>(NozzlePrefabPath, root => root.GetComponentInChildren<WaterStream>(true)?.gameObject);

            foreach (WaterStream stream in Object.FindObjectsByType<WaterStream>(FindObjectsInactive.Include))
            {
                if (stream.GetComponentInParent<TeammateNozzle>(true) != null && stream.GetComponent<StreamSound>() == null)
                {
                    Undo.AddComponent<StreamSound>(stream.gameObject);
                    s_report.Add($"{stream.transform.root.name}: nozzle stream sound.");
                }
            }

            PlayerInputHandler player = Object.FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);
            if (player != null && player.GetComponent<FootstepSound>() == null)
            {
                Undo.AddComponent<FootstepSound>(player.gameObject);
                s_report.Add("Player: footsteps.");
            }

            if (Object.FindAnyObjectByType<AmbienceSound>(FindObjectsInactive.Include) == null)
            {
                GameObject ambience = new GameObject(AmbienceName);
                Undo.RegisterCreatedObjectUndo(ambience, "Setup Sounds");
                ambience.AddComponent<AmbienceSound>();
                s_report.Add("Ambience object added (nature in the background).");
            }

            Debug.Log("Sounds done. Volumes: Water Chaos → Sound Settings. Save the scene (Cmd+S).\n- " + string.Join("\n- ", s_report));
        }

        private static void CreateSettings()
        {
            SoundSettings settings = AssetDatabase.LoadAssetAtPath<SoundSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SoundSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                s_report.Add($"Sound settings created: {SettingsPath}.");
            }

            SerializedObject data = new SerializedObject(settings);
            SerializedProperty sounds = data.FindProperty("_sounds");
            HashSet<int> present = new HashSet<int>();
            for (int i = 0; i < sounds.arraySize; i++)
                present.Add(sounds.GetArrayElementAtIndex(i).FindPropertyRelative("Id").enumValueIndex);

            foreach (var d in Defaults)
            {
                if (present.Contains((int)d.id))
                    continue;

                sounds.arraySize++;
                SerializedProperty entry = sounds.GetArrayElementAtIndex(sounds.arraySize - 1);
                entry.FindPropertyRelative("Id").enumValueIndex = (int)d.id;
                entry.FindPropertyRelative("Volume").floatValue = d.volume;
                entry.FindPropertyRelative("Spatial").boolValue = d.spatial;
                entry.FindPropertyRelative("MinDistance").floatValue = d.min;
                entry.FindPropertyRelative("MaxDistance").floatValue = d.max;
                entry.FindPropertyRelative("Pitch").vector2Value = new Vector2(d.pitchLow, d.pitchHigh);

                AudioClip[] clips = d.clips.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{n}.wav")).Where(c => c != null).ToArray();
                SerializedProperty list = entry.FindPropertyRelative("Clips");
                list.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                if (clips.Length == 0)
                    s_report.Add($"{d.id}: NO CLIP found in {ClipFolder}.");
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void AddToPrefab<T>(string path, System.Func<GameObject, GameObject> target) where T : Component
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameObject host = target(root);
                if (host == null)
                {
                    s_report.Add($"{path}: no place for {typeof(T).Name}.");
                    return;
                }

                if (host.GetComponent<T>() == null)
                {
                    host.AddComponent<T>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    s_report.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}: {typeof(T).Name}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
