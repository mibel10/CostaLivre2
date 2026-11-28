using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    /// <summary>
    /// Interactive playable minigames: Basketball, Quay Fishing, Neon Arcade, Gym Training, and Drift/Street Races.
    /// </summary>
    internal sealed class CostaMinigames
    {
        internal enum Mode { None, Basketball, Fishing, Arcade, Gym, Drift, StreetRace }

        private static readonly Color Panel = new(0.055f, 0.075f, 0.088f, 0.96f);
        private static readonly Color Accent = new(0.92f, 0.66f, 0.39f);
        private static readonly Color Mint = new(0.45f, 0.85f, 0.68f);
        private static readonly Color Paper = new(0.92f, 0.91f, 0.85f);

        internal Mode Active { get; private set; }
        internal bool BlocksWorld => Active is Mode.Basketball or Mode.Fishing or Mode.Arcade or Mode.Gym;

        // World-space race/drift state
        private float raceTimer;
        private float driftScore;
        private int checkpointIndex;
        private Vector3[] raceWaypoints;
        private Action<int, string> onReward;

        // UI minigame state
        private float sessionTimer;
        private float cursor;
        private float cursorDir = 1f;
        private float targetCenter = 0.5f;
        private float targetWidth = 0.22f;
        private int score;
        private int bestScore;
        private int fishPhase; // 0 waiting, 1 biting, 2 reeling
        private float fishWait;
        private float fishTension;
        private string statusText = "";

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private int actionFinger = -1;

        internal Vector3? CurrentWaypoint => (Active is Mode.Drift or Mode.StreetRace) && raceWaypoints != null && checkpointIndex < raceWaypoints.Length
            ? raceWaypoints[checkpointIndex] : null;

        internal string WorldHudStatus
        {
            get
            {
                if (Active == Mode.Drift)
                    return L($"DRIFT · {driftScore:0} pts · {raceTimer:0}s", $"DRIFT · {driftScore:0} pts · {raceTimer:0}s");
                if (Active == Mode.StreetRace && raceWaypoints != null)
                    return L($"RACE · {checkpointIndex}/{raceWaypoints.Length} · {raceTimer:0.0}s",
                        $"CORRIDA · {checkpointIndex}/{raceWaypoints.Length} · {raceTimer:0.0}s");
                return "";
            }
        }

        internal void StartBasketball(Action<int, string> rewardCallback)
        {
            BeginUi(Mode.Basketball, "CostaBest_Basket", 40f, rewardCallback);
            cursor = 0.1f;
            targetCenter = 0.52f;
            targetWidth = 0.18f;
            statusText = L("Press Space / A at the green zone to shoot!", "Aperte Espaço / A na faixa verde para arremessar!");
        }

        internal void StartFishing(Action<int, string> rewardCallback)
        {
            BeginUi(Mode.Fishing, "CostaBest_Fish", 50f, rewardCallback);
            fishPhase = 0;
            fishWait = UnityEngine.Random.Range(1.5f, 3.8f);
            fishTension = 0.35f;
            statusText = L("Line cast... Wait for a bite!", "Linha lançada... Aguarde a fisgada!");
        }

        internal void StartArcade(Action<int, string> rewardCallback)
        {
            BeginUi(Mode.Arcade, "CostaBest_Arcade", 35f, rewardCallback);
            cursor = 0.5f;
            targetCenter = UnityEngine.Random.Range(0.2f, 0.8f);
            targetWidth = 0.16f;
            statusText = L("Hit the neon targets fast!", "Acerte os alvos neon rapidamente!");
        }

        internal void StartGym(Action<int, string> rewardCallback)
        {
            BeginUi(Mode.Gym, "CostaBest_Gym", 30f, rewardCallback);
            cursor = 0f;
            targetCenter = 0.5f;
            targetWidth = 0.24f;
            statusText = L("Press Space / A in rhythm to lift!", "Aperte Espaço / A no ritmo para levantar!");
        }

        internal void StartDrift(Action<int, string> rewardCallback)
        {
            Active = Mode.Drift;
            onReward = rewardCallback;
            driftScore = 0f;
            raceTimer = 45f;
            checkpointIndex = 0;
            raceWaypoints = new[]
            {
                new Vector3(-20f, 0f, CostaCity.Districts[0].Center + 18f),
                new Vector3(12f, 0f, CostaCity.Districts[0].Center + 18f),
                new Vector3(12f, 0f, CostaCity.Districts[0].Center - 18f),
                new Vector3(-20f, 0f, CostaCity.Districts[0].Center - 8f)
            };
        }

        internal void StartStreetRace(Action<int, string> rewardCallback)
        {
            Active = Mode.StreetRace;
            onReward = rewardCallback;
            raceTimer = 0f;
            checkpointIndex = 0;
            raceWaypoints = new[]
            {
                new Vector3(-30f, 0f, CostaCity.Districts[1].Center + 45f),
                new Vector3(-30f, 0f, CostaCity.Districts[2].Center - 35f),
                new Vector3(18f, 0f, CostaCity.Districts[2].Center),
                new Vector3(-30f, 0f, CostaCity.Districts[2].Center + 45f)
            };
        }

        private void BeginUi(Mode mode, string prefKey, float duration, Action<int, string> rewardCallback)
        {
            Active = mode;
            onReward = rewardCallback;
            sessionTimer = duration;
            score = 0;
            bestScore = PlayerPrefs.GetInt(prefKey, 0);
            cursorDir = 1f;
            actionFinger = -1;
        }

        internal void UpdateWorldActivity(Vector3 playerPos, CostaVehicle car)
        {
            if (Active is not (Mode.Drift or Mode.StreetRace))
                return;
            if (car == null)
            {
                Active = Mode.None;
                return;
            }

            float dt = Time.deltaTime;
            if (Active == Mode.Drift)
            {
                raceTimer -= dt;
                if (car.Slip > 0.25f && car.Speed > 5f)
                    driftScore += car.Slip * car.Speed * dt * 14f;
                if (CurrentWaypoint is Vector3 wp && (playerPos - wp).sqrMagnitude < 7f * 7f)
                    checkpointIndex = (checkpointIndex + 1) % raceWaypoints.Length;

                if (raceTimer <= 0f)
                {
                    int reward = Mathf.RoundToInt(Mathf.Clamp(driftScore * 0.45f, 0f, 1800f));
                    int best = PlayerPrefs.GetInt("CostaBest_Drift", 0);
                    if (driftScore > best)
                    {
                        PlayerPrefs.SetInt("CostaBest_Drift", Mathf.RoundToInt(driftScore));
                        PlayerPrefs.Save();
                    }
                    Active = Mode.None;
                    onReward?.Invoke(reward, L($"Drift finished: {driftScore:0} pts (+${reward})",
                        $"Drift concluído: {driftScore:0} pts (+${reward})"));
                }
            }
            else if (Active == Mode.StreetRace)
            {
                raceTimer += dt;
                if (CurrentWaypoint is Vector3 wp && (playerPos - wp).sqrMagnitude < 8.5f * 8.5f)
                {
                    checkpointIndex++;
                    if (checkpointIndex >= raceWaypoints.Length)
                    {
                        int reward = raceTimer < 38f ? 1400 : raceTimer < 55f ? 900 : 500;
                        Active = Mode.None;
                        onReward?.Invoke(reward, L($"Race won in {raceTimer:0.0}s (+${reward})",
                            $"Corrida concluída em {raceTimer:0.0}s (+${reward})"));
                    }
                }
            }
        }

        internal void UpdateUiActivity()
        {
            if (!BlocksWorld)
                return;

            float dt = Time.unscaledDeltaTime;
            sessionTimer -= dt;
            ReadTouch(out bool touchAction, out bool touchHeld, out bool touchExit);
            if (touchExit || Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            {
                Active = Mode.None;
                return;
            }

            bool actionPressed = touchAction || Keyboard.current?.spaceKey.wasPressedThisFrame == true
                || Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
            bool actionHeld = touchHeld || Keyboard.current?.spaceKey.isPressed == true
                || Gamepad.current?.buttonSouth.isPressed == true;

            if (Active is Mode.Basketball or Mode.Arcade or Mode.Gym)
            {
                float speed = Active == Mode.Arcade ? 1.55f : Active == Mode.Basketball ? 1.25f : 1.05f;
                cursor += cursorDir * speed * dt;
                if (cursor >= 1f) { cursor = 1f; cursorDir = -1f; }
                if (cursor <= 0f) { cursor = 0f; cursorDir = 1f; }

                if (actionPressed)
                {
                    float dist = Mathf.Abs(cursor - targetCenter);
                    if (dist <= targetWidth * 0.5f)
                    {
                        score += Active == Mode.Arcade ? 150 : 2;
                        targetCenter = UnityEngine.Random.Range(0.18f, 0.82f);
                        statusText = Active == Mode.Basketball
                            ? L("SWISH! +2 pts", "CESTA! +2 pts")
                            : Active == Mode.Arcade ? L("TARGET HIT! +150", "ALVO ATINGIDO! +150")
                            : L("GOOD LIFT! +2 reps", "BOA SÉRIE! +2 reps");
                    }
                    else
                    {
                        statusText = L("Missed timing!", "Errou o tempo!");
                    }
                }
            }
            else if (Active == Mode.Fishing)
            {
                if (fishPhase == 0)
                {
                    fishWait -= dt;
                    if (fishWait <= 0f)
                    {
                        fishPhase = 1;
                        fishTension = 0.5f;
                        statusText = L("BITE! Hold Space / A to reel in!", "FISGOU! Segure Espaço / A para puxar!");
                    }
                }
                else
                {
                    fishTension += (actionHeld ? 0.62f : -0.48f) * dt;
                    if (fishTension >= 0.96f)
                    {
                        score += 120;
                        fishPhase = 0;
                        fishWait = UnityEngine.Random.Range(1.2f, 3.0f);
                        statusText = L("Caught a Coastal Bass! +120", "Pescou um Robalo Costeiro! +120");
                    }
                    else if (fishTension <= 0.02f)
                    {
                        fishPhase = 0;
                        fishWait = UnityEngine.Random.Range(1.5f, 3.2f);
                        statusText = L("It got away...", "O peixe escapou...");
                    }
                }
            }

            if (sessionTimer <= 0f)
                FinishUi();
        }

        private void FinishUi()
        {
            Mode finished = Active;
            Active = Mode.None;
            string prefKey = finished switch
            {
                Mode.Basketball => "CostaBest_Basket",
                Mode.Fishing => "CostaBest_Fish",
                Mode.Arcade => "CostaBest_Arcade",
                _ => "CostaBest_Gym"
            };
            if (score > bestScore)
            {
                PlayerPrefs.SetInt(prefKey, score);
                PlayerPrefs.Save();
            }
            int cash = finished == Mode.Basketball ? score * 35
                : finished == Mode.Fishing ? score
                : finished == Mode.Arcade ? Mathf.RoundToInt(score * 0.4f)
                : score * 20;
            onReward?.Invoke(cash, L($"Activity complete · Score {score} (+${cash})",
                $"Atividade concluída · Pontuação {score} (+${cash})"));
        }

        private static Rect PanelBounds()
        {
            Rect safe = Screen.safeArea;
            float w = Mathf.Min(safe.width * .96f, Mathf.Max(360f, safe.width * .50f));
            float h = Mathf.Min(safe.height * .92f, Mathf.Max(230f, safe.height * .42f));
            return new Rect(safe.center.x - w * .5f, Screen.height - safe.center.y - h * .5f, w, h);
        }

        private static Rect ActionBounds(Rect panel) => new(panel.x + panel.width * .40f, panel.y + panel.height * .74f, panel.width * .55f, panel.height * .20f);
        private static Rect ExitBounds(Rect panel) => new(panel.x + panel.width * .05f, panel.y + panel.height * .74f, panel.width * .28f, panel.height * .20f);

        private void ReadTouch(out bool action, out bool held, out bool exit)
        {
            action = held = exit = false;
            if (!CostaTouch.Available)
                return;
            Rect panel = PanelBounds();
            foreach (var finger in Touchscreen.current.touches)
            {
                if (!finger.press.isPressed)
                    continue;
                int id = finger.touchId.ReadValue();
                Vector2 point = CostaTouch.ScreenPoint(finger.position.ReadValue());
                if (finger.press.wasPressedThisFrame)
                {
                    exit |= ExitBounds(panel).Contains(point);
                    if (ActionBounds(panel).Contains(point))
                    {
                        action = true;
                        actionFinger = id;
                    }
                }
                held |= actionFinger == id;
            }
            if (!held)
                actionFinger = -1;
        }

        internal void DrawUi()
        {
            if (!BlocksWorld)
                return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            }

            Rect box = PanelBounds();
            Box(box, Panel);
            Box(new Rect(box.x, box.y, box.width, 3f), Accent);

            string heading = Active switch
            {
                Mode.Basketball => L("STREET BASKETBALL", "BASQUETE DE RUA"),
                Mode.Fishing => L("QUAY FISHING", "PESCA NO CAIS"),
                Mode.Arcade => L("NEON ARCADE", "ARCADE NEON"),
                _ => L("IRON & SEA GYM", "ACADEMIA FERRO & MAR")
            };
            Label(new Rect(box.x + 20, box.y + 14, box.width - 125, 30), heading, titleStyle, Paper);
            Label(new Rect(box.xMax - 105, box.y + 18, 100, 24),
                $"{Mathf.CeilToInt(sessionTimer)}s · {score} pts", bodyStyle, Accent);
            Label(new Rect(box.x + 20, box.y + 52, box.width - 40, 24), statusText, bodyStyle, Mint);

            Rect bar = new(box.x + 24, box.y + 98, box.width - 48, 28);
            Box(bar, new Color(0.14f, 0.17f, 0.20f));
            if (Active == Mode.Fishing)
            {
                Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fishTension), bar.height),
                    fishPhase == 1 ? Mint : Accent);
            }
            else
            {
                float tx = bar.x + (targetCenter - targetWidth * 0.5f) * bar.width;
                Box(new Rect(tx, bar.y, targetWidth * bar.width, bar.height), new Color(0.24f, 0.68f, 0.44f));
                float cx = bar.x + cursor * (bar.width - 8f);
                Box(new Rect(cx, bar.y - 4f, 8f, bar.height + 8f), Paper);
            }

            if (CostaTouch.Available)
            {
                Rect action = ActionBounds(box), exit = ExitBounds(box);
                Box(action, actionFinger >= 0 ? Accent : new Color(.20f, .40f, .34f));
                Box(exit, new Color(.20f, .27f, .30f));
                Label(new Rect(action.x + 12, action.y + 4, action.width - 24, action.height - 8),
                    Active == Mode.Fishing ? L("Hold to reel", "Segure para puxar") : L("Action", "Ação"), bodyStyle, Paper);
                Label(new Rect(exit.x + 12, exit.y + 4, exit.width - 24, exit.height - 8), L("Exit", "Sair"), bodyStyle, Paper);
            }
            else
                Label(new Rect(box.x + 20, box.y + box.height * .74f, box.width - 40, 24),
                    L($"Best: {bestScore}  ·  SPACE / A: Action  ·  ESC / B: Exit",
                      $"Recorde: {bestScore}  ·  ESPAÇO / A: Ação  ·  ESC / B: Sair"), bodyStyle, Paper);
        }

        private static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        private static void Box(Rect r, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
