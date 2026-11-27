using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Characters;
using WaterChaos.Interaction;
using WaterChaos.Player;
using WaterChaos.Soap;
using WaterChaos.Teammate;
using WaterChaos.UI;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One command for the character reactions and the water effects:
    /// steam where water meets fire (a white copy of the toxic impact fumes, added to the Cartoon Fire prefab),
    /// a splash of its own for every nozzle with bigger drops, bigger hose drips,
    /// knockdown and burning on every teammate, knockdown (camera) and the burning screen on the player.
    /// Safe to run again. Save the scene afterwards.
    /// </summary>
    public static class CharacterReactionsSetup
    {
        private const string ScenePath = "Assets/WaterChaos/Scenes/WaterChaos_MiniVillage.unity";
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string NozzlePrefabPath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";
        private const string FumesPrefabPath = "Assets/WaterChaos/Prefabs/Toxic/ToxicImpactFumes.prefab";
        private const string FumesMaterialPath = "Assets/WaterChaos/Materials/Toxic/M_ToxicFumes.mat";
        private const string SteamMaterialPath = "Assets/WaterChaos/Materials/Fire/M_FireSteam.mat";
        private const string SteamPrefabPath = "Assets/WaterChaos/Prefabs/FireSteam.prefab";
        private const string HosePrefabFolder = "Assets/WaterChaos/Prefabs/WaterTransfer";
        private const string SplashTemplateName = "WaterSplash";
        private const string SplashName = "Splash";
        private const float SplashDropScale = 1.7f;
        private static readonly Color SteamColor = new Color(0.95f, 0.97f, 1f, 1f);

        private static readonly List<string> s_report = new List<string>();

        [MenuItem("Water Chaos/Setup Character Reactions and Effects (Mini Village)")]
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
            ParticleSystem steam = CreateSteamPrefab();
            AddSteamToFire(steam);

            ParticleSystem splash = FindSceneSplash();
            GiveNozzlePrefabASplash(splash);
            GiveSceneNozzlesASplash(splash);
            BiggerHoseDrips();

            SetUpTeammates();
            SetUpPlayer();

            Debug.Log("Character reactions and effects done. Save the scene (Cmd+S).\n- " + string.Join("\n- ", s_report));
        }

        // ───────────── Steam ─────────────

        private static ParticleSystem CreateSteamPrefab()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SteamMaterialPath);
            if (material == null)
            {
                AssetDatabase.CopyAsset(FumesMaterialPath, SteamMaterialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(SteamMaterialPath);
                foreach (string property in new[] { "_BaseColor", "_Color" })
                {
                    if (material.HasProperty(property))
                        material.SetColor(property, SteamColor);
                }

                EditorUtility.SetDirty(material);
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SteamPrefabPath);
            if (existing != null)
                return existing.GetComponent<ParticleSystem>();

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FumesPrefabPath);
            GameObject steam = Object.Instantiate(source);
            steam.name = "FireSteam";

            foreach (ParticleSystem particles in steam.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = SteamColor;
                main.startSizeMultiplier *= 2.2f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTimeMultiplier *= 2.5f;

                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            }

            if (steam.GetComponent<ParticleSystem>() == null)
                s_report.Add("FireSteam: the fumes prefab has no particle system on its root; the steam will not play.");

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(steam, SteamPrefabPath);
            Object.DestroyImmediate(steam);
            s_report.Add($"Steam prefab {SteamPrefabPath} (white copy of the toxic impact fumes).");
            return saved.GetComponent<ParticleSystem>();
        }

        private static void AddSteamToFire(ParticleSystem steam)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(FirePrefabPath);
            try
            {
                FireSteamEffect effect = root.GetComponent<FireSteamEffect>();
                if (effect == null)
                    effect = root.AddComponent<FireSteamEffect>();
                Set(effect, "_steamPrefab", steam);
                PrefabUtility.SaveAsPrefabAsset(root, FirePrefabPath);
                s_report.Add("Cartoon Fire: steams where water, soapy water or toxic water hits it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ───────────── Splash and drips ─────────────

        private static ParticleSystem FindSceneSplash()
        {
            GameObject template = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include)
                .Select(p => p.gameObject).FirstOrDefault(g => g.name == SplashTemplateName);
            if (template == null)
                s_report.Add($"NO \"{SplashTemplateName}\" in the scene to copy: nozzle splashes not made.");
            return template != null ? template.GetComponent<ParticleSystem>() : null;
        }

        private static void GiveNozzlePrefabASplash(ParticleSystem template)
        {
            if (template == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(NozzlePrefabPath);
            try
            {
                foreach (WaterStreamParticleView view in root.GetComponentsInChildren<WaterStreamParticleView>(true))
                    GiveSplash(view, template, root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, NozzlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            s_report.Add($"Nozzle prefab: a splash of its own, drops ×{SplashDropScale}.");
        }

        private static void GiveSceneNozzlesASplash(ParticleSystem template)
        {
            if (template == null)
                return;

            foreach (WaterStreamParticleView view in Object.FindObjectsByType<WaterStreamParticleView>(FindObjectsInactive.Include))
            {
                SerializedObject data = new SerializedObject(view);
                SerializedProperty splash = data.FindProperty("_splashParticles");

                // The player's nozzle pointed at the one shared scene splash: it now uses the prefab's own.
                if (splash.objectReferenceValue == template && PrefabUtility.IsPartOfPrefabInstance(view))
                {
                    PrefabUtility.RevertPropertyOverride(splash, InteractionMode.AutomatedAction);
                    s_report.Add($"{view.transform.root.name}: uses the nozzle's own splash.");
                    continue;
                }

                if (splash.objectReferenceValue == null)
                {
                    GiveSplash(view, template, view.transform.parent != null ? view.transform.parent : view.transform);
                    s_report.Add($"{view.transform.root.name}: splash added.");
                }
            }
        }

        private static void GiveSplash(WaterStreamParticleView view, ParticleSystem template, Transform parent)
        {
            SerializedObject data = new SerializedObject(view);
            SerializedProperty property = data.FindProperty("_splashParticles");
            if (property.objectReferenceValue != null)
                return;

            GameObject copy = Object.Instantiate(template.gameObject);
            copy.name = SplashName;
            copy.SetActive(true);
            copy.transform.SetParent(parent, false);

            foreach (ParticleSystem particles in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startSizeMultiplier *= SplashDropScale;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }

            property.objectReferenceValue = copy.GetComponent<ParticleSystem>();
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BiggerHoseDrips()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { HosePrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    TransferHoseDripView drips = root.GetComponent<TransferHoseDripView>();
                    if (drips == null)
                        continue;

                    SerializedObject data = new SerializedObject(drips);
                    data.FindProperty("_size").vector2Value = new Vector2(0.09f, 0.16f);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            s_report.Add("Transfer hose drips: bigger drops.");
        }

        // ───────────── Characters ─────────────

        private static void SetUpTeammates()
        {
            GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath);
            foreach (SoapBubbleController teammate in Object.FindObjectsByType<SoapBubbleController>(FindObjectsInactive.Include))
            {
                if (teammate.GetComponent<TeammateMotor>() == null)
                    continue;

                GameObject root = teammate.gameObject;
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    s_report.Add($"{root.name}: NO ANIMATOR, skipped.");
                    continue;
                }

                TeammateSprayAnimation spray = animator.GetComponent<TeammateSprayAnimation>();
                Behaviour[] suspend = spray != null ? new Behaviour[] { spray } : new Behaviour[0];
                Transform chest = animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Chest");

                CharacterKnockdown knockdown = GetOrAdd<CharacterKnockdown>(root);
                SetEnum(knockdown, "_style", 0);
                Set(knockdown, "_model", animator.transform);
                Set(knockdown, "_animator", animator);
                SetArray(knockdown, "_suspend", suspend);

                TeammateBurning burning = GetOrAdd<TeammateBurning>(root);
                Set(burning, "_firePrefab", firePrefab);
                Set(burning, "_fireAnchor", chest != null ? chest : animator.transform);
                Set(burning, "_animator", animator);
                Set(burning, "_knockdown", knockdown);
                SetArray(burning, "_suspend", suspend);

                s_report.Add($"{root.name}: knocked down by plain/toxic water, catches fire from flamethrowers.");
            }
        }

        private static void SetUpPlayer()
        {
            PlayerInputHandler input = Object.FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);
            if (input == null)
            {
                s_report.Add("NO PLAYER in the scene.");
                return;
            }

            GameObject player = input.gameObject;
            Camera camera = player.GetComponentInChildren<Camera>(true);

            CharacterKnockdown knockdown = GetOrAdd<CharacterKnockdown>(player);
            SetEnum(knockdown, "_style", 1);
            Set(knockdown, "_camera", camera != null ? camera.transform : null);
            SetArray(knockdown, "_suspend", player.GetComponent<PlayerMovement>(), player.GetComponent<PlayerLook>());

            GetOrAdd<PlayerBurnOverlay>(player);
            s_report.Add("Player: knocked down by the teammates' plain/toxic water, the screen burns in fire.");
        }

        // ───────────── Helpers ─────────────

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void Set(Object target, string field, Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int index)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).enumValueIndex = index;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            SerializedObject data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            Object[] present = values.Where(v => v != null).ToArray();
            property.arraySize = present.Length;
            for (int i = 0; i < present.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = present[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
