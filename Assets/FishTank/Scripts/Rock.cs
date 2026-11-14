using System.Collections.Generic;
using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// A solid, rounded rock (a scaled sphere). Fish cannot swim through it: they slide round or
    /// over it, which is what makes weaving between the rocks a thing.
    /// </summary>
    public class Rock : MonoBehaviour
    {
        static readonly List<Rock> All = new List<Rock>();

        Vector3 centre;
        Vector3 scale;
        float boundRadius;
        bool registered;

        void OnEnable()
        {
            centre = transform.position;
            Vector3 s = transform.lossyScale;
            scale = new Vector3(Mathf.Max(0.01f, s.x), Mathf.Max(0.01f, s.y), Mathf.Max(0.01f, s.z));
            boundRadius = 0.5f * Mathf.Max(scale.x, scale.y, scale.z);

            // Rocks that come nowhere near the swimming plane (Z = 0) are scenery only.
            registered = Mathf.Abs(centre.z) < boundRadius + 5f;
            if (registered) All.Add(this);
        }

        void OnDisable()
        {
            if (registered) All.Remove(this);
        }

        /// <summary>
        /// Moves a body of the given radius out of any rock it overlaps, sliding it along the X/Y
        /// swimming plane only: its Z never changes. Rocks that do not reach that plane are scenery.
        /// </summary>
        public static Vector3 PushOut(Vector3 position, float radius)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Rock rock = All[i];
                float reach = rock.boundRadius + radius;
                if ((position - rock.centre).sqrMagnitude > reach * reach) continue;

                // In the rock's own space it is a sphere of radius 0.5; grow that by the body's radius.
                Vector3 local = rock.transform.InverseTransformPoint(position);
                float ax = 0.5f + radius / rock.scale.x;
                float ay = 0.5f + radius / rock.scale.y;
                float az = 0.5f + radius / rock.scale.z;
                float inside = (local.x * local.x) / (ax * ax) + (local.y * local.y) / (ay * ay) +
                               (local.z * local.z) / (az * az);
                if (inside >= 1f) continue;

                // Outward direction of the rock's surface here, flattened onto the swimming plane.
                var normal = new Vector3(local.x / (ax * ax), local.y / (ay * ay), local.z / (az * az));
                Vector3 push = rock.transform.rotation * new Vector3(
                    normal.x / rock.scale.x, normal.y / rock.scale.y, normal.z / rock.scale.z);
                push.z = 0f;
                push = push.sqrMagnitude > 1e-10f ? push.normalized : Vector3.up;

                // Slide along that direction until the body is exactly on the surface:
                // solve a t^2 + b t + c = 0 for the distance t, in the rock's own space.
                Vector3 step = rock.transform.InverseTransformVector(push);
                float a = (step.x * step.x) / (ax * ax) + (step.y * step.y) / (ay * ay) + (step.z * step.z) / (az * az);
                float b = 2f * ((local.x * step.x) / (ax * ax) + (local.y * step.y) / (ay * ay) +
                                (local.z * step.z) / (az * az));
                float c = inside - 1f;
                if (a < 1e-10f) continue;

                float t = (-b + Mathf.Sqrt(Mathf.Max(0f, b * b - 4f * a * c))) / (2f * a);
                position += push * t;
            }

            return position;
        }
    }
}
