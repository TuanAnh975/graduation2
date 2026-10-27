using UnityEngine;

namespace HanoiStreet
{
    [CreateAssetMenu(menuName = "Hanoi Street/Street Profile")]
    public sealed class StreetProfile : ScriptableObject
    {
        public int seed = 975;
        [Range(1, 8)] public int blocks = 3;
        [Min(20)] public float blockLength = 64;
        [Min(6)] public float roadWidth = 9;
        [Min(1.5f)] public float sidewalkWidth = 3;
        [Min(3)] public float houseFrontage = 4.5f;
        [Range(0, 1)] public float shopChance = .4f;
        [Range(0, 1)] public float parkedBikeChance = .65f;
        [Range(0, 1)] public float litterChance = .16f;
        [Range(0, 1)] public float treeChance = .3f;
        [Range(0, 30)] public int movingVehicles = 10;
        [Min(1)] public float vehicleSpeed = 6;
        [Tooltip("Prefab pivot at ground centre; +Z is the front. Nominal size in metres.")]
        public PrefabSet houses = new PrefabSet { nominalSize = new Vector3(4.5f, 12, 8) };
        public PrefabSet trees = new PrefabSet { nominalSize = new Vector3(2, 6, 2) };
        public PrefabSet roads = new PrefabSet { nominalSize = new Vector3(9, .15f, 64) };
        public PrefabSet sidewalks = new PrefabSet { nominalSize = new Vector3(3, .18f, 64) };
        public PrefabSet rubbishBags = new PrefabSet { nominalSize = new Vector3(.4f, .5f, .4f) };
        public PrefabSet bins = new PrefabSet { nominalSize = new Vector3(.5f, .9f, .5f) };
        public PrefabSet tables = new PrefabSet { nominalSize = new Vector3(.7f, .65f, .7f) };
        public PrefabSet chairs = new PrefabSet { nominalSize = new Vector3(.35f, .4f, .35f) };
        public PrefabSet motorbikes = new PrefabSet { nominalSize = new Vector3(.65f, 1.1f, 1.8f) };
        public PrefabSet cars = new PrefabSet { nominalSize = new Vector3(1.7f, 1.5f, 3.8f) };
    }

    [System.Serializable]
    public sealed class PrefabSet
    {
        public GameObject[] variants = new GameObject[0];
        public Vector3 nominalSize = Vector3.one;
    }
}
