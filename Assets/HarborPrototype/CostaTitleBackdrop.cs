using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaTitleBackdrop
    {
        private readonly struct Shot
        {
            internal readonly Vector3 Start, End, Focus;
            internal readonly float Hour, Lens;

            internal Shot(Vector3 start, Vector3 end, Vector3 focus, float hour, float lens)
            {
                Start = start;
                End = end;
                Focus = focus;
                Hour = hour;
                Lens = lens;
            }
        }

        private static readonly Shot[] Shots =
        {
            new(new(44, 14, 24), new(49, 17, 18), new(-6, 4, 29), 9.4f, 49),
            new(new(51, 27, 228), new(52, 28, 244), new(-22, 3, 264), 16.4f, 45),
            new(new(34, 23, 451), new(41, 24, 466), new(-28, 4, 490), 18.1f, 45),
            new(new(46, 31, 681), new(52, 32, 700), new(-28, 4, 719), 20.8f, 46)
        };
        private const float ShotLength = 13f;
        private float start;
        private float darkness;

        internal float Hour { get; private set; }

        internal void Begin() => start = Time.unscaledTime;

        internal void Position(Camera camera)
        {
            float elapsed = Mathf.Max(0, Time.unscaledTime - start);
            int index = Mathf.FloorToInt(elapsed / ShotLength) % Shots.Length;
            float local = elapsed % ShotLength;
            Shot shot = Shots[index];
            Hour = shot.Hour + local * .013f;
            float move = Mathf.SmoothStep(0, 1, local / ShotLength);
            Vector3 position = Vector3.Lerp(shot.Start, shot.End, move);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(shot.Focus - position));
            camera.fieldOfView = shot.Lens;
            darkness = 1 - Mathf.Min(Mathf.SmoothStep(0, 1, local / .8f),
                Mathf.SmoothStep(0, 1, (ShotLength - local) / .55f));
        }

        internal void DrawTransition()
        {
            if (Event.current.type != EventType.Repaint || darkness <= 0)
                return;
            Color previous = GUI.color;
            GUI.color = new Color(.013f, .024f, .032f, darkness);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
