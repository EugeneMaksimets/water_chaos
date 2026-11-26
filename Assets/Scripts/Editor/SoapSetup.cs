using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.Items;
using WaterChaos.Player;
using WaterChaos.Soap;
using WaterChaos.Teammate;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One click sets up the soap mechanic: soap bar model, materials, icon and sounds; stream, impact,
    /// burst and dissolve bubble effects; the big character bubble; the soap item and a teammate firefighter prefab
    /// (created only if missing, so tuning is kept). The nozzle gets soap bubbles on its stream, and the open scene
    /// (the Mini Village in batch mode) gets a bar of soap and a teammate next to the player. Safe to run again.
    /// </summary>
    public static class SoapSetup
    {
        private const string ModelFolder = "Assets/WaterChaos/Models/Soap";
        private const string MaterialFolder = "Assets/WaterChaos/Materials/Soap";
        private const string AudioFolder = "Assets/WaterChaos/Audio/Soap";
        private const string PrefabFolder = "Assets/WaterChaos/Prefabs/Soap";
        private const string IconPath = "Assets/WaterChaos/Materials/Icon/icon_soap.png";
        private const string BubbleShaderPath = "Assets/WaterChaos/Shaders/SoapBubble.shader";
        private const string ParticleShaderPath = "Assets/WaterChaos/Shaders/SoapBubbleParticle.shader";
        private const string NozzlePath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";
        private const string FirefighterPath = "Assets/WaterChaos/Prefabs/Firefighter.prefab";

        private const string SoapSceneName = "Soap";
        private const string TeammateSceneName = "Teammate_Firefighter";

        // Soap bar size (cartoon, a bit bigger than a real one so it reads from first person), in meters (m).
        private static readonly Vector3 SoapSize = new Vector3(0.18f, 0.07f, 0.11f);
        private const float SoapCornerRadius = 0.028f;

        private struct SoapAssets
        {
            public Material Bubble;
            public Material BubbleParticle;
            public GameObject StreamBubbles;
            public GameObject ImpactBubbles;
            public GameObject Burst;
            public GameObject DissolvePuff;
            public GameObject SurfaceBubbles;
            public GameObject CharacterBubble;
            public GameObject Soap;
            public GameObject Teammate;
        }

        [MenuItem("Water Chaos/Setup Soap")]
        public static void Setup()
        {
            SoapAssets assets = EnsureAssets();
            SetupNozzle(assets);
            AssetDatabase.SaveAssets();
            SetupScene(assets);
            Debug.Log("Soap: set up. Throw the soap into a water reservoir, pump it and spray the teammate.");
        }

        // ───────────── Assets ─────────────

        private static SoapAssets EnsureAssets()
        {
            foreach (string folder in new[] { ModelFolder, MaterialFolder, AudioFolder, PrefabFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            Material soap = LoadOrCreateLit("M_Soap", new Color(1f, 0.6f, 0.76f), new Color(0.2f, 0.06f, 0.1f), 0.82f);
            Material emboss = LoadOrCreateLit("M_SoapEmboss", new Color(1f, 0.86f, 0.93f), new Color(0.12f, 0.08f, 0.1f), 0.9f);
            Material bubble = LoadOrCreateShaderMaterial("M_SoapBubble", BubbleShaderPath);
            Material particle = LoadOrCreateShaderMaterial("M_SoapBubbleParticle", ParticleShaderPath);

            Mesh soapMesh = LoadOrCreateSoapMesh();
            Sprite icon = LoadOrCreateIcon();
            AudioClip formSound = LoadOrCreateSound("SFX_SoapBubbleForm", BuildFormSound);
            AudioClip popSound = LoadOrCreateSound("SFX_SoapBubblePop", BuildPopSound);

            var assets = new SoapAssets { Bubble = bubble, BubbleParticle = particle };
            assets.StreamBubbles = LoadOrCreatePrefab("SoapStreamBubbles", () => BuildStreamBubbles(particle));
            assets.ImpactBubbles = LoadOrCreatePrefab("SoapImpactBubbles", () => BuildImpactBubbles(particle));
            assets.Burst = LoadOrCreatePrefab("SoapBubbleBurst", () => BuildBurst(particle));
            assets.DissolvePuff = LoadOrCreatePrefab("SoapDissolvePuff", () => BuildDissolvePuff(particle));
            assets.SurfaceBubbles = LoadOrCreatePrefab("SoapSurfaceBubbles", () => BuildSurfaceBubbles(particle));
            assets.CharacterBubble = LoadOrCreatePrefab("SoapBubble",
                () => BuildCharacterBubble(bubble, particle, assets.Burst, formSound, popSound));
            assets.Soap = LoadOrCreatePrefab("Soap", () => BuildSoap(soapMesh, soap, emboss, icon, assets.DissolvePuff));
            assets.Teammate = LoadOrCreatePrefab("Teammate_Firefighter", () => BuildTeammate(assets.CharacterBubble));
            return assets;
        }

        private static Material LoadOrCreateLit(string name, Color color, Color emission, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LoadOrCreateShaderMaterial(string name, string shaderPath)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject LoadOrCreatePrefab(string name, System.Func<GameObject> build)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;

            GameObject root = build();
            root.name = name;
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ───────────── Soap bar ─────────────

        private static Mesh LoadOrCreateSoapMesh()
        {
            string path = $"{ModelFolder}/SoapBar.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            mesh = BuildRoundedBox(SoapSize * 0.5f, SoapCornerRadius);
            mesh.name = "SoapBar";
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Low-poly box with rounded edges and corners (3 segments per 45° of each rounding), smooth normals.
        /// Each face is a grid on the box surface; every grid point is pushed onto the rounded shape.
        /// </summary>
        private static Mesh BuildRoundedBox(Vector3 half, float radius)
        {
            // Offsets inside each rounded edge: r·tan(15°), r·tan(30°), r — even angle steps along the arc.
            float[] arc = { 0.268f, 0.577f, 1f };
            Vector3 inner = half - Vector3.one * radius;

            float[] Axis(float e)
            {
                var values = new List<float>();
                for (int i = arc.Length - 1; i >= 0; i--)
                    values.Add(-(e + radius * arc[i]));
                values.Add(-e);
                values.Add(0f);
                values.Add(e);
                foreach (float a in arc)
                    values.Add(e + radius * a);
                return values.ToArray();
            }

            float[][] grids = { Axis(inner.x), Axis(inner.y), Axis(inner.z) };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int axis = 0; axis < 3; axis++)
            {
                int uAxis = (axis + 1) % 3;
                int vAxis = (axis + 2) % 3;
                foreach (float sign in new[] { 1f, -1f })
                {
                    float[] us = grids[uAxis];
                    float[] vs = grids[vAxis];
                    int start = vertices.Count;

                    for (int j = 0; j < vs.Length; j++)
                    {
                        for (int i = 0; i < us.Length; i++)
                        {
                            Vector3 q = Vector3.zero;
                            q[axis] = sign * half[axis];
                            q[uAxis] = us[i];
                            q[vAxis] = vs[j];

                            var core = new Vector3(
                                Mathf.Clamp(q.x, -inner.x, inner.x),
                                Mathf.Clamp(q.y, -inner.y, inner.y),
                                Mathf.Clamp(q.z, -inner.z, inner.z));
                            Vector3 normal = (q - core).normalized;
                            vertices.Add(core + normal * radius);
                            normals.Add(normal);
                            uvs.Add(new Vector2(i / (us.Length - 1f), j / (vs.Length - 1f)));
                        }
                    }

                    for (int j = 0; j < vs.Length - 1; j++)
                    {
                        for (int i = 0; i < us.Length - 1; i++)
                        {
                            int a = start + j * us.Length + i;
                            int b = a + 1;
                            int c = a + us.Length;
                            int d = c + 1;
                            AddTriangle(triangles, vertices, normals, a, b, c);
                            AddTriangle(triangles, vertices, normals, b, d, c);
                        }
                    }
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // Unity front faces: cross(b - a, c - a) points out of the surface.
        private static void AddTriangle(List<int> triangles, List<Vector3> vertices, List<Vector3> normals, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 outward = normals[a] + normals[b] + normals[c];
            if (Vector3.Dot(face, outward) < 0f)
                (b, c) = (c, b);

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static GameObject BuildSoap(Mesh mesh, Material soap, Material emboss, Sprite icon, GameObject dissolvePuff)
        {
            var root = new GameObject("Soap");
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = soap;

            // A raised oval on top, like the pressed logo of a real bar.
            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "Emboss";
            Object.DestroyImmediate(top.GetComponent<Collider>());
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, SoapSize.y * 0.5f - 0.002f, 0f);
            top.transform.localScale = new Vector3(SoapSize.x * 0.62f, 0.012f, SoapSize.z * 0.55f);
            top.GetComponent<MeshRenderer>().sharedMaterial = emboss;
            top.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var box = root.AddComponent<BoxCollider>();
            box.size = SoapSize;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var item = root.AddComponent<PickableItem>();
            var itemSo = new SerializedObject(item);
            itemSo.FindProperty("_icon").objectReferenceValue = icon;
            itemSo.FindProperty("_heldRotation").vector3Value = new Vector3(0f, 25f, 0f);
            itemSo.FindProperty("_heldScale").floatValue = 1f;
            itemSo.ApplyModifiedPropertiesWithoutUndo();

            var soapItem = root.AddComponent<SoapItem>();
            var soapSo = new SerializedObject(soapItem);
            soapSo.FindProperty("_dissolveEffect").objectReferenceValue = dissolvePuff.GetComponent<ParticleSystem>();
            soapSo.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ───────────── Icon ─────────────

        /// <summary>Inventory icon drawn in code: a pink bar of soap at an angle with a highlight and three bubbles.</summary>
        private static Sprite LoadOrCreateIcon()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (sprite != null)
                return sprite;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var outline = new Color(0.45f, 0.12f, 0.3f, 1f);
            var body = new Color(1f, 0.6f, 0.76f, 1f);
            var shade = new Color(0.9f, 0.42f, 0.6f, 1f);
            var light = new Color(1f, 0.88f, 0.94f, 1f);
            var bubbleTint = new Color(0.8f, 0.93f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.one; // -1..1
                    Color color = Color.clear;

                    // Soap bar: rounded rectangle turned by -18°.
                    Vector2 r = Rotate(p - new Vector2(-0.05f, -0.12f), 18f);
                    float bar = RoundedRect(r, new Vector2(0.62f, 0.34f), 0.16f);
                    if (bar < 0.045f)
                        color = outline;
                    if (bar < 0f)
                    {
                        color = Color.Lerp(body, shade, Mathf.Clamp01(-r.y * 1.6f + 0.2f));
                        float emboss = RoundedRect(r - new Vector2(0f, 0.04f), new Vector2(0.36f, 0.15f), 0.13f);
                        if (emboss < 0f)
                            color = Color.Lerp(color, light, 0.75f);
                        if (Mathf.Abs(emboss) < 0.02f)
                            color = Color.Lerp(color, shade, 0.6f);
                    }

                    // Three bubbles up and to the right.
                    Blend(ref color, Bubble(p, new Vector2(0.55f, 0.55f), 0.2f, bubbleTint));
                    Blend(ref color, Bubble(p, new Vector2(0.18f, 0.72f), 0.12f, bubbleTint));
                    Blend(ref color, Bubble(p, new Vector2(0.78f, 0.18f), 0.09f, bubbleTint));
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(IconPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        }

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(p.x * Mathf.Cos(a) - p.y * Mathf.Sin(a), p.x * Mathf.Sin(a) + p.y * Mathf.Cos(a));
        }

        // Signed distance to a rounded rectangle (negative inside).
        private static float RoundedRect(Vector2 p, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static Color Bubble(Vector2 p, Vector2 center, float radius, Color tint)
        {
            float d = (p - center).magnitude / radius;
            if (d > 1f)
                return Color.clear;

            float ring = Mathf.SmoothStep(0.72f, 0.95f, d) * (1f - Mathf.SmoothStep(0.95f, 1f, d));
            float glint = 1f - Mathf.SmoothStep(0f, 0.3f, (p - center - new Vector2(-0.35f, 0.35f) * radius).magnitude / radius);
            float alpha = Mathf.Clamp01(0.15f + ring * 0.85f + glint);
            Color film = Color.Lerp(tint, new Color(1f, 0.8f, 0.95f), Mathf.Clamp01((p.x - center.x) / radius * 0.5f + 0.5f));
            return new Color(Mathf.Min(1f, film.r + glint), Mathf.Min(1f, film.g + glint), Mathf.Min(1f, film.b + glint), alpha);
        }

        private static void Blend(ref Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 0f)
                return;

            Color rgb = (over * over.a + under * under.a * (1f - over.a)) / a;
            under = new Color(rgb.r, rgb.g, rgb.b, a);
        }

        // ───────────── Sounds ─────────────

        private static AudioClip LoadOrCreateSound(string name, System.Func<float[]> build)
        {
            string path = $"{AudioFolder}/{name}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                return clip;

            WriteWav(path, build(), SampleRate);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private const int SampleRate = 44100;

        /// <summary>Soft rising "bloop" while the bubble grows (0.4 s).</summary>
        private static float[] BuildFormSound()
        {
            int count = (int)(SampleRate * 0.4f);
            var samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(220f, 640f, t * t);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t) * (1f - t * 0.4f);
                samples[i] = Mathf.Sin(phase) * envelope * 0.45f;
            }

            return samples;
        }

        /// <summary>Short wet "pop": a falling chirp with a tiny noise click (0.18 s).</summary>
        private static float[] BuildPopSound()
        {
            int count = (int)(SampleRate * 0.18f);
            var samples = new float[count];
            var random = new System.Random(7);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(1400f, 300f, Mathf.Sqrt(t));
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float envelope = Mathf.Exp(-t * 14f);
                float click = i < SampleRate * 0.006f ? (float)(random.NextDouble() * 2.0 - 1.0) * 0.6f : 0f;
                samples[i] = (Mathf.Sin(phase) * 0.7f + click) * envelope;
            }

            return samples;
        }

        private static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int dataSize = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)1); // mono
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                foreach (float sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        // ───────────── Effects (transparent bubble particles, no collision with gameplay) ─────────────

        /// <summary>World-space bubbles the stream view emits along the arc by script.</summary>
        private static GameObject BuildStreamBubbles(Material material)
        {
            ParticleSystem particles = CreateParticles("SoapStreamBubbles", material, 400);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = true;
            main.gravityModifier = -0.03f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            Wander(particles, 0.25f);
            FadeInOut(particles);
            return particles.gameObject;
        }

        /// <summary>Looping foam of bubbles where soapy water hits; emitted along +Z (the surface normal).</summary>
        private static GameObject BuildImpactBubbles(Material material)
        {
            ParticleSystem particles = CreateParticles("SoapImpactBubbles", material, 250);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.gravityModifier = -0.04f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 25f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.15f;
            Wander(particles, 0.4f);
            FadeInOut(particles);
            return particles.gameObject;
        }

        /// <summary>
        /// The pop of the big bubble: droplets fly off the shell and fall, a few small bubbles drift away.
        /// Sized for a 1 m bubble; <see cref="SoapBubbleView"/> scales it to the bubble radius. Removes itself.
        /// </summary>
        private static GameObject BuildBurst(Material material)
        {
            ParticleSystem droplets = CreateParticles("SoapBubbleBurst", material, 80);
            OneShot(droplets, 60);
            ParticleSystem.MainModule main = droplets.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.gravityModifier = 1f;
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
            ParticleSystem.ShapeModule shape = droplets.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            shape.radiusThickness = 0f;
            FadeInOut(droplets);

            ParticleSystem bubbles = CreateParticles("Bubbles", material, 30);
            bubbles.transform.SetParent(droplets.transform, false);
            OneShot(bubbles, 18);
            ParticleSystem.MainModule bubbleMain = bubbles.main;
            bubbleMain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            bubbleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            bubbleMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            bubbleMain.gravityModifier = -0.02f;
            ParticleSystem.ShapeModule bubbleShape = bubbles.shape;
            bubbleShape.shapeType = ParticleSystemShapeType.Sphere;
            bubbleShape.radius = 1f;
            bubbleShape.radiusThickness = 0f;
            Wander(bubbles, 0.3f);
            FadeInOut(bubbles);
            return droplets.gameObject;
        }

        /// <summary>Puff of bubbles where a bar of soap dissolves. Removes itself.</summary>
        private static GameObject BuildDissolvePuff(Material material)
        {
            ParticleSystem particles = CreateParticles("SoapDissolvePuff", material, 60);
            OneShot(particles, 0);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 1.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.gravityModifier = -0.05f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 30f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Wander(particles, 0.3f);
            FadeInOut(particles);
            return particles.gameObject;
        }

        /// <summary>Looping bubbles over a soapy reservoir; a 1 x 1 m box, resized to each reservoir.</summary>
        private static GameObject BuildSurfaceBubbles(Material material)
        {
            ParticleSystem particles = CreateParticles("SoapSurfaceBubbles", material, 120);
            ParticleSystem.MainModule main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
            main.gravityModifier = -0.01f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 12f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, 0.02f, 1f);
            Wander(particles, 0.15f);
            FadeInOut(particles);
            return particles.gameObject;
        }

        /// <summary>
        /// The big bubble around a character: a separate shell (unit sphere, scaled by the view) plus a few small
        /// bubbles drifting off its surface. No colliders.
        /// </summary>
        private static GameObject BuildCharacterBubble(Material shellMaterial, Material particle, GameObject burst,
            AudioClip formSound, AudioClip popSound)
        {
            var root = new GameObject("SoapBubble");

            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Shell";
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(root.transform, false);
            var shellRenderer = shell.GetComponent<MeshRenderer>();
            shellRenderer.sharedMaterial = shellMaterial;
            shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shellRenderer.receiveShadows = false;

            ParticleSystem sparkles = CreateParticles("Sparkles", particle, 40);
            sparkles.transform.SetParent(root.transform, false);
            ParticleSystem.MainModule main = sparkles.main;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.gravityModifier = -0.01f;
            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            shape.radiusThickness = 0f;
            Wander(sparkles, 0.2f);
            FadeInOut(sparkles);

            var view = root.AddComponent<SoapBubbleView>();
            var so = new SerializedObject(view);
            so.FindProperty("_shell").objectReferenceValue = shell.transform;
            so.FindProperty("_sparkles").objectReferenceValue = sparkles;
            so.FindProperty("_burstEffect").objectReferenceValue = burst.GetComponent<ParticleSystem>();
            so.FindProperty("_formSound").objectReferenceValue = formSound;
            so.FindProperty("_burstSound").objectReferenceValue = popSound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static ParticleSystem CreateParticles(string name, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.startColor = new Color(1f, 1f, 1f, 0.9f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -5f; // in front of the water stream and the big bubble
            return particles;
        }

        private static void OneShot(ParticleSystem particles, short burst)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            if (burst > 0)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });
        }

        // Bubbles never fly straight: a soft noise makes them wobble.
        private static void Wander(ParticleSystem particles, float strength)
        {
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        // Quick fade in, hold, then a fast fade and shrink at the end (the bubble pops).
        private static void FadeInOut(ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(0.85f, 1.05f), new Keyframe(1f, 0.2f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        // ───────────── Teammate ─────────────

        /// <summary>
        /// AI teammate: the original Firefighter prefab as an untouched child, on a root with a capsule body,
        /// gravity, the soap bubble controller and the receiver of soapy hits. The bubble is spawned at runtime
        /// under "BubbleEffectAnchor", next to (never instead of) the model.
        /// </summary>
        private static GameObject BuildTeammate(GameObject bubblePrefab)
        {
            var root = new GameObject(TeammateSceneName);
            int targetLayer = LayerMask.NameToLayer("WaterTarget");
            root.layer = targetLayer >= 0 ? targetLayer : 0;

            var firefighter = AssetDatabase.LoadAssetAtPath<GameObject>(FirefighterPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(firefighter, root.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds bounds = MeasureRenderers(model, root.transform.position);

            var character = root.AddComponent<CharacterController>();
            float height = Mathf.Max(1f, bounds.size.y);
            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.25f, 0.45f);
            character.height = height;
            character.radius = radius;
            character.center = new Vector3(0f, bounds.min.y + height * 0.5f + character.skinWidth, 0f);
            character.stepOffset = Mathf.Min(0.3f, height * 0.5f);

            var anchor = new GameObject("BubbleEffectAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, bounds.center.y, 0f);

            var motor = root.AddComponent<TeammateMotor>();

            var controller = root.AddComponent<SoapBubbleController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bubblePrefab").objectReferenceValue = bubblePrefab.GetComponent<SoapBubbleView>();
            so.FindProperty("_anchor").objectReferenceValue = anchor;
            so.FindProperty("_obstacleMask").intValue = ObstacleMask();
            SerializedProperty suspend = so.FindProperty("_suspendWhileCarried");
            suspend.arraySize = 1;
            suspend.GetArrayElementAtIndex(0).objectReferenceValue = motor;
            so.ApplyModifiedPropertiesWithoutUndo();

            var receiver = root.AddComponent<SoapBubbleReceiver>();
            var receiverSo = new SerializedObject(receiver);
            receiverSo.FindProperty("_controller").objectReferenceValue = controller;
            receiverSo.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Everything solid: not water volumes, fire, rooms, wind zones, UI or ignore-raycast.</summary>
        private static int ObstacleMask()
        {
            int mask = ~0;
            foreach (string layer in new[] { "TransparentFX", "Ignore Raycast", "Water", "UI", "FlowInfluence", "Fire", "Room" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                    mask &= ~(1 << index);
            }

            return mask;
        }

        /// <summary>Bounds of the visible meshes, relative to <paramref name="origin"/>.</summary>
        private static Bounds MeasureRenderers(GameObject target, Vector3 origin)
        {
            bool found = false;
            var bounds = new Bounds(Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.4f));
            foreach (Renderer part in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!(part is MeshRenderer || part is SkinnedMeshRenderer))
                    continue;

                var local = new Bounds(part.bounds.center - origin, part.bounds.size);
                if (!found)
                    bounds = local;
                else
                    bounds.Encapsulate(local);
                found = true;
            }

            return bounds;
        }

        // ───────────── Nozzle ─────────────

        private static void SetupNozzle(SoapAssets assets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NozzlePath);
            try
            {
                var stream = root.GetComponentInChildren<WaterStream>(true);
                if (stream == null || root.GetComponentInChildren<SoapBubbleStreamView>(true) != null)
                    return;

                var splash = root.GetComponentInChildren<WaterStreamParticleView>(true);
                GameObject host = splash != null ? splash.gameObject : stream.gameObject;

                var streamBubbles = (GameObject)PrefabUtility.InstantiatePrefab(assets.StreamBubbles, host.transform);
                streamBubbles.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var impactBubbles = (GameObject)PrefabUtility.InstantiatePrefab(assets.ImpactBubbles, host.transform);
                impactBubbles.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                var view = host.AddComponent<SoapBubbleStreamView>();
                var so = new SerializedObject(view);
                so.FindProperty("_stream").objectReferenceValue = stream;
                so.FindProperty("_streamBubbles").objectReferenceValue = streamBubbles.GetComponent<ParticleSystem>();
                so.FindProperty("_impactBubbles").objectReferenceValue = impactBubbles.GetComponent<ParticleSystem>();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, NozzlePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ───────────── Scene ─────────────

        /// <summary>
        /// Puts a bar of soap at the player's feet and a teammate a few meters ahead, on open ground with room above.
        /// In batch mode opens and saves the Mini Village scene; in the editor works on the open scene, which you save afterwards.
        /// </summary>
        private static void SetupScene(SoapAssets assets)
        {
            Scene scene = Application.isBatchMode
                ? EditorSceneManager.OpenScene(MiniVillage.MiniVillageBuilder.ScenePath)
                : SceneManager.GetActiveScene();

            // Edit-mode queries only see colliders after their transforms are synced.
            Physics.SyncTransforms();
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                SetupSoapyReservoirs(scene, assets);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.LogWarning($"Soap: no player in {scene.name}; place the Soap and Teammate_Firefighter prefabs by hand.");
                return;
            }

            Transform playerTransform = player.transform;
            Vector3 forward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized;
            if (forward == Vector3.zero)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            if (FindRoot(scene, SoapSceneName) == null)
            {
                Vector3 spot = Ground(playerTransform.position + forward * 1.5f + right * 0.6f) + Vector3.up * (SoapSize.y * 0.5f + 0.02f);
                var soap = (GameObject)PrefabUtility.InstantiatePrefab(assets.Soap, scene);
                soap.name = SoapSceneName;
                soap.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(right));
            }

            if (FindRoot(scene, TeammateSceneName) == null)
            {
                Vector3 spot = FindOpenSpot(playerTransform.position, forward, right);
                var teammate = (GameObject)PrefabUtility.InstantiatePrefab(assets.Teammate, scene);
                teammate.name = TeammateSceneName;
                teammate.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(-forward));
            }

            int reservoirs = SetupSoapyReservoirs(scene, assets);

            EditorSceneManager.MarkSceneDirty(scene);
            if (Application.isBatchMode)
                EditorSceneManager.SaveScene(scene);
            Debug.Log($"Soap: soap and teammate placed in {scene.name}; soap bubbles added to {reservoirs} reservoir(s).");
        }

        /// <summary>Every water body in the scene gets bubbles over its surface while it is soapy.</summary>
        private static int SetupSoapyReservoirs(Scene scene, SoapAssets assets)
        {
            // Visible water of each reservoir, as moved by its level or surface view.
            var looks = new Dictionary<WaterReservoirComponent, Renderer>();
            foreach (MonoBehaviour view in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(view is ReservoirLevelView || view is ReservoirSurfaceView) || view.gameObject.scene != scene)
                    continue;

                var so = new SerializedObject(view);
                var reservoir = so.FindProperty("_reservoir").objectReferenceValue as WaterReservoirComponent;
                SerializedProperty lookProperty = so.FindProperty(view is ReservoirLevelView ? "_water" : "_surface");
                var look = lookProperty != null ? lookProperty.objectReferenceValue as Transform : null;
                Renderer renderer = look != null ? look.GetComponentInChildren<Renderer>(true) : null;
                if (reservoir != null && renderer != null)
                    looks[reservoir] = renderer;
            }

            int added = 0;
            foreach (WaterBody body in Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (body.gameObject.scene != scene || body.GetComponentInChildren<SoapyReservoirView>(true) != null)
                    continue;

                var box = body.GetComponent<BoxCollider>();
                Bounds bounds = box.bounds;
                var bubbles = (GameObject)PrefabUtility.InstantiatePrefab(assets.SurfaceBubbles, body.transform);
                bubbles.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), Quaternion.identity);
                bubbles.transform.localScale = Vector3.one;
                ParticleSystem particles = bubbles.GetComponent<ParticleSystem>();
                ParticleSystem.ShapeModule shape = particles.shape;
                // The shape is scaled by the reservoir's own scale too: divide it out.
                Vector3 parentScale = bubbles.transform.lossyScale;
                shape.scale = new Vector3(bounds.size.x * 0.85f / Mathf.Max(parentScale.x, 0.001f), 0.02f,
                    bounds.size.z * 0.85f / Mathf.Max(parentScale.z, 0.001f));
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = Mathf.Clamp(bounds.size.x * bounds.size.z * 1.5f, 6f, 60f);

                var reservoirComponent = body.GetComponent<WaterReservoirComponent>();
                var view = body.gameObject.AddComponent<SoapyReservoirView>();
                var viewSo = new SerializedObject(view);
                viewSo.FindProperty("_reservoir").objectReferenceValue = reservoirComponent;
                viewSo.FindProperty("_bubbles").objectReferenceValue = particles;
                looks.TryGetValue(reservoirComponent, out Renderer surface);
                viewSo.FindProperty("_surface").objectReferenceValue = surface;
                viewSo.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }

            return added;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        /// <summary>First spot ahead of the player with ground below and at least 10 m of free sky above.</summary>
        private static Vector3 FindOpenSpot(Vector3 origin, Vector3 forward, Vector3 right)
        {
            int mask = ObstacleMask();
            float[] distances = { 5f, 7f, 4f, 9f, 11f };
            float[] sides = { 0f, 2f, -2f, 4f, -4f };
            foreach (float distance in distances)
            {
                foreach (float side in sides)
                {
                    Vector3 spot = Ground(origin + forward * distance + right * side);
                    bool open = !Physics.SphereCast(spot + Vector3.up * 1f, 0.6f, Vector3.up, out _, 10f, mask, QueryTriggerInteraction.Ignore);
                    if (open)
                        return spot;
                }
            }

            return Ground(origin + forward * 5f);
        }

        private static Vector3 Ground(Vector3 point)
        {
            Vector3 top = new Vector3(point.x, point.y + 3f, point.z);
            return Physics.Raycast(top, Vector3.down, out RaycastHit hit, 20f, ObstacleMask(), QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }
    }
}
