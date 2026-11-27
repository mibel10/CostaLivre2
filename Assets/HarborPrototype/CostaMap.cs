using System.Collections.Generic;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaMap
    {
        private enum Kind { Landmark, Shop, Robbery, Home, Safehouse, Vendor }

        private readonly struct Pin
        {
            internal readonly Vector3 Position;
            internal readonly Kind Type;
            private readonly string en;
            private readonly string pt;

            internal string Title => L(en, pt);

            internal Pin(Vector3 position, Kind kind, string en, string pt)
            {
                Position = position;
                Type = kind;
                this.en = en;
                this.pt = pt;
            }
        }

        private readonly struct Projection
        {
            private readonly Vector2 center;
            private readonly Vector2 origin;
            private readonly float scale;

            internal Projection(Vector2 center, Vector2 size, float scale)
            {
                this.center = center;
                origin = size * 0.5f;
                this.scale = scale;
            }

            internal Vector2 Point(float x, float z) => origin + new Vector2(x - center.x, center.y - z) * scale;
            internal Vector2 Point(Vector3 point) => Point(point.x, point.z);
            internal Vector3 World(Vector2 point)
            {
                Vector2 relative = (point - origin) / scale;
                return new Vector3(center.x + relative.x, 0, center.y - relative.y);
            }

            internal Rect Area(Rect world)
            {
                Vector2 corner = Point(world.xMin, world.yMax);
                return new Rect(corner.x, corner.y, world.width * scale, world.height * scale);
            }
        }

        private static readonly Color Water = new(0.09f, 0.15f, 0.19f);
        private static readonly Color Land = new(0.16f, 0.21f, 0.23f);
        private static readonly Color Grid = new(0.23f, 0.30f, 0.32f, 0.55f);
        private static readonly Color Street = new(0.31f, 0.37f, 0.38f);
        private static readonly Color Buildings = new(0.23f, 0.28f, 0.28f);
        private static readonly Color Text = new(0.89f, 0.91f, 0.87f);
        private static readonly Color Muted = new(0.67f, 0.73f, 0.72f);
        private static readonly Color Frame = new(0.055f, 0.077f, 0.088f, 0.95f);
        private static readonly Color Accent = new(0.92f, 0.66f, 0.39f);
        private static readonly Color Route = new(0.40f, 0.86f, 0.86f);
        private static readonly Vector2 Overview = new(-37f, 361f);
        private static readonly Pin[] Points =
        {
            new(CostaCity.Hotel, Kind.Robbery, "Grand Hotel", "Grand Hotel"),
            new(CostaCity.Jeweler, Kind.Robbery, "Áurea Jeweler", "Joalheria Áurea"),
            new(CostaCity.Museum, Kind.Robbery, "Santa Luz Museum", "Museu de Santa Luz"),
            new(CostaCity.Electronics, Kind.Robbery, "Electronics store", "Loja de informática"),
            new(CostaCity.GunCounter, Kind.Shop, "Gun shop", "Loja de armas"),
            new(CostaCity.Station, Kind.Landmark, "Police station", "Delegacia"),
            new(CostaCity.ParkGate, Kind.Landmark, "Mirante Park", "Parque Mirante"),
            new(HarborBlockout.Safehouse, Kind.Safehouse, "Safehouse", "Esconderijo"),
            new(CostaCity.Estate[0], Kind.Home, "Vila 01", "Vila 01"),
            new(CostaCity.Estate[1], Kind.Home, "Vila 02", "Vila 02"),
            new(CostaCity.Estate[2], Kind.Home, "Vila 03", "Vila 03"),
            new(CostaCity.Vendors[0], Kind.Vendor, "Loot exchange 01", "Posto de venda 01"),
            new(CostaCity.Vendors[1], Kind.Vendor, "Loot exchange 02", "Posto de venda 02"),
            new(CostaInteriorPlan.Cafe + Vector3.back*2, Kind.Landmark, "Praça Café", "Café da Praça"),
            new(CostaInteriorPlan.Hospital.Entrance, Kind.Landmark, "Coastal Hospital", "Hospital Costeiro"),
            new(CostaInteriorPlan.Office.Entrance, Kind.Landmark, "Technology Centre", "Centro Tecnológico"),
            new(CostaInteriorPlan.Apartment.Entrance, Kind.Home, "Mirante Apartments", "Apartamentos Mirante"),
            new(CostaInteriorPlan.Garage.Entrance, Kind.Landmark, "Quay Garage", "Garagem do Cais"),
            new(new Vector3(CostaCity.HighwayX, 0, CostaCity.Districts[0].Center), Kind.Landmark, "Calado City", "Calado City"),
            new(new Vector3(CostaCity.HighwayX, 0, CostaCity.Districts[1].Center), Kind.Landmark, "Antutu City", "Antutu City"),
            new(new Vector3(CostaCity.HighwayX, 0, CostaCity.Districts[2].Center), Kind.Landmark, "Vice City", "Vice City"),
            new(new Vector3(-42f, 0f, 35.4f), Kind.Shop, "Santa Luz Market", "Empório Santa Luz"),
            new(new Vector3(-69f, 0f, CostaCity.Districts[0].Center - 17.6f), Kind.Shop, "Calado Diner", "Restaurante Calado"),
            new(new Vector3(31f, 0f, 102f), Kind.Landmark, "Iron & Sea Gym", "Academia Ferro & Mar"),
            new(new Vector3(60f, 0f, 108f), Kind.Landmark, "Basketball Court", "Quadra de Basquete"),
            new(new Vector3(24.2f, 0f, 16f), Kind.Landmark, "Quay Fishing", "Pesca no Cais"),
            new(new Vector3(-20f, 0f, CostaCity.Districts[0].Center - 8.2f), Kind.Landmark, "Drift Trial", "Desafio de Drift"),
            new(new Vector3(-42f, 0f, CostaCity.Districts[1].Center - 8.2f), Kind.Landmark, "Street Race", "Corrida de Rua"),
            new(new Vector3(18f, 0f, CostaCity.Districts[1].Center + 13f), Kind.Shop, "Antutu Tuners", "Tunagem Antutu"),
            new(new Vector3(14f, 0f, CostaCity.Districts[2].Center + 12f), Kind.Shop, "Neon Arcade", "Arcade Neon"),
            new(new Vector3(-72f, 0f, CostaCity.Districts[2].Center - 14f), Kind.Landmark, "Vice Nights Club", "Boate Vice Nights")
        };

        private readonly IReadOnlyList<Rect> buildings;
        private Texture2D mapBase;
        private Vector2 mapCenter = Overview;
        private float zoom = 1f;
        private float miniHeading;
        private bool headingReady;
        private readonly Vector3[] route = new Vector3[9];
        private int selected = -1;
        private GUIStyle title;
        private GUIStyle small;
        private GUIStyle markerText;
        private GUIStyle button;

        internal Vector3? Waypoint { get; private set; }
        internal string WaypointTitle => selected >= 0 ? Points[selected].Title : L("Waypoint", "Marcador");

        internal CostaMap(IReadOnlyList<Rect> buildings)
        {
            this.buildings = buildings;
        }

        private static readonly Rect MapBounds = Rect.MinMaxRect(-154, -68, 80, CostaCity.HighwayEnd + 4);

        internal void Prepare()
        {
            const int width = 512, height = 1536;
            var pixels = new Color32[width * height];
            System.Array.Fill(pixels, (Color32)Water);
            DrawMapGeometry((area, color) =>
            {
                int x0 = Mathf.Clamp(Mathf.RoundToInt((area.xMin - MapBounds.xMin) / MapBounds.width * width), 0, width);
                int x1 = Mathf.Clamp(Mathf.RoundToInt((area.xMax - MapBounds.xMin) / MapBounds.width * width), 0, width);
                int y0 = Mathf.Clamp(Mathf.RoundToInt((area.yMin - MapBounds.yMin) / MapBounds.height * height), 0, height);
                int y1 = Mathf.Clamp(Mathf.RoundToInt((area.yMax - MapBounds.yMin) / MapBounds.height * height), 0, height);
                if (x0 == x1 || y0 == y1)
                    return;
                Color32 paint = color;
                for (int y = y0; y < y1; y++)
                {
                    int index = y * width + x0;
                    if (paint.a == 255)
                        System.Array.Fill(pixels, paint, index, x1 - x0);
                    else
                        for (int x = x0; x < x1; x++, index++)
                            pixels[index] = Color32.Lerp(pixels[index], new Color32(paint.r, paint.g, paint.b, 255), color.a);
                }
            });
            mapBase = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Costa Livre minimap geometry", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            mapBase.SetPixels32(pixels);
            mapBase.Apply(false, true);
        }

        internal void Release() => Object.Destroy(mapBase);

        internal void CheckWaypoint(Vector3 position)
        {
            if (Waypoint is Vector3 point && CostaJobs.Near(position, point, 3.0f)
                && !Physics.Linecast(position + Vector3.up, point + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                ClearWaypoint();
        }

        internal bool[] Alarms { get; set; }
        internal Vector3? EventPoint { get; set; }
        internal string EventTitle { get; set; }
        internal Vector3? PoliceSearchCenter { get; set; }
        private static readonly Color EventColor = new(.80f, .55f, 1f);
        private static readonly Color AlarmColor = new(1f, .28f, .22f);

        internal void DrawMini(Rect rect, Vector3 player, float heading, Vector3? objective)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            EnsureStyles();
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            miniHeading = headingReady ? Mathf.LerpAngle(miniHeading, heading, blend) : heading;
            headingReady = true;
            Fill(new Rect(rect.x - 1, rect.y - 19, rect.width + 2, rect.height + 20), Frame);
            Label(new Rect(rect.x + 5, rect.y - 18, rect.width - 10, 17), CostaCity.RegionName(player), markerText, Text);
            GUI.BeginGroup(rect);
            Vector2 size = rect.size;
            Vector2 center = size * .5f;
            var projection = new Projection(new Vector2(player.x, player.z), size, Mathf.Min(size.x, size.y) / 82f);
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = before
                * Matrix4x4.TRS(center, Quaternion.Euler(0, 0, -miniHeading), Vector3.one)
                * Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
            Fill(new Rect(-size.x, -size.y, size.x * 3, size.y * 3), Water);
            GUI.DrawTexture(projection.Area(MapBounds), mapBase);
            Vector3? goal = Waypoint ?? objective;
            if (goal is Vector3 location)
                DrawRoute(player, location, projection, Waypoint.HasValue ? Route : Accent);
            GUI.matrix = before;

            float angle = -miniHeading * Mathf.Deg2Rad;
            float sine = Mathf.Sin(angle), cosine = Mathf.Cos(angle);
            for (int i = 0; i < Points.Length; i++)
            {
                Pin pin = Points[i];
                Vector2 point = Rotated(projection.Point(pin.Position), center, sine, cosine);
                if (Inside(point, size, 7))
                {
                    PinDot(point, ColorFor(pin.Type), pin.Type == Kind.Shop ? 7 : 5);
                    DrawAltitude(point, pin.Position.y - player.y, ColorFor(pin.Type));
                }
            }
            if (goal is Vector3 target)
            {
                Vector2 destination = Constrain(Rotated(projection.Point(target), center, sine, cosine), size, 9);
                Color color = Waypoint.HasValue ? Route : Accent;
                PinDot(destination, color, 8);
                DrawAltitude(destination, target.y - player.y, color);
            }
            if (EventPoint is Vector3 eventPoint)
            {
                Vector2 mark = Rotated(projection.Point(eventPoint), center, sine, cosine);
                PinDot(Constrain(mark, size, 10), EventColor, 10);
            }
            if (PoliceSearchCenter is Vector3 searchCenter)
            {
                Vector2 searchPt = Rotated(projection.Point(searchCenter), center, sine, cosine);
                PinDot(Constrain(searchPt, size, 10), new Color(0.28f, 0.56f, 0.98f, 0.45f), 22);
            }
            for (int i = 0; Alarms != null && i < Mathf.Min(Alarms.Length, CostaRobberies.Sites.Length); i++)
            {
                if (!Alarms[i])
                    continue;
                Vector2 alarm = Rotated(projection.Point(CostaRobberies.Sites[i].Point), center, sine, cosine);
                DrawAlarm(Constrain(alarm, size, 10));
            }
            Vector2 north = Constrain(Rotated(center + Vector2.up * -size.y, center, sine, cosine), size, 9);
            Label(new Rect(north.x - 5, north.y - 8, 14, 16), "N", markerText, Text);
            PlayerArrow(center, 0);
            GUI.EndGroup();
        }

        private void DrawAlarm(Vector2 position)
        {
            PinDot(position, AlarmColor, 14);
            Label(new Rect(position.x - 3, position.y - 8, 9, 16), "!", markerText, Frame);
        }

        private static Vector2 Rotated(Vector2 p, Vector2 center, float s, float c)
        {
            Vector2 d = p - center;
            return center + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        private void DrawAltitude(Vector2 p, float relative, Color color)
        {
            if (Mathf.Abs(relative) < 1.35f)
                return;
            Label(new Rect(p.x + 4, p.y - 11, 15, 15), relative > 0 ? "↑" : "↓", markerText, color);
        }

        internal void DrawFull(Vector3 player, float heading, Vector3? objective, System.Action close, System.Action journal = null)
        {
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 900f, Screen.height / 540f, 1.7f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, width, height), new Color(0.035f, 0.054f, 0.061f, 0.98f));
            Fill(new Rect(0, 0, width, 3), Accent);
            Label(new Rect(28, 13, 490, 36), L("Coastal Region  /  Map", "Região Costeira  /  Mapa"), title, Text);
            if (Button(new Rect(width - 132, 18, 105, 29), L("Close", "Fechar"), false))
                close();

            if (journal != null && Button(new Rect(width-250,18,105,29),L("Journal","Diário"),false))
                journal();

            Rect board = new(27, 62, width - 299, height - 104);
            Rect legend = new(board.xMax + 17, 62, 254, board.height);
            Fill(legend, Frame);
            GUI.BeginGroup(board);
            Vector2 size = board.size;
            float pixelsPerMeter = Mathf.Min(size.x / 245f, size.y / 900f) * zoom;
            var projection = new Projection(mapCenter, size, pixelsPerMeter);
            Event input = Event.current;
            bool onMap = new Rect(Vector2.zero, size).Contains(input.mousePosition);
            if (onMap && input.type == EventType.ScrollWheel)
            {
                Vector3 before = projection.World(input.mousePosition);
                zoom = Mathf.Clamp(zoom * Mathf.Exp(-input.delta.y * 0.08f), 0.90f, 12f);
                pixelsPerMeter = Mathf.Min(size.x / 245f, size.y / 900f) * zoom;
                var updated = new Projection(mapCenter, size, pixelsPerMeter);
                Vector3 after = updated.World(input.mousePosition);
                mapCenter += new Vector2(before.x - after.x, before.z - after.z);
                ClampCenter();
                input.Use();
            }
            else if (onMap && input.type == EventType.MouseDrag && input.button == 1)
            {
                mapCenter += new Vector2(-input.delta.x, input.delta.y) / pixelsPerMeter;
                ClampCenter();
                input.Use();
            }
            projection = new Projection(mapCenter, size, pixelsPerMeter);
            if (onMap && input.type == EventType.MouseDown && input.button == 0)
            {
                PickDestination(projection, input.mousePosition, size);
                input.Use();
            }
            if (CostaUi.Pick(new Rect(Vector2.zero, size)))
                PickDestination(projection, CostaUi.Position, size);
            DrawBase(size, projection);
            if (objective is Vector3 tracked)
                PinDot(projection.Point(tracked), Accent, 9);
            if (Waypoint is Vector3 pinned)
            {
                DrawRoute(player, pinned, projection, Route);
                PinDot(projection.Point(pinned), Route, 11);
            }
            for (int i = 0; i < Points.Length; i++)
            {
                Pin pin = Points[i];
                Vector2 center = projection.Point(pin.Position);
                if (!Inside(center, size, 7))
                    continue;
                Rect hit = new(center.x - 10, center.y - 10, 20, 20);
                PinDot(center, ColorFor(pin.Type), selected == i || hit.Contains(input.mousePosition) ? 11 : 8);
                if (i >= Points.Length - CostaCity.Districts.Length || zoom > 4.7f || hit.Contains(input.mousePosition) || selected == i)
                    Label(new Rect(center.x + 8, center.y - 8, 168, 16), pin.Title, markerText, Text);
            }
            if (EventPoint is Vector3 eventPoint)
            {
                Vector2 mark = projection.Point(eventPoint);
                if (Inside(mark, size, 9))
                {
                    PinDot(mark, EventColor, 10);
                    Label(new Rect(mark.x + 9, mark.y - 20, 180, 16), EventTitle, markerText, EventColor);
                }
            }
            for (int i = 0; Alarms != null && i < Mathf.Min(Alarms.Length, CostaRobberies.Sites.Length); i++)
            {
                if (!Alarms[i])
                    continue;
                Vector2 alarm = projection.Point(CostaRobberies.Sites[i].Point);
                if (!Inside(alarm, size, 9))
                    continue;
                DrawAlarm(alarm);
                Label(new Rect(alarm.x + 11, alarm.y - 25, 185, 16), CostaRobberies.Sites[i].Name, markerText, AlarmColor);
            }
            PlayerArrow(projection.Point(player), heading);
            Label(new Rect(11, 8, 54, 24), "N ↑", small, Text);
            GUI.EndGroup();

            float lx = legend.x + 15;
            float ly = legend.y + 9;
            Label(new Rect(lx, ly, 222, 28), L("LEGEND", "LEGENDA"), small, Accent);
            DrawLegend(lx, ly + 39, ColorFor(Kind.Shop), L("Gun shop", "Loja de armas"));
            DrawLegend(lx, ly + 66, ColorFor(Kind.Robbery), L("Robbery location", "Local de roubo"));
            DrawLegend(lx, ly + 93, ColorFor(Kind.Home), L("Property", "Imóvel"));
            DrawLegend(lx, ly + 120, ColorFor(Kind.Landmark), L("Landmark", "Ponto de interesse"));
            DrawLegend(lx, ly + 147, ColorFor(Kind.Safehouse), L("Safehouse", "Esconderijo"));
            DrawLegend(lx, ly + 174, Route, L("Your waypoint", "Seu marcador"));
            DrawLegend(lx, ly + 201, ColorFor(Kind.Vendor), L("Loot exchange", "Posto de venda"));
            DrawLegend(lx, ly + 223, AlarmColor, L("Robbery in progress", "Roubo em andamento"));
            if (Waypoint.HasValue)
                Label(new Rect(lx, ly + 248, legend.width - 30, 23), WaypointTitle, small, Route);
            else
                Label(new Rect(lx, ly + 248, legend.width - 30, 23), L("Click to set a waypoint", "Clique para marcar"), small, Muted);
            if (Button(new Rect(lx, legend.yMax - 94, 41, 31), "−", false))
                zoom = Mathf.Max(0.90f, zoom / 1.25f);
            if (Button(new Rect(lx + 48, legend.yMax - 94, 41, 31), "+", false))
                zoom = Mathf.Min(12f, zoom * 1.25f);
            if (Button(new Rect(lx + 96, legend.yMax - 94, 123, 31), L("Center player", "Centralizar"), false))
            {
                mapCenter = new Vector2(player.x, player.z);
                ClampCenter();
            }
            if (Waypoint.HasValue && Button(new Rect(lx, legend.yMax - 48, 219, 31), L("Clear waypoint", "Limpar marcador"), false))
                ClearWaypoint();

            if (!CostaTouch.Available)
                Label(new Rect(27, height - 36, width - 54, 24),
                    L("Right-drag to pan · Scroll to zoom", "Arraste com o botão direito para mover · Role para aproximar"), small, Muted);
            GUI.matrix = previous;
        }

        private static bool OnLand(Vector3 position)
        {
            return CostaCity.OnRegionalLand(position) || (position.x >= -152 && position.z >= -64 && position.z <= 168
                && position.x <= (position.z >= 22 ? 78 : 25));
        }

        private void DrawBase(Vector2 size, Projection p)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Fill(new Rect(Vector2.zero, size), Water);
            GUI.DrawTexture(p.Area(MapBounds), mapBase);
        }

        private void DrawMapGeometry(System.Action<Rect, Color> paint)
        {
            paint(new Rect(-152, 22, 230, 146), Land);
            paint(new Rect(-152, -64, 177, 86), Land);
            paint(new Rect(26, 97, 40, 47), new Color(0.21f, 0.28f, 0.24f));
            for (int x = -150; x <= 70; x += 20)
                paint(new Rect(x, 22, 0.46f, 146), Grid);
            for (int z = -60; z < 168; z += 20)
                paint(new Rect(-151, z, z < 22 ? 176 : 228, 0.55f), Grid);

            foreach (float x in CostaCity.Avenues)
                paint(new Rect(x - 2.65f, 21, 5.3f, 145), Street);
            foreach (float z in CostaCity.CrossStreets)
                paint(new Rect(-151, z - 2.35f, 228, 4.7f), Street);
            paint(new Rect(-1.55f, -21, 3.1f, 45), Street);
            paint(new Rect(-28, -1.55f, 51, 3.1f), Street);

            foreach (CostaCity.District district in CostaCity.Districts)
            {
                paint(district.Area, Land);
                paint(new Rect(district.Area.xMin, district.Center - 4, district.Area.width, 8), Street);
            }
            paint(CostaCity.HighwayArea, Street);

            foreach (Rect building in buildings)
                paint(building, Buildings);
            foreach (Vector2 center in HarborBlockout.WarehouseCenters)
                paint(new Rect(center.x - 5.25f, center.y - 4.25f, 10.5f, 8.5f), Buildings);
        }

        private static Color ColorFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Vendor: return new Color(0.96f, 0.75f, 0.30f);
                case Kind.Shop: return new Color(0.76f, 0.68f, 0.94f);
                case Kind.Robbery: return new Color(0.92f, 0.44f, 0.34f);
                case Kind.Home: return new Color(0.59f, 0.76f, 0.92f);
                case Kind.Safehouse: return new Color(0.60f, 0.82f, 0.56f);
                default: return new Color(0.78f, 0.81f, 0.72f);
            }
        }

        private void DrawRoute(Vector3 from, Vector3 to, Projection projection, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            int count = 0;
            route[count++] = from;
            bool regionalFrom = from.z >= CostaCity.HighwayStart;
            bool regionalTo = to.z >= CostaCity.HighwayStart;
            Vector3 a = regionalFrom ? new Vector3(CostaCity.HighwayX, 0, from.z) : SnapStreet(from);
            Vector3 b = regionalTo ? new Vector3(CostaCity.HighwayX, 0, to.z) : SnapStreet(to);
            route[count++] = a;
            if (regionalFrom && regionalTo)
                route[count++] = b;
            else
            {
                Vector3 junction = new(CostaCity.HighwayX, 0, 123);
                if (regionalFrom)
                {
                    route[count++] = junction;
                    a = junction;
                }
                if (regionalTo)
                    b = junction;
                float x = Nearest(a.x, CostaCity.Avenues);
                float z = Nearest(b.z, CostaCity.CrossStreets);
                bool aVertical = Mathf.Abs(a.x - x) < .05f;
                bool bHorizontal = Mathf.Abs(b.z - z) < .05f;
                if (!aVertical)
                    route[count++] = new Vector3(x, 0, a.z);
                route[count++] = new Vector3(x, 0, z);
                if (!bHorizontal)
                    route[count++] = new Vector3(b.x, 0, z);
                route[count++] = b;
                if (regionalTo)
                    route[count++] = new Vector3(CostaCity.HighwayX, 0, to.z);
            }
            route[count++] = to;
            for (int i = 1; i < count; i++)
            {
                Vector2 start = projection.Point(route[i - 1]);
                Vector2 end = projection.Point(route[i]);
                RoadLine(start, end, 5, new Color(.05f,.12f,.15f,.9f));
                RoadLine(start, end, 2.6f, color);
            }
        }

        private static Vector3 SnapStreet(Vector3 position)
        {
            float x = Nearest(position.x, CostaCity.Avenues);
            float z = Nearest(position.z, CostaCity.CrossStreets);
            if (position.z < 23)
                return new Vector3(x, 0, z);
            return Mathf.Abs(position.x - x) < Mathf.Abs(position.z - z)
                ? new Vector3(x, 0, position.z) : new Vector3(position.x, 0, z);
        }

        private static float Nearest(float value, float[] choices)
        {
            float best = choices[0];
            foreach (float candidate in choices)
                if (Mathf.Abs(candidate - value) < Mathf.Abs(best - value))
                    best = candidate;
            return best;
        }

        private static void RoadLine(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 1)
                return;
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            Fill(new Rect(from.x, from.y - width * .5f, delta.magnitude + .35f, width), color);
            GUI.matrix = matrix;
        }

        private void DrawLegend(float x, float y, Color color, string text)
        {
            PinDot(new Vector2(x + 7, y + 9), color, 8);
            Label(new Rect(x + 22, y - 1, 219, 22), text, small, Text);
        }

        private void PickDestination(Projection projection, Vector2 mouse, Vector2 size)
        {
            int pinIndex = -1;
            float closest = 13 * 13;
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 pin = projection.Point(Points[i].Position);
                float distance = (pin - mouse).sqrMagnitude;
                if (distance < closest && Inside(pin, size, 7))
                {
                    closest = distance;
                    pinIndex = i;
                }
            }

            if (pinIndex >= 0)
            {
                selected = pinIndex;
                Waypoint = Points[pinIndex].Position;
                return;
            }

            Vector3 destination = projection.World(mouse);
            if (OnLand(destination) && !Physics.CheckCapsule(destination + Vector3.up * 0.4f,
                destination + Vector3.up * 1.4f, 0.32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                selected = -1;
                Waypoint = destination;
            }
        }

        private static void PinDot(Vector2 position, Color color, float size)
        {
            int radius = Mathf.RoundToInt(size * 0.5f);
            for (int row = -radius; row <= radius; row++)
            {
                int length = (radius - Mathf.Abs(row)) * 2 + 1;
                Fill(new Rect(Mathf.Round(position.x) - length / 2f, Mathf.Round(position.y) + row, length, 1), color);
            }
        }

        private static void PlayerArrow(Vector2 position, float angle)
        {
            float r = angle * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Sin(r), -Mathf.Cos(r));
            Vector2 side = new(-direction.y, direction.x);
            Fill(new Rect(position.x - 2.5f, position.y - 2.5f, 5, 5), Text);
            for (int step = 1; step < 7; step++)
            {
                Vector2 tip = position + direction * step;
                Fill(new Rect(tip.x - 1f, tip.y - 1f, 2, 2), Text);
            }
            for (int wing = -1; wing <= 1; wing += 2)
            {
                for (int step = 1; step <= 3; step++)
                {
                    Vector2 point = position + direction * (6 - step) + side * (step * wing);
                    Fill(new Rect(point.x - 1, point.y - 1, 2, 2), Text);
                }
            }
        }

        private static bool Inside(Vector2 p, Vector2 size, float padding)
        {
            return p.x > padding && p.y > padding && p.x < size.x - padding && p.y < size.y - padding;
        }

        private static Vector2 Constrain(Vector2 point, Vector2 size, float padding)
        {
            Vector2 local = point - size / 2;
            float ratio = Mathf.Max(Mathf.Abs(local.x) / (size.x / 2 - padding),
                Mathf.Abs(local.y) / (size.y / 2 - padding));
            return size / 2 + local / Mathf.Max(1, ratio);
        }

        private void ClampCenter()
        {
            mapCenter.x = Mathf.Clamp(mapCenter.x, -151, 77);
            mapCenter.y = Mathf.Clamp(mapCenter.y, -64, CostaCity.HighwayEnd);
        }

        private void ClearWaypoint()
        {
            Waypoint = null;
            selected = -1;
        }

        private void EnsureStyles()
        {
            if (title != null)
                return;
            title = Style(24, FontStyle.Bold);
            small = Style(13, FontStyle.Bold);
            markerText = Style(11, FontStyle.Normal);
            button = Style(13, FontStyle.Bold);
            button.alignment = TextAnchor.MiddleCenter;
        }

        private static GUIStyle Style(int size, FontStyle weight)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = weight, alignment = TextAnchor.MiddleLeft,
                wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset()
            };
        }

        private bool Button(Rect area, string text, bool selected)
        {
            Fill(area, selected ? Accent : new Color(0.18f, 0.24f, 0.26f));
            button.normal.textColor = selected ? Frame : Text;
            return CostaUi.Button(area, text, button);
        }

        private static void Label(Rect area, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(area, value, style);
        }

        private static void Fill(Rect area, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
