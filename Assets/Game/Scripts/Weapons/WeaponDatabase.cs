using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
    public enum WeaponClass { Pistol, SMG, AssaultRifle, Shotgun, LMG, Sniper, Launcher, Wonder }
    public enum FireMode { Semi, Auto, Burst }
    public enum WeaponSpecial { None, Explosive, Thunder, Chain, Bolt, Rail }

    public class WeaponDef
    {
        public int Id;
        public string Name;
        public WeaponClass Class;
        public int Damage;
        public float HeadMult = 2f;
        public float RPM;
        public FireMode Mode;
        public int BurstCount = 3;
        public int Mag;
        public int Reserve;
        public float ReloadTime;
        public int Pellets = 1;
        public float HipSpread;
        public float AdsSpread;
        public float AdsFov;
        public float Range = 120f;
        public WeaponSpecial Special;
        public float SplashRadius;
        public int SplashDamage;
        public float ProjectileSpeed = 50f;
        public float BoxWeight = 1f;
        public float MoveMult = 1f;
        public float Recoil;
        public string Sound;
        public float SoundPitch = 1f;

        public float FireInterval => 60f / Mathf.Max(1f, RPM);
    }

    /// <summary>All 40 weapons. Stats roughly follow classic CoD Zombies values.</summary>
    public static class WeaponDatabase
    {
        static List<WeaponDef> all;
        public static List<WeaponDef> All { get { if (all == null) Build(); return all; } }
        public static WeaponDef Get(string name) { foreach (var w in All) if (w.Name == name) return w; return null; }
        public const string StartingWeapon = "M1911";

        static WeaponDef W(string name, WeaponClass c, int dmg, float rpm, FireMode mode, int mag, int reserve, float reload)
        {
            var d = new WeaponDef { Name = name, Class = c, Damage = dmg, RPM = rpm, Mode = mode, Mag = mag, Reserve = reserve, ReloadTime = reload };
            switch (c)
            {
                case WeaponClass.Pistol: d.HipSpread = 2.5f; d.AdsSpread = 0.3f; d.AdsFov = 60; d.MoveMult = 1f; d.Recoil = 1.6f; d.Sound = "shot_pistol"; d.HeadMult = 3f; break;
                case WeaponClass.SMG: d.HipSpread = 3.5f; d.AdsSpread = 0.8f; d.AdsFov = 58; d.MoveMult = 1f; d.Recoil = 0.8f; d.Sound = "shot_smg"; d.HeadMult = 2.5f; break;
                case WeaponClass.AssaultRifle: d.HipSpread = 4f; d.AdsSpread = 0.3f; d.AdsFov = 50; d.MoveMult = 0.95f; d.Recoil = 1.0f; d.Sound = "shot_rifle"; d.HeadMult = 2.5f; break;
                case WeaponClass.Shotgun: d.HipSpread = 7f; d.AdsSpread = 5f; d.AdsFov = 60; d.MoveMult = 0.95f; d.Recoil = 4f; d.Sound = "shot_shotgun"; d.Pellets = 8; d.HeadMult = 1.5f; d.Range = 25f; break;
                case WeaponClass.LMG: d.HipSpread = 6f; d.AdsSpread = 1f; d.AdsFov = 50; d.MoveMult = 0.85f; d.Recoil = 0.9f; d.Sound = "shot_lmg"; d.HeadMult = 2f; break;
                case WeaponClass.Sniper: d.HipSpread = 8f; d.AdsSpread = 0f; d.AdsFov = 22; d.MoveMult = 0.9f; d.Recoil = 5f; d.Sound = "shot_sniper"; d.HeadMult = 5f; break;
                case WeaponClass.Launcher: d.HipSpread = 2f; d.AdsSpread = 0f; d.AdsFov = 50; d.MoveMult = 0.85f; d.Recoil = 4f; d.Sound = "shot_launcher"; d.HeadMult = 1f; d.BoxWeight = 0.7f; break;
                case WeaponClass.Wonder: d.HipSpread = 1.5f; d.AdsSpread = 0.2f; d.AdsFov = 60; d.MoveMult = 1f; d.Recoil = 2f; d.Sound = "shot_ray"; d.HeadMult = 1f; d.BoxWeight = 0.35f; break;
            }
            return d;
        }

        static void Build()
        {
            all = new List<WeaponDef>
            {
                // ---------- Pistols (6) ----------
                W("M1911",        WeaponClass.Pistol, 20,  625, FireMode.Semi, 8,  80,  1.6f),
                W("Python",       WeaponClass.Pistol, 100, 300, FireMode.Semi, 6,  60,  2.8f),
                W("Desert Eagle", WeaponClass.Pistol, 120, 280, FireMode.Semi, 7,  49,  2.2f),
                W("CZ75",         WeaponClass.Pistol, 150, 600, FireMode.Semi, 15, 120, 1.8f),
                W("Five-Seven",   WeaponClass.Pistol, 80,  650, FireMode.Semi, 20, 120, 1.7f),
                W("Glock 18",     WeaponClass.Pistol, 40,  1100,FireMode.Auto, 20, 140, 1.6f),

                // ---------- SMGs (7) ----------
                W("MP40",         WeaponClass.SMG, 70,  545,  FireMode.Auto, 32, 192, 2.4f),
                W("MP5K",         WeaponClass.SMG, 70,  800,  FireMode.Auto, 30, 120, 2.0f),
                W("AK-74u",       WeaponClass.SMG, 100, 750,  FireMode.Auto, 20, 160, 2.3f),
                W("Thompson",     WeaponClass.SMG, 100, 720,  FireMode.Auto, 20, 200, 2.4f),
                W("PPSh-41",      WeaponClass.SMG, 70,  1000, FireMode.Auto, 71, 355, 3.4f),
                W("MP7",          WeaponClass.SMG, 80,  950,  FireMode.Auto, 40, 200, 2.0f),
                W("Uzi",          WeaponClass.SMG, 75,  750,  FireMode.Auto, 32, 224, 2.4f),

                // ---------- Assault Rifles (8) ----------
                W("M14",          WeaponClass.AssaultRifle, 105, 500, FireMode.Semi,  8,  96,  2.0f),
                W("M16",          WeaponClass.AssaultRifle, 100, 900, FireMode.Burst, 30, 120, 2.2f),
                W("Galil",        WeaponClass.AssaultRifle, 100, 680, FireMode.Auto,  35, 315, 2.5f),
                W("AK-47",        WeaponClass.AssaultRifle, 110, 600, FireMode.Auto,  30, 180, 2.5f),
                W("FAMAS",        WeaponClass.AssaultRifle, 100, 900, FireMode.Auto,  30, 150, 2.3f),
                W("Commando",     WeaponClass.AssaultRifle, 120, 750, FireMode.Auto,  30, 270, 2.3f),
                W("STG-44",       WeaponClass.AssaultRifle, 100, 550, FireMode.Auto,  30, 180, 2.4f),
                W("AUG",          WeaponClass.AssaultRifle, 120, 700, FireMode.Auto,  30, 270, 2.6f),

                // ---------- Shotguns (5) — damage is per pellet ----------
                W("Olympia",      WeaponClass.Shotgun, 40, 300, FireMode.Semi, 2, 38, 2.5f),
                W("SPAS-12",      WeaponClass.Shotgun, 50, 160, FireMode.Semi, 8, 32, 3.0f),
                W("Remington 870",WeaponClass.Shotgun, 60, 90,  FireMode.Semi, 6, 36, 3.2f),
                W("HS-10",        WeaponClass.Shotgun, 50, 280, FireMode.Semi, 6, 36, 2.8f),
                W("Stakeout",     WeaponClass.Shotgun, 45, 120, FireMode.Semi, 6, 54, 3.0f),

                // ---------- LMGs (4) ----------
                W("RPK",          WeaponClass.LMG, 120, 600,  FireMode.Auto, 100, 400, 4.5f),
                W("HK21",         WeaponClass.LMG, 130, 750,  FireMode.Auto, 125, 500, 4.8f),
                W("MG42",         WeaponClass.LMG, 120, 1200, FireMode.Auto, 125, 500, 5.0f),
                W("M60",          WeaponClass.LMG, 140, 550,  FireMode.Auto, 100, 400, 5.2f),

                // ---------- Snipers (4) ----------
                W("Kar98k",       WeaponClass.Sniper, 500,  50,  FireMode.Semi, 5,  50, 3.0f),
                W("L96A1",        WeaponClass.Sniper, 500,  40,  FireMode.Semi, 5,  50, 3.4f),
                W("Barrett M82",  WeaponClass.Sniper, 1000, 150, FireMode.Semi, 10, 50, 3.8f),
                W("Dragunov",     WeaponClass.Sniper, 300,  300, FireMode.Semi, 10, 60, 3.0f),

                // ---------- Launchers (2) ----------
                W("M72 LAW",      WeaponClass.Launcher, 1500, 60, FireMode.Semi, 1, 20, 2.5f),
                W("China Lake",   WeaponClass.Launcher, 1200, 70, FireMode.Semi, 2, 15, 3.2f),

                // ---------- Wonder Weapons (4) ----------
                W("Ray Gun",      WeaponClass.Wonder, 1000, 180, FireMode.Semi,  20, 160, 2.6f),
                W("Ray Gun Mk II",WeaponClass.Wonder, 600,  900, FireMode.Burst, 21, 168, 2.4f),
                W("Thundergun",   WeaponClass.Wonder, 99999, 60, FireMode.Semi,  2,  12,  3.5f),
                W("Wunderwaffe DG-2", WeaponClass.Wonder, 99999, 60, FireMode.Semi, 3, 9, 4.0f),

                // ---------- Original wonder weapons ----------
                W("Nightshade",   WeaponClass.Wonder, 650, 700, FireMode.Burst, 24, 192, 2.4f),   // 3-round plasma burst rifle
                W("Railgun",      WeaponClass.Wonder, 99999999, 40, FireMode.Semi, 4, 24, 3.4f), // infinite damage, pierces everything in a line
            };

            // Special behaviours
            var law = Get("M72 LAW"); law.Special = WeaponSpecial.Explosive; law.SplashRadius = 5f; law.SplashDamage = 1500; law.ProjectileSpeed = 40f;
            var cl = Get("China Lake"); cl.Special = WeaponSpecial.Explosive; cl.SplashRadius = 4.5f; cl.SplashDamage = 1200; cl.ProjectileSpeed = 30f;
            var rg = Get("Ray Gun"); rg.Special = WeaponSpecial.Explosive; rg.SplashRadius = 2.5f; rg.SplashDamage = 300; rg.ProjectileSpeed = 45f; rg.BoxWeight = 0.6f;
            var rg2 = Get("Ray Gun Mk II"); rg2.Special = WeaponSpecial.Bolt; rg2.ProjectileSpeed = 80f; rg2.Recoil = 1f;
            var tg = Get("Thundergun"); tg.Special = WeaponSpecial.Thunder; tg.Range = 14f; tg.Sound = "shot_thunder"; tg.Recoil = 8f;
            var ww = Get("Wunderwaffe DG-2"); ww.Special = WeaponSpecial.Chain; ww.Range = 60f; ww.Sound = "shot_zap"; ww.Recoil = 5f;
            Get("Olympia").Mode = FireMode.Semi;

            var ns = Get("Nightshade"); ns.Special = WeaponSpecial.Bolt; ns.BurstCount = 3; ns.ProjectileSpeed = 75f; ns.Sound = "shot_plasma"; ns.BoxWeight = 0.4f; ns.Recoil = 1.2f; ns.Range = 150f;
            var rail = Get("Railgun"); rail.Special = WeaponSpecial.Rail; rail.Range = 250f; rail.Sound = "shot_rail"; rail.Recoil = 6f; rail.BoxWeight = 0.25f; rail.MoveMult = 0.9f; rail.AdsFov = 45f;

            for (int i = 0; i < all.Count; i++) { all[i].Id = i; all[i].SoundPitch = 0.9f + ((i * 37) % 23) * 0.011f; }
            Get("Ray Gun").SoundPitch = 1f; Get("Ray Gun Mk II").SoundPitch = 1.25f; Get("Nightshade").SoundPitch = 1f;
            Get("Railgun").SoundPitch = 1f; Get("Thundergun").SoundPitch = 1f; Get("Wunderwaffe DG-2").SoundPitch = 1f;
        }

        /// <summary>Weighted random pick for the mystery box, excluding weapons the player already holds.</summary>
        public static WeaponDef RandomBoxWeapon(ICollection<string> exclude)
        {
            float total = 0;
            foreach (var w in All) if (w.Name != StartingWeapon && !exclude.Contains(w.Name)) total += w.BoxWeight;
            float r = Random.value * total;
            foreach (var w in All)
            {
                if (w.Name == StartingWeapon || exclude.Contains(w.Name)) continue;
                r -= w.BoxWeight;
                if (r <= 0) return w;
            }
            return All[1];
        }
    }
}
