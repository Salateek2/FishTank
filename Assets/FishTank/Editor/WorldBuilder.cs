using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishTank.EditorTools
{
    /// <summary>
    /// Builds the scenery of the whole sea, zone by zone, out of primitives and generated meshes.
    /// The shape of the sea itself (floor, cave, shores) comes from <see cref="World"/>, which the
    /// game rules read too, so what you see is what you bump into.
    ///
    /// The camera watches from the -Z side and the fish swim on Z = 0: low and thin things go in
    /// front, solid things level with the fish, big things behind.
    /// </summary>
    static class WorldBuilder
    {
        const string MaterialDir = "Assets/FishTank/Materials";
        const string PropDir = "Assets/FishTank/Models/Props";

        static System.Random random;
        static Transform root;

        static Material sand, rock, darkRock, coralRock, weed, kelp, wood, darkWood, stone, bone, gold, black, leaf;
        static Material goldGlow, pearlGlow, lavaGlow, glowWeed;
        static Material[] coral, glow;
        static Color[] glowColors;

        public static void Build()
        {
            random = new System.Random(11);
            root = new GameObject("Environment").transform;
            MakeMaterials();

            BuildTerrain();
            BuildIsland();
            BuildSurface();
            BuildBackdrop();

            BuildShallows();
            BuildKelpForest();
            BuildCoralGardens();
            BuildShipwreck();
            BuildRuins();
            BuildCave();
            BuildAbyss();
            BuildBeach();

            // Last, so the random scenery above comes out the same as before.
            BuildLeafBeds();
        }

        // ---------------------------------------------------------------- helpers

        static float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
        static int RI(int min, int maxExclusive) => random.Next(min, maxExclusive);
        static T Pick<T>(T[] items) => items[random.Next(items.Length)];

        /// <summary>A point on the sea bed.</summary>
        static Vector3 G(float x, float z, float lift = 0f) => new Vector3(x, World.Ground(x, z) + lift, z);

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        static Transform Holder(string name, Transform parent, Vector3 position, Vector3 euler = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(euler);
            return t;
        }

        static Transform Prim(PrimitiveType type, Transform parent, Material material, Vector3 position,
            Vector3 scale, Vector3 euler = default)
        {
            Transform t = FishTankSetup.MakePrimitive(type, type.ToString(), parent, material);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = scale;
            return t;
        }

        /// <summary>A stick (capsule or cylinder) that starts at a point and leans away from upright.</summary>
        static Transform Stick(PrimitiveType type, Transform parent, Material material, Vector3 foot,
            float length, float width, Vector3 euler)
        {
            Quaternion q = Quaternion.Euler(euler);
            Transform t = Prim(type, parent, material, foot + q * Vector3.up * (length * 0.5f),
                new Vector3(width, length * 0.5f, width), euler);
            return t;
        }

        static void SolidRock(Transform parent, Material material, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            Transform t = Prim(PrimitiveType.Sphere, parent, material, position, scale, euler);
            t.name = "Rock";
            t.gameObject.AddComponent<Rock>();
        }

        static void GlowLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject("Glow Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- materials

        static void MakeMaterials()
        {
            sand = Lit("Sand", new Color(0.86f, 0.76f, 0.52f));
            rock = Lit("Rock", new Color(0.38f, 0.42f, 0.46f));
            darkRock = Lit("DarkRock", new Color(0.16f, 0.17f, 0.22f));
            coralRock = Lit("CoralRock", new Color(0.62f, 0.45f, 0.48f));
            weed = Lit("Seaweed", new Color(0.16f, 0.55f, 0.28f));
            kelp = Lit("Kelp", new Color(0.3f, 0.42f, 0.12f));
            wood = Lit("Wood", new Color(0.42f, 0.28f, 0.16f));
            darkWood = Lit("DarkWood", new Color(0.2f, 0.14f, 0.1f));
            stone = Lit("Stone", new Color(0.72f, 0.7f, 0.6f));
            bone = Lit("Bone", new Color(0.88f, 0.86f, 0.76f));
            gold = Lit("Gold", new Color(0.95f, 0.72f, 0.15f), 0.7f);
            black = Lit("Black", new Color(0.02f, 0.02f, 0.03f));
            leaf = Lit("PalmLeaf", new Color(0.2f, 0.5f, 0.15f));

            coral = new[]
            {
                Lit("CoralPink", new Color(1f, 0.42f, 0.6f)),
                Lit("CoralOrange", new Color(1f, 0.5f, 0.2f)),
                Lit("CoralPurple", new Color(0.62f, 0.35f, 0.9f)),
                Lit("CoralYellow", new Color(1f, 0.85f, 0.25f)),
                Lit("CoralRed", new Color(0.9f, 0.2f, 0.25f)),
                Lit("CoralTeal", new Color(0.2f, 0.8f, 0.75f)),
                Lit("CoralWhite", new Color(0.95f, 0.9f, 0.85f)),
            };

            glowColors = new[]
            {
                new Color(0.2f, 0.9f, 1f), new Color(1f, 0.3f, 0.9f), new Color(0.55f, 0.4f, 1f),
                new Color(0.3f, 1f, 0.55f),
            };
            glow = new[]
            {
                Glow("GlowCyan", glowColors[0], 3.5f), Glow("GlowMagenta", glowColors[1], 3.5f),
                Glow("GlowViolet", glowColors[2], 3.5f), Glow("GlowGreen", glowColors[3], 3.5f),
            };
            goldGlow = Glow("GoldGlow", new Color(1f, 0.8f, 0.2f), 2.5f);
            pearlGlow = Glow("PearlGlow", new Color(1f, 0.97f, 0.9f), 2.2f);
            lavaGlow = Glow("LavaGlow", new Color(1f, 0.4f, 0.1f), 4f);
            glowWeed = Glow("GlowWeed", new Color(0.1f, 0.8f, 0.7f), 1.6f);
        }

        static Material Lit(string name, Color color, float smoothness = 0.1f)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            FishTankSetup.EnsureFolder(MaterialDir);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A material that shines by itself, so it stays bright in the dark.</summary>
        static Material Glow(string name, Color color, float intensity)
        {
            string path = MaterialDir + "/" + name + ".mat";
            bool isNew = AssetDatabase.LoadAssetAtPath<Material>(path) == null;
            Material material = Lit(name, color * 0.6f);
            if (!isNew) return material;

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>See-through, unlit material for the water surface and light shafts.</summary>
        static Material Clear(string name, Color color)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            FishTankSetup.EnsureFolder(MaterialDir);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------------------------------------------------------------- generated meshes

        static void AddMesh(string name, Transform parent, Material material, Mesh mesh)
        {
            mesh.name = name;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // The sea bed: one sheet that follows World.Ground, far wider and deeper than the swimming area
        // so it runs out into the fog instead of ending at an edge.
        static void BuildTerrain()
        {
            const float x0 = -470f, x1 = 470f, step = 2f;
            float[] zs =
            {
                -160f, -110f, -75f, -50f, -32f, -20f, -13f, -9f, -7f, -5.5f, -4f, -2f, 0f, 2f, 5f, 9f, 14f, 22f,
                32f, 45f, 60f, 80f, 110f, 150f, 210f,
            };

            int nx = Mathf.RoundToInt((x1 - x0) / step) + 1;
            int nz = zs.Length;
            var vertices = new Vector3[nx * nz];
            var triangles = new int[(nx - 1) * (nz - 1) * 6];

            for (int i = 0; i < nx; i++)
            {
                float x = x0 + i * step;
                for (int j = 0; j < nz; j++) vertices[i * nz + j] = G(x, zs[j]);
            }

            int t = 0;
            for (int i = 0; i < nx - 1; i++)
            {
                for (int j = 0; j < nz - 1; j++)
                {
                    int a = i * nz + j, b = a + 1, c = a + nz, d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = c; triangles[t++] = b; triangles[t++] = d;
                }
            }

            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AddMesh("Sea Bed", root, sand, mesh);
        }

        /// <summary>
        /// A faceted slab of rock between a lower and an upper profile, from zFront back to zBack.
        /// Only the faces that can be seen are built: front, underside, top and the two ends.
        /// </summary>
        static Mesh RockSlab(float xMin, float xMax, Func<float, float> lower, Func<float, float> upper,
            float zFront, float zBack)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                // Wind it so the face is visible from the outward side.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    (b, d) = (d, b);
                }

                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }

            const int rows = 6;
            // A point on the rough front face: t = 0 at the lower edge, 1 at the upper edge.
            Vector3 Face(float x, float t)
            {
                float y = Mathf.Lerp(lower(x), upper(x), t);
                float bulge = (Mathf.PerlinNoise(x * 0.23f + 90f, y * 0.23f + 90f) - 0.5f) * 3f;
                return new Vector3(x, y, zFront + bulge);
            }

            int columns = Mathf.CeilToInt(xMax - xMin);
            float step = (xMax - xMin) / columns;
            for (int i = 0; i < columns; i++)
            {
                float a = xMin + i * step, b = a + step;
                for (int r = 0; r < rows; r++)
                {
                    float t0 = r / (float)rows, t1 = (r + 1) / (float)rows;
                    Quad(Face(a, t0), Face(b, t0), Face(b, t1), Face(a, t1), Vector3.back);
                }

                Quad(Face(a, 0f), Face(b, 0f), new Vector3(b, lower(b), zBack), new Vector3(a, lower(a), zBack), Vector3.down);
                Quad(Face(a, 1f), Face(b, 1f), new Vector3(b, upper(b), zBack), new Vector3(a, upper(a), zBack), Vector3.up);
            }

            foreach (float x in new[] { xMin, xMax })
            {
                Vector3 outward = x == xMin ? Vector3.left : Vector3.right;
                for (int r = 0; r < rows; r++)
                {
                    float t0 = r / (float)rows, t1 = (r + 1) / (float)rows;
                    Vector3 p0 = Face(x, t0), p1 = Face(x, t1);
                    Quad(p0, p1, new Vector3(x, p1.y, zBack), new Vector3(x, p0.y, zBack), outward);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The island: solid rock from above the surface down to the roof of the tunnel that runs under
        // it. Seen from the side it is cut away, so you can watch the fish swim through the cave.
        static void BuildIsland()
        {
            Transform g = Group("Island");
            AddMesh("Cave Roof", g, darkRock,
                RockSlab(World.CaveMin, World.CaveMax, World.Ceiling, World.IslandTop, -5f, 5.5f));
            AddMesh("Cave Back", g, darkRock,
                RockSlab(World.CaveMin, World.CaveMax, x => World.Floor(x) - 4f, World.IslandTop, 5f, 80f));

            for (int i = 0; i < 9; i++)
            {
                float x = R(World.CaveMin + 6f, World.CaveMax - 6f);
                Palm(g, new Vector3(x, World.IslandTop(x) - 0.3f, R(0f, 45f)));
            }
        }

        static void BuildSurface()
        {
            Transform g = Group("Surface");
            Material water = Clear("WaterSurface", new Color(0.7f, 0.93f, 1f, 0.38f));
            Prim(PrimitiveType.Plane, g, water, new Vector3(0f, World.TopY, 60f), new Vector3(110f, 1f, 45f),
                new Vector3(180f, 0f, 0f));

            // Shafts of sunlight slanting down through the sunlit zones.
            Material shaft = Clear("LightShaft", new Color(0.85f, 1f, 1f, 0.05f));
            for (float x = World.CaveMax + 6f; x < 232f; x += R(4f, 9f))
            {
                float length = R(22f, 36f);
                var q = Quaternion.Euler(0f, 0f, 16f);
                Vector3 top = new Vector3(x, World.TopY, R(3f, 45f));
                Prim(PrimitiveType.Quad, g, shaft, top - q * Vector3.up * (length * 0.5f),
                    new Vector3(R(1.5f, 5f), length, 1f), new Vector3(0f, 0f, 16f));
            }
        }

        // Far-off rock shapes that loom out of the fog behind everything.
        static void BuildBackdrop()
        {
            Transform g = Group("Backdrop");
            for (int i = 0; i < 46; i++)
            {
                float x = R(-420f, 420f), z = R(75f, 150f);
                float width = R(30f, 70f);
                Prim(PrimitiveType.Sphere, g, rock, G(x, z), new Vector3(width, R(30f, 80f), width * R(0.6f, 1f)),
                    new Vector3(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
            }
        }

        // ---------------------------------------------------------------- shared scenery pieces

        /// <summary>Solid rocks over a stretch of sea bed: small ones everywhere, boulders and spires
        /// level with the fish and behind them.</summary>
        static void ScatterRocks(Transform g, Material material, float xMin, float xMax, int small, int boulders,
            int spires, float spireMin = 0.4f, float spireMax = 0.95f)
        {
            bool Away(float x, float z, float radius) => new Vector2(x, z).magnitude > radius;

            for (int i = 0; i < small; i++)
            {
                float x = R(xMin, xMax), z = R(-45f, 70f);
                if (!Away(x, z, 5f)) continue;
                float s = R(1f, 4.5f);
                SolidRock(g, material, G(x, z), new Vector3(s * R(0.8f, 1.6f), s * R(0.5f, 0.9f), s * R(0.8f, 1.4f)),
                    new Vector3(0f, R(0f, 360f), 0f));
            }

            for (int i = 0; i < boulders; i++)
            {
                float x = R(xMin, xMax), z = R(-2f, 55f);
                if (!Away(x, z, 13f)) continue;
                float width = R(5f, 11f);
                SolidRock(g, material, G(x, z), new Vector3(width, R(10f, 26f), width * R(0.7f, 1.3f)),
                    new Vector3(R(-10f, 10f), R(0f, 360f), R(-10f, 10f)));
            }

            // Spires reach most of the way to the surface. Their near face stays just inside the
            // fish's path, leaving room to slip past in front of them where they can still be seen.
            for (int i = 0; i < spires; i++)
            {
                float x = R(xMin, xMax);
                float width = R(5f, 12f);
                float z = R(width * 0.65f - 1f, 60f);
                if (!Away(x, z, 15f)) continue;
                float height = (World.TopY - World.Floor(x)) * R(spireMin, spireMax);
                SolidRock(g, material, G(x, z), new Vector3(width, height * 2f, width * R(0.7f, 1.3f)),
                    new Vector3(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
            }
        }

        /// <summary>A ring of solid rocks standing on the sea bed with a hole to swim through.</summary>
        static void Arch(Transform g, Material material, float x, float radius, float thickness)
        {
            float baseY = World.Floor(x) - 1f;
            int count = Mathf.CeilToInt(Mathf.PI * radius / (thickness * 0.55f));
            for (int i = 0; i <= count; i++)
            {
                float angle = Mathf.PI * i / count;
                var p = new Vector3(x + Mathf.Cos(angle) * radius, baseY + Mathf.Sin(angle) * radius, R(-0.4f, 0.4f));
                float s = thickness * R(0.9f, 1.25f);
                SolidRock(g, material, p, new Vector3(s, s * R(0.85f, 1.1f), s * R(1f, 1.4f)),
                    new Vector3(R(0f, 40f), R(0f, 360f), 0f));
            }
        }

        /// <summary>A stalk of seaweed that sways. Fish swim straight through it.</summary>
        static void Weed(Transform g, Material material, Vector3 foot, float height, float width)
        {
            Transform holder = Holder("Seaweed", g, foot, new Vector3(0f, R(0f, 360f), 0f));
            Transform stalk = Holder("Stalk", holder, Vector3.zero);
            var sway = stalk.gameObject.AddComponent<Sway>();
            sway.angle = R(5f, 11f) * (height > 9f ? 0.4f : 1f);    // a long stalk swings a long way at the top
            sway.speed = R(0.6f, 1.3f);
            sway.phase = R(0f, 6.28f);
            Prim(PrimitiveType.Capsule, stalk, material, new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height * 0.5f, width * 0.4f));
        }

        static void WeedPatch(Transform g, Material material, float x, float z, float radius, int count, bool tall)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = R(0f, 6.28f), distance = radius * Mathf.Sqrt(R(0f, 1f));
                float px = x + Mathf.Cos(angle) * distance, pz = z + Mathf.Sin(angle) * distance;
                float water = World.TopY - World.Ground(px, pz);
                float height = tall ? Mathf.Clamp(water * R(0.35f, 0.9f), 3f, 38f) : R(2f, 7f);
                Weed(g, material, G(px, pz), height, tall ? 0.9f : 0.5f);
            }
        }

        static void Weeds(Transform g, Material material, float xMin, float xMax, int patches, int forests, int loose)
        {
            for (int i = 0; i < patches; i++)
                WeedPatch(g, material, R(xMin, xMax), R(-45f, 70f), R(3f, 7f), 14, false);
            for (int i = 0; i < forests; i++)
                WeedPatch(g, material, R(xMin, xMax), R(-22f, 60f), R(5f, 11f), 14, true);
            for (int i = 0; i < loose; i++)
                Weed(g, material, G(R(xMin, xMax), R(-45f, 70f)), R(2f, 7f), 0.5f);
        }

        /// <summary>A fan of broad, flat leaves growing from one root, swaying together. The leaves
        /// stay side-on to the camera, so the plant keeps to its own depth and never crosses the
        /// swimming plane.</summary>
        static void LeafPlant(Transform g, Material material, Vector3 foot, float height)
        {
            Transform holder = Holder("Leaf Plant", g, foot, new Vector3(0f, R(-20f, 20f), 0f));
            Transform leaves = Holder("Leaves", holder, Vector3.zero);
            var sway = leaves.gameObject.AddComponent<Sway>();
            sway.angle = R(4f, 9f);
            sway.speed = R(0.6f, 1.2f);
            sway.phase = R(0f, 6.28f);

            int count = RI(4, 7);
            for (int i = 0; i < count; i++)
            {
                float lean = Mathf.Lerp(-42f, 42f, i / (count - 1f)) + R(-8f, 8f);
                float length = height * R(0.65f, 1f) * (1f - Mathf.Abs(lean) / 110f);
                float width = Mathf.Clamp(length * 0.22f, 0.35f, 1.1f);
                var euler = new Vector3(R(-8f, 8f), 0f, lean);
                Prim(PrimitiveType.Sphere, leaves, material, Quaternion.Euler(euler) * Vector3.up * (length * 0.47f),
                    new Vector3(width, length, 0.12f), euler);
            }
        }

        // Beds of leaves along the very bottom of the sea, growing in two rows: a low one just in
        // front of the swimming plane and a taller one just behind it. A fish that hugs the sea bed
        // swims down the lane between them, slipping behind the near leaves and in front of the far ones.
        static void BuildLeafBeds()
        {
            Transform g = Group("Leaf Beds");
            Material seaLeaf = Lit("SeaLeaf", new Color(0.22f, 0.68f, 0.3f));
            Material[] greens = { seaLeaf, seaLeaf, weed, kelp };

            for (float x = -222f; x < 212f; x += R(5f, 11f))
            {
                // Not in the tunnel: its floor is already full of rock teeth and crystals.
                if (x > World.CaveMin - 3f && x < World.CaveMax + 3f) continue;

                // Down in the dark the leaves glow, so the lane can still be seen.
                Material material = x < -139f ? glowWeed : Pick(greens);
                for (int i = RI(3, 6); i > 0; i--)
                {
                    LeafPlant(g, material, G(x + R(-3.5f, 3.5f), R(1.3f, 3.2f), -0.15f), R(3f, 6.5f));
                    LeafPlant(g, material, G(x + R(-3.5f, 3.5f), R(-2.6f, -1.3f), -0.15f), R(1.6f, 3.4f));
                }
            }
        }

        static void Palm(Transform g, Vector3 foot)
        {
            Transform holder = Holder("Palm", g, foot, new Vector3(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)));
            float height = R(6f, 10f);
            Prim(PrimitiveType.Cylinder, holder, wood, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.6f, height * 0.5f, 0.6f));
            for (int i = 0; i < 7; i++)
            {
                var q = Quaternion.Euler(0f, i * 51f, -22f);
                Prim(PrimitiveType.Sphere, holder, leaf, new Vector3(0f, height, 0f) + q * Vector3.right * 2.4f,
                    new Vector3(5f, 0.25f, 1.3f), new Vector3(0f, i * 51f, -22f));
            }
        }

        static void Coral(Transform g, float x, float z, float size)
        {
            Material material = Pick(coral);
            Transform holder = Holder("Coral", g, G(x, z, -0.2f), new Vector3(0f, R(0f, 360f), 0f));

            switch (RI(0, 6))
            {
                case 0:     // branching coral
                    for (int i = RI(5, 9); i > 0; i--)
                        Stick(PrimitiveType.Capsule, holder, material, Vector3.zero, R(1.5f, 4f) * size, 0.4f * size,
                            new Vector3(R(-40f, 40f), 0f, R(-40f, 40f)));
                    break;

                case 1:     // brain coral
                    float w = R(2f, 4.5f) * size;
                    Prim(PrimitiveType.Sphere, holder, material, Vector3.zero, new Vector3(w, w * R(0.6f, 0.85f), w));
                    break;

                case 2:     // tube sponges
                    for (int i = RI(4, 8); i > 0; i--)
                        Stick(PrimitiveType.Cylinder, holder, material,
                            new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f)) * size, R(1.2f, 3.5f) * size,
                            R(0.4f, 0.7f) * size, new Vector3(R(-12f, 12f), 0f, R(-12f, 12f)));
                    break;

                case 3:     // sea fan
                    holder.localRotation = Quaternion.Euler(0f, R(-35f, 35f), 0f);
                    Transform fan = Holder("Fan", holder, Vector3.zero);
                    var sway = fan.gameObject.AddComponent<Sway>();
                    sway.angle = 4f;
                    sway.speed = R(0.6f, 1f);
                    sway.phase = R(0f, 6.28f);
                    float span = R(2.5f, 5f) * size;
                    Prim(PrimitiveType.Cylinder, fan, material, new Vector3(0f, 0.6f, 0f), new Vector3(0.25f, 0.6f, 0.25f));
                    Prim(PrimitiveType.Sphere, fan, material, new Vector3(0f, 1f + span * 0.5f, 0f),
                        new Vector3(span, span * R(0.9f, 1.2f), 0.15f));
                    break;

                case 4:     // table coral
                    float top = R(3f, 6f) * size;
                    Prim(PrimitiveType.Cylinder, holder, material, new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 0.8f, 0.7f));
                    Prim(PrimitiveType.Cylinder, holder, material, new Vector3(0f, 1.7f, 0f), new Vector3(top, 0.15f, top));
                    break;

                default:    // anemone
                    Prim(PrimitiveType.Sphere, holder, Pick(coral), Vector3.zero, new Vector3(1.6f, 1f, 1.6f) * size);
                    for (int i = 0; i < 12; i++)
                    {
                        Transform arm = Holder("Arm", holder, new Vector3(R(-0.5f, 0.5f), 0.3f, R(-0.5f, 0.5f)) * size,
                            new Vector3(0f, R(0f, 360f), 0f));
                        Transform bend = Holder("Bend", arm, Vector3.zero);
                        var wave = bend.gameObject.AddComponent<Sway>();
                        wave.angle = R(10f, 22f);
                        wave.speed = R(1f, 2f);
                        wave.phase = R(0f, 6.28f);
                        float length = R(1f, 2f) * size;
                        Prim(PrimitiveType.Capsule, bend, material, new Vector3(0f, length * 0.5f, 0f),
                            new Vector3(0.16f * size, length * 0.5f, 0.16f * size));
                    }
                    break;
            }
        }

        static void Crystals(Transform g, Vector3 at, bool hanging, float size, int colour, bool light)
        {
            Transform holder = Holder("Crystals", g, at, new Vector3(hanging ? 180f : 0f, R(0f, 360f), 0f));
            for (int i = RI(3, 7); i > 0; i--)
            {
                float length = R(1.2f, 3.6f) * size;
                var euler = new Vector3(R(-35f, 35f), 45f, R(-35f, 35f));
                Prim(PrimitiveType.Cube, holder, glow[colour], Quaternion.Euler(euler) * Vector3.up * (length * 0.4f),
                    new Vector3(0.45f * size, length, 0.45f * size), euler);
            }

            if (light)
                GlowLight(g, at + new Vector3(0f, hanging ? -2f : 2f, -1.5f), glowColors[colour], 14f * size, 13f * size);
        }

        static void Jellyfish(Transform g, Vector3 at, float size)
        {
            Material material = Pick(glow);
            Transform holder = Holder("Jellyfish", g, at);
            var drift = holder.gameObject.AddComponent<Drift>();
            drift.bob = R(1f, 2.5f);
            drift.sway = R(0.5f, 2f);
            drift.speed = R(0.3f, 0.6f);
            drift.phase = R(0f, 6.28f);

            Prim(PrimitiveType.Sphere, holder, material, Vector3.zero, new Vector3(size, size * 0.7f, size));
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 1.05f;
                float length = size * R(0.8f, 1.6f);
                Prim(PrimitiveType.Capsule, holder, material,
                    new Vector3(Mathf.Cos(angle) * size * 0.28f, -size * 0.2f - length * 0.5f, Mathf.Sin(angle) * size * 0.28f),
                    new Vector3(size * 0.07f, length * 0.5f, size * 0.07f));
            }
        }

        static void Pearl(Transform g, float x, float y)
        {
            Transform t = Prim(PrimitiveType.Sphere, g, pearlGlow, new Vector3(x, y, 0f), Vector3.one * 0.75f);
            t.name = "Pearl";
            var treasure = t.gameObject.AddComponent<Treasure>();
            treasure.points = 50;
            treasure.reach = 0.6f;
        }

        /// <summary>A pearl floating somewhere in the water column over this stretch.</summary>
        static void Pearls(Transform g, float xMin, float xMax, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = R(xMin, xMax);
                float low = World.Floor(x) + 2f, high = Mathf.Min(World.TopY, World.Ceiling(x)) - 2f;
                if (high > low) Pearl(g, x, R(low, high));
            }
        }

        static void Chest(Transform g, float x)
        {
            Transform holder = Holder("Treasure Chest", g, new Vector3(x, World.Floor(x) + 1f, 0f));
            Prim(PrimitiveType.Cube, holder, wood, Vector3.zero, new Vector3(2.2f, 1.3f, 1.4f));
            Prim(PrimitiveType.Cube, holder, gold, new Vector3(0f, 0f, 0f), new Vector3(0.35f, 1.36f, 1.46f));
            Prim(PrimitiveType.Cube, holder, gold, new Vector3(0f, 0.6f, 0f), new Vector3(2.3f, 0.2f, 1.5f));
            Prim(PrimitiveType.Sphere, holder, goldGlow, new Vector3(0f, 0.85f, -0.1f), new Vector3(1.7f, 0.8f, 1.1f));
            GlowLight(holder, new Vector3(0f, 2f, -1.5f), new Color(1f, 0.8f, 0.3f), 6f, 8f);

            var treasure = holder.gameObject.AddComponent<Treasure>();
            treasure.points = 500;
            treasure.big = true;
            treasure.spin = false;
            treasure.reach = 1.6f;
        }

        /// <summary>Drops one of the imported models into the scene, turned so its long side runs
        /// left to right and scaled to the wanted length.</summary>
        static Transform PlaceModel(string file, Transform parent, Vector3 position, float length, bool sitOnBottom)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + "/" + file);
            if (model == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            Quaternion turn = bounds.size.z > bounds.size.x ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            float scale = length / Mathf.Max(bounds.size.x, bounds.size.z, 1e-4f);
            var anchor = new Vector3(bounds.center.x, sitOnBottom ? bounds.min.y : bounds.center.y, bounds.center.z);

            Transform holder = Holder(model.name, parent, position);
            Transform pivot = Holder("Pivot", holder, -(turn * anchor) * scale);
            pivot.localRotation = turn;
            pivot.localScale = Vector3.one * scale;
            instance.transform.SetParent(pivot, false);
            return holder;
        }

        // ---------------------------------------------------------------- zones

        // Where the game starts: sand, rocks and weed, with a boat at anchor overhead.
        static void BuildShallows()
        {
            Transform g = Group("Sunny Shallows");
            ScatterRocks(g, rock, -34f, 36f, 55, 9, 8);
            Weeds(g, weed, -36f, 38f, 12, 3, 40);
            Arch(g, rock, -22f, 7f, 3f);

            // The boat floats on the surface with its anchor line running down to the sea bed:
            // something to follow upwards.
            const float boatX = 16f, boatZ = 2.5f;
            PlaceModel("Boat.fbx", g, new Vector3(boatX, World.TopY - 0.7f, boatZ), 11f, true);
            float bottom = World.Ground(boatX + 3f, boatZ);
            float line = World.TopY - bottom;
            Prim(PrimitiveType.Cylinder, g, darkWood, new Vector3(boatX + 3f, bottom + line * 0.5f, boatZ),
                new Vector3(0.12f, line * 0.5f, 0.12f));
            Transform anchor = Holder("Anchor", g, new Vector3(boatX + 3f, bottom + 1.2f, boatZ), new Vector3(0f, 0f, 14f));
            Prim(PrimitiveType.Cylinder, anchor, black, Vector3.zero, new Vector3(0.3f, 1.4f, 0.3f));
            Prim(PrimitiveType.Cube, anchor, black, new Vector3(0f, -1.2f, 0f), new Vector3(2.4f, 0.3f, 0.3f));
            Prim(PrimitiveType.Cube, anchor, black, new Vector3(0f, 0.9f, 0f), new Vector3(1.2f, 0.2f, 0.2f));

            Pearl(g, -22f, World.Floor(-22f) + 3f);                         // under the arch
            for (int i = 1; i <= 4; i++) Pearl(g, boatX + 5f, bottom + line * i / 5f);   // up the anchor line
            Pearls(g, -34f, 36f, 3);
        }

        // Towering kelp you swim through, hiding a chest in its thickest part.
        static void BuildKelpForest()
        {
            Transform g = Group("Kelp Forest");
            ScatterRocks(g, rock, -88f, -38f, 40, 6, 3);
            Weeds(g, weed, -90f, -36f, 8, 0, 30);

            for (int i = 0; i < 26; i++)
                WeedPatch(g, kelp, R(-90f, -38f), R(-16f, 55f), R(4f, 9f), 16, true);
            // A wall of kelp right on the swimming plane.
            for (int i = 0; i < 9; i++)
                WeedPatch(g, kelp, R(-86f, -42f), R(-3f, 3f), R(2f, 4f), 9, true);

            // Giant clams, each holding a pearl.
            foreach (float x in new[] { -80f, -58f, -44f })
            {
                Transform clam = Holder("Clam", g, G(x, 0f, 0.4f));
                Prim(PrimitiveType.Sphere, clam, coralRock, Vector3.zero, new Vector3(3.2f, 1f, 2.4f));
                Prim(PrimitiveType.Sphere, clam, coralRock, new Vector3(0f, 1f, 0.9f), new Vector3(3.2f, 1f, 2.4f),
                    new Vector3(-50f, 0f, 0f));
                Pearl(g, x, World.Floor(x) + 1.3f);
            }

            Chest(g, -66f);
            Pearls(g, -88f, -40f, 4);
        }

        // A raised shelf crowded with colour: corals of every shape, coral towers and arches.
        static void BuildCoralGardens()
        {
            Transform g = Group("Coral Gardens");
            ScatterRocks(g, coralRock, 42f, 110f, 30, 6, 6, 0.5f, 0.98f);
            Weeds(g, weed, 40f, 112f, 5, 0, 30);

            for (int i = 0; i < 230; i++)
            {
                // Mostly close to the swimming plane, thinning out behind it.
                float z = RI(0, 3) == 0 ? R(-28f, -3f) : R(-3f, 16f) + R(0f, 1f) * R(0f, 40f);
                Coral(g, R(40f, 112f), z, z < -3f ? R(0.5f, 0.8f) : R(0.7f, 1.5f));
            }

            Arch(g, coralRock, 58f, 6.5f, 2.6f);
            Arch(g, coralRock, 76f, 8f, 3f);
            Arch(g, coralRock, 96f, 6f, 2.4f);
            // Corals growing on the arches.
            foreach (float x in new[] { 58f, 76f, 96f })
            {
                for (int i = 0; i < 8; i++)
                {
                    Transform bud = Prim(PrimitiveType.Sphere, g, Pick(coral),
                        new Vector3(x + R(-7f, 7f), World.Floor(x) + R(5.5f, 8.5f), R(1f, 2f)), Vector3.one * R(0.8f, 1.8f));
                    bud.name = "Coral Bud";
                }
            }

            Pearl(g, 58f, World.Floor(58f) + 3f);
            Pearl(g, 96f, World.Floor(96f) + 3f);
            Chest(g, 76f);
            Pearls(g, 42f, 110f, 5);
        }

        // A sailing ship lying broken in a hollow, its bow torn off and its ribs standing in the sand.
        static void BuildShipwreck()
        {
            Transform g = Group("Shipwreck Bay");
            ScatterRocks(g, rock, 116f, 170f, 30, 4, 2);
            Weeds(g, weed, 114f, 172f, 8, 2, 30);

            // The main hull lies just behind the swimming plane, heeled over.
            const float shipX = 148f, shipZ = 9f;
            Transform ship = Holder("Wreck", g, G(shipX, shipZ, 3.2f), new Vector3(-7f, 9f, 13f));
            Prim(PrimitiveType.Sphere, ship, darkWood, Vector3.zero, new Vector3(36f, 9f, 10f));
            Prim(PrimitiveType.Cube, ship, wood, new Vector3(0f, 3f, 0f), new Vector3(27f, 0.5f, 7.4f));
            Prim(PrimitiveType.Cube, ship, wood, new Vector3(-10.5f, 5.2f, 0f), new Vector3(8f, 4f, 6.4f));
            Prim(PrimitiveType.Cube, ship, darkWood, new Vector3(-10.5f, 7.4f, 0f), new Vector3(9f, 0.5f, 7.2f));
            Prim(PrimitiveType.Sphere, ship, black, new Vector3(5f, -0.3f, -4.2f), new Vector3(6f, 4f, 2f));      // hole in the side
            Prim(PrimitiveType.Sphere, ship, black, new Vector3(-9f, 5.4f, -3.1f), new Vector3(1.6f, 1.6f, 0.6f)); // cabin window
            Prim(PrimitiveType.Sphere, ship, black, new Vector3(-12f, 5.4f, -3.1f), new Vector3(1.6f, 1.6f, 0.6f));

            Stick(PrimitiveType.Cylinder, ship, wood, new Vector3(2f, 3f, 0f), 19f, 0.8f, Vector3.zero);           // main mast
            Prim(PrimitiveType.Cylinder, ship, wood, new Vector3(2f, 17f, 0f), new Vector3(0.45f, 5.5f, 0.45f),
                new Vector3(90f, 0f, 0f));                                                                        // yard
            Prim(PrimitiveType.Cylinder, ship, darkWood, new Vector3(2f, 20f, 0f), new Vector3(2.4f, 0.3f, 2.4f)); // crow's nest
            Stick(PrimitiveType.Cylinder, ship, wood, new Vector3(10f, 3f, 0f), 7f, 0.7f, new Vector3(0f, 0f, -28f)); // snapped mast
            Stick(PrimitiveType.Cylinder, ship, wood, new Vector3(16f, 2.5f, 0f), 9f, 0.5f, new Vector3(0f, 0f, -68f)); // bowsprit
            for (int i = 0; i < 12; i++)
            {
                float x = -13f + i * 2.4f;
                foreach (float side in new[] { -3.6f, 3.6f })
                    Prim(PrimitiveType.Cube, ship, darkWood, new Vector3(x, 3.8f, side), new Vector3(0.3f, 1.4f, 0.3f));
            }
            WeedPatch(g, weed, shipX, shipZ + 4f, 12f, 16, false);

            // The torn-off bow: a keel and a cage of ribs right on the swimming plane, to swim through.
            const float ribsFrom = 122f;
            for (int i = 0; i < 9; i++)
            {
                float x = ribsFrom + i * 2.4f;
                float height = 7.5f - Mathf.Abs(i - 4) * 0.7f;
                Vector3 foot = G(x, 0f, -0.3f);
                Stick(PrimitiveType.Cylinder, g, darkWood, foot + new Vector3(0f, 0f, 1.6f), height, 0.4f, new Vector3(24f, 0f, R(-4f, 4f)));
                Stick(PrimitiveType.Cylinder, g, darkWood, foot + new Vector3(0f, 0f, -1.6f), height * 0.8f, 0.4f, new Vector3(-24f, 0f, R(-4f, 4f)));
            }
            Prim(PrimitiveType.Cube, g, darkWood, G(ribsFrom + 9.6f, 0f, 0.1f), new Vector3(22f, 0.6f, 0.9f),
                new Vector3(0f, 0f, Mathf.Atan((World.Floor(ribsFrom + 19f) - World.Floor(ribsFrom)) / 19f) * Mathf.Rad2Deg));

            // Spilled cargo.
            for (int i = 0; i < 16; i++)
            {
                float x = R(120f, 168f), z = R(-12f, 6f);
                if (RI(0, 2) == 0)
                    Prim(PrimitiveType.Cylinder, g, wood, G(x, z, 0.6f), new Vector3(1.3f, 0.9f, 1.3f),
                        new Vector3(RI(0, 2) * 90f, R(0f, 360f), R(-10f, 10f)));
                else
                    Prim(PrimitiveType.Cube, g, wood, G(x, z, 0.5f), Vector3.one * R(1.2f, 1.9f),
                        new Vector3(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)));
            }

            for (int i = 0; i < 5; i++) Jellyfish(g, new Vector3(R(118f, 168f), R(-8f, 14f), R(-4f, 18f)), R(1f, 1.8f));

            Chest(g, ribsFrom + 9.6f);
            Pearl(g, ribsFrom + 3f, World.Floor(ribsFrom + 3f) + 3f);
            Pearl(g, ribsFrom + 16f, World.Floor(ribsFrom + 16f) + 3f);
            Pearl(g, 150f, World.Floor(150f) + 21f);        // by the crow's nest
            Pearls(g, 118f, 168f, 4);
        }

        // What is left of a drowned city: a temple, rows of columns, a gateway.
        static void BuildRuins()
        {
            Transform g = Group("Sunken Ruins");
            ScatterRocks(g, rock, 174f, 212f, 22, 3, 0);
            Weeds(g, weed, 172f, 214f, 7, 1, 25);

            void Column(Vector3 foot, float height, bool whole, Vector3 lean = default)
            {
                Transform c = Holder("Column", g, foot, lean);
                Prim(PrimitiveType.Cube, c, stone, new Vector3(0f, 0.3f, 0f), new Vector3(2.3f, 0.6f, 2.3f));
                Prim(PrimitiveType.Cylinder, c, stone, new Vector3(0f, 0.6f + height * 0.5f, 0f), new Vector3(1.5f, height * 0.5f, 1.5f));
                if (whole) Prim(PrimitiveType.Cube, c, stone, new Vector3(0f, 0.9f + height, 0f), new Vector3(2.4f, 0.6f, 2.4f));
            }

            // The temple stands behind the swimming plane on a stepped platform.
            const float templeX = 194f, templeZ = 13f;
            Transform temple = Holder("Temple", g, G(templeX, templeZ, -0.5f), new Vector3(0f, 0f, -3f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(0f, 0.5f, 0f), new Vector3(28f, 1f, 13f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(0f, 1.5f, 0.5f), new Vector3(25f, 1f, 11f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(0f, 2.5f, 1f), new Vector3(22f, 1f, 9f));
            for (int i = 0; i < 6; i++)
            {
                bool broken = i == 4;
                float x = -9.5f + i * 3.8f, height = broken ? 4f : 10f;
                Prim(PrimitiveType.Cylinder, temple, stone, new Vector3(x, 3f + height * 0.5f, -2.2f), new Vector3(1.5f, height * 0.5f, 1.5f));
                Prim(PrimitiveType.Cylinder, temple, stone, new Vector3(x, 3f + 5f, 4f), new Vector3(1.5f, 5f, 1.5f));
            }
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(-4f, 13.7f, -2.2f), new Vector3(15f, 1.4f, 2.4f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(0f, 13.7f, 4f), new Vector3(23f, 1.4f, 2.4f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(-6f, 16.2f, 4f), new Vector3(12.5f, 1f, 2.6f), new Vector3(0f, 0f, 19f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(6f, 16.2f, 4f), new Vector3(12.5f, 1f, 2.6f), new Vector3(0f, 0f, -19f));
            Prim(PrimitiveType.Cube, temple, stone, new Vector3(9f, 3.8f, -3.5f), new Vector3(9f, 1.4f, 2.4f), new Vector3(0f, 14f, -12f)); // fallen lintel
            Prim(PrimitiveType.Sphere, temple, goldGlow, new Vector3(0f, 6f, 2f), Vector3.one * 2.2f);                  // the idol
            GlowLight(temple, new Vector3(0f, 6f, -1f), new Color(1f, 0.8f, 0.3f), 10f, 14f);

            // An avenue of columns leading through the ruins, some standing, some fallen.
            for (int i = 0; i < 7; i++)
            {
                float x = 176f + i * 5.6f;
                Column(G(x, 4.5f, -0.3f), R(6f, 11f), RI(0, 3) != 0, new Vector3(R(-4f, 4f), 0f, R(-6f, 6f)));
                if (i % 2 == 0) Column(G(x + 2f, -4.5f, -0.3f), R(1.5f, 3.5f), false, new Vector3(R(-6f, 6f), 0f, R(-6f, 6f)));
            }
            for (int i = 0; i < 5; i++)
            {
                float x = R(176f, 212f), z = R(-10f, 3f);
                Prim(PrimitiveType.Cylinder, g, stone, G(x, z, 0.6f), new Vector3(1.5f, R(2f, 4.5f), 1.5f),
                    new Vector3(90f, R(0f, 360f), 0f));
            }
            for (int i = 0; i < 14; i++)
                Prim(PrimitiveType.Cube, g, stone, G(R(174f, 214f), R(-14f, 30f), 0.3f),
                    new Vector3(R(1.5f, 4f), R(1f, 2.2f), R(1.5f, 3f)), new Vector3(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)));

            // A leaning obelisk and a far colonnade.
            Transform obelisk = Holder("Obelisk", g, G(209f, 7f, -1f), new Vector3(6f, 20f, -9f));
            Prim(PrimitiveType.Cube, obelisk, stone, new Vector3(0f, 8f, 0f), new Vector3(2.4f, 16f, 2.4f));
            Prim(PrimitiveType.Cube, obelisk, gold, new Vector3(0f, 16.3f, 0f), new Vector3(1.7f, 1.7f, 1.7f), new Vector3(45f, 0f, 45f));
            for (int i = 0; i < 8; i++) Column(G(172f + i * 6f, 34f, -0.5f), 12f, i % 3 != 1);

            Arch(g, stone, 183f, 7.5f, 2.8f);

            Chest(g, 196f);
            Pearl(g, 183f, World.Floor(183f) + 3.5f);
            Pearls(g, 176f, 212f, 4);
        }

        // The tunnel under the island: dark, with glowing crystals and mushrooms lighting the way.
        static void BuildCave()
        {
            Transform g = Group("Glow Cave");
            float from = World.CaveMin + 1.5f, to = World.CaveMax - 1.5f;

            // Rock teeth above and below, kept off the swimming plane itself.
            float Side() => RI(0, 2) == 0 ? R(-4.5f, -1.8f) : R(1.8f, 5f);
            for (float x = from; x < to; x += R(0.7f, 1.6f))
            {
                float z = Side();
                float w = R(0.7f, 1.6f);
                Prim(PrimitiveType.Sphere, g, darkRock, new Vector3(x, World.Ceiling(x) + 0.4f, z), new Vector3(w, R(2f, 6f), w));
                if (RI(0, 2) == 0)
                {
                    z = Side();
                    Prim(PrimitiveType.Sphere, g, darkRock, new Vector3(x, World.Floor(x) - 0.2f, z), new Vector3(w * 1.3f, R(1.5f, 4.5f), w * 1.3f));
                }
            }

            int n = 0;
            for (float x = from + 2f; x < to; x += R(3.5f, 5.5f), n++)
            {
                bool hanging = n % 3 == 1;
                float z = n % 2 == 0 ? R(2.2f, 4.5f) : R(-4f, -2.2f);
                float y = hanging ? World.Ceiling(x) + 0.2f : World.Floor(x) - 0.2f;
                Crystals(g, new Vector3(x, y, z), hanging, R(0.7f, 1.2f), n % glow.Length, n % 2 == 0);
            }

            // Glowing mushrooms along the back wall.
            for (int i = 0; i < 22; i++)
            {
                float x = R(from, to);
                Material cap = Pick(glow);
                Transform m = Holder("Mushroom", g, new Vector3(x, World.Floor(x) - 0.1f, R(2.5f, 4.8f)), new Vector3(R(-10f, 10f), 0f, R(-10f, 10f)));
                float h = R(0.6f, 2f);
                Prim(PrimitiveType.Cylinder, m, bone, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.25f, h * 0.5f, 0.25f));
                Prim(PrimitiveType.Sphere, m, cap, new Vector3(0f, h, 0f), new Vector3(1.2f, 0.6f, 1.2f) * R(0.7f, 1.3f));
            }

            float mid = (World.CaveMin + World.CaveMax) * 0.5f;
            Chest(g, mid);
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(from + 3f, to - 3f, i / 5f);
                if (Mathf.Abs(x - mid) > 3f) Pearl(g, x, World.Floor(x) + World.CaveHeight * 0.5f);
            }
        }

        // The deep: black water, glowing life, smoking vents and the bones of something enormous.
        static void BuildAbyss()
        {
            Transform g = Group("The Abyss");
            const float west = -224f, east = -142f, deepEast = -170f;

            ScatterRocks(g, darkRock, west, east, 50, 14, 10, 0.15f, 0.5f);
            // Dark pillars on the cliff that drops away from the cave mouth.
            for (int i = 0; i < 10; i++)
            {
                float x = R(-166f, -141f), z = R(6f, 40f);
                float w = R(4f, 9f);
                Prim(PrimitiveType.Sphere, g, darkRock, G(x, z), new Vector3(w, R(14f, 40f), w), new Vector3(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
            }

            // Rough rock on the great western wall where the world ends.
            for (int i = 0; i < 40; i++)
            {
                float x = R(-247f, -227f);
                float s = R(4f, 10f);
                Prim(PrimitiveType.Sphere, g, darkRock, new Vector3(x + 2f, World.Floor(x), R(-3f, 14f)),
                    new Vector3(s, s * R(0.8f, 1.6f), s), new Vector3(R(0f, 360f), R(0f, 360f), 0f));
            }

            // Glowing weed and tube worms on the bottom.
            for (int i = 0; i < 20; i++)
            {
                float x = R(west, deepEast), z = R(-20f, 40f);
                for (int k = 0; k < 7; k++)
                    Weed(g, glowWeed, G(x + R(-3f, 3f), z + R(-3f, 3f)), R(2f, 6f), 0.35f);
            }

            int n = 0;
            for (float x = west + 4f; x < deepEast; x += R(5f, 9f), n++)
            {
                float z = n % 3 == 0 ? R(-5f, -2.5f) : R(2.5f, 16f);
                Crystals(g, G(x, z, -0.3f), false, R(1.2f, 2.6f), n % glow.Length, n % 2 == 0);
            }

            for (int i = 0; i < 22; i++)
            {
                float x = R(west, east + 2f);
                float y = R(World.Floor(x) + 6f, -22f);
                Jellyfish(g, new Vector3(x, y, RI(0, 3) == 0 ? R(-5f, -2f) : R(2f, 30f)), R(1.2f, 2.8f));
            }

            // Hydrothermal vents: chimneys with a red-hot rim, puffing bubbles.
            foreach (float x in new[] { -214f, -203f, -181f, -176f })
            {
                float z = R(3f, 7f);
                Transform v = Holder("Vent", g, G(x, z, -0.5f), new Vector3(R(-6f, 6f), 0f, R(-6f, 6f)));
                float height = R(3f, 6f);
                Prim(PrimitiveType.Cylinder, v, darkRock, new Vector3(0f, height * 0.25f, 0f), new Vector3(3f, height * 0.25f, 3f));
                Prim(PrimitiveType.Cylinder, v, darkRock, new Vector3(0f, height * 0.6f, 0f), new Vector3(2.1f, height * 0.25f, 2.1f));
                Prim(PrimitiveType.Cylinder, v, darkRock, new Vector3(0f, height * 0.9f, 0f), new Vector3(1.4f, height * 0.2f, 1.4f));
                Prim(PrimitiveType.Cylinder, v, lavaGlow, new Vector3(0f, height * 1.1f, 0f), new Vector3(1.1f, 0.06f, 1.1f));
                Transform mouth = Holder("Mouth", v, new Vector3(0f, height * 1.15f, 0f));
                mouth.gameObject.AddComponent<Vent>();
                GlowLight(v, new Vector3(0f, height * 1.3f, -1.5f), new Color(1f, 0.45f, 0.15f), 14f, 12f);
            }

            // The skeleton of a whale, lying along the bottom behind the swimming plane.
            const float whaleX = -193f, whaleZ = 6f;
            Transform whale = Holder("Whale Skeleton", g, G(whaleX, whaleZ, 0.5f), new Vector3(0f, -8f, 3f));
            Prim(PrimitiveType.Capsule, whale, bone, new Vector3(0f, 5f, 0f), new Vector3(1f, 15f, 1f), new Vector3(0f, 0f, 90f));
            Prim(PrimitiveType.Sphere, whale, bone, new Vector3(18f, 4.2f, 0f), new Vector3(11f, 4.6f, 5.4f));
            Prim(PrimitiveType.Sphere, whale, black, new Vector3(18.5f, 5f, -2.3f), new Vector3(2f, 1.6f, 1f));          // eye socket
            Prim(PrimitiveType.Cube, whale, bone, new Vector3(20f, 1.6f, -1.6f), new Vector3(10f, 0.6f, 0.7f), new Vector3(0f, 6f, -8f));
            Prim(PrimitiveType.Cube, whale, bone, new Vector3(20f, 1.6f, 1.6f), new Vector3(10f, 0.6f, 0.7f), new Vector3(0f, -6f, -8f));
            for (int i = 0; i < 11; i++)
            {
                float x = 10f - i * 2.1f;
                float length = 6.5f - Mathf.Abs(i - 3) * 0.45f;
                Stick(PrimitiveType.Capsule, whale, bone, new Vector3(x, 5f, 0.3f), length, 0.4f, new Vector3(150f, 0f, 0f));
                Stick(PrimitiveType.Capsule, whale, bone, new Vector3(x, 5f, -0.3f), length, 0.4f, new Vector3(-150f, 0f, 0f));
            }

            Chest(g, -197f);
            // A trail of pearls leading down the cliff into the dark.
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-144f, -172f, i / 5f);
                Pearl(g, x, World.Floor(x) + 4f);
            }
            Pearls(g, west, deepEast, 6);
        }

        // The eastern shore: the sea bed climbs out of the water to a beach with a jetty.
        static void BuildBeach()
        {
            Transform g = Group("Castaway Beach");
            ScatterRocks(g, rock, 216f, 228f, 10, 0, 0);
            Weeds(g, weed, 214f, 232f, 3, 0, 12);

            PlaceModel("Dock_Long.fbx", g, new Vector3(229f, World.TopY - 1.2f, 3f), 18f, true);
            PlaceModel("Boat.fbx", g, new Vector3(217f, World.TopY - 0.7f, 5f), 9f, true);

            for (int i = 0; i < 26; i++)
            {
                float x = R(238f, 330f), z = R(-2f, 60f);
                Palm(g, G(x, z, -0.3f));
            }
            for (int i = 0; i < 12; i++)
            {
                float x = R(240f, 320f), z = R(0f, 50f);
                float s = R(1.5f, 5f);
                Prim(PrimitiveType.Sphere, g, rock, G(x, z), new Vector3(s * 1.3f, s, s), new Vector3(0f, R(0f, 360f), 0f));
            }

            Chest(g, 224f);
            Pearls(g, 216f, 230f, 2);
        }
    }
}
