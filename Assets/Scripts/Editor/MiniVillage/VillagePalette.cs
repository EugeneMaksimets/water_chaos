using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>
    /// Shared materials of the mini village: flat cartoon colours on URP Lit, some with a small
    /// generated greyscale pattern (siding boards, bricks, shingles, planks, tiles...) tinted by the colour.
    /// One material per key, reused by every model.
    /// </summary>
    internal static class VillagePalette
    {
        public const string Root = "Assets/WaterChaos/Environment";
        private const string MaterialFolder = Root + "/Materials";
        private const string TextureFolder = Root + "/Textures";
        private const int TextureSize = 128;

        private struct Entry
        {
            public Color Color;
            public string Texture;
            public float Tiling;
            public float Smoothness;
            public float Metallic;
            public Color Emission;
            public bool Transparent;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        static VillagePalette()
        {
            // Walls and roofs.
            Add("SidingBeige", new Color(0.86f, 0.74f, 0.52f), "Siding");
            Add("SidingCream", new Color(0.93f, 0.86f, 0.70f), "Siding");
            Add("SidingBlue", new Color(0.56f, 0.69f, 0.80f), "Siding");
            Add("SidingMint", new Color(0.64f, 0.80f, 0.66f), "Siding");
            Add("Brick", new Color(0.70f, 0.36f, 0.27f), "Brick");
            Add("BrickDark", new Color(0.52f, 0.27f, 0.22f), "Brick");
            Add("Trim", new Color(0.96f, 0.94f, 0.89f));
            Add("ShutterBrown", new Color(0.46f, 0.24f, 0.16f), "Planks", 2f);
            Add("ShutterGreen", new Color(0.24f, 0.44f, 0.32f), "Planks", 2f);
            Add("Roof", new Color(0.38f, 0.37f, 0.43f), "Shingles");
            Add("RoofDark", new Color(0.24f, 0.27f, 0.34f), "Shingles");
            Add("RoofRed", new Color(0.64f, 0.30f, 0.23f), "Shingles");
            Add("Foundation", new Color(0.58f, 0.55f, 0.52f), "Concrete");
            Add("InteriorWall", new Color(0.88f, 0.82f, 0.72f));
            Add("InteriorWallBlue", new Color(0.68f, 0.76f, 0.80f));
            Add("Wainscot", new Color(0.62f, 0.34f, 0.27f));
            Add("Ceiling", new Color(0.93f, 0.91f, 0.86f));
            Add("WoodFloor", new Color(0.62f, 0.44f, 0.29f), "Planks");
            Add("WoodDark", new Color(0.42f, 0.27f, 0.18f), "Planks");
            Add("WoodLight", new Color(0.78f, 0.60f, 0.40f), "Planks");
            Add("Door", new Color(0.50f, 0.28f, 0.19f), "Planks", 2f);
            Add("DoorRed", new Color(0.72f, 0.22f, 0.18f), "Planks", 2f);

            // Fabric and decoration.
            Add("FabricOrange", new Color(0.76f, 0.41f, 0.23f), "Noise");
            Add("FabricTeal", new Color(0.19f, 0.47f, 0.50f), "Noise");
            Add("FabricCream", new Color(0.92f, 0.87f, 0.74f), "Noise");
            Add("FabricRed", new Color(0.80f, 0.24f, 0.22f), "Noise");
            Add("Rug", new Color(0.20f, 0.46f, 0.52f), "Noise");
            Add("Book1", new Color(0.25f, 0.40f, 0.62f));
            Add("Book2", new Color(0.70f, 0.30f, 0.25f));
            Add("Book3", new Color(0.35f, 0.56f, 0.36f));
            Add("Ceramic", new Color(0.24f, 0.38f, 0.62f), null, 1f, 0.6f);
            Add("CeramicPink", new Color(0.78f, 0.45f, 0.45f), null, 1f, 0.6f);
            Add("Screen", new Color(0.07f, 0.09f, 0.12f), null, 1f, 0.85f);
            Add("Cardboard", new Color(0.77f, 0.61f, 0.42f), "Noise");

            // Streets and ground.
            Add("Asphalt", new Color(0.29f, 0.29f, 0.32f), "Asphalt", 0.5f);
            Add("RoadYellow", new Color(0.97f, 0.80f, 0.24f));
            Add("RoadWhite", new Color(0.95f, 0.95f, 0.92f));
            Add("Sidewalk", new Color(0.74f, 0.73f, 0.70f), "Concrete", 0.5f);
            Add("Curb", new Color(0.82f, 0.81f, 0.78f));
            Add("Concrete", new Color(0.80f, 0.79f, 0.76f), "Concrete", 0.25f);
            Add("Grass", new Color(0.47f, 0.68f, 0.33f), "Grass", 0.2f);
            Add("GrassDark", new Color(0.37f, 0.57f, 0.28f), "Grass", 0.2f);
            Add("Hill", new Color(0.50f, 0.68f, 0.35f), "Grass", 0.1f);
            Add("Soil", new Color(0.44f, 0.31f, 0.21f), "Noise");
            Add("Stone", new Color(0.62f, 0.62f, 0.60f), "Noise");
            Add("Leaves", new Color(0.38f, 0.62f, 0.29f));
            Add("LeavesDark", new Color(0.27f, 0.50f, 0.25f));
            Add("Trunk", new Color(0.46f, 0.31f, 0.21f));
            Add("FlowerPink", new Color(0.96f, 0.52f, 0.66f));
            Add("FlowerYellow", new Color(0.99f, 0.84f, 0.27f));
            Add("FlowerWhite", new Color(0.97f, 0.97f, 0.95f));
            Add("Picket", new Color(0.96f, 0.95f, 0.91f), "Planks", 1f);

            // Pools.
            Add("PoolTile", new Color(0.32f, 0.64f, 0.88f), "Tiles");
            Add("PoolRim", new Color(0.24f, 0.50f, 0.86f));
            Add("Coping", new Color(0.92f, 0.92f, 0.88f), "Concrete", 1f);

            // Plastics and metals.
            Add("PlasticRed", new Color(0.90f, 0.22f, 0.19f), null, 1f, 0.45f);
            Add("PlasticBlue", new Color(0.22f, 0.47f, 0.88f), null, 1f, 0.45f);
            Add("PlasticYellow", new Color(0.99f, 0.81f, 0.21f), null, 1f, 0.45f);
            Add("PlasticGreen", new Color(0.31f, 0.72f, 0.37f), null, 1f, 0.45f);
            Add("PlasticWhite", new Color(0.96f, 0.96f, 0.95f), null, 1f, 0.45f);
            Add("PlasticOrange", new Color(0.99f, 0.52f, 0.16f), null, 1f, 0.45f);
            Add("PlasticPink", new Color(0.96f, 0.52f, 0.67f), null, 1f, 0.45f);
            Add("Metal", new Color(0.68f, 0.70f, 0.73f), null, 1f, 0.6f, 0.4f);
            Add("MetalDark", new Color(0.26f, 0.27f, 0.30f), null, 1f, 0.5f, 0.3f);
            Add("Rubber", new Color(0.12f, 0.12f, 0.13f));
            Add("Black", new Color(0.10f, 0.10f, 0.12f));

            // Gas station and cars.
            Add("GasRed", new Color(0.84f, 0.17f, 0.15f), null, 1f, 0.35f);
            Add("GasWhite", new Color(0.96f, 0.96f, 0.94f), null, 1f, 0.35f);
            Add("GasYellow", new Color(0.99f, 0.78f, 0.15f), null, 1f, 0.35f);
            Add("CarRed", new Color(0.80f, 0.20f, 0.18f), null, 1f, 0.6f);
            Add("CarBlue", new Color(0.24f, 0.44f, 0.78f), null, 1f, 0.6f);
            Add("CarYellow", new Color(0.96f, 0.77f, 0.22f), null, 1f, 0.6f);

            // Office district: brick city blocks, offices and the toxic factory.
            Add("BrickRed", new Color(0.66f, 0.34f, 0.25f), "Brick");
            Add("BrickBrown", new Color(0.50f, 0.31f, 0.25f), "Brick");
            Add("BrickGray", new Color(0.46f, 0.48f, 0.53f), "Brick");
            Add("Stucco", new Color(0.84f, 0.76f, 0.62f), "Concrete", 0.5f);
            Add("OfficeWall", new Color(0.90f, 0.89f, 0.84f));
            Add("OfficeCarpet", new Color(0.34f, 0.40f, 0.48f), "Noise");
            Add("OfficeCarpetGreen", new Color(0.33f, 0.45f, 0.38f), "Noise");
            Add("Linoleum", new Color(0.70f, 0.68f, 0.62f), "Tiles");
            Add("Steel", new Color(0.52f, 0.55f, 0.58f), null, 1f, 0.55f, 0.5f);
            Add("SteelDark", new Color(0.30f, 0.32f, 0.35f), null, 1f, 0.5f, 0.4f);
            Add("Corrugated", new Color(0.50f, 0.58f, 0.55f), "Siding");
            Add("HazardYellow", new Color(0.98f, 0.78f, 0.12f), null, 1f, 0.4f);
            Add("ToxicDrum", new Color(0.30f, 0.55f, 0.20f), null, 1f, 0.45f);
            Add("Whiteboard", new Color(0.97f, 0.97f, 0.96f), null, 1f, 0.7f);
            AddEmissive("ToxicGlow", new Color(0.5f, 1f, 0.2f), new Color(0.35f, 0.9f, 0.1f) * 1.5f);

            // Light, glass.
            AddEmissive("Lamp", new Color(1f, 0.87f, 0.6f), new Color(1f, 0.82f, 0.5f) * 2.2f);
            AddEmissive("LampCool", new Color(0.92f, 0.95f, 1f), new Color(0.9f, 0.95f, 1f) * 1.6f);
            AddEmissive("WindowGlow", new Color(1f, 0.86f, 0.55f), new Color(1f, 0.75f, 0.4f) * 0.8f);
            Entries["Glass"] = new Entry { Color = new Color(0.62f, 0.84f, 0.95f, 0.35f), Smoothness = 0.92f, Transparent = true, Tiling = 1f };
            Add("GlassCrack", new Color(0.95f, 0.97f, 1f), null, 1f, 0.8f);
        }

        /// <summary>Material for the key, created (or refreshed) as an asset on first use.</summary>
        public static Material Get(string key)
        {
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            if (!Entries.TryGetValue(key, out Entry entry))
            {
                Debug.LogError("Mini village: unknown material key " + key);
                entry = Entries["Trim"];
            }

            string path = $"{MaterialFolder}/M_{key}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            Setup(material, entry);
            EditorUtility.SetDirty(material);
            Cache[key] = material;
            return material;
        }

        public static Material[] Get(string[] keys)
        {
            var result = new Material[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                result[i] = Get(keys[i]);
            return result;
        }

        /// <summary>Creates folders and textures; call once before building models.</summary>
        public static void Prepare()
        {
            Cache.Clear();
            Textures.Clear();
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(TextureFolder);
            AssetDatabase.Refresh();
        }

        private static void Add(string key, Color color, string texture = null, float tiling = 1f,
            float smoothness = 0.12f, float metallic = 0f)
        {
            Entries[key] = new Entry
            {
                Color = color, Texture = texture, Tiling = tiling, Smoothness = smoothness, Metallic = metallic,
                Emission = Color.black
            };
        }

        private static void AddEmissive(string key, Color color, Color emission)
        {
            Entries[key] = new Entry { Color = color, Emission = emission, Smoothness = 0.3f, Tiling = 1f };
        }

        private static void Setup(Material material, Entry entry)
        {
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", entry.Color);
            material.SetFloat("_Smoothness", entry.Smoothness);
            material.SetFloat("_Metallic", entry.Metallic);

            Texture2D texture = entry.Texture != null ? GetTexture(entry.Texture) : null;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one * entry.Tiling);

            if (entry.Emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", entry.Emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            if (entry.Transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
            }
        }

        // ───────────── Generated greyscale patterns ─────────────

        private static Texture2D GetTexture(string name)
        {
            if (Textures.TryGetValue(name, out Texture2D cached) && cached != null)
                return cached;

            string path = $"{TextureFolder}/T_{name}.png";
            var random = new System.Random(name.GetHashCode());
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false);
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float value = Pattern(name, x, y, random);
                    texture.SetPixel(x, y, new Color(value, value, value));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Textures[name] = asset;
            return asset;
        }

        private static float Pattern(string name, int x, int y, System.Random random)
        {
            const int s = TextureSize;
            float grain = (float)random.NextDouble() * 0.05f;
            switch (name)
            {
                case "Siding":
                {
                    // 8 horizontal boards per meter, a shadow line under each.
                    int row = y % (s / 8);
                    return row < 2 ? 0.66f : 0.9f + row * 0.006f - grain;
                }
                case "Brick":
                {
                    int rowHeight = s / 8;
                    int row = y / rowHeight;
                    int offset = row % 2 == 0 ? 0 : s / 8;
                    bool mortar = y % rowHeight < 2 || (x + offset) % (s / 4) < 2;
                    return mortar ? 1.25f : 0.86f + ((row * 7 + (x + offset) / (s / 4) * 3) % 5) * 0.03f - grain;
                }
                case "Shingles":
                {
                    int rowHeight = s / 6;
                    int row = y / rowHeight;
                    int offset = row % 2 == 0 ? 0 : s / 10;
                    bool gap = y % rowHeight < 3 || (x + offset) % (s / 5) < 2;
                    return gap ? 0.62f : 0.88f + (y % rowHeight) * 0.004f - grain;
                }
                case "Planks":
                {
                    int plank = x / (s / 6);
                    bool seam = x % (s / 6) < 2 || ((y + plank * 37) % s) < 2;
                    return seam ? 0.66f : 0.86f + (plank % 3) * 0.05f - grain;
                }
                case "Tiles":
                {
                    bool joint = x % (s / 8) < 2 || y % (s / 8) < 2;
                    return joint ? 1.18f : 0.93f - grain;
                }
                case "Concrete":
                {
                    bool joint = x % (s / 2) < 1 || y % (s / 2) < 1;
                    return joint ? 0.78f : 0.94f - grain * 1.5f;
                }
                case "Asphalt":
                    return random.NextDouble() < 0.04 ? 1.15f : 0.9f - grain * 2f;
                case "Grass":
                {
                    float blot = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    return 0.82f + blot * 0.2f - grain * 2f;
                }
                default:
                    return 0.92f - grain * 2f;
            }
        }
    }
}
