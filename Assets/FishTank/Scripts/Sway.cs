using UnityEngine;

namespace FishTank
{
    /// <summary>Gentle side-to-side sway for seaweed, pivoting at the object's origin.</summary>
    public class Sway : MonoBehaviour
    {
        public float angle = 8f;
        public float speed = 1f;
        public float phase;

        void Update()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * speed + phase) * angle);
        }
    }
}
