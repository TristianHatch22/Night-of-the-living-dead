using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOTLD
{
    public enum NetMode { Solo, Host, Client }

    /// <summary>Anything a zombie can chase and hit: the local player or a teammate (host side).</summary>
    public interface IZombieTarget
    {
        Vector3 Position { get; }
        bool Down { get; }
        bool Blind { get; }
        void Hit(float dmg, Vector3 from);
    }

    public class RemoteState
    {
        public Vector3 Pos;
        public float Yaw, Pitch;
        public ushort Weapon;
        public byte Flags, Speed;
        public int Points;
        public float Stamp;
    }

    public class Peer
    {
        public ulong Id;
        public string Name = "Player";
        public int Index;
        public RemoteState State = new RemoteState();
    }

    /// <summary>
    /// Transport + roster for online co-op. Uses Netcode for GameObjects only as a transport (UTP / Relay) and
    /// custom named messages - the game itself is host authoritative (see NetGame).
    /// In solo play nothing here does anything (Mode == Solo).
    /// </summary>
    public static class NetSession
    {
        public static NetMode Mode = NetMode.Solo;
        public static bool IsOnline => Mode != NetMode.Solo;
        public static bool IsHost => Mode == NetMode.Host;
        public static bool IsClient => Mode == NetMode.Client;
        public const int MaxPlayers = 4;
        public const ulong LocalTarget = ulong.MaxValue; // "points go to the local player"

        // flag bits sent with player state
        public const byte FDown = 1, FBlind = 2, FDoomed = 4, FDead = 8, FInvuln = 16, FSprint = 32;

        /// <summary>Who gets the points while the host is applying a hit (set around remote hit processing).</summary>
        public static ulong PointsTarget = LocalTarget;

        public static string StatusMessage;          // shown on the main menu once (e.g. "Host left the game")
        public static string RoomName = "";
        public static string JoinInfo = "";          // lobby code or LAN address shown in the pause menu
        public static int LocalIndex;
        public static readonly Dictionary<ulong, Peer> Peers = new Dictionary<ulong, Peer>();

        public static event Action RosterChanged;
        public static event Action<string> Disconnected;

        static NetworkManager nm;
        static UnityTransport utp;
        static TaskCompletionSource<string> connectTcs;

        public const string KeyName = "PlayerName";
        public static string PlayerName
        {
            get
            {
                var n = PlayerPrefs.GetString(KeyName, "");
                if (string.IsNullOrWhiteSpace(n)) { n = "Survivor" + UnityEngine.Random.Range(100, 1000); PlayerPrefs.SetString(KeyName, n); PlayerPrefs.Save(); }
                return n;
            }
            set { PlayerPrefs.SetString(KeyName, Clean(value)); PlayerPrefs.Save(); }
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Survivor";
            s = s.Trim();
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (!char.IsControl(c) && c != '#' && c != '<' && c != '>') sb.Append(c);
            s = sb.ToString();
            if (s.Length > 16) s = s.Substring(0, 16);
            return s.Length == 0 ? "Survivor" : s;
        }

        public static bool Connected => nm != null && nm.IsListening;
        public static ulong LocalId => nm != null && nm.IsListening ? nm.LocalClientId : 0UL;
        public static int PlayerCount => IsOnline ? Mathf.Max(1, Peers.Count) : 1;

        // ------------------------------------------------------------ setup
        static void Ensure()
        {
            if (nm != null) return;
            Application.runInBackground = true; // never freeze a co-op game when the window loses focus
            var go = new GameObject("NetSession");
            UnityEngine.Object.DontDestroyOnLoad(go);
            utp = go.AddComponent<UnityTransport>();
            nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                EnableSceneManagement = false,
                ConnectionApproval = false,
                TickRate = 30,
            };
            go.AddComponent<NetGame>();
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        static void ResetState()
        {
            Peers.Clear();
            PointsTarget = LocalTarget;
            StatusMessage = null;
            LocalIndex = 0;
        }

        /// <summary>Host a LAN / direct-connect game (no online services needed).</summary>
        public static string StartHostLan(ushort port = 7777)
        {
            try
            {
                Ensure();
                if (nm.ShutdownInProgress) return "Network is still shutting down - try again in a second";
                utp.SetConnectionData("127.0.0.1", port, "0.0.0.0");
                string err = StartHostCommon();
                if (err == null) JoinInfo = LocalAddress() + ":" + port;
                return err;
            }
            catch (Exception e) { return Short(e); }
        }

        /// <summary>Host through a Unity Relay allocation.</summary>
        public static string StartHostRelay(string host, ushort port, byte[] allocId, byte[] key, byte[] connData)
        {
            try
            {
                Ensure();
                if (nm.ShutdownInProgress) return "Network is still shutting down - try again in a second";
                utp.SetRelayServerData(host, port, allocId, key, connData, connData, true);
                return StartHostCommon();
            }
            catch (Exception e) { return Short(e); }
        }

        static string StartHostCommon()
        {
            ResetState();
            if (!nm.StartHost()) return "Could not start the host";
            Mode = NetMode.Host;
            RegisterHandlers();
            Peers[nm.LocalClientId] = new Peer { Id = nm.LocalClientId, Name = PlayerName, Index = 0 };
            LocalIndex = 0;
            return null;
        }

        /// <summary>Join a LAN / direct-connect host.</summary>
        public static Task<string> JoinDirectAsync(string address)
        {
            string ip = address.Trim(); ushort port = 7777;
            int colon = ip.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(ip.Substring(colon + 1), out var p)) { port = p; ip = ip.Substring(0, colon); }
            string host = ip;
            return ConnectClientAsync(() => utp.SetConnectionData(host, port));
        }

        /// <summary>Join through a Relay join allocation.</summary>
        public static Task<string> JoinRelayAsync(string host, ushort port, byte[] allocId, byte[] key, byte[] connData, byte[] hostConnData)
        {
            return ConnectClientAsync(() => utp.SetRelayServerData(host, port, allocId, key, connData, hostConnData, true));
        }

        static async Task<string> ConnectClientAsync(Action setup)
        {
            try
            {
                Ensure();
                if (nm.ShutdownInProgress) return "Network is still shutting down - try again in a second";
                ResetState();
                setup();
                connectTcs = new TaskCompletionSource<string>();
                if (!nm.StartClient()) return "Could not start the client";
                Mode = NetMode.Client;
                RegisterHandlers();
                var tcs = connectTcs;
                var done = await Task.WhenAny(tcs.Task, Task.Delay(15000));
                if (done != tcs.Task) { Leave(); return "Connection timed out"; }
                string res = tcs.Task.Result;
                if (res != null) Leave();
                return res;
            }
            catch (Exception e) { Leave(); return Short(e); }
        }

        /// <summary>Disconnect / stop hosting and return to solo mode.</summary>
        public static void Leave()
        {
            try { if (nm != null && (nm.IsListening || nm.IsClient || nm.IsServer)) nm.Shutdown(); }
            catch (Exception e) { Debug.LogWarning("NetSession.Leave: " + e.Message); }
            Mode = NetMode.Solo;
            Peers.Clear();
            PointsTarget = LocalTarget;
            JoinInfo = ""; RoomName = "";
            connectTcs?.TrySetResult("Cancelled");
            OnlineServices.LeaveLobbyFireAndForget();
        }

        public static string LocalAddress()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        static string Short(Exception e)
        {
            string m = e.Message ?? e.GetType().Name;
            int nl = m.IndexOf('\n'); if (nl > 0) m = m.Substring(0, nl);
            return m.Length > 160 ? m.Substring(0, 160) : m;
        }

        // ------------------------------------------------------------ connection events
        static void OnClientConnected(ulong id)
        {
            if (Mode == NetMode.Client && id == nm.LocalClientId)
            {
                using (var w = new FastBufferWriter(96, Allocator.Temp))
                {
                    w.WriteValueSafe(PlayerName);
                    ToHost("hello", w, NetworkDelivery.Reliable);
                }
            }
        }

        static void OnClientDisconnected(ulong id)
        {
            if (Mode == NetMode.Host)
            {
                if (Peers.Remove(id)) { BroadcastRoster(); RosterChanged?.Invoke(); }
            }
            else if (Mode == NetMode.Client)
            {
                if (id == nm.LocalClientId || id == NetworkManager.ServerClientId)
                {
                    if (connectTcs != null && !connectTcs.Task.IsCompleted) { connectTcs.TrySetResult("Could not reach the host"); return; }
                    Disconnected?.Invoke("Lost connection to the host");
                }
            }
        }

        // ------------------------------------------------------------ messaging
        static void RegisterHandlers()
        {
            var m = nm.CustomMessagingManager;
            foreach (var name in Names)
            {
                string n = name;
                m.RegisterNamedMessageHandler(n, (sender, reader) => Dispatch(n, sender, reader));
            }
        }

        static readonly string[] Names = { "hello", "welcome", "roster", "pstate", "pworld", "zsnap", "zdead", "zlaunch", "hit", "pts", "hurt", "gstate", "repair", "over", "revive", "fx" };

        static void Dispatch(string name, ulong sender, FastBufferReader r)
        {
            try
            {
                switch (name)
                {
                    case "hello": OnHello(sender, r); break;
                    case "welcome": OnWelcome(r); break;
                    case "roster": OnRoster(r); break;
                    default: NetGame.I?.Handle(name, sender, r); break;
                }
            }
            catch (Exception e) { Debug.LogWarning($"Net message '{name}' failed: {e.Message}"); }
        }

        public static void ToHost(string msg, FastBufferWriter w, NetworkDelivery d)
        {
            if (!Connected || IsHost) return;
            nm.CustomMessagingManager.SendNamedMessage(msg, NetworkManager.ServerClientId, w, d);
        }

        public static void ToClient(ulong id, string msg, FastBufferWriter w, NetworkDelivery d)
        {
            if (!Connected || !IsHost || id == nm.LocalClientId) return;
            nm.CustomMessagingManager.SendNamedMessage(msg, id, w, d);
        }

        public static void ToAllClients(string msg, FastBufferWriter w, NetworkDelivery d, ulong except = ulong.MaxValue)
        {
            if (!Connected || !IsHost) return;
            foreach (var id in nm.ConnectedClientsIds)
                if (id != nm.LocalClientId && id != except) nm.CustomMessagingManager.SendNamedMessage(msg, id, w, d);
        }

        public static void WV(FastBufferWriter w, Vector3 v) { w.WriteValueSafe(v.x); w.WriteValueSafe(v.y); w.WriteValueSafe(v.z); }
        public static Vector3 RV(FastBufferReader r) { r.ReadValueSafe(out float x); r.ReadValueSafe(out float y); r.ReadValueSafe(out float z); return new Vector3(x, y, z); }

        // ------------------------------------------------------------ roster
        static void OnHello(ulong sender, FastBufferReader r)
        {
            if (!IsHost) return;
            r.ReadValueSafe(out string name);
            if (Peers.Count >= MaxPlayers && !Peers.ContainsKey(sender)) { nm.DisconnectClient(sender, "Game is full"); return; }
            int idx = 1;
            var used = new HashSet<int>(); foreach (var p in Peers.Values) used.Add(p.Index);
            while (used.Contains(idx)) idx++;
            Peers[sender] = new Peer { Id = sender, Name = Clean(name), Index = idx };
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe((byte)idx);
                ToClient(sender, "welcome", w, NetworkDelivery.Reliable);
            }
            BroadcastRoster();
            RosterChanged?.Invoke();
            NetGame.I?.SendFullState(sender);
        }

        static void OnWelcome(FastBufferReader r)
        {
            if (!IsClient) return;
            r.ReadValueSafe(out byte idx);
            LocalIndex = idx;
            connectTcs?.TrySetResult(null);
        }

        static void BroadcastRoster()
        {
            using (var w = new FastBufferWriter(512, Allocator.Temp))
            {
                w.WriteValueSafe((byte)Peers.Count);
                foreach (var p in Peers.Values) { w.WriteValueSafe(p.Id); w.WriteValueSafe((byte)p.Index); w.WriteValueSafe(p.Name); }
                ToAllClients("roster", w, NetworkDelivery.Reliable);
            }
        }

        static void OnRoundtripRoster() { }

        static void OnRoster(FastBufferReader r)
        {
            if (!IsClient) return;
            r.ReadValueSafe(out byte n);
            var seen = new HashSet<ulong>();
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ulong id); r.ReadValueSafe(out byte idx); r.ReadValueSafe(out string name);
                seen.Add(id);
                if (!Peers.TryGetValue(id, out var p)) { p = new Peer { Id = id }; Peers[id] = p; }
                p.Index = idx; p.Name = name;
            }
            var gone = new List<ulong>();
            foreach (var id in Peers.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) Peers.Remove(id);
            RosterChanged?.Invoke();
        }

        internal static void RaiseDisconnected(string why) => Disconnected?.Invoke(why);
    }
}
