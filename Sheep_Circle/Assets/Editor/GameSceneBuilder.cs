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

        // ----------------------------------------------------------- palette
        static readonly Color Grass = new Color(0.42f, 0.63f, 0.36f);
        static readonly Color IslandColor = new Color(0.34f, 0.54f, 0.29f);
        static readonly Color RoadColor = new Color(0.78f, 0.70f, 0.55f);
        static readonly Color RingRoadColor = new Color(0.72f, 0.63f, 0.48f);
        static readonly Color LineColor = new Color(0.62f, 0.55f, 0.42f);

        static Sprite circle, square, fluffy, oval, spots, shepBody, shepHat, ring;

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

            // The ring hole must match the island, so it is regenerated alongside.
            WriteSprite("ring", 512, p => Annulus(p, 0.48f, 0.48f * (RingInner / RingOuter)));

            circle   = LoadSprite("circle");
            square   = LoadSprite("square");
            fluffy   = LoadSprite("body_fluffy");
            oval     = LoadSprite("body_oval");
            spots    = LoadSprite("cow_spots");
            shepBody = LoadSprite("shepherd_body");
            shepHat  = LoadSprite("shepherd_hat");
            ring     = LoadSprite("ring");

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

            var body = NewSprite("Body", fluffy, Color.white, 10, root.transform);
            var patch = NewSprite("Patch", spots, Color.black, 11, root.transform);
            var head = NewSprite("Head", circle, Color.black, 12, root.transform);
            head.transform.localPosition = new Vector3(0.30f, 0f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            var so = new SerializedObject(animal);
            so.FindProperty("body").objectReferenceValue = body.GetComponent<SpriteRenderer>();
            so.FindProperty("patch").objectReferenceValue = patch.GetComponent<SpriteRenderer>();
            so.FindProperty("head").objectReferenceValue = head.GetComponent<SpriteRenderer>();
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

            var bg = NewSprite("Background", square, Grass, -20, board);
            bg.transform.localScale = new Vector3(60f, 40f, 1f);

            for (int i = 0; i < 4; i++) BuildRoad(i, board);

            var ringGo = NewSprite("RingRoad", ring, RingRoadColor, -8, board);
            ringGo.transform.localScale = Vector3.one * (RingOuter / 0.48f);

            var island = NewSprite("Island", circle, IslandColor, -6, board);
            island.transform.localScale = Vector3.one * (RingInner / 0.47f);

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

            //      slot  name      body      patch     bodyColour                        patchColour                       headColour                        head   size  radius enter weight sheph ringMul laps
            SetKind(kinds, 0, "Koyun", fluffy, null,     new Color(0.97f, 0.96f, 0.93f), Color.black,                     new Color(0.24f, 0.23f, 0.27f), true,  0.88f, 0.40f, 1.00f, 5.0f, false, 1.00f, 0);
            SetKind(kinds, 1, "Inek",  oval,   spots,    new Color(0.98f, 0.98f, 0.96f), new Color(0.16f, 0.16f, 0.19f),  new Color(0.89f, 0.66f, 0.60f), true,  1.22f, 0.55f, 0.62f, 2.0f, false, 1.00f, 0);
            SetKind(kinds, 2, "Keci",  fluffy, null,     new Color(0.79f, 0.64f, 0.42f), Color.black,                     new Color(0.42f, 0.29f, 0.16f), true,  0.74f, 0.33f, 1.30f, 3.0f, false, 1.00f, 0);
            SetKind(kinds, 3, "Tavuk", oval,   null,     new Color(0.95f, 0.81f, 0.30f), Color.black,                     new Color(0.91f, 0.53f, 0.24f), true,  0.58f, 0.26f, 1.75f, 2.0f, false, 1.00f, 0);
            SetKind(kinds, 4, "Coban", shepBody, shepHat, new Color(0.18f, 0.31f, 0.48f), new Color(0.88f, 0.65f, 0.24f), Color.white,                    false, 1.08f, 0.48f, 1.10f, 0.9f, true,  1.55f, 1);

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

            var visual = NewSprite("Road", square, RoadColor, -10, go.transform);
            visual.transform.localScale = new Vector3(length, RoadHalfWidth * 2f, 1f);

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

        static void SetKind(SerializedProperty array, int index, string name, Sprite body, Sprite patch,
                            Color bodyColor, Color patchColor, Color headColor, bool showHead,
                            float size, float radius, float enterMul, float weight,
                            bool shepherd, float ringMul, int laps)
        {
            var el = array.GetArrayElementAtIndex(index);
            el.FindPropertyRelative("displayName").stringValue = name;
            el.FindPropertyRelative("bodySprite").objectReferenceValue = body;
            el.FindPropertyRelative("patchSprite").objectReferenceValue = patch;
            el.FindPropertyRelative("bodyColor").colorValue = bodyColor;
            el.FindPropertyRelative("patchColor").colorValue = patchColor;
            el.FindPropertyRelative("headColor").colorValue = headColor;
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
            Directory.CreateDirectory(ArtDir);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
                importer.SaveAndReimport();
            }
        }

        static float Annulus(Vector2 p, float outer, float inner)
        {
            float d = Vector2.Distance(p, new Vector2(0.5f, 0.5f));
            return Mathf.Max(d - outer, inner - d);
        }
    }
}
