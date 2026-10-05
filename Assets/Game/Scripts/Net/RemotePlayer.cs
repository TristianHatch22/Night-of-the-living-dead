using UnityEngine;

namespace NOTLD
{
    /// <summary>
    /// A teammate as seen on this machine: a blocky survivor with a name tag and their current weapon.
    /// On the host it is also a zombie target; on every machine a downed teammate can be revived by holding USE next to them.
    /// </summary>
    public class RemotePlayer : Interactable, IZombieTarget
    {
        public Peer Peer;
        public ulong Id => Peer != null ? Peer.Id : 0;
        RemoteState S => Peer.State;

        Transform body, armL, armR, legL, legR, holder;
        TextMesh nameTag;
        GameObject weaponGo;
        int weaponIdx = -1;
        float animT, holdT, lastHold, downBlend;
        bool placed;

        public bool Down => (S.Flags & NetSession.FDown) != 0;
        public bool Blind => (S.Flags & NetSession.FBlind) != 0;
        public bool Doomed => (S.Flags & NetSession.FDoomed) != 0;
        public bool Dead => (S.Flags & NetSession.FDead) != 0;
        public Vector3 Position => transform.position;
        public override bool HoldToUse => true;
        public override Vector3 Point => transform.position + Vector3.up * 0.3f;

        public static RemotePlayer Create(Peer peer)
        {
            var go = new GameObject("Teammate_" + peer.Name);
            go.layer = 2; // Ignore Raycast: shots pass through teammates
            var rp = go.AddComponent<RemotePlayer>();
            rp.Peer = peer;
            rp.Build();
            return rp;
        }

