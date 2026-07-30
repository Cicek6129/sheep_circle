using System.Collections.Generic;
using UnityEngine;

namespace SheepCircle
{
    public enum AnimalState
    {
        Queued,          // waiting on the approach road
        Entering,        // trotting from the queue toward the ring
        CirclingInside,  // circling permanently (initial + successfully placed animals)
        OnRing,          // circling with an exit target (shepherd only)
        Exiting,         // heading back out along an exit road
        Herded,          // swept up by the shepherd; he drives the movement now
        Done             // made it home, ready to be despawned
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
        [Tooltip("Blob under the animal. Held at a fixed world rotation so the " +
                 "light never appears to swing around as the animal turns.")]
        [SerializeField] SpriteRenderer shadow;

        [Header("Base speeds")]
        [SerializeField] float baseEnterSpeed = 3.4f;
        [SerializeField] float baseExitSpeed = 3.4f;
        [SerializeField] float queueSlideSpeed = 3.2f;

        [Header("Shepherd")]
        [Tooltip("Gap between animals trailing behind the shepherd, in world units.")]
        [SerializeField] float herdSpacing = 0.6f;

        [Header("Knock-out")]
        [Tooltip("Ring of stars over the head. Switched on only once knocked out.")]
        [SerializeField] SpriteRenderer dizzy;
        [Tooltip("Random tilt on collapsing, in degrees, so a crashed pair does not " +
                 "end up lying perfectly parallel.")]
        [SerializeField] float koTilt = 20f;
        [Tooltip("Grow-up applied to the body when it collapses. A fallen animal " +
                 "is drawn from the side, so it covers about half the pixels its " +
                 "top-down pose did at the same length, and reads as having shrunk. " +
                 "Matching the area exactly would want 1.18 for the sheep, 1.40 for " +
                 "the cow and 1.44 for the goat; one value in between keeps them " +
                 "consistent with each other.")]
        [SerializeField] float koScale = 1.25f;
        [Tooltip("How far the impact shoves each animal apart, in world units.")]
        [SerializeField] float koRecoil = 0.14f;

        public AnimalKind Kind { get; private set; }
        public AnimalState State { get; private set; } = AnimalState.Queued;
        public int LaneIndex { get; private set; }
        public int QueueIndex { get; private set; }

        public bool IsShepherd => Kind != null && Kind.isShepherd;

        /// <summary>Animals on their way out are on the far side of the road and are
        /// past caring. The shepherd never crashes, and neither does his flock.</summary>
        public bool CanCrash => !IsShepherd && !knockedOut
                             && (State == AnimalState.Entering
                              || State == AnimalState.CirclingInside
                              || State == AnimalState.OnRing);

        /// <summary>True while the animal is actively merging into the ring, or has
        /// just landed on it this frame. Two animals already circling at the same
        /// speed can never catch each other, so they are excluded.</summary>
        public bool IsMerging => State == AnimalState.Entering || justEnteredRing;

        /// <summary>Out on the road and not already following the shepherd.</summary>
        public bool CanBeHerded => !IsShepherd
                                && (State == AnimalState.Entering
                                 || State == AnimalState.CirclingInside);

        public float CollisionRadius => Kind != null ? Kind.collisionRadius : 0.28f;
        public Vector2 Position => pos;
        public IReadOnlyList<Animal> Herd => herd;

        readonly List<Animal> herd = new List<Animal>();

        int exitLane;
        float ringAngle;
        public float RingAngle => ringAngle;
        float arcRemaining;
        float exitTravel;
        Vector2 pos;

        /// <summary>True for exactly one frame after transitioning from Entering to
        /// CirclingInside, so the crash check still treats the animal as merging.</summary>
        bool justEnteredRing;

        /// <summary>Once down, the animal stops being moved or turned by anything.</summary>
        bool knockedOut;

        // ----------------------------------------------------------- setup

        /// <summary>Set up the animal visuals from its kind definition.</summary>
        void ApplyVisuals(AnimalKind kind)
        {
            Kind = kind;
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
        }

        /// <summary>Place the animal in a lane queue (the standard setup).</summary>
        public void Setup(AnimalKind kind, int lane, int queueIndex, RingGeometry geo)
        {
            ApplyVisuals(kind);
            LaneIndex = lane;
            QueueIndex = queueIndex;
            State = AnimalState.Queued;

            name = $"{kind.displayName} (lane {lane})";

            // Spawn one slot further out so it visibly walks up to its place.
            pos = geo.QueuePos(lane, queueIndex + 1);
            Apply(-geo.LaneDir(lane));
        }

