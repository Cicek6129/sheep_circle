using System.Collections.Generic;
using UnityEngine;

namespace SheepCircle
{
    public enum AnimalState
    {
        Queued,     // waiting on the approach road
        Entering,   // trotting from the queue toward the ring
        OnRing,     // circling
        Exiting,    // heading back out along an exit road
        Herded,     // swept up by the shepherd; he drives the movement now
        Done        // made it home, ready to be despawned
    }

    /// <summary>
    /// A single animal moving through the roundabout. GameManager drives Tick so
    /// that movement and crash checks happen in a predictable order.
    /// </summary>
    public class Animal : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer patch;
        [SerializeField] SpriteRenderer head;

        [Header("Base speeds")]
        [SerializeField] float baseEnterSpeed = 3.4f;
        [SerializeField] float baseExitSpeed = 3.4f;
        [SerializeField] float queueSlideSpeed = 3.2f;

        [Header("Shepherd")]
        [Tooltip("Gap between animals trailing behind the shepherd, in world units.")]
        [SerializeField] float herdSpacing = 0.6f;

        public AnimalKind Kind { get; private set; }
        public AnimalState State { get; private set; } = AnimalState.Queued;
        public int LaneIndex { get; private set; }
        public int QueueIndex { get; private set; }

        public bool IsShepherd => Kind != null && Kind.isShepherd;

        /// <summary>Animals on their way out are on the far side of the road and are
        /// past caring. The shepherd never crashes, and neither does his flock.</summary>
        public bool CanCrash => !IsShepherd
                             && (State == AnimalState.Entering || State == AnimalState.OnRing);

        public bool IsMerging => State == AnimalState.Entering;

        /// <summary>Out on the road and not already following the shepherd.</summary>
        public bool CanBeHerded => !IsShepherd
                                && (State == AnimalState.Entering || State == AnimalState.OnRing);

        public float CollisionRadius => Kind != null ? Kind.collisionRadius : 0.28f;
        public Vector2 Position => pos;
        public IReadOnlyList<Animal> Herd => herd;

        readonly List<Animal> herd = new List<Animal>();

        int exitLane;
        float ringAngle;
        float arcRemaining;
        float exitTravel;
        Vector2 pos;

        public void Setup(AnimalKind kind, int lane, int queueIndex, RingGeometry geo)
        {
            Kind = kind;
            LaneIndex = lane;
            QueueIndex = queueIndex;
            State = AnimalState.Queued;

            name = $"{kind.displayName} (lane {lane})";
            transform.localScale = Vector3.one * kind.size;

            body.sprite = kind.bodySprite;
            body.color = kind.bodyColor;

            head.gameObject.SetActive(kind.showHead);
            head.color = kind.headColor;

            if (kind.patchSprite != null)
            {
                patch.gameObject.SetActive(true);
                patch.sprite = kind.patchSprite;
                patch.color = kind.patchColor;
            }
            else
            {
                patch.gameObject.SetActive(false);
            }

            // Spawn one slot further out so it visibly walks up to its place.
            pos = geo.QueuePos(lane, queueIndex + 1);
            Apply(-geo.LaneDir(lane));
        }

        public void SetQueueIndex(int index) => QueueIndex = index;

        /// <summary>Roughly how long this animal needs to get from the head of the
        /// queue onto the ring. Used to stop a lane being released into itself.</summary>
        public float MergeSeconds(RingGeometry geo) =>
            geo.roadStart / Mathf.Max(0.01f, baseEnterSpeed * Kind.enterSpeedMul);

        /// <summary>Send this animal onto the ring, aiming for <paramref name="targetExitLane"/>.</summary>
        public void Release(int targetExitLane)
        {
            if (State != AnimalState.Queued) return;
            exitLane = targetExitLane;
            State = AnimalState.Entering;
        }

        /// <summary>Shepherd sweeps someone up.</summary>
        public void Collect(Animal other)
        {
            if (!IsShepherd || !other.CanBeHerded) return;
            other.State = AnimalState.Herded;
            herd.Add(other);
        }

        /// <summary>Called by the shepherd for each animal trailing behind him.</summary>
        public void SetHerdPose(Vector2 worldPos, Vector2 facing)
        {
            pos = worldPos;
            Apply(facing);
        }

        /// <summary>Advance one frame. Returns true once the animal has made it home.</summary>
        public bool Tick(float dt, RingGeometry geo, float ringSpeedDeg)
        {
            switch (State)
            {
                case AnimalState.Queued:
                {
                    Vector2 target = geo.QueuePos(LaneIndex, QueueIndex);
                    pos = Vector2.MoveTowards(pos, target, queueSlideSpeed * dt);
                    Apply(-geo.LaneDir(LaneIndex));
                    break;
                }

                case AnimalState.Entering:
                {
                    Vector2 target = geo.MergePoint(LaneIndex);
                    float speed = baseEnterSpeed * Kind.enterSpeedMul;
                    pos = Vector2.MoveTowards(pos, target, speed * dt);
                    Apply(-geo.LaneDir(LaneIndex));

                    if ((pos - target).sqrMagnitude <= 0.0004f)
                    {
                        pos = target;
                        ringAngle = geo.MergeAngle(LaneIndex);
                        arcRemaining = geo.ArcBetween(LaneIndex, exitLane) + Kind.extraLaps * 360f;
                        State = AnimalState.OnRing;
                    }
                    break;
                }

                case AnimalState.OnRing:
                {
                    float step = ringSpeedDeg * Kind.ringSpeedMul * dt;
                    ringAngle += step;
                    arcRemaining -= step;
                    pos = geo.PointOnRing(ringAngle);
                    Apply(RingGeometry.Dir(ringAngle + 90f));

                    if (arcRemaining <= 0f)
                    {
                        exitTravel = 0f;
                        State = AnimalState.Exiting;
                    }
                    PositionHerd(geo);
                    break;
                }

                case AnimalState.Exiting:
                {
                    exitTravel += baseExitSpeed * dt;
                    pos = geo.ExitPos(exitLane, exitTravel);
                    Apply(geo.LaneDir(exitLane));
                    PositionHerd(geo);

                    if (exitTravel >= geo.exitDistance)
                    {
                        State = AnimalState.Done;
                        return true;
                    }
                    break;
                }
            }

            return false;
        }

        void PositionHerd(RingGeometry geo)
        {
            if (herd.Count == 0) return;

            float spacingDeg = herdSpacing / geo.radius * Mathf.Rad2Deg;

            for (int i = 0; i < herd.Count; i++)
            {
                float back = herdSpacing * (i + 1);

                if (State == AnimalState.OnRing)
                {
                    float angle = ringAngle - spacingDeg * (i + 1);
                    herd[i].SetHerdPose(geo.PointOnRing(angle), RingGeometry.Dir(angle + 90f));
                    continue;
                }

                // Exiting: the tail of the flock is still coming round the ring.
                float along = exitTravel - back;
                if (along >= 0f)
                {
                    herd[i].SetHerdPose(geo.ExitPos(exitLane, along), geo.LaneDir(exitLane));
                }
                else
                {
                    float angle = geo.ExitAngle(exitLane) + along / geo.radius * Mathf.Rad2Deg;
                    herd[i].SetHerdPose(geo.PointOnRing(angle), RingGeometry.Dir(angle + 90f));
                }
            }
        }

        void Apply(Vector2 facing)
        {
            transform.position = new Vector3(pos.x, pos.y, 0f);
            if (facing.sqrMagnitude > 0.0001f)
            {
                float deg = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, deg);
            }
        }
    }
}
