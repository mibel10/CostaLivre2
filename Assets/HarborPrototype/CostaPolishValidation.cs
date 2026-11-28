#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        public void ValidatePolish(Action<int> finish) => StartCoroutine(RunPolishChecks(finish));

        private IEnumerator RunPolishChecks(Action<int> finish)
        {
            IEnumerator checks = PolishChecks();
            while (true)
            {
                bool more;
                try
                {
                    more = checks.MoveNext();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    finish(1);
                    yield break;
                }
                if (!more)
                    break;
                yield return checks.Current;
            }
            finish(0);
        }

        private IEnumerator PolishChecks()
        {
            yield return null;
            CostaCampaign campaign = CostaCampaign.Load();
            Require(campaign != null && campaign.Current(null).id == "intro", "authored campaign startup");
            string ledgerFile = Path.Combine(Path.GetTempPath(), $"costa-polish-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(ledgerFile, "{\"version\":1,\"accounts\":[{\"key\":\"test\",\"cash\":4500,\"mods\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"history\":[]}]}");
                var ledger = new CostaLedger(ledgerFile);
                var account = ledger.Get("test");
                Require(account.cash == 4500 && account.checkpoint == null, "version 1 save without checkpoint");
                CostaCampaign.TryPoint("press", out Vector3 press);
                CostaCampaign.TryPoint("workshop", out Vector3 workshop);
                Require(!campaign.TryAdvance(ledger, account, 1, "intro", 1, workshop, false, null), "branch position validation");
                var checkpoint = new CostaCheckpoint { node = "intro", health = 81, position = press, stamina = 3, atObjective = true };
                Require(campaign.TryAdvance(ledger, account, 1, "intro", 1, press, false, null, snapshot: checkpoint)
                    && account.campaign.node == "rui" && account.checkpoint.node == "rui" && !account.checkpoint.atObjective,
                    "atomic branch and checkpoint");
                Require(campaign.TryAdvance(ledger, account, 2, "rui", 2, workshop, false, null)
                    && (account.campaign.flags & 4096) != 0 && account.campaign.node == "rui"
                    && !campaign.TryAdvance(ledger, account, 2, "rui", 3, workshop, false, null), "optional objective is persistent and unique");
                int cash = account.cash;
                Require(ledger.Apply(account, CostaLedger.Operation.Reward, 500, relic: 1)
                    && !ledger.Apply(account, CostaLedger.Operation.Reward, 500, relic: 1) && account.cash == cash + 500,
                    "relic reward once");
                var reloaded = new CostaLedger(ledgerFile).Get("test");
                Require(reloaded.checkpoint?.health == 81 && reloaded.campaign.decisions.Length == 2 && reloaded.relics == 2,
                    "checkpoint, decisions and relic survive reload");
            }
            finally
            {
                File.Delete(ledgerFile);
                File.Delete(ledgerFile + ".old");
                File.Delete(ledgerFile + ".tmp");
            }

            WithWorld(null);
            double began = Time.realtimeSinceStartupAsDouble;
            double expires = began + 1200;
            double progressReport = began + 20;
            while (!worldReady && !worldLoadFailed && Time.realtimeSinceStartupAsDouble < expires)
            {
                if (Time.realtimeSinceStartupAsDouble >= progressReport)
                {
                    Debug.Log($"COSTA CHECK loading {loadingProgress:P0} {loadingPhase}.");
                    progressReport = Time.realtimeSinceStartupAsDouble + 25;
                }
                yield return null;
            }
            Require(worldReady, $"world initialization and NavMesh. Phase {loadingPhase} at {loadingProgress:P0}");
            Debug.Log($"COSTA CHECK world ready in {Time.realtimeSinceStartupAsDouble - began:F1} seconds.");

            var testBackdrop = new CostaTitleBackdrop();
            testBackdrop.Begin();
            testBackdrop.Position(followCamera);
            Vector3 initial = followCamera.transform.position;
            float initialHour = testBackdrop.Hour;
            yield return null;
            testBackdrop.Position(followCamera);
            Require(initial != followCamera.transform.position || testBackdrop.Hour > initialHour, "title camera runs on unscaled time");
            Require(menu.HudMode is HarborHud.Mode.Full or HarborHud.Mode.Contextual or HarborHud.Mode.Minimal, "saved HUD mode");
            Require(CostaMirrors.MirrorResolution(0, 0) == 0 && CostaMirrors.MirrorResolution(2, 0) == 256
                && CostaMirrors.MirrorResolution(3, 4) == 512, "mirror presets");
            Vector3 origin = new(5, 2, 1);
            Vector3 point = new(8, 3, 2);
            Matrix4x4 reflection = CostaMirrors.ReflectionMatrix(origin, Vector3.right);
            Require((reflection.MultiplyPoint(point) - new Vector3(2, 3, 2)).sqrMagnitude < .001f &&
                (reflection.MultiplyPoint(reflection.MultiplyPoint(point)) - point).sqrMagnitude < .001f, "planar reflection matrix");

            var controller = Resources.Load<RuntimeAnimatorController>("Harbor/DirectionalGait");
            Require(controller != null && controller.animationClips.Length == 5, "directional Blend Tree with five motions");
            playerBody.Animate(HarborCharacter.Motion.Walk, snap: true);
            var movement = Stopwatch.StartNew();
            for (int i = 0; i < 240; i++)
            {
                playerBody.Animate(HarborCharacter.Motion.Walk);
                playerBody.DirectionalGait(playerModel.right * 2.8f - playerModel.forward, 6, i == 0 ? 174 : 0, .75f);
                playerBody.PlantFeet(player);
            }
            movement.Stop();
            Debug.Log($"COSTA CHECK movement 240 updates CPU {movement.Elapsed.TotalMilliseconds / 240:F3} ms/update.");
            Require(float.IsFinite(playerBody.Head.rotation.w), "directional pivot, braking and foot placement");
            playerBody.Animate(HarborCharacter.Motion.Idle, snap: true);

            CostaCinematic arrival = Resources.Load<CostaCinematic>("Harbor/Cinematics/SantaLuzArrival");
            Require(arrival != null && arrival.shots.Length >= 2 &&
                arrival.lines.All(line => line.voice != null && line.voice.length > .2f && !string.IsNullOrEmpty(line.portuguese)
                    && !string.IsNullOrEmpty(line.english)), "cinematic assets, voiced lines and subtitles");
            Vector3 cameraStart = followCamera.transform.position;
            int completed = 0;
            Require(cinematics.Play(arrival, playerBody, player, () => completed++), "Cinemachine Timeline started");
            expires = Time.realtimeSinceStartupAsDouble + 6;
            while (Vector3.Distance(followCamera.transform.position, cameraStart) < .1f &&
                cinematics.Active && Time.realtimeSinceStartupAsDouble < expires)
                yield return null;
            Require(Vector3.Distance(followCamera.transform.position, cameraStart) >= .1f, "Cinemachine drives the output camera");
            cinematics.Finish();
            cinematics.Finish();
            Require(completed == 1 && !cinematics.Active && Vector3.Distance(followCamera.transform.position, cameraStart) < .001f,
                "cinematic skip restores gameplay only once");

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Debug.Log("COSTA CHECK no graphics device for mirror capture.");
            else
            {
                var mirrorRoot = new GameObject("Validation mirror");
                mirrorRoot.transform.position = new Vector3(0, 15, -210);
                var mirrors = new CostaMirrors(followCamera);
                mirrors.Install(mirrorRoot.transform, Vector3.zero, Vector3.back, new Vector2(2, 1.4f));
                yield return null;
                float mirrorBegin = Time.unscaledTime;
                double time = 0;
                while (Time.unscaledTime < mirrorBegin + 1.25f)
                {
                    followCamera.transform.SetPositionAndRotation(mirrorRoot.transform.position + Vector3.back * 3, Quaternion.identity);
                    var capture = Stopwatch.StartNew();
                    mirrors.Tick(2, 3, true, null);
                    time += capture.Elapsed.TotalMilliseconds;
                    yield return null;
                }
                Require(mirrors.Captures >= 2 && mirrors.Captures <= 16, "URP mirror capture budget");
                Debug.Log($"COSTA CHECK mirrors {mirrors.Captures} captures at 256 px. CPU including scans {time / mirrors.Captures:F2} ms per capture.");
                mirrors.Release();
                Destroy(mirrorRoot);
            }

            using var guest = new CostaSession(cars) { Campaign = campaign };
            int port;
            var reserve = new TcpListener(IPAddress.Loopback, 0);
            reserve.Start();
            port = ((IPEndPoint)reserve.LocalEndpoint).Port;
            reserve.Stop();
            const string identityKey = "CostaResumeIdentity";
            string identity = PlayerPrefs.GetString(identityKey, "");
            session.Start("PolishHost", "127.0.0.1", port, true);
            string hostIdentity = PlayerPrefs.GetString(identityKey, "");
            using (var guestIdentity = new RSACryptoServiceProvider(2048))
                PlayerPrefs.SetString(identityKey, guestIdentity.ToXmlString(true));
            guest.Start("PolishGuest", "127.0.0.1", port, false);
            PlayerPrefs.SetString(identityKey, identity.Length == 0 ? hostIdentity : identity);
            expires = Time.realtimeSinceStartupAsDouble + 20;
            while ((!session.Online || !guest.Online) && Time.realtimeSinceStartupAsDouble < expires)
            {
                guest.Tick(null);
                yield return null;
            }
            Require(session.Online && guest.Online && guest.Local.id != session.Local.id, "host and client both online");
            Require(session.Players.Any(player => player.id == guest.Local.id), "guest present on host");
            var pose = new CostaSession.Avatar { playing = true, revision = guest.Local.revision, pos = guest.Local.pos, yaw = guest.Local.yaw };
            expires = Time.realtimeSinceStartupAsDouble + 5;
            while (!guest.Local.playing && Time.realtimeSinceStartupAsDouble < expires)
            {
                guest.Tick(pose);
                yield return null;
            }
            Require(guest.Local.playing, "host accepted guest deployment");
            guest.CampaignRequest("intro", 0);
            expires = Time.realtimeSinceStartupAsDouble + 5;
            while (guest.CurrentNotice.Length == 0 && Time.realtimeSinceStartupAsDouble < expires)
            {
                guest.Tick(pose);
                yield return null;
            }
            Require(guest.CurrentNotice.Length > 0 && guest.Narrative?.node == null, "host rejects campaign from outside checkpoint");
            session.Dispose();
            yield return null;
            yield return null;
            Require(!session.Online && !wasOnline, "return to solo after hosting");
            Debug.Log("COSTA CHECK finished.");
        }

        private static void Require(bool result, string label)
        {
            if (!result)
                throw new InvalidOperationException("COSTA CHECK FAILED " + label);
            Debug.Log("COSTA CHECK passed " + label);
        }
    }
}
#endif