        /// <summary>Place the animal directly on the ring, already circling.
        /// Used for the animals that are on the ring when a level starts.</summary>
        public void SetupAsCircling(AnimalKind kind, float startAngle, RingGeometry geo)
        {
            ApplyVisuals(kind);
            LaneIndex = -1;
            QueueIndex = -1;
            State = AnimalState.CirclingInside;
            ringAngle = startAngle;

            name = $"{kind.displayName} (circling)";

            pos = geo.PointOnRing(startAngle);
            Apply(RingGeometry.Dir(startAngle + 90f));
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
        public void Collect(Animal other, RingGeometry geo)
        {
            if (!IsShepherd || !other.CanBeHerded) return;
            if (herd.Count >= 1) return;
            herd.Add(other);
            other.CommandToExit(exitLane, geo);
        }

        public void CommandToExit(int targetExitLane, RingGeometry geo)
        {
            if (State != AnimalState.CirclingInside && State != AnimalState.Entering) return;
            exitLane = targetExitLane;
            float exitAng = geo.ExitAngle(exitLane);
            arcRemaining = exitAng - ringAngle;
            while (arcRemaining < 0f) arcRemaining += 360f;
            while (arcRemaining >= 360f) arcRemaining -= 360f;
            State = AnimalState.OnRing;
        }

        /// <summary>Called by the shepherd for each animal trailing behind him.</summary>
        public void SetHerdPose(Vector2 worldPos, Vector2 facing)
        {
            pos = worldPos;
            Apply(facing);
        }

        /// <summary>Clear per-frame flags after crash checks have run.</summary>
        public void ClearFrameFlags() => justEnteredRing = false;

        /// <summary>Collapse onto the ground after a crash: swap in the fallen
        /// sprite, tip over, and start the stars circling.
        ///
        /// The animal is left frozen rather than given a new state, because a
        /// crash ends the round and GameManager stops ticking anyone. Apply is
        /// gated on the same flag so nothing can straighten it up afterwards.
        /// </summary>
        /// <param name="impactFrom">Where the blow came from; the animal is shoved
        /// directly away from it.</param>
        public void KnockOut(Vector2 impactFrom)
        {
            if (knockedOut) return;
            knockedOut = true;

            if (Kind != null && Kind.koSprite != null) body.sprite = Kind.koSprite;

            // Every kind currently ships with showHead off and no patch, so these
            // are already inactive. They are switched off anyway: both are drawn
            // for an animal standing upright, and a kind that turned either back
            // on would leave them floating over the fallen body.
            if (head != null) head.gameObject.SetActive(false);
            if (patch != null) patch.gameObject.SetActive(false);

            Vector2 away = pos - impactFrom;
            if (away.sqrMagnitude > 0.0001f) pos += away.normalized * koRecoil;
            transform.position = new Vector3(pos.x, pos.y, 0f);

            // Keep the direction it was travelling - it fell where it was going -
            // then tip it a little off that line.
            transform.rotation *= Quaternion.Euler(0f, 0f, Random.Range(-koTilt, koTilt));

            // Grown, not squashed. koSprite already draws the animal spread flat,
            // so squashing it again only made it look run over; the real problem
            // is that the side-on pose covers far fewer pixels than the top-down
            // one it replaces.
            body.transform.localScale = Vector3.one * koScale;

            if (shadow != null) shadow.transform.rotation = Quaternion.identity;

            if (dizzy != null)
            {
                dizzy.gameObject.SetActive(true);
                dizzy.transform.rotation = Quaternion.identity;
            }
        }

        // ----------------------------------------------------------- tick

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

                        if (IsShepherd)
                        {
                            // Shepherd circles with an exit target.
                            arcRemaining = geo.ArcBetween(LaneIndex, exitLane) + Kind.extraLaps * 360f;
                            State = AnimalState.OnRing;
                        }
                        else
                        {
                            // Regular animal joins the ring permanently.
                            State = AnimalState.CirclingInside;
                            justEnteredRing = true;
                        }
                    }
                    break;
                }

                case AnimalState.CirclingInside:
                {
                    float step = ringSpeedDeg * Kind.ringSpeedMul * dt;
                    ringAngle += step;
                    pos = geo.PointOnRing(ringAngle);
                    Apply(RingGeometry.Dir(ringAngle + 90f));
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
            // Do nothing. The herded animals now exit independently using OnRing state.
        }

        void Apply(Vector2 facing)
        {
            // A collapsed animal stays exactly where and how it fell.
            if (knockedOut) return;

            transform.position = new Vector3(pos.x, pos.y, 0f);
            if (facing.sqrMagnitude > 0.0001f)
            {
                float deg = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, deg);
            }

            // The shadow is a child, so it would otherwise inherit the spin.
            if (shadow != null) shadow.transform.rotation = Quaternion.identity;
        }
    }
}
