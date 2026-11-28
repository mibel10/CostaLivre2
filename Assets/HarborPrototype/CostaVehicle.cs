using UnityEngine;

namespace CostaLivre
{
    internal sealed partial class CostaVehicle
    {
        internal enum Style { Sedan, Compact, Executive, Super, Track, StreetBike, Cruiser }

        internal const float BrakeAcceleration = 20f;
        internal readonly Transform Root;
        private readonly Transform shell;
        private readonly Style style;
        private readonly float topSpeed;
        private readonly float engine;
        private readonly float steeringStrength;
        private readonly Vector3 collisionSize;
        internal readonly Vector3 Home;
        private readonly float homeYaw;
        private readonly BoxCollider body;
        private readonly Collider[] nearbyColliders = new Collider[16];
        private readonly Transform[] wheels;
        private readonly Transform[] doors = new Transform[2];
        private readonly float[] doorUpdated = new float[2];
        private readonly Vector3[] wheelRest;
        private readonly float[] springTravel;
        private readonly float[] springSpeed;
        private readonly RaycastHit[] contacts = new RaycastHit[6];
        private readonly Transform frontPanel;
        private readonly Transform rearPanel;
        private readonly Vector3 frontRest, rearRest;
        private float speed;
        private float slip;
        private float chassisPitch, chassisRoll;
        private float frontDamage, rearDamage;
        private float nextGroundSample;
        private float settleUntil;
        private readonly float[] groundHeights;
        private readonly Renderer[] runningLights;
        private readonly MaterialPropertyBlock lampProperties = new();
        private int lampsLevel = -1;
        internal float Grip { get; set; } = 1;
        internal float Slip => Mathf.Abs(slip);
        internal Vector2 Damage => new(frontDamage, rearDamage);
        internal float Speed => Mathf.Abs(speed);
        internal float SignedSpeed => speed;
        internal bool IsBike => style == Style.StreetBike || style == Style.Cruiser;
        internal float ExitOffset => IsBike ? 0.91f : 1.64f;
        internal float RiderLean => Mathf.DeltaAngle(0, shell.localEulerAngles.z);

        internal CostaVehicle(HarborBlockout art, Transform parent, Vector3 position, float yaw, Color paint,
            bool service = false, Style style = Style.Sedan, bool traffic = false, bool preview = false)
        {
            this.style = style;
            Root = new GameObject(service ? "Police cruiser" : style.ToString()).transform;
            Root.SetParent(parent, false);
            shell = new GameObject("Vehicle bodywork").transform;
            shell.SetParent(Root, false);
            Home = position;
            homeYaw = yaw;

            switch (style)
            {
                case Style.Super: topSpeed = 34; engine = 13; steeringStrength = 0.89f; break;
                case Style.Track: topSpeed = 39; engine = 15; steeringStrength = 0.86f; break;
                case Style.Compact: topSpeed = 20; engine = 8.8f; steeringStrength = 1.13f; break;
                case Style.Executive: topSpeed = 24; engine = 8.4f; steeringStrength = 0.88f; break;
                case Style.StreetBike: topSpeed = 31; engine = 14; steeringStrength = 1.23f; break;
                case Style.Cruiser: topSpeed = 24; engine = 10.5f; steeringStrength = 1.07f; break;
                default: topSpeed = 21; engine = 8; steeringStrength = 1; break;
            }

            wheels = new Transform[IsBike ? 2 : 4];
            if (IsBike)
            {
                BuildBike(art, paint);
                collisionSize = new Vector3(0.73f, 1.20f, 2.33f);
            }
            else
            {
                BuildCar(art, paint, service, !traffic && !preview);
                Vector3 scale = style == Style.Compact ? new Vector3(0.88f, 1.04f, 0.84f)
                    : style == Style.Executive ? new Vector3(1.03f, 1.04f, 1.12f) : Vector3.one;
                shell.localScale = scale;
                collisionSize = Vector3.Scale(new Vector3(2.06f, 1.40f, 4.44f), scale);
            }

            wheelRest = new Vector3[wheels.Length];
            springTravel = new float[wheels.Length];
            springSpeed = new float[wheels.Length];
            groundHeights = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                wheelRest[i] = wheels[i].localPosition;
            float width = IsBike ? .26f : 1.48f;
            float nose = IsBike ? 1.02f : 2.18f;
            frontPanel = art.Part(shell, PrimitiveType.Cube, "Deformable front trim",
                new(0, IsBike ? .54f : .38f, nose), new(width,.14f,.13f), paint,
                finish: HarborBlockout.Finish.Paint).transform;
            rearPanel = art.Part(shell, PrimitiveType.Cube, "Deformable rear trim",
                new(0, IsBike ? .55f : .39f, -nose), new(width,.14f,.13f), paint,
                finish: HarborBlockout.Finish.Paint).transform;
            frontRest = frontPanel.localPosition;
            rearRest = rearPanel.localPosition;
            Renderer[] renderers = shell.GetComponentsInChildren<Renderer>();
            SetupCustomization(art,!service && !traffic,paint,renderers);
            runningLights = System.Array.FindAll(renderers, renderer => renderer.name is "Headlight" or "Headlamp" or "Tail light" or "Tail lamp");
            foreach (Renderer renderer in runningLights)
                renderer.sharedMaterial = art.RunningLightMaterial(renderer.sharedMaterial);

            var collision = new GameObject("Vehicle collision").transform;
            collision.SetParent(Root, false);
            body = collision.gameObject.AddComponent<BoxCollider>();
            if (!preview) art.Damage.Mark(collision.gameObject, CostaWorldDamage.Surface.Metal, 0);
            body.center = Vector3.up * (collisionSize.y / 2 + 0.01f);
            body.size = collisionSize;
            if (!traffic && !preview)
            {
                var obstacle = Root.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = body.size;
                obstacle.center = body.center;
                obstacle.carving = true;
            }
            Reset();
            if (preview) body.enabled = false;
        }

