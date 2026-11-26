using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaVenueLife
    {
        private sealed class Venue
        {
            internal string En, Pt;
            internal Vector3 Kiosk;
            internal HarborCharacter Clerk;
            internal Light Lamp;
            internal Vector3 WorkStart, WorkEnd;
        }

        private readonly Venue[] venues = new Venue[6];
        private float updateAt;
        private float noticeUntil;
        private string notice = "";
        private readonly Transform root;
        private readonly HarborBlockout art;
        private static readonly Color Wood = new(.43f, .29f, .20f);
        private static readonly Color Linen = new(.74f, .70f, .61f);
        private static readonly Color Bronze = new(.63f, .46f, .24f);
        private static readonly Color Screen = new(.22f, .37f, .41f);
        private static readonly Color Green = new(.21f, .37f, .30f);

        internal CostaVenueLife(HarborBlockout art, Transform root)
        {
            this.root = root;
            this.art = art;
            Add(0, CostaCity.Hotel, "Hotel directory", "Guia do hotel", -.9f, 2.9f);
            Add(1, CostaCity.Jeweler, "Jewellery catalogue", "Catálogo da joalheria", .9f, 2.5f);
            Add(2, CostaCity.Museum, "Exhibition guide", "Guia da exposição", -.85f, 3.5f);
            Add(3, CostaCity.Electronics, "Stock terminal", "Terminal de estoque", -.9f, 2.8f);
            Add(4, CostaCity.Station, "Public information", "Informações ao público", -.9f, 2.65f);
            Add(5, new Vector3(-113, 0, 67), "Café menu", "Cardápio do café", .85f, 1.9f);
        }

        private void Add(int index, Vector3 entry, string en, string pt, float side, float depth)
        {
            Vector3 kiosk = entry + new Vector3(side * 2.45f, 0, depth);
            art.Box("Visitor counter", kiosk + Vector3.up * .48f, new(1.17f, .96f, .57f),
                Wood, true);
            art.Box("Counter stone top", kiosk + Vector3.up * .99f, new(1.29f, .095f, .67f), Linen);
            var display = art.Box("Public information terminal", kiosk + new Vector3(0, 1.16f, -.075f),
                new(.32f, .27f, .07f), Screen, finish: HarborBlockout.Finish.Glass);
            display.transform.rotation = Quaternion.Euler(-17, 0, 0);

            Vector3 fixture = kiosk + new Vector3(0, 2.62f, -.12f);
            art.Box("Pendant housing", fixture + Vector3.up * .08f, new(.49f, .14f, .47f), Bronze);
            var lamp = new GameObject("Venue pendant").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = fixture;
            lamp.transform.rotation = Quaternion.Euler(90, 0, 0);
            lamp.type = LightType.Spot;
            lamp.range = 7f;
            lamp.spotAngle = 91;
            lamp.innerSpotAngle = 65;
            lamp.color = new Color(1f, .80f, .60f);
            lamp.shadows = LightShadows.None;

            Vector3 clerkStart = kiosk + new Vector3(-.46f, .03f, .90f);
            HarborCharacter clerk = art.Person("Venue attendant", root, clerkStart, Color.white);
            clerk.WearTexture(Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{index + 6}"));
            venues[index] = new Venue
            {
                En = en, Pt = pt, Kiosk = kiosk, Clerk = clerk, Lamp = lamp,
                WorkStart = clerkStart, WorkEnd = clerkStart + Vector3.right * 1.13f
            };

            Vector3 nook = entry + new Vector3(side * (index == 5 ? -3.7f : -5.3f), 0, depth + .65f);
            switch (index)
            {
                case 0:
                    Seating(nook, Green);
                    art.Box("Reception rug", nook + new Vector3(0, .037f, .78f), new(3.2f,.014f,2.5f), new Color(.48f,.27f,.23f));
                    break;
                case 1:
                    Shelf(nook, Bronze, true);
                    break;
                case 2:
                    art.Box("Exhibit plinth", nook + Vector3.up * .44f, new(.91f,.88f,.88f), Linen, true);
                    art.Part(root, PrimitiveType.Capsule, "Ceramic vessel", nook + Vector3.up * 1.10f, new(.40f,.30f,.40f), Bronze);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 pos = nook + new Vector3(i * .85f, 1.37f, 1.8f);
                        art.Box("Exhibition divider panel", pos, new(.72f,1.45f,.11f), Wood, true);
                        art.Box("Mounted canvas", pos + Vector3.back * .067f, new(.57f,.73f,.018f),
                            Color.Lerp(Linen, Green, i * .31f));
                    }
                    break;
                case 3:
                    Shelf(nook, Screen, false);
                    break;
                case 4:
                    Seating(nook, Screen);
                    break;
                case 5:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 table = nook + Vector3.forward * (i * 1.64f);
                        art.Box("Cafe table", table + Vector3.up * .67f, new(.95f,.12f,.79f), Wood, true);
                        art.Box("Cafe table pedestal", table + Vector3.up * .33f, new(.20f,.65f,.2f), Bronze);
                        art.Part(root, PrimitiveType.Cylinder, "Cup", table + new Vector3(.19f,.80f,.08f),
                            new(.12f,.068f,.12f), Linen);
                        art.Box("Cafe chair", table + new Vector3(-.68f,.23f,0), new(.39f,.46f,.41f), Green, true);
                    }
                    break;
            }
        }

        private void Seating(Vector3 at, Color upholstery)
        {
            art.Box("Upholstered bench", at + Vector3.up * .28f, new(1.70f, .55f, .67f), upholstery, true);
            art.Box("Bench back", at + new Vector3(0, .62f, .29f), new(1.70f, .55f, .16f), upholstery);
            art.Box("Low visitor table", at + new Vector3(0,.27f,-.84f), new(.94f,.54f,.58f), Wood, true);
            art.Box("Visitor brochures", at + new Vector3(.15f,.565f,-.84f), new(.27f,.052f,.18f), Linen);
        }

        private void Shelf(Vector3 at, Color display, bool jewels)
        {
            art.Box("Display cabinet", at + Vector3.up * .46f, new(1.81f,.92f,.57f), Wood, true);
            art.Box("Display shelf", at + new Vector3(0,1.48f,.25f), new(1.79f,1.13f,.08f), Wood);
            for (int level = 0; level < 2; level++)
            {
                float y = 1.03f + level * .48f;
                art.Box("Shelf rail", at + Vector3.up * y, new(1.81f,.08f,.58f), Linen);
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 stock = at + new Vector3(i * .52f, y + .16f, .01f);
                    if (jewels)
                        art.Part(root, PrimitiveType.Sphere, "Jewellery display", stock, new(.16f,.12f,.15f), display,
                            finish: HarborBlockout.Finish.Metal);
                    else
                    {
                        art.Box("Computer display", stock, new(.30f,.22f,.065f), display,
                            finish: HarborBlockout.Finish.Glass);
                        art.Box("Device stand", stock + Vector3.down * .13f, new(.16f,.04f,.12f), Bronze);
                    }
                }
            }
        }

        internal void Update(float hour, Vector3 viewer)
        {
            float night = HarborDaylight.CityNight(hour);
            bool rethink = Time.unscaledTime >= updateAt;
            if (rethink)
                updateAt = Time.unscaledTime + .13f;
            for (int i = 0; i < venues.Length; i++)
            {
                Venue venue = venues[i];
                bool nearby = (viewer - venue.Kiosk).sqrMagnitude < 64 * 64;
                if (rethink)
                {
                    venue.Lamp.enabled = nearby;
                    venue.Lamp.intensity = Mathf.Lerp(1.2f, 2.6f, night);
                }
                float open = i == 0 || i == 4 ? 1 : 1 - night;
                // Attendants slowly move between service and stock during business hours.
                float phase = Mathf.Repeat(hour * 3.75f + i * .71f, 4);
                float walk = phase < 1 ? Mathf.SmoothStep(0, 1, phase)
                    : phase < 2 ? 1 : phase < 3 ? 1 - Mathf.SmoothStep(0, 1, phase - 2) : 0;
                bool visible = nearby && open > .06f;
                if (venue.Clerk.Root.gameObject.activeSelf != visible)
                    venue.Clerk.Root.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                Vector3 position = Vector3.Lerp(venue.WorkStart, venue.WorkEnd, walk * open);
                Vector3 travel = position - venue.Clerk.Root.position;
                if (travel.sqrMagnitude is > .000002f and < .3025f)
                {
                    float heading = travel.x > 0 ? 90f : -90f;
                    venue.Clerk.Root.rotation = Quaternion.RotateTowards(venue.Clerk.Root.rotation,
                        Quaternion.Euler(0, heading, 0), Time.unscaledDeltaTime * 230f);
                    venue.Clerk.Animate(HarborCharacter.Motion.Walk, .62f);
                }
                else
                {
                    venue.Clerk.Root.rotation = Quaternion.RotateTowards(venue.Clerk.Root.rotation,
                        Quaternion.Euler(0, 180, 0), Time.unscaledDeltaTime * 185f);
                    venue.Clerk.Animate(HarborCharacter.Motion.Idle);
                }
                venue.Clerk.Root.position = position;
            }
        }

        internal bool Use(Vector3 player)
        {
            int index = Find(player);
            if (index < 0)
                return false;
            notice = index switch
            {
                0 => L("Atlântico: reception, guest lounge and sea view.", "Atlântico: recepção, sala de hóspedes e vista para o mar."),
                1 => L("Áurea: coastal gems and handcrafted pieces.", "Áurea: gemas da costa e peças artesanais."),
                2 => L("Museum: maritime trade and the Old Quay collection.", "Museu: comércio marítimo e coleção do Cais Velho."),
                3 => L("Showroom: compact computers and graphics workstations.", "Mostruário: computadores compactos e estações gráficas."),
                4 => L("Santa Luz station: public service and lost property.", "Delegacia de Santa Luz: atendimento e achados e perdidos."),
                _ => L("Café da Praça: espresso, fresh bread and orange cake.", "Café da Praça: espresso, pão fresco e bolo de laranja.")
            };
            noticeUntil = Time.unscaledTime + 4;
            return true;
        }

        internal string Prompt(Vector3 player)
        {
            int index = Find(player);
            return index < 0 ? "" : L(venues[index].En, venues[index].Pt);
        }

        internal string Notice => Time.unscaledTime < noticeUntil ? notice : "";

        private int Find(Vector3 player)
        {
            for (int i = 0; i < venues.Length; i++)
            {
                Vector3 kiosk = venues[i].Kiosk;
                if ((player - (kiosk + Vector3.back * .6f)).sqrMagnitude < 1.2f * 1.2f &&
                    !Physics.Linecast(player + Vector3.up * 1.18f, kiosk + Vector3.up * 1.20f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return i;
            }
            return -1;
        }

        internal void Release()
        {
            foreach (Venue venue in venues)
                venue?.Clerk.Release();
        }
    }
}
