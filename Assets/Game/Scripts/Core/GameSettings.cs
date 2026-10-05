using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NOTLD
{
    public enum GraphicsPreset { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>
    /// Persistent player settings + one-click graphics presets.
    /// Every preset step up improves resolution, AA, shadows and post effects.
    /// The whole game is rendered black & white through a global post volume.
    /// </summary>
    public static class GameSettings
    {
        public static GraphicsPreset Preset = GraphicsPreset.High;
        public static float Sensitivity = 1f;   // 0.1 - 3
        public static float FieldOfView = 75f;  // 60 - 100
        public static float Volume = 0.8f;      // 0 - 1

        static bool loaded;
        static UniversalRenderPipelineAsset runtimeAsset;
        static Volume globalVolume;

        public static readonly string[] PresetNames = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Preset = (GraphicsPreset)PlayerPrefs.GetInt("nld_preset", (int)GraphicsPreset.High);
            Sensitivity = PlayerPrefs.GetFloat("nld_sens", 1f);
            FieldOfView = PlayerPrefs.GetFloat("nld_fov", 75f);
            Volume = PlayerPrefs.GetFloat("nld_vol", 0.8f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("nld_preset", (int)Preset);
            PlayerPrefs.SetFloat("nld_sens", Sensitivity);
            PlayerPrefs.SetFloat("nld_fov", FieldOfView);
            PlayerPrefs.SetFloat("nld_vol", Volume);
            PlayerPrefs.Save();
        }

        /// <summary>Apply everything to the current scene.</summary>
        public static void ApplyAll()
        {
            Load();
            AudioListener.volume = Volume;
            ApplyGraphics();
        }

        public static void ApplyGraphics()
        {
            int q = (int)Preset;

            // --- Render pipeline asset (runtime copy so the project asset is never modified) ---
            if (runtimeAsset == null)
            {
                var src = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (src != null)
                {
                    runtimeAsset = Object.Instantiate(src);
                    runtimeAsset.name = "NOTLD_RuntimeURP";
                    QualitySettings.renderPipeline = runtimeAsset;
                }
            }
            if (runtimeAsset != null)
            {
                runtimeAsset.renderScale = new[] { 0.65f, 0.85f, 1f, 1f }[q];
                runtimeAsset.msaaSampleCount = new[] { 1, 1, 2, 4 }[q];
                runtimeAsset.shadowDistance = new[] { 0f, 20f, 35f, 60f }[q];
                runtimeAsset.shadowCascadeCount = new[] { 1, 1, 2, 4 }[q];
                runtimeAsset.supportsHDR = q >= 2;
            }

            QualitySettings.globalTextureMipmapLimit = new[] { 2, 1, 0, 0 }[q];
            QualitySettings.anisotropicFiltering = q >= 2 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
            QualitySettings.lodBias = new[] { 0.5f, 1f, 1.5f, 2f }[q];
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 144;

            // --- Lights ---
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                bool decorative = l.CompareTag("EditorOnly") == false && l.name.StartsWith("Deco");
                if (decorative) l.enabled = q >= 2;
                if (l.type == LightType.Directional || l.name.StartsWith("Key"))
                    l.shadows = q == 0 ? LightShadows.None : (q == 1 ? LightShadows.Hard : LightShadows.Soft);
                else
                    l.shadows = q >= 3 ? LightShadows.Soft : LightShadows.None;
            }

            // --- Fog ---
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.06f, 0.06f);
            RenderSettings.fogDensity = new[] { 0.05f, 0.045f, 0.04f, 0.035f }[q];

            // --- Cameras ---
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = true;
                data.antialiasing = q == 0 ? AntialiasingMode.None : (q == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing);
                data.antialiasingQuality = q == 3 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
                data.renderShadows = q > 0;
                if (cam.CompareTag("MainCamera")) cam.fieldOfView = FieldOfView;
            }

            // --- Post processing (black & white look) ---
            BuildOrUpdateVolume(q);
        }

        static void BuildOrUpdateVolume(int q)
        {
            if (globalVolume == null)
            {
                var go = GameObject.Find("NOTLD_PostVolume");
                if (go == null) go = new GameObject("NOTLD_PostVolume");
                globalVolume = go.GetComponent<Volume>();
                if (globalVolume == null) globalVolume = go.AddComponent<Volume>();
                globalVolume.isGlobal = true;
                globalVolume.priority = 100;
                globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }
            var p = globalVolume.profile;

            var ca = Get<ColorAdjustments>(p);
            ca.saturation.Override(-100f);                       // black & white
            ca.contrast.Override(new[] { 15f, 20f, 25f, 30f }[q]);
            ca.postExposure.Override(0.1f);

            var tm = Get<Tonemapping>(p);
            tm.mode.Override(q >= 2 ? TonemappingMode.ACES : TonemappingMode.Neutral);

            var vg = Get<Vignette>(p);
            vg.intensity.Override(new[] { 0.25f, 0.32f, 0.38f, 0.42f }[q]);
            vg.smoothness.Override(0.45f);

            var bl = Get<Bloom>(p);
            bl.active = q >= 1;
            bl.intensity.Override(new[] { 0f, 0.4f, 0.7f, 0.9f }[q]);
            bl.threshold.Override(0.95f);
            bl.highQualityFiltering.Override(q >= 3);

            var fg = Get<FilmGrain>(p);
            fg.active = q >= 1;
            fg.type.Override(q >= 3 ? FilmGrainLookup.Medium3 : FilmGrainLookup.Thin2);
            fg.intensity.Override(new[] { 0f, 0.25f, 0.35f, 0.45f }[q]);

            var ab = Get<ChromaticAberration>(p);
            ab.active = q >= 3;
            ab.intensity.Override(0.15f);
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet(out T c)) c = p.Add<T>(true);
            c.active = true;
            return c;
        }

        /// <summary>Used by the HUD to flash the screen red-ish (in B&W: darker) when hurt.</summary>
        public static void SetDamageVignette(float amount)
        {
            if (globalVolume == null) return;
            if (globalVolume.profile.TryGet(out Vignette vg))
            {
                float baseV = new[] { 0.25f, 0.32f, 0.38f, 0.42f }[(int)Preset];
                vg.intensity.Override(Mathf.Lerp(baseV, 0.65f, amount));
            }
        }
    }
}
