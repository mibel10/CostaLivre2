using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaTouchIcons
    {
        private readonly Texture2D[] icons = new Texture2D[16];
        private Texture2D round;
        private Texture2D rim;
        private const int Size = 64;

        internal Texture2D Round => round ??= Make("Control disk", point => point.magnitude - 10f);
        internal Texture2D Rim => rim ??= Make("Control rim", point => Mathf.Abs(point.magnitude - 9.5f) - 0.45f);
        internal Texture2D Icon(int index) => icons[index] ??= Make("Control icon", point => Glyph(index, point));

        internal void Release()
        {
            Object.Destroy(round);
            Object.Destroy(rim);
            foreach (Texture2D icon in icons)
                Object.Destroy(icon);
        }

        private static Texture2D Make(string name, System.Func<Vector2, float> distance)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    Vector2 point = new((x + 0.5f) * 20f / Size - 10f, (y + 0.5f) * 20f / Size - 10f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance(point) * 2.8f) * 255);
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float Segment(Vector2 p, float x1, float y1, float x2, float y2)
        {
            Vector2 a = new(x1, y1);
            Vector2 b = new(x2, y2);
            Vector2 delta = b - a;
            Vector2 nearest = a + delta * Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
            return (p - nearest).magnitude - 0.80f;
        }

        private static float Ring(Vector2 p, float x, float y, float radius) =>
            Mathf.Abs((p - new Vector2(x, y)).magnitude - radius) - 0.74f;

        private static float Min(float a, float b, float c) => Mathf.Min(a, Mathf.Min(b, c));
        private static float Min(float a, float b, float c, float d) => Mathf.Min(Min(a, b, c), d);
        private static float Min(float a, float b, float c, float d, float e) => Mathf.Min(Min(a, b, c, d), e);
        private static float Min(float a, float b, float c, float d, float e, float f) => Mathf.Min(Min(a, b, c, d, e), f);
        private static float Min(float a, float b, float c, float d, float e, float f, float g, float h, float i, float j) =>
            Mathf.Min(Min(a, b, c, d, e), Min(f, g, h, i, j));

        private static float Glyph(int index, Vector2 p)
        {
            switch ((CostaTouch.Control)index)
            {
                case CostaTouch.Control.Sprint:
                    return Min(Segment(p, -6, 4, -1, 0), Segment(p, -1, 0, -6, -4),
                        Segment(p, 0, 4, 5, 0), Segment(p, 5, 0, 0, -4));
                case CostaTouch.Control.Crouch:
                    return Min(Segment(p, -4, 2, 0, -2), Segment(p, 0, -2, 4, 2),
                        Segment(p, -5, -5, 5, -5));
                case CostaTouch.Control.Jump:
                    return Min(Segment(p, 0, -5, 0, 6), Segment(p, -4, 2, 0, 6), Segment(p, 0, 6, 4, 2));
                case CostaTouch.Control.Roll:
                    return Min(Ring(p, 0, 0, 4.8f), Segment(p, -5, 2, -2, 5), Segment(p, -5, 2, -6, 6));
                case CostaTouch.Control.Use:
                    return Min(Segment(p, -5, -5, -5, 5), Segment(p, -5, 5, 1, 5), Segment(p, -5, -5, 1, -5),
                        Segment(p, -1, 0, 6, 0), Segment(p, 3, 3, 6, 0), Segment(p, 6, 0, 3, -3));
                case CostaTouch.Control.Aim:
                    return Min(Ring(p, 0, 0, 4.7f), Segment(p, -7, 0, -3, 0), Segment(p, 3, 0, 7, 0),
                        Segment(p, 0, -7, 0, -3), Segment(p, 0, 3, 0, 7));
                case CostaTouch.Control.Fire:
                    return Mathf.Min(Ring(p, 0, 0, 5.4f), p.magnitude - 2f);
                case CostaTouch.Control.Reload:
                case CostaTouch.Control.Retry:
                    float arc = p.x > 0 && p.y > 1 ? 10f : Ring(p, 0, 0, 5f);
                    return Min(arc, Segment(p, 0, 5, 5, 5), Segment(p, 5, 5, 3, 1));
                case CostaTouch.Control.Swap:
                    return Min(Segment(p, -5, 2, 5, 2), Segment(p, 2, 5, 5, 2),
                        Segment(p, -5, -2, 5, -2), Segment(p, -5, -2, -2, -5));
                case CostaTouch.Control.Map:
                    return Min(Segment(p, -6, -5, -6, 4), Segment(p, -6, 4, -2, 6),
                        Segment(p, -2, 6, 2, 4), Segment(p, 2, 4, 6, 6), Segment(p, 6, 6, 6, -4),
                        Segment(p, -6, -5, -2, -3), Segment(p, -2, -3, 2, -5), Segment(p, 2, -5, 6, -4),
                        Segment(p, -2, -3, -2, 6), Segment(p, 2, -5, 2, 4));
                case CostaTouch.Control.Bag:
                    return Min(Segment(p, -4, -5, -4, 4), Segment(p, 4, -5, 4, 4),
                        Segment(p, -4, 4, 4, 4), Segment(p, -4, -5, 4, -5), Segment(p, -2, 6, 2, 6),
                        Segment(p, -2, -1, 2, -1));
                case CostaTouch.Control.Shoulder:
                    return Min(Ring(p, 0, 3, 1.5f), Segment(p, 0, 1.5f, 0, -5),
                        Segment(p, -6, -1, 6, -1), Segment(p, -6, -1, -3, 2),
                        Segment(p, 6, -1, 3, 2));
                case CostaTouch.Control.Menu:
                    return Min(Segment(p, -5, 4, 5, 4), Segment(p, -5, 0, 5, 0), Segment(p, -5, -4, 5, -4));
                default:
                    return Min(Segment(p, -5, -4, -5, 4), Segment(p, 5, -4, 5, 4),
                        Segment(p, -2, -4, -2, 4), Segment(p, 2, -4, 2, 4));
            }
        }
    }
}
