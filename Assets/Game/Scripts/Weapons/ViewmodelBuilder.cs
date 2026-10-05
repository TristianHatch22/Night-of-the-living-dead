using UnityEngine;

namespace NOTLD
{
    public class ViewmodelInfo
    {
        public GameObject Root;
        public Transform Muzzle;
        public Transform Magazine;
        public float SightY;
    }

    /// <summary>
    /// Builds a first-person viewmodel for every weapon out of primitives.
    /// Each class has its own silhouette and each weapon gets unique proportions/details.
    /// (Swap in imported 3D models later by replacing Build().)
    /// </summary>
    public static class ViewmodelBuilder
    {
        static Material dark, metal, wood, poly, accent, glow, glass, sleeve, skin;

        static void InitMats()
        {
            dark = Mats.Get("vm_dark", Mats.Gray(0.07f), 0.55f, 0.6f);
            metal = Mats.Get("vm_metal", Mats.Gray(0.22f), 0.7f, 0.8f);
            wood = Mats.Get("vm_wood", Mats.Gray(0.33f), 0.25f);
            poly = Mats.Get("vm_poly", Mats.Gray(0.13f), 0.2f);
            accent = Mats.Get("vm_accent", Mats.Gray(0.55f), 0.6f, 0.7f);
            glow = Mats.Get("vm_glow", Mats.Gray(1f), 0.9f, 0f, new Color(2.5f, 2.5f, 2.5f));
            glass = Mats.Get("vm_glass", new Color(0.8f, 0.8f, 0.8f, 0.35f), 0.95f, 0f, new Color(0.6f, 0.6f, 0.6f), true);
            sleeve = Mats.Get("vm_sleeve", Mats.Gray(0.2f), 0.1f);
            skin = Mats.Get("vm_skin", Mats.Gray(0.6f), 0.3f);
        }

        static Transform root;
        static GameObject C(Vector3 p, Vector3 s, Material m, float rx = 0, float ry = 0, float rz = 0) => Mats.Prim(PrimitiveType.Cube, root, p, s, m, new Vector3(rx, ry, rz));
        // Cylinder pointing forward (+Z). d = diameter, len = length
        static GameObject Cyl(Vector3 p, float d, float len, Material m) => Mats.Prim(PrimitiveType.Cylinder, root, p, new Vector3(d, len / 2f, d), m, new Vector3(90, 0, 0));
        static GameObject Sph(Vector3 p, Vector3 s, Material m) => Mats.Prim(PrimitiveType.Sphere, root, p, s, m);

        public static ViewmodelInfo Build(WeaponDef def, Transform parent, bool withArms = true)
        {
            InitMats();
            var go = new GameObject("VM_" + def.Name);
            go.transform.SetParent(parent, false);
            root = go.transform;
            var info = new ViewmodelInfo { Root = go };
            float v = 0.9f + (def.Id * 37 % 20) / 100f; // per-weapon proportion variation
            Vector3 muzzle = Vector3.zero;
            GameObject mag = null;

            switch (def.Class)
            {
                case WeaponClass.Pistol: muzzle = Pistol(def, v, out mag); info.SightY = 0.055f; break;
                case WeaponClass.SMG: muzzle = Smg(def, v, out mag); info.SightY = 0.06f; break;
                case WeaponClass.AssaultRifle: muzzle = Rifle(def, v, out mag); info.SightY = 0.075f; break;
                case WeaponClass.Shotgun: muzzle = Shotgun(def, v, out mag); info.SightY = 0.05f; break;
                case WeaponClass.LMG: muzzle = Lmg(def, v, out mag); info.SightY = 0.09f; break;
                case WeaponClass.Sniper: muzzle = Sniper(def, v, out mag); info.SightY = 0.105f; break;
                case WeaponClass.Launcher: muzzle = Launcher(def, v, out mag); info.SightY = 0.08f; break;
                case WeaponClass.Wonder: muzzle = Wonder(def, v, out mag); info.SightY = 0.08f; break;
            }

            if (withArms) Arms(def);

            var mz = new GameObject("Muzzle").transform;
            mz.SetParent(root, false);
            mz.localPosition = muzzle;
            info.Muzzle = mz;
            if (mag != null) info.Magazine = mag.transform;
            SetLayerNoShadows(go);
            return info;
        }

