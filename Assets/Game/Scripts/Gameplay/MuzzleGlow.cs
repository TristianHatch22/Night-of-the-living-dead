using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOTLD
{
    /// <summary>
    /// Screen-space coloured glow drawn on a UI overlay at a world position (a muzzle or an explosion).
    /// The scene post-process turns the 3D world black-and-white, but UI overlays are not desaturated,
    /// so this is what lets muzzle flashes (blue plasma, dark purple rail...) actually show their colour.
    /// </summary>
    public class MuzzleGlow : MonoBehaviour
    {
        static MuzzleGlow inst;
        static Sprite glowSprite, starSprite;

        class Flash
        {
            public RectTransform root;
            public Image glow, star, core;
            public Transform anchor;
            public Vector3 world;
            public float t, life, size, spin;
        }

        readonly List<Flash> flashes = new List<Flash>();
        Canvas canvas;
        Camera cam;

        static MuzzleGlow Get()
        {
            if (inst != null) return inst;
            var go = new GameObject("MuzzleGlow");
            inst = go.AddComponent<MuzzleGlow>();
            inst.canvas = UIFactory.MakeCanvas("MuzzleGlowCanvas", 5);
            inst.canvas.transform.SetParent(go.transform, false);
            // purely visual - never block clicks
            var gr = inst.canvas.GetComponent<GraphicRaycaster>(); if (gr != null) Destroy(gr);
            return inst;
        }

        /// <summary>Flash that follows a transform (the local viewmodel muzzle).</summary>
        public static void Spawn(Transform anchor, Color tint, float size, float life = 0.07f)
        {
            if (anchor == null) return;
            Get().Add(anchor, anchor.position, tint, size, life);
        }

        /// <summary>Flash at a fixed world point. Size is in screen pixels at 1 metre; it shrinks with distance when scaleByDistance is set.</summary>
        public static void SpawnAt(Vector3 world, Color tint, float size, float life, bool scaleByDistance)
        {
            var g = Get();
            if (scaleByDistance)
            {
                var c = g.GetCam();
                if (c != null)
                {
                    float d = Vector3.Distance(c.transform.position, world);
                    size *= Mathf.Clamp(6f / Mathf.Max(d, 1f), 0.15f, 1.5f);
                }
            }
            g.Add(null, world, tint, size, life);
        }

        Camera GetCam()
        {
            if (cam == null || !cam.isActiveAndEnabled)
            {
                var pc = PlayerController.I;
                cam = pc != null && pc.Cam != null ? pc.Cam : Camera.main;
            }
            return cam;
        }

        void Add(Transform anchor, Vector3 world, Color tint, float size, float life)
        {
            EnsureSprites();
            var f = new Flash { anchor = anchor, world = world, life = life, size = size, spin = Random.Range(0f, 360f) };
            var root = UIFactory.Rect("Flash", canvas.transform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(10, 10));
            f.root = root;
            f.glow = MakeImage(root, glowSprite, tint);
            f.star = MakeImage(root, starSprite, Color.Lerp(tint, Color.white, 0.35f));
            f.core = MakeImage(root, glowSprite, Color.Lerp(tint, Color.white, 0.8f));
            flashes.Add(f);
            Place(f, 0f);
        }

        static Image MakeImage(Transform parent, Sprite s, Color c)
        {
            var go = new GameObject("img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var im = go.AddComponent<Image>();
            im.sprite = s; im.color = c; im.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return im;
        }

        void Update()
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.t += Time.unscaledDeltaTime;
                if (f.t >= f.life || f.root == null) { if (f.root != null) Destroy(f.root.gameObject); flashes.RemoveAt(i); continue; }
                Place(f, f.t / f.life);
            }
        }

        void Place(Flash f, float k)
        {
            var c = GetCam();
            if (c == null) { f.root.gameObject.SetActive(false); return; }
            Vector3 w = f.anchor != null ? f.anchor.position : f.world;
            Vector3 sp = c.WorldToScreenPoint(w);
            if (sp.z <= 0.05f) { f.root.gameObject.SetActive(false); return; }
            f.root.gameObject.SetActive(true);
            f.root.anchoredPosition = new Vector2(sp.x, sp.y) / UiScale();

            float fade = 1f - k;
            float pop = Mathf.Lerp(1.25f, 0.55f, k);
            float s = f.size * pop;
            Size(f.glow, s * 1.15f, 0.85f * fade);
            Size(f.star, s * 1.5f, fade);
            Size(f.core, s * 0.45f, fade);
            f.star.rectTransform.localRotation = Quaternion.Euler(0, 0, f.spin + k * 25f);
        }

        // same maths as the CanvasScaler (reference 1920x1080, match 0.5) so positions are right even on the first frame
        static float UiScale()
        {
            float lw = Mathf.Log(Screen.width / 1920f, 2f), lh = Mathf.Log(Screen.height / 1080f, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(lw, lh, 0.5f));
        }

        static void Size(Image im, float px, float alpha)
        {
            im.rectTransform.sizeDelta = new Vector2(px, px);
            var c = im.color; c.a = Mathf.Clamp01(alpha); im.color = c;
        }

        // ---------------------------------------------------------------- procedural sprites
        static void EnsureSprites()
        {
            if (glowSprite != null && starSprite != null) return;
            const int s = 128;
            var g = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var st = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var gp = new Color32[s * s]; var sp = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    gp[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));

                    // 6-point star: bright spikes (narrow rays) plus a soft centre
                    float ang = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 24f);   // 6 spikes
                    float rad = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                    float sa = Mathf.Clamp01(ray * rad * 1.6f + Mathf.Pow(Mathf.Clamp01(1f - d * 2.2f), 2f));
                    sp[y * s + x] = new Color32(255, 255, 255, (byte)(sa * 255f));
                }
            g.SetPixels32(gp); g.Apply(); st.SetPixels32(sp); st.Apply();
            glowSprite = Sprite.Create(g, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            starSprite = Sprite.Create(st, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }
    }
}
