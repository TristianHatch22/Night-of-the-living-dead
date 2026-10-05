using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOTLD
{
    /// <summary>
    /// Gameplay replication for co-op. The HOST runs everything (zombies, rounds, barricade, damage to players);
    /// clients send their position + hit requests and render what the host tells them (puppet zombies, teammates).
    /// Lives on the persistent NetSession object so messages that arrive while the Game scene loads are never lost.
    /// </summary>
    public class NetGame : MonoBehaviour
    {
        public static NetGame I;

        public readonly Dictionary<ushort, Zombie> Zombies = new Dictionary<ushort, Zombie>();
        public readonly Dictionary<ulong, RemotePlayer> Remotes = new Dictionary<ulong, RemotePlayer>();
        readonly Dictionary<ushort, float> recentDead = new Dictionary<ushort, float>();
        readonly RemoteState localState = new RemoteState();

        ushort nextId = 1;
        float zT, pT, sT, allDownT = -1f;
        int lastRound = -1;
        bool lastBreak;
        public int LastHostRound = -1;

        void Awake() { I = this; NetSession.Disconnected += OnDisconnected; }
        void OnDestroy() { NetSession.Disconnected -= OnDisconnected; if (I == this) I = null; }

        void OnDisconnected(string why)
        {
            NetSession.Leave();
            NetSession.StatusMessage = why;
            if (SceneManager.GetActiveScene().name != "MainMenu") { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
        }

        // ------------------------------------------------------------ per-frame
        void Update()
        {
            OnlineServices.Tick(Time.unscaledDeltaTime);
            if (!NetSession.IsOnline || !NetSession.Connected) return;
            var gm = GameManager.I;
            if (gm == null) return; // still in the menu
            EnsureRemotes();
            float t = Time.unscaledTime;
            if (NetSession.IsHost) HostTick(gm, t); else ClientTick(gm, t);
        }

        void EnsureRemotes()
        {
            var gone = new List<ulong>();
            foreach (var kv in Remotes) if (kv.Value == null || !NetSession.Peers.ContainsKey(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { if (Remotes[id] != null) Destroy(Remotes[id].gameObject); Remotes.Remove(id); }
            foreach (var kv in NetSession.Peers)
            {
                if (kv.Key == NetSession.LocalId) continue;
                if (!Remotes.ContainsKey(kv.Key)) Remotes[kv.Key] = RemotePlayer.Create(kv.Value);
            }
        }

        void HostTick(GameManager gm, float t)
        {
            if (t >= pT) { pT = t + 0.05f; SendWorldPlayers(); }
            if (t >= zT) { zT = t + 1f / 15f; SendZombies(); }
            if (gm.Round != lastRound || gm.InBreak != lastBreak || t >= sT)
            {
                if (gm.Round != lastRound) OnlineServices.UpdateLobbyRound(gm.Round);
                lastRound = gm.Round; lastBreak = gm.InBreak; sT = t + 1.5f;
                SendGameState(ulong.MaxValue);
            }
            if (!gm.GameOver) CheckAllDown(gm, t);
        }

        void ClientTick(GameManager gm, float t)
        {
            if (t >= pT)
            {
                pT = t + 0.05f;
                var p = PlayerController.I;
                if (p != null)
                {
                    FillLocal(p, gm);
                    using (var w = new FastBufferWriter(64, Allocator.Temp)) { WriteState(w, localState); NetSession.ToHost("pstate", w, NetworkDelivery.UnreliableSequenced); }
                }
            }
            // drop puppets the host stopped telling us about
            List<ushort> stale = null;
            foreach (var kv in Zombies)
                if (kv.Value == null || t - kv.Value.LastNet > 1.5f) { (stale ?? (stale = new List<ushort>())).Add(kv.Key); }
            if (stale != null)
                foreach (var id in stale) { var z = Zombies[id]; Zombies.Remove(id); if (z != null) { gm.Alive.Remove(z); Destroy(z.gameObject); } }
            if (recentDead.Count > 0)
            {
                List<ushort> old = null;
                foreach (var kv in recentDead) if (t - kv.Value > 6f) (old ?? (old = new List<ushort>())).Add(kv.Key);
                if (old != null) foreach (var id in old) recentDead.Remove(id);
            }
        }

        void CheckAllDown(GameManager gm, float t)
        {
            var pc = PlayerController.I;
            bool all = pc != null && (pc.IsDoomed || pc.IsDead);
            if (all)
                foreach (var kv in NetSession.Peers)
                {
                    if (kv.Key == NetSession.LocalId) continue;
                    var s = kv.Value.State;
                    if ((s.Flags & (NetSession.FDoomed | NetSession.FDead)) == 0) { all = false; break; }
                }
            if (!all) { allDownT = -1f; return; }
            if (allDownT < 0f) allDownT = t;
            else if (t - allDownT > 1.5f) gm.TriggerGameOver();
        }

        // ------------------------------------------------------------ state helpers
        void FillLocal(PlayerController p, GameManager gm)
        {
            localState.Pos = p.transform.position;
            localState.Yaw = p.transform.eulerAngles.y;
            localState.Pitch = p.Pitch;
            var cur = p.Weapons != null ? p.Weapons.Cur : null;
            localState.Weapon = (ushort)(cur != null ? Mathf.Max(0, WeaponDatabase.All.IndexOf(cur.Def)) : 0);
            byte f = 0;
            if (p.IsDown) f |= NetSession.FDown;
            if (p.ZombiesBlind) f |= NetSession.FBlind;
            if (p.IsDoomed) f |= NetSession.FDoomed;
            if (p.IsDead) f |= NetSession.FDead;
            if (p.IsInvulnerable) f |= NetSession.FInvuln;
            if (p.IsSprinting) f |= NetSession.FSprint;
            localState.Flags = f;
            localState.Speed = (byte)Mathf.Clamp(p.MoveAmount * 60f, 0f, 255f);
            localState.Points = gm.Points;
        }

        static void WriteState(FastBufferWriter w, RemoteState s)
        {
            NetSession.WV(w, s.Pos);
            w.WriteValueSafe(s.Yaw); w.WriteValueSafe(s.Pitch);
            w.WriteValueSafe(s.Weapon); w.WriteValueSafe(s.Flags); w.WriteValueSafe(s.Speed); w.WriteValueSafe(s.Points);
        }

        static void ReadState(FastBufferReader r, RemoteState s)
        {
            s.Pos = NetSession.RV(r);
            r.ReadValueSafe(out s.Yaw); r.ReadValueSafe(out s.Pitch);
            r.ReadValueSafe(out s.Weapon); r.ReadValueSafe(out s.Flags); r.ReadValueSafe(out s.Speed); r.ReadValueSafe(out s.Points);
            s.Stamp = Time.unscaledTime;
        }

        // ------------------------------------------------------------ sending (host)
        void SendWorldPlayers()
        {
            var gm = GameManager.I; var pc = PlayerController.I;
            if (pc != null) FillLocal(pc, gm);
            using (var w = new FastBufferWriter(300, Allocator.Temp))
            {
                w.WriteValueSafe((byte)NetSession.Peers.Count);
                foreach (var kv in NetSession.Peers)
                {
                    w.WriteValueSafe(kv.Key);
                    WriteState(w, kv.Key == NetSession.LocalId ? localState : kv.Value.State);
                }
                NetSession.ToAllClients("pworld", w, NetworkDelivery.UnreliableSequenced);
            }
        }

        void SendZombies()
        {
            var list = new List<Zombie>();
            foreach (var z in Zombies.Values) if (z != null && !z.IsDead) list.Add(z);
            int n = Mathf.Min(list.Count, 60);
            using (var w = new FastBufferWriter(8 + n * 20, Allocator.Temp))
            {
                w.WriteValueSafe((ushort)n);
                for (int i = 0; i < n; i++)
                {
                    var z = list[i];
                    w.WriteValueSafe(z.NetId);
                    NetSession.WV(w, z.transform.position);
                    w.WriteValueSafe((byte)(Mathf.Repeat(z.transform.eulerAngles.y, 360f) / 360f * 255f));
                    w.WriteValueSafe(z.StateByte);
                }
                NetSession.ToAllClients("zsnap", w, NetworkDelivery.UnreliableSequenced);
            }
        }

        public void SendGameState(ulong only)
        {
            var gm = GameManager.I; if (gm == null || !NetSession.IsHost) return;
            var b = FindFirstObjectByType<Barricade>();
            using (var w = new FastBufferWriter(32, Allocator.Temp))
            {
                w.WriteValueSafe(gm.Round); w.WriteValueSafe(gm.InBreak); w.WriteValueSafe(gm.TotalKills);
                w.WriteValueSafe((byte)(b != null ? b.Boards : Barricade.MaxBoards));
                if (only == ulong.MaxValue) NetSession.ToAllClients("gstate", w, NetworkDelivery.Reliable);
                else NetSession.ToClient(only, "gstate", w, NetworkDelivery.Reliable);
            }
        }

        public void SendFullState(ulong client) { SendGameState(client); }

        public ushort Register(Zombie z)
        {
            ushort id = nextId++; if (nextId == 0) nextId = 1;
            Zombies[id] = z;
            return id;
        }

        public void ZombieDied(Zombie z, Vector3 vel)
        {
            if (!NetSession.IsHost) return;
            Zombies.Remove(z.NetId);
            using (var w = new FastBufferWriter(24, Allocator.Temp))
            {
                w.WriteValueSafe(z.NetId); NetSession.WV(w, vel);
                NetSession.ToAllClients("zdead", w, NetworkDelivery.Reliable);
            }
        }

        public void SendGameOver(int round, int kills)
        {
            if (!NetSession.IsHost) return;
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe(round); w.WriteValueSafe(kills);
                NetSession.ToAllClients("over", w, NetworkDelivery.Reliable);
            }
        }

        // ------------------------------------------------------------ static senders used by gameplay code
        public static void GivePoints(ulong target, int amount)
        {
            if (!NetSession.IsHost || target == NetSession.LocalTarget || target == NetSession.LocalId) return;
            using (var w = new FastBufferWriter(8, Allocator.Temp)) { w.WriteValueSafe(amount); NetSession.ToClient(target, "pts", w, NetworkDelivery.Reliable); }
        }

        public static void SendHurt(ulong target, float dmg, Vector3 from)
        {
            if (!NetSession.IsHost) return;
            using (var w = new FastBufferWriter(32, Allocator.Temp)) { w.WriteValueSafe(dmg); NetSession.WV(w, from); NetSession.ToClient(target, "hurt", w, NetworkDelivery.Reliable); }
        }

        public static void SendHit(ushort id, int dmg, bool head, DamageKind kind, Vector3 p)
        {
            if (!NetSession.IsClient) return;
            using (var w = new FastBufferWriter(48, Allocator.Temp))
            {
                w.WriteValueSafe(id); w.WriteValueSafe(dmg); w.WriteValueSafe(head); w.WriteValueSafe((byte)kind); NetSession.WV(w, p);
                NetSession.ToHost("hit", w, NetworkDelivery.Reliable);
            }
        }

        public static void SendLaunch(ushort id, Vector3 vel)
        {
            if (!NetSession.IsClient) return;
            using (var w = new FastBufferWriter(32, Allocator.Temp)) { w.WriteValueSafe(id); NetSession.WV(w, vel); NetSession.ToHost("zlaunch", w, NetworkDelivery.Reliable); }
        }

        public static void SendRepair()
        {
            if (!NetSession.IsClient) return;
            using (var w = new FastBufferWriter(8, Allocator.Temp)) { w.WriteValueSafe((byte)1); NetSession.ToHost("repair", w, NetworkDelivery.Reliable); }
        }

        public static void SendRevive(ulong target)
        {
            if (!NetSession.IsOnline) return;
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe(target);
                if (NetSession.IsHost) NetSession.ToClient(target, "revive", w, NetworkDelivery.Reliable);
                else NetSession.ToHost("revive", w, NetworkDelivery.Reliable);
            }
        }

        // cosmetic effects other players should see/hear
        public static void ShotFx(WeaponDef def, Vector3 muzzle, Vector3 end, bool tracer)
        {
            if (!NetSession.IsOnline) return;
            SendFx(0, muzzle, end, (ushort)WeaponDatabase.All.IndexOf(def), tracer ? 1f : 0f);
        }
        public static void ExplosionFx(Vector3 pos, float radius, string sfx)
        {
            if (!NetSession.IsOnline) return;
            SendFx(1, pos, pos, (ushort)(sfx == "plasma_boom" ? 1 : sfx == "grenade_boom" ? 2 : 0), radius);
        }
        public static void RailFx(Vector3 a, Vector3 b)
        {
            if (!NetSession.IsOnline) return;
            SendFx(2, a, b, 0, 0f);
        }

        static void SendFx(byte kind, Vector3 a, Vector3 b, ushort weapon, float f)
        {
            using (var w = new FastBufferWriter(64, Allocator.Temp))
            {
                w.WriteValueSafe(kind); w.WriteValueSafe(NetSession.LocalId);
                NetSession.WV(w, a); NetSession.WV(w, b); w.WriteValueSafe(weapon); w.WriteValueSafe(f);
                if (NetSession.IsHost) NetSession.ToAllClients("fx", w, NetworkDelivery.Unreliable);
                else NetSession.ToHost("fx", w, NetworkDelivery.Unreliable);
            }
        }

        // ------------------------------------------------------------ target selection (host)
        public IZombieTarget Nearest(Vector3 from)
        {
            IZombieTarget best = null; float bs = float.MaxValue;
            var pc = PlayerController.I;
            if (pc != null) { bs = Score(pc, from); best = pc; }
            foreach (var r in Remotes.Values)
            {
                if (r == null) continue;
                float s = Score(r, from);
                if (s < bs) { bs = s; best = r; }
            }
            return best;
        }

        static float Score(IZombieTarget t, Vector3 from)
        {
            Vector3 d = t.Position - from; d.y = 0;
            return d.magnitude + (t.Down ? 1000f : 0f) + (t.Blind ? 500f : 0f);
        }

        // ------------------------------------------------------------ receiving
        public void Handle(string name, ulong sender, FastBufferReader r)
        {
            switch (name)
            {
                case "pstate": if (NetSession.IsHost && NetSession.Peers.TryGetValue(sender, out var peer)) ReadState(r, peer.State); break;
                case "pworld": if (NetSession.IsClient) RecvWorldPlayers(r); break;
                case "zsnap": if (NetSession.IsClient) RecvZombies(r); break;
                case "zdead": if (NetSession.IsClient) RecvZombieDead(r); break;
                case "zlaunch": if (NetSession.IsHost) RecvLaunch(sender, r); break;
                case "hit": if (NetSession.IsHost) RecvHit(sender, r); break;
                case "pts": if (NetSession.IsClient) { r.ReadValueSafe(out int amt); GameManager.I?.AddPointsLocal(amt); } break;
                case "hurt": if (NetSession.IsClient) { r.ReadValueSafe(out float dmg); var from = NetSession.RV(r); PlayerController.I?.TakeDamage(dmg, from); } break;
                case "gstate": if (NetSession.IsClient) RecvGameState(r); break;
                case "repair": if (NetSession.IsHost) RecvRepair(sender); break;
                case "over": if (NetSession.IsClient) { r.ReadValueSafe(out int rd); r.ReadValueSafe(out int k); GameManager.I?.NetGameOver(rd, k); } break;
                case "revive": RecvRevive(r); break;
                case "fx": RecvFx(sender, r); break;
            }
        }

        void RecvWorldPlayers(FastBufferReader r)
        {
            r.ReadValueSafe(out byte n);
            var tmp = new RemoteState();
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ulong id);
                bool mine = id == NetSession.LocalId;
                if (!mine && NetSession.Peers.TryGetValue(id, out var p)) ReadState(r, p.State);
                else ReadState(r, tmp);
            }
        }

        void RecvZombies(FastBufferReader r)
        {
            var gm = GameManager.I; if (gm == null) return;
            var window = FindFirstObjectByType<Barricade>();
            r.ReadValueSafe(out ushort n);
            float t = Time.unscaledTime;
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id);
                Vector3 pos = NetSession.RV(r);
                r.ReadValueSafe(out byte yawB); r.ReadValueSafe(out byte st);
                if (!Zombies.TryGetValue(id, out var z) || z == null)
                {
                    if (recentDead.ContainsKey(id)) continue;
                    z = Zombie.SpawnPuppet(id, pos, (ZombieSpeed)Mathf.Clamp(st >> 4, 0, 2), window);
                    Zombies[id] = z; gm.Alive.Add(z);
                }
                z.NetApply(pos, yawB / 255f * 360f, (byte)(st & 15), t);
            }
        }

        void RecvZombieDead(FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); Vector3 vel = NetSession.RV(r);
            recentDead[id] = Time.unscaledTime;
            if (Zombies.TryGetValue(id, out var z)) { Zombies.Remove(id); if (z != null) z.NetDie(vel); }
        }

        void RecvHit(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); r.ReadValueSafe(out int dmg); r.ReadValueSafe(out bool head); r.ReadValueSafe(out byte kind);
            Vector3 p = NetSession.RV(r);
            if (!Zombies.TryGetValue(id, out var z) || z == null || z.IsDead) return;
            dmg = Mathf.Clamp(dmg, 0, 100000000);
            NetSession.PointsTarget = sender;
            try { z.TakeDamage(dmg, head, (DamageKind)Mathf.Clamp(kind, 0, 3), p); }
            finally { NetSession.PointsTarget = NetSession.LocalTarget; }
        }

        void RecvLaunch(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); Vector3 v = NetSession.RV(r);
            if (!Zombies.TryGetValue(id, out var z) || z == null || z.IsDead) return;
            NetSession.PointsTarget = sender;
            try { z.Launch(v); }
            finally { NetSession.PointsTarget = NetSession.LocalTarget; }
        }

        void RecvRepair(ulong sender)
        {
            var b = FindFirstObjectByType<Barricade>(); var gm = GameManager.I;
            if (b == null || gm == null || b.ClimbLocked || b.Boards >= Barricade.MaxBoards) return;
            NetSession.PointsTarget = sender;
            try { b.RepairBoard(); gm.AddRepairPoints(10); }
            finally { NetSession.PointsTarget = NetSession.LocalTarget; }
        }

        void RecvGameState(FastBufferReader r)
        {
            r.ReadValueSafe(out int round); r.ReadValueSafe(out bool brk); r.ReadValueSafe(out int kills); r.ReadValueSafe(out byte boards);
            var gm = GameManager.I; if (gm == null) return;
            gm.NetApplyState(round, brk, kills);
            var b = FindFirstObjectByType<Barricade>();
            if (b != null) b.SetBoards(boards);
        }

        void RecvRevive(FastBufferReader r)
        {
            r.ReadValueSafe(out ulong target);
            if (target == NetSession.LocalId) { PlayerController.I?.TeammateRevive(); return; }
            if (NetSession.IsHost)
                using (var w = new FastBufferWriter(16, Allocator.Temp)) { w.WriteValueSafe(target); NetSession.ToClient(target, "revive", w, NetworkDelivery.Reliable); }
        }

        void RecvFx(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out byte kind); r.ReadValueSafe(out ulong src);
            Vector3 a = NetSession.RV(r), b = NetSession.RV(r);
            r.ReadValueSafe(out ushort weapon); r.ReadValueSafe(out float f);
            if (NetSession.IsHost)
            {
                // relay to everybody except whoever sent it
                using (var w = new FastBufferWriter(64, Allocator.Temp))
                {
                    w.WriteValueSafe(kind); w.WriteValueSafe(src); NetSession.WV(w, a); NetSession.WV(w, b); w.WriteValueSafe(weapon); w.WriteValueSafe(f);
                    NetSession.ToAllClients("fx", w, NetworkDelivery.Unreliable, sender);
                }
            }
            if (src == NetSession.LocalId || GameManager.I == null) return;
            switch (kind)
            {
                case 0:
                    if (weapon < WeaponDatabase.All.Count)
                    {
                        var def = WeaponDatabase.All[weapon];
                        SFX.PlayAt(def.Sound, a, 0.8f, def.SoundPitch * Random.Range(0.97f, 1.03f));
                        Effects.Puff(a, 0.08f, 0.04f);
                        Effects.MuzzleFlashAt(a, def);
                        if (f > 0.5f) Effects.Tracer(a, b);
                    }
                    break;
                case 1: Effects.Explosion(a, Mathf.Max(1f, f), weapon == 1 ? "plasma_boom" : weapon == 2 ? "grenade_boom" : "explosion"); break;
                case 2: SFX.PlayAt("shot_rail", a, 0.9f, 1f); Effects.RailBeam(a, b); break;
            }
        }
    }
}
