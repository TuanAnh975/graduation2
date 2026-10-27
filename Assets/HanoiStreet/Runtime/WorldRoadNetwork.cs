using UnityEngine;

namespace HanoiStreet
{
    // Stateless graph: every node is shared by four edges, regardless of load order.
    public static class WorldRoadNetwork
    {
        public static uint Hash(int x, int z, int seed, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu ^ (uint)salt * 0xc2b2ae35u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return h;
            }
        }

        static float Signed(int x, int z, int seed, int salt) => (Hash(x, z, seed, salt) & 0xffff) / 32767.5f - 1;

        public static Vector3 Node(Vector2Int node, Vector2Int origin, WorldProfile p)
        {
            Vector3 jitter = node == Vector2Int.zero ? Vector3.zero :
                new Vector3(Signed(node.x, node.y, p.seed, 1), 0, Signed(node.x, node.y, p.seed, 2)) * p.cellSize * p.junctionJitter;
            return new Vector3((node.x - origin.x) * p.cellSize, 0, (node.y - origin.y) * p.cellSize) + jitter;
        }

        public static bool IsFlyover(Vector2Int node, WorldProfile p) => node != Vector2Int.zero &&
            (node.x - 1) % 3 == 0 && node.y % 3 == 0 &&
            (Hash(node.x, node.y, p.seed, 3) & 0xffff) / 65535f < p.flyoverChance;

        public static Vector2Int End(Vector2Int owner, bool horizontal) => owner + (horizontal ? Vector2Int.right : Vector2Int.up);

        public static Vector3 Point(Vector2Int owner, bool horizontal, float t, Vector2Int origin, WorldProfile p)
        {
            t = Mathf.Clamp01(t);
            var end = End(owner, horizontal);
            Vector3 a = Node(owner, origin, p), b = Node(end, origin, p);
            Vector3 axis = horizontal ? Vector3.right : Vector3.forward;
            Vector3 c = a + axis * p.cellSize * .38f, d = b - axis * p.cellSize * .38f;
            float s = 1 - t;
            Vector3 point = s * s * s * a + 3 * s * s * t * c + 3 * s * t * t * d + t * t * t * b;
            if (horizontal)
            {
                float plateau = p.JunctionRadius + 4;
                float ramp = p.cellSize * .48f;
                float da = t * p.cellSize, db = (1 - t) * p.cellSize;
                if (IsFlyover(owner, p)) point.y += p.flyoverHeight * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(plateau, ramp, da)));
                if (IsFlyover(end, p)) point.y += p.flyoverHeight * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(plateau, ramp, db)));
            }
            return point;
        }

        public static Vector3 Tangent(Vector2Int owner, bool horizontal, float t, Vector2Int origin, WorldProfile p)
        {
            return (Point(owner, horizontal, t + .001f, origin, p) - Point(owner, horizontal, t - .001f, origin, p)).normalized;
        }

        // 0 red, 1 amber, 2 green. Bridge roads are grade-separated and never stopped here.
        public static int Signal(Vector2Int node, bool horizontal, float time, WorldProfile p)
        {
            if (IsFlyover(node, p)) return 2;
            float offset = Hash(node.x, node.y, p.seed, 4) % 32;
            float cycle = Mathf.Repeat(time + offset + (horizontal ? 16 : 0), 32);
            return cycle < 10 ? 2 : cycle < 13 ? 1 : 0;
        }
    }
}
