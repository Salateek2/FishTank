using System.Collections.Generic;
using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Something worth finding: a pearl or a treasure chest. Swim into it to collect it.
    /// </summary>
    public class Treasure : MonoBehaviour
    {
        public static readonly List<Treasure> All = new List<Treasure>();

        public int points = 50;
        [Tooltip("Chests are big finds: bigger reward, bigger fanfare.")]
        public bool big;
        public float reach = 1.2f;
        public bool spin = true;

        Vector3 origin;
        float phase;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            // Never leave one buried inside a solid rock.
            origin = Rock.PushOut(transform.position, reach);
            phase = origin.x * 0.7f;
        }

        void Update()
        {
            float bob = Mathf.Sin(Time.time * 1.6f + phase) * (big ? 0.12f : 0.3f);
            transform.position = origin + Vector3.up * bob;
            if (spin) transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || gm.Player == null || !gm.Player.IsAlive) return;

            float range = reach + gm.Player.Radius + 0.3f * gm.Player.Size;
            if ((gm.Player.Position - origin).sqrMagnitude > range * range) return;

            gm.Collect(this);
            Destroy(gameObject);
        }
    }
}
