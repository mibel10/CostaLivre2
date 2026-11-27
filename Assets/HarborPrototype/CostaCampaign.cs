using System;
using System.Collections.Generic;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaCampaign
    {
        [Serializable] internal sealed class Progress
        {
            public string node;
            public int flags;
            public int journalist, mechanic, inspector;
            public string[] decisions = Array.Empty<string>();
        }

        [Serializable] internal sealed class Choice
        {
            public string id, en, pt, next;
            public int needs, earns, journalist, mechanic, inspector, pay;
            public int needTrust, contact;
            public string Caption => L(en,pt);
        }

        [Serializable] internal sealed class Node
        {
            public string id, anchor, en, pt, speaker, objective;
            public int act, loot = -1, ending;
            public string Title => L(en,pt);
            public Choice[] choices;
        }

        [Serializable] private sealed class Script
        {
            public int version;
            public string start;
            public Node[] nodes;
        }

        private readonly Dictionary<string,Node> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string,string> captions = new(StringComparer.Ordinal);
        private readonly string first;

        internal static CostaCampaign Load()
        {
            var file = Resources.Load<TextAsset>("Harbor/Campaign");
            return file == null ? null : new CostaCampaign(file.text);
        }

        internal CostaCampaign(string data)
        {
            var file = JsonUtility.FromJson<Script>(data);
            if (file?.version != 1 || file.nodes == null || file.nodes.Length == 0)
                throw new ArgumentException("Invalid campaign version or empty campaign.");
            first = file.start;
            var reachable = new HashSet<string>();
            var endings = new HashSet<int>();
            foreach (Node node in file.nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || !nodes.TryAdd(node.id,node)
                    || node.ending == 0 && (!TryPoint(node.anchor,out _) || node.choices == null || node.choices.Length == 0))
                    throw new ArgumentException("Invalid campaign node.");
                if (node.ending != 0 && !endings.Add(node.ending))
                    throw new ArgumentException("Duplicate ending.");
                foreach (Choice choice in node.choices ?? Array.Empty<Choice>())
                    if (choice == null || string.IsNullOrWhiteSpace(choice.id)
                        || !captions.TryAdd(choice.id,choice.Caption) || choice.pay is < 0 or > 20000
                        || choice.contact is < 0 or > 2)
                        throw new ArgumentException("Invalid campaign choice.");
            }
            var visiting = new HashSet<string>();
            void Visit(string id)
            {
                if (string.IsNullOrEmpty(id) || !nodes.TryGetValue(id,out Node node))
                    throw new ArgumentException("Unknown campaign destination: " + id);
                if (visiting.Contains(id))
                    throw new ArgumentException("Campaign path cycles at " + id);
                if (!reachable.Add(id)) return;
                visiting.Add(id);
                foreach (Choice choice in node.choices ?? Array.Empty<Choice>()) Visit(choice.next);
                visiting.Remove(id);
            }
            Visit(first);
            if (reachable.Count != nodes.Count || endings.Count < 5)
                throw new ArgumentException("Campaign requires every node to be reachable and at least five endings.");

            var visited = new HashSet<(string, int, int, int, int, int)>();
            var possibleEndings = new HashSet<int>();
            void CheckPaths(string id, int depth, Progress progress)
            {
                if (depth > 100)
                    throw new ArgumentException("Campaign path exceeds the saved decision limit.");
                if (!visited.Add((id,depth,progress.flags,progress.journalist,progress.mechanic,progress.inspector)))
                    return;
                if (visited.Count > 50000)
                    throw new ArgumentException("Campaign exceeds the path validation budget.");
                Node node = nodes[id];
                if (node.ending != 0)
                {
                    possibleEndings.Add(node.ending);
                    return;
                }
                bool canContinue = false;
                foreach (Choice choice in node.choices)
                {
                    if (!Available(choice,progress))
                        continue;
                    canContinue = true;
                    CheckPaths(choice.next,depth+1,Branch(progress,choice));
                }
                if (!canContinue)
                    throw new ArgumentException("No available choices at " + id);
            }
            CheckPaths(first,0,new Progress());
            if (!endings.SetEquals(possibleEndings))
                throw new ArgumentException("Campaign contains endings blocked by trust or flags.");
        }

        private static Progress Branch(Progress old, Choice choice) => new()
        {
            node = choice.next, flags = old.flags | choice.earns,
            journalist = old.journalist + choice.journalist,
            mechanic = old.mechanic + choice.mechanic,
            inspector = old.inspector + choice.inspector
        };

        internal Node Current(Progress progress) =>
            progress?.node != null && nodes.TryGetValue(progress.node,out var node) ? node : nodes[first];

        internal string Caption(string id) => id != null && captions.TryGetValue(id,out string caption) ? caption : id;

        internal static bool Available(Choice choice,Progress progress)
        {
            int flags = progress?.flags ?? 0;
            int trust = choice.contact switch
            {
                0 => progress?.journalist ?? 0,
                1 => progress?.mechanic ?? 0,
                _ => progress?.inspector ?? 0
            };
            return (flags & choice.needs) == choice.needs && trust >= choice.needTrust;
        }

        internal bool Checkpoint(Progress progress,Vector3 position,bool driving,CostaBag bag)
        {
            Node node = Current(progress);
            if (node.ending != 0 || !TryPoint(node.anchor,out Vector3 point) ||
                Mathf.Abs(position.y-point.y) > 1.2f || (position-point).sqrMagnitude > 4.2f*4.2f)
                return false;
            if (node.objective == "vehicle" && !driving || node.objective != "vehicle" && driving)
                return false;
            return node.objective != "loot" || bag != null && node.loot >= 0 && node.loot < bag.Amounts.Length && bag.Amounts[node.loot] > 0;
        }

        internal bool TryAdvance(CostaLedger ledger,CostaLedger.Account account, int index, string checkpoint, int serial,
            Vector3 position,bool driving,CostaBag bag)
        {
            Node node = Current(account.campaign);
            if (index < 0 || index >= (node.choices?.Length ?? 0) || checkpoint != node.id ||
                !Checkpoint(account.campaign,position,driving,bag) || !Available(node.choices[index],account.campaign))
                return false;
            Choice choice = node.choices[index];
            Progress old = account.campaign ?? new();
            string[] trail = old.decisions ?? Array.Empty<string>();
            if (trail.Length >= 100) return false;
            Array.Resize(ref trail,trail.Length+1);
            trail[^1] = choice.id;
            Progress progress = Branch(old,choice);
            progress.decisions = trail;
            return ledger.SaveCampaign(account,old.node,serial,progress,choice.pay);
        }

        internal static bool TryPoint(string anchor,out Vector3 point)
        {
            point = anchor switch
            {
                "press" => new(-48,0,43),
                "workshop" => CostaBuildTuning.CityWorkshop + new Vector3(.9f,0,-5.4f),
                "museum" => CostaCity.Museum + new Vector3(3.2f,0,-4.5f),
                "lobby" => CostaCity.Hotel + new Vector3(-4.2f,0,-3.6f),
                "station" => CostaCity.Station + new Vector3(2.5f,0,-5.8f),
                "exchange" => CostaCity.Vendors[1] + new Vector3(3.4f,0,-1.6f),
                "delivery" => new(-30,0,153),
                "plaza" => new(-31,0,76),
                "regional" => new(-31,0,CostaCity.Districts[1].Center),
                _ => default
            };
            return point.sqrMagnitude > 0;
        }
    }
}
