using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HanoiStreet.Editor
{
    public static class StreetProjectSetup
    {
        const string Root = "Assets/HanoiStreet/Generated";

        [MenuItem("Hanoi Street/Create demo scene")]
        public static void CreateDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<StreetProfile>(Root + "/Residential.asset");
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<StreetProfile>();
                AssetDatabase.CreateAsset(profile, Root + "/Residential.asset");
                MakePrefab(profile.trees, "Tree", new Color(.2f, .4f, .15f));
                MakePrefab(profile.roads, "Road", new Color(.13f, .14f, .15f));
                MakePrefab(profile.sidewalks, "Sidewalk", new Color(.48f, .46f, .42f));
                MakePrefab(profile.rubbishBags, "RubbishBag", Color.black);
                MakePrefab(profile.bins, "Bin", Color.green);
                MakePrefab(profile.tables, "Table", Color.red);
                MakePrefab(profile.chairs, "Chair", Color.blue);
                MakePrefab(profile.motorbikes, "Motorbike", Color.black);
                MakePrefab(profile.cars, "Car", Color.gray);
                EditorUtility.SetDirty(profile);
            }
            // Preserve custom prefab bindings; replace only the original blockout or empty set.
            if (profile.houses.variants == null || profile.houses.variants.Length == 0 ||
                (profile.houses.variants.Length == 1 && profile.houses.variants[0] &&
                 profile.houses.variants[0].name == "House_BLOCKOUT"))
            {
                profile.houses.variants = HanoiHousePrefabBuilder.BuildLibrary();
                profile.houses.nominalSize = new Vector3(4.5f, 3.4f, 8);
                EditorUtility.SetDirty(profile);
            }
            ConfigurePipeline();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var generator = new GameObject("Hanoi runtime generator").AddComponent<StreetGenerator>();
            generator.profile = profile;
            var sun = new GameObject("Afternoon sunlight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(38, -30, 0);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.55f, .6f, .65f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1.7f, 4);
            camera.transform.rotation = Quaternion.Euler(0, 0, 0);
            camera.farClipPlane = 600;
            camera.fieldOfView = 65;
            camera.backgroundColor = new Color(.62f, .7f, .77f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "/HanoiDemo.unity");
            Selection.activeObject = generator;
            Debug.Log("Demo created. Press Play to generate; move the camera with the Scene view before Play if needed.");
        }

        static void ConfigurePipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline) return;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/HanoiURP.asset");
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Root + "/HanoiRenderer.asset");
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, Root + "/HanoiURP.asset");
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static void MakePrefab(PrefabSet set, string name, Color color)
        {
            string path = Root + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) { set.variants = new[] { existing }; return; }
            var root = new GameObject(name + "_BLOCKOUT");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = set.nominalSize;
            cube.transform.localPosition = Vector3.up * set.nominalSize.y / 2;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, Root + "/" + name + ".mat");
            cube.GetComponent<Renderer>().sharedMaterial = material;
            set.variants = new[] { PrefabUtility.SaveAsPrefabAsset(root, path) };
            Object.DestroyImmediate(root);
        }
    }
}
