using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

namespace NOTLD
{
    public class LobbyInfo
    {
        public string Id, Name, Host, RelayCode, LobbyCode;
        public int Players, Max, Round;
    }

    public struct LbEntry
    {
        public int Rank, Score;
        public string Name;
        public bool Mine;
    }

    /// <summary>
    /// Unity Gaming Services glue: anonymous sign-in, Lobby (server browser), Relay (NAT-friendly connection) and
    /// Leaderboards (top-100 highest round). Everything fails soft: if services are not set up the game still works
    /// (LAN co-op + local scores).
    /// </summary>
    public static class OnlineServices
    {
        public const string LeaderboardId = "highest_round";
        public const string SetupHint = "Online services are not set up yet.\nLink the project to Unity Cloud (Edit > Project Settings > Services) and enable Lobby, Relay and Leaderboards.";

        public static string LastError;
        static Task<bool> initTask;
        static float lastInitFail = -999f;

        // ------------------------------------------------------------ sign-in
        public static Task<bool> EnsureAsync()
        {
            bool live = UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
            if (initTask != null)
            {
                if (!initTask.IsCompleted) return initTask;
                if (initTask.Result && live) return initTask;
                if (!initTask.Result && Time.realtimeSinceStartup - lastInitFail < 20f) return initTask;
            }
            if (!live) lastAppliedName = null; // services were reset (e.g. editor play mode restart)
            initTask = InitAsync();
            return initTask;
        }

