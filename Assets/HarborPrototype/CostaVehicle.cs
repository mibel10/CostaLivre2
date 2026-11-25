using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaVehicle
    {
        internal enum Style { Sedan, Compact, Executive, Super, Track, StreetBike, Cruiser }

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
        private float speed;
        internal float Speed => Mathf.Abs(speed);
        internal float SignedSpeed => speed;
        internal bool IsBike => style == Style.StreetBike || style == Style.Cruiser;
        internal float ExitOffset => IsBike ? 0.91f : 1.64f;
        internal float RiderLean => Mathf.DeltaAngle(0, shell.localEulerAngles.z);

        internal CostaVehicle(HarborBlockout art, Transform parent, Vector3 position, float yaw, Color paint,
            bool service = false, Style style = Style.Sedan)
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
                BuildCar(art, paint, service);
                Vector3 scale = style == Style.Compact ? new Vector3(0.88f, 1.04f, 0.84f)
                    : style == Style.Executive ? new Vector3(1.03f, 1.04f, 1.12f) : Vector3.one;
                shell.localScale = scale;
                collisionSize = Vector3.Scale(new Vector3(2.06f, 1.40f, 4.44f), scale);
            }

            body = Root.gameObject.AddComponent<BoxCollider>();
            body.center = Vector3.up * (collisionSize.y / 2 + 0.01f);
            body.size = collisionSize;
            var obstacle = Root.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = body.size;
            obstacle.center = body.center;
            obstacle.carving = true;
            Reset();
        }

        private void BuildCar(HarborBlockout art, Color paint, bool service)
        {
            Color rubber = new(0.075f, 0.078f, 0.081f);
            Color trim = new(0.16f, 0.18f, 0.20f);
            Color window = new(0.13f, 0.22f, 0.25f);
            bool super = style == Style.Super || style == Style.Track;
            if (super)
                BuildSuperBody(art, paint, window);
            else
            {
                art.Model(shell, "Sculpted body", paint, new(-2.23f, 0.73f, 0.36f, 0.76f),
                    new(-1.86f, 0.93f, 0.29f, 0.94f), new(-0.75f, 0.97f, 0.26f, 0.92f),
                    new(1.01f, 0.97f, 0.26f, 0.87f), new(1.91f, 0.92f, 0.31f, 0.78f),
                    new(2.20f, 0.76f, 0.35f, 0.65f));
                art.Model(shell, "Glass cabin", window, new(-1.32f, 0.75f, 0.86f, 0.96f),
                    new(-0.72f, 0.74f, 0.89f, 1.40f), new(0.51f, 0.72f, 0.85f, 1.40f),
                    new(1.04f, 0.80f, 0.84f, 0.91f));
                art.Model(shell, "Roof", paint, new(-0.79f, 0.70f, 1.39f, 1.43f),
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

                Part(art, shell, PrimitiveType.Cube, "Headlight", new(side * 0.60f, 0.58f, 2.115f),
                    new(0.38f, 0.16f, 0.13f), new Color(0.83f, 0.83f, 0.73f));
                Part(art, shell, PrimitiveType.Cube, "Tail light", new(side * 0.60f, 0.68f, -2.146f),
                    new(0.43f, 0.16f, 0.13f), new Color(0.45f, 0.075f, 0.060f));
                Part(art, shell, PrimitiveType.Cube, "Mirror", new(side * 0.87f, 1.06f, 0.61f),
                    new(0.24f, 0.11f, 0.18f), trim);
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
            art.Model(shell, "Wedge body", paint,
                new(-2.24f, 0.80f, 0.32f, 0.72f), new(-1.63f, 1.04f, 0.24f, 0.87f),
                new(-0.62f, 1.00f, 0.22f, 0.77f), new(0.95f, 0.97f, 0.22f, 0.67f),
                new(1.85f, 0.98f, 0.25f, 0.57f), new(2.23f, 0.69f, 0.31f, 0.49f));
            art.Model(shell, "Swept glazing", glass, new(-1.68f, 0.62f, 0.77f, 0.83f),
                new(-0.85f, 0.66f, 0.76f, track ? 1.20f : 1.07f), new(0.24f, 0.61f, 0.70f, 1.13f),
                new(1.27f, 0.66f, 0.65f, 0.69f));
            art.Model(shell, "Swept roof", paint, new(-0.87f, 0.52f, 1.08f, 1.18f),
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

            art.Model(shell, "Tank and fairing", paint, new(-0.98f, 0.14f, 0.62f, 0.74f),
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

        internal void Reset()
        {
            speed = 0;
            Root.SetPositionAndRotation(Home, Quaternion.Euler(0, homeYaw, 0));
            shell.localRotation = Quaternion.identity;
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

        internal void ApplyNetwork(Vector3 position, float yaw, float velocity, bool snap)
        {
            float blend = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 15f);
            Root.SetPositionAndRotation(Vector3.Lerp(Root.position, position, blend),
                Quaternion.Slerp(Root.rotation, Quaternion.Euler(0, yaw, 0), blend));
            speed = velocity;
        }

        internal void Drive(Vector2 input, bool braking)
        {
            float dt = Time.deltaTime;
            float target = braking ? 0 : input.y >= 0 ? topSpeed * input.y : (IsBike ? 3f : 7f) * input.y;
            float acceleration = braking || Mathf.Sign(target) != Mathf.Sign(speed) ? 20f
                : Mathf.Abs(input.y) > 0.03f ? engine : 4.4f;
            speed = Mathf.MoveTowards(speed, target, acceleration * dt);
            if (IsBike)
            {
                float lean = -input.x * Mathf.Clamp01(Mathf.Abs(speed) / 7f) * 24f;
                shell.localRotation = Quaternion.Slerp(shell.localRotation, Quaternion.Euler(0, 0, lean), dt * 9f);
            }
            if (Mathf.Abs(speed) < 0.04f)
                return;

            body.enabled = false;
            try
            {
                Quaternion steering = Root.rotation * Quaternion.Euler(0, input.x * Mathf.Sign(speed)
                    * Mathf.Lerp(92f, 48f, Mathf.Abs(speed) / topSpeed) * steeringStrength * dt, 0);
                Vector3 center = Root.position + Vector3.up * body.center.y;
                Vector3 halves = body.size * 0.5f - Vector3.one * 0.075f;
                if (!Physics.CheckBox(center, halves, steering, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    Root.rotation = steering;
                Vector3 direction = Root.forward * Mathf.Sign(speed);
                float distance = Mathf.Abs(speed) * dt;
                if (Physics.BoxCast(center, halves, direction, out RaycastHit hit, Root.rotation,
                    distance + 0.07f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    distance = Mathf.Max(0, hit.distance - 0.07f);
                    speed = 0;
                }

                Root.position += direction * distance;
                foreach (Transform wheel in wheels)
                    wheel.Rotate(Vector3.right, distance * 163.7f, Space.Self);
            }
            finally
            {
                body.enabled = true;
            }
        }

        private static void Part(HarborBlockout art, Transform parent, PrimitiveType type, string name,
            Vector3 center, Vector3 size, Color color, float z = 0)
        {
            art.Part(parent, type, name, center, size, color).transform.localRotation = Quaternion.Euler(0, 0, z);
        }
    }
}