        private void BuildCar(HarborBlockout art, Color paint, bool service, bool mirrors)
        {
            Color rubber = new(0.075f, 0.078f, 0.081f);
            Color trim = new(0.16f, 0.18f, 0.20f);
            Color window = new(0.13f, 0.22f, 0.25f);
            bool super = style == Style.Super || style == Style.Track;
            if (super)
                BuildSuperBody(art, paint, window);
            else
            {
                art.FinishedModel(shell, "Sculpted body", paint, HarborBlockout.Finish.Paint, new(-2.23f, 0.73f, 0.36f, 0.76f),
                    new(-1.86f, 0.93f, 0.29f, 0.94f), new(-0.75f, 0.97f, 0.26f, 0.92f),
                    new(1.01f, 0.97f, 0.26f, 0.87f), new(1.91f, 0.92f, 0.31f, 0.78f),
                    new(2.20f, 0.76f, 0.35f, 0.65f));
                art.FinishedModel(shell, "Glass cabin", window, HarborBlockout.Finish.Glass, new(-1.32f, 0.75f, 0.86f, 0.96f),
                    new(-0.72f, 0.74f, 0.89f, 1.40f), new(0.51f, 0.72f, 0.85f, 1.40f),
                    new(1.04f, 0.80f, 0.84f, 0.91f));
                art.FinishedModel(shell, "Roof", paint, HarborBlockout.Finish.Paint, new(-0.79f, 0.70f, 1.39f, 1.43f),
                    new(-0.63f, 0.75f, 1.39f, 1.46f), new(0.37f, 0.73f, 1.39f, 1.46f),
                    new(0.54f, 0.68f, 1.38f, 1.43f));
            }

            int count = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    Transform wheel = new GameObject("Wheel").transform;
                    wheel.SetParent(shell, false);
                    wheel.localPosition = new Vector3(side * 0.93f, 0.35f, axle * 1.40f);
                    wheels[count++] = wheel;
                    Part(art, wheel, PrimitiveType.Cylinder, "Tire", Vector3.zero, new(0.70f, 0.14f, 0.70f), rubber, 90);
                    Part(art, wheel, PrimitiveType.Cylinder, "Alloy rim", new(side * 0.146f, 0, 0),
                        new(0.42f, 0.018f, 0.42f), new Color(0.51f, 0.53f, 0.54f), 90);
                }

