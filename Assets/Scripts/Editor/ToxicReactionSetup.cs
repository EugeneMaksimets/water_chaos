using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.Characters;
using WaterChaos.Interaction;
using WaterChaos.Surfaces;
using WaterChaos.ToxicReaction;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One command for the toxic fire reaction: cloud and chain-blast prefabs, the settings asset (created only if
    /// missing, so tuning is kept), the reaction and its look on the Cartoon Fire prefab (every fire in every scene),
    /// and toxic exposure with green fumes on the teammates of the open scene. Safe to run again.
    /// Needs "Setup Toxic Liquid" and "Setup Puddles, Snow, Ice and Toxic Exposure" to have run (fumes prefab, soft dot texture).
    /// </summary>
    public static class ToxicReactionSetup
    {
        private const string MaterialFolder = "Assets/WaterChaos/Materials/Toxic";
        private const string PrefabFolder = "Assets/WaterChaos/Prefabs/Toxic";
        private const string SettingsFolder = "Assets/WaterChaos/ScriptableObjects/Toxic";
        private const string SettingsPath = SettingsFolder + "/ToxicReactionSettings.asset";
        private const string CloudPath = PrefabFolder + "/ToxicCloud.prefab";
        private const string BlastPath = PrefabFolder + "/ToxicChainBlast.prefab";
        private const string FumesPath = PrefabFolder + "/ToxicImpactFumes.prefab";
        private const string FirePath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string DotTexturePath = "Assets/WaterChaos/Textures/Surfaces/T_SoftDot.png";
        private const string BubblesName = "Toxic Bubbles";
        private const string NpcFumesName = "Toxic Fumes";

        [MenuItem("Water Chaos/Setup Toxic Fire Reaction")]
        private static void RunFromMenu()
        {
            string report = Run(SceneManager.GetActiveScene());
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Toxic fire reaction set up. Save the scene (Cmd+S).\n" + report);
        }

        /// <summary>Batch mode: sets up the assets, the fire prefab and the Mini Village teammates, and saves.</summary>
        public static void RunAndSave()
        {
            Scene scene = EditorSceneManager.OpenScene(MiniVillage.MiniVillageBuilder.ScenePath, OpenSceneMode.Single);
            string report = Run(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Toxic fire reaction set up.\n" + report);
        }

        private static string Run(Scene scene)
        {
            foreach (string folder in new[] { MaterialFolder, PrefabFolder, SettingsFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DotTexturePath);
            Material cloud = LoadOrCreateParticleMaterial("M_ToxicCloud", texture, new Color(0.45f, 0.95f, 0.15f, 0.35f));
            Material blast = LoadOrCreateParticleMaterial("M_ToxicBlast", texture, new Color(0.8f, 1f, 0.3f, 0.95f));

            if (AssetDatabase.LoadAssetAtPath<GameObject>(CloudPath) == null)
                SavePrefab(BuildCloud(cloud), CloudPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BlastPath) == null)
                SavePrefab(BuildBlast(blast, cloud), BlastPath);

            ToxicReactionSettings settings = LoadOrCreateSettings();
            string fire = SetupFirePrefab(settings);
            int teammates = SetupTeammates(scene);
            return $"- Settings: {SettingsPath}\n- {fire}\n- {scene.name}: toxic exposure and fumes on {teammates} new teammate(s).";
        }

        // ───────────── Assets ─────────────

        private static ToxicReactionSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ToxicReactionSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ToxicReactionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            // Loaded from disk now: freshly saved prefabs must not be referenced through stale objects.
            var so = new SerializedObject(settings);
            if (so.FindProperty("_cloudPrefab").objectReferenceValue == null)
                so.FindProperty("_cloudPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPath).GetComponent<ToxicCloud>();
            if (so.FindProperty("_chainBlastPrefab").objectReferenceValue == null)
                so.FindProperty("_chainBlastPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BlastPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<ToxicReactionSettings>(SettingsPath);
        }

        private static Material LoadOrCreateParticleMaterial(string name, Texture2D texture, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetColor("_BaseColor", color);
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        /// <summary>Slow green puffs filling a sphere whose radius the cloud sets every frame.</summary>
        private static GameObject BuildCloud(Material material)
        {
            var root = new GameObject("ToxicCloud");
            root.layer = 2; // Ignore Raycast: streams and rays pass through.
            ParticleSystem particles = AddParticles(root, material, out ParticleSystem.MainModule main);
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.95f, 0.15f, 1f), new Color(0.75f, 1f, 0.3f, 1f));
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 45f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.5f;
            Grow(particles, 0.7f, 1.3f);
            Fade(particles);

            root.AddComponent<ToxicCloud>();
            var so = new SerializedObject(root.GetComponent<ToxicCloud>());
            so.FindProperty("_particles").objectReferenceValue = particles;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>A quick green-yellow flash of sparks and a puff, destroying itself when done.</summary>
        private static GameObject BuildBlast(Material sparkMaterial, Material puffMaterial)
        {
            var root = new GameObject("ToxicChainBlast");
            root.layer = 2;
            ParticleSystem sparks = AddParticles(root, sparkMaterial, out ParticleSystem.MainModule main);
            main.loop = false;
            main.duration = 0.5f;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.gravityModifier = 0.4f;
            main.maxParticles = 80;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            Fade(sparks);

            var puffObject = new GameObject("Puff");
            puffObject.layer = 2;
            puffObject.transform.SetParent(root.transform, false);
            ParticleSystem puff = AddParticles(puffObject, puffMaterial, out ParticleSystem.MainModule puffMain);
            puffMain.loop = false;
            puffMain.duration = 0.5f;
            puffMain.playOnAwake = true;
            puffMain.simulationSpace = ParticleSystemSimulationSpace.World;
            puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            puffMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            puffMain.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            ParticleSystem.EmissionModule puffEmission = puff.emission;
            puffEmission.rateOverTime = 0f;
            puffEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            ParticleSystem.ShapeModule puffShape = puff.shape;
            puffShape.shapeType = ParticleSystemShapeType.Sphere;
            puffShape.radius = 0.3f;
            Grow(puff, 0.5f, 1.6f);
            Fade(puff);
            return root;
        }

        private static ParticleSystem AddParticles(GameObject host, Material material, out ParticleSystem.MainModule main)
        {
            var particles = host.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = particles.main;
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void Grow(ParticleSystem particles, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        private static void Fade(ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
        }

        // ───────────── Fire prefab ─────────────

        private static string SetupFirePrefab(ToxicReactionSettings settings)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(FirePath);
            try
            {
                FireExtinguishHandler fire = root.GetComponentInChildren<FireExtinguishHandler>(true);
                if (fire == null)
                    return "ERROR: Cartoon Fire has no FireExtinguishHandler.";

                GameObject host = fire.gameObject;
                var reaction = host.GetComponent<FireToxicReaction>();
                if (reaction == null)
                    reaction = host.AddComponent<FireToxicReaction>();
                var reactionSo = new SerializedObject(reaction);
                reactionSo.FindProperty("_settings").objectReferenceValue = settings;
                reactionSo.ApplyModifiedPropertiesWithoutUndo();

                Transform bubbles = host.transform.Find(BubblesName);
                if (bubbles == null)
                {
                    var fumesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FumesPath);
                    if (fumesPrefab == null)
                        return "ERROR: run Setup Toxic Liquid first (no ToxicImpactFumes prefab).";

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(fumesPrefab, host.transform);
                    instance.name = BubblesName;
                    instance.transform.SetLocalPositionAndRotation(Vector3.up * 0.2f, Quaternion.LookRotation(Vector3.up));
                    bubbles = instance.transform;
                }

                var view = host.GetComponent<FireToxicReactionView>();
                if (view == null)
                    view = host.AddComponent<FireToxicReactionView>();
                var viewSo = new SerializedObject(view);
                viewSo.FindProperty("_bubbles").objectReferenceValue = bubbles.GetComponent<ParticleSystem>();
                viewSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, FirePath);
                return "Cartoon Fire: toxic reaction, green look and bubbles (every fire in every scene uses it).";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ───────────── Teammates ─────────────

        private static int SetupTeammates(Scene scene)
        {
            var fumesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FumesPath);
            int added = 0;
            foreach (TeammateBurning teammate in Object.FindObjectsByType<TeammateBurning>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (teammate.gameObject.scene != scene || teammate.GetComponent<ToxicExposure>() != null || fumesPrefab == null)
                    continue;

                GameObject host = teammate.gameObject;
                var exposure = host.AddComponent<ToxicExposure>();
                var fumes = (GameObject)PrefabUtility.InstantiatePrefab(fumesPrefab, host.transform);
                fumes.name = NpcFumesName;
                fumes.transform.SetLocalPositionAndRotation(Vector3.up * 1.9f, Quaternion.LookRotation(Vector3.up));

                var view = host.AddComponent<ToxicExposureFumesView>();
                var so = new SerializedObject(view);
                so.FindProperty("_exposure").objectReferenceValue = exposure;
                so.FindProperty("_fumes").objectReferenceValue = fumes.GetComponent<ParticleSystem>();
                so.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return added;
        }
    }
}
