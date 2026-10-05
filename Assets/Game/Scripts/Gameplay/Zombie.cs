using UnityEngine;

namespace NOTLD
{
    public enum DamageKind { Bullet, Melee, Explosive, Wonder }
    public enum ZombieSpeed { Walker, Runner, Sprinter }

    public class Zombie : MonoBehaviour
    {
        enum State { Approach, Tearing, Climbing, Chase, Dead }

        public int Health;
        public ZombieSpeed SpeedType;
        public bool IsDead => state == State.Dead;
        public Collider HeadCollider;
        public ushort NetId;
        public bool Puppet;          // client-side copy driven by the host's snapshots
        public float LastNet;
        public byte StateByte => (byte)((int)state | ((int)SpeedType << 4));
        Vector3 netPos; float netYaw;
        IZombieTarget target; float retargetT;

        State state = State.Approach;
        CharacterController cc;
        Barricade window;
        Transform armL, armR, legL, legR, body;
        float speed, vy, animT, nextTear, nextAttack, attackWindup = -1f, groanTimer, climbT, wanderT;
        Vector3 wanderDir;
        Vector3 climbFrom, climbMid, climbTo, slotOffset, flyVel;
        bool flying;

        // ---------- creation ----------
        public static Zombie Spawn(Vector3 pos, int health, ZombieSpeed spd, Barricade window)
        {
            var go = new GameObject("Zombie");
            go.transform.position = pos;
            var z = go.AddComponent<Zombie>();
            z.Health = health; z.SpeedType = spd; z.window = window;
            z.BuildBody();
            if (NetSession.IsHost && NetGame.I != null) z.NetId = NetGame.I.Register(z);
            return z;
        }

        public static Zombie SpawnPuppet(ushort id, Vector3 pos, ZombieSpeed spd, Barricade window)
        {
            var go = new GameObject("Zombie");
            go.transform.position = pos;
            var z = go.AddComponent<Zombie>();
            z.Puppet = true; z.NetId = id; z.Health = 1; z.SpeedType = spd; z.window = window;
            z.netPos = pos; z.LastNet = Time.unscaledTime;
            z.BuildBody();
            return z;
        }