                Transform door = new GameObject("Door hinge").transform;
                door.SetParent(shell, false);
                door.localPosition = new Vector3(side * (super ? 1.01f : 0.98f), 0.63f, 0.65f);
                doors[DoorIndex(side)] = door;
                Part(art, door, PrimitiveType.Cube, "Door skin", new(0, 0.035f, -0.60f),
                    new(0.035f, super ? 0.29f : 0.39f, 1.17f), paint);
                Part(art, door, PrimitiveType.Cube, "Door seal", new(side * 0.018f, -0.18f, -0.60f),
                    new(0.016f, 0.017f, 1.18f), trim);
                Part(art, door, PrimitiveType.Cube, "Door handle", new(side * 0.028f, 0.13f, -0.91f),
                    new(0.038f, 0.023f, 0.14f), trim);

                Part(art, shell, PrimitiveType.Cube, "Headlight", new(side * 0.60f, 0.58f, 2.115f),
                    new(0.38f, 0.16f, 0.13f), new Color(0.83f, 0.83f, 0.73f));
                Part(art, shell, PrimitiveType.Cube, "Tail light", new(side * 0.60f, 0.68f, -2.146f),
                    new(0.43f, 0.16f, 0.13f), new Color(0.45f, 0.075f, 0.060f));
                Part(art, shell, PrimitiveType.Cube, "Mirror", new(side * 0.87f, 1.06f, 0.61f),
                    new(0.24f, 0.11f, 0.18f), trim);
                if (mirrors)
                    art.Mirrors?.Install(shell, new Vector3(side * .895f, 1.06f, .506f),
                        new Vector3(side * .22f, .03f, -1), new Vector2(.205f, .086f), this);
            }

