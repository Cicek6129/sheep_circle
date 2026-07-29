using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// All the maths for the roundabout: where the ring is, where each lane's
    /// queue sits, and where sheep merge in and peel off.
    /// Angles are in degrees, counter-clockwise, 0 = +X.
    /// </summary>
    [System.Serializable]
    public class RingGeometry
    {
        public float radius = 2.8f;
        public int laneCount = 2;

        /// <summary>Bottom lane (270°). The player sends animals in from here.</summary>
        public const int ENTRY_LANE = 1;
        /// <summary>Top lane (90°). The shepherd exits with herded animals here.</summary>
        public const int EXIT_LANE = 0;

        [Tooltip("Angular gap between a lane's merge point and its exit point.")]
        public float laneSplitDeg = 10f;

        [Tooltip("Distance beyond the ring where the first queued sheep waits.")]
        public float roadStart = 0.82f;

        public float queueSpacing = 0.66f;

        [Tooltip("How far past the ring a sheep travels before it despawns.")]
        public float exitDistance = 3.4f;

        public float LaneAngle(int lane) => 90f - lane * (360f / laneCount);
        public float MergeAngle(int lane) => LaneAngle(lane) - laneSplitDeg;
        public float ExitAngle(int lane) => LaneAngle(lane) + laneSplitDeg;

        public static Vector2 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public Vector2 PointOnRing(float deg) => Dir(deg) * radius;

        /// <summary>Outward direction of a lane's road, i.e. away from the ring.</summary>
        public Vector2 LaneDir(int lane) => Dir(LaneAngle(lane));

        public Vector2 MergePoint(int lane) => PointOnRing(MergeAngle(lane));

        /// <summary>Where a queued animal waits. The queue runs parallel to the road
        /// axis rather than straight out from the centre, so the incoming and
        /// outgoing lanes stay a fixed distance apart instead of fanning out.</summary>
        public Vector2 QueuePos(int lane, int index) =>
            MergePoint(lane) + LaneDir(lane) * (roadStart + index * queueSpacing);

        public Vector2 ExitPos(int lane, float distanceOutside) =>
            PointOnRing(ExitAngle(lane)) + LaneDir(lane) * distanceOutside;

        /// <summary>Degrees a sheep travels around the ring going from one lane's
        /// merge point to another lane's exit point.</summary>
        public float ArcBetween(int fromLane, int toLane) =>
            Mathf.Repeat(ExitAngle(toLane) - MergeAngle(fromLane), 360f);

        /// <summary>Lane whose road points closest to <paramref name="worldPos"/>.</summary>
        public int NearestLane(Vector2 worldPos)
        {
            float angle = Mathf.Atan2(worldPos.y, worldPos.x) * Mathf.Rad2Deg;
            int best = 0;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < laneCount; i++)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(angle, LaneAngle(i)));
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }
            return best;
        }
    }
}