        void BuildBody()
        {
            if (Puppet)
            {
                var cap = gameObject.AddComponent<CapsuleCollider>();
                cap.height = 1.62f; cap.radius = 0.3f; cap.center = new Vector3(0, 0.81f, 0);
                var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true;
            }
            else
            {
                cc = gameObject.AddComponent<CharacterController>();
                cc.height = 1.62f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.81f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 50;
            }

            float skinV = Random.Range(0.42f, 0.6f), shirtV = Random.Range(0.15f, 0.5f), pantsV = Random.Range(0.08f, 0.3f);
            var skin = Mats.Get("z_skin_" + (int)(skinV * 10), Mats.Gray(skinV), 0.25f);
            var shirt = Mats.Get("z_shirt_" + (int)(shirtV * 10), Mats.Gray(shirtV), 0.1f);
            var pants = Mats.Get("z_pants_" + (int)(pantsV * 10), Mats.Gray(pantsV), 0.1f);
            var eye = Mats.Get("z_eye", Color.white, 0.5f, 0, new Color(4f, 4f, 4f));
            var blood = Mats.Get("z_blood", Mats.Gray(0.03f), 0.8f);

            body = new GameObject("Body").transform; body.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.22f, 0), new Vector3(0.46f, 0.58f, 0.26f), shirt);
            Mats.Prim(PrimitiveType.Cube, body, new Vector3(0.08f, 1.1f, 0.135f), new Vector3(0.18f, 0.22f, 0.01f), blood); // stain
            Mats.Prim(PrimitiveType.Cube, body, new Vector3(0, 0.88f, 0), new Vector3(0.42f, 0.22f, 0.24f), pants);
            var neck = Mats.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.55f, 0.02f), new Vector3(0.12f, 0.1f, 0.12f), skin);
            var head = Mats.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.72f, 0.04f), new Vector3(0.27f, 0.31f, 0.28f), skin, new Vector3(10, 0, Random.Range(-15f, 15f)), true, "Head");
            HeadCollider = head.GetComponent<Collider>();
            HeadCollider.isTrigger = true;
            ((SphereCollider)HeadCollider).radius = 0.7f;
            Mats.Prim(PrimitiveType.Cube, head.transform, new Vector3(-0.18f, 0.08f, 0.45f), new Vector3(0.14f, 0.07f, 0.05f), eye);
            Mats.Prim(PrimitiveType.Cube, head.transform, new Vector3(0.18f, 0.08f, 0.45f), new Vector3(0.14f, 0.07f, 0.05f), eye);
            Mats.Prim(PrimitiveType.Cube, head.transform, new Vector3(0, -0.25f, 0.45f), new Vector3(0.3f, 0.08f, 0.05f), blood); // mouth

            armL = Limb("ArmL", new Vector3(-0.3f, 1.47f, 0), new Vector3(0.12f, 0.62f, 0.12f), shirt, skin);
            armR = Limb("ArmR", new Vector3(0.3f, 1.47f, 0), new Vector3(0.12f, 0.62f, 0.12f), shirt, skin);
            legL = Limb("LegL", new Vector3(-0.11f, 0.8f, 0), new Vector3(0.16f, 0.8f, 0.16f), pants, pants);
            legR = Limb("LegR", new Vector3(0.11f, 0.8f, 0), new Vector3(0.16f, 0.8f, 0.16f), pants, pants);

            foreach (var r in GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = GameSettings.Preset >= GraphicsPreset.High ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

            speed = SpeedType == ZombieSpeed.Walker ? Random.Range(1.1f, 1.5f) : SpeedType == ZombieSpeed.Runner ? Random.Range(2.9f, 3.4f) : Random.Range(4.5f, 5.0f);
            slotOffset = new Vector3(Random.Range(-1.6f, 1.6f), 0, Random.Range(0.4f, 1.6f));
            groanTimer = Random.Range(1f, 5f);
            animT = Random.value * 10f;
        }

        Transform Limb(string n, Vector3 pivot, Vector3 size, Material upper, Material lower)
        {
            var p = new GameObject(n).transform; p.SetParent(body, false); p.localPosition = pivot;
            Mats.Prim(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.3f, 0), new Vector3(size.x, size.y * 0.6f, size.z), upper);
            Mats.Prim(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.8f, 0), new Vector3(size.x * 0.85f, size.y * 0.4f, size.z * 0.85f), lower);
            return p;
        }

        // ---------- update ----------
        void Update()
        {
            if (GameManager.I != null && GameManager.I.Paused && !NetSession.IsOnline) return;
            if (Puppet) { PuppetUpdate(); return; }
            if (state == State.Dead) { DeadUpdate(); return; }

            groanTimer -= Time.deltaTime;
            if (groanTimer <= 0) { groanTimer = Random.Range(3f, 8f); SFX.PlayAt("groan", transform.position, 0.5f, Random.Range(0.75f, 1.2f)); }

            switch (state)
            {
                case State.Approach: ApproachUpdate(); break;
                case State.Tearing: TearUpdate(); break;
                case State.Climbing: ClimbUpdate(); break;
                case State.Chase:
                    if (target is UnityEngine.Object uo && uo == null) target = null;
                    retargetT -= Time.deltaTime;
                    if (target == null || retargetT <= 0f)
                    {
                        retargetT = 0.4f;
                        target = (NetSession.IsHost && NetGame.I != null) ? NetGame.I.Nearest(transform.position) : (IZombieTarget)PlayerController.I;
                    }
                    if (target != null) ChaseUpdate(target);
                    break;
            }
            Animate();
        }

        void MoveTowards(Vector3 target, float spd)
        {
            Vector3 d = target - transform.position; d.y = 0;
            Vector3 move = Vector3.zero;
            if (d.sqrMagnitude > 0.01f)
            {
                move = d.normalized * spd;
                // separation
                if (GameManager.I != null)
                    foreach (var o in GameManager.I.Alive)
                    {
                        if (o == this || o == null || o.IsDead) continue;
                        Vector3 s = transform.position - o.transform.position; s.y = 0;
                        float m = s.magnitude;
                        if (m < 0.8f && m > 0.001f) move += s / m * (0.8f - m) * 3f;
                    }
                var look = new Vector3(d.x, 0, d.z);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 8f);
            }
            vy = cc.isGrounded ? -1f : vy - 20f * Time.deltaTime;
            move.y = vy;
            cc.Move(move * Time.deltaTime);
        }

        void ApproachUpdate()
        {
            Vector3 target = window.OutsidePoint.position + window.OutsidePoint.TransformDirection(slotOffset * 0.5f);
            MoveTowards(target, Mathf.Max(speed, 1.6f));
            Vector3 flat = target - transform.position; flat.y = 0;
            if (flat.magnitude < 0.5f) { state = State.Tearing; nextTear = Time.time + 1f; }
        }

        void TearUpdate()
        {
            // face the window
            Vector3 d = window.OutsidePoint.position - window.InsidePoint.position; d.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-d), Time.deltaTime * 6f);
            vy = cc.isGrounded ? -1f : vy - 20f * Time.deltaTime;
            cc.Move(new Vector3(0, vy, 0) * Time.deltaTime);

            if (window.Boards > 0)
            {
                if (Time.time >= nextTear)
                {
                    window.TearBoard();
                    float rate = Mathf.Lerp(1.6f, 0.7f, Mathf.Clamp01((GameManager.I.Round - 1) / 15f));
                    nextTear = Time.time + rate + Random.Range(0f, 0.4f);
                }
            }
            else if (!window.ClimbLocked)
            {
                window.ClimbLocked = true;
                state = State.Climbing;
                cc.enabled = false;
                climbT = 0;
                climbFrom = transform.position;
                climbMid = window.SillPoint.position;
                climbTo = window.InsidePoint.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0);
            }
        }

        void ClimbUpdate()
        {
            float dur = SpeedType == ZombieSpeed.Walker ? 1.6f : 1.0f;
            climbT += Time.deltaTime / dur;
            float t = Mathf.Clamp01(climbT);
            Vector3 a = Vector3.Lerp(climbFrom, climbMid, t);
            Vector3 b = Vector3.Lerp(climbMid, climbTo, t);
            transform.position = Vector3.Lerp(a, b, t);
            if (t >= 1f)
            {
                cc.enabled = true;
                window.ClimbLocked = false;
                state = State.Chase;
            }
        }

        void ChaseUpdate(IZombieTarget player)
        {
            // Quick Revive: zombies are clueless - no chasing, no attacking, just shambling about
            if (player.Blind)
            {
                attackWindup = -1f;
                wanderT -= Time.deltaTime;
                if (wanderT <= 0f)
                {
                    wanderT = Random.Range(0.8f, 2f);
                    wanderDir = Random.value < 0.4f ? Vector3.zero : Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                }
                MoveTowards(transform.position + wanderDir * 2f, wanderDir == Vector3.zero ? 0.01f : speed * 0.3f);
                return;
            }
            Vector3 to = player.Position - transform.position; to.y = 0;
            float dist = to.magnitude;

            if (attackWindup >= 0)
            {
                attackWindup -= Time.deltaTime;
                MoveTowards(player.Position, speed * 0.2f);
                if (attackWindup < 0)
                {
                    if (dist < 1.6f && !player.Down) player.Hit(PlayerController.ZombieHitDamage, transform.position);
                    nextAttack = Time.time + 1.1f;
                }
                return;
            }
            if (dist < 1.25f)
            {
                if (Time.time >= nextAttack && !player.Down) attackWindup = 0.35f;
                MoveTowards(transform.position + to.normalized * 0.01f, 0.01f);
            }
            else MoveTowards(player.Position, speed);
        }

        void Animate()
        {
            bool moving = state == State.Approach || state == State.Chase;
            float rate = SpeedType == ZombieSpeed.Walker ? 5f : 10f;
            if (moving) animT += Time.deltaTime * rate;
            float swing = moving ? Mathf.Sin(animT) * (SpeedType == ZombieSpeed.Walker ? 25f : 45f) : 0;
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-swing, 0, 0);

            float reach = state == State.Tearing ? -100f + Mathf.Sin(Time.time * 8f) * 30f : (SpeedType == ZombieSpeed.Walker ? -80f : -40f - swing * 0.6f);
            if (attackWindup >= 0) reach = -150f + attackWindup * 200f;
            armL.localRotation = Quaternion.Euler(reach + Mathf.Sin(animT * 0.5f) * 8f, 0, 6);
            armR.localRotation = Quaternion.Euler(reach - Mathf.Sin(animT * 0.5f) * 8f, 0, -6);
            body.localRotation = Quaternion.Euler(SpeedType == ZombieSpeed.Walker ? 6f : 14f, Mathf.Sin(animT * 0.5f) * 5f, Mathf.Sin(animT * 0.5f) * 4f);
        }

        // ---------- online puppets ----------
        void PuppetUpdate()
        {
            if (state == State.Dead) { DeadUpdate(); return; }
            groanTimer -= Time.deltaTime;
            if (groanTimer <= 0) { groanTimer = Random.Range(3f, 8f); SFX.PlayAt("groan", transform.position, 0.5f, Random.Range(0.75f, 1.2f)); }
            float k = 1f - Mathf.Exp(-16f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, netPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netYaw, 0), k);
            Animate();
        }

        public void NetApply(Vector3 pos, float yaw, byte st, float stamp)
        {
            netPos = pos; netYaw = yaw; LastNet = stamp;
            if (state != State.Dead && st <= (byte)State.Chase) state = (State)st;
        }

        public void NetDie(Vector3 launchVel)
        {
            if (state == State.Dead) return;
            Die(launchVel);
        }

        // ---------- damage ----------
        public void TakeDamage(int dmg, bool head, DamageKind kind, Vector3 hitPoint)
        {
            if (state == State.Dead) return;
            if (Puppet)
            {
                SFX.PlayAt("zombie_hit", hitPoint, 0.5f, Random.Range(0.8f, 1.2f));
                Effects.Puff(hitPoint, 0.12f, 0.05f);
                NetGame.SendHit(NetId, dmg, head, kind, hitPoint);
                return;
            }
            if (kind == DamageKind.Bullet || kind == DamageKind.Melee) GameManager.I.AddPoints(10);
            Health -= dmg;
            SFX.PlayAt("zombie_hit", hitPoint, 0.5f, Random.Range(0.8f, 1.2f));
            Effects.Puff(hitPoint, 0.12f, 0.05f);
            if (Health <= 0)
            {
                int bonus = kind == DamageKind.Melee ? 120 : head ? 90 : 50;
                GameManager.I.AddPoints(bonus);
                Die(Vector3.zero);
            }
        }

        public void Launch(Vector3 velocity)
        {
            if (state == State.Dead) return;
            if (Puppet) { NetGame.SendLaunch(NetId, velocity); return; }
            GameManager.I.AddPoints(50);
            Health = 0;
            Die(velocity);
        }

        void Die(Vector3 launchVel)
        {
            if (state == State.Climbing) window.ClimbLocked = false;
            state = State.Dead;
            foreach (var col in GetComponents<Collider>()) col.enabled = false;
            if (HeadCollider) HeadCollider.enabled = false;
            flying = launchVel.sqrMagnitude > 0.1f;
            flyVel = launchVel;
            climbT = 0;
            if (NetSession.IsHost && NetGame.I != null) NetGame.I.ZombieDied(this, launchVel);
            GameManager.I.OnZombieKilled(this);
            Destroy(gameObject, flying ? 3f : 4f);
        }

        void DeadUpdate()
        {
            climbT += Time.deltaTime;
            if (flying)
            {
                flyVel.y -= 15f * Time.deltaTime;
                transform.position += flyVel * Time.deltaTime;
                transform.Rotate(new Vector3(-400f, 90f, 0) * Time.deltaTime);
                if (transform.position.y < 0) { transform.position = new Vector3(transform.position.x, 0, transform.position.z); flyVel = Vector3.zero; }
            }
            else
            {
                float t = Mathf.Clamp01(climbT / 0.6f);
                body.localRotation = Quaternion.Euler(Mathf.Lerp(10f, -88f, t * t), 0, 0);
                body.localPosition = new Vector3(0, Mathf.Lerp(0, 0.15f, t), 0);
            }
            if (climbT > 2.5f) transform.position += Vector3.down * Time.deltaTime * 0.3f; // sink
        }
    }
}
