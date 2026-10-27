using UnityEngine;

namespace HanoiStreet
{
    [CreateAssetMenu(menuName = "Hanoi Street/World Profile")]
    public sealed class WorldProfile : ScriptableObject
    {
        public int seed = 975;
        [Min(160)] public float cellSize = 256;
        [Range(1, 3)] public int loadRadius = 1;
        [Range(12, 64)] public int curveSegments = 32;
        [Range(0, .2f)] public float junctionJitter = .18f;
        [Range(8, 18)] public float roadWidth = 12;
        [Range(2, 5)] public float sidewalkWidth = 3;
        [Range(0, .8f)] public float flyoverChance = .5f;
        [Range(5.5f, 8)] public float flyoverHeight = 6;
        [Range(0, 24)] public int housesPerRoadSide = 12;
        [Range(1, 8)] public int housesPerFrame = 3;
        [Range(0, 4)] public int vehiclesPerRoad = 2;
        [Range(2, 14)] public float trafficSpeed = 7;
        public bool floatingOrigin = true;
        public StreetProfile streetAssets;

        public float JunctionRadius => roadWidth * .5f + sidewalkWidth + 1;
        public int MaximumLoadedChunks => (loadRadius * 2 + 1) * (loadRadius * 2 + 1);
    }
}
