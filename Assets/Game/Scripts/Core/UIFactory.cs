using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NOTLD
{
    /// <summary>Small helpers for building uGUI from code.</summary>
    public static class UIFactory
    {
        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static readonly Color Bone = new Color(0.92f, 0.92f, 0.88f);
        public static readonly Color Dim = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color PanelBg = new Color(0.02f, 0.02f, 0.02f, 1f);

        public static Canvas MakeCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // mouse + gamepad menu navigation
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2((anchorMin.x + anchorMax.x) / 2f, (anchorMin.y + anchorMax.y) / 2f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent, anchor, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.alignment = align; t.fontStyle = style;
            t.color = color ?? Bone;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.9f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Image Panel(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect("Panel", parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Button Button(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, int fontSize = 34)
        {
            var rt = Rect("Button_" + text, parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.04f);
            var b = rt.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = new Color(1, 1, 1, 0.05f);
            cb.highlightedColor = new Color(1, 1, 1, 0.22f);
            cb.pressedColor = new Color(1, 1, 1, 0.4f);
            cb.selectedColor = new Color(1, 1, 1, 0.12f);
            b.colors = cb;
            b.onClick.AddListener(() => { SFX.Play("click", 0.6f); onClick?.Invoke(); });
            var t = Label(rt, text, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.raycastTarget = false;
            return b;
        }

        public static InputField Input(Transform parent, string text, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, int limit)
        {
            var rt = Rect("Input", parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.10f);
            var field = rt.gameObject.AddComponent<InputField>();
            var inner = new Vector2(Mathf.Max(10, size.x - 24), size.y);
            var txt = Label(rt, "", fontSize, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, inner, Color.white);
            txt.supportRichText = false;
            var ph = Label(rt, placeholder, fontSize, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, inner, Dim, FontStyle.Italic);
            field.textComponent = txt; field.placeholder = ph; field.targetGraphic = img;
            field.characterLimit = limit;
            field.text = text;
            return field;
        }

        public static Slider Slider(Transform parent, Vector2 anchor, Vector2 pos, float width, float min, float max, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent, anchor, anchor, pos, new Vector2(width, 26));
            var bg = Rect("Bg", root, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            bg.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            var fillArea = Rect("FillArea", root, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            var handleArea = Rect("HandleArea", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, 0));
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(20, 0));
            var himg = handle.gameObject.AddComponent<Image>();
            himg.color = Color.white;
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = himg;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }
    }

    /// <summary>Settings panel shared by the main menu and pause menu.</summary>
    public class SettingsPanel
    {
        public GameObject Root;
        Text presetText, sensText, fovText, volText;
        InputField nameInput;
        readonly Action onBack;

        public SettingsPanel(Transform parent, Action onBack)
        {
            this.onBack = onBack;
            GameSettings.Load();
            var bg = UIFactory.Panel(parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 720), UIFactory.PanelBg);
            Root = bg.gameObject;
            var t = bg.transform;
            UIFactory.Label(t, "SETTINGS", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(800, 80), null, FontStyle.Bold);

            // Graphics preset (all-in-one)
            UIFactory.Label(t, "GRAPHICS", 32, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-220, -170), new Vector2(360, 50));
            UIFactory.Button(t, "<", new Vector2(0.5f, 1f), new Vector2(60, -170), new Vector2(60, 50), () => ChangePreset(-1));
            presetText = UIFactory.Label(t, "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(200, -170), new Vector2(200, 50), null, FontStyle.Bold);
            UIFactory.Button(t, ">", new Vector2(0.5f, 1f), new Vector2(340, -170), new Vector2(60, 50), () => ChangePreset(1));

            UIFactory.Label(t, "MOUSE SENSITIVITY", 28, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-220, -260), new Vector2(360, 50));
            UIFactory.Slider(t, new Vector2(0.5f, 1f), new Vector2(180, -260), 260, 0.1f, 3f, GameSettings.Sensitivity, v => { GameSettings.Sensitivity = v; Refresh(); });
            sensText = UIFactory.Label(t, "", 26, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(380, -260), new Vector2(100, 50));

            UIFactory.Label(t, "FIELD OF VIEW", 28, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-220, -340), new Vector2(360, 50));
            UIFactory.Slider(t, new Vector2(0.5f, 1f), new Vector2(180, -340), 260, 60f, 100f, GameSettings.FieldOfView, v => { GameSettings.FieldOfView = Mathf.Round(v); Refresh(); GameSettings.ApplyGraphics(); });
            fovText = UIFactory.Label(t, "", 26, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(380, -340), new Vector2(100, 50));

            UIFactory.Label(t, "MASTER VOLUME", 28, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-220, -420), new Vector2(360, 50));
            UIFactory.Slider(t, new Vector2(0.5f, 1f), new Vector2(180, -420), 260, 0f, 1f, GameSettings.Volume, v => { GameSettings.Volume = v; AudioListener.volume = v; Refresh(); });
            volText = UIFactory.Label(t, "", 26, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(380, -420), new Vector2(100, 50));

            UIFactory.Label(t, "PLAYER NAME", 28, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-220, -505), new Vector2(360, 50));
            nameInput = UIFactory.Input(t, NetSession.PlayerName, "Your name", new Vector2(0.5f, 1f), new Vector2(210, -505), new Vector2(300, 50), 28, 16);
            nameInput.onEndEdit.AddListener(v => CommitName());

            UIFactory.Button(t, "BACK", new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(300, 70), () => { CommitName(); GameSettings.Save(); Root.SetActive(false); this.onBack?.Invoke(); });
            Refresh();
            Root.SetActive(false);
        }

        /// <summary>Saves the name and pushes it to the online profile so the leaderboard shows it.</summary>
        void CommitName()
        {
            NetSession.PlayerName = nameInput.text;
            nameInput.text = NetSession.PlayerName;
            _ = OnlineServices.ApplyNameAsync();
        }

        void ChangePreset(int dir)
        {
            int p = Mathf.Clamp((int)GameSettings.Preset + dir, 0, 3);
            GameSettings.Preset = (GraphicsPreset)p;
            GameSettings.ApplyGraphics();
            GameSettings.Save();
            Refresh();
        }

        void Refresh()
        {
            presetText.text = GameSettings.PresetNames[(int)GameSettings.Preset];
            sensText.text = GameSettings.Sensitivity.ToString("0.00");
            fovText.text = GameSettings.FieldOfView.ToString("0");
            volText.text = Mathf.RoundToInt(GameSettings.Volume * 100) + "%";
        }

        public void Show()
        {
            nameInput.text = NetSession.PlayerName;
            Refresh();
            Root.SetActive(true);
            var first = Root.GetComponentInChildren<UnityEngine.UI.Button>();
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }
}
