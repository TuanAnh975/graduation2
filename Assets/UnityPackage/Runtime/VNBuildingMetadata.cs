using System;
using UnityEngine;

namespace HanoiUrban
{
    [Serializable]
    public class VNBuildingData
    {
        public string buildingId, categoryTag, variantId, frontDirection, styleFamily;
        public float width, depth, height;
        public int floorCount;
        public string status;
    }

    public sealed class VNBuildingMetadata : MonoBehaviour
    {
        public VNBuildingData data;
        public Vector3 StreetForward => transform.forward;
    }
}