            Part(art, shell, PrimitiveType.Cube, "Grille", new(0, 0.53f, 2.19f), new(0.74f, 0.22f, 0.045f), trim);
            if (service)
            {
                Part(art, shell, PrimitiveType.Cube, "Lightbar", new(-0.34f, 1.52f, 0),
                    new(0.64f, 0.12f, 0.23f), new Color(0.58f, 0.11f, 0.09f));
                Part(art, shell, PrimitiveType.Cube, "Lightbar", new(0.34f, 1.52f, 0),
                    new(0.64f, 0.12f, 0.23f), new Color(0.13f, 0.28f, 0.56f));
            }
        }

        private void BuildSuperBody(HarborBlockout art, Color paint, Color glass)
        {
            bool track = style == Style.Track;
            art.FinishedModel(shell, "Wedge body", paint, HarborBlockout.Finish.Paint,
                new(-2.24f, 0.80f, 0.32f, 0.72f), new(-1.63f, 1.04f, 0.24f, 0.87f),
                new(-0.62f, 1.00f, 0.22f, 0.77f), new(0.95f, 0.97f, 0.22f, 0.67f),
                new(1.85f, 0.98f, 0.25f, 0.57f), new(2.23f, 0.69f, 0.31f, 0.49f));
            art.FinishedModel(shell, "Swept glazing", glass, HarborBlockout.Finish.Glass, new(-1.68f, 0.62f, 0.77f, 0.83f),
                new(-0.85f, 0.66f, 0.76f, track ? 1.20f : 1.07f), new(0.24f, 0.61f, 0.70f, 1.13f),
                new(1.27f, 0.66f, 0.65f, 0.69f));
            art.FinishedModel(shell, "Swept roof", paint, HarborBlockout.Finish.Paint, new(-0.87f, 0.52f, 1.08f, 1.18f),
                new(-0.51f, 0.60f, 1.13f, 1.22f), new(0.30f, 0.55f, 1.10f, 1.19f));
            if (track)
            {
                Color carbon = new(0.11f, 0.13f, 0.15f);
                Part(art, shell, PrimitiveType.Cube, "Rear wing", new(0, 1.15f, -1.88f),
                    new(1.92f, 0.055f, 0.38f), carbon);
                foreach (int side in new[] { -1, 1 })
                    Part(art, shell, PrimitiveType.Cube, "Wing mount", new(side * 0.64f, 0.99f, -1.91f),
                        new(0.09f, 0.32f, 0.12f), carbon);
            }
        }

        private void BuildBike(HarborBlockout art, Color paint)
        {
            bool cruiser = style == Style.Cruiser;
            Color rubber = new(0.067f, 0.073f, 0.076f);
            Color engineColor = new(0.27f, 0.29f, 0.30f);
            float axle = cruiser ? 0.91f : 0.83f;
            for (int i = 0; i < wheels.Length; i++)
            {
                Transform wheel = new GameObject("Motorcycle wheel").transform;
                wheel.SetParent(shell, false);
                wheel.localPosition = new Vector3(0, 0.34f, i == 0 ? -axle : axle);
                wheels[i] = wheel;
                Part(art, wheel, PrimitiveType.Cylinder, "Tire", Vector3.zero, new(0.68f, 0.092f, 0.68f), rubber, 90);
                Part(art, wheel, PrimitiveType.Cylinder, "Brake rotor", Vector3.zero, new(0.45f, 0.100f, 0.45f), engineColor, 90);
            }

            art.FinishedModel(shell, "Tank and fairing", paint, HarborBlockout.Finish.Paint, new(-0.98f, 0.14f, 0.62f, 0.74f),
                new(-0.36f, 0.25f, 0.51f, 0.80f), new(0.30f, cruiser ? 0.24f : 0.29f, 0.42f, 0.99f),
                new(0.75f, 0.17f, 0.53f, 0.87f));
            art.Model(shell, "Saddle", rubber, new(-0.82f, 0.15f, 0.79f, 0.87f),
                new(-0.38f, 0.20f, 0.77f, 0.85f), new(0.04f, 0.15f, 0.77f, 0.83f));
            Part(art, shell, PrimitiveType.Cube, "Engine", new(0, 0.48f, -0.04f),
                new(0.47f, 0.30f, 0.46f), engineColor);
            Part(art, shell, PrimitiveType.Cylinder, "Handlebar", new(0, 1.10f, 0.51f),
                new(0.042f, 0.37f, 0.042f), engineColor, 90);
            foreach (int side in new[] { -1, 1 })
            {
                Transform fork = art.Part(shell, PrimitiveType.Cylinder, "Fork", new(side * 0.112f, 0.65f, axle - 0.10f),
                    new(0.043f, 0.33f, 0.043f), engineColor).transform;
                fork.localRotation = Quaternion.Euler(-18, 0, 0);
            }
            Part(art, shell, PrimitiveType.Sphere, "Headlamp", new(0, 0.87f, 0.87f),
                new(0.25f, 0.18f, 0.12f), new Color(0.85f, 0.84f, 0.71f));
            Part(art, shell, PrimitiveType.Cube, "Tail lamp", new(0, 0.74f, -0.98f),
                new(0.22f, 0.076f, 0.085f), new Color(0.48f, 0.065f, 0.060f));
        }

        private static int DoorIndex(int side) => side < 0 ? 0 : 1;

        internal void PoseDoor(int side, float open)
        {
            Transform door = doors[DoorIndex(side)];
            if (door == null)
                return;
            doorUpdated[DoorIndex(side)] = Time.unscaledTime;
            door.localRotation = Quaternion.Euler(0, -side * 62f * open, 0);
        }

        internal Vector3? DoorHandle(int side)
        {
            Transform hinge = doors[DoorIndex(side)];
            return hinge == null ? null : hinge.TransformPoint(new Vector3(side * 0.028f, 0.13f, -0.91f));
        }

        internal void CloseIdleDoors()
        {
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null && Time.unscaledTime - doorUpdated[i] > 0.08f
                    && doors[i].localRotation != Quaternion.identity)
                    doors[i].localRotation = Quaternion.RotateTowards(doors[i].localRotation, Quaternion.identity,
                        300f * Time.unscaledDeltaTime);
        }

        internal bool TryBoard(Vector3 from, Vector3 position, Quaternion rotation, out int side, out Vector3 step)
        {
            side = Vector3.Dot(from - position, rotation * Vector3.right) < 0 ? -1 : 1;
            Vector3 handHeight = Vector3.up * 0.65f;
            return TryExit(position, rotation, side, out step)
                && !Physics.Linecast(from + handHeight, step + handHeight,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        internal bool OwnsCollider(Collider collider) => collider == body;
        internal Vector3 NearestBodyPoint(Vector3 point) => body.ClosestPoint(point);

        internal bool SetAuthoritativePose(Vector3 position, float yaw)
        {
            Transform collision = body.transform;
            if (collision.parent == Root)
                collision.SetParent(Root.parent, true);
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            if (collision.position.Equals(position) && collision.rotation.Equals(rotation))
                return false;
            collision.SetPositionAndRotation(position, rotation);
            return true;
        }

        internal void FollowPresentation()
        {
            Transform collision = body.transform;
            collision.SetParent(Root, false);
            collision.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        internal void StopForBoarding() => speed = 0;

        internal float TopSpeed => topSpeed * CostaBuildTuning.Gearbox(appliedBuild);
        internal float Acceleration => engine * CostaBuildTuning.Engine(appliedBuild);
        internal float ReverseSpeed => IsBike ? 3f : 7f;
        internal float MaximumTurnRate => 92f * steeringStrength;
        internal static int EncodeSpeed(float speed) => Mathf.RoundToInt(speed * 10f);
        internal static float DecodeSpeed(int speed) => speed / 10f;

        internal bool TryExit(Vector3 position, Quaternion rotation, int side, out Vector3 exit)
        {
            exit = position;
            if (side != -1 && side != 1)
                return false;

            Vector3 offset = rotation * Vector3.right * (side * ExitOffset);
            exit += offset;
            int layers = Physics.DefaultRaycastLayers;
            const QueryTriggerInteraction triggers = QueryTriggerInteraction.Ignore;
            bool wasEnabled = body.enabled;
            body.enabled = false;
            try
            {
                if (Physics.CapsuleCast(position + Vector3.up * 0.38f, position + Vector3.up * 1.46f,
                        0.34f, offset.normalized, offset.magnitude, layers, triggers)
                    || !Physics.Raycast(exit + Vector3.up, Vector3.down, out RaycastHit ground, 1.45f, layers, triggers)
                    || ground.normal.y < 0.65f)
                    return false;

                exit.y = ground.point.y + 0.03f;
                return !Physics.CheckCapsule(exit + Vector3.up * 0.38f, exit + Vector3.up * 1.46f,
                    0.34f, layers, triggers);
            }
            finally
            {
                body.enabled = wasEnabled;
            }
        }

        internal bool ValidNetworkTravel(Vector3 from, float fromYaw, Vector3 to, float toYaw, float currentSpeed, float proposedSpeed)
        {
            Quaternion rotation = Quaternion.Euler(0, toYaw, 0);
            Vector3 movement = to - from;
            float distance = movement.magnitude;
            if (distance > 0.02f)
            {
                Vector3 direction = movement / distance;
                Vector3 heading = Quaternion.Euler(0, Mathf.LerpAngle(fromYaw, toYaw, 0.5f), 0) * Vector3.forward;
                float speed = Mathf.Abs(currentSpeed) > Mathf.Abs(proposedSpeed) ? currentSpeed : proposedSpeed;
                if (Mathf.Abs(Vector3.Dot(direction, heading * Mathf.Sign(speed)) - 1f) * distance > 0.22f
                    || SimulatedTravel(from, rotation, direction, distance) + 0.12f < distance)
                    return false;
            }

            bool wasEnabled = body.enabled;
            body.enabled = false;
            try
            {
                return !Physics.CheckBox(to + rotation * body.center, body.size * 0.5f - Vector3.one * 0.075f,
                    rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
            finally
            {
                body.enabled = wasEnabled;
            }
        }

        internal void Reset()
        {
            speed = slip = chassisPitch = chassisRoll = 0;
            SetDamage(Vector2.zero);
            System.Array.Clear(springTravel, 0, springTravel.Length);
            System.Array.Clear(springSpeed, 0, springSpeed.Length);
            System.Array.Clear(groundHeights, 0, groundHeights.Length);
            nextGroundSample = 0;
            for (int i = 0; i < wheels.Length; i++)
                wheels[i].localPosition = wheelRest[i];
            Root.SetPositionAndRotation(Home, Quaternion.Euler(0, homeYaw, 0));
            shell.localRotation = Quaternion.identity;
            foreach (Transform door in doors)
                if (door != null)
                    door.localRotation = Quaternion.identity;
        }

        internal float SimulatedTravel(Vector3 position, Quaternion rotation, Vector3 direction, float distance)
        {
            bool enabled = body.enabled;
            body.enabled = false;
            try
            {
                Vector3 center = position + rotation * body.center;
                Vector3 halves = body.size * 0.5f - Vector3.one * 0.075f;
                int overlaps = Physics.OverlapBoxNonAlloc(center, halves, nearbyColliders, rotation,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < overlaps; i++)
                {
                    Collider obstacle = nearbyColliders[i];
                    if (Vector3.Dot(direction, obstacle.ClosestPoint(center) - center) > 0.01f
                        || (Physics.ComputePenetration(body, position, rotation, obstacle, obstacle.transform.position,
                            obstacle.transform.rotation, out Vector3 separation, out _)
                            && Vector3.Dot(direction, separation) < -0.001f))
                        return 0;
                }
                if (Physics.BoxCast(center, halves, direction, out RaycastHit hit, rotation, distance + 0.07f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return Mathf.Max(0, hit.distance - 0.07f);
                return distance;
            }
            finally
            {
                body.enabled = enabled;
            }
        }

        internal bool ApplyNetwork(Vector3 position, float yaw, float velocity, bool snap, float lean = 0)
        {
            float blend = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 15f);
            Vector3 previous = Root.position;
            Quaternion previousRotation = Root.rotation;
            Quaternion targetRotation = Quaternion.Euler(0, yaw, 0);
            if (!snap && previous.Equals(position) && previousRotation.Equals(targetRotation)
                && Mathf.Abs(velocity) <= .001f && Mathf.Abs(speed) <= .001f
                && Mathf.Abs(lean) <= .001f && Time.unscaledTime >= settleUntil)
                return false;
            Vector3 next = (position - previous).sqrMagnitude < 0.000001f
                ? position : Vector3.Lerp(previous, position, blend);
            Quaternion rotation = Quaternion.Angle(previousRotation, targetRotation) < 0.05f
                ? targetRotation : Quaternion.Slerp(previousRotation, targetRotation, blend);
            bool changed = !previous.Equals(next) || !previousRotation.Equals(rotation);
            if (changed)
            {
                Root.SetPositionAndRotation(next, rotation);
                if ((next - previous).sqrMagnitude < 4 && !previous.Equals(next))
                    SpinWheels(Vector3.Dot(next - previous, Root.forward));
            }
            float dt = Mathf.Max(Time.unscaledDeltaTime, .012f);
            float turnRate = Mathf.DeltaAngle(previousRotation.eulerAngles.y, rotation.eulerAngles.y) / dt;
            float acceleration = Mathf.Clamp((velocity - speed) / dt, -28, 15);
            if (snap || changed || Mathf.Abs(velocity) > .001f || Mathf.Abs(acceleration) > .001f || Mathf.Abs(lean) > .001f)
                settleUntil = Time.unscaledTime + .85f;
            speed = velocity;
            AnimateChassis(dt, turnRate, acceleration, IsBike ? lean : null, sample: changed);
            return changed;
        }

        internal bool Drive(Vector2 input, bool braking)
        {
            float dt = Time.deltaTime;
            if (dt <= 0)
                return false;
            float before = speed;
            float target = braking ? 0 : input.y >= 0 ? TopSpeed * input.y : ReverseSpeed * input.y;
            float acceleration = braking || Mathf.Sign(target) != Mathf.Sign(speed) ? BrakePower * TireGrip
                : Mathf.Abs(input.y) > 0.03f ? Acceleration * Mathf.Lerp(.64f, 1, TireGrip) : 4.4f;
            speed = Mathf.MoveTowards(speed, target, acceleration * dt);
            float y = Root.eulerAngles.y;
            float steer = input.x * Mathf.Sign(speed) * Mathf.Lerp(92, 48, Speed / TopSpeed) * steeringStrength;
            float cornerLoad = Mathf.Abs(steer) * Speed * Mathf.Deg2Rad;
            float available = Mathf.Lerp(3.1f, IsBike ? 10.5f : 8.2f, TireGrip) * (braking ? .69f : 1);
            float excess = Mathf.Clamp01((cornerLoad - available) / 10f);
            float slipLimit = IsBike ? 8 : 13;
            float targetSlip = -Mathf.Sign(steer) * excess * slipLimit;
            if (Speed < 3)
                targetSlip = 0;
            slip = Mathf.Lerp(slip, targetSlip, 1 - Mathf.Exp(-dt * Mathf.Lerp(3.5f, 11f, TireGrip)));
            steer *= 1 - .26f * excess;
            if (Speed < .04f)
            {
                AnimateChassis(dt, 0, (speed - before) / dt, null, false);
                return false;
            }

            body.enabled = false;
            try
            {
                Quaternion steering = Root.rotation * Quaternion.Euler(0, steer * dt, 0);
                Vector3 center = Root.position + Vector3.up * body.center.y;
                Vector3 halves = body.size * 0.5f - Vector3.one * 0.075f;
                bool turned = !steering.Equals(Root.rotation)
                    && !Physics.CheckBox(center, halves, steering, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (turned)
                    Root.rotation = steering;

                Quaternion bearing = Quaternion.Euler(0, slip, 0) * Root.rotation;
                Vector3 direction = bearing * Vector3.forward * Mathf.Sign(speed);
                float distance = Speed * dt;
                if (Physics.BoxCast(center, halves, direction, out RaycastHit hit, Root.rotation,
                    distance + 0.07f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    distance = Mathf.Max(0, hit.distance - 0.07f);
                    if (Speed > 4)
                        RegisterDamage(speed > 0, Speed);
                    speed = slip = 0;
                }

                Root.position += direction * distance;
                if (distance > 0)
                    SpinWheels(Vector3.Dot(direction, Root.forward) * distance);
                AnimateChassis(dt, Mathf.DeltaAngle(y, Root.eulerAngles.y) / dt,
                    Mathf.Clamp((speed - before) / dt, -34, 16), null, sample: distance > 0);
                return turned || distance > 0;
            }
            finally
            {
                body.enabled = true;
            }
        }

        private void AnimateChassis(float dt, float turn, float acceleration, float? bikeLean, bool sample)
        {
            if (sample && Time.unscaledTime >= nextGroundSample)
            {
                nextGroundSample = Time.unscaledTime + .07f;
                for (int i = 0; i < wheels.Length; i++)
                {
                    Vector3 wheel = Root.TransformPoint(Vector3.Scale(wheelRest[i], shell.localScale));
                    int n = Physics.RaycastNonAlloc(wheel + Vector3.up * .38f, Vector3.down, contacts, 1.02f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    float offset = -.095f;
                    for (int j = 0; j < n; j++)
                    {
                        RaycastHit hit = contacts[j];
                        if (OwnsCollider(hit.collider) || hit.normal.y < .65f)
                            continue;
                        offset = Mathf.Max(offset, hit.point.y - Root.position.y);
                    }
                    groundHeights[i] = Mathf.Clamp(offset, -.095f, .17f);
                }
            }

            float lateral = Mathf.Clamp(turn * Speed * Mathf.Deg2Rad, -14, 14);
            float front = 0, rear = 0, left = 0, right = 0, mean = 0;
            int slices = Mathf.Clamp(Mathf.CeilToInt(dt / .016f), 1, 6);
            float step = Mathf.Min(dt, .096f) / slices;
            for (int i = 0; i < wheels.Length; i++)
            {
                Vector3 rest = wheelRest[i];
                float load = -Mathf.Sign(rest.z) * acceleration * .0037f + Mathf.Sign(rest.x) * lateral * .003f;
                float target = groundHeights[i] + load;
                for (int j = 0; j < slices; j++)
                {
                    springSpeed[i] += ((target - springTravel[i]) * (115f * CostaBuildTuning.Spring(appliedBuild)) - springSpeed[i] * 15f) * step;
                    springTravel[i] += springSpeed[i] * step;
                }
                wheels[i].localPosition = rest + Vector3.up * springTravel[i] * .72f;
                mean += springTravel[i];
                if (rest.z > 0) front += springTravel[i]; else rear += springTravel[i];
                if (rest.x > 0) right += springTravel[i]; else left += springTravel[i];
            }
            float alpha = 1 - Mathf.Exp(-dt * 9f);
            chassisPitch = Mathf.Lerp(chassisPitch, Mathf.Clamp((rear - front) * 11 + acceleration * -.16f, -8, 8), alpha);
            float roll = IsBike ? bikeLean ?? Mathf.Clamp(-lateral * 1.85f, -24, 24)
                : Mathf.Clamp((left - right) * 13 + lateral * .25f, -8, 8);
            chassisRoll = Mathf.Lerp(chassisRoll, roll, alpha);
            shell.localPosition = Vector3.up * (mean / wheels.Length * .25f);
            shell.localRotation = Quaternion.Euler(chassisPitch, 0, chassisRoll);
        }

        internal void RegisterDamage(bool front, float impact)
        {
            Vector2 amount = Damage;
            float increase = Mathf.InverseLerp(3.5f, 27, impact) * .66f;
            if (front) amount.x += increase; else amount.y += increase;
            SetDamage(amount);
        }

        internal void SetDamage(Vector2 amount)
        {
            Vector2 limit = new(Mathf.Clamp01(amount.x), Mathf.Clamp01(amount.y));
            if (Damage == limit)
                return;
            frontDamage = limit.x;
            rearDamage = limit.y;
            frontPanel.SetLocalPositionAndRotation(frontRest + new Vector3(.065f,.10f,-.18f) * frontDamage,
                Quaternion.Euler(18f * frontDamage, -10f * frontDamage, -9f * frontDamage));
            rearPanel.SetLocalPositionAndRotation(rearRest + new Vector3(-.07f,.07f,.16f) * rearDamage,
                Quaternion.Euler(-17f * rearDamage, 9f * rearDamage, 8f * rearDamage));
        }

        internal void SetRunningLights(float night)
        {
            int level = Mathf.RoundToInt(night * 16);
            if (level == lampsLevel)
                return;
            lampsLevel = level;
            foreach (Renderer renderer in runningLights)
            {
                Color color = renderer.name.StartsWith("Tail", System.StringComparison.Ordinal)
                    ? new Color(.90f,.06f,.025f) : new Color(.91f,.79f,.58f);
                lampProperties.SetColor("_EmissionColor", color * (level / 16f * 1.8f));
                renderer.SetPropertyBlock(lampProperties);
            }
        }

        private void SpinWheels(float distance)
        {
            foreach (Transform wheel in wheels)
                wheel.Rotate(Vector3.right, distance * 163.7f, Space.Self);
        }

        private static void Part(HarborBlockout art, Transform parent, PrimitiveType type, string name,
            Vector3 center, Vector3 size, Color color, float z = 0)
        {
            var finish = name switch
            {
                "Tire" => HarborBlockout.Finish.Rubber,
                "Door skin" => HarborBlockout.Finish.Paint,
                "Alloy rim" or "Brake rotor" or "Engine" => HarborBlockout.Finish.Metal,
                _ => HarborBlockout.Finish.Standard
            };
            art.Part(parent, type, name, center, size, color, finish: finish).transform.localRotation = Quaternion.Euler(0, 0, z);
        }
    }
}
