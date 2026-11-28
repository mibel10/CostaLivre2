using UnityEngine;

namespace CostaLivre
{
    internal static class CostaBuildTuning
    {
        internal enum Part { Wheels, Tires, Bumper, Wing, Hood, Exhaust, Lights, Mirrors,
            Paint, Stripe, Seats, CabinLight, Engine, Gearbox, Brakes, Suspension, Grip, Color,
            Underglow, WindowTint, Nitro }
        internal const int Count = 21;
        private const int ColorShift = 34;
        private const int UnderglowShift = 58;
        private const int WindowTintShift = 60;
        private const int NitroShift = 61;

        internal static int Option(long build, Part p) => p switch
        {
            Part.Color => (int)(build >> ColorShift) & 0xffffff,
            Part.Underglow => (int)(build >> UnderglowShift) & 3,
            Part.WindowTint => (int)(build >> WindowTintShift) & 1,
            Part.Nitro => (int)(build >> NitroShift) & 1,
            _ => (int)(build >> ((int)p * 2)) & 3
        };
        internal static bool Valid(long build) => build >= 0 && (build >> 62) == 0;

        internal static int MaxOption(Part part) => part switch
        {
            Part.Color => 0xffffff,
            Part.WindowTint or Part.Nitro => 1,
            _ => 3
        };

        internal static long Change(long build, Part part, int option)
        {
            int shift = part switch
            {
                Part.Color => ColorShift,
                Part.Underglow => UnderglowShift,
                Part.WindowTint => WindowTintShift,
                Part.Nitro => NitroShift,
                _ => (int)part * 2
            };
            long mask = (long)MaxOption(part) << shift;
            return (build & ~mask) | (((long)option << shift) & mask);
        }

        internal static int Price(Part part, int level) => part switch
        {
            Part.Color => 360,
            Part.Underglow => 320 + level * 140,
            Part.WindowTint => 280,
            Part.Nitro => 1250,
            >= Part.Engine and <= Part.Grip => 450 + level*475,
            _ => 160 + level*125
        };

        internal static bool Allowed(Part part, int option, bool bike)
        {
            if ((int)part < 0 || (int)part >= Count || option < 0 || option > MaxOption(part))
                return false;
            return !bike || part is not (Part.Bumper or Part.Wing or Part.Hood or Part.Seats or Part.CabinLight or Part.WindowTint);
        }

        internal static bool HasNitro(long build) => Option(build, Part.Nitro) > 0;
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
