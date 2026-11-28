using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal static class CostaDealership
    {
        internal readonly struct Listing
        {
            internal readonly int VehicleIndex;
            internal readonly string En;
            internal readonly string Pt;
            internal readonly int Price;
            internal string Name => L(En, Pt);

            internal Listing(int index, string en, string pt, int price)
            {
                VehicleIndex = index;
                En = en;
                Pt = pt;
                Price = price;
            }
        }

        internal static readonly Vector3 SantaLuzShowroom = new(-16f, 0f, 36f);
        internal static readonly Vector3 AntutuShowroom = new(18f, 0f, CostaCity.Districts[1].Center + 13f);

        internal static readonly Listing[] Catalog =
        {
            new(8,  "Brisa Compact",       "Brisa Compact",        2200),
            new(0,  "Santa Luz Sedan",     "Santa Luz Sedan",      2400),
            new(1,  "Praia Sedan",         "Praia Sedan",          2600),
            new(4,  "Maré Sedan",          "Maré Sedan",           2800),
            new(2,  "Rubi Sedan",          "Rubi Sedan",           2900),
            new(5,  "Areia Sedan",         "Areia Sedan",          3000),
            new(10, "Esmeralda Sedan",     "Esmeralda Sedan",      3200),
            new(13, "Calado Sedan",        "Calado Sedan",         3400),
            new(12, "Orla Cruiser",        "Orla Cruiser",         4800),
            new(11, "Tubarão StreetBike",  "Tubarão 600 Moto",     5900),
            new(9,  "Atlântico Executive", "Atlântico Executivo",  6500),
            new(14, "Antutu Executive",    "Antutu Executivo",     7200),
            new(6,  "Solstice Super",      "Solstice Super",      12500),
            new(15, "Vice Flamingo Super", "Vice Flamingo Super", 14000),
            new(7,  "Mirante Track GT",    "Mirante Track GT",    18500)
        };

        internal static bool FindListing(int vehicleIndex, out Listing result)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].VehicleIndex == vehicleIndex)
                {
                    result = Catalog[i];
                    return true;
                }
            }
            result = default;
            return false;
        }

        internal static bool AtDealership(Vector3 point) =>
            Mathf.Abs(point.y) < 1.5f &&
            ((point - SantaLuzShowroom).sqrMagnitude < 4.5f * 4.5f
             || (point - AntutuShowroom).sqrMagnitude < 4.8f * 4.8f);

        internal static bool AtGarage(Vector3 point, CostaJobs jobs, bool online)
        {
            if (Mathf.Abs(point.y) > 1.5f)
                return false;
            if ((point - CostaInteriorPlan.Garage.Entrance).sqrMagnitude < 5.5f * 5.5f)
                return true;
            if (!online && jobs != null)
            {
                for (int i = 0; i < CostaCity.Estate.Length; i++)
                    if (jobs.Owned(i) && (point - CostaCity.Estate[i]).sqrMagnitude < 7.5f * 7.5f)
                        return true;
            }
            return false;
        }

        internal static void BuildShowroomKiosk(HarborBlockout art)
        {
            Vector3 p = SantaLuzShowroom;
            Color coral = new(0.90f, 0.44f, 0.36f);
            Color cream = new(0.94f, 0.89f, 0.80f);
            Color teal = new(0.14f, 0.64f, 0.66f);
            art.Box("Dealership platform", p + new Vector3(0f, 0.08f, 0f), new Vector3(6.4f, 0.16f, 4.6f), cream, true);
            art.Box("Dealership awning", p + new Vector3(0f, 3.15f, 0.2f), new Vector3(6.2f, 0.22f, 4.2f), teal);
            for (int side = -1; side <= 1; side += 2)
                art.Box("Dealership pillar", p + new Vector3(side * 2.8f, 1.58f, 1.9f), new Vector3(0.24f, 3.16f, 0.24f), coral, true);
            art.NeonTrim(p + new Vector3(0f, 3.30f, -1.92f), new Vector3(6.0f, 0.12f, 0.08f), new Color(0.18f, 0.86f, 0.86f));
            art.Sign("COSTA MOTORS", p + new Vector3(0f, 3.12f, -1.96f), 0.14f, cream);
        }

        internal static bool TryFindDeliverySpot(Vector3 near, float playerYaw, out Vector3 spot, out float heading)
        {
            Vector3 forward = Quaternion.Euler(0, playerYaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, playerYaw, 0) * Vector3.right;
            Vector3[] offsets =
            {
                forward * 5.2f,
                right * 4.8f,
                -right * 4.8f,
                forward * 7.5f + right * 3.2f,
                forward * 7.5f - right * 3.2f,
                -forward * 5.5f
            };
            Vector3 halfExtents = new(1.2f, 0.85f, 2.35f);
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 candidate = near + offsets[i];
                candidate.y = 0f;
                Quaternion rot = Quaternion.Euler(0, playerYaw + 90f, 0);
                if (!Physics.CheckBox(candidate + Vector3.up * 1.05f, halfExtents, rot,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spot = candidate;
                    heading = playerYaw + 90f;
                    return true;
                }
            }
            spot = near + forward * 4.5f;
            spot.y = 0f;
            heading = playerYaw;
            return false;
        }
    }
}
