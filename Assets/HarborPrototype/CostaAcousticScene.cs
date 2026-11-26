using System.Collections.Generic;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaAcousticScene
    {
        internal enum Floor { Stone, Asphalt, Earth, Wood, Metal, Carpet }

        private readonly List<Bounds> rooms = new();
        private readonly List<(Bounds Bounds, Floor Floor)> floorFinishes = new();
        private readonly HashSet<Collider> asphalt = new();

        internal void AddAsphalt(Collider floor) => asphalt.Add(floor);
        internal void AddFloor(Bounds bounds, Floor floor) => floorFinishes.Add((bounds, floor));

        internal Floor FloorAt(Vector3 feet, CostaWorldDamage damage)
        {
            bool supported = Physics.Raycast(feet + Vector3.up * .37f, Vector3.down, out RaycastHit hit, .86f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float highest = supported ? hit.point.y - .005f : feet.y - .50f;
            Floor floor = Floor.Stone;
            if (supported)
            {
                floor = damage.Resolve(hit.collider) switch
                {
                    CostaWorldDamage.Surface.Earth => Floor.Earth,
                    CostaWorldDamage.Surface.Wood => Floor.Wood,
                    CostaWorldDamage.Surface.Metal => Floor.Metal,
                    _ => asphalt.Contains(hit.collider) ? Floor.Asphalt : Floor.Stone
                };
            }
            foreach (var finish in floorFinishes)
            {
                Bounds bounds = finish.Bounds;
                float top = bounds.max.y;
                if (top < highest || top > feet.y + .18f)
                    continue;
                if (feet.x >= bounds.min.x && feet.x <= bounds.max.x
                    && feet.z >= bounds.min.z && feet.z <= bounds.max.z)
                {
                    highest = top;
                    floor = finish.Floor;
                }
            }
            return floor;
        }

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
