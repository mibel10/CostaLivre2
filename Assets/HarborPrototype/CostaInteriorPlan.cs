using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaInteriorPlan
    {
        internal enum Use { Hotel, Shop, Museum, Station, Restaurant, Hospital, Office, Apartment, Garage }
        internal enum EntranceDoor { Swing, Slide, Revolve, Shutter }

        internal const float Rise = 3.3f;
        internal const float Base = .035f;
        internal readonly Vector3 Entry;
        internal readonly float Width, Depth;
        internal readonly int Levels;
        internal readonly Use Purpose;
        internal readonly EntranceDoor Door;
        internal readonly Color Accent;
        internal readonly int Opens, Closes;
        internal float Front => Entry.z - 2;
        internal float Back => Front + Depth;
        internal float Left => Entry.x - Width / 2;
        internal float Right => Entry.x + Width / 2;
        internal bool Vertical => Levels > 1;
        internal float Height => Base + Levels * Rise;
        internal float StairX => Left + 1.62f;
        internal float StairStart => Front + 2.8f;
        internal float StairEnd => Front + 8.4f;
        internal float ShaftLeft => Right - 3.15f;
        internal float ShaftFront => Back - 3.95f;
        internal Vector3 Panel(int level) => new(ShaftLeft - .72f, Y(level), ShaftFront - .75f);
        internal Vector3 Cabin(float y) => new(Right - 1.68f, y, Back - 1.78f);
        internal float Y(int level) => Base + level * Rise;
        internal Vector3 Entrance => Entry + Vector3.back * 2;
        internal float DoorSpan => Door == EntranceDoor.Shutter ? 4.4f : 2.8f;
        internal bool IsOpen(float hour) => Opens == Closes || hour >= Opens && hour < Closes;

        internal bool Contains(Vector3 p, float margin = 0) => p.x > Left - margin && p.x < Right + margin
            && p.z > Front - margin && p.z < Back + margin && p.y >= -.45f && p.y < Height + 2.8f + margin;

        internal bool InCabin(Vector3 p, float y) => p.x > ShaftLeft + .18f && p.x < Right - .3f
            && p.z > ShaftFront + .2f && p.z < Back - .3f && Mathf.Abs(y - p.y) < .45f;

        internal Vector3 Service(int level, int room) =>
            new(room == 0 ? Entry.x - 1.6f : Entry.x + 1.65f, Y(level), Front + (level == 0 && HasOriginalLobby ? 1.25f : Depth * .37f));

        internal Vector3 Chair(int level) =>
            new(Entry.x - (level == 0 ? 2.15f : .4f), Y(level), Front + (level == 0 && HasOriginalLobby ? 1.38f : Depth * .73f));

        internal Vector3 Computer(int level) =>
            new(Entry.x + (level == 0 ? 1.9f : .9f), Y(level), Front + (level == 0 && HasOriginalLobby ? 1.18f : Depth * .61f));

        internal bool HasPartition(int floor) => (floor > 0 || !HasOriginalLobby)
            && Width >= 16 && Purpose != Use.Garage;

        internal Vector3 RoomDoor(int floor) => new(Entry.x+2.70f,Y(floor),Front+Depth*.52f);

        internal bool HasOriginalLobby => this != Hospital && Purpose is not (Use.Apartment or Use.Office or Use.Garage);

        private CostaInteriorPlan(Vector3 entry, float width, float depth, int levels, Use purpose, EntranceDoor door,
            Color accent, int opens = 0, int closes = 0)
        {
            Entry = entry; Width = width; Depth = depth; Levels = levels; Purpose = purpose;
            Door = door; Accent = accent; Opens = opens; Closes = closes;
        }

        internal static readonly Vector3 Cafe = new(-113, 0, 67);
        internal static readonly CostaInteriorPlan Hospital = new(new Vector3(31, 0, 52), 23, 17, 3,
            Use.Hospital, EntranceDoor.Slide, new Color(.20f,.47f,.50f));
        internal static readonly CostaInteriorPlan Office = new(new Vector3(-73, 0, CostaCity.Districts[1].Center + 9), 28, 17, 5,
            Use.Office, EntranceDoor.Slide, new Color(.32f,.44f,.59f), 7, 21);
        internal static readonly CostaInteriorPlan Apartment = new(new Vector3(12, 0, CostaCity.Districts[2].Center - 44), 11, 17, 3,
            Use.Apartment, EntranceDoor.Swing, new Color(.58f,.35f,.24f));
        internal static readonly CostaInteriorPlan Garage = new(new Vector3(28, 0, CostaCity.Districts[0].Center - 44.5f), 11, 19, 2,
            Use.Garage, EntranceDoor.Shutter, new Color(.53f,.44f,.29f), 6, 23);
        internal static readonly CostaInteriorPlan[] All =
        {
            new(CostaCity.Hotel, 25, 16, 7, Use.Hotel, EntranceDoor.Revolve, new(.43f,.28f,.24f)),
            new(CostaCity.Jeweler, 18, 14, 2, Use.Shop, EntranceDoor.Slide, new(.29f,.38f,.34f), 8, 21),
            new(CostaCity.Museum, 27, 17, 2, Use.Museum, EntranceDoor.Swing, new(.46f,.37f,.29f), 8, 20),
            new(CostaCity.Station, 23, 16, 2, Use.Station, EntranceDoor.Slide, new(.23f,.37f,.53f)),
            new(CostaCity.GunShop, 12, 11, 1, Use.Shop, EntranceDoor.Swing, new(.35f,.40f,.33f), 7, 23),
            new(CostaCity.Electronics, 19, 16, 3, Use.Shop, EntranceDoor.Slide, new(.24f,.40f,.51f), 8, 22),
            new(Cafe, 12, 7, 1, Use.Restaurant, EntranceDoor.Swing, new(.52f,.31f,.20f), 6, 23),
            Hospital, Office, Apartment, Garage
        };

        internal static CostaInteriorPlan Find(Vector3 entry)
        {
            foreach (CostaInteriorPlan plan in All)
                if (plan.Entry == entry)
                    return plan;
            return null;
        }
    }
}
