using UnityEngine;
using UnityEngine.InputSystem;

namespace FishTank
{
    /// <summary>
    /// 2.5D arcade swimming in the style of mobile shark / fish games: a 3D fish in a 3D scene that
    /// only ever moves on the X/Y plane (Z stays 0), seen from the side. The fish keeps swimming the
    /// way it is pointing and curves round towards where you steer. The camera follows by itself.
    ///
    /// Steer: mouse - the fish chases the cursor (further away = faster, on top of it = stop)
    ///        touch - drag anywhere, like a floating joystick
    ///        WASD / arrows / gamepad left stick (W = up, S = down)
    /// Boost: hold left mouse button, Space / Shift, gamepad A / right trigger, or a second finger.
    ///        Drains the boost bar; it refills by itself and a little with every fish eaten.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class PlayerFish : MonoBehaviour
    {
        public float baseSpeed = 4.5f;
        public float speedPerSize = 1.3f;
        [Tooltip("How fast the fish gets up to speed, in multiples of its top speed per second.")]
        public float acceleration = 6f;
        [Tooltip("How fast the fish coasts to a stop when you let go.")]
        public float braking = 3f;
        [Tooltip("How quickly the fish curves round to a new direction, in degrees per second.")]
        public float turnRate = 420f;

        [Header("Boost")]
        public float boostSpeedMultiplier = 1.9f;
        [Tooltip("Seconds of boost in a full bar.")]
        public float boostSeconds = 2.2f;
        [Tooltip("Seconds to refill an empty bar.")]
        public float boostRefillSeconds = 4f;
        [Tooltip("Fraction of the bar given back for each fish eaten.")]
        public float boostPerMeal = 0.12f;

        Fish fish;
        float headingAngle;      // direction of travel on screen: 0 = right, 90 = up
        float speed;
        float boost = 1f;
        bool boosting;
        bool boostEmptied;
        float boostRestAt;
        float nextTrailTime;
        bool useMouse;

        public float MaxSpeed => baseSpeed + speedPerSize * fish.Size;
        public bool IsBoosting => boosting;
        /// <summary>How full the boost bar is, 0..1.</summary>
        public float Boost01 => boost;

        void Awake()
        {
            fish = GetComponent<Fish>();
        }

        public void OnAte()
        {
            boost = Mathf.Clamp01(boost + boostPerMeal);
            if (boost > 0.2f) boostEmptied = false;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !fish.IsAlive)
            {
                boosting = false;
                speed = 0f;
                return;
            }

            float dt = Time.deltaTime;
            bool playing = gm.State == GameState.Playing;
            float max = MaxSpeed;

            // x = right on screen, y = up; length 0..1 is how hard the player is pushing.
            Vector2 steer = playing ? ReadSteer() : Vector2.zero;
            float push = steer.magnitude;

            UpdateBoost(gm, playing && push > 0.05f, dt);

            // Curve round towards the wanted direction instead of snapping to it.
            if (push > 0.05f)
            {
                float wanted = Mathf.Atan2(steer.y, steer.x) * Mathf.Rad2Deg;
                headingAngle = Mathf.MoveTowardsAngle(headingAngle, wanted, turnRate * dt);
            }

            float topSpeed = max * (boosting ? boostSpeedMultiplier : 1f);
            float wantSpeed = push > 0.05f ? topSpeed * Mathf.Max(push, boosting ? 1f : 0f) : 0f;
            float rate = wantSpeed > speed ? acceleration : braking;
            speed = Mathf.MoveTowards(speed, wantSpeed, max * rate * dt);

            float rad = headingAngle * Mathf.Deg2Rad;
            var heading = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            fish.Velocity = heading * speed;

            if (boosting && Time.time >= nextTrailTime)
            {
                nextTrailTime = Time.time + 0.04f;
                gm.OnPlayerDashTrail(-heading);
            }

            Vector3 pos = fish.Position + fish.Velocity * dt;

            // Stay in the water: between the sea bed and the surface (or the cave roof), always on the
            // Z = 0 plane. Slopes are slid along; cliffs and the island's flanks are walls.
            float half = fish.Size * 0.5f;
            pos = World.Constrain(fish.Position, pos, half, out _);

            // Rocks that reach the swimming plane are solid: the fish slides round or over them.
            pos = Rock.PushOut(pos, fish.Radius);
            pos = World.Constrain(pos, pos, half, out _);
            transform.position = pos;
        }

