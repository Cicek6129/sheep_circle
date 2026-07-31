using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// Describes a single level: how many animals start on the ring, how many
    /// the player must insert, and how fast the ring spins.
    /// </summary>
    [System.Serializable]
    public class LevelData
    {
        [Tooltip("Number of animals already circling on the ring when the level starts.")]
        public int initialAnimalCount = 6;

        [Tooltip("Number of animals the player must successfully insert to clear the level.")]
        public int animalsToSend = 6;

        [Tooltip("Angular speed of the ring in degrees per second.")]
        public float ringSpeed = 42f;

        [Tooltip("Allow the shepherd (clears animals from the ring) in the queue.")]
        public bool allowShepherd;

        [Tooltip("If not empty, exactly these animals will be spawned on the ring. Leave empty for random generation.")]
        public string[] explicitInitialAnimals;

        [Tooltip("Custom angles (degrees) for each initial animal. Must match explicitInitialAnimals length. Leave empty for equal spacing.")]
        public float[] explicitInitialAngles;

        [Tooltip("If not empty, exactly these animals will be queued. Leave empty for random generation.")]
        public string[] explicitQueueAnimals;
    }
}
