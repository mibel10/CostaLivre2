using System.Collections.Generic;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaAcousticScene
    {
        private readonly List<Bounds> rooms = new();

        internal void AddRoom(Bounds bounds) => rooms.Add(bounds);

        internal float Enclosure(Vector3 point)
        {
            foreach (Bounds room in rooms)
            {
                if (!room.Contains(point))
                    continue;
                Vector3 low = point - room.min;
                Vector3 high = room.max - point;
                float edge = Mathf.Min(Mathf.Min(low.x, high.x), Mathf.Min(low.z, high.z));
                return Mathf.SmoothStep(0, 1, edge / 1.30f);
            }
            return 0;
        }
    }
}
