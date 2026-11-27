using UnityEngine;

namespace CostaLivre
{
    internal static class CostaBuildTuning
    {
        internal enum Part { Wheels, Tires, Bumper, Wing, Hood, Exhaust, Lights, Mirrors,
            Paint, Stripe, Seats, CabinLight, Engine, Gearbox, Brakes, Suspension, Grip, Color }
        internal const int Count = 18;
        private const int ColorShift = 34;
        internal static int Option(long build, Part p) => p == Part.Color ? (int)(build >> ColorShift) & 0xffffff
            : (int)(build >> ((int)p*2)) & 3;
        internal static bool Valid(long build) => build >= 0 && (build >> 58) == 0;

        internal static long Change(long build, Part part, int option)
        {
            int shift = part == Part.Color ? ColorShift : (int)part*2;
            long mask = (part == Part.Color ? 0xffffffL : 3L) << shift;
            return (build & ~mask) | (((long)option << shift) & mask);
        }

        internal static int Price(Part part, int level) => part switch
        {
            Part.Color => 360,
            >= Part.Engine and <= Part.Grip => 450 + level*475,
            _ => 160 + level*125
        };

        internal static bool Allowed(Part part, int option, bool bike)
        {
            if ((int)part < 0 || (int)part >= Count || option < 0 || option > (part == Part.Color ? 0xffffff : 3))
                return false;
            return !bike || part is not (Part.Bumper or Part.Wing or Part.Hood or Part.Seats or Part.CabinLight);
        }

        internal static float Engine(long build) => 1 + .10f*Option(build,Part.Engine) + .035f*Option(build,Part.Gearbox);
        internal static float Gearbox(long build) => 1 + .065f*Option(build,Part.Gearbox);
        internal static float Brakes(long build) => 1 + .115f*Option(build,Part.Brakes);
        internal static float Grip(long build) => 1 + .065f*Option(build,Part.Grip) + .045f*Option(build,Part.Tires);
        internal static float Spring(long build) => 1 + .13f*Option(build,Part.Suspension);

        internal static bool AtWorkshop(Vector3 point)
        {
            Vector3 garage = CostaInteriorPlan.Garage.Entry;
            return Mathf.Abs(point.y) < .65f && ((point-garage).sqrMagnitude < 16f*16f ||
                (point-CityWorkshop).sqrMagnitude < 10f*10f);
        }
        internal static readonly Vector3 CityWorkshop = new(-40,0,51);
    }
}
