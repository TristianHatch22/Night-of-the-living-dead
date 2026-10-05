using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
    /// <summary>
    /// Procedurally synthesized sound effects + original perk jingles, so the game needs no audio files.
    /// To use your own audio, drop a clip named exactly like the sound (e.g. shot_rifle, jingle_Juggernog, box_open)
    /// into Assets/Resources/Audio/ and it will automatically replace the generated one.
    /// </summary>
    public static class SFX
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static AudioSource source2D;
        const int Rate = 44100;
        const float TwoPi = 6.2831853f;

        public static readonly string[] AllNames =
        {
            "shot_pistol","shot_smg","shot_rifle","shot_lmg","shot_shotgun","shot_sniper","shot_launcher",
            "shot_ray","shot_plasma","shot_rail","shot_thunder","shot_zap","plasma_hit","plasma_boom","explosion","grenade_pin","grenade_throw","grenade_bounce","grenade_boom",
            "reload_mag","reload_click","reload_shell","pump","bolt","reload_energy","reload_chirp","reload_launcher","reload_clank","draw",
            "empty","hitmarker","knife","buy","deny","perk","slurp","revive","round_start","round_end","groan","zombie_hit",
            "player_hurt","board","click","down",
            "box_open","box_rattle","box_squeak","box_reveal","box_close","box_womp",
            "jingle_Juggernog","jingle_DoubleTap","jingle_QuickRevive","jingle_SpeedCola"
        };

        public static void Prewarm() { foreach (var n in AllNames) Get(n); }

        static AudioSource Src
        {
            get
            {
                if (source2D == null)
                {
                    var go = new GameObject("SFX_2D");
                    Object.DontDestroyOnLoad(go);
                    source2D = go.AddComponent<AudioSource>();
                    source2D.spatialBlend = 0f;
                }
                return source2D;
            }
        }

        public static void Play(string name, float vol = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(name)) return;
            var c = Get(name);
            if (c == null) return;
            if (Mathf.Abs(pitch - 1f) > 0.001f)
            {
                var go = new GameObject("SFX_" + name);
                var s = go.AddComponent<AudioSource>();
                s.spatialBlend = 0; s.pitch = pitch; s.volume = vol; s.clip = c; s.Play();
                Object.Destroy(go, c.length / Mathf.Max(0.1f, pitch) + 0.1f);
            }
            else Src.PlayOneShot(c, vol);
        }

        public static void PlayAt(string name, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            var c = Get(name);
            if (c == null) return;
            var go = new GameObject("SFX3D_" + name);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0.85f; s.minDistance = 3f; s.maxDistance = 40f; s.rolloffMode = AudioRolloffMode.Linear;
            s.pitch = pitch; s.volume = vol; s.clip = c; s.Play();
            Object.Destroy(go, c.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public static AudioClip Get(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);   // user-supplied override
            if (c == null)
            {
                var d = Generate(name);
                if (d != null) c = Create(name, d);
            }
            if (c != null) cache[name] = c;
            return c;
        }

        static AudioClip Create(string name, float[] d)
        {
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.1f);
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        // ===================================================================== SOUND DEFINITIONS
        static float[] Generate(string n)
        {
            float[] d;
            switch (n)
            {
                // ------------- gunshots -------------
                case "shot_pistol":
                    d = NoiseBurst(0.28f, 17f, 1f, 260f, 120f, 0.5f, 11); Layer(ref d, Crack(0.05f, 0.9f, 12)); return Echo(d, 0.045f, 0.25f, 0.2f);
                case "shot_smg":
                    d = NoiseBurst(0.2f, 26f, 0.9f, 230f, 140f, 0.4f, 13); Layer(ref d, Crack(0.04f, 0.8f, 14)); return Echo(d, 0.04f, 0.2f, 0.12f);
                case "shot_rifle":
                    d = NoiseBurst(0.34f, 14f, 1f, 160f, 80f, 0.7f, 15); Layer(ref d, Crack(0.05f, 1f, 16)); return Echo(d, 0.06f, 0.3f, 0.25f);
                case "shot_lmg":
                    d = NoiseBurst(0.3f, 16f, 1f, 130f, 70f, 0.8f, 17); Layer(ref d, Crack(0.05f, 1f, 18)); return Echo(d, 0.07f, 0.28f, 0.2f);
                case "shot_shotgun":
                    d = NoiseBurst(0.55f, 7f, 1f, 100f, 60f, 0.9f, 19); Layer(ref d, NoiseBurst(0.12f, 40f, 0.8f, 600f, 100f, 0.2f, 20)); Layer(ref d, Crack(0.07f, 1f, 21)); return Echo(d, 0.09f, 0.35f, 0.3f);
                case "shot_sniper":
                    d = NoiseBurst(0.7f, 5f, 1f, 90f, 50f, 1f, 22); Layer(ref d, Crack(0.08f, 1f, 23)); return Echo(d, 0.14f, 0.4f, 0.5f);
                case "shot_launcher":
                    d = NoiseBurst(0.5f, 6f, 1f, 120f, 45f, 1f, 24); Layer(ref d, Crack(0.04f, 0.6f, 25)); Layer(ref d, NoiseBurst(0.6f, 3f, 0.5f, 900f, 200f, 0f, 26), 0.05f); return Echo(d, 0.1f, 0.3f, 0.3f);
                case "shot_ray":
                    d = Osc(0.4f, 1800f, 250f, 1, 0.3f, 3f); Layer(ref d, Osc(0.4f, 90f, 50f, 0, 0.5f, 4f)); Layer(ref d, Zap(0.3f), 0f, 0.4f); return d;
                case "shot_plasma":
                    d = Osc(0.28f, 2600f, 500f, 0, 0.45f, 5f, 45f, 150f); Layer(ref d, Osc(0.22f, 900f, 200f, 2, 0.18f, 8f)); Layer(ref d, NoiseBurst(0.1f, 30f, 0.4f, 800f, 100f, 0.5f, 27)); Layer(ref d, Osc(0.3f, 120f, 60f, 0, 0.5f, 6f)); return Echo(d, 0.08f, 0.25f, 0.2f);
                case "shot_rail":
                    d = Osc(0.1f, 400f, 3200f, 1, 0.22f, 0f); Layer(ref d, Crack(0.1f, 1f, 28), 0.1f); Layer(ref d, NoiseBurst(0.9f, 5f, 1f, 120f, 45f, 1f, 29), 0.1f); Layer(ref d, Osc(0.6f, 2200f, 90f, 0, 0.5f, 6f), 0.1f); return Echo(d, 0.16f, 0.45f, 0.6f);
                case "shot_thunder":
                    d = Osc(0.9f, 120f, 40f, 2, 0.7f, 3f); Layer(ref d, NoiseBurst(0.9f, 4f, 0.8f, 300f, 40f, 1f, 30)); return Echo(d, 0.1f, 0.3f, 0.3f);
                case "shot_zap":
                    d = Zap(0.5f); Layer(ref d, NoiseBurst(0.2f, 10f, 0.3f, 2000f, 100f, 0.2f, 31)); return d;
                case "plasma_hit":
                    d = Zap(0.3f); Layer(ref d, NoiseBurst(0.25f, 12f, 0.6f, 500f, 80f, 0.6f, 32)); return d;
                case "plasma_boom":
                    d = NoiseBurst(0.6f, 6f, 1f, 200f, 60f, 1f, 33); Layer(ref d, Zap(0.5f), 0f, 0.6f); Layer(ref d, Osc(0.5f, 500f, 80f, 2, 0.3f, 5f)); return Echo(d, 0.1f, 0.3f, 0.25f);
                case "grenade_pin":
                    d = Click(1.4f, 0.8f); Layer(ref d, Osc(0.2f, 2300f, 1700f, 0, 0.16f, 16f), 0.01f); Layer(ref d, Click(0.8f, 0.5f), 0.06f); return d;
                case "grenade_throw":
                    d = NoiseBurst(0.28f, 8f, 0.55f, 1800f, 110f, 0.1f, 51); return d;
                case "grenade_bounce":
                    d = Click(0.9f, 0.8f); Layer(ref d, NoiseBurst(0.12f, 28f, 0.4f, 2400f, 300f, 0.35f, 52)); Layer(ref d, Osc(0.14f, 950f, 620f, 0, 0.12f, 24f), 0.005f); return d;
                case "grenade_boom":
                    d = NoiseBurst(1.9f, 2.0f, 1f, 200f, 36f, 1.7f, 61); Layer(ref d, Crack(0.1f, 0.8f, 62)); Layer(ref d, NoiseBurst(0.5f, 6f, 0.8f, 2600f, 200f, 0.3f, 63), 0.02f); Layer(ref d, NoiseBurst(0.7f, 5f, 0.3f, 3800f, 300f, 0f, 64), 0.4f, 0.8f); return Echo(d, 0.15f, 0.4f, 0.45f);
                case "explosion":
                    d = NoiseBurst(1.1f, 3f, 1f, 160f, 45f, 1f, 34); Layer(ref d, Crack(0.08f, 0.5f, 35)); return Echo(d, 0.12f, 0.35f, 0.4f);

                // ------------- reloads / handling -------------
                case "reload_mag":
                    d = Click(0.7f, 0.6f); Layer(ref d, NoiseBurst(0.14f, 22f, 0.35f, 3000f, 200f, 0.2f, 36), 0.12f); return d;
                case "reload_click":
                    d = Click(1.2f, 0.8f); Layer(ref d, NoiseBurst(0.08f, 35f, 0.45f, 400f, 120f, 0.5f, 37), 0.0f); Layer(ref d, Click(0.9f, 0.5f), 0.12f); return d;
                case "reload_shell":
                    d = Click(0.5f, 0.7f); Layer(ref d, Click(0.55f, 0.7f), 0.18f); Layer(ref d, Click(0.6f, 0.7f), 0.36f); Layer(ref d, NoiseBurst(0.05f, 40f, 0.3f, 1500f, 200f, 0.2f, 38), 0.18f); return d;
                case "pump":
                    d = Click(0.6f, 0.8f); Layer(ref d, NoiseBurst(0.1f, 30f, 0.5f, 1500f, 150f, 0.5f, 39)); Layer(ref d, Click(0.9f, 0.9f), 0.14f); Layer(ref d, NoiseBurst(0.08f, 35f, 0.5f, 1200f, 150f, 0.5f, 40), 0.14f); return d;
                case "bolt":
                    d = Click(0.5f, 0.8f); Layer(ref d, NoiseBurst(0.15f, 12f, 0.25f, 4000f, 200f, 0.1f, 41), 0.05f); Layer(ref d, Click(0.8f, 0.9f), 0.22f); Layer(ref d, Click(0.6f, 0.9f), 0.3f); return d;
                case "reload_energy":
                    d = Osc(0.5f, 200f, 900f, 0, 0.35f, 0f, 12f, 40f); Layer(ref d, Osc(0.5f, 100f, 450f, 2, 0.1f, 0f)); return d;
                case "reload_chirp":
                    d = Osc(0.07f, 1200f, 1200f, 0, 0.35f, 10f); Layer(ref d, Osc(0.09f, 1800f, 1800f, 0, 0.35f, 10f), 0.08f); return d;
                case "reload_launcher":
                    d = NoiseBurst(0.2f, 18f, 0.6f, 500f, 90f, 0.8f, 42); Layer(ref d, Click(0.4f, 0.9f), 0.1f); Layer(ref d, NoiseBurst(0.3f, 10f, 0.3f, 900f, 100f, 0.2f, 43), 0.2f); return d;
                case "reload_clank":
                    d = Click(0.4f, 1f); Layer(ref d, NoiseBurst(0.15f, 20f, 0.7f, 800f, 100f, 0.5f, 44)); Layer(ref d, Click(0.5f, 0.8f), 0.12f); return d;
                case "draw":
                    d = NoiseBurst(0.15f, 20f, 0.3f, 3000f, 200f, 0.1f, 45); Layer(ref d, Click(0.9f, 0.6f), 0.08f); return d;
                case "empty":
                    d = Click(0.8f, 0.6f); Layer(ref d, Click(0.5f, 0.3f), 0.03f); return d;
                case "knife":
                    d = NoiseBurst(0.15f, 18f, 0.5f, 5000f, 300f, 0.1f, 46); Layer(ref d, Osc(0.15f, 2400f, 900f, 0, 0.2f, 15f)); return d;

                // ------------- game feedback -------------
                case "hitmarker": return Osc(0.05f, 2200f, 2200f, 0, 0.4f, 40f);
                case "click": return Osc(0.04f, 1200f, 1200f, 0, 0.3f, 60f);
                case "buy": return Chime(new[] { 523f, 659f, 784f, 1046f }, 0.12f);
                case "deny": return Chime(new[] { 220f, 180f }, 0.15f);
                case "perk": return Chime(new[] { 392f, 494f, 587f, 784f, 988f }, 0.14f);
                case "revive": return Chime(new[] { 330f, 392f, 494f, 659f, 880f }, 0.1f);
                case "slurp":
                    d = NoiseBurst(1f, 2f, 0.4f, 900f, 100f, 0f, 47); Layer(ref d, Osc(0.9f, 300f, 200f, 0, 0.1f, 3f, 12f, 60f)); return d;
                case "round_start": return Chime(new[] { 110f, 104f, 98f, 82f }, 0.55f);
                case "round_end": return Chime(new[] { 98f, 123f, 147f, 196f }, 0.4f);
                case "groan": return Groan(1.2f, 85f);
                case "player_hurt": return Groan(0.35f, 160f);
                case "zombie_hit": return NoiseBurst(0.12f, 30f, 0.6f, 400f, 100f, 0.4f, 48);
                case "board": return NoiseBurst(0.25f, 14f, 0.8f, 300f, 90f, 0.7f, 49);
                case "down": return Osc(1.5f, 300f, 60f, 1, 0.4f, 2.5f);

                // ------------- cardboard mystery box (comedic) -------------
                case "box_open":
                    d = CardRip(0.5f, 50); Layer(ref d, NoiseBurst(0.25f, 14f, 0.8f, 200f, 90f, 0.8f, 51), 0.45f); Layer(ref d, Tinkle(0.4f, 7, 52), 0.5f); return d;
                case "box_rattle":
                    d = Tinkle(0.45f, 5, (int)(Time.realtimeSinceStartup * 100f) % 1000); Layer(ref d, NoiseBurst(0.08f, 30f, 0.35f, 300f, 70f, 0.3f, 53)); Layer(ref d, NoiseBurst(0.08f, 30f, 0.3f, 300f, 70f, 0.3f, 54), 0.2f); return d;
                case "box_squeak":
                    return Osc(0.22f, 1500f, 2500f, 0, 0.35f, 6f, 28f, 120f);
                case "box_reveal":
                    d = Chime(new[] { 659f, 880f, 1175f }, 0.09f); Layer(ref d, SlideWhistle(0.55f, 400f, 1800f), 0f, 0.45f); return d;
                case "box_close":
                    d = NoiseBurst(0.3f, 12f, 0.9f, 180f, 80f, 0.9f, 55); Layer(ref d, Tinkle(0.3f, 4, 56), 0.05f); return d;
                case "box_womp":
                    d = Osc(0.3f, 220f, 200f, 2, 0.3f, 3f);
                    Layer(ref d, Osc(0.3f, 196f, 176f, 2, 0.3f, 3f), 0.35f);
                    Layer(ref d, Osc(0.3f, 175f, 155f, 2, 0.3f, 3f), 0.7f);
                    Layer(ref d, Osc(0.8f, 156f, 90f, 2, 0.3f, 1.5f, 6f, 8f), 1.05f);
                    Layer(ref d, NoiseBurst(0.3f, 12f, 0.7f, 180f, 80f, 0.9f, 57), 1.9f); return d;

                // ------------- original perk jingles -------------
                case "jingle_Juggernog":
                    d = Melody(new[] { 62, 62, 65, 69, 67, 65, 62, 60, 62, 65, 69, 74 }, new[] { .5f, .5f, .5f, 1f, .5f, .5f, .5f, 1f, .5f, .5f, .5f, 2f }, 108f, 1, 0.22f);
                    Layer(ref d, Melody(new[] { 38, 38, 41, 36, 38, 38, 41, 34 }, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1.5f }, 108f, 4, 0.45f)); return d;
                case "jingle_DoubleTap":
                    d = Melody(new[] { 67, 71, 74, 72, 71, 67, 64, 67, 69, 67, 64, 62, 64, 67 }, new[] { .5f, .5f, 1f, .5f, .5f, 1f, .5f, .5f, 1f, .5f, .5f, .5f, .5f, 2f }, 132f, 3, 0.3f);
                    Layer(ref d, Melody(new[] { 43, 43, 50, 43, 43, 50, 41, 41 }, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 3f }, 132f, 3, 0.4f)); return d;
                case "jingle_QuickRevive":
                    d = Melody(new[] { 72, 76, 79, 76, 81, 79, 76, 72 }, new[] { .75f, .75f, .75f, .75f, .75f, .75f, .75f, 2f }, 84f, 0, 0.28f);
                    Layer(ref d, Melody(new[] { 48, 48, 53, 55 }, new[] { 2f, 2f, 2f, 1.25f }, 84f, 4, 0.25f)); return d;
                case "jingle_SpeedCola":
                    d = Melody(new[] { 72, 79, 76, 79, 72, 79, 77, 81, 79, 76, 72, 76, 79, 84 }, new[] { .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, 1.5f }, 168f, 2, 0.16f);
                    Layer(ref d, Melody(new[] { 48, 48, 55, 48, 48, 55, 53, 53 }, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, 168f, 4, 0.4f)); return d;
            }
            return null;
        }

        // ===================================================================== SYNTH BUILDING BLOCKS
        static float[] Buf(float seconds) { return new float[Mathf.Max(1, (int)(seconds * Rate))]; }

        static void Layer(ref float[] dst, float[] src, float offset = 0f, float gain = 1f)
        {
            int off = (int)(offset * Rate);
            int need = off + src.Length;
            if (need > dst.Length) System.Array.Resize(ref dst, need);
            for (int i = 0; i < src.Length; i++) dst[off + i] += src[i] * gain;
        }

        static float[] Echo(float[] d, float delay, float fb, float tail)
        {
            int dl = Mathf.Max(1, (int)(delay * Rate));
            int n = d.Length + (int)(tail * Rate);
            var o = new float[n];
            System.Array.Copy(d, o, d.Length);
            for (int i = dl; i < n; i++) o[i] += o[i - dl] * fb;
            return o;
        }

        // Low-passed noise burst with exponential decay and a sine "thump" (gunshots, explosions, impacts)
        static float[] NoiseBurst(float len, float decay, float amp, float lowpassHz, float thumpHz, float thump, int seed)
        {
            var d = Buf(len); var rng = new System.Random(seed);
            float a = Mathf.Clamp01(lowpassHz / Rate * TwoPi * 3f); float y = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float x = (float)(rng.NextDouble() * 2 - 1);
                y += a * (x - y);
                float env = Mathf.Exp(-t * decay);
                float th = Mathf.Sin(TwoPi * thumpHz * t) * Mathf.Exp(-t * decay * 1.5f) * thump;
                d[i] = (y * 2.5f + th) * env * amp;
            }
            return d;
        }

        // Sharp high-passed crack that gives gunshots their snap
        static float[] Crack(float len, float amp, int seed)
        {
            var d = Buf(len); var rng = new System.Random(seed); float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float x = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.15f * (x - lp);
                d[i] = (x - lp) * Mathf.Exp(-t * 90f) * amp;
            }
            return d;
        }

        // Oscillator with frequency glide, optional vibrato. wave: 0 sine, 1 square, 2 saw
        static float[] Osc(float len, float f0, float f1, int wave, float amp, float decay = 0f, float vibHz = 0f, float vibDepth = 0f)
        {
            var d = Buf(len); float phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate; float k = t / len;
                float f = Mathf.Lerp(f0, f1, k);
                if (vibHz > 0f) f += Mathf.Sin(TwoPi * vibHz * t) * vibDepth;
                phase += TwoPi * f / Rate;
                float s = wave == 0 ? Mathf.Sin(phase) : wave == 1 ? (Mathf.Sin(phase) > 0f ? 1f : -1f) * 0.6f : ((phase / Mathf.PI) % 2f) - 1f;
                float env = Mathf.Min(1f, t / 0.008f) * Mathf.Min(1f, (len - t) / 0.02f) * Mathf.Exp(-t * decay);
                d[i] = s * env * amp;
            }
            return d;
        }

        static float[] Zap(float len)
        {
            var d = Buf(len); var rng = new System.Random(3);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float crackle = rng.NextDouble() < 0.08 ? (float)(rng.NextDouble() * 2 - 1) : 0f;
                d[i] = (Mathf.Sin(TwoPi * 60f * t) * 0.3f + crackle) * Mathf.Exp(-t * 4f) * 0.8f;
            }
            return d;
        }

        static float[] Click(float freqMul, float amp)
        {
            var d = new float[1100];
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Sin(i * 0.9f * freqMul) * Mathf.Exp(-i / (150f / freqMul)) * amp;
            return d;
        }

        static float[] Chime(float[] notes, float noteLen)
        {
            int per = (int)(noteLen * Rate);
            var d = new float[per * notes.Length + Rate / 2];
            for (int k = 0; k < notes.Length; k++)
                for (int i = 0; i < per + Rate / 2 && k * per + i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    d[k * per + i] += (Mathf.Sin(TwoPi * notes[k] * t) + 0.3f * Mathf.Sin(2f * TwoPi * notes[k] * t)) * 0.25f * Mathf.Exp(-t * 5f);
                }
            return d;
        }

        static float[] Groan(float len, float baseF)
        {
            var d = Buf(len); var rng = new System.Random(baseF.GetHashCode() + 11); float phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = baseF + Mathf.Sin(t * 6f) * 15f;
                phase += TwoPi * f / Rate;
                float saw = ((phase / Mathf.PI) % 2f) - 1f;
                float env = Mathf.Sin(Mathf.PI * t / len);
                d[i] = (saw * 0.5f + (float)(rng.NextDouble() * 2 - 1) * 0.15f) * env * 0.5f;
            }
            return d;
        }

        // Glass clinking inside a box
        static float[] Tinkle(float len, int count, int seed)
        {
            var d = Buf(len); var rng = new System.Random(seed);
            for (int k = 0; k < count; k++)
            {
                int start = (int)(rng.NextDouble() * len * 0.75f * Rate);
                float f = 2400f + (float)rng.NextDouble() * 4200f; float a = 0.3f * (0.4f + (float)rng.NextDouble() * 0.6f);
                for (int i = 0; start + i < d.Length && i < Rate / 4; i++)
                {
                    float t = (float)i / Rate;
                    d[start + i] += (Mathf.Sin(TwoPi * f * t) + 0.4f * Mathf.Sin(TwoPi * f * 2.76f * t)) * Mathf.Exp(-t * 30f) * a;
                }
            }
            return d;
        }

        // Cardboard flaps ripping open: crackly high-passed noise swell
        static float[] CardRip(float len, int seed)
        {
            var d = Buf(len); var rng = new System.Random(seed); float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float x = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.3f * (x - lp);
                float band = x - lp;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len)) * (0.6f + 0.4f * (float)rng.NextDouble());
                float pop = rng.NextDouble() < 0.004 ? (float)(rng.NextDouble() * 2 - 1) * 0.8f : 0f;
                d[i] = band * env * 0.5f + pop * env;
            }
            return d;
        }

        static float[] SlideWhistle(float len, float f0, float f1)
        {
            return Osc(len, f0, f1, 0, 0.5f, 1.5f, 7f, 25f);
        }

        // ---------------- little tune synth (original jingles) ----------------
        static float[] Melody(int[] notes, float[] beats, float bpm, int inst, float gain)
        {
            float spb = 60f / bpm; float total = 0f;
            for (int i = 0; i < beats.Length; i++) total += beats[i];
            var d = new float[(int)((total * spb + 0.8f) * Rate)];
            float t0 = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float dur = beats[k] * spb;
                if (notes[k] > 0)
                {
                    float f = 440f * Mathf.Pow(2f, (notes[k] - 69) / 12f);
                    int n = (int)((dur + 0.5f) * Rate); int start = (int)(t0 * Rate);
                    for (int i = 0; i < n && start + i < d.Length; i++)
                    {
                        float t = (float)i / Rate;
                        d[start + i] += Voice(f, t, inst) * NoteEnv(t, dur, inst) * gain;
                    }
                }
                t0 += dur;
            }
            return d;
        }

        static float Voice(float f, float t, int inst)
        {
            float ph = TwoPi * f * t;
            switch (inst)
            {
                case 0: return Mathf.Sin(ph) + 0.35f * Mathf.Sin(2f * ph) + 0.15f * Mathf.Sin(3.01f * ph);               // bell
                case 1: return (Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.33f * Mathf.Sin(3f * ph) + 0.25f * Mathf.Sin(4f * ph)) * 0.55f; // saw-ish lead
                case 2: return Mathf.Sin(ph) > 0f ? 0.5f : -0.5f;                                                          // chip square
                case 3: return Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) * Mathf.Exp(-t * 6f) + 0.25f * Mathf.Sin(3f * ph) * Mathf.Exp(-t * 9f); // pluck
                default: return Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph);                                                 // bass
            }
        }

        static float NoteEnv(float t, float dur, int inst)
        {
            float atk = Mathf.Min(1f, t / 0.01f);
            switch (inst)
            {
                case 0: return atk * Mathf.Exp(-t * 3.5f);
                case 3: return atk * Mathf.Exp(-t * 5f);
                default: return atk * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 12f));
            }
        }
    }
}
