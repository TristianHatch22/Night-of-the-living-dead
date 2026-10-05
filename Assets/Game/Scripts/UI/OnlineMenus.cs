using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NOTLD
{
    /// <summary>Runs a callback every frame while its GameObject is active (used for live polling while a panel is open).</summary>
    public class PanelTicker : MonoBehaviour
    {
        public Action Tick;
        void Update() { Tick?.Invoke(); }
    }

    // ===================================================================================== LEADERBOARD
    /// <summary>Top-100 highest round, polled live every few seconds while the panel is open.</summary>
    public class LeaderboardPanel
    {
        public GameObject Root;
        readonly Action onBack;
        Text status, ranks, names, scores, mineText;
        ScrollRect scroll;
        RectTransform content;
        Button backBtn;
        float nextPoll, lastUpdate = -1f, viewH = 500f;
        bool busy, live, failedOnce;
        string lastError;

        public LeaderboardPanel(Transform parent, Action onBack)
        {
            this.onBack = onBack;
            var bg = UIFactory.Panel(parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 900), new Color(0.02f, 0.02f, 0.02f, 1f));
            Root = bg.gameObject;
            var t = bg.transform;
            UIFactory.Label(t, "LEADERBOARD", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(900, 80), null, FontStyle.Bold);
            status = UIFactory.Label(t, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1000, 40), UIFactory.Dim);
            UIFactory.Label(t, "#", 26, TextAnchor.MiddleRight, new Vector2(0.5f, 1f), new Vector2(-440, -160), new Vector2(120, 36), UIFactory.Dim, FontStyle.Bold);
            UIFactory.Label(t, "SURVIVOR", 26, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-250, -160), new Vector2(400, 36), UIFactory.Dim, FontStyle.Bold);
            UIFactory.Label(t, "HIGHEST ROUND", 26, TextAnchor.MiddleRight, new Vector2(0.5f, 1f), new Vector2(330, -160), new Vector2(300, 36), UIFactory.Dim, FontStyle.Bold);

            // scroll area
            var view = UIFactory.Rect("Viewport", t, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.offsetMin = new Vector2(40, 130); view.offsetMax = new Vector2(-40, -200);
            var vimg = view.gameObject.AddComponent<Image>(); vimg.color = new Color(1, 1, 1, 0.02f);
            view.gameObject.AddComponent<RectMask2D>();
            content = UIFactory.Rect("Content", view, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 600));
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            ranks = UIFactory.Label(content, "", 28, TextAnchor.UpperRight, new Vector2(0, 1), new Vector2(10, 0), new Vector2(110, 600));
            names = UIFactory.Label(content, "", 28, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(150, 0), new Vector2(560, 600));
            scores = UIFactory.Label(content, "", 28, TextAnchor.UpperRight, new Vector2(0, 1), new Vector2(760, 0), new Vector2(240, 600));
            foreach (var l in new[] { ranks, names, scores }) { ((RectTransform)l.transform).pivot = new Vector2(0, 1); l.supportRichText = true; }
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = view; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45f;

            mineText = UIFactory.Label(t, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 105), new Vector2(1000, 44), UIFactory.Bone, FontStyle.Bold);
            backBtn = UIFactory.Button(t, "BACK", new Vector2(0.5f, 0f), new Vector2(0, 48), new Vector2(300, 64), () => { Root.SetActive(false); this.onBack?.Invoke(); });

            Root.AddComponent<PanelTicker>().Tick = Tick;
            Root.SetActive(false);
        }

        public void Show()
        {
            Root.SetActive(true);
            nextPoll = 0f; failedOnce = false;
            scroll.verticalNormalizedPosition = 1f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
        }

        void Tick()
        {
            float now = Time.unscaledTime;
            if (!busy && now >= nextPoll) { nextPoll = now + (failedOnce ? 15f : 5f); Refresh(); }

            // controller scrolling (left stick / d-pad)
            var gp = Gamepad.current;
            if (gp != null)
            {
                float v = gp.leftStick.y.ReadValue() + gp.dpad.y.ReadValue();
                if (Mathf.Abs(v) > 0.2f && content.rect.height > viewH)
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + v * Time.unscaledDeltaTime * 1100f / (content.rect.height - viewH));
            }
            if (live && lastUpdate >= 0f)
                status.text = "<color=#7CFF8A>●</color> LIVE   -   TOP 100   -   updated " + Mathf.FloorToInt(now - lastUpdate) + "s ago";
        }

        async void Refresh()
        {
            busy = true;
            try
            {
                var list = await OnlineServices.FetchTop100Async();
                var mine = await OnlineServices.FetchMineAsync();
                if (Root == null) return;
                live = true; failedOnce = false; lastUpdate = Time.unscaledTime;
                Fill(list);
                if (mine.HasValue) mineText.text = "YOUR BEST:  ROUND " + mine.Value.Score + "   (RANK #" + mine.Value.Rank + ")";
                else mineText.text = "Reach round 1 to appear on the board";
            }
            catch (Exception e)
            {
                if (Root == null) return;
                live = false; failedOnce = true;
                lastError = e.Message;
                int nl = lastError.IndexOf('\n'); if (nl > 0) lastError = lastError.Substring(0, nl);
                var local = OnlineServices.LoadLocal();
                status.text = "<color=#FF6A5C>●</color> OFFLINE - showing this device's best runs   (" + (lastError.Length > 70 ? lastError.Substring(0, 70) : lastError) + ")";
                Fill(local);
                mineText.text = local.Count > 0 ? "" : "Play a game to set a score";
            }
            finally { busy = false; }
        }

        void Fill(List<LbEntry> list)
        {
            var r = new StringBuilder(); var n = new StringBuilder(); var s = new StringBuilder();
            const string gold = "<color=#FFD36B><b>", end = "</b></color>";
            for (int i = 0; i < list.Count && i < 100; i++)
            {
                var e = list[i];
                string pre = e.Mine ? gold : "", post = e.Mine ? end : "";
                r.Append(pre).Append(e.Rank).Append(post).Append('\n');
                n.Append(pre).Append(e.Name.Replace("<", "")).Append(post).Append('\n');
                s.Append(pre).Append(e.Score).Append(post).Append('\n');
            }
            if (list.Count == 0) { n.Append("No scores yet - be the first!"); }
            ranks.text = r.ToString(); names.text = n.ToString(); scores.text = s.ToString();
            float h = Mathf.Max(names.preferredHeight, ranks.preferredHeight) + 16f;
            content.sizeDelta = new Vector2(0, h);
            foreach (var l in new[] { ranks, names, scores }) { var rt = (RectTransform)l.transform; rt.sizeDelta = new Vector2(rt.sizeDelta.x, h); }
            viewH = ((RectTransform)content.parent).rect.height;
        }
    }

    // ===================================================================================== CO-OP / SERVERS
    /// <summary>Co-op menu: host (online or LAN) and a Servers tab listing active lobbies.</summary>
    public class CoopPanel
    {
        public GameObject Root;
        readonly Action onBack;
        GameObject hostRoot, serverRoot;
        RectTransform listRoot;
        Text status;
        InputField nameInput, roomInput, codeInput, ipInput;
        Button backBtn, refreshBtn, hostTabBtn, serverTabBtn;
        bool serversTab, busy, listBusy;
        float nextPoll;
        string listSig = null;

        public CoopPanel(Transform parent, Action onBack)
        {
            this.onBack = onBack;
            var bg = UIFactory.Panel(parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160, 940), new Color(0.02f, 0.02f, 0.02f, 1f));
            Root = bg.gameObject;
            var t = bg.transform;
            var top = new Vector2(0.5f, 1f);

            UIFactory.Label(t, "CO-OP", 56, TextAnchor.MiddleCenter, top, new Vector2(0, -50), new Vector2(900, 80), null, FontStyle.Bold);
            UIFactory.Label(t, "YOUR NAME", 28, TextAnchor.MiddleLeft, top, new Vector2(-370, -115), new Vector2(240, 50), UIFactory.Dim);
            nameInput = UIFactory.Input(t, NetSession.PlayerName, "Survivor", top, new Vector2(-30, -115), new Vector2(380, 54), 30, 16);
            nameInput.onEndEdit.AddListener(v => { NetSession.PlayerName = v; nameInput.text = NetSession.PlayerName; });

            hostTabBtn = UIFactory.Button(t, "HOST A GAME", top, new Vector2(-190, -185), new Vector2(340, 58), () => ShowTab(false), 28);
            serverTabBtn = UIFactory.Button(t, "SERVERS", top, new Vector2(170, -185), new Vector2(340, 58), () => ShowTab(true), 28);
            refreshBtn = UIFactory.Button(t, "REFRESH", top, new Vector2(440, -185), new Vector2(180, 58), () => RefreshList(true), 26);

            // ---------------- host tab
            hostRoot = UIFactory.Rect("Host", t, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var h = hostRoot.transform;
            UIFactory.Label(h, "ROOM NAME", 28, TextAnchor.MiddleLeft, top, new Vector2(-370, -290), new Vector2(240, 50), UIFactory.Dim);
            roomInput = UIFactory.Input(h, "", NetSession.PlayerName + "'s game", top, new Vector2(40, -290), new Vector2(520, 54), 30, 24);
            UIFactory.Button(h, "START ONLINE GAME", top, new Vector2(0, -395), new Vector2(620, 80), HostOnline, 38);
            UIFactory.Button(h, "START LAN GAME", top, new Vector2(0, -495), new Vector2(620, 80), HostLan, 38);
            UIFactory.Label(h, "ONLINE: appears in everyone's SERVERS list and can be joined with the room code (up to 4 players).\nLAN: same network or direct IP - friends type your address into DIRECT IP on the SERVERS tab.", 24,
                TextAnchor.UpperCenter, top, new Vector2(0, -600), new Vector2(1040, 100), UIFactory.Dim);

            // ---------------- servers tab
            serverRoot = UIFactory.Rect("Servers", t, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var sv = serverRoot.transform;
            UIFactory.Label(sv, "SERVER", 24, TextAnchor.MiddleLeft, top, new Vector2(-250, -250), new Vector2(560, 36), UIFactory.Dim, FontStyle.Bold);
            UIFactory.Label(sv, "PLAYERS", 24, TextAnchor.MiddleCenter, top, new Vector2(180, -250), new Vector2(140, 36), UIFactory.Dim, FontStyle.Bold);
            UIFactory.Label(sv, "ROUND", 24, TextAnchor.MiddleCenter, top, new Vector2(300, -250), new Vector2(140, 36), UIFactory.Dim, FontStyle.Bold);
            listRoot = UIFactory.Rect("List", sv, top, top, new Vector2(0, -275), new Vector2(1040, 340));
            UIFactory.Label(sv, "ROOM CODE", 26, TextAnchor.MiddleLeft, top, new Vector2(-370, -655), new Vector2(240, 50), UIFactory.Dim);
            codeInput = UIFactory.Input(sv, "", "ABC123", top, new Vector2(-60, -655), new Vector2(300, 54), 30, 8);
            UIFactory.Button(sv, "JOIN", top, new Vector2(170, -655), new Vector2(160, 54), JoinCode, 28);
            UIFactory.Label(sv, "DIRECT IP", 26, TextAnchor.MiddleLeft, top, new Vector2(-370, -720), new Vector2(240, 50), UIFactory.Dim);
            ipInput = UIFactory.Input(sv, "", "192.168.0.10:7777", top, new Vector2(-60, -720), new Vector2(300, 54), 28, 40);
            UIFactory.Button(sv, "JOIN", top, new Vector2(170, -720), new Vector2(160, 54), JoinIp, 28);

            status = UIFactory.Label(t, "", 26, TextAnchor.UpperCenter, top, new Vector2(0, -785), new Vector2(1060, 90), UIFactory.Bone);
            backBtn = UIFactory.Button(t, "BACK", new Vector2(0.5f, 0f), new Vector2(0, 48), new Vector2(300, 64), () => { Root.SetActive(false); this.onBack?.Invoke(); });

            Root.AddComponent<PanelTicker>().Tick = Tick;
            ShowTab(false);
            Root.SetActive(false);
        }

        public void Show()
        {
            Root.SetActive(true);
            nameInput.text = NetSession.PlayerName;
            SetStatus("");
            ShowTab(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(hostTabBtn.gameObject);
        }

        void ShowTab(bool servers)
        {
            serversTab = servers;
            hostRoot.SetActive(!servers);
            serverRoot.SetActive(servers);
            refreshBtn.gameObject.SetActive(servers);
            if (servers) { nextPoll = 0f; RefreshList(true); }
        }

        void SetStatus(string s) { if (status != null) status.text = s; }

        void Tick()
        {
            if (serversTab && !busy && Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + 5f; RefreshList(false); }
        }

        // ---------------- list
        async void RefreshList(bool announce)
        {
            if (listBusy) return;
            listBusy = true;
            if (announce) SetStatus("Looking for games...");
            try
            {
                var list = await OnlineServices.ListLobbiesAsync();
                if (Root == null) return;
                BuildRows(list);
                if (!busy) SetStatus(list.Count == 0 ? "No open games right now - host one, or join with a room code / IP." : list.Count + " game" + (list.Count == 1 ? "" : "s") + " found");
            }
            catch (Exception e)
            {
                if (Root == null) return;
                string m = e.Message; int nl = m.IndexOf('\n'); if (nl > 0) m = m.Substring(0, nl);
                BuildRows(new List<LobbyInfo>());
                if (!busy) SetStatus("Server list unavailable: " + (m.Length > 90 ? m.Substring(0, 90) : m) + "\nLAN / DIRECT IP still works. (Setup: link the project in Project Settings > Services)");
                nextPoll = Time.unscaledTime + 15f;
            }
            finally { listBusy = false; }
        }

        void BuildRows(List<LobbyInfo> list)
        {
            var sb = new StringBuilder();
            foreach (var l in list) sb.Append(l.Id).Append(l.Players).Append('/').Append(l.Round).Append(';');
            if (sb.ToString() == listSig) return;
            listSig = sb.ToString();

            for (int i = listRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(listRoot.GetChild(i).gameObject);
            var top = new Vector2(0.5f, 1f);
            if (list.Count == 0)
            {
                UIFactory.Label(listRoot, "No open games found", 28, TextAnchor.MiddleCenter, top, new Vector2(0, -150), new Vector2(900, 50), UIFactory.Dim, FontStyle.Italic);
                return;
            }
            int n = Math.Min(6, list.Count);
            for (int i = 0; i < n; i++)
            {
                var l = list[i];
                var row = UIFactory.Panel(listRoot, top, new Vector2(0, -i * 56), new Vector2(1040, 50), new Color(1, 1, 1, 0.06f));
                string title = l.Name + (string.IsNullOrEmpty(l.Host) ? "" : "   -   " + l.Host);
                UIFactory.Label(row.transform, title, 28, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-190, 0), new Vector2(680, 50));
                UIFactory.Label(row.transform, l.Players + " / " + l.Max, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(180, 0), new Vector2(140, 50));
                UIFactory.Label(row.transform, l.Round > 0 ? l.Round.ToString() : "-", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(300, 0), new Vector2(140, 50));
                var info = l;
                UIFactory.Button(row.transform, "JOIN", new Vector2(0.5f, 0.5f), new Vector2(430, 0), new Vector2(150, 42), () => JoinLobby(info), 26);
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
        }

        // ---------------- actions
        void StartGame() { SceneManager.LoadScene("Game"); }

        void SaveName() { NetSession.PlayerName = nameInput.text; }

        async void HostOnline()
        {
            if (busy) return;
            busy = true; SaveName();
            SetStatus("Creating online room...");
            string room = string.IsNullOrWhiteSpace(roomInput.text) ? NetSession.PlayerName + "'s game" : roomInput.text.Trim();
            string err = await OnlineServices.HostOnlineAsync(room);
            busy = false;
            if (Root == null) return;
            if (err != null) { SetStatus("Could not create an online room: " + err + "\nTry START LAN GAME, or finish the online setup (see the setup notes)."); return; }
            StartGame();
        }

        void HostLan()
        {
            if (busy) return;
            SaveName();
            string err = NetSession.StartHostLan();
            if (err != null) { SetStatus("Could not host: " + err); return; }
            NetSession.RoomName = string.IsNullOrWhiteSpace(roomInput.text) ? NetSession.PlayerName + "'s game" : roomInput.text.Trim();
            StartGame();
        }

        async void JoinLobby(LobbyInfo info)
        {
            if (busy) return;
            busy = true; SaveName();
            SetStatus("Joining " + info.Name + "...");
            string err = await OnlineServices.JoinLobbyAsync(info);
            busy = false;
            if (Root == null) return;
            if (err != null) { SetStatus("Could not join: " + err); nextPoll = 0f; return; }
            StartGame();
        }

        async void JoinCode()
        {
            if (busy) return;
            string code = codeInput.text.Trim();
            if (code.Length < 4) { SetStatus("Type the room code first"); return; }
            busy = true; SaveName();
            SetStatus("Joining room " + code.ToUpperInvariant() + "...");
            string err = await OnlineServices.JoinByCodeAsync(code);
            busy = false;
            if (Root == null) return;
            if (err != null) { SetStatus("Could not join: " + err); return; }
            StartGame();
        }

        async void JoinIp()
        {
            if (busy) return;
            string ip = ipInput.text.Trim();
            if (ip.Length < 3) { SetStatus("Type the host's address first (for example 192.168.0.10)"); return; }
            busy = true; SaveName();
            SetStatus("Connecting to " + ip + "...");
            string err = await NetSession.JoinDirectAsync(ip);
            busy = false;
            if (Root == null) return;
            if (err != null) { SetStatus("Could not connect: " + err); return; }
            NetSession.JoinInfo = ip;
            StartGame();
        }
    }
}
