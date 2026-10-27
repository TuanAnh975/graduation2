using UnityEngine;

namespace HanoiStreet
{
    // Independent one-way lanes, equal speeds and fixed headway avoid overtaking collisions.
    public sealed class StreetVehicle : MonoBehaviour
    {
        public float speed;
        public float startZ;
        public float endZ;
        public int direction = 1;
        void Update()
        {
            float length = endZ - startZ;
            if (length <= 0) return;
            Vector3 p = transform.localPosition;
            p.z = startZ + Mathf.Repeat(p.z - startZ + direction * speed * Time.deltaTime, length);
            transform.localPosition = p;
        }
    }
}
