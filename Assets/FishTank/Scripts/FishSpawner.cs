using System.Collections.Generic;
using UnityEngine;

namespace FishTank
{
    /// <summary>
    /// Keeps the water around the player populated. Fish swim in from off the sides of the screen at
    /// every height, sized relative to the player: mostly prey, some rivals, a few predators.
    /// </summary>
    public class FishSpawner : MonoBehaviour
    {
        public int targetCount = 28;
        public float spawnInterval = 0.25f;
        [Range(0f, 1f)] public float preyChance = 0.55f;
        [Range(0f, 1f)] public float rivalChance = 0.25f;
        [Tooltip("Share of fish that swim right along the sea bed, between the leaves growing there.")]
        [Range(0f, 1f)] public float bottomChance = 0.3f;

        readonly List<AIFish> spawned = new List<AIFish>();
        float nextSpawnTime;
        bool prewarmed;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.database == null || gm.cameraRig == null || gm.Player == null) return;

            var rig = gm.cameraRig;
            float range = rig.ViewRange;
            Vector3 centre = gm.Player.Position;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                var ai = spawned[i];
                if (ai == null || !ai.Fish.IsAlive)
                {
                    spawned.RemoveAt(i);
                    continue;
                }

                Vector3 to = ai.Fish.Position - centre;
                if (Mathf.Abs(to.x) > range * 1.3f + 8f + ai.Fish.Size)
                {
                    Destroy(ai.gameObject);
                    spawned.RemoveAt(i);
                }
            }

            // Fill the view at once when the scene starts, then top it up from out of sight.
            if (!prewarmed)
            {
                prewarmed = true;
                for (int i = 0; i < targetCount * 3 && spawned.Count < targetCount; i++)
                    Spawn(gm, centre, range, true);
                return;
            }

            if (spawned.Count < targetCount && Time.time >= nextSpawnTime)
            {
                nextSpawnTime = Time.time + spawnInterval;
                Spawn(gm, centre, range, false);
            }
        }

        void Spawn(GameManager gm, Vector3 centre, float range, bool anywhere)
        {
            var all = gm.database.fish;
            if (all == null || all.Length == 0) return;

            float p = gm.Player.TargetSize;

            float roll = Random.value;
            float wanted;
            if (roll < preyChance) wanted = Random.Range(0.3f, 0.8f) * p;
            else if (roll < preyChance + rivalChance) wanted = Random.Range(0.9f, 1.6f) * p;
            else wanted = Random.Range(1.6f, 3f) * p;

            Fish prefab = PickSpecies(all, wanted);
            if (prefab == null) return;
            float size = prefab.baseSize * Mathf.Clamp(wanted / prefab.baseSize, 0.75f, 1.3f);

            // Enter from just off the left or right of the screen (or anywhere when pre-filling),
            // at any height the player can see, on the same Z = 0 plane the player swims on.
            bool fromLeft = Random.value < 0.5f;
            float side = anywhere
                ? Random.Range(-range, range)
                : (fromLeft ? -1f : 1f) * (range + size * 0.6f);

            float half = size * 0.5f;
            var pos = new Vector3(
                centre.x + side,
                centre.y + Random.Range(-1.3f, 1.3f) * gm.cameraRig.HalfHeight,
                0f);

            // Only where there is water: not past the shores, inside the island or in a gap too tight.
            if (Mathf.Abs(pos.x) > World.HalfWidth) return;
            World.Limits(pos.x, half, out float low, out float high);
            if (high < low + 0.6f) return;
            pos.y = Mathf.Clamp(pos.y, low + 0.1f, high - 0.3f);

            // Some fish are bottom dwellers: they come in low and level, down among the leaf beds.
            // Only when the sea bed is in view, so they never appear out of sight below the screen.
            bool bottom = Random.value < bottomChance && low > centre.y - gm.cameraRig.HalfHeight;
            if (bottom) pos.y = Mathf.Min(low + Random.Range(0.1f, 1.2f), high - 0.3f);

            // Never drop a fish right on top of the player.
            if ((pos - centre).magnitude < 3f + size * 1.5f + p) return;

            Fish fish = Instantiate(prefab, pos, Quaternion.identity, transform);
            fish.name = prefab.speciesName;
            fish.SetSize(size, true);

            // Swim across the screen, drifting a little up or down.
            float direction = anywhere ? (Random.value < 0.5f ? -1f : 1f) : (fromLeft ? 1f : -1f);
            var heading = new Vector3(direction, bottom ? 0f : Random.Range(-0.15f, 0.15f), 0f);

            var ai = fish.gameObject.AddComponent<AIFish>();
            float speed = (1.2f + 0.5f * size) * Random.Range(0.8f, 1.2f);
            ai.Init(heading, speed);
            spawned.Add(ai);
        }

        // Picks randomly among the three species whose natural size is closest to the wanted size.
        static Fish PickSpecies(Fish[] all, float wanted)
        {
            const int pool = 3;
            var best = new Fish[pool];
            var bestScore = new float[pool];
            int found = 0;

            foreach (var f in all)
            {
                if (f == null) continue;
                float score = Mathf.Abs(Mathf.Log(f.baseSize / wanted));
                int slot = found < pool ? found++ : -1;
                if (slot < 0)
                {
                    int worst = 0;
                    for (int i = 1; i < pool; i++)
                        if (bestScore[i] > bestScore[worst]) worst = i;
                    if (score < bestScore[worst]) slot = worst;
                }

                if (slot >= 0)
                {
                    best[slot] = f;
                    bestScore[slot] = score;
                }
            }

            return found == 0 ? null : best[Random.Range(0, found)];
        }
    }
}
