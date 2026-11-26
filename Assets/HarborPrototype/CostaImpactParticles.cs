using UnityEngine;
using UnityEngine.Rendering;
using Surface = CostaLivre.CostaWorldDamage.Surface;

namespace CostaLivre
{
    internal sealed class CostaImpactParticles
    {
        private readonly ParticleSystem chips;
        private readonly ParticleSystem dust;
        private readonly ParticleSystem sparks;
        private readonly Material material;
        private uint seed = 29791;

        internal CostaImpactParticles(Transform root)
        {
            Shader shader = Resources.Load<Shader>("Harbor/ImpactParticle");
            if (shader == null)
                return;
            material = new Material(shader);
            chips = Create(root, "Impact fragments", true);
            dust = Create(root, "Impact dust", false);
            sparks = Create(root, "Impact sparks", true);
            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.lengthScale = 2.1f;
            sparkRenderer.velocityScale = .043f;
        }

        private ParticleSystem Create(Transform parent, string name, bool collides)
        {
            GameObject emitter = new GameObject(name);
            emitter.SetActive(false);
            emitter.transform.SetParent(parent, false);
            var system = emitter.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.startSpeed = 0;
            main.startLifetime = 0.65f;
            main.maxParticles = collides ? 320 : 150;
            main.gravityModifier = collides ? 0.84f : 0;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var lifetime = system.colorOverLifetime;
            lifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.83f, .22f), new GradientAlphaKey(0, 1) });
            lifetime.color = new ParticleSystem.MinMaxGradient(fade);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, collides ?
                AnimationCurve.Linear(0, 1, 1, .38f) : AnimationCurve.Linear(0, .25f, 1, 1));

            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.quality = collides ? ParticleSystemCollisionQuality.Medium : ParticleSystemCollisionQuality.Low;
            collision.enableDynamicColliders = false;
            collision.maxCollisionShapes = collides ? 64 : 24;
            collision.radiusScale = collides ? .75f : .25f;
            collision.bounce = collides ? .26f : .04f;
            collision.dampen = collides ? .42f : .74f;
            collision.lifetimeLoss = collides ? .13f : .30f;
            ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            emitter.SetActive(true);
            system.Play();
            return system;
        }

        internal void Burst(Surface surface, Vector3 position, Vector3 normal, bool broken, bool mobile)
        {
            if (chips == null)
                return;
            Color tint = surface switch
            {
                Surface.Metal => new Color(1f, .65f, .24f, 1),
                Surface.Wood => new Color(.66f, .43f, .24f, .96f),
                Surface.Glass => new Color(.53f, .84f, .94f, .83f),
                Surface.Earth => new Color(.49f, .35f, .22f, .65f),
                _ => new Color(.69f, .67f, .61f, .85f)
            };
            bool spark = surface == Surface.Metal;
            int count = broken ? 24 : spark ? 12 : 7;
            if (mobile)
                count = (count + 1) / 2;
            Emit(spark ? sparks : chips, count, position, normal, tint, broken ? 6f : spark ? 5f : 3f,
                broken ? .13f : .06f, spark ? .34f : .69f, spark ? 2 : 0);
            if (surface != Surface.Glass || broken)
                Emit(dust, mobile ? 3 : 6, position, normal, new Color(tint.r, tint.g, tint.b, .27f),
                    .9f, broken ? .53f : .32f, 1.05f, 1);
        }

        internal void Blast(Vector3 position, bool mobile)
        {
            if (chips == null)
                return;
            Emit(sparks, mobile ? 20 : 40, position, Vector3.up, new Color(1, .54f, .20f, .90f), 9f, .105f, .72f, 2);
            Emit(dust, mobile ? 10 : 20, position, Vector3.up, new Color(.40f, .38f, .35f, .43f), 3.3f, 2.0f, 1.75f, 1);
        }

        private void Emit(ParticleSystem system, int count, Vector3 point, Vector3 normal, Color color,
            float speed, float size, float life, int style)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 spread = new Vector3(Random01() * 2 - 1, Random01() * 2 - 1, Random01() * 2 - 1);
                Vector3 direction = (normal * .78f + spread).normalized;
                var particle = new ParticleSystem.EmitParams
                {
                    position = point + normal * .035f,
                    velocity = direction * (speed * (.31f + .69f * Random01())) + Vector3.up * (style == 1 ? .37f : 0),
                    startColor = color,
                    startSize = size * (.38f + .62f * Random01()),
                    startLifetime = life * (.51f + .49f * Random01()),
                    rotation = Random01() * 360
                };
                system.Emit(particle, 1);
            }
        }

        private float Random01()
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            return (seed >> 8) / 16777216f;
        }

        internal void Release()
        {
            if (chips != null)
                Object.Destroy(chips.gameObject);
            if (dust != null)
                Object.Destroy(dust.gameObject);
            if (sparks != null)
                Object.Destroy(sparks.gameObject);
            Object.Destroy(material);
        }
    }
}
