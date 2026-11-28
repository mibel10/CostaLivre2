using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CostaLivre
{
    [CreateAssetMenu(menuName = "Costa Livre/Cutscene")]
    public sealed class CostaCinematic : ScriptableObject
    {
        [Serializable] public struct Shot
        {
            [Min(.5f)] public float seconds;
            [Range(0, 1.25f)] public float dissolve;
            [Range(25, 75)] public float fieldOfView;
            public Vector3 start, finish, lookAt;
        }

        [Serializable] public struct Line
        {
            [Min(0)] public float start;
            [Min(.2f)] public float seconds;
            public AudioClip voice;
            public Vector3 gestureTarget;
            [Range(-1, 1)] public float expression;
            [TextArea] public string portuguese, english;
        }

        public Shot[] shots;
        public Line[] lines;

        private void OnValidate()
        {
            if (shots != null)
                for (int i = 0; i < shots.Length; i++)
                {
                    shots[i].seconds = Mathf.Max(.5f, shots[i].seconds);
                    shots[i].dissolve = Mathf.Clamp(shots[i].dissolve, 0, shots[i].seconds * .45f);
                    shots[i].fieldOfView = Mathf.Clamp(shots[i].fieldOfView, 25, 75);
                }
        }
    }
}
