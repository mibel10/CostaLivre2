using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed partial class CostaSession : IDisposable
    {
        internal const int DefaultPort = 7777;
        internal const int MaxPlayers = 16;
        private const int Protocol = 15;
        private const float Interval = 0.083f;
        internal const int PoliceArmor = 50;
        internal const int MaxWanted = 6;
        internal const float HeatDecayTime = 27f;
        private const float ArrestTime = 1.25f;
        private const int PoliceKit = (1 << 0) | (1 << 3);
        internal static readonly Vector3 CriminalSpawn = CostaCity.CitySpawn + Vector3.left * 2.8f;
        internal static readonly Vector3 PoliceSpawn = CostaCity.Station + new Vector3(0, 0, -4.1f);
        private static readonly Vector3 Holding = CostaCity.Station + new Vector3(-5.35f, 0, 8);

        [Serializable]
        internal sealed class Avatar
        {
            public int id;
            public string name;
            public Vector3 pos;
            public float yaw;
            public bool police;
            public bool crouched;
            public bool standing = true;
            public bool playing;
            public bool moving;
            public bool running;
            public bool aiming;
            public float aimPitch;
            public float jumpPose = -1;
            public float rollPose = -1;
            public float landingCompression;
            public int gesture;
            public float gestureProgress;
            public float impactStrength;
            public float reloadProgress = -1;
            public Vector3 gestureTarget;
            public int equipped = HarborArsenal.Knife;
            public int shotNumber;
            public int shotWeapon = -1;
            public int health = 100;
            public int armor;
            public int wantedLevel;
            public bool carryingLoot;
            public float arrestProgress;
            public float arrestThreat;
            public int outfit;
            public int vehicle = -1;
            public int seat = -1;
            public int lift = -1;
            public int doorCar = -1;
            public int doorSide;
            public float doorTime;
            public Vector3 doorStart;
            public Vector3 doorStep;
            public Vector3 doorCenter;
            public float doorYaw;
            public int revision;
            public int held;
            public float bikeLean;
            public bool arrestProtected;
        }

        [Serializable]
        internal sealed class Vehicle
        {
            public Vector3 pos;
            public float yaw;
            public float speed;
            public int driver;
            public long tune;
            public float lean;
            public float frontDamage;
            public float rearDamage;
            [NonSerialized] internal bool TuneChosen;
        }

        [Serializable]
        internal sealed class ImpactEvent
        {
            public string type = "surface";
            public int protocol = Protocol;
            public int material;
            public Vector3 point;
            public Vector3 normal;
            public bool broken;
        }

        [Serializable]
        private sealed class Frame
        {
            public string type;
            public int protocol;
            public int index;
            public int pong;
            public int staminaRefill;
            public int amount;
            public long option;
            public int id;
            public int credits;
            public int bankPersonal, bankBusiness, transaction;
            public CostaLedger.Entry[] bankHistory;
            public CostaCampaign.Progress campaign;
            public int evidence;
            public CrewOrder crew;
            public int weapons;
            public int notice;
            public bool hasWorldHour;
            public float worldHour;
            public string name;
            public string challenge;
            public string publicKey;
            public string signature;
            public Avatar pose;
            public Avatar[] players;
            public Vehicle[] vehicles;
            public int[] vehicleIds;
            public int humans, policeCount;
            public RosterEntry[] roster;
            public int[] bag;
            public int equipSequence;
            public int[] loaded;
            public bool[] reloading;
            public bool[] sites;
            public CostaInteriors.State[] interiors;
            public bool[] alarms;
            public int[] crewSlots;
            public Dispatch dispatch;
            public CityOpportunity opportunity;
            public Vector3 direction;
            public int[] scenery;
            public CostaTraffic.Car[] traffic;
            public CostaTraffic.CityEvent cityEvent;
            public int harmSequence;
            public int harmAmount;
            public Vector3 harmSource;
            public int pressureSequence;
            public Vector3 pressureSource;
        }

        [Serializable]
        private sealed class WorldSnapshot
        {
            public string type = "state";
            public int protocol = Protocol;
            public bool hasWorldHour = true;
            public float worldHour;
            public int humans, policeCount;
            public RosterEntry[] roster;
            public bool[] sites;
            public CostaInteriors.State[] interiors;
            public bool[] alarms;
            public int[] crewSlots;
            public Dispatch dispatch;
            public CityOpportunity opportunity;
            public int[] scenery;
            public CostaTraffic.Car[] traffic;
            public CostaTraffic.CityEvent cityEvent;
        }

        [Serializable]
        private sealed class PlayerSnapshot
        {
            public int id;
            public int pong;
            public int staminaRefill;
            public Avatar[] players;
            public Vehicle[] vehicles;
            public int[] vehicleIds;
            public int credits;
            public int bankPersonal, bankBusiness, transaction;
            public int evidence;
            public CrewOrder crew;
            public int weapons;
            public int[] bag;
            public int equipSequence;
            public int harmSequence;
            public int harmAmount;
            public Vector3 harmSource;
            public int pressureSequence;
            public Vector3 pressureSource;
            public int[] loaded = new int[HarborArsenal.WeaponNames.Length];
            public bool[] reloading = new bool[HarborArsenal.WeaponNames.Length];
        }

        private sealed class Peer
        {
            internal CostaWire Wire;
            internal readonly RosterEntry Roster=new();
            internal Avatar Pose;
            internal bool Ready;
            internal string ResumeKey;
            internal byte[] Challenge;
            internal CostaLedger.Account Account;
            internal CostaLedger.Entry[] SentHistory;
            internal CostaCampaign.Progress SentCampaign;
            internal int StaminaRefill;
            internal int Credits => Account?.cash ?? 4500;
            internal int Evidence;
            internal float DispatchSince = -1;
            internal int Weapons;
            internal float LastPacket;
            internal float LastPose;
            internal float AirborneSince = -1;
            internal float TakeoffHeight;
            internal float FallPeak;
            internal float LandedAt = -1;
            internal float LandingSeverity;
            internal float GestureStarted;
            internal float GestureEnds;
            internal float Stamina = HarborRun.FullStamina;
            internal float RecoverAfter;
            internal float RollUntil;
            internal float NextRoll;
            internal bool ReportedRoll;
            internal bool SprintExhausted;
            internal float TravelSlack = 0.55f;
            internal float HeldUntil;
            internal float DoorUntil;
            internal float HeatUntil;
            internal float NextCrime;
            internal float NextInteriorUse;
            internal float NextPayroll;
            internal float NextTreatment;
            internal int ArrestTarget;
            internal float ArrestStarted;
            internal float ArrestAfter;
            internal float NextShot;
            internal float NextPressure;
            internal float LastHarm;
            internal float RespawnProtection;
            internal readonly int[] UsedRounds = new int[HarborArsenal.WeaponNames.Length];
            internal readonly float[] ReloadEnds = new float[HarborArsenal.WeaponNames.Length];
            internal readonly CostaBag Bag = new();
            internal readonly PlayerSnapshot Snapshot = new();
        }

        private readonly List<Peer> peers = new();
        private readonly List<Avatar> active = new(MaxPlayers);
        private readonly WorldSnapshot world = new() { sites = new bool[CostaRobberies.Sites.Length], alarms = new bool[CostaRobberies.Sites.Length], crewSlots = new int[CostaRobberies.Sites.Length] };
        private const float ResumeLifetime = 300f;
        private const int ResumeLimit = MaxPlayers * 4;
        private readonly Dictionary<string, (Peer State, float Expires)> disconnected = new();
        private readonly List<string> expiredResumes = new();
        private RSA clientIdentity;
        private bool identitySent;
        private readonly CostaVehicle[] sceneVehicles;
        private readonly CostaWorldDamage scenery;
        private readonly CostaTraffic traffic;
        private readonly CostaInteriors interiors;
        internal Vector3 InteriorListener { get; set; }
        internal CostaEffects InteriorEffects { get; set; }
        private readonly Queue<ImpactEvent> surfaceEvents = new();
        private readonly ImpactEvent impactFrame = new();
        private int lastHarm;
        private int lastPressure;
        private const float RobberyDuration = 8f;
        private const float PreparationTime = 5f;
        private readonly Peer[] robbers = new Peer[CostaRobberies.Sites.Length];
        private readonly float[] robberyEnds = new float[CostaRobberies.Sites.Length];
        private readonly float[] restock = new float[CostaRobberies.Sites.Length];
        private readonly int[] initialHomes;
        private TcpListener listener;
        private Vehicle[] hostCars;
        private float[] doorBusyUntil;
        private CostaWire connection;
        private Task<CostaWire> connecting;
        private CancellationTokenSource connectionCancellation;
        private CostaInternetRoom internetRoom;
        private CostaNorayRoom norayRoom;
        private float connectDeadline;
        private int identity;
        private int nextIdentity;
        private float nextPose;
        private float nextSnapshot;
        private float nextWatchdog;
        private float lastClock;
        private double worldStart;
        private double worldHourReceivedAt;
        private float receivedWorldHour;
        private bool hasWorldHour;
        private readonly Queue<int> noticeEvents = new();
        private bool requestedHost;
        private float noticeUntil;
        private string playerName = "";
        private string status = "";

        internal Avatar[] Players { get; private set; } = Array.Empty<Avatar>();
        internal Vehicle[] Cars { get; private set; }
        internal Avatar Local { get; private set; }
        internal bool[] Sites { get; private set; }
        internal CostaBag Bag { get; } = new();
        internal int Cash { get; private set; }
        internal int WeaponMask { get; private set; }
        internal int EquipAcknowledged { get; private set; }
        internal int[] Loaded { get; private set; }
        internal bool[] Reloading { get; private set; }
        internal bool Online => Local != null && connection != null && !connection.Closed;
        internal bool Connecting => connecting != null || (connection != null && Local == null);
        internal bool[] Alarms { get; private set; }
        internal bool Hosting => listener != null;
        internal string RelayAddress { get; private set; } = "";
        internal string RoomCode => norayRoom?.Code ?? internetRoom?.Code ?? "";
        internal string RoomInvite => norayRoom?.Code ?? (RoomCode.Length > 0 ? RelayAddress + "/join/" + RoomCode : "");
        internal float WorldHour
        {
            get
            {
                double now = Time.realtimeSinceStartupAsDouble;
                return !Hosting && hasWorldHour
                    ? HarborDaylight.Advance(receivedWorldHour, now - worldHourReceivedAt)
                    : HarborDaylight.Advance(HarborDaylight.StartHour, now - worldStart);
            }
        }
        private CostaTraffic.Car[] trafficTarget;
        private CostaTraffic.CityEvent cityEventTarget;
        internal string Status => status;
        internal string Notice { get; private set; } = "";
        internal bool ReadImpact(out ImpactEvent impact) => surfaceEvents.TryDequeue(out impact);
        internal (int Amount, Vector3 Source) Harm { get; private set; }
        internal Vector3? Pressure { get; private set; }

        internal int NoticeEvent => noticeEvents.TryDequeue(out int code) ? code : 0;
        internal string CurrentNotice => Time.realtimeSinceStartup < noticeUntil ? Notice : "";
        internal int PoliceCount => networkPolice;
        internal int HumanCount => networkHumans;
        internal int PoliceLimit => AllowedPolice(HumanCount);
        internal static int AllowedPolice(int players) => players < 2 ? 0 : Mathf.Clamp(players * 3 / 10, 1, 4);

        internal CostaSession(CostaVehicle[] vehicles, CostaWorldDamage scenery = null, CostaTraffic traffic = null, CostaInteriors interiors = null)
        {
            this.traffic = traffic;
            this.interiors = interiors;
            this.scenery = scenery;
            sceneVehicles = vehicles;
            initialHomes = new int[vehicles.Length];
            for (int i = 0; i < vehicles.Length; i++)
                initialHomes[i] = Mathf.RoundToInt(vehicles[i].Root.eulerAngles.y);
        }

        internal void Start(string name, string address, int port, bool host, string relayAddress = null)
        {
            Dispose();
            Uri relay = null;
            IPAddress ip = null;
            bool useNoray = relayAddress == CostaNorayRoom.Service;
            if (useNoray)
            {
                if (!host && !CostaNorayRoom.TryCode(address, out address))
                {
                    status = L("Paste a valid room invitation.", "Cole um convite de sala válido.");
                    return;
                }
            }
            else if (relayAddress != null)
            {
                if (!CostaInternetRoom.TryEndpoint(relayAddress, out relay))
                {
                    status = L("Enter a valid HTTPS relay address.", "Informe um endereço HTTPS válido para o servidor.");
                    return;
                }

                address = address.Trim().ToUpperInvariant();
                if (!host && !CostaInternetRoom.ValidCode(address, 10, CostaInternetRoom.UpperHex))
                {
                    status = L("Enter the room code from the host.", "Informe o código da sala do anfitrião.");
                    return;
                }
            }
            else if (port < 1 || port > 65535 || (!host && (!IPAddress.TryParse(address, out ip)
                || ip.AddressFamily != AddressFamily.InterNetwork)))
            {
                status = L("Enter a valid IPv4 address and port.", "Informe um IPv4 e uma porta válidos.");
                return;
            }

            try
            {
                LoadIdentity();
            }
            catch (Exception error) when (error is CryptographicException || error is PlatformNotSupportedException)
            {
                Dispose();
                status = L("Could not load the player identity.", "Não foi possível carregar a identidade do jogador.");
                return;
            }

            playerName = SafeName(name);
            requestedHost = host;
            worldStart = Time.realtimeSinceStartupAsDouble;
            try
            {
                if (host)
                {
                    interiors?.UseLocalWorld(true);
                    ledger = new CostaLedger(System.IO.Path.Combine(Application.persistentDataPath,"CostaLanBank-v1.json"));
                    bool relayed = useNoray || relay != null;
                    listener = new TcpListener(relayed ? IPAddress.Loopback : IPAddress.Any, relayed ? 0 : port);
                    listener.Start(MaxPlayers);
                    port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    hostCars = new Vehicle[sceneVehicles.Length];
                    doorBusyUntil = new float[hostCars.Length];
                    for (int i = 0; i < hostCars.Length; i++)
                    {
                        hostCars[i] = new Vehicle { pos = sceneVehicles[i].Home, yaw = initialHomes[i] };
                        sceneVehicles[i].ApplyBuild(0);
                        sceneVehicles[i].SetAuthoritativePose(hostCars[i].pos, hostCars[i].yaw);
                    }
                    Physics.SyncTransforms();
                }

                connectionCancellation = new CancellationTokenSource();
                var token = connectionCancellation.Token;
                if (useNoray)
                {
                    norayRoom = new CostaNorayRoom();
                    connecting = norayRoom.Connect(host, address, port, token);
                }
                else if (relay == null)
                    connecting = CostaWire.ConnectTcp(host ? IPAddress.Loopback : ip, port, token);
                else
                {
                    RelayAddress = relay.GetLeftPart(UriPartial.Authority);
                    if (host)
                        internetRoom = new CostaInternetRoom(relay);
                    connecting = host ? internetRoom.Host(port, token) : CostaInternetRoom.Join(relay, address, token);
                }
                connectDeadline = Time.realtimeSinceStartup + (useNoray || relay != null ? 18f : 8f);
                status = L("Connecting…", "Conectando…");
                lastClock = Time.realtimeSinceStartup;
            }
            catch (SocketException)
            {
                Dispose();
                status = L("Could not open that connection.", "Não foi possível abrir essa conexão.");
            }
        }

        internal void Tick(Avatar localPose)
        {
            Harm = default;
            Pressure = null;
            float now = Time.realtimeSinceStartup;
            float elapsed = Mathf.Max(0, now - lastClock);
            float delta = Mathf.Min(elapsed, .10f);
            lastClock = now;

            if (internetRoom?.Ended == true || norayRoom?.Ended == true && connecting == null)
            {
                Dispose();
                status = L("The relay disconnected.", "O servidor intermediário desconectou.");
                return;
            }
            if (listener != null)
            {
                ledger.FinishBank();
                TickHost(now, delta, elapsed);
            }
            TickClient(now, localPose);
            if (Online && !Hosting)
                traffic?.Present(trafficTarget, cityEventTarget, WorldHour);
        }

        private void TickClient(float now, Avatar localPose)
        {
            if (connecting != null)
            {
                if (connecting.Status == TaskStatus.RanToCompletion)
                {
                    connection = connecting.Result;
                    connecting = null;
                    connectionCancellation.Dispose();
                    connectionCancellation = null;
                    nextWatchdog = now + 10f;
                }
                else if (connecting.IsFaulted || connecting.IsCanceled || now >= connectDeadline)
                {
                    bool internet = RelayAddress.Length > 0 || norayRoom != null;
                    bool creatingRoom = requestedHost;
                    Dispose();
                    status = !internet ? L("Connection failed.", "A conexão falhou.")
                        : creatingRoom ? L("Room server unavailable.", "Servidor de salas indisponível.")
                        : L("Check the invite or try again.", "Confira o convite ou tente novamente.");
                    return;
                }
            }

            if (connection == null)
                return;

            Frame presentation = null;
            for (int messages = 0; messages < 24 && connection.Receive(out string text); messages++)
            {
                TrackNetwork(text,now);
                Frame frame = Parse(text);
                if (frame != null && frame.protocol == Protocol && frame.type == "full")
                {
                    Dispose();
                    status = L("The room is full.", "A sala está cheia.");
                    return;
                }
                if (Local == null && frame != null && frame.protocol == Protocol && frame.type == "challenge")
                {
                    if (!SendIdentity(frame.challenge))
                    {
                        Dispose();
                        status = L("Could not authenticate the session.", "Não foi possível autenticar a sessão.");
                        return;
                    }
                    continue;
                }
                if (frame != null && frame.protocol == Protocol && frame.type == "notice")
                {
                    noticeEvents.Enqueue(frame.notice);
                    Notice = FormatNotice(frame.notice);
                    noticeUntil = now + 4.2f;
                    continue;
                }
                if (frame != null && frame.protocol == Protocol && frame.type == "account")
                {
                    BankHistory = frame.bankHistory;
                    Narrative = frame.campaign;
                    continue;
                }
                if (frame != null && frame.protocol == Protocol && frame.type == "surface")
                {
                    ImpactEvent surface = JsonUtility.FromJson<ImpactEvent>(text);
                    if (surfaceEvents.Count < 24 && surface.material >= 0 && surface.material <= (int)CostaWorldDamage.Surface.Earth
                        && Finite(surface.point.x) && Finite(surface.point.y) && Finite(surface.point.z)
                        && Finite(surface.normal.x) && Finite(surface.normal.y) && Finite(surface.normal.z))
                        surfaceEvents.Enqueue(surface);
                    continue;
                }
                if (frame == null || frame.type != "state" || frame.protocol != Protocol || frame.players == null
                    || frame.players.Length > MaxPlayers + 1 || !ValidVehicleSlice(frame) || frame.roster?.Length != frame.humans
                    || Array.Exists(frame.players, avatar => avatar == null || avatar.vehicle < -1 || avatar.vehicle >= sceneVehicles.Length
                        || avatar.doorCar < -1 || avatar.doorCar >= sceneVehicles.Length
                        || avatar.wantedLevel < 0 || avatar.wantedLevel > MaxWanted || !CostaInteriors.ValidRider(avatar.lift, avatar.seat))
                    || Array.Exists(frame.vehicles, vehicle => vehicle == null || !CostaBuildTuning.Valid(vehicle.tune))
                    || interiors != null && !CostaInteriors.Valid(frame.interiors)
                    || frame.sites?.Length != restock.Length || frame.alarms?.Length != restock.Length
                    || frame.crewSlots?.Length != restock.Length
                    || frame.dispatch?.active == true && (frame.dispatch.site < 0 || frame.dispatch.site >= restock.Length)
                    || frame.opportunity?.active == true && (frame.opportunity.site < 0 || frame.opportunity.site >= restock.Length)
                    || frame.crew?.role > 0 && (frame.crew.role > 3 || frame.crew.site < 0 || frame.crew.site >= restock.Length)
                    || frame.loaded?.Length != HarborArsenal.WeaponNames.Length
                    || frame.reloading?.Length != HarborArsenal.WeaponNames.Length)
                {
                    Dispose();
                    status = L("Incompatible session.", "Sessão incompatível.");
                    return;
                }

                identity = frame.id;
                Players = frame.players;
                ReceiveVehicleSlice(frame);
                if (frame.pong == nextPingSequence && pingSent > 0)
                {
                    NetworkRoundTrip = (now-pingSent)*1000f;
                    pingSent = 0;
                }
                Sites = frame.sites;
                Alarms = frame.alarms;
                PoliceDispatch = frame.dispatch?.active == true ? frame.dispatch : null;
                Opportunity = frame.opportunity?.active == true ? frame.opportunity : null;
                Evidence = frame.evidence;
                CrewTask = frame.crew?.role > 0 ? frame.crew : null;
                availableCrewSlots = frame.crewSlots;
                Local = null;
                foreach (Avatar avatar in Players)
                    if (avatar != null && avatar.id == identity)
                        Local = avatar;
                if (Local == null)
                    continue;

                if (!Hosting)
                    presentation = frame;
                if (frame.harmSequence != lastHarm)
                {
                    lastHarm = frame.harmSequence;
                    Harm = (Mathf.Clamp(frame.harmAmount, 1, 120), frame.harmSource);
                }
                if (frame.pressureSequence != lastPressure)
                {
                    lastPressure = frame.pressureSequence;
                    Pressure = frame.pressureSource;
                }

                if (frame.hasWorldHour && frame.worldHour >= 0f && frame.worldHour < 24f)
                {
                    hasWorldHour = true;
                    receivedWorldHour = frame.worldHour;
                    worldHourReceivedAt = Time.realtimeSinceStartupAsDouble;
                }
                Cash = frame.credits;
                BankPersonal = frame.bankPersonal;
                BankBusiness = frame.bankBusiness;
                StaminaRefill = frame.staminaRefill;
                nextTransaction = Mathf.Max(nextTransaction,frame.transaction);
                WeaponMask = frame.weapons;
                EquipAcknowledged = frame.equipSequence;
                Loaded = frame.loaded;
                Reloading = frame.reloading;
                Bag.Replace(frame.bag);
                nextWatchdog = now + 9f;
                status = requestedHost ? L("Hosting", "Hospedando") : L("Connected", "Conectado");
            }

            if (connection.Closed || now >= nextWatchdog)
            {
                Dispose();
                status = L("Disconnected.", "Conexão encerrada.");
                return;
            }

            if (presentation != null)
            {
                interiors?.Read(presentation.interiors);
                scenery?.Apply(presentation.scenery);
                trafficTarget = presentation.traffic;
                cityEventTarget = presentation.cityEvent;
            }

            if (Online && now >= nextPing)
            {
                nextPing=now+2;
                pingSent=now;
                Request("ping",++nextPingSequence);
            }

            if (Online && localPose != null && now >= nextPose)
            {
                nextPose = now + Interval;
                Send(new Frame { type = "pose", pose = localPose });
            }
        }

        private bool TryAcceptPeer(out CostaWire wire)
        {
            if (listener.Pending())
            {
                wire = new CostaWire(listener.AcceptTcpClient());
                return true;
            }
            wire = null;
            return norayRoom != null && norayRoom.TryAccept(out wire)
                || internetRoom != null && internetRoom.TryAccept(out wire);
        }

        private void TickHost(float now, float delta, float elapsed)
        {
            for (int i = 0; i < 3 && TryAcceptPeer(out var wire); i++)
            {
                if (peers.Count >= MaxPlayers)
                    _ = wire.Reject(JsonUtility.ToJson(new Frame { type = "full", protocol = Protocol }));
                else
                {
                    byte[] challenge = new byte[32];
                    using (var random = RandomNumberGenerator.Create())
                        random.GetBytes(challenge);
                    var peer = new Peer { Wire = wire, LastPacket = now, Challenge = challenge };
                    peers.Add(peer);
                    peer.Wire.Send(JsonUtility.ToJson(new Frame
                    {
                        type = "challenge", protocol = Protocol, challenge = Convert.ToBase64String(challenge)
                    }));
                }
            }

            expiredResumes.Clear();
            foreach (var entry in disconnected)
                if (now >= entry.Value.Expires)
                    expiredResumes.Add(entry.Key);
            foreach (string key in expiredResumes)
                disconnected.Remove(key);

            bool left = false;
            for (int index = peers.Count - 1; index >= 0; index--)
            {
                Peer peer = peers[index];
                if (peer.Wire.Closed || now - peer.LastPacket > 10f)
                {
                    ReleaseVehicle(peer);
                    if (interiors != null && peer.Ready && peer.Pose.lift >= 0)
                        peer.Pose.pos = interiors.Landing(peer.Pose.lift,peer.Pose.pos);
                    if (peer.Ready)
                        peer.Pose.seat = peer.Pose.lift = -1;
                    peer.Wire.Dispose();
                    peers.RemoveAt(index);
                    left |= peer.Ready;
                    if (peer.Ready)
                        RememberDisconnected(peer, now);
                    continue;
                }
                Frame pendingPose = null;
                for (int n = 0; n < 12 && peer.Wire.Receive(out string text); n++)
                {
                    Frame message = Parse(text);
                    if (message == null)
                    {
                        peer.Wire.Dispose();
                        break;
                    }
                    peer.LastPacket = now;
                    if (!peer.Ready)
                    {
                        if (!VerifyIdentity(peer.Challenge, message, out string resumeKey)
                            || peers.Exists(other => other.Ready && other.ResumeKey == resumeKey))
                        {
                            peer.Wire.Dispose();
                            break;
                        }
                        peer.ResumeKey = resumeKey;
                        peer.Account = ledger.Get(resumeKey);
                        peer.Weapons = peer.Account.weapons;
                        peer.Challenge = null;
                        if (disconnected.TryGetValue(peer.ResumeKey, out var resume))
                        {
                            disconnected.Remove(peer.ResumeKey);
                            Peer saved = resume.State;
                            peer.Pose = saved.Pose;
                            peer.Pose.name = SafeName(message.name);
                            peer.Pose.playing = false;
                            peer.Pose.doorCar = -1;
                            peer.Pose.doorTime = 0;
                            peer.Evidence = saved.Evidence;
                            peer.Weapons = saved.Weapons;
                            peer.Bag.Replace(saved.Bag.Amounts);
                            peer.HeldUntil = saved.HeldUntil;
                            peer.ArrestAfter = saved.ArrestAfter;
                            peer.NextShot = saved.NextShot;
                            peer.HeatUntil = saved.HeatUntil;
                            peer.NextCrime = saved.NextCrime;
                            peer.NextTreatment = saved.NextTreatment;
                            peer.NextPayroll = saved.NextPayroll;
                            peer.Pose.arrestProgress = 0;
                            peer.RespawnProtection = saved.RespawnProtection;
                            Array.Copy(saved.UsedRounds, peer.UsedRounds, peer.UsedRounds.Length);
                            Array.Copy(saved.ReloadEnds, peer.ReloadEnds, peer.ReloadEnds.Length);
                            peer.Ready = true;
                            peer.LastPose = now;
                            if (peer.Pose.police && CountPolice() > AllowedPolice(CountPlayers()))
                                SetRole(peer, false);
                            continue;
                        }

                        peer.Ready = true;
                        var id = ++nextIdentity;
                        Vector3 position = FindCriminalSpawn();
                        peer.Pose = new Avatar { id = id, name = SafeName(message.name), pos = position, yaw = 350, revision = 1 };
                        peer.LastPose = now;
                    }
                    else
                    {
                        if (message.type == "pose")
                        {
                            if (message.pose != null && message.pose.revision == peer.Pose.revision)
                                pendingPose = message;
                            continue;
                        }
                        if (pendingPose != null)
                        {
                            Handle(peer, pendingPose, now);
                            pendingPose = null;
                        }
                        Handle(peer, message, now);
                    }
                }
                if (pendingPose != null && !peer.Wire.Closed)
                    Handle(peer, pendingPose, now);
            }

            if (left)
                Rebalance();
            TickInteriors(now, delta);
            TickPayroll(now);
            TickGetaways(now);
            TickRobberies(now);

            for (int i = 0; i < hostCars.Length; i++)
            {
                Vehicle vehicle = hostCars[i];
                if (vehicle.driver != 0 || Mathf.Abs(vehicle.speed) < 0.05f)
                    continue;

                Quaternion rotation = Quaternion.Euler(0, vehicle.yaw, 0);
                Vector3 direction = rotation * Vector3.forward * Mathf.Sign(vehicle.speed);
                float travel = Mathf.Abs(vehicle.speed) * delta;
                float safeTravel = sceneVehicles[i].SimulatedTravel(vehicle.pos, rotation, direction, travel);
                vehicle.pos += direction * safeTravel;
                if (sceneVehicles[i].SetAuthoritativePose(vehicle.pos, vehicle.yaw))
                    Physics.SyncTransforms();
                vehicle.lean = Mathf.MoveTowards(vehicle.lean, 0, 80 * delta);
                if (safeTravel + .045f < travel)
                    DamageCar(vehicle, vehicle.speed);
                vehicle.speed = safeTravel < travel ? 0 : Mathf.MoveTowards(vehicle.speed, 0, 4.5f * delta);
            }

            foreach (Peer peer in peers)
                if (peer.Ready)
                    peer.Pose.arrestThreat = 0;
            foreach (Peer peer in peers)
                if (peer.Ready && peer.ArrestTarget != 0)
                    AdvanceArrest(peer, now);

            TickPatrol(now, delta);
            TickOpportunity(now);
            TickDispatch(now);

            Vector3? chase = null;
            int strongest = 0;
            foreach (Peer peer in peers)
                if (peer.Ready && peer.Pose.playing && peer.Pose.wantedLevel >= 2 && peer.Pose.wantedLevel > strongest)
                {
                    strongest = peer.Pose.wantedLevel;
                    chase = peer.Pose.pos;
                }
            if (traffic != null)
            {
                float hour = WorldHour;
                traffic.Simulate(delta, hour, chase, elapsed);
                traffic.UpdateSignals(hour);
            }
            if (now < nextSnapshot)
                return;
            nextSnapshot = now + Interval;
            active.Clear();
            foreach (Peer peer in peers)
            {
                if (!peer.Ready)
                    continue;
                peer.Pose.doorTime = Mathf.Max(0, peer.DoorUntil - now);
                if (peer.Pose.doorTime == 0)
                    peer.Pose.doorCar = -1;
                DecayHeat(peer, now);
                peer.Pose.carryingLoot = peer.Bag.Count > 0;
                peer.Pose.held = Mathf.CeilToInt(Mathf.Max(0, peer.HeldUntil - now));
                peer.Pose.arrestProtected = now < peer.ArrestAfter;
                for (int i = 0; i < peer.ReloadEnds.Length; i++)
                {
                    if (peer.ReloadEnds[i] > 0 && now >= peer.ReloadEnds[i])
                    {
                        peer.ReloadEnds[i] = 0;
                        peer.UsedRounds[i] = 0;
                    }
                }
                int heldWeapon = peer.Pose.equipped;
                float reloadEnds = heldWeapon >= 0 && heldWeapon < HarborArsenal.Knife
                    ? peer.ReloadEnds[heldWeapon] : 0;
                peer.Pose.reloadProgress = reloadEnds > now ? Mathf.Clamp01(1 -
                    (reloadEnds - now) / HarborArsenal.ShotRules(heldWeapon).reload) : -1;
                float landProgress = peer.LandedAt >= 0
                    ? (now - peer.LandedAt) / Mathf.Lerp(0.14f, 0.43f, peer.LandingSeverity) : 1;
                peer.Pose.landingCompression = landProgress < 1
                    ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(landProgress)) * Mathf.Lerp(0.23f, 1f, peer.LandingSeverity) : 0;
                if (now >= peer.GestureEnds)
                    peer.Pose.gesture = 0;
                else
                    peer.Pose.gestureProgress = Mathf.InverseLerp(peer.GestureStarted, peer.GestureEnds, now);
                active.Add(peer.Pose);
            }
            if (patrol != null)
                active.Add(patrol.Pose);
            if (active.Count == 0)
                return;
            IndexEntities();
            world.worldHour = WorldHour;
            world.interiors = interiors?.States;
            world.dispatch = dispatch;
            world.opportunity = opportunity;
            world.scenery = scenery?.Capture();
            world.traffic = traffic?.Cars;
            world.cityEvent = traffic?.Incident;
            for (int i = 0; i < world.sites.Length; i++)
                {
                world.sites[i] = now >= restock[i];
                world.alarms[i] = RobberyAlarm(i, now);
                world.crewSlots[i] = robbers[i] == null ? 0
                    : (crews[i].Lookout == null ? 1 : 0) | (crews[i].Driver == null ? 2 : 0);
            }
            UpdateRoster();
            world.humans = CountPlayers();
            world.policeCount = CountPolice();
            byte[] shared = Encoding.UTF8.GetBytes(JsonUtility.ToJson(world));
            foreach (Peer peer in peers)
            {
                if (!peer.Ready)
                    continue;
                var snapshot = peer.Snapshot;
                snapshot.id = peer.Pose.id;
                snapshot.credits = peer.Credits;
                snapshot.bankPersonal = peer.Account.personal;
                snapshot.bankBusiness = peer.Account.business;
                if (peer.SentHistory != peer.Account.history || peer.SentCampaign != peer.Account.campaign)
                {
                    peer.SentHistory = peer.Account.history;
                    peer.SentCampaign = peer.Account.campaign;
                    peer.Wire.Send(JsonUtility.ToJson(new AccountUpdate
                    {
                        bankHistory = peer.SentHistory, campaign = peer.SentCampaign
                    }));
                }
                snapshot.staminaRefill = peer.StaminaRefill;
                snapshot.transaction = peer.Account.serial;
                snapshot.evidence = peer.Evidence;
                snapshot.crew = CrewState(peer, now);
                snapshot.weapons = WeaponInventory(peer);
                snapshot.bag = peer.Bag.Amounts;
                for (int i = 0; i < snapshot.loaded.Length; i++)
                {
                    snapshot.loaded[i] = HarborArsenal.ShotRules(i).magazine - peer.UsedRounds[i];
                    snapshot.reloading[i] = peer.ReloadEnds[i] > 0;
                }
                FilterSnapshot(peer);
                peer.Wire.SendState(shared, JsonUtility.ToJson(snapshot));
            }
        }

        private void RememberDisconnected(Peer peer, float now)
        {
            peer.Wire = null;
            if (disconnected.Count >= ResumeLimit)
            {
                string oldest = null;
                float expires = float.PositiveInfinity;
                foreach (var entry in disconnected)
                    if (entry.Value.Expires < expires)
                    {
                        oldest = entry.Key;
                        expires = entry.Value.Expires;
                    }
                if (oldest != null)
                    disconnected.Remove(oldest);
            }
            disconnected[peer.ResumeKey] = (peer, now + ResumeLifetime);
        }

        private void Handle(Peer peer, Frame msg, float now)
        {
            Avatar avatar = peer.Pose;
            if (msg.type == "ping")
            {
                peer.Snapshot.pong = msg.index;
                return;
            }
            if (msg.type == "pose")
            {
                if (msg.pose != null && msg.pose.revision == avatar.revision)
                {
                    avatar.playing |= msg.pose.playing;
                    if (avatar.playing && avatar.seat < 0 && avatar.lift < 0 && now >= peer.HeldUntil && now >= peer.DoorUntil)
                        UpdatePose(peer, msg.pose, now);
                }
                return;
            }
            if (msg.type == "equip")
            {
                if (now >= peer.HeldUntil && now >= peer.DoorUntil && CanEquip(peer, msg.index))
                    avatar.equipped = msg.index;
                peer.Snapshot.equipSequence = msg.equipSequence;
                return;
            }

            if ((now < peer.HeldUntil || now < peer.DoorUntil) && msg.type != "outfit")
                return;

            if (!avatar.playing && msg.type != "police" && msg.type != "outfit")
                return;

            if ((avatar.lift >= 0 && msg.type is not ("outfit" or "police")) ||
                (avatar.seat >= 0 && msg.type is not ("interior" or "outfit" or "police")))
                return;

            switch (msg.type)
            {
                case "interior":
                    UseInterior(peer, msg.index, now);
                    break;
                case "police":
                    if (avatar.police)
                    {
                        SetRole(peer, false);
                    }
                    else if (avatar.wantedLevel > 0 || peer.Bag.Count > 0)
                    {
                        SetNotice(peer, 12);
                    }
                    else if (CountPolice() >= AllowedPolice(CountPlayers()))
                    {
                        SetNotice(peer, 1);
                    }
                    else
                    {
                        SetRole(peer, true);
                    }
                    break;
                case "outfit":
                    avatar.outfit = Mathf.Clamp(msg.index, 0, 242);
                    break;
                case "campaign":
                    ReceiveCampaign(peer,msg);
                    break;
                case "workshop":
                    ReceiveWorkshop(peer,msg);
                    break;
                case "bank":
                    ReceiveBank(peer,msg);
                    break;
                case "crew":
                    UseCrew(peer, msg.index, now);
                    break;
                case "loot":
                    StartRobbery(peer, msg.index, now);
                    break;
                case "sell":
                    if (!avatar.police && avatar.vehicle < 0 && AtVendor(avatar.pos))
                    {
                        int amount = peer.Bag.PriceOfSale(msg.index);
                        if (Reward(peer,amount))
                        {
                            peer.Bag.Sell(msg.index);
                            BeginGesture(peer, HarborCharacter.Gesture.Reach, now);
                            SetNotice(peer, 7);
                            FinishGetaway(peer, msg.index, now);
                        }
                    }
                    break;
                case "buy":
                    if (!avatar.police && avatar.vehicle < 0 && Near(avatar.pos, CostaCity.GunCounter, 2.35f)
                        && msg.index >= 0 && msg.index < CostaJobs.WeaponPrices.Length
                        && (peer.Weapons & (1 << msg.index)) == 0 && Purchase(peer,CostaJobs.WeaponPrices[msg.index],msg.index))
                    {
                        peer.Weapons |= 1 << msg.index;
                        SetNotice(peer, 8);
                    }
                    break;
                case "fire":
                    if (CanEquip(peer, msg.index) && avatar.equipped == msg.index
                        && now + 0.025f >= peer.NextShot && UpdatePose(peer, msg.pose, now, forShot: true))
                        Fire(peer, msg.index, msg.direction, now);
                    break;
                case "reload":
                    if (msg.index != HarborArsenal.Knife && CanEquip(peer, msg.index)
                        && avatar.equipped == msg.index && peer.UsedRounds[msg.index] > 0 && peer.ReloadEnds[msg.index] == 0)
                        peer.ReloadEnds[msg.index] = now + HarborArsenal.ShotRules(msg.index).reload;
                    break;
                case "arrest":
                    BeginArrest(peer, msg.index, now);
                    break;
                case "enter":
                    if (avatar.vehicle < 0 && msg.index >= 0 && msg.index < hostCars.Length)
                    {
                        Vehicle car = hostCars[msg.index];
                        CostaVehicle vehicle = sceneVehicles[msg.index];
                        if (car.driver == 0 && now >= doorBusyUntil[msg.index] && Mathf.Abs(car.speed) < 3f
                            && !avatar.crouched && avatar.rollPose < 0 && Supported(avatar.pos)
                            && Near(car.pos, avatar.pos, 4f)
                            && vehicle.TryBoard(avatar.pos, car.pos, Quaternion.Euler(0, car.yaw, 0), out int side, out Vector3 step))
                        {
                            car.driver = avatar.id;
                            if (!car.TuneChosen && sceneVehicles[msg.index].Customizable && peer.Account.mods[msg.index] != 0)
                            {
                                car.tune = peer.Account.mods[msg.index];
                                car.TuneChosen = true;
                                sceneVehicles[msg.index].ApplyBuild(car.tune);
                            }
                            car.speed = 0;
                            avatar.vehicle = msg.index;
                            StartDoorMotion(peer, msg.index, side, avatar.pos, step, car, now, CostaVehicleMotion.EnterTime);
                            avatar.pos = car.pos;
                        }
                    }
                    break;
                case "exit":
                    if (avatar.vehicle >= 0)
                    {
                        Vehicle car = hostCars[avatar.vehicle];
                        CostaVehicle vehicle = sceneVehicles[avatar.vehicle];
                        if (vehicle.TryExit(car.pos, Quaternion.Euler(0, car.yaw, 0), msg.index, out Vector3 exit)
                            && ValidPoint(exit))
                        {
                            int index = avatar.vehicle;
                            ReleaseVehicle(peer);
                            StartDoorMotion(peer, index, msg.index, exit, exit, car, now, CostaVehicleMotion.ExitTime);
                            avatar.pos = exit;
                            avatar.yaw = car.yaw;
                        }
                    }
                    break;
            }
        }

        private void StartRobbery(Peer peer, int site, float now)
        {
            if (site < 0 || site >= robbers.Length || now < restock[site]
                || robbers[site] != null || crews[site] != null || CrewIndex(peer) >= 0 || !CanCollect(peer, site, now))
                return;
            if (!peer.Bag.CanLoot(site))
            {
                SetNotice(peer, 6);
                return;
            }
            robbers[site] = peer;
            crews[site] = new Crew { Collector = peer, AlarmAt = now + PreparationTime };
            robberyEnds[site] = now + PreparationTime + (Vulnerable(site) ? 4.5f : RobberyDuration);
            SetNotice(peer, 22);
        }

        private static bool CanCollect(Peer peer, int site, float now) =>
            !peer.Pose.police && peer.Pose.playing && peer.Pose.vehicle < 0
            && peer.Pose.rollPose < 0 && now >= peer.HeldUntil && now >= peer.DoorUntil
            && Near(peer.Pose.pos, CostaRobberies.Sites[site].Point, 2.55f);

        private bool RobberyAlarm(int site, float now) => robbers[site] != null && now >= crews[site].AlarmAt;

        private void TickRobberies(float now)
        {
            for (int i = 0; i < robbers.Length; i++)
            {
                Peer collector = robbers[i];
                if (collector == null)
                    continue;
                if (!peers.Contains(collector) || !CanCollect(collector, i, now))
                {
                    robbers[i] = null;
                    crews[i] = null;
                    if (peers.Contains(collector))
                        SetNotice(collector, 14);
                    continue;
                }
                Crew crew = crews[i];
                if (!CrewAvailable(crew.Lookout, now))
                    crew.Lookout = null;
                if (!CrewAvailable(crew.Driver, now))
                    crew.Driver = null;
                if (now < crew.AlarmAt)
                    continue;
                if (!crew.AlarmSent)
                {
                    crew.AlarmSent = true;
                    SetNotice(collector, 13);
                }
                float assist = LookoutInPlace(crew, i, now) ? 2 : 0;
                if (now < robberyEnds[i] - assist)
                    continue;
                robbers[i] = null;
                if (!collector.Bag.AddLoot(i))
                {
                    crews[i] = null;
                    SetNotice(collector, 6);
                    continue;
                }
                StartGetaway(i, now);
                restock[i] = now + 90;
                if (Vulnerable(i))
                    EndOpportunity(now);
                RaiseHeat(collector, now, false);
                BeginGesture(collector, HarborCharacter.Gesture.PickUp, now, CostaRobberies.PickupTarget(i));
                SetNotice(collector, 5);
            }
        }

        private void TickInteriors(float now, float delta)
        {
            if (interiors == null)
                return;
            float hour = WorldHour;
            foreach (Peer peer in peers)
                if (peer.Ready && peer.Pose.playing)
                    interiors.ObserveDoor(peer.Pose.pos, now, hour);
            interiors.Step(now, delta);
            foreach (Peer peer in peers)
            {
                if (!peer.Ready || peer.Pose.lift < 0)
                    continue;
                Avatar avatar = peer.Pose;
                avatar.pos.y = interiors.Height(avatar.lift);
                if (interiors.Moving(avatar.lift))
                    continue;
                avatar.lift = -1;
                ResetMotion(peer);
                peer.LastPose = now;
                avatar.revision++;
            }
            interiors.Present(delta,true,InteriorEffects,InteriorListener);
        }

        private void UseInterior(Peer peer, int key, float now)
        {
            Avatar avatar = peer.Pose;
            if (interiors == null || avatar.vehicle >= 0 || avatar.rollPose >= 0
                || avatar.lift >= 0 || now < peer.NextInteriorUse || avatar.seat < 0 && !Supported(avatar.pos))
                return;
            if (avatar.seat < 0 && (key & 7) == (int)CostaInteriors.Action.Sit)
                foreach (Peer occupant in peers)
                    if (occupant != peer && occupant.Ready && occupant.Pose.seat == key)
                        return;
            peer.NextInteriorUse = now + .32f;
            int previousSeat = avatar.seat;
            bool accepted = interiors.Use(key,now,WorldHour,ref avatar.pos,ref avatar.seat,out var action,out int notice);
            if (notice > 0)
                SetNotice(peer, notice);
            if (!accepted)
                return;

            if (action == CostaInteriors.Action.Sit && avatar.seat != previousSeat)
            {
                ResetMotion(peer);
                avatar.standing = avatar.seat < 0;
                if (avatar.seat >= 0)
                    avatar.yaw = 180;
                avatar.revision++;
            }
            else if (action is CostaInteriors.Action.Travel or CostaInteriors.Action.Call)
            {
                int building = CostaInteriors.BuildingOf(key);
                foreach (Peer rider in peers)
                {
                    if (!rider.Ready || !rider.Pose.playing || rider.Pose.vehicle >= 0
                        || rider.Pose.seat >= 0 || !interiors.Boarded(building,rider.Pose.pos))
                        continue;
                    ResetMotion(rider);
                    rider.Pose.lift = building;
                    rider.Pose.revision++;
                }
            }
            else if (action == CostaInteriors.Action.Service)
            {
                if (now < peer.NextTreatment)
                {
                    SetNotice(peer,35);
                    return;
                }
                peer.NextTreatment = now + 35;
                bool hospital = CostaInteriorPlan.All[CostaInteriors.BuildingOf(key)].Purpose == CostaInteriorPlan.Use.Hospital;
                avatar.health = Mathf.Min(100,avatar.health+(hospital ? 65 : 12));
                peer.Stamina = HarborRun.FullStamina;
                peer.SprintExhausted = false;
                peer.StaminaRefill++;
                SetNotice(peer,34);
            }
        }

        private void StartDoorMotion(Peer peer, int index, int side, Vector3 start, Vector3 step,
            Vehicle car, float now, float duration)
        {
            ResetMotion(peer);
            peer.DoorUntil = doorBusyUntil[index] = now + duration;
            peer.LastPose = peer.DoorUntil;
            Avatar avatar = peer.Pose;
            avatar.doorCar = index;
            avatar.doorSide = side;
            avatar.doorStart = start;
            avatar.doorStep = step;
            avatar.doorCenter = car.pos;
            avatar.doorYaw = car.yaw;
            avatar.standing = false;
            avatar.revision++;
        }

        private bool UpdatePose(Peer peer, Avatar pose, float now, bool forShot = false)
        {
            if (pose == null || pose.revision != peer.Pose.revision || !ValidPoint(pose.pos)
                || !Finite(pose.yaw) || (!forShot && now - peer.LastPose < 0.035f))
                return false;

            Avatar avatar = peer.Pose;
            float elapsed = Mathf.Clamp(now - peer.LastPose, 0f, 0.5f);
            Vector3 movement = pose.pos - avatar.pos;
            float distance = movement.magnitude;
            bool moving = distance > 0.002f;
            float acceptedSpeed = 0;
            if (avatar.vehicle < 0)
            {
                float planar = new Vector2(movement.x, movement.z).magnitude;
                bool reportsRoll = Finite(pose.rollPose) && pose.rollPose >= 0;
                bool startsRoll = reportsRoll && !peer.ReportedRoll && !pose.crouched && now >= peer.NextRoll
                    && peer.Stamina >= 0.70f && Supported(avatar.pos);
                bool rolling = reportsRoll && (startsRoll || now < peer.RollUntil);
                if (!rolling)
                    pose.rollPose = -1;
                bool allowedSprint = pose.running && !rolling && !pose.crouched
                    && !peer.SprintExhausted && peer.Stamina > 0;
                float speed = Mathf.Max(FootSpeed(avatar, avatar.running && !peer.SprintExhausted, avatar.equipped),
                    FootSpeed(pose, allowedSprint, avatar.equipped));
                float slack = Mathf.Min(0.55f, peer.TravelSlack + elapsed * speed - planar);
                if (slack < 0 || !ValidFootHeight(peer, pose.pos, now, elapsed)
                    || moving && MovementBlocked(avatar, pose, movement / distance, distance))
                {
                    CorrectPose(peer, now);
                    return false;
                }

                peer.TravelSlack = slack;
                peer.ReportedRoll = reportsRoll;
                if (startsRoll)
                {
                    peer.RollUntil = now + 0.75f;
                    peer.NextRoll = now + 1.20f;
                    peer.Stamina = Mathf.Max(0, peer.Stamina - 0.75f);
                    peer.RecoverAfter = now + 0.75f;
                }
                if (!pose.running)
                    peer.SprintExhausted = false;
                bool supported = Supported(pose.pos);
                if (supported)
                {
                    if (peer.AirborneSince >= 0 && !rolling)
                    {
                        peer.LandingSeverity = HarborCharacter.FallSeverity(peer.FallPeak - pose.pos.y);
                        peer.LandedAt = now;
                    }
                    peer.AirborneSince = -1;
                }
                else if (peer.AirborneSince < 0)
                {
                    peer.AirborneSince = now - elapsed;
                    peer.TakeoffHeight = avatar.pos.y;
                    peer.FallPeak = Mathf.Max(avatar.pos.y, pose.pos.y);
                }
                else
                    peer.FallPeak = Mathf.Max(peer.FallPeak, pose.pos.y);
                if (allowedSprint && supported && planar > 0.02f)
                {
                    peer.Stamina = Mathf.Max(0, peer.Stamina - elapsed);
                    peer.SprintExhausted = peer.Stamina == 0;
                    peer.RecoverAfter = now + 0.75f;
                }
                else if (now >= peer.RecoverAfter)
                    peer.Stamina = Mathf.Min(HarborRun.FullStamina, peer.Stamina + elapsed * 0.85f);

                avatar.running = allowedSprint;
                avatar.standing = supported && !pose.crouched && pose.rollPose < 0;
            }
            else
            {
                Vehicle car = hostCars[avatar.vehicle];
                CostaVehicle rules = sceneVehicles[avatar.vehicle];
                float speed = CostaVehicle.DecodeSpeed(pose.held);
                float acceleration = Mathf.Sign(speed) != Mathf.Sign(car.speed) ? rules.BrakePower
                    : rules.Acceleration;
                float speedLimit = Mathf.Min(rules.TopSpeed, Mathf.Abs(car.speed) + acceleration * elapsed + 0.45f);
                float turn = Mathf.Abs(Mathf.DeltaAngle(car.yaw, pose.yaw));
                float travel = Mathf.Max(Mathf.Abs(car.speed), Mathf.Abs(speed)) * elapsed;
                float slack = Mathf.Min(0.65f, peer.TravelSlack + travel - distance);
                int encodedLimit = CostaVehicle.EncodeSpeed(speedLimit);
                if (Mathf.Abs(pose.pos.y) > 0.03f || pose.held < -CostaVehicle.EncodeSpeed(rules.ReverseSpeed)
                    || pose.held < -encodedLimit || pose.held > encodedLimit || turn > rules.MaximumTurnRate * elapsed + 3f
                    || slack < 0 || (moving || turn > 0.02f) && !rules.ValidNetworkTravel(car.pos, car.yaw, pose.pos, pose.yaw, car.speed, speed))
                {
                    CorrectPose(peer, now);
                    return false;
                }
                peer.TravelSlack = slack;
                acceptedSpeed = Mathf.Clamp(speed, -rules.ReverseSpeed, rules.TopSpeed);
                avatar.running = avatar.standing = false;
            }

            peer.LastPose = now;
            avatar.pos = pose.pos;
            avatar.yaw = Mathf.Repeat(pose.yaw, 360);
            avatar.crouched = pose.crouched && avatar.vehicle < 0;
            avatar.moving = pose.moving;
            avatar.aiming = pose.aiming;
            avatar.aimPitch = Finite(pose.aimPitch) ? Mathf.Clamp(pose.aimPitch, -25f, 55f) : 0;
            avatar.jumpPose = Finite(pose.jumpPose) ? Mathf.Clamp(pose.jumpPose, -1, 1) : -1;
            avatar.rollPose = Finite(pose.rollPose) ? Mathf.Clamp(pose.rollPose, -1, 1) : -1;
            if (avatar.vehicle >= 0)
            {
                Vehicle car = hostCars[avatar.vehicle];
                float oldSpeed = car.speed;
                if (Mathf.Abs(oldSpeed) > 5 && Mathf.Abs(acceptedSpeed) < 1.15f)
                {
                    Quaternion facing = Quaternion.Euler(0, pose.yaw, 0);
                    float reach = .28f + Mathf.Min(elapsed * Mathf.Abs(oldSpeed), .65f);
                    Vector3 approach = facing * Vector3.forward * Mathf.Sign(oldSpeed);
                    if (sceneVehicles[avatar.vehicle].SimulatedTravel(pose.pos, facing, approach, reach) + .025f < reach)
                        DamageCar(car, oldSpeed);
                }
                car.pos = new Vector3(pose.pos.x, 0, pose.pos.z);
                car.yaw = avatar.yaw;
                if (sceneVehicles[avatar.vehicle].SetAuthoritativePose(car.pos, car.yaw))
                    Physics.SyncTransforms();
                car.speed = acceptedSpeed;
                car.lean = sceneVehicles[avatar.vehicle].IsBike && Finite(pose.bikeLean)
                    ? Mathf.Clamp(pose.bikeLean, -24, 24) : 0;
            }
            return true;
        }

        private static void DamageCar(Vehicle car, float speed)
        {
            float amount = Mathf.InverseLerp(3.5f, 27f, Mathf.Abs(speed)) * .66f;
            if (speed > 0)
                car.frontDamage = Mathf.Min(1, car.frontDamage + amount);
            else
                car.rearDamage = Mathf.Min(1, car.rearDamage + amount);
        }

        private static void BeginGesture(Peer peer, HarborCharacter.Gesture gesture, float now, Vector3 target = default)
        {
            peer.Pose.gesture = (int)gesture;
            peer.Pose.gestureProgress = 0;
            peer.Pose.gestureTarget = target;
            peer.GestureStarted = now;
            peer.GestureEnds = now + HarborCharacter.GestureDuration(gesture);
        }

        private static void ResetMotion(Peer peer)
        {
            peer.DoorUntil = 0;
            peer.Pose.doorCar = -1;
            peer.Pose.doorTime = 0;
            peer.AirborneSince = -1;
            peer.LandedAt = -1;
            peer.Pose.landingCompression = 0;
            peer.GestureEnds = 0;
            peer.Pose.gesture = 0;
            peer.RollUntil = 0;
            peer.ReportedRoll = false;
            peer.TravelSlack = 0.55f;
        }

        private void CorrectPose(Peer peer, float now)
        {
            peer.Pose.revision++;
            peer.LastPose = now;
            peer.TravelSlack = 0.55f;
            if (peer.Pose.vehicle >= 0)
                hostCars[peer.Pose.vehicle].speed = 0;
        }

        private static bool Supported(Vector3 feet) =>
            Physics.Raycast(feet + Vector3.up * 0.16f, Vector3.down, out RaycastHit floor, 0.27f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && floor.normal.y > 0.65f;

        private static bool ValidFootHeight(Peer peer, Vector3 point, float now, float elapsed)
        {
            float rise = point.y - peer.Pose.pos.y;
            if (rise > 5.8f * elapsed + 0.16f)
                return false;
            if (peer.AirborneSince < 0)
                return true;

            float age = now - peer.AirborneSince;
            if (point.y > peer.TakeoffHeight + 1.14f)
                return false;
            return Supported(point) || age < 0.66f || rise < -elapsed * 1.4f;
        }

        private static float FootSpeed(Avatar pose, bool running, int equipped) =>
            pose.rollPose >= 0 ? 7.3f : pose.crouched ? 2f : running
                ? HarborArsenal.SprintSpeed(equipped)
                : HarborArsenal.WalkSpeed(equipped, false);

        private static int WeaponInventory(Peer peer) =>
            HarborArsenal.KnifeMask | (peer.Pose.police ? PoliceKit : peer.Weapons);

        private static bool CanEquip(Peer peer, int index)
        {
            return peer.Pose.playing && peer.Pose.vehicle < 0 && index >= 0 && index < HarborArsenal.WeaponNames.Length
                && (WeaponInventory(peer) & (1 << index)) != 0;
        }

        private void Fire(Peer shooter, int weapon, Vector3 direction, float now)
        {
            if (!CanEquip(shooter, weapon) || shooter.Pose.equipped != weapon
                || shooter.Pose.rollPose >= 0 || now + 0.025f < shooter.NextShot
                || !Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z)
                || direction.sqrMagnitude < 0.85f || direction.sqrMagnitude > 1.15f)
                return;
            var rules = HarborArsenal.ShotRules(weapon);
            bool melee = weapon == HarborArsenal.Knife;
            if (melee)
            {
                if (shooter.Pose.crouched
                    || Mathf.Abs(direction.y) > 0.05f)
                    return;
            }
            else
            {
                if (shooter.UsedRounds[weapon] >= rules.magazine || shooter.ReloadEnds[weapon] > 0)
                    return;
                shooter.UsedRounds[weapon]++;
            }
            shooter.NextShot = now + rules.interval;
            shooter.Pose.shotWeapon = weapon;
            shooter.Pose.shotNumber++;
            direction.Normalize();

            float heightAboveFeet = melee ? HarborArsenal.KnifeHitHeight : shooter.Pose.crouched ? 0.90f : 1.22f;
            Vector3 origin = shooter.Pose.pos + Vector3.up * heightAboveFeet;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right == Vector3.zero)
                right = Vector3.right;
            Vector3 up = Vector3.Cross(direction, right);
            bool sentSurface = false;
            for (int i = 0; i < rules.pellets; i++)
            {
                Vector2 scatter = HarborArsenal.Scatter(i, rules.pellets, rules.spread, shooter.Pose.aiming);
                Vector3 ray = (direction + right * scatter.x + up * scatter.y).normalized;
                DamageAlongRay(shooter, origin, ray, rules.range, rules.damage, now, melee, ref sentSurface);
            }
        }

        private void DamageAlongRay(Peer shooter, Vector3 origin, Vector3 direction, float range, int damage, float now,
            bool melee, ref bool sentSurface)
        {
            float closest = Physics.Raycast(origin, direction, out RaycastHit cover, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? cover.distance : range;
            Peer victim = null;
            foreach (Peer candidate in peers)
            {
                Avatar avatar = candidate.Pose;
                if (!candidate.Ready || candidate == shooter || !avatar.playing || avatar.vehicle >= 0
                    || now < candidate.HeldUntil || now < candidate.RespawnProtection)
                    continue;
                Vector3 center = avatar.pos + Vector3.up * (avatar.crouched ? 0.59f : 0.91f);
                float height = avatar.crouched ? 0.24f : 0.56f;
                float distance = CapsuleEntry(origin - center, direction, height, 0.32f);
                if (distance < closest)
                {
                    victim = candidate;
                    closest = distance;
                }
            }

            if (!melee)
                NearMiss(shooter, victim, origin, direction, closest, now);

            if (victim == null)
            {
                if (scenery != null && cover.collider != null)
                {
                    bool broken = scenery.Hit(cover.collider, damage);
                    if (!sentSurface || broken)
                    {
                        BroadcastImpact(cover, broken);
                        sentSurface = true;
                    }
                }
                return;
            }
            int absorbed = Mathf.Min(victim.Pose.armor, (damage + 1) / 2);
            victim.Pose.armor -= absorbed;
            victim.Pose.health -= damage - absorbed;
            if (!shooter.Pose.police)
                RaiseHeat(shooter, now, true);
            var feedback = victim.Snapshot;
            feedback.harmAmount = Mathf.Min(120, damage - absorbed / 2 +
                (now - victim.LastHarm < .08f ? feedback.harmAmount : 0));
            victim.LastHarm = now;
            feedback.harmSource = origin;
            feedback.harmSequence++;
            BeginGesture(victim, HarborCharacter.Gesture.Impact, now, origin);
            victim.Pose.impactStrength = Mathf.Clamp01(feedback.harmAmount / 55f);
            if (victim.Pose.health <= 0)
            {
                victim.Pose.health = 100;
                victim.Pose.seat = victim.Pose.lift = -1;
                victim.Pose.armor = victim.Pose.police ? PoliceArmor : 0;
                victim.Pose.pos = victim.Pose.police ? PoliceSpawn : FindCriminalSpawn();
                ResetMotion(victim);
                victim.Pose.revision++;
                victim.RespawnProtection = now + 3;
                SetNotice(victim, 11);
            }
        }

        private void NearMiss(Peer shooter, Peer victim, Vector3 origin, Vector3 direction, float distance, float now)
        {
            foreach (Peer peer in peers)
            {
                if (!peer.Ready || peer == shooter || peer == victim || now < peer.NextPressure ||
                    !peer.Pose.playing || peer.Pose.vehicle >= 0 || now < peer.HeldUntil)
                    continue;
                Vector3 center = peer.Pose.pos + Vector3.up * (peer.Pose.crouched ? .71f : 1.18f);
                float t = Vector3.Dot(center - origin, direction);
                if (t < .8f || t > distance)
                    continue;
                Vector3 closest = origin + direction * t;
                if ((center - closest).sqrMagnitude > 1.35f * 1.35f ||
                    Physics.Linecast(closest, center, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                peer.NextPressure = now + .46f;
                peer.Snapshot.pressureSequence++;
                peer.Snapshot.pressureSource = closest;
            }
        }

        private void BroadcastImpact(RaycastHit hit, bool broken)
        {
            impactFrame.point = hit.point;
            impactFrame.normal = hit.normal;
            impactFrame.material = (int)scenery.Resolve(hit.collider);
            impactFrame.broken = broken;
            string json = JsonUtility.ToJson(impactFrame);
            foreach (Peer peer in peers)
                if (peer.Ready && peer.Pose.playing && (peer.Pose.pos - hit.point).sqrMagnitude < 62 * 62)
                    peer.Wire.Send(json);
        }

        internal static float CapsuleEntry(Vector3 origin, Vector3 direction, float halfSegment, float radius)
        {
            float radiusSquared = radius * radius;
            float endpoint = Mathf.Clamp(origin.y, -halfSegment, halfSegment);
            if ((origin - Vector3.up * endpoint).sqrMagnitude <= radiusSquared)
                return 0;

            float closest = Mathf.Min(
                SphereEntry(origin - Vector3.up * halfSegment, direction, radiusSquared),
                SphereEntry(origin + Vector3.up * halfSegment, direction, radiusSquared));
            Vector2 horizontal = new Vector2(direction.x, direction.z);
            Vector2 flatOrigin = new Vector2(origin.x, origin.z);
            float a = horizontal.sqrMagnitude;
            float b = Vector2.Dot(flatOrigin, horizontal);
            float discriminant = b * b - a * (flatOrigin.sqrMagnitude - radiusSquared);
            if (a > 0.000001f && discriminant >= 0)
            {
                float entry = (-b - Mathf.Sqrt(discriminant)) / a;
                if (entry >= 0 && Mathf.Abs(origin.y + entry * direction.y) <= halfSegment)
                    closest = Mathf.Min(closest, entry);
            }
            return closest;
        }

        private static float SphereEntry(Vector3 offset, Vector3 direction, float radiusSquared)
        {
            float projection = Vector3.Dot(offset, direction);
            float discriminant = projection * projection - offset.sqrMagnitude + radiusSquared;
            if (discriminant < 0)
                return float.PositiveInfinity;
            float entry = -projection - Mathf.Sqrt(discriminant);
            return entry >= 0 ? entry : float.PositiveInfinity;
        }

        private bool MovementBlocked(Avatar avatar, Avatar pose, Vector3 direction, float distance)
        {
            float height = pose.rollPose >= 0 ? 1.0f : pose.crouched ? 1.16f : 1.8f;
            const float radius = 0.27f;
            Vector3 lower = avatar.pos + Vector3.up * 0.43f;
            Vector3 upper = avatar.pos + Vector3.up * (height - radius - 0.08f);
            return (Physics.CapsuleCast(lower, upper, radius, direction, out RaycastHit hit, distance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.distance + 0.10f < distance)
                || Physics.CheckCapsule(pose.pos + Vector3.up * 0.43f,
                    pose.pos + Vector3.up * (height - radius - 0.08f), radius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static void RaiseHeat(Peer peer, float now, bool combat)
        {
            peer.HeatUntil = now + HeatDecayTime;
            if (combat && now < peer.NextCrime)
                return;
            peer.NextCrime = now + 6f;
            peer.Pose.wantedLevel = Mathf.Min(MaxWanted, peer.Pose.wantedLevel + 1);
        }

        private void DecayHeat(Peer peer, float now)
        {
            Avatar avatar = peer.Pose;
            int floor = peer.Bag.Count > 0 ? 1 : 0;
            if (avatar.wantedLevel <= floor || now < peer.HeatUntil)
                return;
            foreach (Peer other in peers)
                if (other.Ready && other.Pose.playing && other.Pose.police
                    && Near(avatar.pos, other.Pose.pos, 22f) && ClearArrestLine(avatar.pos, other.Pose.pos))
                {
                    peer.HeatUntil = now + 5f;
                    return;
                }
            avatar.wantedLevel--;
            peer.HeatUntil = now + HeatDecayTime;
        }

        private static bool ClearArrestLine(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from + Vector3.up * 0.87f, to + Vector3.up * 0.87f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private Peer ArrestTarget(Peer officer, int id, float now)
        {
            if (!officer.Pose.police || !officer.Pose.playing || officer.Pose.vehicle >= 0 || now < officer.DoorUntil)
                return null;
            foreach (Peer target in peers)
            {
                if (target.Ready && target.Pose.id == id && target.Pose.playing && !target.Pose.police
                    && target.Pose.vehicle < 0 && target.Pose.rollPose < 0 && now >= target.DoorUntil && now >= target.HeldUntil
                    && now >= target.ArrestAfter && Near(target.Pose.pos, officer.Pose.pos, 2.65f)
                    && ClearArrestLine(officer.Pose.pos, target.Pose.pos))
                    return target;
            }
            return null;
        }

        private void BeginArrest(Peer officer, int id, float now)
        {
            if (officer.ArrestTarget == id || ArrestTarget(officer, id, now) == null)
                return;
            officer.ArrestTarget = id;
            officer.ArrestStarted = now;
        }

        private void AdvanceArrest(Peer officer, float now)
        {
            Peer target = ArrestTarget(officer, officer.ArrestTarget, now);
            if (target == null)
            {
                officer.ArrestTarget = 0;
                officer.Pose.arrestProgress = 0;
                return;
            }

            float elapsed = now - officer.ArrestStarted;
            officer.Pose.arrestProgress = Mathf.Clamp01(elapsed / ArrestTime);
            if (elapsed < ArrestTime)
            {
                target.Pose.arrestThreat = Mathf.Max(target.Pose.arrestThreat, officer.Pose.arrestProgress);
                return;
            }

            officer.ArrestTarget = 0;
            officer.Pose.arrestProgress = 0;
            if (officer.Wire != null)
                officer.Evidence += target.Bag.Count;
            target.Bag.Clear();
            target.Pose.seat = target.Pose.lift = -1;
            target.Pose.wantedLevel = 0;
            target.Pose.arrestThreat = 0;
            target.Pose.pos = Holding;
            target.Pose.yaw = 180;
            target.Pose.standing = true;
            target.Pose.jumpPose = target.Pose.rollPose = -1;
            target.Pose.crouched = target.Pose.moving = target.Pose.running = false;
            ResetMotion(target);
            target.Pose.revision++;
            target.HeldUntil = now + 7;
            target.ArrestAfter = now + 18;
            SetNotice(target, 3);
            SetNotice(officer, 4);
        }

        private void SetRole(Peer peer, bool police)
        {
            ReleaseVehicle(peer);
            peer.Pose.police = police;
            if (police && peer.NextPayroll == 0) peer.NextPayroll = Time.realtimeSinceStartup+300;
            peer.Pose.seat = peer.Pose.lift = -1;
            peer.DispatchSince = -1;
            peer.Pose.equipped = police ? 0 : HarborArsenal.Knife;
            peer.Pose.health = 100;
            peer.Pose.armor = police ? PoliceArmor : 0;
            Array.Clear(peer.UsedRounds, 0, peer.UsedRounds.Length);
            Array.Clear(peer.ReloadEnds, 0, peer.ReloadEnds.Length);
            peer.Pose.standing = true;
            peer.Pose.jumpPose = peer.Pose.rollPose = -1;
            peer.Pose.crouched = peer.Pose.moving = peer.Pose.running = false;
            peer.Pose.wantedLevel = !police && peer.Bag.Count > 0 ? 1 : 0;
            peer.ArrestTarget = 0;
            peer.Pose.arrestProgress = 0;
            peer.Pose.pos = police ? PoliceSpawn + Vector3.right * (peer.Pose.id % 4) : FindCriminalSpawn();
            peer.Pose.yaw = police ? 180 : 350;
            ResetMotion(peer);
            peer.Pose.revision++;
            SetNotice(peer, police ? 2 : 9);
        }

        private Vector3 FindCriminalSpawn()
        {
            Vector3 best = CriminalSpawn;
            float bestClearance = -1f;
            for (int lane = 0; lane < 5; lane++)
            {
                for (int row = 0; row < 6; row++)
                {
                    Vector3 point = CriminalSpawn + new Vector3(-4 - lane * 1.7f, 0, 9 + row * 1.7f);
                    if (Physics.CheckCapsule(point + Vector3.up * 0.42f, point + Vector3.up * 1.35f, 0.39f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        continue;
                    float clearance = float.PositiveInfinity;
                    foreach (Peer peer in peers)
                        if (peer.Ready && peer.Pose != null)
                            clearance = Mathf.Min(clearance, (peer.Pose.pos - point).sqrMagnitude);
                    if (clearance <= bestClearance)
                        continue;
                    bestClearance = clearance;
                    best = point;
                }
            }
            return best;
        }

        private void Rebalance()
        {
            int total = CountPolice();
            int limit = AllowedPolice(CountPlayers());
            for (int i = peers.Count - 1; i >= 0 && total > limit; i--)
            {
                Peer peer = peers[i];
                if (peer.Ready && peer.Pose.police)
                {
                    SetRole(peer, false);
                    total--;
                }
            }
        }

        private int CountPlayers()
        {
            int total = 0;
            foreach (Peer peer in peers)
                if (peer.Ready) total++;
            return total;
        }

        private int CountPolice()
        {
            int total = 0;
            foreach (Peer peer in peers)
                if (peer.Ready && peer.Pose.police) total++;
            return total;
        }

        private void ReleaseVehicle(Peer peer)
        {
            if (peer.Pose == null || peer.Pose.vehicle < 0)
                return;

            Vehicle car = hostCars[peer.Pose.vehicle];
            if (car.driver == peer.Pose.id)
                car.driver = 0;
            peer.Pose.vehicle = -1;
        }

        private static bool ValidPoint(Vector3 point) => Finite(point.x) && Finite(point.y) && Finite(point.z)
            && point.y >= -0.9f && point.y <= 38
            && (point.x >= -153 && point.x <= 79 && point.z >= -65 && point.z <= 169 || CostaCity.OnRegionalLand(point));

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Near(Vector3 a, Vector3 b, float radius) =>
            Mathf.Abs(a.y - b.y) < 1.5f && (a - b).sqrMagnitude <= radius * radius;

        internal static bool AtVendor(Vector3 point)
        {
            foreach (Vector3 vendor in CostaCity.Vendors)
                if (Near(point, vendor, 2.45f)) return true;
            return false;
        }

        private static void SetNotice(Peer peer, int code)
        {
            peer.Wire?.Send(JsonUtility.ToJson(new Frame { type = "notice", protocol = Protocol, notice = code }));
        }

        private static string FormatNotice(int code)
        {
            switch (code)
            {
                case 1: return L("All police slots are filled.", "Limite de policiais atingido.");
                case 2: return L("Police kit equipped.", "Equipamento policial liberado.");
                case 3: return L("Arrested. Items confiscated.", "Você foi preso. Itens confiscados.");
                case 4: return L("Suspect arrested.", "Suspeito preso.");
                case 5: return L("Loot added to backpack.", "Saque colocado na mochila.");
                case 6: return L("Not enough space.", "Espaço insuficiente na mochila.");
                case 7: return L("Items sold.", "Itens vendidos.");
                case 8: return L("Weapon purchased.", "Arma comprada.");
                case 9: return L("Criminal role assigned.", "Função de criminoso atribuída.");
                case 10: return L("You were hit.", "Você foi atingido.");
                case 11: return L("You respawned.", "Você voltou à cidade.");
                case 12: return L("Clear your wanted status before joining police.", "Deixe de ser procurado antes de entrar na polícia.");
                case 13: return L("Alarm triggered. Stay at the display to collect the loot.", "Alarme disparado. Fique na vitrine para pegar o saque.");
                case 14: return L("Robbery interrupted.", "Roubo interrompido.");
                case 15: return L("Police assignment complete. Reward paid.", "Ocorrência concluída. Recompensa recebida.");
                case 16: return L("Recovered items delivered. Reward paid.", "Itens recuperados entregues. Recompensa recebida.");
                case 17: return L("False alarm confirmed. Reward paid.", "Alarme falso verificado. Recompensa recebida.");
                case 18: return L("Lookout assigned. Hold the entrance.", "Você é vigia. Cubra a entrada.");
                case 19: return L("Driver assigned. Wait at the marked point.", "Você é motorista. Aguarde no ponto marcado.");
                case 20: return L("Loot handed to getaway driver.", "Saque entregue ao motorista.");
                case 21: return L("Crew sale complete. Bonus paid.", "Venda da equipe concluída. Bônus recebido.");
                case 22: return L("Preparing the robbery. Stay near the display.", "Preparando o roubo. Fique perto da vitrine.");
                case 36: return L("Transaction complete.", "Transação concluída.");
                case 37: return L("Transaction declined.", "Transação recusada.");
                case 38: return L("Bank transfer received.", "Transferência bancária recebida.");
                case 41: return L("Bank is saving another operation. Try again.", "O banco está salvando outra operação. Tente novamente.");
                case 40: return L("Police salary deposited.","Salário policial depositado.");
                case 39: return L("Campaign checkpoint saved.","Progresso da campanha salvo.");
                default: return CostaInteriors.Notice(code);
            }
        }

        private static Frame Parse(string text)
        {
            try { return JsonUtility.FromJson<Frame>(text); }
            catch (ArgumentException) { return null; }
        }

        private void LoadIdentity()
        {
            clientIdentity = new RSACryptoServiceProvider(2048);
            const string key = "CostaResumeIdentity";
            string saved = PlayerPrefs.GetString(key, "");
            if (saved.Length != 0)
                clientIdentity.FromXmlString(saved);
            else
            {
                PlayerPrefs.SetString(key, clientIdentity.ToXmlString(true));
                PlayerPrefs.DeleteKey("CostaResumeKey");
                PlayerPrefs.Save();
            }
        }

        private bool SendIdentity(string challenge)
        {
            if (identitySent || clientIdentity == null || challenge == null || challenge.Length != 44)
                return false;
            try
            {
                byte[] nonce = Convert.FromBase64String(challenge);
                if (nonce.Length != 32)
                    return false;
                identitySent = true;
                RSAParameters identityKey = clientIdentity.ExportParameters(false);
                Send(new Frame
                {
                    type = "hello", protocol = Protocol, name = playerName,
                    publicKey = Convert.ToBase64String(identityKey.Modulus),
                    signature = Convert.ToBase64String(clientIdentity.SignData(nonce, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                });
                return true;
            }
            catch (Exception error) when (error is FormatException || error is CryptographicException)
            {
                return false;
            }
        }

        private static bool VerifyIdentity(byte[] nonce, Frame frame, out string key)
        {
            key = null;
            if (frame.type != "hello" || frame.protocol != Protocol || nonce == null
                || frame.publicKey?.Length != 344 || frame.signature?.Length != 344)
                return false;

            try
            {
                byte[] modulus = Convert.FromBase64String(frame.publicKey);
                byte[] signature = Convert.FromBase64String(frame.signature);
                byte[] exponent = { 1, 0, 1 };
                if (modulus.Length != 256 || signature.Length != 256)
                    return false;
                using var verifier = new RSACryptoServiceProvider();
                verifier.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
                if (!verifier.VerifyData(nonce, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    return false;
                key = Convert.ToBase64String(modulus);
                return true;
            }
            catch (Exception error) when (error is FormatException || error is CryptographicException)
            {
                return false;
            }
        }

        private static string SafeName(string value)
        {
            string result = "";
            if (value != null)
                foreach (char ch in value)
                {
                    if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_')
                        result += ch;
                    if (result.Length == 20)
                        break;
                }
            result = result.Trim();
            return result.Length > 0 ? result : "Santa Luz";
        }

        private void Send(Frame frame) => connection?.Send(JsonUtility.ToJson(frame));

        internal void Request(string type, int index = 0) => Send(new Frame { type = type, index = index });

        internal void Equip(int weapon, int sequence) =>
            Send(new Frame { type = "equip", index = weapon, equipSequence = sequence });

        internal void Fire(int weapon, Vector3 direction, Avatar pose) =>
            Send(new Frame { type = "fire", index = weapon, direction = direction, pose = pose });

        internal void ExitVehicle(int side) => Request("exit", side);

        private static async Task DiscardConnection(Task<CostaWire> task)
        {
            try
            {
                (await task.ConfigureAwait(false)).Dispose();
            }
            catch (Exception exception) when (CostaWire.NetworkException(exception) || exception is NotSupportedException)
            {
            }
        }

        public void Dispose()
        {
            interiors?.UseLocalWorld(false);
            clientIdentity?.Dispose();
            clientIdentity = null;
            identitySent = false;
            if (listener != null)
            {
                listener.Stop();
                listener = null;
                foreach (CostaVehicle vehicle in sceneVehicles)
                    vehicle.FollowPresentation();
            }
            connection?.Dispose();
            connection = null;
            connectionCancellation?.Cancel();
            connectionCancellation?.Dispose();
            connectionCancellation = null;
            if (connecting != null)
                _ = DiscardConnection(connecting);
            connecting = null;
            internetRoom?.Dispose();
            internetRoom = null;
            norayRoom?.Dispose();
            norayRoom = null;
            RelayAddress = "";
            foreach (Peer peer in peers)
                peer.Wire.Dispose();
            peers.Clear();
            active.Clear();
            disconnected.Clear();
            expiredResumes.Clear();
            Array.Clear(restock, 0, restock.Length);
            Array.Clear(robbers, 0, robbers.Length);
            Alarms = null;
            ResetActivities();
            scenery?.Reset();
            traffic?.Reset();
            trafficTarget = null;
            cityEventTarget = null;
            world.traffic = null;
            world.cityEvent = null;
            surfaceEvents.Clear();
            lastHarm = lastPressure = 0;
            Harm = default;
            Pressure = null;
            Players = Array.Empty<Avatar>();
            Cars = null;
            ResetInterest();
            Sites = null;
            Local = null;
            Bag.Clear();
            nextIdentity = 0;
            identity = 0;
            ledger?.FinishBank(wait:true);
            ledger = null;
            nextTransaction = BankPersonal = BankBusiness = 0;
            StaminaRefill = 0;
            BankHistory = null;
            Narrative = null;
            nextPose = 0;
            nextSnapshot = 0;
            hasWorldHour = false;
            noticeEvents.Clear();
            noticeUntil = 0;
            status = "";
        }
    }
}
