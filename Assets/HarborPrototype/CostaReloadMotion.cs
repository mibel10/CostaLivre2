using UnityEngine;
using Cue = CostaLivre.CostaEffects.Cue;

namespace CostaLivre
{
    // The same normalized animation is driven by the solo or host ammo timer.
    internal sealed class CostaReloadMotion
    {
        private Transform model;
        private Transform part;
        private Transform action;
        private Vector3 seated;
        private Vector3 actionRest;
        private bool shellFeed;
        private float previous = -1;

        internal void Attach(Transform weapon, int slot = -1)
        {
            ClearPose();
            model = weapon;
            part = model != null ? model.Find("Reload prop") : null;
            action = model != null ? model.Find("Reload action") : null;
            if (part != null)
                seated = part.localPosition;
            if (action != null)
                actionRest = action.localPosition;
            shellFeed = slot == 1;
            previous = -1;
        }

        internal bool Apply(float progress, HarborCharacter body, Transform mount, CostaEffects effects, AudioSource spatial = null)
        {
            if (progress < 0 || model == null || part == null)
            {
                if (previous >= 0)
                    ClearPose();
                previous = -1;
                return false;
            }

            float lowering = Ease(progress / .22f) * (1 - Ease((progress - .75f) / .25f));
            Vector3 hand = body.ReloadGrip(lowering);
            mount.SetPositionAndRotation(hand, body.Root.rotation * Quaternion.Euler(15f * lowering, 0, -24f * lowering));
            float rack = Ease((progress - .76f) / .06f) * (1 - Ease((progress - .84f) / .10f));
            if (action != null)
                action.localPosition = actionRest + Vector3.back * (rack * (shellFeed ? .16f : .08f));

            if (shellFeed)
            {
                float feed = Mathf.Clamp01((progress - .23f) / .48f) * 3;
                int cycle = Mathf.FloorToInt(feed);
                float insertion = Ease(feed - cycle);
                bool feeding = progress >= .23f && progress < .71f;
                part.gameObject.SetActive(feeding);
                part.localPosition = seated + new Vector3(-.12f, -.16f, -.08f) * (1 - insertion);
                if (feeding || action == null)
                    body.ReloadSupport(part.position, lowering);
                else
                    body.ReloadSupport(action.position - body.Root.right * .035f, lowering);
            }
            else
            {
                float travel = Ease((progress - .20f) / .24f) * (1 - Ease((progress - .46f) / .22f));
                part.localPosition = seated + new Vector3(-.095f, -.24f, .075f) * travel;
                part.localRotation = Quaternion.Euler(12f * travel, 5f * travel, -14f * travel);
                Vector3 grip = rack > .10f && action != null ? action.position : part.position;
                body.ReloadSupport(grip - body.Root.right * .032f, lowering);
            }

            if (previous >= 0)
            {
                if (shellFeed)
                {
                    PlayCrossing(.37f, Cue.ReloadIn);
                    PlayCrossing(.53f, Cue.ReloadIn);
                    PlayCrossing(.69f, Cue.ReloadIn);
                }
                else
                {
                    PlayCrossing(.23f, Cue.ReloadOut);
                    PlayCrossing(.66f, Cue.ReloadIn);
                }
                PlayCrossing(.86f, Cue.ReloadDone);
            }
            previous = progress;
            return true;

            void PlayCrossing(float at, Cue sound)
            {
                if (previous < at && progress >= at && progress - previous < .28f)
                    effects.Play(sound, spatial == null ? .52f : .28f, spatial);
            }
        }

        private void ClearPose()
        {
            if (part != null)
            {
                part.SetLocalPositionAndRotation(seated, Quaternion.identity);
                if (shellFeed)
                    part.gameObject.SetActive(false);
            }
            if (action != null)
                action.localPosition = actionRest;
        }

        private static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
    }
}
