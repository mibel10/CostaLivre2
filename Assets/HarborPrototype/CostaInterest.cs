using System;
using System.Collections.Generic;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaInterest
    {
        private const float Cell = 80;
        private const float Reach = 155;
        private readonly Dictionary<(int,int),int> cells = new();
        private int[] next = new int[32];
        private Vector3[] points = new Vector3[32];
        private int count;
        private bool useHash;

        internal void Begin(int expected)
        {
            Clear();
            useHash=expected>=96;
        }

        private static (int x,int z) Key(Vector3 p) => (Mathf.FloorToInt(p.x/Cell),Mathf.FloorToInt(p.z/Cell));
        internal void Clear()
        {
            cells.Clear();
            count = 0;
        }
        internal void Add(Vector3 position)
        {
            if (count == next.Length)
            {
                Array.Resize(ref next,count*2);
                Array.Resize(ref points,count*2);
            }
            if (!useHash)
            {
                points[count++]=position;
                return;
            }
            var key = Key(position);
            next[count] = cells.TryGetValue(key,out int head) ? head : -1;
            points[count] = position;
            cells[key] = count++;
        }

        internal void Query(Vector3 center,List<int> indices)
        {
            indices.Clear();
            if(!useHash)
            {
                for(int i=0;i<count;i++)
                    if((points[i]-center).sqrMagnitude<=Reach*Reach) indices.Add(i);
                return;
            }
            var min = Key(center-new Vector3(Reach,0,Reach));
            var max = Key(center+new Vector3(Reach,0,Reach));
            for(int z=min.z;z<=max.z;z++)
                for(int x=min.x;x<=max.x;x++)
                    if(cells.TryGetValue((x,z),out int first))
                        for(int i=first;i>=0;i=next[i])
                            if((points[i]-center).sqrMagnitude <= Reach*Reach)
                                indices.Add(i);
        }
    }
}
