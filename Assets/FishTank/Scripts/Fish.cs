using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Shared body of every fish (player and AI): size, facing, animation and being eaten.
    /// Prefabs are normalised so that a fish of Size 1 is one world unit long and faces local +Z.
    ///
    /// 2.5D: fish are 3D models that only ever move on the X/Y plane, seen from the side.
    /// Rotation is split over a strict parent-child chain so the angles can never fight each other:
    ///   root   - only turns round Z: aims the nose (root +X) along the swimming direction
    ///   Flip   - only rolls round X: half a barrel roll so the belly stays down when heading left
    ///   Model  - fixed quarter turn that lines the prefab's nose (+Z) up with the root's +X
    /// </summary>
    public class Fish : MonoBehaviour
    {
        public static readonly List<Fish> All = new List<Fish>();

        static readonly int SwimSpeedId = Animator.StringToHash("SwimSpeed");
        static readonly int AttackId = Animator.StringToHash("Attack");

        [Header("Filled in by FishTank > Build Game")]
        public string speciesName;
        [Tooltip("Typical body length of this species in world units.")]
        public float baseSize = 1f;
        [Tooltip("Half the body height of a size-1 fish.")]
        public float bodyHalfHeight = 0.25f;
        public Animator animator;

        [Header("Feel")]
        public float growSpeed = 1.5f;

        // Constants rather than inspector fields, so every fish prefab picks up changes at once.
        const float TurnSpeed = 600f;   // degrees per second the nose swings round: a turn-around takes 0.3s
        const float FlipSpeed = 12f;    // how snappily the body rolls belly-down after turning round

        [HideInInspector] public Vector3 Velocity;
        [HideInInspector] public bool destroyWhenEaten = true;

        public float Size { get; private set; } = 1f;
        public float TargetSize { get; private set; } = 1f;
        public bool IsAlive { get; private set; } = true;

        public Vector3 Position => transform.position;
        public Vector3 Forward => Quaternion.Euler(0f, 0f, aim) * Vector3.right;
        public Vector3 MouthPosition => Position + Forward * (0.4f * Size);
        public float Radius => Size * (0.5f + bodyHalfHeight) * 0.4f;

        float aim;          // nose direction on screen, degrees round Z: 0 = right, 90 = up
        float wantAim;
        float roll;         // 0 = heading right, -180 = heading left (turns its back to the camera on the way)
        bool facingLeft;
        float punch;
        Transform flip;
        Renderer[] renderers;

        void Awake()
        {
            BuildHierarchy();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        // root (aim) > Flip (roll) > Model (fixed offset) > everything the prefab came with.
        void BuildHierarchy()
        {
            flip = transform.Find("Flip");
            if (flip != null) return;

            flip = new GameObject("Flip").transform;
            flip.SetParent(transform, false);

            var model = new GameObject("Model").transform;
            model.SetParent(flip, false);
            model.localRotation = Quaternion.Euler(0f, 90f, 0f);    // prefab nose +Z -> root +X

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != flip) child.SetParent(model, false);
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetSize(float size, bool instant = false)
        {
            TargetSize = Mathf.Max(0.05f, size);
            if (instant)
            {
                Size = TargetSize;
                transform.localScale = Vector3.one * Size;
            }
        }

        /// <summary>Points the nose along a direction, either smoothly or at once.</summary>
        public void Face(Vector3 direction, bool instant = false)
        {
            if (!Aim(direction)) return;
            if (instant)
            {
                aim = wantAim;
                roll = facingLeft ? -180f : 0f;
                ApplyRotation();
            }
        }

        void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, aim);
            flip.localRotation = Quaternion.Euler(roll, 0f, 0f);
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in renderers)
                if (r != null) r.enabled = visible;
        }

        /// <summary>Quick squash-and-stretch gulp.</summary>
        public void Punch() => punch = 1f;

        public void Bite()
        {
            Punch();
            if (animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, AttackId))
                animator.CrossFadeInFixedTime(AttackId, 0.05f, 0, 0f);
        }

        public void GetEaten(Fish eater)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Velocity = Vector3.zero;
            StartCoroutine(Swallow(eater));
        }

        public void Revive(Vector3 position)
        {
            StopAllCoroutines();
            IsAlive = true;
            Velocity = Vector3.zero;
            transform.position = position;
            transform.localScale = Vector3.one * Size;
            SetVisible(true);
        }

        IEnumerator Swallow(Fish eater)
        {
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            const float duration = 0.18f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                Vector3 mouth = eater != null ? eater.MouthPosition : startPos;
                transform.position = Vector3.Lerp(startPos, mouth, k);
                transform.localScale = startScale * (1f - k);
                yield return null;
            }

            if (destroyWhenEaten) Destroy(gameObject);
            else SetVisible(false);
        }

        void Update()
        {
            if (!IsAlive) return;

            float dt = Time.deltaTime;

            bool resizing = !Mathf.Approximately(Size, TargetSize);
            if (resizing)
                Size = Mathf.MoveTowards(Size, TargetSize, growSpeed * Mathf.Max(0.3f, Size) * dt);

            if (resizing || punch > 0f)
            {
                // Gulp: the body bulges and shortens for a moment (root X is the fish's length).
                float bulge = 0f;
                if (punch > 0f)
                {
                    punch = Mathf.Max(0f, punch - dt / 0.22f);
                    bulge = Mathf.Sin(punch * Mathf.PI);
                }
                transform.localScale = new Vector3(
                    Size * (1f - 0.18f * bulge), Size * (1f + 0.3f * bulge), Size * (1f + 0.3f * bulge));
            }

            float speed = Velocity.magnitude;

            // The nose swings round to the swimming direction; the body rolls belly-down to match.
            if (speed > 0.05f * Size) Aim(Velocity);
            aim = Mathf.MoveTowardsAngle(aim, wantAim, TurnSpeed * dt);
            roll = Mathf.Lerp(roll, facingLeft ? -180f : 0f, 1f - Mathf.Exp(-FlipSpeed * dt));
            ApplyRotation();

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat(SwimSpeedId, Mathf.Clamp(0.6f + speed / (Size * 3f + 1f), 0.6f, 2.5f));
        }

        // Sets the wanted nose angle from a direction on the X/Y plane. Which way up the fish is only
        // changes when it is clearly heading left or right, so swimming straight up or down never
        // makes it flicker between the two.
        bool Aim(Vector3 direction)
        {
            var planar = new Vector2(direction.x, direction.y);
            float length = planar.magnitude;
            if (length < 1e-4f) return false;

            wantAim = Mathf.Atan2(planar.y, planar.x) * Mathf.Rad2Deg;
            float sideways = planar.x / length;
            if (Mathf.Abs(sideways) > 0.2f) facingLeft = sideways < 0f;
            return true;
        }
    }
}
