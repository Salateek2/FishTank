using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Simple tank fish: cruises across the screen on the same X/Y plane as the player, flees from a
    /// player that can eat it and hunts a player it can eat.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class AIFish : MonoBehaviour
    {
        public Fish Fish { get; private set; }

        Vector3 heading = Vector3.right;
        float cruiseSpeed = 2f;
        float wanderPhase;
        float wanderFrequency;
        float turnPhase;

        void Awake()
        {
            Fish = GetComponent<Fish>();
        }

        public void Init(Vector3 direction, float cruiseSpeed)
        {
            direction.z = 0f;
            heading = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.right;
            this.cruiseSpeed = cruiseSpeed;
            wanderPhase = Random.value * Mathf.PI * 2f;
            turnPhase = Random.value * Mathf.PI * 2f;
            wanderFrequency = Random.Range(0.4f, 1.1f);
            Fish.Velocity = heading * cruiseSpeed;
            Fish.Face(heading, true);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !Fish.IsAlive) return;

            float dt = Time.deltaTime;
            Vector3 pos = Fish.Position;

            // Cruise across the view with a gentle meander; the spawner removes fish that leave it.
            float turn = Mathf.Sin(Time.time * wanderFrequency * 0.6f + turnPhase) * 7f;
            heading = Quaternion.Euler(0f, 0f, turn * dt) * heading;

            Vector3 desired = heading * cruiseSpeed
                              + Vector3.up * (Mathf.Sin(Time.time * wanderFrequency + wanderPhase) * cruiseSpeed * 0.25f);

            var player = gm.Player;
            if (player != null && gm.State == GameState.Playing && player.IsAlive)
            {
                Vector3 to = player.Position - pos;
                float dist = to.magnitude;
                float playerSpeed = gm.PlayerMaxSpeed;

                if (dist > 0.001f)
                {
                    if (gm.CanEat(Fish, player) && !gm.PlayerInvulnerable && dist < 2.5f + 1.2f * Fish.Size)
                    {
                        // Hunt, but never faster than the player can escape.
                        desired = to / dist * Mathf.Min(cruiseSpeed * 1.6f, playerSpeed * 0.8f);
                    }
                    else if (gm.CanEat(player, Fish) && dist < 2f + 1.5f * player.Size)
                    {
                        desired = -to / dist * Mathf.Min(cruiseSpeed * 1.7f, playerSpeed * 0.75f);
                    }
                }
            }

            // Stay between the sea bed and the surface (or the cave roof), looking a little ahead so
            // the fish rises over a slope instead of ploughing into it.
            float half = Fish.Size * 0.5f;
            float ahead = pos.x + Mathf.Sign(Fish.Velocity.x) * (1.5f + Fish.Size);
            World.Limits(pos.x, half, out float low, out float high);
            World.Limits(ahead, half, out float lowAhead, out float highAhead);
            low = Mathf.Max(low, lowAhead);
            high = Mathf.Min(high, highAhead);
            if (high > low)
            {
                if (pos.y < low + 0.3f) desired.y = Mathf.Max(desired.y, cruiseSpeed * 0.8f);
                else if (pos.y > high - 0.3f) desired.y = Mathf.Min(desired.y, -cruiseSpeed * 0.8f);
            }

            desired.z = 0f;
            Fish.Velocity = Vector3.MoveTowards(Fish.Velocity, desired, cruiseSpeed * 3f * dt);

            Vector3 next = World.Constrain(pos, pos + Fish.Velocity * dt, half, out bool blocked);
            if (blocked)
            {
                // A wall: turn round and swim back the other way.
                heading = new Vector3(-heading.x, heading.y, 0f);
                Fish.Velocity = new Vector3(-Fish.Velocity.x, Fish.Velocity.y, 0f);
            }

            next = Rock.PushOut(next, Fish.Radius);
            transform.position = World.Constrain(next, next, half, out _);
        }
    }
}