        static void SetLayerNoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void Arms(WeaponDef def)
        {
            bool twoHand = def.Class != WeaponClass.Pistol;
            // right hand on grip
            C(new Vector3(0.0f, -0.075f, -0.03f), new Vector3(0.06f, 0.07f, 0.08f), skin, -15);
            C(new Vector3(0.03f, -0.13f, -0.2f), new Vector3(0.08f, 0.08f, 0.32f), sleeve, 25, -8);
            // left hand
            if (twoHand)
            {
                C(new Vector3(-0.005f, -0.05f, 0.2f), new Vector3(0.065f, 0.05f, 0.09f), skin);
                C(new Vector3(-0.09f, -0.12f, 0.02f), new Vector3(0.08f, 0.08f, 0.36f), sleeve, 18, 22);
            }
            else
            {
                C(new Vector3(-0.025f, -0.085f, -0.02f), new Vector3(0.05f, 0.06f, 0.07f), skin, -15, 10);
                C(new Vector3(-0.1f, -0.15f, -0.2f), new Vector3(0.08f, 0.08f, 0.32f), sleeve, 25, 20);
            }
        }

        // ---------------- PISTOLS ----------------
        static Vector3 Pistol(WeaponDef d, float v, out GameObject mag)
        {
            Material body = d.Name == "Glock 18" || d.Name == "Five-Seven" ? poly : metal;
            float len = 0.19f * v;
            if (d.Name == "Python")
            {
                Cyl(new Vector3(0, 0.03f, 0.03f), 0.055f, 0.045f, dark);            // cylinder
                Cyl(new Vector3(0, 0.035f, 0.13f), 0.022f, 0.17f, metal);          // barrel
                C(new Vector3(0, 0.05f, 0.13f), new Vector3(0.012f, 0.012f, 0.17f), metal); // rib
                C(new Vector3(0, 0.02f, -0.02f), new Vector3(0.03f, 0.04f, 0.06f), metal);
                C(new Vector3(0, -0.04f, -0.04f), new Vector3(0.03f, 0.1f, 0.045f), wood, -18);
                mag = null;
                return new Vector3(0, 0.035f, 0.22f);
            }
            float h = d.Name == "Desert Eagle" ? 0.05f : 0.038f;
            C(new Vector3(0, 0.03f, 0.04f), new Vector3(0.032f, h, len), body);                 // slide
            C(new Vector3(0, 0.0f, 0.03f), new Vector3(0.03f, 0.025f, len * 0.85f), dark);      // frame
            C(new Vector3(0, 0.03f + h / 2f, -0.04f), new Vector3(0.012f, 0.01f, 0.01f), dark); // rear sight
            C(new Vector3(0, 0.03f + h / 2f, 0.04f + len / 2f - 0.01f), new Vector3(0.006f, 0.01f, 0.006f), accent); // front sight
            var g = C(new Vector3(0, -0.055f, -0.02f), new Vector3(0.03f, 0.1f, 0.045f), d.Name == "M1911" ? wood : poly, -15);
            C(new Vector3(0, -0.02f, 0.03f), new Vector3(0.006f, 0.025f, 0.03f), dark);          // trigger guard
            mag = C(new Vector3(0, -0.1f, -0.03f), new Vector3(0.025f, 0.02f, 0.035f), dark, -15);
            if (d.Name == "Glock 18") C(new Vector3(0, -0.12f, -0.035f), new Vector3(0.025f, 0.06f, 0.035f), dark, -15); // extended mag
            return new Vector3(0, 0.03f, 0.04f + len / 2f);
        }

