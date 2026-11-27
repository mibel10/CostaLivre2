using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CostaLivre
{
    internal sealed partial class CostaSession
    {
        [Serializable]
        internal sealed class RosterEntry
        {
            public int id;
            public string name;
        }

        private readonly List<RosterEntry> roster = new(MaxPlayers);
        private RosterEntry[] bankRoster = Array.Empty<RosterEntry>();

        private void UpdateRoster()
        {
            roster.Clear();
            foreach(Peer peer in peers)
                if(peer.Ready)
                {
                    peer.Roster.id=peer.Pose.id;
                    peer.Roster.name=peer.Pose.name;
                    roster.Add(peer.Roster);
                }
            Copy(roster,ref world.roster);
        }

        private readonly CostaInterest interest = new();
        private readonly List<int> nearby = new(32);
        private readonly List<Avatar> relevantPlayers = new(MaxPlayers+1);
        private readonly List<Vehicle> relevantCars = new(16);
        private readonly List<int> relevantIds = new(16);
        private readonly List<int> presentIds = new(16);
        private Vehicle[] vehicleCache;
        private bool[] carPresent;
        private float lastTrafficSample;
        private int trafficBytes;
        private float nextTrafficReport;
        internal int NetworkEntities { get; private set; }
        internal int NetworkBytesPerSecond { get; private set; }
        internal float NetworkRoundTrip { get; private set; }
        private int nextPingSequence;
        private float nextPing;
        private float pingSent;

        private static void Copy<T>(List<T> source, ref T[] destination)
        {
            if (destination?.Length != source.Count)
                destination = new T[source.Count];
            source.CopyTo(destination);
        }

        private void IndexEntities()
        {
            interest.Begin(active.Count+hostCars.Length);
            foreach (Avatar avatar in active) interest.Add(avatar.pos);
            foreach (Vehicle vehicle in hostCars) interest.Add(vehicle.pos);
        }

        private void FilterSnapshot(Peer peer,float now)
        {
            interest.Query(peer.Pose.pos,nearby);
            relevantPlayers.Clear(); relevantCars.Clear(); relevantIds.Clear(); presentIds.Clear();
            uint visible = 0;
            foreach(int index in nearby)
            {
                if(index < active.Count) relevantPlayers.Add(active[index]);
                else
                {
                    int id = index-active.Count;
                    visible |= 1u<<id;
                }
            }
            if (peer.Pose.vehicle >= 0) visible |= 1u<<peer.Pose.vehicle;
            if (peer.Pose.doorCar >= 0) visible |= 1u<<peer.Pose.doorCar;
            foreach (Avatar avatar in relevantPlayers)
            {
                if (avatar.vehicle >= 0) visible |= 1u<<avatar.vehicle;
                if (avatar.doorCar >= 0) visible |= 1u<<avatar.doorCar;
            }
            bool refresh = now >= peer.NextFullCars;
            if (refresh) peer.NextFullCars = now + 1.5f;
            for(int i=0;i<hostCars.Length;i++)
            {
                uint mask = 1u<<i;
                if ((visible&mask)==0) continue;
                presentIds.Add(i);
                Vehicle car=hostCars[i];
                bool entering=(peer.VisibleCars&mask)==0;
                bool unchanged=peer.CarStamps[i].Matches(car);
                if (!refresh && !entering && unchanged && car.speed==0 && car.driver==0)
                    continue;
                bool distant=(car.pos-peer.Pose.pos).sqrMagnitude>88*88;
                if (!refresh && !entering && distant && car.speed!=0 && car.driver==0
                    && car.tune==peer.CarStamps[i].Tune && now < peer.CarStamps[i].SentAt+.25f)
                    continue;
                peer.CarStamps[i] = new CarStamp(car,now);
                relevantIds.Add(i);
                relevantCars.Add(car);
            }
            peer.VisibleCars=visible;
            Copy(relevantPlayers,ref peer.Snapshot.players);
            Copy(relevantCars,ref peer.Snapshot.vehicles);
            Copy(relevantIds,ref peer.Snapshot.vehicleIds);
            Copy(presentIds,ref peer.Snapshot.visibleCars);
        }

        private readonly struct CarStamp
        {
            private readonly Vector3 position;
            private readonly float yaw, front, rear, speed;
            internal readonly long Tune;
            internal readonly float SentAt;
            private readonly int driver;
            internal CarStamp(Vehicle car,float now)
            {
                position=car.pos; yaw=car.yaw; front=car.frontDamage; rear=car.rearDamage; Tune=car.tune; driver=car.driver; speed=car.speed; SentAt=now;
            }
            internal bool Matches(Vehicle car) => position.Equals(car.pos) && yaw==car.yaw && front==car.frontDamage
                && rear==car.rearDamage && Tune==car.tune && driver==car.driver && speed==car.speed;
        }

        private bool ValidVehicleSlice(Frame frame)
        {
            if(frame.vehicles == null || frame.vehicleIds?.Length != frame.vehicles.Length
                || frame.visibleCars == null || frame.visibleCars.Length > sceneVehicles.Length
                || frame.humans is < 1 or > MaxPlayers || frame.policeCount is < 0 or > 4)
                return false;
            uint present=0, updated=0;
            foreach(int i in frame.visibleCars)
            {
                if(i<0 || i>=sceneVehicles.Length || (present&(1u<<i))!=0) return false;
                present |= 1u<<i;
            }
            for(int j=0;j<frame.vehicleIds.Length;j++)
            {
                int i = frame.vehicleIds[j];
                if(i<0 || i>=sceneVehicles.Length || (present&(1u<<i))==0 || (updated&(1u<<i))!=0)
                    return false;
                updated |= 1u<<i;
            }
            return true;
        }

        private void ReceiveVehicleSlice(Frame frame)
        {
            vehicleCache ??= new Vehicle[sceneVehicles.Length];
            carPresent ??= new bool[sceneVehicles.Length];
            Array.Clear(carPresent,0,carPresent.Length);
            foreach(int i in frame.visibleCars) carPresent[i]=true;
            for(int j=0;j<frame.vehicleIds.Length;j++) vehicleCache[frame.vehicleIds[j]]=frame.vehicles[j];
            Cars=vehicleCache;
            NetworkEntities=frame.players.Length+frame.visibleCars.Length;
            networkHumans=frame.humans;
            networkPolice=frame.policeCount;
            bankRoster=frame.roster;
        }

        internal bool CarRelevant(int i) => carPresent != null && carPresent[i] && vehicleCache[i]!=null;
        private int networkHumans, networkPolice;

        private void TrackNetwork(string text,float now)
        {
            trafficBytes += Encoding.UTF8.GetByteCount(text)+4;
            if(now>=nextTrafficReport)
            {
                if(lastTrafficSample>0)
                    NetworkBytesPerSecond=Mathf.RoundToInt(trafficBytes/Mathf.Max(.1f,now-lastTrafficSample));
                trafficBytes=0;
                lastTrafficSample=now;
                nextTrafficReport=now+1;
            }
        }

        private void ResetInterest()
        {
            vehicleCache=null; carPresent=null;
            bankRoster=Array.Empty<RosterEntry>();
            nextPingSequence=0; nextPing=0; pingSent=0;
            networkHumans=networkPolice=NetworkEntities=NetworkBytesPerSecond=0;
            NetworkRoundTrip=0;
            lastTrafficSample=nextTrafficReport=0;
            trafficBytes=0;
        }
    }
}
