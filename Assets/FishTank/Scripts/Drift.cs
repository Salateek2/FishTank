using UnityEngine;

namespace FishTank
{
    /// <summary>Slow floating motion with a gentle pulse, for jellyfish and other drifting things.</summary>
    public class Drift : MonoBehaviour
    {
        public float bob = 1.5f;
        public float sway = 1f;
        public float speed = 0.5f;
        public float phase;
        public float pulse = 0.12f;

        Vector3 origin;
        Vector3 scale;

        void Start()
        {
            origin = transform.position;
            scale = transform.localScale;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.position = origin + new Vector3(Mathf.Sin(t * 0.6f) * sway, Mathf.Sin(t) * bob, 0f);

            float squeeze = Mathf.Sin(t * 3f) * pulse;
            transform.localScale = new Vector3(scale.x * (1f - squeeze), scale.y * (1f + squeeze), scale.z * (1f - squeeze));
        }
    }
}
