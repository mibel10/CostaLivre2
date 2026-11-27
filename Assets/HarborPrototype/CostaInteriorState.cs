using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed partial class CostaInteriors
    {
        internal enum Action { Entrance, LightLeft, LightRight, Computer, Sit, Call, Travel, Service }
        internal const float LiftSpeed = 2.25f;

        [Serializable]
        internal sealed class State
        {
            public bool open;
            public int lights;
            public int computers;
            public int roomDoors;
            public float height;
            public int target;
            public bool moving;
            [NonSerialized] internal float OpenUntil;
            [NonSerialized] internal float DepartAt;
        }

        private State[] solo;
        private bool networkWorld;
        private bool transformDirty;
        internal State[] States { get; private set; }

        internal void UseLocalWorld(bool host)
        {
            if (host)
            {
                if (networkWorld)
                    return;
                solo = States;
                States = new State[buildings.Length];
                for (int i = 0; i < buildings.Length; i++)
                {
                    States[i] = new State
                    {
                        lights = (1 << (buildings[i].Plan.Levels * 2)) - 1,
                        height = buildings[i].Plan.Y(0)
                    };
                }
                networkWorld = true;
            }
            else if (networkWorld)
            {
                States = solo;
                networkWorld = false;
            }
        }

        internal void Read(State[] states)
        {
            if (!networkWorld)
            {
                solo = States;
                networkWorld = true;
            }
            States = states;
        }

        internal static bool Valid(State[] states)
        {
            if (states?.Length != CostaInteriorPlan.All.Length)
                return false;
            for (int i = 0; i < states.Length; i++)
            {
                State state = states[i];
                CostaInteriorPlan plan = CostaInteriorPlan.All[i];
                if (state == null || !float.IsFinite(state.height) || state.height < plan.Y(0) - .01f
                    || state.height > plan.Height + .01f || state.target < 0 || state.target > plan.Levels)
                    return false;
            }
            return true;
        }

        internal static int Encode(int building, Action action, int floor = 0) => (building * 16 + floor) * 8 + (int)action;
        internal static int BuildingOf(int key) => key / 128;
        private static int FloorOf(int key) => key / 8 % 16;

        internal int FindAction(Vector3 position, int seat, int lift, float hour, out string title, out int picker)
        {
            title = "";
            picker = -1;
            if (lift >= 0)
            {
                title = L("Elevator in motion", "Elevador em movimento");
                return -1;
            }
            if (seat >= 0)
            {
                title = L("Stand up", "Levantar");
                return seat;
            }
            float best = 1.14f * 1.14f;
            int selected = -1;
            string label = "";
            int cabin = -1;
            void Offer(int key, Vector3 at, string text, int liftMenu = -1, float range = 1.14f)
            {
                float distance = (position - at).sqrMagnitude;
                if (distance >= best || distance >= range*range)
                    return;
                best = distance;
                selected = key;
                label = text;
                cabin = liftMenu;
            }

            for (int i = 0; i < buildings.Length; i++)
            {
                var p = buildings[i].Plan;
                if (!p.Contains(position, 2.0f))
                    continue;
                if ((position - p.Entrance).sqrMagnitude < 3.1f &&
                    (p.Door == CostaInteriorPlan.EntranceDoor.Swing || !p.IsOpen(hour)))
                    Offer(Encode(i,Action.Entrance), p.Entrance,
                        !p.IsOpen(hour) && position.z <= p.Front ? L("Closed", "Fechado")
                            : States[i].open ? L("Door open", "Porta aberta") : L("Open door", "Abrir porta"));

                if (!p.Contains(position))
                    continue;
                int level = Mathf.RoundToInt((position.y - CostaInteriorPlan.Base) / CostaInteriorPlan.Rise);
                if (level < 0 || level > p.Levels || Mathf.Abs(position.y - p.Y(level)) > .52f)
                    continue;
                State state = States[i];
                if (p.Vertical)
                {
                    if (!state.moving && p.InCabin(position, state.height))
                    {
                        picker = i;
                        title = L("Select elevator floor", "Selecionar andar do elevador");
                        return Encode(i, Action.Travel, level);
                    }
                    Offer(Encode(i,Action.Call,level), p.Panel(level), state.moving
                        ? L("Elevator called / busy", "Elevador em movimento") : L("Call elevator", "Chamar elevador"));
                }
                if (level >= p.Levels)
                    continue;
                if (p.HasPartition(level))
                    Offer(Encode(i,Action.Entrance,level+1),p.RoomDoor(level),
                        (state.roomDoors & (1 << level)) == 0 ? L("Open room door","Abrir porta da sala")
                            : L("Close room door","Fechar porta da sala"));
                Offer(Encode(i,Action.LightLeft,level), p.Service(level,0), L("Left room lights", "Luzes da sala esquerda"));
                Offer(Encode(i,Action.LightRight,level), p.Service(level,1), L("Right room lights", "Luzes da sala direita"));
                Offer(Encode(i,Action.Sit,level), p.Chair(level) + Vector3.back * .62f, L("Sit", "Sentar"), range: .90f);
                Offer(Encode(i,Action.Computer,level), p.Computer(level), L("Use terminal", "Usar terminal"), range: .76f);
                if (level == 0 && p.Purpose is CostaInteriorPlan.Use.Hospital or CostaInteriorPlan.Use.Restaurant)
                    Offer(Encode(i,Action.Service), ServicePoint(p), p.Purpose == CostaInteriorPlan.Use.Hospital
                        ? L("Receive treatment", "Receber atendimento") : L("Rest at the café", "Fazer uma pausa no café"));
            }

            title = label;
            picker = cabin;
            return selected;
        }

        private static Vector3 ServicePoint(CostaInteriorPlan p) =>
            new(p.Entry.x+2.8f, p.Y(0), p.Front+p.Depth*.34f);

        internal bool Use(int key, float now, float hour, ref Vector3 position, ref int seat, out Action action, out int notice)
        {
            action = (Action)(key & 7);
            notice = 0;
            if (key < 0 || BuildingOf(key) >= buildings.Length)
                return false;
            int index = BuildingOf(key);
            int level = FloorOf(key);
            var p = buildings[index].Plan;
            State state = States[index];
            if (seat >= 0)
            {
                if (seat != key || action != Action.Sit || level >= p.Levels)
                    return false;
                position = p.Chair(level) + Vector3.back * .75f;
                seat = -1;
                return true;
            }
            if (level > p.Levels || !p.Contains(position, 1.95f))
                return false;
            switch (action)
            {
                case Action.Entrance:
                    if (level > 0)
                    {
                        int room = level - 1;
                        if (!p.HasPartition(room) || !Near3(position,p.RoomDoor(room),1.38f))
                            return false;
                        state.roomDoors ^= 1 << room;
                        return true;
                    }
                    if (!Near3(position, p.Entrance, 1.55f))
                        return false;
                    if (!p.IsOpen(hour) && position.z < p.Front - .1f)
                    {
                        notice = 23;
                        return false;
                    }
                    state.open = true;
                    state.OpenUntil = now + 4;
                    return true;
                case Action.Call:
                    if (!p.Vertical || !Near3(position,p.Panel(level),1.4f))
                        return false;
                    return BeginTravel(index, level, now, out notice);
                case Action.Travel:
                    if (!p.Vertical || !p.InCabin(position, state.height))
                        return false;
                    return BeginTravel(index, level, now, out notice);
                case Action.LightLeft:
                case Action.LightRight:
                    int zone = action == Action.LightLeft ? 0 : 1;
                    if (level >= p.Levels || !Near3(position,p.Service(level,zone),1.38f))
                        return false;
                    state.lights ^= 1 << (level*2+zone);
                    return true;
                case Action.Computer:
                    if (level >= p.Levels || !Near3(position,p.Computer(level),1.12f))
                        return false;
                    state.computers ^= 1 << level;
                    notice = 25 + (int)p.Purpose;
                    return true;
                case Action.Sit:
                    if (level >= p.Levels || !Near3(position,p.Chair(level)+Vector3.back*.62f,1.20f))
                        return false;
                    seat = key;
                    position = p.Chair(level);
                    return true;
                case Action.Service:
                    return level == 0 && p.IsOpen(hour) && Near3(position,ServicePoint(p),1.45f)
                        && p.Purpose is CostaInteriorPlan.Use.Restaurant or CostaInteriorPlan.Use.Hospital;
                default:
                    return false;
            }
        }

        internal static string Notice(int code) => code switch
        {
            23 => L("This venue is closed. See the hours at the entrance.", "Estabelecimento fechado. Consulte os horários na entrada."),
            24 => L("Elevator is already in motion.", "O elevador já está em movimento."),
            25 => L("Guest services terminal · rooms upstairs, terrace on the roof.",
                "Terminal de hóspedes · quartos nos andares superiores, terraço na cobertura."),
            26 => L("Stock terminal · the sales counter is on the ground floor.",
                "Terminal de estoque · atendimento no balcão do térreo."),
            27 => L("Gallery terminal · upper floor accessible by the elevator or stairs.",
                "Terminal da galeria · acesse o piso superior pelo elevador ou pelas escadas."),
            28 => L("Station terminal · return confiscated items to the station entrance.",
                "Terminal da delegacia · entregue itens recuperados na entrada."),
            29 => L("Menu · ask for service at the café counter.", "Cardápio · solicite atendimento no balcão do café."),
            30 => L("Admissions terminal · treatment is available on the ground floor.",
                "Terminal de triagem · atendimento disponível no térreo."),
            31 => L("Directory · offices upstairs and terrace access on the roof.", "Diretório · escritórios acima e terraço na cobertura."),
            32 => L("Residential directory · use the stairs or lift to reach the apartments.",
                "Diretório residencial · use as escadas ou o elevador para chegar aos apartamentos."),
            33 => L("Workshop terminal · vehicle access through the ground-floor shutter.",
                "Terminal da oficina · acesso de veículos pelo portão do térreo."),
            34 => L("Service complete. Health or stamina restored.", "Atendimento concluído. Vida ou fôlego recuperado."),
            35 => L("Service is temporarily unavailable for you.", "Atendimento temporariamente indisponível para você."),
            _ => ""
        };

        private static bool Near3(Vector3 a, Vector3 b, float radius) => Mathf.Abs(a.y-b.y) < .6f
            && (a-b).sqrMagnitude < radius*radius;

        private bool BeginTravel(int index, int floor, float now, out int notice)
        {
            notice = 0;
            State state = States[index];
            if (state.moving)
            {
                notice = 24;
                return false;
            }
            if (state.target == floor)
                return false;
            state.target = floor;
            state.DepartAt = now + .71f;
            state.moving = true;
            return true;
        }

        internal bool Boarded(int index, Vector3 position) => buildings[index].Plan.InCabin(position, States[index].height);
        internal bool Moving(int index) => States[index].moving;
        internal float Height(int index) => States[index].height;
        internal float VisualHeight(int index) => buildings[index].CabVisual;
        internal Vector3 Landing(int index, Vector3 position)
        {
            var p = buildings[index].Plan;
            int closest = Mathf.Clamp(Mathf.RoundToInt((position.y - CostaInteriorPlan.Base)/CostaInteriorPlan.Rise), 0, p.Levels);
            return p.Panel(closest);
        }

        internal void ObserveDoor(Vector3 position, float now, float hour)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                var p = buildings[i].Plan;
                State state = States[i];
                bool inside = position.z > p.Front + .10f;
                float radius = p.Door == CostaInteriorPlan.EntranceDoor.Shutter ? 6f : 2.45f;
                if (!Near3(position, p.Entrance, state.open && p.Door != CostaInteriorPlan.EntranceDoor.Shutter ? 2.1f : radius))
                    continue;
                bool auto = p.Door != CostaInteriorPlan.EntranceDoor.Swing;
                if (!state.open && !inside && (!auto || !p.IsOpen(hour)))
                    continue;
                if (state.open || auto || inside)
                {
                    state.open = true;
                    state.OpenUntil = now + 1.1f;
                }
            }
        }

        internal void Step(float now, float delta)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                State state = States[i];
                if (state.open && now >= state.OpenUntil)
                    state.open = false;
                if (!state.moving || now < state.DepartAt)
                    continue;
                float target = buildings[i].Plan.Y(state.target);
                state.height = Mathf.MoveTowards(state.height, target, LiftSpeed*delta);
                if (state.height == target)
                    state.moving = false;
            }
        }

        internal void Present(float delta, bool exact, CostaEffects effects, Vector3 listener)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                var p = b.Plan;
                State state = States[i];
                float door = Mathf.MoveTowards(b.DoorVisual, state.open ? 1 : 0, delta*2.6f);
                if (!Mathf.Approximately(door,b.DoorVisual))
                {
                    if ((b.DoorVisual == 0 || b.DoorVisual == 1) && (listener-p.Entrance).sqrMagnitude < 90)
                        effects?.PlayAt(CostaEffects.Cue.PanelOpen,p.Entrance,.20f);
                    b.DoorVisual = door;
                    float eased = Mathf.SmoothStep(0,1,door);
                    if (p.Door is CostaInteriorPlan.EntranceDoor.Swing or CostaInteriorPlan.EntranceDoor.Revolve)
                        b.Doors[0].rotation = Quaternion.Euler(0, -90*eased, 0);
                    else if (p.Door == CostaInteriorPlan.EntranceDoor.Shutter)
                        b.Doors[0].position = p.Entrance + Vector3.up*(1.29f+eased*2.63f);
                    else
                    {
                        float offset = p.DoorSpan/4*(1+eased*1.97f);
                        b.Doors[0].position = p.Entrance + new Vector3(-offset,1.29f,0);
                        b.Doors[1].position = p.Entrance + new Vector3(offset,1.29f,0);
                    }
                    transformDirty = true;
                }

                for (int f = 0; f < b.RoomDoors.Length; f++)
                {
                    Transform hinge = b.RoomDoors[f];
                    if (hinge == null)
                        continue;
                    float target = (state.roomDoors & (1 << f)) != 0 ? 1 : 0;
                    float roomDoor = Mathf.MoveTowards(b.RoomDoorVisual[f],target,delta*3.1f);
                    if (roomDoor == b.RoomDoorVisual[f])
                        continue;
                    b.RoomDoorVisual[f] = roomDoor;
                    hinge.rotation = Quaternion.Euler(0,92*Mathf.SmoothStep(0,1,roomDoor),0);
                    transformDirty = true;
                }

                if (b.Cab == null)
                    continue;
                float height = exact || Mathf.Abs(state.height-b.CabVisual) > 2*CostaInteriorPlan.Rise
                    ? state.height : Mathf.MoveTowards(b.CabVisual, state.height, delta*LiftSpeed*1.65f);
                if (b.Cab.position.y != height)
                {
                    b.CabVisual = height;
                    b.Cab.position = p.Cabin(height);
                    transformDirty = true;
                }

                int display = state.moving ? -1 : state.target;
                if (b.IndicatorFloor != display)
                {
                    b.IndicatorFloor = display;
                    string digits = display < 0 ? "↕" : display == p.Levels ? "R" : (display + 1).ToString();
                    b.RoofIndicator.text = digits;
                    foreach (RoomSet room in b.Rooms)
                        if (room.FloorNumber != null)
                            room.FloorNumber.text = digits;
                }

                if (b.DisplayFloor >= 0 && b.DisplayFloor != state.target && !state.moving
                    && (listener-b.Cab.position).sqrMagnitude < 100)
                    effects?.PlayAt(CostaEffects.Cue.Purchase,b.Cab.position,.23f);
                if (!state.moving)
                    b.DisplayFloor = state.target;
                for (int level = 0; level < b.Gates.Length; level++)
                {
                    float gate = Mathf.MoveTowards(b.GateVisual[level],
                        !state.moving && state.target == level && Mathf.Abs(p.Y(level)-height)<.04f ? 1 : 0, delta*2.9f);
                    if (gate == b.GateVisual[level])
                        continue;
                    b.GateVisual[level] = gate;
                    b.Gates[level].position = new Vector3(p.Cabin(0).x, p.Y(level)+1.11f + 2.25f*gate,p.ShaftFront);
                    transformDirty = true;
                }
            }
            if (transformDirty)
            {
                Physics.SyncTransforms();
                transformDirty = false;
            }
        }

        private TextMesh Indicator(Transform parent, CostaInteriorPlan p, int level)
        {
            var go = new GameObject("Elevator floor indicator");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(p.Cabin(0).x, p.Y(level)+2.66f,p.ShaftFront-.135f);
            var text = go.AddComponent<TextMesh>();
            text.text = "1";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 48;
            text.characterSize = .035f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(.68f,.94f,.89f);
            return text;
        }
    }
}
