using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SheepCircle
{
    /// <summary>
    /// Drives the game: spawns the initial circling animals, fills the entry
    /// queue, lets the player release them onto the ring, detects crashes and
    /// manages the level progression.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] RingGeometry geometry = new RingGeometry();
        [SerializeField] Animal animalPrefab;
        [SerializeField] Burst burstPrefab;
        [SerializeField] Burst dustPrefab;
        [SerializeField] EntryLane entryLane;
        [SerializeField] Transform animalParent;
        [SerializeField] HUD hud;
        [SerializeField] new Camera camera;

        [Header("Animals")]
        [SerializeField] AnimalKind[] kinds;

        [Header("Levels")]
        [Tooltip("Hand-authored levels. Beyond these the game generates levels procedurally.")]
        [SerializeField] LevelData[] levels = new LevelData[]
        {
            new LevelData { initialAnimalCount = 4, animalsToSend = 4, ringSpeed = 35f },
            new LevelData { initialAnimalCount = 5, animalsToSend = 5, ringSpeed = 40f },
            new LevelData { initialAnimalCount = 6, animalsToSend = 6, ringSpeed = 45f },
            new LevelData { initialAnimalCount = 7, animalsToSend = 6, ringSpeed = 50f },
            new LevelData { initialAnimalCount = 8, animalsToSend = 7, ringSpeed = 55f, allowShepherd = true },
            new LevelData { initialAnimalCount = 9, animalsToSend = 8, ringSpeed = 60f, allowShepherd = true },
            new LevelData { initialAnimalCount = 10, animalsToSend = 8, ringSpeed = 65f, allowShepherd = true },
        };

        [Header("Rules")]
        [SerializeField] int maxQueuePerLane = 4;
        [SerializeField] float releaseCooldown = 0.34f;
        [SerializeField] float restartDelay = 0.6f;
        [SerializeField] float levelCompleteDelay = 1.5f;
        [Tooltip("Minimum level index (0-based) before a shepherd can appear.")]
        [SerializeField] int shepherdMinLevel = 4;

        const string BestLevelKey = "SheepCircle.BestLevel";

        /// <summary>Survives the scene reload a restart does, so the title card
        /// greets the player once per launch instead of after every crash.</summary>
        static bool titleShown;

        readonly List<Animal> animals = new List<Animal>();
        readonly List<Animal> finished = new List<Animal>();

        float cooldownTimer;
        float gameOverTimer;
        float levelCompleteTimer;
        int currentLevel;
        int animalsPlaced;
        int regularAnimalsCreated;
        int totalRegularToSend;
        int shepherdsAlive;
        bool shepherdCreatedThisLevel;
        bool gameOver;
        bool levelComplete;
        bool waitingToStart;

        public RingGeometry Geometry => geometry;

        // ----------------------------------------------------------- level data

        LevelData GetLevelData(int level)
        {
            if (levels != null && level < levels.Length) return levels[level];

            // Procedural generation beyond hand-authored levels.
            return new LevelData
            {
                initialAnimalCount = Mathf.Min(18, 6 + level),
                animalsToSend = Mathf.Min(14, 4 + level),
                ringSpeed = Mathf.Min(120f, 35f + level * 6f),
                allowShepherd = level >= shepherdMinLevel
            };
        }

        // ----------------------------------------------------------- lifecycle

        void Awake()
        {
            if (camera == null) camera = Camera.main;
        }

        void Start()
        {
            currentLevel = 0;

            int bestLevel = PlayerPrefs.GetInt(BestLevelKey, 0);
            hud.SetScore(0);
            hud.SetBest(bestLevel);
            hud.HideGameOver();

            waitingToStart = !titleShown;
            if (waitingToStart)
            {
                hud.ShowStart(bestLevel);
            }
            else
            {
                hud.HideStart();
                LoadLevel(0);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (waitingToStart)
            {
                TickAnimals(dt);

                if (AnyPressed())
                {
                    waitingToStart = false;
                    titleShown = true;
                    hud.HideStart();
                    LoadLevel(0);
                }
                return;
            }

            if (levelComplete)
            {
                TickAnimals(dt);
                levelCompleteTimer -= dt;
                if (levelCompleteTimer <= 0f && AnyPressed())
                    LoadLevel(currentLevel + 1);
                return;
            }

            if (gameOver)
            {
                gameOverTimer -= dt;
                if (gameOverTimer <= 0f && AnyPressed()) RestartLevel();
                return;
            }

            HandleInput();
            TickAnimals(dt);
            CheckHerding();
            CheckCrashes();
            ClearFrameFlags();

            cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);
        }

        // ----------------------------------------------------------- levels

        void LoadLevel(int level)
        {
            currentLevel = level;
            animalsPlaced = 0;
            regularAnimalsCreated = 0;
            shepherdsAlive = 0;
            shepherdCreatedThisLevel = false;
            cooldownTimer = 0f;
            gameOverTimer = 0f;
            levelCompleteTimer = 0f;
            gameOver = false;
            levelComplete = false;

            // Destroy every animal still alive from the previous level.
            for (int i = animals.Count - 1; i >= 0; i--)
                if (animals[i] != null) Destroy(animals[i].gameObject);
            animals.Clear();

            if (entryLane != null) entryLane.Clear();

            LevelData data = GetLevelData(level);
            totalRegularToSend = data.animalsToSend;

            hud.SetLevel(level + 1);
            hud.SetProgress(0, totalRegularToSend);
            hud.HideLevelComplete();
            hud.HideGameOver();

            SpawnInitialAnimals(data);
            RefillQueue(data);
        }

        void SpawnInitialAnimals(LevelData data)
        {
            float angleStep = 360f / data.initialAnimalCount;
            for (int i = 0; i < data.initialAnimalCount; i++)
            {
                AnimalKind kind = PickNonShepherdKind();
                Animal animal = Instantiate(animalPrefab, animalParent);
                animal.SetupAsCircling(kind, i * angleStep, geometry);
                animals.Add(animal);
            }
        }

        void RefillQueue(LevelData data)
        {
            while (entryLane != null && entryLane.QueueCount < maxQueuePerLane)
            {
                bool allRegularDone = regularAnimalsCreated >= totalRegularToSend;
                bool shepherdDone = shepherdCreatedThisLevel || !data.allowShepherd;

                if (allRegularDone && shepherdDone) break;

                // Maybe inject the shepherd once some regulars have been created.
                if (data.allowShepherd && !shepherdCreatedThisLevel
                    && shepherdsAlive == 0 && regularAnimalsCreated >= 2
                    && Random.value < 0.2f)
                {
                    AnimalKind sk = PickShepherdKind();
                    if (sk != null)
                    {
                        SpawnQueueAnimal(sk);
                        shepherdCreatedThisLevel = true;
                        shepherdsAlive++;
                        continue;
                    }
                }

                if (!allRegularDone)
                {
                    SpawnQueueAnimal(PickNonShepherdKind());
                    regularAnimalsCreated++;
                }
                else
                {
                    break;
                }
            }
        }

        void SpawnQueueAnimal(AnimalKind kind)
        {
            Animal animal = Instantiate(animalPrefab, animalParent);
            animal.Setup(kind, RingGeometry.ENTRY_LANE, entryLane.QueueCount, geometry);
            entryLane.Enqueue(animal);
            animals.Add(animal);
        }

        void LevelCompleted()
        {
            if (levelComplete) return;

            levelComplete = true;
            levelCompleteTimer = levelCompleteDelay;

            int best = PlayerPrefs.GetInt(BestLevelKey, 0);
            if (currentLevel + 1 > best)
            {
                best = currentLevel + 1;
                PlayerPrefs.SetInt(BestLevelKey, best);
                PlayerPrefs.Save();
            }

            hud.SetBest(best);
            hud.ShowLevelComplete(currentLevel + 1);
        }

        // ----------------------------------------------------------- input

        void HandleInput()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
                TryRelease();

            Keyboard keys = Keyboard.current;
            if (keys == null) return;

            if (keys.spaceKey.wasPressedThisFrame) TryRelease();
        }

        /// <summary>Dismisses the title card and, later, the game-over card. The
        /// press is swallowed here because Update returns straight after, so the
        /// same tap can never also release an animal.</summary>
        bool AnyPressed()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) return true;

            Keyboard keys = Keyboard.current;
            return keys != null && (keys.rKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame);
        }

        void TryRelease()
        {
            if (entryLane == null || !entryLane.HasWaiting) return;
            if (cooldownTimer > 0f) return;

            Animal animal = entryLane.Dequeue();
            if (animal == null) return;

            // Shepherd exits from the top, regular animals stay on the ring.
            int exit = animal.IsShepherd ? RingGeometry.EXIT_LANE : RingGeometry.ENTRY_LANE;
            animal.Release(exit);

            // Dust puff behind the animal as it moves off.
            Vector2 behind = geometry.LaneDir(RingGeometry.ENTRY_LANE) * (animal.Kind.size * 0.45f);
            SpawnEffect(dustPrefab, animal.Position + behind, animal.Kind.size);

            cooldownTimer = Mathf.Max(releaseCooldown, animal.MergeSeconds(geometry) + 0.06f);

            // Refill the queue with the next waiting animal.
            RefillQueue(GetLevelData(currentLevel));
        }

        // ----------------------------------------------------------- kind picking

        AnimalKind PickNonShepherdKind()
        {
            float total = 0f;
            for (int i = 0; i < kinds.Length; i++)
                if (!kinds[i].isShepherd) total += kinds[i].spawnWeight;

            float roll = Random.value * total;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i].isShepherd) continue;
                roll -= kinds[i].spawnWeight;
                if (roll <= 0f) return kinds[i];
            }

            // Fallback.
            for (int i = 0; i < kinds.Length; i++)
                if (!kinds[i].isShepherd) return kinds[i];
            return kinds[0];
        }

        AnimalKind PickShepherdKind()
        {
            for (int i = 0; i < kinds.Length; i++)
                if (kinds[i].isShepherd) return kinds[i];
            return null;
        }

        // ----------------------------------------------------------- simulation

        void TickAnimals(float dt)
        {
            LevelData data = GetLevelData(currentLevel);
            float ringSpeed = data.ringSpeed;
            finished.Clear();

            for (int i = 0; i < animals.Count; i++)
            {
                if (animals[i].State == AnimalState.Herded) continue;

                AnimalState prev = animals[i].State;
                if (animals[i].Tick(dt, geometry, ringSpeed))
                    finished.Add(animals[i]);

                // A regular animal just landed on the ring.
                if (prev == AnimalState.Entering
                    && animals[i].State == AnimalState.CirclingInside
                    && !animals[i].IsShepherd)
                {
                    animalsPlaced++;
                    hud.SetProgress(animalsPlaced, totalRegularToSend);
                    hud.SetScore(animalsPlaced);

                    if (animalsPlaced >= totalRegularToSend)
                        LevelCompleted();
                }
            }

            for (int i = 0; i < finished.Count; i++) Finish(finished[i]);
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
                }
                shepherdsAlive--;
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

                    // At least one of them must be merging - two animals that are
                    // already circling at the same speed can never catch each other.
                    if (!animals[i].IsMerging && !animals[j].IsMerging) continue;

                    float reach = animals[i].CollisionRadius + animals[j].CollisionRadius;
                    if ((animals[i].Position - animals[j].Position).sqrMagnitude > reach * reach) continue;

                    SpawnEffect(burstPrefab, (animals[i].Position + animals[j].Position) * 0.5f, reach * 1.9f);

                    // Invariant casing: the Turkish 'i' would otherwise become a
                    // dotted capital that the default font atlas has no glyph for.
                    EndGame($"{animals[i].Kind.displayName.ToUpperInvariant()} ile {animals[j].Kind.displayName.ToUpperInvariant()} TOSLADI!");
                    return;
                }
            }
        }

        void ClearFrameFlags()
        {
            for (int i = 0; i < animals.Count; i++)
                animals[i].ClearFrameFlags();
        }

        void SpawnEffect(Burst prefab, Vector2 at, float size)
        {
            if (prefab == null) return;

            Burst effect = Instantiate(prefab, animalParent);
            effect.Play(at, size);
        }

        // ----------------------------------------------------------- game state

        void EndGame(string reason)
        {
            if (gameOver) return;

            gameOver = true;
            gameOverTimer = restartDelay;

            int best = PlayerPrefs.GetInt(BestLevelKey, 0);
            hud.SetBest(best);
            hud.ShowGameOver(reason, animalsPlaced);
        }

        void RestartLevel() => LoadLevel(currentLevel);

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