        // ---------------- SMGs ----------------
        static Vector3 Smg(WeaponDef d, float v, out GameObject mag)
        {
            float len = 0.3f * v;
            bool woody = d.Name == "Thompson" || d.Name == "PPSh-41";
            C(new Vector3(0, 0.02f, 0.06f), new Vector3(0.05f, 0.065f, len), d.Name == "MP7" ? poly : metal);
            float bl = d.Name == "MP5K" || d.Name == "MP7" ? 0.06f : 0.14f;
            Cyl(new Vector3(0, 0.03f, 0.06f + len / 2f + bl / 2f), 0.022f, bl, dark);
            if (d.Name == "PPSh-41") C(new Vector3(0, 0.03f, 0.06f + len / 2f + 0.05f), new Vector3(0.04f, 0.04f, 0.14f), dark); // barrel shroud
            C(new Vector3(0, 0.058f, 0.0f), new Vector3(0.01f, 0.02f, 0.02f), dark); // rear sight
            C(new Vector3(0, 0.055f, 0.06f + len / 2f - 0.01f), new Vector3(0.006f, 0.02f, 0.006f), accent);
            C(new Vector3(0, -0.05f, -0.03f), new Vector3(0.035f, 0.09f, 0.045f), woody ? wood : poly, -12); // grip
            if (d.Name == "PPSh-41" || d.Name == "Thompson")
            {
                var drum = Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -0.06f, 0.1f), new Vector3(0.12f, 0.025f, 0.12f), dark, new Vector3(0, 0, 90));
                mag = drum;
                C(new Vector3(0, -0.005f, -0.17f), new Vector3(0.04f, 0.07f, 0.18f), wood, 6); // wood stock
                C(new Vector3(0, -0.02f, 0.2f), new Vector3(0.04f, 0.04f, 0.1f), wood);         // fore grip
            }
            else
            {
                float ml = d.Name == "MP40" || d.Name == "Uzi" ? 0.16f : 0.12f;
                float mz = d.Name == "Uzi" ? -0.03f : 0.11f;
                mag = C(new Vector3(0, -0.04f - ml / 2f, mz), new Vector3(0.025f, ml, 0.035f), dark, d.Name == "AK-74u" ? 15 : 0);
                if (d.Name != "MP5K" && d.Name != "Uzi") C(new Vector3(0, 0.0f, -0.15f), new Vector3(0.015f, 0.05f, 0.18f), dark); // folding stock
                if (d.Name == "AK-74u") C(new Vector3(0, 0.02f, 0.17f), new Vector3(0.052f, 0.05f, 0.1f), wood);
            }
            return new Vector3(0, 0.03f, 0.06f + len / 2f + bl);
        }

        // ---------------- ASSAULT RIFLES ----------------
        static Vector3 Rifle(WeaponDef d, float v, out GameObject mag)
        {
            float len = 0.42f * v;
            bool woody = d.Name == "AK-47" || d.Name == "M14" || d.Name == "Galil" && false || d.Name == "STG-44";
            bool bullpup = d.Name == "FAMAS" || d.Name == "AUG";
            Material body = d.Name == "AUG" ? poly : metal;
            C(new Vector3(0, 0.02f, 0.08f), new Vector3(0.05f, 0.075f, len), body);
            float bl = 0.2f * v;
            Cyl(new Vector3(0, 0.035f, 0.08f + len / 2f + bl / 2f), 0.02f, bl, dark);
            C(new Vector3(0, 0.03f, 0.08f + len / 2f - 0.05f), new Vector3(0.058f, 0.06f, 0.14f), woody ? wood : poly); // handguard
            C(new Vector3(0, -0.055f, bullpup ? 0.05f : -0.03f), new Vector3(0.035f, 0.09f, 0.045f), poly, -12);        // grip
            if (!bullpup) C(new Vector3(0, 0.0f, 0.08f - len / 2f - 0.1f), new Vector3(0.045f, 0.08f, 0.2f), woody ? wood : poly, 4); // stock

            float magZ = bullpup ? -0.08f : 0.07f;
            float rot = d.Name == "AK-47" || d.Name == "Galil" ? 18f : 8f;
            if (d.Name == "M14") mag = C(new Vector3(0, -0.05f, magZ), new Vector3(0.028f, 0.07f, 0.06f), dark);
            else mag = C(new Vector3(0, -0.08f, magZ), new Vector3(0.026f, 0.14f, 0.05f), dark, rot);

            if (d.Name == "M16" || d.Name == "Commando")
            {
                C(new Vector3(0, 0.075f, 0.02f), new Vector3(0.012f, 0.03f, 0.14f), dark); // carry handle
                C(new Vector3(0, 0.085f, 0.02f), new Vector3(0.03f, 0.008f, 0.14f), dark);
                C(new Vector3(0, 0.07f, 0.08f + len / 2f + 0.03f), new Vector3(0.008f, 0.05f, 0.01f), dark);
            }
            else if (d.Name == "AUG")
            {
                Cyl(new Vector3(0, 0.095f, 0.06f), 0.04f, 0.16f, dark);            // integrated scope
                C(new Vector3(0, 0.07f, 0.06f), new Vector3(0.015f, 0.03f, 0.1f), poly);
                C(new Vector3(0, -0.05f, 0.18f), new Vector3(0.03f, 0.08f, 0.035f), poly, 10); // fore grip
            }
            else if (d.Name == "FAMAS")
            {
                C(new Vector3(0, 0.085f, 0.06f), new Vector3(0.012f, 0.035f, 0.3f), dark); // long handle
            }
            else
            {
                C(new Vector3(0, 0.065f, 0.0f), new Vector3(0.012f, 0.02f, 0.02f), dark);
                C(new Vector3(0, 0.06f, 0.08f + len / 2f + bl - 0.02f), new Vector3(0.006f, 0.03f, 0.006f), accent);
            }
            return new Vector3(0, 0.035f, 0.08f + len / 2f + bl);
        }

        // ---------------- SHOTGUNS ----------------
        static Vector3 Shotgun(WeaponDef d, float v, out GameObject mag)
        {
            float bl = (d.Name == "Stakeout" || d.Name == "HS-10" ? 0.38f : 0.55f) * v;
            C(new Vector3(0, 0.015f, 0.03f), new Vector3(0.05f, 0.07f, 0.2f), metal);
            if (d.Name == "Olympia")
            {
                Cyl(new Vector3(-0.014f, 0.035f, 0.13f + bl / 2f), 0.028f, bl, dark);
                Cyl(new Vector3(0.014f, 0.035f, 0.13f + bl / 2f), 0.028f, bl, dark);
                C(new Vector3(0, 0.0f, 0.25f), new Vector3(0.05f, 0.035f, 0.22f), wood);
            }
            else
            {
                Cyl(new Vector3(0, 0.035f, 0.13f + bl / 2f), 0.03f, bl, dark);
                Cyl(new Vector3(0, 0.0f, 0.13f + bl / 2f - 0.03f), 0.026f, bl - 0.06f, metal); // tube mag
                C(new Vector3(0, 0.0f, 0.25f), new Vector3(0.05f, 0.045f, 0.13f), d.Name == "SPAS-12" || d.Name == "HS-10" ? poly : wood); // pump
            }
            C(new Vector3(0, -0.055f, -0.03f), new Vector3(0.035f, 0.09f, 0.045f), d.Name == "Stakeout" || d.Name == "HS-10" ? poly : wood, -12);
            if (d.Name != "Stakeout" && d.Name != "HS-10") C(new Vector3(0, -0.01f, -0.2f), new Vector3(0.045f, 0.085f, 0.24f), d.Name == "SPAS-12" ? poly : wood, 6);
            C(new Vector3(0, 0.055f, 0.13f + bl - 0.02f), new Vector3(0.012f, 0.012f, 0.012f), accent); // bead sight
            mag = null;
            return new Vector3(0, 0.035f, 0.13f + bl);
        }

        // ---------------- LMGs ----------------
        static Vector3 Lmg(WeaponDef d, float v, out GameObject mag)
        {
            float len = 0.5f * v;
            C(new Vector3(0, 0.025f, 0.08f), new Vector3(0.065f, 0.09f, len), metal);
            float bl = 0.32f * v;
            if (d.Name == "MG42")
            {
                C(new Vector3(0, 0.035f, 0.08f + len / 2f + bl / 2f), new Vector3(0.05f, 0.05f, bl), dark); // perforated jacket
                for (int i = 0; i < 4; i++) C(new Vector3(0.026f, 0.035f, 0.08f + len / 2f + 0.04f + i * 0.07f), new Vector3(0.005f, 0.025f, 0.035f), metal);
            }
            else Cyl(new Vector3(0, 0.035f, 0.08f + len / 2f + bl / 2f), 0.028f, bl, dark);
            C(new Vector3(0, 0.03f, 0.08f + len / 2f - 0.06f), new Vector3(0.07f, 0.07f, 0.16f), d.Name == "RPK" ? wood : poly);
            C(new Vector3(0, -0.06f, -0.04f), new Vector3(0.035f, 0.1f, 0.045f), poly, -12);
            C(new Vector3(0, 0.0f, 0.08f - len / 2f - 0.1f), new Vector3(0.05f, 0.09f, 0.2f), d.Name == "RPK" ? wood : poly, 4);
            if (d.Name == "RPK") mag = C(new Vector3(0, -0.1f, 0.1f), new Vector3(0.03f, 0.18f, 0.06f), dark, 20);
            else mag = C(new Vector3(-0.07f, -0.04f, 0.08f), new Vector3(0.08f, 0.1f, 0.12f), dark); // box/belt
            // bipod folded
            C(new Vector3(0.015f, -0.01f, 0.08f + len / 2f + bl * 0.6f), new Vector3(0.008f, 0.008f, 0.16f), dark);
            C(new Vector3(-0.015f, -0.01f, 0.08f + len / 2f + bl * 0.6f), new Vector3(0.008f, 0.008f, 0.16f), dark);
            C(new Vector3(0, 0.085f, 0.05f), new Vector3(0.012f, 0.025f, 0.02f), dark);
            return new Vector3(0, 0.035f, 0.08f + len / 2f + bl);
        }

        // ---------------- SNIPERS ----------------
        static Vector3 Sniper(WeaponDef d, float v, out GameObject mag)
        {
            float len = 0.36f * v;
            bool woody = d.Name == "Kar98k" || d.Name == "Dragunov";
            C(new Vector3(0, 0.02f, 0.06f), new Vector3(0.05f, 0.07f, len), d.Name == "Barrett M82" ? metal : (woody ? wood : poly));
            float bl = (d.Name == "Barrett M82" ? 0.42f : 0.38f) * v;
            Cyl(new Vector3(0, 0.035f, 0.06f + len / 2f + bl / 2f), d.Name == "Barrett M82" ? 0.03f : 0.02f, bl, dark);
            if (d.Name == "Barrett M82") C(new Vector3(0, 0.035f, 0.06f + len / 2f + bl), new Vector3(0.06f, 0.035f, 0.05f), dark); // muzzle brake
            // scope
            Cyl(new Vector3(0, 0.1f, 0.05f), 0.045f, 0.26f, dark);
            Cyl(new Vector3(0, 0.1f, 0.19f), 0.055f, 0.04f, dark);
            Cyl(new Vector3(0, 0.1f, -0.09f), 0.05f, 0.03f, dark);
            C(new Vector3(0, 0.07f, 0.05f), new Vector3(0.015f, 0.03f, 0.1f), dark);
            C(new Vector3(0, -0.055f, -0.04f), new Vector3(0.035f, 0.09f, 0.045f), woody ? wood : poly, -12);
            C(new Vector3(0, -0.005f, 0.06f - len / 2f - 0.12f), new Vector3(0.045f, 0.09f, 0.24f), woody ? wood : poly, 5);
            if (d.Name == "Kar98k" || d.Name == "L96A1")
            {
                C(new Vector3(0.04f, 0.04f, 0.0f), new Vector3(0.04f, 0.01f, 0.01f), metal); // bolt handle
                Sph(new Vector3(0.06f, 0.04f, 0.0f), Vector3.one * 0.018f, metal);
                mag = null;
            }
            else mag = C(new Vector3(0, -0.07f, 0.1f), new Vector3(0.03f, 0.1f, 0.06f), dark, 10);
            return new Vector3(0, 0.035f, 0.06f + len / 2f + bl);
        }

        // ---------------- LAUNCHERS ----------------
        static Vector3 Launcher(WeaponDef d, float v, out GameObject mag)
        {
            if (d.Name == "M72 LAW")
            {
                Cyl(new Vector3(0, 0.04f, 0.1f), 0.075f, 0.75f, poly);
                Cyl(new Vector3(0, 0.04f, 0.47f), 0.085f, 0.04f, dark);
                Cyl(new Vector3(0, 0.04f, -0.27f), 0.085f, 0.04f, dark);
                C(new Vector3(0, 0.1f, 0.1f), new Vector3(0.012f, 0.04f, 0.012f), dark);
                C(new Vector3(0, -0.04f, 0.0f), new Vector3(0.03f, 0.07f, 0.06f), dark);
                mag = null;
                return new Vector3(0, 0.04f, 0.5f);
            }
            C(new Vector3(0, 0.02f, 0.05f), new Vector3(0.06f, 0.08f, 0.22f), metal);
            Cyl(new Vector3(0, 0.04f, 0.33f), 0.05f, 0.4f, dark);
            Cyl(new Vector3(0, -0.015f, 0.3f), 0.04f, 0.34f, metal);
            C(new Vector3(0, -0.015f, 0.3f), new Vector3(0.06f, 0.05f, 0.12f), wood);
            C(new Vector3(0, -0.06f, -0.03f), new Vector3(0.035f, 0.1f, 0.045f), poly, -12);
            C(new Vector3(0, 0.0f, -0.17f), new Vector3(0.04f, 0.08f, 0.22f), dark, 5);
            C(new Vector3(0, 0.1f, 0.2f), new Vector3(0.04f, 0.04f, 0.01f), dark); // ladder sight
            mag = null;
            return new Vector3(0, 0.04f, 0.53f);
        }

        // ---------------- WONDER WEAPONS ----------------
        static Vector3 Wonder(WeaponDef d, float v, out GameObject mag)
        {
            mag = null;
            switch (d.Name)
            {
                case "Ray Gun":
                    Sph(new Vector3(0, 0.03f, 0.03f), new Vector3(0.07f, 0.09f, 0.16f), accent);
                    Cyl(new Vector3(0, 0.035f, 0.14f), 0.03f, 0.14f, metal);
                    for (int i = 0; i < 3; i++) Cyl(new Vector3(0, 0.035f, 0.09f + i * 0.04f), 0.07f - i * 0.008f, 0.008f, dark);
                    Cyl(new Vector3(0, 0.07f, 0.02f), 0.018f, 0.1f, glow);                 // energy tube
                    C(new Vector3(0, -0.055f, -0.03f), new Vector3(0.03f, 0.09f, 0.04f), dark, -15);
                    Sph(new Vector3(0, 0.035f, 0.21f), Vector3.one * 0.03f, glow);
                    return new Vector3(0, 0.035f, 0.22f);
                case "Ray Gun Mk II":
                    C(new Vector3(0, 0.025f, 0.05f), new Vector3(0.06f, 0.08f, 0.26f), accent);
                    for (int i = -1; i <= 1; i++) Cyl(new Vector3(i * 0.02f, 0.04f, 0.24f), 0.016f, 0.14f, metal);
                    C(new Vector3(0, 0.075f, 0.05f), new Vector3(0.02f, 0.02f, 0.2f), glow);
                    C(new Vector3(0.033f, 0.02f, 0.05f), new Vector3(0.005f, 0.04f, 0.18f), glow);
                    C(new Vector3(0, -0.055f, -0.03f), new Vector3(0.035f, 0.09f, 0.045f), dark, -12);
                    C(new Vector3(0, 0.0f, -0.14f), new Vector3(0.04f, 0.06f, 0.12f), dark);
                    return new Vector3(0, 0.04f, 0.32f);
                case "Thundergun":
                    Cyl(new Vector3(0, 0.04f, 0.15f), 0.16f, 0.36f, metal);
                    for (int i = 0; i < 5; i++) Cyl(new Vector3(0, 0.04f, 0.0f + i * 0.07f), 0.19f, 0.012f, dark);
                    Cyl(new Vector3(0, 0.04f, 0.36f), 0.2f, 0.05f, dark);
                    Cyl(new Vector3(0, 0.04f, 0.37f), 0.1f, 0.05f, glow);
                    C(new Vector3(0, -0.08f, -0.03f), new Vector3(0.035f, 0.1f, 0.045f), wood, -12);
                    C(new Vector3(0, -0.02f, -0.18f), new Vector3(0.05f, 0.08f, 0.2f), wood, 5);
                    return new Vector3(0, 0.04f, 0.4f);
                case "Nightshade":
                    C(new Vector3(0, 0.02f, 0.07f), new Vector3(0.05f, 0.07f, 0.42f), poly);
                    C(new Vector3(0, 0.062f, 0.05f), new Vector3(0.028f, 0.014f, 0.3f), glow);
                    C(new Vector3(0.027f, 0.02f, 0.07f), new Vector3(0.004f, 0.03f, 0.3f), glow);
                    C(new Vector3(-0.027f, 0.02f, 0.07f), new Vector3(0.004f, 0.03f, 0.3f), glow);
                    Cyl(new Vector3(0, 0.05f, 0.36f), 0.018f, 0.2f, metal);
                    Cyl(new Vector3(-0.022f, 0.016f, 0.36f), 0.018f, 0.2f, metal);
                    Cyl(new Vector3(0.022f, 0.016f, 0.36f), 0.018f, 0.2f, metal);
                    for (int i = 0; i < 3; i++) Cyl(new Vector3(0, 0.03f, 0.3f + i * 0.05f), 0.075f - i * 0.012f, 0.01f, dark);
                    Sph(new Vector3(0, 0.03f, 0.47f), Vector3.one * 0.03f, glow);
                    C(new Vector3(0, 0.085f, -0.1f), new Vector3(0.01f, 0.05f, 0.14f), accent, -8);
                    C(new Vector3(0, 0.0f, -0.17f), new Vector3(0.04f, 0.075f, 0.2f), poly, 4);
                    C(new Vector3(0, -0.055f, -0.02f), new Vector3(0.032f, 0.09f, 0.045f), dark, -12);
                    mag = C(new Vector3(0, -0.06f, 0.1f), new Vector3(0.03f, 0.1f, 0.07f), glow);
                    return new Vector3(0, 0.03f, 0.49f);
                case "Railgun":
                    C(new Vector3(0, 0.02f, 0.0f), new Vector3(0.09f, 0.1f, 0.34f), metal);
                    C(new Vector3(-0.035f, 0.05f, 0.42f), new Vector3(0.018f, 0.03f, 0.64f), accent);
                    C(new Vector3(0.035f, 0.05f, 0.42f), new Vector3(0.018f, 0.03f, 0.64f), accent);
                    C(new Vector3(0, 0.05f, 0.42f), new Vector3(0.05f, 0.01f, 0.64f), glow);
                    for (int i = 0; i < 5; i++) Cyl(new Vector3(0, 0.05f, 0.2f + i * 0.1f), 0.12f, 0.014f, dark);
                    C(new Vector3(-0.065f, 0.03f, 0.0f), new Vector3(0.03f, 0.08f, 0.22f), glow);
                    mag = C(new Vector3(0.065f, 0.03f, 0.0f), new Vector3(0.03f, 0.08f, 0.22f), glow);
                    C(new Vector3(0, 0.1f, 0.0f), new Vector3(0.012f, 0.03f, 0.12f), dark);
                    C(new Vector3(0, -0.06f, -0.06f), new Vector3(0.035f, 0.1f, 0.05f), dark, -12);
                    C(new Vector3(0, 0.0f, -0.24f), new Vector3(0.05f, 0.09f, 0.22f), poly, 4);
                    return new Vector3(0, 0.05f, 0.76f);
                default: // Wunderwaffe DG-2
                    C(new Vector3(0, 0.02f, 0.06f), new Vector3(0.06f, 0.08f, 0.38f), metal);
                    Cyl(new Vector3(0, 0.035f, 0.3f), 0.035f, 0.14f, dark);
                    for (int i = 0; i < 3; i++)
                    {
                        Sph(new Vector3(0, 0.1f, -0.04f + i * 0.08f), Vector3.one * 0.06f, glass);
                        Sph(new Vector3(0, 0.1f, -0.04f + i * 0.08f), Vector3.one * 0.025f, glow);
                        Cyl(new Vector3(0, 0.065f, -0.04f + i * 0.08f), 0.03f, 0.015f, dark);
                    }
                    for (int i = 0; i < 4; i++) Cyl(new Vector3(0, 0.035f, 0.33f + i * 0.02f), 0.06f, 0.006f, accent);
                    C(new Vector3(0, -0.055f, -0.04f), new Vector3(0.035f, 0.09f, 0.045f), wood, -12);
                    C(new Vector3(0, 0.0f, -0.2f), new Vector3(0.045f, 0.085f, 0.2f), wood, 5);
                    return new Vector3(0, 0.035f, 0.38f);
            }
        }
    }
}
