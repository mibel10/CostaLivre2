using System;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaTraffic
    {
        [Serializable]
        internal sealed class Car
        {
            public Vector3 position;
            public float yaw;
            public float speed;
        }

        [Serializable]
        internal sealed class CityEvent
        {
            public int site = -1;
            public int kind;
            public float remaining;
        }

        // Right-hand lanes on the existing marked streets. Paths do not cross
        // buildings or use the player vehicle spawn positions as waypoints.
        private static readonly Vector3[][] Routes =
        {
            Loop(-79, -30, 29, 76), Loop(-30, 18, 76, 123), Loop(-123, -79, 76, 123)
        };
        private static readonly Vector3[] EventSites =
        {
            new(-32.25f, 0, 61), new(20.25f, 0, 108), new(-81.25f, 0, 98)
        };
        private readonly Car[] state = new Car[6];
        private readonly CostaVehicle[] cars = new CostaVehicle[6];
        private readonly RaycastHit[] obstacles = new RaycastHit[12];
        private readonly int[] segment = new int[6];
        private readonly Renderer[,,] lamps = new Renderer[3, 2, 3];
        private readonly HarborCharacter[] streetCrew = new HarborCharacter[3];
        private readonly GameObject[,] eventProps = new GameObject[3, 3];
        private readonly Renderer[,] beacons = new Renderer[3, 3];
        private readonly System.Random random = new();
        private readonly MaterialPropertyBlock light = new();
        private readonly MaterialPropertyBlock hazard = new();
        private readonly CityEvent incident = new();
        private float nextEvent = 24;
        private float accumulator;
        private float motorHour;
        private bool running;
        private Car[] lastTrafficFrame;
        private CityEvent lastEventFrame;

        internal CostaTraffic(HarborBlockout art, Transform root)
        {
            for (int i = 0; i < cars.Length; i++)
                cars[i] = new CostaVehicle(art, root, Vector3.zero, 0,
                    Color.Lerp(new Color(.21f, .31f, .34f), new Color(.53f, .38f, .28f), i / 5f),
                    style: i % 2 == 0 ? CostaVehicle.Style.Compact : CostaVehicle.Style.Sedan, traffic: true);

            for (int i = 0; i < EventSites.Length; i++)
            {
                Vector3 site = EventSites[i];
                float x = i == 0 ? -30 : i == 1 ? 18 : -79;
                float z = i == 0 ? 76 : 123;
                BuildSignal(art, i, 0, new Vector3(x - 4.4f, 0, z + 4.4f));
                BuildSignal(art, i, 1, new Vector3(x + 4.4f, 0, z - 4.4f));
                eventProps[i, 0] = EventProp(art, site, "Temporary roadwork", new Color(.84f, .42f, .15f), new(1.25f, .68f, .45f));
                eventProps[i, 1] = EventProp(art, site, "Stranded vehicle", new Color(.34f, .29f, .25f), new(1.78f, 1.25f, 3.62f));
                eventProps[i, 2] = EventProp(art, site, "Delivery trolley", new Color(.29f, .56f, .47f), new(.98f, .83f, .95f));
                streetCrew[i] = art.Person("City service worker", root, site + new Vector3(.85f, 0, 2), new Color(.73f,.64f,.44f));
                streetCrew[i].Root.rotation = Quaternion.Euler(0, 180, 0);
                streetCrew[i].Root.gameObject.SetActive(false);
                for (int kind = 0; kind < 3; kind++)
                {
                    GameObject prop = eventProps[i, kind];
                    var beacon = art.Part(prop.transform, PrimitiveType.Cube, "Reflective incident marker",
                        new(0, .56f, 0), new(.55f, .15f, .8f), new Color(.92f, .68f, .25f));
                    Renderer marker = beacon.GetComponent<Renderer>();
                    marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    marker.sharedMaterial = art.RunningLightMaterial(marker.sharedMaterial);
                    beacons[i, kind] = marker;
                    prop.SetActive(false);
                }
            }
            Reset();
        }

        private static GameObject EventProp(HarborBlockout art, Vector3 site, string label, Color color, Vector3 size) =>
            art.Box(label, site + Vector3.up * (size.y * .5f), size, color, true, bake: false);

        // Each corner targets the intersection from the proper lane.
        private static Vector3[] Loop(float left, float right, float bottom, float top) => new Vector3[]
        {
            new(left + 2.25f,0,bottom + 2.25f), new(left + 2.25f,0,top - 2.25f),
            new(right - 2.25f,0,top - 2.25f), new(right - 2.25f,0,bottom + 2.25f)
        };

        private void BuildSignal(HarborBlockout art, int site, int axis, Vector3 foot)
        {
            art.Box("Signal pole", foot + Vector3.up * 1.48f, new(.14f, 2.96f, .14f), new Color(.18f,.21f,.21f));
            art.Box("Signal housing", foot + Vector3.up * 2.89f, new(.35f,.91f,.35f), new Color(.10f,.12f,.12f));
            for (int i = 0; i < 3; i++)
            {
                GameObject lamp = art.Box("Traffic signal lens", foot + Vector3.up * (3.17f - i * .26f),
                    new(.23f,.18f,.37f), Color.gray);
                var renderer = lamp.GetComponent<Renderer>();
                renderer.sharedMaterial = art.RunningLightMaterial(renderer.sharedMaterial);
                lamps[site, axis, i] = renderer;
            }
        }

        internal void Reset()
        {
            accumulator = 0;
            running = false;
            lastTrafficFrame = null;
            lastEventFrame = null;
            nextEvent = 24;
            SetIncident(-1, 0);
            incident.remaining = 0;
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i].FollowPresentation();
                cars[i].Root.gameObject.SetActive(false);
                var route = Routes[i / 2];
                segment[i] = (i % 2) * 2;
                int a = segment[i], b = (a + 1) % route.Length;
                state[i] = new Car
                {
                    position = Vector3.Lerp(route[a], route[b], .45f),
                    yaw = Yaw(route[b] - route[a])
                };
                cars[i].ApplyNetwork(state[i].position, state[i].yaw, 0, true);
            }
        }

        internal Car[] Cars => running ? state : null;
        internal CostaVehicle[] VehiclesForAudio => cars;
        internal CityEvent Incident => incident;

        internal void Simulate(float step, float hour, Vector3? pursuit)
        {
            if (step <= 0)
                return;
            accumulator += step;
            if (accumulator < .075f)
                return;
            float dt = Mathf.Min(.2f, accumulator);
            accumulator = 0;
            motorHour = hour;
            running = true;

            if (incident.site >= 0)
            {
                incident.remaining -= dt;
                if (incident.remaining <= 0)
                {
                    SetIncident(-1, 0);
                    nextEvent = random.Next(30, 66);
                }
            }
            else if ((nextEvent -= dt) <= 0)
            {
                int site = random.Next(EventSites.Length);
                Vector3 at = EventSites[site] + Vector3.up * .7f;
                if (!Physics.CheckBox(at, new Vector3(1.15f, .66f, 2f), Quaternion.identity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    SetIncident(site, random.Next(3));
                    incident.remaining = random.Next(16, 31);
                }
                else
                    nextEvent = 3;
            }

            float night = HarborDaylight.CityNight(hour);
            for (int i = 0; i < cars.Length; i++)
                StepCar(i, dt, night, pursuit);
            Physics.SyncTransforms();
        }

        private void StepCar(int index, float dt, float night, Vector3? pursuit)
        {
            CostaVehicle vehicle = cars[index];
            Car car = state[index];
            var route = Routes[index / 2];
            int a = segment[index], b = (a + 1) % route.Length;
            Vector3 direction = (route[b] - route[a]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            float distance = Vector3.Dot(route[b] - car.position, direction);
            bool flee = pursuit.HasValue && (pursuit.Value - car.position).sqrMagnitude < 24 * 24;
            float targetSpeed = Mathf.Lerp(index % 2 == 0 ? 8.1f : 6.7f, index % 2 == 0 ? 6.4f : 3.7f, night);
            if (flee)
            {
                float separation = Vector3.Distance(pursuit.Value, car.position);
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Lerp(0, 4f, Mathf.InverseLerp(4, 17, separation)));
            }

            bool eastWest = Mathf.Abs(direction.x) > .5f;
            int signal = SignalAt(route[b]);
            bool red = signal >= 0 && !Green(signal, eastWest, motorHour);
            float stopLine = red ? 7.1f : 0;
            if (red && distance > 2.4f)
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(7f * Mathf.Max(0, distance - stopLine)));
            else if (distance < 10)
                targetSpeed = Mathf.Min(targetSpeed, 3.6f);

            // Stay in the lane. A staggered offset permits passing small debris,
            // while full-width obstacles and crossing traffic force a stop.
            float lane = Mathf.Clamp(Vector3.Dot(route[b] - car.position, right), -1.4f, 1.4f);
            float steer = Mathf.Clamp(lane * .48f, -.55f, .55f);
            Vector3 heading = (direction + right * steer).normalized;
            float probe = Mathf.Max(2.6f, car.speed * .73f);
            if (Blocked(vehicle, car.position, heading, probe))
            {
                bool bypass = !red && distance > 10 && Mathf.Abs(lane) < .95f;
                Vector3 detour = (direction + right * .62f).normalized;
                if (bypass && !Blocked(vehicle, car.position, detour, probe))
                {
                    heading = detour;
                    targetSpeed = Mathf.Min(targetSpeed, 2.6f);
                }
                else
                    targetSpeed = 0;
            }

            car.speed = Mathf.MoveTowards(car.speed, targetSpeed, dt * (targetSpeed < car.speed ? 8.1f : 3.2f));
            float idealYaw = Yaw(heading);
            float angleError = Mathf.Abs(Mathf.DeltaAngle(car.yaw, idealYaw));
            float advance = car.speed * dt * (angleError > 45 ? .18f : 1f);
            if (red && distance > 2.4f)
                advance = Mathf.Min(advance, Mathf.Max(0, distance - stopLine));
            if (advance > 0 && !Blocked(vehicle, car.position, heading, advance + .23f))
                car.position += heading * advance;
            else if (advance > 0)
                car.speed = 0;

            if (distance <= .38f)
                segment[index] = b;
            if (car.speed > .04f)
                car.yaw = Mathf.MoveTowardsAngle(car.yaw, idealYaw, dt * 116f);

            if (!vehicle.Root.gameObject.activeSelf)
                vehicle.Root.gameObject.SetActive(true);
            vehicle.ApplyNetwork(car.position, car.yaw, car.speed, true);
            vehicle.SetRunningLights(night);
        }

        private bool Blocked(CostaVehicle vehicle, Vector3 from, Vector3 bearing, float distance)
        {
            int count = Physics.BoxCastNonAlloc(from + Vector3.up * .69f + bearing * 1.3f,
                new Vector3(.72f,.52f,.64f), bearing, obstacles, Quaternion.LookRotation(bearing), distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!vehicle.OwnsCollider(obstacles[i].collider))
                    return true;
            return false;
        }

        internal void Present(Car[] remote, CityEvent worldEvent, float hour)
        {
            if (remote?.Length != cars.Length)
                return;
            bool receivedTraffic = remote != lastTrafficFrame;
            bool receivedEvent = worldEvent != lastEventFrame;
            bool collisionChanged = false;
            float night = HarborDaylight.CityNight(hour);
            for (int i = 0; i < cars.Length; i++)
            {
                if (remote[i] == null)
                    continue;
                bool spawn = !cars[i].Root.gameObject.activeSelf;
                if (spawn)
                    cars[i].Root.gameObject.SetActive(true);
                if (receivedTraffic || spawn)
                    collisionChanged |= cars[i].SetAuthoritativePose(remote[i].position, remote[i].yaw) || spawn;
                cars[i].ApplyNetwork(remote[i].position, remote[i].yaw, remote[i].speed, spawn);
                cars[i].SetRunningLights(night);
            }
            if (receivedEvent && worldEvent != null)
            {
                int previousSite = incident.site, previousKind = incident.kind;
                SetIncident(worldEvent.site, worldEvent.kind);
                incident.remaining = worldEvent.remaining;
                collisionChanged |= previousSite != incident.site || previousKind != incident.kind;
            }
            lastTrafficFrame = remote;
            lastEventFrame = worldEvent;
            UpdateSignals(hour);
            if (collisionChanged)
                Physics.SyncTransforms();
        }

        private int previousSignalMask = -1;
        internal void UpdateSignals(float hour)
        {
            int mask = 0;
            for (int i = 0; i < EventSites.Length; i++)
            for (int a = 0; a < 2; a++)
                mask |= SignalColor(i, a == 1, hour) << (2 * (i * 2 + a));
            if (incident.site >= 0)
            {
                float pulse = .16f + Mathf.Pow(.5f + .5f * Mathf.Sin(Time.unscaledTime * 9f), 4) * 1.15f;
                hazard.SetColor("_EmissionColor", new Color(1,.41f,.08f) * pulse);
                beacons[incident.site, incident.kind].SetPropertyBlock(hazard);
            }
            if (mask == previousSignalMask)
                return;
            previousSignalMask = mask;
            for (int i = 0; i < EventSites.Length; i++)
            for (int a = 0; a < 2; a++)
            for (int c = 0; c < 3; c++)
            {
                Color tint = c == 0 ? new Color(1,.12f,.06f) : c == 1 ? new Color(1,.57f,.07f) : new Color(.12f,1,.30f);
                bool lit = c == SignalColor(i, a == 1, hour);
                light.SetColor("_BaseColor", tint * (lit ? .78f : .13f));
                light.SetColor("_EmissionColor", lit ? tint * 1.65f : Color.black);
                lamps[i,a,c].SetPropertyBlock(light);
            }
        }

        internal void PresentCrew(Vector3 observer)
        {
            if (incident.site < 0)
                return;
            HarborCharacter worker = streetCrew[incident.site];
            bool nearby = (observer - worker.Root.position).sqrMagnitude < 42 * 42;
            if (worker.Root.gameObject.activeSelf != nearby)
                worker.Root.gameObject.SetActive(nearby);
            if (!nearby)
                return;
            worker.Animate(HarborCharacter.Motion.Idle);
            float phase = Mathf.Repeat(Time.unscaledTime * .39f, 1);
            if (phase < .74f)
                worker.PlayGesture(incident.kind == 2 ? HarborCharacter.Gesture.PickUp : HarborCharacter.Gesture.Reach,
                    phase / .74f, EventSites[incident.site] + Vector3.up * .72f + Vector3.forward * 1.28f);
        }

        internal void Release()
        {
            foreach (HarborCharacter worker in streetCrew)
                worker?.Release();
        }

        private void SetIncident(int site, int kind)
        {
            if (site < 0 || site >= EventSites.Length || kind < 0 || kind >= 3)
                site = -1;
            if (site == incident.site && kind == incident.kind)
                return;
            if (incident.site >= 0)
            {
                eventProps[incident.site, incident.kind].SetActive(false);
                streetCrew[incident.site].Root.gameObject.SetActive(false);
            }
            incident.site = site;
            incident.kind = kind;
            if (site >= 0)
                eventProps[site, kind].SetActive(true);
        }

        internal bool NearbyEvent(Vector3 player, out int kind)
        {
            kind = incident.kind;
            return incident.site >= 0 && (EventSites[incident.site] - player).sqrMagnitude < 24 * 24;
        }

        private static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        private static int SignalAt(Vector3 corner)
        {
            if ((corner - new Vector3(-30,0,76)).sqrMagnitude < 13) return 0;
            if ((corner - new Vector3(18,0,123)).sqrMagnitude < 13) return 1;
            if ((corner - new Vector3(-79,0,123)).sqrMagnitude < 13) return 2;
            return -1;
        }

        private static bool Green(int index, bool eastWest, float hour) => SignalColor(index, eastWest, hour) == 2;

        private static int SignalColor(int index, bool eastWest, float hour)
        {
            float phase = Mathf.Repeat(hour * 30 + index * 6 + (eastWest ? 18 : 0), 36);
            return phase < 14 ? 2 : phase < 17 ? 1 : 0;
        }

    }
}
