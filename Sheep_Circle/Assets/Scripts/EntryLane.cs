using System.Collections.Generic;
using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// One approach road. Owns the queue of animals waiting to be let onto the
    /// ring and carries the collider the player taps.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class EntryLane : MonoBehaviour
    {
        [SerializeField] int laneIndex;
        [SerializeField] SpriteRenderer road;

        public int LaneIndex => laneIndex;
        public int QueueCount => queue.Count;
        public bool HasWaiting => queue.Count > 0;

        readonly List<Animal> queue = new List<Animal>();

        float flashTimer;
        Color roadBaseColor;

        void Awake()
        {
            if (road != null) roadBaseColor = road.color;
        }

        void Update()
        {
            if (road == null || flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / 0.18f);

            // Brighten toward white rather than multiplying: the road carries a
            // texture now, and its tint sits just under white, so scaling the
            // base colour up would clip instead of reading as a flash.
            road.color = Color.Lerp(roadBaseColor, Color.white, t * 0.9f);
        }

        public void Enqueue(Animal animal)
        {
            animal.SetQueueIndex(queue.Count);
            queue.Add(animal);
        }

        /// <summary>Pops the animal at the head of the queue, or null if empty.</summary>
        public Animal Dequeue()
        {
            if (queue.Count == 0) return null;

            Animal head = queue[0];
            queue.RemoveAt(0);
            for (int i = 0; i < queue.Count; i++) queue[i].SetQueueIndex(i);

            flashTimer = 0.18f;
            return head;
        }

        public void Clear() => queue.Clear();
    }
}
