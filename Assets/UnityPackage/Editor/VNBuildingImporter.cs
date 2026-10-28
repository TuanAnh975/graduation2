using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HanoiUrban;

public sealed class VNBuildingImporter : AssetPostprocessor
{
    bool IsBuilding => Path.GetFileName(assetPath).StartsWith("VN_", StringComparison.Ordinal)
        && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
        && File.Exists(Path.ChangeExtension(assetPath, ".json"));

    static Transform Socket(Transform root, string suffix) =>
        root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.EndsWith(suffix, StringComparison.Ordinal));

    void OnPreprocessModel()
    {
        if (!IsBuilding) return;
        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.generateSecondaryUV = true;
        importer.addCollider = false;
        importer.isReadable = false;
        importer.importNormals = ModelImporterNormals.Import;
    }

    Material OnAssignMaterialModel(Material source, Renderer renderer)
    {
        if (!IsBuilding) return null;
        string packageRoot = Path.GetDirectoryName(Path.GetDirectoryName(assetPath));
        string path = (packageRoot + "/Materials/" + source.name + ".mat").Replace('\\', '/');
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    void OnPostprocessModel(GameObject model)
    {
        if (!IsBuilding) return;
        var data = JsonUtility.FromJson<VNBuildingData>(
            File.ReadAllText(Path.ChangeExtension(assetPath, ".json")));
        var origin = Socket(model.transform, "__VN_ORIGIN_SOCKET");
        var front = Socket(model.transform, "__VN_FRONT_SOCKET");
        var up = Socket(model.transform, "__VN_UP_SOCKET");
        if (!origin || !front || !up)
            throw new InvalidDataException(assetPath + ": required orientation sockets missing.");

        Vector3 localOrigin = model.transform.InverseTransformPoint(origin.position);
        Vector3 sourceForward = model.transform.InverseTransformVector(front.position - origin.position);
        Vector3 sourceUp = model.transform.InverseTransformVector(up.position - origin.position);
        if (sourceForward.sqrMagnitude < .01f || sourceUp.sqrMagnitude < .01f)
            throw new InvalidDataException(assetPath + ": invalid orientation sockets.");

        Quaternion correction = Quaternion.Inverse(
            Quaternion.LookRotation(sourceForward.normalized, sourceUp.normalized));
        var content = new GameObject("VN_Geometry").transform;
        content.SetParent(model.transform, false);
        var children = model.transform.Cast<Transform>().Where(t => t != content).ToArray();
        foreach (var child in children) child.SetParent(content, false);
        content.localRotation = correction;
        content.localPosition = -(correction * localOrigin);
        // After this correction: Blender -Y => Unity +Z; Blender +Z => Unity +Y.
        // Width may change handedness; no negative object scale is introduced.
        var component = model.AddComponent<VNBuildingMetadata>();
        component.data = data;
        foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
            if (mr.sharedMaterials.All(m => m && m.name == "VN_Glazing"))
                mr.shadowCastingMode = ShadowCastingMode.Off;
    }
}

public static class VNBuildingLibraryMenu
{
    [Serializable] class MaterialEntry
    {
        public string name;
        public float[] color;
        public float metallic, roughness, alpha;
    }
    [Serializable] class MaterialManifest { public MaterialEntry[] materials; }

    [MenuItem("Tools/Vietnam Urban/Build Materials and Prefabs")]
    public static void Build()
    {
        var manifests = AssetDatabase.FindAssets("materials t:TextAsset")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith("/Materials/materials.json", StringComparison.Ordinal)).ToArray();
        foreach (string manifestPath in manifests)
        {
            string materialDir = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
            string packageRoot = Path.GetDirectoryName(materialDir).Replace('\\', '/');
            var manifest = JsonUtility.FromJson<MaterialManifest>(File.ReadAllText(manifestPath));
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("URP/Lit or Standard shader required.");
            foreach (var entry in manifest.materials)
            {
                string path = materialDir + "/" + entry.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                Color color = new Color(entry.color[0], entry.color[1], entry.color[2], entry.alpha);
                material.color = color;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", entry.metallic);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f-entry.roughness);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1f-entry.roughness);
                bool transparent = entry.alpha < .99f;
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1 : 0);
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", transparent ? 3 : 0);
                material.SetInt("_SrcBlend", (int)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
                material.SetInt("_DstBlend", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                material.SetInt("_ZWrite", transparent ? 0 : 1);
                material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                material.renderQueue = transparent ? 3000 : -1;
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                if (transparent)
                {
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.EnableKeyword("_ALPHABLEND_ON");
                }
                else
                {
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHABLEND_ON");
                }
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            string prefabDir = packageRoot + "/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabDir))
                AssetDatabase.CreateFolder(packageRoot, "Prefabs");
            foreach (string jsonPath in Directory.GetFiles(packageRoot + "/Models", "VN_*.json"))
            {
                var data = JsonUtility.FromJson<VNBuildingData>(File.ReadAllText(jsonPath));
                string fbxPath = Path.ChangeExtension(jsonPath, ".fbx").Replace('\\', '/');
                if (!File.Exists(fbxPath)) continue;
                EnsureTag(data.categoryTag);
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (!instance) throw new InvalidOperationException("Could not instantiate " + fbxPath);
                try
                {
                    instance.name = data.buildingId;
                    instance.tag = data.categoryTag;
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabDir + "/" + data.buildingId + ".prefab");
                    AssetDatabase.SetLabels(prefab, new[] {"VietnamUrban", data.categoryTag, data.styleFamily});
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Vietnam Urban prefabs built. Front = local +Z, base = Y0. Review shaders and navigation in Unity.");
    }

    static void EnsureTag(string value)
    {
        var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = manager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; ++i)
            if (tags.GetArrayElementAtIndex(i).stringValue == value) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = value;
        manager.ApplyModifiedProperties();
    }
}