        void UpdateBoost(GameManager gm, bool moving, float dt)
        {
            // The short delay stops the click that started the game from also boosting.
            bool held = moving && Time.time > gm.StartedAt + 0.25f && BoostHeld();
            bool was = boosting;
            boosting = held && boost > 0f && !boostEmptied;

            if (boosting)
            {
                boost = Mathf.Max(0f, boost - dt / boostSeconds);
                boostRestAt = Time.time + 0.4f;
                // Run it dry and it has to recover a little before it works again.
                if (boost <= 0f) boostEmptied = true;
                if (!was) gm.OnPlayerDashed(-fish.Forward);
            }
            else if (Time.time >= boostRestAt)
            {
                boost = Mathf.Min(1f, boost + dt / boostRefillSeconds);
                if (boost > 0.2f) boostEmptied = false;
            }
        }

        static bool BoostHeld()
        {
            var k = Keyboard.current;
            if (k != null && (k.spaceKey.isPressed || k.leftShiftKey.isPressed || k.rightShiftKey.isPressed))
                return true;

            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed) return true;

            var g = Gamepad.current;
            if (g != null && (g.buttonSouth.isPressed || g.rightTrigger.isPressed || g.rightShoulder.isPressed))
                return true;

            // Touch: steer with one finger, hold a second finger down to boost.
            var t = Touchscreen.current;
            return t != null && t.touches.Count > 1 && t.touches[1].press.isPressed;
        }

        /// <summary>Wanted direction on screen (x = right, y = up) with length 0..1.</summary>
        Vector2 ReadSteer()
        {
            // Touch: wherever the finger lands becomes the centre of a floating joystick.
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                Vector2 drag = touch.primaryTouch.position.ReadValue() - touch.primaryTouch.startPosition.ReadValue();
                drag /= Screen.height * 0.12f;
                return drag.magnitude > 0.15f ? Vector2.ClampMagnitude(drag, 1f) : Vector2.zero;
            }

            Vector2 v = Vector2.zero;

            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
            }

            var g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();

            // Keys or stick take over from the mouse until the mouse is moved or clicked again.
            var m = Mouse.current;
            if (v.sqrMagnitude > 0.01f)
            {
                useMouse = false;
                return Vector2.ClampMagnitude(v, 1f);
            }
            if (m != null && (m.delta.ReadValue().sqrMagnitude > 4f || m.leftButton.isPressed)) useMouse = true;

            // Mouse: chase the cursor. Further away = faster; right on top of the fish = stop.
            // The comparison is done on screen, so it stays right however the camera has swung.
            var cam = Camera.main;
            if (useMouse && m != null && cam != null)
            {
                Vector3 fishOnScreen = cam.WorldToScreenPoint(fish.Position);
                if (fishOnScreen.z > 0f)
                {
                    Vector2 delta = m.position.ReadValue() - (Vector2)fishOnScreen;
                    float unit = Screen.height * 0.5f / Mathf.Max(0.1f, ViewHalfHeight());   // pixels per world unit
                    Vector2 world = delta / unit;
                    float dead = 0.6f * fish.Size;
                    float distance = world.magnitude;
                    if (distance > dead)
                        return world / distance * Mathf.Clamp01((distance - dead) / (fish.Size * 2f) + 0.25f);
                }
            }

            return Vector2.zero;
        }

        static float ViewHalfHeight()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.cameraRig != null ? gm.cameraRig.HalfHeight : 6f;
        }
    }
}
