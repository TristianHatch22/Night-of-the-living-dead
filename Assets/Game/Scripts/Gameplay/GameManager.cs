using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOTLD
{
    /// <summary>Points, rounds and zombie spawning.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public int Points = 500;
        public int Round;
        public int TotalKills, Headshots;
        public bool Paused, GameOver;
        public readonly List<Zombie> Alive = new List<Zombie>();

        Barricade window;
        readonly List<Transform> spawns = new List<Transform>();
        int toSpawn;
        readonly Dictionary<ulong, int> repairPts = new Dictionary<ulong, int>();
        bool netBreak;
        public bool InBreak => inBreak;
        float nextSpawn, breakTimer = 3f;
        bool inBreak = true;
        static int MaxAlive => 24 + 8 * (NetSession.PlayerCount - 1);

        public System.Action<int> OnPointsChanged; // delta
        public System.Action<int> OnRoundChanged;

        void Awake()
        {
            I = this;
            window = FindFirstObjectByType<Barricade>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("ZombieSpawn")) spawns.Add(t);
        }

        void Update()
        {
            if ((Paused && !NetSession.IsOnline) || GameOver || NetSession.IsClient) return;

            if (inBreak)
            {
                breakTimer -= Time.deltaTime;
                if (breakTimer <= 0) StartRound(Round + 1);
                return;
            }

            if (toSpawn > 0 && Alive.Count < MaxAlive && Time.time >= nextSpawn)
            {
                SpawnZombie();
                toSpawn--;
                nextSpawn = Time.time + Mathf.Max(0.4f, 2.0f * Mathf.Pow(0.95f, Round - 1));
            }

            if (toSpawn <= 0 && Alive.Count == 0)
            {
                inBreak = true;
                breakTimer = 10f;
                SFX.Play("round_end", 0.9f);
            }
        }

        void StartRound(int r)
        {
            Round = r;
            inBreak = false;
            repairPts.Clear();
            toSpawn = Mathf.RoundToInt(ZombieCount(r) * (1f + 0.5f * (NetSession.PlayerCount - 1)));
            nextSpawn = Time.time + 1.5f;
            SFX.Play("round_start", 1f);
            OnRoundChanged?.Invoke(r);
            OnlineServices.SubmitRound(r);
        }

        /// <summary>Client side: apply the round / break state the host broadcast.</summary>
        public void NetApplyState(int round, bool brk, int kills)
        {
            TotalKills = kills;
            if (round > 0 && round != Round)
            {
                Round = round;
                SFX.Play("round_start", 1f);
                OnRoundChanged?.Invoke(round);
                OnlineServices.SubmitRound(round);
            }
            if (brk && !netBreak) SFX.Play("round_end", 0.9f);
            netBreak = brk; inBreak = brk;
        }

        public void NetGameOver(int round, int kills)
        {
            Round = round; TotalKills = kills;
            TriggerGameOver();
        }

        public static int ZombieCount(int r)
        {
            int[] early = { 6, 8, 13, 18, 24, 27, 28, 28, 29 };
            if (r <= early.Length) return early[r - 1];
            return Mathf.RoundToInt(0.09f * r * r + 0.0029f * r + 23.958f);
        }

        public static int ZombieHealth(int r)
        {
            if (r < 10) return 150 + 100 * (r - 1);
            return Mathf.RoundToInt(950f * Mathf.Pow(1.1f, r - 9));
        }

        ZombieSpeed RollSpeed()
        {
            float runner = Mathf.Clamp01((Round - 3) * 0.17f);
            float sprinter = Mathf.Clamp01((Round - 7) * 0.1f);
            float r = Random.value;
            if (r < sprinter * 0.7f) return ZombieSpeed.Sprinter;
            if (r < runner) return ZombieSpeed.Runner;
            return ZombieSpeed.Walker;
        }

        void SpawnZombie()
        {
            if (spawns.Count == 0 || window == null) return;
            var sp = spawns[Random.Range(0, spawns.Count)];
            var z = Zombie.Spawn(sp.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)), ZombieHealth(Round), RollSpeed(), window);
            Alive.Add(z);
        }

        public void OnZombieKilled(Zombie z)
        {
            Alive.Remove(z);
            TotalKills++;
        }

        /// <summary>Award points. On the host while processing a teammate's hit, the points go to that teammate instead.</summary>
        public void AddPoints(int amount)
        {
            if (NetSession.IsHost && NetSession.PointsTarget != NetSession.LocalTarget) { NetGame.GivePoints(NetSession.PointsTarget, amount); return; }
            AddPointsLocal(amount);
        }

        public void AddPointsLocal(int amount)
        {
            if (PlayerController.I != null && PlayerController.I.IsDown) return;
            Points += amount;
            OnPointsChanged?.Invoke(amount);
        }

        public void AddRepairPoints(int amount)
        {
            ulong key = NetSession.PointsTarget;
            repairPts.TryGetValue(key, out int have);
            if (have >= 500) return;
            repairPts[key] = have + amount;
            AddPoints(amount);
        }

        public bool Spend(int cost)
        {
            if (Points < cost) return false;
            Points -= cost;
            OnPointsChanged?.Invoke(-cost);
            return true;
        }

        public void TriggerGameOver()
        {
            if (GameOver) return;
            GameOver = true;
            if (NetSession.IsHost) NetGame.I?.SendGameOver(Round, TotalKills);
            SFX.Play("round_start", 1f, 0.6f);
            HUD.I?.ShowGameOver(Round, TotalKills);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = (p && !NetSession.IsOnline) ? 0f : 1f; // online games never freeze
            Cursor.lockState = p ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = p;
        }

        public static void LoadMenu() { Time.timeScale = 1f; NetSession.Leave(); SceneManager.LoadScene("MainMenu"); }
        public static void Restart() { if (NetSession.IsOnline) { LoadMenu(); return; } Time.timeScale = 1f; SceneManager.LoadScene("Game"); }

        // ---------------- shared combat helpers ----------------
        public static void Explode(Vector3 pos, float radius, int damage, string sfx = "explosion")
        {
            Effects.Explosion(pos, radius, sfx);
            NetGame.ExplosionFx(pos, radius, sfx);
            var list = new List<Zombie>(I.Alive);
            foreach (var z in list)
            {
                if (z == null || z.IsDead) continue;
                float d = Vector3.Distance(z.transform.position + Vector3.up, pos);
                if (d > radius) continue;
                int dmg = Mathf.RoundToInt(damage * Mathf.Lerp(1f, 0.35f, d / radius));
                z.TakeDamage(dmg, false, DamageKind.Explosive, z.transform.position + Vector3.up);
            }
        }
    }
}
