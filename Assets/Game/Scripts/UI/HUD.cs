using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOTLD
{
    /// <summary>In-game HUD, pause menu and game-over screen (built from code).</summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I;

        Text roundText, pointsText, ammoText, weaponText, promptText, messageText, popupTemplate, grenadeText;
        RectTransform crossRoot, perkRow, popupRoot;
        RectTransform[] crossLines = new RectTransform[4];
        Image hitmarker, damageFlash, scopeOverlay;
        GameObject pausePanel, gameOverPanel;
        Text gameOverText, teamText, rankText;
        float teamT;
        LeaderboardPanel board;
        SettingsPanel settings;
        float hitT, msgT, dmgT, roundFlashT;
        int shownRound;
        readonly List<(Text t, float life)> popups = new List<(Text, float)>();

        void Awake() { I = this; }

        void Start()
        {
            var canvas = UIFactory.MakeCanvas("HUDCanvas", 10);
            var c = canvas.transform;

            // damage flash + scope
            damageFlash = UIFactory.Stretch("DamageFlash", c).gameObject.AddComponent<Image>();
            damageFlash.sprite = MakeVignetteSprite(); damageFlash.type = Image.Type.Simple; damageFlash.color = new Color(0.8f, 0f, 0f, 0f); damageFlash.raycastTarget = false;
            scopeOverlay = UIFactory.Stretch("Scope", c).gameObject.AddComponent<Image>();
            scopeOverlay.sprite = MakeScopeSprite(); scopeOverlay.preserveAspect = false; scopeOverlay.raycastTarget = false;
            scopeOverlay.gameObject.SetActive(false);

            // crosshair
            crossRoot = UIFactory.Rect("Crosshair", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 4; i++)
            {
                var r = UIFactory.Rect("Line", crossRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, i < 2 ? new Vector2(3, 14) : new Vector2(14, 3));
                r.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.85f);
                crossLines[i] = r;
            }
            var hm = UIFactory.Rect("Hitmarker", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
            for (int i = 0; i < 4; i++)
            {
                var l = UIFactory.Rect("H", hm, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i % 2 == 0 ? -10 : 10, i < 2 ? 10 : -10), new Vector2(3, 14));
                l.localRotation = Quaternion.Euler(0, 0, (i == 0 || i == 3) ? -45 : 45);
                l.gameObject.AddComponent<Image>().color = Color.white;
            }
            hitmarker = hm.gameObject.AddComponent<Image>(); hitmarker.color = new Color(1, 1, 1, 0);
            hm.gameObject.SetActive(false);

            // round counter (bottom left, big)
            roundText = UIFactory.Label(c, "", 140, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(160, 110), new Vector2(300, 180), new Color(0.85f, 0.85f, 0.85f), FontStyle.Bold);
            // perks above round
            perkRow = UIFactory.Rect("Perks", c, Vector2.zero, Vector2.zero, new Vector2(240, 250), new Vector2(400, 60));

            // points + ammo (bottom right)
            pointsText = UIFactory.Label(c, "500", 52, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(-170, 260), new Vector2(300, 60), Color.white, FontStyle.Bold);
            popupRoot = UIFactory.Rect("Popups", c, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-170, 300), new Vector2(300, 60));
            ammoText = UIFactory.Label(c, "", 48, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(-170, 110), new Vector2(300, 60), Color.white, FontStyle.Bold);
            weaponText = UIFactory.Label(c, "", 28, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(-170, 160), new Vector2(300, 40), UIFactory.Dim);
            grenadeText = UIFactory.Label(c, "", 28, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(-170, 62), new Vector2(380, 40), UIFactory.Dim);

            // prompt + messages
            promptText = UIFactory.Label(c, "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(1400, 50));
            messageText = UIFactory.Label(c, "", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1400, 90), Color.white, FontStyle.Bold);

            teamText = UIFactory.Label(c, "", 26, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -30), new Vector2(760, 220), UIFactory.Bone);
            BuildPause(c);
            BuildGameOver(c);
            RefreshPerks();

            if (GameManager.I != null)
            {
                GameManager.I.OnPointsChanged += d => SpawnPopup(d);
                GameManager.I.OnRoundChanged += r => { roundFlashT = 3f; };
            }
        }

        // Red radial vignette: transparent in the middle, red toward the screen edges (drawn as UI so it stays red under the black & white filter)
        static Sprite MakeVignetteSprite()
        {
            int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.05f, d));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeScopeSprite()
        {
            int s = 512;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = d > 0.92f ? (byte)255 : (byte)0;
                    if (d <= 0.92f && (Mathf.Abs(x - s / 2) < 1 || Mathf.Abs(y - s / 2) < 1)) a = 230;
                    if (d <= 0.92f && d > 0.88f) a = (byte)(255 * (d - 0.88f) / 0.04f);
                    px[y * s + x] = new Color32(0, 0, 0, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        void BuildPause(Transform c)
        {
            var bg = UIFactory.Stretch("Pause", c).gameObject;
            bg.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);
            pausePanel = bg;
            var t = bg.transform;
            var menu = UIFactory.Rect("Menu", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 600)).gameObject;
            UIFactory.Label(menu.transform, "PAUSED", 80, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(600, 100), null, FontStyle.Bold);
            UIFactory.Button(menu.transform, "RESUME", new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(380, 70), () => TogglePause());
            UIFactory.Button(menu.transform, "SETTINGS", new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(380, 70), () => { menu.SetActive(false); settings.Show(); });
            UIFactory.Button(menu.transform, "LEADERBOARD", new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(380, 70), () => { menu.SetActive(false); board.Show(); });
            var restartBtn = UIFactory.Button(menu.transform, "RESTART", new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(380, 70), GameManager.Restart);
            var menuBtn = UIFactory.Button(menu.transform, "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(380, 70), GameManager.LoadMenu);
            if (NetSession.IsOnline)
            {
                restartBtn.gameObject.SetActive(false);
                ((RectTransform)menuBtn.transform).anchoredPosition = new Vector2(0, -210);
                UIFactory.Label(menu.transform, (string.IsNullOrEmpty(NetSession.RoomName) ? "" : NetSession.RoomName + "   -   ") + NetSession.JoinInfo, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -125), new Vector2(900, 40), UIFactory.Dim);
            }
            settings = new SettingsPanel(t, () => { menu.SetActive(true); SelectFirst(menu.transform); });
            board = new LeaderboardPanel(t, () => { menu.SetActive(true); SelectFirst(menu.transform); });
            pausePanel.SetActive(false);
        }

        void BuildGameOver(Transform c)
        {
            var bg = UIFactory.Stretch("GameOver", c).gameObject;
            bg.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            gameOverPanel = bg;
            UIFactory.Label(bg.transform, "GAME OVER", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1200, 140), Color.white, FontStyle.Bold);
            gameOverText = UIFactory.Label(bg.transform, "", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1200, 120));
            rankText = UIFactory.Label(bg.transform, "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(1200, 50), UIFactory.Dim);
            var again = UIFactory.Button(bg.transform, "PLAY AGAIN", new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(380, 70), GameManager.Restart);
            var goMenu = UIFactory.Button(bg.transform, "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(380, 70), GameManager.LoadMenu);
            if (NetSession.IsOnline) { again.gameObject.SetActive(false); ((RectTransform)goMenu.transform).anchoredPosition = new Vector2(0, -90); }
            gameOverPanel.SetActive(false);
        }

        public void TogglePause()
        {
            var gm = GameManager.I;
            if (gm == null || gm.GameOver) return;
            bool p = !gm.Paused;
            gm.SetPaused(p);
            pausePanel.SetActive(p);
            if (p) { pausePanel.transform.Find("Menu").gameObject.SetActive(true); settings.Root.SetActive(false); board.Root.SetActive(false); SelectFirst(pausePanel.transform.Find("Menu")); }
        }

        static void SelectFirst(Transform root)
        {
            var b = root != null ? root.GetComponentInChildren<Button>() : null;
            if (b != null && UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        public void ShowGameOver(int round, int kills)
        {
            gameOverPanel.SetActive(true);
            SelectFirst(gameOverPanel.transform);
            gameOverText.text = $"You survived {round} round{(round == 1 ? "" : "s")}\nKills: {kills}";
            FetchRank();
        }

        async void FetchRank()
        {
            rankText.text = "Submitting score...";
            try
            {
                await System.Threading.Tasks.Task.Delay(1800);
                var mine = await OnlineServices.FetchMineAsync();
                if (this == null) return;
                rankText.text = mine.HasValue ? $"WORLD RANK  #{mine.Value.Rank}      BEST ROUND  {mine.Value.Score}" : "Leaderboard offline - score saved on this device";
            }
            catch (System.Exception) { if (this != null) rankText.text = ""; }
        }

        void UpdateTeam(float dt)
        {
            if (teamText == null) return;
            if (!NetSession.IsOnline) { if (teamText.text.Length > 0) teamText.text = ""; return; }
            teamT -= dt; if (teamT > 0f) return; teamT = 0.25f;
            var gm = GameManager.I; var p = PlayerController.I;
            var list = new List<Peer>(NetSession.Peers.Values);
            list.Sort((a, b) => a.Index.CompareTo(b.Index));
            var sb = new System.Text.StringBuilder();
            foreach (var peer in list)
            {
                bool me = peer.Id == NetSession.LocalId;
                int pts = me ? gm.Points : peer.State.Points;
                bool doomed = me ? (p != null && p.IsDoomed) : (peer.State.Flags & NetSession.FDoomed) != 0;
                bool dead = me ? (p != null && p.IsDead) : (peer.State.Flags & NetSession.FDead) != 0;
                sb.Append(me ? "> " : "   ").Append(peer.Name).Append("    ").Append(pts);
                if (dead) sb.Append("    DEAD"); else if (doomed) sb.Append("    DOWN");
                sb.Append('\n');
            }
            teamText.text = sb.ToString();
        }

        public void ShowMessage(string m, float time) { messageText.text = m; msgT = time; }
        public void Hitmarker(bool kill) { hitT = kill ? 0.25f : 0.15f; SFX.Play("hitmarker", kill ? 0.5f : 0.3f, kill ? 0.8f : 1f); }
        public void FlashDamage() { dmgT = 0.5f; }

        public void RefreshPerks()
        {
            if (perkRow == null) return;
            foreach (Transform ch in perkRow) Destroy(ch.gameObject);
            var p = PlayerController.I;
            if (p == null) return;
            for (int i = 0; i < p.Perks.Count; i++)
            {
                var box = UIFactory.Panel(perkRow, new Vector2(0, 0.5f), new Vector2(-170 + i * 64, 0), new Vector2(56, 56), new Color(0.9f, 0.9f, 0.9f, 0.9f));
                UIFactory.Label(box.transform, PerkMachine.PerkShort(p.Perks[i]), 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56), Color.black, FontStyle.Bold);
            }
        }

        void SpawnPopup(int delta)
        {
            if (popupRoot == null) return;
            var t = UIFactory.Label(popupRoot, (delta > 0 ? "+" : "") + delta, 34, TextAnchor.MiddleRight, new Vector2(0.5f, 0.5f), new Vector2(Random.Range(-40, 0), 0), new Vector2(300, 40), delta > 0 ? Color.white : UIFactory.Dim, FontStyle.Bold);
            popups.Add((t, 0f));
        }

        void Update()
        {
            var gm = GameManager.I; var p = PlayerController.I;
            if (gm == null || p == null) return;
            float dt = Time.unscaledDeltaTime;

            // round number with flash on change
            if (gm.Round != shownRound) { shownRound = gm.Round; }
            roundText.text = gm.Round > 0 ? gm.Round.ToString() : "";
            if (roundFlashT > 0)
            {
                roundFlashT -= dt;
                float a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(roundFlashT * 4f));
                roundText.color = new Color(1, 1, 1, a);
            }
            else roundText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            pointsText.text = gm.Points.ToString();
            UpdateTeam(dt);
            var gt = GrenadeThrower.I;
            if (gt != null) { grenadeText.text = (GameInput.UsingGamepad ? "[D-PAD UP] " : "[G] ") + "GRENADE x" + gt.Count; grenadeText.color = gt.Count > 0 ? Color.white : UIFactory.Dim; }
            var w = p.Weapons;
            if (w != null && w.Cur != null)
            {
                ammoText.text = $"{w.Cur.Mag} / {w.Cur.Reserve}";
                ammoText.color = w.Cur.Mag == 0 ? UIFactory.Dim : Color.white;
                weaponText.text = w.Cur.Def.Name + (w.IsReloading ? "  -  RELOADING" : "");

                // crosshair
                bool showCross = !p.IsSprinting && (w.AdsT < 0.5f || w.Cur.Def.Class == WeaponClass.Shotgun) && !w.ScopedIn && !p.IsDown;
                crossRoot.gameObject.SetActive(showCross);
                float gap = 8f + w.CurrentSpread * 9f;
                crossLines[0].anchoredPosition = new Vector2(0, gap + 7);
                crossLines[1].anchoredPosition = new Vector2(0, -gap - 7);
                crossLines[2].anchoredPosition = new Vector2(gap + 7, 0);
                crossLines[3].anchoredPosition = new Vector2(-gap - 7, 0);
                scopeOverlay.gameObject.SetActive(w.ScopedIn);
            }

            // prompt
            var f = p.Focus;
            string prompt = f != null && !p.IsDown ? f.Prompt(p) : null;
            if (prompt != null)
            {
                prompt = prompt.Replace("{USE}", GameInput.UseLabel);
                int cost = f.PriceFor(p);
                if (cost > 0 && gm.Points < cost) prompt += "   (not enough points)";
            }
            promptText.text = prompt ?? "";

            // hitmarker
            if (hitT > 0) { hitT -= dt; hitmarker.transform.gameObject.SetActive(true); }
            else hitmarker.transform.gameObject.SetActive(false);

            // message
            if (msgT > 0) { msgT -= dt; if (msgT <= 0) messageText.text = ""; }

            // damage
            if (dmgT > 0) dmgT -= dt;
            damageFlash.color = new Color(0.8f, 0f, 0f, Mathf.Clamp01(Mathf.Max(dmgT * 1.6f, p.HurtAmount * 0.9f, p.IsDown ? 0.7f : 0f)));

            // popups
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (t, life) = popups[i];
                life += dt;
                if (t == null) { popups.RemoveAt(i); continue; }
                var rt = (RectTransform)t.transform;
                rt.anchoredPosition += new Vector2(-20f * dt, 60f * dt);
                t.color = new Color(t.color.r, t.color.g, t.color.b, 1f - life / 1f);
                popups[i] = (t, life);
                if (life >= 1f) { Destroy(t.gameObject); popups.RemoveAt(i); }
            }
        }
    }
}
