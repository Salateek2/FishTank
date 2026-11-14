using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Fully automatic 2.5D side-scroll camera. It never rotates: it always looks straight down +Z at
    /// the X/Y plane the fish swim on, follows the player smoothly and slides a little ahead of the
    /// way the fish is pointing so you see more of where you are going. It pulls back as the fish grows.
    /// The player never controls it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [Header("Framing")]
        public float sideHalfHeight = 4.5f;
        public float sideHalfHeightPerSize = 2.5f;
        public float sideFov = 50f;
        [Tooltip("Extra field of view while boosting, for a sense of speed.")]
        public float boostFovKick = 7f;
        [Tooltip("How far above the fish the camera sits, as a fraction of the visible half height.")]
        public float heightOffset = 0.08f;

        [Header("Look-ahead & smoothing")]
        [Tooltip("How far the view slides ahead of the fish's nose, as a fraction of the visible half " +
                 "height. Up and down get half of this.")]
        public float lookAhead = 0.45f;
        [Tooltip("How quickly the look-ahead swings over when the fish turns round.")]
        public float lookAheadSpeed = 3f;
        [Tooltip("Seconds the camera takes to catch up with the fish. Low = tight.")]
        public float followSmoothTime = 0.15f;
        public float zoomSharpness = 2f;
        [Tooltip("Maximum shake offset as a fraction of the camera distance.")]
        public float shakeAmount = 0.03f;

        Camera cam;
        float halfHeight;
        float fov;
        float trauma;
        Vector3 focus;
        Vector3 focusVelocity;
        Vector3 currentLookAhead;
        bool hasFocus;

        /// <summary>Half the visible height at the fish, in world units.</summary>
        public float HalfHeight => halfHeight;
        /// <summary>Sideways distance from the player at which a fish is safely off screen.</summary>
        public float ViewRange => halfHeight * (cam != null ? cam.aspect : 1.78f) * 1.5f + 3f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            halfHeight = sideHalfHeight + sideHalfHeightPerSize * 0.8f;
            fov = sideFov;
            cam.fieldOfView = fov;
            transform.rotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;

            float dt = Time.deltaTime;
            Fish player = gm.Player;
            float size = player.Size;

            float wantHalf = sideHalfHeight + sideHalfHeightPerSize * size;
            if (cam.aspect < 1.2f) wantHalf *= 1.2f / cam.aspect;   // keep a usable width on portrait phones
            halfHeight = Mathf.Lerp(halfHeight, wantHalf, 1f - Mathf.Exp(-zoomSharpness * dt));
            float distance = halfHeight / Mathf.Tan(sideFov * 0.5f * Mathf.Deg2Rad);

            // Slide ahead of the nose: fully sideways, half as much up and down.
            Vector3 nose = player.Forward;
            Vector3 wantLookAhead = gm.State == GameState.Playing && player.IsAlive
                ? new Vector3(nose.x * lookAhead * halfHeight, nose.y * lookAhead * 0.5f * halfHeight, 0f)
                : Vector3.zero;
            currentLookAhead = Vector3.Lerp(currentLookAhead, wantLookAhead, 1f - Mathf.Exp(-lookAheadSpeed * dt));

            Vector3 target = player.Position;
            target.z = 0f;
            if (!hasFocus)
            {
                focus = target;
                hasFocus = true;
            }
            if (dt > 1e-5f)
                focus = Vector3.SmoothDamp(focus, target, ref focusVelocity, followSmoothTime, Mathf.Infinity, dt);

            Vector3 position = focus + currentLookAhead + new Vector3(0f, heightOffset * halfHeight, -distance);

            // Never dip under the sand (in front of the swimming plane the shores are not there).
            float sand = Mathf.Max(World.OpenFloor(position.x - 4f), World.OpenFloor(position.x),
                World.OpenFloor(position.x + 4f));
            position.y = Mathf.Max(position.y, sand + 0.6f);

            // Shake runs on real time so it keeps going through freeze-frames.
            if (trauma > 0f)
            {
                trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 2.5f);
                Vector2 offset = Random.insideUnitCircle * (trauma * trauma * distance * shakeAmount);
                position += new Vector3(offset.x, offset.y, 0f);
            }

            // Locked side-on: the camera only ever moves, it never turns.
            transform.SetPositionAndRotation(position, Quaternion.identity);

            float wantFov = sideFov + (gm.PlayerDashing ? boostFovKick : 0f);
            fov = Mathf.Lerp(fov, wantFov, 1f - Mathf.Exp(-8f * dt));
            cam.fieldOfView = fov;

            // Clear around the fish, fading to blue behind it.
            RenderSettings.fogStartDistance = distance + 5f + 4f * size;
            RenderSettings.fogEndDistance = distance + 45f + 16f * size;
        }

        /// <summary>Adds camera shake; 0.2 is a nudge, 1 is a big hit.</summary>
        public void Shake(float strength)
        {
            trauma = Mathf.Clamp01(Mathf.Max(trauma, strength));
        }
    }
}
