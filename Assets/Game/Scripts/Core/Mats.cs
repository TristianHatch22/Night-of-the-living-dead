using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NOTLD
{
    /// <summary>Material cache + primitive helpers. In edit mode materials are saved as assets so built scenes keep them.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        const string Folder = "Assets/Game/Materials";

        public static Material Get(string key, Color color, float smooth = 0.3f, float metal = 0f, Color? emission = null, bool transparent = false)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                string path = Folder + "/" + key + ".mat";
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Game")) AssetDatabase.CreateFolder("Assets", "Game");
                    if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game", "Materials");
                    m = Create(key, color, smooth, metal, emission, transparent);
                    AssetDatabase.CreateAsset(m, path);
                }
                cache[key] = m;
                return m;
            }
#endif
            m = Create(key, color, smooth, metal, emission, transparent);
            cache[key] = m;
            return m;
        }

        static Material Create(string key, Color color, float smooth, float metal, Color? emission, bool transparent)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool collider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            }
            return go;
        }

        public static Color Gray(float v) => new Color(v, v, v, 1f);
    }
}
