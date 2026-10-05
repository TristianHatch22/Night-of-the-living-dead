using UnityEngine;

namespace NOTLD
{
    /// <summary>
    /// Builds the one-room gas-station challenge map: a single window on the north wall,
    /// 4 slushy perk machines, and the cardboard mystery box. Run once in the editor (saved into the scene).
    /// </summary>
    public static class MapBuilder
    {
        static Transform root;
        static Material concrete, tileA, tileB, wall, wallDark, trim, wood, dirt, cardboard, tape, white, chrome, glass, black, fence, bark;

        static void InitMats()
        {
            concrete = Mats.Get("map_concrete", Mats.Gray(0.3f), 0.15f);
            tileA = Mats.Get("map_tileA", Mats.Gray(0.42f), 0.45f);
            tileB = Mats.Get("map_tileB", Mats.Gray(0.3f), 0.45f);
            wall = Mats.Get("map_wall", Mats.Gray(0.55f), 0.1f);
            wallDark = Mats.Get("map_wallDark", Mats.Gray(0.25f), 0.1f);
            trim = Mats.Get("map_trim", Mats.Gray(0.12f), 0.3f);
            wood = Mats.Get("map_board", Mats.Gray(0.38f), 0.15f);
            dirt = Mats.Get("map_dirt", Mats.Gray(0.16f), 0.05f);
            cardboard = Mats.Get("map_cardboard", Mats.Gray(0.52f), 0.05f);
            tape = Mats.Get("map_tape", Mats.Gray(0.75f), 0.6f);
            white = Mats.Get("map_white", Mats.Gray(0.8f), 0.5f);
            chrome = Mats.Get("map_chrome", Mats.Gray(0.6f), 0.85f, 1f);
            glass = Mats.Get("map_glass", new Color(0.85f, 0.85f, 0.85f, 0.3f), 0.95f, 0, null, true);
            black = Mats.Get("map_black", Mats.Gray(0.04f), 0.3f);
            fence = Mats.Get("map_fence", Mats.Gray(0.1f), 0.2f);
            bark = Mats.Get("map_bark", Mats.Gray(0.07f), 0.05f);
        }

        static GameObject Box(string name, Vector3 pos, Vector3 size, Material m, bool col = true, Transform parent = null, Vector3? euler = null)
            => Mats.Prim(PrimitiveType.Cube, parent ?? root, pos, size, m, euler, col, name);

        public static GameObject Build()
        {
            var old = GameObject.Find("Map");
            if (old) Object.DestroyImmediate(old);
            InitMats();
            root = new GameObject("Map").transform;

            const float H = 4f, T = 0.3f, HALF = 6f;

            // ---------- floor (checker tiles) + ceiling ----------
            Box("FloorBase", new Vector3(0, -0.15f, 0), new Vector3(HALF * 2 + T * 2, 0.3f, HALF * 2 + T * 2), concrete);
            var tiles = new GameObject("Tiles").transform; tiles.SetParent(root, false);
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < 6; z++)
                    Box("Tile", new Vector3(-5 + x * 2, 0.005f, -5 + z * 2), new Vector3(1.98f, 0.01f, 1.98f), (x + z) % 2 == 0 ? tileA : tileB, false, tiles);
            Box("Ceiling", new Vector3(0, H + 0.15f, 0), new Vector3(HALF * 2 + T * 2, 0.3f, HALF * 2 + T * 2), wallDark);
            for (int i = -2; i <= 2; i++) Box("CeilingBeam", new Vector3(i * 2.4f, H - 0.1f, 0), new Vector3(0.2f, 0.2f, HALF * 2), trim, false);

