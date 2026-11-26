using UnityEngine;

namespace CostaLivre
{
    // The logical player and vehicle are already at the host-approved destinations.
    // Only the rendered body and the decorative door use this intermediate path.
    internal sealed class CostaVehicleMotion
    {
        internal const float EnterTime = 1.10f;
        internal const float ExitTime = 0.58f;

        private CostaVehicle vehicle;
        private bool entering;
        private int side;
        private Vector3 start;
        private Vector3 step;
        private Vector3 center;
        private Quaternion heading;
        private float endsAt;

        internal bool Active => vehicle != null;
        internal bool Entering => entering;

        internal void Begin(CostaVehicle target, bool enter, int doorSide, Vector3 transitionStart,
            Vector3 stand, Vector3 vehicleCenter, float yaw, float secondsLeft)
        {
            vehicle = target;
            entering = enter;
            side = doorSide;
            start = transitionStart;
            step = stand;
            center = vehicleCenter;
            heading = Quaternion.Euler(0, yaw, 0);
            endsAt = Time.realtimeSinceStartup + secondsLeft;
        }

        internal void Begin(CostaSession.Avatar avatar, CostaVehicle[] cars)
        {
            Clear();
            if (avatar.doorTime > 0 && avatar.doorCar >= 0 && avatar.doorCar < cars.Length
                && (avatar.doorSide == -1 || avatar.doorSide == 1))
                Begin(cars[avatar.doorCar], avatar.vehicle >= 0, avatar.doorSide, avatar.doorStart,
                    avatar.doorStep, avatar.doorCenter, avatar.doorYaw, avatar.doorTime);
        }

        internal bool Sample(HarborCharacter body)
        {
            if (!Active)
                return false;
            float remaining = endsAt - Time.realtimeSinceStartup;
            if (remaining <= 0)
            {
                Clear();
                return false;
            }

            float progress = Mathf.Clamp01(1f - remaining / (entering ? EnterTime : ExitTime));
            Vector3 right = heading * Vector3.right * side;
            Vector3 seat = center + right * 0.46f + heading * Vector3.back * 0.18f;
            float approach = entering ? Ease(progress / 0.29f) : 1f;
            float slip = entering ? Ease((progress - 0.58f) / 0.35f) : 1f - Ease(progress / 0.72f);
            Vector3 bodyPosition = Vector3.Lerp(Vector3.Lerp(start, step, approach), seat, slip);
            Quaternion towardDoor = Quaternion.LookRotation(entering ? -right : right);
            Quaternion rotation = Quaternion.Slerp(towardDoor, heading, slip);
            if (entering && approach < 1f && (step - start).sqrMagnitude > 0.03f)
            {
                Vector3 travel = step - start;
                travel.y = 0;
                if (travel.sqrMagnitude > 0.01f)
                    rotation = Quaternion.Slerp(Quaternion.LookRotation(travel), towardDoor, approach);
            }

            body.Root.gameObject.SetActive(true);
            body.Root.SetPositionAndRotation(bodyPosition, rotation);
            float door = entering ? Ease((progress - 0.42f) / 0.19f) * (1f - Ease((progress - 0.85f) / 0.15f))
                : Ease(progress / 0.22f) * (1f - Ease((progress - 0.65f) / 0.35f));
            vehicle.PoseDoor(side, door);
            body.VehiclePose(entering, progress, slip, side, vehicle.IsBike);
            return true;
        }

        internal void Clear()
        {
            vehicle?.PoseDoor(side, 0);
            vehicle = null;
        }

        private static float Ease(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
    }
}
