using System.Collections;
using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Game feel: bubble bursts, synthesised sound effects and freeze-frames.
    /// Everything is generated in code, so no art or audio assets are needed.
    /// </summary>
    public class Juice : MonoBehaviour
    {
        const int SampleRate = 44100;

        AudioSource chompSource;
        AudioSource sfxSource;
        AudioClip chompClip;
        AudioClip whooshClip;
        AudioClip deathClip;
        Sprite bubbleSprite;

        const int SpeckCount = 160;
        Transform[] specks;
        SpriteRenderer[] speckSprites;
        float[] speckScale;

        float stopUntil;
        bool stopping;

        public static Juice Create()
        {
            var go = new GameObject("Juice");
            return go.AddComponent<Juice>();
        }

        void Awake()
        {
            chompSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            chompSource.playOnAwake = sfxSource.playOnAwake = false;
            chompSource.spatialBlend = sfxSource.spatialBlend = 0f;

            chompClip = MakeChomp();
            whooshClip = MakeWhoosh();
            deathClip = MakeDeath();
            bubbleSprite = MakeBubbleSprite();
            MakeSpecks();
        }

        // ---------------------------------------------------------------- drifting specks

        // Tiny particles hanging in the water around the camera. They are what makes swimming through
        // open water read as movement: without them there is nothing to rush past.
        void MakeSpecks()
        {
            specks = new Transform[SpeckCount];
            speckSprites = new SpriteRenderer[SpeckCount];
            speckScale = new float[SpeckCount];
            for (int i = 0; i < SpeckCount; i++)
            {
                var go = new GameObject("Speck");
                go.transform.SetParent(transform, false);
                go.transform.position = Random.insideUnitSphere * 10f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = bubbleSprite;
                specks[i] = go.transform;
                speckSprites[i] = sr;
                speckScale[i] = Random.Range(0.5f, 1.5f);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || specks == null) return;

            var gm = GameManager.Instance;
            float size = gm != null && gm.Player != null ? gm.Player.Size : 1f;
            float box = 8f + 5f * size;
            float speckSize = 0.03f + 0.035f * size;

            Vector3 centre = cam.transform.position;
            Quaternion facing = cam.transform.rotation;
            for (int i = 0; i < SpeckCount; i++)
            {
                // Specks that fall behind wrap round to the far side of a box that travels with the camera.
                Vector3 d = specks[i].position - centre;
                d.x = Mathf.Repeat(d.x + box, box * 2f) - box;
                d.y = Mathf.Repeat(d.y + box, box * 2f) - box;
                d.z = Mathf.Repeat(d.z + box, box * 2f) - box;

                specks[i].SetPositionAndRotation(centre + d, facing);
                specks[i].localScale = Vector3.one * (speckSize * speckScale[i]);

                // Fade out towards the edge of the box (hides the wrap) and right in front of the lens.
                float far = d.magnitude / box;
                float alpha = 0.5f * Mathf.Clamp01(1f - far) * Mathf.Clamp01(far * 6f);
                speckSprites[i].color = new Color(0.8f, 0.95f, 1f, alpha);
            }
        }

        void OnDestroy()
        {
            // Never leave the game frozen if the scene is reloaded mid freeze-frame.
            Time.timeScale = 1f;
        }

        // ---------------------------------------------------------------- bubbles

        /// <summary>Bursts <paramref name="count"/> bubbles of roughly <paramref name="size"/> world units.</summary>
        public void Bubbles(Vector3 position, int count, float size, Vector3 push = default)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Bubble");
                go.transform.SetParent(transform, false);
                go.transform.position = position + Random.insideUnitSphere * size;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = bubbleSprite;
                sr.color = new Color(0.85f, 0.97f, 1f, 1f);

                Vector3 velocity = Random.insideUnitSphere * (size * 14f) + push;
                go.AddComponent<Bubble>().Init(sr, velocity, Random.Range(0.45f, 0.9f),
                    size * Random.Range(0.5f, 1.3f));
            }
        }

        // ---------------------------------------------------------------- sound

        /// <summary>Chomp that climbs in pitch as the combo grows.</summary>
        public void Chomp(int combo)
        {
            chompSource.pitch = Mathf.Min(2f, 1f + 0.09f * Mathf.Max(0, combo - 1)) * Random.Range(0.96f, 1.04f);
            chompSource.PlayOneShot(chompClip, 0.8f);
        }

        public void Whoosh() => sfxSource.PlayOneShot(whooshClip, 0.6f);
        public void Death() => sfxSource.PlayOneShot(deathClip, 0.8f);

        // ---------------------------------------------------------------- freeze-frame

        /// <summary>Freezes the game for a moment to give an impact some weight.</summary>
        public void HitStop(float seconds)
        {
            stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + seconds);
            if (!stopping) StartCoroutine(Stop());
        }

        IEnumerator Stop()
        {
            stopping = true;
            Time.timeScale = 0f;
            while (Time.unscaledTime < stopUntil) yield return null;
            Time.timeScale = 1f;
            stopping = false;
        }

        // ---------------------------------------------------------------- generated assets

        static AudioClip MakeChomp()
        {
            int n = (int)(SampleRate * 0.1f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(620f, 150f, u) / SampleRate;
                float noise = Random.value * 2f - 1f;
                float attack = Mathf.Clamp01(u * 40f);
                data[i] = (Mathf.Sin(phase) * 0.7f + noise * 0.3f * (1f - u)) * Mathf.Exp(-5f * u) * attack;
            }
            return MakeClip("Chomp", data);
        }

        static AudioClip MakeWhoosh()
        {
            int n = (int)(SampleRate * 0.22f);
            var data = new float[n];
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float noise = Random.value * 2f - 1f;
                low += (noise - low) * Mathf.Lerp(0.25f, 0.05f, u);
                data[i] = low * Mathf.Sin(Mathf.PI * u) * 1.6f;
            }
            return MakeClip("Whoosh", data);
        }

        static AudioClip MakeDeath()
        {
            int n = (int)(SampleRate * 0.45f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(330f, 70f, u) / SampleRate;
                float s = Mathf.Sin(phase);
                float attack = Mathf.Clamp01(u * 60f);
                data[i] = (s * 0.5f + Mathf.Sign(s) * 0.2f) * (1f - u) * attack;
            }
            return MakeClip("Death", data);
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static Sprite MakeBubbleSprite()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - c, y - c).magnitude / (c - 1f);
                    // Faint inside, bright rim, soft outer edge.
                    float a = Mathf.Lerp(0.18f, 0.95f, Mathf.InverseLerp(0.6f, 1f, d)) * Mathf.Clamp01((1f - d) * 6f + 0.5f);
                    if (d > 1.08f) a = 0f;
                    // Small highlight towards the top-left.
                    float h = new Vector2(x - c * 0.6f, y - c * 1.4f).magnitude / (c * 0.25f);
                    a = Mathf.Max(a, Mathf.Clamp01(1f - h) * 0.9f);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
