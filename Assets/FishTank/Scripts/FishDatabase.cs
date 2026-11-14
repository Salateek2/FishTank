using UnityEngine;

namespace FishTank
{
    /// <summary>All fish prefabs, sorted from smallest to largest species.</summary>
    public class FishDatabase : ScriptableObject
    {
        public Fish playerPrefab;
        public Fish[] fish;
    }
}
