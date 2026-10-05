using UnityEngine;

namespace NOTLD
{
    /// <summary>A thrown frag grenade: bounces around, then detonates after its fuse runs out.</summary>
    public class Grenade : MonoBehaviour
    {
        public const float Radius = 6.5f;
        public const int Damage = 2600;
        const float SelfRadius = 3.6f;
        const float SelfDamage = 95f;

        float fuse = 2.4f;
        float bounceCd;
        bool blown;
        Rigidbody rb;
        Light blink;
        Renderer blinkR;

        public static Grenade Create(Vector3 pos, Vector3 velocity, Collider ignore)
        {
            var go = new GameObject("Grenade");
            go.transform.position = pos;

            var body = Mats.Get("grenade_body", new Color(0.16f, 0.17f, 0.15f), 0.35f, 0.4f);
            var metal = Mats.Get("grenade_metal", new Color(0.55f, 0.55f, 0.55f), 0.7f, 0.9f);
            var bm = Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.085f, 0.1f, 0.085f), body);
            Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.058f, 0), new Vector3(0.035f, 0.012f, 0.035f), metal);           // fuse neck
            Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0.022f, 0.062f, 0), new Vector3(0.05f, 0.008f, 0.014f), metal);          // spoon
            var led = Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 0.05f, 0.04f), Vector3.one * 0.016f,
                Mats.Get("grenade_led", Color.white, 0.2f, 0f, new Color(5, 5, 5)));
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.05f;
            var pm = new PhysicsMaterial("GrenadePM") { bounciness = 0.45f, dynamicFriction = 0.5f, staticFriction = 0.5f };
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            col.sharedMaterial = pm;
            if (ignore != null) Physics.IgnoreCollision(col, ignore);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.onUnitSphere * 14f;

            var g = go.AddComponent<Grenade>();
            g.rb = rb;
            g.blinkR = led.GetComponent<Renderer>();
            return g;
        }

        void Update()
        {
            if (blown) return;
            fuse -= Time.deltaTime;
            bounceCd -= Time.deltaTime;

            // blink faster as the fuse burns down
            if (blinkR != null)
            {
                float rate = Mathf.Lerp(3f, 16f, 1f - Mathf.Clamp01(fuse / 2.4f));
                blinkR.enabled = Mathf.Sin(Time.time * rate * Mathf.PI) > 0f;
            }
            if (fuse <= 0f) Explode();
            else if (transform.position.y < -30f) Destroy(gameObject);   // fell out of the world
        }

        void OnCollisionEnter(Collision c)
        {
            if (blown || bounceCd > 0f) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.2f) return;
            bounceCd = 0.09f;
            SFX.PlayAt("grenade_bounce", transform.position, Mathf.Clamp01(speed / 8f) * 0.8f + 0.15f, Random.Range(0.9f, 1.12f));
        }

        void Explode()
        {
            blown = true;
            Vector3 pos = transform.position + Vector3.up * 0.15f;

            // zombies (host-authoritative online: puppet hits are forwarded to the host)
            GameManager.Explode(pos, Radius, Damage, "grenade_boom");

            // the thrower is hurt if they stay too close (never enough to kill outright from full health)
            var pc = PlayerController.I;
            if (pc != null && !pc.IsDown)
            {
                float d = Vector3.Distance(pc.transform.position + Vector3.up, pos);
                if (d < SelfRadius) pc.TakeDamage(SelfDamage * Mathf.Lerp(1f, 0.25f, d / SelfRadius), pos);
            }
            Destroy(gameObject);
        }
    }
}
