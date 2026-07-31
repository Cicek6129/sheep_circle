using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("Wool tufts and a feather, thrown out by a crash.")]
        [SerializeField] Burst debrisPrefab;
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
            // Level 1: 4 on ring (2 close, 2 far apart), 8 in queue
            new LevelData { 
                ringSpeed = 35f, 
                explicitInitialAnimals = new string[] { "Tavuk", "Koyun", "Koyun", "Inek" },
                explicitInitialAngles  = new float[]   { 0f, 25f, 160f, 220f },
                explicitQueueAnimals = new string[] { "Koyun", "Inek", "Koyun", "Tavuk", "Keci", "Koyun", "Tavuk", "Coban" } 
            },
            // Level 2: 5 on ring (3 clustered at top, 2 spread at bottom), 9 in queue
            new LevelData { 
                ringSpeed = 40f, 
                explicitInitialAnimals = new string[] { "Keci", "Koyun", "Inek", "Tavuk", "Keci" },
                explicitInitialAngles  = new float[]   { 0f, 30f, 55f, 180f, 270f },
                explicitQueueAnimals = new string[] { "Inek", "Koyun", "Keci", "Coban", "Tavuk", "Inek", "Koyun", "Keci", "Tavuk" } 
            },
            // Level 3: 6 on ring (2 pairs close + 2 loners), 10 in queue
            new LevelData { 
                ringSpeed = 45f, 
                explicitInitialAnimals = new string[] { "Inek", "Tavuk", "Koyun", "Koyun", "Keci", "Inek" },
                explicitInitialAngles  = new float[]   { 10f, 35f, 120f, 150f, 240f, 310f },
                explicitQueueAnimals = new string[] { "Koyun", "Keci", "Tavuk", "Koyun", "Coban", "Inek", "Tavuk", "Keci", "Koyun", "Inek" } 
            },
            // Level 4: 7 on ring (4 clustered + 3 spread), 11 in queue
            new LevelData { 
                ringSpeed = 50f, 
                explicitInitialAnimals = new string[] { "Tavuk", "Keci", "Inek", "Koyun", "Tavuk", "Keci", "Koyun" },
                explicitInitialAngles  = new float[]   { 0f, 22f, 50f, 80f, 170f, 250f, 320f },
                explicitQueueAnimals = new string[] { "Inek", "Koyun", "Coban", "Keci", "Tavuk", "Inek", "Koyun", "Keci", "Tavuk", "Koyun", "Inek" } 
            },
            // Level 5: 8 on ring (3 tight group + 2 pair + 3 spread), 12 in queue
            new LevelData { 
                ringSpeed = 55f, 
                explicitInitialAnimals = new string[] { "Koyun", "Inek", "Keci", "Tavuk", "Koyun", "Inek", "Keci", "Tavuk" },
                explicitInitialAngles  = new float[]   { 5f, 28f, 55f, 130f, 155f, 210f, 280f, 340f },
                explicitQueueAnimals = new string[] { "Tavuk", "Keci", "Inek", "Koyun", "Coban", "Tavuk", "Keci", "Inek", "Koyun", "Tavuk", "Keci", "Inek" } 
            },
            new LevelData { initialAnimalCount = 9, animalsToSend = 8, ringSpeed = 60f, allowShepherd = true },
            new LevelData { initialAnimalCount = 10, animalsToSend = 8, ringSpeed = 65f, allowShepherd = true },
        };

        [Header("Rules")]
        [SerializeField] int maxQueuePerLane = 4;
        [SerializeField] float releaseCooldown = 0.34f;
        [SerializeField] float restartDelay = 0.6f;
        [SerializeField] float levelCompleteDelay = 1.5f;
        [Tooltip("Minimum level index (0-based) before a shepherd can appear.")]
        [SerializeField] int shepherdMinLevel = 0;

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
        int perfectsThisLevel;
        bool shepherdCreatedThisLevel;
        bool gameOver;
        bool levelComplete;
        bool waitingToStart;
        int explicitQueueSpawnIndex;

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
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
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

            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame && hud != null)
            {
                Vector2 pos = pointer.position.ReadValue();
                if (hud.IsPointerOverSoundButton(pos))
                {
                    hud.ToggleSound();
                    return;
                }

                if (hud.IsPointerOverMenuButton(pos))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayTap();
                    if (hud.IsLevelSelectActive)
                    {
                        hud.HideLevelSelect();
                        // If we were waiting to start, and we close the menu without picking a level, 
                        // maybe go back to the start panel? 
                        // For simplicity, just hide it.
                    }
                    else
                    {
                        hud.ShowLevelSelect(PlayerPrefs.GetInt(BestLevelKey, 0));
                    }
                    return;
                }

                if (hud.IsLevelSelectActive)
                {
                    int clickedLevel = hud.GetClickedLevelIndex(pos);
                    if (clickedLevel >= 0)
                    {
                        int bestLevel = PlayerPrefs.GetInt(BestLevelKey, 0);
                        if (clickedLevel <= bestLevel)
                        {
                            if (AudioManager.Instance != null) AudioManager.Instance.PlayTap();
                            waitingToStart = false;
                            titleShown = true;
                            hud.HideStart();
                            hud.HideLevelSelect();
                            LoadLevel(clickedLevel);
                        }
                    }
                    return; // Eat the tap if level select is open
                }
            }

            // Android Back Button / Escape handling
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame && hud != null)
            {
                if (hud.IsLevelSelectActive)
                {
                    hud.HideLevelSelect();
                    return;
                }
                else if (!waitingToStart && !gameOver && !levelComplete)
                {
                    hud.ShowLevelSelect(PlayerPrefs.GetInt(BestLevelKey, 0));
                    return;
                }
            }

            // Pause game time if level select is active? 
            // Or just let animals tick in the background. The original game has no pause. Let them tick.
            
            if (waitingToStart)
            {
                TickAnimals(dt);

                if (AnyPressed(pointer))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayTap();
                    waitingToStart = false;
                    titleShown = true;
                    hud.HideStart();
                    LoadLevel(PlayerPrefs.GetInt(BestLevelKey, 0)); // Start from best level instead of 0
                }
                return;
            }

            if (levelComplete)
            {
                TickAnimals(dt);
                levelCompleteTimer -= dt;
                if (levelCompleteTimer <= 0f && AnyPressed(pointer))
                    LoadLevel(currentLevel + 1);
                return;
            }

            if (gameOver)
            {
                gameOverTimer -= dt;
                if (gameOverTimer <= 0f && AnyPressed(pointer)) RestartLevel();
                return;
            }

            if (!hud.IsLevelSelectActive)
            {
                HandleInput(pointer);
            }
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
            perfectsThisLevel = 0;
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
            
            explicitQueueSpawnIndex = 0;
            if (data.explicitQueueAnimals != null && data.explicitQueueAnimals.Length > 0)
            {
                int c = 0;
                for (int i = 0; i < data.explicitQueueAnimals.Length; i++)
                {
                    if (data.explicitQueueAnimals[i] != "Coban") c++;
                }
                totalRegularToSend = c;
            }
            else
            {
                totalRegularToSend = data.animalsToSend;
            }

            hud.SetLevel(level + 1);
            hud.SetProgress(0, totalRegularToSend);
            hud.HideLevelComplete();
            hud.HideGameOver();

            SpawnInitialAnimals(data);
            RefillQueue(data);
        }

        void SpawnInitialAnimals(LevelData data)
        {
            if (data.explicitInitialAnimals != null && data.explicitInitialAnimals.Length > 0)
            {
                bool hasAngles = data.explicitInitialAngles != null 
                    && data.explicitInitialAngles.Length == data.explicitInitialAnimals.Length;
                float angleStep = 360f / data.explicitInitialAnimals.Length;

                for (int i = 0; i < data.explicitInitialAnimals.Length; i++)
                {
                    float angle = hasAngles ? data.explicitInitialAngles[i] : i * angleStep;
                    AnimalKind kind = FindKindByName(data.explicitInitialAnimals[i]);
                    Animal animal = Instantiate(animalPrefab, animalParent);
                    animal.SetupAsCircling(kind, angle, geometry);
                    animals.Add(animal);
                }
            }
            else
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
        }

        void RefillQueue(LevelData data)
        {
            while (entryLane != null && entryLane.QueueCount < maxQueuePerLane)
            {
                if (data.explicitQueueAnimals != null && data.explicitQueueAnimals.Length > 0)
                {
                    if (explicitQueueSpawnIndex < data.explicitQueueAnimals.Length)
                    {
                        AnimalKind kind = FindKindByName(data.explicitQueueAnimals[explicitQueueSpawnIndex]);
                        if (kind != null)
                        {
                            SpawnQueueAnimal(kind);
                            if (kind.isShepherd) shepherdsAlive++;
                            else regularAnimalsCreated++;
                        }
                        explicitQueueSpawnIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
                else
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

            int earnedStars = 1;
            if (perfectsThisLevel >= totalRegularToSend && totalRegularToSend > 0)
                earnedStars = 3;
            else if (perfectsThisLevel > 0)
                earnedStars = 2;

            hud.SetBest(best);
            hud.ShowLevelComplete(currentLevel + 1, earnedStars);
        }

        // ----------------------------------------------------------- input

        void HandleInput(Pointer pointer)
        {
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                TryRelease();
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            Keyboard keys = Keyboard.current;
            if (keys == null) return;

            if (keys.spaceKey.wasPressedThisFrame) TryRelease();
#endif
        }

        /// <summary>Dismisses the title card and, later, the game-over card. The
        /// press is swallowed here because Update returns straight after, so the
        /// same tap can never also release an animal.</summary>
        bool AnyPressed(Pointer pointer)
        {
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                return true;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            Keyboard keys = Keyboard.current;
            return keys != null && (keys.rKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame);
#else
            return false;
#endif
        }

        void TryRelease()
        {
            if (cooldownTimer > 0f) return;
            if (entryLane == null || !entryLane.HasWaiting) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayTap();

            Animal animal = entryLane.Dequeue();
            if (animal == null) return;

            // Shepherd exits from the top road, regular animals exit from the bottom.
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

        AnimalKind FindKindByName(string name)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i].displayName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return kinds[i];
            }
            return kinds[0];
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
                    // Check for PERFECT pass
                    float minDistanceSq = float.MaxValue;
                    float requiredReachForCrash = 0f;

                    for (int j = 0; j < animals.Count; j++)
                    {
                        if (i == j) continue;
                        if (animals[j].State == AnimalState.CirclingInside || animals[j].State == AnimalState.OnRing)
                        {
                            float distSq = (animals[i].Position - animals[j].Position).sqrMagnitude;
                            if (distSq < minDistanceSq)
                            {
                                minDistanceSq = distSq;
                                requiredReachForCrash = animals[i].CollisionRadius + animals[j].CollisionRadius;
                            }
                        }
                    }

                    // A perfect pass is when the distance is very close to the crash radius.
                    float perfectMargin = 0.25f; 
                    if (minDistanceSq < float.MaxValue)
                    {
                        float dist = Mathf.Sqrt(minDistanceSq);
                        if (dist > requiredReachForCrash && dist <= requiredReachForCrash + perfectMargin)
                        {
                            perfectsThisLevel++;
                            SpawnPerfectText(animals[i].Position);
                        }
                    }

                    if (AudioManager.Instance != null) AudioManager.Instance.PlayScore();
                    animalsPlaced++;
                    hud.SetProgress(animalsPlaced, totalRegularToSend);
                    hud.SetScore(animalsPlaced);
                }
            }

            for (int i = 0; i < finished.Count; i++) Finish(finished[i]);

            if (!levelComplete && animalsPlaced >= totalRegularToSend)
            {
                if (entryLane == null || entryLane.QueueCount == 0)
                {
                    bool anyEntering = false;
                    for (int j = 0; j < animals.Count; j++)
                    {
                        if (animals[j].State == AnimalState.Entering || animals[j].State == AnimalState.Queued)
                        {
                            anyEntering = true;
                            break;
                        }
                    }
                    if (!anyEntering)
                    {
                        LevelCompleted();
                    }
                }
            }
        }

        void Finish(Animal animal)
        {
            if (animal.IsShepherd)
            {
                IReadOnlyList<Animal> herd = animal.Herd;
                for (int i = 0; i < herd.Count; i++)
                {
                    if (herd[i] != null && animals.Contains(herd[i]))
                    {
                        animals.Remove(herd[i]);
                        if (herd[i].gameObject != null)
                            Destroy(herd[i].gameObject);
                    }
                }
                shepherdsAlive--;
            }

            if (animals.Contains(animal))
            {
                animals.Remove(animal);
                if (animal.gameObject != null)
                    Destroy(animal.gameObject);
            }
        }

        void CheckHerding()
        {
            if (shepherdsAlive == 0) return;

            for (int i = 0; i < animals.Count; i++)
            {
                Animal shepherd = animals[i];
                if (!shepherd.IsShepherd) continue;
                if (shepherd.State != AnimalState.OnRing && shepherd.State != AnimalState.Exiting) continue;

                if (shepherd.Herd.Count >= 1) continue; // Already collected one

                Animal closestAhead = null;
                float minDelta = 180f; // Only look at the 180 degrees in front

                for (int j = 0; j < animals.Count; j++)
                {
                    Animal other = animals[j];
                    if (!other.CanBeHerded) continue;
                    if (other.State == AnimalState.Entering) continue;

                    float delta = Mathf.DeltaAngle(shepherd.RingAngle, other.RingAngle);
                    if (delta < 0f) delta += 360f;

                    // Ensure it's actually ahead and reasonably close (e.g., within 180 degrees)
                    if (delta > 0f && delta < minDelta)
                    {
                        minDelta = delta;
                        closestAhead = other;
                    }
                }

                if (closestAhead != null)
                {
                    shepherd.Collect(closestAhead, geometry);
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

                    // A shepherd and the animal it is herding are intentionally close.
                    if (animals[i].IsShepherd && animals[i].Herd.Any(a => a == animals[j])) continue;
                    if (animals[j].IsShepherd && animals[j].Herd.Any(a => a == animals[i])) continue;

                    // At least one of them must be merging - two animals that are
                    // already circling at the same speed can never catch each other.
                    if (!animals[i].IsMerging && !animals[j].IsMerging) continue;

                    float reach = animals[i].CollisionRadius + animals[j].CollisionRadius;
                    if ((animals[i].Position - animals[j].Position).sqrMagnitude > reach * reach) continue;

                    Vector2 impact = (animals[i].Position + animals[j].Position) * 0.5f;

                    SpawnEffect(burstPrefab, impact, reach * 1.9f);
                    SpawnEffect(debrisPrefab, impact, reach * 2.6f);

                    // Both go down, each shoved away from the other.
                    animals[i].KnockOut(animals[j].Position);
                    animals[j].KnockOut(animals[i].Position);

                    if (AudioManager.Instance != null) AudioManager.Instance.PlayCrash();

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

        void SpawnPerfectText(Vector2 at)
        {
            GameObject go = new GameObject("PerfectText");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = "PERFECT!";
            tmp.fontSize = 4f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.84f, 0f, 1f); // Gold
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.sortingOrder = 100;
            
            StartCoroutine(AnimatePerfectText(tmp));
        }

        System.Collections.IEnumerator AnimatePerfectText(TMPro.TextMeshPro tmp)
        {
            float elapsed = 0f;
            float duration = 0.9f;
            Vector3 startPos = tmp.transform.position;
            Vector3 endPos = startPos + Vector3.up * 1.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                tmp.transform.position = Vector3.Lerp(startPos, endPos, t * t * (3f - 2f * t)); // Smoothstep
                
                Color c = tmp.color;
                c.a = 1f - (t * t);
                tmp.color = c;
                
                yield return null;
            }
            if (tmp != null) Destroy(tmp.gameObject);
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
