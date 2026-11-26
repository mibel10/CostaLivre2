using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaTransitions
    {
        private float began = -20;
        private float length = 1;
        private float cover;
        private string title = "";
        private GUIStyle heading;

        internal void Begin(string caption, float duration, float opacity)
        {
            began = Time.unscaledTime;
            length = duration;
            cover = opacity;
            title = caption;
        }

        internal void Draw()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float t = (Time.unscaledTime - began) / length;
            if (t >= 1)
                return;
            float alpha = cover * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 1.8f)));
            Color previous = GUI.color;
            GUI.color = new Color(.014f,.035f,.045f,alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (title.Length > 0)
            {
                float visible = Mathf.SmoothStep(0, 1, t * 9f) * (1 - Mathf.SmoothStep(.60f, 1, t));
                heading ??= new GUIStyle(GUI.skin.label)
                    { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                heading.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .045f), 18, 48);
                float offset = 9 * (1 - Mathf.SmoothStep(0, 1, t * 5));
                GUI.color = new Color(.99f,.88f,.71f,visible);
                float y = Screen.height * .32f + offset;
                GUI.Label(new Rect(0, y, Screen.width, heading.fontSize * 2), title, heading);
                GUI.DrawTexture(new Rect(Screen.width*.5f - 31, y + heading.fontSize * 1.64f, 62, 2), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
