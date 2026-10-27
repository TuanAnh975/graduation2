using UnityEngine;

namespace HanoiStreet
{
    // Authored dimensions exclude balconies, awnings and roof overhangs.
    public sealed class HanoiHouse : MonoBehaviour
    {
        [Range(1, 4)] public int floors = 1;
        public Vector3 bodySize = new Vector3(4.5f, 3.4f, 8);
        public bool glassEntrance;
        public bool tiledRoof;
    }
}
