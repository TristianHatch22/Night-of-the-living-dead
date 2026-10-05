using UnityEngine;

namespace NOTLD
{
    /// <summary>Lightweight procedural VFX (no particle assets needed).</summary>
    public static partial class Effects
    {
        public static void Puff(Vector3 pos, float size, float shade)
        {
            var m = Mats.Get("fx_puff_" + (int)(shade * 10), Mats.Gray(shade), 0.1f);
            for (int i = 0; i < 4; i++)
            {
                var go = Mats.Prim(PrimitiveType.Cube, null, pos + Random.insideUnitSphere * size * 0.5f, Vector3.one * size * Random.Range(0.4f, 1f), m, Random.rotation.eulerAngles);
                go.AddComponent<FxShrink>().Init(0.35f, Random.insideUnitSphere * 1.5f);
            }
        }

        public static void Impact(Vector3 pos, Vector3 normal)
        {
            var m = Mats.Get("fx_spark", Color.white, 0.5f, 0f, new Color(3, 3, 3));
            for (int i = 0; i < 3; i++)
            {
                var go = Mats.Prim(PrimitiveType.Cube, null, pos + normal * 0.02f, Vector3.one * 0.025f, m);
                go.AddComponent<FxShrink>().Init(0.2f, (normal + Random.insideUnitSphere) * 3f);
            }
        }

        public static void MuzzleFlash(Transform muzzle, float size)
        {
            if (muzzle == null) return;
            var m = Mats.Get("fx_flash", Color.white, 0.5f, 0f, new Color(6, 6, 6));
            var go = Mats.Prim(PrimitiveType.Sphere, muzzle, Vector3.zero, new Vector3(size, size, size * 2f), m);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 6f; l.intensity = 3f; l.color = Color.white; l.shadows = LightShadows.None;
            go.AddComponent<FxShrink>().Init(0.05f, Vector3.zero);
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = Mats.Get("fx_tracer", Color.white, 0.5f, 0, new Color(2, 2, 2));
            lr.startWidth = 0.012f; lr.endWidth = 0.004f;
            lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, 0.04f);
        }

        public static void RailBeam(Vector3 from, Vector3 to)
        {
            var go = new GameObject("RailBeam");
            go.transform.position = from;
            var lr = go.AddComponent<LineRenderer>();
            lr.material = Mats.Get("fx_rail", Color.white, 0.5f, 0f, new Color(8, 8, 8));
            lr.positionCount = 2; lr.useWorldSpace = true;
            lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.startWidth = 0.14f; lr.endWidth = 0.14f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 7f; l.intensity = 5f; l.shadows = LightShadows.None;
            go.AddComponent<FxBeam>().Init(0.5f, 0.14f, lr, l);
            PlasmaBurst(from, 0.5f);
            PlasmaBurst(to, 0.8f);
        }

        public static void PlasmaBurst(Vector3 pos, float size)
        {
            var m = Mats.Get("fx_plasma_ball", Color.white, 0.2f, 0f, new Color(6, 6, 6));
            var go = Mats.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * 0.2f, m);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FxGrow>().Init(0.22f, size);
        }

        public static void Lightning(Vector3 from, Vector3 to, float life = 0.25f)
        {
            var go = new GameObject("Bolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = Mats.Get("fx_bolt", Color.white, 0.5f, 0, new Color(6, 6, 6));
            lr.startWidth = 0.05f; lr.endWidth = 0.03f;
            int n = 10; lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < n - 1) p += Random.insideUnitSphere * 0.25f;
                lr.SetPosition(i, p);
            }
            var l = go.AddComponent<Light>();
            go.transform.position = (from + to) / 2f;
            l.type = LightType.Point; l.range = 8; l.intensity = 4; l.shadows = LightShadows.None;
            Object.Destroy(go, life);
        }

        public static void Explosion(Vector3 pos, float radius, string sfx = "explosion")
        {
            var m = Mats.Get("fx_boom", Color.white, 0.2f, 0f, new Color(5, 5, 5));
            var go = Mats.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * 0.5f, m);
            go.AddComponent<FxGrow>().Init(0.3f, radius * 1.4f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = radius * 4f; l.intensity = 6f; l.shadows = LightShadows.None;
            Puff(pos, 0.6f, 0.08f);
            if (sfx == "grenade_boom") GrenadeBlast(pos, radius);
            SFX.PlayAt(sfx, pos, 1f);
        }

        public static void ThunderBlast(Transform origin, float range)
        {
            var m = Mats.Get("fx_thunder", new Color(1, 1, 1, 0.3f), 0.2f, 0f, new Color(1.5f, 1.5f, 1.5f), true);
            for (int i = 0; i < 6; i++)
            {
                var go = Mats.Prim(PrimitiveType.Sphere, null, origin.position + origin.forward * (1f + i * range / 7f), Vector3.one * 0.5f, m);
                go.AddComponent<FxGrow>().Init(0.25f + i * 0.04f, 1.5f + i * 0.8f);
            }
        }
    }

    public class FxBeam : MonoBehaviour
    {
        float life, t, width; LineRenderer lr; Light l;
        public void Init(float life, float width, LineRenderer lr, Light l) { this.life = life; this.width = width; this.lr = lr; this.l = l; }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(1f - t / life);
            float w = width * k * k;
            lr.startWidth = w; lr.endWidth = w;
            if (l) l.intensity = 5f * k;
            if (t >= life) Destroy(gameObject);
        }
    }

    public class FxShrink : MonoBehaviour
    {
        float life, t; Vector3 vel, start;
        public void Init(float life, Vector3 vel) { this.life = life; this.vel = vel; start = transform.localScale; }
        void Update()
        {
            t += Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            vel += Physics.gravity * 0.3f * Time.deltaTime;
            transform.localScale = start * Mathf.Clamp01(1 - t / life);
            if (t >= life) Destroy(gameObject);
        }
    }

    public class FxGrow : MonoBehaviour
    {
        float life, t, size;
        public void Init(float life, float size) { this.life = life; this.size = size; }
        void Update()
        {
            t += Time.deltaTime;
            float k = t / life;
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, size, Mathf.Sqrt(k));
            var l = GetComponent<Light>(); if (l) l.intensity = Mathf.Lerp(6f, 0f, k);
            if (t >= life) Destroy(gameObject);
        }
    }
}
