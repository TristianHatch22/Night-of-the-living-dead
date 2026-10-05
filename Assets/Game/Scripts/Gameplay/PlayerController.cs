using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IZombieTarget
    {
        public static PlayerController I;

        public const float BaseHealth = 149.99f;   // 3 zombie hits (50 each) to go down
        public const float JugHealth = 299.99f;    // 6 zombie hits with Juggernog
        public const float ZombieHitDamage = 50f;

        public Camera Cam;
        public WeaponManager Weapons;
        public readonly List<Perk> Perks = new List<Perk>();
        public int SelfRevivesUsed;
        public bool IsDown { get; private set; }
        /// <summary>Co-op: bled out - can no longer be revived.</summary>
        public bool IsDead { get; private set; }
        /// <summary>Down with no Quick Revive: only a teammate can bring this player back.</summary>
        public bool IsDoomed => IsDown && !selfReviving;
        public float Pitch => pitch;
        bool selfReviving;

        // zombies treat the local player (solo / host) as a target through this interface
        Vector3 IZombieTarget.Position => transform.position;
        bool IZombieTarget.Down => IsDown;
        bool IZombieTarget.Blind => ZombiesBlind;
        void IZombieTarget.Hit(float dmg, Vector3 from) { TakeDamage(dmg, from); }
        public bool IsDrinking { get; private set; }
        public bool IsSprinting { get; private set; }
        public float Health = BaseHealth;
        public float MaxHealth => HasPerk(Perk.Juggernog) ? JugHealth : BaseHealth;
        public float HurtAmount { get; private set; }
        public Interactable Focus { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public float MoveAmount { get; private set; }

        // Quick Revive protection
        public float InvulnUntil, BlindUntil;
        public bool IsInvulnerable => Time.time < InvulnUntil;
        /// <summary>Zombies ignore the player (no chasing, no attacking) while true.</summary>
        public bool ZombiesBlind => Time.time < BlindUntil;

        CharacterController cc;
        Transform camPivot;
        float pitch, yaw, vy, lastHurt = -10f, camHeight = 1.62f, recoilPitch, recoilYaw;
        const float WalkSpeed = 4.2f, SprintSpeed = 6.6f, Gravity = 20f, JumpSpeed = 6f;

        void Awake()
        {
            I = this;
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.35f;
            gameObject.layer = 2; // Ignore Raycast so our own bullets never hit us

            camPivot = new GameObject("CamPivot").transform;
            camPivot.SetParent(transform, false);
            camPivot.localPosition = new Vector3(0, camHeight, 0);

            if (Cam == null) Cam = Camera.main;
            if (Cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                Cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            Cam.transform.SetParent(camPivot, false);
            Cam.transform.localPosition = Vector3.zero;
            Cam.transform.localRotation = Quaternion.identity;
            Cam.nearClipPlane = 0.01f;
            Cam.farClipPlane = 200f;
            Cam.backgroundColor = Color.black;
            Cam.clearFlags = CameraClearFlags.SolidColor;

            Weapons = gameObject.AddComponent<WeaponManager>();
            gameObject.AddComponent<GrenadeThrower>();
            yaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.identity;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Health = MaxHealth;
        }

        void OnDestroy() { GameInput.StopRumble(); }

        public bool HasPerk(Perk p) => Perks.Contains(p);

        void Update()
        {
            GameInput.Detect();
            // pause toggle works even while paused
            if (GameInput.PausePressed()) HUD.I?.TogglePause();

            var gm = GameManager.I;
            if (gm != null && (gm.Paused || gm.GameOver)) { GameInput.TickRumble(Time.unscaledDeltaTime); return; }
            GameInput.TickRumble(Time.deltaTime);

            // ---- look ----
            float sensMul = Weapons.IsAiming ? Weapons.AdsSensitivity : 1f;
            Vector2 md = GameInput.Look(Time.deltaTime) * GameSettings.Sensitivity * sensMul;
            LookDelta = md;
            yaw += md.x;
            pitch = Mathf.Clamp(pitch - md.y, -88f, 88f);
            // recoil recovery
            recoilPitch = Mathf.Lerp(recoilPitch, 0, Time.deltaTime * 10f);
            recoilYaw = Mathf.Lerp(recoilYaw, 0, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Euler(0, yaw + recoilYaw, 0);

            float targetCam = IsDown ? 0.45f : camHeight;
            var lp = camPivot.localPosition;
            lp.y = Mathf.Lerp(lp.y, targetCam, Time.deltaTime * 6f);
            camPivot.localPosition = lp;
            camPivot.localRotation = Quaternion.Euler(pitch - recoilPitch, 0, IsDown ? 18f : 0);

            // ---- move ----
            Vector2 input = IsDown ? Vector2.zero : GameInput.Move();
            IsSprinting = GameInput.SprintHeld() && input.y > 0.1f && !Weapons.IsAiming && !IsDown;
            float spd = (IsSprinting ? SprintSpeed : WalkSpeed) * Weapons.MoveMultiplier;
            if (Weapons.IsAiming) spd *= 0.6f;
            if (IsDown) spd = 0;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * spd;
            MoveAmount = input.magnitude * (IsSprinting ? 1.6f : 1f);

            if (cc.isGrounded)
            {
                vy = -2f;
                if (GameInput.JumpPressed() && !IsDown) vy = JumpSpeed;
            }
            else vy -= Gravity * Time.deltaTime;
            move.y = vy;
            cc.Move(move * Time.deltaTime);

            // ---- health regen ----
            if (!IsDown && Time.time - lastHurt > 2.4f) Health = Mathf.MoveTowards(Health, MaxHealth, 120f * Time.deltaTime);
            HurtAmount = Mathf.Clamp01(1f - Health / MaxHealth);
            GameSettings.SetDamageVignette(HurtAmount);

            // ---- interaction ----
            UpdateFocus();
            if (Focus != null && !IsDown && !IsDrinking)
            {
                if (Focus.HoldToUse ? GameInput.UseHeld() : GameInput.UsePressed()) Focus.Interact(this);
            }
        }

        void UpdateFocus()
        {
            Focus = null;
            float best = float.MaxValue;
            Vector3 eye = Cam.transform.position, fwd = Cam.transform.forward;
            foreach (var it in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                Vector3 to = it.Point - eye;
                Vector3 flat = new Vector3(to.x, 0, to.z);
                if (flat.magnitude > it.Range) continue;
                float dot = Vector3.Dot(fwd, to.normalized);
                if (dot < 0.35f) continue;
                if (it.Prompt(this) == null) continue;
                float score = flat.magnitude - dot;
                if (score < best) { best = score; Focus = it; }
            }
        }

        public void AddRecoil(float up, float side)
        {
            recoilPitch += up;
            recoilYaw += side;
            pitch -= up * 0.35f; // part of the kick stays, like real recoil
        }

        // ---------------- damage / down ----------------
        public void TakeDamage(float dmg, Vector3 from)
        {
            if (IsDown || IsInvulnerable) return;
            Health -= dmg;
            lastHurt = Time.time;
            SFX.Play("player_hurt", 0.8f, Random.Range(0.9f, 1.1f));
            GameInput.Rumble(0.8f, 0.5f, 0.2f);
            HUD.I?.FlashDamage();
            if (Health <= 0) GoDown();
        }

        void GoDown()
        {
            Health = 0;
            bool canSelfRevive = HasPerk(Perk.QuickRevive) && SelfRevivesUsed < 3;
            SFX.Play("down", 1f);
            IsDown = true;
            if (!canSelfRevive)
            {
                if (NetSession.IsOnline) { StartCoroutine(BleedOut()); return; } // co-op: wait for a teammate
                StartCoroutine(DeathRoutine());
                return;
            }
            StartCoroutine(SelfRevive());
        }

        /// <summary>Co-op: lie there for 45s hoping a teammate gets to you.</summary>
        IEnumerator BleedOut()
        {
            float t = 0f, total = 45f;
            while (IsDown && t < total)
            {
                t += Time.deltaTime;
                HUD.I?.ShowMessage("BLEEDING OUT  " + Mathf.CeilToInt(total - t), 0.15f);
                yield return null;
            }
            if (IsDown) { IsDead = true; HUD.I?.ShowMessage("YOU BLED OUT", 4f); }
        }

        /// <summary>A teammate finished reviving us.</summary>
        public void TeammateRevive()
        {
            if (!IsDown || IsDead) return;
            IsDown = false;
            Health = MaxHealth;
            lastHurt = Time.time;
            InvulnUntil = Time.time + 2f;
            BlindUntil = Time.time + 1.5f;
            SFX.Play("revive", 0.9f);
            HUD.I?.ShowMessage("REVIVED", 1.5f);
        }

        IEnumerator DeathRoutine()
        {
            HUD.I?.ShowMessage("YOU ARE DEAD", 2.5f);
            yield return new WaitForSeconds(2.5f);
            GameManager.I.TriggerGameOver();
        }

        /// <summary>Quick Revive: brief downed time, then back up with 2s invulnerability and zombies clueless for 3s.</summary>
        IEnumerator SelfRevive()
        {
            SelfRevivesUsed++;
            selfReviving = true;
            Perks.Clear(); // lose all perks when going down (classic rule)
            HUD.I?.RefreshPerks();
            BlindUntil = Time.time + 1000f;   // zombies ignore you while you're getting up
            InvulnUntil = Time.time + 1000f;
            float t = 0f, downTime = 3f;
            while (t < downTime)
            {
                t += Time.deltaTime;
                HUD.I?.ShowMessage("QUICK REVIVE  " + Mathf.CeilToInt(downTime - t), 0.1f);
                yield return null;
            }
            IsDown = false;
            selfReviving = false;
            Health = MaxHealth;
            InvulnUntil = Time.time + 2f;
            BlindUntil = Time.time + 3f;
            SFX.Play("revive", 0.9f);
            HUD.I?.ShowMessage("REVIVED", 1.5f);
        }

        // ---------------- perks ----------------
        public void DrinkPerk(Perk p)
        {
            if (IsDrinking) return;
            StartCoroutine(DrinkRoutine(p));
        }

        IEnumerator DrinkRoutine(Perk p)
        {
            IsDrinking = true;
            Weapons.ShowDrink(true);
            SFX.Play("slurp", 0.9f);
            yield return new WaitForSeconds(1.8f);
            Weapons.ShowDrink(false);
            if (!Perks.Contains(p)) Perks.Add(p);
            if (p == Perk.Juggernog) Health = MaxHealth;
            SFX.Play("perk", 0.8f);
            HUD.I?.RefreshPerks();
            HUD.I?.ShowMessage(PerkMachine.PerkName(p).ToUpper(), 1.5f);
            IsDrinking = false;
        }
    }
}
