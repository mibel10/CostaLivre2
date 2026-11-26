using System.Collections.Generic;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaWorldDamage
    {
        internal enum Surface { Concrete, Metal, Wood, Glass, Earth }

        private sealed class Piece
        {
            internal GameObject Object;
            internal int Limit;
            internal int Damage;
            internal Surface Material;
            internal Vector3 Rest;
            internal Quaternion RestRotation;
            internal Collider Collider;
            internal Renderer Renderer;
            internal float FellAt;
        }

        private readonly Dictionary<Collider, Surface> surfaces = new();
        private readonly Dictionary<Collider, int> indices = new();
        private readonly List<Piece> pieces = new();
        private readonly List<Piece> toppling = new();
        private readonly MaterialPropertyBlock color = new();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private int[] snapshot;

        internal void Mark(GameObject target, Surface surface, int durability)
        {
            if (!target.TryGetComponent(out Collider collider))
                return;
            surfaces[collider] = surface;
            if (durability <= 0)
                return;
            indices.Add(collider, pieces.Count);
            pieces.Add(new Piece
            {
                Object = target, Limit = durability, Material = surface, Collider = collider,
                Rest = target.transform.localPosition, RestRotation = target.transform.localRotation,
                Renderer = target.GetComponent<Renderer>()
            });
            snapshot = null;
        }

        internal Surface Resolve(Collider collider) =>
            surfaces.TryGetValue(collider, out Surface surface) ? surface : Surface.Concrete;

        internal bool Hit(Collider collider, int power)
        {
            if (!indices.TryGetValue(collider, out int index))
                return false;
            Piece piece = pieces[index];
            if (piece.Damage >= piece.Limit)
                return false;
            Set(piece, Mathf.Min(piece.Limit, piece.Damage + Mathf.Max(0, power)));
            return piece.Damage >= piece.Limit;
        }

        internal int[] Capture()
        {
            snapshot ??= new int[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
                snapshot[i] = pieces[i].Damage;
            return snapshot;
        }

        internal void Apply(int[] state)
        {
            if (state?.Length != pieces.Count)
                return;
            for (int i = 0; i < pieces.Count; i++)
                Set(pieces[i], Mathf.Clamp(state[i], 0, pieces[i].Limit));
        }

        internal void Reset()
        {
            foreach (Piece piece in pieces)
                Set(piece, 0);
        }

        internal void Animate()
        {
            for (int i = toppling.Count - 1; i >= 0; i--)
            {
                Piece piece = toppling[i];
                float t = Mathf.Clamp01((Time.unscaledTime - piece.FellAt) / .39f);
                piece.Object.transform.SetLocalPositionAndRotation(
                    piece.Rest + new Vector3(.12f * t, -.046f * Mathf.Sin(t * Mathf.PI), .28f * t),
                    piece.RestRotation * Quaternion.Euler(Mathf.SmoothStep(0, 90, t), 8f * t, 0));
                if (t >= 1)
                    toppling.RemoveAt(i);
            }
        }

        private void Set(Piece piece, int damage)
        {
            if (piece.Damage == damage)
                return;
            bool wasBroken = piece.Damage >= piece.Limit;
            piece.Damage = damage;
            bool intact = damage < piece.Limit;
            if (piece.Material == Surface.Wood)
            {
                piece.Collider.enabled = intact;
                if (!intact && !wasBroken)
                {
                    piece.FellAt = Time.unscaledTime;
                    toppling.Add(piece);
                }
                else if (intact && wasBroken)
                {
                    toppling.Remove(piece);
                    piece.Object.transform.SetLocalPositionAndRotation(piece.Rest, piece.RestRotation);
                }
                return;
            }
            if (piece.Object.activeSelf != intact)
                piece.Object.SetActive(intact);
            if (piece.Material == Surface.Concrete && piece.Renderer != null)
            {
                piece.Renderer.GetPropertyBlock(color);
                color.SetColor(BaseColor, piece.Renderer.sharedMaterial.color * Mathf.Lerp(1, .62f, damage / (float)piece.Limit));
                piece.Renderer.SetPropertyBlock(color);
            }
        }
    }
}
