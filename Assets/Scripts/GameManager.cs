using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SheepCircle
{
    /// <summary>
    /// Owns the round: spawns animals into the lane queues, releases them onto the
    /// ring when the player taps, checks for crashes and scales the difficulty.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] RingGeometry geometry = new RingGeometry();
        [SerializeField] Animal animalPrefab;
        [SerializeField] Burst burstPrefab;
        [SerializeField] Burst dustPrefab;
        [SerializeField] EntryLane[] lanes;
        [SerializeField] Transform animalParent;
        [SerializeField] HUD hud;
        [SerializeField] new Camera camera;

        [Header("Animals")]
        [SerializeField] AnimalKind[] kinds;

        [Header("Difficulty")]
        [SerializeField] float startRingSpeed = 42f;
        [SerializeField] float ringSpeedPerScore = 1.15f;
        [SerializeField] float maxRingSpeed = 108f;
        [SerializeField] float startSpawnInterval = 1.9f;
        [SerializeField] float spawnRampPerScore = 0.03f;
        [SerializeField] float minSpawnInterval = 0.6f;

        [Header("Rules")]
        [SerializeField] int maxQueuePerLane = 4;
        [SerializeField] float releaseCooldown = 0.34f;
        [SerializeField] float restartDelay = 0.6f;
        [Tooltip("Shortest arc an animal is allowed to travel before leaving the ring.")]
        [SerializeField] float minExitArc = 60f;
        [Tooltip("No shepherd shows up before this score, and never two at once.")]
        [SerializeField] int shepherdMinScore = 5;

        const string BestScoreKey = "SheepCircle.Best";

        /// <summary>Survives the scene reload a restart does, so the title card
        /// greets the player once per launch instead of after every crash.</summary>
        static bool titleShown;

        readonly List<Animal> animals = new List<Animal>();
        readonly List<Animal> finished = new List<Animal>();

        float[] laneCooldown;
        float spawnTimer;
        float gameOverTimer;
        int score;
        int shepherdsAlive;
        bool gameOver;
        bool waitingToStart;

        public RingGeometry Geometry => geometry;

        float RingSpeed => Mathf.Min(maxRingSpeed, startRingSpeed + score * ringSpeedPerScore);
        float SpawnInterval => Mathf.Max(minSpawnInterval, startSpawnInterval - score * spawnRampPerScore);

        void Awake()
        {
            if (camera == null) camera = Camera.main;
            laneCooldown = new float[lanes.Length];
        }

        void Start()
        {
            score = 0;
            spawnTimer = 0.4f;

            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            hud.SetScore(0);
            hud.SetBest(best);
            hud.HideGameOver();

            // One waiting animal per lane so the board is never empty at the start.
            for (int i = 0; i < lanes.Length; i++) SpawnInto(lanes[i]);

            waitingToStart = !titleShown;
            if (waitingToStart) hud.ShowStart(best);
            else hud.HideStart();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (waitingToStart)
            {
                // Let the queues walk up to their slots so the board behind the
                // title card is alive, but hold off spawning and taps.
                TickAnimals(dt);

                if (AnyPressed())
                {
                    waitingToStart = false;
                    titleShown = true;
                    hud.HideStart();
                }
                return;
            }

            if (gameOver)
            {
                gameOverTimer -= dt;
                if (gameOverTimer <= 0f && AnyPressed()) Restart();
                return;
            }

            HandleInput();
            UpdateSpawning(dt);
            TickAnimals(dt);
            CheckHerding();
            CheckCrashes();

            for (int i = 0; i < laneCooldown.Length; i++)
                laneCooldown[i] = Mathf.Max(0f, laneCooldown[i] - dt);
        }

        // ---------------------------------------------------------------- input

        void HandleInput()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                Vector2 screenPos = pointer.position.ReadValue();
                if (hud != null && hud.SoundButtonRect != null && 
                    RectTransformUtility.RectangleContainsScreenPoint(hud.SoundButtonRect, screenPos))
                {
                    hud.ToggleSound();
                    AudioManager.Instance?.PlayTap();
                    return;
                }

                Vector2 world = camera.ScreenToWorldPoint(screenPos);
                Collider2D hit = Physics2D.OverlapPoint(world);

                EntryLane lane = null;
                if (hit != null) hit.TryGetComponent(out lane);

                // Tapping anywhere else still counts: use whichever road is closest.
                if (lane == null) lane = LaneByIndex(geometry.NearestLane(world));

                TryRelease(lane);
            }

            Keyboard keys = Keyboard.current;
            if (keys == null) return;

            if (keys.digit1Key.wasPressedThisFrame) TryRelease(LaneByIndex(0));
            if (keys.digit2Key.wasPressedThisFrame) TryRelease(LaneByIndex(1));
            if (keys.digit3Key.wasPressedThisFrame) TryRelease(LaneByIndex(2));
            if (keys.digit4Key.wasPressedThisFrame) TryRelease(LaneByIndex(3));
        }

        /// <summary>Dismisses the title card and, later, the game-over card. The
        /// press is swallowed here because Update returns straight after, so the
        /// same tap can never also release an animal.</summary>
        bool AnyPressed()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                if (hud != null && hud.SoundButtonRect != null)
                {
                    Vector2 screenPos = pointer.position.ReadValue();
                    if (RectTransformUtility.RectangleContainsScreenPoint(hud.SoundButtonRect, screenPos))
                    {
                        hud.ToggleSound();
                        AudioManager.Instance?.PlayTap();
                        return false;
                    }
                }
                return true;
            }

            Keyboard keys = Keyboard.current;
            return keys != null && (keys.rKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame);
        }

        EntryLane LaneByIndex(int index)
        {
            for (int i = 0; i < lanes.Length; i++)
                if (lanes[i].LaneIndex == index) return lanes[i];
            return null;
        }

        void TryRelease(EntryLane lane)
        {
            if (lane == null || !lane.HasWaiting) return;

            int slot = System.Array.IndexOf(lanes, lane);
            if (slot < 0 || laneCooldown[slot] > 0f) return;

            Animal animal = lane.Dequeue();
            if (animal == null) return;

            // The shepherd loops all the way round and leaves by his own road.
            int exit = animal.IsShepherd ? lane.LaneIndex : PickExitLane(lane.LaneIndex);
            animal.Release(exit);
            AudioManager.Instance?.PlayTap();

            // Kicked up where it was standing, i.e. behind it once it moves off.
            // This is the only immediate answer the player gets to their tap, so
            // it fires on release rather than when the animal reaches the ring.
            Vector2 behind = geometry.LaneDir(lane.LaneIndex) * (animal.Kind.size * 0.45f);
            SpawnEffect(dustPrefab, animal.Position + behind, animal.Kind.size);

            // Hold the lane until this one is clear of the approach, otherwise a
            // slow cow would be rear-ended by whatever the player taps next.
            laneCooldown[slot] = Mathf.Max(releaseCooldown, animal.MergeSeconds(geometry) + 0.06f);
        }

        int PickExitLane(int fromLane)
        {
            List<int> options = new List<int>();
            for (int i = 0; i < geometry.laneCount; i++)
                if (geometry.ArcBetween(fromLane, i) >= minExitArc) options.Add(i);

            if (options.Count == 0) return fromLane;
            return options[Random.Range(0, options.Count)];
        }

        // -------------------------------------------------------------- spawning

        void UpdateSpawning(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;

            spawnTimer = SpawnInterval;

            EntryLane target = EmptiestLane();
            if (target == null)
            {
                EndGame("YOL TIKANDI!");
                return;
            }

            SpawnInto(target);
        }

        EntryLane EmptiestLane()
        {
            EntryLane best = null;
            int bestCount = int.MaxValue;

            for (int i = 0; i < lanes.Length; i++)
            {
                int count = lanes[i].QueueCount;
                if (count >= maxQueuePerLane) continue;

                // Random tiebreak so the queues do not fill in a fixed order.
                if (count < bestCount || (count == bestCount && Random.value < 0.5f))
                {
                    bestCount = count;
                    best = lanes[i];
                }
            }

            return best;
        }

        void SpawnInto(EntryLane lane)
        {
            AnimalKind kind = PickKind();

            Animal animal = Instantiate(animalPrefab, animalParent);
            animal.Setup(kind, lane.LaneIndex, lane.QueueCount, geometry);
            lane.Enqueue(animal);
            animals.Add(animal);

            if (kind.isShepherd) shepherdsAlive++;
        }

        AnimalKind PickKind()
        {
            bool allowShepherd = score >= shepherdMinScore && shepherdsAlive == 0;

            float total = 0f;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i].isShepherd && !allowShepherd) continue;
                total += kinds[i].spawnWeight;
            }

            float roll = Random.value * total;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i].isShepherd && !allowShepherd) continue;

                roll -= kinds[i].spawnWeight;
                if (roll <= 0f) return kinds[i];
            }

            // Fallback: first non-shepherd kind.
            for (int i = 0; i < kinds.Length; i++)
                if (!kinds[i].isShepherd) return kinds[i];

            return kinds[0];
        }

        // ------------------------------------------------------------- simulation

        void TickAnimals(float dt)
        {
            float ringSpeed = RingSpeed;
            finished.Clear();

            for (int i = 0; i < animals.Count; i++)
            {
                // Herded animals are driven by the shepherd, not by themselves.
                if (animals[i].State == AnimalState.Herded) continue;
                if (animals[i].Tick(dt, geometry, ringSpeed)) finished.Add(animals[i]);
            }

            for (int i = 0; i < finished.Count; i++) Finish(finished[i]);
            if (finished.Count > 0) hud.SetScore(score);
        }

        void Finish(Animal animal)
        {
            if (animal.IsShepherd)
            {
                IReadOnlyList<Animal> herd = animal.Herd;
                for (int i = 0; i < herd.Count; i++)
                {
                    animals.Remove(herd[i]);
                    Destroy(herd[i].gameObject);
                    score++;
                AudioManager.Instance?.PlayScore();
            }
            shepherdsAlive--;
        }
        else
        {
            score++;
            AudioManager.Instance?.PlayScore();
        }

            animals.Remove(animal);
            Destroy(animal.gameObject);
        }

        void CheckHerding()
        {
            if (shepherdsAlive == 0) return;

            for (int i = 0; i < animals.Count; i++)
            {
                Animal shepherd = animals[i];
                if (!shepherd.IsShepherd) continue;
                if (shepherd.State != AnimalState.OnRing && shepherd.State != AnimalState.Exiting) continue;

                for (int j = 0; j < animals.Count; j++)
                {
                    Animal other = animals[j];
                    if (!other.CanBeHerded) continue;

                    float reach = shepherd.CollisionRadius + other.CollisionRadius + 0.06f;
                    if ((shepherd.Position - other.Position).sqrMagnitude <= reach * reach)
                        shepherd.Collect(other);
                }
            }
        }

        void CheckCrashes()
        {
            for (int i = 0; i < animals.Count; i++)
            {
                if (!animals[i].CanCrash) continue;

                for (int j = i + 1; j < animals.Count; j++)
                {
                    if (!animals[j].CanCrash) continue;

                    // A crash is always someone merging badly. Two animals already
                    // circling travel at the same speed, so they can never touch.
                    if (!animals[i].IsMerging && !animals[j].IsMerging) continue;

                    float reach = animals[i].CollisionRadius + animals[j].CollisionRadius;
                    if ((animals[i].Position - animals[j].Position).sqrMagnitude > reach * reach) continue;

                    // Burst goes where they actually met, sized to the pair, so a
                    // cow taking out a chicken lands heavier than two chickens.
                    // 1.9x covers both bodies with a little margin; much above
                    // that and it spills off the ring onto the grass.
                    SpawnEffect(burstPrefab, (animals[i].Position + animals[j].Position) * 0.5f, reach * 1.9f);

                    // Invariant casing: the Turkish 'i' would otherwise become a
                    // dotted capital that the default font atlas has no glyph for.
                    AudioManager.Instance?.PlayCrash();
                    EndGame($"{animals[i].Kind.displayName.ToUpperInvariant()} ile {animals[j].Kind.displayName.ToUpperInvariant()} TOSLADI!");
                    return;
                }
            }
        }

        void SpawnEffect(Burst prefab, Vector2 at, float size)
        {
            if (prefab == null) return;

            Burst effect = Instantiate(prefab, animalParent);
            effect.Play(at, size);
        }

        // ------------------------------------------------------------- game state

        void EndGame(string reason)
        {
            if (gameOver) return;

            gameOver = true;
            gameOverTimer = restartDelay;

            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt(BestScoreKey, best);
                PlayerPrefs.Save();
            }

            hud.SetBest(best);
            hud.ShowGameOver(reason, score);
        }

        void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (geometry == null) return;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Vector3 prev = geometry.PointOnRing(0f);
            for (int i = 1; i <= 72; i++)
            {
                Vector3 next = geometry.PointOnRing(i * 5f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            for (int lane = 0; lane < geometry.laneCount; lane++)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(geometry.MergePoint(lane), 0.15f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(geometry.ExitPos(lane, 0f), 0.15f);

                Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
                for (int slot = 0; slot < 4; slot++)
                    Gizmos.DrawWireSphere(geometry.QueuePos(lane, slot), 0.2f);
            }
        }
#endif
    }
}
