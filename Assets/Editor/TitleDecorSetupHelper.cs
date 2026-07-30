using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SheepCircle.EditorTools
{
    /// <summary>
    /// Dresses the title card in the open scene: bunting along the top, a grass
    /// verge along the bottom, sun, clouds, butterflies, and a ribbon behind the
    /// best-score line.
    ///
    /// Everything lands in one "TitleDecor" object inserted as the panel's FIRST
    /// child, so it draws under the logo, the sign and the button - sibling order
    /// is draw order in UGUI. The panel's own dim image draws before any child,
    /// so the decor still sits over the veil.
    ///
    /// Separate menu item rather than a change to GameSceneBuilder, which
    /// rebuilds the board and would take the level and audio wiring with it.
    ///
    /// Sheep Circle > Setup Title Decor.
    /// </summary>
    public static class TitleDecorSetupHelper
    {
        const string ArtDir = "Assets/Art";
        const string DecorName = "TitleDecor";
        const string RibbonName = "RecordRibbon";

        public static void Setup()
        {
            ImportUiArt();

            var hud = Object.FindFirstObjectByType<HUD>();
            Transform panel = hud != null ? hud.transform.Find("StartPanel") : null;
            if (panel == null)
            {
                Debug.LogError("Sheep Circle: no StartPanel in the open scene. Open Game.unity first.");
                return;
            }

            Transform old = panel.Find(DecorName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var decor = new GameObject(DecorName, typeof(RectTransform));
            decor.transform.SetParent(panel, false);
            Stretch(decor.GetComponent<RectTransform>());
            decor.transform.SetSiblingIndex(0);   // behind logo, card and button

            int made = 0;

            // Bunting and grass are tiled rather than stretched: both sprites were
            // cut so their left and right edges match exactly, so repeating them
            // across any screen width leaves no seam, and nothing gets squashed.
            made += EdgeStrip("Bunting", "bunting", decor.transform, top: true, height: 200f);
            made += EdgeStrip("Grass", "grass_strip", decor.transform, top: false, height: 200f);

            made += Piece("Sun", "sun", decor.transform, new Vector2(0f, 1f), new Vector2(205f, -175f), 300f);
            made += Piece("CloudRight", "cloud", decor.transform, new Vector2(1f, 1f), new Vector2(-265f, -250f), 420f);
            made += Piece("CloudLeft", "cloud", decor.transform, new Vector2(0f, 1f), new Vector2(455f, -350f), 290f);
            made += Piece("ButterflyA", "butterfly", decor.transform, new Vector2(0f, 0.5f), new Vector2(300f, -40f), 110f);
            made += Piece("ButterflyB", "butterfly", decor.transform, new Vector2(1f, 0.5f), new Vector2(-330f, 120f), 85f);

            made += Ribbon(panel);

            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            EditorSceneManager.SaveScene(panel.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"Sheep Circle: title decor placed. {made} pieces.");
        }

        // ------------------------------------------------------------ pieces

        /// <summary>A strip pinned to the top or bottom edge, repeating sideways.</summary>
        static int EdgeStrip(string name, string spriteName, Transform parent, bool top, float height)
        {
            Sprite sprite = Load(spriteName);
            if (sprite == null) { Missing(spriteName); return 0; }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Tiled;
            img.raycastTarget = false;

            return 1;
        }

        static int Piece(string name, string spriteName, Transform parent,
                         Vector2 anchor, Vector2 offset, float width)
        {
            Sprite sprite = Load(spriteName);
            if (sprite == null) { Missing(spriteName); return 0; }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            float height = width * (sprite.rect.height / sprite.rect.width);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = offset;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            return 1;
        }

        /// <summary>Slides a ribbon in behind the best-score line on the sign.</summary>
        static int Ribbon(Transform panel)
        {
            Sprite sprite = Load("ribbon_banner");
            Transform best = FindDeep(panel, "StartBest");

            if (sprite == null) { Missing("ribbon_banner"); return 0; }
            if (best == null)
            {
                Debug.LogWarning("Sheep Circle: StartBest not found, ribbon skipped.");
                return 0;
            }

            Transform existing = best.parent.Find(RibbonName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var bestRect = best.GetComponent<RectTransform>();

            var go = new GameObject(RibbonName, typeof(RectTransform));
            go.transform.SetParent(best.parent, false);

            float width = 440f;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, width * (sprite.rect.height / sprite.rect.width));
            rect.anchoredPosition = bestRect.anchoredPosition;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Directly behind the text it is backing.
            go.transform.SetSiblingIndex(best.GetSiblingIndex());

            // The line reads as engraved on the ribbon rather than floating.
            if (best.TryGetComponent(out TMP_Text text)) text.color = new Color(0.42f, 0.28f, 0.14f);

            return 1;
        }

        // ----------------------------------------------------------- helpers

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }

            return null;
        }

        static void Missing(string name) =>
            Debug.LogWarning($"Sheep Circle: {ArtDir}/{name}.png missing, skipped.");

        static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        /// <summary>UI sprites want the canvas default of 100 pixels per unit, not
        /// the one-world-unit setting the board sprites use: tiled images size
        /// their repeat from it, so the wrong value gives comically large tiles.
        /// Tiling also needs a full-rect mesh.</summary>
        static void ImportUiArt()
        {
            string[] names =
            {
                "bunting", "ribbon_banner", "cloud", "sun",
                "butterfly", "grass_strip", "icon_settings",
            };

            foreach (string name in names)
            {
                string path = $"{ArtDir}/{name}.png";
                if (!File.Exists(path)) continue;
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                if (importer.textureType == TextureImporterType.Sprite
                    && Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                    && importer.alphaIsTransparency
                    && !importer.mipmapEnabled)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }
        }
    }
}
