using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Sets the mood of the water around the player: each zone tints it, and it gets darker with
    /// depth and inside the cave, where the player carries a soft glow so glowing things stand out.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WaterMood : MonoBehaviour
    {
        public Light sun;
        public float sunIntensity = 1.4f;
        public Color brightAmbient = new Color(0.45f, 0.62f, 0.72f);
        public Color darkAmbient = new Color(0.05f, 0.07f, 0.14f);
        public Color darkWater = new Color(0.005f, 0.02f, 0.06f);

        Camera cam;
        Light lantern;
        float darkness;
        Color water;
        bool started;

        void Awake()
        {
            cam = GetComponent<Camera>();

            var go = new GameObject("Player Glow");
            lantern = go.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(0.7f, 0.9f, 1f);
            lantern.shadows = LightShadows.None;
            lantern.intensity = 0f;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            Vector3 at = gm != null && gm.Player != null ? gm.Player.Position : transform.position;
            float size = gm != null && gm.Player != null ? gm.Player.Size : 1f;

            float wantDark = World.Darkness(at.x, at.y);
            Color wantWater = Color.Lerp(World.WaterTint(at.x), darkWater, wantDark);

            float k = started ? 1f - Mathf.Exp(-2.5f * Time.deltaTime) : 1f;
            started = true;
            darkness = Mathf.Lerp(darkness, wantDark, k);
            water = Color.Lerp(water, wantWater, k);

            RenderSettings.fogColor = water;
            cam.backgroundColor = water;
            RenderSettings.ambientLight = Color.Lerp(brightAmbient, darkAmbient, darkness);
            if (sun != null) sun.intensity = Mathf.Lerp(sunIntensity, 0.08f, darkness);

            lantern.transform.position = at + new Vector3(0f, 0.5f * size, -1.5f - size);
            lantern.range = 16f + 5f * size;
            lantern.intensity = darkness * (18f + 10f * size);
        }
    }
}
