using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace HanoiStreet
{
    public sealed class StreetGenerator : MonoBehaviour
    {
        public StreetProfile profile;
        public bool generateOnStart = true;
        Transform generated;
        System.Random rng;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        static readonly Color Asphalt = new Color(.13f, .14f, .15f);
        static readonly Color Concrete = new Color(.48f, .46f, .42f);
        float Random(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        bool Chance(float probability) => rng.NextDouble() < probability;

        void Start() { if (generateOnStart) Generate(); }

        [ContextMenu("Generate street")]
        public void Generate()
        {
            if (!profile) { Debug.LogError("Assign a StreetProfile.", this); return; }
            if (profile.blockLength < 20 || profile.blocks < 1 || profile.houseFrontage < 3 ||
                profile.roadWidth < 6 || profile.sidewalkWidth < 1.5f)
            { Debug.LogError("Invalid street dimensions.", this); return; }
            if (generated)
            {
                generated.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generated.gameObject);
                else DestroyImmediate(generated.gameObject);
            }
            rng = new System.Random(profile.seed);
            generated = new GameObject("Generated street — seed " + profile.seed).transform;
            generated.SetParent(transform, false);
            float length = profile.blocks * profile.blockLength;
            Spawn(profile.roads, "Road", new Vector3(0, -.15f, length / 2),
                new Vector3(profile.roadWidth, .15f, length), 0, Asphalt);
            // Continuous accessible strip next to the road; clutter stays on the building side.
            for (int side = -1; side <= 1; side += 2)
            {
                float edge = profile.roadWidth / 2;
                Spawn(profile.sidewalks, "Sidewalk", new Vector3(side * (edge + profile.sidewalkWidth / 2), 0, length / 2),
                    new Vector3(profile.sidewalkWidth, .18f, length), 0, Concrete);
                int count = Mathf.FloorToInt(length / profile.houseFrontage);
                float frontage = length / count;
                for (int i = 0; i < count; i++)
                {
                    float z = (i + .5f) * frontage;
                    float height = rng.Next(1, 5) * 3.4f;
                    float depth = Mathf.Max(1, profile.houses.nominalSize.z);
                    float x = side * (edge + profile.sidewalkWidth + depth / 2);
                    Color facade = Color.Lerp(new Color(.78f, .75f, .62f), new Color(.5f, .59f, .57f), Random(0, 1));
                    Spawn(profile.houses, "Tube house", new Vector3(x, .18f, z),
                        new Vector3(depth, height, frontage - .08f), side == 1 ? 270 : 90, facade, true);
                    float propX = side * (edge + profile.sidewalkWidth - .65f);
                    bool shop = Chance(profile.shopChance);
                    if (shop)
                    {
                        Spawn(profile.tables, "Food stall table", new Vector3(propX, .18f, z), new Vector3(.7f, .65f, .7f), 0, Color.red);
                        foreach (float dz in new[] { -.65f, .65f })
                            Spawn(profile.chairs, "Plastic stool", new Vector3(propX, .18f, z + dz), new Vector3(.35f, .4f, .35f), 0, Color.blue);
                    }
                    else if (Chance(profile.parkedBikeChance))
                        Spawn(profile.motorbikes, "Parked motorbike", new Vector3(propX, .18f, z), new Vector3(.65f, 1.1f, 1.8f), Random(-15, 15), Color.black);
                    else if (Chance(profile.treeChance))
                        Spawn(profile.trees, "Street tree", new Vector3(propX, .18f, z), new Vector3(1.6f, 6, 1.6f), 0, new Color(.2f, .38f, .16f));
                    // Bags collect beside bins at block ends, rather than uniformly across the street.
                    if (i % 10 == 0)
                    {
                        Spawn(profile.bins, "Rubbish bin", new Vector3(propX, .18f, z - 1.5f), new Vector3(.5f, .9f, .5f), 0, Color.green);
                        if (Chance(profile.litterChance))
                            Spawn(profile.rubbishBags, "Bag beside bin", new Vector3(propX, .18f, z - 2.1f), new Vector3(.4f, .5f, .4f), 0, Color.black);
                    }
                }
            }
            int perLane = Mathf.Min(Mathf.CeilToInt(profile.movingVehicles / 2f), Mathf.FloorToInt(length / 8));
            for (int i = 0; i < profile.movingVehicles && perLane > 0; i++)
            {
                int lane = i % 2 == 0 ? 1 : -1;
                int slot = i / 2;
                if (slot >= perLane) break;
                bool car = Chance(.2f);
                GameObject vehicle = Spawn(car ? profile.cars : profile.motorbikes, "Traffic",
                    new Vector3(lane * profile.roadWidth * .25f, .02f, slot * length / perLane),
                    car ? new Vector3(1.7f, 1.5f, 3.8f) : new Vector3(.65f, 1.1f, 1.8f), lane == 1 ? 0 : 180,
                    car ? new Color(.6f, .62f, .65f) : Color.black);
                var motion = vehicle.AddComponent<StreetVehicle>();
                motion.direction = lane;
                motion.speed = Mathf.Max(1, profile.vehicleSpeed);
                motion.startZ = 0;
                motion.endZ = length;
            }
            Debug.Log("Generated Hanoi street with seed " + profile.seed, this);
        }

        GameObject Spawn(PrefabSet set, string label, Vector3 position, Vector3 size, float yaw, Color color, bool house = false)
        {
            GameObject prefab = null;
            if (set.variants != null && set.variants.Length > 0)
                prefab = set.variants[rng.Next(set.variants.Length)];
            GameObject obj;
            if (prefab)
            {
                obj = Instantiate(prefab, generated);
                Vector3 n = set.nominalSize;
                // Houses face across the road; authored width is local X and depth local Z.
                Vector3 localSize = house ? new Vector3(size.z, size.y, size.x) : size;
                var authoredHouse = prefab.GetComponent<HanoiHouse>();
                if (house && authoredHouse)
                {
                    // Fit frontage only: keep authored floor height and architectural details.
                    n = authoredHouse.bodySize;
                    localSize.y = n.y;
                    localSize.z = n.z;
                }
                obj.transform.localScale = Vector3.Scale(obj.transform.localScale,
                    new Vector3(localSize.x / Mathf.Max(.01f, n.x), localSize.y / Mathf.Max(.01f, n.y), localSize.z / Mathf.Max(.01f, n.z)));
            }
            else
            {
                obj = new GameObject(label + " [placeholder]");
                obj.transform.SetParent(generated, false);
                var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shape.transform.SetParent(obj.transform, false);
                shape.transform.localPosition = Vector3.up * size.y / 2;
                shape.transform.localScale = size;
                // Placeholder boxes use world-aligned footprints, regardless of facing.
                shape.transform.localRotation = Quaternion.Euler(0, -yaw, 0);
                if (!materials.TryGetValue(color, out Material mat))
                {
                    var shader = Shader.Find(GraphicsSettings.currentRenderPipeline ? "Universal Render Pipeline/Lit" : "Standard");
                    mat = new Material(shader) { color = color };
                    materials[color] = mat;
                }
                shape.GetComponent<Renderer>().sharedMaterial = mat;
            }
            obj.name = label;
            obj.transform.localPosition = position;
            obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return obj;
        }

        void OnDestroy()
        {
            foreach (var material in materials.Values)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            materials.Clear();
        }
    }
}
