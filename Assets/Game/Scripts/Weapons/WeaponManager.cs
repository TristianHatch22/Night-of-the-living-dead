using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
    public class WeaponSlot
    {
        public WeaponDef Def;
        public int Mag, Reserve;
        public ViewmodelInfo VM;
    }

    /// <summary>Inventory (2 weapons), shooting, ADS, reload, knife, viewmodel animation, wonder weapon effects.</summary>
    public class WeaponManager : MonoBehaviour
    {
        public readonly List<WeaponSlot> Slots = new List<WeaponSlot>();
        public int Current;
        public WeaponSlot Cur => Slots.Count > 0 ? Slots[Current] : null;
        public bool IsAiming { get; private set; }
        public float AdsT { get; private set; }
        public float CurrentSpread { get; private set; }
        public bool ScopedIn => Cur != null && Cur.Def.Class == WeaponClass.Sniper && AdsT > 0.95f;
        public bool IsReloading => reloadT >= 0;
        public float MoveMultiplier => Cur != null ? Cur.Def.MoveMult : 1f;
        public float AdsSensitivity => Cur == null ? 1f : Mathf.Clamp(TargetAdsFov / GameSettings.FieldOfView, 0.25f, 1f);
        float TargetAdsFov => Cur == null ? GameSettings.FieldOfView : Mathf.Min(GameSettings.FieldOfView, Cur.Def.AdsFov * GameSettings.FieldOfView / 75f);

        PlayerController player;
        Transform vmRoot, cupRoot;
        float nextFire, reloadT = -1f, reloadDur, switchT = -1f, meleeT = -1f, kick, bobT, cycleAt = -1f;
        int burstLeft, pendingSwitch = -1;
        bool meleeHitDone, drinking, reloadSecondPlayed;
        string cycleSound;
        Vector3 swayPos, vmPos; Quaternion vmRot = Quaternion.identity;

        static readonly Vector3 HipPos = new Vector3(0.14f, -0.13f, 0.28f);
        static readonly Vector3 SprintPos = new Vector3(0.12f, -0.22f, 0.24f);

        void Start()
        {
            player = GetComponent<PlayerController>();
            vmRoot = new GameObject("ViewmodelRoot").transform;
            vmRoot.SetParent(player.Cam.transform, false);
            vmRoot.localPosition = HipPos;
            BuildCup();
            GiveWeapon(WeaponDatabase.Get(WeaponDatabase.StartingWeapon));
        }

        void BuildCup()
        {
            cupRoot = new GameObject("SlushyCup").transform;
            cupRoot.SetParent(player.Cam.transform, false);
            cupRoot.localPosition = new Vector3(0.05f, -0.12f, 0.28f);
            var glass = Mats.Get("cup_plastic", new Color(0.9f, 0.9f, 0.9f, 0.45f), 0.9f, 0, null, true);
            var slush = Mats.Get("cup_slush", Mats.Gray(0.8f), 0.6f, 0, new Color(0.4f, 0.4f, 0.4f));
            var lid = Mats.Get("cup_lid", Mats.Gray(0.95f), 0.4f);
            var straw = Mats.Get("cup_straw", Mats.Gray(0.2f), 0.4f);
            Mats.Prim(PrimitiveType.Cylinder, cupRoot, new Vector3(0, 0, 0), new Vector3(0.08f, 0.08f, 0.08f), glass);
            Mats.Prim(PrimitiveType.Cylinder, cupRoot, new Vector3(0, -0.01f, 0), new Vector3(0.07f, 0.065f, 0.07f), slush);
            Mats.Prim(PrimitiveType.Sphere, cupRoot, new Vector3(0, 0.085f, 0), new Vector3(0.085f, 0.03f, 0.085f), lid);
            Mats.Prim(PrimitiveType.Cylinder, cupRoot, new Vector3(0, 0.13f, -0.01f), new Vector3(0.008f, 0.06f, 0.008f), straw, new Vector3(-12, 0, 0));
            foreach (var r in cupRoot.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cupRoot.gameObject.SetActive(false);
        }

        public HashSet<string> OwnedNames()
        {
            var h = new HashSet<string>();
            foreach (var s in Slots) h.Add(s.Def.Name);
            return h;
        }

        public WeaponSlot FindSlot(WeaponDef def)
        {
            foreach (var s in Slots) if (s.Def == def) return s;
            return null;
        }

        public void RefillAmmo(WeaponSlot slot)
        {
            slot.Mag = slot.Def.Mag;
            slot.Reserve = slot.Def.Reserve;
        }

        public void GiveWeapon(WeaponDef def)
        {
            if (def == null) return;
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].Def == def) { RefillAmmo(Slots[i]); SwitchTo(i); return; }

            var slot = new WeaponSlot { Def = def, Mag = def.Mag, Reserve = def.Reserve };
            slot.VM = ViewmodelBuilder.Build(def, vmRoot);
            slot.VM.Root.SetActive(false);
            if (Slots.Count < 2)
            {
                Slots.Add(slot);
                SwitchTo(Slots.Count - 1, true);
            }
            else
            {
                Destroy(Slots[Current].VM.Root);
                Slots[Current] = slot;
                SwitchTo(Current, true);
            }
        }

        void SwitchTo(int i, bool force = false)
        {
            if (i < 0 || i >= Slots.Count) return;
            if (i == Current && !force) return;
            reloadT = -1f; burstLeft = 0; cycleAt = -1f;
            pendingSwitch = i;
            switchT = 0f;
        }

        void ApplyVisibleSlot()
        {
            for (int k = 0; k < Slots.Count; k++) Slots[k].VM.Root.SetActive(k == Current && !drinking);
        }

        public void ShowDrink(bool on)
        {
            drinking = on;
            cupRoot.gameObject.SetActive(on);
            reloadT = -1f;
            ApplyVisibleSlot();
        }

        // ---------------- sound helpers ----------------
        static string ReloadStartSound(WeaponDef d)
        {
            switch (d.Class)
            {
                case WeaponClass.Shotgun: return "reload_shell";
                case WeaponClass.Wonder: return "reload_energy";
                case WeaponClass.Launcher: return "reload_launcher";
                default: return "reload_mag";
            }
        }

        static string ReloadEndSound(WeaponDef d)
        {
            switch (d.Class)
            {
                case WeaponClass.Shotgun: return d.Name == "Olympia" ? "reload_click" : "pump";
                case WeaponClass.Sniper: return (d.Name == "Kar98k" || d.Name == "L96A1") ? "bolt" : "reload_click";
                case WeaponClass.Wonder: return "reload_chirp";
                case WeaponClass.Launcher: return "reload_clank";
                default: return "reload_click";
            }
        }

        static void RumbleFor(WeaponDef d)
        {
            if (d.Special == WeaponSpecial.Rail) { GameInput.Rumble(1f, 1f, 0.3f); return; }
            switch (d.Class)
            {
                case WeaponClass.Pistol: GameInput.Rumble(0.15f, 0.25f, 0.06f); break;
                case WeaponClass.SMG: GameInput.Rumble(0.15f, 0.2f, 0.05f); break;
                case WeaponClass.AssaultRifle: GameInput.Rumble(0.3f, 0.35f, 0.07f); break;
                case WeaponClass.Shotgun: GameInput.Rumble(0.8f, 0.6f, 0.15f); break;
                case WeaponClass.LMG: GameInput.Rumble(0.3f, 0.4f, 0.06f); break;
                case WeaponClass.Sniper: GameInput.Rumble(0.9f, 0.7f, 0.2f); break;
                case WeaponClass.Launcher: GameInput.Rumble(1f, 0.8f, 0.25f); break;
                default: GameInput.Rumble(0.5f, 0.7f, 0.12f); break;
            }
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm != null && (gm.Paused || gm.GameOver)) return;
            if (Cur == null) return;
            var def = Cur.Def;
            bool busy = drinking || player.IsDown;

            // pump / bolt cycle sound after a shot
            if (cycleAt > 0 && Time.time >= cycleAt) { SFX.Play(cycleSound, 0.5f); cycleAt = -1f; }

            // ---- switching ----
            if (switchT >= 0)
            {
                switchT += Time.deltaTime;
                if (pendingSwitch >= 0 && switchT >= 0.22f) { Current = pendingSwitch; pendingSwitch = -1; ApplyVisibleSlot(); SFX.Play("draw", 0.5f); }
                if (switchT >= 0.45f) switchT = -1f;
            }
            if (!busy)
            {
                int digit = GameInput.WeaponDigit();
                if (digit >= 0) SwitchTo(digit);
                if ((GameInput.WeaponSwapPressed() || Mathf.Abs(GameInput.Scroll()) > 0.1f) && Slots.Count > 1) SwitchTo((Current + 1) % Slots.Count);
            }

            // ---- ADS ----
            IsAiming = GameInput.AimHeld() && !busy && switchT < 0 && meleeT < 0 && !player.IsSprinting;
            AdsT = Mathf.MoveTowards(AdsT, IsAiming && !IsReloading ? 1f : 0f, Time.deltaTime * 7f);
            float baseFov = GameSettings.FieldOfView;
            player.Cam.fieldOfView = Mathf.Lerp(baseFov, TargetAdsFov, AdsT) + (player.IsSprinting ? 4f : 0f);

            // ---- spread ----
            float moveBloom = player.MoveAmount * (def.Class == WeaponClass.Sniper ? 3f : 1.2f);
            CurrentSpread = Mathf.Lerp(def.HipSpread + moveBloom, def.AdsSpread + moveBloom * 0.25f, AdsT);

            // ---- reload ----
            if (IsReloading)
            {
                reloadT += Time.deltaTime;
                if (!reloadSecondPlayed && reloadT / reloadDur > 0.6f)
                {
                    reloadSecondPlayed = true;
                    SFX.Play(ReloadEndSound(def), 0.6f, player.HasPerk(Perk.SpeedCola) ? 1.3f : 1f);
                }
                if (reloadT >= reloadDur)
                {
                    int need = def.Mag - Cur.Mag;
                    int take = Mathf.Min(need, Cur.Reserve);
                    Cur.Mag += take; Cur.Reserve -= take;
                    reloadT = -1f;
                }
            }
            else if (!busy && GameInput.ReloadPressed()) StartReload();

            // ---- melee ----
            if (meleeT >= 0)
            {
                meleeT += Time.deltaTime;
                if (!meleeHitDone && meleeT > 0.12f) { meleeHitDone = true; DoMelee(); }
                if (meleeT > 0.55f) meleeT = -1f;
            }
            else if (!busy && GameInput.MeleePressed())
            {
                meleeT = 0; meleeHitDone = false; reloadT = -1f; burstLeft = 0;
                SFX.Play("knife", 0.7f);
            }

            // ---- fire ----
            bool canFire = !busy && !IsReloading && switchT < 0 && meleeT < 0 && !player.IsSprinting;
            float interval = def.FireInterval / (player.HasPerk(Perk.DoubleTap) ? 1.33f : 1f);
            if (canFire && Time.time >= nextFire)
            {
                if (burstLeft > 0)
                {
                    FireOnce(); burstLeft--;
                    nextFire = Time.time + (burstLeft > 0 ? interval : interval + 0.18f);
                }
                else
                {
                    bool trigger = def.Mode == FireMode.Auto ? GameInput.FireHeld() : GameInput.FirePressed();
                    if (trigger)
                    {
                        if (Cur.Mag <= 0)
                        {
                            SFX.Play("empty", 0.6f);
                            nextFire = Time.time + 0.25f;
                            if (Cur.Reserve > 0) StartReload();
                        }
                        else if (def.Mode == FireMode.Burst)
                        {
                            burstLeft = def.BurstCount - 1;
                            FireOnce();
                            nextFire = Time.time + interval;
                        }
                        else
                        {
                            FireOnce();
                            nextFire = Time.time + interval;
                        }
                    }
                }
            }

            AnimateViewmodel(def);
        }

        void StartReload()
        {
            if (Cur.Mag >= Cur.Def.Mag || Cur.Reserve <= 0 || IsReloading) return;
            reloadDur = Cur.Def.ReloadTime * (player.HasPerk(Perk.SpeedCola) ? 0.5f : 1f);
            reloadT = 0f;
            reloadSecondPlayed = false;
            burstLeft = 0;
            SFX.Play(ReloadStartSound(Cur.Def), 0.7f, player.HasPerk(Perk.SpeedCola) ? 1.3f : 1f);
        }

        // ---------------- shooting ----------------
        void FireOnce()
        {
            if (Cur.Mag <= 0) { burstLeft = 0; return; }
            var def = Cur.Def;
            Cur.Mag--;
            var cam = player.Cam.transform;
            SFX.Play(def.Sound, 0.6f, def.SoundPitch * Random.Range(0.97f, 1.03f));
            RumbleFor(def);
            Effects.MuzzleFlash(Cur.VM.Muzzle, def);
            if (NetSession.IsOnline) NetGame.ShotFx(def, Cur.VM.Muzzle.position, AimPoint(cam.position, cam.forward, def.Range), def.Class != WeaponClass.Launcher);
            float rec = def.Recoil * (IsAiming ? 0.6f : 1f);
            player.AddRecoil(rec, Random.Range(-rec, rec) * 0.35f);
            kick = Mathf.Min(kick + 0.03f + rec * 0.006f, 0.09f);
            int dtMul = player.HasPerk(Perk.DoubleTap) ? 2 : 1;

            // pump / bolt cycle sound
            cycleAt = -1f;
            if (def.Class == WeaponClass.Shotgun && def.Name != "Olympia") { cycleSound = "pump"; cycleAt = Time.time + 0.28f; }
            else if (def.Class == WeaponClass.Sniper && (def.Name == "Kar98k" || def.Name == "L96A1")) { cycleSound = "bolt"; cycleAt = Time.time + 0.35f; }

            switch (def.Special)
            {
                case WeaponSpecial.Explosive:
                case WeaponSpecial.Bolt:
                {
                    Vector3 aim = AimPoint(cam.position, cam.forward, def.Range);
                    Vector3 from = Cur.VM.Muzzle.position;
                    Projectile.Spawn(from, (aim - from).normalized, def, dtMul);
                    return;
                }
                case WeaponSpecial.Thunder: Thunder(cam, def); return;
                case WeaponSpecial.Chain: Chain(cam, def); return;
                case WeaponSpecial.Rail: Rail(cam, def); return;
            }

            for (int p = 0; p < def.Pellets; p++)
            {
                Vector3 dir = SpreadDir(cam, CurrentSpread);
                if (Physics.Raycast(cam.position, dir, out var hit, def.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    var z = hit.collider.GetComponentInParent<Zombie>();
                    if (z != null && !z.IsDead)
                    {
                        bool head = hit.collider == z.HeadCollider;
                        float falloff = def.Class == WeaponClass.Shotgun ? Mathf.Lerp(1f, 0.3f, hit.distance / def.Range) : 1f;
                        int dmg = Mathf.RoundToInt(def.Damage * (head ? def.HeadMult : 1f) * falloff * dtMul);
                        z.TakeDamage(dmg, head, DamageKind.Bullet, hit.point);
                        HUD.I?.Hitmarker(z.IsDead);
                    }
                    else Effects.Impact(hit.point, hit.normal);
                    if (p == 0 && Random.value < 0.5f) Effects.Tracer(Cur.VM.Muzzle.position, hit.point);
                }
            }
        }

        static Vector3 SpreadDir(Transform cam, float spreadDeg)
        {
            Vector2 r = Random.insideUnitCircle * spreadDeg;
            return Quaternion.AngleAxis(r.x, cam.up) * Quaternion.AngleAxis(r.y, cam.right) * cam.forward;
        }

        static Vector3 AimPoint(Vector3 o, Vector3 d, float range)
        {
            if (Physics.Raycast(o, d, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return hit.point;
            return o + d * range;
        }

        /// <summary>Railgun: instantly kills every zombie along a perfectly straight line; only solid walls stop it.</summary>
        void Rail(Transform cam, WeaponDef def)
        {
            Vector3 o = cam.position, dir = cam.forward;
            var hits = Physics.RaycastAll(o, dir, def.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float end = def.Range;
            bool killedAny = false;
            var seen = new HashSet<Zombie>();
            foreach (var h in hits)
            {
                var z = h.collider.GetComponentInParent<Zombie>();
                if (z != null)
                {
                    if (z.IsDead || !seen.Add(z)) continue;
                    bool head = h.collider == z.HeadCollider;
                    z.TakeDamage(def.Damage, head, DamageKind.Bullet, h.point);
                    killedAny = true;
                    continue; // pierce straight through
                }
                if (h.collider.isTrigger) continue;
                end = h.distance;
                Effects.Impact(h.point, h.normal);
                break;
            }
            Effects.RailBeam(Cur.VM.Muzzle.position, o + dir * end);
            NetGame.RailFx(Cur.VM.Muzzle.position, o + dir * end);
            if (killedAny) HUD.I?.Hitmarker(true);
        }

        void Thunder(Transform cam, WeaponDef def)
        {
            Effects.ThunderBlast(Cur.VM.Muzzle, def.Range);
            var list = new List<Zombie>(GameManager.I.Alive);
            foreach (var z in list)
            {
                if (z == null || z.IsDead) continue;
                Vector3 to = z.transform.position + Vector3.up - cam.position;
                if (to.magnitude > def.Range) continue;
                if (Vector3.Dot(cam.forward, to.normalized) < 0.7f) continue;
                Vector3 push = to.normalized * Random.Range(14f, 20f) + Vector3.up * Random.Range(5f, 9f);
                z.Launch(push);
            }
        }

        void Chain(Transform cam, WeaponDef def)
        {
            Zombie first = null;
            if (Physics.Raycast(cam.position, cam.forward, out var hit, def.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                first = hit.collider.GetComponentInParent<Zombie>();
            if (first == null || first.IsDead)
            {
                float best = 0.93f;
                foreach (var z in GameManager.I.Alive)
                {
                    if (z == null || z.IsDead) continue;
                    Vector3 to = z.transform.position + Vector3.up - cam.position;
                    if (to.magnitude > def.Range) continue;
                    float d = Vector3.Dot(cam.forward, to.normalized);
                    if (d > best) { best = d; first = z; }
                }
            }
            Vector3 from = Cur.VM.Muzzle.position;
            if (first == null)
            {
                Effects.Lightning(from, AimPoint(cam.position, cam.forward, 30f));
                return;
            }
            var hitSet = new HashSet<Zombie>();
            var curZ = first;
            for (int jumps = 0; jumps < 10 && curZ != null; jumps++)
            {
                Vector3 p = curZ.transform.position + Vector3.up * 1.2f;
                Effects.Lightning(from, p, 0.35f);
                hitSet.Add(curZ);
                curZ.TakeDamage(999999, false, DamageKind.Wonder, p);
                from = p;
                Zombie next = null; float nd = 7f;
                foreach (var z in GameManager.I.Alive)
                {
                    if (z == null || z.IsDead || hitSet.Contains(z)) continue;
                    float d = Vector3.Distance(z.transform.position, curZ.transform.position);
                    if (d < nd) { nd = d; next = z; }
                }
                curZ = next;
            }
        }

        void DoMelee()
        {
            var cam = player.Cam.transform;
            Zombie best = null; float bd = 2.1f;
            foreach (var z in GameManager.I.Alive)
            {
                if (z == null || z.IsDead) continue;
                Vector3 to = z.transform.position + Vector3.up - cam.position;
                float d = new Vector3(to.x, 0, to.z).magnitude;
                if (d > bd || Vector3.Dot(cam.forward, to.normalized) < 0.4f) continue;
                bd = d; best = z;
            }
            if (best != null)
            {
                best.TakeDamage(150, false, DamageKind.Melee, best.transform.position + Vector3.up * 1.2f);
                HUD.I?.Hitmarker(best.IsDead);
            }
        }

        // ---------------- viewmodel animation ----------------
        void AnimateViewmodel(WeaponDef def)
        {
            float dt = Time.deltaTime;
            Vector3 adsPos = new Vector3(0, -Cur.VM.SightY, 0.2f);
            Vector3 target = Vector3.Lerp(HipPos, adsPos, AdsT);
            Quaternion rot = Quaternion.identity;

            // bob
            float moveAmt = player.MoveAmount;
            bobT += dt * (player.IsSprinting ? 13f : 9f) * Mathf.Clamp01(moveAmt + 0.05f);
            float bobScale = Mathf.Lerp(1f, 0.15f, AdsT) * moveAmt;
            target += new Vector3(Mathf.Cos(bobT) * 0.012f, Mathf.Abs(Mathf.Sin(bobT)) * 0.012f, 0) * bobScale;
            // idle breathing
            target += new Vector3(0, Mathf.Sin(Time.time * 1.6f) * 0.002f, 0) * (1 - AdsT);

            // sway
            Vector2 ld = player.LookDelta;
            swayPos = Vector3.Lerp(swayPos, new Vector3(-ld.x, -ld.y, 0) * 0.0025f * (1 - AdsT * 0.8f), dt * 8f);
            swayPos = Vector3.ClampMagnitude(swayPos, 0.04f);
            target += swayPos;
            rot *= Quaternion.Euler(swayPos.y * 300f, swayPos.x * -300f, swayPos.x * 200f);

            if (player.IsSprinting && meleeT < 0)
            {
                target = SprintPos + new Vector3(Mathf.Cos(bobT) * 0.03f, Mathf.Abs(Mathf.Sin(bobT)) * 0.025f, 0);
                rot *= Quaternion.Euler(8f, -38f, 12f);
            }

            if (IsReloading)
            {
                float k = Mathf.Sin(Mathf.Clamp01(reloadT / reloadDur) * Mathf.PI);
                target += new Vector3(0, -0.08f, -0.02f) * k;
                rot *= Quaternion.Euler(25f * k, 0, 35f * k);
                if (Cur.VM.Magazine) Cur.VM.Magazine.gameObject.SetActive(!(reloadT / reloadDur > 0.25f && reloadT / reloadDur < 0.6f));
            }
            else if (Cur.VM.Magazine && !Cur.VM.Magazine.gameObject.activeSelf) Cur.VM.Magazine.gameObject.SetActive(true);

            if (switchT >= 0)
            {
                float k = switchT < 0.22f ? switchT / 0.22f : 1f - (switchT - 0.22f) / 0.23f;
                target += new Vector3(0, -0.3f, 0) * Mathf.Clamp01(k);
                rot *= Quaternion.Euler(40f * Mathf.Clamp01(k), 0, 0);
            }

            if (meleeT >= 0)
            {
                float k = Mathf.Sin(Mathf.Clamp01(meleeT / 0.4f) * Mathf.PI);
                target += new Vector3(-0.1f, 0.03f, 0.18f) * k;
                rot *= Quaternion.Euler(-10f * k, -30f * k, -60f * k);
            }

            if (drinking) target += new Vector3(0, -0.4f, 0);

            // recoil kick
            kick = Mathf.Lerp(kick, 0, dt * 14f);
            target += new Vector3(0, kick * 0.15f, -kick);
            rot *= Quaternion.Euler(-kick * 120f, 0, 0);

            vmPos = Vector3.Lerp(vmPos == Vector3.zero ? target : vmPos, target, dt * 18f);
            vmRot = Quaternion.Slerp(vmRot, rot, dt * 14f);
            vmRoot.localPosition = vmPos;
            vmRoot.localRotation = vmRot;

            // sniper scope hides the gun
            Cur.VM.Root.SetActive(!drinking && !ScopedIn);

            if (drinking)
            {
                cupRoot.localRotation = Quaternion.Slerp(cupRoot.localRotation, Quaternion.Euler(-50f, 0, 0), dt * 4f);
                cupRoot.localPosition = Vector3.Lerp(cupRoot.localPosition, new Vector3(0.0f, -0.06f, 0.2f), dt * 4f);
            }
            else
            {
                cupRoot.localRotation = Quaternion.identity;
                cupRoot.localPosition = new Vector3(0.05f, -0.2f, 0.28f);
            }
        }
    }

    // ===================================================================== PROJECTILES
    public class Projectile : MonoBehaviour
    {
        WeaponDef def; Vector3 vel; float life; int mul;

        public static void Spawn(Vector3 pos, Vector3 dir, WeaponDef def, int dtMul)
        {
            var go = new GameObject("Proj_" + def.Name);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(dir);
            var p = go.AddComponent<Projectile>();
            p.def = def; p.vel = dir * def.ProjectileSpeed; p.mul = dtMul;
            bool energy = def.Class == WeaponClass.Wonder;
            var m = energy ? Mats.Get("fx_energy", Color.white, 0.5f, 0, new Color(5, 5, 5)) : Mats.Get("vm_dark", Mats.Gray(0.07f), 0.55f, 0.6f);
            Vector3 size = energy ? (def.Name == "Nightshade" ? new Vector3(0.06f, 0.06f, 0.55f) : new Vector3(0.1f, 0.1f, 0.3f)) : new Vector3(0.06f, 0.12f, 0.06f);
            var vis = Mats.Prim(energy ? PrimitiveType.Sphere : PrimitiveType.Cylinder, go.transform, Vector3.zero, size, m, energy ? Vector3.zero : new Vector3(90, 0, 0));
            vis.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (energy)
            {
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.range = 4f; l.intensity = 3f; l.shadows = LightShadows.None;
            }
            else
            {
                var trail = go.AddComponent<TrailRenderer>();
                trail.material = Mats.Get("fx_smoke", Mats.Gray(0.5f), 0.1f, 0, null, true);
                trail.time = 0.4f; trail.startWidth = 0.12f; trail.endWidth = 0.4f;
            }
        }

        void Update()
        {
            if (GameManager.I != null && GameManager.I.Paused) return;
            life += Time.deltaTime;
            Vector3 step = vel * Time.deltaTime;
            if (Physics.Raycast(transform.position, vel.normalized, out var hit, step.magnitude + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                Hit(hit);
                return;
            }
            transform.position += step;
            if (life > 4f) Destroy(gameObject);
        }

        void Hit(RaycastHit hit)
        {
            bool energy = def.Class == WeaponClass.Wonder;
            var z = hit.collider.GetComponentInParent<Zombie>();
            if (z != null && !z.IsDead)
            {
                bool head = hit.collider == z.HeadCollider;
                z.TakeDamage(def.Damage * mul, head, energy ? DamageKind.Bullet : DamageKind.Explosive, hit.point);
                HUD.I?.Hitmarker(z.IsDead);
            }
            if (def.Special == WeaponSpecial.Explosive) GameManager.Explode(hit.point + hit.normal * 0.2f, def.SplashRadius, def.SplashDamage * mul, energy ? "plasma_boom" : "explosion");
            else if (energy) { Effects.PlasmaBurst(hit.point, 0.6f); SFX.PlayAt("plasma_hit", hit.point, 0.8f, Random.Range(0.9f, 1.15f)); }
            else Effects.Impact(hit.point, hit.normal);
            Destroy(gameObject);
        }
    }
}
