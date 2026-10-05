using UnityEngine;

namespace NOTLD
{
    /// <summary>Muzzle flashes (per-weapon colour/size) and the frag-grenade blast.</summary>
    public static partial class Effects
    {
        public static readonly Color FlashWarm = new Color(1f, 0.78f, 0.38f);

        /// <summary>Muzzle flash colour: blue for the Nightshade, dark purple for the Railgun, warm for everything else.</summary>
        public static Color FlashColor(WeaponDef def)
        {
            if (def != null)
            {
                if (def.Name == "Nightshade") return new Color(0.15f, 0.5f, 1f);
                if (def.Name == "Railgun") return new Color(0.36f, 0.05f, 0.68f);
            }
            return FlashWarm;
        }

        /// <summary>Relative flash size per weapon.</summary>
        public static float FlashScale(WeaponDef def)
        {
            if (def == null) return 1f;
            if (def.Name == "Railgun") return 2.4f;
            if (def.Name == "Nightshade") return 1.5f;
            switch (def.Class)
            {
                case WeaponClass.Pistol: return 0.8f;
                case WeaponClass.SMG: return 0.9f;
                case WeaponClass.Shotgun: return 1.5f;
                case WeaponClass.LMG: return 1.2f;
                case WeaponClass.Sniper: return 1.3f;
                case WeaponClass.Launcher: return 1.7f;
                case WeaponClass.Wonder: return 1.3f;
                default: return 1f;
            }
        }

        static Material FlashMat(Color tint)
        {
            return Mats.Get("fx_flash_" + ColorUtility.ToHtmlStringRGB(tint), tint, 0.5f, 0f, new Color(tint.r * 6f, tint.g * 6f, tint.b * 6f, 1f));
        }

        /// <summary>First-person flash on the local viewmodel muzzle (3D star + coloured screen glow).</summary>
        public static void MuzzleFlash(Transform muzzle, WeaponDef def)
        {
            if (muzzle == null) return;
            float sc = FlashScale(def);
            Color tint = FlashColor(def);
            MuzzleFlash(muzzle, 0.05f * sc, tint);
            MuzzleGlow.Spawn(muzzle, tint, 190f * sc, (def != null && def.Name == "Railgun") ? 0.12f : 0.07f);
        }

        public static void MuzzleFlash(Transform muzzle, float size, Color tint)
        {
            if (muzzle == null) return;
            var m = FlashMat(tint);
            var root = new GameObject("MuzzleFlash").transform;
            root.SetParent(muzzle, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            Mats.Prim(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(size, size, size * 2f), m);
            for (int i = 0; i < 3; i++)   // star spikes across the muzzle
                Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(size * 0.16f, size * 3.4f, size * 0.16f), m, new Vector3(0, 0, i * 60f));
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0, 0, size * 1.7f), new Vector3(size * 0.3f, size * 0.3f, size * 3.4f), m);   // forward jet
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = root.gameObject.AddComponent<Light>();
            l.type = LightType.Point; l.range = 7f; l.intensity = 3.5f + size * 30f; l.color = tint; l.shadows = LightShadows.None;
            root.gameObject.AddComponent<FxShrink>().Init(0.06f, Vector3.zero);
        }

        /// <summary>Flash for another player's shot (world position only).</summary>
        public static void MuzzleFlashAt(Vector3 pos, WeaponDef def)
        {
            float sc = FlashScale(def);
            Color tint = FlashColor(def);
            var go = Mats.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * 0.12f * sc, FlashMat(tint));
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 6f; l.intensity = 3f; l.color = tint; l.shadows = LightShadows.None;
            go.AddComponent<FxShrink>().Init(0.06f, Vector3.zero);
            MuzzleGlow.SpawnAt(pos, tint, 190f * sc, 0.07f, true);
        }

        /// <summary>Frag grenade detonation: hot sparks, flying debris, rolling smoke, a shockwave ring and a screen glow.</summary>
        static void GrenadeBlast(Vector3 pos, float radius)
        {
            var hot = Mats.Get("fx_hot", Color.white, 0.2f, 0f, new Color(7, 7, 7));
            var dark = Mats.Get("fx_debris", Mats.Gray(0.07f), 0.1f);
            var smoke = Mats.Get("fx_smoke", Mats.Gray(0.13f), 0.05f);

            var ring = Mats.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * 0.5f,
                Mats.Get("fx_shock", new Color(1, 1, 1, 0.22f), 0.2f, 0f, new Color(1.2f, 1.2f, 1.2f), true));
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.AddComponent<FxShock>().Init(0.4f, radius * 1.7f);

            for (int i = 0; i < 26; i++)
            {
                var go = Mats.Prim(PrimitiveType.Cube, null, pos, Vector3.one * Random.Range(0.04f, 0.08f), hot, Random.rotation.eulerAngles);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<FxShrink>().Init(Random.Range(0.35f, 0.7f), Random.onUnitSphere * Random.Range(8f, 18f) + Vector3.up * 3f);
            }
            for (int i = 0; i < 16; i++)
            {
                var go = Mats.Prim(PrimitiveType.Cube, null, pos, Vector3.one * Random.Range(0.06f, 0.15f), dark, Random.rotation.eulerAngles);
                go.AddComponent<FxShrink>().Init(Random.Range(0.9f, 1.5f), (Random.onUnitSphere + Vector3.up * 0.8f) * Random.Range(4f, 9f));
            }
            for (int i = 0; i < 7; i++)
            {
                var go = Mats.Prim(PrimitiveType.Sphere, null, pos + Random.insideUnitSphere * 0.7f, Vector3.one * Random.Range(1f, 1.9f), smoke);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<FxShrink>().Init(Random.Range(1.3f, 1.9f), Vector3.up * 1.6f + Random.insideUnitSphere * 1.2f);
            }

            // orange-white flash on the screen overlay (the 3D scene is black-and-white)
            MuzzleGlow.SpawnAt(pos, new Color(1f, 0.55f, 0.15f), 1300f, 0.42f, true);

            var pc = PlayerController.I;
            if (pc != null)
            {
                float d = Vector3.Distance(pc.transform.position, pos);
                float reach = radius * 2.4f;
                if (d < reach) GameInput.Rumble(Mathf.Lerp(1f, 0.2f, d / reach), 1f, 0.4f);
            }
        }
    }

    /// <summary>Expanding flattened sphere used as a shockwave ring.</summary>
    public class FxShock : MonoBehaviour
    {
        float life = 0.4f, t, size = 6f;
        public void Init(float life, float size) { this.life = life; this.size = size; }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            float r = Mathf.Lerp(0.5f, size, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(r, r * 0.14f * (1f - k) + 0.01f, r);
            if (t >= life) Destroy(gameObject);
        }
    }
}