        void Build()
        {
            Range = 2.4f;
            float v = 0.55f + 0.1f * (Peer.Index % 4);
            var skin = Mats.Get("rp_skin", Mats.Gray(0.62f), 0.3f);
            var shirt = Mats.Get("rp_shirt_" + Peer.Index, Mats.Gray(v), 0.1f);
            var pants = Mats.Get("rp_pants", Mats.Gray(0.18f), 0.1f);
            var hair = Mats.Get("rp_hair", Mats.Gray(0.08f), 0.2f);

            body = new GameObject("Body").transform; body.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.22f, 0), new Vector3(0.46f, 0.58f, 0.26f), shirt);
            Mats.Prim(PrimitiveType.Cube, body, new Vector3(0, 0.88f, 0), new Vector3(0.42f, 0.22f, 0.24f), pants);
            Mats.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.55f, 0.0f), new Vector3(0.12f, 0.1f, 0.12f), skin);
            var head = Mats.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.72f, 0.02f), new Vector3(0.27f, 0.31f, 0.28f), skin);
            Mats.Prim(PrimitiveType.Sphere, head.transform, new Vector3(0, 0.25f, -0.1f), new Vector3(1.05f, 0.7f, 1.05f), hair);
            armL = Limb("ArmL", new Vector3(-0.3f, 1.47f, 0), new Vector3(0.12f, 0.62f, 0.12f), shirt, skin);
            armR = Limb("ArmR", new Vector3(0.3f, 1.47f, 0), new Vector3(0.12f, 0.62f, 0.12f), shirt, skin);
            legL = Limb("LegL", new Vector3(-0.11f, 0.8f, 0), new Vector3(0.16f, 0.8f, 0.16f), pants, pants);
            legR = Limb("LegR", new Vector3(0.11f, 0.8f, 0), new Vector3(0.16f, 0.8f, 0.16f), pants, pants);
            foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);

            holder = new GameObject("WeaponHolder").transform; holder.SetParent(body, false);
            holder.localPosition = new Vector3(0.16f, 1.3f, 0.35f);

            var tgo = new GameObject("NameTag"); tgo.transform.SetParent(transform, false);
            tgo.transform.localPosition = new Vector3(0, 2.2f, 0);
            nameTag = tgo.AddComponent<TextMesh>();
            nameTag.font = UIFactory.Font; nameTag.text = Peer.Name; nameTag.fontSize = 64; nameTag.characterSize = 0.045f;
            nameTag.anchor = TextAnchor.MiddleCenter; nameTag.alignment = TextAlignment.Center; nameTag.color = Color.white;
            tgo.GetComponent<MeshRenderer>().sharedMaterial = UIFactory.Font.material;
        }

        Transform Limb(string n, Vector3 pivot, Vector3 size, Material upper, Material lower)
        {
            var p = new GameObject(n).transform; p.SetParent(body, false); p.localPosition = pivot;
            Mats.Prim(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.3f, 0), new Vector3(size.x, size.y * 0.6f, size.z), upper);
            Mats.Prim(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.8f, 0), new Vector3(size.x * 0.85f, size.y * 0.4f, size.z * 0.85f), lower);
            return p;
        }

        void Update()
        {
            if (Peer == null) return;
            var s = S;
            if (!placed && s.Stamp > 0f) { transform.position = s.Pos; transform.rotation = Quaternion.Euler(0, s.Yaw, 0); placed = true; }
            if (!placed) { transform.position = new Vector3(0, -50f, 0); return; }

            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            Vector3 before = transform.position;
            transform.position = Vector3.Lerp(transform.position, s.Pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, s.Yaw, 0), k);
            if (nameTag != null) { nameTag.text = Peer.Name + (Doomed || Dead ? "  (DOWN)" : ""); }

            // weapon in hand
            if (weaponIdx != s.Weapon && s.Weapon < WeaponDatabase.All.Count) SetWeapon(s.Weapon);

            // animation
            float moved = new Vector3(transform.position.x - before.x, 0, transform.position.z - before.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            bool moving = moved > 0.3f && !Down;
            animT += Time.deltaTime * (moving ? Mathf.Clamp(moved * 1.6f, 4f, 11f) : 0f);
            float swing = moving ? Mathf.Sin(animT) * 35f : 0f;
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            float armUp = -70f + (s.Pitch * -0.6f);
            armR.localRotation = Quaternion.Euler(armUp, 0, -4);
            armL.localRotation = Quaternion.Euler(armUp + 10f, 0, 8);
            downBlend = Mathf.MoveTowards(downBlend, Down ? 1f : 0f, Time.deltaTime * 2.5f);
            body.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -85f, downBlend), 0, 0);
            body.localPosition = new Vector3(0, Mathf.Lerp(0f, 0.12f, downBlend), 0);
            holder.localRotation = Quaternion.Euler(s.Pitch * 0.6f, 0, 0);

            if (nameTag != null && Camera.main != null) nameTag.transform.rotation = Camera.main.transform.rotation;
        }

        void SetWeapon(int idx)
        {
            weaponIdx = idx;
            if (weaponGo != null) Destroy(weaponGo);
            var info = ViewmodelBuilder.Build(WeaponDatabase.All[idx], holder, false);
            weaponGo = info.Root;
            weaponGo.transform.localPosition = Vector3.zero;
            weaponGo.transform.localRotation = Quaternion.identity;
            foreach (var c in weaponGo.GetComponentsInChildren<Collider>()) Destroy(c);
            weaponGo.SetActive(true);
        }

        // ---------------- revive (hold USE) ----------------
        public override string Prompt(PlayerController p)
        {
            if (Peer == null || !Doomed || Dead) return null;
            return "Hold {USE} to revive " + Peer.Name;
        }

        public override void Interact(PlayerController p)
        {
            if (!Doomed || Dead) return;
            if (Time.time - lastHold > 0.25f) holdT = 0f;
            lastHold = Time.time;
            holdT += Time.deltaTime;
            HUD.I?.ShowMessage("REVIVING  " + Mathf.CeilToInt(Mathf.Max(0f, 3f - holdT)), 0.15f);
            if (holdT >= 3f)
            {
                holdT = 0f;
                NetGame.SendRevive(Id);
                SFX.Play("revive", 0.7f);
            }
        }

        // ---------------- zombie target (host only) ----------------
        public void Hit(float dmg, Vector3 from)
        {
            if ((S.Flags & NetSession.FInvuln) != 0) return;
            NetGame.SendHurt(Id, dmg, from);
        }
    }
}
