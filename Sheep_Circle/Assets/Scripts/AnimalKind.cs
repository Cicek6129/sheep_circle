using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// One species on the roundabout. Ordinary animals all circle at the same
    /// angular speed (so nobody rear-ends anybody); they differ in how much room
    /// they take up and how fast they trot in from the queue.
    /// The shepherd is the exception - see the Special block.
    /// </summary>
    [System.Serializable]
    public class AnimalKind
    {
        public string displayName = "Koyun";

        [Header("Visuals")]
        public Sprite bodySprite;
        [Tooltip("Optional overlay drawn on top of the body: cow spots, the shepherd's hat.")]
        public Sprite patchSprite;
        public Color bodyColor = Color.white;
        public Color patchColor = Color.black;
        public Color headColor = new Color(0.23f, 0.23f, 0.27f);
        [Tooltip("Off for sprites that already include a head.")]
        public bool showHead = true;

        [Header("Gameplay")]
        [Tooltip("World-space diameter of the animal.")]
        public float size = 0.62f;

        [Tooltip("Radius used for crash checks. Keep a little under half the size.")]
        public float collisionRadius = 0.28f;

        [Tooltip("Multiplier on the base speed when trotting from the queue onto the ring.")]
        public float enterSpeedMul = 1f;

        [Tooltip("Relative chance of this kind showing up in a queue.")]
        public float spawnWeight = 5f;

        [Header("Special")]
        [Tooltip("Sweeps the ring gathering everyone it catches, and never crashes.")]
        public bool isShepherd;

        [Tooltip("Speed around the ring relative to everyone else. Only the shepherd should differ.")]
        public float ringSpeedMul = 1f;

        [Tooltip("Extra full laps before heading for the exit.")]
        public int extraLaps;
    }
}
