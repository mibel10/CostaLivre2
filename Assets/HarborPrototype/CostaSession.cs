using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaSession : IDisposable
    {
        internal const int DefaultPort = 7777;
        internal const int MaxPlayers = 16;
        private const int Protocol = 2;
        private const float Interval = 0.083f;
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
            public float jumpPose = -1;
            public float rollPose = -1;
            public int equipped = -1;
            public int shotNumber;
            public int shotWeapon = -1;
            public int health = 100;
            public bool wanted;
            public int outfit;
            public int vehicle = -1;
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
            public float lean;
        }

        [Serializable]
        private sealed class Frame
        {
            public string type;
            public int protocol;
            public int index;
            public int id;
            public int credits;
            public int weapons;
            public int notice;
            public int noticeVersion;
            public string name;
            public string resumeKey;
            public Avatar pose;
            public Avatar[] players;
            public Vehicle[] vehicles;
            public int[] bag;
            public bool[] sites;
            public Vector3 direction;
        }

        private sealed class Peer
        {
            internal CostaWire Wire;
            internal Avatar Pose;
            internal bool Ready;
            internal string ResumeKey;
            internal int Credits = 4500;
            internal int Weapons;
            internal int Notice;
            internal int NoticeVersion;
            internal float LastPacket;
            internal float LastPose;
            internal float HeldUntil;
            internal float ArrestAfter;
            internal float NextShot;
            internal float RespawnProtection;
            internal readonly int[] UsedRounds = new int[HarborArsenal.WeaponNames.Length];
            internal readonly float[] ReloadEnds = new float[HarborArsenal.WeaponNames.Length];
            internal readonly CostaBag Bag = new();
        }

        private readonly List<Peer> peers = new();
        private readonly Dictionary<string, Peer> disconnected = new();
        private readonly string resumeKey = LoadResumeKey();
        private readonly CostaVehicle[] sceneVehicles;
        private readonly float[] restock = new float[CostaRobberies.Sites.Length];
        private readonly int[] initialHomes;
        private TcpListener listener;
        private Vehicle[] hostCars;
        private CostaWire connection;
        private Task connecting;
        private TcpClient pendingClient;
        private float connectDeadline;
        private int identity;
        private int nextIdentity;
        private float nextPose;
        private float nextSnapshot;
        private float nextWatchdog;
        private float lastClock;
        private int seenNotice;
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
        internal bool Online => Local != null && connection != null && !connection.Closed;
        internal bool Connecting => connecting != null || (connection != null && Local == null);
        internal bool Hosting => listener != null;
        internal string Status => status;
        internal string Notice { get; private set; } = "";
        internal string CurrentNotice => Time.unscaledTime < noticeUntil ? Notice : "";
        internal int PoliceCount
        {
            get
            {
                int total = 0;
                foreach (Avatar player in Players)
                    if (player.police) total++;
                return total;
            }
        }
        internal int PoliceLimit => AllowedPolice(Players.Length);
        internal static int AllowedPolice(int players) => Mathf.Min(4, Mathf.Max(0, players) * 3 / 10);

        internal CostaSession(CostaVehicle[] vehicles)
        {
            sceneVehicles = vehicles;
            initialHomes = new int[vehicles.Length];
            for (int i = 0; i < vehicles.Length; i++)
                initialHomes[i] = Mathf.RoundToInt(vehicles[i].Root.eulerAngles.y);
        }

        internal void Start(string name, string address, int port, bool host)
        {
            Dispose();
            if (port < 1 || port > 65535 || (!host && (!IPAddress.TryParse(address, out IPAddress ip)
                || ip.AddressFamily != AddressFamily.InterNetwork)))
            {
                status = L("Enter a valid IPv4 address and port.", "Informe um IPv4 e uma porta válidos.");
                return;
            }

            playerName = SafeName(name);
            requestedHost = host;
            try
            {
                if (host)
                {
                    listener = new TcpListener(IPAddress.Any, port);
                    listener.Start(MaxPlayers);
                    hostCars = new Vehicle[sceneVehicles.Length];
                    for (int i = 0; i < hostCars.Length; i++)
                        hostCars[i] = new Vehicle { pos = sceneVehicles[i].Home, yaw = initialHomes[i] };
                }
                pendingClient = new TcpClient(AddressFamily.InterNetwork);
                connecting = pendingClient.ConnectAsync(host ? IPAddress.Loopback : IPAddress.Parse(address), port);
                connectDeadline = Time.unscaledTime + 8f;
                status = L("Connecting…", "Conectando…");
                lastClock = Time.unscaledTime;
            }
            catch (SocketException)
            {
                Dispose();
                status = L("Could not open that connection.", "Não foi possível abrir essa conexão.");
            }
        }

        internal void Tick(Avatar localPose)
        {
            float now = Time.unscaledTime;
            float delta = Mathf.Clamp(now - lastClock, 0, 0.10f);
            lastClock = now;

            if (listener != null)
                TickHost(now, delta);
            TickClient(now, localPose);
        }

        private void TickClient(float now, Avatar localPose)
        {
            if (connecting != null)
            {
                if (connecting.Status == TaskStatus.RanToCompletion)
                {
                    connecting = null;
                    connection = new CostaWire(pendingClient);
                    pendingClient = null;
                    Send(new Frame { type = "hello", protocol = Protocol, name = playerName, resumeKey = resumeKey });
                    nextWatchdog = now + 8f;
                }
                else if (connecting.IsFaulted || connecting.IsCanceled || now >= connectDeadline)
                {
                    Dispose();
                    status = L("Connection failed.", "A conexão falhou.");
                    return;
                }
            }

            if (connection == null)
                return;

            while (connection.Receive(out string text))
            {
                Frame frame = Parse(text);
                if (frame == null || frame.type != "state" || frame.protocol != Protocol || frame.players == null
                    || frame.players.Length > MaxPlayers || frame.vehicles == null || frame.vehicles.Length != sceneVehicles.Length
                    || Array.Exists(frame.players, avatar => avatar == null || avatar.vehicle < -1 || avatar.vehicle >= sceneVehicles.Length)
                    || Array.Exists(frame.vehicles, vehicle => vehicle == null))
                {
                    Dispose();
                    status = L("Incompatible session.", "Sessão incompatível.");
                    return;
                }

                identity = frame.id;
                Players = frame.players;
                Cars = frame.vehicles;
                Sites = frame.sites;
                Local = null;
                foreach (Avatar avatar in Players)
                    if (avatar != null && avatar.id == identity)
                        Local = avatar;
                if (Local == null)
                    continue;

                Cash = frame.credits;
                WeaponMask = frame.weapons;
                Bag.Replace(frame.bag);
                nextWatchdog = now + 9f;
                status = requestedHost ? L("Hosting", "Hospedando") : L("Connected", "Conectado");
                if (frame.noticeVersion != seenNotice)
                {
                    seenNotice = frame.noticeVersion;
                    Notice = FormatNotice(frame.notice);
                    noticeUntil = now + 4.2f;
                }
            }

            if (connection.Closed || now >= nextWatchdog)
            {
                Dispose();
                status = L("Disconnected.", "Conexão encerrada.");
                return;
            }

            if (Online && localPose != null && now >= nextPose)
            {
                nextPose = now + Interval;
                Send(new Frame { type = "pose", pose = localPose });
            }
        }

        private void TickHost(float now, float delta)
        {
            for (int i = 0; i < 3 && listener.Pending(); i++)
            {
                TcpClient client = listener.AcceptTcpClient();
                if (peers.Count >= MaxPlayers)
                    client.Close();
                else
                    peers.Add(new Peer { Wire = new CostaWire(client), LastPacket = now });
            }

            bool left = false;
            for (int index = peers.Count - 1; index >= 0; index--)
            {
                Peer peer = peers[index];
                if (peer.Wire.Closed || now - peer.LastPacket > 10f)
                {
                    ReleaseVehicle(peer);
                    peer.Wire.Dispose();
                    peers.RemoveAt(index);
                    left |= peer.Ready;
                    if (peer.Ready)
                        disconnected[peer.ResumeKey] = peer;
                    continue;
                }
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
                        if (message.type != "hello" || message.protocol != Protocol
                            || !Guid.TryParseExact(message.resumeKey, "N", out _)
                            || peers.Exists(other => other.Ready && other.ResumeKey == message.resumeKey))
                        {
                            peer.Wire.Dispose();
                            break;
                        }
                        peer.ResumeKey = message.resumeKey;
                        if (disconnected.TryGetValue(peer.ResumeKey, out Peer saved))
                        {
                            disconnected.Remove(peer.ResumeKey);
                            peer.Pose = saved.Pose;
                            peer.Pose.name = SafeName(message.name);
                            peer.Pose.playing = false;
                            peer.Credits = saved.Credits;
                            peer.Weapons = saved.Weapons;
                            peer.Bag.Replace(saved.Bag.Amounts);
                            peer.HeldUntil = saved.HeldUntil;
                            peer.ArrestAfter = saved.ArrestAfter;
                            peer.NextShot = saved.NextShot;
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
                        Handle(peer, message, now);
                    }
                }
            }

            if (left)
                Rebalance();

            Physics.SyncTransforms();
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
                vehicle.lean = Mathf.MoveTowards(vehicle.lean, 0, 80 * delta);
                vehicle.speed = safeTravel < travel ? 0 : Mathf.MoveTowards(vehicle.speed, 0, 4.5f * delta);
            }

            if (now < nextSnapshot)
                return;
            nextSnapshot = now + Interval;
            List<Avatar> active = new();
            foreach (Peer peer in peers)
            {
                if (!peer.Ready)
                    continue;
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
                active.Add(peer.Pose);
            }
            Avatar[] players = active.ToArray();
            var ready = new bool[restock.Length];
            for (int i = 0; i < ready.Length; i++)
                ready[i] = now >= restock[i];
            foreach (Peer peer in peers)
            {
                if (!peer.Ready)
                    continue;
                var snapshot = new Frame
                {
                    type = "state", protocol = Protocol, id = peer.Pose.id, players = players,
                    vehicles = hostCars, sites = ready, credits = peer.Credits,
                    weapons = peer.Pose.police ? PoliceKit : peer.Weapons, bag = peer.Bag.Amounts,
                    notice = peer.Notice, noticeVersion = peer.NoticeVersion
                };
                peer.Wire.Send(JsonUtility.ToJson(snapshot));
            }
        }

        private void Handle(Peer peer, Frame msg, float now)
        {
            Avatar avatar = peer.Pose;
            if (msg.type == "pose")
            {
                if (msg.pose != null && msg.pose.revision == avatar.revision)
                {
                    avatar.playing |= msg.pose.playing;
                    if (avatar.playing && now >= peer.HeldUntil)
                        UpdatePose(peer, msg.pose, now);
                }
                return;
            }
            if (now < peer.HeldUntil && msg.type != "outfit")
                return;

            if (!avatar.playing && msg.type != "police" && msg.type != "outfit")
                return;

            switch (msg.type)
            {
                case "police":
                    if (avatar.police)
                    {
                        SetRole(peer, false);
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
                case "loot":
                    if (!avatar.police && avatar.vehicle < 0 && msg.index >= 0 && msg.index < restock.Length
                        && Near(avatar.pos, CostaRobberies.Sites[msg.index].Point, 2.55f)
                        && now >= restock[msg.index])
                    {
                        if (peer.Bag.AddLoot(msg.index))
                        {
                            restock[msg.index] = now + 90;
                            avatar.wanted = true;
                            SetNotice(peer, 5);
                        }
                        else
                        {
                            SetNotice(peer, 6);
                        }
                    }
                    break;
                case "sell":
                    if (!avatar.police && avatar.vehicle < 0 && AtVendor(avatar.pos))
                    {
                        int amount = peer.Bag.Sell(msg.index);
                        peer.Credits += amount;
                        avatar.wanted = peer.Bag.Count > 0;
                        if (amount > 0)
                            SetNotice(peer, 7);
                    }
                    break;
                case "buy":
                    if (!avatar.police && avatar.vehicle < 0 && Near(avatar.pos, CostaCity.GunCounter, 2.35f)
                        && msg.index >= 0 && msg.index < CostaJobs.WeaponPrices.Length
                        && (peer.Weapons & (1 << msg.index)) == 0 && peer.Credits >= CostaJobs.WeaponPrices[msg.index])
                    {
                        peer.Credits -= CostaJobs.WeaponPrices[msg.index];
                        peer.Weapons |= 1 << msg.index;
                        SetNotice(peer, 8);
                    }
                    break;
                case "fire":
                    Fire(peer, msg.index, msg.direction, now);
                    break;
                case "reload":
                    if (CanShoot(peer, msg.index) && peer.UsedRounds[msg.index] > 0 && peer.ReloadEnds[msg.index] == 0)
                        peer.ReloadEnds[msg.index] = now + HarborArsenal.ShotRules(msg.index).reload;
                    break;
                case "arrest":
                    TryArrest(peer, msg.index, now);
                    break;
                case "enter":
                    if (avatar.vehicle < 0 && msg.index >= 0 && msg.index < hostCars.Length)
                    {
                        Vehicle car = hostCars[msg.index];
                        if (car.driver == 0 && Near(car.pos, avatar.pos, 4.0f)
                            && (!Physics.Linecast(avatar.pos + Vector3.up * 0.8f, car.pos + Vector3.up * 0.8f,
                                out RaycastHit obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                || obstruction.transform == sceneVehicles[msg.index].Root))
                        {
                            car.driver = avatar.id;
                            avatar.vehicle = msg.index;
                            avatar.pos = car.pos;
                            avatar.revision++;
                        }
                    }
                    break;
                case "exit":
                    if (avatar.vehicle >= 0 && msg.pose != null && ValidPoint(msg.pose.pos))
                    {
                        Vehicle car = hostCars[avatar.vehicle];
                        if (Near(car.pos, msg.pose.pos, 4.6f))
                        {
                            ReleaseVehicle(peer);
                            avatar.pos = msg.pose.pos;
                            avatar.yaw = car.yaw;
                            avatar.revision++;
                        }
                    }
                    break;
            }
        }

        private void UpdatePose(Peer peer, Avatar pose, float now)
        {
            if (pose == null || pose.revision != peer.Pose.revision || !ValidPoint(pose.pos)
                || !Finite(pose.yaw) || now - peer.LastPose < 0.035f)
                return;

            Avatar avatar = peer.Pose;
            float speedLimit = avatar.vehicle >= 0 ? 48 : 13;
            float elapsed = Mathf.Clamp(now - peer.LastPose, 0.03f, 1.25f);
            if (Vector3.Distance(avatar.pos, pose.pos) > elapsed * speedLimit + 0.9f)
                return;

            peer.LastPose = now;
            avatar.pos = pose.pos;
            avatar.yaw = Mathf.Repeat(pose.yaw, 360);
            avatar.crouched = pose.crouched && avatar.vehicle < 0;
            avatar.standing = pose.standing && !avatar.crouched && avatar.vehicle < 0;
            avatar.moving = pose.moving;
            avatar.running = pose.running;
            avatar.equipped = CanShoot(peer, pose.equipped) ? pose.equipped : -1;
            avatar.jumpPose = Finite(pose.jumpPose) ? Mathf.Clamp(pose.jumpPose, -1, 1) : -1;
            avatar.rollPose = Finite(pose.rollPose) ? Mathf.Clamp(pose.rollPose, -1, 1) : -1;
            if (avatar.vehicle >= 0)
            {
                Vehicle car = hostCars[avatar.vehicle];
                car.pos = new Vector3(pose.pos.x, 0, pose.pos.z);
                car.yaw = avatar.yaw;
                car.speed = Mathf.Clamp(pose.held / 10f, -8, 42);
                car.lean = sceneVehicles[avatar.vehicle].IsBike && Finite(pose.bikeLean)
                    ? Mathf.Clamp(pose.bikeLean, -24, 24) : 0;
            }
        }

        private static bool CanShoot(Peer peer, int index)
        {
            return peer.Pose.playing && peer.Pose.vehicle < 0 && index >= 0 && index < HarborArsenal.WeaponNames.Length
                && ((peer.Pose.police ? PoliceKit : peer.Weapons) & (1 << index)) != 0;
        }

        private void Fire(Peer shooter, int weapon, Vector3 direction, float now)
        {
            if (!CanShoot(shooter, weapon) || now + 0.025f < shooter.NextShot
                || !Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z)
                || direction.sqrMagnitude < 0.85f || direction.sqrMagnitude > 1.15f)
                return;
            var rules = HarborArsenal.ShotRules(weapon);
            if (shooter.UsedRounds[weapon] >= rules.magazine || shooter.ReloadEnds[weapon] > 0)
                return;
            shooter.UsedRounds[weapon]++;
            shooter.NextShot = now + rules.interval;
            shooter.Pose.shotWeapon = weapon;
            shooter.Pose.shotNumber++;
            direction.Normalize();

            Vector3 origin = shooter.Pose.pos + Vector3.up * (shooter.Pose.crouched ? 0.90f : 1.22f);
            float closest = Physics.Raycast(origin, direction, out RaycastHit cover, rules.range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? cover.distance : rules.range;
            Peer victim = null;
            foreach (Peer candidate in peers)
            {
                Avatar avatar = candidate.Pose;
                if (!candidate.Ready || candidate == shooter || !avatar.playing || avatar.vehicle >= 0
                    || now < candidate.HeldUntil || now < candidate.RespawnProtection)
                    continue;
                Vector3 center = avatar.pos + Vector3.up * (avatar.crouched ? 0.59f : 0.91f);
                float distance = Vector3.Dot(center - origin, direction);
                Vector3 point = origin + direction * distance;
                float height = avatar.crouched ? 0.24f : 0.56f;
                center.y += Mathf.Clamp(point.y - center.y, -height, height);
                if (distance > 0 && distance < closest && (point - center).sqrMagnitude <= 0.32f * 0.32f)
                {
                    victim = candidate;
                    closest = distance;
                }
            }

            if (victim == null)
                return;
            victim.Pose.health -= rules.damage;
            SetNotice(victim, 10);
            if (victim.Pose.health <= 0)
            {
                victim.Pose.health = 100;
                victim.Pose.pos = victim.Pose.police ? PoliceSpawn : FindCriminalSpawn();
                victim.Pose.revision++;
                victim.RespawnProtection = now + 3;
                SetNotice(victim, 11);
            }
        }

        private void TryArrest(Peer officer, int id, float now)
        {
            if (!officer.Pose.police || officer.Pose.vehicle >= 0)
                return;
            foreach (Peer target in peers)
            {
                if (!target.Ready || target.Pose.id != id || !target.Pose.playing || target.Pose.police || target.Pose.vehicle >= 0
                    || now < target.HeldUntil || now < target.ArrestAfter)
                    continue;

                if (!Near(target.Pose.pos, officer.Pose.pos, 2.65f)
                    || Physics.Linecast(officer.Pose.pos + Vector3.up * 0.87f, target.Pose.pos + Vector3.up * 0.87f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return;

                target.Bag.Clear();
                target.Pose.wanted = false;
                target.Pose.pos = Holding;
                target.Pose.yaw = 180;
                target.Pose.standing = true;
                target.Pose.jumpPose = target.Pose.rollPose = -1;
                target.Pose.crouched = target.Pose.moving = target.Pose.running = false;
                target.Pose.revision++;
                target.HeldUntil = now + 7;
                target.ArrestAfter = now + 18;
                SetNotice(target, 3);
                SetNotice(officer, 4);
                return;
            }
        }

        private void SetRole(Peer peer, bool police)
        {
            ReleaseVehicle(peer);
            peer.Pose.police = police;
            peer.Pose.equipped = -1;
            peer.Pose.health = 100;
            Array.Clear(peer.UsedRounds, 0, peer.UsedRounds.Length);
            Array.Clear(peer.ReloadEnds, 0, peer.ReloadEnds.Length);
            peer.Pose.standing = true;
            peer.Pose.jumpPose = peer.Pose.rollPose = -1;
            peer.Pose.crouched = peer.Pose.moving = peer.Pose.running = false;
            peer.Pose.wanted = !police && peer.Bag.Count > 0;
            peer.Pose.pos = police ? PoliceSpawn + Vector3.right * (peer.Pose.id % 4) : FindCriminalSpawn();
            peer.Pose.yaw = police ? 180 : 350;
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
            && point.x >= -153 && point.x <= 79 && point.y >= -0.9f && point.y <= 24 && point.z >= -65 && point.z <= 169;

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
            peer.Notice = code;
            peer.NoticeVersion++;
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
                default: return "";
            }
        }

        private static Frame Parse(string text)
        {
            try { return JsonUtility.FromJson<Frame>(text); }
            catch (ArgumentException) { return null; }
        }

        private static string LoadResumeKey()
        {
            string value = PlayerPrefs.GetString("CostaResumeKey", "");
            if (!Guid.TryParseExact(value, "N", out _))
            {
                value = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("CostaResumeKey", value);
                PlayerPrefs.Save();
            }
            return value;
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

        internal void Fire(int weapon, Vector3 direction) =>
            Send(new Frame { type = "fire", index = weapon, direction = direction });

        internal void ExitVehicle(Vector3 point) => Send(new Frame { type = "exit", pose = new Avatar { pos = point } });

        public void Dispose()
        {
            listener?.Stop();
            listener = null;
            connection?.Dispose();
            connection = null;
            pendingClient?.Close();
            pendingClient = null;
            connecting = null;
            foreach (Peer peer in peers)
                peer.Wire.Dispose();
            peers.Clear();
            disconnected.Clear();
            Array.Clear(restock, 0, restock.Length);
            Players = Array.Empty<Avatar>();
            Cars = null;
            Sites = null;
            Local = null;
            Bag.Clear();
            nextIdentity = 0;
            identity = 0;
            nextPose = 0;
            nextSnapshot = 0;
            seenNotice = 0;
            noticeUntil = 0;
            status = "";
        }
    }
}
