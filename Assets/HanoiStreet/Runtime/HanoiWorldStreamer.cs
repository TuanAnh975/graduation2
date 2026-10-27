using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HanoiStreet
{
    public sealed class HanoiWorldStreamer : MonoBehaviour
    {
        public WorldProfile profile;
        public Transform viewer;
        public WorldProfile Profile => profile;
        public int LoadedChunks => chunks.Count;
        public Vector2Int OriginCell => origin;

        sealed class Chunk
        {
            public GameObject root;
            public bool ready;
            public readonly List<Mesh> meshes = new List<Mesh>();
        }
        readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        readonly HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<StreamedTraffic> vehicles = new List<StreamedTraffic>();
        Vector2Int origin, centre = new Vector2Int(int.MinValue, int.MinValue);
        Coroutine worker;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!profile || !viewer || profile.cellSize < 160 || profile.curveSegments < 12 ||
                profile.loadRadius < 1 || profile.loadRadius > 3 || profile.housesPerFrame < 1)
            { Debug.LogError("World requires profile, viewer, cellSize >= 160, radius 1–3 and a positive generation budget.", this); return; }
            if (transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
            { Debug.LogError("World root must have identity rotation and unit scale.", this); return; }
            RefreshWanted();
            worker = StartCoroutine(Stream());
        }

        void Update()
        {
            if (worker == null || !viewer || !profile) return;
            Rebase();
            RefreshWanted();
        }

        Vector2Int ViewerCell()
        {
            var p = viewer.position - transform.position;
            return origin + new Vector2Int(Mathf.FloorToInt(p.x / profile.cellSize), Mathf.FloorToInt(p.z / profile.cellSize));
        }

        void RefreshWanted()
        {
            var next = ViewerCell();
            if (next == centre) return;
            centre = next;
            wanted.Clear();
            for (int z = -profile.loadRadius; z <= profile.loadRadius; z++)
                for (int x = -profile.loadRadius; x <= profile.loadRadius; x++)
                    wanted.Add(centre + new Vector2Int(x, z));
        }

        void Rebase()
        {
            if (!profile.floatingOrigin) return;
            var p = viewer.position - transform.position;
            if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) < profile.cellSize * 8) return;
            var shiftCells = new Vector2Int(Mathf.RoundToInt(p.x / profile.cellSize), Mathf.RoundToInt(p.z / profile.cellSize));
            Vector3 shift = new Vector3(shiftCells.x * profile.cellSize, 0, shiftCells.y * profile.cellSize);
            origin += shiftCells;
            foreach (var chunk in chunks.Values) chunk.root.transform.position -= shift;
            viewer.position -= shift;
        }

        IEnumerator Stream()
        {
            while (true)
            {
                var removed = new List<Vector2Int>();
                foreach (var pair in chunks) if (!wanted.Contains(pair.Key)) removed.Add(pair.Key);
                foreach (var key in removed) Remove(key);
                bool found = false;
                Vector2Int nearest = default;
                int best = int.MaxValue;
                // Explicit loops make the tie-break stable, independent of dictionary enumeration.
                for (int z = -profile.loadRadius; z <= profile.loadRadius; z++)
                    for (int x = -profile.loadRadius; x <= profile.loadRadius; x++)
                    {
                        var key = centre + new Vector2Int(x, z);
                        int distance = x * x + z * z;
                        if (distance < best && !chunks.ContainsKey(key)) { best = distance; nearest = key; found = true; }
                    }
                if (!found) { yield return null; continue; }
                var chunk = new Chunk { root = new GameObject($"District {nearest.x}, {nearest.y}") };
                chunk.root.transform.SetParent(transform, false);
                chunk.root.transform.position = Node(nearest);
                chunks.Add(nearest, chunk);
                yield return BuildChunk(nearest, chunk);
                if (!wanted.Contains(nearest)) Remove(nearest);
                else chunk.ready = true;
                yield return null;
            }
        }

        public Vector3 Node(Vector2Int coord) => transform.position + WorldRoadNetwork.Node(coord, origin, profile);
        public Vector3 EdgePoint(Vector2Int coord, bool horizontal, float t) => transform.position + WorldRoadNetwork.Point(coord, horizontal, t, origin, profile);
        public Vector3 EdgeTangent(Vector2Int coord, bool horizontal, float t) => WorldRoadNetwork.Tangent(coord, horizontal, t, origin, profile);
        public bool IsRoadLoaded(Vector2Int owner) => chunks.TryGetValue(owner, out var chunk) && chunk.ready;
        public void RegisterTraffic(StreamedTraffic vehicle) { if (!vehicles.Contains(vehicle)) vehicles.Add(vehicle); }
        public void UnregisterTraffic(StreamedTraffic vehicle) => vehicles.Remove(vehicle);
        public bool HasVehicleAhead(StreamedTraffic vehicle)
        {
            foreach (var other in vehicles)
            {
                if (!other || other == vehicle || !other.gameObject.activeInHierarchy) continue;
                Vector3 delta = other.transform.position - vehicle.transform.position;
                if (Mathf.Abs(delta.y) > 2 || delta.sqrMagnitude > 36 || delta.sqrMagnitude < .01f) continue;
                if (Vector3.Dot(vehicle.transform.forward, other.transform.forward) > .5f &&
                    Vector3.Dot(delta.normalized, vehicle.transform.forward) > .65f) return true;
            }
            return false;
        }
        public float EdgeLength(Vector2Int owner, bool horizontal)
        {
            float length = 0;
            var previous = EdgePoint(owner, horizontal, 0);
            for (int i = 1; i <= 24; i++)
            {
                var next = EdgePoint(owner, horizontal, i / 24f);
                length += Vector3.Distance(previous, next); previous = next;
            }
            return length;
        }

        IEnumerator BuildChunk(Vector2Int coord, Chunk chunk)
        {
            var root = chunk.root.transform;
            Vector3 ground = transform.position + new Vector3((coord.x - origin.x + .5f) * profile.cellSize, -.18f, (coord.y - origin.y + .5f) * profile.cellSize);
            Box(root, "Ground", ground, new Vector3(profile.cellSize, .3f, profile.cellSize), Material("Ground", new Color(.3f, .32f, .27f)));
            BuildJunction(coord, root);
            for (int axis = 0; axis < 2; axis++)
            {
                if (!wanted.Contains(coord)) yield break;
                bool horizontal = axis == 0;
                BuildRoad(coord, horizontal, chunk);
                yield return null;
                var rng = new System.Random((int)(WorldRoadNetwork.Hash(coord.x, coord.y, profile.seed, 10 + axis) & 0x7fffffff));
                int budget = 0;
                float frontage = profile.streetAssets ? profile.streetAssets.houses.nominalSize.x : 4.5f;
                int houses = Mathf.Min(profile.housesPerRoadSide, Mathf.FloorToInt(EdgeLength(coord, horizontal) * .66f / Mathf.Max(7.5f, frontage + 3)));
                for (int i = 0; i < houses; i++)
                {
                    float t = Mathf.Lerp(.17f, .83f, (i + .5f) / Mathf.Max(1, houses));
                    Vector3 point = EdgePoint(coord, horizontal, t);
                    if (point.y - transform.position.y > .08f) continue; // No houses or shops on flyover ramps.
                    Vector3 forward = EdgeTangent(coord, horizontal, t);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        point = EdgePoint(coord, horizontal, t); // Origin may have shifted during the previous yield.
                        BuildHouseAndProps(root, point, right, forward, side, rng);
                        if (++budget >= profile.housesPerFrame)
                        {
                            budget = 0; yield return null;
                            if (!wanted.Contains(coord)) yield break;
                        }
                    }
                }
                BuildTraffic(coord, horizontal, root);
            }
        }

        void BuildRoad(Vector2Int owner, bool horizontal, Chunk chunk)
        {
            float half = profile.roadWidth / 2;
            var asphalt = Material("Asphalt", new Color(.12f, .13f, .14f));
            var concrete = Material("Concrete", new Color(.5f, .48f, .44f));
            Ribbon(chunk, owner, horizontal, "Curved asphalt", -half, half, .025f, 0, 1, asphalt, true, thickness: .45f);
            float start = Trim(owner, horizontal, false), end = Trim(owner, horizontal, true);
            for (int side = -1; side <= 1; side += 2)
            {
                float a = side * half, b = side * (half + profile.sidewalkWidth);
                Ribbon(chunk, owner, horizontal, "Sidewalk", Mathf.Min(a, b), Mathf.Max(a, b), .18f, start, end, concrete, true, thickness: .18f);
                Ribbon(chunk, owner, horizontal, "Kerb", side * half - .07f, side * half + .07f, .2f, start, end, concrete);
            }
            bool avenue = (horizontal ? owner.y : owner.x) % 3 == 0;
            if (avenue)
                Ribbon(chunk, owner, horizontal, "Median", -.55f, .55f, .33f, start, end,
                    Material("Median", new Color(.43f, .47f, .31f)), true, thickness: .33f);
            // Broken lane marks stop before junctions.
            for (float t = start + .02f; t < end - .02f; t += .045f)
            {
                if (!avenue)
                    Ribbon(chunk, owner, horizontal, "Centre dash", -.06f, .06f, .04f, t, Mathf.Min(t + .016f, end), Material("Paint", new Color(.9f, .85f, .64f)), false, 1);
            }
            if (!horizontal || (!WorldRoadNetwork.IsFlyover(owner, profile) && !WorldRoadNetwork.IsFlyover(WorldRoadNetwork.End(owner, true), profile))) return;
            Ribbon(chunk, owner, horizontal, "Flyover underside", -half, half, -.4f, 0, 1, concrete);
            for (int i = 1; i < 16; i++)
            {
                float t = i / 16f;
                Vector3 p = EdgePoint(owner, true, t);
                float height = p.y - transform.position.y;
                if (height < 1) continue;
                Box(chunk.root.transform, "Flyover pier", new Vector3(p.x, transform.position.y + (height - .4f) / 2, p.z),
                    new Vector3(1.2f, height - .4f, 1.2f), concrete);
            }
            for (int side = -1; side <= 1; side += 2)
                Ribbon(chunk, owner, true, "Flyover guardrail", side * (half + profile.sidewalkWidth) - .08f,
                    side * (half + profile.sidewalkWidth) + .08f, .95f, start, end, Material("Steel", new Color(.3f, .32f, .34f)), true, thickness: .8f);
        }

        float Trim(Vector2Int owner, bool horizontal, bool end)
        {
            float low = 0, high = .3f;
            Vector3 node = EdgePoint(owner, horizontal, end ? 1 : 0);
            for (int i = 0; i < 16; i++)
            {
                float mid = (low + high) / 2;
                float distance = Vector3.Distance(node, EdgePoint(owner, horizontal, end ? 1 - mid : mid));
                if (distance < profile.JunctionRadius) low = mid; else high = mid;
            }
            return end ? 1 - high : high;
        }

        void BuildJunction(Vector2Int node, Transform parent)
        {
            Vector3 p = Node(node);
            float radius = profile.JunctionRadius;
            bool bridge = WorldRoadNetwork.IsFlyover(node, profile);
            var road = Material("Asphalt", new Color(.12f, .13f, .14f));
            if (bridge)
            {
                Box(parent, "Lower north-south crossing", p, new Vector3(profile.roadWidth, .04f, radius * 2), road);
                Box(parent, "Flyover deck", p + Vector3.up * (profile.flyoverHeight - .22f), new Vector3(radius * 2, .5f, profile.roadWidth), road);
                var concrete = Material("Concrete", new Color(.5f, .48f, .44f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float offset = side * (profile.roadWidth / 2 + profile.sidewalkWidth / 2);
                    Box(parent, "Flyover footpath", p + new Vector3(0, profile.flyoverHeight + .09f, offset), new Vector3(radius * 2, .18f, profile.sidewalkWidth), concrete);
                    Box(parent, "Lower footpath", p + new Vector3(offset, .09f, 0), new Vector3(profile.sidewalkWidth, .18f, radius * 2), concrete);
                    Box(parent, "Flyover junction barrier", p + new Vector3(0, profile.flyoverHeight + .6f, side * (profile.roadWidth / 2 + profile.sidewalkWidth)),
                        new Vector3(radius * 2, .9f, .16f), concrete);
                }
            }
            else
            {
                Box(parent, "Intersection", p, new Vector3(radius * 2, .04f, radius * 2), road);
                for (int i = 0; i < 4; i++)
                {
                    bool horizontal = i < 2;
                    int side = i % 2 == 0 ? 1 : -1;
                    Vector3 offset = horizontal ? new Vector3(side * (radius + 2), 0, side * (profile.roadWidth / 2 + .8f)) :
                        new Vector3(-side * (profile.roadWidth / 2 + .8f), 0, side * (radius + 2));
                    BuildSignal(parent, node, p + offset, horizontal, horizontal ? (side == 1 ? 90 : -90) : (side == 1 ? 0 : 180));
                }
                // Zebra crossings at four approaches; keep the median out of the junction.
                var paint = Material("Paint", new Color(.9f, .85f, .64f));
                int stripes = Mathf.Max(1, Mathf.FloorToInt((profile.roadWidth / 2 - .6f) / 1.1f));
                for (int side = -1; side <= 1; side += 2)
                    for (int stripe = -stripes; stripe <= stripes; stripe++)
                    {
                        Box(parent, "Crosswalk", p + new Vector3(stripe * 1.1f, .035f, side * (radius - 2)), new Vector3(.6f, .025f, 2.2f), paint);
                        Box(parent, "Crosswalk", p + new Vector3(side * (radius - 2), .035f, stripe * 1.1f), new Vector3(2.2f, .025f, .6f), paint);
                    }
            }
        }

        void BuildSignal(Transform parent, Vector2Int node, Vector3 p, bool horizontal, float yaw)
        {
            var root = new GameObject("Traffic signal").transform;
            root.SetParent(parent, false); root.position = p; root.rotation = Quaternion.Euler(0, yaw, 0);
            var steel = Material("Steel", new Color(.3f, .32f, .34f));
            Box(root, "Pole", root.TransformPoint(new Vector3(0, 1.65f, 0)), new Vector3(.08f, 3.3f, .08f), steel);
            Box(root, "Signal housing", root.TransformPoint(new Vector3(0, 3, .12f)), new Vector3(.4f, 1.15f, .28f), Material("Black", new Color(.04f, .04f, .04f)), root.rotation);
            var signal = root.gameObject.AddComponent<StreetSignal>();
            signal.world = this; signal.node = node; signal.horizontal = horizontal; signal.bulbs = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = i == 0 ? "Red" : i == 1 ? "Amber" : "Green";
                bulb.transform.SetParent(root, false);
                bulb.transform.localPosition = new Vector3(0, 3.35f - i * .34f, .3f);
                bulb.transform.localScale = Vector3.one * .25f;
                Destroy(bulb.GetComponent<Collider>());
                signal.bulbs[i] = bulb.GetComponent<Renderer>();
                signal.bulbs[i].sharedMaterial = Material("Signal bulb", Color.white, true);
            }
        }

        void BuildHouseAndProps(Transform parent, Vector3 point, Vector3 right, Vector3 forward, int side, System.Random rng)
        {
            var assets = profile.streetAssets;
            if (!assets) return;
            var prefab = Pick(assets.houses, rng);
            var authored = prefab ? prefab.GetComponent<HanoiHouse>() : null;
            float depth = authored ? authored.bodySize.z : assets.houses.nominalSize.z;
            float offset = profile.roadWidth / 2 + profile.sidewalkWidth + depth / 2 + .25f;
            var position = point + right * side * offset + Vector3.up * .18f;
            Quaternion facing = Quaternion.LookRotation(-right * side, Vector3.up);
            if (prefab)
            {
                var house = Instantiate(prefab, position, facing, parent);
                house.name = prefab.name;
            }
            else Box(parent, "House placeholder", position + Vector3.up * 5, new Vector3(4.5f, 10, depth), Material("Plaster", new Color(.7f, .68f, .56f)), facing);
            Vector3 prop = point + right * side * (profile.roadWidth / 2 + profile.sidewalkWidth - .6f) + Vector3.up * .18f;
            float chance = (float)rng.NextDouble();
            if (chance < assets.shopChance)
            {
                PlaceSet(parent, assets.tables, prop, facing, rng);
                PlaceSet(parent, assets.chairs, prop + forward * .7f, facing, rng);
                PlaceSet(parent, assets.chairs, prop - forward * .7f, facing, rng);
            }
            else if (rng.NextDouble() < assets.parkedBikeChance) PlaceSet(parent, assets.motorbikes, prop, facing, rng);
            else if (rng.NextDouble() < assets.treeChance) PlaceSet(parent, assets.trees, prop, facing, rng);
            if (rng.NextDouble() < .1)
            {
                PlaceSet(parent, assets.bins, prop + forward * 2, facing, rng);
                if (rng.NextDouble() < assets.litterChance) PlaceSet(parent, assets.rubbishBags, prop + forward * 2.6f, facing, rng);
            }
        }

        void BuildTraffic(Vector2Int owner, bool horizontal, Transform root)
        {
            var assets = profile.streetAssets;
            if (!assets) return;
            var rng = new System.Random((int)(WorldRoadNetwork.Hash(owner.x, owner.y, profile.seed, horizontal ? 30 : 31) & 0x7fffffff));
            for (int i = 0; i < profile.vehiclesPerRoad; i++)
            {
                var prefab = Pick(rng.NextDouble() < .2 ? assets.cars : assets.motorbikes, rng);
                if (!prefab) continue;
                var obj = Instantiate(prefab, root);
                // Disable legacy straight-line motion if a custom prefab includes it.
                foreach (var motion in obj.GetComponents<StreetVehicle>()) motion.enabled = false;
                obj.AddComponent<StreamedTraffic>().Initialize(this, owner, horizontal, i % 2 == 0,
                    .2f + i * .15f, (uint)rng.Next());
            }
        }

        static GameObject Pick(PrefabSet set, System.Random rng) => set != null && set.variants != null && set.variants.Length > 0 ? set.variants[rng.Next(set.variants.Length)] : null;
        static void PlaceSet(Transform parent, PrefabSet set, Vector3 p, Quaternion rotation, System.Random rng)
        { var prefab = Pick(set, rng); if (prefab) Instantiate(prefab, p, rotation, parent); }

        Material Material(string name, Color color, bool unlit = false)
        {
            if (materials.TryGetValue(name, out var existing)) return existing;
            string shader = GraphicsSettings.currentRenderPipeline ? (unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit") : (unlit ? "Unlit/Color" : "Standard");
            var mat = new Material(Shader.Find(shader)) { name = name, color = color };
            materials.Add(name, mat); return mat;
        }

        static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material mat, Quaternion? rotation = null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.position = position; obj.transform.rotation = rotation ?? Quaternion.identity;
            obj.transform.localScale = size; obj.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void Ribbon(Chunk chunk, Vector2Int owner, bool horizontal, string name, float left, float right, float height,
            float start, float end, Material mat, bool collision = false, int segments = 0, float thickness = 0)
        {
            int count = segments > 0 ? segments : Mathf.Max(2, Mathf.CeilToInt(profile.curveSegments * (end - start)));
            int topVertices = (count + 1) * 2;
            var vertices = new Vector3[thickness > 0 ? topVertices * 2 : topVertices];
            var uv = new Vector2[vertices.Length]; var indices = new List<int>();
            var root = chunk.root.transform;
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Lerp(start, end, i / (float)count);
                Vector3 p = EdgePoint(owner, horizontal, t) + Vector3.up * height;
                Vector3 side = Vector3.Cross(Vector3.up, EdgeTangent(owner, horizontal, t)).normalized;
                vertices[i * 2] = root.InverseTransformPoint(p + side * left);
                vertices[i * 2 + 1] = root.InverseTransformPoint(p + side * right);
                uv[i * 2] = new Vector2(0, t * profile.cellSize / 4);
                uv[i * 2 + 1] = new Vector2((right - left) / 4, uv[i * 2].y);
                if (i == count) continue;
                int v = i * 2;
                indices.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            if (thickness > 0)
            {
                int o = topVertices;
                for (int i = 0; i < topVertices; i++) { vertices[i + o] = vertices[i] - Vector3.up * thickness; uv[i + o] = uv[i]; }
                for (int i = 0; i < count; i++)
                {
                    int v = i * 2;
                    indices.AddRange(new[] { v + o, v + 1 + o, v + 2 + o, v + 1 + o, v + 3 + o, v + 2 + o });
                    indices.AddRange(new[] { v, v + o, v + 2, v + 2, v + o, v + 2 + o });
                    indices.AddRange(new[] { v + 1, v + 3, v + 1 + o, v + 3, v + 3 + o, v + 1 + o });
                }
                indices.AddRange(new[] { 0, 1, o, 1, o + 1, o });
                int last = count * 2;
                indices.AddRange(new[] { last, last + o, last + 1, last + 1, last + o, last + o + 1 });
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = indices.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); chunk.meshes.Add(mesh);
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void Remove(Vector2Int key)
        {
            if (!chunks.TryGetValue(key, out var chunk)) return;
            chunk.root.SetActive(false); Destroy(chunk.root);
            foreach (var mesh in chunk.meshes) Destroy(mesh);
            chunks.Remove(key);
        }

        void OnDisable()
        {
            if (worker != null) StopCoroutine(worker);
            worker = null;
            foreach (var key in new List<Vector2Int>(chunks.Keys)) Remove(key);
            wanted.Clear(); centre = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var mat in materials.Values) Destroy(mat);
            materials.Clear();
            vehicles.Clear();
        }
    }
}
