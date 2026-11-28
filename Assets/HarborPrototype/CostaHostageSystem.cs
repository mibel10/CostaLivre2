using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaHostageSystem
    {
        private sealed class Hostage
        {
            internal HarborCharacter Body;
            internal CapsuleCollider Hitbox;
            internal Vector3 Home;
            internal float HomeYaw;
            internal bool IsClerk;
            internal bool ArmedSecurity;
            internal float Fear;
            internal float Courage;
            internal float GazeAwayTimer;
            internal float SilentAlarmProgress;
            internal bool AlarmTriggered;
            internal bool ArmedReacting;
            internal float FireCooldown;
            internal int Health;
            internal bool Alive = true;
            internal float RespawnAt;
            internal Vector3 CurrentPos;
        }

        private const int NpcsPerSite = 3;
        private const float ActiveRadius = 26f;
        private const float AimRange = 11.5f;
        private const float GazeLossGrace = 3.5f;
        private const float WarningShotRadius = 15.5f;
        private const float UpwardPitchThreshold = -18f;

        private readonly Hostage[,] pool;
        private readonly bool[] siteAlerted;
        private readonly float[] siteCalmAt;
        private int activeSite = -1;
        private GUIStyle labelStyle;
        private GUIStyle barTextStyle;

        internal CostaHostageSystem(HarborBlockout art, Transform parent)
        {
            int count = CostaRobberies.Sites.Length;
            pool = new Hostage[count, NpcsPerSite];
            siteAlerted = new bool[count];
            siteCalmAt = new float[count];

            Transform root = new GameObject("RobberyHostages").transform;
            root.SetParent(parent, false);

            Vector3[] offsets =
            {
                new(-1.55f, 0f, 1.55f),
                new(1.55f, 0f, -0.75f),
                new(-1.35f, 0f, -1.35f)
            };

            for (int s = 0; s < count; s++)
            {
                Vector3 center = CostaRobberies.Sites[s].Point;
                for (int n = 0; n < NpcsPerSite; n++)
                {
                    bool clerk = n == 0;
                    float seed = Mathf.Sin((s * 17 + n * 31 + 5) * 1.37f) * 0.5f + 0.5f;
                    float courage = clerk ? Mathf.Lerp(0.56f, 0.88f, seed) : Mathf.Lerp(0.18f, 0.54f, seed);
                    bool armed = clerk && courage > 0.72f;
                    Vector3 home = center + offsets[n];
                    HarborCharacter body = art.Person(
                        clerk ? $"ShopClerk_{s}" : $"Hostage_{s}_{n}",
                        root,
                        home,
                        armed ? new Color(0.24f, 0.30f, 0.38f) : Color.white);
                    Texture2D look = Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{(s * 3 + n) % 10 + 1}");
                    if (look != null)
                        body.WearTexture(look);

                    CapsuleCollider col = body.Root.gameObject.AddComponent<CapsuleCollider>();
                    col.center = Vector3.up * 0.92f;
                    col.radius = 0.32f;
                    col.height = 1.84f;
                    col.isTrigger = true;
                    body.Root.gameObject.SetActive(false);

                    pool[s, n] = new Hostage
                    {
                        Body = body,
                        Hitbox = col,
                        Home = home,
                        HomeYaw = clerk ? 180f : (n == 1 ? -135f : 135f),
                        IsClerk = clerk,
                        ArmedSecurity = armed,
                        Courage = courage,
                        Health = armed ? 90 : 60,
                        Alive = true,
                        CurrentPos = home
                    };
                }
            }
        }

        internal void NotifyRobberyStarted(int site)
        {
            if (site < 0 || site >= siteAlerted.Length)
                return;
            siteAlerted[site] = true;
            siteCalmAt[site] = Time.time + 45f;
            for (int n = 0; n < NpcsPerSite; n++)
            {
                Hostage h = pool[site, n];
                if (!h.Alive) continue;
                h.Fear = Mathf.Max(h.Fear, 0.42f);
                h.GazeAwayTimer = 0f;
            }
        }

        internal float GetLootSpeedMultiplier(int site)
        {
            if (site < 0 || site >= siteAlerted.Length)
                return 1f;
            int aliveCount = 0;
            int intimidatedCount = 0;
            float avgFear = 0f;
            for (int n = 0; n < NpcsPerSite; n++)
            {
                Hostage h = pool[site, n];
                if (!h.Alive) continue;
                aliveCount++;
                avgFear += h.Fear;
                if (h.Fear >= 0.65f)
                    intimidatedCount++;
            }
            if (aliveCount == 0)
                return 1.35f;
            if (intimidatedCount == aliveCount)
                return 2.0f;
            return Mathf.Lerp(1.0f, 1.75f, avgFear / aliveCount);
        }

        internal bool TryHit(Collider collider, int damage, Action<string> onNotice, Action onCrime)
        {
            if (collider == null || activeSite < 0)
                return false;
            for (int n = 0; n < NpcsPerSite; n++)
            {
                Hostage h = pool[activeSite, n];
                if (!h.Alive || h.Hitbox != collider)
                    continue;
                h.Health -= damage;
                h.Fear = Mathf.Clamp01(h.Fear + 0.35f);
                siteAlerted[activeSite] = true;
                siteCalmAt[activeSite] = Time.time + 50f;
                if (h.Health <= 0)
                {
                    h.Alive = false;
                    h.ArmedReacting = false;
                    h.RespawnAt = Time.time + 90f;
                    h.Body.Root.gameObject.SetActive(false);
                    onNotice?.Invoke(h.ArmedSecurity
                        ? L("Armed security neutralized!", "Segurança armado neutralizado!")
                        : L("Hostage down! Police alerted.", "Refém abatido! Polícia alertada."));
                    onCrime?.Invoke();
                }
                return true;
            }
            return false;
        }

        internal void Update(
            Vector3 playerPos,
            Camera cam,
            bool aiming,
            bool hasFirearm,
            float pitch,
            bool shotFired,
            float delta,
            Action<int, string> onSilentAlarm,
            Action<int> onSecurityHitPlayer)
        {
            float bestDistSq = ActiveRadius * ActiveRadius;
            int nearest = -1;
            for (int s = 0; s < CostaRobberies.Sites.Length; s++)
            {
                float dSq = (CostaRobberies.Sites[s].Point - playerPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    nearest = s;
                }
            }

            if (activeSite != nearest)
            {
                if (activeSite >= 0)
                    SetSiteVisible(activeSite, false);
                activeSite = nearest;
                if (activeSite >= 0)
                    SetSiteVisible(activeSite, true);
            }

            if (activeSite < 0)
                return;

            Vector3 siteCenter = CostaRobberies.Sites[activeSite].Point;
            float distToSite = Vector3.Distance(playerPos, siteCenter);
            bool inShop = distToSite <= 14.5f;
            bool warningShot = shotFired && hasFirearm && distToSite <= WarningShotRadius;
            bool upwardShot = warningShot && pitch < UpwardPitchThreshold;

            if ((aiming && hasFirearm && inShop) || warningShot)
            {
                siteAlerted[activeSite] = true;
                siteCalmAt[activeSite] = Time.time + 40f;
            }
            else if (siteAlerted[activeSite] && Time.time > siteCalmAt[activeSite] && distToSite > 18f)
            {
                ResetSiteState(activeSite);
            }

            Vector3 camPos = cam != null ? cam.transform.position : playerPos + Vector3.up * 1.5f;
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;

            for (int n = 0; n < NpcsPerSite; n++)
            {
                Hostage h = pool[activeSite, n];
                if (!h.Alive)
                {
                    if (Time.time >= h.RespawnAt && distToSite > 18f)
                    {
                        h.Alive = true;
                        h.Health = h.ArmedSecurity ? 90 : 60;
                        h.Fear = 0f;
                        h.GazeAwayTimer = 0f;
                        h.SilentAlarmProgress = 0f;
                        h.AlarmTriggered = false;
                        h.ArmedReacting = false;
                        h.Body.Root.gameObject.SetActive(true);
                    }
                    continue;
                }

                if (warningShot)
                {
                    float pulse = upwardShot ? 0.48f : 0.36f;
                    h.Fear = Mathf.Clamp01(h.Fear + pulse);
                    h.GazeAwayTimer = 0f;
                    h.SilentAlarmProgress = Mathf.Max(0f, h.SilentAlarmProgress - 0.45f);
                    if (h.Fear >= 0.65f)
                        h.ArmedReacting = false;
                }

                Vector3 npcHead = h.CurrentPos + Vector3.up * 1.35f;
                Vector3 toNpc = npcHead - camPos;
                float npcDist = toNpc.magnitude;
                bool playerAimingAtNpc = aiming && hasFirearm && npcDist <= AimRange
                    && Vector3.Dot(camFwd, toNpc / Mathf.Max(0.01f, npcDist)) > 0.72f;

                if (playerAimingAtNpc)
                {
                    h.Fear = Mathf.Clamp01(h.Fear + 0.55f * delta);
                    h.GazeAwayTimer = 0f;
                    h.SilentAlarmProgress = Mathf.Max(0f, h.SilentAlarmProgress - 0.35f * delta);
                    if (h.Fear >= 0.62f)
                        h.ArmedReacting = false;
                }
                else if (siteAlerted[activeSite])
                {
                    h.GazeAwayTimer += delta;
                    if (h.GazeAwayTimer > GazeLossGrace)
                    {
                        float decay = (0.14f + h.Courage * 0.16f) * delta;
                        h.Fear = Mathf.Max(0f, h.Fear - decay);
                    }

                    if (h.Fear < 0.35f && h.Courage >= 0.48f && inShop)
                    {
                        if (!h.AlarmTriggered)
                        {
                            h.SilentAlarmProgress = Mathf.Min(1f, h.SilentAlarmProgress + (0.22f + h.Courage * 0.25f) * delta);
                            if (h.SilentAlarmProgress >= 1f)
                            {
                                h.AlarmTriggered = true;
                                if (h.ArmedSecurity)
                                {
                                    h.ArmedReacting = true;
                                    h.FireCooldown = 0.6f;
                                    onSilentAlarm?.Invoke(activeSite,
                                        L("Security drew a weapon and alerted police!", "Segurança sacou uma arma e acionou a polícia!"));
                                }
                                else
                                {
                                    onSilentAlarm?.Invoke(activeSite,
                                        L("A brave clerk triggered the silent alarm!", "Um funcionário corajoso acionou o alarme silencioso!"));
                                }
                            }
                        }
                        else if (h.ArmedSecurity && !h.ArmedReacting)
                        {
                            h.ArmedReacting = true;
                            h.FireCooldown = 0.8f;
                        }
                    }
                }
                else
                {
                    h.Fear = Mathf.Max(0f, h.Fear - 0.25f * delta);
                    h.SilentAlarmProgress = 0f;
                }

                if (h.ArmedReacting && inShop)
                {
                    h.FireCooldown -= delta;
                    if (h.FireCooldown <= 0f)
                    {
                        h.FireCooldown = 1.35f;
                        onSecurityHitPlayer?.Invoke(9);
                    }
                }

                // Pose & animation (lightweight distance-culled)
                Vector3 lookDir = playerPos - h.CurrentPos;
                lookDir.y = 0f;
                float targetYaw = siteAlerted[activeSite] && lookDir.sqrMagnitude > 0.04f
                    ? Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg
                    : h.HomeYaw;
                h.Body.Root.position = h.CurrentPos;
                h.Body.Root.rotation = Quaternion.Slerp(h.Body.Root.rotation, Quaternion.Euler(0f, targetYaw, 0f), delta * 7f);

                bool crouched = h.Fear >= 0.60f && !h.ArmedReacting;
                bool aimingGun = h.ArmedReacting;
                h.Body.AnimateDistance(HarborCharacter.Motion.Idle, distToSite * distToSite);
                if (crouched)
                    h.Body.CrouchPose();
                else if (aimingGun)
                    h.Body.HoldWeapon(true, 0f, true);
            }
        }

        internal void DrawHud(Camera cam, int lootingSite, float lootProgress, float lootMult)
        {
            if (cam == null)
                return;

            EnsureStyles();

            if (activeSite >= 0 && siteAlerted[activeSite])
            {
                for (int n = 0; n < NpcsPerSite; n++)
                {
                    Hostage h = pool[activeSite, n];
                    if (!h.Alive) continue;
                    Vector3 worldHead = h.CurrentPos + Vector3.up * (h.Fear >= 0.60f ? 1.55f : 2.08f);
                    Vector3 screen = cam.WorldToScreenPoint(worldHead);
                    if (screen.z <= 0.3f || screen.z > 18f)
                        continue;

                    float x = screen.x;
                    float y = Screen.height - screen.y;
                    float w = 96f;
                    float bh = 7f;

                    string tag;
                    Color fill;
                    if (h.ArmedReacting)
                    {
                        tag = L("Armed!", "Armado!");
                        fill = new Color(0.95f, 0.24f, 0.22f);
                    }
                    else if (h.SilentAlarmProgress > 0.05f && !h.AlarmTriggered)
                    {
                        tag = L("Alarm!", "Alarme!");
                        fill = new Color(0.96f, 0.42f, 0.16f);
                    }
                    else if (h.Fear >= 0.65f)
                    {
                        tag = L("Surrendered", "Rendido");
                        fill = new Color(0.24f, 0.84f, 0.50f);
                    }
                    else
                    {
                        tag = h.IsClerk ? L("Nervous clerk", "Caixa nervoso") : L("Nervous", "Nervoso");
                        fill = new Color(0.94f, 0.76f, 0.24f);
                    }

                    GUI.color = new Color(0.05f, 0.08f, 0.10f, 0.78f);
                    GUI.DrawTexture(new Rect(x - w * 0.5f - 2, y - 20, w + 4, 30), Texture2D.whiteTexture);

                    GUI.color = Color.white;
                    GUI.Label(new Rect(x - w * 0.5f, y - 19, w, 16), tag, labelStyle);

                    GUI.color = new Color(0.18f, 0.22f, 0.24f, 0.9f);
                    GUI.DrawTexture(new Rect(x - w * 0.5f, y - 1, w, bh), Texture2D.whiteTexture);
                    GUI.color = fill;
                    GUI.DrawTexture(new Rect(x - w * 0.5f, y - 1, w * Mathf.Clamp01(h.Fear), bh), Texture2D.whiteTexture);

                    if (h.SilentAlarmProgress > 0.05f && !h.AlarmTriggered)
                    {
                        GUI.color = new Color(0.98f, 0.22f, 0.20f, 0.95f);
                        GUI.DrawTexture(new Rect(x - w * 0.5f, y + bh + 1, w * h.SilentAlarmProgress, 4f), Texture2D.whiteTexture);
                    }
                    GUI.color = Color.white;
                }
            }

            if (lootingSite >= 0 && lootProgress > 0f)
            {
                float bw = 260f;
                float bx = (Screen.width - bw) * 0.5f;
                float by = Screen.height - 152f;
                GUI.color = new Color(0.05f, 0.08f, 0.10f, 0.85f);
                GUI.DrawTexture(new Rect(bx - 8, by - 26, bw + 16, 46), Texture2D.whiteTexture);
                GUI.color = Color.white;
                string title = L($"Looting ({lootMult:0.0}x intimidation)", $"Saqueando ({lootMult:0.0}x intimidação)");
                GUI.Label(new Rect(bx, by - 23, bw, 18), title, barTextStyle);
                GUI.color = new Color(0.20f, 0.25f, 0.28f, 0.9f);
                GUI.DrawTexture(new Rect(bx, by, bw, 10), Texture2D.whiteTexture);
                GUI.color = lootMult >= 1.95f ? new Color(0.24f, 0.88f, 0.52f) : new Color(0.95f, 0.72f, 0.26f);
                GUI.DrawTexture(new Rect(bx, by, bw * Mathf.Clamp01(lootProgress), 10), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private void SetSiteVisible(int site, bool visible)
        {
            for (int n = 0; n < NpcsPerSite; n++)
            {
                Hostage h = pool[site, n];
                if (h.Alive)
                    h.Body.Root.gameObject.SetActive(visible);
            }
        }

        private void ResetSiteState(int site)
        {
            siteAlerted[site] = false;
            for (int n = 0; n < NpcsPerSite; n++)
            {
                Hostage h = pool[site, n];
                h.Fear = 0f;
                h.GazeAwayTimer = 0f;
                h.SilentAlarmProgress = 0f;
                h.AlarmTriggered = false;
                h.ArmedReacting = false;
            }
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
                return;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0.95f, 0.96f, 0.94f);
            barTextStyle = new GUIStyle(labelStyle) { fontSize = 12 };
        }
    }
}
