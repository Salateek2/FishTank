using UnityEngine;

namespace FishTank
{
    /// <summary>A vent in the sea bed that puffs out bubbles while the player is near enough to see it.</summary>
    public class Vent : MonoBehaviour
    {
        public float interval = 0.12f;
        public float size = 0.3f;

        float next;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || Time.time < next) return;
            next = Time.time + interval * Random.Range(0.6f, 1.4f);

            if (Mathf.Abs(gm.Player.Position.x - transform.position.x) > 45f) return;
            gm.VentBubble(transform.position, size);
        }
    }
}
