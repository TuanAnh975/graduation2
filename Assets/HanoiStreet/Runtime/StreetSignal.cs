using UnityEngine;

namespace HanoiStreet
{
    public sealed class StreetSignal : MonoBehaviour
    {
        public Renderer[] bulbs;
        public HanoiWorldStreamer world;
        public Vector2Int node;
        public bool horizontal;
        int previous = -1;
        MaterialPropertyBlock properties;

        void Update()
        {
            if (!world) return;
            int state = WorldRoadNetwork.Signal(node, horizontal, Time.time, world.Profile);
            if (state == previous) return;
            previous = state;
            if (properties == null) properties = new MaterialPropertyBlock();
            for (int i = 0; i < bulbs.Length; i++)
            {
                Color color = i == state ? (i == 0 ? Color.red : i == 1 ? Color.yellow : Color.green) : new Color(.06f, .06f, .06f);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                bulbs[i].SetPropertyBlock(properties);
            }
        }
    }
}
