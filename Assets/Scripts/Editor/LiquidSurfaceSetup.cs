using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.Player;
using WaterChaos.Surfaces;
using WaterChaos.UI;
using WaterChaos.Water;
using Object = UnityEngine.Object;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One command for puddles, ice, snow and toxic exposure:
    /// textures, materials, the settings asset, the puddle / snowdrift / snow VFX prefabs (created only if missing,
    /// so tuning is kept); the snow look on every nozzle stream (Nozzle prefab and streams in the scenes);
    /// a <see cref="LiquidSurfaceController"/> in the Mini Village and Sample scenes; the toxic exposure and the toxic
    /// screen on the player. Safe to run again. In batch mode it saves the scenes itself.
    /// </summary>
    public static class LiquidSurfaceSetup
    {
        private const string TextureFolder = "Assets/WaterChaos/Textures/Surfaces";
        private const string MaterialFolder = "Assets/WaterChaos/Materials/Surfaces";
        private const string PrefabFolder = "Assets/WaterChaos/Prefabs/Surfaces";
        private const string SettingsFolder = "Assets/WaterChaos/ScriptableObjects/Surfaces";
        private const string SettingsPath = SettingsFolder + "/LiquidSurfaceSettings.asset";
        private const string NozzlePath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ControllerName = "Liquid Surfaces";
        private const int IgnoreRaycastLayer = 2;

        // Liquid settles on everything except these layers (fires, the player, trigger volumes, effects).
        private static readonly string[] NonSurfaceLayers =
            { "TransparentFX", "Ignore Raycast", "Water", "UI", "FlowInfluence", "Player", "Fire", "Room" };

        private static readonly List<string> s_report = new List<string>();

        private struct Assets
        {
            public LiquidSurfaceSettings Settings;
            public PuddleView Puddle;
            public SnowdriftView Snowdrift;
            public ParticleSystem Flakes;
            public ParticleSystem Powder;
        }

        [MenuItem("Water Chaos/Setup Puddles, Snow, Ice and Toxic Exposure")]
        private static void RunFromMenu()
        {
            s_report.Clear();
            Assets assets = EnsureAssets();
            SetupNozzlePrefab(assets);
            SetupScene(SceneManager.GetActiveScene(), assets);
            EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            Debug.Log("Liquid surfaces set up. Save the scene (Cmd+S).\n- " + string.Join("\n- ", s_report));
        }

        /// <summary>Batch mode: sets up assets, the nozzle prefab, and the Mini Village and Sample scenes, and saves.</summary>
        public static void RunAndSave()
        {
            s_report.Clear();
            Assets assets = EnsureAssets();
            SetupNozzlePrefab(assets);

            foreach (string path in new[] { MiniVillage.MiniVillageBuilder.ScenePath, SampleScenePath })
            {
                if (!File.Exists(path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SetupScene(scene, assets);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Liquid surfaces set up.\n- " + string.Join("\n- ", s_report));
        }

        // ───────────── Assets ─────────────

        private static Assets EnsureAssets()
        {
            foreach (string folder in new[] { TextureFolder, MaterialFolder, PrefabFolder, SettingsFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            Texture2D puddleTexture = LoadOrCreateTexture("T_PuddleBlob", 256, PuddlePixel);
            Texture2D iceTexture = LoadOrCreateTexture("T_IceSheet", 256, IcePixel);
            Texture2D flakeTexture = LoadOrCreateTexture("T_Snowflake", 64, SnowflakePixel);
            Texture2D dotTexture = LoadOrCreateTexture("T_SoftDot", 64, SoftDotPixel);

            Material water = LoadOrCreateLit("M_PuddleWater", puddleTexture, new Color(0.55f, 0.78f, 1f, 0.55f), 0.95f, Color.black, true);
            Material ice = LoadOrCreateLit("M_PuddleIce", iceTexture, new Color(0.62f, 0.88f, 1f, 0.85f), 1f, new Color(0.08f, 0.16f, 0.22f), true);
            Material toxic = LoadOrCreateLit("M_PuddleToxic", puddleTexture, new Color(0.45f, 1f, 0.12f, 0.85f), 0.9f, new Color(0.18f, 0.55f, 0.02f), true);
            Material gasoline = LoadOrCreateLit("M_PuddleGasoline", puddleTexture, new Color(1f, 0.72f, 0.2f, 0.6f), 0.95f, Color.black, true);
            Material snow = LoadOrCreateLit("M_Snowdrift", null, new Color(0.95f, 0.97f, 1f, 1f), 0.25f, new Color(0.08f, 0.09f, 0.12f), false);
            Material flake = LoadOrCreateParticleMaterial("M_SnowFlake", flakeTexture, Color.white);
            Material powder = LoadOrCreateParticleMaterial("M_SnowPowder", dotTexture, new Color(0.95f, 0.98f, 1f, 0.9f));
            Material bubble = LoadOrCreateParticleMaterial("M_ToxicBubble", dotTexture, new Color(0.55f, 1f, 0.15f, 0.9f));

            var assets = new Assets
            {
                Settings = LoadOrCreateSettings(),
                Flakes = LoadOrCreatePrefab("SnowStreamFlakes", () => BuildFlakes(flake)).GetComponent<ParticleSystem>(),
                Powder = LoadOrCreatePrefab("SnowImpactPowder", () => BuildImpactPowder(powder)).GetComponent<ParticleSystem>(),
            };
            assets.Puddle = LoadOrCreatePrefab("Puddle", () => BuildPuddle(water, ice, toxic, gasoline, bubble)).GetComponent<PuddleView>();
            assets.Snowdrift = LoadOrCreatePrefab("Snowdrift", () => BuildSnowdrift(snow, powder)).GetComponent<SnowdriftView>();
            return assets;
        }

        private static LiquidSurfaceSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LiquidSurfaceSettings>(SettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<LiquidSurfaceSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            var so = new SerializedObject(settings);
            so.FindProperty("_surfaceMask").intValue = SurfaceMask();
            so.ApplyModifiedPropertiesWithoutUndo();
            s_report.Add($"Settings: {SettingsPath}");
            return settings;
        }

        private static int SurfaceMask()
        {
            int mask = ~0;
            foreach (string layer in NonSurfaceLayers)
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                    mask &= ~(1 << index);
            }

            return mask;
        }

        // ───────────── Textures ─────────────

        private static Texture2D LoadOrCreateTexture(string name, int size, Func<float, float, Color> pixel)
        {
            string path = $"{TextureFolder}/{name}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                return texture;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
            }

            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.SetPixels(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Cartoon puddle: a wobbly blob with a soft edge and a lighter rim.</summary>
        private static Color PuddlePixel(float x, float y)
        {
            float radius = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float edge = 0.86f + 0.06f * Mathf.Sin(angle * 3f + 0.7f) + 0.04f * Mathf.Sin(angle * 5f + 2.1f);
            float alpha = Mathf.Clamp01((edge - radius) / 0.05f);
            float rim = Mathf.Clamp01(1f - Mathf.Abs(edge - 0.08f - radius) / 0.06f);
            float shade = Mathf.Lerp(0.85f, 1f, rim);
            return new Color(shade, shade, shade, alpha * Mathf.Lerp(0.85f, 1f, rim));
        }

        /// <summary>Ice sheet: the puddle blob with white diagonal glints.</summary>
        private static Color IcePixel(float x, float y)
        {
            Color blob = PuddlePixel(x, y);
            float stripe = Mathf.Abs(Mathf.Sin((x + y) * 6f));
            float glint = Mathf.Clamp01((stripe - 0.93f) / 0.07f) * Mathf.Clamp01(1f - Mathf.Abs(x - y) * 0.9f);
            float shade = Mathf.Lerp(0.78f, 1f, glint);
            return new Color(shade, shade, shade, Mathf.Min(1f, blob.a * (0.85f + glint * 0.15f)));
        }

        /// <summary>Six-armed cartoon snowflake.</summary>
        private static Color SnowflakePixel(float x, float y)
        {
            var p = new Vector2(x, y);
            float best = float.MaxValue;
            for (int arm = 0; arm < 6; arm++)
            {
                float angle = arm * Mathf.PI / 3f;
                var tip = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.85f;
                best = Mathf.Min(best, DistanceToSegment(p, Vector2.zero, tip));
                // Little side branches.
                Vector2 mid = tip * 0.55f;
                var side = new Vector2(Mathf.Cos(angle + 0.8f), Mathf.Sin(angle + 0.8f)) * 0.25f;
                var other = new Vector2(Mathf.Cos(angle - 0.8f), Mathf.Sin(angle - 0.8f)) * 0.25f;
                best = Mathf.Min(best, DistanceToSegment(p, mid, mid + side));
                best = Mathf.Min(best, DistanceToSegment(p, mid, mid + other));
            }

            float alpha = Mathf.Clamp01((0.09f - best) / 0.04f);
            alpha = Mathf.Max(alpha, Mathf.Clamp01((0.2f - p.magnitude) / 0.05f));
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color SoftDotPixel(float x, float y)
        {
            float radius = Mathf.Sqrt(x * x + y * y);
            float alpha = Mathf.Clamp01(1f - radius);
            return new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha));
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * t);
        }

        // ───────────── Materials ─────────────

        private static Material LoadOrCreateLit(string name, Texture2D texture, Color color, float smoothness, Color emission, bool transparent)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (transparent)
                MakeTransparent(material);

            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LoadOrCreateParticleMaterial(string name, Texture2D texture, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            MakeTransparent(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            // Off: otherwise highlights stay visible where the texture is fully transparent (a square around puddles).
            if (material.HasProperty("_BlendModePreserveSpecular"))
                material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        // ───────────── Prefabs ─────────────

        private static GameObject LoadOrCreatePrefab(string name, Func<GameObject> build)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;

            GameObject root = build();
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            s_report.Add($"Prefab: {path}");
            return prefab;
        }

        /// <summary>Root (view, scaled disc holder) → Quad lying flat; toxic bubbles beside it. No colliders.</summary>
        private static GameObject BuildPuddle(Material water, Material ice, Material toxic, Material gasoline, Material bubble)
        {
            var root = new GameObject("Puddle");
            var disc = new GameObject("Disc");
            disc.transform.SetParent(root.transform, false);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Surface";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(disc.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            ParticleSystem bubbles = BuildToxicBubbles(bubble);
            bubbles.transform.SetParent(root.transform, false);

            SetLayerRecursively(root, IgnoreRaycastLayer);

            var view = root.AddComponent<PuddleView>();
            var so = new SerializedObject(view);
            so.FindProperty("_disc").objectReferenceValue = disc.transform;
            so.FindProperty("_surface").objectReferenceValue = renderer;
            so.FindProperty("_toxicBubbles").objectReferenceValue = bubbles;
            so.FindProperty("_water").objectReferenceValue = water;
            so.FindProperty("_ice").objectReferenceValue = ice;
            so.FindProperty("_toxic").objectReferenceValue = toxic;
            so.FindProperty("_gasoline").objectReferenceValue = gasoline;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Root (view) → Mound (scaled: diameter, height) → three squashed snow lumps. No colliders.</summary>
        private static GameObject BuildSnowdrift(Material snow, Material powder)
        {
            var root = new GameObject("Snowdrift");
            var mound = new GameObject("Mound");
            mound.transform.SetParent(root.transform, false);

            // A sphere of diameter 1 stretched to 2 in Y, centered on the floor: the visible top reaches the mound height.
            AddLump(mound.transform, snow, Vector3.zero, new Vector3(1f, 2f, 1f));
            AddLump(mound.transform, snow, new Vector3(0.28f, 0f, 0.12f), new Vector3(0.55f, 1.4f, 0.55f));
            AddLump(mound.transform, snow, new Vector3(-0.22f, 0f, -0.18f), new Vector3(0.5f, 1.55f, 0.5f));

            ParticleSystem puff = BuildDriftPuff(powder);
            puff.transform.SetParent(root.transform, false);

            SetLayerRecursively(root, IgnoreRaycastLayer);

            var view = root.AddComponent<SnowdriftView>();
            var so = new SerializedObject(view);
            so.FindProperty("_mound").objectReferenceValue = mound.transform;
            so.FindProperty("_powder").objectReferenceValue = puff;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void AddLump(Transform parent, Material material, Vector3 position, Vector3 scale)
        {
            GameObject lump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lump.name = "Lump";
            Object.DestroyImmediate(lump.GetComponent<Collider>());
            lump.transform.SetParent(parent, false);
            lump.transform.localPosition = position;
            lump.transform.localScale = scale;
            var renderer = lump.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static ParticleSystem NewParticles(string name, Material material, out ParticleSystem.MainModule main)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = particles.main;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        /// <summary>Snowflakes emitted from code along the arc (emission off), drifting and spinning.</summary>
        private static GameObject BuildFlakes(Material material)
        {
            ParticleSystem particles = NewParticles("SnowStreamFlakes", material, out ParticleSystem.MainModule main);
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.15f;
            main.maxParticles = 1500;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 1.5f;

            FadeOut(particles, Color.white);
            return particles.gameObject;
        }

        /// <summary>White powder puffing back from where the snow lands (along +Z, the surface normal).</summary>
        private static GameObject BuildImpactPowder(Material material)
        {
            ParticleSystem particles = NewParticles("SnowImpactPowder", material, out ParticleSystem.MainModule main);
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 40f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.08f;

            GrowOverLife(particles);
            FadeOut(particles, Color.white);
            return particles.gameObject;
        }

        /// <summary>A short puff each time a drift grows (played by the view).</summary>
        private static ParticleSystem BuildDriftPuff(Material material)
        {
            ParticleSystem particles = NewParticles("Powder", material, out ParticleSystem.MainModule main);
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.15f;
            particles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            FadeOut(particles, Color.white);
            return particles;
        }

        /// <summary>Green bubbles rising off a toxic puddle (shape radius follows the puddle).</summary>
        private static ParticleSystem BuildToxicBubbles(Material material)
        {
            ParticleSystem particles = NewParticles("ToxicBubbles", material, out ParticleSystem.MainModule main);
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 1f, 0.15f, 0.9f), new Color(0.8f, 1f, 0.35f, 0.9f));
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 6f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            shape.radiusThickness = 1f;
            // The circle shape lies in XY: turn it flat onto the floor.
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            GrowOverLife(particles);
            FadeOut(particles, new Color(0.6f, 1f, 0.2f));
            return particles;
        }

        private static void GrowOverLife(ParticleSystem particles)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
        }

        private static void FadeOut(ParticleSystem particles, Color color)
        {
            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = layer;
        }

        // ───────────── Snow look on streams ─────────────

        private static void SetupNozzlePrefab(Assets assets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NozzlePath);
            try
            {
                int added = 0;
                foreach (WaterStream stream in root.GetComponentsInChildren<WaterStream>(true))
                {
                    if (AddSnowLook(stream, root, assets))
                        added++;
                }

                if (added > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, NozzlePath);
                s_report.Add($"Nozzle prefab: snow look on {added} new stream(s).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Adds a <see cref="SnowStreamView"/> next to the stream, hiding the views drawing that stream while it snows.</summary>
        private static bool AddSnowLook(WaterStream stream, GameObject searchRoot, Assets assets)
        {
            if (stream.GetComponent<SnowStreamView>() != null)
                return false;

            var renderers = new List<Renderer>();
            foreach (MonoBehaviour view in searchRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(view is WaterStreamTubeView || view is WaterStreamView || view is WaterStreamParticleView))
                    continue;
                if (new SerializedObject(view).FindProperty("_stream").objectReferenceValue != stream)
                    continue;

                if (view is WaterStreamParticleView particles)
                    renderers.AddRange(particles.Renderers);
                else
                    renderers.Add(view.GetComponent<Renderer>());
            }

            GameObject flakes = (GameObject)PrefabUtility.InstantiatePrefab(assets.Flakes.gameObject, stream.transform);
            flakes.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject powder = (GameObject)PrefabUtility.InstantiatePrefab(assets.Powder.gameObject, stream.transform);
            powder.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var snow = stream.gameObject.AddComponent<SnowStreamView>();
            var so = new SerializedObject(snow);
            so.FindProperty("_stream").objectReferenceValue = stream;
            so.FindProperty("_snowflakes").objectReferenceValue = flakes.GetComponent<ParticleSystem>();
            so.FindProperty("_impactPowder").objectReferenceValue = powder.GetComponent<ParticleSystem>();
            SerializedProperty list = so.FindProperty("_waterRenderers");
            list.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ───────────── Scenes ─────────────

        private static void SetupScene(Scene scene, Assets assets)
        {
            SetupController(scene, assets);

            int streams = 0;
            foreach (WaterStream stream in Object.FindObjectsByType<WaterStream>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (stream.gameObject.scene != scene)
                    continue;

                GameObject root = stream.transform.root.gameObject;
                if (AddSnowLook(stream, root, assets))
                    streams++;
            }

            s_report.Add($"{scene.name}: snow look added to {streams} scene stream(s) not from the Nozzle prefab.");
            SetupPlayer(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetupController(Scene scene, Assets assets)
        {
            LiquidSurfaceController controller = null;
            foreach (LiquidSurfaceController found in Object.FindObjectsByType<LiquidSurfaceController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found.gameObject.scene == scene)
                    controller = found;
            }

            if (controller == null)
            {
                var go = new GameObject(ControllerName);
                SceneManager.MoveGameObjectToScene(go, scene);
                controller = go.AddComponent<LiquidSurfaceController>();
                s_report.Add($"{scene.name}: added '{ControllerName}' (LiquidSurfaceController).");
            }

            // Loaded from disk here: references kept from before opening the scene may already be unloaded.
            var puddle = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Puddle.prefab");
            var snowdrift = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Snowdrift.prefab");
            var so = new SerializedObject(controller);
            so.FindProperty("_settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LiquidSurfaceSettings>(SettingsPath);
            so.FindProperty("_puddlePrefab").objectReferenceValue = puddle != null ? puddle.GetComponent<PuddleView>() : null;
            so.FindProperty("_snowdriftPrefab").objectReferenceValue = snowdrift != null ? snowdrift.GetComponent<SnowdriftView>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool ok = so.FindProperty("_puddlePrefab").objectReferenceValue != null
                && so.FindProperty("_snowdriftPrefab").objectReferenceValue != null
                && so.FindProperty("_settings").objectReferenceValue != null;
            s_report.Add(ok ? $"{scene.name}: controller has settings, puddle and snowdrift prefabs."
                            : $"{scene.name}: ERROR controller is missing settings or prefabs.");
        }

        private static void SetupPlayer(Scene scene)
        {
            PlayerInputHandler input = null;
            foreach (PlayerInputHandler found in Object.FindObjectsByType<PlayerInputHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found.gameObject.scene == scene)
                    input = found;
            }

            if (input == null)
            {
                s_report.Add($"{scene.name}: NO PLAYER, toxic screen not added.");
                return;
            }

            GameObject player = input.gameObject;
            if (player.GetComponent<ToxicExposure>() == null)
                player.AddComponent<ToxicExposure>();
            if (player.GetComponent<PlayerToxicOverlay>() == null)
                player.AddComponent<PlayerToxicOverlay>();

            string collider = player.GetComponent<CharacterController>() != null ? "CharacterController" : "NO CharacterController";
            s_report.Add($"{scene.name}: player '{player.name}' has ToxicExposure + PlayerToxicOverlay ({collider}, layer {LayerMask.LayerToName(player.layer)}).");
        }
    }
}
