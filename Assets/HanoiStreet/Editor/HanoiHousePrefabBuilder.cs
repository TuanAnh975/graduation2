using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HanoiStreet.Editor
{
    public static class HanoiHousePrefabBuilder
    {
        const string Folder = "Assets/HanoiStreet/Generated/Houses";
        const float Width = 4.5f, Depth = 8, FloorHeight = 3.4f, Front = Depth / 2;
        static readonly string[] ShopNames = { "PHỞ HÀ NỘI", "TẠP HÓA", "CÀ PHÊ", "BÚN CHẢ" };

        [MenuItem("Hanoi Street/Create house prefab library")]
        public static void CreateLibrary() { BuildLibrary(); AssetDatabase.SaveAssets(); }

        [MenuItem("Hanoi Street/Validate house prefab library")]
        public static void ValidateLibrary()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Folder });
            if (paths.Length != 16) throw new System.InvalidOperationException("Expected 16 house prefabs. Create the library first.");
            var combinations = new HashSet<string>();
            foreach (var guid in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var house = prefab.GetComponent<HanoiHouse>();
                if (!house || house.floors < 1 || house.floors > 4 ||
                    !Mathf.Approximately(house.bodySize.y, house.floors * FloorHeight))
                    throw new System.InvalidOperationException(prefab.name + ": invalid floor dimensions.");
                if (!combinations.Add($"{house.floors}-{house.glassEntrance}-{house.tiledRoof}"))
                    throw new System.InvalidOperationException("Duplicate house combination.");
                var sign = prefab.GetComponentInChildren<TextMesh>();
                if (!sign || string.IsNullOrWhiteSpace(sign.text) || !sign.font)
                    throw new System.InvalidOperationException(prefab.name + ": missing shop sign/font.");
                bool flagFound = false;
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.sharedMaterial || !renderer.sharedMaterial.shader)
                        throw new System.InvalidOperationException(prefab.name + ": missing material/shader.");
                    flagFound |= renderer.sharedMaterial.name == "FlagRed";
                }
                if (flagFound != (house.floors > 1))
                    throw new System.InvalidOperationException(prefab.name + ": incorrect flag placement.");
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.GetComponent<TextMesh>()) continue;
                    if (!filter.sharedMesh || !AssetDatabase.Contains(filter.sharedMesh))
                        throw new System.InvalidOperationException(prefab.name + ": unsaved mesh.");
                    foreach (var v in filter.sharedMesh.vertices)
                        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                            throw new System.InvalidOperationException(prefab.name + ": invalid mesh vertex.");
                }
            }
            Debug.Log("PASS: 16 unique house variants, floor dimensions, signs, flags, materials and saved mesh vertices.");
        }

        public static GameObject[] BuildLibrary()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var prefabs = new List<GameObject>();
            for (int floors = 1; floors <= 4; floors++)
                for (int door = 0; door < 2; door++)
                    for (int roof = 0; roof < 2; roof++)
                    {
                        string name = $"Hanoi_{floors}F_{(door == 0 ? "Shutter" : "Glass")}_{(roof == 0 ? "CageTin" : "Tile")}";
                        string path = Folder + "/" + name + ".prefab";
                        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (existing) { prefabs.Add(existing); continue; }
                        var root = new GameObject(name);
                        try
                        {
                            var data = root.AddComponent<HanoiHouse>();
                            data.floors = floors;
                            data.glassEntrance = door == 1;
                            data.tiledRoof = roof == 1;
                            data.bodySize = new Vector3(Width, floors * FloorHeight, Depth);
                            Build(root.transform, floors, door == 1, roof == 1);
                            Combine(root.transform, name);
                            var collider = root.AddComponent<BoxCollider>();
                            collider.center = Vector3.up * data.bodySize.y / 2;
                            collider.size = data.bodySize;
                            prefabs.Add(PrefabUtility.SaveAsPrefabAsset(root, path));
                        }
                        finally { Object.DestroyImmediate(root); }
                    }
            AssetDatabase.SaveAssets();
            Debug.Log("House library: 16 prefabs (1–4 floors × two entrances × two roofs). Existing assets preserved.");
            return prefabs.ToArray();
        }

        static Material Mat(string name, Color color, float smoothness = .15f, float metal = 0)
        {
            string path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metal);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void Box(Transform parent, string name, Vector3 p, Vector3 size, Material mat, float rotateZ = 0)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = p;
            obj.transform.localScale = size;
            obj.transform.localRotation = Quaternion.Euler(0, 0, rotateZ);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
        }

        static void Bar(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = "Steel bar";
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = (a + b) / 2;
            obj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            obj.transform.localScale = new Vector3(thickness, (b - a).magnitude / 2, thickness);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
        }

        static void Build(Transform t, int floors, bool glassDoor, bool tiles)
        {
            var wall = Mat("Plaster" + floors, Color.Lerp(new Color(.77f, .73f, .58f), new Color(.57f, .66f, .63f), (floors - 1) / 3f));
            var concrete = Mat("Concrete", new Color(.48f, .47f, .42f));
            var steel = Mat("DarkSteel", new Color(.18f, .2f, .19f), .4f, .65f);
            var shutter = Mat("Shutter", new Color(.44f, .49f, .46f), .4f, .5f);
            // Opaque tinted glass avoids sorting artifacts; no interior is modelled yet.
            var glass = Mat("TintedGlass", new Color(.13f, .27f, .31f), .88f, .35f);
            var red = Mat("SignRed", new Color(.62f, .055f, .035f));
            var cream = Mat("AwningCream", new Color(.85f, .8f, .64f));
            float height = floors * FloorHeight;
            Box(t, "Plaster body", new Vector3(0, height / 2, 0), new Vector3(Width, height, Depth), wall);
            Box(t, "Damp plinth", new Vector3(0, .16f, Front + .025f), new Vector3(Width, .32f, .06f), concrete);
            Box(t, "Entrance recess", new Vector3(0, 1.25f, Front + .04f), new Vector3(3.65f, 2.5f, .08f), steel);
            if (glassDoor)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(t, "Glass entrance leaf", new Vector3(side * .88f, 1.24f, Front + .1f), new Vector3(1.65f, 2.35f, .07f), glass);
                    Bar(t, new Vector3(side * .12f, .95f, Front + .19f), new Vector3(side * .12f, 1.5f, Front + .19f), .025f, steel);
                }
            }
            else
            {
                for (int slat = 0; slat < 24; slat++)
                    Box(t, "Roller shutter slat", new Vector3(0, .1f + slat * .1f, Front + .13f), new Vector3(3.4f, .085f, .08f), shutter);
            }
            Box(t, "Shop sign", new Vector3(0, 2.95f, Front + .1f), new Vector3(4.15f, .65f, .16f), red);
            var textObj = new GameObject("Vietnamese shop sign");
            textObj.transform.SetParent(t, false);
            textObj.transform.localPosition = new Vector3(0, 2.95f, Front + .19f);
            // TextMesh faces local -Z; turn it toward the street (+Z).
            textObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var text = textObj.AddComponent<TextMesh>();
            text.text = ShopNames[floors - 1];
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 64;
            text.characterSize = .065f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1, .9f, .45f);
            // Retractable awning roller, arms and alternating canvas strips.
            Bar(t, new Vector3(-2, 2.58f, Front + .2f), new Vector3(2, 2.58f, Front + .2f), .15f, steel);
            for (int stripe = 0; stripe < 12; stripe++)
            {
                float x = -1.925f + stripe * .35f;
                // Sloped strip along Z, with its outer edge lower than the wall edge.
                var strip = new GameObject("Retractable canvas stripe");
                strip.transform.SetParent(t, false);
                strip.transform.localPosition = new Vector3(x, 2.43f, Front + .8f);
                strip.transform.localRotation = Quaternion.Euler(14, 0, 0);
                Box(strip.transform, "Canvas", Vector3.zero, new Vector3(.35f, .035f, 1.3f), stripe % 2 == 0 ? red : cream);
            }
            for (int side = -1; side <= 1; side += 2)
                Bar(t, new Vector3(side * 1.8f, 2.1f, Front + .15f), new Vector3(side * 1.8f, 2.27f, Front + 1.42f), .035f, steel);
            for (int floor = 1; floor < floors; floor++)
            {
                float y = floor * FloorHeight;
                Box(t, "Balcony slab", new Vector3(0, y + .02f, Front + .36f), new Vector3(4.2f, .16f, .95f), concrete);
                Box(t, "Window frame", new Vector3(0, y + 1.45f, Front + .05f), new Vector3(2.95f, 2, .09f), steel);
                Box(t, "Window glass", new Vector3(0, y + 1.45f, Front + .105f), new Vector3(2.8f, 1.85f, .04f), glass);
                Box(t, "Window mullion", new Vector3(0, y + 1.45f, Front + .14f), new Vector3(.055f, 1.9f, .045f), cream);
                Box(t, "Window transom", new Vector3(0, y + 1.65f, Front + .14f), new Vector3(2.8f, .05f, .045f), cream);
                for (int bar = 0; bar <= 14; bar++)
                {
                    float x = -2 + bar * 4f / 14;
                    Bar(t, new Vector3(x, y + .15f, Front + .82f), new Vector3(x, y + 1.12f, Front + .82f), .025f, steel);
                }
                Bar(t, new Vector3(-2.05f, y + 1.12f, Front + .82f), new Vector3(2.05f, y + 1.12f, Front + .82f), .045f, steel);
                for (int side = -1; side <= 1; side += 2)
                    Bar(t, new Vector3(side * 2, y + 1.12f, Front), new Vector3(side * 2, y + 1.12f, Front + .82f), .04f, steel);
                AddFlag(t, new Vector3(-1.65f, y + 1.35f, Front + .92f), steel);
            }
            if (tiles) TiledRoof(t, height);
            else CageRoof(t, height, steel);
        }

        static void CageRoof(Transform t, float y, Material steel)
        {
            var tin = Mat("ZincRoof", new Color(.38f, .46f, .48f), .45f, .65f);
            // Steel enclosure around a roof terrace; gate remains an open visual bay.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float z = -Depth / 2 + i * Depth / 20;
                    Bar(t, new Vector3(side * 2.12f, y, z), new Vector3(side * 2.12f, y + 1.9f, z), .025f, steel);
                }
                for (int i = 0; i <= 10; i++)
                {
                    float x = -2.12f + i * .424f;
                    Bar(t, new Vector3(x, y, side * Front), new Vector3(x, y + 1.9f, side * Front), .025f, steel);
                }
                Bar(t, new Vector3(side * 2.12f, y + 1.9f, -Front), new Vector3(side * 2.12f, y + 1.9f, Front), .065f, steel);
                Bar(t, new Vector3(-2.12f, y + 1.9f, side * Front), new Vector3(2.12f, y + 1.9f, side * Front), .065f, steel);
            }
            // Corrugation runs in the drainage direction (+Z); shallow slope prevents a flat roof.
            var roof = new GameObject("Sloped corrugated tin roof");
            roof.transform.SetParent(t, false);
            roof.transform.localPosition = new Vector3(0, y + 2, 0);
            roof.transform.localRotation = Quaternion.Euler(5, 0, 0);
            Box(roof.transform, "Tin sheet", Vector3.zero, new Vector3(4.9f, .045f, 8.6f), tin);
            for (int i = 0; i < 33; i++)
                Box(roof.transform, "Corrugation ridge", new Vector3(-2.4f + i * .15f, .04f, 0), new Vector3(.04f, .045f, 8.6f), tin);
        }

        static void TiledRoof(Transform t, float y)
        {
            var tile = Mat("Terracotta", new Color(.46f, .17f, .08f));
            var edge = Mat("TerracottaLight", new Color(.59f, .24f, .12f));
            float rise = 1.45f, half = 2.55f;
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Pitched tile roof", new Vector3(side * half / 2, y + rise / 2 + .1f, 0),
                    new Vector3(Mathf.Sqrt(half * half + rise * rise), .12f, 8.6f), tile, -side * angle);
                for (int row = 1; row <= 9; row++)
                {
                    float fraction = row / 9f;
                    Box(t, "Tile row lip", new Vector3(side * half * fraction, y + rise * (1 - fraction) + .18f, 0),
                        new Vector3(.055f, .07f, 8.6f), edge, -side * angle);
                }
            }
            Bar(t, new Vector3(0, y + rise + .18f, -4.35f), new Vector3(0, y + rise + .18f, 4.35f), .18f, edge);
        }

        static void AddFlag(Transform t, Vector3 p, Material steel)
        {
            Bar(t, p - Vector3.up * .2f, p + Vector3.up * .85f, .025f, steel);
            var red = Mat("FlagRed", new Color(.85f, .015f, .025f));
            var yellow = Mat("FlagYellow", new Color(1, .85f, 0));
            // Vietnam flag: 3:2 ratio, centred five-point star with one point upward.
            var vertices = new[] { new Vector3(0, .2f, 0), new Vector3(.9f, .2f, 0), new Vector3(.9f, .8f, 0), new Vector3(0, .8f, 0) };
            MeshPart(t, "Vietnam flag cloth", p, vertices, new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 }, red);
            var star = new Vector3[11];
            star[0] = new Vector3(.45f, .5f, .008f);
            var indices = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                float angle = (90 + i * 36) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? .18f : .069f;
                star[i + 1] = star[0] + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                int a = i + 1, b = (i + 1) % 10 + 1;
                indices.AddRange(new[] { 0, a, b, 0, b, a });
            }
            MeshPart(t, "Five point yellow star", p, star, indices.ToArray(), yellow);
            // Back star: offset behind the cloth so both sides show the emblem.
            for (int i = 0; i < star.Length; i++) star[i].z = -.008f;
            MeshPart(t, "Star reverse", p, star, indices.ToArray(), yellow);
        }

        static void MeshPart(Transform parent, string name, Vector3 p, Vector3[] vertices, int[] triangles, Material mat)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = p;
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void Combine(Transform root, string assetName)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new List<GameObject>();
            var temporaryMeshes = new List<Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponent<TextMesh>()) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh) continue;
                var mat = renderer.sharedMaterial;
                if (!groups.ContainsKey(mat)) groups[mat] = new List<CombineInstance>();
                groups[mat].Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                sources.Add(filter.gameObject);
                if ((filter.sharedMesh.name == "Vietnam flag cloth" || filter.sharedMesh.name == "Five point yellow star" ||
                     filter.sharedMesh.name == "Star reverse") && !AssetDatabase.Contains(filter.sharedMesh) && !temporaryMeshes.Contains(filter.sharedMesh))
                    temporaryMeshes.Add(filter.sharedMesh);
            }
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = assetName + "_" + group.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "/" + mesh.name + ".asset");
                var child = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root, false);
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                child.GetComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
            foreach (var source in sources) Object.DestroyImmediate(source);
            foreach (var mesh in temporaryMeshes) Object.DestroyImmediate(mesh);
        }
    }
}
