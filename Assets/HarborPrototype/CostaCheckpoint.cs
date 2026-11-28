using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    [Serializable]
    internal sealed class CostaCheckpoint
    {
        public string node;
        public Vector3 position, vehiclePosition;
        public Vector2 damage;
        public int health, vehicle = -1;
        public float yaw, pitch, stamina, vehicleYaw;
        public double daylight;
        public bool atObjective;

        internal bool Valid(string expected) => node == expected && !string.IsNullOrEmpty(node) &&
            Point(position) && (vehicle < 0 || Point(vehiclePosition)) && vehicle is >= -1 and < 16 &&
            health is > 0 and <= 100 && float.IsFinite(yaw) && float.IsFinite(pitch) &&
            float.IsFinite(stamina) && stamina >= 0 && double.IsFinite(daylight) && daylight >= 0 &&
            float.IsFinite(vehicleYaw) && float.IsFinite(damage.x) && float.IsFinite(damage.y) &&
            damage.x is >= 0 and <= 1 && damage.y is >= 0 and <= 1;

        private static bool Point(Vector3 value) =>
            value.x is > -700 and < 700 && value.y is > -3 and < 140 && value.z is > -300 and < 1060;
    }

    public sealed partial class HarborRun
    {
        private CostaCheckpoint CaptureCheckpoint(string node)
        {
            if (session.Online || session.Hosting || wantedLevel != 0 || pursuit || vehicleMotion.Active ||
                soloSeat >= 0 || soloLift >= 0 || encounter.Active || encounter.Health <= 0 ||
                (car == null ? !grounded || groundVelocity.sqrMagnitude > .64f : car.Speed > .8f))
                return null;
            Vector3 position = player.transform.position;
            Vector3 road = car?.Root.position ?? position;
            if (!Physics.Raycast(road + Vector3.up * .05f, Vector3.down, .78f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;
            var checkpoint = new CostaCheckpoint
            {
                node = node, position = position, yaw = yaw, pitch = pitch, health = encounter.Health,
                stamina = stamina, daylight = soloDaySeconds, atObjective = true
            };
            if (car != null)
            {
                checkpoint.vehicle = Array.IndexOf(cars, car);
                if (checkpoint.vehicle < 0)
                    return null;
                checkpoint.vehiclePosition = car.Root.position;
                checkpoint.vehicleYaw = car.Root.eulerAngles.y;
                checkpoint.damage = car.Damage;
            }
            return checkpoint.Valid(node) ? checkpoint : null;
        }

        private void TrySaveStage()
        {
            if (session.Online || session.Campaign == null || Time.unscaledTime < checkpointRetry ||
                !CanOpenCampaign())
                return;
            string node = session.Campaign.Current(CampaignProgress).id;
            if (jobs.Account.checkpoint is { atObjective: true } saved && saved.node == node)
                return;

            checkpointRetry = Time.unscaledTime + 1.25f;
            CostaCheckpoint snapshot = CaptureCheckpoint(node);
            if (snapshot != null && jobs.Ledger.SaveCheckpoint(jobs.Account, snapshot))
                ShowWorldNotice(L("Stage checkpoint saved", "Checkpoint da etapa salvo"));
        }

        private float checkpointRetry;

        private bool RestoreCheckpoint()
        {
            if (session.Online || session.Hosting || jobs.InProgress ||
                jobs.Account.checkpoint is not CostaCheckpoint saved || !saved.Valid(session.Campaign?.Current(jobs.Account.campaign).id) ||
                saved.vehicle >= cars.Length)
                return false;

            Physics.SyncTransforms();
            Vector3 road = saved.vehicle >= 0 ? saved.vehiclePosition : saved.position;
            if (!Physics.Raycast(road + Vector3.up * .12f, Vector3.down, .9f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            ResetRun();
            StopPursuit();
            harborJob = false;
            cargo.SetActive(false);
            player.enabled = false;
            player.transform.position = saved.position;
            playerModel.rotation = Quaternion.Euler(0, saved.yaw, 0);
            airbornePeak = saved.position.y;
            yaw = saved.yaw;
            pitch = Mathf.Clamp(saved.pitch, -68, 78);
            stamina = Mathf.Clamp(saved.stamina, 0, FullStamina);
            soloDaySeconds = saved.daylight;
            encounter.RestoreHealth(saved.health - encounter.Health);
            player.enabled = true;

            if (saved.vehicle >= 0)
            {
                CostaVehicle vehicle = cars[saved.vehicle];
                vehicle.Root.SetPositionAndRotation(saved.vehiclePosition, Quaternion.Euler(0, saved.vehicleYaw, 0));
                vehicle.FollowPresentation();
                vehicle.SetDamage(saved.damage);
                EnterVehicle(vehicle);
                player.transform.position = vehicle.Root.position;
            }

            PositionCamera(false, true);
            ApplyWorldHour();
            transitions.Begin(L("Stage restored", "Etapa restaurada"), 1.35f, .83f);
            return true;
        }
    }
}
