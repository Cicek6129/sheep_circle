using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SheepCircle.EditorTools
{
    /// <summary>
    /// Rebuilds the whole playfield from code. The board is pure geometry, so
    /// keeping it here means a tuning change is one menu click rather than an
    /// afternoon of dragging things around the Scene view.
    /// Sheep Circle > Rebuild Everything.
    /// </summary>
    public static class GameSceneBuilder
    {
        // ------------------------------------------------------------ tuning
        const float Radius = 2.6f;
        const float LaneSplit = 0f; // Animals enter exactly at the center of the lane
        const float RoadStart = 1.0f;
        const float QueueSpacing = 1.05f;
        const float ExitDistance = 4.0f;
        const int MaxQueue = 3;

        const float RingOuter = 3.35f;
        const float RingInner = 1.90f;
        const float RoadHalfWidth = 1.30f; // Full width as requested
        const float CameraSize = 6.5f;

        const string ScenePath = "Assets/Scenes/Game.unity";
        const string PrefabPath = "Assets/Prefabs/Animal.prefab";
        const string BurstPrefabPath = "Assets/Prefabs/Burst.prefab";
        const string DustPrefabPath = "Assets/Prefabs/Dust.prefab";
        const string ArtDir = "Assets/Art";

        // How much world space one ground tile covers. The art is 512px, so a
        // 4-unit tile lands close to 1:1 on screen at the default camera size.
        const float TileWorldSize = 4f;

        // ----------------------------------------------------------- palette
        // Only the camera letterbox still needs a flat colour; everything on the
        // board is textured now. Sampled from grass_tile so the edges blend.
        static readonly Color Grass = new Color(0.42f, 0.63f, 0.36f);
        static readonly Color LineColor = new Color(0.55f, 0.47f, 0.35f, 0.55f);

        // Road surfaces sit a notch under white so EntryLane's tap flash has
        // somewhere to brighten to.
        static readonly Color RoadTint = new Color(0.86f, 0.86f, 0.86f);

        static Sprite circle, square, ring, island;
        static Sprite sheep, cow, goat, chicken, shepherd;
        static Sprite grassTile, roadTile, shadowBlob, crashBurst, dustPuff;

        [MenuItem("Sheep Circle/Rebuild Everything (DANGER)")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
            {
                Debug.LogError("TMP Essential Resources are missing. Import them before rebuilding.");
                return;
            }

            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Scenes");

            ImportHandDrawnArt();

            // Ring and island are cut out of the ground textures rather than
            // filled with flat colour, so they read as the same dirt and grass
            // as everything around them. The ring hole must match the island,
            // so the two are always regenerated together.
            // The user provided a custom ring.png, so we should NOT overwrite it!
            // WriteTexturedSprite("ring", 512, "road_tile", RingOuter / 0.48f,
            //                     p => Annulus(p, 0.48f, 0.48f * (RingInner / RingOuter)));
            WriteTexturedSprite("island", 512, "grass_tile", RingInner / 0.47f,
                                p => Vector2.Distance(p, new Vector2(0.5f, 0.5f)) - 0.47f);

            circle     = LoadSprite("circle");
            square     = LoadSprite("square");
            ring       = LoadSprite("ring");
            island     = LoadSprite("island");
            sheep      = LoadSprite("sheep");
            cow        = LoadSprite("cow");
            goat       = LoadSprite("goat");
            chicken    = LoadSprite("chicken");
            shepherd   = LoadSprite("shepherd");
            grassTile  = LoadSprite("grass_tile");
            roadTile   = LoadSprite("road_tile");
            shadowBlob = LoadSprite("shadow");
            crashBurst = LoadSprite("crash_burst");
            dustPuff   = LoadSprite("dust_puff");

            foreach (var pair in new (string, Sprite)[] {
                ("sheep", sheep), ("cow", cow), ("goat", goat), ("chicken", chicken),
                ("shepherd", shepherd), ("grass_tile", grassTile), ("road_tile", roadTile),
                ("shadow", shadowBlob) })
            {
                if (pair.Item2 == null)
                {
                    Debug.LogError($"Sheep Circle: {ArtDir}/{pair.Item1}.png is missing. Rebuild aborted.");
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Animal prefab = BuildAnimalPrefab();

            //                                          sprite      order  life  start  end  spin  hold
            Burst burst = BuildEffectPrefab("Burst", BurstPrefabPath, crashBurst, 30, 0.70f, 0.45f, 1.30f, 22f, 0.35f);
            // Order 8 puts the dust under the animal but over its shadow, so it
            // reads as kicked up from beneath rather than pasted on top.
            Burst dust = BuildEffectPrefab("Dust", DustPrefabPath, dustPuff, 8, 0.55f, 0.30f, 1.05f, 10f, 0.10f);

            BuildBoard(prefab, burst, dust);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Call all the other setup helpers in the correct order
            SceneSetupHelper.SetupLevelUI();
            AudioSetupHelper.InjectAudio();
            KnockoutSetupHelper.Setup();
            DecorSetupHelper.Setup();
            TitleDecorSetupHelper.Setup();

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(AssetDatabase.GetAssetPath(sceneAsset), true) };

            AssetDatabase.SaveAssets();
            Debug.Log("Sheep Circle: Everything rebuilt successfully.");
        }

        // ------------------------------------------------------------ prefab

        static Animal BuildAnimalPrefab()
        {
            var root = new GameObject("Animal");
            var animal = root.AddComponent<Animal>();

            // Sits under everything and is kept unrotated by Animal.Apply.
            var shadow = NewSprite("Shadow", shadowBlob, new Color(1f, 1f, 1f, 0.8f), 5, root.transform);
            shadow.transform.localPosition = new Vector3(0.03f, -0.05f, 0f);
            shadow.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

            var body = NewSprite("Body", sheep, Color.white, 10, root.transform);

            // Patch and head are leftovers from the placeholder art, which drew
            // animals as a tinted blob plus a separate head dot. The drawn
            // sprites include both, so every kind now leaves these switched off
            // - they stay wired up in case a kind ever wants an overlay again.
            var patch = NewSprite("Patch", null, Color.white, 11, root.transform);
            var head = NewSprite("Head", circle, Color.black, 12, root.transform);
            head.transform.localPosition = new Vector3(0.30f, 0f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            // Off in the prefab too, not just at runtime, so opening the asset
            // does not show a stray black dot beside the animal.
            head.SetActive(false);
            patch.SetActive(false);

            var so = new SerializedObject(animal);
            so.FindProperty("body").objectReferenceValue = body.GetComponent<SpriteRenderer>();
            so.FindProperty("patch").objectReferenceValue = patch.GetComponent<SpriteRenderer>();
            so.FindProperty("head").objectReferenceValue = head.GetComponent<SpriteRenderer>();
            so.FindProperty("shadow").objectReferenceValue = shadow.GetComponent<SpriteRenderer>();
            so.FindProperty("herdSpacing").floatValue = 0.95f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return asset.GetComponent<Animal>();
        }

        /// <summary>A one-shot Burst prefab. The crash wants to be loud and cover
        /// the animals; the dust wants to be quick, soft and sit behind them.
        /// Same component either way, only the numbers differ.</summary>
        static Burst BuildEffectPrefab(string name, string path, Sprite sprite, int order,
                                       float life, float startScale, float endScale,
                                       float spin, float hold)
        {
            var root = new GameObject(name);
            var burst = root.AddComponent<Burst>();

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;

            var so = new SerializedObject(burst);
            so.FindProperty("sprite").objectReferenceValue = sr;
            so.FindProperty("life").floatValue = life;
            so.FindProperty("startScale").floatValue = startScale;
            so.FindProperty("endScale").floatValue = endScale;
            so.FindProperty("maxSpin").floatValue = spin;
            so.FindProperty("hold").floatValue = hold;
            so.ApplyModifiedPropertiesWithoutUndo();

            var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return asset.GetComponent<Burst>();
        }

        // ------------------------------------------------------------- board

        static void BuildBoard(Animal prefab, Burst burstPrefab, Burst dustPrefab)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Grass;

            var board = new GameObject("Board").transform;

            NewTiledSprite("Background", grassTile, Color.white, -20, board, new Vector2(60f, 40f));

            for (int i = 0; i < 2; i++) BuildRoad(i, board);

            var ringGo = NewSprite("RingRoad", ring, RoadTint, -5, board); // -5 ensures it's above the straight road (-10)
            
            // The user provided a custom 1024x1024 ring image. 
            // The dirt path is between pixel radii 243 and 492. Center is at ~367.5px.
            // 367.5 / 512 = 0.7177. So the dirt path is at 71.77% of the image radius.
            float dirtFraction = 0.7177f;
            float desiredRadius = Radius / dirtFraction;
            float nativeRadius = (ring.texture.width / 2f) / ring.pixelsPerUnit;
            ringGo.transform.localScale = Vector3.one * (desiredRadius / nativeRadius);

            var islandGo = NewSprite("Island", island, Color.white, -6, board);
            islandGo.transform.localScale = Vector3.one * (RingInner / 0.47f);

            var animalParent = new GameObject("Animals").transform;

            HUD hud = BuildHud();

            var gm = new GameObject("GameManager").AddComponent<GameManager>();

            var lanes = UnityEngine.Object.FindObjectsByType<EntryLane>(FindObjectsSortMode.None);
            Array.Sort(lanes, (a, b) => a.LaneIndex.CompareTo(b.LaneIndex));

            var so = new SerializedObject(gm);
            so.FindProperty("geometry.radius").floatValue = Radius;
            so.FindProperty("geometry.laneCount").intValue = 2;
            so.FindProperty("geometry.laneSplitDeg").floatValue = LaneSplit;
            so.FindProperty("geometry.roadStart").floatValue = RoadStart;
            so.FindProperty("geometry.queueSpacing").floatValue = QueueSpacing;
            so.FindProperty("geometry.exitDistance").floatValue = ExitDistance;

            so.FindProperty("animalPrefab").objectReferenceValue = prefab;
            so.FindProperty("burstPrefab").objectReferenceValue = burstPrefab;
            so.FindProperty("dustPrefab").objectReferenceValue = dustPrefab;
            so.FindProperty("animalParent").objectReferenceValue = animalParent;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("camera").objectReferenceValue = cam;
            so.FindProperty("maxQueuePerLane").intValue = MaxQueue;

            var entryProp = so.FindProperty("entryLane");
            if (entryProp != null)
            {
                foreach (var l in lanes)
                {
                    if (l.LaneIndex == RingGeometry.ENTRY_LANE)
                    {
                        entryProp.objectReferenceValue = l;
                        break;
                    }
                }
            }

            var kinds = so.FindProperty("kinds");
            kinds.arraySize = 5;

            // The sprites are fully coloured now, so bodyColour is white - any
            // other tint would multiply into the artwork and dull it. Likewise
            // showHead is off for everyone: the drawn sprites include the head.
            //      slot  name      body      bodyColour   head   size  radius enter weight sheph ringMul laps
            SetKind(kinds, 0, "Koyun", sheep,    Color.white, false, 0.88f, 0.40f, 1.00f, 5.0f, false, 1.00f, 0);
            SetKind(kinds, 1, "Inek",  cow,      Color.white, false, 1.22f, 0.55f, 0.85f, 2.0f, false, 1.00f, 0);
            SetKind(kinds, 2, "Keci",  goat,     Color.white, false, 0.74f, 0.33f, 1.30f, 3.0f, false, 1.00f, 0);
            SetKind(kinds, 3, "Tavuk", chicken,  Color.white, false, 0.58f, 0.26f, 1.75f, 2.0f, false, 1.00f, 0);
            SetKind(kinds, 4, "Coban", shepherd, Color.white, false, 1.08f, 0.48f, 1.10f, 0.9f, true,  1.00f, 0);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRoad(int lane, Transform parent)
        {
            float angle = 90f - lane * 180f; // lane 0 = top (90°), lane 1 = bottom (270°)
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            const float inner = 2.4f;
            const float outer = 15f;
            float length = outer - inner;
            float mid = (outer + inner) * 0.5f;

            var go = new GameObject("Lane" + lane);
            go.transform.SetParent(parent, false);
            go.transform.position = dir * mid;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // roadTile is 512px, PixelsPerUnit is 128, so native height is 4 units.
            // To make it RoadHalfWidth*2 (2.6 units) high without cropping in Tiled mode,
            // we give the SpriteRenderer its native height and scale the transform down.
            float nativeHeight = roadTile.bounds.size.y; 
            float scaleY = (RoadHalfWidth * 2f) / nativeHeight;

            var visual = NewTiledSprite("Road", roadTile, RoadTint, -10, go.transform,
                                        new Vector2(length, nativeHeight));
            visual.transform.localScale = new Vector3(1f, scaleY, 1f);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(length, RoadHalfWidth * 2f);

            var entry = go.AddComponent<EntryLane>();
            var so = new SerializedObject(entry);
            so.FindProperty("laneIndex").intValue = lane;
            so.FindProperty("road").objectReferenceValue = visual.GetComponent<SpriteRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --------------------------------------------------------------- hud

        static HUD BuildHud()
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Everything that belongs to a round in progress, grouped so the
            // title card can switch it off in one go.
            var playHud = new GameObject("PlayHud", typeof(RectTransform));
            playHud.transform.SetParent(canvasGo.transform, false);
            var playRect = playHud.GetComponent<RectTransform>();
            playRect.anchorMin = Vector2.zero;
            playRect.anchorMax = Vector2.one;
            playRect.offsetMin = Vector2.zero;
            playRect.offsetMax = Vector2.zero;

            var score = NewText("Score", playHud.transform, "0", 120f, Color.white);
            Anchor(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(600f, 150f));
            score.fontStyle = FontStyles.Bold;

            var best = NewText("Best", playHud.transform, "REKOR  0", 40f, new Color(1f, 1f, 1f, 0.75f));
            Anchor(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(600f, 60f));

            var hint = NewText("Hint", playHud.transform,
                               "Yola tıkla -> sıradaki hayvan çembere girsin   |   1-4 tuşları", 34f,
                               new Color(1f, 1f, 1f, 0.65f));
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(1400f, 60f));

            var panel = new GameObject("GameOver", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.06f, 0.78f);

            var title = NewText("Title", panel.transform, "", 84f, new Color(1f, 0.83f, 0.35f));
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1600f, 200f));
            title.fontStyle = FontStyles.Bold;

            var bodyText = NewText("Body", panel.transform, "", 46f, Color.white);
            Anchor(bodyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1400f, 260f));

            var audioMgr = new GameObject("AudioManager");
            audioMgr.AddComponent<AudioManager>();

            GameObject start = BuildStartPanel(canvasGo.transform,
                                               out RectTransform playButton,
                                               out TextMeshProUGUI startBest,
                                               out UnityEngine.UI.Image soundImg,
                                               out Sprite soundOn, out Sprite soundOff);

            GameObject levelSelect = BuildLevelSelectPanel(canvasGo.transform,
                                                           out UnityEngine.UI.Image menuBtn,
                                                           out UnityEngine.UI.Image[] lvlImgs,
                                                           out TextMeshProUGUI[] lvlTxts);

            var hud = canvasGo.AddComponent<HUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("bestText").objectReferenceValue = best;
            so.FindProperty("gameOverPanel").objectReferenceValue = panel;
            so.FindProperty("gameOverTitle").objectReferenceValue = title;
            so.FindProperty("gameOverBody").objectReferenceValue = bodyText;
            so.FindProperty("playHud").objectReferenceValue = playHud;
            so.FindProperty("startPanel").objectReferenceValue = start;
            so.FindProperty("startButton").objectReferenceValue = playButton;
            so.FindProperty("startBestText").objectReferenceValue = startBest;
            so.FindProperty("soundButtonImage").objectReferenceValue = soundImg;
            so.FindProperty("soundOnSprite").objectReferenceValue = soundOn;
            so.FindProperty("soundOffSprite").objectReferenceValue = soundOff;

            so.FindProperty("levelSelectPanel").objectReferenceValue = levelSelect;
            so.FindProperty("menuButtonImage").objectReferenceValue = menuBtn;
            
            var imgsProp = so.FindProperty("levelButtonImages");
            imgsProp.arraySize = lvlImgs.Length;
            for (int i = 0; i < lvlImgs.Length; i++)
                imgsProp.GetArrayElementAtIndex(i).objectReferenceValue = lvlImgs[i];

            var txtsProp = so.FindProperty("levelButtonTexts");
            txtsProp.arraySize = lvlTxts.Length;
            for (int i = 0; i < lvlTxts.Length; i++)
                txtsProp.GetArrayElementAtIndex(i).objectReferenceValue = lvlTxts[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            levelSelect.SetActive(false);

            panel.SetActive(false);
            return hud;
        }

        /// <summary>The card the player sees before the first round. Nothing on
        /// it is clickable - GameManager starts on any tap - so it needs no
        /// EventSystem, which the scene deliberately does without.</summary>
        static GameObject BuildStartPanel(Transform canvas, out RectTransform playButton,
                                          out TextMeshProUGUI bestLine,
                                          out UnityEngine.UI.Image soundImg,
                                          out Sprite soundOn, out Sprite soundOff)
        {
            var start = new GameObject("StartPanel", typeof(RectTransform));
            start.transform.SetParent(canvas, false);

            var rect = start.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Lighter than the game-over veil: the board should still read
            // through it, since it is the best advert the game has.
            start.AddComponent<Image>().color = new Color(0.05f, 0.09f, 0.06f, 0.58f);

            // Card and button are sized from their own artwork's aspect, then the
            // logo and button are stacked around the card. Redrawing the wooden
            // sign at a different shape reflows the card instead of smearing it.
            const float CardWidth = 520f;
            const float ButtonWidth = 420f;
            const float LogoHeight = 260f;
            const float Gap = 30f;

            Sprite wood = LoadSprite("panel_wood");
            Sprite green = LoadSprite("button_green");

            float cardH = CardWidth / Aspect(wood, 1.39f);
            float buttonH = ButtonWidth / Aspect(green, 2.9f);

            const float cardY = -25f;
            float logoY = cardY + cardH * 0.5f + Gap + LogoHeight * 0.5f;
            float buttonY = cardY - cardH * 0.5f - Gap - buttonH * 0.5f;

            Sprite logo = LoadSprite("logo");
            if (logo != null)
            {
                var img = NewImage("Logo", start.transform, logo, Color.white);
                img.preserveAspect = true;
                Anchor(img.rectTransform, Middle, new Vector2(0f, logoY), new Vector2(1000f, LogoHeight));
            }
            else
            {
                // Stand-in until logo.png exists; the layout is identical either way.
                var word = NewText("Title", start.transform, "SHEEP CIRCLE", 118f,
                                   new Color(1f, 0.97f, 0.87f));
                word.fontStyle = FontStyles.Bold;
                Anchor(word.rectTransform, Middle, new Vector2(0f, logoY), new Vector2(1400f, LogoHeight));
            }

            var card = NewImage("Card", start.transform, wood, new Color(1f, 1f, 1f, 0.97f));
            Anchor(card.rectTransform, Middle, new Vector2(0f, cardY), new Vector2(CardWidth, cardH));

            float inner = CardWidth - 70f;

            var how = NewText("How", card.transform,
                              "Yola tıkla, sıradaki\nhayvan çembere girsin", 40f,
                              new Color(1f, 0.97f, 0.90f));
            how.fontStyle = FontStyles.Bold;
            Anchor(how.rectTransform, Middle, new Vector2(0f, cardH * 0.22f), new Vector2(inner, 150f));

            var warn = NewText("Warn", card.transform, "Çarpışırlarsa oyun biter", 34f,
                               new Color(1f, 0.88f, 0.72f, 0.85f));
            Anchor(warn.rectTransform, Middle, new Vector2(0f, -cardH * 0.02f), new Vector2(inner, 60f));

            bestLine = NewText("StartBest", card.transform, "REKOR  0", 44f,
                               new Color(1f, 0.83f, 0.35f));
            bestLine.fontStyle = FontStyles.Bold;
            Anchor(bestLine.rectTransform, Middle, new Vector2(0f, -cardH * 0.25f), new Vector2(inner, 70f));

            var button = NewImage("PlayButton", start.transform, green, Color.white);
            Anchor(button.rectTransform, Middle, new Vector2(0f, buttonY), new Vector2(ButtonWidth, buttonH));
            playButton = button.rectTransform;

            var label = NewText("Label", button.transform, "BAŞLA", 62f, Color.white);
            label.fontStyle = FontStyles.Bold;
            Anchor(label.rectTransform, Middle, new Vector2(0f, 2f), new Vector2(ButtonWidth - 40f, buttonH));

            soundOn = LoadSprite("icon_sound_on");
            soundOff = LoadSprite("icon_sound_off");
            var soundBtn = new GameObject("SoundButton", typeof(RectTransform));
            soundBtn.transform.SetParent(start.transform, false);
            soundImg = soundBtn.AddComponent<Image>();
            if (soundOn != null) soundImg.sprite = soundOn;
            Anchor(soundImg.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(80f, 80f));

            return start;
        }

        static GameObject BuildLevelSelectPanel(Transform canvas, out UnityEngine.UI.Image menuBtn,
                                                out UnityEngine.UI.Image[] lvlImgs,
                                                out TextMeshProUGUI[] lvlTxts)
        {
            var menuGo = new GameObject("MenuButton", typeof(RectTransform));
            menuGo.transform.SetParent(canvas, false);
            menuBtn = menuGo.AddComponent<Image>();
            menuBtn.sprite = LoadSprite("button_green");
            Anchor(menuBtn.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(100f, 100f));
            
            var menuText = NewText("MenuText", menuGo.transform, "MENU", 28f, Color.white);
            menuText.fontStyle = FontStyles.Bold;
            Anchor(menuText.rectTransform, Middle, new Vector2(0f, 0f), new Vector2(100f, 100f));

            var panelGo = new GameObject("LevelSelectPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvas, false);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panelGo.AddComponent<Image>().color = new Color(0.05f, 0.09f, 0.06f, 0.88f);

            Sprite wood = LoadSprite("panel_wood");
            Sprite green = LoadSprite("button_green");
            
            const float CardWidth = 1000f;
            float cardH = CardWidth / Aspect(wood, 1.39f);
            
            var card = NewImage("Card", panelGo.transform, wood, new Color(1f, 1f, 1f, 0.97f));
            Anchor(card.rectTransform, Middle, Vector2.zero, new Vector2(CardWidth, cardH));

            var title = NewText("Title", card.transform, "ANA MENÜ", 70f, new Color(1f, 0.97f, 0.90f));
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, Middle, new Vector2(0f, cardH * 0.4f), new Vector2(CardWidth, 100f));

            int cols = 5;
            int rows = 3;
            lvlImgs = new UnityEngine.UI.Image[cols * rows];
            lvlTxts = new TextMeshProUGUI[cols * rows];

            float startX = -CardWidth * 0.35f;
            float startY = cardH * 0.15f;
            float spacingX = CardWidth * 0.7f / (cols - 1);
            float spacingY = -cardH * 0.45f / (rows - 1);
            float btnW = spacingX * 0.8f;
            float btnH = Mathf.Abs(spacingY) * 0.7f;

            for (int i = 0; i < cols * rows; i++)
            {
                int r = i / cols;
                int c = i % cols;

                var btn = NewImage($"LevelBtn_{i}", card.transform, green, Color.white);
                Anchor(btn.rectTransform, Middle, new Vector2(startX + c * spacingX, startY + r * spacingY), new Vector2(btnW, btnH));
                lvlImgs[i] = btn;

                var txt = NewText($"LevelTxt_{i}", btn.transform, (i + 1).ToString(), btnH * 0.5f, Color.white);
                txt.fontStyle = FontStyles.Bold;
                Anchor(txt.rectTransform, Middle, new Vector2(0f, 2f), new Vector2(btnW, btnH));
                lvlTxts[i] = txt;
            }

            return panelGo;
        }

        // ----------------------------------------------------------- helpers

        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        static float Aspect(Sprite sprite, float fallback) =>
            sprite != null ? sprite.rect.width / sprite.rect.height : fallback;

        /// <summary>A UI image; falls back to a flat dark plate when the art it
        /// wants has not been drawn yet, so the layout never collapses.</summary>
        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = color;
            }
            else
            {
                img.color = new Color(0.16f, 0.13f, 0.10f, 0.92f);
            }

            return img;
        }

        static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        static GameObject NewSprite(string name, Sprite sprite, Color color, int order, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        /// <summary>A sprite that repeats its texture to fill <paramref name="worldSize"/>
        /// instead of being stretched to it.</summary>
        static GameObject NewTiledSprite(string name, Sprite sprite, Color color, int order,
                                         Transform parent, Vector2 worldSize)
        {
            var go = NewSprite(name, sprite, color, order, parent);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = worldSize;
            return go;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        static void SetKind(SerializedProperty array, int index, string name, Sprite body,
                            Color bodyColor, bool showHead,
                            float size, float radius, float enterMul, float weight,
                            bool shepherd, float ringMul, int laps)
        {
            var el = array.GetArrayElementAtIndex(index);
            el.FindPropertyRelative("displayName").stringValue = name;
            el.FindPropertyRelative("bodySprite").objectReferenceValue = body;
            el.FindPropertyRelative("patchSprite").objectReferenceValue = null;
            el.FindPropertyRelative("bodyColor").colorValue = bodyColor;
            el.FindPropertyRelative("patchColor").colorValue = Color.white;
            el.FindPropertyRelative("headColor").colorValue = Color.white;
            el.FindPropertyRelative("showHead").boolValue = showHead;
            el.FindPropertyRelative("size").floatValue = size;
            el.FindPropertyRelative("collisionRadius").floatValue = radius;
            el.FindPropertyRelative("enterSpeedMul").floatValue = enterMul;
            el.FindPropertyRelative("spawnWeight").floatValue = weight;
            el.FindPropertyRelative("isShepherd").boolValue = shepherd;
            el.FindPropertyRelative("ringSpeedMul").floatValue = ringMul;
            el.FindPropertyRelative("extraLaps").intValue = laps;
        }

        // ------------------------------------------------------------ sprites

        /// <summary>Renders a signed-distance function to an antialiased white PNG
        /// and imports it as a sprite one world unit across.</summary>
        static void WriteSprite(string fileName, int size, Func<Vector2, float> sdf)
        {
            var pixels = new Color[size * size];
            float aa = 1.5f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - sdf(p) / aa));
                }
            }

            WritePixels(fileName, size, pixels);
        }

        static void WritePixels(string fileName, int size, Color[] pixels)
        {
            Directory.CreateDirectory(ArtDir);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string path = $"{ArtDir}/{fileName}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static float Annulus(Vector2 p, float outer, float inner)
        {
            float d = Vector2.Distance(p, new Vector2(0.5f, 0.5f));
            return Mathf.Max(d - outer, inner - d);
        }

        /// <summary>Same as WriteSprite, but fills the shape with a repeating
        /// ground texture instead of flat white. <paramref name="worldSpan"/> is
        /// how wide the finished sprite will be in the scene, which is what sets
        /// the texel density so it matches the tiled ground around it.</summary>
        static void WriteTexturedSprite(string fileName, int size, string tileName,
                                        float worldSpan, Func<Vector2, float> sdf)
        {
            Texture2D tile = LoadPng($"{ArtDir}/{tileName}.png");
            if (tile == null)
            {
                Debug.LogWarning($"Sheep Circle: {tileName}.png not found, {fileName} falls back to flat white.");
                WriteSprite(fileName, size, sdf);
                return;
            }

            float repeats = Mathf.Max(1f, worldSpan / TileWorldSize);
            var tilePixels = tile.GetPixels();
            int tw = tile.width, th = tile.height;

            var pixels = new Color[size * size];
            float aa = 1.5f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);

                    int sx = Mathf.RoundToInt(p.x * repeats * tw) % tw;
                    int sy = Mathf.RoundToInt(p.y * repeats * th) % th;
                    Color c = tilePixels[sy * tw + sx];

                    c.a = Mathf.Clamp01(0.5f - sdf(p) / aa);
                    pixels[y * size + x] = c;
                }
            }

            WritePixels(fileName, size, pixels);
            UnityEngine.Object.DestroyImmediate(tile);
        }

        /// <summary>Reads a PNG straight off disk, so it works regardless of
        /// whether the asset happens to be marked readable.</summary>
        static Texture2D LoadPng(string path)
        {
            if (!File.Exists(path)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(path))) return tex;

            UnityEngine.Object.DestroyImmediate(tex);
            return null;
        }

        /// <summary>Applies the sprite import settings the board relies on to
        /// every hand-drawn PNG in the art folder. Pixels-per-unit is set to the
        /// texture width so a sprite is exactly one world unit across, which is
        /// the assumption Animal.Setup scales against.</summary>
        static void ImportHandDrawnArt()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                bool isTile = Path.GetFileNameWithoutExtension(path).EndsWith("_tile");
                float ppu = isTile ? tex.width / TileWorldSize : tex.width;
                var wrap = isTile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

                bool dirty = importer.textureType != TextureImporterType.Sprite
                          || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                          || importer.wrapMode != wrap
                          || importer.mipmapEnabled
                          || !importer.alphaIsTransparency
                          || importer.textureCompression != TextureImporterCompression.Uncompressed;

                if (!dirty) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = wrap;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                // Tiled draw mode refuses anything but a full rectangle mesh.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }
        }
    }
}
