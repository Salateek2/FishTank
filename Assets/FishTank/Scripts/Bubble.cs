using UnityEngine;

namespace FishTank
{
    /// <summary>One short-lived bubble: bursts outwards, then floats up, wobbles and fades.</summary>
    public class Bubble : MonoBehaviour
    {
        Vector3 velocity;
        float life;
        float maxLife;
        float baseScale;
        float wobblePhase;
        SpriteRenderer sprite;

        public void Init(SpriteRenderer sprite, Vector3 velocity, float life, float scale)
        {
            this.sprite = sprite;
            this.velocity = velocity;
            this.life = maxLife = life;
            baseScale = scale;
            wobblePhase = Random.value * Mathf.PI * 2f;
            transform.localScale = Vector3.one * scale;
            FaceCamera();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // The burst speed bleeds off quickly and buoyancy takes over.
            float wobble = Mathf.Sin(Time.time * 6f + wobblePhase) * baseScale * 2f;
            var rise = new Vector3(wobble, baseScale * 6f, 0f);
            velocity = Vector3.Lerp(velocity, rise, 1f - Mathf.Exp(-4f * dt));
            transform.position += velocity * dt;
            FaceCamera();

            float k = life / maxLife;
            transform.localScale = Vector3.one * (baseScale * Mathf.Lerp(1.3f, 0.8f, k));
            Color c = sprite.color;
            c.a = Mathf.Clamp01(k * 2f);
            sprite.color = c;
        }

        void FaceCamera()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
