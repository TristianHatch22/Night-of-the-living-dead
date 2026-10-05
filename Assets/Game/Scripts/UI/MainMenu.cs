using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NOTLD
{
    /// <summary>"Night of the Living Dead" title screen with Play / Settings / Quit.</summary>
    public class MainMenu : MonoBehaviour
    {
        Text title, sub;
        GameObject mainButtons;
        SettingsPanel settings;
        CoopPanel coop;
        LeaderboardPanel board;
        Camera cam;
        float camT;
        Transform[] shamblers;

        void Start()
        {
            Time.timeScale = 1f;
            if (NetSession.IsOnline) NetSession.Leave();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.Load();
            BuildBackground();

            var canvas = UIFactory.MakeCanvas("MenuCanvas", 10);
            var c = canvas.transform;
            var shade = UIFactory.Stretch("Shade", c).gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, 0.35f); shade.raycastTarget = false;

            title = UIFactory.Label(c, "NIGHT OF THE\nLIVING DEAD", 120, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(560, -260), new Vector2(1000, 300), Color.white, FontStyle.Bold);
            title.lineSpacing = 0.85f;
            sub = UIFactory.Label(c, "a one-room survival challenge", 30, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(560, -440), new Vector2(1000, 50), UIFactory.Dim, FontStyle.Italic);

            mainButtons = UIFactory.Rect("Buttons", c, new Vector2(0, 0), new Vector2(0, 0), new Vector2(330, 330), new Vector2(460, 520)).gameObject;
            var b = mainButtons.transform;
            var playBtn = UIFactory.Button(b, "SOLO", new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(420, 76), StartSolo, 42);
            UIFactory.Button(b, "CO-OP", new Vector2(0.5f, 1f), new Vector2(0, -125), new Vector2(420, 76), () => { mainButtons.SetActive(false); coop.Show(); }, 42);
            UIFactory.Button(b, "LEADERBOARD", new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(420, 76), () => { mainButtons.SetActive(false); board.Show(); }, 42);
            UIFactory.Button(b, "SETTINGS", new Vector2(0.5f, 1f), new Vector2(0, -295), new Vector2(420, 76), () => { mainButtons.SetActive(false); settings.Show(); }, 42);
            UIFactory.Button(b, "QUIT", new Vector2(0.5f, 1f), new Vector2(0, -380), new Vector2(420, 76), Quit, 42);

            if (!string.IsNullOrEmpty(NetSession.StatusMessage))
            {
                UIFactory.Label(c, NetSession.StatusMessage, 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1400, 60), new Color(1f, 0.55f, 0.5f), FontStyle.Bold);
                NetSession.StatusMessage = null;
            }

            UIFactory.Label(c, "KEYBOARD:  WASD move   SHIFT sprint   SPACE jump   LMB fire   RMB aim   R reload   V knife   F buy / rebuild   1-2 / wheel swap   ESC pause\nCONTROLLER:  LS move   RS look   RT fire   LT aim   A jump   X reload   Y swap   B use   RS-click knife   LS-click sprint   START pause",
                22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1900, 80), UIFactory.Dim);

            System.Action backToMenu = () => { mainButtons.SetActive(true); UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playBtn.gameObject); };
            settings = new SettingsPanel(c, backToMenu);
            coop = new CoopPanel(c, backToMenu);
            board = new LeaderboardPanel(c, backToMenu);
            GameSettings.ApplyAll();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playBtn.gameObject);
            SFX.Play("round_start", 0.6f, 0.8f);
        }

        void StartSolo()
        {
            NetSession.Leave();
            SceneManager.LoadScene("Game");
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void BuildBackground()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera"); go.tag = "MainCamera";
                cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 1.6f, -6f);

            var ground = Mats.Get("menu_ground", Mats.Gray(0.15f), 0.05f);
            var dark = Mats.Get("menu_dark", Mats.Gray(0.05f), 0.1f);
            Mats.Prim(PrimitiveType.Cube, null, new Vector3(0, -0.1f, 10), new Vector3(60, 0.2f, 60), ground);
            // house silhouette
            Mats.Prim(PrimitiveType.Cube, null, new Vector3(6, 3, 18), new Vector3(10, 6, 6), dark);
            Mats.Prim(PrimitiveType.Cube, null, new Vector3(6, 7, 18), new Vector3(7.5f, 7.5f, 6), dark, new Vector3(0, 0, 45));
            var win = Mats.Get("menu_window", Color.white, 0.5f, 0, new Color(3, 3, 3));
            Mats.Prim(PrimitiveType.Cube, null, new Vector3(4, 3, 14.95f), new Vector3(1.2f, 1.4f, 0.05f), win);
            Mats.Prim(PrimitiveType.Cube, null, new Vector3(8, 3, 14.95f), new Vector3(1.2f, 1.4f, 0.05f), win);
            // trees
            for (int i = 0; i < 8; i++)
            {
                float x = -14 + i * 4 + Random.Range(-1f, 1f);
                if (x > 0 && x < 12) continue;
                float h = Random.Range(4f, 8f);
                Mats.Prim(PrimitiveType.Cylinder, null, new Vector3(x, h / 2, Random.Range(12f, 22f)), new Vector3(0.4f, h / 2, 0.4f), dark);
            }
            // shambling zombie silhouettes
            shamblers = new Transform[5];
            for (int i = 0; i < shamblers.Length; i++)
            {
                var z = new GameObject("Shambler").transform;
                z.position = new Vector3(-6 + i * 3 + Random.Range(-0.8f, 0.8f), 0, 6 + Random.Range(0f, 6f));
                z.rotation = Quaternion.Euler(0, 180 + Random.Range(-20f, 20f), 0);
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(0, 1.2f, 0), new Vector3(0.45f, 0.6f, 0.25f), dark);
                Mats.Prim(PrimitiveType.Sphere, z, new Vector3(0, 1.7f, 0.05f), new Vector3(0.27f, 0.3f, 0.28f), dark);
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(-0.11f, 0.45f, 0), new Vector3(0.15f, 0.9f, 0.15f), dark);
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(0.11f, 0.45f, 0), new Vector3(0.15f, 0.9f, 0.15f), dark);
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(-0.28f, 1.45f, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), dark);
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(0.28f, 1.45f, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), dark);
                var eye = Mats.Get("z_eye", Color.white, 0.5f, 0, new Color(4f, 4f, 4f));
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(-0.05f, 1.73f, 0.19f), new Vector3(0.04f, 0.02f, 0.01f), eye);
                Mats.Prim(PrimitiveType.Cube, z, new Vector3(0.05f, 1.73f, 0.19f), new Vector3(0.04f, 0.02f, 0.01f), eye);
                shamblers[i] = z;
            }
            // moon + fog
            var moonGo = new GameObject("Moon");
            moonGo.transform.rotation = Quaternion.Euler(20, 160, 0);
            var moon = moonGo.AddComponent<Light>(); moon.type = LightType.Directional; moon.intensity = 0.6f; moon.shadows = LightShadows.Soft;
            var lampGo = new GameObject("Deco_Lamp"); lampGo.transform.position = new Vector3(-3, 4, 4);
            var lamp = lampGo.AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 14; lamp.intensity = 2.5f;
            lampGo.AddComponent<LightFlicker>();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Mats.Gray(0.08f);
        }

        void Update()
        {
            camT += Time.deltaTime;
            if (cam)
            {
                cam.transform.position = new Vector3(Mathf.Sin(camT * 0.1f) * 1.5f, 1.6f + Mathf.Sin(camT * 0.3f) * 0.1f, -6f);
                cam.transform.rotation = Quaternion.Euler(-2f, Mathf.Sin(camT * 0.1f) * -5f, 0);
            }
            if (shamblers != null)
                foreach (var z in shamblers)
                {
                    z.position += z.forward * -0.0f + new Vector3(0, 0, -0.25f * Time.deltaTime);
                    if (z.position.z < -2f) z.position = new Vector3(z.position.x, 0, 14f);
                    z.rotation = Quaternion.Euler(Mathf.Sin(camT * 2f + z.position.x) * 4f, 180, Mathf.Sin(camT * 1.5f + z.position.x) * 5f);
                }
            // flickering title
            if (title)
            {
                float a = Random.value < 0.03f ? 0.4f : 1f;
                title.color = new Color(1, 1, 1, a);
            }
        }
    }
}
