using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// The shape of the sea: where the bottom is, where rock hangs overhead, which zone a spot belongs
    /// to and how dark it is there. Everything is a plain function of position, so the game rules and
    /// the scenery builder (FishTank > Build Game) always agree with each other.
    ///
    /// West to east: The Abyss - Glow Cave (a tunnel under an island) - Kelp Forest - Sunny Shallows
    /// (start) - Coral Gardens - Shipwreck Bay - Sunken Ruins - Castaway Beach.
    /// </summary>
    public static class World
    {
        public const float HalfWidth = 246f;
        /// <summary>Height of the water surface.</summary>
        public const float TopY = 20f;
        /// <summary>Typical depth of the sea bed in the shallows.</summary>
        public const float SeaBed = -20f;

        /// <summary>The island's rock fills everything above the tunnel between these two X values.</summary>
        public const float CaveMin = -137f;
        public const float CaveMax = -93f;
        public const float CaveHeight = 10f;

        const float Open = 1000f;

        public static readonly string[] ZoneNames =
        {
            "THE ABYSS", "GLOW CAVE", "KELP FOREST", "SUNNY SHALLOWS",
            "CORAL GARDENS", "SHIPWRECK BAY", "SUNKEN RUINS", "CASTAWAY BEACH",
        };

        public const int HomeZone = 3;

        static float Step(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

        /// <summary>0 outside a..d, 1 between b and c, smooth in between.</summary>
        static float Bump(float x, float a, float b, float c, float d) => Step(a, b, x) * (1f - Step(c, d, x));

        /// <summary>Sea bed height without the shores at either end of the world.</summary>
        public static float OpenFloor(float x)
        {
            float y = SeaBed;
            y += 11f * Bump(x, 38f, 60f, 96f, 114f);        // coral shelf
            y -= 9f * Bump(x, 114f, 128f, 158f, 172f);      // the bowl the wreck lies in
            y += 3f * Step(172f, 184f, x);                  // ruins plateau
            y -= 3f * Bump(x, -90f, -78f, -48f, -36f);      // kelp hollow
            y -= 56f * (1f - Step(-168f, -139f, x));        // the abyss

            y += 1.1f * Mathf.Sin(x * 0.11f) + 0.6f * Mathf.Sin(x * 0.27f + 1.3f) + 0.3f * Mathf.Sin(x * 0.61f + 0.4f);
            return y;
        }

        /// <summary>Height of the sea bed on the swimming plane (Z = 0), shores included.</summary>
        public static float Floor(float x)
        {
            float shore = Mathf.Max(Step(214f, 244f, x), Step(-226f, -246f, x));
            return Mathf.Lerp(OpenFloor(x), TopY + 5f, shore);
        }

        public static bool InCave(float x) => x > CaveMin && x < CaveMax;

        /// <summary>Height of the rock overhead; very high where the water is open to the surface.</summary>
        public static float Ceiling(float x)
        {
            return InCave(x) ? Floor(x) + CaveHeight + 0.8f * Mathf.Sin(x * 0.45f) : Open;
        }

        /// <summary>Top of the island that sits over the cave.</summary>
        public static float IslandTop(float x)
        {
            float t = Mathf.InverseLerp(CaveMin, CaveMax, x);
            return TopY + 2f + 8f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
        }

        /// <summary>
        /// Height of the sea bed anywhere, for scenery. In front of the swimming plane it falls gently
        /// away (so nothing hides the fish); behind it the ground rolls up into hills.
        /// </summary>
        public static float Ground(float x, float z)
        {
            if (z <= 0f)
            {
                float front = OpenFloor(x) + 0.04f * z;
                return Mathf.Lerp(front, Floor(x), Step(-9f, -4f, z));
            }

            float hills = (Mathf.PerlinNoise(x * 0.045f + 500f, z * 0.045f + 500f) - 0.4f) * 9f * Mathf.Clamp01(z / 30f);
            return Floor(x) + Mathf.Min(0.003f * z * z, 26f) + hills;
        }

        /// <summary>Lowest and highest Y the centre of a body may be at, for a body this tall.</summary>
        public static void Limits(float x, float halfSize, out float low, out float high)
        {
            low = Floor(x) + halfSize * 0.6f + 0.2f;
            high = Mathf.Min(TopY, Ceiling(x)) - halfSize;
        }

        /// <summary>
        /// Keeps a move inside the water. Gentle slopes are slid along; walls (the island's flanks,
        /// cliffs, the shores) stop sideways movement instead, which is reported as blocked.
        /// </summary>
        public static Vector3 Constrain(Vector3 from, Vector3 to, float halfSize, out bool blocked)
        {
            blocked = false;
            to.x = Mathf.Clamp(to.x, -HalfWidth, HalfWidth);
            to.z = 0f;

            Limits(to.x, halfSize, out float low, out float high);
            float slack = 4f * Mathf.Abs(to.x - from.x) + 0.3f;
            if (high < low || to.y < low - slack || to.y > high + slack)
            {
                blocked = true;
                to.x = from.x;
                Limits(to.x, halfSize, out low, out high);
            }

            to.y = high < low ? low : Mathf.Clamp(to.y, low, high);
            return to;
        }

        /// <summary>True if a body this tall fits in the water at this spot.</summary>
        public static bool IsOpen(float x, float y, float halfSize)
        {
            if (Mathf.Abs(x) > HalfWidth) return false;
            Limits(x, halfSize, out float low, out float high);
            return y >= low && y <= high;
        }

        /// <summary>Index into <see cref="ZoneNames"/>, or -1 in open water that belongs to no zone.</summary>
        public static int ZoneAt(float x, float y)
        {
            if (x < CaveMin - 2f) return y < -34f ? 0 : -1;
            if (x < CaveMax) return 1;
            if (x < -36f) return 2;
            if (x < 38f) return 3;
            if (x < 114f) return 4;
            if (x < 172f) return 5;
            if (x < 216f) return 6;
            return 7;
        }

        /// <summary>0 = sunlit, 1 = pitch dark: grows with depth and inside the cave.</summary>
        public static float Darkness(float x, float y)
        {
            float deep = Mathf.InverseLerp(-26f, -60f, y);
            float cave = 0.85f * Bump(x, CaveMin - 3f, CaveMin + 5f, CaveMax - 5f, CaveMax + 3f);
            return Mathf.Clamp01(Mathf.Max(deep, cave));
        }

        /// <summary>Colour of sunlit water here; each zone has its own tint.</summary>
        public static Color WaterTint(float x)
        {
            Color c = new Color(0.04f, 0.33f, 0.52f);
            c = Color.Lerp(c, new Color(0.03f, 0.36f, 0.33f), Bump(x, -96f, -80f, -50f, -32f));     // kelp: green
            c = Color.Lerp(c, new Color(0.05f, 0.46f, 0.62f), Bump(x, 34f, 56f, 100f, 118f));       // coral: turquoise
            c = Color.Lerp(c, new Color(0.05f, 0.26f, 0.42f), Bump(x, 112f, 128f, 158f, 174f));     // wreck: murky
            c = Color.Lerp(c, new Color(0.09f, 0.38f, 0.45f), Step(170f, 186f, x));                 // ruins: jade
            c = Color.Lerp(c, new Color(0.03f, 0.2f, 0.42f), 1f - Step(-170f, -139f, x));           // over the abyss
            return c;
        }
    }
}
