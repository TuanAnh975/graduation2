using System;
using UnityEditor;
using UnityEngine;

namespace HanoiStreet.Editor
{
    public static class WorldGraphValidation
    {
        [MenuItem("Hanoi Street/Validate world graph")]
        public static void Validate()
        {
            var saved = AssetDatabase.LoadAssetAtPath<WorldProfile>("Assets/HanoiStreet/Generated/OpenWorld.asset");
            var p = saved ? saved : ScriptableObject.CreateInstance<WorldProfile>();
            int edges = 0, flyovers = 0;
            try
            {
                for (int x = -6; x <= 6; x++)
                    for (int z = -6; z <= 6; z++)
                    {
                        var node = new Vector2Int(x, z);
                        if (WorldRoadNetwork.IsFlyover(node, p)) flyovers++;
                        for (int axis = 0; axis < 2; axis++)
                        {
                            bool horizontal = axis == 0;
                            var end = WorldRoadNetwork.End(node, horizontal);
                            Vector3 a = WorldRoadNetwork.Point(node, horizontal, 0, Vector2Int.zero, p);
                            Vector3 b = WorldRoadNetwork.Point(node, horizontal, 1, Vector2Int.zero, p);
                            Vector3 expectedA = WorldRoadNetwork.Node(node, Vector2Int.zero, p);
                            Vector3 expectedB = WorldRoadNetwork.Node(end, Vector2Int.zero, p);
                            if (horizontal && WorldRoadNetwork.IsFlyover(node, p)) expectedA.y = p.flyoverHeight;
                            if (horizontal && WorldRoadNetwork.IsFlyover(end, p)) expectedB.y = p.flyoverHeight;
                            Require(Vector3.Distance(a, expectedA) < .001f && Vector3.Distance(b, expectedB) < .001f, "Road endpoints do not meet the shared junction.");
                            var axisVector = horizontal ? Vector3.right : Vector3.forward;
                            Require(Vector3.Dot(WorldRoadNetwork.Tangent(node, horizontal, 0, Vector2Int.zero, p), axisVector) > .999f, "Junction entry is not aligned.");
                            Require(Vector3.Dot(WorldRoadNetwork.Tangent(node, horizontal, 1, Vector2Int.zero, p), axisVector) > .999f, "Junction exit is not aligned.");
                            for (int sample = 0; sample <= 32; sample++)
                            {
                                float t = sample / 32f;
                                Vector3 point = WorldRoadNetwork.Point(node, horizontal, t, Vector2Int.zero, p);
                                var shiftedOrigin = new Vector2Int(12, -9);
                                Vector3 shifted = WorldRoadNetwork.Point(node, horizontal, t, shiftedOrigin, p);
                                Vector3 expectedShift = new Vector3(shiftedOrigin.x * p.cellSize, 0, shiftedOrigin.y * p.cellSize);
                                Require(Vector3.Distance(point, shifted + expectedShift) < .02f, "Floating-origin shift changes road geometry.");
                                Require(!float.IsNaN(point.x) && !float.IsInfinity(point.z) && point.y >= -.001f, "Invalid road sample.");
                            }
                            edges++;
                        }
                        for (int second = 0; second < 64; second++)
                        {
                            bool ewGreen = WorldRoadNetwork.Signal(node, true, second, p) == 2;
                            bool nsGreen = WorldRoadNetwork.Signal(node, false, second, p) == 2;
                            Require(WorldRoadNetwork.IsFlyover(node, p) || !(ewGreen && nsGreen), "Conflicting crossing greens.");
                        }
                    }
                Debug.Log($"PASS world graph: {edges} edges, {flyovers} flyovers; endpoints, tangents, origin invariance and signal separation. This does not validate rendering, traffic collisions or frame rate.");
            }
            finally { if (!saved) UnityEngine.Object.DestroyImmediate(p); }
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
