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

        private void FilterSnapshot(Peer peer)
        {
            interest.Query(peer.Pose.pos,nearby);
            relevantPlayers.Clear(); relevantCars.Clear(); relevantIds.Clear();
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
            // The transport can replace a pending snapshot, so each vehicle slice
            // must stand alone, including stationary cars.
            for (int i = 0; i < hostCars.Length; i++)
            {
                if ((visible & (1u << i)) == 0)
                    continue;
                relevantIds.Add(i);
                relevantCars.Add(hostCars[i]);
            }
            Copy(relevantPlayers,ref peer.Snapshot.players);
            Copy(relevantCars,ref peer.Snapshot.vehicles);
            Copy(relevantIds,ref peer.Snapshot.vehicleIds);
        }

        private bool ValidVehicleSlice(Frame frame)
        {
            if (frame.vehicles == null || frame.vehicleIds?.Length != frame.vehicles.Length
                || frame.vehicles.Length > sceneVehicles.Length || frame.humans is < 1 or > MaxPlayers
                || frame.policeCount is < 0 or > 4)
                return false;
            uint present = 0;
            foreach (int i in frame.vehicleIds)
            {
                if (i < 0 || i >= sceneVehicles.Length || (present & (1u << i)) != 0)
                    return false;
                present |= 1u << i;
            }
            return true;
        }

        private void ReceiveVehicleSlice(Frame frame)
        {
            vehicleCache ??= new Vehicle[sceneVehicles.Length];
            carPresent ??= new bool[sceneVehicles.Length];
            Array.Clear(carPresent,0,carPresent.Length);
            for (int j = 0; j < frame.vehicleIds.Length; j++)
            {
                int index = frame.vehicleIds[j];
                carPresent[index] = true;
                vehicleCache[index] = frame.vehicles[j];
            }
            Cars=vehicleCache;
            NetworkEntities=frame.players.Length+frame.vehicleIds.Length;
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
