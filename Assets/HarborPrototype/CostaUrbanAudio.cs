using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaUrbanAudio
    {
        private static readonly string[] SoundNames =
        {
            "Wind", "DistantTraffic", "StreetMotor", "BikeMotor", "Siren",
            "Conversation1", "Conversation2", "Conversation3"
        };

        private sealed class Motor
        {
            internal CostaSpatialAudio.Voice Voice;
            internal CostaVehicle Vehicle;
            internal CostaVehicle Desired;
            internal float Gain;
            internal float Pitch = .78f;
        }

        private readonly CostaSpatialAudio space;
        private readonly CostaVehicle[] traffic;
        private readonly CostaVehicle[] parked;
        private readonly CostaVenueLife venues;
        private readonly System.Random random = new();
        private readonly Transform root;
        private readonly AudioSource wind;
        private readonly AudioSource streets;
        private readonly CostaSpatialAudio.Voice siren;
        private readonly Motor[] motors;
        private readonly CostaVehicle[] selected;
        private ResourceRequest[] pending;
        private readonly AudioClip[] clips = new AudioClip[SoundNames.Length];
        private float motorsAt;
        private float conversationAt;
        private int lastConversation = -1;
        private float sirenAt;
        private float incidentUntil;
        private float sirenGain;
        private Vector3 incident;
        private float presence;

        internal CostaUrbanAudio(Transform parent, CostaSpatialAudio space, CostaVehicle[] traffic,
            CostaVehicle[] parked, CostaVenueLife venues)
        {
            this.space = space;
            this.traffic = traffic;
            this.parked = parked;
            this.venues = venues;
            root = new GameObject("Santa Luz ambient layers").transform;
            root.SetParent(parent, false);
            wind = Bed("Coastal wind");
            streets = Bed("Distant traffic");
            siren = space.LoopVoice("Street siren");
            motors = new Motor[Application.isMobilePlatform ? 2 : 3];
            selected = new CostaVehicle[motors.Length];
            for (int i = 0; i < motors.Length; i++)
                motors[i] = new Motor { Voice = space.LoopVoice("Passing motor " + (i + 1)) };
            pending = new ResourceRequest[SoundNames.Length];
            for (int i = 0; i < pending.Length; i++)
                pending[i] = Resources.LoadAsync<AudioClip>("Harbor/CityAudio/" + SoundNames[i]);
            sirenAt = Time.unscaledTime + Between(12, 22);
            conversationAt = Time.unscaledTime + Between(2, 5);
        }

        private AudioSource Bed(string name)
        {
            AudioSource source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(root, false);
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.reverbZoneMix = 0;
            source.volume = 0;
            source.priority = 158;
            return source;
        }

        internal void Update(Vector3 player, float activity, float night, float rain, int heat,
            CostaVehicle localCar, Vector3? officer)
        {
            if (pending != null)
            {
                for (int i = 0; i < pending.Length; i++)
                    if (!pending[i].isDone)
                        return;
                for (int i = 0; i < pending.Length; i++)
                    clips[i] = pending[i].asset as AudioClip;
                wind.clip = clips[0];
                streets.clip = clips[1];
                pending = null;
            }

            float now = Time.unscaledTime;
            float dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
            float blend = 1 - Mathf.Exp(-6 * dt);
            presence = Mathf.Lerp(presence, activity, blend);
            float enclosure = space.Enclosure;
            float density = CityDensity(player);
            float dayTraffic = Mathf.Lerp(1, .34f, night);
            float coast = Mathf.Clamp01(Mathf.InverseLerp(27, -40, player.z));
            float gust = .91f + .09f * Mathf.Sin(now * .57f);
            MixBed(wind, presence * (0.15f + .11f * coast + .13f * rain) * (1 - enclosure * .86f) * gust, blend);
            MixBed(streets, presence * density * dayTraffic * (.25f - .07f * rain) * (1 - enclosure * .81f), blend);

            if (now >= motorsAt)
            {
                PickMotors(localCar, activity > .05f);
                motorsAt = now + .21f;
            }
            foreach (Motor motor in motors)
                UpdateMotor(motor, localCar, blend);

            UpdateSiren(player, density, heat, officer, blend, now);
            if (activity > .6f && now >= conversationAt)
            {
                Transform speaker = venues.NearestSpeaker(space.Listener);
                conversationAt = now + (speaker == null ? 2.4f : Between(7.3f, 15.8f));
                if (speaker != null)
                {
                    int clip = random.Next(3);
                    if (clip == lastConversation)
                        clip = (clip + 1) % 3;
                    lastConversation = clip;
                    space.Play(clips[5 + clip], speaker.position + Vector3.up * 1.56f,
                        .30f * Mathf.Lerp(1, .66f, night), 2.3f, 22f, 133, Between(.96f, 1.04f));
                }
            }
        }

        private static float CityDensity(Vector3 player)
        {
            if (player.z < 23)
                return .28f;
            if (player.z < CostaCity.HighwayStart)
                return 1;
            foreach (CostaCity.District district in CostaCity.Districts)
            {
                float distance = Mathf.Abs(player.z - district.Center);
                if (distance < district.Area.height * .5f + 18)
                    return Mathf.Lerp(.21f, .81f, 1 - Mathf.InverseLerp(42, 80, distance));
            }
            return .20f;
        }

        private void PickMotors(CostaVehicle localCar, bool active)
        {
            System.Array.Clear(selected, 0, selected.Length);
            if (active)
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    CostaVehicle closest = null;
                    float distance = 44 * 44;
                    Consider(traffic);
                    Consider(parked);
                    selected[i] = closest;

                    void Consider(CostaVehicle[] vehicles)
                    {
                        foreach (CostaVehicle item in vehicles)
                        {
                            if (item == localCar || !item.Root.gameObject.activeInHierarchy
                                || vehicles == parked && item.Speed < .32f
                                || System.Array.IndexOf(selected, item) >= 0)
                                continue;
                            float separation = (space.Listener - item.Root.position).sqrMagnitude;
                            if (separation < distance)
                            {
                                distance = separation;
                                closest = item;
                            }
                        }
                    }
                }
            }

            foreach (Motor motor in motors)
            {
                int match = motor.Vehicle == null ? -1 : System.Array.IndexOf(selected, motor.Vehicle);
                motor.Desired = match >= 0 ? motor.Vehicle : null;
                if (match >= 0)
                    selected[match] = null;
            }
            foreach (CostaVehicle vehicle in selected)
            {
                if (vehicle == null)
                    continue;
                foreach (Motor motor in motors)
                {
                    if (motor.Desired != null)
                        continue;
                    motor.Desired = vehicle;
                    break;
                }
            }
        }

        private void UpdateMotor(Motor motor, CostaVehicle localCar, float blend)
        {
            bool keep = motor.Vehicle != null && motor.Vehicle == motor.Desired && motor.Vehicle != localCar;
            motor.Gain = Mathf.Lerp(motor.Gain, keep ? presence * .22f : 0, blend);
            if (!keep && motor.Gain < .004f)
            {
                motor.Voice.Stop();
                motor.Vehicle = motor.Desired;
                motor.Gain = 0;
            }
            CostaVehicle car = motor.Vehicle;
            if (car == null)
                return;

            float speed = Mathf.Clamp01(car.Speed / (car.TopSpeed * .74f));
            Vector3 point = car.Root.TransformPoint(new Vector3(0, .61f, car.IsBike ? 0 : .87f));
            motor.Pitch = Mathf.Lerp(motor.Pitch, .74f + speed * .93f, blend);
            float gain = motor.Gain * (.71f + speed * .41f);
            if (!motor.Voice.Playing && keep)
            {
                AudioClip clip = clips[car.IsBike ? 3 : 2];
                if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                    motor.Voice.Play(clip, point, gain, 3.5f, 45, 144, motor.Pitch, true, space);
            }
            if (motor.Voice.Playing)
            {
                motor.Voice.Move(point, gain, motor.Pitch);
                motor.Voice.Update(space, blend, .18f);
            }
        }

        private void UpdateSiren(Vector3 player, float density, int heat, Vector3? officer, float blend, float now)
        {
            if (heat == 0 && presence > .6f && now >= sirenAt)
            {
                sirenAt = now + Between(28, 65);
                if (density > .42f)
                {
                    incident = OnRoad(player, Between(56, 84) * (random.Next(2) == 0 ? -1 : 1));
                    incidentUntil = now + Between(4.5f, 8.7f);
                }
            }
            bool alarm = heat > 0 || now < incidentUntil;
            float volume = heat > 0 ? .13f + heat * .028f : .115f;
            sirenGain = Mathf.Lerp(sirenGain, alarm ? presence * volume : 0, blend);
            if (sirenGain <= .003f || clips[4] == null || clips[4].loadState != AudioDataLoadState.Loaded)
            {
                siren.Stop();
                return;
            }
            Vector3 point = heat > 0 ? officer ?? OnRoad(player, player.z > CostaCity.HighwayStart ? 46 : 35) : incident;
            if (!siren.Playing)
                siren.Play(clips[4], point, sirenGain, 9, 145, 109, 1, true, space);
            siren.Move(point, sirenGain, 1);
            siren.Update(space, blend, .16f);
        }

        private static Vector3 OnRoad(Vector3 player, float ahead)
        {
            float z = Mathf.Clamp(player.z + ahead, -15, CostaCity.HighwayEnd - 3);
            if (z >= CostaCity.HighwayStart)
                return new Vector3(CostaCity.HighwayX - 2, 1.27f, z);
            float x = CostaCity.Avenues[0];
            foreach (float avenue in CostaCity.Avenues)
                if (Mathf.Abs(avenue - player.x) < Mathf.Abs(x - player.x))
                    x = avenue;
            return new Vector3(x - 2, 1.27f, z);
        }

        private static void MixBed(AudioSource source, float level, float blend)
        {
            source.volume = Mathf.Lerp(source.volume, level, blend);
            if (source.clip == null || source.clip.loadState != AudioDataLoadState.Loaded)
                return;
            if (source.volume > .002f && !source.isPlaying)
                source.Play();
            else if (source.volume <= .002f && level <= .002f && source.isPlaying)
                source.Stop();
        }

        private float Between(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        internal void Release() => Object.Destroy(root.gameObject);
    }
}
