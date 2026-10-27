using UnityEngine;

namespace HanoiStreet
{
    public sealed class StreamedTraffic : MonoBehaviour
    {
        HanoiWorldStreamer world;
        Vector2Int owner;
        bool horizontal, positive, committed, turning, nextHorizontal, nextPositive;
        Vector2Int nextOwner;
        float distance, length, nextLength, turnProgress;
        uint routeSeed;
        int junctions;

        public void Initialize(HanoiWorldStreamer streamer, Vector2Int edge, bool eastWest, bool forward, float fraction, uint seed)
        {
            world = streamer; owner = edge; horizontal = eastWest; positive = forward; routeSeed = seed;
            length = world.EdgeLength(owner, horizontal);
            distance = length * fraction;
            committed = distance > length - world.Profile.JunctionRadius - 3;
            world.RegisterTraffic(this);
            Place();
        }

        void Update()
        {
            if (!world) return;
            if (world.HasVehicleAhead(this)) return;
            if (turning) { AdvanceJunction(); return; }
            float next = distance + world.Profile.trafficSpeed * Time.deltaTime;
            var destination = positive ? WorldRoadNetwork.End(owner, horizontal) : owner;
            float stop = length - world.Profile.JunctionRadius - 3;
            if (next >= stop && !committed)
            {
                if (WorldRoadNetwork.Signal(destination, horizontal, Time.time, world.Profile) != 2 || !NextEdgeAvailable(destination))
                    next = Mathf.Min(next, stop);
                else committed = true;
            }
            distance = next;
            if (distance >= length - world.Profile.JunctionRadius)
            {
                if (!NextEdgeAvailable(destination)) { distance = length - world.Profile.JunctionRadius; Place(); return; }
                Vector2Int delta = NextDirection();
                var nextNode = destination + delta;
                nextPositive = delta.x > 0 || delta.y > 0;
                nextOwner = nextPositive ? destination : nextNode;
                nextHorizontal = delta.x != 0;
                nextLength = world.EdgeLength(nextOwner, nextHorizontal);
                distance = length - world.Profile.JunctionRadius;
                turning = true; turnProgress = 0;
            }
            Place();
        }

        void OnDestroy() { if (world) world.UnregisterTraffic(this); }

        void Pose(Vector2Int edge, bool eastWest, bool forward, float fraction, out Vector3 point, out Vector3 tangent)
        {
            float t = forward ? fraction : 1 - fraction;
            tangent = world.EdgeTangent(edge, eastWest, t) * (forward ? 1 : -1);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            point = world.EdgePoint(edge, eastWest, t) + right * world.Profile.roadWidth * .25f + Vector3.up * .06f;
        }

        void AdvanceJunction()
        {
            float radius = world.Profile.JunctionRadius;
            // Recompute world-space controls each frame so floating-origin shifts remain seamless.
            Pose(owner, horizontal, positive, (length - radius) / length, out var a, out var incoming);
            Pose(nextOwner, nextHorizontal, nextPositive, radius / nextLength, out var d, out var outgoing);
            Vector3 b = a + incoming * radius * .75f, c = d - outgoing * radius * .75f;
            float approximateLength = Mathf.Max(4, (Vector3.Distance(a, b) + Vector3.Distance(b, c) + Vector3.Distance(c, d)) * .85f);
            turnProgress = Mathf.Min(1, turnProgress + world.Profile.trafficSpeed * Time.deltaTime / approximateLength);
            float t = turnProgress, s = 1 - t;
            transform.position = s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d;
            Vector3 direction = 3 * s * s * (b - a) + 6 * s * t * (c - b) + 3 * t * t * (d - c);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (t < 1) return;
            owner = nextOwner; horizontal = nextHorizontal; positive = nextPositive;
            distance = radius; length = nextLength; turning = false; committed = false; junctions++;
        }

        Vector2Int NextDirection()
        {
            var delta = (horizontal ? Vector2Int.right : Vector2Int.up) * (positive ? 1 : -1);
            // Do not turn from the flyover into the lower crossing road.
            var destination = positive ? WorldRoadNetwork.End(owner, horizontal) : owner;
            if (WorldRoadNetwork.IsFlyover(destination, world.Profile)) return delta;
            uint choice = WorldRoadNetwork.Hash(junctions, (int)routeSeed, world.Profile.seed, 20) % 10;
            return choice < 7 ? delta : choice < 9 ? new Vector2Int(-delta.y, delta.x) : new Vector2Int(delta.y, -delta.x);
        }

        bool NextEdgeAvailable(Vector2Int destination)
        {
            var delta = NextDirection();
            var next = destination + delta;
            return world.IsRoadLoaded(delta.x > 0 || delta.y > 0 ? destination : next);
        }

        void Place()
        {
            float t = Mathf.Clamp01(distance / Mathf.Max(1, length));
            Pose(owner, horizontal, positive, t, out var p, out var tangent);
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }
}