        static async Task<bool> InitAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                else if (UnityServices.State == ServicesInitializationState.Initializing)
                    while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Delay(100);
                if (UnityServices.State != ServicesInitializationState.Initialized) throw new Exception("Unity Services failed to initialise");
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await ApplyNameAsync();
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = Short(e);
                lastInitFail = Time.realtimeSinceStartup;
                return false;
            }
        }

        static string lastAppliedName;
        public static async Task ApplyNameAsync()
        {
            try
            {
                string want = NetSession.PlayerName.Replace(' ', '_');
                if (want.Length < 3) want = want + "___".Substring(0, 3 - want.Length);
                if (want == lastAppliedName || !AuthenticationService.Instance.IsSignedIn) return;
                await AuthenticationService.Instance.UpdatePlayerNameAsync(want);
                lastAppliedName = want;
            }
            catch (Exception) { /* cosmetic only */ }
        }

        static string Short(Exception e)
        {
            string m = e.Message ?? e.GetType().Name;
            int nl = m.IndexOf('\n'); if (nl > 0) m = m.Substring(0, nl);
            if (m.Contains("project") || m.Contains("Project") || m.Contains("ID")) m = "Project not linked to Unity Cloud";
            return m.Length > 140 ? m.Substring(0, 140) : m;
        }

        public static string StripTag(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Player";
            int h = name.LastIndexOf('#');
            return (h > 0 ? name.Substring(0, h) : name).Replace('_', ' ');
        }

        // ------------------------------------------------------------ lobbies / relay
        static string currentLobbyId;
        static float heartbeatT;
        static bool isLobbyHost;
        static string lastRoundSent;

        /// <summary>Create a public lobby + relay allocation and start hosting. Returns an error string or null.</summary>
        public static async Task<string> HostOnlineAsync(string roomName)
        {
            if (!await EnsureAsync()) return LastError ?? "Could not sign in";
            try
            {
                await ApplyNameAsync();
                var alloc = await RelayService.Instance.CreateAllocationAsync(NetSession.MaxPlayers - 1);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                var ep = alloc.ServerEndpoints.FirstOrDefault(x => x.ConnectionType == "dtls") ?? alloc.ServerEndpoints[0];

                string err = NetSession.StartHostRelay(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.Key, alloc.ConnectionData);
                if (err != null) return err;

                var opts = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "relay", new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                        { "host", new DataObject(DataObject.VisibilityOptions.Public, NetSession.PlayerName) },
                        { "round", new DataObject(DataObject.VisibilityOptions.Public, "0") },
                        { "game", new DataObject(DataObject.VisibilityOptions.Public, "NOTLD") },
                    }
                };
                var lobby = await LobbyService.Instance.CreateLobbyAsync(roomName, NetSession.MaxPlayers, opts);
                currentLobbyId = lobby.Id; isLobbyHost = true; heartbeatT = 0f;
                NetSession.RoomName = roomName;
                NetSession.JoinInfo = "Room code: " + lobby.LobbyCode;
                return null;
            }
            catch (Exception e)
            {
                NetSession.Leave();
                return Short(e);
            }
        }

        public static async Task<List<LobbyInfo>> ListLobbiesAsync()
        {
            if (!await EnsureAsync()) throw new Exception(LastError ?? "Could not sign in");
            var res = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter> { new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT) },
                Order = new List<QueryOrder> { new QueryOrder(false, QueryOrder.FieldOptions.Created) },
            });
            var list = new List<LobbyInfo>();
            foreach (var l in res.Results)
            {
                if (l.Data != null && l.Data.TryGetValue("game", out var g) && g.Value != "NOTLD") continue;
                list.Add(ToInfo(l));
            }
            return list;
        }

        static LobbyInfo ToInfo(Lobby l)
        {
            string Get(string k) => l.Data != null && l.Data.TryGetValue(k, out var d) ? d.Value : "";
            int.TryParse(Get("round"), out int round);
            return new LobbyInfo { Id = l.Id, Name = l.Name, Host = Get("host"), RelayCode = Get("relay"), LobbyCode = l.LobbyCode, Players = l.MaxPlayers - l.AvailableSlots, Max = l.MaxPlayers, Round = round };
        }

        public static async Task<string> JoinLobbyAsync(LobbyInfo info)
        {
            if (!await EnsureAsync()) return LastError ?? "Could not sign in";
            try
            {
                await ApplyNameAsync();
                var lobby = await LobbyService.Instance.JoinLobbyByIdAsync(info.Id);
                return await JoinRelayOfLobby(lobby);
            }
            catch (Exception e) { return Short(e); }
        }

        public static async Task<string> JoinByCodeAsync(string code)
        {
            if (!await EnsureAsync()) return LastError ?? "Could not sign in";
            try
            {
                await ApplyNameAsync();
                var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code.Trim().ToUpperInvariant());
                return await JoinRelayOfLobby(lobby);
            }
            catch (Exception e) { return Short(e); }
        }

        static async Task<string> JoinRelayOfLobby(Lobby lobby)
        {
            currentLobbyId = lobby.Id; isLobbyHost = false;
            if (lobby.Data == null || !lobby.Data.TryGetValue("relay", out var rc)) return "That lobby has no relay code";
            var ja = await RelayService.Instance.JoinAllocationAsync(rc.Value);
            var ep = ja.ServerEndpoints.FirstOrDefault(x => x.ConnectionType == "dtls") ?? ja.ServerEndpoints[0];
            string err = await NetSession.JoinRelayAsync(ep.Host, (ushort)ep.Port, ja.AllocationIdBytes, ja.Key, ja.ConnectionData, ja.HostConnectionData);
            if (err != null) { LeaveLobbyFireAndForget(); return err; }
            NetSession.RoomName = lobby.Name;
            NetSession.JoinInfo = "Room code: " + lobby.LobbyCode;
            return null;
        }

        /// <summary>Called by the host whenever the round changes so the server browser shows it.</summary>
        public static async void UpdateLobbyRound(int round)
        {
            if (!isLobbyHost || currentLobbyId == null) return;
            string v = round.ToString();
            if (v == lastRoundSent) return;
            lastRoundSent = v;
            try
            {
                await LobbyService.Instance.UpdateLobbyAsync(currentLobbyId, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject> { { "round", new DataObject(DataObject.VisibilityOptions.Public, v) } }
                });
            }
            catch (Exception) { }
        }

        /// <summary>Keeps the lobby alive (needs a ping every ~30s). Called from NetDriver.Update.</summary>
        public static void Tick(float dt)
        {
            if (!isLobbyHost || currentLobbyId == null) return;
            heartbeatT -= dt;
            if (heartbeatT > 0f) return;
            heartbeatT = 15f;
            SendHeartbeat();
        }

        static async void SendHeartbeat()
        {
            try { await LobbyService.Instance.SendHeartbeatPingAsync(currentLobbyId); } catch (Exception) { }
        }

        public static async void LeaveLobbyFireAndForget()
        {
            string id = currentLobbyId; bool host = isLobbyHost;
            currentLobbyId = null; isLobbyHost = false; lastRoundSent = null;
            if (id == null) return;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn) return;
                if (host) await LobbyService.Instance.DeleteLobbyAsync(id);
                else await LobbyService.Instance.RemovePlayerAsync(id, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------ leaderboard
        const string LocalKey = "lb_local_v1";
        static int lastSubmitted;
        static float lastSubmitFail = -999f;

        /// <summary>Submit the round the player just reached. Only ever called when it improves the session best.</summary>
        public static async void SubmitRound(int round)
        {
            SaveLocal(round);
            if (round <= lastSubmitted) return;
            if (Time.realtimeSinceStartup - lastSubmitFail < 30f) return;
            try
            {
                if (!await EnsureAsync()) { lastSubmitFail = Time.realtimeSinceStartup; return; }
                await ApplyNameAsync();
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, round);
                lastSubmitted = Mathf.Max(lastSubmitted, round);
            }
            catch (Exception e) { LastError = Short(e); lastSubmitFail = Time.realtimeSinceStartup; }
        }

        /// <summary>Top 100 (live from the cloud). Throws when services are unavailable.</summary>
        public static async Task<List<LbEntry>> FetchTop100Async()
        {
            if (!await EnsureAsync()) throw new Exception(LastError ?? "Could not sign in");
            var res = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, new GetScoresOptions { Offset = 0, Limit = 100 });
            string me = AuthenticationService.Instance.PlayerId;
            var list = new List<LbEntry>();
            foreach (var e in res.Results)
                list.Add(new LbEntry { Rank = e.Rank + 1, Name = StripTag(e.PlayerName), Score = (int)e.Score, Mine = e.PlayerId == me });
            return list;
        }

        /// <summary>The player's own best entry (works even when they're outside the top 100).</summary>
        public static async Task<LbEntry?> FetchMineAsync()
        {
            if (!await EnsureAsync()) return null;
            try
            {
                var e = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId);
                return new LbEntry { Rank = e.Rank + 1, Name = StripTag(e.PlayerName), Score = (int)e.Score, Mine = true };
            }
            catch (Exception) { return null; }
        }

        // ---- local fallback (this device's best runs) ----
        public static List<LbEntry> LoadLocal()
        {
            var list = new List<LbEntry>();
            string raw = PlayerPrefs.GetString(LocalKey, "");
            foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int c = part.LastIndexOf(':');
                if (c <= 0 || !int.TryParse(part.Substring(c + 1), out int sc)) continue;
                list.Add(new LbEntry { Name = part.Substring(0, c), Score = sc });
            }
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (list.Count > 100) list.RemoveRange(100, list.Count - 100);
            for (int i = 0; i < list.Count; i++) { var e = list[i]; e.Rank = i + 1; list[i] = e; }
            return list;
        }

        static void SaveLocal(int round)
        {
            try
            {
                var list = LoadLocal();
                string me = NetSession.PlayerName.Replace(';', ' ').Replace(':', ' ');
                int idx = list.FindIndex(e => e.Name == me);
                if (idx >= 0) { if (list[idx].Score >= round) return; list[idx] = new LbEntry { Name = me, Score = round }; }
                else list.Add(new LbEntry { Name = me, Score = round });
                list.Sort((a, b) => b.Score.CompareTo(a.Score));
                if (list.Count > 100) list.RemoveRange(100, list.Count - 100);
                PlayerPrefs.SetString(LocalKey, string.Join(";", list.Select(e => e.Name + ":" + e.Score)));
                PlayerPrefs.Save();
            }
            catch (Exception) { }
        }
    }
}