            // ---------- walls ----------
            Box("Wall_S", new Vector3(0, H / 2, -HALF - T / 2), new Vector3(HALF * 2 + T * 2, H, T), wall);
            Box("Wall_E", new Vector3(HALF + T / 2, H / 2, 0), new Vector3(T, H, HALF * 2), wall);
            Box("Wall_W", new Vector3(-HALF - T / 2, H / 2, 0), new Vector3(T, H, HALF * 2), wall);
            // north wall with a single window (x -1..1, y 1..2.4)
            float wz = HALF + T / 2;
            Box("Wall_N_L", new Vector3(-3.65f, H / 2, wz), new Vector3(5.3f + T, H, T), wall);
            Box("Wall_N_R", new Vector3(3.65f, H / 2, wz), new Vector3(5.3f + T, H, T), wall);
            Box("Wall_N_Low", new Vector3(0, 0.5f, wz), new Vector3(2f, 1f, T), wall);
            Box("Wall_N_High", new Vector3(0, 3.2f, wz), new Vector3(2f, 1.6f, T), wall);
            // wainscot strip around the room
            Box("Wainscot_S", new Vector3(0, 0.5f, -HALF + 0.01f), new Vector3(HALF * 2, 1f, 0.02f), wallDark, false);
            Box("Wainscot_E", new Vector3(HALF - 0.01f, 0.5f, 0), new Vector3(0.02f, 1f, HALF * 2), wallDark, false);
            Box("Wainscot_W", new Vector3(-HALF + 0.01f, 0.5f, 0), new Vector3(0.02f, 1f, HALF * 2), wallDark, false);
            Box("Wainscot_NL", new Vector3(-3.5f, 0.5f, HALF - 0.01f), new Vector3(5f, 1f, 0.02f), wallDark, false);
            Box("Wainscot_NR", new Vector3(3.5f, 0.5f, HALF - 0.01f), new Vector3(5f, 1f, 0.02f), wallDark, false);

            BuildWindow(wz);
            BuildOutside();
            BuildLights(H);
            BuildProps();

            // ---------- perks (slushy machines) + box ----------
            BuildPerk(Perk.Juggernog, new Vector3(5.45f, 0, -3f), -90, 0.32f);
            BuildPerk(Perk.SpeedCola, new Vector3(5.45f, 0, 2.2f), -90, 0.58f);
            BuildPerk(Perk.DoubleTap, new Vector3(-5.45f, 0, -3f), 90, 0.14f);
            BuildPerk(Perk.QuickRevive, new Vector3(-5.45f, 0, 2.2f), 90, 0.88f);
            BuildBox(new Vector3(0, 0, -5.45f), 0);

            // ---------- wall buys ----------
            BuildWallBuy("Thompson", 1200, new Vector3(5.98f, 1.5f, -0.4f), -90f);   // east wall
            BuildWallBuy("Olympia", 500, new Vector3(-5.98f, 1.5f, -0.4f), 90f);    // west wall
            BuildWallBuy("M14", 500, new Vector3(-3.5f, 1.5f, 5.98f), 180f);        // window wall

            var ps = new GameObject("PlayerSpawn").transform;
            ps.SetParent(root, false);
            ps.localPosition = new Vector3(0, 0.05f, -2f);
            return root.gameObject;
        }

