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
        private static readonly Vector2 Overview = new(-37f, 47f);
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
            new(CostaCity.Vendors[1], Kind.Vendor, "Loot exchange 02", "Posto de venda 02")
        };

        private readonly IReadOnlyList<Rect> buildings;
        private Vector2 mapCenter = Overview;
        private float zoom = 1f;
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

        internal void CheckWaypoint(Vector3 position)
        {
            if (Waypoint is Vector3 point && CostaJobs.Near(position, point, 3.0f)
                && !Physics.Linecast(position + Vector3.up, point + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                ClearWaypoint();
        }

        internal void DrawMini(Rect rect, Vector3 player, float heading, Vector3? objective)
        {
            EnsureStyles();
            Fill(new Rect(rect.x - 2, rect.y - 24, rect.width + 4, rect.height + 26), Frame);
            Label(new Rect(rect.x + 5, rect.y - 23, rect.width - 11, 20), L("SANTA LUZ", "SANTA LUZ"), small, Text);
            Label(new Rect(rect.xMax - 16, rect.y - 23, 13, 20), "N", small, Route);
            GUI.BeginGroup(rect);
            var size = rect.size;
            var projection = new Projection(new Vector2(player.x, player.z), size, Mathf.Min(size.x, size.y) / 74f);
            DrawBase(size, projection);
            for (int i = 0; i < Points.Length; i++)
            {
                Pin pin = Points[i];
                Vector2 point = projection.Point(pin.Position);
                if (Inside(point, size, 7))
                    PinDot(point, ColorFor(pin.Type), pin.Type == Kind.Shop ? 7 : 5);
            }
            Vector3? goal = Waypoint ?? objective;
            if (goal is Vector3 location)
            {
                Vector2 destination = projection.Point(location);
                Vector2 edge = Constrain(destination, size, 9);
                DrawGuide(size * 0.5f, edge, Waypoint.HasValue ? Route : Accent);
                PinDot(edge, Waypoint.HasValue ? Route : Accent, 8);
            }
            PlayerArrow(size * 0.5f, heading);
            GUI.EndGroup();
        }

        internal void DrawFull(Vector3 player, float heading, Vector3? objective, System.Action close)
        {
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 900f, Screen.height / 540f, 1.7f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, width, height), new Color(0.035f, 0.054f, 0.061f, 0.98f));
            Fill(new Rect(0, 0, width, 3), Accent);
            Label(new Rect(28, 13, 490, 36), L("Santa Luz  /  City map", "Santa Luz  /  Mapa da cidade"), title, Text);
            if (Button(new Rect(width - 132, 18, 105, 29), L("Close", "Fechar"), false))
                close();

            Rect board = new(27, 62, width - 299, height - 104);
            Rect legend = new(board.xMax + 17, 62, 254, board.height);
            Fill(legend, Frame);
            GUI.BeginGroup(board);
            Vector2 size = board.size;
            float pixelsPerMeter = Mathf.Min(size.x, size.y) / 257f * zoom;
            var projection = new Projection(mapCenter, size, pixelsPerMeter);
            Event input = Event.current;
            bool onMap = new Rect(Vector2.zero, size).Contains(input.mousePosition);
            if (onMap && input.type == EventType.ScrollWheel)
            {
                Vector3 before = projection.World(input.mousePosition);
                zoom = Mathf.Clamp(zoom * Mathf.Exp(-input.delta.y * 0.08f), 0.90f, 3.8f);
                pixelsPerMeter = Mathf.Min(size.x, size.y) / 257f * zoom;
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
            DrawBase(size, projection);
            if (objective is Vector3 tracked)
                PinDot(projection.Point(tracked), Accent, 9);
            if (Waypoint is Vector3 pinned)
            {
                DrawGuide(projection.Point(player), projection.Point(pinned), Route);
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
                if (zoom > 1.55f || hit.Contains(input.mousePosition) || selected == i)
                    Label(new Rect(center.x + 8, center.y - 8, 168, 16), pin.Title, markerText, Text);
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
            if (Waypoint.HasValue)
                Label(new Rect(lx, ly + 238, legend.width - 30, 23), WaypointTitle, small, Route);
            else
                Label(new Rect(lx, ly + 238, legend.width - 30, 23), L("Click to set a waypoint", "Clique para marcar"), small, Muted);
            if (Button(new Rect(lx, legend.yMax - 94, 41, 31), "−", false))
                zoom = Mathf.Max(0.90f, zoom / 1.25f);
            if (Button(new Rect(lx + 48, legend.yMax - 94, 41, 31), "+", false))
                zoom = Mathf.Min(3.8f, zoom * 1.25f);
            if (Button(new Rect(lx + 96, legend.yMax - 94, 123, 31), L("Center player", "Centralizar"), false))
            {
                mapCenter = new Vector2(player.x, player.z);
                ClampCenter();
            }
            if (Button(new Rect(lx, legend.yMax - 48, 219, 31), L("Clear waypoint", "Limpar marcador"), false))
                ClearWaypoint();

            Label(new Rect(27, height - 36, width - 54, 24),
                L("Scroll: zoom    Right drag: pan    Click: waypoint    M / ESC: close",
                    "Roda do mouse: zoom    Arrastar com o botão direito: mover    Clique: marcar    M / ESC: fechar"), small, Muted);
            GUI.matrix = previous;
        }

        private static bool OnLand(Vector3 position)
        {
            return position.x >= -152 && position.z >= -64 && position.z <= 168
                && position.x <= (position.z >= 22 ? 78 : 25);
        }

        private void DrawBase(Vector2 size, Projection p)
        {
            Fill(new Rect(Vector2.zero, size), Water);
            Fill(p.Area(new Rect(-152, 22, 230, 146)), Land);
            Fill(p.Area(new Rect(-152, -64, 177, 86)), Land);
            Fill(p.Area(new Rect(26, 97, 40, 47)), new Color(0.21f, 0.28f, 0.24f));
            for (int x = -150; x <= 70; x += 20)
                Fill(p.Area(new Rect(x, 22, 0.28f, 146)), Grid);
            for (int z = -60; z < 168; z += 20)
                Fill(p.Area(new Rect(-151, z, z < 22 ? 176 : 228, 0.28f)), Grid);

            foreach (float x in CostaCity.Avenues)
                Fill(p.Area(new Rect(x - 2.65f, 21, 5.3f, 145)), Street);
            foreach (float z in CostaCity.CrossStreets)
                Fill(p.Area(new Rect(-151, z - 2.35f, 228, 4.7f)), Street);
            Fill(p.Area(new Rect(-1.55f, -21, 3.1f, 45)), Street);
            Fill(p.Area(new Rect(-28, -1.55f, 51, 3.1f)), Street);

            foreach (Rect building in buildings)
                Fill(p.Area(building), Buildings);
            foreach (Vector2 center in HarborBlockout.WarehouseCenters)
                Fill(p.Area(new Rect(center.x - 5.25f, center.y - 4.25f, 10.5f, 8.5f)), Buildings);
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

        private static void DrawGuide(Vector2 start, Vector2 end, Color color)
        {
            float distance = Vector2.Distance(start, end);
            int dots = Mathf.Clamp(Mathf.FloorToInt(distance / 7), 1, 90);
            for (int i = 1; i < dots; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)dots);
                Fill(new Rect(point.x - 1, point.y - 1, 2, 2), color);
            }
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
            foreach (int wing in new[] { -1, 1 })
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
            mapCenter.y = Mathf.Clamp(mapCenter.y, -64, 168);
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
            return GUI.Button(area, text, button);
        }

        private static void Label(Rect area, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(area, value, style);
        }

        private static void Fill(Rect area, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
