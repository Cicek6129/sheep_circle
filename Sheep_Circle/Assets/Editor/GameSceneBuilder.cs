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
    /// Sheep Circle > Rebuild Game Scene.
    /// </summary>
    public static class GameSceneBuilder
    {
        // ------------------------------------------------------------ tuning
        const float Radius = 2.6f;
        const float LaneSplit = 14f;
        const float RoadStart = 1.0f;
        const float QueueSpacing = 1.05f;
        const float ExitDistance = 4.0f;
        const int MaxQueue = 3;

        const float RingOuter = 3.35f;
        const float RingInner = 1.90f;
        const float RoadHalfWidth = 1.30f;
        const float CameraSize = 6.5f;

        const string ScenePath = "Assets/Scenes/Game.unity";
        const string PrefabPath = "Assets/Prefabs/Animal.prefab";
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
        static Sprite grassTile, roadTile, shadowBlob;

        [MenuItem("Sheep Circle/Rebuild Game Scene")]
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
            WriteTexturedSprite("ring", 512, "road_tile", RingOuter / 0.48f,
                                p => Annulus(p, 0.48f, 0.48f * (RingInner / RingOuter)));
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
            BuildBoard(prefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(AssetDatabase.GetAssetPath(sceneAsset), true) };

            AssetDatabase.SaveAssets();
            Debug.Log("Sheep Circle: scene rebuilt.");
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

        // ------------------------------------------------------------- board

        static void BuildBoard(Animal prefab)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Grass;

            var board = new GameObject("Board").transform;

            NewTiledSprite("Background", grassTile, Color.white, -20, board, new Vector2(60f, 40f));

            for (int i = 0; i < 4; i++) BuildRoad(i, board);

            var ringGo = NewSprite("RingRoad", ring, RoadTint, -8, board);
            ringGo.transform.localScale = Vector3.one * (RingOuter / 0.48f);

            var islandGo = NewSprite("Island", island, Color.white, -6, board);
            islandGo.transform.localScale = Vector3.one * (RingInner / 0.47f);

            var animalParent = new GameObject("Animals").transform;

            HUD hud = BuildHud();

            var gm = new GameObject("GameManager").AddComponent<GameManager>();

            var lanes = UnityEngine.Object.FindObjectsByType<EntryLane>(FindObjectsSortMode.None);
            Array.Sort(lanes, (a, b) => a.LaneIndex.CompareTo(b.LaneIndex));

            var so = new SerializedObject(gm);
            so.FindProperty("geometry.radius").floatValue = Radius;
            so.FindProperty("geometry.laneCount").intValue = 4;
            so.FindProperty("geometry.laneSplitDeg").floatValue = LaneSplit;
            so.FindProperty("geometry.roadStart").floatValue = RoadStart;
            so.FindProperty("geometry.queueSpacing").floatValue = QueueSpacing;
            so.FindProperty("geometry.exitDistance").floatValue = ExitDistance;

            so.FindProperty("animalPrefab").objectReferenceValue = prefab;
            so.FindProperty("animalParent").objectReferenceValue = animalParent;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("camera").objectReferenceValue = cam;
            so.FindProperty("maxQueuePerLane").intValue = MaxQueue;

            var laneProp = so.FindProperty("lanes");
            laneProp.arraySize = lanes.Length;
            for (int i = 0; i < lanes.Length; i++)
                laneProp.GetArrayElementAtIndex(i).objectReferenceValue = lanes[i];

            var kinds = so.FindProperty("kinds");
            kinds.arraySize = 5;

            // The sprites are fully coloured now, so bodyColour is white - any
            // other tint would multiply into the artwork and dull it. Likewise
            // showHead is off for everyone: the drawn sprites include the head.
            //      slot  name      body      bodyColour   head   size  radius enter weight sheph ringMul laps
            SetKind(kinds, 0, "Koyun", sheep,    Color.white, false, 0.88f, 0.40f, 1.00f, 5.0f, false, 1.00f, 0);
            SetKind(kinds, 1, "Inek",  cow,      Color.white, false, 1.22f, 0.55f, 0.62f, 2.0f, false, 1.00f, 0);
            SetKind(kinds, 2, "Keci",  goat,     Color.white, false, 0.74f, 0.33f, 1.30f, 3.0f, false, 1.00f, 0);
            SetKind(kinds, 3, "Tavuk", chicken,  Color.white, false, 0.58f, 0.26f, 1.75f, 2.0f, false, 1.00f, 0);
            SetKind(kinds, 4, "Coban", shepherd, Color.white, false, 1.08f, 0.48f, 1.10f, 0.9f, true,  1.55f, 1);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRoad(int lane, Transform parent)
        {
            float angle = 90f - lane * 90f;
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            const float inner = 2.4f;
            const float outer = 15f;
            float length = outer - inner;
            float mid = (outer + inner) * 0.5f;

            var go = new GameObject("Lane" + lane);
            go.transform.SetParent(parent, false);
            go.transform.position = dir * mid;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var visual = NewTiledSprite("Road", roadTile, RoadTint, -10, go.transform,
                                        new Vector2(length, RoadHalfWidth * 2f));

            // Divider between the incoming and outgoing sides of the road.
            var line = NewSprite("Divider", square, LineColor, -9, go.transform);
            line.transform.localScale = new Vector3(length, 0.09f, 1f);

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

            var score = NewText("Score", canvasGo.transform, "0", 120f, Color.white);
            Anchor(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(600f, 150f));
            score.fontStyle = FontStyles.Bold;

            var best = NewText("Best", canvasGo.transform, "REKOR  0", 40f, new Color(1f, 1f, 1f, 0.75f));
            Anchor(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(600f, 60f));

            var hint = NewText("Hint", canvasGo.transform,
                               "Yola tikla -> siradaki hayvan cembere girsin   |   1-4 tuslari", 34f,
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

            var hud = canvasGo.AddComponent<HUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("bestText").objectReferenceValue = best;
            so.FindProperty("gameOverPanel").objectReferenceValue = panel;
            so.FindProperty("gameOverTitle").objectReferenceValue = title;
            so.FindProperty("gameOverBody").objectReferenceValue = bodyText;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            return hud;
        }

        // ----------------------------------------------------------- helpers

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