        // ================================================================ WINDOW
        static void BuildWindow(float wz)
        {
            var w = new GameObject("Window").transform;
            w.SetParent(root, false);
            w.localPosition = new Vector3(0, 0, wz);
            Box("Frame_Top", new Vector3(0, 2.45f, 0), new Vector3(2.2f, 0.1f, 0.4f), trim, false, w);
            Box("Frame_Bottom", new Vector3(0, 0.98f, 0), new Vector3(2.3f, 0.06f, 0.5f), trim, false, w);
            Box("Frame_L", new Vector3(-1.05f, 1.7f, 0), new Vector3(0.1f, 1.5f, 0.4f), trim, false, w);
            Box("Frame_R", new Vector3(1.05f, 1.7f, 0), new Vector3(0.1f, 1.5f, 0.4f), trim, false, w);
            // broken glass shards stuck in the frame
            Box("Shard", new Vector3(-0.85f, 2.25f, 0.05f), new Vector3(0.3f, 0.25f, 0.01f), glass, false, w, new Vector3(0, 0, 20));
            Box("Shard", new Vector3(0.8f, 1.15f, 0.05f), new Vector3(0.35f, 0.2f, 0.01f), glass, false, w, new Vector3(0, 0, -15));

            // invisible blocker: stops the player (and nothing else gets through except climbing zombies)
            var blocker = Box("WindowBlocker", new Vector3(0, 1.75f, 0), new Vector3(2f, 1.5f, 0.3f), trim, true, w);
            Object.DestroyImmediate(blocker.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(blocker.GetComponent<MeshFilter>());
            blocker.layer = 2; // Ignore Raycast: bullets pass through the opening

            var boards = new GameObject("Boards").transform;
            boards.SetParent(w, false);
            var rng = new System.Random(42);
            for (int i = 0; i < Barricade.MaxBoards; i++)
            {
                float y = 1.12f + i * 0.23f;
                float rz = (float)(rng.NextDouble() * 16 - 8);
                var b = Box("Board" + i, new Vector3((float)(rng.NextDouble() * 0.2 - 0.1), y, 0.24f), new Vector3(2.4f, 0.17f, 0.04f), wood, false, boards, new Vector3(0, 0, rz));
                // nails
                Box("Nail", new Vector3(-0.45f, 0, 0.6f), new Vector3(0.02f, 0.15f, 0.5f), black, false, b.transform);
                Box("Nail", new Vector3(0.45f, 0, 0.6f), new Vector3(0.02f, 0.15f, 0.5f), black, false, b.transform);
            }

            var outside = new GameObject("OutsidePoint").transform; outside.SetParent(w, false); outside.localPosition = new Vector3(0, 0, 1.0f);
            var inside = new GameObject("InsidePoint").transform; inside.SetParent(w, false); inside.localPosition = new Vector3(0, 0.02f, -1.1f);
            var sill = new GameObject("SillPoint").transform; sill.SetParent(w, false); sill.localPosition = new Vector3(0, 1.05f, 0);
            w.gameObject.AddComponent<Barricade>();
        }

        // ================================================================ OUTSIDE (zombie yard)
        static void BuildOutside()
        {
            var o = new GameObject("Outside").transform; o.SetParent(root, false);
            Box("Ground", new Vector3(0, -0.15f, 12f), new Vector3(20f, 0.3f, 12f), dirt, true, o);
            // fence around the yard
            for (int i = -9; i <= 9; i += 1)
            {
                Box("FencePost", new Vector3(i, 1f, 17.8f), new Vector3(0.12f, 2f, 0.12f), fence, true, o);
            }
            Box("FenceRail", new Vector3(0, 1.6f, 17.8f), new Vector3(18f, 0.1f, 0.06f), fence, true, o);
            Box("FenceRail", new Vector3(0, 0.6f, 17.8f), new Vector3(18f, 0.1f, 0.06f), fence, true, o);
            Box("YardWall_W", new Vector3(-9.5f, 1.5f, 12f), new Vector3(0.3f, 3f, 12f), wallDark, true, o);
            Box("YardWall_E", new Vector3(9.5f, 1.5f, 12f), new Vector3(0.3f, 3f, 12f), wallDark, true, o);
            Box("YardWall_N", new Vector3(0, 1f, 18.2f), new Vector3(19f, 2f, 0.3f), fence, true, o);

            // dead trees
            var rng = new System.Random(7);
            Vector3[] trees = { new Vector3(-6.5f, 0, 15f), new Vector3(6.8f, 0, 14.5f), new Vector3(-3f, 0, 17f), new Vector3(4f, 0, 16.8f) };
            foreach (var t in trees)
            {
                float h = 3.5f + (float)rng.NextDouble() * 2f;
                Mats.Prim(PrimitiveType.Cylinder, o, t + Vector3.up * h / 2f, new Vector3(0.3f, h / 2f, 0.3f), bark, null, true, "Tree");
                for (int b = 0; b < 3; b++)
                    Mats.Prim(PrimitiveType.Cylinder, o, t + Vector3.up * (h * (0.55f + b * 0.15f)), new Vector3(0.08f, 0.9f, 0.08f), bark, new Vector3((float)rng.NextDouble() * 60 - 30, b * 120, 50), false, "Branch");
            }
            // abandoned car silhouette
            Box("CarBody", new Vector3(-5f, 0.6f, 11f), new Vector3(1.8f, 0.8f, 4f), black, true, o, new Vector3(0, 20, 3));
            Box("CarTop", new Vector3(-5f, 1.25f, 11.2f), new Vector3(1.6f, 0.6f, 2f), black, true, o, new Vector3(0, 20, 3));
            // gas pump
            Box("Pump", new Vector3(5f, 0.9f, 10f), new Vector3(0.7f, 1.8f, 0.5f), white, true, o);
            Box("PumpTop", new Vector3(5f, 1.95f, 10f), new Vector3(0.8f, 0.3f, 0.6f), wallDark, false, o);

            Vector3[] sp = { new Vector3(-6f, 0.1f, 14f), new Vector3(-2.5f, 0.1f, 16f), new Vector3(2.5f, 0.1f, 16f), new Vector3(6.5f, 0.1f, 13f) };
            for (int i = 0; i < sp.Length; i++)
            {
                var s = new GameObject("ZombieSpawn_" + i).transform; s.SetParent(o, false); s.localPosition = sp[i];
            }
        }

        // ================================================================ LIGHTS
        static void BuildLights(float H)
        {
            var lamp = new GameObject("Lamp").transform; lamp.SetParent(root, false);
            lamp.localPosition = new Vector3(0, H, 0);
            Mats.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, -0.35f, 0), new Vector3(0.02f, 0.35f, 0.02f), black);
            Mats.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, -0.72f, 0), new Vector3(0.5f, 0.06f, 0.5f), trim);
            Mats.Prim(PrimitiveType.Sphere, lamp, new Vector3(0, -0.8f, 0), Vector3.one * 0.14f, Mats.Get("map_bulb", Color.white, 0.5f, 0, new Color(6, 6, 6)));
            var keyGo = new GameObject("KeyLight"); keyGo.transform.SetParent(lamp, false); keyGo.transform.localPosition = new Vector3(0, -0.95f, 0);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Point; key.range = 20f; key.intensity = 5f; key.color = Color.white; key.shadows = LightShadows.Soft;
            keyGo.AddComponent<LightFlicker>();

            // second dim fill light near the box
            var fillGo = new GameObject("Deco_FillLight"); fillGo.transform.SetParent(root, false); fillGo.transform.localPosition = new Vector3(0, 2.5f, -4.5f);
            var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Point; fill.range = 7f; fill.intensity = 1.5f;

            // moonlight through the window
            var moonGo = new GameObject("Moon"); moonGo.transform.SetParent(root, false);
            moonGo.transform.rotation = Quaternion.Euler(35f, 200f, 0);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional; moon.intensity = 0.5f; moon.color = Color.white; moon.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Mats.Gray(0.22f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.04f; RenderSettings.fogColor = Mats.Gray(0.06f);
            RenderSettings.skybox = null;
        }

        // ================================================================ PROPS (gas station store)
        static void BuildProps()
        {
            var p = new GameObject("Props").transform; p.SetParent(root, false);
            foreach (float sx in new[] { -3.6f, 3.6f })
            {
                var shelf = new GameObject("Shelf").transform; shelf.SetParent(p, false); shelf.localPosition = new Vector3(sx, 0, -5.55f);
                Box("Back", new Vector3(0, 1f, 0.15f), new Vector3(2f, 2f, 0.05f), wallDark, true, shelf);
                for (int i = 0; i < 4; i++)
                {
                    Box("Plank", new Vector3(0, 0.2f + i * 0.5f, 0), new Vector3(2f, 0.04f, 0.4f), trim, false, shelf);
                    for (int k = 0; k < 6; k++)
                    {
                        if ((i * 7 + k * 3 + (int)sx) % 4 == 0) continue; // looted gaps
                        float g = 0.25f + ((i * 13 + k * 7) % 5) * 0.12f;
                        Box("Item", new Vector3(-0.8f + k * 0.32f, 0.33f + i * 0.5f, 0), new Vector3(0.2f, 0.22f + (k % 3) * 0.05f, 0.25f), Mats.Get("map_item_" + (int)(g * 100), Mats.Gray(g), 0.3f), false, shelf);
                    }
                }
                shelf.gameObject.AddComponent<BoxCollider>().center = new Vector3(0, 1, 0);
                shelf.GetComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.45f);
            }
            // posters / signs
            Box("Poster", new Vector3(-5.98f, 2.4f, -0.4f), new Vector3(0.02f, 0.9f, 0.65f), white, false, p);
            Box("Poster", new Vector3(5.98f, 2.4f, -0.4f), new Vector3(0.02f, 0.7f, 1.1f), wallDark, false, p);
            Sign(p, "OPEN 24 HRS", new Vector3(-3.5f, 3.2f, 5.97f), 0f, 0.09f);
            Sign(p, "NO REFUNDS", new Vector3(3.5f, 3.2f, 5.97f), 0f, 0.08f);
            // debris
            Box("Crate", new Vector3(4.6f, 0.3f, 5.2f), new Vector3(0.6f, 0.6f, 0.6f), cardboard, true, p, new Vector3(0, 25, 0));
            Box("Crate", new Vector3(-4.7f, 0.25f, 5.3f), new Vector3(0.5f, 0.5f, 0.5f), cardboard, true, p, new Vector3(0, -10, 0));
            Box("Paper", new Vector3(1.5f, 0.012f, 1.5f), new Vector3(0.3f, 0.005f, 0.4f), white, false, p, new Vector3(0, 30, 0));
            Box("Paper", new Vector3(-2f, 0.012f, -1f), new Vector3(0.3f, 0.005f, 0.4f), white, false, p, new Vector3(0, -40, 0));
            Box("BloodPool", new Vector3(0.8f, 0.011f, 4.6f), new Vector3(1.2f, 0.005f, 0.8f), black, false, p, new Vector3(0, 15, 0));
        }

        static void Sign(Transform parent, string text, Vector3 pos, float yRot, float charSize, Color? color = null)
        {
            var go = new GameObject("Sign_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yRot, 0);
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; tm.text = text; tm.fontSize = 64; tm.characterSize = charSize * 0.25f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color ?? Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ================================================================ SLUSHY PERK MACHINE
        static void BuildPerk(Perk perk, Vector3 pos, float yRot, float slushShade)
        {
            var m = new GameObject("Slushy_" + perk).transform;
            m.SetParent(root, false);
            m.localPosition = pos;
            m.localRotation = Quaternion.Euler(0, yRot, 0);

            var slush = Mats.Get("slush_" + perk, Mats.Gray(slushShade), 0.7f, 0, Mats.Gray(slushShade * 0.35f));
            Box("Cabinet", new Vector3(0, 0.5f, 0), new Vector3(1.0f, 1.0f, 0.7f), white, false, m);
            Box("CabinetPanel", new Vector3(0, 0.5f, 0.355f), new Vector3(0.85f, 0.75f, 0.01f), Mats.Get("slushpanel_" + perk, Mats.Gray(slushShade * 0.8f), 0.4f), false, m);
            Box("Kick", new Vector3(0, 0.04f, 0.0f), new Vector3(1.02f, 0.08f, 0.72f), black, false, m);
            Box("DripTray", new Vector3(0, 1.02f, 0.3f), new Vector3(0.9f, 0.04f, 0.18f), chrome, false, m);
            Box("TopDeck", new Vector3(0, 1.08f, 0), new Vector3(1.0f, 0.08f, 0.62f), chrome, false, m);
            for (int i = -1; i <= 1; i += 2)
            {
                var bowl = new GameObject("Bowl").transform; bowl.SetParent(m, false); bowl.localPosition = new Vector3(i * 0.24f, 1.12f, 0);
                Mats.Prim(PrimitiveType.Cylinder, bowl, new Vector3(0, 0.3f, 0), new Vector3(0.42f, 0.3f, 0.42f), glass, null, false, "Glass");
                Mats.Prim(PrimitiveType.Cylinder, bowl, new Vector3(0, 0.24f, 0), new Vector3(0.38f, 0.23f, 0.38f), slush, null, false, "Slush");
                var paddle = new GameObject("Paddle").transform; paddle.SetParent(bowl, false); paddle.localPosition = new Vector3(0, 0.3f, 0);
                Mats.Prim(PrimitiveType.Cube, paddle, Vector3.zero, new Vector3(0.3f, 0.45f, 0.02f), white);
                Mats.Prim(PrimitiveType.Cylinder, bowl, new Vector3(0, 0.62f, 0), new Vector3(0.44f, 0.03f, 0.44f), black, null, false, "Lid");
                Box("Spout", new Vector3(i * 0.24f, 1.16f, 0.27f), new Vector3(0.08f, 0.1f, 0.1f), black, false, m);
                Box("Handle", new Vector3(i * 0.24f, 1.24f, 0.33f), new Vector3(0.03f, 0.14f, 0.03f), chrome, false, m, new Vector3(-20, 0, 0));
            }
            // header sign
            Box("Header", new Vector3(0, 2.0f, 0.02f), new Vector3(1.05f, 0.42f, 0.12f), black, false, m);
            Box("HeaderGlow", new Vector3(0, 2.0f, 0.085f), new Vector3(0.98f, 0.36f, 0.005f), Mats.Get("slush_glow_" + perk, Mats.Gray(0.25f), 0.3f, 0, Mats.Gray(0.25f)), false, m);
            Box("Post", new Vector3(0, 1.78f, -0.2f), new Vector3(0.06f, 0.1f, 0.06f), chrome, false, m);
            Sign(m, PerkMachine.PerkName(perk).ToUpper(), new Vector3(0, 2.06f, 0.095f), 180, 0.075f);
            Sign(m, PerkMachine.PerkCost(perk) + " PTS", new Vector3(0, 1.9f, 0.095f), 180, 0.05f, Mats.Gray(0.7f));
            // cup stack on the side
            for (int c = 0; c < 4; c++)
                Mats.Prim(PrimitiveType.Cylinder, m, new Vector3(0.42f, 1.16f + c * 0.035f, 0.18f), new Vector3(0.09f, 0.04f, 0.09f), white, null, false, "Cup");

            var col = m.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.1f, 0); col.size = new Vector3(1.05f, 2.25f, 0.75f);

            var lightGo = new GameObject("Deco_PerkLight"); lightGo.transform.SetParent(m, false); lightGo.transform.localPosition = new Vector3(0, 2.0f, 0.6f);
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.range = 3.5f; l.intensity = 0.9f;

            var pm = m.gameObject.AddComponent<PerkMachine>();
            pm.Perk = perk;
        }

        // ================================================================ CARDBOARD MYSTERY BOX
        // ================================================================ WALL BUY
        static void BuildWallBuy(string weapon, int price, Vector3 pos, float yRot)
        {
            var def = WeaponDatabase.Get(weapon);
            var w = new GameObject("WallBuy_" + weapon).transform;
            w.SetParent(root, false);
            w.localPosition = pos;
            w.localRotation = Quaternion.Euler(0, yRot, 0);   // local +Z points out into the room
            var plaque = Mats.Get("wall_plaque", Mats.Gray(0.05f), 0.2f);
            var chalk = Mats.Get("wall_chalk", Mats.Gray(0.75f), 0.2f);
            Box("Plaque", new Vector3(0, 0, 0.02f), new Vector3(1.9f, 0.9f, 0.04f), plaque, false, w);
            Box("FrameT", new Vector3(0, 0.44f, 0.045f), new Vector3(1.9f, 0.025f, 0.012f), chalk, false, w);
            Box("FrameB", new Vector3(0, -0.44f, 0.045f), new Vector3(1.9f, 0.025f, 0.012f), chalk, false, w);
            Box("FrameL", new Vector3(-0.94f, 0, 0.045f), new Vector3(0.025f, 0.9f, 0.012f), chalk, false, w);
            Box("FrameR", new Vector3(0.94f, 0, 0.045f), new Vector3(0.025f, 0.9f, 0.012f), chalk, false, w);
            // weapon model mounted side-on, centred and scaled to fit the plaque
            var info = ViewmodelBuilder.Build(def, w, false);
            var mt = info.Root.transform;
            mt.localRotation = Quaternion.Euler(0, 90, 0);
            var bounds = new Bounds(mt.position, Vector3.zero); bool first = true;
            foreach (var r in info.Root.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.z);
            float sc = Mathf.Min(2.2f, 1.6f / Mathf.Max(0.1f, maxDim));
            mt.localScale = Vector3.one * sc;
            bounds = new Bounds(mt.position, Vector3.zero); first = true;
            foreach (var r in info.Root.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            mt.position += w.TransformPoint(new Vector3(0, 0, 0.12f)) - bounds.center;
            Sign(w, weapon.ToUpper(), new Vector3(0, 0.58f, 0.04f), 180, 0.08f);
            Sign(w, price + " PTS", new Vector3(0, -0.6f, 0.04f), 180, 0.06f, Mats.Gray(0.7f));
            var lightGo = new GameObject("Deco_WallBuyLight"); lightGo.transform.SetParent(w, false); lightGo.transform.localPosition = new Vector3(0, 0.3f, 0.9f);
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.range = 3f; l.intensity = 1.0f;
            var wb = w.gameObject.AddComponent<WallBuy>();
            wb.WeaponName = weapon; wb.Price = price;
        }

        static void BuildBox(Vector3 pos, float yRot)
        {
            var b = new GameObject("MysteryBox").transform;
            b.SetParent(root, false);
            b.localPosition = pos;
            b.localRotation = Quaternion.Euler(0, yRot, 0);
            Box("Body", new Vector3(0, 0.32f, 0), new Vector3(1.1f, 0.64f, 0.66f), cardboard, false, b);
            Box("TapeFront", new Vector3(0, 0.32f, 0.332f), new Vector3(0.12f, 0.64f, 0.005f), tape, false, b);
            Box("TapeSide", new Vector3(0.552f, 0.32f, 0), new Vector3(0.005f, 0.64f, 0.12f), tape, false, b);
            Box("Inside", new Vector3(0, 0.62f, 0), new Vector3(1.06f, 0.02f, 0.62f), black, false, b);
            var fl = new GameObject("FlapL").transform; fl.SetParent(b, false); fl.localPosition = new Vector3(-0.55f, 0.645f, 0);
            Box("Flap", new Vector3(0.275f, 0, 0), new Vector3(0.55f, 0.015f, 0.66f), cardboard, false, fl);
            var fr = new GameObject("FlapR").transform; fr.SetParent(b, false); fr.localPosition = new Vector3(0.55f, 0.645f, 0);
            Box("Flap", new Vector3(-0.275f, 0, 0), new Vector3(0.55f, 0.015f, 0.66f), cardboard, false, fr);
            Sign(b, "?", new Vector3(-0.3f, 0.36f, 0.34f), 180, 0.5f, Mats.Gray(0.15f));
            Sign(b, "?", new Vector3(0.3f, 0.36f, 0.34f), 180, 0.5f, Mats.Gray(0.15f));
            Sign(b, "THIS SIDE UP", new Vector3(0, 0.12f, 0.34f), 180, 0.06f, Mats.Gray(0.2f));
            var col = b.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.32f, 0); col.size = new Vector3(1.1f, 0.64f, 0.66f);
            var lightGo = new GameObject("Deco_BoxLight"); lightGo.transform.SetParent(b, false); lightGo.transform.localPosition = new Vector3(0, 1.6f, 0.4f);
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 60; l.range = 4f; l.intensity = 2f;
            lightGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            b.gameObject.AddComponent<MysteryBox>();
        }
    }


}
